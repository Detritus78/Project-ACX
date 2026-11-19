using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Network.GameEvent.Events;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: /sort (JJ 2026-11-18: "Could we have the server just sort items by item type? i.e. all pyreals next to
    /// each other all rings next to each other all weapons by type next to each other all gems next to each other. that
    /// would make it easier to sell and see what you have."). AC:Unreal's plugin API can't put an item into the main pack or
    /// into a chosen slot, so a MudSort-style plugin can't sort a pack in place; the server owns the order (each item's
    /// PlacementPosition) and already tells the client "this item is in slot N" on every drag, so it sorts and sends that.
    /// The main pack and each side pack are sorted where they are: nothing changes packs, worn items and the order of the
    /// side packs themselves are left alone.
    /// /sort burden, /sort value and /sort damage (JJ 2026-11-19) order each pack by weight or value (largest first), or
    /// put the weapons first, strongest first: melee by average hit (damage x (1 - variance / 2)), thrown and bombs by that x their modifier (Potency), launchers
    /// by elemental bonus x damage modifier (no bonus: last, by modifier), casters by elemental damage %, with the rest after in the usual
    /// order. Spells on a weapon (Blood Drinker) don't count: they only work while it's wielded.
    /// </summary>
    partial class Player
    {
        /// <summary>
        /// Sorts every pack in place and returns the one line to tell the player
        /// </summary>
        public enum AcxSortMode { Type, Burden, Value, Damage }

        public string AcxSortPacks(AcxSortMode mode = AcxSortMode.Type)
        {
            if (IsTrading)
                return "Close the trade window first.";
            if (IsBusy || Teleporting)
                return "You're busy; try again in a moment.";

            var packs = new List<Container> { this };
            packs.AddRange(Inventory.Values.OfType<Container>().OrderBy(c => c.PlacementPosition));

            var sorted = 0;
            foreach (var pack in packs)
                if (AcxSortPack(pack, mode))
                    sorted++;

            return sorted == 0 ? "Your packs are already in order." : "Packs sorted.";
        }

        private bool AcxSortPack(Container pack, AcxSortMode mode)
        {
            var current = pack.Inventory.Values.Where(i => !i.UseBackpackSlot).OrderBy(i => i.PlacementPosition).ToList();
            IOrderedEnumerable<WorldObject> first = mode switch
            {
                AcxSortMode.Burden => current.OrderByDescending(i => i.EncumbranceVal ?? 0),
                AcxSortMode.Value => current.OrderByDescending(i => i.Value ?? 0),
                AcxSortMode.Damage => current.OrderBy(i => AcxIsWeapon(i) ? 0 : 1).ThenBy(i => AcxIsWeapon(i) ? AcxSortGroup(i) : 0).ThenByDescending(AcxDamageScore),
                _ => current.OrderBy(_ => 0),
            };
            var sorted = first
                .ThenBy(AcxSortGroup)
                .ThenBy(AcxSortKind)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => (int)(i.MaterialType ?? 0))
                .ThenByDescending(i => i.StackSize ?? 1)
                .ThenByDescending(i => i.Value ?? 0)
                .ThenBy(i => i.Guid.Full)
                .ToList();

            if (current.SequenceEqual(sorted))
                return false;

            // one "item is in slot i" per item, first slot first: the client takes each out and puts it in at i, as on a
            // drag, so after the last one its order is ours even if its own slot numbers had drifted
            for (var i = 0; i < sorted.Count; i++)
            {
                sorted[i].PlacementPosition = i;
                Session.Network.EnqueueSend(new GameEventItemServerSaysContainId(Session, sorted[i], pack));
            }
            return true;
        }

        private static bool AcxIsWeapon(WorldObject item) =>
            (item.ItemType & (ItemType.MeleeWeapon | ItemType.MissileWeapon | ItemType.Caster)) != 0 && item.WeenieType != WeenieType.Ammunition;

        /// <summary>
        /// /sort damage: how hard a weapon hits, comparable within its kind (melee, missile, caster)
        /// </summary>
        private static double AcxDamageScore(WorldObject item)
        {
            if (!AcxIsWeapon(item))
                return 0;
            if (Factories.Tables.Wcids.AcxBombWcids.IsHealingBomb(item.WeenieClassId))
                return -1;   // no damage: after the other thrown weapons
            if ((item.Damage ?? 0) > 0)
            {
                // the average hit; thrown weapons and Alchemist's bombs also carry a damage modifier (a bomb's Potency) that
                // scales it (JJ: "and alchemy?")
                var hit = (item.Damage ?? 0) * (1 - (item.DamageVariance ?? 0) / 2);
                return (item.ItemType & ItemType.MissileWeapon) != 0 ? hit * (item.DamageMod ?? 1) : hit;
            }
            if ((item.ItemType & ItemType.MissileWeapon) != 0)
            {
                // launchers: the elemental bonus as the damage modifier boosts it; without one, by modifier alone, after the
                // elemental ones (JJ: bonus and modifier both, "don't consider any ammo", "bows without elemental modifier
                // would always be lower anyways")
                var mod = item.DamageMod ?? 1;
                return (item.ElementalDamageBonus ?? 0) * mod + mod / 1000;
            }
            return item.ElementalDamageMod ?? 1;   // casters
        }

        /// <summary>
        /// The order of the groups: money, weapons, armor and clothing, jewelry, gems, food and kits, components and scrolls,
        /// salvage and tools, keys, everything else
        /// </summary>
        private static int AcxSortGroup(WorldObject item)
        {
            var type = item.ItemType;

            if (item.WeenieType == WeenieType.Coin || (type & ItemType.Money) != 0) return 0;
            if ((type & ItemType.PromissoryNote) != 0) return 1;

            if ((type & ItemType.MeleeWeapon) != 0) return 10;
            if ((type & ItemType.MissileWeapon) != 0) return item.WeenieType == WeenieType.Ammunition ? 12 : 11;
            if ((type & ItemType.Caster) != 0) return 13;

            if ((type & ItemType.Armor) != 0) return 20;
            if ((type & ItemType.Clothing) != 0) return 21;

            if ((type & ItemType.Jewelry) != 0) return 30;
            if ((type & ItemType.Gem) != 0) return 40;

            if ((type & ItemType.Food) != 0) return 50;
            if (item.WeenieType == WeenieType.Healer) return 51;
            if ((type & ItemType.ManaStone) != 0) return 52;

            if ((type & ItemType.SpellComponents) != 0) return 60;
            if (item.WeenieType == WeenieType.Scroll) return 61;

            if ((type & ItemType.TinkeringMaterial) != 0) return 70;
            if ((type & (ItemType.TinkeringTool | ItemType.CraftCookingBase | ItemType.CraftAlchemyBase | ItemType.CraftFletchingBase
                | ItemType.CraftAlchemyIntermediate | ItemType.CraftFletchingIntermediate)) != 0 || item.WeenieType == WeenieType.CraftTool) return 71;

            if ((type & ItemType.Key) != 0) return 80;
            if (item.WeenieType == WeenieType.Lockpick) return 81;

            return 90;
        }

        /// <summary>
        /// The order inside a group, before the name: trade notes by what one is worth (highest first), weapons by weapon
        /// type (sword, axe, mace ...), armor, clothing and jewelry by where they're worn, salvage by material
        /// </summary>
        private static long AcxSortKind(WorldObject item)
        {
            switch (AcxSortGroup(item))
            {
                case 0:
                case 1:
                    return -((item.Value ?? 0) / Math.Max(1, item.StackSize ?? 1));
                case 10:
                case 11:
                case 13:
                    return (long)item.W_WeaponType;
                case 20:
                case 21:
                case 30:
                    var worn = (uint)(item.ValidLocations ?? 0);
                    return worn == 0 ? long.MaxValue : worn & (~worn + 1);   // its first slot (lowest bit)
                case 70:
                    return (long)(item.MaterialType ?? 0);
                default:
                    return 0;
            }
        }
    }
}
