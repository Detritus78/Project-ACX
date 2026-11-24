using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using log4net;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: the bank and wealth rank (JJ 2026-11-12). Each character banks pyreals anywhere with /bank.
    /// Rank is for clout only: it comes from the banked balance, relative to every other character with something banked
    /// (the richest is rank 10, the poorest rank 1, the rest spread evenly between). It shows in /bank and on the wealth
    /// leaderboard; allegiance rank stays retail's (JJ 2026-11-13: patron/vassal ranks untouched). Admins aren't ranked.
    /// </summary>
    public static class AcxWealth
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static Dictionary<uint, int> ranks = new();
        private static Timer timer;

        private const uint CoinWcid = 273;

        /// <summary>
        /// The trade notes /bank withdraw hands out for large amounts, largest first
        /// </summary>
        private static readonly (uint Wcid, int Value)[] TradeNotes = { (20630, 250000), (2627, 100000), (2626, 50000) };

        /// <summary>
        /// Above this, a withdrawal comes as trade notes rather than coins
        /// </summary>
        private const long CoinWithdrawalLimit = 500000;

        public static void Start()
        {
            if (timer != null)
                return;

            timer = new Timer(_ => WorldManager.EnqueueAction(new ActionEventDelegate(UpdateRanks)), null, TimeSpan.Zero, TimeSpan.FromHours(1));
        }

        public static long GetBalance(IPlayer player) => player.GetProperty(PropertyInt64.AcxBankedPyreals) ?? 0;

        public static int GetRank(uint playerGuid) => ranks.TryGetValue(playerGuid, out var rank) ? rank : 0;

        /// <summary>
        /// Ranks every character with something banked and pushes the new rank to anyone whose rank changed.
        /// Runs on the world thread (hourly, and after every deposit or withdrawal).
        /// </summary>
        public static void UpdateRanks()
        {
            try
            {
                var banked = PlayerManager.GetAllPlayers()
                    .Where(p => GetBalance(p) > 0 && AcxLeaderboards.Counts(p))
                    .OrderByDescending(GetBalance).ToList();

                var newRanks = new Dictionary<uint, int>();
                for (var i = 0; i < banked.Count; i++)
                    newRanks[banked[i].Guid.Full] = banked.Count == 1 ? 10 : 10 - (int)Math.Floor(9.0 * i / (banked.Count - 1));

                var oldTop = ranks.FirstOrDefault(r => r.Value == 10).Key;
                ranks = newRanks;

                if (banked.Count > 0 && banked[0].Guid.Full != oldTop)
                    AcxLeaderboards.OnNewLeader(AcxLeaderboards.Board.Wealth, banked[0].Name);
            }
            catch (Exception e)
            {
                log.Error($"AcxWealth.UpdateRanks: {e}");
            }
        }

        // ---- the bank (JJ 2026-11-13: short commands, every currency, notes in and out) ----

        /// <summary>
        /// Trade notes by their Roman numeral (in hundreds of pyreals: MMD = 250,000), for /bank withdraw note
        /// </summary>
        private static readonly (string Roman, long Value, uint Wcid)[] NoteSizes =
        {
            ("I", 100, 2621), ("V", 500, 2622), ("X", 1000, 2623), ("L", 5000, 2624), ("C", 10000, 2625), ("CL", 15000, 7374),
            ("CC", 20000, 7375), ("CCL", 25000, 7376), ("D", 50000, 2626), ("DCCL", 75000, 7377), ("M", 100000, 2627),
            ("MD", 150000, 20628), ("MM", 200000, 20629), ("MMD", 250000, 20630),
        };

        private static HashSet<uint> currencies;

        /// <summary>
        /// Project ACX: vendor-payment items that aren't money, so the bank doesn't take them (JJ 2026-11-24: "remove olthoi
        /// gland ... pitted slag ... small olthoi venom sac ... writ of apology.. Those should not be bankable"): Mutated
        /// Olthoi Gland, Pitted Slag, Small Olthoi Venom Sac, Writ of Apology. Any already banked can still be withdrawn.
        /// </summary>
        private static readonly HashSet<uint> NotBankable = new HashSet<uint> { 43747, 43491, 36376, 37559 };

        /// <summary>
        /// The currencies besides pyreals the bank holds: the ACX Mark and every stackable item a vendor takes as payment
        /// (stipends, Mhoire coins, A'nekshay tokens...). Trade notes are pyreals; one-off turn-ins (a Dark Tusker Paw) aren't money.
        /// </summary>
        public static HashSet<uint> Currencies
        {
            get
            {
                if (currencies != null)
                    return currencies;

                var set = new HashSet<uint>();
                try
                {
                    using var context = new ACE.Database.Models.World.WorldDbContext();
                    var wcids = context.WeeniePropertiesDID.Where(d => d.Type == (ushort)PropertyDataId.AlternateCurrency).Select(d => d.Value).Distinct().ToList();
                    foreach (var wcid in wcids)
                    {
                        var weenie = ACE.Database.DatabaseManager.World.GetCachedWeenie(wcid);
                        if (weenie != null && weenie.GetItemType() != ItemType.PromissoryNote && (weenie.GetProperty(PropertyInt.MaxStackSize) ?? 1) >= 10
                            && !NotBankable.Contains(wcid))
                            set.Add(wcid);
                    }
                }
                catch (Exception e)
                {
                    log.Error($"AcxWealth.Currencies: {e}");
                }
                if (AcxMarks.Wcid != 0)
                    set.Add(AcxMarks.Wcid);
                return currencies = set;
            }
        }

        /// <summary>
        /// How many of a currency (e.g. ACX Marks) the player has in the bank (the economy log)
        /// </summary>
        public static long GetBankedCurrency(IPlayer player, uint wcid) => GetCurrencies(player).TryGetValue(wcid, out var count) ? count : 0;

        private static Dictionary<uint, long> GetCurrencies(IPlayer player)
        {
            var banked = new Dictionary<uint, long>();
            foreach (var entry in (player.GetProperty(PropertyString.AcxBankedCurrencies) ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split(':');
                if (parts.Length == 2 && uint.TryParse(parts[0], out var wcid) && long.TryParse(parts[1], out var count) && count > 0)
                    banked[wcid] = count;
            }
            return banked;
        }

        private static void SetCurrencies(IPlayer player, Dictionary<uint, long> banked)
        {
            var text = string.Join(";", banked.Where(b => b.Value > 0).Select(b => $"{b.Key}:{b.Value}"));
            if (text.Length == 0)
                player.RemoveProperty(PropertyString.AcxBankedCurrencies);
            else
                player.SetProperty(PropertyString.AcxBankedCurrencies, text);
        }

        private static string CurrencyName(uint wcid, long count)
        {
            var weenie = ACE.Database.DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie == null)
                return $"#{wcid}";
            return count == 1 ? weenie.GetName() : weenie.GetPluralName();
        }

        /// <summary>
        /// The currency a word names ("marks", "stipend", "mhoire"), or null; several matches come back in ambiguous
        /// </summary>
        private static uint? FindCurrency(string word, IEnumerable<uint> among, out List<uint> ambiguous)
        {
            ambiguous = null;
            word = word.ToLowerInvariant().TrimEnd('s');
            var matches = among.Where(wcid =>
            {
                var weenie = ACE.Database.DatabaseManager.World.GetCachedWeenie(wcid);
                if (weenie == null)
                    return false;
                var words = $"{weenie.GetName()} {weenie.GetPluralName()}".ToLowerInvariant().Split(' ', '\'', '-');
                return words.Any(w => w.StartsWith(word)) || weenie.GetName().ToLowerInvariant().StartsWith(word);
            }).Distinct().ToList();

            if (matches.Count == 1)
                return matches[0];
            if (matches.Count > 1)
                ambiguous = matches;
            return null;
        }

        /// <summary>
        /// Project ACX: whether a banked currency may be sent to another character (JJ 2026-11-24: "Gear markers and stipends
        /// are attuned and bonded. They cannot be transferred ... ACX is the exception"). Attuned currencies (stipends, gear
        /// markers, Mhoire coins, trade tokens, Colosseum and Gauntlet coins, Writs of Apology...) stay with the character
        /// that earned them; they can still be banked and withdrawn. The ACX Mark is the one tradeable currency.
        /// </summary>
        public static bool IsTransferable(uint wcid)
        {
            if (wcid == AcxMarks.Wcid)
                return true;
            var weenie = ACE.Database.DatabaseManager.World.GetCachedWeenie(wcid);
            return weenie != null && (weenie.GetProperty(PropertyInt.Attuned) ?? 0) == 0;
        }

        private static bool IsWord(string word, params string[] forms) => forms.Contains(word);

        /// <summary>
        /// /bank deposit (/b d): a | all, n | notes, a pyreal amount, or a currency name with an optional count (default all)
        /// </summary>
        public static string Deposit(Player player, string[] args)
        {
            if (args.Length == 0)
                return DepositUsage;

            var what = args[0].ToLowerInvariant();
            var everything = IsWord(what, "a", "all", "everything");
            var parts = new List<string>();
            long pyreals = 0;

            // trade notes: their value goes in as pyreals
            if (everything || IsWord(what, "n", "note", "notes"))
            {
                foreach (var note in player.GetAllPossessions().Where(i => i.ItemType == ItemType.PromissoryNote).ToList())
                {
                    var value = (long)(note.Value ?? 0) * (note.StackSize ?? 1);
                    if (value > 0 && player.TryConsumeFromInventoryWithNetworking(note))
                        pyreals += value;
                }
            }

            // pyreals: all with everything, or "p [amount]", or a bare amount
            long coinsWanted = 0;
            if (everything)
                coinsWanted = player.CoinValue ?? 0;
            else if (IsWord(what, "p", "py", "pyreal", "pyreals"))
                coinsWanted = args.Length > 1 && TryAmount(args[1], out var amount) ? amount : player.CoinValue ?? 0;
            else if (TryAmount(what, out var bare))
                coinsWanted = bare;

            if (coinsWanted > (player.CoinValue ?? 0))
                return $"You only have {player.CoinValue ?? 0:N0} pyreals on you.";
            if (coinsWanted > 0 && player.TryConsumeFromInventoryWithNetworking(CoinWcid, (int)coinsWanted))
                pyreals += coinsWanted;

            // other currencies
            var banked = GetCurrencies(player);
            var deposited = new Dictionary<uint, long>();
            List<uint> wanted;
            long? limit = null;
            if (everything)
                wanted = Currencies.ToList();
            else if (pyreals == 0 && coinsWanted == 0 && !IsWord(what, "n", "note", "notes", "p", "py", "pyreal", "pyreals"))
            {
                var carried = player.GetAllPossessions().Where(i => Currencies.Contains(i.WeenieClassId)).Select(i => i.WeenieClassId).Distinct();
                var wcid = FindCurrency(what, carried, out var ambiguous);
                if (wcid == null)
                    return ambiguous != null ? $"Which one: {string.Join(", ", ambiguous.Select(w => CurrencyName(w, 2)))}?" : $"You carry no currency called {args[0]}. {DepositUsage}";
                wanted = new List<uint> { wcid.Value };
                if (args.Length > 1 && TryAmount(args[1], out var count))
                    limit = count;
            }
            else
                wanted = new List<uint>();

            foreach (var wcid in wanted)
            {
                foreach (var stack in player.GetAllPossessions().Where(i => i.WeenieClassId == wcid).ToList())
                {
                    var left = limit.HasValue ? limit.Value - deposited.GetValueOrDefault(wcid) : long.MaxValue;
                    if (left <= 0)
                        break;
                    var take = (int)Math.Min(left, stack.StackSize ?? 1);
                    if (player.TryConsumeFromInventoryWithNetworking(stack, take))
                        deposited[wcid] = deposited.GetValueOrDefault(wcid) + take;
                }
            }

            if (pyreals == 0 && deposited.Count == 0)
                return everything ? "You have no money on you." : "You have nothing like that to deposit.";

            foreach (var (wcid, count) in deposited)
                banked[wcid] = banked.GetValueOrDefault(wcid) + count;
            SetCurrencies(player, banked);

            if (pyreals > 0)
            {
                player.SetProperty(PropertyInt64.AcxBankedPyreals, GetBalance(player) + pyreals);
                (player as Player)?.AcxSendClientCoinValue();   // Project ACX: the client counts the bank (Player.AcxClientCoinValue)
                UpdateRanks();
                parts.Add($"{pyreals:N0} pyreals");
            }
            parts.AddRange(deposited.Select(d => $"{d.Value:N0} {CurrencyName(d.Key, d.Value)}"));
            return $"Deposited {string.Join(", ", parts)}. {Balance(player)}";
        }

        /// <summary>
        /// /bank withdraw (/b w): a pyreal amount or all (big amounts as notes), n | note &lt;size&gt; [count] (MMD, D, C... or 250k),
        /// or a currency name with an optional count (default 1)
        /// </summary>
        public static string Withdraw(Player player, string[] args)
        {
            if (args.Length == 0)
                return WithdrawUsage;

            var what = args[0].ToLowerInvariant();
            var balance = GetBalance(player);

            if (IsWord(what, "n", "note", "notes"))
            {
                if (args.Length < 2)
                    return $"Which note? {NoteList}";
                var size = NoteSizes.FirstOrDefault(n => n.Roman.Equals(args[1], StringComparison.OrdinalIgnoreCase) || (TryAmount(args[1], out var v) && v == n.Value));
                if (size.Wcid == 0)
                    return $"There's no {args[1]} note. {NoteList}";
                var count = args.Length > 2 && TryAmount(args[2], out var c) ? c : 1;
                if (count < 1 || count > 250)
                    return "You can take 1 to 250 notes of a size at a time.";
                var cost = size.Value * count;
                if (cost > balance)
                    return $"{count} {size.Roman} note{(count == 1 ? "" : "s")} come to {cost:N0} pyreals; you have {balance:N0} banked.";

                var notes = WorldObjectFactory.CreateNewWorldObject(size.Wcid);
                notes.SetStackSize((int)count);
                if (!Hand(player, new List<WorldObject> { notes }))
                    return "You can't carry that. Make some room in your pack.";
                player.SetProperty(PropertyInt64.AcxBankedPyreals, balance - cost);
                (player as Player)?.AcxSendClientCoinValue();   // Project ACX: the client counts the bank (Player.AcxClientCoinValue)
                UpdateRanks();
                return $"Withdrew {count} {notes.Name}{(count == 1 ? "" : "s")} ({cost:N0} pyreals). {Balance(player)}";
            }

            if (IsWord(what, "a", "all") || IsWord(what, "p", "py", "pyreal", "pyreals") || TryAmount(what, out _))
            {
                long amount;
                if (IsWord(what, "a", "all"))
                    amount = balance;
                else if (IsWord(what, "p", "py", "pyreal", "pyreals"))
                {
                    if (args.Length < 2 || !TryAmount(args[1], out amount))
                        return WithdrawUsage;
                }
                else
                    TryAmount(what, out amount);

                if (amount <= 0)
                    return "You have no pyreals banked.";
                if (amount > balance)
                    return $"You only have {balance:N0} pyreals banked.";

                var items = new List<WorldObject>();
                var remaining = amount;
                if (amount > CoinWithdrawalLimit)
                {
                    foreach (var (wcid, value) in TradeNotes)
                    {
                        var count = (int)(remaining / value);
                        if (count == 0)
                            continue;
                        var notes = WorldObjectFactory.CreateNewWorldObject(wcid);
                        notes.SetStackSize(count);
                        items.Add(notes);
                        remaining -= count * value;
                    }
                }
                while (remaining > 0)
                {
                    var coins = WorldObjectFactory.CreateNewWorldObject(CoinWcid);
                    var stack = (int)Math.Min(remaining, coins.MaxStackSize ?? 25000);
                    coins.SetStackSize(stack);
                    items.Add(coins);
                    remaining -= stack;
                }

                if (!Hand(player, items))
                    return "You can't carry that much. Make some room in your pack.";
                player.SetProperty(PropertyInt64.AcxBankedPyreals, balance - amount);
                (player as Player)?.AcxSendClientCoinValue();   // Project ACX: the client counts the bank (Player.AcxClientCoinValue)
                UpdateRanks();
                return $"Withdrew {amount:N0} pyreals. {Balance(player)}";
            }

            // another currency
            var banked = GetCurrencies(player);
            var found = FindCurrency(what, banked.Keys, out var ambiguous);
            if (found == null)
                return ambiguous != null ? $"Which one: {string.Join(", ", ambiguous.Select(w => CurrencyName(w, 2)))}?" : $"You have no {args[0]} banked. {WithdrawUsage}";

            var wanted = 1L;
            if (args.Length > 1)
            {
                if (IsWord(args[1].ToLowerInvariant(), "a", "all"))
                    wanted = banked[found.Value];
                else if (!TryAmount(args[1], out wanted))
                    return WithdrawUsage;
            }
            if (wanted <= 0 || wanted > banked[found.Value])
                return $"You have {banked[found.Value]:N0} {CurrencyName(found.Value, banked[found.Value])} banked.";

            var created = new List<WorldObject>();
            var left = wanted;
            while (left > 0)
            {
                var stack = WorldObjectFactory.CreateNewWorldObject(found.Value);
                var size = (int)Math.Min(left, Math.Max(1, (int)(stack.MaxStackSize ?? 1)));
                stack.SetStackSize(size);
                created.Add(stack);
                left -= size;
            }
            if (!Hand(player, created))
                return "You can't carry that. Make some room in your pack.";

            banked[found.Value] -= wanted;
            SetCurrencies(player, banked);
            return $"Withdrew {wanted:N0} {CurrencyName(found.Value, wanted)}. {Balance(player)}";
        }

        /// <summary>
        /// /bank transfer (/b t): moves pyreals or a currency from this character's bank to another character's, by name, after
        /// a yes/no. Forms: /b t &lt;amount&gt; &lt;name&gt;, /b t n &lt;size&gt; [count] &lt;name&gt; (that many notes' worth of pyreals),
        /// /b t &lt;currency&gt; [count] &lt;name&gt;. The other character may be offline; they're told if online.
        /// </summary>
        public static string Transfer(Player player, string[] args)
        {
            long pyreals = 0, count = 0;
            uint currency = 0;
            string[] nameWords;

            // Project ACX: /b t all <name> sends the whole bank, pyreals and every currency (JJ 2026-11-24, moving everything
            // to another character: "the commands aren't working. We need to simplify the instructions..")
            if (args.Length >= 2 && args[0].Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var allTarget = FindTarget(string.Join(' ', args[1..]).Trim(), out var notFound);
                if (allTarget == null)
                    return notFound;
                if (allTarget.Guid == player.Guid)
                    return "That's you.";
                var balance = GetBalance(player);
                var currencies = GetCurrencies(player).Where(c => c.Value > 0 && IsTransferable(c.Key)).ToList();
                if (balance <= 0 && currencies.Count == 0)
                    return "You have nothing in your bank you can send (attuned currencies stay with you).";
                var list = string.Join(", ", (balance > 0 ? new[] { $"{balance:N0} pyreals" } : new string[0]).Concat(currencies.Select(c => $"{c.Value:N0} {CurrencyName(c.Key, c.Value)}")));
                player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                {
                    if (GetBalance(player) > 0)
                        DoTransfer(player, allTarget, GetBalance(player), 0, 0);
                    foreach (var c in GetCurrencies(player).Where(c => c.Value > 0 && IsTransferable(c.Key)).ToList())
                        DoTransfer(player, allTarget, 0, c.Key, c.Value);
                }), $"Send your whole bank to {allTarget.Name}: {list}? Attuned currencies stay with you. This can't be undone.");
                return $"Confirm sending your whole bank to {allTarget.Name}.";
            }

            if (args.Length >= 3 && IsWord(args[0].ToLowerInvariant(), "n", "note", "notes"))
            {
                var size = NoteSizes.FirstOrDefault(n => n.Roman.Equals(args[1], StringComparison.OrdinalIgnoreCase) || (TryAmount(args[1], out var v) && v == n.Value));
                if (size.Wcid == 0)
                    return $"There's no {args[1]} note. {NoteList}";
                var notes = args.Length >= 4 && TryAmount(args[2], out var n) ? n : 1;
                nameWords = args[(notes == 1 && !TryAmount(args[2], out _) ? 2 : 3)..];
                pyreals = size.Value * notes;
            }
            else if (args.Length >= 3 && IsWord(args[0].ToLowerInvariant(), "p", "py", "pyreal", "pyreals") && TryAmount(args[1], out var p))
            {
                pyreals = p;
                nameWords = args[2..];
            }
            // Project ACX: "/b t 10 marks <name>" (count first) works as well as "/b t marks 10 <name>" (JJ 2026-11-24)
            else if (args.Length >= 3 && TryAmount(args[0], out var countFirst) && FindCurrency(args[1], GetCurrencies(player).Keys, out _) is uint currencyAfter)
            {
                currency = currencyAfter;
                count = countFirst;
                nameWords = args[2..];
            }
            else if (args.Length >= 2 && TryAmount(args[0], out var amount))
            {
                pyreals = amount;
                nameWords = args[1..];
            }
            else if (args.Length >= 2)
            {
                var found = FindCurrency(args[0], GetCurrencies(player).Keys, out var ambiguous);
                if (found == null)
                    return ambiguous != null ? $"Which one: {string.Join(", ", ambiguous.Select(w => CurrencyName(w, 2)))}?" : $"You have no {args[0]} banked (/b d {args[0]} banks them from your pack first). {TransferUsage}";
                currency = found.Value;
                // a count, or "all"; none means 1
                var all = args.Length >= 3 && args[1].Equals("all", StringComparison.OrdinalIgnoreCase);
                long c = 0;
                var counted = args.Length >= 3 && TryAmount(args[1], out c);
                count = all ? GetCurrencies(player).GetValueOrDefault(currency) : counted ? c : 1;
                nameWords = args[(all || counted ? 2 : 1)..];
            }
            else
                return TransferUsage;

            if (currency != 0 && !IsTransferable(currency))
                return $"{CurrencyName(currency, 2)} are attuned: they stay with you.";

            var name = string.Join(' ', nameWords).Trim();
            if (name.Length == 0)
                return TransferUsage;
            var target = FindTarget(name, out var missing);
            if (target == null)
                return missing;
            if (target.Guid == player.Guid)
                return "That's you.";

            var what = currency == 0 ? $"{pyreals:N0} pyreals" : $"{count:N0} {CurrencyName(currency, count)}";
            if (currency == 0 ? pyreals > GetBalance(player) : count > GetCurrencies(player).GetValueOrDefault(currency))
                return $"You don't have {what} banked. {Balance(player)}";

            player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () => DoTransfer(player, target, pyreals, currency, count)),
                $"Send {what} from your bank to {target.Name}'s bank? This can't be undone.");
            return $"Confirm sending {what} to {target.Name}.";
        }

        private static void DoTransfer(Player player, IPlayer target, long pyreals, uint currency, long count)
        {
            string what;
            if (currency == 0)
            {
                var balance = GetBalance(player);
                if (pyreals <= 0 || pyreals > balance)
                    return;
                player.SetProperty(PropertyInt64.AcxBankedPyreals, balance - pyreals);
                (player as Player)?.AcxSendClientCoinValue();   // Project ACX: the client counts the bank (Player.AcxClientCoinValue)
                target.SetProperty(PropertyInt64.AcxBankedPyreals, GetBalance(target) + pyreals);
                (target as Player)?.AcxSendClientCoinValue();   // Project ACX: the client counts the bank (Player.AcxClientCoinValue)
                what = $"{pyreals:N0} pyreals";
            }
            else
            {
                var mine = GetCurrencies(player);
                if (count <= 0 || count > mine.GetValueOrDefault(currency))
                    return;
                mine[currency] -= count;
                SetCurrencies(player, mine);
                var theirs = GetCurrencies(target);
                theirs[currency] = theirs.GetValueOrDefault(currency) + count;
                SetCurrencies(target, theirs);
                what = $"{count:N0} {CurrencyName(currency, count)}";
            }

            log.Info($"[BANK] {player.Name} sent {what} to {target.Name}");
            if (PlayerManager.GetOnlinePlayer(target.Guid) is Player online)
                online.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} sent {what} to your bank.", ChatMessageType.Broadcast));
            else
                target.SaveBiotaToDatabase();

            if (currency == 0)
                UpdateRanks();
            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Sent {what} to {target.Name}'s bank. {Balance(player)}", ChatMessageType.Broadcast));
        }

        public const string TransferUsage = "Send to another character's bank: /b t all <name> (everything), /b t <amount> <name> (pyreals), /b t marks <count> <name> (any currency). A first name is enough.";

        /// <summary>
        /// Project ACX: the character a transfer goes to: the full name, or a first name or start of a name that only one
        /// character has (JJ 2026-11-24: "Gunnar" for Gunnar Hamarr)
        /// </summary>
        private static IPlayer FindTarget(string name, out string notFound)
        {
            notFound = null;
            if (name.Length == 0)
            {
                notFound = TransferUsage;
                return null;
            }
            var exact = PlayerManager.FindByName(name);
            if (exact != null)
                return exact;
            var matches = PlayerManager.GetAllPlayers().Where(p => !p.IsDeleted && !p.IsPendingDeletion && p.Name != null
                && p.Name.TrimStart('+').StartsWith(name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 1)
                return matches[0];
            notFound = matches.Count == 0 ? $"There's no character called {name}." : $"Which one: {string.Join(", ", matches.Select(p => p.Name))}?";
            return null;
        }

        /// <summary>
        /// Puts all the items in the player's pack, or none of them
        /// </summary>
        private static bool Hand(Player player, List<WorldObject> items)
        {
            if (!player.CanAddToInventory(items))
            {
                foreach (var item in items)
                    item.Destroy();
                return false;
            }
            foreach (var item in items)
                player.TryCreateInInventoryWithNetworking(item);
            return true;
        }

        private static bool TryAmount(string text, out long amount)
        {
            text = text.Replace(",", "").ToLowerInvariant();
            var multiplier = 1L;
            if (text.EndsWith("k")) { multiplier = 1000; text = text[..^1]; }
            else if (text.EndsWith("m") && text.Length > 1 && char.IsDigit(text[^2])) { multiplier = 1000000; text = text[..^1]; }
            if (decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number) && number > 0)
            {
                amount = (long)(number * multiplier);
                return true;
            }
            amount = 0;
            return false;
        }

        private static string NoteList => "Notes: " + string.Join(", ", NoteSizes.Select(n => $"{n.Roman} {n.Value:N0}")) + ".";

        public const string DepositUsage = "Deposit: /b d a (everything: pyreals, notes, every currency), /b d n (notes), /b d <amount> (pyreals, 50k works), /b d marks [count] (or any currency by name).";

        public const string WithdrawUsage = "Withdraw: /b w <amount> (pyreals; over 500k come as notes), /b w all, /b w n <MMD|MM|M|D|C|...> [count], /b w marks [count] (or any banked currency by name).";

        public static string Balance(Player player)
        {
            var rank = GetRank(player.Guid.Full);
            var others = GetCurrencies(player).OrderBy(c => CurrencyName(c.Key, 2)).Select(c => $"{c.Value:N0} {CurrencyName(c.Key, c.Value)}").ToList();
            return $"Banked: {GetBalance(player):N0} pyreals{(others.Count > 0 ? ", " + string.Join(", ", others) : "")}. Wealth rank: {(rank > 0 ? rank.ToString() : "none")}.";
        }
    }
}
