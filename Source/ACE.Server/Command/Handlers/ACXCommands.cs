using System;
using System.Globalization;

using ACE.Entity.Enum;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX developer commands
    /// </summary>
    public static class ACXCommands
    {
        [CommandHandler("acxfx", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Plays a PhysicsScript by id on your wielded weapon (or your last appraised object).",
            "<script id in hex, e.g. 33000F82>")]
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
            var target = player.GetEquippedWeapon() ?? CommandHandlerHelper.GetLastAppraisedObject(session);
            if (target == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Wield a weapon or appraise an object first.");
                return;
            }

            player.EnqueueBroadcast(new GameMessagePlayScriptId(target.Guid, scriptId));
            CommandHandlerHelper.WriteOutputInfo(session, $"Playing script {scriptId:X8} on {target.Name}.");
        }
    }
}
