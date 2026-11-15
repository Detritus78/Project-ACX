using ACE.Entity.Enum;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: kill experience bonus (JJ 2026-11-11, from ClassicACE): surface_bonus_xp (25%) for kills outside
    /// dungeons. (ClassicACE's hot dungeon was ported too and dropped 2026-11-13 by JJ.)
    /// </summary>
    partial class Player
    {
        private long AcxKillXpBonus(long amount, XpType xpType)
        {
            if (xpType != XpType.Kill || amount <= 0 || CurrentLandblock == null || CurrentLandblock.IsDungeon)
                return 0;

            return (long)System.Math.Round(amount * PropertyManager.GetDouble("surface_bonus_xp").Item);
        }
    }
}
