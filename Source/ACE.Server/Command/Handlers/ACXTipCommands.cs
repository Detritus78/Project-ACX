using ACE.Entity.Enum;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: /tip (WorldObjects/Player_AcxTip.cs)
    /// </summary>
    public static class ACXTipCommands
    {
        [CommandHandler("tip", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Tips the cow you have selected until you've tipped enough for Dwennon. /tip again to stop; /tip count shows how many.", "[count]")]
        public static void HandleTip(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, session.Player.AcxTip(string.Join(" ", parameters)));
        }
    }
}
