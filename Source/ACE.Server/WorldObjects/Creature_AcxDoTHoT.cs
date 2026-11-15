using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: the bleed and heal-over-time engine (JJ 2026-11-11, ported from bDekaru's ClassicACE,
    /// Creature_DoT_HoT.cs). Damage and heals over time that aren't spells: each entry ticks every 2.5 s (a fifth of its
    /// total, so a bleed runs about 12.5 s), entries stack, and a player's carry over through logout.
    /// Uses:
    /// - Bleeds: a hit with a dagger, or a sword swung with a multi-strike attack, bleeds for its damage plus 5 over the
    ///   next ticks, scaled by acx_bleed_damage_scale (0.5 by default, Pourkarutide's live value; ClassicACE's is 1.0).
    ///   One hit in 10 instead cashes in that attacker's bleeds of that type at once: a hemorrhage.
    /// - Alchemy: a trained alchemist's healing kit can add a heal over time worth 4x the heal (Healer_AcxAlchemy).
    /// </summary>
    partial class Creature
    {
        public struct AcxDoT
        {
            public int TickAmount, TotalAmount;
            public CombatType CombatType;
            public DamageType DamageType;
            public WorldObject Source;

            public AcxDoT(int tick, int total, CombatType combatType, DamageType damageType, WorldObject source)
            {
                TickAmount = tick; TotalAmount = total; CombatType = combatType; DamageType = damageType; Source = source;
            }
        }

        public struct AcxHoT
        {
            public int TickAmount, TotalAmount;
            public DamageType VitalType;
            public WorldObject Source;

            public AcxHoT(int tick, int total, DamageType vitalType, WorldObject source)
            {
                TickAmount = tick; TotalAmount = total; VitalType = vitalType; Source = source;
            }
        }

        private readonly object acxDoTHoTLock = new();
        private List<AcxDoT> acxDoTs = new();
        private List<AcxHoT> acxHoTs = new();
        private double acxDoTHoTNextTick;
        private const double AcxDoTHoTTickInterval = 2.5;

        public void AcxClearDoTsAndHoTs()
        {
            lock (acxDoTHoTLock)
            {
                acxDoTs.Clear();
                acxHoTs.Clear();
            }
        }

        public void AcxApplyHoT(int tickAmount, int totalAmount, DamageType vitalType, WorldObject source)
        {
            if (vitalType != DamageType.Health && vitalType != DamageType.Stamina && vitalType != DamageType.Mana || totalAmount <= 0)
                return;

            lock (acxDoTHoTLock)
                acxHoTs.Add(new AcxHoT(Math.Max(1, tickAmount), totalAmount, vitalType, source));

            var what = vitalType == DamageType.Health ? "healing" : $"{vitalType.GetName().ToLower()} gain";
            var sourcePlayer = source as Player;
            sourcePlayer?.SendMessage($"You infuse {(source == this ? "yourself" : Name)} with {totalAmount:N0} points of periodic {what}.", ChatMessageType.Magic);
            if (this is Player targetPlayer && targetPlayer != sourcePlayer && source != null)
                targetPlayer.SendMessage($"{source.Name} infuses you with {totalAmount:N0} points of periodic {what}.", ChatMessageType.Magic);
        }

        public void AcxApplyDoT(int tickAmount, int totalAmount, bool isCritical, CombatType combatType, DamageType damageType, WorldObject source)
        {
            if (source == null || IsDead || Invincible || totalAmount <= 0 || this is Player { IsAcxGhost: true })
                return;

            var sourcePlayer = source as Player;
            var targetPlayer = this as Player;

            if (targetPlayer != null && targetPlayer.UnderLifestoneProtection)
            {
                sourcePlayer?.SendMessage($"The Lifestone's magic protects {targetPlayer.Name} from the attack!", ChatMessageType.Magic);
                targetPlayer.HandleLifestoneProtection();
                return;
            }

            var resist = GetNegativeRatingMod(GetDotResistanceRating());
            tickAmount = Math.Max(1, (int)Math.Round(tickAmount * resist));
            totalAmount = (int)Math.Round(totalAmount * resist);

            lock (acxDoTHoTLock)
                acxDoTs.Add(new AcxDoT(tickAmount, totalAmount, combatType, damageType, source));

            if (targetPlayer != null && source is Creature sourceCreature)
                targetPlayer.SetCurrentAttacker(sourceCreature);
            if (sourcePlayer != null && targetPlayer != null)
                Player.UpdatePKTimers(sourcePlayer, targetPlayer);

            // no "your attack adds N periodic damage" line per hit (JJ 2026-11-13: chat as spamless as possible); the ticks show
        }

        /// <summary>
        /// Called from Player_Tick and Monster_Tick: every 2.5 s, one tick of each bleed and heal
        /// </summary>
        public void AcxDoTHoTTick(double now)
        {
            if (now < acxDoTHoTNextTick)
                return;
            acxDoTHoTNextTick = now + AcxDoTHoTTickInterval;

            List<AcxDoT> dots;
            List<AcxHoT> hots;
            lock (acxDoTHoTLock)
            {
                if (acxDoTs.Count == 0 && acxHoTs.Count == 0)
                    return;
                if (IsDead)
                {
                    acxDoTs.Clear();
                    acxHoTs.Clear();
                    return;
                }

                dots = acxDoTs;
                hots = acxHoTs;
                acxDoTs = dots.Where(d => d.TotalAmount > d.TickAmount).Select(d => new AcxDoT(d.TickAmount, d.TotalAmount - d.TickAmount, d.CombatType, d.DamageType, d.Source)).ToList();
                acxHoTs = hots.Where(h => h.TotalAmount > h.TickAmount).Select(h => new AcxHoT(h.TickAmount, h.TotalAmount - h.TickAmount, h.VitalType, h.Source)).ToList();
            }

            // damage, one hit per damage type, credited to each source; an invincible target's bleeds run out without
            // hurting it or crediting anyone (Dekaru 7879b0ba)
            foreach (var group in dots.GroupBy(d => d.DamageType).OrderByDescending(g => g.Sum(d => Math.Min(d.TickAmount, d.TotalAmount))))
            {
                if (IsDead || Invincible)
                    break;

                var amount = group.Sum(d => (float)Math.Min(d.TickAmount, d.TotalAmount));
                foreach (var d in group.Where(d => d.Source != null))
                    DamageHistory.Add(d.Source, group.Key, (uint)Math.Min(d.TickAmount, d.TotalAmount));

                TakeDamageOverTime(amount, group.Key);

                foreach (var bySource in group.Where(d => d.Source is Player p && p != this).GroupBy(d => (Player)d.Source))
                {
                    if (this is Player targetPlayer)
                        Player.UpdatePKTimers(bySource.Key, targetPlayer);
                    TakeDamageOverTime_NotifySource(bySource.Key, group.Key, bySource.Sum(d => (float)Math.Min(d.TickAmount, d.TotalAmount)));
                    if (!IsDead)
                        EmoteManager.OnDamage(bySource.Key);
                }
            }

            if (IsDead)
                return;

            // heals, one per vital, boosted by healing ratings
            var healingMod = GetHealingRatingMod();
            foreach (var group in hots.GroupBy(h => h.VitalType))
            {
                var amount = (int)Math.Round(group.Sum(h => Math.Min(h.TickAmount, h.TotalAmount)) * healingMod);
                var vital = group.Key == DamageType.Health ? Health : group.Key == DamageType.Stamina ? Stamina : Mana;
                var healed = UpdateVitalDelta(vital, amount);
                if (group.Key == DamageType.Health && healed > 0)
                    DamageHistory.OnHeal((uint)healed);
                // no chat line per heal tick (JJ 2026-11-13: chat as spamless as possible)
            }
        }

        /// <summary>
        /// Called after a melee attack lands: daggers and multi-strike sword attacks make the target bleed, and so may an
        /// attacker or weapon with an innate bleed chance (AcxAttacksCauseBleedChance, Dekaru's AttacksCauseBleedChance;
        /// nothing has one yet: a hook for bleeding monsters or elites)
        /// </summary>
        public void AcxApplyWeaponBleed(WorldObject source, WorldObject weapon, float baseDamage, DamageType damageType, bool isCritical)
        {
            if (source == null || baseDamage <= 0 || IsDead)
                return;

            var bleeds = weapon != null && (weapon.W_WeaponType == WeaponType.Dagger || (weapon.W_WeaponType == WeaponType.Sword && weapon.W_AttackType.IsMultiStrike()));
            if (!bleeds)
            {
                var chance = Math.Max(source.GetProperty(PropertyFloat.AcxAttacksCauseBleedChance) ?? 0, weapon?.GetProperty(PropertyFloat.AcxAttacksCauseBleedChance) ?? 0);
                if (chance <= 0 || chance <= ThreadSafeRandom.Next(0.0f, 1.0f))
                    return;
            }

            var total = 5 + (int)Math.Round(baseDamage);
            if (!(source is Player && this is Player))
                total = (int)Math.Round(total * PropertyManager.GetDouble("acx_bleed_damage_scale").Item);
            if (total <= 0)
                return;

            if (0.10 > ThreadSafeRandom.Next(0.0f, 1.0f) && AcxHemorrhage(source, CombatType.Melee, damageType))
                return;

            AcxApplyDoT(Math.Max(1, total / 5), total, isCritical, CombatType.Melee, damageType, source);
        }

        /// <summary>
        /// Cashes in an attacker's bleeds of one type as a single burst of damage
        /// </summary>
        private bool AcxHemorrhage(WorldObject source, CombatType combatType, DamageType damageType)
        {
            if (source == null || IsDead || Invincible || this is Player { IsAcxGhost: true })
                return false;

            List<AcxDoT> cashed;
            lock (acxDoTHoTLock)
            {
                cashed = acxDoTs.Where(d => d.Source == source && d.CombatType == combatType && d.DamageType == damageType).ToList();
                if (cashed.Count == 0)
                    return false;
                acxDoTs = acxDoTs.Where(d => d.Source != source || d.CombatType != combatType || d.DamageType != damageType).ToList();
            }

            var damage = cashed.Sum(d => (float)d.TotalAmount);
            var sourcePlayer = source as Player;

            if (this is Player targetPlayer)
            {
                if (source is Creature sourceCreature)
                    targetPlayer.SetCurrentAttacker(sourceCreature);
                if (sourcePlayer != null)
                    Player.UpdatePKTimers(sourcePlayer, targetPlayer);
                targetPlayer.TakeDamage(source, damageType, damage, BodyPart.Chest);
                targetPlayer.SendMessage($"Hemorrhage! {source.Name}'s wounds on you burst for {damage:N0} points of {damageType.GetName().ToLower()} damage.", ChatMessageType.CombatEnemy);
            }
            else
                TakeDamage(source, damageType, damage);

            EnqueueBroadcast(new GameMessageSound(Guid, Sound.HitFlesh1, 0.5f), new GameMessageScript(Guid, PlayScript.DirtyFightingDamageOverTime));
            sourcePlayer?.SendMessage($"Hemorrhage! {Name}'s wounds burst for {damage:N0} points of {damageType.GetName().ToLower()} damage!", ChatMessageType.CombatSelf);
            return true;
        }

        /// <summary>
        /// Called at logout: a player's bleeds and heals carry over (AcxDoTLog / AcxHoTLog)
        /// </summary>
        public void AcxSaveDoTsAndHoTs()
        {
            lock (acxDoTHoTLock)
            {
                if (acxDoTs.Count > 0)
                    SetProperty(PropertyString.AcxDoTLog, string.Join(",", acxDoTs.Select(d => $"{d.TickAmount}:{d.TotalAmount}:{(int)d.CombatType}:{(int)d.DamageType}")));
                else
                    RemoveProperty(PropertyString.AcxDoTLog);

                if (acxHoTs.Count > 0)
                    SetProperty(PropertyString.AcxHoTLog, string.Join(",", acxHoTs.Select(h => $"{h.TickAmount}:{h.TotalAmount}:{(int)h.VitalType}")));
                else
                    RemoveProperty(PropertyString.AcxHoTLog);
            }
        }

        /// <summary>
        /// Called at login: picks up the bleeds and heals saved at logout
        /// </summary>
        public void AcxRestoreDoTsAndHoTs()
        {
            lock (acxDoTHoTLock)
            {
                foreach (var entry in (GetProperty(PropertyString.AcxDoTLog) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = entry.Split(':');
                    if (p.Length == 4 && int.TryParse(p[0], out var tick) && int.TryParse(p[1], out var total) && int.TryParse(p[2], out var combat) && int.TryParse(p[3], out var type))
                        acxDoTs.Add(new AcxDoT(tick, total, (CombatType)combat, (DamageType)type, null));
                }
                foreach (var entry in (GetProperty(PropertyString.AcxHoTLog) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = entry.Split(':');
                    if (p.Length == 3 && int.TryParse(p[0], out var tick) && int.TryParse(p[1], out var total) && int.TryParse(p[2], out var vital))
                        acxHoTs.Add(new AcxHoT(tick, total, (DamageType)vital, null));
                }
                RemoveProperty(PropertyString.AcxDoTLog);
                RemoveProperty(PropertyString.AcxHoTLog);
            }
        }
    }
}
