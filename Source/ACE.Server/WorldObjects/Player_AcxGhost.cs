using System;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.WorldObjects.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Sequence;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: waiting for a revive as a ghost (JJ 2026-11-11, built from Pourkarutide's ghost code).
    /// From level 10, a player who dies (not to a player, not in an arena) stands back up beside their corpse as an
    /// untouchable, ethereal ghost for up to 5 minutes instead of going to the lifestone. A ghost can only move (within
    /// 5 m of the body, held by the server) and talk: using, looting, picking up, trading, portals, combat and casting
    /// are refused. A friend revives them with a revive scroll (Managers/AcxRevive.cs), which gives back this death's
    /// vitae. /release or /ls, the timer running out, a recall and logging out all send the ghost to the lifestone. Death
    /// says only "Welcome to the underworld"; /ghost explains the rest (JJ's wording, 2026-11-13).
    /// While a ghost, fellowship experience passes them by.
    /// </summary>
    partial class Player
    {
        public const int AcxGhostMinLevel = 10;
        public const double AcxGhostSeconds = 300;
        private const float AcxGhostLeash = 5.0f;
        private const float AcxGhostTranslucency = 0.5f;
        private const float AcxGhostSelfTranslucency = 0.65f;   // your own view, a bit clearer than others see (JJ 2026-11-13: ~30% more)

        /// <summary>
        /// The corpse made by this player's last death (set in Creature.CreateCorpse)
        /// </summary>
        public Corpse AcxLastCorpse;

        private Corpse acxGhostCorpse;
        private double acxGhostStart;
        private float acxGhostVitaeBefore;
        private DateTime acxGhostLeashMessage;

        /// <summary>
        /// The player reviving this ghost, if a revive cast is under way (one at a time)
        /// </summary>
        public Player AcxReviver;
        public double AcxReviveStarted;

        public bool IsAcxGhost => acxGhostCorpse != null;

        public Corpse AcxGhostCorpse => acxGhostCorpse;

        /// <summary>
        /// The vitae multiplier now (1 with no vitae), taken just before a death adds to it
        /// </summary>
        public float AcxVitaeValue() => HasVitae ? EnchantmentManager.GetVitae().StatModValue : 1.0f;

        /// <summary>
        /// Called from Die once the corpse exists: if this death qualifies, the player stands back up as a ghost at the
        /// corpse instead of going to the lifestone. Returns false for the usual death.
        /// </summary>
        public bool AcxBeginGhost(DamageHistoryInfo topDamager, float vitaeBefore)
        {
            var corpse = AcxLastCorpse;
            AcxLastCorpse = null;

            if (!PropertyManager.GetBool("acx_ghosts").Item || (Level ?? 1) < AcxGhostMinLevel || IsLoggingOut
                || corpse == null || corpse.IsDestroyed || corpse.Location == null || corpse.IsOnNoDropLandblock
                || IsPKDeath(topDamager) || IsPKLiteDeath(topDamager))
                return false;

            // keep the body at least as long as the wait (empty corpses normally go in 15 s)
            corpse.AcxHoldsGhost = true;
            if ((corpse.TimeToRot ?? 0) < AcxGhostSeconds + 60)
                corpse.TimeToRot = AcxGhostSeconds + 60;

            WorldManager.ThreadSafeTeleport(this, new Position(corpse.Location), new ActionEventDelegate(() =>
            {
                // stand back up, with the vitals a normal death leaves
                SetCombatMode(CombatMode.NonCombat);
                AcxSetVitals(0.75f, 0.75f, 0.75f);
                DamageHistory.Reset();
                OnHealthUpdate();
                IsInDeathProcess = false;

                acxGhostCorpse = corpse;
                acxGhostStart = Time.GetUnixTime();
                acxGhostVitaeBefore = vitaeBefore;
                AcxSetGhostState(true);

                // logged out on the way: the next login sends them to the lifestone (AcxGhostOnLogin)
                if (IsLoggingOut)
                {
                    LogOut_Final(true);
                    return;
                }

                PlayParticleEffect(PlayScript.VitaeDownBlack, Guid);
                Session.Network.EnqueueSend(new GameMessageSystemChat("Welcome to the underworld. Type /ghost for more information.", ChatMessageType.Broadcast));
            }));

            return true;
        }

        /// <summary>
        /// Ghost on or off: ethereal, untouchable, ignored by monsters, and see-through to others
        /// </summary>
        private void AcxSetGhostState(bool ghost)
        {
            if (ghost)
                SetProperty(PropertyBool.AcxGhost, true);
            else
                RemoveProperty(PropertyBool.AcxGhost);

            Ethereal = ghost;
            ReportCollisions = !ghost;
            Attackable = !ghost;
            // the player sees themselves more see-through than others see them
            Translucency = ghost ? AcxGhostSelfTranslucency : null;
            Session?.Network.EnqueueSend(new GameMessageUpdateObject(this));
            Translucency = ghost ? AcxGhostTranslucency : null;
            EnqueueBroadcastPhysicsState();
            EnqueueBroadcast(false, new GameMessageUpdateObject(this));
        }

        /// <summary>
        /// Ends the ghost wait without moving the player (revive, release, recall)
        /// </summary>
        private void AcxEndGhost()
        {
            var corpse = acxGhostCorpse;
            acxGhostCorpse = null;
            AcxReviver = null;
            AcxSetGhostState(false);

            // an empty body goes as it normally would
            if (corpse != null)
                corpse.AcxHoldsGhost = false;
            if (corpse != null && !corpse.IsDestroyed && corpse.Inventory.Count == 0)
                corpse.TimeToRot = Corpse.EmptyDecayTime;
        }

        /// <summary>
        /// The ghost goes to the lifestone (/release, /ls, timer)
        /// </summary>
        public void AcxReleaseGhost(string message)
        {
            if (!IsAcxGhost)
                return;

            AcxEndGhost();
            Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Broadcast));

            var lifestone = Sanctuary ?? Instantiation ?? Location;
            WorldManager.ThreadSafeTeleport(this, new Position(lifestone), new ActionEventDelegate(() =>
            {
                SetCombatMode(CombatMode.NonCombat);
                SetLifestoneProtection();
            }));
        }

        /// <summary>
        /// Called by a revive scroll when its cast completes: back to life at the body, with this death's vitae returned
        /// </summary>
        public void AcxRevivedBy(Player reviver, int tier)
        {
            if (!IsAcxGhost)
                return;

            AcxEndGhost();

            switch (tier)
            {
                case 1: AcxSetVitals(0, 0, 0); break;   // 1 health
                case 2: AcxSetVitals(0.5f, 0.5f, 0.5f); break;
                default: AcxSetVitals(1, 1, 1); break;
            }

            // give back the vitae this death cost
            if (HasVitae)
            {
                if (acxGhostVitaeBefore >= 1.0f)
                    EnchantmentManager.RemoveVitae();
                else
                {
                    EnchantmentManager.GetVitae().StatModValue = acxGhostVitaeBefore;
                    ChangesDetected = true;
                    if (EnchantmentManager is EnchantmentManagerWithCaching caching)
                        caching.ClearCache();
                    EnchantmentManager.SendUpdateVitae();
                }
            }

            PlayParticleEffect(PlayScript.VitaeUpWhite, Guid);
            PlayParticleEffect(PlayScript.HealthUpRed, Guid);
            Session.Network.EnqueueSend(new GameMessageSystemChat(reviver == this ? "You bring yourself back to life, and this death's vitae is lifted." : $"{reviver.Name} brings you back to life, and this death's vitae is lifted.", ChatMessageType.Broadcast));

            CheckMonsters();
        }

        /// <summary>
        /// Sets health, stamina and mana to a share of their maximum (health at least 1)
        /// </summary>
        private void AcxSetVitals(float health, float stamina, float mana)
        {
            var newHealth = Math.Max(1u, (uint)Math.Round(Health.MaxValue * health));
            var newStamina = (uint)Math.Round(Stamina.MaxValue * stamina);
            var newMana = (uint)Math.Round(Mana.MaxValue * mana);

            UpdateVital(Health, newHealth);
            UpdateVital(Stamina, newStamina);
            UpdateVital(Mana, newMana);

            Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute2ndLevel(this, Vital.Health, newHealth),
                new GameMessagePrivateUpdateAttribute2ndLevel(this, Vital.Stamina, newStamina),
                new GameMessagePrivateUpdateAttribute2ndLevel(this, Vital.Mana, newMana));
        }

        /// <summary>
        /// Called from Heartbeat: the wait runs out, or the body is gone
        /// </summary>
        public void AcxGhostTick()
        {
            if (!IsAcxGhost)
                return;

            // a reviver who vanished mid-cast (logged out) doesn't hold the ghost forever
            if (AcxReviver != null && Time.GetUnixTime() - AcxReviveStarted > 30)
                AcxReviver = null;

            if (acxGhostCorpse.IsDestroyed)
                AcxReleaseGhost("Your body is gone. You return to your lifestone.");
            else if (Time.GetUnixTime() - acxGhostStart >= AcxGhostSeconds && AcxReviver == null)
                AcxReleaseGhost("No one came. You return to your lifestone.");
        }

        public string AcxGhostTimeLeft()
        {
            if (!IsAcxGhost)
                return "You are not a ghost.";

            var left = Math.Max(0, AcxGhostSeconds - (Time.GetUnixTime() - acxGhostStart));
            return $"So you've had a bad day? Now what? Type /ls or /release to head back to your lifestone, or hang out for {(int)left / 60}:{(int)left % 60:00} in case a friend revives you with a revive scroll. Don't worry, your vitae will pass with time or XP. Just remember: when your vitae is gone, your body is gone, along with any pyreals still on it. However, your memory shall live on.";
        }

        /// <summary>
        /// Called from Teleport: a ghost that's moved by anything else (a recall, an admin) stops being a ghost there
        /// </summary>
        public void AcxGhostOnTeleport()
        {
            if (!IsAcxGhost)
                return;

            AcxEndGhost();
            Session.Network.EnqueueSend(new GameMessageSystemChat("You leave your body behind.", ChatMessageType.Broadcast));
        }

        /// <summary>
        /// Called from PlayerEnterWorld: someone who logged out (or was cut off) as a ghost wakes at their lifestone
        /// </summary>
        public void AcxGhostOnLogin()
        {
            if (!(GetProperty(PropertyBool.AcxGhost) ?? false))
                return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(3);
            actionChain.AddAction(this, () =>
            {
                AcxSetGhostState(false);
                Session.Network.EnqueueSend(new GameMessageSystemChat("You left while a ghost, so you wake at your lifestone.", ChatMessageType.Broadcast));
                WorldManager.ThreadSafeTeleport(this, new Position(Sanctuary ?? Instantiation ?? Location));
            });
            actionChain.EnqueueChain();
        }

        /// <summary>
        /// Called at the top of the actions a ghost can't do. Returns true (after telling them) if this player is a ghost.
        /// </summary>
        public bool AcxGhostBlocks(bool sendUseDone = false)
        {
            if (!IsAcxGhost)
                return false;

            SendTransientError("You're a ghost: you can only move and talk. /ls or /release returns you to your lifestone.");
            if (sendUseDone)
                SendUseDoneEvent();
            return true;
        }

        /// <summary>
        /// AcxGhostBlocks for item moves: also tells the client the move failed
        /// </summary>
        public bool AcxGhostBlocksItem(uint itemGuid)
        {
            if (!AcxGhostBlocks())
                return false;

            Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, itemGuid));
            return true;
        }

        /// <summary>
        /// Called from UpdatePlayerPosition: a ghost can't move more than 5 m from its body. Returns true when the move is
        /// refused, after putting the client back where it was.
        /// </summary>
        public bool AcxGhostLeashRefuses(Position newPosition)
        {
            if (!IsAcxGhost || acxGhostCorpse.Location == null)
                return false;

            if (acxGhostCorpse.Location.DistanceTo(newPosition) <= AcxGhostLeash)
                return false;

            Sequences.GetNextSequence(SequenceType.ObjectForcePosition);
            SendUpdatePosition();

            if (DateTime.UtcNow - acxGhostLeashMessage > TimeSpan.FromSeconds(3))
            {
                acxGhostLeashMessage = DateTime.UtcNow;
                SendTransientError("You can't stray far from your body.");
            }
            return true;
        }
    }
}
