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
            "[from level, default 10] [to level, default 130] [step, default 5] [rend=2.5 slayer=1.5 crit=0.15 critx=2 dmg=10 wcid=<weapon> spells=1 (what-if gear: rend or cleave value, slayer vs each monster, Biting Strike, Crushing Blow, extra damage, a given weapon instead of loot Long Swords, its own spells on)]")]
        public static void HandleAcxXpModel(Session session, params string[] parameters)
        {
            var from = parameters.Length > 0 && int.TryParse(parameters[0], out var f) ? Math.Clamp(f, 1, 275) : 10;
            var to = parameters.Length > 1 && int.TryParse(parameters[1], out var t) ? Math.Clamp(t, from, 275) : 130;
            var step = parameters.Length > 2 && int.TryParse(parameters[2], out var s) ? Math.Clamp(s, 1, 50) : 5;

            var setup = new AcxBench.XpSetup();
            foreach (var p in parameters.Where(p => p.Contains('=')))
            {
                var kv = p.Split('=', 2);
                if (!double.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                    continue;
                switch (kv[0].ToLowerInvariant())
                {
                    case "rend": setup.Rend = v; break;
                    case "slayer": setup.Slayer = v; break;
                    case "crit": setup.CritChance = v; break;
                    case "critx": setup.CritMultiplier = v; break;
                    case "dmg": setup.Damage = (int)v; break;
                    case "wcid": setup.Wcid = (uint)v; break;
                    case "spells": setup.Spells = v != 0; break;
                }
            }

            CommandHandlerHelper.WriteOutputInfo(session, AcxBench.StartXpModel(from, to, step, setup, text => CommandHandlerHelper.WriteOutputInfo(session, text)));
        }

        [CommandHandler("acxxptables", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Writes the XP tables (character level, attribute, trained and specialized skill ranks) to Logs/acxbench/acxxptables.csv, for level estimates.")]
        public static void HandleAcxXpTables(Session session, params string[] parameters)
        {
            var table = ACE.DatLoader.DatManager.PortalDat.XpTable;
            var rows = new[] { table.CharacterLevelXPList.Count, table.AttributeXpList.Count, table.TrainedSkillXpList.Count, table.SpecializedSkillXpList.Count }.Max();
            var sb = new System.Text.StringBuilder("Index,CharacterLevelXP,AttributeXP,TrainedSkillXP,SpecializedSkillXP\n");
            string At<T>(System.Collections.Generic.List<T> list, int i) => i < list.Count ? list[i].ToString() : "";
            for (var i = 0; i < rows; i++)
                sb.AppendLine(string.Join(",", i, At(table.CharacterLevelXPList, i), At(table.AttributeXpList, i), At(table.TrainedSkillXpList, i), At(table.SpecializedSkillXpList, i)));
            var dir = System.IO.Path.Combine("Logs", "acxbench");
            System.IO.Directory.CreateDirectory(dir);
            var file = System.IO.Path.Combine(dir, "acxxptables.csv");
            System.IO.File.WriteAllText(file, sb.ToString());
            CommandHandlerHelper.WriteOutputInfo(session, $"XP tables written to {file} ({rows} rows).");
        }

        [CommandHandler("acxgear", AccessLevel.Admin, CommandHandlerFlag.None, 1,
            "Gear bench: a quest weapon against loot of its weapon type in every class at the same wield requirement, untinked and tinked. Writes a CSV to Logs/acxbench.",
            "<quest weapon wcid | class> [character level, default: where loot of that wield requirement drops] [kinds=all: every loot kind, not one per class] [check=1: each row the bench's way and the game's way, 30 swings] [debuff=1: Imperil and the matching vulnerability on each monster] [wield=N: compare against loot of this wield requirement] [single=1: just this weapon, not its element versions]")]
        public static void HandleAcxGear(Session session, params string[] parameters)
        {
            // /acxgear class <level> [debuff=1]: every weapon class at a level (AcxBench_Gear.RunClassBench)
            if (parameters[0].Equals("class", StringComparison.OrdinalIgnoreCase))
            {
                var classLevel = parameters.Length > 1 && int.TryParse(parameters[1], out var cl) ? Math.Clamp(cl, 1, 275) : 155;
                var classDebuff = parameters.Any(p => p.Equals("debuff=1", StringComparison.OrdinalIgnoreCase));
                // type=slash|bludgeon: the missile classes' damage type (default pierce)
                var missileType = parameters.Any(p => p.Equals("type=slash", StringComparison.OrdinalIgnoreCase)) ? DamageType.Slash
                    : parameters.Any(p => p.Equals("type=bludgeon", StringComparison.OrdinalIgnoreCase)) ? DamageType.Bludgeon : DamageType.Pierce;
                CommandHandlerHelper.WriteOutputInfo(session, AcxBench.StartClassBench(classLevel, classDebuff, missileType, text => CommandHandlerHelper.WriteOutputInfo(session, text)));
                return;
            }

            if (!uint.TryParse(parameters[0], out var wcid))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: /acxgear <quest weapon wcid> [level]");
                return;
            }
            var level = parameters.Length > 1 && int.TryParse(parameters[1], out var l) ? Math.Clamp(l, 1, 275) : 0;
            var allKinds = parameters.Any(p => p.Equals("kinds=all", StringComparison.OrdinalIgnoreCase));
            var check = parameters.Any(p => p.Equals("check=1", StringComparison.OrdinalIgnoreCase));
            var debuff = parameters.Any(p => p.Equals("debuff=1", StringComparison.OrdinalIgnoreCase));
            var lootWield = parameters.Select(p => p.StartsWith("wield=", StringComparison.OrdinalIgnoreCase) && int.TryParse(p.Substring(6), out var w) ? w : 0).Max();
            var single = parameters.Any(p => p.Equals("single=1", StringComparison.OrdinalIgnoreCase));

            CommandHandlerHelper.WriteOutputInfo(session, AcxBench.StartGear(wcid, level, allKinds, check, debuff, lootWield, single, text => CommandHandlerHelper.WriteOutputInfo(session, text)));
        }
    }
}
