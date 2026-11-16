using ACE.Entity.Enum;

namespace ACE.Server.WorldObjects
{
    partial class WorldObject
    {
        /// <summary>
        /// Project ACX: an untinkerable quest or named weapon (JJ 2026-11-15: "Maybe allow one imbue on quest weapons and two
        /// spell transfers?", then "That seems perfect to me.. let's make it happen", for "All untinkerable quest/named"): a
        /// melee weapon, launcher or caster with no workmanship that has a wield requirement or spells (vendor and base
        /// weapons have neither). A rule rather than data, so weapons players already own count too. These take one imbue
        /// (RecipeManager.VerifyRequirements; no salvage tinks or slayer stones, which still need workmanship) and
        /// AcxQuestWeaponTransfers spell transfers (WorldObject_SpellTransfer); a failed imbue keeps the weapon unless
        /// acx_quest_imbue_failure_destroys is on. /acxgear benched it: such a weapon comes out about level with a fully
        /// tinked one-handed loot weapon of its wield requirement, the end-game ones ahead.
        /// </summary>
        public bool IsAcxQuestWeapon => (WeenieType == WeenieType.MeleeWeapon || WeenieType == WeenieType.MissileLauncher || WeenieType == WeenieType.Caster)
            && Workmanship == null && (WieldDifficulty != null || (Biota.PropertiesSpellBook?.Count ?? 0) > 0);

        public const int AcxQuestWeaponTransfers = 2;

        /// <summary>
        /// Project ACX: Atlan weapons take no rending imbue (JJ 2026-11-15: "there's no point in fire rend or any element rend
        /// on any atlan", the Minor ones too, which don't cleave)
        /// </summary>
        public bool IsAcxAtlanWeapon => IsAcxQuestWeapon && (Name ?? "").Contains("Atlan");
    }
}
