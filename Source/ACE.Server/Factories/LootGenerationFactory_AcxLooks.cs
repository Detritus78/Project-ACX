using System.Collections.Generic;

using ACE.Entity.Enum;
using ACE.Server.Factories.Tables.Wcids;
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
        /// Rolls an item's colours as loot generation does, e.g. after salvage gave it another weapon version's look
        /// </summary>
        public static void RollAcxLootColor(WorldObject wo)
        {
            MutateColor(wo);
        }

        /// <summary>
        /// Thrown weapons that don't run up their +Z, where the Nether Baton's sparks (0.35 up the model) float off the
        /// weapon (JJ 2026-11-15 on a Nether Djarid: "The location of the nether colors and animation look a little
        /// strange"): Djarid and Javelin lie on their side and the Dart lies flat. Retail's only sparks placed along them
        /// are fire weapons' own (JJ: "the fx are actually fire"), so these, and the Dagger with them ("anything that is
        /// fire for nether swap it out"), get retail nether effects that sit
        /// at the grip instead, picked by JJ in game with /acxfx ("BEB looks best applied twice", "those animations and
        /// colors look great. replace the fire ones"): 0x33000BEB twice (black nether flames, the nether smoke rising)
        /// and 0x330012B9 (purple and black sparks around the object's origin, the hand). The Shouken, Axe and Club keep
        /// the baton's sparks ("12A3 works for the star"; "the axe's are fine as they are").
        /// </summary>
        private static readonly HashSet<uint> GripAuraSetups = new HashSet<uint>() { 0x02000132, 0x0200012E, 0x02000131 };

        public const uint NetherGripFlames = 0x33000BEB;
        public const int NetherGripFlamesCount = 2;
        public const uint NetherGripSparks = 0x330012B9;

        /// <summary>
        /// Nether weapons of every kind carry the smoke and the sparks; their own colours are left alone
        /// </summary>
        public static void ApplyNetherWeaponLook(WorldObject wo)
        {
            wo.AcxAuraScript = NetherWeaponAuraScript;
            wo.AcxAuraScriptCount = NetherWeaponAuraScriptCount;

            if (AcxThrownWcids.IsEternalThrown(wo.WeenieClassId) && GripAuraSetups.Contains(wo.SetupTableId))
            {
                wo.AcxAuraScript2 = NetherGripFlames;
                wo.AcxAuraScript2Count = NetherGripFlamesCount;
                wo.AcxAuraScript3 = NetherGripSparks;
            }
            else
            {
                wo.AcxAuraScript2 = NetherWeaponAuraScript2;
                wo.AcxAuraScript2Count = 1;
                wo.AcxAuraScript3 = null;
            }
        }
    }
}
