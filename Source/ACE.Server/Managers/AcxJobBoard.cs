using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using log4net;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: town job boards. Each town's board (a Book) lists the retail quests nearby that a character
    /// hasn't finished; /job take by the board takes one, and reading the board again pays for finished ones (the four starter
    /// towns' Job Runners pay too).
    /// A job counts as finished when the character carries the quest flag that quest already stamps on completion.
    /// The jobs live in Content/jobs/jobs.json, so they can be changed without a rebuild (/acxjobreload).
    /// </summary>
    public static class AcxJobBoard
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public class Job
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public int Level { get; set; }
            public string Start { get; set; }
            public string Where { get; set; }
            public string Notes { get; set; }
            public string Flag { get; set; }
        }

        public class Town
        {
            public string Name { get; set; }
            public List<Job> Jobs { get; set; } = new();
        }

        private class JobFile
        {
            public List<Town> Towns { get; set; } = new();
        }

        private static Dictionary<string, Town> towns;
        private static readonly object loadLock = new();

        /// <summary>
        /// How close a player must stand to a board or runner to take a job
        /// </summary>
        private const float TakeRange = 15.0f;

        private static string JobFilePath => Path.Combine(PropertyManager.GetString("content_folder").Item, "jobs", "jobs.json");

        private static Dictionary<string, Town> Towns
        {
            get
            {
                if (towns == null)
                    Reload(out _);
                return towns;
            }
        }

        public static bool Reload(out string message)
        {
            lock (loadLock)
            {
                try
                {
                    var file = JsonSerializer.Deserialize<JobFile>(File.ReadAllText(JobFilePath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                    towns = file.Towns.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
                    message = $"Job boards loaded: {towns.Count} towns, {towns.Values.Sum(t => t.Jobs.Count)} jobs from {JobFilePath}";
                    log.Info(message);
                    return true;
                }
                catch (Exception e)
                {
                    towns ??= new Dictionary<string, Town>(StringComparer.OrdinalIgnoreCase);
                    message = $"Job boards could not be loaded from {JobFilePath}: {e.Message}";
                    log.Error(message);
                    return false;
                }
            }
        }

        public static bool IsJobObject(WorldObject wo) => wo.GetProperty(PropertyString.AcxJobTown) != null;

        private static Town GetTown(WorldObject wo)
        {
            var name = wo.GetProperty(PropertyString.AcxJobTown);
            return name != null && Towns.TryGetValue(name, out var town) ? town : null;
        }

        private static string TakenFlag(Job job) => $"AcxJobTaken_{job.Id}";
        private static string DoneFlag(Job job) => $"AcxJobDone_{job.Id}";

        private enum JobState { Open, Taken, Finished, Done, AlreadyDone }

        private static JobState GetState(Player player, Job job)
        {
            var quests = player.QuestManager;
            if (quests.HasQuest(DoneFlag(job)))
                return JobState.Done;
            var taken = quests.HasQuest(TakenFlag(job));
            var finished = !string.IsNullOrEmpty(job.Flag) && quests.HasQuest(job.Flag);
            if (taken)
                return finished ? JobState.Finished : JobState.Taken;
            return finished ? JobState.AlreadyDone : JobState.Open;
        }

        /// <summary>
        /// Called from Book.ActOnUse: shows this player the jobs they haven't finished, one page each
        /// </summary>
        public static void ShowBoard(Book board, Player player)
        {
            var town = GetTown(board);
            var pages = new List<string>();

            // Project ACX: the board pays for finished jobs when it's read (JJ 2026-11-16, choosing "pay at the board" over
            // a Job Runner in every town); the four starter towns' runners still pay too
            if (town != null)
                PayFinished(town, player);

            if (town == null)
            {
                pages.Add("This board is empty.");
            }
            else
            {
                var listed = town.Jobs.Select((job, i) => (job, number: i + 1, state: GetState(player, job)))
                    .Where(j => j.state != JobState.Done && j.state != JobState.AlreadyDone).ToList();
                var done = town.Jobs.Count(j => GetState(player, j) == JobState.Done);

                var cover = new StringBuilder();
                cover.Append($"{town.Name} Job Board\n\n");
                cover.Append("Work wanted around town. Each page is one job.\n\n");
                cover.Append($"To take a job, stand by this board and type /job take <number>.\n");
                cover.Append("When you've finished it, come back and read this board to collect your pay.\n\n");
                cover.Append($"Open to you here: {listed.Count}. Finished and paid: {done} of {town.Jobs.Count}.\n");
                cover.Append("/job lists the jobs you've taken.\n\nDungeon contracts, matched to your level: talk to a Town Crier (3 a day). /contract lists yours.");
                pages.Add(cover.ToString());

                foreach (var (job, number, state) in listed)
                {
                    var page = new StringBuilder();
                    page.Append($"Job {number}: {job.Name}\n");
                    if (job.Level > 0)
                        page.Append($"For level {job.Level} or so\n");
                    page.Append($"\nStart: {job.Start}, {job.Where}.\n");
                    if (!string.IsNullOrEmpty(job.Notes))
                        page.Append($"\n{job.Notes}\n");
                    page.Append(state switch
                    {
                        JobState.Taken => "\nYou've taken this job.",
                        JobState.Finished => "\nFinished! Read this board again for your pay.",
                        _ => $"\nTake it with /job take {number}",
                    });
                    pages.Add(page.ToString());
                }
            }

            var pageData = pages.Select(text => new PropertiesBookPageData
            {
                AuthorId = 0xFFFFFFFF,
                AuthorName = "",
                AuthorAccount = "",
                IgnoreAuthor = true,
                PageText = text,
            }).ToList();

            player.Session.Network.EnqueueSend(new GameEventBookDataResponse(player.Session, board.Guid.Full, 1000, pageData.Count, pageData, "", 0xFFFFFFFF, "", true));
        }

        /// <summary>
        /// Pays for every finished job this player took in the town; false if their pack is too full to take the pay
        /// </summary>
        private static bool PayFinished(Town town, Player player)
        {
            foreach (var job in town.Jobs.Where(j => GetState(player, j) == JobState.Finished))
            {
                if (!PayJob(player, job))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat("You can't carry your job pay right now. Make some room in your pack and read the board again.", ChatMessageType.Broadcast));
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Called from Creature.ActOnUse: pays for every finished job taken in this runner's town
        /// </summary>
        public static void OnRunnerUse(Creature runner, Player player)
        {
            var town = GetTown(runner);
            if (town == null)
                return;

            var paid = 0;
            foreach (var job in town.Jobs.Where(j => GetState(player, j) == JobState.Finished))
            {
                if (!PayJob(player, job))
                {
                    Tell(runner, player, "You can't carry your pay right now. Make some room in your pack and come back.");
                    return;
                }
                paid++;
            }

            // Project ACX: the starter towns' runners explain job boards to a newcomer once, in their own words (JJ 2026-11-16:
            // "The runners will explain the process for the first time and how it works. in human sarcastic speak", "in the
            // noob towns"); boards pay for themselves now, so after that they just needle
            if (!player.QuestManager.HasQuest(RunnerIntroFlag))
            {
                player.QuestManager.Stamp(RunnerIntroFlag);
                foreach (var line in RunnerIntro)
                    Tell(runner, player, line);
                return;
            }

            if (paid > 0)
            {
                Tell(runner, player, "Look at you, finishing things. Your mother would be proud. Probably.");
                return;
            }

            var taken = town.Jobs.Where(j => GetState(player, j) == JobState.Taken).Select(j => j.Name).ToList();
            if (taken.Count > 0)
                Tell(runner, player, $"Still working on {string.Join(", ", taken)}? It won't finish itself. I've checked.");
            else
                Tell(runner, player, RunnerQuips[ACE.Common.ThreadSafeRandom.Next(0, RunnerQuips.Length - 1)]);
        }

        private const string RunnerIntroFlag = "AcxJobRunnerIntro";

        private static readonly string[] RunnerIntro =
        {
            "Oh good, another hero. Here's how this works, since nobody reads anything.",
            "See that job board? Each page is a job. Stand next to it and type /job take and the job's number.",
            "Then go do the job. Wander off, poke things, try not to die. Come back and read the board again and it pays you: pyreals, some experience and an ACX Mark. Try to contain your excitement.",
            "Every town has a board now, so stop asking me where the work is. And each job pays once. Once. I don't care how nicely you ask.",
        };

        private static readonly string[] RunnerQuips =
        {
            "The board's right there. It has words on it. You can read words, right?",
            "Read the board, /job take a number. I'm not doing it for you.",
            "Jobs are on the board. Pay's on the board. I'm just here for the scenery.",
        };

        private static bool PayJob(Player player, Job job)
        {
            var level = Math.Max(1, job.Level);
            var pyreals = level * 100;

            if (pyreals > 0)
            {
                var coins = WorldObjectFactory.CreateNewWorldObject((uint)Factories.Enum.WeenieClassName.coinstack);
                coins.SetStackSize(Math.Min(pyreals, (int)(coins.MaxStackSize ?? 25000)));
                if (!player.TryCreateInInventoryWithNetworking(coins))
                {
                    coins.Destroy();
                    return false;
                }
            }

            player.GrantLevelProportionalXp(0.1, 0, (long)level * level * 200);

            player.QuestManager.Erase(TakenFlag(job));
            player.QuestManager.Stamp(DoneFlag(job));
            player.SetProperty(PropertyInt.AcxJobsDone, (player.GetProperty(PropertyInt.AcxJobsDone) ?? 0) + 1);
            AcxLeaderboards.CheckLeader(AcxLeaderboards.Board.Jobs);

            AcxMarks.Give(player, "job");   // one ACX Mark per job (JJ 2026-11-13)
            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Job done: {job.Name}. You're paid {pyreals:N0} pyreals, an ACX Mark and some experience.", ChatMessageType.Broadcast));
            return true;
        }

        private static void Tell(WorldObject runner, Player player, string text)
        {
            player.Session.Network.EnqueueSend(new GameEventTell(runner, text, player, ChatMessageType.Tell));
        }

        /// <summary>
        /// /job take &lt;number&gt;: takes that job from the board or runner the player is standing by
        /// </summary>
        public static string TakeJob(Player player, int number)
        {
            var near = player.CurrentLandblock?.GetWorldObjectsForPhysicsHandling()
                .Where(wo => IsJobObject(wo) && player.GetCylinderDistance(wo) <= TakeRange)
                .OrderBy(wo => player.GetCylinderDistance(wo)).FirstOrDefault();
            if (near == null)
                return "Stand by a town's job board to take a job.";

            var town = GetTown(near);
            if (town == null)
                return "This town has no jobs posted.";

            if (number < 1 || number > town.Jobs.Count)
                return $"The {town.Name} board has jobs 1 to {town.Jobs.Count}.";

            var job = town.Jobs[number - 1];
            switch (GetState(player, job))
            {
                case JobState.Done:
                case JobState.AlreadyDone:
                    return $"You've already done {job.Name}.";
                case JobState.Taken:
                case JobState.Finished:
                    return $"You've already taken {job.Name}.";
            }

            player.QuestManager.Stamp(TakenFlag(job));
            return $"Job taken: {job.Name}. Start with {job.Start}, {job.Where}.";
        }

        /// <summary>
        /// /job drop &lt;number&gt;: gives a taken job back to the board the player is standing by
        /// </summary>
        public static string DropJob(Player player, int number)
        {
            var near = player.CurrentLandblock?.GetWorldObjectsForPhysicsHandling()
                .Where(wo => IsJobObject(wo) && player.GetCylinderDistance(wo) <= TakeRange).FirstOrDefault();
            var nearTown = near != null ? GetTown(near) : null;
            if (nearTown == null)
                return "Stand by the job board of the town the job is from.";
            if (number < 1 || number > nearTown.Jobs.Count)
                return $"The {nearTown.Name} board has jobs 1 to {nearTown.Jobs.Count}.";

            var job = nearTown.Jobs[number - 1];
            if (GetState(player, job) != JobState.Taken)
                return $"You haven't got {job.Name} on the go.";

            player.QuestManager.Erase(TakenFlag(job));
            return $"Job dropped: {job.Name}.";
        }

        /// <summary>
        /// /job: the jobs this player has taken, in every town
        /// </summary>
        public static string ListJobs(Player player)
        {
            var lines = new List<string>();
            foreach (var town in Towns.Values)
            {
                for (var i = 0; i < town.Jobs.Count; i++)
                {
                    var job = town.Jobs[i];
                    var state = GetState(player, job);
                    if (state == JobState.Taken)
                        lines.Add($"{town.Name} job {i + 1}: {job.Name} (start with {job.Start}, {job.Where})");
                    else if (state == JobState.Finished)
                        lines.Add($"{town.Name} job {i + 1}: {job.Name} (finished: read the {town.Name} job board for your pay)");
                }
            }

            return lines.Count > 0 ? "Your jobs:\n" + string.Join("\n", lines) : "You haven't taken any jobs. Read a town's job board to find some.";
        }

        /// <summary>
        /// Jobs finished and paid, for the leaderboard
        /// </summary>
        public static int CountDone(Player player) => Towns.Values.Sum(t => t.Jobs.Count(j => player.QuestManager.HasQuest(DoneFlag(j))));
    }
}
