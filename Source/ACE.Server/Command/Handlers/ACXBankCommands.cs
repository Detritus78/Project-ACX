using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: the bank (Managers/AcxWealth.cs). /b is short for /bank, d for deposit, w for withdraw, a for all, n for notes:
    /// /b d a banks every pyreal, note and currency you carry; /b w n MMD hands out a 250,000 note; /b t n MMD 25 Bob sends
    /// 6,250,000 pyreals from your bank to Bob's. The bank lives on each character's own record (AcxBankedPyreals,
    /// AcxBankedCurrencies), so no account numbers are needed: character names are unique.
    /// </summary>
    public static class ACXBankCommands
    {
        private const string Help = "/b (balance) | /b d a (bank everything) | /b w <amount> | /b w n <MMD|MM|MD|M|D|C> | /b t all <name> (send everything) | /b t <amount> <name> | /b t marks <count|all> <name>. A first name is enough.";

        [CommandHandler("bank", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Your bank, anywhere: pyreals (trade notes go in as pyreals and come out as any note size), ACX Marks and every other currency. What you have banked in pyreals sets your wealth rank. Short form: /b.",
            Help)]
        public static void HandleBank(Session session, params string[] parameters)
        {
            var player = session.Player;
            var action = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "";
            var args = parameters.Skip(1).ToArray();

            var message = action switch
            {
                "" or "b" or "bal" or "balance" => AcxWealth.Balance(player),
                "d" or "dep" or "deposit" => AcxWealth.Deposit(player, args),
                "w" or "with" or "withdraw" => AcxWealth.Withdraw(player, args),
                "t" or "transfer" or "send" => AcxWealth.Transfer(player, args),
                _ => $"{AcxWealth.DepositUsage} {AcxWealth.WithdrawUsage} {AcxWealth.TransferUsage}",
            };

            CommandHandlerHelper.WriteOutputInfo(session, message);
        }

        [CommandHandler("b", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Short for /bank: /b d a deposits everything, /b w n MMD withdraws a 250,000 note.",
            Help)]
        public static void HandleB(Session session, params string[] parameters) => HandleBank(session, parameters);
    }
}
