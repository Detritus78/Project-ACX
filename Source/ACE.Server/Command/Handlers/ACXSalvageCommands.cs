using System;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: Smart Salvage settings (WorldObjects/Player_AcxSalvage.cs)
    /// </summary>
    public static class ACXSalvageCommands
    {
        [CommandHandler("smartsalvage", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Smart Salvage: the Ust skips materials you choose and keeps salvage in workmanship bands.",
            "on | off | status | mode whitelist|blacklist | inscribed skip|keep | add <material> | remove <material> | clear")]
        public static void HandleSmartSalvage(Session session, params string[] parameters)
        {
            var player = session.Player;
            var action = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "status";
            var arg = parameters.Length > 1 ? string.Concat(parameters.Skip(1)) : "";   // "green garnet" -> "greengarnet"

            switch (action)
            {
                case "on":
                case "off":
                    player.SetProperty(PropertyBool.AcxSmartSalvage, action == "on");
                    break;

                case "mode":
                    if (arg.ToLowerInvariant() is not ("whitelist" or "blacklist"))
                    {
                        Reply(session, "Use /smartsalvage mode whitelist (salvage only the listed materials) or blacklist (salvage everything but them).");
                        return;
                    }
                    player.SetProperty(PropertyBool.AcxSalvageWhitelist, arg.ToLowerInvariant() == "whitelist");
                    break;

                case "inscribed":
                    if (arg.ToLowerInvariant() is not ("skip" or "keep"))
                    {
                        Reply(session, "Use /smartsalvage inscribed skip (leave inscribed items alone) or keep (salvage them too).");
                        return;
                    }
                    player.SetProperty(PropertyBool.AcxSalvageAvoidInscribed, arg.ToLowerInvariant() == "skip");
                    break;

                case "add":
                case "remove":
                    if (!Enum.TryParse(arg, true, out MaterialType material) || material == MaterialType.Unknown)
                    {
                        Reply(session, $"There's no material called \"{string.Join(" ", parameters.Skip(1))}\".");
                        return;
                    }
                    var filter = player.AcxSalvageFilter();
                    if (action == "add") filter.Add(material); else filter.Remove(material);
                    player.AcxSetSalvageFilter(filter);
                    break;

                case "clear":
                    player.AcxSetSalvageFilter(Enumerable.Empty<MaterialType>());
                    break;

                case "status":
                    break;

                default:
                    Reply(session, "Usage: /smartsalvage on | off | status | mode whitelist|blacklist | inscribed skip|keep | add <material> | remove <material> | clear");
                    return;
            }

            var list = player.AcxSalvageFilter();
            var whitelist = player.GetProperty(PropertyBool.AcxSalvageWhitelist) ?? false;
            Reply(session, $"Smart Salvage is {(player.AcxSmartSalvageOn ? "on" : "off")}. "
                + (list.Count == 0 ? "No materials listed, so everything is salvaged. " : $"{(whitelist ? "Only" : "Never")} salvaging: {string.Join(", ", list.Select(RecipeManager.GetMaterialName))}. ")
                + $"Inscribed items are {((player.GetProperty(PropertyBool.AcxSalvageAvoidInscribed) ?? false) ? "left alone" : "salvaged")}. "
                + "Salvage is kept in workmanship bands (under 4, 4-7, 8+) while it's on.");
        }

        private static void Reply(Session session, string text) => CommandHandlerHelper.WriteOutputInfo(session, text);
    }
}
