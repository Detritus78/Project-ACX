using ACE.Entity.Enum;
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
    }
}
