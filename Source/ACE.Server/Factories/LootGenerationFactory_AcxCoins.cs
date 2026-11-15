using System.Collections.Generic;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Server.Factories.Enum;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    /// <summary>
    /// Project ACX: monsters carry no coins (JJ 2026-11-12). Money comes from selling what you hunt, trading and NPC rewards;
    /// treasure chests still hold coins.
    /// </summary>
    public static partial class LootGenerationFactory
    {
        /// <summary>
        /// The other mundane drops, equally likely, that take the place of a monster's coin roll
        /// </summary>
        private static readonly TreasureItemType[] AcxCoinReplacements =
        {
            TreasureItemType.Consumable, TreasureItemType.HealKit, TreasureItemType.Lockpick, TreasureItemType.SpellComponent, TreasureItemType.ManaStone
        };

        /// <summary>
        /// Death treasure for a creature's corpse: the usual roll, with every coin stack re-rolled as another mundane item
        /// </summary>
        public static List<WorldObject> CreateAcxCreatureLoot(TreasureDeath profile)
        {
            var loot = CreateRandomLootObjects(profile);

            for (var i = loot.Count - 1; i >= 0; i--)
            {
                if (loot[i].WeenieClassId != (uint)WeenieClassName.coinstack)
                    continue;

                loot[i].Destroy();
                loot.RemoveAt(i);

                var replacement = CreateRandomLootObjects(profile, TreasureItemCategory.MundaneItem, AcxCoinReplacements[ThreadSafeRandom.Next(0, AcxCoinReplacements.Length - 1)]);
                if (replacement != null)
                    loot.Insert(i, replacement);
            }

            return loot;
        }
    }
}
