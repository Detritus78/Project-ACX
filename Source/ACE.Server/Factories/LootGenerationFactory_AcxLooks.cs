using ACE.Entity.Enum;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    /// <summary>
    /// Project ACX: custom looks for generated loot
    /// </summary>
    public static partial class LootGenerationFactory
    {
        /// <summary>
        /// Penumbral Horror smoke, stacked three times
        /// </summary>
        public const uint NetherWeaponAuraScript = 0x33000F82;
        public const int NetherWeaponAuraScriptCount = 3;

        /// <summary>
        /// The Nether Baton's sparks (its model's own effect, setup 0x02001A30)
        /// </summary>
        public const uint NetherWeaponAuraScript2 = 0x330012A3;

        private static void ApplyAcxLootLooks(WorldObject wo)
        {
            if (wo == null || wo.W_DamageType != DamageType.Nether || (wo.ItemType & ItemType.WeaponOrCaster) == 0)
                return;

            ApplyNetherWeaponLook(wo);
        }

        /// <summary>
        /// Nether weapons of every kind carry the smoke and the sparks; their own colours are left alone
        /// </summary>
        public static void ApplyNetherWeaponLook(WorldObject wo)
        {
            wo.AcxAuraScript = NetherWeaponAuraScript;
            wo.AcxAuraScriptCount = NetherWeaponAuraScriptCount;
            wo.AcxAuraScript2 = NetherWeaponAuraScript2;
        }
    }
}
