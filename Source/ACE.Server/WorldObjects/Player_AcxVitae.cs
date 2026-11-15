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
    /// - Vitae also wears off by itself, 1% every 5 minutes, counting time offline.
    /// - The corpse remembers how much vitae its death added; the owner gets half back the first time they open it
    ///   (a revive scroll gives it all back instead, Player_AcxGhost).
    /// - Below level 10 a death gives no vitae and drops nothing; deaths to monsters drop no items (all carried pyreals
    ///   still go on the corpse, Player_Death.GetNumCoinsDropped).
    /// - The corpse stays while its owner has vitae, then gives way to a "Here lies" headstone (Corpse_AcxVitae).
    /// </summary>
    partial class Player
    {
        public const int AcxDeathMinLevel = 10;
        private const double AcxVitaeDecaySeconds = 300;
        private static readonly TimeSpan AcxHeadstoneInterval = TimeSpan.FromHours(1);

        private double acxVitaeNextDecay;

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

        /// <summary>
        /// Called from Heartbeat: vitae wears off 1% every 5 minutes
        /// </summary>
        public void AcxVitaeTick(double now)
        {
            if (now < acxVitaeNextDecay)
                return;
            acxVitaeNextDecay = now + AcxVitaeDecaySeconds;

            if (!HasVitae)
            {
                RemoveProperty(PropertyFloat.AcxVitaeDecayTimestamp);
                return;
            }

            var last = GetProperty(PropertyFloat.AcxVitaeDecayTimestamp);
            if (last == null)
            {
                SetProperty(PropertyFloat.AcxVitaeDecayTimestamp, now);
                return;
            }

            var points = (int)Math.Floor((now - last.Value) / AcxVitaeDecaySeconds);
            if (points <= 0)
                return;

            SetProperty(PropertyFloat.AcxVitaeDecayTimestamp, last.Value + points * AcxVitaeDecaySeconds);
            AcxReduceVitae(points, "Time heals");
        }

        /// <summary>
        /// Called from PlayerEnterWorld: catch up on vitae that wore off while offline (a few seconds after login)
        /// </summary>
        public void AcxVitaeOnLogin()
        {
            acxVitaeNextDecay = 0;
            var chain = new ActionChain();
            chain.AddDelaySeconds(10);
            chain.AddAction(this, () => AcxVitaeTick(Time.GetUnixTime()));
            chain.EnqueueChain();
        }

        /// <summary>
        /// Called from Die once the corpse exists: marks what the corpse remembers about this death
        /// </summary>
        public void AcxPrepareCorpse(DamageHistoryInfo lastDamager, DamageHistoryInfo topDamager, float vitaeBefore)
        {
            var corpse = AcxLastCorpse;
            if (corpse == null || corpse.IsDestroyed || (Level ?? 1) < AcxDeathMinLevel || corpse.IsOnNoDropLandblock
                || IsPKDeath(topDamager) || IsPKLiteDeath(topDamager))
                return;

            var points = (int)Math.Round((vitaeBefore - AcxVitaeValue()) * 100);
            if (points > 0)
                corpse.SetProperty(PropertyInt.AcxCorpseVitae, points);

            if (!HasVitae)
                return;

            if (GetProperty(PropertyFloat.AcxVitaeDecayTimestamp) == null)
                SetProperty(PropertyFloat.AcxVitaeDecayTimestamp, Time.GetUnixTime());

            // the corpse stays while its owner has vitae (Corpse_AcxVitae)
            corpse.SetProperty(PropertyBool.AcxVitaeCorpse, true);

            // a monster kill (not a suicide or a player) marks the corpse for a headstone, at most once an hour
            var killer = lastDamager?.TryGetAttacker();
            var lastHeadstone = GetProperty(PropertyFloat.AcxLastHeadstone) ?? 0;
            if (killer is Creature && killer is not Player && killer != this && Time.GetUnixTime() - lastHeadstone >= AcxHeadstoneInterval.TotalSeconds)
            {
                corpse.SetProperty(PropertyBool.AcxHeadstoneCorpse, true);
                SetProperty(PropertyFloat.AcxLastHeadstone, Time.GetUnixTime());
            }

            // kept across restarts even when empty: the corpse holds the vitae
            corpse.SaveBiotaToDatabase();
        }
    }
}
