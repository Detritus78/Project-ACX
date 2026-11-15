using System;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: salvage sacks, the gambling halls' prizes in place of their junk (toys, pet wings, candy, poison; JJ
    /// 2026-11-13: "getting a bag of salvage makes me more happy than getting a picture frame"). Used, a sack becomes a bag
    /// of one random salvage (the contract list): AcxSalvageSack (int 30019) 1 = Small (Low-Stakes): 25-75 units at
    /// workmanship 3-6; 2 = plain (Mid-Stakes): 50-100 at 5-8; 3 = Heavy (High-Stakes): a full 100 at 7-10.
    /// Content: acx/Content/invasions/40-gambling.sql.
    /// </summary>
    public static class AcxSalvageSack
    {
        public static bool IsSack(WorldObject wo) => (wo.GetProperty(PropertyInt.AcxSalvageSack) ?? 0) > 0;

        public static void Open(WorldObject sack, Player player)
        {
            var tier = Math.Clamp(sack.GetProperty(PropertyInt.AcxSalvageSack) ?? 1, 1, 3);
            var (minUnits, maxUnits, minWork, maxWork) = tier switch
            {
                1 => (25, 75, 3, 6),
                2 => (50, 100, 5, 8),
                _ => (100, 100, 7, 10),
            };

            var salvage = WorldObjectFactory.CreateNewWorldObject(AcxContracts.RandomSalvageWcid());
            if (salvage == null)
                return;
            var units = ThreadSafeRandom.Next(minUnits, maxUnits);
            salvage.Structure = (ushort)units;
            salvage.MaxStructure = 100;
            salvage.ItemWorkmanship = ThreadSafeRandom.Next(minWork, maxWork);
            salvage.SetProperty(PropertyInt.NumItemsInMaterial, 1);

            if (!player.CanAddToInventory(salvage))
            {
                salvage.Destroy();
                player.SendTransientError("Make some room in your pack first.");
                return;
            }
            if (!player.TryConsumeFromInventoryWithNetworking(sack, 1))
            {
                salvage.Destroy();
                return;
            }
            player.TryCreateInInventoryWithNetworking(salvage);
            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You empty the {sack.Name}: {salvage.Name}, {units} units, workmanship {salvage.ItemWorkmanship}.", ChatMessageType.Broadcast));
        }
    }
}
