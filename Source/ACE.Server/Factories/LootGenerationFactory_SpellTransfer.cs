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

        public static void CalculateArcaneLore(WorldObject wo, List<SpellId> allSpellIds, List<SpellId> lifeCreatureEnchantmentsIds, List<SpellId> cantripIds, int minSpellcraft, int maxSpellcraft, int rolledSpellcraft, bool updateItem, out int minArcaneLore, out int maxArcaneLore, out int rolledArcaneLore)
        {
            var spellRolls = new List<(SpellId SpellId, float MinRoll, float MaxRoll)>();

            if (lifeCreatureEnchantmentsIds != null)
            {
                var spells = new List<Server.Entity.Spell>();

                foreach (var spellId in lifeCreatureEnchantmentsIds)
                {
                    if (cantripIds != null && cantripIds.Contains(spellId))
                        continue;

                    spells.Add(new Server.Entity.Spell(spellId));
                }

                spells = spells.OrderBy(i => i.Formula.Level).ToList();

                // exclude highest spell
                for (var i = 0; i < spells.Count - 1; i++)
                {
                    var spell = spells[i];

                    var spellRoll = GetArcaneLoreLogValue(wo, (SpellId)spell.Id);
                    if (spellRoll != 0)
                        spellRolls.Add(((SpellId)spell.Id, spellRoll, spellRoll));
                    else
                    {
                        var spellArcaneRoll = spell.Formula.Level * 5.0f;

                        spellRolls.Add(((SpellId)spell.Id, spellArcaneRoll * 0.5f, spellArcaneRoll * 1.5f));
                    }
                }
            }

            if (cantripIds != null)
            {
                foreach (var cantripId in cantripIds)
                {
                    var spellRoll = GetArcaneLoreLogValue(wo, cantripId);
                    if (spellRoll != 0)
                    {
                        spellRolls.Add((cantripId, spellRoll, spellRoll));
                        continue;
                    }

                    var cantripLevels = SpellLevelProgression.GetSpellLevels(cantripId);

                    if (cantripLevels == null || cantripLevels.Count != 4)
                    {
                        log.Error($"CalculateArcaneLore({cantripId}) - unknown cantrip");
                        continue;
                    }

                    // minor 5-10, others 10-20
                    if (cantripLevels.IndexOf(cantripId) == 0)
                        spellRolls.Add((cantripId, 5, 10));
                    else
                        spellRolls.Add((cantripId, 10, 20));
                }
            }

            var itemSkillLevelFactor = 0.0f;

            if (wo.ItemSkillLevelLimit.HasValue && wo.ItemSkillLevelLimit > 0)
                itemSkillLevelFactor = wo.ItemSkillLevelLimit.Value / 2.0f;

            var fArcaneMin = Math.Max(minSpellcraft - itemSkillLevelFactor, 0);
            var fArcaneMax = Math.Max(maxSpellcraft - itemSkillLevelFactor, 0);
            var fArcaneRolled = Math.Max(rolledSpellcraft - itemSkillLevelFactor, 0);

            var minArcaneRoll = 0f;
            var maxArcaneRoll = 0f;
            var arcaneRoll = 0f;
            foreach (var spell in spellRolls)
            {
                var spellRoll = (float)ThreadSafeRandom.Next(spell.MinRoll, spell.MaxRoll);
                if (updateItem)
                    UpdateArcaneLoreLog(wo, spell.SpellId, spellRoll);

                minArcaneRoll += spell.MinRoll;
                maxArcaneRoll += spell.MaxRoll;
                arcaneRoll += spellRoll;
            }
            minArcaneLore = (int)Math.Floor(fArcaneMin + minArcaneRoll);
            maxArcaneLore = (int)Math.Floor(fArcaneMax + maxArcaneRoll);
            rolledArcaneLore = (int)Math.Floor(fArcaneRolled + arcaneRoll);

            // never lower an item's existing arcane lore
            var currentArcaneLore = Math.Max(wo.BaseItemDifficultyOverride ?? 0, wo.ItemDifficulty ?? 0);
            minArcaneLore = Math.Max(currentArcaneLore, minArcaneLore);
            maxArcaneLore = Math.Max(currentArcaneLore, maxArcaneLore);
            rolledArcaneLore = Math.Max(currentArcaneLore, rolledArcaneLore);

            if (updateItem)
                wo.ItemDifficulty = rolledArcaneLore;
        }

        private static void UpdateArcaneLoreLog(WorldObject obj, SpellId spellId, float roll)
        {
            if (obj.ArcaneLoreRollLog != null)
            {
                var updatedValue = false;
                var spellsRollSplitString = obj.ArcaneLoreRollLog.Split(",");
                for (int i = 0; i < spellsRollSplitString.Length; i++)
                {
                    if (spellsRollSplitString[i].StartsWith($"{(int)spellId}:"))
                    {
                        spellsRollSplitString[i] = $"{(int)spellId}:{roll:0.00}";
                        updatedValue = true;
                        break;
                    }
                }

                if (!updatedValue)
                    obj.ArcaneLoreRollLog += $",{(int)spellId}:{roll:0.00}";
                else
                    obj.ArcaneLoreRollLog = string.Join(",", spellsRollSplitString);
            }
            else
                obj.ArcaneLoreRollLog = $"{(int)spellId}:{roll:0.00}";
        }

        private static float GetArcaneLoreLogValue(WorldObject obj, SpellId spellId)
        {
            if (obj.ArcaneLoreRollLog != null)
            {
                foreach (var spellRollString in obj.ArcaneLoreRollLog.Split(","))
                {
                    if (spellRollString.StartsWith($"{(int)spellId}:"))
                    {
                        if (float.TryParse(spellRollString.Split(":").Last(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var roll))
                            return roll;
                    }
                }
            }
            return 0;
        }
    }
}
