using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using log4net;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: leaderboards (JJ 2026-11-12). Wealth (names and rank only), job board completions, deaths, the deadliest
    /// monsters, and areas explored. Read on a town's records board or with /top; taking the number 1 spot is announced to
    /// everyone online, at most once per board per hour. Admins aren't ranked.
    /// </summary>
    public static class AcxLeaderboards
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public enum Board { Wealth, Jobs, Deaths, Monsters, Explored }

        private static readonly Dictionary<Board, string> BoardNames = new()
        {
            { Board.Wealth, "Wealthiest" },
            { Board.Jobs, "Most jobs done" },
            { Board.Deaths, "Most deaths" },
            { Board.Monsters, "Deadliest monsters" },
            { Board.Explored, "Most explored" },
        };

        private const int TopCount = 10;

        private static readonly ConcurrentDictionary<Board, string> leaders = new();
        private static readonly ConcurrentDictionary<Board, DateTime> lastAnnounced = new();
        private static readonly TimeSpan AnnounceCooldown = TimeSpan.FromHours(1);

        // ---- the boards ----

        /// <summary>
        /// Admins stay off the boards and out of wealth ranks, unless acx_leaderboards_include_admins (Preview only, for testing)
        /// </summary>
        public static bool Counts(IPlayer player) => (player.Account?.AccessLevel ?? 0) == (uint)AccessLevel.Player || PropertyManager.GetBool("acx_leaderboards_include_admins").Item;

        private static List<IPlayer> RankedPlayers() => PlayerManager.GetAllPlayers().Where(Counts).ToList();

        /// <summary>
        /// The top of a board as (name, value shown) rows, best first
        /// </summary>
        public static List<(string Name, string Value)> GetTop(Board board)
        {
            switch (board)
            {
                case Board.Wealth:
                    return RankedPlayers().Where(p => AcxWealth.GetRank(p.Guid.Full) > 0)
                        .OrderByDescending(AcxWealth.GetBalance).Take(TopCount)
                        .Select(p => (p.Name, $"rank {AcxWealth.GetRank(p.Guid.Full)}")).ToList();

                case Board.Jobs:
                    return TopBy(p => p.GetProperty(PropertyInt.AcxJobsDone) ?? 0, v => $"{v:N0} jobs");

                case Board.Deaths:
                    return TopBy(p => p.GetProperty(PropertyInt.NumDeaths) ?? 0, v => $"{v:N0} deaths");

                case Board.Explored:
                    return TopBy(p => (p.GetProperty(PropertyInt.AcxAreasExplored) ?? 0) + (p.GetProperty(PropertyInt.AcxDungeonsExplored) ?? 0),
                        v => $"{v:N0} areas");

                case Board.Monsters:
                    lock (monsterKills)
                        return monsterKills.OrderByDescending(kv => kv.Value).Take(TopCount).Select(kv => (kv.Key, $"{kv.Value:N0} kills")).ToList();
            }
            return new();
        }

        private static List<(string, string)> TopBy(Func<IPlayer, int> value, Func<int, string> format)
        {
            return RankedPlayers().Select(p => (p, v: value(p))).Where(x => x.v > 0)
                .OrderByDescending(x => x.v).Take(TopCount).Select(x => (x.p.Name, format(x.v))).ToList();
        }

        public static string FormatBoard(Board board)
        {
            var top = GetTop(board);
            var text = new StringBuilder($"{BoardNames[board]}\n\n");
            if (top.Count == 0)
                text.Append("No one yet.");
            for (var i = 0; i < top.Count; i++)
                text.Append($"{i + 1}. {top[i].Name}: {top[i].Value}\n");
            return text.ToString();
        }

        /// <summary>
        /// The first few of every board, for /top on its own
        /// </summary>
        public static string FormatAllBoards(int count)
        {
            var text = new StringBuilder();
            foreach (var board in Enum.GetValues<Board>())
            {
                var top = GetTop(board).Take(count).ToList();
                text.Append($"{BoardNames[board]}: ");
                text.Append(top.Count == 0 ? "no one yet" : string.Join(", ", top.Select((t, i) => $"{i + 1}. {t.Name} ({t.Value})")));
                text.Append('\n');
            }
            return text.ToString();
        }

        public static bool TryParseBoard(string name, out Board board)
        {
            switch (name)
            {
                case "wealth": case "rich": board = Board.Wealth; return true;
                case "jobs": board = Board.Jobs; return true;
                case "deaths": board = Board.Deaths; return true;
                case "monsters": case "npc": board = Board.Monsters; return true;
                case "explored": case "explore": board = Board.Explored; return true;
            }
            board = default;
            return false;
        }

        // ---- announcements ----

        /// <summary>
        /// Called after anything that can change a board: announces a new number 1, at most once per board per hour
        /// </summary>
        public static void CheckLeader(Board board)
        {
            var top = GetTop(board);
            if (top.Count > 0)
                OnNewLeader(board, top[0].Name);
        }

        public static void OnNewLeader(Board board, string name)
        {
            var previous = leaders.TryGetValue(board, out var p) ? p : null;
            leaders[board] = name;

            if (previous == null || previous == name)
                return;   // the first look after a restart only records the leader

            if (lastAnnounced.TryGetValue(board, out var last) && DateTime.UtcNow - last < AnnounceCooldown)
                return;
            lastAnnounced[board] = DateTime.UtcNow;

            var message = board switch
            {
                Board.Wealth => $"{name} is now the wealthiest in Dereth!",
                Board.Jobs => $"{name} has now done more town jobs than anyone in Dereth!",
                Board.Deaths => $"{name} has now died more than anyone in Dereth. Be careful out there!",
                Board.Monsters => $"{name} are now the deadliest monsters in Dereth!",
                Board.Explored => $"{name} has now explored more of Dereth than anyone!",
                _ => null,
            };
            if (message != null)
                PlayerManager.BroadcastToAll(new GameMessageSystemChat(message, ChatMessageType.Broadcast));
        }

        /// <summary>
        /// Records each board's current leader without announcing, so a restart doesn't announce anyone
        /// </summary>
        public static void Start()
        {
            LoadMonsterKills();
            foreach (var board in Enum.GetValues<Board>())
            {
                var top = GetTop(board);
                if (top.Count > 0)
                    leaders[board] = top[0].Name;
            }
            Task.Run(CountWalkableLand);
        }

        // ---- records boards ----

        /// <summary>
        /// Called from Book.ActOnUse: a town's records board, one board per page
        /// </summary>
        public static void ShowRecords(Book book, Player player)
        {
            var pages = new List<string> { "Records of Dereth\n\nThe best (and worst) of Dereth, one list per page.\n\nAlso with /top wealth, jobs, deaths, monsters or explored. /explored shows your own exploring." };
            pages.AddRange(Enum.GetValues<Board>().Select(FormatBoard));

            var pageData = pages.Select(text => new PropertiesBookPageData { AuthorId = 0xFFFFFFFF, AuthorName = "", AuthorAccount = "", IgnoreAuthor = true, PageText = text }).ToList();
            player.Session.Network.EnqueueSend(new GameEventBookDataResponse(player.Session, book.Guid.Full, 1000, pageData.Count, pageData, "", 0xFFFFFFFF, "", true));
        }

        // ---- deadliest monsters ----

        private static readonly Dictionary<string, int> monsterKills = new();

        private static string MonsterKillsPath => Path.Combine(Path.GetDirectoryName(typeof(AcxLeaderboards).Assembly.Location), "Config", "acx-monster-kills.json");

        private static void LoadMonsterKills()
        {
            try
            {
                if (!File.Exists(MonsterKillsPath))
                    return;
                var kills = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(MonsterKillsPath));
                lock (monsterKills)
                {
                    monsterKills.Clear();
                    foreach (var kv in kills)
                        monsterKills[kv.Key] = kv.Value;
                }
            }
            catch (Exception e)
            {
                log.Error($"AcxLeaderboards: couldn't read {MonsterKillsPath}: {e.Message}");
            }
        }

        /// <summary>
        /// Called from Player.OnDeath: counts the kill for the monster that landed the killing blow
        /// </summary>
        public static void OnPlayerDeath(Player player, DamageHistoryInfo lastDamager)
        {
            if (!Counts(player))
                return;

            CheckLeader(Board.Deaths);

            var killer = lastDamager?.TryGetAttacker();
            if (killer == null || killer is Player || killer is not Creature)
                return;

            string json;
            lock (monsterKills)
            {
                monsterKills[killer.Name] = (monsterKills.TryGetValue(killer.Name, out var n) ? n : 0) + 1;
                json = JsonSerializer.Serialize(monsterKills);
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MonsterKillsPath));
                File.WriteAllText(MonsterKillsPath, json);
            }
            catch (Exception e)
            {
                log.Error($"AcxLeaderboards: couldn't write {MonsterKillsPath}: {e.Message}");
            }

            CheckLeader(Board.Monsters);
        }

        // ---- areas explored ----

        private static readonly ConcurrentDictionary<uint, HashSet<ushort>> explored = new();

        private static HashSet<ushort> GetExplored(Player player)
        {
            return explored.GetOrAdd(player.Guid.Full, _ =>
            {
                var set = new HashSet<ushort>();
                var saved = player.GetProperty(PropertyString.AcxExploredLandblocks) ?? "";
                for (var i = 0; i + 4 <= saved.Length; i += 4)
                    if (ushort.TryParse(saved.AsSpan(i, 4), System.Globalization.NumberStyles.HexNumber, null, out var lb))
                        set.Add(lb);
                return set;
            });
        }

        /// <summary>
        /// Called from Landblock when a player enters it: counts each outdoor map square once.
        /// Dungeons count when their exploration marker is reached (AcxExploration).
        /// </summary>
        public static void OnEnterLandblock(Player player, Landblock landblock)
        {
            if (landblock.IsDungeon)
                return;

            var set = GetExplored(player);
            var id = landblock.Id.Landblock;

            lock (set)
            {
                if (!set.Add(id))
                    return;

                player.SetProperty(PropertyInt.AcxAreasExplored, (player.GetProperty(PropertyInt.AcxAreasExplored) ?? 0) + 1);
                player.SetProperty(PropertyString.AcxExploredLandblocks, string.Concat(set.OrderBy(x => x).Select(x => x.ToString("X4"))));
            }

            CheckLeader(Board.Explored);
        }

        private static int walkableLand;

        /// <summary>
        /// Outdoor map squares with any land that isn't water, counted once from the cell dat
        /// </summary>
        private static void CountWalkableLand()
        {
            try
            {
                var count = 0;
                for (uint x = 0; x < 0xFF; x++)
                {
                    for (uint y = 0; y < 0xFF; y++)
                    {
                        var block = DatManager.CellDat.ReadFromDat<CellLandblock>((x << 24) | (y << 16) | 0xFFFF);
                        if (block?.Terrain == null || block.Height.All(h => h == 0))
                            continue;
                        if (block.Terrain.Any(t => ((t & CellLandblock.TerrainMask_Type) >> CellLandblock.TerrainShift_Type) < 0x10))
                            count++;
                    }
                }
                walkableLand = count;
                log.Info($"AcxLeaderboards: {count:N0} outdoor map squares have walkable land");
            }
            catch (Exception e)
            {
                log.Error($"AcxLeaderboards: couldn't count walkable land: {e.Message}");
            }
        }

        public static string ExploredSummary(Player player)
        {
            var areas = player.GetProperty(PropertyInt.AcxAreasExplored) ?? 0;
            var dungeons = player.GetProperty(PropertyInt.AcxDungeonsExplored) ?? 0;
            var share = walkableLand > 0 ? $" ({100.0 * areas / walkableLand:0.0}% of Dereth's land)" : "";
            return $"You've explored {areas:N0} areas outdoors{share} and reached the end of {dungeons:N0} dungeons.";
        }
    }
}
