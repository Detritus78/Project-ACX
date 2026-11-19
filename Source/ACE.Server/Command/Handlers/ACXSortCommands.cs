using ACE.Entity.Enum;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: /sort (WorldObjects/Player_AcxSort.cs)
    /// </summary>
    public static class ACXSortCommands
    {
        [CommandHandler("sort", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Puts the items in each of your packs in order: money, weapons, armor, jewelry, gems and so on. Nothing changes packs.")]
        public static void HandleSort(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, session.Player.AcxSortPacks());
        }
    }
}
