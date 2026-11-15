using System;

using ACE.DatLoader;
using ACE.Entity.Enum;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX test kit: /acxlevel raises a character to a level, so each tier can be tested at its level
    /// (ACX Foundry's level buttons). The experience is left unassigned, for the tester to spend (JJ 2026-11-12).
    /// </summary>
    partial class Player
    {
        public string AcxLevelTo(int target)
        {
            var levels = DatManager.PortalDat.XpTable.CharacterLevelXPList;
            target = Math.Clamp(target, 1, levels.Count - 1);

            if (target <= (Level ?? 1))
                return $"You're already level {Level}. Levels only go up: use a new character to test a lower tier.";

            var needed = (long)levels[target] - (TotalExperience ?? 0);
            if (needed > 0)
                GrantXP(needed, XpType.Admin, ShareType.None);

            return $"You're now level {Level}. The experience is unassigned: spend it on your skills and attributes.";
        }
    }
}
