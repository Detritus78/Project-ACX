using System;
using System.Globalization;
using System.IO;

using ACE.Entity.Enum;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: an XP-per-hour tracker (/xph, JJ 2026-11-13). XP is being tuned so level 126 takes about 6 months at
    /// 2 hours a night, 5 nights a week, and the progression calculator (bench/progression.py, fed by /acxxpmodel)
    /// guesses the play style: how much of a session is fighting, how many attacks land, how much XP comes from quests,
    /// how much hunting is outdoors. This measures them in real sessions, from login: XP by source, kills, deaths,
    /// fighting time, hit rate and the outdoor share, per hour. Read only: it changes no XP or gameplay, and lives in
    /// memory (nothing saved on the character). Each session is appended to Logs/acx-xph.csv at logout, and on /xph log.
    /// </summary>
    partial class Player
    {
        /// <summary>
        /// Fighting time: every attack this player makes on a monster (melee, missile or spell, hit or miss) and every
        /// attack a monster makes on this player (hit, evaded or resisted) counts the next 6 seconds as fighting. Attacks
        /// closer together than that join into one stretch, so a fight counts from its first attack to 6 seconds after its
        /// last; walking to the next monster, looting, buffing and resting beyond that don't. 6 seconds bridges the slowest
        /// swing or spell cast and a monster's pause between attacks.
        /// </summary>
        private const double AcxXphFightWindowSeconds = 6;

        private static readonly string AcxXphLogFile = Path.Combine("Logs", "acx-xph.csv");

        private const string AcxXphCsvHeader = "timestamp,reason,measured_from,character,access_level,level_start,level_end,xp_start,xp_end,"
            + "session_minutes,fighting_minutes,outdoor_fighting_minutes,xp_total,xp_kill,xp_quest,xp_fellowship,xp_allegiance,xp_other,xp_admin,"
            + "vitae_soaked,outdoor_kill_xp,kills,deaths,attacks,attacks_landed,xp_per_hour,kill_xp_per_hour,quest_xp_per_hour,"
            + "fellowship_allegiance_xp_per_hour,other_xp_per_hour,kills_per_hour,xp_per_kill,fighting_share,kill_xp_per_fighting_hour,"
            + "outdoor_fighting_share,outdoor_kill_xp_share,quest_share,hit_rate";

        private static readonly object acxXphLogLock = new();

        /// <summary>
        /// One measurement: from login, or from /xph reset
        /// </summary>
        private class AcxXphStats
        {
            public DateTime Start = DateTime.UtcNow;
            public DateTime End;
            public bool FromReset;
            public int StartLevel, EndLevel;
            public long StartXp, EndXp;

            public long[] Xp = new long[Enum.GetValues<XpType>().Length];   // by XpType, as granted
            public long OutdoorKillXp;
            public long VitaeSoaked;
            public int Kills, Deaths, Attacks, Landed;

            public double FightSeconds, OutdoorFightSeconds;   // stretches up to the last attack
            public DateTime? LastFight;
            public bool LastFightOutdoors;

            /// <summary>
            /// Counts the time since the last attack as fighting, up to the window
            /// </summary>
            public void AddFightTime(DateTime now)
            {
                if (LastFight == null)
                    return;

                var seconds = Math.Clamp((now - LastFight.Value).TotalSeconds, 0, AcxXphFightWindowSeconds);
                FightSeconds += seconds;
                if (LastFightOutdoors)
                    OutdoorFightSeconds += seconds;
            }

            public AcxXphStats CopyAt(DateTime now)
            {
                var copy = (AcxXphStats)MemberwiseClone();
                copy.Xp = (long[])Xp.Clone();
                copy.AddFightTime(now);
                copy.LastFight = null;
                copy.End = now;
                return copy;
            }

            public double Seconds => Math.Max(1, (End - Start).TotalSeconds);
            public double PerHour(double amount) => amount * 3600 / Seconds;

            public long KillXp => Xp[(int)XpType.Kill];
            public long QuestXp => Xp[(int)XpType.Quest];
            public long GroupXp => Xp[(int)XpType.Fellowship] + Xp[(int)XpType.Allegiance];
            public long OtherXp => Xp[(int)XpType.Proficiency] + Xp[(int)XpType.Emote];
            public long AdminXp => Xp[(int)XpType.Admin];

            /// <summary>
            /// Everything earned by playing: admin grants (/acxlevel, /grantxp) left out
            /// </summary>
            public long TotalXp => KillXp + QuestXp + GroupXp + OtherXp;

            public double FightShare => FightSeconds / Seconds;
            public double KillXpPerFightingHour => FightSeconds > 0 ? KillXp * 3600 / FightSeconds : 0;
            public double XpPerKill => Kills > 0 ? (double)KillXp / Kills : 0;
            public double QuestShare => TotalXp > 0 ? (double)QuestXp / TotalXp : 0;
            public double HitRate => Attacks > 0 ? (double)Landed / Attacks : 0;
            public double OutdoorFightShare => FightSeconds > 0 ? OutdoorFightSeconds / FightSeconds : 0;
            public double OutdoorKillShare => KillXp > 0 ? (double)OutdoorKillXp / KillXp : 0;
        }

        private readonly object acxXphLock = new();
        private AcxXphStats acxXph;   // null until the player enters the world

        /// <summary>
        /// Outdoors the way the surface kill bonus counts it (AcxKillXpBonus): any landblock that isn't all dungeon
        /// </summary>
        private bool AcxXphOutdoors()
        {
            var landblock = CurrentLandblock;
            return landblock != null && !landblock.IsDungeon;
        }

        /// <summary>
        /// Called from PlayerEnterWorld, and by /xph reset: starts a new measurement
        /// </summary>
        public void AcxXphStart(bool reset = false)
        {
            var stats = new AcxXphStats { FromReset = reset, StartLevel = Level ?? 1, StartXp = TotalExperience ?? 0 };

            lock (acxXphLock)
                acxXph = stats;
        }

        /// <summary>
        /// Called from UpdateXpAndLevel, where every grant of experience lands: after the server modifiers, the surface
        /// bonus and the fellowship split, before vitae soaks any up
        /// </summary>
        private void AcxXphRecordXp(long amount, XpType xpType)
        {
            if (amount <= 0 || !Enum.IsDefined(xpType))
                return;

            var outdoors = AcxXphOutdoors();

            lock (acxXphLock)
            {
                if (acxXph == null)
                    return;

                acxXph.Xp[(int)xpType] += amount;
                if (xpType == XpType.Kill && outdoors)
                    acxXph.OutdoorKillXp += amount;
            }
        }

        /// <summary>
        /// Called from AcxSoakXpIntoVitae: experience that went to paying off vitae instead of the player
        /// </summary>
        private void AcxXphVitaeSoaked(long amount)
        {
            lock (acxXphLock)
            {
                if (acxXph != null)
                    acxXph.VitaeSoaked += amount;
            }
        }

        /// <summary>
        /// Called from Creature.OnDeath_GrantXP for each player given a share of a monster's kill experience
        /// </summary>
        public void AcxXphKill(Creature victim)
        {
            if (victim is Player)
                return;

            lock (acxXphLock)
            {
                if (acxXph != null)
                    acxXph.Kills++;
            }
        }

        /// <summary>
        /// Called from OnDeath
        /// </summary>
        private void AcxXphDeath()
        {
            lock (acxXphLock)
            {
                if (acxXph != null)
                    acxXph.Deaths++;
            }
        }

        /// <summary>
        /// Called from RecordAcxDps: a melee, missile or spell projectile attack landed, or missed (evaded or resisted)
        /// </summary>
        private void AcxXphAttack(bool missed)
        {
            lock (acxXphLock)
            {
                if (acxXph == null)
                    return;

                acxXph.Attacks++;
                if (!missed)
                    acxXph.Landed++;
            }
        }

        /// <summary>
        /// Called from OnAttackMonster (this player attacked a monster) and SetCurrentAttacker (a monster attacked this
        /// player): carries on the current stretch of fighting, or starts a new one. Fights with players don't count.
        /// </summary>
        public void AcxXphFighting(Creature other)
        {
            if (other == null || other is Player)
                return;

            var outdoors = AcxXphOutdoors();

            lock (acxXphLock)
            {
                if (acxXph == null)
                    return;

                var now = DateTime.UtcNow;
                acxXph.AddFightTime(now);
                acxXph.LastFight = now;
                acxXph.LastFightOutdoors = outdoors;
            }
        }

        /// <summary>
        /// The numbers so far, with the current stretch of fighting counted up to now
        /// </summary>
        private AcxXphStats AcxXphSnapshot()
        {
            lock (acxXphLock)
            {
                if (acxXph == null)
                    return null;

                var stats = acxXph.CopyAt(DateTime.UtcNow);
                stats.EndLevel = Level ?? 1;
                stats.EndXp = TotalExperience ?? 0;
                return stats;
            }
        }

        public string AcxXphReport()
        {
            var s = AcxXphSnapshot();
            if (s == null)
                return "XP per hour: nothing measured yet.";

            var report = $"XP per hour for {Name}, {(s.FromReset ? "since /xph reset" : "since login")} {AcxXphTimeText(s.Seconds)} ago\n";

            report += $"Level {s.StartLevel} to {s.EndLevel}, total experience +{s.EndXp - s.StartXp:N0}";
            if (s.VitaeSoaked > 0)
                report += $", after vitae soaked up {s.VitaeSoaked:N0}";
            if (s.AdminXp > 0)
                report += $" (admin grants {s.AdminXp:N0}, not counted below)";
            report += "\n";

            report += $"Earned {s.TotalXp:N0} XP: {s.PerHour(s.TotalXp):N0} per hour\n";
            report += $"  kills {s.KillXp:N0} ({s.PerHour(s.KillXp):N0}/h), quests {s.QuestXp:N0} ({s.PerHour(s.QuestXp):N0}/h), "
                + $"fellowship/allegiance {s.GroupXp:N0} ({s.PerHour(s.GroupXp):N0}/h), other {s.OtherXp:N0} ({s.PerHour(s.OtherXp):N0}/h)\n";
            report += $"  quests: {100 * s.QuestShare:N0}% of all XP\n";

            report += $"Kills: {s.Kills:N0} ({s.PerHour(s.Kills):N0} per hour), {s.XpPerKill:N0} XP per kill. Deaths: {s.Deaths}\n";
            report += $"Fighting: {AcxXphTimeText(s.FightSeconds)} of {AcxXphTimeText(s.Seconds)} ({100 * s.FightShare:N0}%), "
                + $"kill XP per fighting hour {s.KillXpPerFightingHour:N0}\n";
            report += $"Attacks: {s.Attacks:N0}, {100 * s.HitRate:N0}% landed\n";
            report += $"Outdoors: {100 * s.OutdoorFightShare:N0}% of fighting time, {100 * s.OutdoorKillShare:N0}% of kill XP";

            return report;
        }

        /// <summary>
        /// Appends this measurement to Logs/acx-xph.csv: at logout (reason "logout") and on /xph log ("command")
        /// </summary>
        public bool AcxXphWriteLog(string reason)
        {
            var s = AcxXphSnapshot();
            if (s == null)
                return false;

            static string F(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

            var row = string.Join(",",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), reason, s.FromReset ? "reset" : "login",
                Name.Contains(',') || Name.Contains('"') ? $"\"{Name.Replace("\"", "\"\"")}\"" : Name,
                Account != null ? ((AccessLevel)Account.AccessLevel).ToString() : "",
                s.StartLevel, s.EndLevel, s.StartXp, s.EndXp,
                F(s.Seconds / 60, "0.0"), F(s.FightSeconds / 60, "0.0"), F(s.OutdoorFightSeconds / 60, "0.0"),
                s.TotalXp, s.KillXp, s.QuestXp, s.Xp[(int)XpType.Fellowship], s.Xp[(int)XpType.Allegiance], s.OtherXp, s.AdminXp,
                s.VitaeSoaked, s.OutdoorKillXp, s.Kills, s.Deaths, s.Attacks, s.Landed,
                F(s.PerHour(s.TotalXp), "0"), F(s.PerHour(s.KillXp), "0"), F(s.PerHour(s.QuestXp), "0"),
                F(s.PerHour(s.GroupXp), "0"), F(s.PerHour(s.OtherXp), "0"), F(s.PerHour(s.Kills), "0.0"), F(s.XpPerKill, "0"),
                F(s.FightShare, "0.000"), F(s.KillXpPerFightingHour, "0"),
                F(s.OutdoorFightShare, "0.000"), F(s.OutdoorKillShare, "0.000"), F(s.QuestShare, "0.000"), F(s.HitRate, "0.000"));

            try
            {
                lock (acxXphLogLock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(AcxXphLogFile));

                    var isNew = !File.Exists(AcxXphLogFile) || new FileInfo(AcxXphLogFile).Length == 0;
                    File.AppendAllText(AcxXphLogFile, (isNew ? AcxXphCsvHeader + "\n" : "") + row + "\n");
                }
                return true;
            }
            catch (Exception e)
            {
                log.Warn($"AcxXph: couldn't write {Name}'s session to {AcxXphLogFile}: {e.Message}");
                return false;
            }
        }

        private static string AcxXphTimeText(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return time.TotalHours >= 1 ? $"{(int)time.TotalHours}h {time.Minutes:00}m"
                : time.TotalMinutes >= 1 ? $"{time.Minutes}m {time.Seconds:00}s"
                : $"{time.Seconds}s";
        }
    }
}
