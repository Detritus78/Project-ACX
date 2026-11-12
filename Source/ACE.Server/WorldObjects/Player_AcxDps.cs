using System;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: a damage-per-second meter for combat testing (/dps)
    /// </summary>
    partial class Player
    {
        private bool acxDpsRunning;
        private DateTime? acxDpsFirstEvent;
        private DateTime acxDpsStopTime;
        private double acxDpsDamage;
        private double acxDpsTickDamage;
        private int acxDpsHits;
        private int acxDpsCrits;
        private int acxDpsMisses;

        public bool AcxDpsRunning => acxDpsRunning;

        public void StartAcxDps()
        {
            acxDpsRunning = true;
            acxDpsFirstEvent = null;
            acxDpsDamage = acxDpsTickDamage = 0;
            acxDpsHits = acxDpsCrits = acxDpsMisses = 0;
        }

        public void StopAcxDps()
        {
            if (!acxDpsRunning)
                return;

            acxDpsRunning = false;
            acxDpsStopTime = DateTime.UtcNow;
        }

        /// <summary>
        /// A melee, missile or spell projectile attack: damage dealt, or a miss (evaded or resisted)
        /// </summary>
        public void RecordAcxDps(float damage, bool critical, bool missed)
        {
            if (!acxDpsRunning)
                return;

            acxDpsFirstEvent ??= DateTime.UtcNow;

            if (missed)
            {
                acxDpsMisses++;
                return;
            }

            acxDpsHits++;
            acxDpsDamage += damage;

            if (critical)
                acxDpsCrits++;
        }

        /// <summary>
        /// A damage-over-time tick from one of this player's spells
        /// </summary>
        public void RecordAcxDpsTick(float damage)
        {
            if (!acxDpsRunning || acxDpsFirstEvent == null)
                return;

            acxDpsTickDamage += damage;
        }

        public string GetAcxDpsReport()
        {
            if (acxDpsFirstEvent == null)
                return acxDpsRunning ? "DPS meter running: no attacks yet." : "DPS meter: nothing recorded. Start it with /dps start.";

            var end = acxDpsRunning ? DateTime.UtcNow : acxDpsStopTime;
            var seconds = Math.Max(0.001, (end - acxDpsFirstEvent.Value).TotalSeconds);
            var attacks = acxDpsHits + acxDpsMisses;
            var total = acxDpsDamage + acxDpsTickDamage;

            var report = $"DPS meter ({(acxDpsRunning ? "running" : "stopped")}): {seconds:N1} s since the first attack\n";
            report += $"Damage: {total:N0} ({total / seconds:N1} per second)";
            if (acxDpsTickDamage > 0)
                report += $", of which {acxDpsTickDamage:N0} from damage over time";
            report += "\n";
            report += $"Attacks: {attacks} ({attacks / seconds * 60:N1} per minute), hits {acxDpsHits}, misses {acxDpsMisses}";
            if (attacks > 0)
                report += $" ({100.0 * acxDpsMisses / attacks:N0}%)";
            report += "\n";
            if (acxDpsHits > 0)
                report += $"Average hit {acxDpsDamage / acxDpsHits:N1}, crits {acxDpsCrits} ({100.0 * acxDpsCrits / acxDpsHits:N0}% of hits)";

            return report;
        }
    }
}
