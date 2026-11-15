using ACE.Entity.Enum;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: /OfflineSwear (WorldObjects/Player_AcxAllegiance.cs)
    /// </summary>
    public static class ACXWorldCommands
    {
        [CommandHandler("offlineswear", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1,
            "Swear allegiance to one of your own characters that's offline.", "<patron name>")]
        public static void HandleOfflineSwear(Session session, params string[] parameters)
        {
            var result = session.Player.AcxOfflineSwear(string.Join(" ", parameters));
            if (result != null)
                CommandHandlerHelper.WriteOutputInfo(session, result);
        }
    }
}
