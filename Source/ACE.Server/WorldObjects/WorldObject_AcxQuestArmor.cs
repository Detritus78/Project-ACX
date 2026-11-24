using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;

namespace ACE.Server.WorldObjects
{
    partial class WorldObject
    {
        /// <summary>
        /// Project ACX: untinkerable quest armor and clothing take spell transfers, like quest weapons (JJ 2026-11-24, on the
        /// gromnie hide armor: "If we didnt do anything unique to quest armor then that armor probably isn't any good", then
        /// "yes lets do it" for two transfers on every quest armor piece, armor level unchanged). Quest armor is armor or
        /// clothing with no workmanship that has a wield requirement or spells, the same test as IsAcxQuestWeapon: vendor and
        /// base armor have neither, and anything a vendor sells is left out (the mage-shop robes carry spells; buyable isn't a
        /// quest reward). Cloaks are left out (their own proc system). The plain gromnie hide pieces have neither a
        /// requirement nor spells, so they are listed by wcid. A rule rather than data, so pieces players already own count
        /// too. Extraction from them stays blocked (no workmanship).
        /// </summary>
        public bool IsAcxQuestArmor => Workmanship == null
            && (ItemType & ItemType.Vestements) != 0
            && !ACE.Server.Entity.Cloak.IsCloak(this)
            && (WieldDifficulty != null || (Biota.PropertiesSpellBook?.Count ?? 0) > 0 || AcxQuestArmorWcids.Contains(WeenieClassId))
            && !AcxVendorSold.Value.Contains(WeenieClassId);

        public const int AcxQuestArmorTransfers = 2;

        /// <summary>
        /// Every wcid on a vendor's shop list, read once from the world database
        /// </summary>
        private static readonly Lazy<HashSet<uint>> AcxVendorSold = new Lazy<HashSet<uint>>(() =>
        {
            using (var ctx = new ACE.Database.Models.World.WorldDbContext())
                return ctx.WeeniePropertiesCreateList.Where(c => c.DestinationType == (sbyte)DestinationType.Shop).Select(c => c.WeenieClassId).Distinct().ToHashSet();
        });

        private static readonly HashSet<uint> AcxQuestArmorWcids = new HashSet<uint>
        {
            // Gromnie Hide Boots, Gauntlets, Helmet, Coat, Leggings (the Leather Crafters' hide turn-ins)
            28142, 28148, 28149, 28146, 28162,
        };
    }
}
