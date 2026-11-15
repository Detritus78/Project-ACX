using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories.Tables;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    /// <summary>
    /// Project ACX: extra weapon mutations on loot (JJ 2026-11-12, ported from ClassicACE with its numbers).
    /// Weapons and casters can roll, 2% each and at most two of: Armor Cleaving (casters: an elemental Resistance Cleaving
    /// instead), Biting Strike, Crushing Blow (Shield Cleaving dropped 2026-11-13, JJ: not needed), each tagged after the
    /// weapon's name ([AC], [BS], [CB], a caster's element, a slayer's creature: "Fire Javelin [AC][Olthoi]"). These
    /// start at loot tier 2 (setting acx_extra_mutation_min_tier). Any weapon or caster at any tier can also roll a slayer
    /// (1%). Shields can roll Magic Absorbing (2%, from the same tier; ClassicACE counts these but has no roll, so the
    /// chance is ours) at 0.30, a little over retail's 0.25 (the Aegis shields and tomes; JJ 2026-11-13). A weapon never ends up holding both halves of an imbue pair: RecipeManager_AcxPairs refuses the
    /// matching imbue.
    /// </summary>
    public static partial class LootGenerationFactory
    {
        private const float AcxExtraMutationChance = 0.02f;
        private const int AcxMaxExtraMutations = 2;

        private static int AcxExtraMutationMinTier => (int)PropertyManager.GetLong("acx_extra_mutation_min_tier").Item;

        private static bool AcxRoll(TreasureDeath profile, float chance) => chance * (1 + profile.LootQualityMod) > ThreadSafeRandom.Next(0.0f, 1.0f);

        /// <summary>
        /// Called at the end of the melee, missile and caster mutations
        /// </summary>
        /// <summary>
        /// Project ACX: one-handed axes (and, a little, spears) roll a tighter damage range (JJ 2026-11-13). Retail axe loot rolls ~0.87 variance
        /// (a hit can be 13% of the top number) against ~0.5 for swords, which left axes 14-23% under their class
        /// (/acxbench). The variance on the item itself changes, so appraisal shows the better range.
        /// acx_heavy/light/finesse_axe_variance_mod scale it; 1.0 = retail.
        /// </summary>
        private static void AcxAdjustVariance(WorldObject wo)
        {
            if (wo.DamageVariance == null || (wo.WeaponSkill != Skill.HeavyWeapons && wo.WeaponSkill != Skill.LightWeapons && wo.WeaponSkill != Skill.FinesseWeapons))
                return;

            // spears too, a smaller nudge (JJ 2026-11-13): ~0.7 variance left them 2-11% under the class
            var setting = wo.W_WeaponType == WeaponType.Spear ? "acx_spear_variance_mod" : wo.W_WeaponType != WeaponType.Axe ? null : wo.WeaponSkill switch
            {
                Skill.HeavyWeapons => "acx_heavy_axe_variance_mod",
                Skill.LightWeapons => "acx_light_axe_variance_mod",
                _ => "acx_finesse_axe_variance_mod",
            };
            if (setting != null)
                wo.DamageVariance *= PropertyManager.GetDouble(setting).Item;
        }

        private static void RollAcxExtraMutations(WorldObject wo, TreasureDeath profile)
        {
            if (profile.LootQualityMod < 0)
                return;

            var tags = new List<string>();

            if (profile.Tier >= AcxExtraMutationMinTier)
            {
                var count = 0;
                if (count < AcxMaxExtraMutations && AcxRoll(profile, AcxExtraMutationChance))
                {
                    if (wo.IsCaster)
                    {
                        wo.ResistanceModifierType = AcxCleaveElement(wo);
                        wo.ResistanceModifier = 0.6f;
                        tags.Add(AcxElementTag(wo.ResistanceModifierType.Value));
                    }
                    else
                    {
                        wo.IgnoreArmor = 0.625f;   // about -75 armor against 200 armor
                        tags.Add("AC");
                    }
                    count++;
                }
                if (count < AcxMaxExtraMutations && AcxRoll(profile, AcxExtraMutationChance))
                {
                    wo.CriticalFrequency = wo.IsCaster ? 0.10f : 0.15f;   // Biting Strike
                    tags.Add("BS");
                    count++;
                }
                if (count < AcxMaxExtraMutations && AcxRoll(profile, AcxExtraMutationChance))
                {
                    wo.SetProperty(PropertyFloat.CriticalMultiplier, wo.IsCaster ? 1.5 : 2.0);    // Crushing Blow
                    tags.Add("CB");
                    count++;
                }
            }

            if (AcxRoll(profile, AcxSlayerTypeChance.GetSlayerChanceForTier(profile.Tier)))
            {
                wo.SlayerCreatureType = AcxSlayerTypeChance.Roll(profile);
                wo.SlayerDamageBonus = 1.3f;
                tags.Add(Regex.Replace(wo.SlayerCreatureType.Value.ToString(), "(?<=[a-z])(?=[A-Z])", " "));   // PhyntosWasp -> Phyntos Wasp
            }

            // each roll is a tag of its own after the name, so loot rules and players can pick them out (JJ 2026-11-15: "last
            // time I ran a server with mutations it gave them unique names which was used to identify and loot them"; then
            // the short form, one bracket per tag so a rule for "[BS]" matches whatever else rolled): Fire Javelin [AC][BS][Olthoi]
            if (tags.Count > 0)
                wo.Name = $"{wo.Name} {string.Concat(tags.Select(t => $"[{t}]"))}";
        }

        /// <summary>
        /// A caster's cleave tag: its element as retail names elemental weapons
        /// </summary>
        public static string AcxElementTag(DamageType element)
        {
            return element switch
            {
                DamageType.Fire => "Fire",
                DamageType.Cold => "Frost",
                DamageType.Acid => "Acid",
                DamageType.Electric => "Lightning",
                _ => element.ToString(),
            };
        }

        /// <summary>
        /// A caster cleaves its own element, or a random one. ACE matches cleaving to one exact element, so ClassicACE's
        /// all-elements cleave wouldn't work here.
        /// </summary>
        private static DamageType AcxCleaveElement(WorldObject caster)
        {
            var own = caster.W_DamageType;
            if (own == DamageType.Fire || own == DamageType.Cold || own == DamageType.Acid || own == DamageType.Electric)
                return own;
            return ThreadSafeRandom.Next(0, 3) switch { 0 => DamageType.Fire, 1 => DamageType.Cold, 2 => DamageType.Acid, _ => DamageType.Electric };
        }

        /// <summary>
        /// Called at the end of the armor mutation: shields can roll Magic Absorbing
        /// </summary>
        private static void RollAcxShieldMutations(WorldObject wo, TreasureDeath profile)
        {
            if (!wo.IsShield || profile.LootQualityMod < 0 || profile.Tier < AcxExtraMutationMinTier)
                return;

            if (AcxRoll(profile, AcxExtraMutationChance))
            {
                wo.AbsorbMagicDamage = 0.3f;   // up to 30% (retail Magic Absorbing items are 0.25), scaled by Magic Defense
                wo.Name = $"{wo.Name} of Magic Absorbing";
            }
        }
    }
}

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        /// <summary>
        /// Project ACX: armor set settings (JJ 2026-11-11, ClassicACE): equipmentsetid_enabled turns armor sets on loot
        /// on or off, equipmentsetid_drop_rate scales how often they appear (1 = retail), and
        /// equipmentsetid_name_decoration puts the set's name in front of the armor's name.
        /// </summary>
        private static void RollAcxEquipmentSet(WorldObject wo, TreasureDeath profile, Entity.TreasureRoll roll)
        {
            if (!PropertyManager.GetBool("equipmentsetid_enabled").Item)
                return;

            var rate = PropertyManager.GetDouble("equipmentsetid_drop_rate").Item;
            var set = EquipmentSetChance.Roll(wo, profile, roll);
            if (set != null && rate < 1.0 && ThreadSafeRandom.Next(0.0f, 1.0f) >= rate)
                set = null;
            for (var extra = rate - 1.0; set == null && extra > 0; extra -= 1.0)
                if (ThreadSafeRandom.Next(0.0f, 1.0f) < extra)
                    set = EquipmentSetChance.Roll(wo, profile, roll);
            if (set == null)
                return;

            wo.EquipmentSetId = set;
            if (PropertyManager.GetBool("equipmentsetid_name_decoration").Item)
            {
                var setName = set.ToString();
                if (set >= EquipmentSet.Soldiers && set <= EquipmentSet.Crafters)
                    setName = setName.TrimEnd('s') + "'s";
                wo.Name = $"{setName} {wo.Name}";
            }
        }
    }
}
