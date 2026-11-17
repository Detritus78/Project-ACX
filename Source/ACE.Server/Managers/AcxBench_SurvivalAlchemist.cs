using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Mutations;
using ACE.Server.Factories;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.Physics.Animation;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: the Alchemist on the survival bench (JJ 2026-11-16: "Lets put it in and bench it and see how that goes. It
    /// will need tweaking."). The bomb's blast and debuff as Player.AcxBombImpact does them, and pack fights (one player against
    /// several of a monster at once, all on the player) so the blast's splash counts: /acxsurvival pack.
    /// In a pack every other living monster stands inside the blast (they crowd the player); cleaving melee weapons strike their
    /// CleaveTargets of them; bows and war bolts stay single-target (mages would use blasts and rings, which the bench doesn't cast).
    /// </summary>
    public static partial class AcxBench
    {
        /// <summary>
        /// A bomb landing (Player.AcxBombImpact): up to acx_alchemist_splash_targets other living monsters of the pack (it stands
        /// together, so any of them is nearest) take acx_alchemist_splash_share of a hit
        /// (its own defense roll), then everything hit takes the bomb's debuff (the thrower's Alchemy against Magic Defense)
        /// </summary>
        private static void BenchBombBlast(Fight f, Player p, WorldObject projectile, Creature target, bool hit)
        {
            var bomb = projectile.ProjectileAmmo ?? projectile;
            var hits = new List<Creature>();
            if (hit && target.Health.Current > 0)
                hits.Add(target);

            if (f.Pack != null)
            {
                var others = AcxBombWcids.SplashTargets(f.Pack.Where(c => c != target && Alive(f, c)));
                var share = AcxBombWcids.SplashShare;
                foreach (var other in others)
                {
                    var se = DamageEvent.CalculateDamage(p, other, projectile);
                    if (!se.HasDamage || se.GeneralFailure)
                        continue;
                    f.BlastHits++;
                    Hurt(f, other, se.Damage * share, p);
                    if (other.Health.Current > 0)
                        hits.Add(other);
                }
            }

            var debuff = AcxBombWcids.GetDebuffSpell(bomb);
            if (debuff == null)
                return;
            foreach (var creature in hits)
            {
                f.DebuffTries++;
                if (p.AcxBombDebuff(creature, debuff.Value, bomb, show: false))
                    f.DebuffsLanded++;
            }
        }

        /// <summary>
        /// A bomb tinked as a player would: the rend of its type (Armor Rending on the Nether bomb, whose rend no gem gives), its
        /// tinks as Obsidian (Potency +0.04 each, RecipeManager_Acx), and transfers of Legendary Blood Thirst, Alchemical Prowess
        /// (the blast's power) and Quickness (the throw)
        /// </summary>
        private static void TinkBombReal(WorldObject bomb)
        {
            var rend = survivalBombImbue switch
            {
                // none by default: a bomb takes no imbue (RecipeManager_AcxPairs); bombimbue= benches what one would do
                "cs" => ImbuedEffectType.CriticalStrike, "cb" => ImbuedEffectType.CripplingBlow, "ar" => ImbuedEffectType.ArmorRending,
                "rend" => bomb.W_DamageType == DamageType.Nether ? ImbuedEffectType.ArmorRending : WorldObject.GetRendDamageType(bomb.W_DamageType),
                _ => ImbuedEffectType.Undef,
            };
            if (ImbueMutations.TryGetValue(rend, out var mutation))
                MutationCache.GetMutation(mutation).TryMutate(bomb);
            for (var i = Math.Max(0, MaxTinks - bomb.NumTimesTinkered); i > 0; i--)
            {
                bomb.DamageMod = (bomb.DamageMod ?? 1.0) + 0.04;
                bomb.NumTimesTinkered++;
            }
            TransferReal(bomb, new[] { LegendaryBloodThirst, LegendaryAlchemicalProwess, LegendaryQuickness }.Take(Math.Max(0, bomb.MaxExtraSpellsCount)));
        }

        private const uint LegendaryAlchemicalProwess = 6040;

        public class PackOptions
        {
            public List<int> Levels = new() { 275 };
            public List<string> Builds = new() { "alchemist", "archer", "mage", "melee", "meleeshield", "twohand" };
            public List<string> Profiles = new() { "veteran" };
            public List<uint> Monsters;      // null: a few end-game kinds
            public int Size = 4;
            public int Fights = 10;
            public double MaxSeconds = 600;
            public int Gear = 3;
            public string BombImbue;         // as the survival bench's bombimbue=
            public bool ThrownShield;        // as the survival bench's shield=1
        }

        // pack monsters: end-game kinds that come in groups (melee crowds and casters)
        private static readonly uint[] PackMonsters = { 51750, 46503, 52616, 51756, 87848, 46511 };   // Rynthid Minion, Spectral Archer, Brier Wasp, Rynthid Slayer, Vermilion War Gromnie, Spectral Bloodmage

        public static string StartPack(PackOptions options, Action<string> reply)
        {
            if (running)
                return "A bench is already running.";
            running = true;
            Task.Run(() =>
            {
                try { reply(RunPack(options)); }
                catch (Exception e) { log.Error($"AcxBench pack failed: {e}"); reply($"The pack bench failed: {e.Message}"); }
                finally { running = false; }
            });
            return $"Pack bench started: {options.Size} monsters at a time, levels {string.Join(",", options.Levels)}, builds {string.Join(",", options.Builds)}. Results go to Logs/acxbench/.";
        }

        /// <summary>
        /// One pack fight: size monsters of one kind, all after the player; won when all are dead before the player is
        /// </summary>
        private static Fight RunPackFight(SurvivalBuild b, int level, SurvivalGear gear, int version, uint monsterWcid, int size, uint petWcid, bool heal, double maxSeconds)
        {
            var p = DressSurvivor(b, level, -1, gear, version, out var loadout);
            var pack = new List<Creature>();
            for (var i = 0; i < size; i++)
            {
                var m = MakeMonster(monsterWcid, false);
                if (m == null)
                    return null;
                pack.Add(m);
            }
            var f = new Fight { Heal = heal, Max = maxSeconds, HealAt = 0.55, P = p, M = pack[0], Pack = pack, Build = b, Gear = loadout };
            if (petWcid != 0)
                f.Pet = MakePet(petWcid);

            if (b.Style == "missile")
            {
                var projectile = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(loadout.Ammo.WeenieClassId), new ObjectGuid(ProjectileGuid));
                projectile.ProjectileSource = p;
                projectile.ProjectileTarget = pack[0];
                projectile.ProjectileLauncher = loadout.Weapon == loadout.Ammo ? null : loadout.Weapon;
                projectile.ProjectileAmmo = loadout.Ammo;
                AcxThrownWcids.CarryRoll(loadout.Ammo, projectile);
                projectile.Location = new Position(p.Location);
                f.Projectile = projectile;
            }
            if (b.Style == "magic")
                (f.CastCycle, f.CastLand) = CastTiming(p, loadout.Bolt);

            Healer Kit(uint wcid) => WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(wcid), new ObjectGuid(SurvivalKitGuid)) as Healer;
            f.HealthKit = Kit(TreatedHealingKit);
            f.StaminaKit = Kit(EternalStaminaKit);
            f.ManaKit = Kit(EternalManaKit);
            var stance = p.CurrentMotionState.Stance;
            f.HealTime = MotionTable.GetAnimationLength(p.MotionTableId, stance, MotionCommand.SkillHealSelf) + MotionTable.GetAnimationLength(p.MotionTableId, stance, MotionCommand.SkillHealSelf, MotionCommand.Ready);
            if (f.HealTime <= 0)
                f.HealTime = MotionTable.GetAnimationLength(p.MotionTableId, MotionStance.NonCombat, MotionCommand.SkillHealSelf) + 0.5;

            f.At(0, () => Act(f, null));
            MobAi first = null;
            foreach (var m in pack)
            {
                var ai = new MobAi { C = m, Target = p };
                first ??= ai;
                f.At(0, () => MobDecide(f, ai, 0));
            }
            if (f.Pet != null)
            {
                var petAi = new MobAi { C = f.Pet, Target = pack[0] };
                f.At(0, () => MobDecide(f, petAi, 0));
            }
            f.At(5.0, () => Heartbeat(f, first));

            var events = 0;
            while (!f.Over && f.Q.TryDequeue(out var action, out var time))
            {
                if (time > maxSeconds)
                    break;
                f.T = time;
                action();
                if (++events > 400000)
                {
                    Unhandled(f, "event cap");
                    break;
                }
            }
            return f;
        }

        private static string RunPack(PackOptions o)
        {
            var started = DateTime.Now;
            survivalGear = o.Gear;
            survivalLootTier = 0;
            survivalBombImbue = o.BombImbue;
            survivalSetOverride = null;
            survivalAetheria = "defense";
            survivalTrinket = null;
            survivalMeleeKind = null;
            var builds = SurvivalBuilds.Where(b => o.Builds.Contains(b.Name)).Select(b => o.ThrownShield ? WithShield(b) : b).ToList();
            var monsters = o.Monsters ?? PackMonsters.ToList();

            var sb = new StringBuilder("Level,Profile,Build,Monster,Wcid,MonsterLevel,PackSize,Fights,WinPct,ClearSecMedian,DieSecMedian,DealtPerSec,PlayerDealtPerSec,Weapon,BlastHitsPerFight,DebuffLandPct\n");
            var summary = new StringBuilder();
            string Sec(double v) => double.IsInfinity(v) || double.IsNaN(v) ? "never" : F(v, "0");

            foreach (var level in o.Levels)
            {
                var gear = RollSurvivalGear(level);
                foreach (var profile in o.Profiles)
                {
                    survivalVeteran = profile == "veteran";

                    // the healing comparison (JJ's target: a Healing bomb about a healing kit's worth per throw, per player in the burst)
                    if (builds.Any(b => b.Name == "alchemist"))
                    {
                        var alchemist = DressSurvivor(SurvivalBuilds.First(b => b.Name == "alchemist"), level, -1, gear, 0, out _);
                        var alchemy = alchemist.GetCreatureSkill(Skill.Alchemy).Current;
                        var bombHeal = alchemy * PropertyManager.GetDouble("acx_alchemist_heal_per_skill").Item;
                        var kit = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(TreatedHealingKit), new ObjectGuid(SurvivalKitGuid)) as Healer;
                        var line = $"L{level} {profile}: Alchemy {alchemy}; Healing bomb {bombHeal:0} health over 12.5 s per player or pet in the burst";
                        foreach (var name in new[] { "alchemist", "melee", "archer" })
                        {
                            var healer = DressSurvivor(SurvivalBuilds.First(b => b.Name == name), level, -1, gear, 0, out _);
                            var healing = healer.GetCreatureSkill(Skill.Healing).Current;
                            var average = healing * (kit?.HealkitMod ?? 1.0) * 0.35 * 1.1;   // Healer.GetHealAmount: 20-50% of skill x kit, 10% crits double
                            line += $"; {name}'s Treated Healing Kit (Healing {healing}) averages {average:0} per use";
                        }
                        summary.AppendLine(line);
                    }

                    foreach (var b in builds)
                    foreach (var wcid in monsters)
                    {
                        if (DatabaseManager.World.GetCachedWeenie(wcid) == null)
                            continue;
                        try
                        {
                            // the weapon version that does best against one of them (a short trial each, as the solo bench picks)
                            var best = 0;
                            var bestDps = -1.0;
                            for (var v = 0; v < WeaponVersions(b, gear); v++)
                            {
                                var trial = RunFight(b, level, -1, gear, v, wcid, false, 0, false, 20);
                                var dps = trial == null ? 0 : trial.DmgByPlayer / Math.Max(1.0, double.IsNaN(trial.DeathTime) ? Math.Min(20, trial.T) : trial.DeathTime);
                                if (dps > bestDps)
                                    (best, bestDps) = (v, dps);
                            }
                            var pet = b.Pet && level >= 180 ? SurvivalPets200[0] : 0u;

                            var clears = new List<double>();
                            var deaths = new List<double>();
                            double wins = 0, dealt = 0, byPlayer = 0, time = 0, blast = 0, tries = 0, landed = 0;
                            var fights = 0;
                            string monsterName = "", weapon = "";
                            var monsterLevel = 0;
                            for (var i = 0; i < o.Fights; i++)
                            {
                                var f = RunPackFight(b, level, gear, best, wcid, o.Size, pet, true, o.MaxSeconds);
                                if (f == null)
                                    continue;
                                fights++;
                                monsterName = f.Pack[0].Name;
                                monsterLevel = f.Pack[0].Level ?? 0;
                                weapon = f.Gear.Name;
                                var clear = double.IsNaN(f.KillTime) ? double.PositiveInfinity : f.KillTime;
                                var death = double.IsNaN(f.DeathTime) ? double.PositiveInfinity : f.DeathTime;
                                clears.Add(clear);
                                deaths.Add(death);
                                if (clear < death)
                                    wins++;
                                dealt += f.DmgByPlayer + f.DmgByPet;
                                byPlayer += f.DmgByPlayer;
                                time += Math.Min(f.T, o.MaxSeconds);
                                blast += f.BlastHits;
                                tries += f.DebuffTries;
                                landed += f.DebuffsLanded;
                            }
                            if (fights == 0)
                                continue;
                            sb.AppendLine(string.Join(",", level, profile, b.Name, Csv(monsterName), wcid, monsterLevel, o.Size, fights, F(wins * 100 / fights, "0"), Sec(MedianOf(clears)), Sec(MedianOf(deaths)),
                                F(time > 0 ? dealt / time : 0, "0.#"), F(time > 0 ? byPlayer / time : 0, "0.#"), Csv(weapon), F(blast / fights, "0.#"), tries > 0 ? F(landed * 100 / tries, "0") : ""));
                            summary.AppendLine($"L{level} {profile} {b.Name} vs {o.Size} x {monsterName}: win {wins * 100 / fights:0}%, clear {Sec(MedianOf(clears))} s, {(time > 0 ? dealt / time : 0):0} dps, {(time > 0 ? byPlayer / time : 0):0} by the player ({weapon})");
                        }
                        catch (Exception e)
                        {
                            log.Warn($"[ACXSURVIVAL] pack L{level} {b.Name} vs {wcid} failed: {e}");
                        }
                    }
                }
            }

            var dir = Path.Combine("Logs", "acxbench");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"acxsurvival-pack{o.Size}-{string.Join("_", o.Levels)}-{started:yyyyMMdd-HHmmss}.csv");
            File.WriteAllText(file, sb.ToString());
            return $"Pack bench done in {(DateTime.Now - started).TotalSeconds:0}s, written to {file}.\n" + summary.ToString().TrimEnd();
        }
    }
}
