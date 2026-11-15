using System.Collections.Generic;

using ACE.DatLoader.FileTypes;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// Project ACX: the EnvCells of one landblock, which is all the navmesh needs.
    /// ClassicACE's LandblockGeometry / CellGeometry classes also tracked building interiors, walkable polygons,
    /// wall lines and connected cells; none of that fed the navmesh (its dungeon loader took every EnvCell in the
    /// landblock), so only that loader is kept here.
    /// </summary>
    internal static class LandblockGeometry
    {
        /// <summary>
        /// Returns the EnvCells 0x0100 .. 0x0100 + NumCells - 1 of the landblock. Cells missing from the dat are left out.
        /// </summary>
        public static List<EnvCell> LoadEnvCells(ushort landblock, DatSource dats, out int missingCells)
        {
            missingCells = 0;

            var cells = new List<EnvCell>();
            var landblockId = (uint)landblock << 16;

            var info = dats.ReadCell<LandblockInfo>(landblockId | 0xFFFE);
            if (info == null)
                return cells;

            for (uint i = 0; i < info.NumCells; i++)
            {
                var cell = dats.ReadCell<EnvCell>(landblockId + 0x0100 + i);
                if (cell == null)
                {
                    missingCells++;
                    continue;
                }
                cells.Add(cell);
            }

            return cells;
        }
    }
}
