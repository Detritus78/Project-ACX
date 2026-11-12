using System;
using System.Collections.Generic;

using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    /// <summary>
    /// Project ACX: an item's Arcane Lore requirement comes only from its highest spell or cantrip
    /// </summary>
    public static partial class LootGenerationFactory
    {
        /// <summary>
        /// Arcane Lore needed for each spell level I-VIII (index = spell level): the trained lore a character
        /// can afford with about 5% of their XP by the time that spell level drops
        /// </summary>
        private static readonly int[] AcxArcaneLoreBySpellLevel = { 0, 25, 25, 75, 105, 130, 150, 165, 190 };

        /// <summary>
        /// Minor, Major, Epic and Legendary cantrips count as spell levels III, VI, VII and VIII
        /// </summary>
        private static readonly int[] AcxCantripSpellLevels = { 3, 6, 7, 8 };

        private static readonly string[] AcxCantripPrefixes = { "Minor ", "Major ", "Epic ", "Legendary " };

        /// <summary>
        /// A spell's level I-VIII, with cantrips placed by their tier (their own power is only 1-30)
        /// </summary>
        public static int GetAcxSpellLevel(SpellId spellId)
        {
            var spell = new Spell(spellId);
            if (spell.NotFound)
                return 0;

            if (spell.IsCantrip)
            {
                for (var tier = AcxCantripPrefixes.Length - 1; tier > 0; tier--)
                {
                    if (spell.Name.StartsWith(AcxCantripPrefixes[tier]))
                        return AcxCantripSpellLevels[tier];
                }
                return AcxCantripSpellLevels[0];
            }

            return (int)spell.Level;
        }

        /// <summary>
        /// The Arcane Lore requirement for a set of spells, or null when there are none
        /// </summary>
        public static int? GetAcxArcaneLore(IEnumerable<int> spellIds)
        {
            var highestLevel = 0;

            if (spellIds != null)
            {
                foreach (var spellId in spellIds)
                    highestLevel = Math.Max(highestLevel, GetAcxSpellLevel((SpellId)spellId));
            }

            if (highestLevel <= 0)
                return null;

            return AcxArcaneLoreBySpellLevel[Math.Min(highestLevel, AcxArcaneLoreBySpellLevel.Length - 1)];
        }

        public static int? GetAcxArcaneLore(WorldObject wo)
        {
            return GetAcxArcaneLore(wo.Biota.PropertiesSpellBook?.Keys);
        }

        /// <summary>
        /// Recalculates the requirement of an item created with one, such as a quest item
        /// </summary>
        public static void ApplyAcxArcaneLore(WorldObject wo)
        {
            if (wo == null || (wo.ItemDifficulty ?? 0) <= 0)
                return;

            var arcaneLore = GetAcxArcaneLore(wo);
            if (arcaneLore != null)
                wo.ItemDifficulty = arcaneLore;
        }
    }
}
