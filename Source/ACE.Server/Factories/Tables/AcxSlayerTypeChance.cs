using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;

namespace ACE.Server.Factories.Tables
{
    /// <summary>
    /// Project ACX: which creature type a looted slayer weapon hunts, by loot tier (ported from ClassicACE with its
    /// numbers: every listed type equally likely). Used by LootGenerationFactory_AcxMutations.cs.
    /// </summary>
    public static class AcxSlayerTypeChance
    {
        public static float GetSlayerChanceForTier(int tier)
        {
            switch (tier)
            {
                case 1:
                default:
                    return 0.01f;
                case 2:
                    return 0.01f;
                case 3:
                    return 0.01f;
                case 4:
                    return 0.01f;
                case 5:
                    return 0.01f;
                case 6:
                    return 0.01f;
                case 7:
                    return 0.01f;
                case 8:
                    return 0.01f;
            }
        }

        private static readonly CreatureType[] T1_SlayerTypeChances =
        {
            CreatureType.Drudge, CreatureType.Olthoi, CreatureType.Banderling, CreatureType.Drudge, CreatureType.Mosswart, CreatureType.Lugian,
            CreatureType.Tumerok, CreatureType.Mite, CreatureType.Tusker, CreatureType.PhyntosWasp, CreatureType.Rat, CreatureType.Cow,
            CreatureType.Golem, CreatureType.Undead, CreatureType.Gromnie, CreatureType.Reedshark, CreatureType.Armoredillo, CreatureType.Virindi,
            CreatureType.Wisp, CreatureType.Shadow, CreatureType.Mattekar, CreatureType.Sclavus, CreatureType.ShallowsShark, CreatureType.Monouga,
            CreatureType.Zefir, CreatureType.Human, CreatureType.Shreth, CreatureType.Chittick, CreatureType.Moarsman, CreatureType.Slithis,
            CreatureType.Grievver, CreatureType.Niffis, CreatureType.Ursuin, CreatureType.Crystal, CreatureType.Idol, CreatureType.Carenzi,
            CreatureType.Siraluun, CreatureType.Simulacrum, CreatureType.Elemental,
        };

        private static readonly CreatureType[] T2_SlayerTypeChances =
        {
            CreatureType.Drudge, CreatureType.Olthoi, CreatureType.Banderling, CreatureType.Drudge, CreatureType.Mosswart, CreatureType.Lugian,
            CreatureType.Tumerok, CreatureType.Mite, CreatureType.Tusker, CreatureType.PhyntosWasp, CreatureType.Rat, CreatureType.Cow,
            CreatureType.Golem, CreatureType.Undead, CreatureType.Gromnie, CreatureType.Reedshark, CreatureType.Armoredillo, CreatureType.Virindi,
            CreatureType.Wisp, CreatureType.Shadow, CreatureType.Mattekar, CreatureType.Sclavus, CreatureType.ShallowsShark, CreatureType.Monouga,
            CreatureType.Zefir, CreatureType.Human, CreatureType.Shreth, CreatureType.Chittick, CreatureType.Moarsman, CreatureType.Slithis,
            CreatureType.Grievver, CreatureType.Niffis, CreatureType.Ursuin, CreatureType.Crystal, CreatureType.Idol, CreatureType.Carenzi,
            CreatureType.Siraluun, CreatureType.Simulacrum, CreatureType.Elemental,
        };

        private static readonly CreatureType[] T3_SlayerTypeChances =
        {
            CreatureType.Drudge, CreatureType.Olthoi, CreatureType.Banderling, CreatureType.Drudge, CreatureType.Mosswart, CreatureType.Lugian,
            CreatureType.Tumerok, CreatureType.Mite, CreatureType.Tusker, CreatureType.PhyntosWasp, CreatureType.Rat, CreatureType.Cow,
            CreatureType.Golem, CreatureType.Undead, CreatureType.Gromnie, CreatureType.Reedshark, CreatureType.Armoredillo, CreatureType.Virindi,
            CreatureType.Wisp, CreatureType.Shadow, CreatureType.Mattekar, CreatureType.Sclavus, CreatureType.ShallowsShark, CreatureType.Monouga,
            CreatureType.Zefir, CreatureType.Human, CreatureType.Shreth, CreatureType.Chittick, CreatureType.Moarsman, CreatureType.Slithis,
            CreatureType.Grievver, CreatureType.Niffis, CreatureType.Ursuin, CreatureType.Crystal, CreatureType.Idol, CreatureType.Carenzi,
            CreatureType.Siraluun, CreatureType.Simulacrum, CreatureType.Elemental, CreatureType.Margul, CreatureType.Burun,
        };

