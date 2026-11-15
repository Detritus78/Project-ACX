using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: revive scrolls I, II and III (JJ 2026-11-11). Used on the corpse of someone waiting beside it as a
    /// ghost (WorldObjects/Player_AcxGhost.cs). I is instant and brings them back with 1 health and no stamina or mana;
    /// II is a 10 second kneeling cast, back with half of everything; III a 20 second cast, back with full vitals. Every
    /// revive gives back the vitae that death cost. During a cast the reviver must hold still: moving cancels it and keeps
    /// the scroll; damage doesn't interrupt. The scroll is used up only when the revive completes. One cast at a time.
    /// Sold by the Life Magic scriveners in the Direlands towns and Arwic; they resell for 1 pyreal
    /// (Player_AcxCommerce). Reviving yourself with a second character is allowed (JJ).
    /// Scrolls: acx/Content/death/10-revive-scrolls.sql (900600-900602, AcxReviveTier 1-3).
    /// </summary>
    public static class AcxRevive
    {
        private static readonly double[] CastSeconds = { 0, 10, 20 };

        private const float HoldStillRange = 1.0f;

        public static readonly uint[] ScrollWcids = { 900600, 900601, 900602 };

        /// <summary>
        /// The Life Magic scriveners that sell the scrolls: the Grand Masters of the Direlands towns and Arwic's scrivener
        /// </summary>
        private static readonly (uint Wcid, ushort Landblock)[] Sellers =
        {
            (20220, 0x1134),   // Ayan Baqur
            (20220, 0x2581),   // Fort Tethana
            (20220, 0x2B12),   // Candeth Keep
            (20220, 0x3F31),   // Wai Jhou
            (20222, 0xC6A9),   // Arwic
        };

        public static int Tier(WorldObject item) => item.GetProperty(PropertyInt.AcxReviveTier) ?? 0;

        public static bool IsScroll(WorldObject item) => Tier(item) > 0;

        /// <summary>
        /// Called from Vendor.LoadInventory: extra items this vendor sells
        /// </summary>
        public static IEnumerable<uint> ShopItems(Vendor vendor)
        {
            var lb = (ushort)((vendor.Location?.LandblockId.Raw ?? 0) >> 16);
            return Sellers.Any(s => s.Wcid == vendor.WeenieClassId && s.Landblock == lb) ? ScrollWcids : Enumerable.Empty<uint>();
        }

        /// <summary>
        /// Called from WorldObject.HandleActionUseOnTarget once the reviver has walked to the corpse
        /// </summary>
        public static void Use(Player reviver, WorldObject scroll, WorldObject target)
        {
            var tier = Tier(scroll);
            var corpse = target as Corpse;
            var ghost = corpse?.VictimId != null ? PlayerManager.GetOnlinePlayer(corpse.VictimId.Value) : null;

            string refusal = null;
            if (ghost == null || !ghost.IsAcxGhost || ghost.AcxGhostCorpse != corpse)
                refusal = "A revive scroll works on the body of someone waiting beside it as a ghost.";
            else if (ghost.AcxReviver != null)
                refusal = $"{ghost.Name} is already being revived.";
            else if (reviver.IsBusy || reviver.Teleporting)
                refusal = "You're too busy.";

            if (refusal != null)
            {
                reviver.SendTransientError(refusal);
                reviver.SendUseDoneEvent();
                return;
            }

            var seconds = CastSeconds[tier - 1];
            if (seconds <= 0)
            {
                Finish(reviver, ghost, scroll, tier);
                reviver.SendUseDoneEvent();
                return;
            }

            ghost.AcxReviver = reviver;
            ghost.AcxReviveStarted = Time.GetUnixTime();
            reviver.IsBusy = true;
            reviver.SetCombatMode(CombatMode.NonCombat);
            reviver.EnqueueBroadcastMotion(new Motion(MotionStance.NonCombat, MotionCommand.KneelState));

            reviver.Session.Network.EnqueueSend(new GameMessageSystemChat($"You kneel over {ghost.Name}'s body. Hold still for {seconds:0} seconds: moving cancels the revive (you keep the scroll).", ChatMessageType.Broadcast));
            ghost.Session.Network.EnqueueSend(new GameMessageSystemChat($"{reviver.Name} kneels over your body to revive you ({seconds:0} seconds).", ChatMessageType.Broadcast));
            reviver.SendUseDoneEvent();

            Tick(reviver, ghost, scroll, corpse, tier, new Position(reviver.Location), Time.GetUnixTime() + seconds);
        }

        private static void Tick(Player reviver, Player ghost, WorldObject scroll, Corpse corpse, int tier, Position start, double endTime)
        {
            var chain = new ActionChain();
            chain.AddDelaySeconds(0.5);
            chain.AddAction(reviver, () =>
            {
                string fail = null;
                if (reviver.IsDead || reviver.Teleporting || reviver.IsLoggingOut)
                    fail = "The revive is cancelled.";
                else if (reviver.Location.DistanceTo(start) > HoldStillRange)
                    fail = "You moved, so the revive is cancelled. You keep the scroll.";
                else if (PlayerManager.GetOnlinePlayer(ghost.Guid) != ghost || !ghost.IsAcxGhost || ghost.AcxGhostCorpse != corpse || ghost.AcxReviver != reviver)
                    fail = $"{ghost.Name} is no longer waiting to be revived.";

                if (fail != null)
                {
                    Stop(reviver, ghost);
                    reviver.SendTransientError(fail);
                    if (ghost.AcxReviver == null && ghost.IsAcxGhost)
                        ghost.Session.Network.EnqueueSend(new GameMessageSystemChat($"{reviver.Name}'s revive was cancelled.", ChatMessageType.Broadcast));
                    return;
                }

                if (Time.GetUnixTime() < endTime)
                    Tick(reviver, ghost, scroll, corpse, tier, start, endTime);
                else
                {
                    Stop(reviver, ghost);
                    Finish(reviver, ghost, scroll, tier);
                }
            });
            chain.EnqueueChain();
        }

        /// <summary>
        /// Ends a cast: the reviver stands up and the ghost can be revived again
        /// </summary>
        private static void Stop(Player reviver, Player ghost)
        {
            if (ghost.AcxReviver == reviver)
                ghost.AcxReviver = null;
            reviver.IsBusy = false;
            reviver.EnqueueBroadcastMotion(new Motion(MotionStance.NonCombat, MotionCommand.Ready));
        }

        /// <summary>
        /// The scroll is checked again (it may have been dropped, sold or traded during the cast), used up, and the ghost lives
        /// </summary>
        private static void Finish(Player reviver, Player ghost, WorldObject scroll, int tier)
        {
            if (reviver.FindObject(scroll.Guid.Full, Player.SearchLocations.MyInventory) == null || !reviver.TryConsumeFromInventoryWithNetworking(scroll, 1))
            {
                reviver.SendTransientError("The scroll is gone, so the revive fails.");
                return;
            }

            ghost.AcxRevivedBy(reviver, tier);
            if (reviver != ghost)
                reviver.Session.Network.EnqueueSend(new GameMessageSystemChat($"You bring {ghost.Name} back to life.", ChatMessageType.Broadcast));
        }
    }
}
