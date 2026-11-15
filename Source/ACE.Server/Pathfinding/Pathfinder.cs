//
// Project ACX: indoor pathfinding, ported from ClassicACE (April 2025 snapshot).
// ClassicACE's code is based on Trevis' pathfinding proof of concept at https://gitlab.com/trevis/ace.mods.pathfinding/-/tree/master
//
// Differences from ClassicACE:
// - Everything is off unless the 'pathfinding' server bool is on (/modifybool pathfinding true).
// - Only dungeon landblocks (EnvCells only, see DungeonLandblocks) get meshes.
// - A mesh is never built on a landblock thread. Meshes are prebuilt with /acxmeshbuild all; a dungeon that still has
//   none is built in the background, one build at a time, and its monsters behave as in plain ACE until it is ready.
// - A failed build is logged and remembered (no retry loop); queries on a landblock without a mesh just return null.
// - Mesh files are written atomically to Pathfinding/Meshes/Indoors next to the server (a Docker volume in acx/compose.yml).
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using DotRecast.Recast;
using DotRecast.Recast.Toolset;
using DotRecast.Recast.Toolset.Tools;

using log4net;

using ACE.Entity;
using ACE.Server.Managers;
using ACE.Server.Pathfinding.Geometry;

namespace ACE.Server.Pathfinding
{
    public enum AgentWidth
    {
        Narrow,
        Wide
    }

    public sealed class MeshBuildResult
    {
        public ushort Landblock;
        public bool Success;
        public string Error;
        public double Seconds;
        public int Cells;
        public int Triangles;
        public int NarrowPolys;
        /// <summary>
        /// 0 when the dungeon is too tight for a wide agent: only the narrow mesh is saved
        /// </summary>
        public int WidePolys;
        /// <summary>
        /// Dat entries (cells, objects, vertices) that were missing and left out of the mesh
        /// </summary>
        public int SkippedPieces;
    }

    public static class Pathfinder
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public const int VERTS_PER_POLY = 6;
        private const int MAX_POLYS = 256;

        private static readonly RcVec3f HalfExtents = new RcVec3f(1.25f, 1.25f, 1.25f);

        public static readonly AgentWidth[] AgentWidths = { AgentWidth.Narrow, AgentWidth.Wide };

        /// <summary>
        /// The 'pathfinding' server bool. Off by default; flip it live with /modifybool pathfinding true|false
        /// </summary>
        public static bool Enabled => PropertyManager.GetBool("pathfinding").Item;

        private static string rootDirectory;

        /// <summary>
        /// Pathfinding data folder: 'Pathfinding' next to the server assembly (/ace/Pathfinding in Docker)
        /// </summary>
        public static string RootDirectory
        {
            get => rootDirectory ??= Path.Combine(Path.GetDirectoryName(typeof(Pathfinder).Assembly.Location) ?? AppContext.BaseDirectory, "Pathfinding");
            set => rootDirectory = value;
        }

        public static string MeshDirectory => Path.Combine(RootDirectory, "Meshes", "Indoors");

        public static string LogDirectory => Path.Combine(RootDirectory, "Logs");

        /// <summary>
        /// Same file names as ClassicACE: landblock in the high word, 0 = narrow / 1 = wide in the low word
        /// </summary>
        public static string GetMeshPath(ushort landblock, AgentWidth agentWidth) => Path.Combine(MeshDirectory, $"{((uint)landblock << 16) + (uint)agentWidth:X8}.mesh");

        public static bool HasMeshFile(ushort landblock) => File.Exists(GetMeshPath(landblock, AgentWidth.Narrow));

        // ---------------------------------------------------------------------------------------------
        // Mesh cache
        // ---------------------------------------------------------------------------------------------

        private const int StateLoading = 0;
        private const int StateReady = 1;
        private const int StateUnavailable = 2;

        private sealed class LandblockMeshes
        {
            public volatile int State;
            public DtNavMesh Narrow;
            public DtNavMesh Wide;

            public DtNavMesh Get(AgentWidth agentWidth) => agentWidth == AgentWidth.Wide ? Wide : Narrow;
        }

        /// <summary>
        /// Meshes of loaded landblocks. DtNavMesh is read-only once built; every query makes its own DtNavMeshQuery,
        /// so landblock threads can share a mesh.
        /// </summary>
        private static readonly ConcurrentDictionary<ushort, LandblockMeshes> loaded = new ConcurrentDictionary<ushort, LandblockMeshes>();

