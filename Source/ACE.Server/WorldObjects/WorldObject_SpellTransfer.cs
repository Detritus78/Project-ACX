using System;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: item state used by spell extraction / transfer scrolls (ported from ClassicACE)
    /// </summary>
    partial class WorldObject
    {
        /// <summary>
        /// Number of spells added to this item by spell transfer scrolls
        /// </summary>
        public int? ExtraSpellsCount
        {
            get => GetProperty(PropertyInt.ExtraSpellsCount);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.ExtraSpellsCount); else SetProperty(PropertyInt.ExtraSpellsCount, value.Value); }
        }

        /// <summary>
        /// Overrides the workmanship-based extra spell slot count
        /// </summary>
        public int? ExtraSpellsMaxOverride
        {
            get => GetProperty(PropertyInt.ExtraSpellsMaxOverride);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.ExtraSpellsMaxOverride); else SetProperty(PropertyInt.ExtraSpellsMaxOverride, value.Value); }
        }

        /// <summary>
        /// Arcane Lore of the item's own spells, which transfers will not lower
        /// </summary>
        public int? BaseItemDifficultyOverride
        {
            get => GetProperty(PropertyInt.BaseItemDifficultyOverride);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.BaseItemDifficultyOverride); else SetProperty(PropertyInt.BaseItemDifficultyOverride, value.Value); }
        }

        /// <summary>
        /// Spellcraft of the item's own spells, which transfers will not lower
        /// </summary>
        public int? BaseSpellcraftOverride
        {
            get => GetProperty(PropertyInt.BaseSpellcraftOverride);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.BaseSpellcraftOverride); else SetProperty(PropertyInt.BaseSpellcraftOverride, value.Value); }
        }

        /// <summary>
        /// Comma-separated spell ids added by spell transfer scrolls
        /// </summary>
        public string ExtraSpellsList
        {
            get => GetProperty(PropertyString.ExtraSpellsList);
            set { if (value == null) RemoveProperty(PropertyString.ExtraSpellsList); else SetProperty(PropertyString.ExtraSpellsList, value); }
        }

        /// <summary>
        /// Comma-separated spellId:roll pairs, so adding a spell does not re-roll the Arcane Lore of the others
        /// </summary>
        public string ArcaneLoreRollLog
        {
            get => GetProperty(PropertyString.ArcaneLoreRollLog);
            set { if (value == null) RemoveProperty(PropertyString.ArcaneLoreRollLog); else SetProperty(PropertyString.ArcaneLoreRollLog, value); }
        }

        public bool BlockSpellExtraction
        {
            get => GetProperty(PropertyBool.BlockSpellExtraction) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.BlockSpellExtraction); else SetProperty(PropertyBool.BlockSpellExtraction, value); }
        }

        public double? RolledSpellcraftMultiplier
        {
            get => GetProperty(PropertyFloat.RolledSpellcraftMultiplier);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.RolledSpellcraftMultiplier); else SetProperty(PropertyFloat.RolledSpellcraftMultiplier, value.Value); }
        }

        /// <summary>
        /// Robes and dresses
        /// </summary>
        public bool IsRobe => ItemType == ItemType.Clothing && ClothingPriority.HasValue && ClothingPriority.Value.HasFlag(CoverageMask.OuterwearChest);

        public bool CanHaveExtraSpells
        {
            get
            {
                if (ExtraSpellsMaxOverride.HasValue && ExtraSpellsMaxOverride > 0)
                    return true;
                else if (IsAcxQuestWeapon)
                    return true;   // Project ACX: two transfers on an untinkerable quest or named weapon (WorldObject_AcxQuestWeapon)
                else if (IsAcxExplorerGear)
                    return true;   // Project ACX: two transfers on Explorer Society armor, clothing and jewelry (WorldObject_AcxExplorerGear)
                else if (IsAcxQuestArmor)
                    return true;   // Project ACX: two transfers on untinkerable quest armor and clothing (WorldObject_AcxQuestArmor)
                else
                    return ItemWorkmanship > 0 && (ItemType & (ItemType.WeaponOrCaster | ItemType.Vestements | ItemType.Jewelry)) != 0;
            }
        }

        public int MaxExtraSpellsCount
        {
            get
            {
                if (ExtraSpellsMaxOverride.HasValue)
                    return Math.Max(ExtraSpellsMaxOverride.Value, 0);

                if (IsAcxQuestWeapon)
                    return AcxQuestWeaponTransfers;

                if (IsAcxExplorerGear)
                    return AcxExplorerGearTransfers;

                if (IsAcxQuestArmor)
                    return AcxQuestArmorTransfers;

                var baseSlots = (int)Math.Floor((ItemWorkmanship ?? 0) / 2f);
                if (IsRobe)
                    return baseSlots == 0 ? 1 : (baseSlots * 2);
                return baseSlots;
            }
        }
    }
}
