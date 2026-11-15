using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: the XP-per-hour tracker (WorldObjects/Player_AcxXpTracker.cs)
    /// </summary>
    public static class ACXXpTrackerCommands
    {
        [CommandHandler("xph", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Your experience per hour this session: by source, kills, fighting time, hits and how much is outdoors.",
            "(nothing: show the numbers) | reset: measure again from now | log: write them to the server's Logs/acx-xph.csv\n"
            + "Admins: /xph <player name> shows an online player's numbers.")]
        public static void HandleXph(Session session, params string[] parameters)
        {
            var player = session.Player;
            var action = string.Join(" ", parameters).Trim();

            switch (action.ToLowerInvariant())
            {
                case "":
                    break;

                case "reset":
                    player.AcxXphStart(true);
                    CommandHandlerHelper.WriteOutputInfo(session, "XP per hour: measuring again from now.");
                    return;

                case "log":
                    CommandHandlerHelper.WriteOutputInfo(session, player.AcxXphWriteLog("command")
                        ? "XP per hour: written to the server's Logs/acx-xph.csv."
                        : "XP per hour: couldn't write the log (see the server log).");
                    break;

                default:
                    if (session.AccessLevel < AccessLevel.Admin)
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, "Usage: /xph | /xph reset | /xph log");
                        return;
                    }

                    player = PlayerManager.GetOnlinePlayer(action);
                    if (player == null)
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"No player named {action} is online.");
                        return;
                    }
                    break;
            }

            CommandHandlerHelper.WriteOutputInfo(session, player.AcxXphReport());
        }
    }
}
