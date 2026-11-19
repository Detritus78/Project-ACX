using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

using log4net;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: exploration contracts, redone (JJ 2026-11-12). Each character has 3 contract slots; talking to any Town
    /// Crier fills every free one (no command needed; with none free, the crier insults you). A finished or dropped
    /// contract's slot rests 20 hours before it takes a new one, each slot on its own timer (JJ 2026-11-14, replacing
    /// "3 a day", which allowed 3 before the midnight-UTC reset and 3 more after); a contract that runs out frees its slot. Each sends you to a
    /// dungeon matched to your level (its usual monsters from 10 below to 5 above you), favouring dungeons you haven't
    /// explored, to reach its exploration marker or kill some of one monster that lives there. Each comes with a Contract
    /// Portal Gem to its entrance, kept until the contract ends, so far dungeons (and dying in them) cost no travel;
    /// finishing pays on the spot (experience, pyreals and a full bag of salvage, scaled to the dungeon).
    /// A contract and its gem run out 24 hours after they're handed out.
    /// /contract lists yours; /contract drop gives one up; /sc shares your top one with your fellowship (see Share).
    /// The dungeons come from Content/exploration/markers.json.
    /// </summary>
    public static class AcxContracts
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public class Monster
        {
            public uint Wcid { get; set; }
            public string Name { get; set; }
            public int Count { get; set; }
        }

        public class Entrance
        {
            public uint Cell { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
        }

        public class Dungeon
        {
            public string Landblock { get; set; }
            public string Name { get; set; }
            public int Level { get; set; }
            public int Distance { get; set; }
            public Entrance Entrance { get; set; }
            public List<Monster> Monsters { get; set; } = new();
        }

        public class Contract
        {
            public string Id { get; set; }
            public string Landblock { get; set; }
            public string Name { get; set; }
            public int Level { get; set; }
            public int Distance { get; set; }
            public uint TargetWcid { get; set; }
            public string TargetName { get; set; }
            public int Needed { get; set; }
            public int Progress { get; set; }
            public double Received { get; set; }
            public bool Started { get; set; }   // used its gem or walked into its dungeon

            public bool IsKill => TargetWcid != 0;
            // a shared contract may replace one with no kills yet, even if its gem was used or its dungeon entered (JJ
            // 2026-11-18, after a buddy couldn't take a share: both had only started contracts with 0 kills left)
            public bool Untouched => Progress == 0;
            public string Task => IsKill ? $"kill {Needed} {TargetName} in {Name}" : $"reach the exploration marker deep in {Name}";
        }

        private class State
        {
            public List<Contract> Active { get; set; } = new();
            public List<double> Cooldowns { get; set; } = new();   // when each resting slot takes a contract again
        }

        private const int Slots = 3;
        private const double SlotCooldown = 72000;   // 20 hours, like ACX's daily quests (JJ 2026-11-14)
        private const double Lifetime = 86400;   // a contract and its gem last 24 hours from receipt (JJ), like Legendary Keys
        private const uint GemWcid = 900550;

        /// <summary>
        /// Salvage bags a contract can pay out (the retail "Salvaged ..." materials)
        /// </summary>
        public static uint RandomSalvageWcid() => SalvageWcids[ThreadSafeRandom.Next(0, SalvageWcids.Length - 1)];

        private static readonly uint[] SalvageWcids =
        {
            21034, 20980, 21035, 21036, 21037, 20981, 21038, 21039, 21040, 21041, 21042, 20982, 21043, 20983, 21044, 21045, 21046, 21047,
            21048, 21049, 20984, 20985, 21050, 21051, 21052, 21053, 21054, 20986, 21055, 21056, 21057, 21058, 21059, 20987, 20988, 21060,
            21061, 21062, 20989, 21064, 21065, 21066, 20990, 21067, 21068, 21069, 21070, 20991, 21071, 21072, 21073, 21074, 21075, 21076,
            21077, 21078, 20993, 21079, 21080, 21081, 21082, 21083, 20994, 21084, 21085, 21086, 20995, 21087, 21088, 21089,
        };

        private static List<Dungeon> dungeons;

        /// <summary>
        /// Every contract dungeon, in markers.json's order (AcxEntranceTest walks them)
        /// </summary>
        public static IReadOnlyList<Dungeon> AllDungeons => Dungeons;

        private static List<Dungeon> Dungeons
        {
            get
            {
                if (dungeons != null)
                    return dungeons;
                var path = Path.Combine(PropertyManager.GetString("content_folder").Item, "exploration", "markers.json");
                try
                {
                    dungeons = JsonSerializer.Deserialize<List<Dungeon>>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        .Where(d => d.Entrance != null).ToList();
                }
                catch (Exception e)
                {
                    log.Error($"AcxContracts: couldn't read {path}: {e.Message}");
                    dungeons = new();
                }
                return dungeons;
            }
        }

        private static State Load(Player player)
        {
            State state = null;
            var json = player.GetProperty(PropertyString.AcxContracts);
            if (json != null)
            {
                try { state = JsonSerializer.Deserialize<State>(json); }
                catch (Exception e) { log.Warn($"AcxContracts: {player.Name}'s contracts couldn't be read: {e.Message}"); }
            }
            state ??= new State();

            // contracts end 24 hours after they were handed out, along with their gems
            var now = Time.GetUnixTime();
            foreach (var c in state.Active.Where(c => c.Received == 0))
                c.Received = now;
            var expired = state.Active.Where(c => now - c.Received >= Lifetime).ToList();
            foreach (var c in expired)
            {
                state.Active.Remove(c);   // its slot is free at once: it already sat 24 hours
                RemoveGem(player, c);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your contract has run out: {c.Task}.", ChatMessageType.Broadcast));
            }

            // a kill contract for a monster its dungeon no longer lists (2026-11-18: event-only spawns such as Carrion
            // Shrethlets, which never appear while their event is off) asks for one the dungeon does spawn, or becomes an
            // exploration contract when it lists none
            var retargeted = false;
            foreach (var c in state.Active.Where(c => c.IsKill))
            {
                var dungeon = Dungeons.FirstOrDefault(d => d.Landblock == c.Landblock);
                if (dungeon == null || dungeon.Monsters.Any(m => m.Wcid == c.TargetWcid))
                    continue;
                var was = c.Task;
                var m = dungeon.Monsters.FirstOrDefault();
                c.TargetWcid = m?.Wcid ?? 0;
                c.TargetName = m?.Name;
                c.Needed = m != null ? Math.Clamp(m.Count, 5, 10) : 0;
                c.Progress = 0;
                retargeted = true;
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your contract changed: {was} is now {c.Task}.", ChatMessageType.Broadcast));
            }

            if (state.Cooldowns.RemoveAll(t => t <= now) > 0 || expired.Count > 0 || retargeted)
                Save(player, state);
            return state;
        }

        private static int FreeSlots(State state) => Math.Max(0, Slots - state.Active.Count - state.Cooldowns.Count);

        private static void RestSlot(State state) => state.Cooldowns.Add(Time.GetUnixTime() + SlotCooldown);

        private static string Until(double unixTime)
        {
            var left = TimeSpan.FromSeconds(Math.Max(60, unixTime - Time.GetUnixTime()));
            return $"{(int)left.TotalHours}h {left.Minutes}m";
        }

        private static void Save(Player player, State state) => player.SetProperty(PropertyString.AcxContracts, JsonSerializer.Serialize(state));

        private static bool HasExplored(Player player, Dungeon d) => player.QuestManager.HasQuest($"AcxDungeonMarker_{d.Landblock}");

        /// <summary>
        /// New contracts: dungeons matched to the player's level, unexplored ones three times as likely
        /// </summary>
        private static List<Contract> MakeContracts(Player player, List<Contract> active, int count)
        {
            var level = player.Level ?? 1;
            var busy = active.Select(c => c.Landblock).ToHashSet();
            var pool = Dungeons.Where(d => !busy.Contains(d.Landblock)).ToList();

            // Project ACX: down to 20 levels under the player, so contracts keep sending people to dungeons they haven't
            // explored (JJ 2026-11-16: "lets open that up to some to places that are 10 or 20 levels below.. So they can
            // keep exploring"; was 10 under)
            var matched = pool.Where(d => d.Level >= level - 20 && d.Level <= level + 5).ToList();
            if (matched.Count < count)
                matched = pool.Where(d => d.Level >= level - 30 && d.Level <= level + 10).ToList();
            if (matched.Count < count)
                matched = pool.OrderBy(d => Math.Abs(d.Level - level)).Take(10).ToList();

            var offers = new List<Contract>();
            while (offers.Count < count && matched.Count > 0)
            {
                var weights = matched.Select(d => HasExplored(player, d) ? 1 : 3).ToList();
                var roll = ThreadSafeRandom.Next(0, weights.Sum() - 1);
                var i = 0;
                while (roll >= weights[i]) { roll -= weights[i]; i++; }
                var d = matched[i];
                matched.RemoveAt(i);

                var contract = new Contract { Id = Guid.NewGuid().ToString("N")[..8], Landblock = d.Landblock, Name = d.Name, Level = d.Level, Distance = d.Distance };
                if (d.Monsters.Count > 0 && ThreadSafeRandom.Next(0, 1) == 1)
                {
                    var m = d.Monsters[ThreadSafeRandom.Next(0, d.Monsters.Count - 1)];
                    contract.TargetWcid = m.Wcid;
                    contract.TargetName = m.Name;
                    contract.Needed = Math.Clamp(m.Count, 5, 10);
                }
                offers.Add(contract);
            }
            return offers;
        }

        public static string List(Player player)
        {
            var state = Load(player);
            var lines = new List<string>();
            if (state.Active.Count == 0)
                lines.Add("You don't hold any contracts.");
            else
            {
                lines.Add("Your contracts:");
                for (var i = 0; i < state.Active.Count; i++)
                    lines.Add($"  {i + 1}. {Describe(state.Active[i])}{(state.Active[i].IsKill ? $": {state.Active[i].Progress}/{state.Active[i].Needed}" : "")}");
            }
            foreach (var ready in state.Cooldowns.OrderBy(t => t))
                lines.Add($"  Resting slot: takes a new contract in {Until(ready)}.");
            var free = FreeSlots(state);
            if (free > 0)
                lines.Add($"{free} free slot{(free == 1 ? "" : "s")}: talk to a Town Crier.");
            return string.Join("\n", lines);
        }

        /// <summary>
        /// What a Town Crier says when he has nothing left for you (JJ: "he randomly insults you")
        /// </summary>
        private static readonly string[] Insults =
        {
            "You again? Even the drudges have more ambition. Come back tomorrow.",
            "I've no more work for you today. Go and bother a mosswart.",
            "I've met olthoi with better manners. No more work today.",
            "If I had a pyreal for every time you asked, I'd have retired to Arwic by now.",
            "Asheron himself couldn't find more work for the likes of you today.",
            "Go and stand by your lifestone a while. I hear you know the way.",
            "The only contract left today is on your head, and I'm not saying who took it.",
            "Shouldn't you be off dying in a dungeon somewhere?",
            "I'm a Town Crier, not a fountain. Tomorrow.",
            "Even a gromnie knows when to stop asking.",
            "You smell like a banderling's armpit. Come back tomorrow, preferably washed.",
            "Move along, you're blocking my view of the portal.",
            "Out of contracts, and out of patience. Shoo.",
        };

        /// <summary>
        /// Called from Creature.ActOnUse when a player talks to a Town Crier: fills every free contract slot
        /// </summary>
        public static void OnCrierUse(Creature crier, Player player)
        {
            var state = Load(player);

            if (state.Active.Count >= Slots)
            {
                Tell(crier, player, $"You've still got {Slots} contracts. Finish one before you come begging for more.");
                return;
            }

            var free = FreeSlots(state);
            if (free == 0)
            {
                Tell(crier, player, Insults[ThreadSafeRandom.Next(0, Insults.Length - 1)]);
                player.SendMessage(NoFreeSlot(state));
                return;
            }

            var given = new List<Contract>();
            foreach (var contract in MakeContracts(player, state.Active, free))
            {
                var error = GiveGem(player, contract);
                if (error != null)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{error} Then talk to the Town Crier again.", ChatMessageType.Broadcast));
                    break;
                }
                contract.Received = Time.GetUnixTime();
                state.Active.Add(contract);
                given.Add(contract);
            }
            Save(player, state);

            if (given.Count == 0)
                return;

            Tell(crier, player, given.Count == 1 ? "Here's a job for you. Mind you don't die." : $"Here's {given.Count} jobs for you. Mind you don't die.");
            var lines = new List<string> { "Contracts taken, for the next 24 hours (each Contract Portal Gem takes you to its dungeon's entrance):" };
            lines.AddRange(given.Select(c => $"  {Describe(c)}"));
            player.Session.Network.EnqueueSend(new GameMessageSystemChat(string.Join("\n", lines), ChatMessageType.Broadcast));
        }

        /// <summary>
        /// Puts a contract's portal gem in the player's pack; returns why it couldn't, or null
        /// </summary>
        private static string GiveGem(Player player, Contract contract)
        {
            var gem = WorldObjectFactory.CreateNewWorldObject(GemWcid);
            if (gem == null)
            {
                log.Error("AcxContracts: the contract gems are missing (no weenie 900550)");
                return "The contract gems are missing; tell an admin.";
            }
            gem.SetProperty(PropertyString.AcxContractId, contract.Id);
            gem.LongDesc = $"Takes you to the entrance of {contract.Name}, for your contract there. Use it as often as you like; it crumbles when the contract ends, at the latest 24 hours after you got it.";
            if (!player.TryCreateInInventoryWithNetworking(gem))
            {
                gem.Destroy();
                return "Make room in your pack for the contract gem.";
            }
            return null;
        }

        /// <summary>
        /// /sc: offers your top contract (first on /contract's list) to everyone in your fellowship (JJ 2026-11-14: "so a
        /// buddy and I can plan every night to do our daily together"; one at a time: finish it, then /sc shares the next).
        /// Each fellow gets a yes/no popup saying what the contract is and what it costs them: a free contract slot, or else
        /// it replaces their newest contract with no kills yet (since 2026-11-18 even one whose gem was used). They get their own
        /// gem and their own count from 0 (kills already count for every fellow in the dungeon who holds the same contract),
        /// and each holder is paid once when they finish; finishing together starts both slots' 20-hour rest at the same
        /// moment. Rewards are never split through the fellowship, so sharing can't pay anyone for more than their three
        /// slots allow. Messages are one short chat line each, no banner (JJ): "Contract offered.", then "Contract accepted."
        /// or "Contract rejected." per fellow; a fellow with no free slot gets "No free slots available..." instead of the popup.
        /// </summary>
        public static void Share(Player player)
        {
            var state = Load(player);
            if (state.Active.Count == 0)
            {
                player.SendMessage("You have no contract to share.");
                return;
            }
            if (player.Fellowship == null)
            {
                player.SendMessage("You're not in a fellowship.");
                return;
            }

            var contract = state.Active[0];
            var sharer = player.Guid.Full;
            var offered = false;
            foreach (var fellow in player.Fellowship.GetFellowshipMembers().Values.Where(p => p != player))
            {
                switch (CantTake(fellow, contract, player.Level ?? 1))
                {
                    case Refusal.None:
                        var replaced = Replacement(Load(fellow));
                        var cost = replaced == null ? "Taking it uses one of your free contract slots." : $"Taking it replaces your contract to {replaced.Task}.";
                        var prompt = $"{player.Name} shares a contract:\n\n{Describe(contract)}\n\n{cost}\n\nAccept shared contract?";
                        if (fellow.ConfirmationManager.EnqueueSend(new Confirmation_AcxChoice(fellow.Guid, () => Accept(fellow, contract, sharer), () => Answer(sharer, false)), prompt))
                            offered = true;
                        break;

                    case Refusal.NoSlot:
                        fellow.SendMessage(NoFreeSlot(Load(fellow)));
                        Answer(sharer, false);
                        offered = true;
                        break;

                    // a fellow outside the fellowship level rules, or who already holds it, never sees it
                }
            }

            player.SendMessage(offered ? "Contract offered." : "Nobody in your fellowship can take that contract.");
        }

        private static void Answer(uint sharer, bool accepted) => PlayerManager.GetOnlinePlayer(sharer)?.SendMessage(accepted ? "Contract accepted." : "Contract rejected.");

        /// <summary>
        /// The line for a player with no free contract slot, with when the next one frees if one is resting
        /// </summary>
        private static string NoFreeSlot(State state) =>
            state.Cooldowns.Count > 0 ? $"No free slots available. Next available slot in {Until(state.Cooldowns.Min())}." : "No free slots available.";

        private static bool SameTask(Contract a, Contract b) => a.Landblock == b.Landblock && a.TargetWcid == b.TargetWcid;

        /// <summary>
        /// The contract a shared one would replace: none while they have a free slot, otherwise their newest contract they
        /// haven't started
        /// </summary>
        private static Contract Replacement(State state)
        {
            if (FreeSlots(state) > 0)
                return null;
            return state.Active.Where(c => c.Untouched).OrderByDescending(c => c.Received).FirstOrDefault();
        }

        private enum Refusal { None, Level, AlreadyHas, NoSlot }

        /// <summary>
        /// Whether this player can take a contract shared by someone of sharerLevel: only if the two would share XP under
        /// the fellowship level rules (Fellowship.AcxShareRule; JJ 2026-11-14), so a level 1 never sees a level 200's
        /// contract and a level 200 can't farm a level 1's
        /// </summary>
        private static Refusal CantTake(Player player, Contract shared, int sharerLevel)
        {
            if (!Fellowship.AcxShareRule(new[] { sharerLevel, player.Level ?? 1 }).share)
                return Refusal.Level;

            var state = Load(player);
            if (state.Active.Any(c => SameTask(c, shared)))
                return Refusal.AlreadyHas;
            if (FreeSlots(state) == 0 && !state.Active.Any(c => c.Untouched))
                return Refusal.NoSlot;
            return Refusal.None;
        }

        private static void Accept(Player player, Contract shared, uint sharer)
        {
            var sharerLevel = PlayerManager.GetOnlinePlayer(sharer)?.Level ?? player.Level ?? 1;
            var refusal = CantTake(player, shared, sharerLevel);
            if (refusal != Refusal.None)
            {
                if (refusal == Refusal.NoSlot)
                    player.SendMessage(NoFreeSlot(Load(player)));
                Answer(sharer, false);
                return;
            }

            var state = Load(player);
            var copy = new Contract
            {
                Id = Guid.NewGuid().ToString("N")[..8], Landblock = shared.Landblock, Name = shared.Name, Level = shared.Level, Distance = shared.Distance,
                TargetWcid = shared.TargetWcid, TargetName = shared.TargetName, Needed = shared.Needed, Received = Time.GetUnixTime()
            };

            var error = GiveGem(player, copy);
            if (error != null)
            {
                player.SendMessage(error);
                Answer(sharer, false);
                return;
            }

            var replaced = Replacement(state);
            if (replaced != null)
            {
                state.Active.Remove(replaced);
                RemoveGem(player, replaced);
            }
            state.Active.Add(copy);
            Save(player, state);

            Answer(sharer, true);
        }

        private static void Tell(WorldObject crier, Player player, string text)
        {
            player.Session.Network.EnqueueSend(new GameEventTell(crier, text, player, ChatMessageType.Tell));
        }

        private static string Describe(Contract c) => $"{char.ToUpper(c.Task[0])}{c.Task[1..]} (level {c.Level}{(c.IsKill ? "" : $", {c.Distance} m in")})";

        public static string Drop(Player player, int number)
        {
            var state = Load(player);
            if (number < 1 || number > state.Active.Count)
                return state.Active.Count > 0 ? $"Your contracts are 1 to {state.Active.Count} (see /contract)." : "You don't hold any contracts.";

            var contract = state.Active[number - 1];
            state.Active.RemoveAt(number - 1);
            RestSlot(state);   // a dropped contract's slot rests like a finished one, so dropping can't reroll contracts
            Save(player, state);
            RemoveGem(player, contract);
            return $"Contract dropped: {contract.Task}. That slot takes a new contract in 20 hours.";
        }

        private static void RemoveGem(Player player, Contract contract)
        {
            foreach (var gem in player.GetAllPossessions().Where(i => i.GetProperty(PropertyString.AcxContractId) == contract.Id).ToList())
                player.TryConsumeFromInventoryWithNetworking(gem);
        }

        /// <summary>
        /// Called from Gem.UseGem: a contract gem takes the player to its dungeon's entrance. It's kept until the contract
        /// ends, so a player who dies there can come back (JJ 2026-11-12).
        /// </summary>
        public static void UseGem(Gem gem, Player player)
        {
            var id = gem.GetProperty(PropertyString.AcxContractId);
            var contract = Load(player).Active.FirstOrDefault(c => c.Id == id);
            var dungeon = contract != null ? Dungeons.FirstOrDefault(d => d.Landblock == contract.Landblock) : null;

            if (dungeon == null)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("That contract is over; the gem crumbles.", ChatMessageType.Broadcast));
                player.TryConsumeFromInventoryWithNetworking(gem);
                return;
            }

            if (player.PKTimerActive)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("You've been in a player fight too recently to use a portal gem.", ChatMessageType.Broadcast));
                return;
            }

            MarkStarted(player, c => c.Id == id);

            var e = dungeon.Entrance;
            player.Teleport(new Position(e.Cell, new Vector3(e.X, e.Y, e.Z), Quaternion.Identity));
        }

        /// <summary>
        /// Called from Landblock when a player enters it: walking into a contract's dungeon starts that contract, so a
        /// shared contract never replaces one the player is in the middle of (marker contracts have no count to show it)
        /// </summary>
        public static void OnEnterLandblock(Player player, Landblock landblock)
        {
            if (!landblock.IsDungeon || player.GetProperty(PropertyString.AcxContracts) == null)
                return;

            var lb = landblock.Id.Landblock.ToString("X4");
            MarkStarted(player, c => c.Landblock == lb);
        }

        private static void MarkStarted(Player player, Func<Contract, bool> which)
        {
            var state = Load(player);
            var changed = false;
            foreach (var contract in state.Active.Where(c => !c.Started && which(c)))
            {
                contract.Started = true;
                changed = true;
            }
            if (changed)
                Save(player, state);
        }

        /// <summary>
        /// Called from AcxExploration.OnMarkerUse: completes any marker contract for this dungeon
        /// </summary>
        public static void OnMarkerReached(Player player, ushort landblock)
        {
            var lb = landblock.ToString("X4");
            var state = Load(player);
            foreach (var contract in state.Active.Where(c => !c.IsKill && c.Landblock == lb).ToList())
                Complete(player, state, contract);
        }

        /// <summary>
        /// Called from Creature.OnDeath: counts the kill for the killer and their fellows in the same dungeon
        /// </summary>
        public static void OnCreatureDeath(Creature creature, DamageHistoryInfo lastDamager)
        {
            if (creature is Player || lastDamager?.TryGetAttacker() is not Player killer)
                return;

            var lb = (creature.Location?.LandblockId.Raw >> 16)?.ToString("X4");
            var players = killer.Fellowship != null
                ? killer.Fellowship.GetFellowshipMembers().Values.Where(p => p.CurrentLandblock == killer.CurrentLandblock).ToList()
                : new List<Player> { killer };

            foreach (var player in players)
            {
                var state = player.GetProperty(PropertyString.AcxContracts) != null ? Load(player) : null;
                var contract = state?.Active.FirstOrDefault(c => c.IsKill && c.Landblock == lb && c.TargetWcid == creature.WeenieClassId);
                if (contract == null)
                    continue;

                contract.Progress++;
                if (contract.Progress >= contract.Needed)
                    Complete(player, state, contract);
                else
                {
                    Save(player, state);
                    if (PropertyManager.GetBool("acx_contract_progress_message").Item)   // spamless by default: /contract shows progress
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Contract: {contract.Progress} of {contract.Needed} {contract.TargetName}.", ChatMessageType.Broadcast));
                }
            }
        }

        private static void Complete(Player player, State state, Contract contract)
        {
            state.Active.Remove(contract);
            RestSlot(state);
            Save(player, state);
            RemoveGem(player, contract);

            var level = Math.Max(1, contract.Level);
            var depth = contract.IsKill ? 1.0 : 1.0 + contract.Distance / 1000.0;   // deeper markers pay more
            var pyreals = (int)(level * 150 * depth);

            player.GrantLevelProportionalXp(0.15 * depth, 0, (long)(level * level * 300 * depth));

            var coins = WorldObjectFactory.CreateNewWorldObject((uint)Factories.Enum.WeenieClassName.coinstack);
            coins.SetStackSize(Math.Min(pyreals, (int)(coins.MaxStackSize ?? 25000)));
            if (!player.TryCreateInInventoryWithNetworking(coins))
                coins.Destroy();

            var salvage = WorldObjectFactory.CreateNewWorldObject(SalvageWcids[ThreadSafeRandom.Next(0, SalvageWcids.Length - 1)]);
            if (salvage != null)
            {
                salvage.Structure = 100;   // a full bag (JJ 2026-11-12)
                salvage.MaxStructure = 100;
                salvage.ItemWorkmanship = Math.Clamp(1 + level / 25, 1, 10);
                salvage.SetProperty(PropertyInt.NumItemsInMaterial, 1);
                if (!player.TryCreateInInventoryWithNetworking(salvage))
                    salvage.Destroy();
            }

            // ACX Marks per contract: acx_contract_marks (JJ 2026-11-16: "Maybe for contracts we can bump them to two marks per run"; was one)
            var marks = (int)Math.Max(0, PropertyManager.GetLong("acx_contract_marks").Item);
            if (marks > 0)
                AcxMarks.Give(player, "contract", null, marks);
            var markText = marks == 1 ? "an ACX Mark" : $"{marks} ACX Marks";
            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Contract complete: {contract.Task}! You're paid {pyreals:N0} pyreals, a full bag of salvage, {markText} and some experience.", ChatMessageType.Broadcast));
        }
    }
}
