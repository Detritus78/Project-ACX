using System.Collections.Generic;

using ACE.Common;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;
using ACE.Server.WorldObjects;

using DamageType = ACE.Entity.Enum.DamageType;

namespace ACE.Server.Factories.Tables.Wcids
{
    /// <summary>
    /// Project ACX: Eternal thrown weapons (acx/Content/thrown/10-eternal-thrown.sql; JJ 2026-11-14: "I want to make
    /// thrown weapon appealing more so then the others since nobody has played it since it was made"). One unlimited-use
    /// weapon per retail thrown family, plain or acid/fire/frost/electric, that never runs out and leaves the off hand
    /// free for a shield. They roll like atlatls (the non-elemental atlatl script: damage multiplier and wield
    /// requirement), then their multiplier is scaled by tier to JJ's band: 85% of atlatls through tier 6, staying under
    /// one-handed melee, then a little over one-handed melee at tiers 7 and 8 (JJ: "sword trumps through all levels up
    /// until T7 we can let thrown beat out sword but lets close the gap"; then, as needing no ammo or Fletching is worth
    /// something, the bottom of that band: "Lets go with A"). Benched with /acxbench thrownloot, without buffs.
    /// The multiplier then goes into the weapon's own damage, and its damage range rolls too, like a melee weapon's (JJ
    /// 2026-11-15: "its the base damage that should reflect the differences", "it should have variations just like melee
    /// weapons"): the examine window shows each drop's damage, every throw hits in that range, and Blood Drinker adds
    /// the same flat amount as on a sword (on a missile weapon's multiplier it was multiplied, about 7x at tier 8).
    /// Like atlatls: plain only through tier 4, mixed at tier 5, elemental beside the physical ones from tier 6.
    /// </summary>
    public static class AcxThrownWcids
    {
        // acx/Content/thrown/10-eternal-thrown.sql: 900810-900816 plain (dart, shouken, dagger, club, djarid, javelin,
        // axe), then each family's acid, fire, frost and electric version from 900817, then each family's nether
        // version from 900845 (AcxNetherWeaponWcids turns a drop into one)
        private const uint FirstPlain = 900810, FirstElemental = 900817, FirstNether = 900845;
        private const int Families = 7, Elements = 4;

        private static ChanceTable<WeenieClassName> Plain()
        {
            var table = new ChanceTable<WeenieClassName>();
            for (var f = 0; f < Families; f++)
                table.Add(((WeenieClassName)(FirstPlain + f), 1.0f / Families));
            return Settle(table);
        }

        /// <summary>
        /// the last entry takes up what float rounding left over, so the table adds up to 1 exactly
        /// </summary>
        private static ChanceTable<WeenieClassName> Settle(ChanceTable<WeenieClassName> table)
        {
            var rest = 1.0M;
            for (var i = 0; i < table.Count - 1; i++)
                rest -= (decimal)table[i].chance;
            table[^1] = (table[^1].result, (float)rest);
            return table;
        }

        private static ChanceTable<WeenieClassName> Mixed(float plainShare)
        {
            var table = new ChanceTable<WeenieClassName>();
            for (var f = 0; f < Families; f++)
            {
                table.Add(((WeenieClassName)(FirstPlain + f), plainShare / Families));
                for (var e = 0; e < Elements; e++)
                    table.Add(((WeenieClassName)(FirstElemental + f * Elements + e), (1.0f - plainShare) / (Families * Elements)));
            }
            return Settle(table);
        }

        private static readonly ChanceTable<WeenieClassName> T1_T4_Chances = Plain();
        private static readonly ChanceTable<WeenieClassName> T5_Chances = Mixed(0.51f);        // atlatls: 51% plain at tier 5
        private static readonly ChanceTable<WeenieClassName> T6_T8_Chances = Mixed(3.0f / 7);   // atlatls: 3 physical of 7 types

        private static readonly List<ChanceTable<WeenieClassName>> thrownTiers = new List<ChanceTable<WeenieClassName>>()
        {
            T1_T4_Chances,
            T1_T4_Chances,
            T1_T4_Chances,
            T1_T4_Chances,
            T5_Chances,
            T6_T8_Chances,
            T6_T8_Chances,
            T6_T8_Chances,
        };

        /// <summary>
        /// The atlatl script's damage multiplier times this, by tier (calibrated with /acxbench thrownloot against the
        /// atlatl and one-handed melee medians). Benched without Blood Drinker or Swift Killer on any class (JJ
        /// 2026-11-15: "blood drinker is only applied if a weapon rolls with it ... Or the player cast's it"): 85% of the
        /// atlatl median at tiers 1-6, 104% / 106% of the one-handed melee median at tiers 7 / 8, where the buffed
        /// calibration of 2026-11-15 00:13 had put them
        /// </summary>
        private static readonly double[] DamageModScale = { 1.201, 1.201, 1.805, 1.791, 2.956, 3.239, 4.830, 5.343 };

