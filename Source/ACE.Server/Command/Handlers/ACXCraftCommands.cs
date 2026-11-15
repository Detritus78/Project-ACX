using ACE.Entity.Enum;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: crafting guarantee scrolls (WorldObjects/Player_AcxCraftGuarantee.cs)
    /// </summary>
    public static class ACXCraftCommands
    {
        [CommandHandler("guarantees", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "How long your crafting guarantee scrolls have left.")]
        public static void HandleGuarantees(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, session.Player.AcxGuaranteesText());
        }
    }
}
