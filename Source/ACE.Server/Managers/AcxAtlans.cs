using System;

using ACE.Common;
using ACE.Database;
using ACE.Entity.Enum;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: Atlan revamp (JJ 2026-11-12, acx/Content/atlans/10-atlan-revamp.sql). Silencia's Magma Golem forges each
    /// Atlan with a random flavor: Armor Rending, Critical Strike or Crippling Blow (wands only the last two, since Armor
    /// Rending does nothing for spells). Stones swap the weapon for a new item, so the flavor is carried across.
    /// </summary>
    public static class AcxAtlans
    {
        /// <summary>
        /// The plain Atlans the golem forges: axe, mace, sword, two-handed sword, wand, bow
        /// </summary>
        private static readonly uint[] PlainAtlans = { 46037, 46071, 46088, 46105, 46122, 46139 };

        private const ImbuedEffectType Flavors = ImbuedEffectType.ArmorRending | ImbuedEffectType.CriticalStrike | ImbuedEffectType.CripplingBlow;

        private static readonly ImbuedEffectType[] WeaponFlavors = { ImbuedEffectType.ArmorRending, ImbuedEffectType.CriticalStrike, ImbuedEffectType.CripplingBlow };
        private static readonly ImbuedEffectType[] CasterFlavors = { ImbuedEffectType.CriticalStrike, ImbuedEffectType.CripplingBlow };

        /// <summary>
        /// The current Atlan line (plain, Minor, Major, Blackfire and Enhanced versions), not the older Fine/Superior/Peerless ones
        /// </summary>
        public static bool IsAtlan(WorldObject wo)
        {
            var wcid = wo.WeenieClassId;
            return ((wcid >= 46037 && wcid <= 46141) || (wcid >= 71262 && wcid <= 71268)) && wo.Name.Contains("Atlan");
        }

        /// <summary>
        /// Called when any new object is created: a plain Atlan without a flavor gets one
        /// </summary>
        public static void OnCreate(WorldObject wo)
        {
            if (wo == null || Array.IndexOf(PlainAtlans, wo.WeenieClassId) < 0 || (wo.ImbuedEffect & Flavors) != 0)
                return;

            var flavors = wo.WeenieType == WeenieType.Caster ? CasterFlavors : WeaponFlavors;
            SetFlavor(wo, flavors[ThreadSafeRandom.Next(0, flavors.Length - 1)]);
        }

        /// <summary>
        /// Called when a recipe turns one item into another: an Atlan keeps its flavor through every stone change
        /// </summary>
        public static bool CarryOver(WorldObject from, WorldObject to)
        {
            if (from == null || to == null || !IsAtlan(from) || !IsAtlan(to))
                return false;

            var flavor = from.ImbuedEffect & Flavors;
            if (flavor == 0)
                return false;

            SetFlavor(to, flavor);
            return true;
        }

        private static void SetFlavor(WorldObject wo, ImbuedEffectType flavor)
        {
            wo.ImbuedEffect = (wo.ImbuedEffect & ~Flavors) | flavor;
            if (RecipeManager.IconUnderlay.TryGetValue(flavor, out var underlay))
                wo.IconUnderlayId = underlay;
        }
    }
}
