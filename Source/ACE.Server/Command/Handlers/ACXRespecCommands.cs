using System.Collections.Generic;

using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: an admin skill reset for a logged-in character (JJ 2026-11-24, turning Einar the thrown monarch into Brokk
    /// the tinker: "to make this easier can you reset the skills on brokk?"). The trainers' fresh start without the trainer:
    /// every trained and specialized skill reset with its credits and experience refunded (Player.AcxResetAllSkills; the
    /// free trade skills stay trained). Attributes are left alone: the Concordia gems do those.
    /// </summary>
    public static class ACXRespecCommands
    {
        [CommandHandler("acxresetskills", AccessLevel.Admin, CommandHandlerFlag.None, 1,
            "Resets every trained and specialized skill on a logged-in character, refunding the credits and experience.",
            "<character name>")]
        public static void HandleResetSkills(Session session, params string[] parameters)
        {
            var name = string.Join(" ", parameters).Trim();
            var player = PlayerManager.GetOnlinePlayer(name);
            if (player == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{name} isn't logged in.");
                return;
            }

            player.AcxResetAllSkills();
            player.SendMessage("Your skills have been reset. Your skill credits and experience are back to spend.");
            CommandHandlerHelper.WriteOutputInfo(session, $"{player.Name}'s skills reset: {player.AvailableSkillCredits} credits, {player.AvailableExperience:N0} experience to spend.");
        }

        private static readonly Dictionary<string, (Skill skill, PropertyInt aug)> AugSkills = new Dictionary<string, (Skill, PropertyInt)>
        {
            ["weapon"] = (Skill.WeaponTinkering, PropertyInt.AugmentationSpecializeWeaponTinkering),
            ["armor"] = (Skill.ArmorTinkering, PropertyInt.AugmentationSpecializeArmorTinkering),
            ["item"] = (Skill.ItemTinkering, PropertyInt.AugmentationSpecializeItemTinkering),
            ["magicitem"] = (Skill.MagicItemTinkering, PropertyInt.AugmentationSpecializeMagicItemTinkering),
            ["salvaging"] = (Skill.Salvaging, PropertyInt.AugmentationSpecializeSalvaging),
        };

        /// <summary>
        /// Project ACX: specialize a tinkering skill or Salvaging for skill credits (JJ 2026-11-24: "Can we change it so we can
        /// use skill credits to specize it. If even temporarily? I still want to use these skill credits up."). Retail only
        /// specializes these with the 1-billion-XP Essences (the dat spec cost is 999, so the client offers no button). This
        /// does what an Essence does (sets its augmentation flag, so a later reset keeps the specialization and refunds no
        /// credits, exactly as for an Essence) and charges the skill's trained cost in credits instead of the XP.
        /// </summary>
        [CommandHandler("acxspecialize", AccessLevel.Admin, CommandHandlerFlag.None, 2,
            "Specializes a tinkering skill or Salvaging on a logged-in character for skill credits (the skill's trained cost), as its Essence would.",
            "<character name>, <weapon | armor | item | magicitem | salvaging>")]
        public static void HandleSpecialize(Session session, params string[] parameters)
        {
            var parts = string.Join(" ", parameters).Split(',');
            if (parts.Length != 2 || !AugSkills.TryGetValue(parts[1].Trim().ToLower().Replace(" ", ""), out var entry))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: acxspecialize <character name>, <weapon | armor | item | magicitem | salvaging>");
                return;
            }

            var player = PlayerManager.GetOnlinePlayer(parts[0].Trim());
            if (player == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{parts[0].Trim()} isn't logged in.");
                return;
            }

            var creatureSkill = player.GetCreatureSkill(entry.skill);
            if (creatureSkill.AdvancementClass != SkillAdvancementClass.Trained)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{player.Name}'s {entry.skill} is {creatureSkill.AdvancementClass}, not trained.");
                return;
            }

            var cost = DatManager.PortalDat.SkillTable.SkillBaseHash[(uint)entry.skill].TrainedCost;
            if ((player.AvailableSkillCredits ?? 0) < cost)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{player.Name} has {player.AvailableSkillCredits ?? 0} credits; {entry.skill} costs {cost}.");
                return;
            }

            player.SetProperty(entry.aug, 1);
            creatureSkill.AdvancementClass = SkillAdvancementClass.Specialized;
            creatureSkill.InitLevel = 10;
            creatureSkill.Ranks = (ushort)Player.CalcSkillRank(SkillAdvancementClass.Specialized, creatureSkill.ExperienceSpent);
            player.AvailableSkillCredits -= cost;

            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(player, creatureSkill));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0));
            player.SendMessage($"Your {entry.skill} is now specialized.");
            CommandHandlerHelper.WriteOutputInfo(session, $"{player.Name}'s {entry.skill} specialized for {cost} credits; {player.AvailableSkillCredits} left.");
        }
    }
}
