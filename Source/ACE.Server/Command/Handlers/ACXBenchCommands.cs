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

        [CommandHandler("acxsurvival", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "End-game survival bench: realistic characters (XP spent per build) in median tinked loot, fully buffed, fight the top spawning monsters toe to toe through the server's combat code, without and with healing kits. Writes CSVs to Logs/acxbench.",
            "[levels=200,240,275] [builds=melee,meleeshield,archer,mage] [fights=20] [max=600 (seconds)] [healat=0.55] [mobs=<wcid,wcid,...> (replaces the default list)] [elites=0] [pets=0] [lum=<0-100> (luminance auras in percent; default by level)] [profiles=fresh,veteran] [gear=base|sets|aetheria|full] [set=Soldiers|Defenders|Archers|Adepts|Hearty|Wise|...] [aeth=defense|offense] [trinket=scarab|diamond|gromnie|ruby|amber|sapphire] | dummy items=0x...,0x... [levels=275] [builds=melee] [seconds=60] [transfers=1] [runs=10] [variants=0] [profile=veteran]")]
        public static void HandleAcxSurvival(Session session, params string[] parameters)
        {
            // /acxsurvival dummy items=0x...,0x... [levels=275] [builds=melee] [seconds=60] [transfers=0|1] [runs=10] [variants=0|1]:
            // shard items worn by the realistic build against the Training Dummy (AcxBench_SurvivalDummy.cs)
            // /acxsurvival duo [fights=10] [towns=0]: duos (and each build solo) against the invasion elites (AcxBench_Survival.RunDuo)
            if (parameters.Length > 0 && parameters[0].Equals("duo", StringComparison.OrdinalIgnoreCase))
            {
                var duo = new AcxBench.DuoOptions();
                foreach (var p in parameters.Skip(1).Where(p => p.Contains('=')))
                {
                    var kv = p.Split('=', 2);
                    if (kv[0].Equals("fights", StringComparison.OrdinalIgnoreCase) && int.TryParse(kv[1], out var n)) duo.Fights = Math.Clamp(n, 1, 100);
                    if (kv[0].Equals("towns", StringComparison.OrdinalIgnoreCase)) duo.Towns = kv[1] != "0";
                    if (kv[0].Equals("top", StringComparison.OrdinalIgnoreCase)) duo.Top = kv[1] != "0";
                    if (kv[0].Equals("only", StringComparison.OrdinalIgnoreCase)) duo.Only = kv[1].Split(',').ToList();
                    if (kv[0].Equals("party", StringComparison.OrdinalIgnoreCase)) duo.Parties = kv[1] != "0";
                }
                CommandHandlerHelper.WriteOutputInfo(session, AcxBench.StartDuo(duo, text => CommandHandlerHelper.WriteOutputInfo(session, text)));
                return;
            }

            // /acxsurvival pack [size=4] [levels=275] [builds=alchemist,archer,...] [profiles=veteran] [fights=10] [mobs=<wcid,...>] [gear=full]: one
            // player against a pack of a monster, so the Alchemist's blast counts (AcxBench_SurvivalAlchemist.cs)
            if (parameters.Length > 0 && parameters[0].Equals("pack", StringComparison.OrdinalIgnoreCase))
            {
                var pack = new AcxBench.PackOptions();
                foreach (var p in parameters.Skip(1).Where(p => p.Contains('=')))
                {
                    var kv = p.Split('=', 2);
                    var value = kv[1];
                    switch (kv[0].ToLowerInvariant())
                    {
                        case "size": if (int.TryParse(value, out var size)) pack.Size = Math.Clamp(size, 1, 8); break;
                        case "levels": pack.Levels = value.Split(',').Select(v => int.TryParse(v, out var l) ? Math.Clamp(l, 1, 275) : 0).Where(l => l > 0).ToList(); break;
                        case "builds": pack.Builds = value.ToLowerInvariant().Split(',').ToList(); break;
                        case "profiles": pack.Profiles = value.ToLowerInvariant().Split(',').ToList(); break;
                        case "fights": if (int.TryParse(value, out var fights)) pack.Fights = Math.Clamp(fights, 1, 100); break;
                        case "mobs": pack.Monsters = value.Split(',').Select(v => uint.TryParse(v, out var w) ? w : 0).Where(w => w != 0).ToList(); break;
                        case "gear": pack.Gear = value.ToLowerInvariant() switch { "base" => 0, "sets" => 1, "aetheria" => 2, "full" => 3, _ => int.TryParse(value, out var g) ? Math.Clamp(g, 0, 3) : 3 }; break;
                        case "bombimbue": pack.BombImbue = value.ToLowerInvariant(); break;
                        case "shield": pack.ThrownShield = value != "0"; break;
                    }
                }
                CommandHandlerHelper.WriteOutputInfo(session, AcxBench.StartPack(pack, text => CommandHandlerHelper.WriteOutputInfo(session, text)));
                return;
            }

            // /acxsurvival atlans [runs=20] [gear=full]: each Atlan grade vs loot of its wield on the Training Dummy (AcxBench_SurvivalDummy.cs)
            if (parameters.Length > 0 && parameters[0].Equals("atlans", StringComparison.OrdinalIgnoreCase))
            {
                var atlanRuns = 20;
                var atlanGear = 3;
                foreach (var p in parameters.Skip(1).Where(p => p.Contains('=')))
                {
                    var kv = p.Split('=', 2);
                    if (kv[0].Equals("runs", StringComparison.OrdinalIgnoreCase) && int.TryParse(kv[1], out var r)) atlanRuns = Math.Clamp(r, 1, 100);
                    if (kv[0].Equals("gear", StringComparison.OrdinalIgnoreCase))
                        atlanGear = kv[1].ToLowerInvariant() switch { "base" => 0, "sets" => 1, "aetheria" => 2, "full" => 3, _ => int.TryParse(kv[1], out var g) ? Math.Clamp(g, 0, 3) : 3 };
                }
                CommandHandlerHelper.WriteOutputInfo(session, AcxBench.StartAtlans(atlanRuns, atlanGear, text => CommandHandlerHelper.WriteOutputInfo(session, text)));
                return;
            }

            if (parameters.Length > 0 && parameters[0].Equals("dummy", StringComparison.OrdinalIgnoreCase))
            {
                static uint Hex(string v) => uint.TryParse(v.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? v[2..] : v, System.Globalization.NumberStyles.HexNumber, null, out var g) ? g : 0;
                var dummy = new AcxBench.DummyOptions();
                foreach (var p in parameters.Skip(1).Where(p => p.Contains('=')))
                {
                    var kv = p.Split('=', 2);
                    var value = kv[1];
                    switch (kv[0].ToLowerInvariant())
                    {
                        case "items":
                            dummy.Items = value.Split(',').Select(Hex).Where(g => g != 0).ToList();
                            break;
                        case "levels":
                            dummy.Levels = value.Split(',').Select(v => int.TryParse(v, out var l) ? Math.Clamp(l, 1, 275) : 0).Where(l => l > 0).ToList();
                            break;
                        case "builds":
                            dummy.Builds = value.ToLowerInvariant().Split(',').ToList();
                            break;
                        case "seconds":
                            if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds)) dummy.Seconds = Math.Clamp(seconds, 5, 600);
                            break;
                        case "transfers":
                            dummy.Transfers = value != "0";
                            break;
                        case "runs":
                            if (int.TryParse(value, out var runs)) dummy.Runs = Math.Clamp(runs, 1, 100);
                            break;
                        case "variants":
                            dummy.Variants = value != "0";
                            break;
                        case "profile":
                            dummy.Veteran = value.Equals("veteran", StringComparison.OrdinalIgnoreCase);
                            break;
                    }
                }
                if (dummy.Items.Count == 0 && !dummy.Variants)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, "Usage: /acxsurvival dummy items=0x800003A5,0x800007DA [levels=275] [builds=melee] [seconds=60] [transfers=1] [runs=10] [variants=0]");
                    return;
                }
                CommandHandlerHelper.WriteOutputInfo(session, AcxBench.StartDummy(dummy, text => CommandHandlerHelper.WriteOutputInfo(session, text)));
                return;
            }

            var options = new AcxBench.SurvivalOptions();
            foreach (var p in parameters.Where(p => p.Contains('=')))
            {
                var kv = p.Split('=', 2);
                var value = kv[1];
                switch (kv[0].ToLowerInvariant())
                {
                    case "levels":
                        options.Levels = value.Split(',').Select(v => int.TryParse(v, out var l) ? Math.Clamp(l, 1, 275) : 0).Where(l => l > 0).ToList();
                        break;
                    case "builds":
                        options.Builds = value.ToLowerInvariant().Split(',').ToList();
                        break;
                    case "fights":
                        if (int.TryParse(value, out var fights)) options.Fights = Math.Clamp(fights, 1, 200);
                        break;
                    case "max":
                        if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var max)) options.MaxSeconds = Math.Clamp(max, 10, 3600);
                        break;
                    case "healat":
                        if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var healAt)) options.HealAt = Math.Clamp(healAt, 0.05, 0.95);
                        break;
                    case "mobs":
                        options.Monsters = value.Split(',').Select(v => uint.TryParse(v, out var w) ? w : 0).Where(w => w != 0).ToList();
                        break;
                    case "tier":   // another loot tier's gear at these levels (2026-11-16: the Alchemist's low-tier rules)
                        if (int.TryParse(value, out var lootTier)) options.LootTier = Math.Clamp(lootTier, 0, 8);
                        break;
                    case "elites":
                        options.Elites = value != "0";
                        break;
                    case "pets":
                        options.Pets = value != "0";
                        break;
                    case "lum":
                        if (int.TryParse(value, out var lum)) options.LumLevel = Math.Clamp(lum, 0, 100);
                        break;
                    case "profiles":
                        options.Profiles = value.ToLowerInvariant().Split(',').Where(v => v == "fresh" || v == "veteran").ToList();
                        break;
                    case "gear":
                        // base | sets | aetheria | full (each adds to the one before)
                        options.Gear = value.ToLowerInvariant() switch { "sets" => 1, "aetheria" => 2, "full" => 3, _ => int.TryParse(value, out var g) ? Math.Clamp(g, 0, 3) : 0 };
                        break;
                    case "set":
                        options.ArmorSet = value;
                        break;
                    case "aeth":
                        options.Aetheria = value.ToLowerInvariant() == "offense" ? "offense" : "defense";
                        break;
                    case "trinket":
                        options.Trinket = value.ToLowerInvariant();
                        break;
                    case "wtable":
                        options.MeleeKind = value;   // e.g. Heavy.Schlagers, Light.Daggers, Finesse.Rapiers
                        break;
                    case "bombimbue":   // cs | cb | ar | rend: an imbue on the Alchemist's bombs (none by default, as the game allows)
                        options.BombImbue = value.ToLowerInvariant();
                        break;
                    case "shield":   // 1: the Alchemist and the thrown build carry a shield
                        options.ThrownShield = value != "0";
                        break;
                }
            }
            if (options.Levels.Count == 0 || options.Builds.Count == 0 || options.Profiles.Count == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: /acxsurvival [levels=200,240,275] [builds=melee,meleeshield,archer,mage] [fights=20]");
                return;
            }
            CommandHandlerHelper.WriteOutputInfo(session, AcxBench.StartSurvival(options, text => CommandHandlerHelper.WriteOutputInfo(session, text)));
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
