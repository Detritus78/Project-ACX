using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: Alchemy heal-over-time (JJ 2026-11-11, ported from bDekaru's ClassicACE Healer.TryProcAlchemyHoT).
    /// When a healer with trained Alchemy heals with a kit, an Alchemy skill check against the heal's difficulty can add
    /// a heal over time of 4x the heal (ticking every 2.5 s, Creature_AcxDoTHoT). At most one try per kit every 2 minutes.
    /// </summary>
    partial class Healer
    {
        private double acxNextAlchemyTry;
        private const double AcxAlchemyTryInterval = 120;

        private void AcxTryAlchemyHoT(Player healer, Player target, int difficulty, int healAmount, PropertyAttribute2nd vital)
        {
            if (target.IsDead || healAmount <= 0)
                return;

            var now = Time.GetUnixTime();
            if (acxNextAlchemyTry > now)
                return;

            var skill = healer.GetCreatureSkill(Skill.Alchemy);
            if (skill.AdvancementClass < SkillAdvancementClass.Trained)
                return;

            acxNextAlchemyTry = now + AcxAlchemyTryInterval;

            if (ThreadSafeRandom.Next(0.0f, 1.0f) >= SkillCheck.GetSkillChance((int)skill.Current, difficulty))
                return;

            var vitalType = vital switch
            {
                PropertyAttribute2nd.MaxHealth => DamageType.Health,
                PropertyAttribute2nd.MaxStamina => DamageType.Stamina,
                PropertyAttribute2nd.MaxMana => DamageType.Mana,
                _ => DamageType.Undef,
            };
            if (vitalType == DamageType.Undef)
                return;

            target.AcxApplyHoT((int)(healAmount / 2.5), healAmount * 4, vitalType, healer);
            Proficiency.OnSuccessUse(healer, skill, (uint)difficulty);
        }
    }
}