        public static WeenieClassName Roll(int tier)
        {
            return thrownTiers[tier - 1].Roll();
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            var id = (uint)wcid;
            weaponType = TreasureWeaponType.Atlatl;
            return id >= FirstPlain && id < FirstNether + Families;
        }

        /// <summary>
        /// The nether version of a plain or elemental Eternal thrown weapon (its family's)
        /// </summary>
        public static bool TryGetNether(WeenieClassName wcid, out uint netherWcid)
        {
            var id = (uint)wcid;
            netherWcid = 0;
            if (id >= FirstPlain && id < FirstElemental)
                netherWcid = FirstNether + (id - FirstPlain);
            else if (id >= FirstElemental && id < FirstNether)
                netherWcid = FirstNether + (id - FirstElemental) / Elements;
            return netherWcid != 0;
        }

        // retail quality for tier 5 and up: the elemental atlatl script's ElementalDamageBonus (1-4 at tier 5 up to 8-22
        // at tier 8) is flat damage on the dart, so on a thrown weapon (base 8-11) it would swing damage four times as
        // much; it becomes a multiplier with the effect it has on that tier's atlatl dart instead (Atlatl Dart 12,
        // Greater 18, Prismatic 28, Deadly Prismatic 52), around the tier's mean bonus
        private static readonly int[] DartDamage = { 12, 12, 18, 18, 28, 28, 52, 52 };
        private static readonly double[] MeanBonus = { 0, 0, 0, 0, 2.35, 5.20, 13.10, 17.03 };

        public static double QualityFromBonus(int tier, int elementalDamageBonus)
        {
            var t = System.Math.Clamp(tier, 1, 8) - 1;
            return (DartDamage[t] + elementalDamageBonus) / (DartDamage[t] + MeanBonus[t]);
        }

        public static bool IsEternalThrown(uint wcid) => TryGetValue((WeenieClassName)wcid, out _);

        /// <summary>
        /// The same family's version with another damage type, for salvage that changes it (AcxWeaponFamilies.GetVersion):
        /// a Fire Javelin turned frost takes the Frost Javelin's model, icon and name, a physical type the plain one's
        /// </summary>
        public static bool TryGetVersion(uint wcid, DamageType damageType, out uint version)
        {
            version = 0;
            if (!IsEternalThrown(wcid))
                return false;

            var family = wcid < FirstElemental ? wcid - FirstPlain : wcid < FirstNether ? (wcid - FirstElemental) / Elements : wcid - FirstNether;

            // 10-eternal-thrown.sql's element order: acid, fire, frost, electric
            int element = damageType switch { DamageType.Acid => 0, DamageType.Fire => 1, DamageType.Cold => 2, DamageType.Electric => 3, _ => -1 };

            if (element >= 0)
                version = FirstElemental + family * Elements + (uint)element;
            else if (damageType == DamageType.Nether)
                version = FirstNether + family;
            else if ((damageType & DamageType.Physical) != 0)
                version = FirstPlain + family;

            return version != 0;
        }

        public static double GetDamageModScale(int tier) => DamageModScale[System.Math.Clamp(tier, 1, 8) - 1];

        /// <summary>
        /// Tiers 3 and 4 get a quality roll of their own: the atlatl script spreads them only about 7%, so a pack of
        /// djarids would read alike (JJ: "I shouldnt have 5 in my pack that say the same thing"); averages 1
        /// </summary>
        public static double RollQuality(int tier) => tier == 3 || tier == 4 ? ThreadSafeRandom.Next(0.92f, 1.08f) : 1.0;

        // the damage range, as melee loot rolls it (a sword's 0.47-0.62 around 0.53, 10/20/40/20/10) around the retail
        // thrown weapons' 0.25; averages 0.25, so the benched damage holds
        private static readonly ChanceTable<float> VarianceChances = new ChanceTable<float>()
        {
            (0.19f, 0.10f),
            (0.22f, 0.20f),
            (0.25f, 0.40f),
            (0.28f, 0.20f),
            (0.31f, 0.10f),
        };

        public static float RollVariance() => VarianceChances.Roll();

        /// <summary>
        /// A thrown weapon's projectile is made new from its weenie (Creature.LaunchProjectile); an Eternal thrown one
        /// takes the damage and damage range its weapon rolled as loot, so every throw hits in the range it shows
        /// </summary>
        public static void CarryRoll(WorldObject weapon, WorldObject projectile)
        {
            if (!IsEternalThrown(weapon.WeenieClassId))
                return;

            projectile.Damage = weapon.Damage;
            projectile.DamageVariance = weapon.DamageVariance;
        }
    }
}
