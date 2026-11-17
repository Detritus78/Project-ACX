using System;
using System.Numerics;

using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: weapon-style identities
    /// </summary>
    partial class Player
    {
        /// <summary>
        /// A one-handed melee weapon with nothing in the off-hand (no shield, no second weapon)
        /// </summary>
        public bool IsAcxSingleWeaponStyle()
        {
            var weapon = GetEquippedMeleeWeapon(true);

            return weapon != null && !weapon.IsTwoHanded && GetEquippedOffHand() == null;
        }

        /// <summary>
        /// Power bar refill time multiplier for the single weapon style (dual wield uses 0.8)
        /// </summary>
        public float GetAcxSingleWeaponRefillMod()
        {
            if (!IsAcxSingleWeaponStyle())
                return 1.0f;

            return 1.0f - (float)PropertyManager.GetDouble("acx_single_weapon_speed_bonus").Item;
        }

        /// <summary>
        /// Swing animation speed multiplier for the single weapon style (dual wield uses 1.2)
        /// </summary>
        public float GetAcxSingleWeaponAnimSpeedMod()
        {
            if (!IsAcxSingleWeaponStyle())
                return 1.0f;

            return 1.0f + (float)PropertyManager.GetDouble("acx_single_weapon_speed_bonus").Item;
        }

        /// <summary>
        /// Project ACX: dual wield's follow-up (JJ 2026-11-16: "dual wield should technically logically kill faster than one
        /// weapon?", "Lets test the double hit"). On every dual-wield attack the other hand's weapon also strikes the target
        /// for acx_dual_wield_follow_up of a normal hit (0 = off), with its own damage type, crits (Biting Strike, Crushing Blow, Critical Strike, Crippling Blow), rend, armor cleaving and spells, so carrying two
        /// damage types pays off. Once per attack (not per strike of a multi-strike weapon); no target procs, no cleave.
        /// </summary>
        public DamageEvent AcxDualWieldFollowUp(Creature target, WorldObject swinging)
        {
            var share = PropertyManager.GetDouble("acx_dual_wield_follow_up").Item;
            if (share <= 0 || !IsDualWieldAttack || target == null || !target.IsAlive)
                return null;

            // the other hand swings for this hit: the damage roll (DamageEvent: crit chance, crit damage, rend, armor
            // cleaving, damage type) reads the swinging hand's weapon, not the weapon passed in
            DualWieldAlternate = !DualWieldAlternate;
            try
            {
                var other = GetEquippedMeleeWeapon();
                if (other == null || other == swinging)
                    return null;

                return DamageTarget(target, other, (float)share);
            }
            finally
            {
                DualWieldAlternate = !DualWieldAlternate;
            }
        }

        /// <summary>
        /// Melee and missile defense with the single weapon style's bonus
        /// </summary>
        public uint ApplyAcxSingleWeaponDefense(uint defenseSkill)
        {
            if (!IsAcxSingleWeaponStyle())
                return defenseSkill;

            return (uint)Math.Round(defenseSkill * (1.0 + PropertyManager.GetDouble("acx_single_weapon_defense_bonus").Item));
        }

        /// <summary>
        /// Damage multiplier for the extra targets of a cleave or a two-handed spear's pierce (the main target takes the full hit)
        /// </summary>
        public static float GetAcxExtraTargetDamageMod()
        {
            return (float)PropertyManager.GetDouble("acx_extra_target_damage").Item;
        }

        /// <summary>
        /// How far behind the target a spear thrust reaches, and how far to either side of the line it still hits
        /// </summary>
        private const float AcxPierceRange = 3.0f;
        private const float AcxPierceWidth = 1.0f;

        /// <summary>
        /// Two-handed spears also hit the closest monster standing directly behind the target.
        /// They are the thrust-only two-handers, which have no cleave; two-handed swords, axes and maces keep cleaving.
        /// </summary>
        public static bool IsAcxPiercingWeapon(WorldObject weapon)
        {
            return weapon != null && weapon.IsTwoHanded && !weapon.IsCleaving
                && weapon.W_AttackType.HasFlag(AttackType.Thrust) && !weapon.W_AttackType.HasFlag(AttackType.Slash);
        }

        public Creature GetAcxPierceTarget(Creature target, WorldObject weapon)
        {
            if (target == null || !IsAcxPiercingWeapon(weapon))
                return null;

            var targetPos = target.Location.ToGlobal();
            var direction = targetPos - Location.ToGlobal();
            direction.Z = 0;

            if (direction.LengthSquared() < 0.0001f)
                return null;

            direction = Vector3.Normalize(direction);

            Creature pierceTarget = null;
            var pierceDistance = float.MaxValue;

            foreach (var obj in PhysicsObj.ObjMaint.GetVisibleObjectsValuesWhere(o => o.WeenieObj.WorldObject is Creature))
            {
                var creature = (Creature)obj.WeenieObj.WorldObject;

                if (creature == target || creature == this || creature.IsDead || creature.Teleporting)
                    continue;

                if (!creature.Attackable && creature.TargetingTactic == TargetingTactic.None)
                    continue;

                if (creature is CombatPet || CheckPKStatusVsTarget(creature, null) != null)
                    continue;

                var offset = creature.Location.ToGlobal() - targetPos;

                if (Math.Abs(offset.Z) > AcxPierceRange)
                    continue;

                offset.Z = 0;

                var behind = Vector3.Dot(offset, direction);
                if (behind <= 0 || behind > AcxPierceRange)
                    continue;

                var sideways = (offset - direction * behind).Length();
                if (sideways > AcxPierceWidth)
                    continue;

                if (behind < pierceDistance)
                {
                    pierceTarget = creature;
                    pierceDistance = behind;
                }
            }

            return pierceTarget;
        }
    }
}
