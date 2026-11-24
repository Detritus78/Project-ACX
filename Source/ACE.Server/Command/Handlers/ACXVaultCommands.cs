using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: /vault and /acxvault (Managers/AcxVaults.cs): the Haven vaults
    /// </summary>
    public static class ACXVaultCommands
    {
        [CommandHandler("vault", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Shows your account's vaults, gives one up (like abandoning a house; what's inside stays for the next leaseholder), or empties the pack you have selected into the vault you have open.",
            "[abandon [name, e.g. 3-C] | dump]")]
        public static void HandleVault(Session session, params string[] parameters)
        {
            if (parameters.Length > 0 && parameters[0] == "dump")
                CommandHandlerHelper.WriteOutputInfo(session, session.Player.AcxVaultDump());   // Project ACX: empty the selected pack into the open vault
            else if (parameters.Length > 0 && parameters[0] == "abandon")
                AcxVaults.Abandon(session.Player, parameters.Length > 1 ? parameters[1] : null);
            else
                CommandHandlerHelper.WriteOutputInfo(session, AcxVaults.Status(session.Player));
        }

        [CommandHandler("acxvault", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Lists the vaults in Concordia, frees one (items stay inside), or sets its rent paid for days from now (negative = overdue).",
            "[release <1-A> | paid <1-A> <days>]")]
        public static void HandleAcxVault(Session session, params string[] parameters)
        {
            // the vault landblock (Concordia), loaded if nobody is there (its objects appear a moment after loading)
            var landblock = AcxVaults.VaultLandblock;
            string result;
            if (parameters.Length == 0)
                result = AcxVaults.List(landblock);
            else if (parameters.Length >= 2 && parameters[0] == "release")
                result = AcxVaults.Admin(landblock, "release", parameters[1], 0);
            else if (parameters.Length >= 3 && parameters[0] == "paid" && int.TryParse(parameters[2], out var days))
                result = AcxVaults.Admin(landblock, "paid", parameters[1], days);
            else
                result = "Usage: /acxvault [release <1-A> | paid <1-A> <days>]";
            CommandHandlerHelper.WriteOutputInfo(session, result);
        }
    }
}