        private static readonly CreatureType[] T4_SlayerTypeChances =
        {
            CreatureType.Drudge, CreatureType.Olthoi, CreatureType.Banderling, CreatureType.Drudge, CreatureType.Mosswart, CreatureType.Lugian,
            CreatureType.Tumerok, CreatureType.Mite, CreatureType.Tusker, CreatureType.PhyntosWasp, CreatureType.Rat, CreatureType.Cow,
            CreatureType.Golem, CreatureType.Undead, CreatureType.Gromnie, CreatureType.Reedshark, CreatureType.Armoredillo, CreatureType.Virindi,
            CreatureType.Wisp, CreatureType.Shadow, CreatureType.Mattekar, CreatureType.Sclavus, CreatureType.ShallowsShark, CreatureType.Monouga,
            CreatureType.Zefir, CreatureType.Human, CreatureType.Shreth, CreatureType.Chittick, CreatureType.Moarsman, CreatureType.Slithis,
            CreatureType.Grievver, CreatureType.Niffis, CreatureType.Ursuin, CreatureType.Crystal, CreatureType.Idol, CreatureType.Carenzi,
            CreatureType.Siraluun, CreatureType.Simulacrum, CreatureType.Elemental, CreatureType.Margul, CreatureType.Burun,
        };

        private static readonly CreatureType[] T5_SlayerTypeChances =
        {
            CreatureType.Drudge, CreatureType.Olthoi, CreatureType.Banderling, CreatureType.Drudge, CreatureType.Mosswart, CreatureType.Lugian,
            CreatureType.Tumerok, CreatureType.Mite, CreatureType.Tusker, CreatureType.PhyntosWasp, CreatureType.Rat, CreatureType.Cow,
            CreatureType.Golem, CreatureType.Undead, CreatureType.Gromnie, CreatureType.Reedshark, CreatureType.Armoredillo, CreatureType.Virindi,
            CreatureType.Wisp, CreatureType.Shadow, CreatureType.Mattekar, CreatureType.Sclavus, CreatureType.ShallowsShark, CreatureType.Monouga,
            CreatureType.Zefir, CreatureType.Human, CreatureType.Shreth, CreatureType.Chittick, CreatureType.Moarsman, CreatureType.Slithis,
            CreatureType.Grievver, CreatureType.Niffis, CreatureType.Ursuin, CreatureType.Crystal, CreatureType.Idol, CreatureType.Carenzi,
            CreatureType.Siraluun, CreatureType.Simulacrum, CreatureType.Elemental, CreatureType.Margul, CreatureType.Burun,
        };

        private static readonly CreatureType[] T6_T8_SlayerTypeChances =
        {
            CreatureType.Drudge, CreatureType.Olthoi, CreatureType.Banderling, CreatureType.Drudge, CreatureType.Mosswart, CreatureType.Lugian,
            CreatureType.Tumerok, CreatureType.Mite, CreatureType.Tusker, CreatureType.PhyntosWasp, CreatureType.Rat, CreatureType.Cow,
            CreatureType.Golem, CreatureType.Undead, CreatureType.Gromnie, CreatureType.Reedshark, CreatureType.Armoredillo, CreatureType.Virindi,
            CreatureType.Wisp, CreatureType.Shadow, CreatureType.Mattekar, CreatureType.Sclavus, CreatureType.ShallowsShark, CreatureType.Monouga,
            CreatureType.Zefir, CreatureType.Human, CreatureType.Shreth, CreatureType.Chittick, CreatureType.Moarsman, CreatureType.Slithis,
            CreatureType.Grievver, CreatureType.Niffis, CreatureType.Ursuin, CreatureType.Crystal, CreatureType.Idol, CreatureType.Carenzi,
            CreatureType.Siraluun, CreatureType.Simulacrum, CreatureType.Elemental, CreatureType.Margul, CreatureType.Burun,
        };

        private static readonly CreatureType[][] slayerTypes = { T1_SlayerTypeChances, T2_SlayerTypeChances, T3_SlayerTypeChances, T4_SlayerTypeChances, T5_SlayerTypeChances, T6_T8_SlayerTypeChances, T6_T8_SlayerTypeChances, T6_T8_SlayerTypeChances };

        public static CreatureType Roll(TreasureDeath profile)
        {
            var types = slayerTypes[System.Math.Clamp(profile.Tier, 1, slayerTypes.Length) - 1];
            return types[ThreadSafeRandom.Next(0, types.Length - 1)];
        }
    }
}
