using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: every character has the trade skills trained, free (JJ 2026-11-16: "I want all trade skills like alchemy
    /// and cooking and tinkering to be trained when a new player logs in the first time"), except Alchemy since it became
    /// the Alchemist's fighting skill, and Fletching (2026-11-17, JJ on finding it free: "that needs to be fixed"): an
    /// archer's Prismatic arrows need it (base 250, specialized for Deadly Prismatic), so free it was a free archer perk.
    /// Salvaging already is (retail).
    /// They are in AlwaysTrained, so they can't be untrained for skill credits; lowering one only returns its XP.
    /// </summary>
    partial class Player
    {
        public static readonly Skill[] AcxTradeSkills =
        {
            // not Alchemy: the Alchemist's fighting skill, so it costs credits like any weapon skill (JJ 2026-11-16: "yes take it
            // off free"); nor Fletching (2026-11-17); characters granted either before keep it
            Skill.Cooking,
            Skill.ArmorTinkering, Skill.WeaponTinkering, Skill.ItemTinkering, Skill.MagicItemTinkering
        };

        /// <summary>
        /// Once per character, at its first login after this change (a new character's first login): trains each trade skill
        /// it hasn't trained, for no credits, and returns the credits it spent training any it had (at creation, or at a
        /// temple before this change), so nobody pays for what others get free. A specialization stays, and so does what it
        /// cost above training. Before the player description is sent, so the client starts with the right skills.
        /// </summary>
        private void AcxGrantTradeSkills()
        {
            if ((GetProperty(PropertyBool.AcxTradeSkillsGranted) ?? false) || IsOlthoiPlayer)
                return;

            DatManager.PortalDat.CharGen.HeritageGroups.TryGetValue((uint)(HeritageGroup), out var heritage);

            foreach (var skill in AcxTradeSkills)
            {
                var creatureSkill = GetCreatureSkill(skill);

                if (creatureSkill.AdvancementClass < SkillAdvancementClass.Trained)
                {
                    TrainSkill(skill, 0);
                    continue;
                }

                // already trained: refund what training it cost, at the price character creation charges (the heritage's
                // own cost where it has one)
                if (!DatManager.PortalDat.SkillTable.SkillBaseHash.TryGetValue((uint)skill, out var skillBase))
                    continue;

                var trainedCost = skillBase.TrainedCost;
                if (heritage != null)
                {
                    foreach (var skillGroup in heritage.Skills)
                    {
                        if (skillGroup.SkillNum == (int)skill)
                        {
                            trainedCost = skillGroup.NormalCost;
                            break;
                        }
                    }
                }
                AvailableSkillCredits += trainedCost;
            }

            SetProperty(PropertyBool.AcxTradeSkillsGranted, true);
        }
    }
}
