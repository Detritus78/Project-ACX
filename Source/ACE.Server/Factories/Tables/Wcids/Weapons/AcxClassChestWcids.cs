using ACE.Database.Models.World;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Factories.Tables.Wcids
{
    /// <summary>
    /// Project ACX: Preview's Marketplace test chests, one per weapon class (acx/Experiments/marketplace-loot-chests.sql;
    /// JJ 2026-11-15: "create chests that spawn only missile weapons, heavy weapons, light weapons, finesse weapons,
    /// twohand"). Their treasure profiles roll weapons only, and this picks the weapon from that class alone (no casters).
    /// </summary>
    public static class AcxClassChestWcids
    {
        // treasure profiles, numbered like the chests that use them
        public const uint Missile = 900021, Heavy = 900022, Light = 900023, Finesse = 900024, TwoHanded = 900025;

        // bow, crossbow, atlatl and thrown alike
        private static readonly ChanceTable<TreasureWeaponType> MissileChances = new ChanceTable<TreasureWeaponType>()
        {
            ( TreasureWeaponType.Bow,       0.25f ),
            ( TreasureWeaponType.Crossbow,  0.25f ),
            ( TreasureWeaponType.Atlatl,    0.25f ),
            ( TreasureWeaponType.AcxThrown, 0.25f ),
        };

        public static bool TryRoll(TreasureDeath treasureDeath, ref TreasureWeaponType weaponType, out WeenieClassName wcid)
        {
            switch (treasureDeath.TreasureType)
            {
                case Missile:
                    weaponType = MissileChances.Roll();
                    wcid = WeaponWcids.Roll(treasureDeath, ref weaponType);
                    return true;

                case Heavy:
                    wcid = HeavyWeaponWcids.Roll(out weaponType);
                    return true;

                case Light:
                    wcid = LightWeaponWcids.Roll(out weaponType);
                    return true;

                case Finesse:
                    wcid = FinesseWeaponWcids.Roll(out weaponType);
                    return true;

                case TwoHanded:
                    weaponType = TreasureWeaponType.TwoHandedWeapon;
                    wcid = WeaponWcids.Roll(treasureDeath, ref weaponType);
                    return true;
            }
            wcid = WeenieClassName.undef;
            return false;
        }
    }
}
