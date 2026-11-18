using ACE.Entity;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>
        /// Project ACX: no player is stranded in portal space across logins (JJ 2026-11-18: "yeah build both"). AC:Unreal
        /// disconnects a player it can't place at a destination ("world entry failed: The destination geometry could not finish
        /// loading"; the Dungeon Maggreth contract entrance, 014F01B9, did this on 2026-11-17), and the player was saved at that
        /// spot, so every login failed the same way until an admin moved the character. A player who leaves while a teleport
        /// hasn't finished is now saved where they last stood before it; if it was a login, at their lifestone. A quick drop
        /// (under AcxPortalSafetySeconds since the teleport began) keeps the destination: AC:Unreal gives up after 45 seconds.
        /// </summary>
        private Position acxTeleportFrom;

        private const double AcxPortalSafetySeconds = 20;

        /// <summary>
        /// Teleport, as it starts: remember where the player stood, fully arrived (not an earlier teleport's destination)
        /// </summary>
        private void AcxOnTeleportStart()
        {
            if (!Teleporting && Location != null)
                acxTeleportFrom = new Position(Location);
        }

        /// <summary>
        /// OnTeleportComplete: the player materialized at the destination
        /// </summary>
        private void AcxOnTeleportComplete()
        {
            acxTeleportFrom = null;
            AcxEntranceTest.OnArrived(this);
        }

        /// <summary>
        /// FinalizeLogout, before the save: still in portal space, so back to where they came from
        /// </summary>
        private void AcxPortalSafetyAtLogout()
        {
            if (!Teleporting || Common.Time.GetUnixTime() - (LastTeleportStartTimestamp ?? 0) < AcxPortalSafetySeconds)
                return;

            AcxEntranceTest.OnStuck(this);

            var back = IsDead ? null : acxTeleportFrom;
            back ??= Sanctuary ?? Instantiation;
            if (back == null)
                return;

            log.Warn($"[ACX] {Name} left while still in portal space at {Location?.ToLOCString()}; saved at {back.ToLOCString()} instead");
            Location = new Position(back);
        }
    }
}
