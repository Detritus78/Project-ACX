using System;
using System.Globalization;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
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
        [CommandHandler("dps", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0,
            "Damage-per-second meter for combat testing: counts melee, missile, spell and damage-over-time damage you deal.",
            "start | stop | (nothing: show the numbers so far)")]
        public static void HandleDps(Session session, params string[] parameters)
        {
            var player = session.Player;
            var action = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "";

            switch (action)
            {
                case "start":
                    player.StartAcxDps();
                    CommandHandlerHelper.WriteOutputInfo(session, "DPS meter started. The clock starts at your first attack; /dps shows the numbers, /dps stop ends it.");
                    return;

                case "stop":
                    player.StopAcxDps();
                    break;

                case "":
                    break;

                default:
                    CommandHandlerHelper.WriteOutputInfo(session, "Usage: /dps start | /dps stop | /dps");
                    return;
            }

            CommandHandlerHelper.WriteOutputInfo(session, player.GetAcxDpsReport());
        }

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

        [CommandHandler("acxtex", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Lists or swaps the textures of your wielded weapon (or last appraised object), live.",
            "list | clear | <old texture> <new texture> [part]   e.g. /acxtex 05000C53 0500303D")]
        public static void HandleAcxTex(Session session, params string[] parameters)
        {
            var player = session.Player;
            var target = player.GetEquippedWeapon() ?? CommandHandlerHelper.GetLastAppraisedObject(session);
            if (target == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Wield a weapon or appraise an object first.");
                return;
            }

            var mode = parameters[0].ToLowerInvariant();

            if (mode == "list")
            {
                var objDesc = target.CalculateObjDesc();
                var setup = DatManager.PortalDat.ReadFromDat<SetupModel>(target.SetupTableId);
                for (var part = 0; part < setup.Parts.Count; part++)
                {
                    var gfx = DatManager.PortalDat.ReadFromDat<GfxObj>(setup.Parts[part]);
                    foreach (var surfaceId in gfx.Surfaces.Distinct())
                    {
                        var surface = DatManager.PortalDat.ReadFromDat<Surface>(surfaceId);
                        if (surface.OrigTextureId == 0)
                            continue;
                        var shown = surface.OrigTextureId;
                        foreach (var change in objDesc.TextureChanges.Where(t => t.PartIndex == part && t.OldTexture == shown))
                            shown = change.NewTexture;
                        CommandHandlerHelper.WriteOutputInfo(session, $"part {part}: texture {surface.OrigTextureId:X8}" + (shown != surface.OrigTextureId ? $" (now shows {shown:X8})" : ""));
                    }
                }
                return;
            }

            if (mode == "clear")
            {
                target.ClearAcxTextureSwaps();
                player.EnqueueBroadcast(new GameMessageUpdateObject(target));
                CommandHandlerHelper.WriteOutputInfo(session, $"Cleared texture swaps on {target.Name}.");
                return;
            }

            if (parameters.Length < 2 || !TryParseHex(parameters[0], out var oldTexture) || !TryParseHex(parameters[1], out var newTexture)
                || (oldTexture >> 24) != 0x05 || (newTexture >> 24) != 0x05)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: /acxtex list | clear | <old 05xxxxxx> <new 05xxxxxx> [part]");
                return;
            }

            byte partIndex = 0;
            if (parameters.Length > 2 && !byte.TryParse(parameters[2], out partIndex))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Part must be a number, e.g. 0.");
                return;
            }

            target.SetAcxTextureSwap(partIndex, oldTexture, newTexture);
            player.EnqueueBroadcast(new GameMessageUpdateObject(target));
            CommandHandlerHelper.WriteOutputInfo(session, $"{target.Name}: part {partIndex} texture {oldTexture:X8} now shows {newTexture:X8}.");
        }

        private static bool TryParseHex(string text, out uint value)
        {
            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(2);
            return uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }
    }
}
