using System.Collections.Generic;
using System.Linq;

using ACE.Entity;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>
        /// Project ACX: /vault dump empties the selected pack into the open vault (JJ 2026-11-24: drag a pack onto the vault's
        /// items to empty it, "If there isnt room it fills what it can and the rest stays in the bag"; AC:Unreal refuses any
        /// pack dropped on an item list or another pack, "cannot place container in item list", so a command instead, "yes
        /// temporary"). Only the pack selected (last appraised) in your own inventory, only into the vault you have open.
        /// Salvage goes to a Salvage Barrel in the vault first; everything else to the vault's main grid, then its packs. What
        /// doesn't fit, or can't go in (attuned items, packs), stays in your pack.
        /// </summary>
        public string AcxVaultDump()
        {
            var vault = CurrentLandblock?.GetObject(LastOpenedContainerId) as ACE.Server.WorldObjects.Container;
            if (vault == null || !AcxVaults.IsVault(vault))
                return "Open a vault first.";

            var selected = RequestedAppraisalTarget is uint id ? FindObject(id, SearchLocations.MyInventory) : null;
            if (selected is not ACE.Server.WorldObjects.Container pack || selected == this)
                return "Select one of your packs first (click it), then /vault dump.";
            if (pack.Inventory.Count == 0)
                return $"Your {pack.Name} is empty.";

            // where an item may go, in order: Salvage Barrels for salvage, then the vault, then the vault's packs
            List<Container> Places(WorldObject item)
            {
                var packs = vault.Inventory.Values.OfType<Container>().ToList();
                return packs.Where(p => IsPouch(p) && p.AcxAccepts(item))
                    .Append(vault)
                    .Concat(packs.Where(p => !IsPouch(p) && p.AcxAccepts(item)))
                    .ToList();
            }

            static bool IsPouch(Container c) => (c.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.AcxPouchItemTypes) ?? 0) != 0;
            bool HasRoom(Container c) => c.Inventory.Values.Count(i => i is not ACE.Server.WorldObjects.Container) < (c.ItemCapacity ?? 0);

            var moved = 0;
            var left = 0;
            foreach (var item in pack.Inventory.Values.OrderBy(i => i.PlacementPosition ?? 0).ToList())
            {
                var place = item is ACE.Server.WorldObjects.Container || item.IsAttunedOrContainsAttuned ? null : Places(item).FirstOrDefault(HasRoom);
                if (place != null && DoHandleActionPutItemInContainer(item, this, false, place, vault, 0))
                    moved++;
                else
                    left++;
            }

            return left == 0 ? $"Moved {moved} items into {vault.Name}." : $"Moved {moved} items into {vault.Name}; {left} didn't fit or can't go in.";
        }
    }
}
