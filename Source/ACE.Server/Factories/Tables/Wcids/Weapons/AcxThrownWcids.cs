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
    /// free for a shield.
    /// A thrown weapon is a launcher and its own ammunition in one (JJ 2026-11-15: "it is ammo. Just eternal ammo", "it
    /// could be a launcher and its own ammo", "how can we get a modifier on our eternal ammo so that it behaves and is
    /// treated similar to bows for damage ... If we do the modifier and it looks just like a bow it makes more sense"):
    ///   its Damage and damage range are the ammunition's part, what an atlatl gets from its dart: the tier's dart damage
    ///   plus, from tier 5, the elemental bonus the atlatl script rolls, with a damage range rolled per drop;
    ///   its DamageMod is the launcher's part: rolled by the atlatl script as an atlatl's is (+70% to +160%), and raised
    ///   by Mahogany as a bow's is (RecipeManager_New).
    /// A hit is (own damage + Blood Drinker and the like) x DamageMod (BaseDamageMod; the projectile carries the weapon's
    /// own damage: CarryRoll), an atlatl's with that dart, so buffs and tinkering move both alike and a thrown weapon
    /// sits in JJ's band by its slower throw alone: about 85% of an atlatl ("Lets go with A"), which at tiers 7 and 8 is
    /// a little over one-handed melee ("sword trumps through all levels up until T7 we can let thrown beat out sword").
    /// Benched with /acxgear class (real monsters, buffed, untinked and tinked); OwnDamageScale tunes it by tier.
    /// The examine window shows both numbers (JJ: no "invisible hidden numbers"): the appraisal, and only the appraisal,
    /// carries an ammunition type for it (AppraisalAmmoType, AppraiseInfo), so AC:Unreal prints a launcher's lines
    /// ("Damage Bonus: 25 - 33", "Damage Modifier: +150%."), and a usage line says it for any client (DescribeDamage).
    /// The weapon's public description has no ammunition type, so it still takes a shield and no client or combat macro
    /// asks it for ammunition.
    /// Before: the multiplier was scaled to the band on a base of 10 (up to x13 at tier 8, and Blood Drinker with it),
    /// then folded into Damage (2026-11-15 morning: "its the base damage that should reflect the differences"), which
    /// left buffs and tinkering far behind the launchers' (48-66% of an atlatl on /acxgear class).
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

        // the tier's atlatl dart, the ammunition a thrown weapon carries built in: Atlatl Dart 12, Greater 18, Prismatic 28,
        // Deadly Prismatic 52 (the darts /acxbench and /acxgear give atlatls by tier)
        private static readonly int[] DartDamage = { 12, 12, 18, 18, 28, 28, 52, 52 };

        // calibration of the weapon's own damage by tier (1 = the dart's), against the atlatl of the tier on /acxgear class
        private static readonly double[] OwnDamageScale = { 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 };

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

        /// <summary>
        /// A drop's own damage: the tier's dart plus the elemental bonus the atlatl script rolled (tier 5 up: 1-4 at tier
        /// 5 to 8-22 at tier 8; on an atlatl it is flat damage on a dart of the launcher's type, and a thrown weapon is
        /// always its own type), with tiers 3 and 4's quality roll
        /// </summary>
        public static int RollOwnDamage(int tier, int? elementalDamageBonus)
        {
            var t = System.Math.Clamp(tier, 1, 8) - 1;
            return System.Math.Max(1, (int)System.Math.Round((DartDamage[t] + (elementalDamageBonus ?? 0)) * OwnDamageScale[t] * RollQuality(tier)));
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

        /// <summary>
        /// Tiers 3 and 4 get a quality roll of their own: the atlatl script spreads them only about 7%, so a pack of
        /// djarids would read alike (JJ: "I shouldnt have 5 in my pack that say the same thing"); averages 1
        /// </summary>
        public static double RollQuality(int tier) => tier == 3 || tier == 4 ? ThreadSafeRandom.Next(0.92f, 1.08f) : 1.0;

        // the damage range, as melee loot rolls it (a sword's 0.47-0.62 around 0.53, 10/20/40/20/10) around the retail
        // thrown weapons' 0.25, the darts' range too (0.24-0.30); averages 0.25
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
        /// The ammunition type an Eternal thrown weapon reports in its appraisal and nowhere else (AppraiseInfo): the
        /// examine window prints a damage multiplier only for a weapon that takes ammunition, and this is none of the
        /// retail kinds (AmmoType), so it prints a launcher's lines without "Uses arrows as ammunition."
        /// </summary>
        public const int AppraisalAmmoType = 0x200;

        /// <summary>
        /// The examine window's usage line for an Eternal thrown weapon: its damage type, which a launcher's lines leave
        /// out, and its multiplier, for a client that prints none for a weapon without separate ammunition
        /// </summary>
        public static string DescribeDamage(WorldObject weapon, double damageMod)
        {
            var type = ACE.Entity.Enum.DamageTypeExtensions.GetName(weapon.W_DamageType);
            var percent = (int)System.Math.Round(System.Math.Abs(damageMod - 1.0) * 100.0);
            return $"Thrown: its own ammunition. {(type != null ? type + " damage" : "Its damage")}, multiplied by its Damage Modifier ({(damageMod < 1.0 ? "-" : "+")}{percent}%).";
        }

        /// <summary>
        /// A thrown weapon's projectile is made new from its weenie (Creature.LaunchProjectile); an Eternal thrown one
        /// takes the damage and damage range its weapon rolled as loot (its ammunition's part; the multiplier is read from
        /// the wielded weapon), so every throw hits in the range it shows
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
