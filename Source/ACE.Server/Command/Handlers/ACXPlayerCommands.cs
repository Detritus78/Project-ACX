using ACE.Entity.Enum;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX player commands
    /// </summary>
    public static class ACXPlayerCommands
    {
        // ClassicACE's /fixinvisible, shortened
        [CommandHandler("fi", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Resends all nearby items and creatures, for when things have gone invisible")]
        public static void HandleFixInvisible(Session session, params string[] parameters)
        {
            foreach (var worldObject in session.Player.GetKnownObjects())
                session.Player.TrackObject(worldObject, true);
        }
    }
}
