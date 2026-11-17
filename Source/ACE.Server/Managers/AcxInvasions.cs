using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading;

using log4net;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics.Common;
using ACE.Server.WorldObjects;

using Landblock = ACE.Server.Entity.Landblock;
using Position = ACE.Entity.Position;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: elite town invasions (JJ 2026-11-13; they replace the planned elite dungeon bosses). Every
    /// acx_invasion_min_minutes to acx_invasion_max_minutes, when at least acx_invasion_min_players are online, a random town
    /// from Content/invasions/towns.json gets a world-wide warning, and acx_invasion_warning_minutes later its invader appears
    /// as an elite (Creature_AcxElite.cs) 15-40 m from the town's portal drop. It fights anyone in town; normal death rules apply.
    /// Its corpse is open to everyone for acx_invasion_corpse_minutes and carries acx_invasion_corpse_marks ACX Marks, one per
    /// player (Corpse_AcxElite.cs, AcxMarks.cs); it leaves after acx_invasion_duration_minutes otherwise.
    /// Three world-wide lines per invasion (warning, attack, end), nothing per hit or tick; /invasion shows the current one.
    /// The same town list tells Corpse_AcxVitae whether a headstone is in a town (those decay).
    /// </summary>
    public static class AcxInvasions
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public class Town
        {
            public string Name { get; set; }
            public int Tier { get; set; }
            public string Cell { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public uint Invader { get; set; }
            public string InvaderName { get; set; }
            public int InvaderLevel { get; set; }
            public float Radius { get; set; } = 100;
            public bool Invasions { get; set; } = true;

            /// <summary>
            /// The town's own elite strength, on top of the elite settings (JJ 2026-11-16: "per town settings"; each town
            /// invades with a different creature, so one global setting can't make every elite a two-player fight)
            /// </summary>
            public float HealthScale { get; set; } = 1;
            public float DamageScale { get; set; } = 1;

            public Position Centre => new Position(Convert.ToUInt32(Cell, 16), X, Y, Z, 0, 0, 0, 1);
        }

        /// <summary>
        /// The town's healthScale and damageScale (towns.json) on an invader just made elite. Public for the /acxsurvival bench.
        /// </summary>
        public static void ApplyTownStrength(Creature creature, Town t)
        {
            if (t.HealthScale > 0 && t.HealthScale != 1)
            {
                var max = (long)creature.Health.MaxValue;
                var change = (long)Math.Round(max * t.HealthScale) - max;
                creature.Health.StartingValue = (uint)Math.Clamp(creature.Health.StartingValue + change, 1, uint.MaxValue);
                creature.Health.Current = creature.Health.MaxValue;
            }

            if (t.DamageScale > 0)
                creature.AcxEliteDamageScale = t.DamageScale;
        }

        private class TownFile
        {
            public List<Town> Towns { get; set; } = new();
        }

        private enum Phase { Idle, Warning, Active }

        private static List<Town> towns;
        private static readonly object loadLock = new();
        private static Timer timer;

        private static Phase phase = Phase.Idle;
        private static Town town;
        private static DateTime nextInvasion;
        private static DateTime phaseEnds;
        private static Creature invader;
        private static Landblock heldLandblock;
        private static bool heldWasPermaloaded;

        private static string TownFilePath => Path.Combine(PropertyManager.GetString("content_folder").Item, "invasions", "towns.json");

        public static List<Town> Towns
        {
            get
            {
                if (towns == null)
                    Reload(out _);
                return towns;
            }
        }

        public static bool Reload(out string message)
        {
            lock (loadLock)
            {
                try
                {
                    var file = JsonSerializer.Deserialize<TownFile>(File.ReadAllText(TownFilePath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                    towns = file.Towns;
                    message = $"Invasion towns loaded: {towns.Count} ({towns.Count(t => t.Invasions)} can be invaded) from {TownFilePath}";
                    log.Info(message);
                    return true;
                }
                catch (Exception e)
                {
                    towns ??= new List<Town>();
                    message = $"Invasion towns could not be loaded from {TownFilePath}: {e.Message}";
                    log.Error(message);
                    return false;
                }
            }
        }

        public static void Start()
        {
            if (timer != null)
                return;

            ScheduleNext();
            timer = new Timer(_ => WorldManager.EnqueueAction(new ActionEventDelegate(Tick)), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        private static void ScheduleNext()
        {
            var min = PropertyManager.GetLong("acx_invasion_min_minutes").Item;
            var max = Math.Max(min, PropertyManager.GetLong("acx_invasion_max_minutes").Item);
            nextInvasion = DateTime.UtcNow.AddMinutes(ThreadSafeRandom.Next((int)min, (int)max));
        }

        /// <summary>
        /// The town a position is in, or null. Outdoors, or inside a town building; never in a dungeon.
        /// </summary>
        public static Town TownAt(Position pos)
        {
            if (pos == null)
                return null;
            if (pos.Indoors && !(LScape.get_landcell(pos.Cell) is EnvCell { SeenOutside: true }))
                return null;

            var global = pos.ToGlobal();
            foreach (var t in Towns)
            {
                var centre = t.Centre.ToGlobal();
                if (Vector2.DistanceSquared(new Vector2(global.X, global.Y), new Vector2(centre.X, centre.Y)) <= t.Radius * t.Radius)
                    return t;
            }
            return null;
        }

        /// <summary>
        /// Every 30 seconds on the world thread
        /// </summary>
        private static void Tick()
        {
            try
            {
                var now = DateTime.UtcNow;
                switch (phase)
                {
                    case Phase.Idle:
                        if (now < nextInvasion || !PropertyManager.GetBool("acx_invasions_enabled").Item)
                            return;
                        if (PlayerManager.GetOnlineCount() < PropertyManager.GetLong("acx_invasion_min_players").Item)
                        {
                            nextInvasion = now.AddMinutes(5);   // try again soon after someone logs in
                            return;
                        }
                        var candidates = Towns.Where(t => t.Invasions && t.Invader != 0).ToList();
                        if (candidates.Count == 0)
                        {
                            ScheduleNext();
                            return;
                        }
                        Warn(candidates[ThreadSafeRandom.Next(0, candidates.Count - 1)], PropertyManager.GetLong("acx_invasion_warning_minutes").Item);
                        break;

                    case Phase.Warning:
                        if (now >= phaseEnds)
                            Attack();
                        break;

                    case Phase.Active:
                        if (invader == null || invader.IsDestroyed || invader.CurrentLandblock == null)
                            End();   // lost with its landblock: nothing to announce
                        else if (now >= phaseEnds && !invader.IsDead)
                            Withdraw();
                        break;
                }
            }
            catch (Exception e)
            {
                log.Error($"AcxInvasions.Tick: {e}");
            }
        }

        private static void Broadcast(string message) => PlayerManager.BroadcastToAll(new GameMessageSystemChat(message, ChatMessageType.WorldBroadcast));

        private static void Warn(Town t, long minutes)
        {
            town = t;
            phase = Phase.Warning;
            phaseEnds = DateTime.UtcNow.AddMinutes(minutes);
            var when = minutes <= 0 ? "It attacks now." : $"You have {minutes} minute{(minutes == 1 ? "" : "s")} to prepare.";   // JJ 2026-11-14
            Broadcast($"Scouts report an elite {t.InvaderName} gathering outside {t.Name}. {when}");
        }

        private static void Attack()
        {
            var created = Spawn(town, out var error);
            if (created == null)
            {
                log.Warn($"AcxInvasions: {town.Name}: {error}");
                Broadcast($"The elite {town.InvaderName} turned back before reaching {town.Name}.");
                End();
                return;
            }

            invader = created;
            phase = Phase.Active;
            phaseEnds = DateTime.UtcNow.AddMinutes(PropertyManager.GetLong("acx_invasion_duration_minutes").Item);
            Broadcast($"{invader.Name} attacks {town.Name}! {invader.Location.GetMapCoordStr()}");
        }

        private static Creature Spawn(Town t, out string error)
        {
            error = null;
            var centre = t.Centre;
            var centreGlobal = centre.ToGlobal();

            // 40 tries at open ground 15-40 m out; towns without any (Linvak Tukal's platforms) get the portal drop itself
            for (var attempt = 0; attempt <= 40; attempt++)
            {
                var angle = (float)ThreadSafeRandom.Next(0.0f, 2 * MathF.PI);
                Position pos;
                if (attempt < 40)
                {
                    var distance = (float)ThreadSafeRandom.Next(15.0f, 40.0f);
                    pos = centre.FromGlobal(centreGlobal + new Vector3(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance, 0));
                    if (pos.Indoors)
                        continue;
                    pos.PositionZ = pos.GetTerrainZ();
                    if (!pos.IsWalkable())
                        continue;
                    if (LScape.get_landcell(pos.Cell) is not SortCell cell || cell.has_building() || cell.WaterType == LandDefs.WaterType.EntirelyWater)
                        continue;
                }
                else
                {
                    pos = new Position(centre);
                    log.Warn($"AcxInvasions: {t.Name}: no open ground near the portal drop, so the invader stands on it");
                }

                if (WorldObjectFactory.CreateNewWorldObject(t.Invader) is not Creature creature)
                {
                    error = $"weenie {t.Invader} is not a creature";
                    return null;
                }

                creature.Location = new Position(pos);
                creature.Location.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle + MathF.PI / 2);   // facing the town
                creature.SetPosition(PositionType.Home, new Position(creature.Location));
                creature.AcxMakeElite();
                ApplyTownStrength(creature, t);
                creature.AcxEliteLootTier = t.Tier;
                creature.Attackable = true;
                creature.HomeRadius = t.Radius * 1.5f;

                Hold(creature.Location.LandblockId);
                if (creature.EnterWorld())
                    return creature;

                Release();
                creature.Destroy();
            }

            error ??= "no open ground found near the portal drop";
            return null;
        }

        /// <summary>
        /// Keeps the invaded landblock loaded while nobody is there, so the invader isn't unloaded with it
        /// </summary>
        private static void Hold(LandblockId id)
        {
            heldLandblock = LandblockManager.GetLandblock(id, false);
            heldWasPermaloaded = heldLandblock.Permaload;
            heldLandblock.Permaload = true;
        }

        private static void Release()
        {
            if (heldLandblock != null && !heldWasPermaloaded)
                heldLandblock.Permaload = false;
            heldLandblock = null;
        }

        private static void Withdraw()
        {
            var name = invader.Name;
            invader.FadeOutAndDestroy();
            Broadcast($"{name} withdrew from {town.Name}.");
            End();
        }

        private static void End()
        {
            Release();
            invader = null;
            town = null;
            phase = Phase.Idle;
            ScheduleNext();
        }

        /// <summary>
        /// Called from Creature.OnDeath. The corpse (Corpse_AcxElite.cs) carries the reward: anyone who reaches it before it
        /// rots may take one ACX Mark.
        /// </summary>
        public static void OnCreatureDeath(Creature creature)
        {
            if (phase != Phase.Active || creature != invader)
                return;

            try
            {
                var killers = creature.DamageHistory.TotalDamage.Values
                    .Select(e => e.TryGetAttacker() as Player ?? (e.PetOwner != null ? e.TryGetPetOwner() : null))
                    .Where(p => p != null).Distinct().Take(6).Select(p => p.Name).ToList();
                var names = killers.Count == 0 ? "" : $" by {string.Join(", ", killers.Take(5))}{(killers.Count > 5 ? " and others" : "")}";
                Broadcast($"{town.Name} is safe: {creature.Name} was slain{names}.");
            }
            catch (Exception e)
            {
                log.Error($"AcxInvasions.OnCreatureDeath: {e}");
            }
            End();
        }

        /// <summary>
        /// Called from the elite heartbeat: an idle invader wakes for anyone within acx_invasion_awareness metres,
        /// even someone standing still at a vendor
        /// </summary>
        public static void OnInvaderHeartbeat(Creature creature)
        {
            if (creature != invader || creature.IsAwake || creature.PhysicsObj == null)
                return;

            var range = PropertyManager.GetDouble("acx_invasion_awareness").Item;
            var target = creature.PhysicsObj.ObjMaint.GetKnownPlayersValuesAsPlayer()
                .Where(p => p.Attackable && !p.Teleporting && !p.IsDead && !p.IsAcxGhost && p.Location != null && creature.Location.DistanceTo(p.Location) <= range)
                .OrderBy(p => creature.Location.DistanceTo(p.Location))
                .FirstOrDefault();
            target?.AlertMonster(creature);
        }

        /// <summary>
        /// For /invasion
        /// </summary>
        public static string Status(bool admin)
        {
            var sb = new StringBuilder();
            switch (phase)
            {
                case Phase.Warning:
                    sb.Append($"An elite {town.InvaderName} is gathering outside {town.Name}. It attacks in {Minutes(phaseEnds)}.");
                    break;
                case Phase.Active:
                    var health = invader.Health.MaxValue > 0 ? 100.0 * invader.Health.Current / invader.Health.MaxValue : 0;
                    sb.Append($"{invader.Name} is attacking {town.Name} at {invader.Location.GetMapCoordStr()}, {health:0}% health. It withdraws in {Minutes(phaseEnds)}. {invader.AcxEliteDescription()}");
                    if (admin)
                        sb.Append($" Health {invader.Health.Current:N0}/{invader.Health.MaxValue:N0}, level {invader.Level}, {invader.XpOverride:N0} XP.");
                    break;
                default:
                    sb.Append("No town is under attack.");
                    break;
            }
            if (admin)
            {
                sb.Append(PropertyManager.GetBool("acx_invasions_enabled").Item ? "" : " Invasions are off (acx_invasions_enabled).");
                if (phase == Phase.Idle)
                    sb.Append($" Next invasion roll in {Minutes(nextInvasion)}.");
            }
            return sb.ToString();
        }

        private static string Minutes(DateTime when)
        {
            var minutes = Math.Max(0, (int)Math.Ceiling((when - DateTime.UtcNow).TotalMinutes));
            return $"{minutes} minute{(minutes == 1 ? "" : "s")}";
        }

        /// <summary>
        /// /acxinvasion start: warns a town (or a random one) now; warning minutes 0 attacks straight away
        /// </summary>
        public static string AdminStart(string townName, long? warningMinutes)
        {
            if (phase != Phase.Idle)
                return $"An invasion is already under way. {Status(true)}";

            Town t;
            if (string.IsNullOrEmpty(townName))
            {
                var candidates = Towns.Where(c => c.Invasions && c.Invader != 0).ToList();
                if (candidates.Count == 0)
                    return "No towns can be invaded.";
                t = candidates[ThreadSafeRandom.Next(0, candidates.Count - 1)];
            }
            else
            {
                t = Towns.FirstOrDefault(c => c.Name.Equals(townName, StringComparison.OrdinalIgnoreCase))
                    ?? Towns.FirstOrDefault(c => c.Name.StartsWith(townName, StringComparison.OrdinalIgnoreCase));
                if (t == null)
                    return $"No town called {townName}. /acxinvasion list shows them.";
            }

            Warn(t, warningMinutes ?? PropertyManager.GetLong("acx_invasion_warning_minutes").Item);
            if (phaseEnds <= DateTime.UtcNow)
                Attack();
            return Status(true);
        }

        public static string AdminStop()
        {
            switch (phase)
            {
                case Phase.Warning:
                    Broadcast($"The elite {town.InvaderName} turned back before reaching {town.Name}.");
                    End();
                    return "Invasion called off.";
                case Phase.Active:
                    Withdraw();
                    return "Invader removed.";
                default:
                    return "No invasion is under way.";
            }
        }

        /// <summary>
        /// /acxinvasion kill: slays the invader, for testing its corpse
        /// </summary>
        public static string AdminKill()
        {
            if (phase != Phase.Active || invader == null)
                return "No invader to kill.";
            invader.Smite(invader);
            return "Invader slain.";
        }

        public static string AdminNext(int minutes)
        {
            nextInvasion = DateTime.UtcNow.AddMinutes(minutes);
            return $"Next invasion roll in {minutes} minutes.";
        }

        public static string List()
        {
            var sb = new StringBuilder($"{Towns.Count} towns:\n");
            foreach (var group in Towns.GroupBy(t => t.Tier).OrderBy(g => g.Key))
                sb.Append($"Tier {group.Key}: {string.Join(", ", group.Select(t => $"{t.Name} ({t.InvaderName} {t.InvaderLevel}{(t.Invasions ? "" : ", off")})"))}\n");
            return sb.ToString().TrimEnd();
        }
    }
}
