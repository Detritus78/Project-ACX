using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using log4net;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.DatLoader;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Factories.Entity;
using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.Physics.Animation;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: weapon bench (JJ 2026-11-13). Runs every real weapon of a class (each loot table entry: the plain
    /// weapon and its acid / lightning / fire / frost versions) through the server's own combat code against the target
    /// dummy (900015: 1.0 resistance to everything, the same armor everywhere) and writes a CSV to Logs/acxbench/.
    ///
    /// Each weapon is rolled through the real loot generator (LootGenerationFactory.MutateItem, no spells) Rolls times
    /// at the tier, and each copy swings Swings times at every attack height for three power bar settings: fast (empty
    /// bar), half and full. Timing is the real attack cycle (swing animation at the character's speed, plus the power
    /// bar refill; for missile weapons aim + reload + the accuracy bar), damage is DamageEvent.CalculateDamage, misses
    /// are re-rolled (always hit), and dagger / multi-strike sword bleeds count at their steady-state rate.
    ///
    /// The attacker is an in-memory character that is never in the world: JJ's test build by default (Str 100, Coord 100,
    /// Quick 70, End 40, Focus 10, Self 10; str= coord= quick= and so on change it), the weapon skills specialized, no heritage (so no heritage weapon bonus),
    /// one weapon and no shield (so ACX's single-weapon style applies). /acxbench in ACXBenchCommands.cs.
    /// </summary>
    public static partial class AcxBench
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public class Options
        {
            public string Class = "all";
            public List<int> Tiers = new() { 1 };
            public int Rolls = 100;
            public int Swings = 20;
            public int Str = 100, Coord = 100, Quick = 70, End = 40, Focus = 10, Self = 10;

            // what-if: a top base speed (WeaponTime) for a weapon type, e.g. axespeed=45 makes no axe slower than 45; faster ones
            // stay as they are, and each copy keeps its loot speed roll
            public Dictionary<WeaponType, int> SpeedOverrides = new();

            // what-if: Blood Drinker on every weapon, as its damage bonus (bd=20 is Blood Drinker VI); it adds to the top
            // damage of every strike, as the spell does. On a launcher it adds to each shot before the launcher's damage
            // multiplier (BaseDamageMod; ACE counts a launcher's Damage the same way), and a thrown weapon adds it too
            public int BloodDrinker;

            // what-if: Swift Killer on every weapon, as its speed reduction (sk=60 is Swift Killer VI); speed stops at 0
            public int SwiftKiller;

            // misses count: the target gets the typical melee/missile defense of the monsters at each tier's level, the
            // player what a character of that level can afford: a quarter of their XP in the weapon skill, a tenth in each of
            // Str/Coord/Quick, plus the level's buffs (str=/coord=/quick= are then the innate values)
            public bool Misses;

            public string StatsTag => $"S{Str}C{Coord}Q{Quick}" + string.Concat(SpeedOverrides.Select(s => $"-{s.Key}{s.Value}")) + (BloodDrinker > 0 ? $"-BD{BloodDrinker}" : "") + (SwiftKiller > 0 ? $"-SK{SwiftKiller}" : "") + (Misses ? "-misses" : "");
        }

        // the Foundry level buttons, one per tier
        private static readonly int[] TierLevels = { 10, 20, 50, 80, 115, 150, 200, 275 };

        private static readonly (string Name, float Level)[] PowerSettings = { ("fast", 0.0f), ("half", 0.5f), ("full", 1.0f) };
        private static readonly AttackHeight[] Heights = { AttackHeight.High, AttackHeight.Medium, AttackHeight.Low };

        private const uint TargetWcid = 900015;
        private const uint BenchCell = 0xA9B4001B;   // anywhere works: nothing is placed in the world

        // fixed guids, never registered: the bench objects stay out of the world and out of the guid allocators
        private const uint AttackerGuid = 0x5FFFFFF0;
        private const uint TargetGuid = 0xFFFFFFF0;
        private const uint WeaponGuid = 0xFFFFFFF1;
        private const uint AmmoGuid = 0xFFFFFFF2;
        private const uint ProjectileGuid = 0xFFFFFFF3;

        private static volatile bool running;

        private class Entry
        {
            public string Class;
            public string Family;
            public int FamilyIndex;   // 0 = the plain weapon (each loot table lists it first)
            public uint Wcid;
            public Skill Skill;
        }

        private class Sums
        {
            public double Time, Direct, Bleed, Strikes, Crits, Swings, Evades, Failures;
        }

        private class Result
        {
            public int Tier, Level;
            public Entry Entry;
            public string Name, WeaponType, Element, Ammo = "";
            public bool MultiStrike, Bleeds;
            public int BaseSpeed, BaseDamage;
            public double RolledSpeed, RolledDamage, Variance, DamageMod, ElementalBonus, AcxMutated;
            public Sums[] Power = new Sums[3];
            public double[] FullByHeight = new double[3];
            public double Dps(int p) => Power[p].Time > 0 ? (Power[p].Direct + Power[p].Bleed) / Power[p].Time : 0;
        }

        public static string Start(Options options, Action<string> reply)
        {
            if (running)
                return "A bench is already running.";
            running = true;

            Task.Run(() =>
            {
                try
                {
                    reply(Run(options));
                }
                catch (Exception e)
                {
                    log.Error($"AcxBench failed: {e}");
                    reply($"The bench failed: {e.Message}");
                }
                finally
                {
                    running = false;
                }
            });
            return $"Bench started: {options.Class}, tier {string.Join(",", options.Tiers)}, {options.Rolls} loot rolls x {options.Swings} swings, Str {options.Str} Coord {options.Coord} Quick {options.Quick}. Results go to Logs/acxbench/.";
        }

        private static string Run(Options o)
        {
            var started = DateTime.Now;
            var results = new List<Result>();

            var targetWeenie = DatabaseManager.World.GetCachedWeenie(TargetWcid);
            if (targetWeenie == null)
                return $"The target dummy ({TargetWcid}) is missing from the world database.";

            countMisses = o.Misses;
            var spawned = o.Misses ? SpawnedMonsters() : null;
            foreach (var tier in o.Tiers)
            {
                var level = TierLevels[tier - 1];
                var target = (Creature)WorldObjectFactory.CreateWorldObject(targetWeenie, new ObjectGuid(TargetGuid));
                target.Location = new Position(BenchCell, 50.0f, 51.0f, 0.0f, 0, 0, 1, 0);   // facing the attacker
                if (target.Invincible)
                    return "The target dummy is invincible, so it can't take damage.";

                var attacker = CreateAttacker(level, o);
                if (o.Misses)
                    SetLevelSkills(attacker, target, spawned, level);
                foreach (var entry in GetWeapons(o.Class, tier))
                {
                    var result = BenchWeapon(entry, tier, level, attacker, target, o);
                    if (result != null)
                        results.Add(result);
                }
            }

            if (results.Count == 0)
                return "No weapons matched.";

            var dir = Path.Combine("Logs", "acxbench");
            Directory.CreateDirectory(dir);
            var stamp = started.ToString("yyyyMMdd-HHmmss");
            var file = Path.Combine(dir, $"acxbench-{o.Class}-T{string.Join("_", o.Tiers)}-{o.StatsTag}-{stamp}.csv");
            File.WriteAllText(file, ToCsv(results));

            var seconds = (DateTime.Now - started).TotalSeconds;
            return $"Bench done in {seconds:0}s: {results.Count} weapons, written to {file}.\n" + Summary(results);
        }

        /// <summary>
        /// Every weapon in the loot tables for a class: melee tables are the same at every tier, missile tables change by tier
        /// </summary>
        private static List<Entry> GetWeapons(string cls, int tier)
        {
            var list = new List<Entry>();

            void AddMelee(string name, Type table, Skill skill)
            {
                foreach (var field in table.GetFields(BindingFlags.NonPublic | BindingFlags.Static).Where(f => f.FieldType == typeof(ChanceTable<WeenieClassName>)))
                {
                    var i = 0;
                    foreach (var (wcid, _) in (ChanceTable<WeenieClassName>)field.GetValue(null))
                        list.Add(new Entry { Class = name, Family = field.Name, FamilyIndex = i++, Wcid = (uint)wcid, Skill = skill });
                }
            }

            void AddMissile(string family, Type table, string cls = "Missile")
            {
                var tiers = table.GetFields(BindingFlags.NonPublic | BindingFlags.Static).FirstOrDefault(f => f.FieldType == typeof(List<ChanceTable<WeenieClassName>>));
                if (tiers == null)
                    return;
                foreach (var (wcid, _) in ((List<ChanceTable<WeenieClassName>>)tiers.GetValue(null))[tier - 1])
                {
                    if (!list.Any(e => e.Wcid == (uint)wcid))
                        list.Add(new Entry { Class = cls, Family = $"{family}: {wcid}", FamilyIndex = 0, Wcid = (uint)wcid, Skill = Skill.MissileWeapons });
                }
            }

            var all = cls == "all";
            if (all || cls == "heavy")
                AddMelee("Heavy", typeof(HeavyWeaponWcids), Skill.HeavyWeapons);
            if (all || cls == "light")
                AddMelee("Light", typeof(LightWeaponWcids), Skill.LightWeapons);
            if (all || cls == "finesse")
                AddMelee("Finesse", typeof(FinesseWeaponWcids), Skill.FinesseWeapons);
            if (all || cls == "twohanded")
                AddMelee("TwoHanded", typeof(TwoHandedWeaponWcids), Skill.TwoHandedCombat);
            if (all || cls == "thrown")
            {
                // thrown weapons: fixed damage, no loot tiers (each one is its own ammo): the vendors' seven, the fletched
                // Spike line, and the few special ones (Project ACX thrown weapons review 2026-11-14,
                // ~/Work/ac/research/thrown-weapons-2026-11-14.md: JJ "Yes! lets do it")
                foreach (var (wcid, family) in ThrownWeapons)
                    list.Add(new Entry { Class = "Thrown", Family = family, FamilyIndex = 0, Wcid = wcid, Skill = Skill.MissileWeapons });
            }
            if (all || cls == "thrownloot")
                AddMissile("Eternal Thrown", typeof(AcxThrownWcids), "ThrownLoot");   // Project ACX: rolled as loot, like launchers
            if (all || cls == "missile")
            {
                AddMissile("Bows", typeof(BowWcids_Aluvian));
                AddMissile("Bows", typeof(BowWcids_Gharundim));
                AddMissile("Bows", typeof(BowWcids_Sho));
                AddMissile("Crossbows", typeof(CrossbowWcids));
                AddMissile("Atlatls", typeof(AtlatlWcids));
            }
            return list;
        }

        private static readonly (uint Wcid, string Family)[] ThrownWeapons =
        {
            (316, "Throwing Darts"), (343, "Shoukens"), (315, "Throwing Daggers"), (310, "Throwing Clubs"), (317, "Djarids"), (320, "Javelins"), (304, "Throwing Axes"),
            (23873, "Spikes"), (23866, "Spikes"), (23859, "Spikes"), (44375, "Spikes"), (44215, "Spikes"),   // Spike, Greater, Deadly, Greater Deadly (wield 310), Burning Sands (fire)
            (3709, "Special Thrown"), (34999, "Special Thrown"), (52733, "Special Thrown"), (52732, "Special Thrown"),   // Ivory Tooth Dart, Channeling Stone Hatchet, Vortex Thorn, Storming Portal Axe
        };

        private static Player CreateAttacker(int level, Options o)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(1);   // "human", what character creation starts from
            var player = new Player(weenie, new ObjectGuid(AttackerGuid), 0);

            var sex = DatManager.PortalDat.CharGen.HeritageGroups[(uint)HeritageGroup.Aluvian].Genders[1];
            player.SetProperty(PropertyDataId.MotionTable, sex.MotionTable);
            player.SetProperty(PropertyDataId.CombatTable, sex.CombatTable);
            player.SetProperty(PropertyDataId.Setup, sex.SetupID);
            player.SetProperty(PropertyInt.HeritageGroup, 0);   // no heritage weapon bonus
            player.Name = "ACX Bench";
            player.Level = level;

            player.Attributes[PropertyAttribute.Strength].StartingValue = (uint)o.Str;
            player.Attributes[PropertyAttribute.Coordination].StartingValue = (uint)o.Coord;
            player.Attributes[PropertyAttribute.Quickness].StartingValue = (uint)o.Quick;
            player.Attributes[PropertyAttribute.Endurance].StartingValue = (uint)o.End;
            player.Attributes[PropertyAttribute.Focus].StartingValue = (uint)o.Focus;
            player.Attributes[PropertyAttribute.Self].StartingValue = (uint)o.Self;

            foreach (var skill in new[] { Skill.HeavyWeapons, Skill.LightWeapons, Skill.FinesseWeapons, Skill.TwoHandedCombat, Skill.MissileWeapons })
            {
                var creatureSkill = player.GetCreatureSkill(skill);
                creatureSkill.AdvancementClass = SkillAdvancementClass.Specialized;
                creatureSkill.InitLevel = 10;
            }

            player.Health.Current = player.Health.MaxValue;
            player.Stamina.Current = player.Stamina.MaxValue;   // an exhausted character swings at zero power
            player.Mana.Current = player.Mana.MaxValue;

            player.Location = new Position(BenchCell, 50.0f, 50.0f, 0.0f, 0, 0, 0, 1);
            return player;
        }

        private static Result BenchWeapon(Entry entry, int tier, int level, Player attacker, Creature target, Options o)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(entry.Wcid);
            if (weenie == null)
                return null;

            var result = new Result { Tier = tier, Level = level, Entry = entry };
            for (var p = 0; p < PowerSettings.Length; p++)
                result.Power[p] = new Sums();
            var byHeight = new Sums[Heights.Length];
            for (var h = 0; h < Heights.Length; h++)
                byHeight[h] = new Sums();

            var isMissile = entry.Class == "Missile" || entry.Class == "Thrown" || entry.Class == "ThrownLoot";
            var scale = PropertyManager.GetDouble("acx_bleed_damage_scale").Item;

            for (var roll = 0; roll < o.Rolls; roll++)
            {
                var weapon = WorldObjectFactory.CreateWorldObject(weenie, new ObjectGuid(WeaponGuid));
                if (entry.Class != "Thrown")
                    LootGenerationFactory.MutateItem(weapon, new TreasureDeath { Tier = tier, LootQualityMod = 0 }, false);

                if (roll == 0)
                {
                    result.Name = weenie.GetProperty(PropertyString.Name) ?? weapon.Name;
                    result.WeaponType = weapon.W_WeaponType.ToString();
                    result.Element = weapon.W_DamageType.ToString().Replace(", ", "+");
                    result.MultiStrike = weapon.W_AttackType.IsMultiStrike();
                    result.Bleeds = !isMissile && (weapon.W_WeaponType == WeaponType.Dagger || (weapon.W_WeaponType == WeaponType.Sword && result.MultiStrike));
                    result.BaseSpeed = weenie.GetProperty(PropertyInt.WeaponTime) ?? 0;
                    result.BaseDamage = weenie.GetProperty(PropertyInt.Damage) ?? 0;
                }
                if (o.SpeedOverrides.TryGetValue(weapon.W_WeaponType, out var speed) && (weenie.GetProperty(PropertyInt.WeaponTime) ?? 0) > speed)
                    weapon.WeaponTime = (int)Math.Round(speed * (double)(weapon.WeaponTime ?? 0) / weenie.GetProperty(PropertyInt.WeaponTime).Value);
                if (o.BloodDrinker > 0 && entry.Class != "Thrown" && entry.Class != "ThrownLoot")   // a thrown weapon's Blood Drinker goes on its projectile (BenchMissile)
                    weapon.Damage = (weapon.Damage ?? 0) + o.BloodDrinker;
                if (o.SwiftKiller > 0)
                    weapon.WeaponTime = Math.Max(0, (weapon.WeaponTime ?? 0) - o.SwiftKiller);
                result.RolledSpeed += weapon.WeaponTime ?? 0;
                result.RolledDamage += weapon.Damage ?? 0;
                result.Variance += weapon.DamageVariance ?? 0;
                result.DamageMod += weapon.DamageMod ?? 0;
                result.ElementalBonus += weapon.GetProperty(PropertyInt.ElementalDamageBonus) ?? 0;
                // ACX's extra loot mutations (2% each from tier 2): real drops, so they stay in, but counted
                if (weapon.GetProperty(PropertyFloat.IgnoreShield) != null || weapon.GetProperty(PropertyFloat.IgnoreArmor) != null || weapon.GetProperty(PropertyFloat.CriticalFrequency) != null
                    || weapon.GetProperty(PropertyFloat.CriticalMultiplier) != null || weapon.GetProperty(PropertyInt.SlayerCreatureType) != null)
                    result.AcxMutated++;

                var location = weapon.ValidLocations ?? EquipMask.MeleeWeapon;
                if (!attacker.TryEquipObject(weapon, location))
                {
                    result.Power[0].Failures++;
                    continue;
                }

                if (isMissile)
                    BenchMissile(attacker, weapon, target, result, o, tier);
                else
                    BenchMelee(attacker, weapon, target, result, byHeight, scale, o);

                // empty-handed again for the next copy
                foreach (var item in attacker.EquippedObjects.Values.ToList())
                    attacker.TryDequipObject(item.Guid, out _, out _);
            }

            result.RolledSpeed /= o.Rolls;
            result.RolledDamage /= o.Rolls;
            result.Variance /= o.Rolls;
            result.DamageMod /= o.Rolls;
            result.ElementalBonus /= o.Rolls;
            result.AcxMutated /= o.Rolls;
            for (var h = 0; h < Heights.Length; h++)
                result.FullByHeight[h] = isMissile ? result.Dps(2) : byHeight[h].Time > 0 ? byHeight[h].Direct / byHeight[h].Time : 0;
            return result;
        }

        private static void BenchMelee(Player attacker, WorldObject weapon, Creature target, Result result, Sums[] fullByHeight, double bleedScale, Options o)
        {
            var stance = attacker.GetCombatStance();
            attacker.CurrentMotionState = new Motion(stance);

            for (var p = 0; p < PowerSettings.Length; p++)
            {
                var sums = result.Power[p];
                for (var h = 0; h < Heights.Length; h++)
                {
                    attacker.PowerLevel = PowerSettings[p].Level;
                    attacker.AttackHeight = Heights[h];
                    attacker.PrevMotionCommand = MotionCommand.Invalid;
                    var heightTime = 0.0;
                    var heightDamage = 0.0;

                    for (var swing = 0; swing < o.Swings; swing++)
                    {
                        // as Player.Attack / DoSwingMotion
                        var animSpeed = attacker.GetAnimSpeed() * attacker.GetAcxSingleWeaponAnimSpeedMod();
                        var motion = attacker.GetSwingAnimation();
                        var animLength = MotionTable.GetAnimationLength(attacker.MotionTableId, stance, motion, animSpeed);
                        if (animLength <= 0)
                        {
                            sums.Failures++;
                            continue;
                        }
                        var frames = MotionTable.GetAttackFrames(attacker.MotionTableId, stance, motion);
                        var strikes = attacker.GetNumStrikes(attacker.GetWeaponAttackType(weapon));
                        if (strikes != frames.Count)
                            strikes = frames.Count;

                        var swingDamage = 0.0f;
                        for (var s = 0; s < strikes; s++)
                        {
                            var damageEvent = Hit(attacker, target, weapon, sums);
                            if (damageEvent == null)
                                continue;
                            swingDamage += damageEvent.Damage;
                            if (damageEvent.IsCritical)
                                sums.Crits++;
                        }

                        // as Creature.AcxApplyWeaponBleed: 1 in 10 attacks cashes in the running bleeds instead of adding one
                        var bleed = 0.0;
                        if (result.Bleeds && swingDamage > 0)
                            bleed = 0.9 * Math.Round((5 + (int)Math.Round(swingDamage)) * bleedScale);

                        var time = animLength + PowerSettings[p].Level * attacker.GetAcxSingleWeaponRefillMod();
                        sums.Time += time;
                        sums.Direct += swingDamage;
                        sums.Bleed += bleed;
                        sums.Strikes += strikes;
                        sums.Swings++;

                        heightTime += time;
                        heightDamage += swingDamage + bleed;
                    }

                    if (p == PowerSettings.Length - 1)
                    {
                        fullByHeight[h].Time += heightTime;
                        fullByHeight[h].Direct += heightDamage;
                    }
                }
            }
        }

        // the ammo players use at each tier: basic, Greater, Prismatic, Deadly Prismatic (prismatic ammo takes the
        // launcher's element, so elemental and Slashing/Blunt launchers get their bonus)
        private static readonly uint[] ArrowsByTier = { 300, 300, 5304, 5304, 43952, 43952, 43951, 43951 };
        private static readonly uint[] QuarrelsByTier = { 305, 305, 5313, 5313, 43958, 43958, 43956, 43956 };
        private static readonly uint[] DartsByTier = { 12464, 12464, 15287, 15287, 43955, 43955, 43953, 43953 };

        private static void BenchMissile(Player attacker, WorldObject launcher, Creature target, Result result, Options o, int tier)
        {
            // a thrown weapon is its own ammo and has no launcher (Player.LaunchMissile)
            var thrown = launcher.DefaultCombatStyle == CombatStyle.ThrownWeapon;
            WorldObject ammo;
            if (thrown)
                ammo = launcher;
            else
            {
                var ammoWcid = launcher.DefaultCombatStyle switch
                {
                    CombatStyle.Crossbow => QuarrelsByTier[tier - 1],
                    CombatStyle.Atlatl => DartsByTier[tier - 1],
                    _ => ArrowsByTier[tier - 1],
                };
                ammo = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(ammoWcid), new ObjectGuid(AmmoGuid));
                attacker.TryEquipObject(ammo, EquipMask.MissileAmmo);
            }
            result.Ammo = ammo.Name;

            // as Creature.LaunchProjectile
            var projectile = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(ammo.WeenieClassId), new ObjectGuid(ProjectileGuid));
            projectile.ProjectileSource = attacker;
            projectile.ProjectileTarget = target;
            projectile.ProjectileLauncher = thrown ? null : launcher;
            projectile.ProjectileAmmo = ammo;
            AcxThrownWcids.CarryRoll(ammo, projectile);
            if (thrown && o.BloodDrinker > 0)
                projectile.Damage = (projectile.Damage ?? 0) + o.BloodDrinker;   // Blood Drinker on the thrown stack
            projectile.Location = new Position(attacker.Location);

            var stance = attacker.GetCombatStance();
            attacker.CurrentMotionState = new Motion(stance);
            attacker.AttackHeight = AttackHeight.Medium;

            // as Player.LaunchMissile: aim + shot, reload, back to ready, then the accuracy bar refills
            var aim = MotionTable.GetAnimationLength(attacker.MotionTableId, stance, MotionCommand.AimLevel, 1.0f);
            var reload = MotionTable.GetAnimationLength(attacker.MotionTableId, stance, MotionCommand.Reload, attacker.GetAnimSpeed());
            var link = MotionTable.GetAnimationLength(attacker.MotionTableId, stance, MotionCommand.Reload, MotionCommand.Ready);

            for (var p = 0; p < PowerSettings.Length; p++)
            {
                var sums = result.Power[p];
                attacker.AccuracyLevel = PowerSettings[p].Level;
                var cycle = aim + reload + link + PowerSettings[p].Level;

                for (var shot = 0; shot < o.Swings * Heights.Length; shot++)
                {
                    var damageEvent = Hit(attacker, target, projectile, sums);
                    if (damageEvent != null)
                    {
                        sums.Direct += damageEvent.Damage;
                        if (damageEvent.IsCritical)
                            sums.Crits++;
                    }
                    sums.Time += cycle;
                    sums.Strikes++;
                    sums.Swings++;
                }
            }
        }

        /// <summary>
        /// One strike through DamageEvent, re-rolled on a miss (the bench always hits) unless misses count
        /// </summary>
        // weapon skill ranks a character can afford with about a quarter of its XP, and attribute ranks with a tenth, at each
        // tier's level (DAT XP tables)
        private static readonly Dictionary<int, int> RanksAtLevel = new() { [10] = 50, [20] = 91, [50] = 161, [80] = 189, [115] = 206, [150] = 220, [200] = 226, [275] = 226 };
        private static readonly Dictionary<int, int> AttributeRanksAtLevel = new() { [10] = 15, [20] = 48, [50] = 107, [80] = 134, [115] = 156, [150] = 172, [200] = 190, [275] = 190 };

        [ThreadStatic] private static bool countMisses;

        /// <summary>
        /// Misses mode: the player's weapon skills as a character of this level has them (specialized, ranks, the level's
        /// buff), the target's defenses as the monsters there have them
        /// </summary>
        private static void SetLevelSkills(Player attacker, Creature target, List<MonsterInfo> spawned, int level)
        {
            var buff = AttributeBuff[BuffLevelFor(level)];   // weapon mastery buffs match the attribute buffs, level for level
            foreach (var attribute in new[] { PropertyAttribute.Strength, PropertyAttribute.Coordination, PropertyAttribute.Quickness })
            {
                attacker.Attributes[attribute].Ranks = (uint)AttributeRanksAtLevel[level];
                attacker.Attributes[attribute].StartingValue += (uint)buff;   // stands in for the buff spell
            }
            foreach (var skill in new[] { Skill.HeavyWeapons, Skill.LightWeapons, Skill.FinesseWeapons, Skill.TwoHandedCombat, Skill.MissileWeapons })
            {
                var creatureSkill = attacker.GetCreatureSkill(skill);
                creatureSkill.InitLevel = 10 + (uint)buff;
                creatureSkill.Ranks = (ushort)RanksAtLevel[level];
            }

            var (melee, missile) = MedianMonsterDefense(spawned, level);
            foreach (var (skill, wanted) in new[] { (Skill.MeleeDefense, melee), (Skill.MissileDefense, missile) })
            {
                var creatureSkill = target.GetCreatureSkill(skill);
                var withoutInit = (int)creatureSkill.Current - (int)creatureSkill.InitLevel;
                creatureSkill.InitLevel = (uint)Math.Max(0, wanted - withoutInit);
            }
            log.Info($"[ACXBENCH] misses L{level}: Str {attacker.Strength.Current} Coord {attacker.Coordination.Current} Quick {attacker.Quickness.Current}, weapon skill {attacker.GetCreatureSkill(Skill.HeavyWeapons).Current} melee, {attacker.GetCreatureSkill(Skill.MissileWeapons).Current} missile; target defense {melee} melee, {missile} missile");
        }

        private static DamageEvent Hit(Player attacker, Creature target, WorldObject damageSource, Sums sums)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                var damageEvent = DamageEvent.CalculateDamage(attacker, target, damageSource);
                if (damageEvent.GeneralFailure)
                    break;
                if (damageEvent.Evaded)
                {
                    sums.Evades++;
                    if (countMisses)
                        return damageEvent;   // a miss: the attack's time is spent, no damage
                    continue;
                }
                return damageEvent;
            }
            sums.Failures++;
            return null;
        }

        private static string F(double value, string format = "0.###") => value.ToString(format, CultureInfo.InvariantCulture);

        private static string ToCsv(List<Result> results)
        {
            var sb = new StringBuilder();
            var header = new List<string> { "Tier", "Level", "Class", "Family", "Variant", "WCID", "Weapon", "WeaponType", "DamageType", "MultiStrike", "Bleeds",
                "BaseSpeed", "AvgRolledSpeed", "BaseDamage", "AvgRolledDamage", "AvgVariance", "AvgDamageMod", "AvgElementalBonus", "Ammo", "ExtraMutationRate" };
            foreach (var (name, _) in PowerSettings)
                header.AddRange(new[] { $"{name}_CycleSec", $"{name}_StrikesPerAttack", $"{name}_DamagePerStrike", $"{name}_CritRate", $"{name}_DirectDPS", $"{name}_BleedDPS", $"{name}_DPS" });
            header.AddRange(new[] { "full_DPS_High", "full_DPS_Medium", "full_DPS_Low",
                "full_ClassMedianDPS", "full_VsClassMedianPct", "full_RankInClass", "fast_ClassMedianDPS", "fast_VsClassMedianPct", "fast_RankInClass",
                "full_VsPlainVariantPct", "MissRerollRate", "Failures", "Flags" });
            sb.AppendLine(string.Join(",", header));

            foreach (var r in results)
            {
                var classmates = results.Where(x => x.Tier == r.Tier && x.Entry.Class == r.Entry.Class).ToList();
                var plain = results.FirstOrDefault(x => x.Tier == r.Tier && x.Entry.Class == r.Entry.Class && x.Entry.Family == r.Entry.Family && x.Entry.FamilyIndex == 0);
                var fullMedian = Median(classmates.Select(x => x.Dps(2)));
                var fastMedian = Median(classmates.Select(x => x.Dps(0)));
                var fullVs = fullMedian > 0 ? (r.Dps(2) / fullMedian - 1) * 100 : 0;
                var fastVs = fastMedian > 0 ? (r.Dps(0) / fastMedian - 1) * 100 : 0;
                var vsPlain = plain != null && plain != r && plain.Dps(2) > 0 ? (r.Dps(2) / plain.Dps(2) - 1) * 100 : 0;
                var swings = r.Power.Sum(s => s.Strikes);
                var failures = r.Power.Sum(s => s.Failures);

                var flags = new List<string>();
                if (Math.Abs(fullVs) >= 25) flags.Add(fullVs > 0 ? "HIGH full" : "LOW full");
                if (Math.Abs(fastVs) >= 25) flags.Add(fastVs > 0 ? "HIGH fast" : "LOW fast");
                if (Math.Abs(vsPlain) >= 5) flags.Add("element differs from plain");
                if (failures > 0) flags.Add("failures");

                var row = new List<string> { r.Tier.ToString(), r.Level.ToString(), r.Entry.Class, r.Entry.Family, IsElemental(r.Element) ? "element" : "plain", r.Entry.Wcid.ToString(),
                    Csv(r.Name), r.WeaponType, Csv(r.Element), r.MultiStrike ? "yes" : "no", r.Bleeds ? "yes" : "no",
                    r.BaseSpeed.ToString(), F(r.RolledSpeed, "0.#"), r.BaseDamage.ToString(), F(r.RolledDamage, "0.##"), F(r.Variance), F(r.DamageMod), F(r.ElementalBonus, "0.##"), Csv(r.Ammo), F(r.AcxMutated) };
                for (var p = 0; p < PowerSettings.Length; p++)
                {
                    var s = r.Power[p];
                    row.Add(F(s.Swings > 0 ? s.Time / s.Swings : 0));
                    row.Add(F(s.Swings > 0 ? s.Strikes / s.Swings : 0, "0.##"));
                    row.Add(F(s.Strikes > 0 ? s.Direct / s.Strikes : 0, "0.##"));
                    row.Add(F(s.Strikes > 0 ? s.Crits / s.Strikes : 0));
                    row.Add(F(s.Time > 0 ? s.Direct / s.Time : 0, "0.##"));
                    row.Add(F(s.Time > 0 ? s.Bleed / s.Time : 0, "0.##"));
                    row.Add(F(r.Dps(p), "0.##"));
                }
                row.AddRange(r.FullByHeight.Select(d => F(d, "0.##")));
                row.Add(F(fullMedian, "0.##"));
                row.Add(F(fullVs, "0.#"));
                row.Add((classmates.Count(x => x.Dps(2) > r.Dps(2)) + 1).ToString());
                row.Add(F(fastMedian, "0.##"));
                row.Add(F(fastVs, "0.#"));
                row.Add((classmates.Count(x => x.Dps(0) > r.Dps(0)) + 1).ToString());
                row.Add(F(vsPlain, "0.#"));
                // misses mode: the share of full-power/full-accuracy strikes that missed; otherwise the re-rolled evade share
                var full = r.Power[PowerSettings.Length - 1];
                row.Add(F(countMisses ? (full.Strikes > 0 ? full.Evades / full.Strikes : 0)
                    : swings > 0 ? r.Power.Sum(s => s.Evades) / (swings + r.Power.Sum(s => s.Evades)) : 0));
                row.Add(F(failures, "0"));
                row.Add(Csv(string.Join("; ", flags)));
                sb.AppendLine(string.Join(",", row));
            }
            return sb.ToString();
        }

        private static bool IsElemental(string damageType) => damageType != null && (damageType.Contains("Acid") || damageType.Contains("Fire") || damageType.Contains("Cold") || damageType.Contains("Electric"));

        private static string Csv(string text) => text != null && (text.Contains(',') || text.Contains('"')) ? $"\"{text.Replace("\"", "\"\"")}\"" : text ?? "";

        private static double Median(IEnumerable<double> values)
        {
            var list = values.OrderBy(v => v).ToList();
            if (list.Count == 0)
                return 0;
            return list.Count % 2 == 1 ? list[list.Count / 2] : (list[list.Count / 2 - 1] + list[list.Count / 2]) / 2;
        }

        private static string Summary(List<Result> results)
        {
            var sb = new StringBuilder();
            foreach (var group in results.GroupBy(r => (r.Tier, r.Entry.Class)))
            {
                var plain = group.Where(r => r.Entry.FamilyIndex == 0).OrderByDescending(r => r.Dps(2)).ToList();
                var median = Median(plain.Select(r => r.Dps(2)));
                sb.AppendLine($"T{group.Key.Tier} {group.Key.Class}: median full-swing DPS {median:0.0}; best {plain.First().Name} {plain.First().Dps(2):0.0}, worst {plain.Last().Name} {plain.Last().Dps(2):0.0}.");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
