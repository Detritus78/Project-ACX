using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Numerics;

using log4net;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: the Entrance Tester (JJ 2026-11-18: "can we do a chest or multiple chests with all the gems. I can just
    /// pick them up and use them one by one until I find a broken one", then "yeah build both"), a Preview-only gem
    /// (acx/Experiments/entrance-tester.sql, wcid 900853). Each use sends its player to the next contract dungeon's entrance
    /// (AcxContracts.AllDungeons, markers.json order), and Logs/acx-entrance-test.csv gets one line per entrance: arrived, or
    /// stuck when the player left while still in portal space (AC:Unreal's "world entry failed", Player_AcxPortalSafety, which
    /// also saves the player back where they stood). Progress is the quest AcxEntranceTest, so it carries over between
    /// sessions; /acxentrance shows it and /acxentrance <n> makes entrance n the next one.
    /// The Entrance Fixer (gem 900854; JJ 2026-11-18: "Just move the point a meter or two in a direction. if it fails we rotate
    /// the meter direction", "just give me a gem with the broken 17", "ones that get fixed we take out"): each use tries the next entrance logged STUCK, moved 1.5 m north, then east, south, west and
    /// the diagonals, then the same at 3 m, until one arrives; Logs/acx-entrance-fix.csv records every try and its spot.
    /// </summary>
    public static class AcxEntranceTest
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public const uint Wcid = 900853;
        public const uint FixWcid = 900854;
        public const uint CheckWcid = 900855;
        public const uint TourWcid = 900856;
        public const uint OlthoiTourWcid = 900857;

        // the Olthoi Tour (gem 900857; JJ 2026-11-21: "lets a gem that ports me around to olthoi dungeons", to try monster
        // chasing with pathfinding on and off): each use goes to the next olthoi dungeon's entrance, lowest level first, from
        // Logs/acx-olthoi-tour.json; quest AcxOlthoiTour keeps the place, wrapping around
        private static readonly string OlthoiTourList = Path.Combine("Logs", "acx-olthoi-tour.json");
        private const string OlthoiTourQuest = "AcxOlthoiTour";

        // the Town Tour (gem 900856; JJ 2026-11-18: "create me a gem that goes to all the towns so I can see them"): each use
        // goes to the next town's Concordia portal (beside its town network ruin), from Logs/acx-town-tour.json
        // (acx/scripts/concordia/town_portals_to_concordia.py); quest AcxTownTour keeps the place, wrapping around
        private static readonly string TourList = Path.Combine("Logs", "acx-town-tour.json");
        private const string TourQuest = "AcxTownTour";

        // the Portal Checker (gem 900855; JJ 2026-11-18: "lets check those other ones with a gem too"): every other portal
        // destination 30-portal-drop-cells.sql corrected, from Logs/acx-portal-check.json, each tried once
        private static readonly string CheckList = Path.Combine("Logs", "acx-portal-check.json");
        private static readonly string CheckFile = Path.Combine("Logs", "acx-portal-check.csv");
        private record CheckSpot(string name, uint cell, float x, float y, float z);
        public const string Quest = "AcxEntranceTest";

        private static readonly string LogFile = Path.Combine("Logs", "acx-entrance-test.csv");
        private static readonly object logLock = new();

        private static readonly string FixFile = Path.Combine("Logs", "acx-entrance-fix.csv");

        // fix mode's moves, in the order they're tried: a direction and a distance in meters (north = +y, east = +x)
        private static readonly (string Name, float X, float Y)[] Directions =
        {
            ("north", 0, 1), ("east", 1, 0), ("south", 0, -1), ("west", -1, 0),
            ("northeast", 0.7071f, 0.7071f), ("southeast", 0.7071f, -0.7071f), ("southwest", -0.7071f, -0.7071f), ("northwest", -0.7071f, 0.7071f),
        };
        private static readonly float[] Distances = { 1.5f, 3f };

        private record Test(int Number, AcxContracts.Dungeon Dungeon, string Move = null, Vector3? Spot = null, CheckSpot Check = null);

        // the entrance each player was just sent to; and the last one tried, for reports
        private static readonly ConcurrentDictionary<uint, Test> pending = new();
        private static readonly ConcurrentDictionary<uint, Test> last = new();


        public static void Use(Player player)
        {
            var dungeons = AcxContracts.AllDungeons;
            var done = player.QuestManager.GetCurrentSolves(Quest);
            if (done >= dungeons.Count)
            {
                Chat(player, $"All {dungeons.Count} entrances tested; the results are in {LogFile}. /acxentrance 1 starts over.");
                return;
            }

            var number = done + 1;
            var dungeon = dungeons[done];
            player.QuestManager.Stamp(Quest);
            pending[player.Guid.Full] = new Test(number, dungeon);
            last[player.Guid.Full] = new Test(number, dungeon);
            Chat(player, $"Entrance {number} of {dungeons.Count}: {dungeon.Name} (level {dungeon.Level}, landblock {dungeon.Landblock}).");

            var e = dungeon.Entrance;
            player.Teleport(new Position(e.Cell, new Vector3(e.X, e.Y, e.Z), Quaternion.Identity));
        }

        /// <summary>
        /// Fix mode: the next stuck entrance and the next move not yet tried for it
        /// </summary>
        public static void UseFix(Player player)
        {
            var dungeons = AcxContracts.AllDungeons;
            var stuck = ReadColumn(LogFile, 2, r => r[^1].StartsWith("STUCK")).Select(int.Parse).Distinct().OrderBy(n => n).ToList();
            var tries = ReadRows(FixFile);
            var fixedNumbers = tries.Where(r => r[^1] == "arrived").Select(r => r[2]).ToHashSet();

            foreach (var number in stuck)
            {
                if (fixedNumbers.Contains(number.ToString(CultureInfo.InvariantCulture)) || number < 1 || number > dungeons.Count)
                    continue;
                var tried = tries.Where(r => r[2] == number.ToString(CultureInfo.InvariantCulture)).Select(r => r[6]).ToHashSet();
                // first the exact spot (30-portal-drop-cells.sql gave it the right cell), then the small moves
                var moves = new[] { (Name: "exact spot", X: 0f, Y: 0f) }.Concat(Distances.SelectMany(d => Directions.Select(dir => (Name: $"{d.ToString("0.#", CultureInfo.InvariantCulture)} m {dir.Name}", X: dir.X * d, Y: dir.Y * d)))).ToList();
                var index = moves.FindIndex(m => !tried.Contains(m.Name));
                if (index < 0)
                    continue;   // every move failed: left for a closer look

                var dungeon = dungeons[number - 1];
                var move = moves[index];
                var e = dungeon.Entrance;
                var spot = new Vector3(e.X + move.X, e.Y + move.Y, e.Z);
                var test = new Test(number, dungeon, move.Name, spot);
                pending[player.Guid.Full] = test;
                last[player.Guid.Full] = test;
                Chat(player, $"Fix try: entrance {number}, {dungeon.Name}, {(index == 0 ? "exact spot, corrected cell" : "moved " + move.Name)} (try {index + 1} of {moves.Count}).");
                player.Teleport(new Position(e.Cell, spot, Quaternion.Identity));
                return;
            }
            Chat(player, $"Entrance Fixer: nothing left to try ({fixedNumbers.Count} of {stuck.Count} stuck entrances fixed).");
        }

        public static void UseCheck(Player player)
        {
            List<CheckSpot> spots;
            try { spots = System.Text.Json.JsonSerializer.Deserialize<List<CheckSpot>>(File.ReadAllText(CheckList)); }
            catch (Exception ex) { Chat(player, $"Portal Checker: couldn't read {CheckList} ({ex.Message})."); return; }
            var tried = ReadRows(CheckFile).Select(r => r[2]).ToHashSet();
            var index = Enumerable.Range(0, spots.Count).FirstOrDefault(i => !tried.Contains((i + 1).ToString(CultureInfo.InvariantCulture)), -1);
            if (index < 0)
            {
                Chat(player, $"Portal Checker: all {spots.Count} portal drops tried; results in {CheckFile}.");
                return;
            }
            var spot = spots[index];
            var test = new Test(index + 1, null, Check: spot);
            pending[player.Guid.Full] = test;
            Chat(player, $"Portal check {index + 1} of {spots.Count}: {spot.name}.");
            player.Teleport(new Position(spot.cell, new Vector3(spot.x, spot.y, spot.z), Quaternion.Identity));
        }

        public static void UseTour(Player player) => UseTour(player, TourList, TourQuest, "Town");

        public static void UseOlthoiTour(Player player) => UseTour(player, OlthoiTourList, OlthoiTourQuest, "Olthoi dungeon");

        private static void UseTour(Player player, string list, string quest, string what)
        {
            List<CheckSpot> stops;
            try { stops = System.Text.Json.JsonSerializer.Deserialize<List<CheckSpot>>(File.ReadAllText(list)); }
            catch (Exception ex) { Chat(player, $"{what} tour: couldn't read {list} ({ex.Message})."); return; }
            if (stops.Count == 0) return;
            var index = player.QuestManager.GetCurrentSolves(quest) % stops.Count;
            player.QuestManager.Stamp(quest);   // SetQuestCompletions caps an unregistered quest at 0 (GetMaxSolves): it never moved on
            var stop = stops[index];
            Chat(player, $"{what} {index + 1} of {stops.Count}: {stop.name}.");
            player.Teleport(new Position(stop.cell, new Vector3(stop.x, stop.y, stop.z), Quaternion.Identity));
        }

        public static void OnArrived(Player player)
        {
            if (pending.TryRemove(player.Guid.Full, out var test))
                Write(player, test, "arrived");
        }

        public static void OnStuck(Player player)
        {
            if (pending.TryRemove(player.Guid.Full, out var test))
                Write(player, test, "STUCK in portal space");
        }

        /// <summary>
        /// /acxentrance [n]
        /// </summary>
        public static string Command(Player player, string argument)
        {
            var total = AcxContracts.AllDungeons.Count;
            if (string.IsNullOrWhiteSpace(argument))
                return $"Entrance Tester: {player.QuestManager.GetCurrentSolves(Quest)} of {total} entrances tried; results in {LogFile}. /acxentrance bad <what's wrong> reports the last one.";
            // "bad <note>": the player arrived, but something's wrong there (in a wall, under the floor, the wrong place)
            if (argument.Trim().StartsWith("bad", StringComparison.OrdinalIgnoreCase))
            {
                if (!last.TryGetValue(player.Guid.Full, out var tried))
                    return "Use the Entrance Tester first: the report goes on the last entrance it sent you to.";
                var note = argument.Trim().Substring(3).Trim().Replace(",", ";").Replace("\"", "'");
                Write(player, tried, "REPORTED: " + (note.Length > 0 ? note : "no note"));
                return $"Reported entrance {tried.Number}: {tried.Dungeon.Name}.";
            }
            if (!int.TryParse(argument.Trim(), out var next) || next < 1 || next > total)
                return $"Use /acxentrance 1-{total} to make that entrance the next one.";
            player.QuestManager.SetQuestCompletions(Quest, next - 1);
            return $"The next use of the Entrance Tester goes to entrance {next} of {total}: {AcxContracts.AllDungeons[next - 1].Name}.";
        }

        private static void Chat(Player player, string text)
        {
            player.Session.Network.EnqueueSend(new GameMessageSystemChat(text, ChatMessageType.Broadcast));
        }

        private static string Coords(Vector3 v) => $"{v.X.ToString("0.###", CultureInfo.InvariantCulture)} {v.Y.ToString("0.###", CultureInfo.InvariantCulture)} {v.Z.ToString("0.###", CultureInfo.InvariantCulture)}";

        private static List<string[]> ReadRows(string file)
        {
            try
            {
                lock (logLock)
                    return File.Exists(file) ? File.ReadAllLines(file).Skip(1).Select(l => l.Split(',')).Where(r => r.Length >= 7).ToList() : new();
            }
            catch (Exception ex)
            {
                log.Warn($"[ACX ENTRANCE TEST] couldn't read {file}: {ex.Message}");
                return new();
            }
        }

        private static IEnumerable<string> ReadColumn(string file, int column, Func<string[], bool> where) => ReadRows(file).Where(where).Select(r => r[column]);

        private static void Write(Player player, Test test, string result)
        {
            if (test.Check != null)
            {
                var c = test.Check;
                WriteLine(CheckFile, "time,character,number,portal,cell,spot,result", string.Join(",",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    player.Name.Replace(",", ""),
                    test.Number.ToString(CultureInfo.InvariantCulture),
                    "\"" + c.name.Replace("\"", "'").Replace(",", "") + "\"",
                    $"0x{c.cell:X8}",
                    Coords(new Vector3(c.x, c.y, c.z)),
                    result));
                return;
            }
            var e = test.Dungeon.Entrance;
            if (test.Move != null)
            {
                // fix mode: time,character,number,landblock,dungeon,cell,move,spot,result
                WriteLine(FixFile, "time,character,number,landblock,dungeon,cell,move,spot,result", string.Join(",",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    player.Name.Replace(",", ""),
                    test.Number.ToString(CultureInfo.InvariantCulture),
                    test.Dungeon.Landblock,
                    "\"" + test.Dungeon.Name.Replace("\"", "'").Replace(",", "") + "\"",
                    $"0x{e.Cell:X8}",
                    test.Move,
                    Coords(test.Spot.Value),
                    result));
                return;
            }
            var line = string.Join(",",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                player.Name.Replace(",", ""),
                test.Number.ToString(CultureInfo.InvariantCulture),
                test.Dungeon.Landblock,
                "\"" + test.Dungeon.Name.Replace("\"", "'") + "\"",
                $"0x{e.Cell:X8} [{e.X.ToString("0.###", CultureInfo.InvariantCulture)} {e.Y.ToString("0.###", CultureInfo.InvariantCulture)} {e.Z.ToString("0.###", CultureInfo.InvariantCulture)}]",
                result);
            WriteLine(LogFile, "time,character,number,landblock,dungeon,entrance,result", line);
        }

        private static void WriteLine(string file, string header, string line)
        {
            try
            {
                lock (logLock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    var isNew = !File.Exists(file);
                    using var writer = new StreamWriter(file, append: true);
                    if (isNew)
                        writer.WriteLine(header);
                    writer.WriteLine(line);
                }
            }
            catch (Exception ex)
            {
                log.Warn($"[ACX ENTRANCE TEST] couldn't write {file}: {ex.Message}");
            }
        }
    }
}
