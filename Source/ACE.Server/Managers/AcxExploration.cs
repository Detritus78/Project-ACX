using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Pathfinding;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: dungeon exploration markers (JJ 2026-11-12). Each dungeon with an entrance from the outside world has a
    /// marker at the walkable spot farthest from its entrance. The first time a character uses a dungeon's marker they get
    /// experience scaled to the dungeon's level, and it counts on the explored leaderboard.
    /// Markers are placed by acx/Content/exploration/10-dungeon-markers.sql; names and levels come from markers.json.
    /// The spots are worked out with /acxmarkerplan from the dungeon meshes (Pathfinding/).
    /// </summary>
    public static class AcxExploration
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public class Dungeon
        {
            public string Landblock { get; set; }
            public string Name { get; set; }
            public int Level { get; set; }
        }

        private static Dictionary<ushort, Dungeon> dungeons;

        private static string MarkersPath => Path.Combine(PropertyManager.GetString("content_folder").Item, "exploration", "markers.json");

        private static Dictionary<ushort, Dungeon> Dungeons
        {
            get
            {
                if (dungeons != null)
                    return dungeons;
                try
                {
                    var list = JsonSerializer.Deserialize<List<Dungeon>>(File.ReadAllText(MarkersPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    dungeons = list.ToDictionary(d => Convert.ToUInt16(d.Landblock, 16));
                }
                catch (Exception e)
                {
                    log.Error($"AcxExploration: couldn't read {MarkersPath}: {e.Message}");
                    dungeons = new();
                }
                return dungeons;
            }
        }

        public static bool IsMarker(WorldObject wo) => wo.GetProperty(PropertyBool.AcxExplorationMarker) == true;

        private static string MarkerFlag(ushort landblock) => $"AcxDungeonMarker_{landblock:X4}";

        /// <summary>
        /// Called from Creature.ActOnUse: the first use per character pays out and counts the dungeon as explored
        /// </summary>
        public static void OnMarkerUse(Creature marker, Player player)
        {
            var landblock = (ushort)(marker.Location.LandblockId.Raw >> 16);
            AcxContracts.OnMarkerReached(player, landblock);
            Dungeons.TryGetValue(landblock, out var dungeon);
            var name = dungeon?.Name ?? "this dungeon";

            if (player.QuestManager.HasQuest(MarkerFlag(landblock)))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You've already explored {name}.", ChatMessageType.Broadcast));
                return;
            }

            player.QuestManager.Stamp(MarkerFlag(landblock));

            var level = Math.Max(1, dungeon?.Level ?? 1);
            player.GrantLevelProportionalXp(0.1, 0, (long)level * level * 200);

            player.SetProperty(PropertyInt.AcxDungeonsExplored, (player.GetProperty(PropertyInt.AcxDungeonsExplored) ?? 0) + 1);
            AcxMarks.Give(player, "marker");   // one ACX Mark per marker (JJ 2026-11-13)
            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Dungeon explored: {name}! You've reached its deepest point and found an ACX Mark.", ChatMessageType.Broadcast));
            AcxLeaderboards.CheckLeader(AcxLeaderboards.Board.Explored);
        }

        // ---- planning the spots ----

        public class PlanInput
        {
            public string Landblock { get; set; }
            public string Name { get; set; }
            public int Level { get; set; }
            public uint Cell { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
        }

        public class PlanOutput : PlanInput
        {
            public float Distance { get; set; }
            public string Status { get; set; }
        }

        private static CancellationTokenSource planCancel;

        /// <summary>
        /// /acxmarkerplan: for each dungeon in Pathfinding/marker-input.json, samples points on its mesh and keeps the one
        /// farthest (by walking route) from the entrance. Writes Pathfinding/marker-plan.json. Runs in the background.
        /// </summary>
        public static string StartPlan(int samples)
        {
            if (planCancel != null)
                return "A marker plan is already running.";

            var inputPath = Path.Combine(Pathfinder.RootDirectory, "marker-input.json");
            var outputPath = Path.Combine(Pathfinder.RootDirectory, "marker-plan.json");
            if (!File.Exists(inputPath))
                return $"No {inputPath}.";

            var input = JsonSerializer.Deserialize<List<PlanInput>>(File.ReadAllText(inputPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            planCancel = new CancellationTokenSource();
            var token = planCancel.Token;

            Task.Factory.StartNew(() =>
            {
                var output = new List<PlanOutput>();
                try
                {
                    foreach (var d in input)
                    {
                        if (token.IsCancellationRequested)
                            break;
                        output.Add(PlanOne(d, samples, token));
                    }
                    File.WriteAllText(outputPath, JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
                    log.Info($"[Markers] Plan done: {output.Count(o => o.Status == "ok")} of {output.Count} dungeons placed; written to {outputPath}");
                }
                catch (Exception e)
                {
                    log.Error($"[Markers] Plan failed: {e}");
                }
                finally
                {
                    planCancel = null;
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            return $"Marker plan started for {input.Count} dungeons ({samples} samples each). Progress goes to the server log.";
        }

        private static PlanOutput PlanOne(PlanInput d, int samples, CancellationToken token)
        {
            var result = new PlanOutput { Landblock = d.Landblock, Name = d.Name, Level = d.Level, Status = "no mesh" };
            var landblock = Convert.ToUInt16(d.Landblock, 16);
            if (!Pathfinder.HasMeshFile(landblock))
                return result;

            // load the meshes and wait for them
            var waited = 0;
            while (!Pathfinder.RequestMeshes(landblock) && waited < 60000 && !token.IsCancellationRequested)
            {
                Thread.Sleep(100);
                waited += 100;
            }

            try
            {
                var entrance = new Position(d.Cell, new System.Numerics.Vector3(d.X, d.Y, d.Z), System.Numerics.Quaternion.Identity);
                var start = Pathfinder.GetClosestPointOnMesh(entrance, AgentWidth.Narrow);
                if (start == null)
                {
                    result.Status = "entrance off the mesh";
                    return result;
                }

                Position best = null;
                var bestDistance = 0f;
                for (var i = 0; i < samples; i++)
                {
                    var point = Pathfinder.GetRandomPointOnMesh(start, AgentWidth.Narrow);
                    if (point == null)
                        continue;
                    if (Pathfinder.GetRouteDistance(start, point, AgentWidth.Narrow, out var distance) && distance > bestDistance)
                    {
                        best = point;
                        bestDistance = distance;
                    }
                }

                if (best == null)
                {
                    result.Status = "no reachable point";
                    return result;
                }

                result.Cell = best.Cell;
                result.X = best.PositionX;
                result.Y = best.PositionY;
                result.Z = best.PositionZ;
                result.Distance = bestDistance;
                result.Status = "ok";
                log.Info($"[Markers] {d.Landblock} {d.Name}: {bestDistance:0} m from the entrance");
                return result;
            }
            catch (Exception e)
            {
                result.Status = $"error: {e.Message}";
                return result;
            }
            finally
            {
                Pathfinder.UnloadMeshes(landblock);
            }
        }

        public static void StopPlan() => planCancel?.Cancel();
    }
}
