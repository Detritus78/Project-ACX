using System;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: the weapon bench (Managers/AcxBench.cs)
    /// </summary>
    public static class ACXBenchCommands
    {
        private static readonly string[] Classes = { "heavy", "light", "finesse", "twohanded", "missile", "thrown", "thrownloot", "all" };   // Project ACX: thrownloot = Eternal thrown weapons

        [CommandHandler("acxbench", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Weapon bench: every real weapon of a class, rolled as loot and swung through the server's combat code at a target dummy. Writes a CSV to Logs/acxbench.",
            "[heavy|light|finesse|twohanded|missile|thrown|thrownloot|all] [tier 1-8 | all] [loot rolls per weapon, default 100] [str=100 coord=100 quick=70 end=40 focus=10 self=10] [axespeed=45 (any weapon type)] [bd=20 (Blood Drinker VI on the weapon)] [sk=60 (Swift Killer VI)] [misses=1 (level-appropriate skill vs the monsters' defense)]")]
        public static void HandleAcxBench(Session session, params string[] parameters)
        {
            var options = new AcxBench.Options();

            if (parameters.Length > 0)
                options.Class = parameters[0].ToLower();

            if (!Classes.Contains(options.Class))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: /acxbench [heavy|light|finesse|twohanded|missile|thrown|thrownloot|all] [tier 1-8 | all] [loot rolls]");
                return;
            }

            if (parameters.Length > 1)
            {
                if (parameters[1].Equals("all", StringComparison.OrdinalIgnoreCase))
                    options.Tiers = Enumerable.Range(1, 8).ToList();
                else if (int.TryParse(parameters[1], out var tier) && tier >= 1 && tier <= 8)
                    options.Tiers = new() { tier };
                else
                {
                    CommandHandlerHelper.WriteOutputInfo(session, "The tier is 1 to 8, or all.");
                    return;
                }
            }

            if (parameters.Length > 2 && int.TryParse(parameters[2], out var rolls))
                options.Rolls = Math.Clamp(rolls, 1, 1000);

            // attacker attributes, as name=value after the other parameters
            foreach (var parameter in parameters.Skip(1).Where(p => p.Contains('=')))
            {
                var parts = parameter.Split('=');
                if (parts.Length != 2 || !int.TryParse(parts[1], out var value) || value < 1 || value > 1000)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't read {parameter}: use str=, coord=, quick=, end=, focus= or self= with a number.");
                    return;
                }
                var key = parts[0].ToLower();
                if (key.EndsWith("speed") && Enum.TryParse<WeaponType>(key[..^5], true, out var weaponType))
                {
                    options.SpeedOverrides[weaponType] = value;
                    continue;
                }
                switch (key)
                {
                    case "bd": options.BloodDrinker = value; break;
                    case "sk": options.SwiftKiller = value; break;
                    case "misses": options.Misses = value != 0; break;
                    case "str": options.Str = value; break;
                    case "coord": options.Coord = value; break;
                    case "quick": options.Quick = value; break;
                    case "end": options.End = value; break;
                    case "focus": options.Focus = value; break;
                    case "self": options.Self = value; break;
                    default:
                        CommandHandlerHelper.WriteOutputInfo(session, $"Unknown setting {parts[0]}: use str, coord, quick, end, focus, self, or a weapon type + speed such as axespeed.");
                        return;
                }
            }

            CommandHandlerHelper.WriteOutputInfo(session, AcxBench.Start(options, text => CommandHandlerHelper.WriteOutputInfo(session, text)));
        }

        [CommandHandler("acxxpmodel", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "XP model: for each character level, fights the monsters that spawn near that level and records XP per hour of fighting. Writes a CSV to Logs/acxbench.",
            "[from level, default 10] [to level, default 130] [step, default 5]")]
        public static void HandleAcxXpModel(Session session, params string[] parameters)
        {
            var from = parameters.Length > 0 && int.TryParse(parameters[0], out var f) ? Math.Clamp(f, 1, 275) : 10;
            var to = parameters.Length > 1 && int.TryParse(parameters[1], out var t) ? Math.Clamp(t, from, 275) : 130;
            var step = parameters.Length > 2 && int.TryParse(parameters[2], out var s) ? Math.Clamp(s, 1, 50) : 5;

            CommandHandlerHelper.WriteOutputInfo(session, AcxBench.StartXpModel(from, to, step, text => CommandHandlerHelper.WriteOutputInfo(session, text)));
        }
    }
}
