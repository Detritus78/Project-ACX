using System.Linq;

using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>
        /// Project ACX: the trainers' fresh start (Managers/AcxTrainers.cs): every trained and specialized skill reset as a
        /// temple resets one (ResetSkill: specialized back to trained, then untrained where the skill can be; always-trained
        /// skills, which include ACX's free trade skills, keep their training and give back only their experience), with the
        /// credits and experience refunded. Quiet: one experience refund and no chat line per skill.
        /// </summary>
        public void AcxResetAllSkills()
        {
            long experience = 0;

            foreach (var creatureSkill in Skills.Values.ToList())
            {
                if (creatureSkill.AdvancementClass < SkillAdvancementClass.Trained)
                    continue;
                if (!DatManager.PortalDat.SkillTable.SkillBaseHash.TryGetValue((uint)creatureSkill.Skill, out var skillBase))
                    continue;

                IsSkillSpecializedViaAugmentation(creatureSkill.Skill, out var viaAugmentation);
                var untrainable = IsSkillUntrainable(creatureSkill.Skill);

                if (creatureSkill.AdvancementClass == SkillAdvancementClass.Specialized && !(viaAugmentation && !untrainable))
                {
                    creatureSkill.AdvancementClass = SkillAdvancementClass.Trained;
                    creatureSkill.InitLevel = 0;
                    if (!viaAugmentation)
                        AvailableSkillCredits += skillBase.UpgradeCostFromTrainedToSpecialized;
                }

                if (untrainable)
                {
                    creatureSkill.AdvancementClass = SkillAdvancementClass.Untrained;
                    creatureSkill.InitLevel = 0;
                    AvailableSkillCredits += skillBase.TrainedCost;
                }

                experience += (long)creatureSkill.ExperienceSpent;
                creatureSkill.ExperienceSpent = 0;
                creatureSkill.Ranks = 0;

                Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(this, creatureSkill));
            }

            if (experience > 0)
                RefundXP(experience);

            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.AvailableSkillCredits, AvailableSkillCredits ?? 0));
        }
    }
}
