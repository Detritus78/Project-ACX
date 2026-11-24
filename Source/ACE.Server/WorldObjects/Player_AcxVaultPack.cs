using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>
        /// Project ACX: emptying a pack into a vault (JJ 2026-11-24: "if I drag it onto a empty pack slot it places the entire
        /// pack in the slot but if I drag it into a object slot like a stored item it just empties the pack so far as there is
        /// room. If there isnt room it fills what it can and the rest stays in the bag"). A pack (or Salvage Barrel) from your
        /// inventory dropped onto a pack inside a vault empties into that pack: every item that fits and that the pack takes
        /// (a Salvage Barrel only takes salvage) moves; attuned items and the rest stay in your pack, which stays with you.
        /// Dropped on the vault itself the pack goes in whole, as before (pending: the client's placement for a drop on the
        /// vault's item grid, logged here, to empty there too).
        /// </summary>
        private bool AcxTryEmptyPackIntoVault(uint itemGuid, uint containerGuid, int placement)
        {
            if (FindObject(itemGuid, SearchLocations.MyInventory) is not ACE.Server.WorldObjects.Container pack || pack.Inventory.Count == 0)
                return false;

            var target = FindObject(containerGuid, SearchLocations.Landblock | SearchLocations.LastUsedContainer, out _, out var targetRoot, out _) as ACE.Server.WorldObjects.Container;
            var vault = targetRoot ?? target;
            if (target == null || vault == null || !AcxVaults.IsVault(vault) || vault.Guid != LastOpenedContainerId)
                return false;   // only into the vault you have open

            log.Info($"[VAULTPACK] {Name} dropped {pack.Name} on {(target == vault ? "the vault" : target.Name)} ({vault.Name}), placement {placement}");

            if (target == vault)
                return false;   // the whole pack goes into a bag slot

            if (IsBusy || Teleporting)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YoureTooBusy));
                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, itemGuid));
                return true;
            }

            var moved = 0;
            var left = 0;
            foreach (var item in pack.Inventory.Values.OrderBy(i => i.PlacementPosition ?? 0).ToList())
            {
                var full = target.Inventory.Count(i => i.Value is not ACE.Server.WorldObjects.Container) >= (target.ItemCapacity ?? 0);
                if (full || item is ACE.Server.WorldObjects.Container || item.IsAttunedOrContainsAttuned || !target.AcxAccepts(item))
                {
                    left++;
                    continue;
                }
                if (DoHandleActionPutItemInContainer(item, this, false, target, vault, 0))
                    moved++;
                else
                    left++;
            }

            // the pack itself never moves
            Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, itemGuid));
            Session.Network.EnqueueSend(new GameMessageSystemChat(left == 0 ? $"Moved {moved} items into {target.Name}." : $"Moved {moved} items into {target.Name}; {left} didn't fit or can't go in.", ChatMessageType.Broadcast));
            return true;
        }
    }
}
