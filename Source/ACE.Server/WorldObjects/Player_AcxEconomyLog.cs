using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: the economy log, for the first month of play (JJ 2026-11-17: "Lets certain add the mark tracking and pyreals
    /// gain rate by character. maybe track xp progression also. lets do that for one month. Keep the logs simple"). One short
    /// line per play session in Logs/acx-economy.csv, written at logout beside the /xph line (Logs/acx-xph.csv, which has the
    /// XP detail):
    ///   end,character,minutes,level,levels,xp,coin,sold,bought,marks_in,marks_out,marks_held,mark_sources
    /// coin = change in pyreals carried plus banked; sold / bought = pyreals from and to vendors for goods (trade notes left
    /// out); marks_in = Marks gained, by source in mark_sources ("contract 2 job 1"; "other" is the rest of the change in Marks
    /// held: gambling, trades, the bank, negative when given away); marks_out = Marks spent at vendors (the Mark Trader).
    /// Sessions under a minute aren't written. Admins only with acx_economy_log_admins. It runs acx_economy_log_days from the
    /// first line written (acx_economy_log_started records that day), then stops by itself; 0 days turns it off.
    /// </summary>
    partial class Player
    {
        private static readonly string AcxEconomyLogFile = Path.Combine("Logs", "acx-economy.csv");

        private const string AcxEconomyCsvHeader = "end,character,minutes,level,levels,xp,coin,sold,bought,marks_in,marks_out,marks_held,mark_sources";

        private static readonly object acxEconomyLogLock = new();

        private DateTime acxEconStart;
        private int acxEconStartLevel;
        private long acxEconStartXp, acxEconStartCoin, acxEconStartMarks;
        private long acxEconSold, acxEconBought, acxEconMarksSpent;
        private readonly Dictionary<string, long> acxEconMarkSources = new();

        private long AcxEconCoin() => (CoinValue ?? 0) + AcxWealth.GetBalance(this);

        private long AcxEconMarksHeld() => AcxMarks.Wcid == 0 ? 0 : GetNumInventoryItemsOfWCID(AcxMarks.Wcid) + AcxWealth.GetBankedCurrency(this, AcxMarks.Wcid);

        /// <summary>
        /// At login: where the session starts
        /// </summary>
        private void AcxEconStart()
        {
            acxEconStart = DateTime.UtcNow;
            acxEconStartLevel = Level ?? 1;
            acxEconStartXp = TotalExperience ?? 0;
            acxEconStartCoin = AcxEconCoin();
            acxEconStartMarks = AcxEconMarksHeld();
            acxEconSold = acxEconBought = acxEconMarksSpent = 0;
            acxEconMarkSources.Clear();
        }

        public void AcxEconMarksEarned(string source, long amount)
        {
            acxEconMarkSources[source] = (acxEconMarkSources.TryGetValue(source, out var n) ? n : 0) + amount;
        }

        public void AcxEconVendorSold(long pyreals) => acxEconSold += pyreals;

        public void AcxEconVendorBought(long pyreals) => acxEconBought += pyreals;

        public void AcxEconMarksSpent(long marks) => acxEconMarksSpent += marks;

        private static bool AcxEconomyLogOn()
        {
            var days = PropertyManager.GetLong("acx_economy_log_days").Item;
            if (days <= 0)
                return false;

            var started = PropertyManager.GetString("acx_economy_log_started").Item;
            if (string.IsNullOrEmpty(started))
            {
                PropertyManager.ModifyString("acx_economy_log_started", DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                return true;
            }
            return !DateTime.TryParse(started, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var from)
                || DateTime.UtcNow < from.AddDays(days);
        }

        /// <summary>
        /// At logout: the session's line
        /// </summary>
        private void AcxEconWriteLog()
        {
            if (acxEconStart == default)
                return;

            var minutes = (DateTime.UtcNow - acxEconStart).TotalMinutes;
            if (minutes < 1)
                return;
            if (IsAdmin && !PropertyManager.GetBool("acx_economy_log_admins").Item)
                return;
            if (!AcxEconomyLogOn())
                return;

            var held = AcxEconMarksHeld();
            var tracked = acxEconMarkSources.Values.Sum();
            var other = held - acxEconStartMarks + acxEconMarksSpent - tracked;
            var sources = acxEconMarkSources.Where(kv => kv.Value != 0).Select(kv => $"{kv.Key} {kv.Value}").ToList();
            if (other != 0)
                sources.Add($"other {other}");

            var line = string.Join(",",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),   // the server's local time, as Logs/acx-xph.csv
                Name.Replace(",", ""),
                ((int)Math.Round(minutes)).ToString(CultureInfo.InvariantCulture),
                (Level ?? 1).ToString(CultureInfo.InvariantCulture),
                ((Level ?? 1) - acxEconStartLevel).ToString(CultureInfo.InvariantCulture),
                ((TotalExperience ?? 0) - acxEconStartXp).ToString(CultureInfo.InvariantCulture),
                (AcxEconCoin() - acxEconStartCoin).ToString(CultureInfo.InvariantCulture),
                acxEconSold.ToString(CultureInfo.InvariantCulture),
                acxEconBought.ToString(CultureInfo.InvariantCulture),
                (tracked + other).ToString(CultureInfo.InvariantCulture),
                acxEconMarksSpent.ToString(CultureInfo.InvariantCulture),
                held.ToString(CultureInfo.InvariantCulture),
                string.Join(" ", sources));

            try
            {
                lock (acxEconomyLogLock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(AcxEconomyLogFile));
                    var isNew = !File.Exists(AcxEconomyLogFile);
                    using var writer = new StreamWriter(AcxEconomyLogFile, append: true);
                    if (isNew)
                        writer.WriteLine(AcxEconomyCsvHeader);
                    writer.WriteLine(line);
                }
            }
            catch (Exception ex)
            {
                log.Warn($"[ACX ECONOMY] {Name}: couldn't write {AcxEconomyLogFile}: {ex.Message}");
            }

            acxEconStart = default;
        }
    }
}
