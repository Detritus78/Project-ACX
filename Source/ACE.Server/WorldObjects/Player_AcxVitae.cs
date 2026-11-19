using System;

using ACE.Common;
using ACE.Common.Extensions;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: the vitae overhaul (JJ 2026-11-11, ported from ClassicACE's CustomDM rules, ACE's own 5% per death
    /// and 40% cap kept for the balance pass).
    /// - Vitae soaks up experience: while you have vitae, experience you earn pays it off first and only the rest is
    ///   yours (allegiance experience doesn't count).
    /// - Vitae doesn't wear off by itself: you work it off (the 1%-per-5-minutes wear-off is gone, JJ 2026-11-18: "You
    ///   have to work it off").
    /// - A revive scroll gives a death's vitae back (Player_AcxGhost). Opening your own corpse used to give half of it
    ///   back; gone 2026-11-18 (JJ: "We already don't drop anything so lets make vitae painful").
    /// - Below level 10 a death gives no vitae and drops nothing; deaths to monsters drop no items (half the carried
    ///   pyreals go on the corpse, all of them 2026-11-11 to 11-18, Player_Death.GetNumCoinsDropped).
    /// - Corpses decay as in retail (2026-11-18; the vitae hold and its headstone are gone).
    /// </summary>
    partial class Player
    {
        public const int AcxDeathMinLevel = 10;


        /// <summary>
        /// Experience earned while carrying vitae pays it off first. Returns what's left for the player.
        /// </summary>
        private long AcxSoakXpIntoVitae(long amount)
        {
            var vitae = EnchantmentManager.GetVitae();
            if (vitae == null || amount <= 0)
                return amount;

            var penalty = vitae.StatModValue;
            var start = penalty;
            var left = amount;
            long pool = VitaeCpPool ?? 0;
            var deathLevel = DeathLevel ?? Level ?? 1;

            while (left > 0 && penalty < 1.0f)
            {
                var threshold = (long)VitaeCPPoolThreshold(penalty, deathLevel);
                var need = Math.Max(1, threshold - pool);
                if (left < need)
                {
                    pool += left;
                    left = 0;
                    break;
                }
                left -= need;
                pool = 0;
                penalty = EnchantmentManager.ReduceVitae();
            }

            VitaeCpPool = (int)pool;
            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.VitaeCpPool, VitaeCpPool.Value));
            AcxXphVitaeSoaked(amount - left);

            if (penalty != start)
                AcxVitaeChanged(penalty, "Your experience has eased your vitae");

            return left;
        }

        /// <summary>
        /// Lowers vitae by whole points (1 point = 1%)
        /// </summary>
        public void AcxReduceVitae(int points, string reason)
        {
            var vitae = EnchantmentManager.GetVitae();
            if (vitae == null || points <= 0)
                return;

            var penalty = vitae.StatModValue;
            for (var i = 0; i < points && penalty < 1.0f; i++)
                penalty = EnchantmentManager.ReduceVitae();

            if (penalty >= 1.0f)
            {
                VitaeCpPool = 0;
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.VitaeCpPool, 0));
            }
            AcxVitaeChanged(penalty, reason);
        }

        private void AcxVitaeChanged(float penalty, string reason)
        {
            if (penalty.EpsilonEquals(1.0f) || penalty > 1.0f)
            {
                EnchantmentManager.RemoveVitae();
                Session.Network.EnqueueSend(new GameMessageSystemChat($"{reason}: your vitae is gone.", ChatMessageType.Magic));
                return;
            }
            EnchantmentManager.SendUpdateVitae();
            Session.Network.EnqueueSend(new GameMessageSystemChat($"{reason}: your vitae penalty is now {Math.Round((1 - penalty) * 100)}%.", ChatMessageType.Magic));
        }
    }
}
