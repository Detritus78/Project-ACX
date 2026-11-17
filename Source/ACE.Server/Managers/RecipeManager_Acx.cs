using System;
using System.Collections.Generic;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: tinkers for the salvage retail left unused (acx/Content/salvage/30-salvage-remap.sql).
    /// They are matched by the salvage's material, so any bag of that material works, and applied here rather than by
    /// mutation scripts. The recipes in the database carry the skill, messages and requirements.
    /// </summary>
    public partial class RecipeManager
    {
        private enum AcxTinker
        {
            Fire = 1, Cold, Acid, Lightning,        // change a weapon's or caster's element
            Pierce, Slash, Bludgeon,                // change a weapon's damage type
            ArcaneLore5, ArcaneLore10,              // lower the Arcane Lore requirement (10 also doubles the mana cost)
            ElementalRend,                          // fire, cold, acid and lightning rending on a caster (an imbue)
            MissileDefense, MagicDefense,           // +1% to a melee weapon's missile / magic defense bonus
            Potency                                 // +4% to an Alchemist's bomb's Potency (its damage multiplier), as Mahogany on a bow
        }

        /// <summary>
        /// The recipe for weapons (or any item) and, for the element changes, the one for casters (Magic Item Tinkering)
        /// </summary>
        private static readonly Dictionary<MaterialType, (AcxTinker Tinker, uint Recipe, uint CasterRecipe)> acxTinkers = new()
        {
            { MaterialType.YellowGarnet, (AcxTinker.Fire,           9301, 9313) },
            { MaterialType.WhiteQuartz,  (AcxTinker.Cold,           9302, 9314) },
            { MaterialType.GreenJade,    (AcxTinker.Acid,           9303, 9315) },
            { MaterialType.Amethyst,     (AcxTinker.Lightning,      9304, 9316) },
            { MaterialType.Onyx,         (AcxTinker.Pierce,         9305, 0) },
            { MaterialType.Tourmaline,   (AcxTinker.Slash,          9306, 0) },
            { MaterialType.WhiteJade,    (AcxTinker.Bludgeon,       9307, 0) },
            { MaterialType.Turquoise,    (AcxTinker.ArcaneLore5,    9308, 0) },
            { MaterialType.Serpentine,   (AcxTinker.ArcaneLore10,   9309, 0) },
            { MaterialType.TigerEye,     (AcxTinker.ElementalRend,  9310, 0) },
            { MaterialType.Teak,         (AcxTinker.MissileDefense, 9311, 0) },
            { MaterialType.Ebony,        (AcxTinker.MagicDefense,   9312, 0) },
            // JJ 2026-11-16: "what if we added potency and a new salvage" ... "its a modifier for the physical damage";
            // Obsidian, which nothing used (acx/Content/alchemist/20-obsidian-potency.sql)
            { MaterialType.Obsidian,     (AcxTinker.Potency,        9317, 0) },
        };

        /// <summary>
        /// The recipes' mutation data ids are 0x38001000 + the tinker number
        /// </summary>
        private const uint AcxDataIdBase = 0x38001000;

        private const UiEffects AcxElementEffects = UiEffects.Fire | UiEffects.Lightning | UiEffects.Frost | UiEffects.Acid | UiEffects.Nether;

        public static Recipe GetAcxRecipe(WorldObject source, WorldObject target)
        {
            if (source.ItemType != ItemType.TinkeringMaterial || source.MaterialType == null)
                return null;

            if (!acxTinkers.TryGetValue(source.MaterialType.Value, out var entry))
                return null;

            // workmanship, except Tiger Eye (an imbue) on an untinkerable quest or named caster, which takes one imbue
            // (WorldObject_AcxQuestWeapon)
            if (target.Workmanship == null && !(entry.Tinker == AcxTinker.ElementalRend && target.IsAcxQuestWeapon))
                return null;

            var weapon = target.WeenieType == WeenieType.MeleeWeapon || target.WeenieType == WeenieType.MissileLauncher;
            var caster = target.WeenieType == WeenieType.Caster;

            uint recipeId;
            switch (entry.Tinker)
            {
                case AcxTinker.Fire:
                case AcxTinker.Cold:
                case AcxTinker.Acid:
                case AcxTinker.Lightning:
                    recipeId = weapon ? entry.Recipe : caster ? entry.CasterRecipe : 0;
                    break;

                case AcxTinker.Pierce:
                case AcxTinker.Slash:
                case AcxTinker.Bludgeon:
                    recipeId = weapon ? entry.Recipe : 0;
                    break;

                case AcxTinker.ElementalRend:
                    recipeId = caster ? entry.Recipe : 0;
                    break;

                case AcxTinker.MissileDefense:
                case AcxTinker.MagicDefense:
                    recipeId = target.WeenieType == WeenieType.MeleeWeapon ? entry.Recipe : 0;
                    break;

                case AcxTinker.Potency:
                    recipeId = AcxBombWcids.IsDamageBomb(target.WeenieClassId) ? entry.Recipe : 0;
                    break;

                default:
                    recipeId = entry.Recipe;   // Arcane Lore: any looted item; the recipe requires a lore requirement
                    break;
            }

            return recipeId != 0 ? DatabaseManager.World.GetCachedRecipe(recipeId) : null;
        }

        private static bool IsAcxMutation(uint dataId)
        {
            return dataId > AcxDataIdBase && dataId <= AcxDataIdBase + (uint)AcxTinker.Potency;
        }

        private static bool TryAcxMutate(uint dataId, WorldObject target)
        {
            switch ((AcxTinker)(dataId - AcxDataIdBase))
            {
                case AcxTinker.Fire:      SetAcxDamageType(target, DamageType.Fire, UiEffects.Fire); break;
                case AcxTinker.Cold:      SetAcxDamageType(target, DamageType.Cold, UiEffects.Frost); break;
                case AcxTinker.Acid:      SetAcxDamageType(target, DamageType.Acid, UiEffects.Acid); break;
                case AcxTinker.Lightning: SetAcxDamageType(target, DamageType.Electric, UiEffects.Lightning); break;
                case AcxTinker.Pierce:    SetAcxDamageType(target, DamageType.Pierce, UiEffects.Undef); break;
                case AcxTinker.Slash:     SetAcxDamageType(target, DamageType.Slash, UiEffects.Undef); break;
                case AcxTinker.Bludgeon:  SetAcxDamageType(target, DamageType.Bludgeon, UiEffects.Undef); break;

                case AcxTinker.ArcaneLore5:
                    target.ItemDifficulty = Math.Max(0, (target.ItemDifficulty ?? 0) - 5);
                    break;

                case AcxTinker.ArcaneLore10:
                    target.ItemDifficulty = Math.Max(0, (target.ItemDifficulty ?? 0) - 10);
                    if (target.ManaRate != null)
                        target.ManaRate *= 2;
                    break;

                case AcxTinker.ElementalRend:
                    target.ImbuedEffect = ImbuedEffectType.FireRending | ImbuedEffectType.ColdRending | ImbuedEffectType.AcidRending | ImbuedEffectType.ElectricRending;
                    break;

                case AcxTinker.MissileDefense:
                    target.WeaponMissileDefense = (target.WeaponMissileDefense ?? 1.0) + 0.01;
                    break;

                case AcxTinker.MagicDefense:
                    target.WeaponMagicDefense = (target.WeaponMagicDefense ?? 1.0) + 0.01;
                    break;

                case AcxTinker.Potency:
                    target.DamageMod = (target.DamageMod ?? 1.0) + 0.04;   // Mahogany's step (recipe 3855)
                    break;

                default:
                    return false;
            }

            target.NumTimesTinkered++;
            return true;
        }

        /// <summary>
        /// Changes a weapon's or caster's damage type, its element glow, and any resistance cleaving to match (as ClassicACE).
        /// An item's damage type can be changed once (the recipes require AcxDamageTypeChanged unset).
        /// </summary>
        private static void SetAcxDamageType(WorldObject target, DamageType damageType, UiEffects elementEffect)
        {
            target.W_DamageType = damageType;
            var uiEffects = ((target.UiEffects ?? UiEffects.Undef) & ~AcxElementEffects) | elementEffect;
            target.UiEffects = uiEffects != UiEffects.Undef ? uiEffects : null;

            if (target.ResistanceModifierType != null && target.ResistanceModifierType != DamageType.Undef)
            {
                target.ResistanceModifierType = damageType;

                // a caster's cleave tag follows its element ("Fire Staff [Fire]" -> "[Frost]"; RollAcxExtraMutations)
                if (target.Name != null)
                    target.Name = System.Text.RegularExpressions.Regex.Replace(target.Name, @"\[(Fire|Frost|Acid|Lightning)\]", $"[{LootGenerationFactory.AcxElementTag(damageType)}]");
            }

            target.SetProperty(PropertyBool.AcxDamageTypeChanged, true);

            ApplyAcxVersionLook(target, damageType);
        }

        private static readonly PropertyDataId[] AcxLookDataIds = { PropertyDataId.Setup, PropertyDataId.PaletteBase, PropertyDataId.ClothingBase, PropertyDataId.Icon, PropertyDataId.PhysicsEffectTable };

        /// <summary>
        /// Gives the item the model, icon, colours and name of its weapon's version with the new damage type
        /// (Flaming Battle Axe for fire, the plain Battle Axe for a physical type), and drops the nether smoke and sparks
        /// </summary>
        private static void ApplyAcxVersionLook(WorldObject target, DamageType damageType)
        {
            if (damageType != DamageType.Nether)
            {
                target.AcxAuraScript = null;
                target.AcxAuraScriptCount = 1;
                target.AcxAuraScript2 = null;
                target.AcxAuraScript2Count = 1;
                target.AcxAuraScript3 = null;
            }

            var versionWcid = AcxWeaponFamilies.GetVersion(target.WeenieClassId, damageType);
            var version = versionWcid != null ? DatabaseManager.World.GetCachedWeenie(versionWcid.Value) : null;

            if (version == null)
                return;

            foreach (var property in AcxLookDataIds)
            {
                var value = version.GetProperty(property);
                if (value != null)
                    target.SetProperty(property, value.Value);
                else
                    target.RemoveProperty(property);
            }

            target.PaletteTemplate = version.GetProperty(PropertyInt.PaletteTemplate);
            target.Shade = version.GetProperty(PropertyFloat.Shade);
            LootGenerationFactory.RollAcxLootColor(target);

            // keeps the special-roll tags after the name ("Fire Javelin [AC][BS]"; LootGenerationFactory.RollAcxExtraMutations)
            var versionName = version.GetProperty(PropertyString.Name);
            if (versionName != null)
                target.Name = versionName + System.Text.RegularExpressions.Regex.Match(target.Name ?? "", @"( (\[[^\]]+\])+)$").Value;
        }
    }
}
