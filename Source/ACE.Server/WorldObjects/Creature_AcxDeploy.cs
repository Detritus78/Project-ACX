using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Factories.Tables;
using ACE.Server.Pathfinding;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: procedural chests and traps, ported from ClassicACE (JJ 2026-11-12).
    /// When a monster spawns, it sometimes places a chest beside itself (about 1%, varying by day and area), and in
    /// dungeons sometimes a spell trap with a pressure plate (about 5 to 10%). Chests vanish once emptied, 10 minutes after
    /// first being opened, or after an hour unopened; they don't refill in place (Chest.Reset).
    /// End-of-retail chests and trap only (JJ 2026-11-12): ClassicACE's own chest, corpse and trap objects aren't used.
    /// In dungeons with pathfinding on, chests and traps go at a random spot within 40 m, chests against a wall (as on
    /// ClassicACE); otherwise beside the monster.
    /// Traps last an hour.
    /// </summary>
    partial class Creature
    {
        /// <summary>
        /// Chests by tier 1-6: end-of-retail chests only (JJ 2026-11-12). Retail has no plain chest above tier 4 that a
        /// spawned chest could use (the Sturdy Steel Chest's 7,500 lock has no key), so tiers 5-6 get the tier 5 runed chest.
        /// </summary>
        private static readonly List<uint>[] AcxChests =
        {
            new() { 27243, 27245, 27244, 27242, 1932, 3978 },
            new() { 1915, 3961, 1924, 3970, 1918, 3964, 7493, 1930, 3976, 1921, 3967, 1927, 3973, 1934, 3980, 1937, 3983, 1940, 3986, 1943, 3989, 1949, 3995, 1913, 3959 },
            new() { 1916, 3962, 1925, 3971, 1919, 3965, 7500, 1931, 3977, 1922, 3968, 1928, 3974, 1935, 3981, 1938, 3984, 1941, 3987, 1944, 3990, 1950, 3996 },
            new() { 3960, 2544, 1914, 1923, 3969, 1917, 3963, 7494, 7495, 1929, 3975, 1920, 3966, 1926, 3972, 1933, 3979, 1936, 3982, 1939, 3985, 1942, 3988, 1948, 3994, 7297, 1912, 3958 },
            new() { 22566 },
            new() { 22566 },
        };

        /// <summary>
        /// Outdoor runed chests by tier 1-6 (retail has runed chests for tiers 2-5; tier 1 and 6 use the nearest)
        /// </summary>
        private static readonly List<uint>[] AcxRunedChests =
        {
            new() { 22572 }, new() { 22572, 22568 }, new() { 22576, 22570 }, new() { 22571, 22567 }, new() { 22566 }, new() { 22566 },
        };

        /// <summary>
        /// Monsters that drop a key get the chest that key opens
        /// </summary>
        private static readonly (uint[] Keys, uint[] Chests)[] AcxKeyChests =
        {
            (new uint[] { 23107, 23108 }, new uint[] { 23085, 23086 }),   // Mangled and Twisted Dark Keys: Reinforced Mahogany Chests
            (new uint[] { 30823 }, new uint[] { 30796 }),                  // Broken Black Marrow Key
        };

        private static readonly SpellId[] AcxTrapSpells =
        {
            SpellId.ForceBolt1, SpellId.WhirlingBlade1, SpellId.ShockWave1, SpellId.FlameBolt1, SpellId.FrostBolt1, SpellId.LightningBolt1, SpellId.AcidStream1, SpellId.HarmOther1,
        };

        private const uint AcxMagicTrapWcid = 8548;       // retail Magic trap: casts its spell on whoever activates it
        private const uint AcxPressurePlateWcid = 2131;   // retail Pressure Plate: activates its target when stepped on

        private const double AcxChestChance = 0.0075;
        private const double AcxTrapChance = 0.05;

        private static readonly TimeSpan AcxUnopenedChestLifetime = TimeSpan.FromHours(1);
        private const double AcxOpenedChestLifetime = 600;   // seconds after first opened
        private static readonly TimeSpan AcxTrapLifetime = TimeSpan.FromHours(1);

        public override void OnGeneration(WorldObject generator)
        {
            base.OnGeneration(generator);

            if (this is not Player && (!Attackable || Tolerance != Tolerance.None || this is Pet))
                return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(5);
            actionChain.AddAction(this, AcxDeploy);
            actionChain.EnqueueChain();
        }

        private void AcxDeploy()
        {
            if (Location == null || CurrentLandblock == null || IsDead)
                return;

            // the chance shifts by day and by area, as on ClassicACE
            var seed = DateTime.UtcNow.DayOfYear + (CurrentLandblock.Id.LandblockX << 8 | CurrentLandblock.Id.LandblockY);
            var pseudoRandom = new Random(seed);
            var trapExtra = pseudoRandom.NextDouble();
            var chestExtra = pseudoRandom.NextDouble();

            var inDungeon = CurrentLandblock.IsDungeon;
            var chestChance = AcxChestChance * (1 + chestExtra);

            if (inDungeon && ThreadSafeRandom.Next(0.0f, 1.0f) < AcxTrapChance * (1 + trapExtra))
                AcxDeployTrap();

            var hasChest = false;
            if (!inDungeon && !AcxImmobile && ThreadSafeRandom.Next(0.0f, 1.0f) < chestChance)
                hasChest = AcxDeployChest(AcxKeyChestFor() ?? AcxRunedChests[AcxTier() - 1]);

            if (!hasChest && Location.Indoors && ThreadSafeRandom.Next(0.0f, 1.0f) < chestChance)
                AcxDeployChest(AcxKeyChestFor() ?? AcxChests[AcxTier() - 1]);
        }

        /// <summary>
        /// Loot tier 1-6 from the monster's death treasure
        /// </summary>
        private int AcxTier() => Math.Clamp(DeathTreasure?.Tier ?? 1, 1, 6);

        private bool AcxImmobile => GetProperty(PropertyBool.AiImmobile) ?? false;

        /// <summary>
        /// A random walkable spot within 40 m in this dungeon (against a wall if asked), or null without pathfinding
        /// </summary>
        private Position AcxDungeonSpot(AgentWidth width, bool againstWall)
        {
            if (!Pathfinder.Enabled || !Location.Indoors || !(CurrentLandblock?.IsDungeon ?? false))
                return null;

            var spot = Pathfinder.GetRandomPointWithinCircle(Location, 40, width);
            if (spot != null && againstWall)
                spot = Pathfinder.GetNearestWallPosition(spot, 20, AgentWidth.Narrow, out _, true) ?? spot;
            return spot;
        }

        private List<uint> AcxKeyChestFor()
        {
            var createList = Biota.PropertiesCreateList;
            if (createList == null)
                return null;

            foreach (var (keys, chests) in AcxKeyChests)
                if (createList.Any(c => keys.Contains(c.WeenieClassId)))
                    return chests.ToList();
            return null;
        }

        private bool AcxDeployChest(List<uint> wcids)
        {
            if (wcids == null || wcids.Count == 0)
                return false;

            if (WorldObjectFactory.CreateNewWorldObject(wcids[ThreadSafeRandom.Next(0, wcids.Count - 1)]) is not Chest chest)
                return false;

            var radius = PhysicsObj.GetRadius() + 0.5f;
            chest.Location = AcxDungeonSpot(AgentWidth.Wide, againstWall: true) ?? new Position(Location).InFrontOf(AcxImmobile ? radius : -radius, true);
            chest.Location.LandblockId = new LandblockId(chest.Location.GetCell());

            chest.SetProperty(PropertyBool.AcxProceduralChest, true);
            chest.ChestClearedWhenClosed = true;
            chest.SetProperty(PropertyBool.ChestRegenOnClose, false);
            chest.SetProperty(PropertyFloat.ResetInterval, AcxOpenedChestLifetime);   // Chest.Reset removes it instead

            if (!chest.EnterWorld())
            {
                chest.Destroy();
                return false;
            }

            var lifetime = new ActionChain();
            lifetime.AddDelaySeconds(AcxUnopenedChestLifetime.TotalSeconds);
            lifetime.AddAction(chest, () =>
            {
                if (!chest.IsDestroyed && !chest.IsOpen && chest.UseLockTimestamp == null)
                    chest.FadeOutAndDestroy();
            });
            lifetime.EnqueueChain();
            return true;
        }

        private bool AcxDeployTrap()
        {
            var trap = WorldObjectFactory.CreateNewWorldObject(AcxMagicTrapWcid);
            var plate = WorldObjectFactory.CreateNewWorldObject(AcxPressurePlateWcid);
            if (trap == null || plate == null)
            {
                trap?.Destroy();
                plate?.Destroy();
                return false;
            }

            var plateLocation = AcxDungeonSpot(AgentWidth.Narrow, againstWall: false) ?? new Position(Location).InFrontOf(2);
            var trapLocation = (Pathfinder.Enabled ? Pathfinder.GetNearestWallPosition(plateLocation, 20, AgentWidth.Narrow, out _) : null) ?? new Position(plateLocation).InFrontOf(8);
            trapLocation.Rotate(GetDirection(trapLocation.ToGlobal(), plateLocation.ToGlobal()));
            trapLocation.PositionZ += 2.5f;

            var tier = AcxTier();
            var spell = AcxTrapSpells[ThreadSafeRandom.Next(0, AcxTrapSpells.Length - 1)];
            var levels = SpellLevelProgression.GetSpellLevels(spell);
            trap.SpellDID = (uint)(levels != null && levels.Count > 0 ? levels[Math.Min(tier, levels.Count - 1)] : spell);
            trap.ItemSpellcraft = (tier + 1) * 50;
            trap.Location = trapLocation;
            trap.Location.LandblockId = new LandblockId(trap.Location.GetCell());

            if (!trap.EnterWorld())
            {
                trap.Destroy();
                plate.Destroy();
                return false;
            }

            plate.Location = plateLocation;
            plate.Location.LandblockId = new LandblockId(plate.Location.GetCell());
            plate.ActivationTarget = trap.Guid.Full;

            if (!plate.EnterWorld())
            {
                trap.Destroy();
                plate.Destroy();
                return false;
            }

            var lifetime = new ActionChain();
            lifetime.AddDelaySeconds(AcxTrapLifetime.TotalSeconds);
            lifetime.AddAction(trap, () =>
            {
                if (!trap.IsDestroyed)
                    trap.Destroy();
                if (!plate.IsDestroyed)
                    plate.Destroy();
            });
            lifetime.EnqueueChain();
            return true;
        }
    }
}
