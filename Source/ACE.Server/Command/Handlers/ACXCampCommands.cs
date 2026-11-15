using System;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: T.A.R. camps (Managers/AcxCampManager.cs, 2026-11-13). /tar is ClassicACE's player command, with /camp
    /// as another name for it; /acxtar is ours, for looking at or clearing a player's camps.
    /// </summary>
    public static class ACXCampCommands
    {
        [CommandHandler("tar", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Show your current T.A.R. (Type, Area, Rest) experience multipliers.",
            "[creature type, e.g. olthoi]")]
        [CommandHandler("camp", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Show your current T.A.R. (Type, Area, Rest) experience multipliers.",
            "[creature type, e.g. olthoi]")]
        public static void HandleTar(Session session, params string[] parameters)
        {
            CreatureType? type = null;

            if (parameters.Length > 0)
            {
                // "altered human" finds AlteredHuman
                if (!Enum.TryParse(string.Concat(parameters), true, out CreatureType parsed) || !Enum.IsDefined(parsed) || parsed == CreatureType.Invalid)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"There's no creature type called {string.Join(" ", parameters)}. Usage: /tar [creature type], e.g. /tar olthoi");
                    return;
                }
                type = parsed;
            }

            CommandHandlerHelper.WriteOutputInfo(session, AcxCampManager.Report(session.Player, type));
        }

        [CommandHandler("acxtar", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0,
            "Shows every T.A.R. camp an online player has (yourself if no name), or clears them.",
            "[player name] [reset]")]
        public static void HandleAcxTar(Session session, params string[] parameters)
        {
            var reset = parameters.Length > 0 && parameters[^1].Equals("reset", StringComparison.OrdinalIgnoreCase);
            var name = string.Join(" ", reset ? parameters.SkipLast(1) : parameters);

            var player = name.Length == 0 ? session.Player : PlayerManager.GetOnlinePlayer(name);
            if (player == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"No player called {name} is online.");
                return;
            }

            if (reset)
            {
                AcxCampManager.Reset(player);
                CommandHandlerHelper.WriteOutputInfo(session, $"Cleared {player.Name}'s T.A.R. camps.");
                return;
            }

            CommandHandlerHelper.WriteOutputInfo(session, AcxCampManager.Dump(player));
        }
    }
}
