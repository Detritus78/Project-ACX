using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: /invasion and /acxinvasion (Managers/AcxInvasions.cs)
    /// </summary>
    public static class ACXInvasionCommands
    {
        [CommandHandler("invasion", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Shows the elite town invasion under way, if any.")]
        public static void HandleInvasion(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, AcxInvasions.Status(false));
        }

        [CommandHandler("acxinvasion", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Elite town invasions: status, start one (a town or random; warning minutes, 0 = attack now), stop the current one, kill its invader (to test the corpse), set when the next is rolled, list the towns, or reload towns.json.",
            "[start [town] [warning minutes] | stop | kill | next <minutes> | list | reload]")]
        public static void HandleAcxInvasion(Session session, params string[] parameters)
        {
            string result;
            if (parameters.Length == 0)
                result = AcxInvasions.Status(true);
            else if (parameters[0] == "start")
            {
                // town names have spaces ("Fort Tethana"); a trailing number is the warning
                var words = parameters[1..];
                long? warning = null;
                if (words.Length > 0 && long.TryParse(words[^1], out var minutes))
                {
                    warning = minutes;
                    words = words[..^1];
                }
                result = AcxInvasions.AdminStart(string.Join(' ', words), warning);
            }
            else if (parameters[0] == "stop")
                result = AcxInvasions.AdminStop();
            else if (parameters[0] == "kill")
                result = AcxInvasions.AdminKill();
            else if (parameters[0] == "next" && parameters.Length > 1 && int.TryParse(parameters[1], out var next))
                result = AcxInvasions.AdminNext(next);
            else if (parameters[0] == "list")
                result = AcxInvasions.List();
            else if (parameters[0] == "reload")
                AcxInvasions.Reload(out result);
            else
                result = "Usage: /acxinvasion [start [town] [warning minutes] | stop | kill | next <minutes> | list | reload]";
            CommandHandlerHelper.WriteOutputInfo(session, result);
        }
    }
}