        /// <summary>
        /// Landblocks whose build failed, with the reason. Not retried at runtime until /acxmeshbuild succeeds for them.
        /// </summary>
        private static readonly ConcurrentDictionary<ushort, string> failedBuilds = new ConcurrentDictionary<ushort, string>();

        /// <summary>
        /// One mesh build at a time, server-wide: a build of a big dungeon allocates a lot of memory
        /// </summary>
        private static readonly SemaphoreSlim buildGate = new SemaphoreSlim(1, 1);

        public static int LoadedLandblockCount => loaded.Count;

        public static IReadOnlyDictionary<ushort, string> FailedBuilds => failedBuilds;

        /// <summary>
        /// Returns TRUE if the meshes for this landblock are loaded. Otherwise starts loading them in the background
        /// (building them first if there are no files) and returns FALSE.
        /// </summary>
        public static bool RequestMeshes(ushort landblock)
        {
            if (loaded.TryGetValue(landblock, out var entry))
                return entry.State == StateReady;

            entry = new LandblockMeshes();
            if (!loaded.TryAdd(landblock, entry))
                return false;

            Task.Factory.StartNew(() => LoadOrBuild(landblock, entry), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            return false;
        }

        /// <summary>
        /// Drops a landblock's meshes from memory (called when the landblock unloads)
        /// </summary>
        public static void UnloadMeshes(ushort landblock)
        {
            loaded.TryRemove(landblock, out _);
        }

        private static bool TryGetMesh(Position pos, AgentWidth agentWidth, out DtNavMesh mesh)
        {
            mesh = null;

            if (pos == null)
                return false;

            var landblock = (ushort)(pos.Cell >> 16);

            if (loaded.TryGetValue(landblock, out var entry))
            {
                if (entry.State != StateReady)
                    return false;

                mesh = entry.Get(agentWidth);
                return mesh != null;
            }

            RequestMeshes(landblock);
            return false;
        }

        private static void LoadOrBuild(ushort landblock, LandblockMeshes entry)
        {
            try
            {
                if (TryReadMeshes(landblock, out var narrow, out var wide))
                {
                    entry.Narrow = narrow;
                    entry.Wide = wide;
                    entry.State = StateReady;
                    return;
                }

                if (failedBuilds.ContainsKey(landblock) || !DungeonLandblocks.IsDungeon(landblock))
                {
                    entry.State = StateUnavailable;
                    return;
                }

                log.Warn($"[Pathfinding] No saved mesh for dungeon {landblock:X4}: building it in the background. /acxmeshbuild all prebuilds every dungeon.");

                var result = BuildAndSave(landblock, CancellationToken.None, out narrow, out wide);
                if (result.Success)
                {
                    entry.Narrow = narrow;
                    entry.Wide = wide;
                    entry.State = StateReady;
                    log.Info($"[Pathfinding] Built mesh for {landblock:X4} in {result.Seconds:N1}s ({result.NarrowPolys} narrow / {result.WidePolys} wide polygons)");
                }
                else
                    entry.State = StateUnavailable;
            }
            catch (Exception ex)
            {
                entry.State = StateUnavailable;
                log.Error($"[Pathfinding] Failed to load the mesh for {landblock:X4}: {ex}");
            }
        }

        /// <summary>
        /// Reads a landblock's mesh files. FALSE if there is no narrow mesh file or it can't be read.
        /// The wide mesh is optional (tight dungeons have none).
        /// </summary>
        public static bool TryReadMeshes(ushort landblock, out DtNavMesh narrow, out DtNavMesh wide)
        {
            narrow = null;
            wide = null;

            var narrowPath = GetMeshPath(landblock, AgentWidth.Narrow);
            if (!File.Exists(narrowPath))
                return false;

            try
            {
                narrow = ReadMeshFile(narrowPath);

                var widePath = GetMeshPath(landblock, AgentWidth.Wide);
                if (File.Exists(widePath))
                    wide = ReadMeshFile(widePath);

                return narrow != null;
            }
            catch (Exception ex)
            {
                log.Warn($"[Pathfinding] Could not read the mesh files for {landblock:X4} ({ex.GetType().Name}: {ex.Message}); they will be rebuilt");
                narrow = null;
                wide = null;
                return false;
            }
        }

        private static DtNavMesh ReadMeshFile(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0)
                return null;

            var meshData = new DtMeshDataReader().Read(new RcByteBuffer(bytes), VERTS_PER_POLY, true);

            var mesh = new DtNavMesh();
            var status = mesh.Init(meshData, VERTS_PER_POLY, 0);
            if (status.Failed())
                throw new InvalidDataException($"DtNavMesh.Init failed ({status})");

            return mesh;
        }

