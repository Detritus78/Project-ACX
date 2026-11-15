using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ACE.Common;
using ACE.Common.Extensions;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: T.A.R. camps, "Type, Area, Rest" (2026-11-13, ported from ClassicACE's CampManager). Every kill counts
    /// on three camps: the creature's Type, the Area (the dungeon landblock, or a 16x16-landblock square outdoors) and
    /// Rest (every kill). Each camp pays a third of the kill experience times (1 - count / its max), so farming one type in
    /// one spot without a break earns less and less. A camp starts recovering 2 minutes after it was last used: Type, Area
    /// and turn-in camps 1 per 5 minutes, Rest 1 per 3 seconds, online or off. A kill counts 1 + 1 per full 1,000 health
    /// the creature lost. Items handed to NPCs count on a camp per item (10 deep) that scales the items given back.
    /// Counts live on the character in AcxCampRegistry (string 30012), "key:count:last;...", keys R (rest), T (creature
    /// type), A (outdoor square), D (dungeon landblock), I (turn-in weenie); camps that have fully recovered are dropped.
    /// All numbers are settings (acx_tar_*); /tar shows your multipliers.
    /// </summary>
    public static class AcxCampManager
    {
        private class Camp
        {
            public long Count;
            public long LastDecay;   // unix seconds of the last use, or of the last recovery step
        }

        private const string RestKey = "R";

        // ClassicACE caps a single kill at 100 interactions per camp
        private const long MaxInteractionsPerKill = 100;

        // ClassicACE's stackable trophy rewards start at 120%
        private const double TrophyMultiplierMax = 1.2;

        private static bool Enabled => PropertyManager.GetBool("acx_tar_enabled").Item;
        private static bool TurnInsEnabled => Enabled && PropertyManager.GetBool("acx_tar_turnin_enabled").Item;

        private static long Now => (long)Time.GetUnixTime();

        private static string TypeKey(CreatureType type) => $"T{(int)type}";
        private static string TurnInKey(uint wcid) => $"I{wcid}";

        /// <summary>
        /// The Area camp the player is standing in: the whole landblock in a dungeon, else the 16x16-landblock square
        /// </summary>
        private static string AreaKey(Player player)
        {
            var landblock = player.CurrentLandblock;
            if (landblock == null)
                return null;

            // ClassicACE's InDungeon: a dungeon landblock, or indoors on a landblock that has a dungeon
            if (landblock.IsDungeon || (landblock.HasDungeon && player.Location != null && player.Location.Indoors))
                return $"D{landblock.Id.Raw >> 16:X4}";

            return $"A{landblock.Id.LandblockX >> 4:X}{landblock.Id.LandblockY >> 4:X}";
        }

        private static long MaxInteractions(string key) => key[0] switch
        {
            'R' => PropertyManager.GetLong("acx_tar_rest_interactions").Item,
            'T' => PropertyManager.GetLong("acx_tar_type_interactions").Item,
            'I' => PropertyManager.GetLong("acx_tar_turnin_interactions").Item,
            _ => PropertyManager.GetLong("acx_tar_area_interactions").Item,
        };

        private static double DecaySeconds(string key) => PropertyManager.GetDouble(key == RestKey ? "acx_tar_rest_decay_seconds" : "acx_tar_decay_seconds").Item;

        private static double DecayDelay => PropertyManager.GetDouble("acx_tar_decay_delay").Item;

        /// <summary>
        /// How many interactions the camp has recovered since LastDecay
        /// </summary>
        private static long Recovered(string key, Camp camp, long now)
        {
            var seconds = now - camp.LastDecay;
            if (seconds < DecayDelay)
                return 0;

            return (long)Math.Floor((seconds - DecayDelay) / Math.Max(DecaySeconds(key), 0.001));
        }

        private static Dictionary<string, Camp> Load(Player player)
        {
            var camps = new Dictionary<string, Camp>();
            foreach (var entry in (player.GetProperty(PropertyString.AcxCampRegistry) ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split(':');
                if (parts.Length == 3 && parts[0].Length > 0 && long.TryParse(parts[1], out var count) && long.TryParse(parts[2], out var last))
                    camps[parts[0]] = new Camp { Count = count, LastDecay = last };
            }
            return camps;
        }

        private static void Save(Player player, Dictionary<string, Camp> camps, long now)
        {
            // a camp that has recovered fully pays 100% again, the same as having no entry
            var kept = camps.Where(c => c.Value.Count > Recovered(c.Key, c.Value, now)).ToList();

            if (kept.Count == 0)
                player.RemoveProperty(PropertyString.AcxCampRegistry);
            else
                player.SetProperty(PropertyString.AcxCampRegistry, string.Join(";", kept.Select(c => $"{c.Key}:{c.Value.Count}:{c.Value.LastDecay}")));
        }

        private static Camp GetOrCreate(Dictionary<string, Camp> camps, string key, long now)
        {
            if (!camps.TryGetValue(key, out var camp))
            {
                camp = new Camp { Count = 0, LastDecay = now };
                camps[key] = camp;
            }
            return camp;
        }

        /// <summary>
        /// ClassicACE's CheckDecay: nothing recovers until the delay has passed since LastDecay (and a use inside the delay
        /// restarts it); after that, whole steps come off the count and LastDecay restarts from now
        /// </summary>
        private static void Decay(string key, Camp camp, long now, bool isInteraction)
        {
            if (now - camp.LastDecay < DecayDelay)
            {
                if (isInteraction)
                    camp.LastDecay = now;
                return;
            }

            var recovered = Recovered(key, camp, now);
            if (recovered > 0)
            {
                camp.LastDecay = now;
                camp.Count = Math.Max(camp.Count - recovered, 0);
            }
        }

        private static double Bonus(string key, long count)
        {
            var max = Math.Max(MaxInteractions(key), 1);
            return 1.0 - (double)Math.Min(count, max) / max;
        }

        /// <summary>
        /// ClassicACE's HandleCampInteraction for one camp: recover, read the bonus, then count this use
        /// </summary>
        private static double Interact(Dictionary<string, Camp> camps, string key, long amount, long now)
        {
            var camp = GetOrCreate(camps, key, now);
            Decay(key, camp, now, true);

            var bonus = Bonus(key, camp.Count);

            if (amount > 0)
            {
                camp.LastDecay = now;
                camp.Count = Math.Min(camp.Count + amount, MaxInteractions(key));
            }
            return bonus;
        }

        /// <summary>
        /// Called from Player.EarnXP before any modifier: kill experience is paid a third per camp, at that camp's bonus
        /// </summary>
        public static long OnKillXp(Player player, long amount, XpType xpType, Creature killed)
        {
            if (xpType != XpType.Kill || killed == null || !Enabled)
                return amount;

            // an elite's huge health would fill the camps in one kill
            if (killed.IsAcxElite)
                return amount;

            // ClassicACE leaves kills of creatures without a type alone
            var type = killed.CreatureType ?? CreatureType.Invalid;
            if (type == CreatureType.Invalid)
                return amount;

            var healthPer = Math.Max(PropertyManager.GetLong("acx_tar_health_per_interaction").Item, 1);
            var interactions = Math.Clamp((long)Math.Floor(1 + killed.DamageHistory.TotalHealth / healthPer), 1, MaxInteractionsPerKill);

            var now = Now;
            var camps = Load(player);

            var typeBonus = Math.Round(Interact(camps, TypeKey(type), interactions, now), 2);
            var areaKey = AreaKey(player);
            var areaBonus = areaKey != null ? Math.Round(Interact(camps, areaKey, interactions, now), 2) : 1.0;
            var restBonus = Math.Round(Interact(camps, RestKey, interactions, now), 2);

            Save(player, camps, now);

            if ((typeBonus < 1 || areaBonus < 1 || restBonus < 1) && PropertyManager.GetBool("acx_tar_kill_message").Item)
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"T: {typeBonus * 100:0}% A: {areaBonus * 100:0}% R: {restBonus * 100:0}%", ChatMessageType.Broadcast));

            var third = amount / 3.0;
            return (long)Math.Round(third * typeBonus + third * areaBonus + third * restBonus);
        }

        /// <summary>
        /// Called from Player.HandleActionGiveObjectRequest when an NPC accepts an item: counts the turn-in and returns its
        /// bonus from before this one, which the NPC's emotes scale their rewards by
        /// </summary>
        /// <summary>
        /// The camp bonus for turn-ins the camps leave alone (JJ 2026-11-14):
        ///  * the gambling halls' staff (token sellers, gamesmasters and shopkeepers, 9492-9506): tokens are bought, so
        ///    gambling is never worn down ("the Tar needs to be removed from gambling");
        ///  * the trophy buyers who pay experience for monster parts: every Collector (Wing, Jewel, Stone, Black Hill...),
        ///    Leather and Ivory Crafter, Trophy Smith, and the hide buyers Gillian the Poacher (8654) and Yi Yo-Jin (5898)
        ///    ("I want collectors wing collectors leather people ivory and other crafters that give xp for items exempt too").
        /// </summary>
        public const float Exempt = -1f;

        private static bool IsExemptNpc(WorldObject npc)
        {
            if (npc == null)
                return false;
            if (npc.WeenieClassId >= 9492 && npc.WeenieClassId <= 9506 || npc.WeenieClassId is 8654 or 5898)
                return true;
            var name = npc.Name ?? "";
            return name.Contains("Collector") || name.Contains("Crafter") || name.Contains("Trophy Smith");
        }

        public static float OnTurnIn(Player player, WorldObject item, WorldObject npc)
        {
            if (IsExemptNpc(npc))
                return Exempt;

            if (!TurnInsEnabled || item == null)
                return 1.0f;

            var now = Now;
            var camps = Load(player);
            var bonus = Interact(camps, TurnInKey(item.WeenieClassId), 1, now);
            Save(player, camps, now);

            return (float)bonus;
        }

        /// <summary>
        /// ClassicACE's CurrentTrophyCampMultiplier: 120% down to 20% with the trophy's turn-in camp
        /// </summary>
        private static double TrophyMultiplier(Player player, uint trophyWcid)
        {
            if (trophyWcid == 0)
                return 1.0;

            var now = Now;
            var camps = Load(player);
            var key = TurnInKey(trophyWcid);
            var camp = GetOrCreate(camps, key, now);
            Decay(key, camp, now, true);
            Save(player, camps, now);

            var max = Math.Max(MaxInteractions(key), 1);
            return Math.Min(TrophyMultiplierMax - (double)Math.Min(camp.Count, max) / max, TrophyMultiplierMax);
        }

        /// <summary>
        /// Called from EmoteManager's Give emote. Scales the stack an NPC hands back for a turn-in; returns false when
        /// nothing is left to give.
        /// </summary>
        /// <param name="emoteSet">the set this Give emote is in</param>
        /// <param name="rootEmoteSet">the set that started the emote chain</param>
        /// <param name="campBonus">the turn-in bonus OnTurnIn returned for the item that started the chain</param>
        public static bool ScaleGiveReward(Player player, PropertiesEmote emoteSet, PropertiesEmoteAction emote, PropertiesEmote rootEmoteSet, float campBonus, ref int stackSize)
        {
            if (!TurnInsEnabled || emote.WeenieClassId == null || campBonus == Exempt)
                return true;

            if (stackSize > 1 && PropertyManager.GetBool("stackable_trophy_rewards_use_tar").Item && emoteSet?.WeenieClassId != null)
                stackSize = (int)Math.Round(stackSize * TrophyMultiplier(player, emoteSet.WeenieClassId.Value));

            if (stackSize < 1)
                stackSize = 1;

            // rewards for a turn-in, unless the NPC is just handing the same item back
            if (rootEmoteSet == null || rootEmoteSet.Category != EmoteCategory.Give || rootEmoteSet.WeenieClassId == emote.WeenieClassId)
                return true;

            var roundedBonus = Math.Round(campBonus, 2);
            stackSize = (int)Math.Ceiling(stackSize * roundedBonus);

            if (stackSize == 0)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your item reward amount has been reduced to zero! T: {roundedBonus * 100:0}%", ChatMessageType.Broadcast));
                return false;
            }
            return true;
        }

        /// <summary>
        /// Called from EmoteManager's AwardXP and AwardNoShareXP: with acx_tar_turnin_xp, quest experience for a turn-in
        /// scales with the turn-in bonus too
        /// </summary>
        public static long ScaleTurnInXp(Player player, long amount, PropertiesEmote rootEmoteSet, float campBonus)
        {
            if (amount <= 0 || !TurnInsEnabled || !PropertyManager.GetBool("acx_tar_turnin_xp").Item || campBonus == Exempt)
                return amount;

            if (rootEmoteSet == null || rootEmoteSet.Category != EmoteCategory.Give || (rootEmoteSet.WeenieClassId ?? 0) == 0)
                return amount;

            var roundedBonus = Math.Round(campBonus, 2);
            if (roundedBonus >= 1)
                return amount;

            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your experience reward has been reduced. T: {roundedBonus * 100:0}%", ChatMessageType.Broadcast));
            return (long)Math.Round(amount * roundedBonus);
        }

        /// <summary>
        /// The multiplier and full recovery time of a camp right now. Read-only: unlike ClassicACE's /tar, looking
        /// doesn't restart a camp's recovery delay.
        /// </summary>
        private static (double bonus, TimeSpan recovery) View(Dictionary<string, Camp> camps, string key, long now)
        {
            if (!camps.TryGetValue(key, out var camp))
                return (1.0, TimeSpan.Zero);

            var count = Math.Max(camp.Count - Recovered(key, camp, now), 0);
            var recoveredAt = camp.LastDecay + DecayDelay + camp.Count * DecaySeconds(key);

            return (Bonus(key, count), TimeSpan.FromSeconds(Math.Ceiling(Math.Max(recoveredAt - now, 0))));
        }

        private static string Line(string label, (double bonus, TimeSpan recovery) view)
        {
            var line = $"   {label}: {view.bonus * 100:0}%";
            if (view.bonus < 1)
                line += $" - Estimated recovery time: {view.recovery.GetFriendlyString()}";
            return line;
        }

        /// <summary>
        /// /tar: the player's T.A.R. multipliers, for one creature type or for every type that is down
        /// </summary>
        public static string Report(Player player, CreatureType? type)
        {
            if (!Enabled)
                return "T.A.R. is turned off on this server.";

            var now = Now;
            var camps = Load(player);

            var sb = new StringBuilder("Current T.A.R. experience multipliers:");

            if (type != null)
                sb.Append('\n').Append(Line($"Type({type})", View(camps, TypeKey(type.Value), now)));
            else
            {
                foreach (var (typeName, view) in camps.Keys.Where(k => k[0] == 'T' && int.TryParse(k.AsSpan(1), out _))
                    .Select(k => (name: ((CreatureType)int.Parse(k.AsSpan(1))).ToString(), view: View(camps, k, now)))
                    .Where(t => t.view.bonus < 1)
                    .OrderBy(t => t.view.bonus))
                    sb.Append('\n').Append(Line($"Type({typeName})", view));
            }

            var areaKey = AreaKey(player);
            sb.Append('\n').Append(Line("Area", areaKey != null ? View(camps, areaKey, now) : (1.0, TimeSpan.Zero)));
            sb.Append('\n').Append(Line("Rest", View(camps, RestKey, now)));

            if (type == null)
                sb.Append("\nUse /tar <creature type> for one type, e.g. /tar olthoi.");

            return sb.ToString();
        }

        /// <summary>
        /// /acxtar: every camp a player has, raw
        /// </summary>
        public static string Dump(Player player)
        {
            var now = Now;
            var camps = Load(player);
            if (camps.Count == 0)
                return $"{player.Name} has no T.A.R. camps.";

            var sb = new StringBuilder($"{player.Name}'s T.A.R. camps ({(player.GetProperty(PropertyString.AcxCampRegistry) ?? "").Length} characters stored):");
            foreach (var (key, camp) in camps.OrderBy(c => c.Key))
            {
                var name = key[0] switch
                {
                    'R' => "Rest",
                    'T' when int.TryParse(key.AsSpan(1), out var t) => $"Type {(CreatureType)t}",
                    'A' => $"Area square {key.Substring(1)}",
                    'D' => $"Dungeon {key.Substring(1)}",
                    'I' => $"Turn-in wcid {key.Substring(1)}",
                    _ => key,
                };
                var view = View(camps, key, now);
                var full = view.recovery > TimeSpan.Zero ? $"full in {view.recovery.GetFriendlyString()}" : "recovered";
                sb.Append($"\n   {name}: {camp.Count}/{MaxInteractions(key)} stored, {view.bonus * 100:0}% now, {full}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// /acxtar reset: clears every camp
        /// </summary>
        public static void Reset(Player player) => player.RemoveProperty(PropertyString.AcxCampRegistry);
    }
}
