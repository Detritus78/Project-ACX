using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameEvent.Events;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>
        /// The Town Network portal, rerouted to Concordia's arrival point (acx/Content/concordia/10-concordia-portals.sql)
        /// </summary>
        private const uint AcxTownNetworkPortalWcid = 42852;

        /// <summary>
        /// Project ACX: the Eternal Concordia Portal Gem recalls its user to Concordia, as the recall runes do (JJ
        /// 2026-11-15: "concordia portal gem summons a portal and it isnt a recall"): to the Town Network portal's
        /// destination, with a recall spell's checks and its 2 second fade (HandleCastSpell_PortalRecall)
        /// </summary>
        public void AcxConcordiaRecall()
        {
            if (IsOlthoiPlayer)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.OlthoiCanOnlyRecallToLifestone));
                return;
            }

            if (PKTimerActive)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveBeenInPKBattleTooRecently));
                return;
            }

            var destination = DatabaseManager.World.GetCachedWeenie(AcxTownNetworkPortalWcid)?.GetPosition(PositionType.Destination);
            if (destination == null)
                return;

            var recall = new ActionChain();
            recall.AddAction(this, () => DoPreTeleportHide());
            recall.AddDelaySeconds(2.0f);
            recall.AddAction(this, () =>
            {
                var teleportDest = new Position(destination);
                AdjustDungeon(teleportDest);

                Teleport(teleportDest);
            });
            recall.EnqueueChain();
        }
    }
}
