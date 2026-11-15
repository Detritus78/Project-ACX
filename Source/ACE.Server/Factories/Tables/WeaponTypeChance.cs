using System;
using System.Collections.Generic;

using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables
{
    public static class WeaponTypeChance
    {
        private static ChanceTable<TreasureWeaponType> T1_T4_Chances = new ChanceTable<TreasureWeaponType>()
        {
            // melee: 84%
            // missile: 12%
            // caster: 4%
            ( TreasureWeaponType.Sword,    0.12f ),
            ( TreasureWeaponType.Mace,     0.12f ),
            ( TreasureWeaponType.Axe,      0.12f ),
            ( TreasureWeaponType.Spear,    0.12f ),
            ( TreasureWeaponType.Unarmed,  0.12f ),
            ( TreasureWeaponType.Staff,    0.12f ),
            ( TreasureWeaponType.Dagger,   0.12f ),
            ( TreasureWeaponType.Bow,      0.04f ),
            ( TreasureWeaponType.Crossbow, 0.04f ),
            ( TreasureWeaponType.Atlatl,   0.04f ),
            ( TreasureWeaponType.Caster,   0.04f ),
        };

        private static ChanceTable<TreasureWeaponType> T5_T6_Chances = new ChanceTable<TreasureWeaponType>()
        {
            // melee: 63%
            // missile: 27%
            // caster: 10%
            ( TreasureWeaponType.Sword,    0.09f ),
            ( TreasureWeaponType.Mace,     0.09f ),
            ( TreasureWeaponType.Axe,      0.09f ),
            ( TreasureWeaponType.Spear,    0.09f ),
            ( TreasureWeaponType.Unarmed,  0.09f ),
            ( TreasureWeaponType.Staff,    0.09f ),
            ( TreasureWeaponType.Dagger,   0.09f ),
            ( TreasureWeaponType.Bow,      0.09f ),
            ( TreasureWeaponType.Crossbow, 0.09f ),
            ( TreasureWeaponType.Atlatl,   0.09f ),
            ( TreasureWeaponType.Caster,   0.10f ),
        };

        // from magloot corpse logs
        // it appears they might have gotten rid of the tier chances
        // Project ACX: Eternal thrown weapons take 6% of weapon drops, as often as atlatls, and every other type drops
        // 6% less often (JJ 2026-11-14: "6% works"); retail's chances below are the originals x 0.94
        private static ChanceTable<TreasureWeaponType> RetailChances = new ChanceTable<TreasureWeaponType>()
        {
            // melee: 59.2% (retail 63%)
            // missile: 24.4% with thrown (retail 20%)
            // two-handed: 9.4% (retail 10%)
            // caster: 6.6% (retail 7%)
            ( TreasureWeaponType.Sword,           0.0846f ),   // 0.09
            ( TreasureWeaponType.Mace,            0.0846f ),   // 0.09
            ( TreasureWeaponType.Axe,             0.0846f ),   // 0.09
            ( TreasureWeaponType.Spear,           0.0846f ),   // 0.09
            ( TreasureWeaponType.Unarmed,         0.0846f ),   // 0.09
            ( TreasureWeaponType.Staff,           0.0846f ),   // 0.09
            ( TreasureWeaponType.Dagger,          0.0846f ),   // 0.09
            ( TreasureWeaponType.Bow,             0.0658f ),   // 0.07
            ( TreasureWeaponType.Crossbow,        0.0658f ),   // 0.07
            ( TreasureWeaponType.Atlatl,          0.0564f ),   // 0.06
            ( TreasureWeaponType.Caster,          0.0658f ),   // 0.07
            ( TreasureWeaponType.TwoHandedWeapon, 0.094f ),    // 0.10: see TreasureWeaponType for an explanation of why this is here,
                                                               // and not deeper in WeaponWcids.cs
            ( TreasureWeaponType.AcxThrown,       0.06f ),     // Project ACX
        };

        /// <summary>
        /// Even chance for all melee weapon types
        /// Only used for test methods
        /// </summary>
        public static ChanceTable<TreasureWeaponType> MeleeChances = new ChanceTable<TreasureWeaponType>()
        {
            ( TreasureWeaponType.Sword,           0.125f ),
            ( TreasureWeaponType.Mace,            0.125f ),
            ( TreasureWeaponType.Axe,             0.125f ),
            ( TreasureWeaponType.Spear,           0.125f ),
            ( TreasureWeaponType.Unarmed,         0.125f ),
            ( TreasureWeaponType.Staff,           0.125f ),
            ( TreasureWeaponType.Dagger,          0.125f ),
            ( TreasureWeaponType.TwoHandedWeapon, 0.125f ),
        };

        /// <summary>
        /// Roughly even chance for all missile weapon types
        /// Only used for test methods
        /// </summary>
        public static ChanceTable<TreasureWeaponType> MissileChances = new ChanceTable<TreasureWeaponType>()
        {
            ( TreasureWeaponType.Bow,             0.334f ),
            ( TreasureWeaponType.Crossbow,        0.333f ),
            ( TreasureWeaponType.Atlatl,          0.333f ),
        };

        private static readonly List<ChanceTable<TreasureWeaponType>> weaponTiers = new List<ChanceTable<TreasureWeaponType>>()
        {
            T1_T4_Chances,
            T1_T4_Chances,
            T1_T4_Chances,
            T1_T4_Chances,
            T5_T6_Chances,
            T5_T6_Chances,
        };

        public static TreasureWeaponType Roll(int tier)
        {
            // todo: add unique profiles for t7 / t8?
            //tier = Math.Clamp(tier, 1, 6);

            //return weaponTiers[tier - 1].Roll();

            return RetailChances.Roll();
        }
    }
}
