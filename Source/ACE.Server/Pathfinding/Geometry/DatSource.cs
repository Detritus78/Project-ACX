using System.Collections.Generic;
using System.IO;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// Project ACX: reads the dat files for one mesh build without adding them to DatManager's FileCache.
    /// A bulk build touches every dungeon cell in the game, which would otherwise stay cached for the life of the server.
    /// Files the server has already cached are reused; everything else is kept only for this build.
    /// Not thread-safe: use one instance per build.
    /// </summary>
    internal sealed class DatSource
    {
        private readonly Dictionary<uint, FileType> cellFiles = new Dictionary<uint, FileType>();
        private readonly Dictionary<uint, FileType> portalFiles = new Dictionary<uint, FileType>();

        public T ReadCell<T>(uint fileId) where T : FileType, new() => Read<T>(DatManager.CellDat, cellFiles, fileId);

        public T ReadPortal<T>(uint fileId) where T : FileType, new() => Read<T>(DatManager.PortalDat, portalFiles, fileId);

        private static T Read<T>(DatDatabase db, Dictionary<uint, FileType> local, uint fileId) where T : FileType, new()
        {
            if (local.TryGetValue(fileId, out var file))
                return file as T;

            var result = ReadUncached<T>(db, fileId);
            local[fileId] = result;     // a miss is remembered as null
            return result;
        }

        /// <summary>
        /// Returns the file from DatManager's cache if it is there, otherwise reads it from the dat without caching it.
        /// Returns null if the dat has no such file. Thread-safe.
        /// </summary>
        public static T ReadUncached<T>(DatDatabase db, uint fileId) where T : FileType, new()
        {
            if (db.FileCache.TryGetValue(fileId, out var cached))
                return cached as T;

            if (!db.AllFiles.ContainsKey(fileId))
                return null;

            var datReader = db.GetReaderForFile(fileId);
            if (datReader == null)
                return null;

            var obj = new T();
            using (var memoryStream = new MemoryStream(datReader.Buffer))
            using (var reader = new BinaryReader(memoryStream))
                obj.Unpack(reader);

            return obj;
        }
    }
}
