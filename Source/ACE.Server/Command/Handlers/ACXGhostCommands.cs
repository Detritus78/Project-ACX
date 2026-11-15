using ACE.Entity.Enum;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: waiting for a revive as a ghost (WorldObjects/Player_AcxGhost.cs)
    /// </summary>
    public static class ACXGhostCommands
    {
        [CommandHandler("release", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "While a ghost: stop waiting for a revive and return to your lifestone.")]
        public static void HandleRelease(Session session, params string[] parameters)
        {
            if (!session.Player.IsAcxGhost)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "You are not a ghost.");
                return;
            }
            session.Player.AcxReleaseGhost("You leave your body and return to your lifestone.");
        }

        [CommandHandler("ghost", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "While a ghost: what to do now, and how long you have left to be revived.")]
        public static void HandleGhost(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, session.Player.AcxGhostTimeLeft());
        }
    }
}
