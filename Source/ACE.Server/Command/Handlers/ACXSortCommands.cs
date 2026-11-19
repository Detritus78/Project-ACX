using ACE.Entity.Enum;
using ACE.Server.Network;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: /sort (WorldObjects/Player_AcxSort.cs)
    /// </summary>
    public static class ACXSortCommands
    {
        [CommandHandler("sort", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Puts the items in each of your packs in order: money, weapons, armor, jewelry, gems and so on; or heaviest, most valuable, or strongest weapons first. Nothing changes packs.",
            "[burden | value | damage]")]
        public static void HandleSort(Session session, params string[] parameters)
        {
            var mode = (parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "") switch
            {
                "" => Player.AcxSortMode.Type,
                "burden" or "weight" => Player.AcxSortMode.Burden,
                "value" => Player.AcxSortMode.Value,
                "damage" => Player.AcxSortMode.Damage,
                _ => (Player.AcxSortMode?)null,
            };
            if (mode == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Use /sort, /sort burden, /sort value or /sort damage.");
                return;
            }
            CommandHandlerHelper.WriteOutputInfo(session, session.Player.AcxSortPacks(mode.Value));
        }
    }
}
