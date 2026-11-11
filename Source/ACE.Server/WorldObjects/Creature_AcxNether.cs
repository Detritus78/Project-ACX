using System;
using System.Collections.Generic;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: nether is an endgame damage type.
    /// Monsters without a retail nether resistance take acx_nether_resist_default (0.86, about what a war mage gets by always
    /// picking the best element); Rynthid and Obsidian Plains monsters take acx_nether_resist_endgame (1.25).
    /// Retail's deliberate values (anything other than the placeholder 1.0) are kept.
    /// </summary>
    partial class Creature
    {
        public double GetAcxBaseNetherResist()
        {
            if (this is Player || (ResistNether.HasValue && Math.Abs(ResistNether.Value - 1.0) > 0.001))
                return ResistNether ?? 1.0;

            if (IsAcxNetherEndgame())
                return PropertyManager.GetDouble("acx_nether_resist_endgame").Item;

            return PropertyManager.GetDouble("acx_nether_resist_default").Item;
        }

        private bool IsAcxNetherEndgame()
        {
            if (Name != null && Name.Contains("Rynthid", StringComparison.OrdinalIgnoreCase))
                return true;

            return Location != null && ObsidianPlainsLandblocks.Value.Contains(Location.LandblockId.Landblock);
        }

        /// <summary>
        /// Landblock of the Obsidian Plains portal drop (Obsidian Plains, 2D2F)
        /// </summary>
        private const ushort ObsidianPlainsCenter = 0x2D2F;

        private const int ObsidianPlainTerrainType = 0x6;

        /// <summary>
        /// The Obsidian Plains: the connected patch of landblocks around the portal drop whose ground is mostly Obsidian Plain terrain
        /// (534 landblocks, x 22-3E, y 21-3D). The terrain type alone appears all over the map, so it is grown out from the center.
        /// </summary>
        private static readonly Lazy<HashSet<ushort>> ObsidianPlainsLandblocks = new Lazy<HashSet<ushort>>(() =>
        {
            var region = new HashSet<ushort>();
            var queue = new Queue<ushort>();

            if (IsMostlyObsidianPlain(ObsidianPlainsCenter))
            {
                region.Add(ObsidianPlainsCenter);
                queue.Enqueue(ObsidianPlainsCenter);
            }

            while (queue.Count > 0)
            {
                var landblock = queue.Dequeue();
                int x = landblock >> 8, y = landblock & 0xFF;

                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx > 0xFE || ny < 0 || ny > 0xFE)
                        continue;

                    var neighbor = (ushort)((nx << 8) | ny);
                    if (!region.Contains(neighbor) && IsMostlyObsidianPlain(neighbor))
                    {
                        region.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            log.Info($"[ACX] Obsidian Plains: {region.Count} landblocks");
            return region;
        });

        private static bool IsMostlyObsidianPlain(ushort landblock)
        {
            try
            {
                var cellLandblock = DatManager.CellDat.ReadFromDat<CellLandblock>(((uint)landblock << 16) | 0xFFFF);
                var count = 0;
                foreach (var terrain in cellLandblock.Terrain)
                {
                    if (((terrain & CellLandblock.TerrainMask_Type) >> CellLandblock.TerrainShift_Type) == ObsidianPlainTerrainType)
                        count++;
                }
                return count > cellLandblock.Terrain.Count / 2;
            }
            catch
            {
                return false;
            }
        }
    }
}
