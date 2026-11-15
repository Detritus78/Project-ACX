using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using log4net;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// Project ACX: /acxmeshbuild. Builds and saves navmeshes on one background thread, one landblock at a time, pausing
    /// after each build (half as long as the build took, at least 100 ms) so the world keeps ticking. Every failure is
    /// logged; the run ends with a built / failed / skipped summary in the server log and in Pathfinding/Logs.
    /// </summary>
    public static class MeshBuildJob
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Pause after each build, as a fraction of the time it took: the builder uses at most 2/3 of one core
        /// </summary>
        private const double ThrottleFraction = 0.5;
        private const int MinPauseMs = 100;

        private static readonly object jobLock = new object();
        private static Thread thread;
        private static CancellationTokenSource cancellation;

        private static string jobName;
        private static int total, done, built, failed, skipped;
        private static int current = -1;
        private static DateTime startTime;

        public static bool IsRunning
        {
            get { lock (jobLock) return thread != null && thread.IsAlive; }
        }

        /// <summary>
        /// Starts a build. getLandblocks runs on the build thread (listing every dungeon reads the whole cell dat index).
        /// With force, landblocks that already have mesh files are rebuilt; otherwise they are skipped.
        /// report receives the start, the summary and any job error (it is called on the build thread).
        /// </summary>
        public static bool Start(string name, Func<List<ushort>> getLandblocks, bool force, Action<string> report, out string message)
        {
            lock (jobLock)
            {
                if (thread != null && thread.IsAlive)
                {
                    message = $"A mesh build is already running ({Progress()}). Use /acxmeshbuild status or /acxmeshbuild stop.";
                    return false;
                }

                cancellation = new CancellationTokenSource();
                var token = cancellation.Token;

                jobName = name;
                total = done = built = failed = skipped = 0;
                current = -1;
                startTime = DateTime.UtcNow;

                thread = new Thread(() => Run(name, getLandblocks, force, report, token))
                {
                    IsBackground = true,
                    Name = "ACX navmesh build",
                    Priority = ThreadPriority.BelowNormal
                };
                thread.Start();

                message = $"Mesh build started in the background: {name}{(force ? " (rebuilding existing meshes)" : "")}. Progress goes to the server log; /acxmeshbuild status shows it here.";
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

        public static string Status()
        {
            lock (jobLock)
            {
                if (thread == null)
                    return "No mesh build has run since the server started.";

                return thread.IsAlive ? $"Mesh build running: {Progress()}" : $"Last mesh build finished: {Progress()}";
            }
        }

        private static string Progress()
        {
            var elapsed = DateTime.UtcNow - startTime;
            var text = $"{jobName}: {done}/{total} done, {built} built, {failed} failed, {skipped} skipped, {elapsed:hh\\:mm\\:ss} elapsed";

            var currentLandblock = current;
            if (currentLandblock >= 0 && thread != null && thread.IsAlive)
                text += $", building {currentLandblock:X4}";

            var attempted = built + failed;
            var remaining = total - done;
            if (attempted > 0 && remaining > 0 && thread != null && thread.IsAlive)
            {
                // skipped landblocks take no time, so estimate from the ones actually built
                var perBuild = elapsed.TotalSeconds / attempted;
                text += $", about {TimeSpan.FromSeconds(perBuild * remaining):hh\\:mm\\:ss} left at most";
            }
            return text;
        }

        private static void Run(string name, Func<List<ushort>> getLandblocks, bool force, Action<string> report, CancellationToken token)
        {
            var results = new List<MeshBuildResult>();
            var cancelled = false;
            string jobError = null;

            try
            {
                var landblocks = getLandblocks();
                total = landblocks.Count;

                log.Info($"[Pathfinding] Mesh build started ({name}): {total} landblocks{(force ? ", rebuilding existing meshes" : "")}");

                var progressTimer = Stopwatch.StartNew();

                foreach (var landblock in landblocks)
                {
                    if (token.IsCancellationRequested)
                    {
                        cancelled = true;
                        break;
                    }

                    if (!force && Pathfinder.HasMeshFile(landblock))
                    {
                        Interlocked.Increment(ref skipped);
                        Interlocked.Increment(ref done);
                        continue;
                    }

                    current = landblock;

                    MeshBuildResult result;
                    try
                    {
                        result = Pathfinder.BuildAndSave(landblock, token, out var narrow, out var wide);

                        if (result.Success)
                            Pathfinder.ReplaceLoadedMeshes(landblock, narrow, wide);
                    }
                    catch (OperationCanceledException)
                    {
                        cancelled = true;
                        break;
                    }

                    results.Add(result);

                    if (result.Success)
                        Interlocked.Increment(ref built);
                    else
                        Interlocked.Increment(ref failed);      // BuildAndSave logged the reason

                    Interlocked.Increment(ref done);
                    current = -1;

                    if (progressTimer.Elapsed.TotalSeconds >= 60)
                    {
                        progressTimer.Restart();
                        log.Info($"[Pathfinding] Mesh build progress: {Progress()}");
                    }

                    // throttle
                    var pauseMs = Math.Max(MinPauseMs, (int)(result.Seconds * 1000 * ThrottleFraction));
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
                log.Error($"[Pathfinding] Mesh build ({name}) stopped by an error: {ex}");
            }
            finally
            {
                current = -1;

                var summary = $"Mesh build {(cancelled ? "stopped" : jobError != null ? "aborted" : "finished")} ({name}): {built} built, {failed} failed, {skipped} skipped (already had meshes) of {total}, in {DateTime.UtcNow - startTime:hh\\:mm\\:ss}.";

                var logFile = WriteLogFile(name, summary, results, jobError);
                if (logFile != null)
                    summary += $" Details: {logFile}";

                log.Info($"[Pathfinding] {summary}");

                foreach (var failure in results.Where(r => !r.Success).Take(10))
                    summary += $"\n  {failure.Landblock:X4}: {failure.Error}";
                if (failed > 10)
                    summary += $"\n  ... and {failed - 10} more in the log file";

                try
                {
                    report?.Invoke(summary);
                }
                catch (Exception ex)
                {
                    log.Warn($"[Pathfinding] Could not report the mesh build summary: {ex.Message}");
                }
            }
        }

        private static string WriteLogFile(string name, string summary, List<MeshBuildResult> results, string jobError)
        {
            try
            {
                Directory.CreateDirectory(Pathfinder.LogDirectory);
                var path = Path.Combine(Pathfinder.LogDirectory, $"meshbuild-{DateTime.Now:yyyyMMdd-HHmmss}.log");

                var sb = new StringBuilder();
                sb.AppendLine($"Project ACX navmesh build: {name}");
                sb.AppendLine(summary);
                if (jobError != null)
                    sb.AppendLine($"Job error: {jobError}");

                var failures = results.Where(r => !r.Success).ToList();
                sb.AppendLine();
                sb.AppendLine($"Failed ({failures.Count}):");
                foreach (var r in failures)
                    sb.AppendLine($"  {r.Landblock:X4}  {r.Seconds,7:N1}s  cells={r.Cells}  triangles={r.Triangles}  {r.Error}");

                var builds = results.Where(r => r.Success).ToList();
                sb.AppendLine();
                sb.AppendLine($"Built ({builds.Count}):  landblock  seconds  cells  triangles  narrow-polys  wide-polys  missing-dat-pieces");
                foreach (var r in builds.OrderByDescending(r => r.Seconds))
                    sb.AppendLine($"  {r.Landblock:X4}  {r.Seconds,7:N1}s  {r.Cells,5}  {r.Triangles,7}  {r.NarrowPolys,6}  {r.WidePolys,6}  {r.SkippedPieces}");

                File.WriteAllText(path, sb.ToString());
                return path;
            }
            catch (Exception ex)
            {
                log.Warn($"[Pathfinding] Could not write the mesh build log: {ex.Message}");
                return null;
            }
        }
    }
}
