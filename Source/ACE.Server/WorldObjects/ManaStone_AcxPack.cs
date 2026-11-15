using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: one mana stone recharges every item in a pack (JJ 2026-11-11, ported from ClassicACE). The stone's
    /// mana is shared out evenly between the items in that pack that need it, the same way retail shares it between
    /// equipped items. Mana stones may target packs (acx/Content/crafting/10-mana-stone-packs.sql).
    /// </summary>
    partial class ManaStone
    {
        private WeenieError AcxChargePack(Player player, Container pack)
        {
            var needing = pack.Inventory.Values.Where(i => i.ItemCurMana.HasValue && i.ItemMaxMana.HasValue && i.ItemCurMana < i.ItemMaxMana).ToList();
            var given = new Dictionary<WorldObject, int>();

            while (ItemCurMana > 0)
            {
                var stillNeeding = needing.Where(i => i.ItemCurMana < i.ItemMaxMana).ToList();
                if (stillNeeding.Count == 0)
                    break;

                var ration = Math.Max(ItemCurMana.Value / stillNeeding.Count, 1);
                foreach (var item in stillNeeding)
                {
                    var topOff = item.ItemMaxMana.Value - item.ItemCurMana.Value;
                    var amount = Math.Min(ration, topOff);
                    ItemCurMana -= amount;

                    if (player.LumAugItemManaGain != 0)
                    {
                        amount = (int)Math.Round(amount * Creature.GetPositiveRatingMod(player.LumAugItemManaGain * 5));
                        if (amount > topOff)
                        {
                            ItemCurMana += amount - topOff;
                            amount = topOff;
                        }
                    }

                    item.ItemCurMana += amount;
                    given[item] = (given.TryGetValue(item, out var g) ? g : 0) + amount;

                    if (ItemCurMana <= 0)
                        break;
                }
            }

            if (given.Count == 0)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Nothing in the {pack.Name} needs mana.", ChatMessageType.Broadcast));
                return WeenieError.ActionCancelled;
            }

            var stillNeeded = needing.Sum(i => i.ItemMaxMana.Value - i.ItemCurMana.Value);
            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {Name} gives {given.Values.Sum():N0} points of mana to the following items in the {pack.Name}: {string.Join(", ", given.Keys.Select(i => i.Name))}."
                + (stillNeeded > 0 ? $"\nThey need {stillNeeded:N0} more mana to be fully charged." : "\nThey are fully charged."), ChatMessageType.Broadcast));

            if (!DoDestroyDiceRoll(player) && !UnlimitedUse)
            {
                ItemCurMana = null;
                SetUiEffect(player, ACE.Entity.Enum.UiEffects.Undef);
            }
            if (UnlimitedUse && ItemMaxMana.HasValue)
                ItemCurMana = ItemMaxMana;

            return WeenieError.None;
        }
    }
}
