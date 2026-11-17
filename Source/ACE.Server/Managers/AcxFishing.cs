using System;
using System.Linq;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: fishing extras (JJ 2026-11-13). Every real cast at a fishing hole (the hole's emotes stamp FishingDelay,
    /// at most once per 5 seconds) has acx_fishing_mob_chance of pulling a water creature out of the hole: a Reedshark,
    /// Niffis, Moarsman or Remoran of about half the fisher's level (acx_fishing_mob_level), never an elite, which leaves
    /// after acx_fishing_mob_minutes if nobody kills it, and may drop a salvage sack, a pearl or an ACX Mark (AddCatchDrops).
    /// The ACX Mark rewards (6 Tackle Master milestones and a 1-in-1,000 turn-in gift, each once a day) and the +50%
    /// pyreal payments are data: acx/Content/invasions/50-fishing.sql.
    /// </summary>
    public static class AcxFishing
    {
        // water creatures that spawn in the world, by level (the lowest of each kind)
        private static readonly (uint Wcid, int Level)[] Creatures =
        {
            (2577, 8), (223, 8), (7984, 14), (221, 15), (222, 15), (7985, 18), (7986, 21), (7180, 30), (4246, 40),
            (7987, 61), (7988, 67), (4247, 80), (24134, 100), (4248, 100), (27856, 115), (7181, 115), (31912, 160),
            (31914, 160), (31915, 160), (31906, 185), (31908, 185), (32033, 185), (32035, 185), (44047, 200), (33737, 200),
            (44049, 220), (39137, 220),
        };

        /// <summary>
        /// The Tackle Master's ACX Mark rewards stamp a quest named with this prefix instead of giving the Mark: one per
        /// milestone (AcxFishMark_Guppy...) and one for the rare turn-in gift (AcxFishMark_Gift), each at most once per
        /// character per 20 hours (the quest rows' min_Delta), so a fishing bot can't farm them (JJ 2026-11-13).
        /// </summary>
        public const string MarkQuestPrefix = "AcxFishMark_";

        public static void TryMark(Player player, string quest)
        {
            if (!player.QuestManager.CanSolve(quest))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("You've already had today's ACX Mark for that; it comes again tomorrow.", ChatMessageType.Broadcast));
                return;
            }
            player.QuestManager.Stamp(quest);
            AcxMarks.Give(player, "fishing", "your catch");
        }

        // salvage sacks and pearls by the fisher's level: under 50, 50-125, over 125
        private static readonly uint[] Sacks = { 900721, 900722, 900723 };
        private static readonly uint[] Pearls = { 900724, 900725, 900726 };

        /// <summary>
        /// Called from Creature.CreateCorpse for a creature pulled from a fishing hole: on top of its own loot, each a separate
        /// chance (JJ 2026-11-13: "a capped chance... so theres a chance all you get is just regular junk loot"):
        /// a salvage sack (acx_fishing_sack_chance), a pearl the Tackle Master buys (acx_fishing_pearl_chance), and an ACX
        /// Mark (acx_fishing_creature_mark_chance) at most once a day for the fisher (AcxFishMark_Creature).
        /// </summary>
        public static void AddCatchDrops(Creature creature, Corpse corpse)
        {
            if (corpse == null)
                return;

            var tier = creature.AcxFisherLevel < 50 ? 0 : creature.AcxFisherLevel <= 125 ? 1 : 2;

            void Drop(uint wcid)
            {
                var item = WorldObjectFactory.CreateNewWorldObject(wcid);
                if (item != null && !corpse.TryAddToInventory(item))
                    item.Destroy();
            }

            if (ThreadSafeRandom.Next(0.0f, 1.0f) < PropertyManager.GetDouble("acx_fishing_sack_chance").Item)
                Drop(Sacks[tier]);
            if (ThreadSafeRandom.Next(0.0f, 1.0f) < PropertyManager.GetDouble("acx_fishing_pearl_chance").Item)
                Drop(Pearls[tier]);
            if (ThreadSafeRandom.Next(0.0f, 1.0f) < PropertyManager.GetDouble("acx_fishing_creature_mark_chance").Item
                && PlayerManager.GetOnlinePlayer(creature.AcxFisherGuid) is Player fisher && fisher.QuestManager.CanSolve(CreatureMarkQuest))
            {
                fisher.QuestManager.Stamp(CreatureMarkQuest);
                var mark = AcxMarks.Create(1);
                if (mark != null && !corpse.TryAddToInventory(mark))
                    mark.Destroy();
            }
        }

        private const string CreatureMarkQuest = MarkQuestPrefix + "Creature";

        public static void OnCast(Player fisher, WorldObject hole)
        {
            if (hole == null || hole.Location == null || ThreadSafeRandom.Next(0.0f, 1.0f) >= PropertyManager.GetDouble("acx_fishing_mob_chance").Item)
                return;

            // about half the fisher's level, so fishing stays safe unbuffed (JJ: "always 50% of the fishers level", never an
            // elite): the creatures within 20% of that, else the two closest to it
            var target = (fisher.Level ?? 1) * PropertyManager.GetDouble("acx_fishing_mob_level").Item;
            var near = Creatures.Where(c => Math.Abs(c.Level - target) <= Math.Max(3, target * 0.2)).ToList();
            if (near.Count == 0)
                near = Creatures.OrderBy(c => Math.Abs(c.Level - target)).Take(2).ToList();
            var pick = near[ThreadSafeRandom.Next(0, near.Count - 1)];

            if (WorldObjectFactory.CreateNewWorldObject(pick.Wcid) is not Creature creature)
                return;

            creature.Location = new Position(hole.Location);
            if (!creature.EnterWorld())
            {
                creature.Location = fisher.Location.InFrontOf(3.0f);
                if (!creature.EnterWorld())
                {
                    creature.Destroy();
                    return;
                }
            }

            creature.AcxFisherGuid = fisher.Guid.Full;
            creature.AcxFisherLevel = fisher.Level ?? 1;

            fisher.Session.Network.EnqueueSend(new GameMessageSystemChat($"Something big takes your bait... a {creature.Name} bursts out of the water!", ChatMessageType.Broadcast));
            fisher.AlertMonster(creature);

            var chain = new ActionChain();
            chain.AddDelaySeconds(PropertyManager.GetLong("acx_fishing_mob_minutes").Item * 60);
            chain.AddAction(creature, () =>
            {
                if (!creature.IsDead && !creature.IsDestroyed)
                    creature.FadeOutAndDestroy();
            });
            chain.EnqueueChain();
        }
    }
}
