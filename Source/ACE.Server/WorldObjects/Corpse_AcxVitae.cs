using System;
using System.Linq;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: a player's corpse stays while its owner has vitae, then gives way to a permanent ethereal
    /// "Here lies" headstone at the spot (JJ 2026-11-11). Coins still on the corpse are lost with it. Only a death to a
    /// monster leaves a headstone, at most one per character per hour (Player_AcxVitae marks the corpse); other corpses
    /// just go. The owner gets half the death's vitae back the first time they open it.
    /// </summary>
    partial class Corpse
    {
        public const uint AcxHeadstoneWcid = 900620;

        /// <summary>
        /// Called from Decay: true while the corpse waits for its owner's vitae to go. When it has gone, a marked corpse
        /// leaves its headstone and the corpse rots straight away.
        /// </summary>
        public bool AcxHeldByVitae()
        {
            if (!(GetProperty(PropertyBool.AcxVitaeCorpse) ?? false) || VictimId == null)
                return false;

            if (AcxOwnerHasVitae(VictimId.Value))
                return true;

            RemoveProperty(PropertyBool.AcxVitaeCorpse);

            // whatever is still on it (coins) is lost, rather than spilled on the ground as a rotting player corpse does
            foreach (var guid in Inventory.Keys.ToList())
                if (TryRemoveFromInventory(guid, out var item))
                    item.Destroy();

            if (GetProperty(PropertyBool.AcxHeadstoneCorpse) ?? false)
            {
                RemoveProperty(PropertyBool.AcxHeadstoneCorpse);
                AcxPlaceHeadstone();
            }
            TimeToRot = 0;
            return false;
        }

        private static bool AcxOwnerHasVitae(uint guid)
        {
            var owner = PlayerManager.FindByGuid(guid, out var isOnline);
            if (owner == null)
                return false;
            if (isOnline && owner is Player player)
                return player.HasVitae;
            var registry = (owner as OfflinePlayer)?.Biota.PropertiesEnchantmentRegistry;
            return registry != null && registry.Any(e => e.SpellId == (int)SpellId.Vitae);
        }

        private void AcxPlaceHeadstone()
        {
            var stone = WorldObjectFactory.CreateNewWorldObject(AcxHeadstoneWcid);
            if (stone == null || Location == null)
                return;

            var owner = Name.StartsWith("Corpse of ") ? Name["Corpse of ".Length..] : Name;
            var died = Time.GetDateTimeFromTimestamp(CreationTimestamp ?? Time.GetUnixTime()).ToString("MMMM d, yyyy");
            stone.Name = $"Here lies {owner}";
            stone.LongDesc = $"Here lies {owner}, fallen {died}. {LongDesc}";
            stone.Location = new Position(Location);

            // in a town (Content/invasions/towns.json) it fades after a while, so towns don't fill up with them (JJ 2026-11-13)
            var inTown = AcxInvasions.TownAt(Location) != null;
            stone.TimeToRot = inTown ? PropertyManager.GetLong("acx_town_headstone_minutes").Item * 60 : -1;

            if (!stone.EnterWorld())
            {
                stone.Destroy();
                return;
            }
            if (!inTown)
                stone.SaveBiotaToDatabase();
        }

        /// <summary>
        /// Called from Open: the owner's first look gives back half of this death's vitae
        /// </summary>
        public void AcxOnOwnerOpen(Player player)
        {
            var points = GetProperty(PropertyInt.AcxCorpseVitae) ?? 0;
            if (points <= 0)
                return;

            RemoveProperty(PropertyInt.AcxCorpseVitae);
            var refund = points / 2;
            if (refund > 0 && player.HasVitae)
                player.AcxReduceVitae(refund, "You recover your body");
        }
    }
}
