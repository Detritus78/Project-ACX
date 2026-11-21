using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: leased vaults (JJ 2026-11-13, alpha), in Concordia since 2026-11-14 (acx_vault_landblock, 018A). All
    /// storage is physical: vault chests stand in rooms, several to a room, each with a hook above it. A vault belongs to an account:
    /// it opens for every character on that account, and for anyone carrying one of its keys (the key only has to be in
    /// the pack). The room gates stay open; only chests and hooks are locked.
    /// Everything is done by using an item from the Vault Keeper on a vault: a Vault Deed leases a free vault, a Vault
    /// Rent Writ pays 30 more days (anyone may pay), a Blank Vault Key becomes a key (leaseholder only), and a Vault Lock
    /// changes the lock so every old key stops working (leaseholder only, comes with one new key). Unpaid rent loses the lease
    /// (after acx_vault_grace_days): the vault is free again for anyone's deed and whatever is inside goes with it (JJ
    /// 2026-11-13: "Items stay in the box but its open for anyone to buy if you lose your box"); its burden shows it isn't
    /// empty. /vault abandon gives a lease up the same way, like abandoning a house. A vault is separate from housing: an
    /// account can hold a house, villa or mansion and a vault.
    /// The vaults are Storage chests and hooks without a house: their lease lives on the chest and is saved to the shard
    /// like house storage. Vaults are placed by hand (JJ, with ACX Foundry), so nothing depends on their ids: the vaults in
    /// one dungeon cell make a room, rooms are numbered in the order their first vault was placed ("Vault 2-C" is the
    /// third vault of the second room), a hook belongs to the nearest vault, and a key names its vault by guid.
    /// Content: acx/Content/storage/10-haven-vaults.sql (weenies), placements in acx/Content/concordia/. Settings: acx_vault_*.
    /// </summary>
    public static class AcxVaults
    {
        public const uint VaultWcid = 900700;
        public const uint HookWcid = 900701;
        public const uint KeyWcid = 900706;
        public const uint FloorHookWcid = 900709;   // in front of each vault (JJ 2026-11-21)

        // vaults closer than this share a room; a hook belongs to a vault within HookReach
        private const float HookReach = 4.0f;

        private enum Tool { None, Deed, RentWrit, BlankKey, Lock }

        public static bool IsVault(WorldObject wo) => wo.WeenieClassId == VaultWcid;

        public static bool IsVaultHook(WorldObject wo) => wo.WeenieClassId == HookWcid || wo.WeenieClassId == FloorHookWcid;

        public static bool IsTool(WorldObject wo) => wo.WeenieClassId == KeyWcid || (wo.GetProperty(PropertyInt.AcxVaultTool) ?? 0) > 0;

        /// <summary>
        /// Every vault's name in a landblock: rooms (the dungeon cell the vaults stand in) numbered by their first vault's
        /// guid, letters by guid within the room. Rooms used to be vaults within 8 m of each other, which joined vault rooms
        /// that share a wall (2026-11-14).
        /// </summary>
        private static Dictionary<uint, string> Names(Landblock landblock)
        {
            var roomOfCell = new Dictionary<uint, int>();
            var letters = new List<int>();
            var names = new Dictionary<uint, string>();
            foreach (var vault in AllVaults(landblock))
            {
                if (!roomOfCell.TryGetValue(vault.Location.Cell, out var room))
                {
                    room = roomOfCell[vault.Location.Cell] = letters.Count;
                    letters.Add(0);
                }
                names[vault.Guid.Full] = $"Vault {room + 1}-{(char)('A' + Math.Min(25, letters[room]++))}";
            }
            return names;
        }

        public static string VaultName(WorldObject vault) => Names(vault.CurrentLandblock).TryGetValue(vault.Guid.Full, out var name) ? name : vault.Name;

        /// <summary>
        /// Gives every vault in the landblock its current name (they change when vaults are added or removed)
        /// </summary>
        private static void RefreshNames(Landblock landblock)
        {
            foreach (var (guid, name) in Names(landblock))
            {
                var vault = landblock.GetObject(guid);
                if (vault == null || vault.Name == name)
                    continue;
                vault.Name = name;
                vault.EnqueueBroadcast(new GameMessagePublicUpdatePropertyString(vault, PropertyString.Name, name));
            }
        }

        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private static uint? Owner(WorldObject vault) => (uint?)vault.GetProperty(PropertyInt.AcxVaultOwnerAccount);

        /// <summary>
        /// True once a lease has run out past the grace days: the vault is free again, contents and all
        /// </summary>
        private static bool Lapsed(WorldObject vault) => Owner(vault) != null && PaidUntil(vault) + Setting("acx_vault_grace_days") * 86400 < Now;

        /// <summary>
        /// The account leasing the vault, or null when it is free or its lease has lapsed
        /// </summary>
        private static uint? Leaseholder(WorldObject vault) => Lapsed(vault) ? null : Owner(vault);

        private static long PaidUntil(WorldObject vault) => vault.GetProperty(PropertyInt64.AcxVaultPaidUntil) ?? 0;

        private static string Date(long unixSeconds) => DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("MMM d, yyyy");

        private static long Setting(string name) => PropertyManager.GetLong(name).Item;

        /// <summary>
        /// Called from Storage.SetEphemeralValues: name and size come from the vault's number and the settings
        /// </summary>
        public static void OnVaultCreated(Storage vault)
        {
            // named once its landblock has loaded the other vaults
            var chain = new ActionChain();
            chain.AddDelaySeconds(3);
            chain.AddAction(vault, () => { if (vault.CurrentLandblock != null) RefreshNames(vault.CurrentLandblock); });
            chain.EnqueueChain();

            vault.ItemCapacity = (byte)Math.Clamp(Setting("acx_vault_item_slots"), 1, 255);
            vault.ContainerCapacity = (byte)Math.Clamp(Setting("acx_vault_bag_slots"), 0, 255);
        }

        /// <summary>
        /// Why this player can't open the vault (or its hook), or null when they can
        /// </summary>
        private static string Refusal(WorldObject vault, Player player)
        {
            var name = VaultName(vault);
            var owner = Leaseholder(vault);

            if (owner == null)
                return $"{name} is free. Buy a Vault Deed from the Vault Keeper and use it on the vault to lease it.";
            if (owner == player.Account.AccountId || HasKey(player, vault))
                return null;
            return $"{name} is locked.";
        }

        private static bool HasKey(Player player, WorldObject vault)
        {
            var vaultId = unchecked((int)vault.Guid.Full);
            var code = vault.GetProperty(PropertyInt.AcxVaultLockCode);
            return player.GetInventoryItemsOfWCID(KeyWcid).Any(k => k.GetProperty(PropertyInt.AcxVaultKeyFor) == vaultId && k.GetProperty(PropertyInt.AcxVaultLockCode) == code);
        }

        /// <summary>
        /// Called from Storage.CheckUseRequirements for vaults
        /// </summary>
        public static ActivationResult CheckUse(WorldObject vault, Player player)
        {
            if (player.IgnoreHouseBarriers)
                return new ActivationResult(true);

            var refusal = Refusal(vault, player);
            if (refusal != null)
            {
                player.SendTransientError(refusal);
                vault.EnqueueBroadcast(new GameMessageSound(vault.Guid, Sound.OpenFailDueToLock, 1.0f));
                return new ActivationResult(false);
            }

            if (Leaseholder(vault) == player.Account.AccountId)
            {
                var daysLeft = (PaidUntil(vault) - Now) / 86400.0;
                if (daysLeft < 0)
                    player.SendTransientError($"Rent on {VaultName(vault)} is overdue since {Date(PaidUntil(vault))}: pay it before {Date(PaidUntil(vault) + Setting("acx_vault_grace_days") * 86400)} or lose the vault and everything in it.");
                else if (daysLeft < Setting("acx_vault_rent_warning_days"))
                    player.SendTransientError($"Rent on {VaultName(vault)} is due {Date(PaidUntil(vault))}. Unpaid, the vault and everything in it goes to the next leaseholder.");
            }
            return new ActivationResult(true);
        }

        /// <summary>
        /// Called from Hook.CheckUseRequirements for vault hooks: the hook follows its vault's lease and keys
        /// </summary>
        public static ActivationResult CheckHookUse(WorldObject hook, Player player)
        {
            if (player.IgnoreHouseBarriers)
                return new ActivationResult(true);

            var vault = VaultOfHook(hook);
            var refusal = vault == null ? "This hook isn't working." : Refusal(vault, player);
            if (refusal != null)
            {
                player.SendTransientError(refusal);
                return new ActivationResult(false);
            }
            return new ActivationResult(true);
        }

        private static WorldObject VaultOfHook(WorldObject hook)
        {
            return AllVaults(hook.CurrentLandblock).Where(v => v.Location.DistanceTo(hook.Location) <= HookReach)
                .OrderBy(v => v.Location.DistanceTo(hook.Location)).FirstOrDefault();
        }

        /// <summary>
        /// The vault's line in its appraisal
        /// </summary>
        public static string Describe(WorldObject vault)
        {
            if (Leaseholder(vault) == null)
                return $"{VaultName(vault)} is free to lease: buy a Vault Deed from the Vault Keeper and use it on the vault. Whatever is inside comes with it.\n";

            var leasedBy = vault.GetProperty(PropertyString.AcxVaultOwnerName);
            var paid = PaidUntil(vault) < Now ? $"Its rent is overdue since {Date(PaidUntil(vault))}." : $"Rent is paid through {Date(PaidUntil(vault))}.";
            return $"{VaultName(vault)} is leased by {leasedBy}. {paid}\n";
        }

        public static string DescribeHook(WorldObject hook)
        {
            var vault = VaultOfHook(hook);
            return vault != null ? $"This hook belongs to {VaultName(vault)}. " : "";
        }

        /// <summary>
        /// Called from WorldObject.HandleActionUseOnTarget once the player has walked to the target: deeds, rent writs,
        /// blank keys and locks. Returns false for anything else.
        /// </summary>
        public static bool TryUseTool(Player player, WorldObject tool, WorldObject target)
        {
            var kind = (Tool)(tool.GetProperty(PropertyInt.AcxVaultTool) ?? 0);
            if (kind == Tool.None)
                return false;

            string message;
            var used = false;

            if (!IsVault(target))
                message = $"Use the {tool.Name} on a vault in Concordia.";
            else
            {
                (message, used) = kind switch
                {
                    Tool.Deed => Lease(player, target),
                    Tool.RentWrit => PayRent(target),
                    Tool.BlankKey => CutKey(player, target),
                    _ => ChangeLock(player, target),
                };
            }

            if (used)
            {
                player.TryConsumeFromInventoryWithNetworking(tool, 1);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Broadcast));
            }
            else
                player.SendTransientError(message);

            player.SendUseDoneEvent();
            return true;
        }

        private static (string, bool) Lease(Player player, WorldObject vault)
        {
            var name = VaultName(vault);
            if (Leaseholder(vault) != null)
                return ($"{name} is already leased.", false);

            var account = player.Account.AccountId;
            var mine = AllVaults(vault.CurrentLandblock).Where(v => Leaseholder(v) == account).ToList();
            var allowed = Setting("acx_vault_per_account");   // 0 = no limit (JJ 2026-11-14: "remove the number of storage owned restriction")
            if (allowed > 0 && mine.Count >= allowed)
                return ($"Your account already leases {string.Join(", ", mine.Select(VaultName))}; an account can lease {allowed}.", false);

            var days = Setting("acx_vault_rent_days");
            vault.SetProperty(PropertyInt.AcxVaultOwnerAccount, (int)account);
            vault.SetProperty(PropertyString.AcxVaultOwnerName, player.Name);
            vault.SetProperty(PropertyInt64.AcxVaultPaidUntil, Now + days * 86400);
            vault.SetProperty(PropertyInt.AcxVaultLockCode, ThreadSafeRandom.Next(1, int.MaxValue - 1));
            vault.SaveBiotaToDatabase();

            return ($"You lease {name}, paid through {Date(PaidUntil(vault))}. It opens for every character on your account and for anyone carrying one of its keys. Blank Vault Keys from the Vault Keeper become keys when used on it.", true);
        }

        private static (string, bool) PayRent(WorldObject vault)
        {
            var name = VaultName(vault);
            if (Leaseholder(vault) == null)
                return ($"{name} isn't leased; a Vault Deed leases it.", false);

            var paidUntil = Math.Max(Now, PaidUntil(vault)) + Setting("acx_vault_rent_days") * 86400;
            var maxAhead = Setting("acx_vault_max_prepaid_days");
            if (paidUntil - Now > maxAhead * 86400)
                return ($"{name} is paid through {Date(PaidUntil(vault))}; rent can be paid up to {maxAhead} days ahead.", false);

            vault.SetProperty(PropertyInt64.AcxVaultPaidUntil, paidUntil);
            vault.SaveBiotaToDatabase();
            return ($"{name} is paid through {Date(paidUntil)}.", true);
        }

        private static (string, bool) CutKey(Player player, WorldObject vault)
        {
            var name = VaultName(vault);
            if (Leaseholder(vault) != player.Account.AccountId)
                return ($"Only the leaseholder of {name} can cut its keys.", false);

            if (!GiveKey(player, vault))
                return ("You have no room for the key.", false);

            return ($"You cut a key to {name}. Whoever carries it can open the vault and its hook.", true);
        }

        private static (string, bool) ChangeLock(Player player, WorldObject vault)
        {
            var name = VaultName(vault);
            if (Leaseholder(vault) != player.Account.AccountId)
                return ($"Only the leaseholder of {name} can change its lock.", false);

            vault.SetProperty(PropertyInt.AcxVaultLockCode, ThreadSafeRandom.Next(1, int.MaxValue - 1));
            vault.SaveBiotaToDatabase();

            var keyText = GiveKey(player, vault) ? "Here is one new key." : "You had no room for the new key; a Blank Vault Key used on the vault cuts one.";
            return ($"The lock on {name} is changed: old keys no longer open it. {keyText}", true);
        }

        private static bool GiveKey(Player player, WorldObject vault)
        {
            var key = WorldObjectFactory.CreateNewWorldObject(KeyWcid);
            if (key == null)
                return false;

            key.SetProperty(PropertyInt.AcxVaultKeyFor, unchecked((int)vault.Guid.Full));
            key.SetProperty(PropertyInt.AcxVaultLockCode, vault.GetProperty(PropertyInt.AcxVaultLockCode) ?? 0);
            key.Name = $"Key to {VaultName(vault)}";

            if (player.TryCreateInInventoryWithNetworking(key))
                return true;

            key.Destroy();
            return false;
        }

        private static List<WorldObject> AllVaults(Landblock landblock) =>
            landblock == null ? new List<WorldObject>() : landblock.GetAllWorldObjectsForDiagnostics().Where(IsVault).OrderBy(v => v.Guid.Full).ToList();

        /// <summary>
        /// Frees a vault; whatever is inside stays for the next leaseholder
        /// </summary>
        private static void Release(WorldObject vault)
        {
            vault.RemoveProperty(PropertyInt.AcxVaultOwnerAccount);
            vault.RemoveProperty(PropertyString.AcxVaultOwnerName);
            vault.RemoveProperty(PropertyInt64.AcxVaultPaidUntil);
            vault.RemoveProperty(PropertyInt.AcxVaultLockCode);
            vault.SaveBiotaToDatabase();
        }

        /// <summary>
        /// The landblock the vaults stand in (loaded on demand; its objects appear a moment after loading)
        /// </summary>
        public static Landblock VaultLandblock => LandblockManager.GetLandblock(new LandblockId((uint)Setting("acx_vault_landblock") << 16 | 0xFFFF), false);

        /// <summary>
        /// /vault: the vaults the player's account leases
        /// </summary>
        public static string Status(Player player)
        {
            var mine = AllVaults(VaultLandblock).Where(v => Leaseholder(v) == player.Account.AccountId).ToList();
            if (mine.Count == 0)
                return "Your account doesn't lease a vault. The Vault Keeper in Concordia sells Vault Deeds.";
            var lines = mine.Select(vault =>
            {
                var overdue = PaidUntil(vault) < Now ? $" It is overdue: pay before {Date(PaidUntil(vault) + Setting("acx_vault_grace_days") * 86400)} or lose it and everything in it." : "";
                return $"{VaultName(vault)} ({(vault as Container)?.Inventory.Count ?? 0} items inside), rent paid through {Date(PaidUntil(vault))}.{overdue}";
            });
            return mine.Count == 1
                ? $"Your account leases {lines.First()} /vault abandon gives it up."
                : $"Your account leases:\n{string.Join("\n", lines)}\n/vault abandon {ShortName(mine[0])} gives one up.";
        }

        private static string ShortName(WorldObject vault) => VaultName(vault).Replace("Vault ", "");

        /// <summary>
        /// /vault abandon [name]: gives up one of the account's leases after a yes/no, like abandoning a house; the contents
        /// stay in the vault. The name is needed only when the account leases more than one.
        /// </summary>
        public static void Abandon(Player player, string which = null)
        {
            var mine = AllVaults(VaultLandblock).Where(v => Leaseholder(v) == player.Account.AccountId).ToList();
            if (mine.Count == 0)
            {
                player.SendTransientError("Your account doesn't lease a vault.");
                return;
            }
            var vault = which != null
                ? mine.FirstOrDefault(v => ShortName(v).Equals(which, StringComparison.OrdinalIgnoreCase) || VaultName(v).Equals(which, StringComparison.OrdinalIgnoreCase))
                : mine.Count == 1 ? mine[0] : null;
            if (vault == null)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Which one? Your account leases {string.Join(", ", mine.Select(ShortName))}: /vault abandon {ShortName(mine[0])}", ChatMessageType.Broadcast));
                return;
            }

            var name = VaultName(vault);
            var count = (vault as Container)?.Inventory.Count ?? 0;
            var question = $"Give up {name}? {(count > 0 ? $"The {count} item{(count == 1 ? "" : "s")} inside stay in it for whoever leases it next." : "It is empty.")} Rent already paid is not refunded.";
            player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
            {
                if (vault.IsDestroyed || Leaseholder(vault) != player.Account.AccountId)
                    return;
                Release(vault);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You give up {name}. Its keys no longer open it.", ChatMessageType.Broadcast));
            }), question);
        }

        /// <summary>
        /// /acxvault: the vaults' leases, for testing
        /// </summary>
        public static string List(Landblock landblock)
        {
            var lines = AllVaults(landblock).Select(v => Owner(v) == null
                ? $"{VaultName(v)}: free ({v.ItemCapacity}+{v.ContainerCapacity} slots), {(v as Container)?.Inventory.Count ?? 0} items"
                : $"{VaultName(v)}: {v.GetProperty(PropertyString.AcxVaultOwnerName)} (account {Owner(v)}), paid through {Date(PaidUntil(v))}{(Lapsed(v) ? " LAPSED (free)" : PaidUntil(v) < Now ? " OVERDUE" : "")}, {(v as Container)?.Inventory.Count ?? 0} items").ToList();
            return lines.Count == 0 ? "No vaults found (the vault landblock may still be loading; try again)." : string.Join("\n", lines);
        }

        /// <summary>
        /// /acxvault release|paid: free a vault (items stay in it) or move its paid-through date to days from now
        /// </summary>
        public static string Admin(Landblock landblock, string verb, string vaultName, int days)
        {
            var vault = AllVaults(landblock).FirstOrDefault(v => VaultName(v).Equals($"Vault {vaultName}", StringComparison.OrdinalIgnoreCase) || VaultName(v).Equals(vaultName, StringComparison.OrdinalIgnoreCase));
            if (vault == null)
                return $"No vault {vaultName} here.";

            if (verb == "release")
                Release(vault);
            else
            {
                vault.SetProperty(PropertyInt64.AcxVaultPaidUntil, Now + (long)days * 86400);
                vault.SaveBiotaToDatabase();
            }
            return $"{VaultName(vault)}: {(verb == "release" ? "free again (anything inside stays)" : $"paid through {Date(PaidUntil(vault))}")}.";
        }
    }
}
