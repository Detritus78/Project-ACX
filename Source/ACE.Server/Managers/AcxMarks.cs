using System;
using System.Collections.Generic;

using log4net;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Factories;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: the ACX Mark (acx_mark_wcid, Content/invasions/10-currency.sql), the tradeable currency for coin-only goods
    /// (JJ 2026-11-13). Each event gives one: an exploration marker, a finished contract or job, and an elite invader's corpse,
    /// which carries a stack of acx_invasion_corpse_marks from which anyone who reaches it may take one.
    /// </summary>
    public static class AcxMarks
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static uint Wcid => (uint)PropertyManager.GetLong("acx_mark_wcid").Item;

        public static bool IsMark(WorldObject wo) => wo != null && Wcid != 0 && wo.WeenieClassId == Wcid;

        public static WorldObject Create(int amount)
        {
            if (Wcid == 0 || amount <= 0)
                return null;
            var marks = WorldObjectFactory.CreateNewWorldObject(Wcid);
            if (marks == null)
            {
                log.Warn($"AcxMarks: weenie {Wcid} (acx_mark_wcid) is missing");
                return null;
            }
            marks.SetStackSize(Math.Min(amount, Math.Max(1, (int)(marks.MaxStackSize ?? 1))));
            return marks;
        }

        /// <summary>
        /// Gives the player one ACX Mark (or more), at their feet if their pack is full. With forWhat, says so in chat;
        /// without, the caller's own message mentions it. source names it in the economy log (Player_AcxEconomyLog).
        /// </summary>
        public static void Give(Player player, string source, string forWhat = null, int amount = 1)
        {
            var marks = Create(amount);
            if (marks == null)
                return;

            var name = amount == 1 ? marks.Name : marks.GetPluralName();
            if (!player.TryCreateInInventoryWithNetworking(marks))
            {
                marks.Location = new Position(player.Location);
                if (!marks.EnterWorld())
                    marks.Destroy();
            }
            if (forWhat != null)
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You receive {(amount == 1 ? "an" : amount.ToString())} {name} for {forWhat}.", ChatMessageType.Broadcast));
            player.AcxEconMarksEarned(source, amount);
        }

        /// <summary>
        /// Called from the player's inventory actions before an item leaves an open corpse: if it is the ACX Mark stack on an
        /// elite's corpse, the player gets one Mark of their own (once per corpse) and the stack shrinks by one; the move itself
        /// is cancelled. Returns true when it handled the action.
        /// </summary>
        public static bool TryTakeFromCorpse(Player player, uint itemGuid)
        {
            if (player.LastOpenedContainerId == ObjectGuid.Invalid)
                return false;
            if (player.CurrentLandblock?.GetObject(player.LastOpenedContainerId) is not Corpse { AcxMarkTakers: not null } corpse)
                return false;
            if (!corpse.Inventory.TryGetValue(new ObjectGuid(itemGuid), out var stack) || !IsMark(stack))
                return false;

            var session = player.Session;
            session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(session, itemGuid));

            if (!corpse.AcxMarkTakers.Add(player.Guid.Full))
            {
                session.Network.EnqueueSend(new GameEventCommunicationTransientString(session, "Really? You already took yours. Want everyone else's too?"));
                return true;
            }

            var mine = Create(1);
            if (mine == null || !player.TryCreateInInventoryWithNetworking(mine))
            {
                mine?.Destroy();
                corpse.AcxMarkTakers.Remove(player.Guid.Full);
                session.Network.EnqueueSend(new GameEventCommunicationTransientString(session, $"You have no room for the {stack.Name}."));
                return true;
            }
            player.AcxEconMarksEarned("invasion", 1);

            if ((stack.StackSize ?? 1) > 1)
            {
                stack.SetStackSize(stack.StackSize - 1);
                session.Network.EnqueueSend(new GameMessageSetStackSize(stack));
            }
            else if (corpse.TryRemoveFromInventory(stack.Guid, out _))
            {
                session.Network.EnqueueSend(new GameMessageInventoryRemoveObject(stack));
                stack.Destroy();
            }
            return true;
        }
    }
}
