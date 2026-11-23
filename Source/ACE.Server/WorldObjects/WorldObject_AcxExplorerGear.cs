using System.Collections.Generic;

namespace ACE.Server.WorldObjects
{
    partial class WorldObject
    {
        /// <summary>
        /// Project ACX: the Explorer Society's armor, clothing and jewelry (the Lucky Gold Letter rewards: the old Explorer
        /// leather and jewelry, Amateur and Seasoned Explorer, the Society Robe and Hat) take spell transfers (JJ 2026-11-22:
        /// "The explorer gear I want to be able to add atleast two extra spells per piece ... And adjust anyone thats built
        /// the set in game"). They have no workmanship, so they had no transfer slots. A rule rather than data, like
        /// IsAcxQuestWeapon, so pieces players already own count too. Extraction from them stays blocked (no workmanship).
        /// The Explorer weapons are quest weapons and already have AcxQuestWeaponTransfers.
        /// </summary>
        public bool IsAcxExplorerGear => AcxExplorerGearWcids.Contains(WeenieClassId);

        public const int AcxExplorerGearTransfers = 2;

        private static readonly HashSet<uint> AcxExplorerGearWcids = new HashSet<uint>
        {
            // old Explorer (retail 2003)
            8680, 8682, 8686, 8688, 8690, 8696, 8700, 8707, 8711, 25731, 25732, 25733, 25734, 25735,
            // Explorer Society Robe, Society Explorer Hat
            12310, 12309,
            // Amateur Explorer
            45960, 45962, 45964, 45966, 45968, 45970, 45972, 45976, 45978, 45998, 46000, 46002, 46004, 46006, 46008, 46010, 70210,
            // Seasoned Explorer
            45961, 45963, 45965, 45967, 45969, 45971, 45973, 45975, 45977, 45979, 45997, 45999, 46001, 46003, 46005, 46007, 46009,
        };
    }
}
