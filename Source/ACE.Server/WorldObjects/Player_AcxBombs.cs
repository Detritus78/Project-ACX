using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: an Alchemist's bomb landing (AcxBombWcids; called by ProjectileCollisionHelper when the bomb reaches its
    /// target). JJ 2026-11-16: physical damage ("yes" to the explosion being physical: Missile Defense, armor and resistances
    /// count), "on the first throw the physical lands before the debuff. The second throw takes advantage of the debuff"
    /// (so the debuff goes on after every hit is in), and the debuffs resist on the thrower's Alchemy against Magic Defense,
    /// with no chat line per target (chat as spamless as possible).
    /// </summary>
    partial class Player
    {
        /// <summary>
        /// A damage bomb's blast, after its hit on its target (mainHit, already dealt): the nearest other monsters within
        /// acx_alchemist_splash_radius (AcxBombWcids.SplashTargets) take a share of a hit each (AcxBombWcids.SplashShare),
        /// rolling their own Missile Defense like a cleave target; then everything it hit takes the bomb's debuff
        /// </summary>
        public void AcxBombImpact(WorldObject projectile, Creature target, DamageEvent mainHit)
        {
            var bomb = projectile.ProjectileAmmo ?? projectile;

            // the blast's look: a burst on the target (the bomb's own hit already showed blood or the evade)
            target.EnqueueBroadcast(new GameMessageScript(target.Guid, PlayScript.Explode));

            var hit = new List<Creature>();
            if (mainHit != null && mainHit.HasDamage && !target.IsDead)
                hit.Add(target);

            var others = AcxBombWcids.SplashTargets(AcxBombBurst(target, PropertyManager.GetDouble("acx_alchemist_splash_radius").Item)
                .Where(AcxBombCanHarm).OrderBy(c => target.GetCylinderDistance(c)));
            var share = AcxBombWcids.SplashShare;
            if (share > 0)
            {
                foreach (var other in others)
                {
                    var damageEvent = DamageTarget(other, projectile, share);
                    if (damageEvent != null && damageEvent.HasDamage && !other.IsDead)
                        hit.Add(other);
                }
            }

            var debuff = AcxBombWcids.GetDebuffSpell(bomb);
            if (debuff != null)
                foreach (var creature in hit)
                    AcxBombDebuff(creature, debuff.Value, bomb);
        }

        /// <summary>
        /// A Healing bomb's burst: players and pets within acx_alchemist_heal_radius of the impact heal over time (a heal per
        /// point of Alchemy, refreshed by the next bomb rather than stacked); monsters there fester (resisted by Magic Defense)
        /// </summary>
        public void AcxHealingBombImpact(WorldObject projectile, Creature target)
        {
            var bomb = projectile.ProjectileAmmo ?? projectile;
            var radius = PropertyManager.GetDouble("acx_alchemist_heal_radius").Item;
            var total = (int)Math.Round(GetCreatureSkill(Skill.Alchemy).Current * PropertyManager.GetDouble("acx_alchemist_heal_per_skill").Item);

            var burst = AcxBombBurst(target, radius);
            burst.Add(target);
            if (target.GetCylinderDistance(this) <= radius)
                burst.Add(this);

            foreach (var creature in burst)
            {
                if (creature.IsDead)
                    continue;
                if (creature is Player || creature is CombatPet)
                {
                    creature.AcxRefreshBombHoT(total, this);
                    creature.EnqueueBroadcast(new GameMessageScript(creature.Guid, PlayScript.HealthUpRed));
                }
                else if (AcxBombCanHarm(creature))
                    AcxBombDebuff(creature, AcxBombWcids.FesterSpell, bomb);
            }
        }

        /// <summary>
        /// The creatures within radius of the impact (cylinder distance, as cleave measures), not counting the target
        /// </summary>
        private List<Creature> AcxBombBurst(Creature target, double radius)
        {
            var found = new List<Creature>();
            if (PhysicsObj == null || target.PhysicsObj == null)
                return found;

            foreach (var obj in PhysicsObj.ObjMaint.GetVisibleObjectsValuesWhere(o => o.WeenieObj.WorldObject is Creature))
            {
                var creature = (Creature)obj.WeenieObj.WorldObject;
                if (creature == target || creature == this || creature.Teleporting || creature.IsDead)
                    continue;
                if (target.GetCylinderDistance(creature) <= radius)
                    found.Add(creature);
            }
            return found;
        }

        /// <summary>
        /// Whether a bomb may hurt or debuff this creature: what a cleave may hit (Creature.GetCleaveTarget's rules)
        /// </summary>
        private bool AcxBombCanHarm(Creature creature)
        {
            if (creature.IsDead || creature.Teleporting || creature is CombatPet)
                return false;
            if (CheckPKStatusVsTarget(creature, null) != null)
                return false;
            return creature.Attackable || creature.TargetingTactic != TargetingTactic.None;
        }

        /// <summary>
        /// A bomb's debuff on one creature: none on what is immune to non-projectile magic, else resisted on the thrower's
        /// Alchemy against the creature's Magic Defense (WorldObject.MagicDefenseCheck, a war spell's resist), else on it as a
        /// spell from the thrower, refreshing the thrower's last one (EnchantmentManager: the stronger of a category wins).
        /// The spell's own effect shows it land (show: false for the bench); no chat line. Returns whether it landed.
        /// </summary>
        public bool AcxBombDebuff(Creature target, uint spellId, WorldObject bomb, bool show = true)
        {
            if (target.IsDead || target.Invincible || target.NonProjectileMagicImmune)
                return false;

            var spell = new Spell(spellId);
            if (spell.NotFound)
                return false;

            if (MagicDefenseCheck(GetCreatureSkill(Skill.Alchemy).Current, target.GetEffectiveMagicDefense(), out _))
                return false;

            target.EnchantmentManager.Add(spell, this, bomb);

            if (show && spell.TargetEffect != 0)
                target.EnqueueBroadcast(new GameMessageScript(target.Guid, spell.TargetEffect, spell.Formula.Scale));
            return true;
        }

        /// <summary>
        /// How fast this player throws, on top of Quickness (Player_Missile's aim and reload animations): an Alchemist's bomb by
        /// acx_alchemist_throw_speed, and any thrown weapon (a bomb too) by acx_thrown_shield_speed with a shield in the other
        /// hand (JJ 2026-11-16: "slow the throws with shields for the throwing class"). A sword gives up its single-weapon
        /// speed for a shield (about 13% of its damage); a thrown weapon had nothing to give up, so the shield cost it nothing
        /// and made it the hardest class to kill at the end game (/acxsurvival shield=1: 62-67% of fights won without, 85-90% with)
        /// </summary>
        public float AcxThrowSpeedMod(WorldObject ammo)
        {
            if (ammo == null)
                return 1.0f;
            var speed = AcxBombWcids.IsBomb(ammo.WeenieClassId) ? (float)PropertyManager.GetDouble("acx_alchemist_throw_speed").Item : 1.0f;
            if (ammo.WeenieType == WeenieType.Missile && GetEquippedShield() != null)   // a thrown weapon is its own ammunition (arrows and darts are Ammunition)
                speed *= (float)PropertyManager.GetDouble("acx_thrown_shield_speed").Item;
            return speed;
        }
    }
}
