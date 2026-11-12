using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Factories.Tables;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    /// <summary>
    /// Project ACX: recalculates Spellcraft / Arcane Lore when spell transfer scrolls change an item's spells.
    /// Ported from ClassicACE's end-of-retail ruleset branch, which matches ACE's own loot formulas:
    /// spellcraft = max spell power * 0.9-1.1, arcane lore = spellcraft - skill limit / 2
    /// + 2.5-7.5 per level for each creature/life spell except the highest + 5-10 / 10-20 per cantrip.
    /// </summary>
    public static partial class LootGenerationFactory
    {
        /// <summary>
        /// Weapon proc spells (level 1 ids); a transferred spell in this list becomes the item's cast-on-strike
        /// </summary>
        public static readonly HashSet<SpellId> TransferableProcSpells = new HashSet<SpellId>()
        {
            SpellId.StaminaToHealthSelf1,
            SpellId.StaminaToManaSelf1,
            SpellId.ManaToStaminaSelf1,
            SpellId.ManaToHealthSelf1,
            SpellId.HealthToStaminaSelf1,
            SpellId.HealthToManaSelf1,

            SpellId.DrainStamina1,
            SpellId.DrainMana1,
            SpellId.DrainHealth1,

            SpellId.BloodLoather,
            SpellId.LeadenWeapon1,
            SpellId.TurnBlade1,
            SpellId.Brittlemail1,
        };

        public static SpellId GetLevel1SpellId(SpellId spellId)
        {
            if (spellId == SpellId.Undef)
                return SpellId.Undef;

            var levels = SpellLevelProgression.GetSpellLevels(spellId);

            return levels != null && levels.Count > 0 ? levels[0] : spellId;
        }

        public static int GetTransferMaxBaseMana(WorldObject wo) => GetMaxBaseMana(wo);

        public static float GetTransferManaRate(int maxBaseMana) => CalculateManaRate(maxBaseMana);

        public static ushort RollItemMaxStructure(WorldObject wo)
        {
            return (ushort)ThreadSafeRandom.Next(10, 20 + (int)(wo.ItemWorkmanship * 2));
        }

        public static void CalculateSpellcraft(WorldObject wo, List<SpellId> allSpellIds, bool updateItem, out int effectiveMinSpellcraft, out int effectiveMaxSpellcraft, out int effectiveRolledSpellcraft, out int realSpellcraft)
        {
            var maxSpellPower = 0;

            if (allSpellIds != null)
            {
                foreach (var spellId in allSpellIds)
                {
                    var spell = new Server.Entity.Spell(spellId);

                    if (spell.Power > maxSpellPower)
                        maxSpellPower = (int)spell.Power;
                }
            }

            (float min, float max) spellcraftMultiplierRange;
            float spellcraftMultiplier;

            if (wo.RolledSpellcraftMultiplier.HasValue)
            {
                // Load our saved multiplier.
                spellcraftMultiplierRange = ((float)wo.RolledSpellcraftMultiplier.Value, (float)wo.RolledSpellcraftMultiplier.Value);
                spellcraftMultiplier = (float)wo.RolledSpellcraftMultiplier.Value;
            }
            else
            {
                switch (wo.ItemType)
                {
                    case ItemType.Armor:
                    case ItemType.Clothing:
                    case ItemType.Jewelry:

                    case ItemType.MeleeWeapon:
                    case ItemType.MissileWeapon:
                    case ItemType.Caster:
                        spellcraftMultiplierRange = (0.9f, 1.1f);
                        break;
                    default:
                        spellcraftMultiplierRange = (1.0f, 1.0f);
                        break;
                }

                spellcraftMultiplier = (float)ThreadSafeRandom.Next(spellcraftMultiplierRange.min, spellcraftMultiplierRange.max);
            }

            // retail was capped at 370
            effectiveMinSpellcraft = Math.Min((int)Math.Ceiling(maxSpellPower * spellcraftMultiplierRange.min), 370);
            effectiveMaxSpellcraft = Math.Min((int)Math.Ceiling(maxSpellPower * spellcraftMultiplierRange.max), 370);
            effectiveRolledSpellcraft = Math.Min((int)Math.Ceiling(maxSpellPower * spellcraftMultiplier), 370);

            // never lower an item's existing spellcraft
            var currentSpellcraft = Math.Max(wo.BaseSpellcraftOverride ?? 1, wo.ItemSpellcraft ?? 1);
            realSpellcraft = Math.Max(currentSpellcraft, effectiveRolledSpellcraft);

            if (updateItem)
            {
                wo.ItemSpellcraft = realSpellcraft;
                wo.RolledSpellcraftMultiplier = spellcraftMultiplier;
            }
        }

        /// <summary>
        /// Project ACX: the requirement comes only from the highest spell or cantrip (GetAcxArcaneLore),
        /// so the estimate and the result are the same number, and older items move to the new table.
        /// </summary>
        public static void CalculateArcaneLore(WorldObject wo, List<SpellId> allSpellIds, List<SpellId> lifeCreatureEnchantmentsIds, List<SpellId> cantripIds, int minSpellcraft, int maxSpellcraft, int rolledSpellcraft, bool updateItem, out int minArcaneLore, out int maxArcaneLore, out int rolledArcaneLore)
        {
            var arcaneLore = GetAcxArcaneLore(allSpellIds?.Select(i => (int)i)) ?? wo.ItemDifficulty ?? 0;

            minArcaneLore = maxArcaneLore = rolledArcaneLore = arcaneLore;

            if (updateItem)
                wo.ItemDifficulty = arcaneLore;
        }
    }
}
