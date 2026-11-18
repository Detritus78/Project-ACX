using System;
using System.Collections.Concurrent;
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
    /// </summary>
    public static class AcxEntranceTest
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public const uint Wcid = 900853;
        public const string Quest = "AcxEntranceTest";

        private static readonly string LogFile = Path.Combine("Logs", "acx-entrance-test.csv");
        private static readonly object logLock = new();

        // the entrance each player was just sent to: number (1-based) and dungeon; and the last one tried, for reports
        private static readonly ConcurrentDictionary<uint, (int Number, AcxContracts.Dungeon Dungeon)> pending = new();
        private static readonly ConcurrentDictionary<uint, (int Number, AcxContracts.Dungeon Dungeon)> last = new();

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
            pending[player.Guid.Full] = (number, dungeon);
            last[player.Guid.Full] = (number, dungeon);
            Chat(player, $"Entrance {number} of {dungeons.Count}: {dungeon.Name} (level {dungeon.Level}, landblock {dungeon.Landblock}).");

            var e = dungeon.Entrance;
            player.Teleport(new Position(e.Cell, new Vector3(e.X, e.Y, e.Z), Quaternion.Identity));
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

        private static void Write(Player player, (int Number, AcxContracts.Dungeon Dungeon) test, string result)
        {
            var e = test.Dungeon.Entrance;
            var line = string.Join(",",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                player.Name.Replace(",", ""),
                test.Number.ToString(CultureInfo.InvariantCulture),
                test.Dungeon.Landblock,
                "\"" + test.Dungeon.Name.Replace("\"", "'") + "\"",
                $"0x{e.Cell:X8} [{e.X.ToString("0.###", CultureInfo.InvariantCulture)} {e.Y.ToString("0.###", CultureInfo.InvariantCulture)} {e.Z.ToString("0.###", CultureInfo.InvariantCulture)}]",
                result);
            try
            {
                lock (logLock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogFile));
                    var isNew = !File.Exists(LogFile);
                    using var writer = new StreamWriter(LogFile, append: true);
                    if (isNew)
                        writer.WriteLine("time,character,number,landblock,dungeon,entrance,result");
                    writer.WriteLine(line);
                }
            }
            catch (Exception ex)
            {
                log.Warn($"[ACX ENTRANCE TEST] couldn't write {LogFile}: {ex.Message}");
            }
        }
    }
}
