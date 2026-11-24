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
        // Project ACX: the Entrance Tester's progress (Managers/AcxEntranceTest.cs; a Preview test tool)
        [CommandHandler("acxentrance", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0,
            "Entrance Tester: how many contract entrances are tried, make entrance n the next one, or report the last one.", "[n | bad <what's wrong>]")]
        public static void HandleAcxEntrance(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, AcxEntranceTest.Command(session.Player, string.Join(" ", parameters)));
        }

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

            CommandHandlerHelper.WriteOutputInfo(session, "Usage: /contract (yours), /contract drop <number>, /sc [number] shares one with your fellowship. Talk to a Town Crier to fill your free contract slots.");
        }

        // Project ACX: "share contract" (JJ asked for /cs, but AC:Unreal keeps /cs for Society chat, so it never reaches the server)
        [CommandHandler("sc", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Share a contract with your fellowship (the number from /contract's list, the top one if left out): each fellow gets a yes/no popup, and taking it uses one of their contracts for the day.",
            "[number]")]
        public static void HandleShareContract(Session session, params string[] parameters)
        {
            AcxContracts.Share(session.Player, parameters.Length > 0 && int.TryParse(parameters[0], out var n) ? n : 1);
        }
    }
}
