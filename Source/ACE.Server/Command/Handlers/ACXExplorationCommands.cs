using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: dungeon exploration markers (Managers/AcxExploration.cs)
    /// </summary>
    public static class ACXExplorationCommands
    {
        [CommandHandler("acxmarkerplan", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Works out each dungeon's exploration marker spot (farthest walkable point from its entrance) from Pathfinding/marker-input.json into Pathfinding/marker-plan.json.",
            "[samples per dungeon, default 300] | stop")]
        public static void HandleMarkerPlan(Session session, params string[] parameters)
        {
            if (parameters.Length > 0 && parameters[0] == "stop")
            {
                AcxExploration.StopPlan();
                CommandHandlerHelper.WriteOutputInfo(session, "Stopping the marker plan.");
                return;
            }

            var samples = parameters.Length > 0 && int.TryParse(parameters[0], out var n) ? n : 300;
            CommandHandlerHelper.WriteOutputInfo(session, AcxExploration.StartPlan(samples));
        }

        [CommandHandler("acxroute", AccessLevel.Admin, CommandHandlerFlag.None, 2,
            "Plans a walking route through a dungeon's hallways (not the rooms behind doors) that keeps clear of every hotspot, into Pathfinding/route-XXXX.json (Pathfinding/AcxRoutePlanner.cs).",
            "<start cell hex> [clearance m, default 8] [doors: also go into the rooms behind doors] [cover=N: how near the route must pass each part, default 12] [from=<cell>,x,y,z to=<cell>,x,y,z: just the shortest way between two spots]")]
        public static void HandleRoute(Session session, params string[] parameters)
        {
            var cell = System.Convert.ToUInt32(parameters[0], 16);
            var clearance = parameters.Length > 1 && float.TryParse(parameters[1], out var c) ? c : 8f;
            var skipDoors = System.Array.IndexOf(parameters, "doors") < 0;   // hallways only unless asked
            var coverArg = System.Array.Find(parameters, a => a.StartsWith("cover="));
            var cover = coverArg != null && float.TryParse(coverArg.Substring(6), out var cv) ? cv : 12f;
            // from=<cell hex>,x,y,z to=<cell hex>,x,y,z: just the shortest open way between two spots
            ACE.Entity.Position Spot(string prefix)
            {
                var arg = System.Array.Find(parameters, a => a.StartsWith(prefix));
                if (arg == null) return null;
                var f = arg.Substring(prefix.Length).Split(',');
                return new ACE.Entity.Position(System.Convert.ToUInt32(f[0], 16), float.Parse(f[1]), float.Parse(f[2]), float.Parse(f[3]), 0, 0, 0, 1);
            }
            CommandHandlerHelper.WriteOutputInfo(session, ACE.Server.Pathfinding.AcxRoutePlanner.Start((ushort)(cell >> 16), cell, clearance, skipDoors, cover, Spot("from="), Spot("to=")));
        }
    }
}
