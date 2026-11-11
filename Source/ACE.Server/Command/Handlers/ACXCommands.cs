using System;
using System.Globalization;

using ACE.Entity.Enum;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX developer commands
    /// </summary>
    public static class ACXCommands
    {
        [CommandHandler("acxfx", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Plays a PhysicsScript by id on your wielded weapon, yourself, or your last appraised object.",
            "<script id in hex, e.g. 33000F82> [weapon | self | appraised]  (default: weapon)")]
        public static void HandleAcxFx(Session session, params string[] parameters)
        {
            var text = parameters[0].Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(2);

            if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var scriptId) || (scriptId >> 24) != 0x33)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Script id must be a 0x33xxxxxx PhysicsScript id, e.g. /acxfx 33000F82");
                return;
            }

            var player = session.Player;
            var mode = parameters.Length > 1 ? parameters[1].ToLowerInvariant() : "weapon";

            WorldObject target;
            switch (mode)
            {
                case "self":
                    target = player;
                    break;
                case "appraised":
                    target = CommandHandlerHelper.GetLastAppraisedObject(session);
                    break;
                default:
                    target = player.GetEquippedWeapon();
                    break;
            }

            if (target == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"No {mode} target: wield a weapon, or appraise an object first.");
                return;
            }

            player.EnqueueBroadcast(new GameMessagePlayScriptId(target.Guid, scriptId));
            CommandHandlerHelper.WriteOutputInfo(session, $"Playing script {scriptId:X8} on {target.Name}.");
        }
    }
}
