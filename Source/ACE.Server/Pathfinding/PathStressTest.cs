using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using DotRecast.Detour;

using log4net;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// Project ACX: /acxpathtest. For every dungeon with a saved mesh, loads the mesh files (separately from the live
    /// cache), then runs random FindRoute / GetRandomPointWithinCircle / GetNearestWallPosition queries on both widths,
    /// several threads sharing each mesh the way landblock threads do. Logs exceptions, failures and timing, and writes
    /// the details to Pathfinding/Logs.
    /// </summary>
    public static class PathStressTest
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public const int DefaultRoutesPerDungeon = 50;
        public const int DefaultThreads = 2;

        /// <summary>
        /// Exceptions written to the server log; all of them go to the details file
        /// </summary>
        private const int MaxLoggedExceptions = 20;

        private const float CircleRadius = 40.0f;
        private const float WallRadius = 5.0f;

        private static readonly object jobLock = new object();
        private static Thread thread;
        private static CancellationTokenSource cancellation;
        private static int total, done;
        private static DateTime startTime;

        private sealed class MeshStats
        {
            public ushort Landblock;
            public AgentWidth Width;
            public int Polys;
            public int Routes, RouteOk, RoutePartial, RouteNone, BadPoints, NoRandomPoint;
            public int Circles, CircleNone;
            public int Walls, WallNone;
            public int Exceptions;
            public double RouteMsTotal, RouteMsMax, CircleMsMax;
            public readonly List<double> RouteTimes = new List<double>();
        }

        public static bool IsRunning
        {
            get { lock (jobLock) return thread != null && thread.IsAlive; }
        }

        public static bool Start(int routesPerDungeon, int threads, Action<string> report, out string message)
        {
            lock (jobLock)
            {
                if (thread != null && thread.IsAlive)
                {
                    message = $"A path test is already running: {done}/{total} dungeons. /acxpathtest stop ends it.";
                    return false;
                }

                cancellation = new CancellationTokenSource();
                var token = cancellation.Token;
                total = done = 0;
                startTime = DateTime.UtcNow;

                thread = new Thread(() => Run(routesPerDungeon, threads, report, token))
                {
                    IsBackground = true,
                    Name = "ACX path test",
                    Priority = ThreadPriority.BelowNormal
                };
                thread.Start();

                message = $"Path test started in the background: {routesPerDungeon} random routes per dungeon and mesh width, {threads} threads per mesh. Summary here when done; progress in the server log.";
                return true;
            }
        }

        public static bool Stop()
        {
            lock (jobLock)
            {
                if (thread == null || !thread.IsAlive)
                    return false;

                cancellation.Cancel();
                return true;
            }
        }

        /// <summary>
        /// Landblocks with a saved narrow mesh file
        /// </summary>
        public static List<ushort> GetLandblocksWithMeshes()
        {
            var landblocks = new List<ushort>();

            if (!Directory.Exists(Pathfinder.MeshDirectory))
                return landblocks;

            foreach (var file in Directory.EnumerateFiles(Pathfinder.MeshDirectory, "*.mesh"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (uint.TryParse(name, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var meshId) && (meshId & 0xFFFF) == (uint)AgentWidth.Narrow)
                    landblocks.Add((ushort)(meshId >> 16));
            }

            landblocks.Sort();
            return landblocks;
        }

        private static void Run(int routesPerDungeon, int threads, Action<string> report, CancellationToken token)
        {
            var stats = new List<MeshStats>();
            var loadFailures = new List<string>();
            var exceptionDetails = new List<string>();
            var loggedExceptions = 0;
            var cancelled = false;
            string jobError = null;

            try
            {
                var landblocks = GetLandblocksWithMeshes();
                total = landblocks.Count;

                log.Info($"[Pathfinding] Path test started: {total} dungeons with meshes, {routesPerDungeon} routes per mesh width, {threads} threads");

                var progressTimer = Stopwatch.StartNew();

                foreach (var landblock in landblocks)
                {
                    if (token.IsCancellationRequested)
                    {
                        cancelled = true;
                        break;
                    }

                    var dungeonTimer = Stopwatch.StartNew();

                    DtNavMesh narrow = null, wide = null;
                    try
                    {
                        if (!Pathfinder.TryReadMeshes(landblock, out narrow, out wide))
                            loadFailures.Add($"{landblock:X4}: mesh files could not be read (see the server log)");
                    }
                    catch (Exception ex)
                    {
                        loadFailures.Add($"{landblock:X4}: {ex.GetType().Name}: {ex.Message}");
                    }

                    foreach (var (width, mesh) in new[] { (AgentWidth.Narrow, narrow), (AgentWidth.Wide, wide) })
                    {
                        if (mesh == null)
                            continue;

                        var meshStats = TestMesh(landblock, width, mesh, routesPerDungeon, threads, token, exceptionDetails);
                        stats.Add(meshStats);

                        if (meshStats.Exceptions > 0 && loggedExceptions < MaxLoggedExceptions)
                        {
                            loggedExceptions++;
                            lock (exceptionDetails)
                                log.Warn($"[Pathfinding] Path test {landblock:X4} {width}: {meshStats.Exceptions} queries threw. First: {exceptionDetails.FirstOrDefault(e => e.StartsWith($"{landblock:X4} {width} "))}");
                        }
                    }

                    Interlocked.Increment(ref done);

                    if (progressTimer.Elapsed.TotalSeconds >= 60)
                    {
                        progressTimer.Restart();
                        log.Info($"[Pathfinding] Path test progress: {done}/{total} dungeons, {stats.Sum(s => s.Exceptions)} exceptions so far");
                    }

                    // throttle: rest half as long as the dungeon took, at least 50 ms
                    var pauseMs = Math.Max(50, (int)(dungeonTimer.Elapsed.TotalMilliseconds * 0.5));
                    if (token.WaitHandle.WaitOne(pauseMs))
                    {
                        cancelled = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                jobError = ex.ToString();
                log.Error($"[Pathfinding] Path test stopped by an error: {ex}");
            }
            finally
            {
                var summary = BuildSummary(stats, loadFailures, cancelled, jobError);

                var logFile = WriteLogFile(summary, stats, loadFailures, exceptionDetails, jobError);
                if (logFile != null)
                    summary += $"\nDetails: {logFile}";

                log.Info($"[Pathfinding] {summary}");

                try
                {
                    report?.Invoke(summary);
                }
                catch (Exception ex)
                {
                    log.Warn($"[Pathfinding] Could not report the path test summary: {ex.Message}");
                }
            }
        }

        private static MeshStats TestMesh(ushort landblock, AgentWidth width, DtNavMesh mesh, int routes, int threads, CancellationToken token, List<string> exceptionDetails)
        {
            var stats = new MeshStats { Landblock = landblock, Width = width, Polys = CountPolys(mesh) };
            var cell = ((uint)landblock << 16) | 0x0100;

            try
            {
                Parallel.For(0, routes, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, threads), CancellationToken = token }, i =>
                {
                    var stage = "random point";
                    try
                    {
                        var start = Pathfinder.GetRandomPointOnMesh(mesh, cell);
                        var end = Pathfinder.GetRandomPointOnMesh(mesh, cell);
                        if (start == null || end == null)
                        {
                            lock (stats) stats.NoRandomPoint++;
                            return;
                        }

                        stage = $"FindRoute {start.ToLOCString()} -> {end.ToLOCString()}";
                        var timer = Stopwatch.StartNew();
                        var route = Pathfinder.FindRoute(mesh, start, end);
                        var routeMs = timer.Elapsed.TotalMilliseconds;

                        var badPoints = route != null && route.Any(p => !float.IsFinite(p.PositionX) || !float.IsFinite(p.PositionY) || !float.IsFinite(p.PositionZ));

                        stage = $"GetRandomPointWithinCircle {start.ToLOCString()}";
                        timer.Restart();
                        var circlePoint = Pathfinder.GetRandomPointWithinCircle(mesh, start, CircleRadius);
                        var circleMs = timer.Elapsed.TotalMilliseconds;

                        stage = $"GetNearestWallPosition {start.ToLOCString()}";
                        var wall = Pathfinder.GetNearestWallPosition(mesh, start, WallRadius, out _);

                        lock (stats)
                        {
                            stats.Routes++;
                            stats.RouteTimes.Add(routeMs);
                            stats.RouteMsTotal += routeMs;
                            stats.RouteMsMax = Math.Max(stats.RouteMsMax, routeMs);

                            if (route == null || route.Count == 0)
                                stats.RouteNone++;
                            else if (route[route.Count - 1].DistanceTo(end) > 1.0f)
                                stats.RoutePartial++;   // the end is in a part of the mesh the start can't walk to
                            else
                                stats.RouteOk++;

                            if (badPoints)
                                stats.BadPoints++;

                            stats.Circles++;
                            stats.CircleMsMax = Math.Max(stats.CircleMsMax, circleMs);
                            if (circlePoint == null)
                                stats.CircleNone++;

                            stats.Walls++;
                            if (wall == null)
                                stats.WallNone++;
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (stats)
                            stats.Exceptions++;
                        lock (exceptionDetails)
                            exceptionDetails.Add($"{landblock:X4} {width} {stage}: {ex}");
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // /acxpathtest stop: keep what was measured
            }

            return stats;
        }

        private static int CountPolys(DtNavMesh mesh)
        {
            var polys = 0;
            for (var i = 0; i < mesh.GetMaxTiles(); i++)
            {
                var tile = mesh.GetTile(i);
                if (tile?.data?.header != null)
                    polys += tile.data.header.polyCount;
            }
            return polys;
        }

        private static double Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0)
                return 0;
            var index = (int)Math.Ceiling(p * sorted.Count) - 1;
            return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
        }

        private static string BuildSummary(List<MeshStats> stats, List<string> loadFailures, bool cancelled, string jobError)
        {
            var routeTimes = stats.SelectMany(s => s.RouteTimes).OrderBy(t => t).ToList();
            var routes = stats.Sum(s => s.Routes);
            var slowest = stats.OrderByDescending(s => s.RouteMsMax).FirstOrDefault();

            var sb = new StringBuilder();
            sb.Append($"Path test {(cancelled ? "stopped" : jobError != null ? "aborted" : "finished")} in {DateTime.UtcNow - startTime:hh\\:mm\\:ss}: ");
            sb.Append($"{done}/{total} dungeons, {stats.Count} meshes, {loadFailures.Count} load failures, {stats.Sum(s => s.Exceptions)} exceptions.");
            sb.Append($"\nRoutes: {routes} ({stats.Sum(s => s.RouteOk)} reached the end, {stats.Sum(s => s.RoutePartial)} partial (unconnected areas), {stats.Sum(s => s.RouteNone)} none, {stats.Sum(s => s.BadPoints)} with bad points), {stats.Sum(s => s.NoRandomPoint)} times no random point.");
            sb.Append($"\nRoute time: avg {(routes > 0 ? stats.Sum(s => s.RouteMsTotal) / routes : 0):N2} ms, p95 {Percentile(routeTimes, 0.95):N2} ms, p99 {Percentile(routeTimes, 0.99):N2} ms, max {(slowest?.RouteMsMax ?? 0):N1} ms{(slowest != null ? $" ({slowest.Landblock:X4} {slowest.Width})" : "")}.");
            sb.Append($"\nRandom point in circle: {stats.Sum(s => s.Circles)} ({stats.Sum(s => s.CircleNone)} none, max {(stats.Count > 0 ? stats.Max(s => s.CircleMsMax) : 0):N1} ms). Nearest wall: {stats.Sum(s => s.Walls)} ({stats.Sum(s => s.WallNone)} none within {WallRadius}).");
            return sb.ToString();
        }

        private static string WriteLogFile(string summary, List<MeshStats> stats, List<string> loadFailures, List<string> exceptionDetails, string jobError)
        {
            try
            {
                Directory.CreateDirectory(Pathfinder.LogDirectory);
                var path = Path.Combine(Pathfinder.LogDirectory, $"pathtest-{DateTime.Now:yyyyMMdd-HHmmss}.log");

                var sb = new StringBuilder();
                sb.AppendLine("Project ACX path test");
                sb.AppendLine(summary);
                if (jobError != null)
                    sb.AppendLine($"Job error: {jobError}");

                sb.AppendLine();
                sb.AppendLine($"Load failures ({loadFailures.Count}):");
                foreach (var failure in loadFailures)
                    sb.AppendLine($"  {failure}");

                lock (exceptionDetails)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Exceptions ({exceptionDetails.Count}):");
                    foreach (var exception in exceptionDetails)
                        sb.AppendLine($"  {exception}");
                }

                sb.AppendLine();
                sb.AppendLine("Per mesh, slowest first:  landblock width polys | routes ok partial none bad-points | avg-ms max-ms | circle-none wall-none | exceptions");
                foreach (var s in stats.OrderByDescending(s => s.RouteMsMax))
                {
                    var avg = s.Routes > 0 ? s.RouteMsTotal / s.Routes : 0;
                    sb.AppendLine($"  {s.Landblock:X4} {s.Width,-6} {s.Polys,6} | {s.Routes,4} {s.RouteOk,4} {s.RoutePartial,4} {s.RouteNone,4} {s.BadPoints,3} | {avg,7:N2} {s.RouteMsMax,8:N1} | {s.CircleNone,4} {s.WallNone,4} | {s.Exceptions}");
                }

                File.WriteAllText(path, sb.ToString());
                return path;
            }
            catch (Exception ex)
            {
                log.Warn($"[Pathfinding] Could not write the path test log: {ex.Message}");
                return null;
            }
        }
    }
}
