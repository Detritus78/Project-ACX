using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: exploration contracts (Managers/AcxContracts.cs)
    /// </summary>
    public static class ACXContractCommands
    {
        [CommandHandler("contract", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Dungeon contracts matched to your level: list yours, or drop one. Talk to a Town Crier to fill your free contract slots.",
            "(nothing: yours) | drop <number>")]
        public static void HandleContract(Session session, params string[] parameters)
        {
            var player = session.Player;
            var action = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "";

            if (action == "")
            {
                CommandHandlerHelper.WriteOutputInfo(session, AcxContracts.List(player));
                return;
            }

            if (action == "drop" && parameters.Length > 1 && int.TryParse(parameters[1], out var number))
            {
                CommandHandlerHelper.WriteOutputInfo(session, AcxContracts.Drop(player, number));
                return;
            }

            CommandHandlerHelper.WriteOutputInfo(session, "Usage: /contract (yours), /contract drop <number>, /sc shares your top one with your fellowship. Talk to a Town Crier to fill your free contract slots.");
        }

        // Project ACX: "share contract" (JJ asked for /cs, but AC:Unreal keeps /cs for Society chat, so it never reaches the server)
        [CommandHandler("sc", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Share your top contract (first on /contract's list) with your fellowship: each fellow gets a yes/no popup, and taking it uses one of their contracts for the day.",
            "")]
        public static void HandleShareContract(Session session, params string[] parameters)
        {
            AcxContracts.Share(session.Player);
        }
    }
}