        private static void WriteMeshFile(string path, DtMeshData meshData)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var tempPath = path + ".tmp";
            using (var stream = File.Create(tempPath))
            using (var writer = new BinaryWriter(stream))
                new DtMeshDataWriter().Write(writer, meshData, RcByteOrder.LITTLE_ENDIAN, false);

            File.Move(tempPath, path, true);
        }

        /// <summary>
        /// Puts freshly built meshes in place of the old ones if the landblock is loaded, so its monsters use them right away
        /// </summary>
        public static void ReplaceLoadedMeshes(ushort landblock, DtNavMesh narrow, DtNavMesh wide)
        {
            if (!loaded.TryGetValue(landblock, out var entry))
                return;

            entry.Narrow = narrow;
            entry.Wide = wide;
            entry.State = narrow != null ? StateReady : StateUnavailable;
        }

        // ---------------------------------------------------------------------------------------------
        // Building
        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds and saves both meshes of a landblock. Never throws (except OperationCanceledException while waiting
        /// for another build to finish); failures are logged and returned in the result.
        /// Runs one build at a time server-wide; call it from a background thread.
        /// </summary>
        public static MeshBuildResult BuildAndSave(ushort landblock, CancellationToken cancellationToken, out DtNavMesh narrow, out DtNavMesh wide)
        {
            narrow = null;
            wide = null;

            var result = new MeshBuildResult { Landblock = landblock };

            buildGate.Wait(cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var dats = new DatSource();

                var cells = LandblockGeometry.LoadEnvCells(landblock, dats, out var missingCells);
                result.Cells = cells.Count;
                result.SkippedPieces += missingCells;

                if (cells.Count == 0)
                {
                    result.Error = "no EnvCells in the cell dat";
                    return result;
                }

                var exporter = new LandblockGeometryExporter(cells, dats);
                exporter.LoadLandblockInfo();
                result.SkippedPieces += exporter.SkippedPieces;

                var geom = CellGeometryProvider.LoadGeometry(exporter);
                if (geom == null)
                {
                    result.Error = "no physics polygons";
                    return result;
                }
                result.Triangles = geom.faces.Length / 3;

                foreach (var agentWidth in AgentWidths)
                {
                    var path = GetMeshPath(landblock, agentWidth);

                    var meshData = new NavMeshBuilder().Build(geom, GetMeshSettings(agentWidth));
                    var polyCount = meshData?.header?.polyCount ?? 0;

                    DtNavMesh mesh = null;
                    if (polyCount > 0)
                    {
                        mesh = new DtNavMesh();
                        var status = mesh.Init(meshData, VERTS_PER_POLY, 0);
                        if (status.Failed())
                        {
                            if (agentWidth == AgentWidth.Narrow)
                            {
                                result.Error = $"DtNavMesh.Init failed for the narrow mesh ({status})";
                                return result;
                            }
                            mesh = null;
                        }
                    }

                    if (mesh == null)
                    {
                        if (agentWidth == AgentWidth.Narrow)
                        {
                            // Recast returns no mesh when nothing is walkable, or when the tile has more than 65535 vertices
                            result.Error = "Recast produced no narrow navmesh (nothing walkable, or too many vertices for one tile)";
                            return result;
                        }

                        // too tight for a wide agent: keep only the narrow mesh, and don't leave an old wide mesh beside it
                        if (File.Exists(path))
                            File.Delete(path);
                        continue;
                    }

                    WriteMeshFile(path, meshData);

                    if (agentWidth == AgentWidth.Narrow)
                    {
                        narrow = mesh;
                        result.NarrowPolys = polyCount;
                    }
                    else
                    {
                        wide = mesh;
                        result.WidePolys = polyCount;
                    }
                }

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"{ex.GetType().Name}: {ex.Message}";
                log.Debug($"[Pathfinding] Mesh build for {landblock:X4} threw: {ex}");
                return result;
            }
            finally
            {
                buildGate.Release();

                result.Seconds = stopwatch.Elapsed.TotalSeconds;

                if (result.Success)
                    failedBuilds.TryRemove(landblock, out _);
                else
                {
                    narrow = null;
                    wide = null;
                    failedBuilds[landblock] = result.Error;
                    log.Warn($"[Pathfinding] Mesh build failed for {landblock:X4}: {result.Error}");
                }
            }
        }

        private static RcNavMeshBuildSettings GetMeshSettings(AgentWidth type)
        {
            switch (type)
            {
                case AgentWidth.Narrow:
                    return new RcNavMeshBuildSettings()
                    {
                        agentHeight = 2f,
                        agentMaxClimb = 0.95f,
                        agentMaxSlope = 50f,
                        cellHeight = 0.1f,
                        cellSize = 0.1f,
                        agentRadius = 0.7f,//0.45f,
                        detailSampleDist = 6.0f,
                        detailSampleMaxError = 1.0f,
                        edgeMaxError = 1f,
                        edgeMaxLen = 12.0f,
                        mergedRegionSize = 20,
                        minRegionSize = 8,
                        vertsPerPoly = VERTS_PER_POLY,
                        partitioning = (int)RcPartition.WATERSHED
                    };
                case AgentWidth.Wide:
                default:
                    return new RcNavMeshBuildSettings()
                    {
                        agentHeight = 2f,
                        agentMaxClimb = 0.95f,
                        agentMaxSlope = 50f,
                        cellHeight = 0.1f,
                        cellSize = 0.1f,
                        agentRadius = 1.4f,
                        detailSampleDist = 6.0f,
                        detailSampleMaxError = 1.0f,
                        edgeMaxError = 1f,
                        edgeMaxLen = 12.0f,
                        mergedRegionSize = 20,
                        minRegionSize = 8,
                        vertsPerPoly = VERTS_PER_POLY,
                        partitioning = (int)RcPartition.WATERSHED
                    };
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Queries. Each one makes its own DtNavMeshQuery, so they are safe on any landblock thread.
        // Positions must be in one landblock; returned positions carry the start position's cell id (as in ClassicACE).
        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// ClassicACE's any-angle search (raycasts while searching). Measured on sample dungeons: about twice the cost of
        /// plain A* (avg 0.35 ms vs 0.17 ms, p99 ~5 ms vs ~3 ms per route), for slightly more direct routes
        /// </summary>
        private const bool AnyAngleRoutes = true;

        private static long randomSeed = DateTime.UtcNow.Ticks;

        private static IRcRand NewRandom() => new RcRand(Interlocked.Increment(ref randomSeed));

        private static RcVec3f ToRecast(Position pos) => new RcVec3f(pos.PositionX, pos.PositionZ, pos.PositionY);

        private static Position FromRecast(uint cell, RcVec3f point) => new Position(cell, new Vector3(point.X, point.Z, point.Y), Quaternion.Identity);

        /// <summary>
        /// Find a route to the end position. Returns the corner points of the path (the first is the start), or null.
        /// If the end can't be reached, the route ends at the closest reachable point.
        /// </summary>
        public static List<Position> FindRoute(Position start, Position end, AgentWidth agentWidth)
        {
            if (start == null || end == null || (start.Cell >> 16) != (end.Cell >> 16))
                return null;

            if (!TryGetMesh(start, agentWidth, out var mesh))
                return null;

            return FindRoute(mesh, start, end);
        }

        internal static List<Position> FindRoute(DtNavMesh mesh, Position start, Position end)
        {
            var query = new DtNavMeshQuery(mesh);
            var filter = new DtQueryDefaultFilter();

            query.FindNearestPoly(ToRecast(start), HalfExtents, filter, out long startRef, out var startPt, out _);
            query.FindNearestPoly(ToRecast(end), HalfExtents, filter, out long endRef, out var endPt, out _);

            if (startRef == 0 || endRef == 0)
                return null;

            var tool = new RcTestNavMeshTool();
            var polys = new List<long>();
            var path = new DtStraightPath[MAX_POLYS];

            var status = tool.FindStraightPath(query, startRef, endRef, startPt, endPt, filter, AnyAngleRoutes, ref polys, path, out var straightPathCount, MAX_POLYS, 0);
            if (status.Failed() || straightPathCount == 0)
                return null;

            var route = new List<Position>(straightPathCount);
            for (var i = 0; i < straightPathCount; i++)
                route.Add(FromRecast(start.Cell, path[i].pos));

            return route;
        }

        /// <summary>
        /// ClassicACE's monster route: the wide mesh first, the narrow one if the wide route is missing or stops more
        /// than 2 units short of the end. Of two partial routes, the one that gets closer wins.
        /// </summary>
        public static List<Position> FindRouteAnyWidth(Position start, Position end)
        {
            var route = FindRoute(start, end, AgentWidth.Wide);

            if (route == null || route.Count == 0)
                return FindRoute(start, end, AgentWidth.Narrow);

            var wideMiss = end.DistanceTo(route[route.Count - 1]);
            if (wideMiss > 2.0f)
            {
                var narrowRoute = FindRoute(start, end, AgentWidth.Narrow);
                if (narrowRoute != null && narrowRoute.Count > 0 && end.DistanceTo(narrowRoute[narrowRoute.Count - 1]) < wideMiss)
                    return narrowRoute;
            }
            return route;
        }

        public static bool GetRouteDistance(Position start, Position end, AgentWidth agentWidth, out float distance)
        {
            distance = 0f;

            var route = FindRoute(start, end, agentWidth);
            if (route == null || route.Count == 0 || route[route.Count - 1].DistanceTo(end) > 1)
                return false;

            var previousRouteEntry = start;
            foreach (var routeEntry in route)
            {
                distance += previousRouteEntry.DistanceTo(routeEntry);
                previousRouteEntry = routeEntry;
            }
            return true;
        }

        /// <summary>
        /// Get a random point anywhere on the landblock's navmesh
        /// </summary>
        public static Position GetRandomPointOnMesh(Position start, AgentWidth agentWidth)
        {
            if (!TryGetMesh(start, agentWidth, out var mesh))
                return null;

            return GetRandomPointOnMesh(mesh, start.Cell);
        }

        internal static Position GetRandomPointOnMesh(DtNavMesh mesh, uint cell)
        {
            var query = new DtNavMeshQuery(mesh);
            var filter = new DtQueryDefaultFilter();

            var status = query.FindRandomPoint(filter, NewRandom(), out long randomRef, out var randomPt);
            if (status.Failed() || randomRef == 0)
                return null;

            return FromRecast(cell, randomPt);
        }

        public static Position GetRandomPointWithinCircle(Position location, float radius, AgentWidth agentWidth)
        {
            if (!TryGetMesh(location, agentWidth, out var mesh))
                return null;

            return GetRandomPointWithinCircle(mesh, location, radius);
        }

        internal static Position GetRandomPointWithinCircle(DtNavMesh mesh, Position location, float radius)
        {
            var query = new DtNavMeshQuery(mesh);
            var filter = new DtQueryDefaultFilter();

            query.FindNearestPoly(ToRecast(location), HalfExtents, filter, out long startRef, out var startPt, out _);
            if (startRef == 0)
                return null;

            var status = query.FindRandomPointWithinCircle(startRef, startPt, radius, filter, NewRandom(), out long randomRef, out var randomPt);
            if (status.Failed() || randomRef == 0)
                return null;

            return FromRecast(location.Cell, randomPt);
        }

        public static Position GetClosestPointOnMesh(Position location, AgentWidth agentWidth)
        {
            if (!TryGetMesh(location, agentWidth, out var mesh))
                return null;

            return GetClosestPointOnMesh(mesh, location);
        }

        internal static Position GetClosestPointOnMesh(DtNavMesh mesh, Position location)
        {
            var query = new DtNavMeshQuery(mesh);
            var filter = new DtQueryDefaultFilter();

            query.FindNearestPoly(ToRecast(location), HalfExtents, filter, out long startRef, out var startPt, out _);
            if (startRef == 0)
                return null;

            return FromRecast(location.Cell, startPt);
        }

        /// <summary>
        /// The nearest wall point within radius, rotated to face along the wall normal (away from the wall),
        /// or into the wall with inverseNormal
        /// </summary>
        public static Position GetNearestWallPosition(Position location, float radius, AgentWidth agentWidth, out float distance, bool inverseNormal = false)
        {
            distance = float.MaxValue;

            if (!TryGetMesh(location, agentWidth, out var mesh))
                return null;

            return GetNearestWallPosition(mesh, location, radius, out distance, inverseNormal);
        }

        internal static Position GetNearestWallPosition(DtNavMesh mesh, Position location, float radius, out float distance, bool inverseNormal = false)
        {
            distance = float.MaxValue;

            var query = new DtNavMeshQuery(mesh);
            var filter = new DtQueryDefaultFilter();

            query.FindNearestPoly(ToRecast(location), HalfExtents, filter, out long startRef, out var startPt, out _);
            if (startRef == 0)
                return null;

            // with no wall inside the radius, Detour reports the radius as the distance (and a zero hit point)
            var status = query.FindDistanceToWall(startRef, startPt, radius, filter, out var hitDist, out var wallPt, out var wallNormal);
            if (status.Failed() || hitDist >= radius || (wallNormal.X == 0 && wallNormal.Y == 0 && wallNormal.Z == 0))
                return null;

            distance = hitDist;

            var position = FromRecast(location.Cell, wallPt);

            if (inverseNormal)
                position.Rotate(new Vector3(-wallNormal.X, -wallNormal.Z, -wallNormal.Y));
            else
                position.Rotate(new Vector3(wallNormal.X, wallNormal.Z, wallNormal.Y));

            return position;
        }
    }
}
