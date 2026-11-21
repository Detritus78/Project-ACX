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
        // Project ACX: Eternal thrown weapons and the Alchemist's bombs are two types of their own, each as often as a bow,
        // crossbow or caster (JJ 2026-11-21: "I would like the same opportunity to loot thrown or bombs as everything else";
        // was 6% for both together, bombs 1 in 8 of it, 0.75% of weapons: 3 bombs in 542 weapons over Nov 18-21). AcxThrown
        // carries both, split by acx_alchemist_loot_share (0.5); retail's chances below are the originals / 1.14
        private static ChanceTable<TreasureWeaponType> RetailChances = new ChanceTable<TreasureWeaponType>()
        {
            // melee: 55.2% (retail 63%)
            // missile: 30.3% with thrown and bombs (retail 20%)
            // two-handed: 8.8% (retail 10%)
            // caster: 6.1% (retail 7%)
            ( TreasureWeaponType.Sword,           0.0789f ),   // 0.09
            ( TreasureWeaponType.Mace,            0.0789f ),   // 0.09
            ( TreasureWeaponType.Axe,             0.0789f ),   // 0.09
            ( TreasureWeaponType.Spear,           0.0789f ),   // 0.09
            ( TreasureWeaponType.Unarmed,         0.0789f ),   // 0.09
            ( TreasureWeaponType.Staff,           0.0789f ),   // 0.09
            ( TreasureWeaponType.Dagger,          0.0789f ),   // 0.09
            ( TreasureWeaponType.Bow,             0.0614f ),   // 0.07
            ( TreasureWeaponType.Crossbow,        0.0614f ),   // 0.07
            ( TreasureWeaponType.Atlatl,          0.0526f ),   // 0.06
            ( TreasureWeaponType.Caster,          0.0614f ),   // 0.07
            ( TreasureWeaponType.TwoHandedWeapon, 0.0877f ),   // 0.10: see TreasureWeaponType for an explanation of why this is here,
                                                               // and not deeper in WeaponWcids.cs
            ( TreasureWeaponType.AcxThrown,       0.1232f ),   // Project ACX: thrown 6.16% + bombs 6.16%
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
