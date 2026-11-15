using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: leaderboards (Managers/AcxLeaderboards.cs)
    /// </summary>
    public static class ACXLeaderboardCommands
    {
        [CommandHandler("top", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Leaderboards: the top 3 on every board, or the top 10 for wealth, jobs, deaths, monsters (the deadliest) or explored.",
            "[wealth | jobs | deaths | monsters | explored]")]
        public static void HandleTop(Session session, params string[] parameters)
        {
            if (parameters.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, AcxLeaderboards.FormatAllBoards(3) + "\nThe full top 10: /top wealth | jobs | deaths | monsters | explored");
                return;
            }
            if (!AcxLeaderboards.TryParseBoard(parameters[0].ToLowerInvariant(), out var board))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: /top wealth | jobs | deaths | monsters | explored");
                return;
            }
            CommandHandlerHelper.WriteOutputInfo(session, AcxLeaderboards.FormatBoard(board));
        }

        [CommandHandler("explored", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "How much of Dereth you've explored.")]
        public static void HandleExplored(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, AcxLeaderboards.ExploredSummary(session.Player));
        }
    }
}
