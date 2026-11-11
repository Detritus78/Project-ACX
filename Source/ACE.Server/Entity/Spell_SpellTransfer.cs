using System.Collections.Generic;

using ACE.Entity.Enum;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Project ACX: spell classification used by spell extraction / transfer scrolls (ported from ClassicACE)
    /// </summary>
    partial class Spell
    {
        private static readonly List<SpellCategory> cantripCategories = new List<SpellCategory>()
        {
            SpellCategory.GreatVitalityRaising,
            SpellCategory.PoorVitalityLowering,
            SpellCategory.GreatVigorRaising,
            SpellCategory.PoorVigorLowering,
            SpellCategory.GreaterIntellectRaising,
            SpellCategory.LessorIntellectLowering,
            SpellCategory.ExtraManaConversionBonus
        };

        public bool IsCantrip
        {
            get
            {
                if (cantripCategories.Contains(_spellBase.Category))
                    return true;
                return _spellBase.Desc.Contains("Additional spells can be layered over this.");
            }
        }

        /// <summary>
        /// Spells that only make sense on a weapon or caster
        /// </summary>
        public bool IsWeaponTargetType => (Category >= SpellCategory.AttackModRaising && Category <= SpellCategory.WeaponTimeLowering) || (Category >= SpellCategory.ManaConversionModLowering && Category <= SpellCategory.ManaConversionModRaising) || Category == SpellCategory.SpellDamageRaising;
    }
}
