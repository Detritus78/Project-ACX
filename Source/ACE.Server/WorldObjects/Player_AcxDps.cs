using System;

using ACE.Entity.Enum;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: a damage-per-second meter for combat testing (/dps)
    /// </summary>
    partial class Player
    {
        /// <summary>
        /// Every test runs this long from the first attack, then stops and reports
        /// </summary>
        private const double AcxDpsTestSeconds = 60;

        private bool acxDpsRunning;
        private int acxDpsTestId;
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
            acxDpsTestId++;
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

            // a test that ran the full time ends exactly on the buzzer
            if (acxDpsFirstEvent != null && (acxDpsStopTime - acxDpsFirstEvent.Value).TotalSeconds > AcxDpsTestSeconds)
                acxDpsStopTime = acxDpsFirstEvent.Value.AddSeconds(AcxDpsTestSeconds);
        }

        /// <summary>
        /// Starts the clock at the first attack and ends the test after AcxDpsTestSeconds
        /// </summary>
        private bool StartOrCheckAcxDpsClock()
        {
            if (acxDpsFirstEvent != null)
                return (DateTime.UtcNow - acxDpsFirstEvent.Value).TotalSeconds <= AcxDpsTestSeconds;

            acxDpsFirstEvent = DateTime.UtcNow;
            Session.Network.EnqueueSend(new GameMessageSystemChat($"DPS test: go! {AcxDpsTestSeconds:N0} seconds.", ChatMessageType.Broadcast));

            var testId = acxDpsTestId;
            var buzzer = new ActionChain();
            buzzer.AddDelaySeconds(AcxDpsTestSeconds);
            buzzer.AddAction(this, () =>
            {
                if (testId != acxDpsTestId || !acxDpsRunning)
                    return;

                StopAcxDps();
                Session.Network.EnqueueSend(new GameMessageSystemChat($"DPS test: time!\n{GetAcxDpsReport()}", ChatMessageType.Broadcast));
            });
            buzzer.EnqueueChain();

            return true;
        }

        /// <summary>
        /// A melee, missile or spell projectile attack: damage dealt, or a miss (evaded or resisted)
        /// </summary>
        public void RecordAcxDps(float damage, bool critical, bool missed)
        {
            AcxXphAttack(missed);   // the /xph hit rate counts every attack, meter running or not

            if (!acxDpsRunning || !StartOrCheckAcxDpsClock())
                return;

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
            if (!acxDpsRunning || acxDpsFirstEvent == null || (DateTime.UtcNow - acxDpsFirstEvent.Value).TotalSeconds > AcxDpsTestSeconds)
                return;

            acxDpsTickDamage += damage;
        }

        public string GetAcxDpsReport()
        {
            if (acxDpsFirstEvent == null)
                return acxDpsRunning ? "DPS meter running: no attacks yet." : "DPS meter: nothing recorded. Start it with /dps start.";

            var end = acxDpsRunning ? DateTime.UtcNow : acxDpsStopTime;
            var seconds = Math.Clamp((end - acxDpsFirstEvent.Value).TotalSeconds, 0.001, AcxDpsTestSeconds);
            var attacks = acxDpsHits + acxDpsMisses;
            var total = acxDpsDamage + acxDpsTickDamage;

            var report = $"DPS meter ({(acxDpsRunning ? "running" : "stopped")}): {seconds:N1} of {AcxDpsTestSeconds:N0} s\n";
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
