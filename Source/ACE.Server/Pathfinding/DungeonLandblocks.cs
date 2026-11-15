using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Server.Pathfinding.Geometry;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// Project ACX: finds the dungeon landblocks (EnvCells only, no traversable overworld) in the cell dat.
    /// Uses the same test as Landblock.IsDungeon, so it agrees with what the running server calls a dungeon,
    /// but reads the dat directly so it works for landblocks that are not loaded.
    /// </summary>
    public static class DungeonLandblocks
    {
        private static readonly ConcurrentDictionary<ushort, bool> cache = new ConcurrentDictionary<ushort, bool>();

        /// <summary>
        /// Every dungeon landblock in the cell dat, in landblock order
        /// </summary>
        public static List<ushort> GetAll()
        {
            var landblocks = DatManager.CellDat.AllFiles.Keys
                .Where(id => (id & 0xFFFF) == 0xFFFE)   // LandblockInfo: only landblocks with cells, buildings or objects have one
                .Select(id => (ushort)(id >> 16))
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            return landblocks.Where(IsDungeon).ToList();
        }

        public static bool IsDungeon(ushort landblock)
        {
            return cache.GetOrAdd(landblock, Compute);
        }

        private static bool Compute(ushort landblock)
        {
            var landblockX = landblock >> 8;
            var landblockY = landblock & 0xFF;

            // hack for NW island, as in Landblock.IsDungeon
            if (landblockX < 0x08 && landblockY > 0xF8)
                return false;

            var landblockId = (uint)landblock << 16;

            var info = DatSource.ReadUncached<LandblockInfo>(DatManager.CellDat, landblockId | 0xFFFE);
            if (info == null || info.NumCells == 0 || info.Buildings == null || info.Buildings.Count != 0)
                return false;

            // a landblock with no terrain entry counts as flat, as it does for Landblock.IsDungeon
            var cellLandblock = DatSource.ReadUncached<CellLandblock>(DatManager.CellDat, landblockId | 0xFFFF);
            if (cellLandblock == null)
                return true;

            foreach (var height in cellLandblock.Height)
            {
                if (height != 0)
                    return false;
            }
            return true;
        }
    }
}
