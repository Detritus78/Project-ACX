using System;
using System.Collections.Generic;

using ACE.Entity.Enum;
using ACE.Server.Network;
using ACE.Server.Pathfinding;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: pathfinding admin commands. Both run in the background and also work from the server console.
    /// Monster pathfinding itself is the 'pathfinding' server bool: /modifybool pathfinding true|false
    /// </summary>
    public static class ACXPathfindingCommands
    {
        [CommandHandler("acxmeshbuild", AccessLevel.Admin, CommandHandlerFlag.None, 1,
            "Builds pathfinding navmeshes in the background, one dungeon at a time. all: every dungeon landblock that has no mesh yet (all force: rebuild every one). here: rebuild the dungeon you are in.",
            "all [force] | here | status | stop")]
        public static void HandleAcxMeshBuild(Session session, params string[] parameters)
        {
            void report(string message) => CommandHandlerHelper.WriteOutputInfo(session, message);

            // the job logs its own summary, which the console already shows
            var jobReport = session == null ? null : (Action<string>)report;

            string startMessage;

            switch (parameters[0].ToLowerInvariant())
            {
                case "all":
                    var force = parameters.Length > 1 && parameters[1].Equals("force", StringComparison.OrdinalIgnoreCase);
                    MeshBuildJob.Start("all dungeons", DungeonLandblocks.GetAll, force, jobReport, out startMessage);
                    report(startMessage);
                    return;

                case "here":
                    var location = session?.Player?.Location;
                    if (location == null)
                    {
                        report("/acxmeshbuild here needs a player in the world.");
                        return;
                    }

                    var landblock = (ushort)location.Landblock;
                    if (!DungeonLandblocks.IsDungeon(landblock))
                    {
                        report($"Landblock {landblock:X4} is not a dungeon (a landblock with EnvCells only): no mesh is built for it.");
                        return;
                    }

                    MeshBuildJob.Start($"landblock {landblock:X4}", () => new List<ushort> { landblock }, true, jobReport, out startMessage);
                    report(startMessage);
                    return;

                case "status":
                    report(MeshBuildJob.Status());
                    report($"Pathfinding is {(Pathfinder.Enabled ? "ON" : "OFF")} (/modifybool pathfinding true|false). {Pathfinder.LoadedLandblockCount} landblocks have meshes in memory, {Pathfinder.FailedBuilds.Count} builds failed since the server started. Meshes: {Pathfinder.MeshDirectory}");
                    return;

                case "stop":
                    report(MeshBuildJob.Stop() ? "Stopping the mesh build after the current landblock." : "No mesh build is running.");
                    return;

                default:
                    report("Usage: /acxmeshbuild all [force] | here | status | stop");
                    return;
            }
        }

        [CommandHandler("acxpathtest", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Stress-tests pathfinding in the background: random route, random-point and wall queries on every saved dungeon mesh. Logs exceptions, failures and timing, and writes the details to Pathfinding/Logs.",
            "[routes per dungeon, default 50] [threads per mesh, default 2] | stop")]
        public static void HandleAcxPathTest(Session session, params string[] parameters)
        {
            void report(string message) => CommandHandlerHelper.WriteOutputInfo(session, message);

            if (parameters.Length > 0 && parameters[0].Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                report(PathStressTest.Stop() ? "Stopping the path test after the current dungeon." : "No path test is running.");
                return;
            }

            var routesPerDungeon = PathStressTest.DefaultRoutesPerDungeon;
            var threads = PathStressTest.DefaultThreads;

            if (parameters.Length > 0 && (!int.TryParse(parameters[0], out routesPerDungeon) || routesPerDungeon < 1 || routesPerDungeon > 10000))
            {
                report("Usage: /acxpathtest [routes per dungeon, 1-10000] [threads per mesh, 1-8] | stop");
                return;
            }

            if (parameters.Length > 1 && (!int.TryParse(parameters[1], out threads) || threads < 1 || threads > 8))
            {
                report("Usage: /acxpathtest [routes per dungeon, 1-10000] [threads per mesh, 1-8] | stop");
                return;
            }

            // the test logs its own summary, which the console already shows
            PathStressTest.Start(routesPerDungeon, threads, session == null ? null : report, out var startMessage);
            report(startMessage);
        }
    }
}
