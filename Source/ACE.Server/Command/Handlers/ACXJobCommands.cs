using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: town job boards (Managers/AcxJobBoard.cs)
    /// </summary>
    public static class ACXJobCommands
    {
        [CommandHandler("job", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Town jobs: list the jobs you've taken, or take or drop one while standing by a job board or Job Runner.",
            "(nothing: your jobs) | take <number> | drop <number>")]
        public static void HandleJob(Session session, params string[] parameters)
        {
            var player = session.Player;
            var action = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "";

            if (action == "")
            {
                CommandHandlerHelper.WriteOutputInfo(session, AcxJobBoard.ListJobs(player));
                return;
            }

            if ((action == "take" || action == "drop") && parameters.Length > 1 && int.TryParse(parameters[1], out var number))
            {
                CommandHandlerHelper.WriteOutputInfo(session, action == "take" ? AcxJobBoard.TakeJob(player, number) : AcxJobBoard.DropJob(player, number));
                return;
            }

            CommandHandlerHelper.WriteOutputInfo(session, "Usage: /job, /job take <number>, /job drop <number>");
        }

        [CommandHandler("acxjobreload", AccessLevel.Developer, CommandHandlerFlag.None, 0,
            "Reloads the town job boards from Content/jobs/jobs.json.")]
        public static void HandleJobReload(Session session, params string[] parameters)
        {
            AcxJobBoard.Reload(out var message);
            CommandHandlerHelper.WriteOutputInfo(session, message);
        }
    }
}
