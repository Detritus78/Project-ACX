using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using ACE.Common;
using ACE.Common.Extensions;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.DatLoader;
using ACE.DatLoader.Entity.AnimationHooks;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Mutations;
using ACE.Server.Factories;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.Physics.Animation;
using ACE.Server.WorldObjects;

using Spell = ACE.Server.Entity.Spell;
using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: end-game survival bench (JJ 2026-11-16: "a bench with end game mobs to see if they are killable with our best
    /// loot currently realistically or will a player die everytime ... skills and attribute spending like endurance"; "theres
    /// skill involved in the game too ... We dont want to remove skill from the game", so this is a straight toe-to-toe fight).
    ///
    /// A character per level and build: the level's total XP spent on attributes, vitals and skills with the DAT cost tables, in
    /// a written priority order (SurvivalBuilds), on End of Retail templates (innate 380 with the 10 attribute augmentations),
    /// XP augmentations where the XP reaches, luminance auras scaled from none at 200 to all at 275. Gear: median loot armor of
    /// the level's tier with 10 Steel, a baggy shirt and pants, an optional tower shield, the level's median loot weapon tinked
    /// the game's way (melee: rend + iron/granite + transfers; bow: rend + mahogany + transfers; caster: rend + green garnet), full
    /// Incantation self buffs cast through the enchantment registry (Impenetrability and banes on every worn piece).
    ///
    /// Each fight runs on a timeline: the player attacks at its real attack cycle and hit chance (DamageEvent with the monster's
    /// real defenses); the monster attacks the way Monster_Combat picks (spell roll from its spellbook, else melee or missile),
    /// at its real animation timing and delays, through DamageEvent (melee, missile) and SpellProjectile.CalculateDamage (war,
    /// void, life projectiles), with drains, harms, debuffs and DoTs applied to the player; pets fight beside mages and archers
    /// and the monster re-picks its target every 5 seconds by its targeting tactic. Vitals regenerate every 5 seconds by the
    /// server's regen formula. Out: Logs/acxbench/acxsurvival-*.csv (one row per level, build, monster, healing mode) and
    /// acxsurvival-builds-*.csv (the character sheets). /acxsurvival in ACXBenchCommands.cs.
    /// </summary>
    public static partial class AcxBench
    {
        public class SurvivalOptions
        {
            public List<int> Levels = new() { 200, 240, 275 };
            public List<string> Builds = new() { "melee", "meleeshield", "archer", "mage", "twohand", "dualwield" };
            public int Fights = 20;
            public double MaxSeconds = 600;
            public double HealAt = 0.55;      // a health kit when health drops under this share
            public List<uint> Monsters;      // null: the default end-game list
            public bool Elites = true;
            public bool Pets = true;
            public int LumLevel = -1;        // -1: by level (none at 200, all at 275); else 0..100 percent of every aura
            public List<string> Profiles = new() { "fresh", "veteran" };   // fresh: the level's XP exactly; veteran: maxed (survivalVeteran)
            public int Gear;                 // 0 base, 1 + armor set, 2 + aetheria, 3 + cloak and trinket
            public string ArmorSet;          // an EquipmentSet name for every build (comparison runs), else each build's own
            public string Aetheria = "defense";
            public string Trinket;           // null: each build's own
            public string MeleeKind;         // "Heavy.Schlagers": the melee builds' loot weapon kind (and skill); null: Long Swords
            public int LootTier;             // tier=N: the gear of loot tier N at every level (0: the level's)
            public string BombImbue;         // bombimbue=rend|cs|cb|ar: an imbue on the Alchemist's bombs (null: none, as the game allows)
            public bool ThrownShield;        // shield=1: the Alchemist and the thrown build carry a shield (a thrown weapon leaves a hand free)
        }

        /// <summary>
        /// A thrown-weapon build with a shield in its other hand (JJ 2026-11-16: "We have not considered a shield or any kind of
        /// defense modifiers like sword single hand"): Shield trained and raised as the sword-and-shield build has it
        /// </summary>
        private static SurvivalBuild WithShield(SurvivalBuild b)
        {
            if (b.Shield || b.Name != "alchemist" && b.Name != "thrown")
                return b;
            var spend = b.Spend.ToList();
            spend.Insert(spend.IndexOf("augs") + 1, "s:Shield");
            return new SurvivalBuild
            {
                Name = b.Name, Source = b.Source + " + a shield", Style = b.Style, Shield = true, Pet = b.Pet,
                Str = b.Str, End = b.End, Coord = b.Coord, Quick = b.Quick, Focus = b.Focus, Self = b.Self,
                Specialized = b.Specialized, Trained = b.Trained.Append(Skill.Shield).Distinct().ToArray(), Spend = spend.ToArray(), SkilledAug = b.SkilledAug,
            };
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Builds (End of Retail templates, adapted; JJ can reorder the Spend lists)
        // ---------------------------------------------------------------------------------------------------------------

        private class SurvivalBuild
        {
            public string Name, Source, Style;          // Style: melee, missile, magic
            public bool Shield, Pet;
            public int Str, End, Coord, Quick, Focus, Self;
            public Skill[] Specialized, Trained;
            public string[] Spend;                      // a:attribute v:vital s:skill, "augs" = the XP augmentations
            public Skill SkilledAug;                    // which "Master of ..." augmentation (melee / missile / magic)
        }

        private static readonly SurvivalBuild[] SurvivalBuilds =
        {
            new SurvivalBuild
            {
                Name = "melee", Style = "melee", Source = "Covenant Warrior (loresraat) + 10 attribute augs into Endurance",
                Str = 100, End = 60, Coord = 100, Quick = 100, Focus = 10, Self = 10,
                Specialized = new[] { Skill.HeavyWeapons, Skill.MeleeDefense, Skill.MissileDefense, Skill.MagicDefense },
                Trained = new[] { Skill.Healing, Skill.Shield, Skill.ManaConversion, Skill.LifeMagic, Skill.CreatureEnchantment, Skill.ItemEnchantment },
                Spend = new[] { "s:HeavyWeapons", "s:MeleeDefense", "v:Health", "a:Endurance", "a:Strength", "a:Coordination", "a:Quickness", "s:MagicDefense", "s:MissileDefense",
                    "augs", "s:Healing", "s:Shield", "v:Stamina", "a:Self", "a:Focus", "v:Mana", "s:LifeMagic", "s:CreatureEnchantment", "s:ItemEnchantment", "s:ManaConversion" },
                SkilledAug = Skill.HeavyWeapons,
            },
            new SurvivalBuild
            {
                Name = "meleeshield", Style = "melee", Shield = true, Source = "as melee, with a tower shield",
                Str = 100, End = 60, Coord = 100, Quick = 100, Focus = 10, Self = 10,
                Specialized = new[] { Skill.HeavyWeapons, Skill.MeleeDefense, Skill.MissileDefense, Skill.MagicDefense },
                Trained = new[] { Skill.Healing, Skill.Shield, Skill.ManaConversion, Skill.LifeMagic, Skill.CreatureEnchantment, Skill.ItemEnchantment },
                Spend = new[] { "s:HeavyWeapons", "s:MeleeDefense", "v:Health", "a:Endurance", "a:Strength", "a:Coordination", "a:Quickness", "s:MagicDefense", "s:MissileDefense",
                    "augs", "s:Shield", "s:Healing", "v:Stamina", "a:Self", "a:Focus", "v:Mana", "s:LifeMagic", "s:CreatureEnchantment", "s:ItemEnchantment", "s:ManaConversion" },
                SkilledAug = Skill.HeavyWeapons,
            },
            new SurvivalBuild
            {
                Name = "archer", Style = "missile", Pet = true, Source = "Summoning Archer \"Klein\" (loresraat): 10/100/100/100/10/60",
                Str = 10, End = 100, Coord = 100, Quick = 100, Focus = 10, Self = 60,
                Specialized = new[] { Skill.MissileWeapons, Skill.MagicDefense, Skill.MeleeDefense, Skill.Summoning, Skill.Recklessness },
                Trained = new[] { Skill.Healing, Skill.ManaConversion, Skill.LifeMagic, Skill.CreatureEnchantment, Skill.ItemEnchantment },
                Spend = new[] { "s:MissileWeapons", "s:MeleeDefense", "v:Health", "a:Endurance", "a:Coordination", "a:Quickness", "s:MagicDefense", "s:Summoning", "a:Self",
                    "augs", "s:Healing", "v:Stamina", "v:Mana", "a:Focus", "a:Strength", "s:LifeMagic", "s:CreatureEnchantment", "s:ItemEnchantment", "s:ManaConversion", "s:Recklessness" },
                SkilledAug = Skill.MissileWeapons,
            },
            new SurvivalBuild
            {
                Name = "mage", Style = "magic", Pet = true, Source = "Tugaker Mage (loresraat) with JJ's notes: Endurance heavy, spec Melee Defense, Summoning",
                Str = 10, End = 100, Coord = 60, Quick = 10, Focus = 100, Self = 100,
                Specialized = new[] { Skill.WarMagic, Skill.MagicDefense, Skill.MeleeDefense, Skill.Summoning },
                Trained = new[] { Skill.Healing, Skill.ManaConversion, Skill.LifeMagic, Skill.CreatureEnchantment, Skill.ItemEnchantment },
                Spend = new[] { "s:WarMagic", "s:MagicDefense", "s:MeleeDefense", "v:Health", "a:Endurance", "a:Self", "a:Focus", "v:Mana", "s:Summoning", "a:Coordination",
                    "s:ManaConversion", "augs", "s:Healing", "v:Stamina", "a:Quickness", "a:Strength", "s:LifeMagic", "s:CreatureEnchantment", "s:ItemEnchantment" },
                SkilledAug = Skill.WarMagic,
            },
            new SurvivalBuild
            {
                Name = "twohand", Style = "melee", Source = "melee template (StrategyWiki General Tank / Covenant Warrior) with Two Handed Combat as the weapon skill",
                Str = 100, End = 60, Coord = 100, Quick = 100, Focus = 10, Self = 10,
                Specialized = new[] { Skill.TwoHandedCombat, Skill.MeleeDefense, Skill.MissileDefense, Skill.MagicDefense },
                Trained = new[] { Skill.Healing, Skill.ManaConversion, Skill.LifeMagic, Skill.CreatureEnchantment, Skill.ItemEnchantment },
                Spend = new[] { "s:TwoHandedCombat", "s:MeleeDefense", "v:Health", "a:Endurance", "a:Strength", "a:Coordination", "a:Quickness", "s:MagicDefense", "s:MissileDefense",
                    "augs", "s:Healing", "v:Stamina", "a:Self", "a:Focus", "v:Mana", "s:LifeMagic", "s:CreatureEnchantment", "s:ItemEnchantment", "s:ManaConversion" },
                SkilledAug = Skill.TwoHandedCombat,
            },
            new SurvivalBuild
            {
                Name = "dualwield", Style = "melee", Source = "melee template with Dual Wield specialized (loresraat Dual Wield: (Coord + Coord) / 3 since 2014; 70 credits max specialized, so Missile Defense trained)",
                Str = 100, End = 60, Coord = 100, Quick = 100, Focus = 10, Self = 10,
                Specialized = new[] { Skill.HeavyWeapons, Skill.DualWield, Skill.MeleeDefense, Skill.MagicDefense },
                Trained = new[] { Skill.MissileDefense, Skill.Healing, Skill.ManaConversion, Skill.LifeMagic, Skill.CreatureEnchantment, Skill.ItemEnchantment },
                Spend = new[] { "s:HeavyWeapons", "s:DualWield", "s:MeleeDefense", "v:Health", "a:Endurance", "a:Strength", "a:Coordination", "a:Quickness", "s:MagicDefense", "s:MissileDefense",
                    "augs", "s:Healing", "v:Stamina", "a:Self", "a:Focus", "v:Mana", "s:LifeMagic", "s:CreatureEnchantment", "s:ItemEnchantment", "s:ManaConversion" },
                SkilledAug = Skill.HeavyWeapons,
            },
            // 2026-11-16 (JJ: check whether the Alchemist's level 100 edge is retail's atlatl-vs-bow balance): the archer's skills with an
            // atlatl + darts, or a plain Eternal thrown weapon (AcxThrownWcids); both take their damage bonus from Strength
            // (Creature.GetAttributeMod: only bows and crossbows read Coordination), so Strength is raised in place of Endurance and Self
            new SurvivalBuild
            {
                Name = "atlatl", Style = "missile", Pet = true, Source = "the archer's skills with an atlatl + darts; Strength-based (atlatl damage reads Strength): 100/60/100/100/10/10",
                Str = 100, End = 60, Coord = 100, Quick = 100, Focus = 10, Self = 10,
                Specialized = new[] { Skill.MissileWeapons, Skill.MagicDefense, Skill.MeleeDefense, Skill.Summoning, Skill.Recklessness },
                Trained = new[] { Skill.Healing, Skill.ManaConversion, Skill.LifeMagic, Skill.CreatureEnchantment, Skill.ItemEnchantment },
                Spend = new[] { "s:MissileWeapons", "s:MeleeDefense", "v:Health", "a:Endurance", "a:Coordination", "a:Strength", "a:Quickness", "s:MagicDefense", "s:Summoning", "a:Self",
                    "augs", "s:Healing", "v:Stamina", "v:Mana", "a:Focus", "s:LifeMagic", "s:CreatureEnchantment", "s:ItemEnchantment", "s:ManaConversion", "s:Recklessness" },
                SkilledAug = Skill.MissileWeapons,
            },
            new SurvivalBuild
            {
                Name = "thrown", Style = "missile", Pet = true, Source = "the archer's skills with a plain Eternal thrown weapon (Fable's, no bombs); Strength-based: 100/60/100/100/10/10",
                Str = 100, End = 60, Coord = 100, Quick = 100, Focus = 10, Self = 10,
                Specialized = new[] { Skill.MissileWeapons, Skill.MagicDefense, Skill.MeleeDefense, Skill.Summoning, Skill.Recklessness },
                Trained = new[] { Skill.Healing, Skill.ManaConversion, Skill.LifeMagic, Skill.CreatureEnchantment, Skill.ItemEnchantment },
                Spend = new[] { "s:MissileWeapons", "s:MeleeDefense", "v:Health", "a:Endurance", "a:Coordination", "a:Strength", "a:Quickness", "s:MagicDefense", "s:Summoning", "a:Self",
                    "augs", "s:Healing", "v:Stamina", "v:Mana", "a:Focus", "s:LifeMagic", "s:CreatureEnchantment", "s:ItemEnchantment", "s:ManaConversion", "s:Recklessness" },
                SkilledAug = Skill.MissileWeapons,
            },
            new SurvivalBuild
            {
                // Project ACX 2026-11-16: the Alchemist (AcxBombWcids): specialized Alchemy is its attack skill and its blast's power,
                // (Coordination + Focus) / 3, so Coordination, Focus and Quickness (throw speed, defenses), then Endurance; no Strength
                Name = "alchemist", Style = "missile", Source = "Alchemist (ACX bombs): spec Alchemy, Coordination / Focus / Quickness, Endurance (EoR-style missile template)",
                Str = 10, End = 60, Coord = 100, Quick = 100, Focus = 100, Self = 10,
                Specialized = new[] { Skill.Alchemy, Skill.MeleeDefense, Skill.MagicDefense },
                Trained = new[] { Skill.MissileDefense, Skill.Healing, Skill.ManaConversion, Skill.LifeMagic, Skill.CreatureEnchantment, Skill.ItemEnchantment },
                Spend = new[] { "s:Alchemy", "s:MeleeDefense", "v:Health", "a:Endurance", "a:Coordination", "a:Focus", "a:Quickness", "s:MagicDefense", "s:MissileDefense",
                    "augs", "s:Healing", "v:Stamina", "a:Self", "v:Mana", "a:Strength", "s:LifeMagic", "s:CreatureEnchantment", "s:ItemEnchantment", "s:ManaConversion" },
                SkilledAug = Skill.MissileWeapons,
            },
            new SurvivalBuild
            {
                Name = "dualmix", Style = "melee", Source = "as dualwield, with two different loot swords: two damage types, each with its own rend, both weapons' spells (the best pair per monster)",
                Str = 100, End = 60, Coord = 100, Quick = 100, Focus = 10, Self = 10,
                Specialized = new[] { Skill.HeavyWeapons, Skill.DualWield, Skill.MeleeDefense, Skill.MagicDefense },
                Trained = new[] { Skill.MissileDefense, Skill.Healing, Skill.ManaConversion, Skill.LifeMagic, Skill.CreatureEnchantment, Skill.ItemEnchantment },
                Spend = new[] { "s:HeavyWeapons", "s:DualWield", "s:MeleeDefense", "v:Health", "a:Endurance", "a:Strength", "a:Coordination", "a:Quickness", "s:MagicDefense", "s:MissileDefense",
                    "augs", "s:Healing", "v:Stamina", "a:Self", "a:Focus", "v:Mana", "s:LifeMagic", "s:CreatureEnchantment", "s:ItemEnchantment", "s:ManaConversion" },
                SkilledAug = Skill.HeavyWeapons,
            },
        };

        // the XP augmentations an End of Retail character buys (AugmentationDevice weenies; cost read from the device), in order
        private static readonly (uint Wcid, PropertyInt Prop, string Name)[] SurvivalAugs =
        {
            (41480, PropertyInt.AugmentationDamageReduction, "Iron Skin of the Invincible"),
            (41478, PropertyInt.AugmentationDamageBonus, "Frenzy of the Slayer"),
            (0, PropertyInt.AugmentationSkilledMelee, "Master of the ..."),   // the build's skill group, filled in below
            (29269, PropertyInt.AugmentationCriticalDefense, "Critical Protection"),
            (43167, PropertyInt.AugmentationJackOfAllTrades, "Jack of All Trades"),
            (41482, PropertyInt.AugmentationCriticalExpertise, "Eye of the Remorseless"),
            (41481, PropertyInt.AugmentationCriticalPower, "Hand of the Remorseless"),
        };

        // luminance auras (Seer), at most
        private static readonly (PropertyInt Prop, int Max)[] SurvivalLumAuras =
        {
            (PropertyInt.LumAugDamageRating, 5), (PropertyInt.LumAugDamageReductionRating, 5), (PropertyInt.LumAugCritDamageRating, 5),
            (PropertyInt.LumAugCritReductionRating, 5), (PropertyInt.LumAugAllSkills, 10), (PropertyInt.LumAugSkilledSpec, 5),
        };

        // ---------------------------------------------------------------------------------------------------------------
        // Monsters: the highest-level kinds that spawn in numbers (Preview's world DB, 2026-11-16: placed or on a placed generator,
        // 20+ spawn spots), one per name, 240-300; bosses (1-4 spots) left out. Elites: the two highest invasion elites.
        // ---------------------------------------------------------------------------------------------------------------

        private static readonly uint[] SurvivalMonsters =
        {
            51760, 51758, 52583, 52585, 52587, 53348, 53347, 87848, 44031, 44033,   // L300: Rynthid Sorcerer, Raging Rynthid Sorcerer, A'nekshen Storm Caller / Storm Reaver / Thorn Dancer, Vermilion War / Emerald Hunting Gromnie, Mu-miyah Lord, Dust Golem, Burning Sands Golem
            35160,                                                                  // L285: Annihilator
            46539, 46511, 46688, 51756, 51752, 52519, 52616, 44054,                 // L265: Spectral Samurai / Bloodmage / Voidmage, Rynthid Slayer / Rager, Zefir Thorn Stalker, Brier Wasp, War Reaper
            46503, 51750, 51215,                                                    // L240: Spectral Archer, Rynthid Minion, Olthoi Hive Warrior
        };
        private static readonly uint[] SurvivalElites = { 48774, 72803 };          // Spirit of Protection (Wai Jhou, 250), Sclavus Sentinel (Stonehold, 225)

        // Incantation (level VIII) self buffs
        private static readonly uint[] SurvivalCreatureBuffs = { 4325, 4299, 4297, 4319, 4305, 4329, 4560, 4558, 4596, 4556, 4602 };   // Str End Coord Quick Focus Willpower, Invulnerability Impregnability Magic Resistance, Healing / Mana Conversion Mastery
        private static readonly Dictionary<Skill, uint[]> SurvivalMasteries = new()
        {
            { Skill.HeavyWeapons, new uint[] { 4624 } }, { Skill.MissileWeapons, new uint[] { 4628, 4522, 4534 } }, { Skill.WarMagic, new uint[] { 4638 } },
            { Skill.Shield, new uint[] { 5858 } }, { Skill.Summoning, new uint[] { 6123 } }, { Skill.DualWield, new uint[] { 5810 } },
            { Skill.Alchemy, new uint[] { 4506 } },   // Incantation of Alchemy Mastery Self
        };
        private static readonly uint[] SurvivalLifeBuffs = { 4291, 4460, 4462, 4464, 4466, 4468, 4470, 4472, 4496, 4498, 4494 };   // Armor Self, 7 protections, Regeneration, Rejuvenation, Mana Renewal
        private static readonly uint[] SurvivalArmorBuffs = { 4407, 4391, 4393, 4397, 4401, 4403, 4409, 4412 };                   // Impenetrability, 7 banes
        private static readonly uint[] SurvivalWeaponAuras = { 4395, 4417, 4405, 4400 };   // Blood Drinker, Swift Killer, Heart Seeker, Defender
        private static readonly uint[] SurvivalCasterAuras = { 4414, 4418, 4400 };         // Spirit Drinker, Hermetic Link, Defender

        // the war bolt per element (Incantation)
        private static readonly Dictionary<DamageType, uint> SurvivalBolts = new()
        {
            { DamageType.Fire, 4439 }, { DamageType.Cold, 4447 }, { DamageType.Electric, 4451 }, { DamageType.Acid, 4433 },
            { DamageType.Pierce, 4443 }, { DamageType.Slash, 4457 }, { DamageType.Bludgeon, 4455 },
        };

        // level 200 summons (570 Summoning) and level 180 (530): Skeleton Samurai / Bushi in four elements (pet weenies)
        private static readonly uint[] SurvivalPets200 = { 49170, 49177, 48955, 49163 };
        private static readonly uint[] SurvivalPets180 = { 49169, 48949 };

        private const uint TreatedHealingKit = 9229, EternalStaminaKit = 30249, EternalManaKit = 30248;
        private const uint SteelMutation = 0x38000011, GreenGarnetMutation = 0x3800004B;

        // fixed guids for the bench objects, never registered (the existing benches use 0xFFFFFFF0-F4)
        private const uint SurvivalGuidBase = 0xFFFFFE00;
        private const uint SurvivalMonsterGuid = 0xFFFFFDF0, SurvivalPetGuid = 0xFFFFFDF1, SurvivalProjectileGuid = 0xFFFFFDF2, SurvivalKitGuid = 0xFFFFFDF3;

        private static readonly PropertyInfo CurrentSpellProperty = typeof(Creature).GetProperty("CurrentSpell", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo CombatModeSetter = typeof(Creature).GetProperty("CombatMode").GetSetMethod(true);
        private static readonly MethodInfo DrainResistMethod = typeof(WorldObject).GetMethod("GetDrainResistanceType", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(PropertyAttribute2nd) }, null);
        private static readonly MethodInfo BoostResistVitalMethod = typeof(WorldObject).GetMethod("GetBoostResistanceType", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(PropertyAttribute2nd) }, null);
        private static readonly MethodInfo DrainResistTypeMethod = typeof(WorldObject).GetMethod("GetDrainResistanceType", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(DamageType) }, null);
        private static readonly MethodInfo BoostResistTypeMethod = typeof(WorldObject).GetMethod("GetBoostResistanceType", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(DamageType) }, null);

        private static SpellId[] EliteSpells(string field) => (SpellId[])typeof(Creature).GetField(field, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        public static string StartSurvival(SurvivalOptions options, Action<string> reply)
        {
            if (running)
                return "A bench is already running.";
            running = true;

            Task.Run(() =>
            {
                try
                {
                    reply(RunSurvival(options));
                }
                catch (Exception e)
                {
                    log.Error($"AcxBench survival failed: {e}");
                    reply($"The survival bench failed: {e.Message}");
                }
                finally
                {
                    running = false;
                }
            });
            return $"Survival bench started: levels {string.Join(",", options.Levels)}, builds {string.Join(",", options.Builds)}, {options.Fights} fights per monster and mode. Results go to Logs/acxbench/.";
        }

        // ---------------------------------------------------------------------------------------------------------------
        // The character
        // ---------------------------------------------------------------------------------------------------------------

        private class Sheet
        {
            public string Build;
            public int Level;
            public long XpTotal, XpLeft;
            public int CreditsTotal, CreditsUsed;
            public List<string> Raised = new(), Augs = new();
            public int Lum;
            public string Stats = "";
        }

        /// <summary>
        /// A fresh character of the build at the level: innate attributes, trained and specialized skills, the level's XP spent in the
        /// build's order with the DAT cost tables, XP augmentations bought where the XP reaches, luminance auras
        /// </summary>
        // the profile the next characters are made for: false = "fresh" (exactly the level's XP and credits), true = "maxed veteran"
        // (coordinator / JJ 2026-11-16: XP past the cap spent to every rank cap, every XP aug, every luminance aura, the extra skill
        // credits ACX's data offers, a Legendary cantrip set)
        [ThreadStatic] private static bool survivalVeteran;

        // the melee builds' weapon kind ("Heavy.Schlagers"); null: Long Swords
        [ThreadStatic] private static string survivalMeleeKind;
        private static (Type Table, Skill Skill)? MeleeClass(string kind) => kind?.Split('.')[0].ToLowerInvariant() switch
        {
            "heavy" => (typeof(HeavyWeaponWcids), Skill.HeavyWeapons), "light" => (typeof(LightWeaponWcids), Skill.LightWeapons), "finesse" => (typeof(FinesseWeaponWcids), Skill.FinesseWeapons), _ => null,
        };

        // AwardTrainingCredits in the world DB: Huntsman of Silyun (Chasing Oswald) 1, Hea Arantah (figurine) 1, Nalicana 2 (bought with
        // luminance); the WEventCoordinator's (an event / admin NPC) left out
        private const int VeteranExtraCredits = 4;

        // The gear a character of a level realistically has (JJ 2026-11-16: "if you bench you need to bench with the right gear for the
        // right level. at level 180 set pieces open up but most players don't get it right away ... I would suggest set pieces from
        // level 250 and up to 275. 180-250 will likely have legendary gear pieces ... 126-180 will likely be in epic gear. At 180
        // likely mostly epic"): cantrips are Major to level 125, Epic for 126-180 and Legendary from 181, on what is worn (every
        // profile) and in the weapon's transfers (TransferReal); armor sets from level 250 (DressExtras).
        // 0: no band (the gear bench: Legendary, as before)
        [ThreadStatic] private static int survivalBandLevel;
        private const int EpicFromLevel = 126, LegendaryFromLevel = 181, SetsFromLevel = 250;

        // a Legendary cantrip's Epic and Major versions (the world DB's spells, by name)
        private static readonly Dictionary<uint, (uint Epic, uint Major)> CantripGrades = new()
        {
            { 6040, (4683, 2501) }, { 6043, (4686, 2504) }, { 6044, (4687, 2505) }, { 6047, (4691, 2509) }, { 6053, (4694, 2513) }, { 6054, (4695, 2514) },
            { 6055, (4696, 2515) }, { 6063, (4704, 2524) }, { 6064, (4705, 2525) }, { 6069, (5896, 5891) }, { 6072, (4712, 2531) }, { 6073, (5034, 5070) },
            { 6075, (4715, 2534) }, { 6089, (4661, 2586) }, { 6094, (4666, 2591) }, { 6101, (4227, 2577) }, { 6102, (4911, 2571) }, { 6103, (3963, 2572) },
            { 6104, (4226, 2573) }, { 6105, (3964, 2574) }, { 6106, (4019, 2575) }, { 6107, (3965, 2576) }, { 6125, (6124, 6126) },
        };

        // The class bench's band (JJ 2026-11-19, a level 15 weapon benched with Legendary transfers: "No weapon at that range woudl
        // have a legendary transfer. It actually wouldnt have any", "make sure the bench is comparing apples to apple"): the
        // transfers on both sides, the item and its loot reference, are of the grade loot of the level carries (NumCantrips: tiers
        // 1-2 seldom one and only Minor, Minor through tier 4): none below level 50, Minor 50-114, Major 115-125, then the band
        // above. Set for the class bench's thread only (RunClassBench); the survival bench keeps its own band.
        [ThreadStatic] private static int classBandLevel;
        private const int MinorFromLevel = 50, MajorFromLevel = 115;

        // a Legendary damage cantrip's Minor version: Blood Thirst, Strength, Coordination, Quickness
        private static readonly Dictionary<uint, uint> MinorGrades = new() { { 6089, 2598 }, { 6107, 2583 }, { 6103, 2579 }, { 6106, 2582 } };

        // 0: no transfer at this level (TransferReal skips it)
        private static uint BandCantrip(uint legendary)
        {
            var band = survivalBandLevel > 0 ? survivalBandLevel : classBandLevel;
            if (band <= 0 || band >= LegendaryFromLevel || !CantripGrades.TryGetValue(legendary, out var grade))
                return legendary;
            if (survivalBandLevel <= 0 && band < MinorFromLevel)
                return 0;
            if (survivalBandLevel <= 0 && band < MajorFromLevel)
                return MinorGrades.TryGetValue(legendary, out var minor) ? minor : grade.Major;
            return band >= EpicFromLevel ? grade.Epic : grade.Major;
        }

        // Blood Thirst's damage in the band: Legendary 10, Epic 7, Major 4, Minor 2, none 0
        private static int BandBloodThirst => BandCantrip(LegendaryBloodThirst) switch { 0 => 0, 2598 => 2, 2586 => 4, 4661 => 7, _ => 10 };

        // what the band puts in a weapon's transfer slots, for the bench's output
        private static string BandTransferGrade(int level) => level < MinorFromLevel ? "none" : level < MajorFromLevel ? "Minor" : level < EpicFromLevel ? "Major" : level < LegendaryFromLevel ? "Epic" : "Legendary";

        // a veteran's worn Legendary cantrips (+35 to a skill or attribute, +80 armor), as items' spells
        private static readonly uint[] VeteranCantripsAll = { 6055, 6054, 6063, 6104, 6053, 6102 };   // Invulnerability, Impregnability, Magic Resistance, Endurance, Healing Prowess, Armor
        private static readonly Dictionary<string, uint[]> VeteranCantripsStyle = new()
        {
            { "melee", new uint[] { 6107, 6103, 6106 } },          // Strength, Coordination, Quickness (+ the weapon skill's Aptitude, below)
            { "missile", new uint[] { 6103, 6106, 6101, 6125 } },  // Coordination, Quickness, Willpower, Summoning Prowess
            { "magic", new uint[] { 6105, 6101, 6125, 6064 } },    // Focus, Willpower, Summoning Prowess, Mana Conversion Prowess
            { "alchemist", new uint[] { 6103, 6105, 6106, 6101 } }, // by build name: Coordination, Focus, Quickness, Willpower
            { "atlatl", new uint[] { 6107, 6103, 6106, 6101 } },    // Strength, Coordination, Quickness, Willpower
            { "thrown", new uint[] { 6107, 6103, 6106, 6101 } },
        };
        private static readonly Dictionary<Skill, uint> VeteranAptitudes = new()
        {
            { Skill.HeavyWeapons, 6072 }, { Skill.LightWeapons, 6043 }, { Skill.FinesseWeapons, 6047 }, { Skill.TwoHandedCombat, 6073 },
            { Skill.MissileWeapons, 6044 }, { Skill.WarMagic, 6075 }, { Skill.Shield, 6069 },
            { Skill.Alchemy, 6040 },   // Legendary Alchemical Prowess
        };

        private static Player CreateSurvivor(SurvivalBuild b, int level, int lumPercent, Sheet sheet)
        {
            var p = CreateAttacker(level, new Options { Str = b.Str, Coord = b.Coord, Quick = b.Quick, End = b.End, Focus = b.Focus, Self = b.Self });
            survivalBandLevel = level;
            foreach (var s in new[] { Skill.HeavyWeapons, Skill.LightWeapons, Skill.FinesseWeapons, Skill.TwoHandedCombat, Skill.MissileWeapons })
            {
                var cs = p.GetCreatureSkill(s);
                cs.AdvancementClass = SkillAdvancementClass.Untrained;
                cs.InitLevel = 0;
                cs.Ranks = 0;
            }
            var skillTable = DatManager.PortalDat.SkillTable.SkillBaseHash;
            var table0 = DatManager.PortalDat.XpTable;
            var available = (DatManager.PortalDat.CharGen.HeritageGroups.TryGetValue((uint)HeritageGroup.Aluvian, out var heritage0) ? (int)heritage0.SkillCredits : 50)
                + Enumerable.Range(1, Math.Min(level, table0.CharacterLevelSkillCreditList.Count - 1)).Sum(i => (int)table0.CharacterLevelSkillCreditList[i])
                + (survivalVeteran ? VeteranExtraCredits : 0);
            int Cost(Skill s, bool spec) => skillTable.TryGetValue((uint)s, out var sb0) ? (spec ? sb0.SpecializedCost : sb0.TrainedCost) : 0;
            // a build that asks for more skill credits than the level gives drops trained skills from the end of its list (least needed last)
            var trainedList = b.Trained.ToList();
            while (trainedList.Count > 0 && b.Specialized.Sum(s => Cost(s, true)) + trainedList.Sum(s => Cost(s, false)) > available)
            {
                sheet.Augs.Add($"(not trained for lack of credits: {trainedList[^1]})");
                trainedList.RemoveAt(trainedList.Count - 1);
            }
            var credits = 0;
            foreach (var s in trainedList)
            {
                var cs = p.GetCreatureSkill(s);
                cs.AdvancementClass = SkillAdvancementClass.Trained;
                cs.InitLevel = 0;
                cs.Ranks = 0;
                credits += skillTable.TryGetValue((uint)s, out var sb) ? sb.TrainedCost : 0;
            }
            foreach (var s in b.Specialized)
            {
                var cs = p.GetCreatureSkill(s);
                cs.AdvancementClass = SkillAdvancementClass.Specialized;
                cs.InitLevel = 10;
                cs.Ranks = 0;
                credits += skillTable.TryGetValue((uint)s, out var sb) ? sb.SpecializedCost : 0;
            }

            var table = DatManager.PortalDat.XpTable;
            var xp = survivalVeteran ? long.MaxValue / 4 : (long)table.CharacterLevelXPList[level];
            var levelCredits = 0;
            for (var i = 1; i <= level && i < table.CharacterLevelSkillCreditList.Count; i++)
                levelCredits += (int)table.CharacterLevelSkillCreditList[i];
            var heritageCredits = DatManager.PortalDat.CharGen.HeritageGroups.TryGetValue((uint)HeritageGroup.Aluvian, out var heritage) ? (int)heritage.SkillCredits : 50;

            sheet.Build = b.Name;
            sheet.Level = level;
            sheet.XpTotal = xp;
            sheet.CreditsTotal = heritageCredits + levelCredits + (survivalVeteran ? VeteranExtraCredits : 0);
            sheet.CreditsUsed = credits;

            // the 10 attribute augmentations behind the 380 innate points (End of Retail: 330 at creation + 50), paid in XP first
            // they are bought only when they cost under 15% of the character's XP (about level 190 and up); below that the character keeps
            // its creation spread (330: the augmented attribute back down by the 50 the augs gave it)
            var attributeAug = DatabaseManager.World.GetCachedWeenie(29294)?.GetProperty(PropertyInt64.AugmentationCost) ?? 0;
            var augPoints = b.Str + b.End + b.Coord + b.Quick + b.Focus + b.Self - 330;
            if (augPoints > 0 && attributeAug > 0)
            {
                var count = augPoints / 5;
                if (survivalVeteran || attributeAug * count <= xp * 0.15)
                {
                    xp -= attributeAug * count;
                    sheet.Augs.Add($"{count} attribute augs");
                }
                else
                {
                    // the attribute the augs went into: the build's one innate above 10 that isn't 100
                    var augmented = new[] { (PropertyAttribute.Strength, b.Str), (PropertyAttribute.Endurance, b.End), (PropertyAttribute.Coordination, b.Coord),
                        (PropertyAttribute.Quickness, b.Quick), (PropertyAttribute.Focus, b.Focus), (PropertyAttribute.Self, b.Self) }
                        .Where(a => a.Item2 > 10 && a.Item2 < 100).Select(a => a.Item1).DefaultIfEmpty(PropertyAttribute.Endurance).First();
                    p.Attributes[augmented].StartingValue = (uint)Math.Max(10, (int)p.Attributes[augmented].StartingValue - augPoints);
                    sheet.Augs.Add($"no attribute augs yet (5 billion XP; innate back to 330: {augmented} -{augPoints})");
                }
            }

            // XP goes to the cheapest next rank among everything the build raises, earlier items first on a tie (2026-11-16). Until
            // then each item took all it could afford in the list's order, so a level 100 archer had Missile Weapons at 213 of 226
            // ranks and Coordination at 9 of 190: no player does that (100 attribute ranks cost 3M XP, the weapon skill's last 13
            // ranks 3 billion), and it flattered whatever scales with a skill, as the Alchemist's bombs do, over what scales with
            // an attribute, as every other weapon does. A level's XP bought in full, nothing changes at 275.
            // The XP augmentations keep their place: bought once everything listed before "augs" is paid for and they are affordable.
            var items = new List<(string Label, List<uint> Costs, Action<int> Set)>();
            long beforeAugs = 0;
            var hasAugs = false;
            foreach (var item in b.Spend)
            {
                if (item == "augs")
                {
                    hasAugs = true;
                    continue;
                }
                var kind = item[0];
                var name2 = item[2..];
                List<uint> costs = null;
                Action<int> set = null;
                if (kind == 'a' && Enum.TryParse<PropertyAttribute>(name2, out var attr))
                {
                    costs = table.AttributeXpList;
                    set = r => p.Attributes[attr].Ranks = (uint)r;
                }
                else if (kind == 'v')
                {
                    var vital = name2 == "Health" ? p.Health : name2 == "Stamina" ? p.Stamina : p.Mana;
                    costs = table.VitalXpList;
                    set = r => vital.Ranks = (uint)r;
                }
                else if (kind == 's' && Enum.TryParse<Skill>(name2, out var skill))
                {
                    var cs = p.GetCreatureSkill(skill);
                    if (cs.AdvancementClass < SkillAdvancementClass.Trained)
                        continue;
                    costs = cs.AdvancementClass == SkillAdvancementClass.Specialized ? table.SpecializedSkillXpList : table.TrainedSkillXpList;
                    set = r => cs.Ranks = (ushort)r;
                }
                if (costs == null)
                    continue;
                items.Add((name2, costs, set));
                if (!hasAugs)
                    beforeAugs += costs[^1];
            }

            if (hasAugs)
            {
                foreach (var (wcid, prop, name) in SurvivalAugs)
                {
                    var (augWcid, augProp, augName) = wcid != 0 ? (wcid, prop, name)
                        : b.SkilledAug == Skill.MissileWeapons ? (41490u, PropertyInt.AugmentationSkilledMissile, "Master of the Focused Eye")
                        : b.SkilledAug == Skill.WarMagic ? (41476u, PropertyInt.AugmentationSkilledMagic, "Master of the Five Fold Path")
                        : (41477u, PropertyInt.AugmentationSkilledMelee, "Master of the Steel Circle");
                    var cost = DatabaseManager.World.GetCachedWeenie(augWcid)?.GetProperty(PropertyInt64.AugmentationCost) ?? 0;
                    if (cost <= 0 || xp - beforeAugs < cost)
                        continue;
                    xp -= cost;
                    p.SetProperty(augProp, 1);
                    sheet.Augs.Add(augName);
                }
            }

            var bought = new int[items.Count];
            while (true)
            {
                var next = -1;
                long nextCost = long.MaxValue;
                for (var i = 0; i < items.Count; i++)
                {
                    var costs = items[i].Costs;
                    if (bought[i] + 1 >= costs.Count)
                        continue;
                    var step = (long)costs[bought[i] + 1] - costs[bought[i]];
                    if (step < nextCost)
                    {
                        nextCost = step;
                        next = i;
                    }
                }
                if (next < 0 || nextCost > xp)
                    break;
                xp -= nextCost;
                bought[next]++;
            }
            for (var i = 0; i < items.Count; i++)
            {
                items[i].Set(bought[i]);
                sheet.Raised.Add($"{items[i].Label} {bought[i]}/{items[i].Costs.Count - 1}");
            }
            sheet.XpLeft = survivalVeteran ? -1 : xp;

            // luminance auras: none at 200 (luminance starts there), all at the cap 275, or a given share
            var lumShare = survivalVeteran ? 1.0 : lumPercent >= 0 ? lumPercent / 100.0 : Math.Clamp((level - 200) / 75.0, 0, 1);
            sheet.Lum = (int)Math.Round(lumShare * 100);
            foreach (var (prop, max) in SurvivalLumAuras)
            {
                var n = (int)Math.Round(max * lumShare);
                if (n > 0)
                    p.SetProperty(prop, n);
            }
            return p;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Gear: median loot rolls of the level's tier, rolled once per level
        // ---------------------------------------------------------------------------------------------------------------

        private class PieceRoll
        {
            public uint Wcid;
            public int ArmorLevel, Workmanship;
            public Dictionary<PropertyFloat, double> Mods = new();
        }

        private static readonly PropertyFloat[] ArmorModProps =
        {
            PropertyFloat.ArmorModVsSlash, PropertyFloat.ArmorModVsPierce, PropertyFloat.ArmorModVsBludgeon, PropertyFloat.ArmorModVsCold,
            PropertyFloat.ArmorModVsFire, PropertyFloat.ArmorModVsAcid, PropertyFloat.ArmorModVsElectric, PropertyFloat.ArmorModVsNether,
        };

        private static readonly WeenieClassName[] SurvivalArmor =
        {
            WeenieClassName.helmet, WeenieClassName.breastplateplatemail, WeenieClassName.girthplatemail, WeenieClassName.pauldronsplatemail, WeenieClassName.vambracesplatemail,
            WeenieClassName.gauntletsplatemail, WeenieClassName.tassetsplatemail, WeenieClassName.greavesplatemail, WeenieClassName.sollerets,
        };

        /// <summary>
        /// The median (by armor level) of 41 real loot rolls of an armor piece or shield at a tier, no spells
        /// </summary>
        private static PieceRoll RollArmorPiece(uint wcid, int tier)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            var rolls = new List<PieceRoll>();
            for (var i = 0; i < 41; i++)
            {
                var wo = WorldObjectFactory.CreateWorldObject(weenie, new ObjectGuid(SurvivalGuidBase));
                LootGenerationFactory.MutateItem(wo, new TreasureDeath { Tier = tier, LootQualityMod = 0 }, false);
                var roll = new PieceRoll { Wcid = wcid, ArmorLevel = wo.GetProperty(PropertyInt.ArmorLevel) ?? 0, Workmanship = wo.ItemWorkmanship ?? 0 };
                foreach (var prop in ArmorModProps)
                    if (wo.GetProperty(prop) is double v)
                        roll.Mods[prop] = v;
                rolls.Add(roll);
            }
            return rolls.OrderBy(r => r.ArmorLevel).ElementAt(rolls.Count / 2);
        }

        private static WorldObject MakePiece(PieceRoll roll, uint guid, bool steel)
        {
            var wo = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(roll.Wcid), new ObjectGuid(guid));
            wo.SetProperty(PropertyInt.ArmorLevel, roll.ArmorLevel);
            wo.ItemWorkmanship = roll.Workmanship;
            foreach (var (prop, v) in roll.Mods)
                wo.SetProperty(prop, v);
            if (steel)
            {
                // 10 Steel, as the tink recipe applies it (the Steel mutation script)
                for (var i = wo.NumTimesTinkered; i < MaxTinks; i++)
                    MutationCache.GetMutation(SteelMutation).TryMutate(wo);
            }
            return wo;
        }

        private class SurvivalGear
        {
            public int Tier;
            public List<PieceRoll> Armor = new();
            public PieceRoll Shield;
            public List<WorldObject> Swords = new(), Bows = new(), Casters = new(), TwoHanders = new(), Bombs = new(), Atlatls = new(), Thrown = new();
            public string Describe;
        }

        /// <summary>
        /// The median of 41 real loot rolls of a weapon at a tier (no ACX extra mutation or slayer, no spells), kept whole: its
        /// damage, variance, speed, workmanship and wield, and also its rolled Bonus to Attack Skill, melee / missile / magic defense
        /// bonuses, damage multiplier, elemental bonus and caster mods (the gear bench's LootRoll keeps only the damage part). Ordered
        /// as the gear bench orders (RollLootTier's measure; casters by their elemental damage).
        /// </summary>
        private static WorldObject RollWeaponFull(uint wcid, int tier)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie == null)
                return null;
            var rolls = new List<WorldObject>();
            for (var attempt = 0; attempt < 2000 && rolls.Count < LootRollsPerKind; attempt++)
            {
                var wo = WorldObjectFactory.CreateWorldObject(weenie, new ObjectGuid(WeaponGuid));
                LootGenerationFactory.MutateItem(wo, new TreasureDeath { Tier = tier, LootQualityMod = 0 }, false);
                if (wo.IgnoreArmor != null || wo.CriticalFrequency != null || wo.GetProperty(PropertyFloat.CriticalMultiplier) != null || wo.SlayerCreatureType != null)
                    continue;
                rolls.Add(wo);
            }
            if (rolls.Count == 0)
                return null;
            var ammo = NominalAmmoDamage[Math.Clamp(tier, 1, 8) - 1];
            double Measure(WorldObject r) => r is Caster ? r.ElementalDamageMod ?? 1
                : (((r.Damage ?? 0) > 0 ? (r.Damage ?? 0) * (1 - (r.DamageVariance ?? 0) / 2) : ammo) + (r.ElementalDamageBonus ?? 0)) * (r.DamageMod ?? 1);
            return rolls.OrderBy(Measure).ElementAt(rolls.Count / 2);
        }

        /// <summary>
        /// A fresh copy of a rolled weapon: the weenie with every rolled int, float and bool property copied over
        /// </summary>
        private static WorldObject CloneRolled(WorldObject source, uint guid)
        {
            var copy = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(source.WeenieClassId), new ObjectGuid(guid));
            foreach (var (prop, value) in source.GetAllPropertyInt())
                copy.SetProperty(prop, value);
            foreach (var (prop, value) in source.GetAllPropertyFloat())
                copy.SetProperty(prop, value);
            foreach (var (prop, value) in source.GetAllPropertyBools())
                copy.SetProperty(prop, value);
            return copy;
        }

        private static SurvivalGear RollSurvivalGear(int level)
        {
            var tier = survivalLootTier > 0 ? survivalLootTier : TierFor(level);   // tier=N on the command: another tier's loot at this level
            var gear = new SurvivalGear { Tier = tier };
            foreach (var wcid in SurvivalArmor)
                gear.Armor.Add(RollArmorPiece((uint)wcid, tier));
            gear.Shield = RollArmorPiece((uint)WeenieClassName.shieldkitelarge, tier);   // JJ: "Tower shields drain your stamina almost nobody uses them"

            var kindClass = MeleeClass(survivalMeleeKind);
            var swords = ((Factories.Entity.ChanceTable<WeenieClassName>)(kindClass?.Table ?? typeof(HeavyWeaponWcids)).GetField(kindClass != null ? survivalMeleeKind.Split('.')[1] : "LongSwords", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Select(e => (uint)e.result).Distinct();
            foreach (var wcid in swords)
                if (RollWeaponFull(wcid, tier) is WorldObject sword)
                    gear.Swords.Add(sword);

            var nodachis = ((Factories.Entity.ChanceTable<WeenieClassName>)typeof(TwoHandedWeaponWcids).GetField("Nodachis", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Select(e => (uint)e.result).Distinct();
            foreach (var wcid in nodachis)
                if (RollWeaponFull(wcid, tier) is WorldObject twoHander)
                    gear.TwoHanders.Add(twoHander);

            var bowTiers = (List<Factories.Entity.ChanceTable<WeenieClassName>>)typeof(BowWcids_Aluvian).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
                .First(f => f.FieldType == typeof(List<Factories.Entity.ChanceTable<WeenieClassName>>)).GetValue(null);
            foreach (var group in bowTiers[tier - 1].Select(e => (uint)e.result).Distinct()
                .GroupBy(w => (DamageType)(DatabaseManager.World.GetCachedWeenie(w)?.GetProperty(PropertyInt.DamageType) ?? 0)))
                if (RollWeaponFull(group.First(), tier) is WorldObject bow)
                    gear.Bows.Add(bow);

            foreach (var wcid in new[] { WeenieClassName.wandfire, WeenieClassName.wandfrost, WeenieClassName.wandelectric, WeenieClassName.wandacid, WeenieClassName.wandslashing, WeenieClassName.wandpiercing, WeenieClassName.wandblunt })
                if (RollWeaponFull((uint)wcid, tier) is WorldObject caster)
                    gear.Casters.Add(caster);

            // the atlatls of the tier (one per damage type, as the bows) and the Eternal thrown weapons that drop at it (every family's plain
            // version, and from tier 5 its acid / fire / frost / electric ones; AcxThrownWcids), for the comparison builds
            var atlatlTiers = (List<Factories.Entity.ChanceTable<WeenieClassName>>)typeof(AtlatlWcids).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
                .First(f => f.FieldType == typeof(List<Factories.Entity.ChanceTable<WeenieClassName>>)).GetValue(null);
            foreach (var group in atlatlTiers[tier - 1].Select(e => (uint)e.result).Distinct()
                .GroupBy(w => (DamageType)(DatabaseManager.World.GetCachedWeenie(w)?.GetProperty(PropertyInt.DamageType) ?? 0)))
                if (RollWeaponFull(group.First(), tier) is WorldObject atlatl)
                    gear.Atlatls.Add(atlatl);
            for (var wcid = 900810u; wcid <= (tier >= 5 ? 900844u : 900816u); wcid++)
                if (RollWeaponFull(wcid, tier) is WorldObject thrown)
                    gear.Thrown.Add(thrown);

            // the Alchemist's damage bombs that drop at the tier (AcxBombWcids: acx_alchemist_low_tier_rule), each its tier's median roll
            for (var wcid = AcxBombWcids.Fire; wcid <= AcxBombWcids.Nether; wcid++)
                if (AcxBombWcids.DropsAtTier(wcid, tier) && RollWeaponFull(wcid, tier) is WorldObject bomb)
                    gear.Bombs.Add(bomb);

            string Mods(WorldObject w) => $"atk {w.WeaponOffense ?? 1:0.##} def {w.WeaponDefense ?? 1:0.##}";
            gear.Describe = $"T{tier}: armor AL {string.Join("/", gear.Armor.Select(a => a.ArmorLevel))} +200 Steel each, shield AL {gear.Shield.ArmorLevel}; "
                + $"swords {string.Join(" ", gear.Swords.Select(w => $"{w.Damage}d/{w.DamageVariance:0.##} {Mods(w)}"))}; "
                + $"bows {string.Join(" ", gear.Bows.Select(w => $"x{w.DamageMod:0.##}+{w.ElementalDamageBonus ?? 0} {Mods(w)}"))}; "
                + $"casters {string.Join(" ", gear.Casters.Select(w => $"{w.ElementalDamageMod:0.###} {Mods(w)}"))}; "
                + $"bombs {string.Join(" ", gear.Bombs.Select(w => $"{w.Name} {w.Damage}d x{w.DamageMod:0.##} wield {w.WieldRequirements}/{(Skill)(w.WieldSkillType ?? 0)}/{w.WieldDifficulty}+{w.WieldRequirements2}/{(Skill)(w.WieldSkillType2 ?? 0)}/{w.WieldDifficulty2} {(AcxBombWcids.GetDebuffSpell(w) is uint d ? new Spell(d).Name : "-")} ({w.MaterialType})"))}"
                + (gear.Bombs.Count > 0 ? " | bomb " + ThrownExamine(gear.Bombs[0], level).Replace("\n", " / ") : "")
                + (gear.Thrown.Count > 0 ? " | thrown " + ThrownExamine(gear.Thrown[0], level).Replace("\n", " / ") : "");
            return gear;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Setup of one fight: a fresh character, worn gear, buffs, the chosen weapon, the pet, the monster
        // ---------------------------------------------------------------------------------------------------------------

        private class Loadout
        {
            public WorldObject Weapon, Ammo;   // melee weapon / bow + arrows / caster
            public Spell Bolt;                 // mage
            public string Name;
            public List<string> Extras = new();   // set / aetheria / cloak / trinket spells active, for the sheet
            public List<WorldObject> Aetheria = new();
            public WorldObject Cloak;
        }

        private static void SetCombatMode(Creature c, CombatMode mode) => CombatModeSetter.Invoke(c, new object[] { mode });

        /// <summary>
        /// A fresh character of the build, in its gear, buffed, with weapon version v (index into the build's weapon list), ready to fight
        /// </summary>
        private static Player DressSurvivor(SurvivalBuild b, int level, int lumPercent, SurvivalGear gear, int version, out Loadout loadout, Sheet sheet = null, WorldObject weaponOverride = null)
        {
            var p = CreateSurvivor(b, level, lumPercent, sheet ?? new Sheet());
            var guid = SurvivalGuidBase;
            var worn = new List<WorldObject>();
            foreach (var roll in gear.Armor)
                worn.Add(MakePiece(roll, ++guid, true));
            foreach (var wcid in new[] { WeenieClassName.shirtbaggy, WeenieClassName.pantsbaggy })
                worn.Add(WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie((uint)wcid), new ObjectGuid(++guid)));
            if (b.Shield)
            {
                // the median large kite shield, 10 Steel, with ACX's Magic Absorbing (LootGenerationFactory_AcxMutations: AbsorbMagicDamage 0.30)
                var shield = MakePiece(gear.Shield, ++guid, true);
                shield.AbsorbMagicDamage = 0.3f;
                shield.Name = $"{shield.Name} of Magic Absorbing";
                worn.Add(shield);
            }
            foreach (var item in worn)
                if (!p.TryEquipObject(item, item.ValidLocations ?? EquipMask.None))
                    log.Warn($"[ACXSURVIVAL] couldn't wear {item.Name}");

            loadout = new Loadout();
            var buff = BuffLevelFor(level);
            if (weaponOverride != null)
            {
                loadout.Weapon = weaponOverride;
                loadout.Name = weaponOverride.Name;
            }
            else if (b.Style == "melee")
            {
                var mainIndex = b.Name == "dualmix" ? version / gear.Swords.Count : version;
                var weapon = CloneRolled(b.Name == "twohand" ? gear.TwoHanders[version] : gear.Swords[mainIndex], WeaponGuid);
                TinkReal(weapon, buff, new List<uint>());
                loadout.Weapon = weapon;
                loadout.Name = $"{weapon.Name} (tinked)";
            }
            else if (b.Name == "alchemist")
            {
                var bomb = CloneRolled(gear.Bombs[version], WeaponGuid);
                if (gear.Bombs[version].GetProperty(PropertyDataId.AcxBombTier) is uint debuff)
                    bomb.SetProperty(PropertyDataId.AcxBombTier, debuff);
                TinkBombReal(bomb);
                loadout.Weapon = bomb;
                loadout.Ammo = bomb;
                loadout.Name = $"{bomb.Name} x{bomb.DamageMod:0.##} (tinked)";
            }
            else if (b.Style == "missile")
            {
                var bow = CloneRolled((b.Name == "atlatl" ? gear.Atlatls : b.Name == "thrown" ? gear.Thrown : gear.Bows)[version], WeaponGuid);
                var ammo = MissileAmmo(p, bow, TierFor(level));   // the level's ammunition (ArrowsByTier), whatever tier= the gear is
                TinkLauncherReal(bow, ammo);
                loadout.Weapon = bow;
                loadout.Ammo = ammo;
                loadout.Name = $"{bow.Name} + {ammo?.Name} (tinked)";
            }
            else
            {
                var caster = CloneRolled(gear.Casters[version], WeaponGuid);
                ImbueReal(caster, ImbuedEffectType.Undef);
                for (var i = caster.NumTimesTinkered; i < MaxTinks; i++)
                    MutationCache.GetMutation(GreenGarnetMutation).TryMutate(caster);
                loadout.Weapon = caster;
                loadout.Bolt = new Spell(BuffAt(SurvivalBolts.TryGetValue(caster.W_DamageType, out var bolt) ? bolt : 4439u, buff));   // the bolt of the character's spell level
                loadout.Name = $"{caster.Name} {caster.ElementalDamageMod:0.###} (tinked) + {loadout.Bolt.Name}";
            }

            if (!p.TryEquipObject(loadout.Weapon, loadout.Weapon.ValidLocations ?? EquipMask.MeleeWeapon))
                log.Warn($"[ACXSURVIVAL] couldn't wield {loadout.Weapon.Name}");
            if (b.Name.StartsWith("dual") && weaponOverride == null)
            {
                // the off-hand, in the shield slot (Creature.GetDualWieldWeapon), tinked the same way (rend of its own damage type): dualwield the
                // same median sword; dualmix another damage type's
                var n = gear.Swords.Count;
                var offIndex = b.Name == "dualmix" ? version % n : version;
                if (b.Name == "dualmix" && offIndex == version / n)
                    offIndex = (offIndex + 1) % n;
                var offhand = CloneRolled(gear.Swords[offIndex], AmmoGuid);
                TinkReal(offhand, BuffLevelFor(level), new List<uint>());
                if (!p.TryEquipObject(offhand, EquipMask.Shield))
                    log.Warn("[ACXSURVIVAL] couldn't wield the off-hand weapon");
                foreach (var spellId in offhand.Biota.GetKnownSpellsIds(offhand.BiotaDatabaseLock))
                {
                    var spell = new Spell((uint)spellId);
                    if (spell.NotFound)
                        continue;
                    var onWeapon = spell.School == MagicSchool.ItemEnchantment && !spell.HasItemCategory && !spell.IsPortalSpell;
                    (onWeapon ? offhand : p).EnchantmentManager.Add(spell, offhand, offhand, true);
                }
                loadout.Name += b.Name == "dualmix" ? $" + {offhand.Name} (tinked)" : " + the same off-hand";
            }
            if (b.Shield)
                loadout.Extras.Add("Large Kite Shield of Magic Absorbing: up to 30% of spell damage, scaled by Shield skill (SpellProjectile.GetShieldMod)");
            foreach (var spellId in loadout.Weapon.Biota.GetKnownSpellsIds(loadout.Weapon.BiotaDatabaseLock))
            {
                var spell = new Spell((uint)spellId);
                if (spell.NotFound)
                    continue;
                var onWeapon = spell.School == MagicSchool.ItemEnchantment && !spell.HasItemCategory && !spell.IsPortalSpell;
                (onWeapon ? loadout.Weapon : p).EnchantmentManager.Add(spell, loadout.Weapon, loadout.Weapon, true);
            }

            if (survivalGear >= 1)
                DressExtras(p, b, worn.Take(gear.Armor.Count).ToList(), loadout);

            // full self buffs, cast the game's way: creature, life and aura spells on the character; Impenetrability and banes on every
            // worn piece (WorldObject_Magic: a self-cast impen/bane lands on each enchantable Clothing and shield)
            var buffs = new List<uint>(SurvivalCreatureBuffs);
            foreach (var skill in b.Specialized.Concat(b.Trained))
                if (SurvivalMasteries.TryGetValue(skill, out var ids) || DummyMasteries.TryGetValue(skill, out ids))
                    buffs.Add(ids.FirstOrDefault(id => new Spell(id).StatModKey == (uint)skill) is var match && match != 0 ? match : ids[0]);
            buffs.AddRange(SurvivalLifeBuffs);
            buffs.AddRange(b.Style == "magic" ? SurvivalCasterAuras : SurvivalWeaponAuras);
            foreach (var id in buffs)
                p.EnchantmentManager.Add(new Spell(BuffAt(id, buff)), p, null, false);
            foreach (var item in p.EquippedObjects.Values.Where(i => (i.WeenieType == WeenieType.Clothing || i.IsShield) && i.IsEnchantable).ToList())
                foreach (var id in SurvivalArmorBuffs)
                    item.EnchantmentManager.Add(new Spell(BuffAt(id, buff)), p, null, false);

            // every profile wears its level band's cantrips (survivalBandLevel)
            {
                var cantrips = new List<uint>(VeteranCantripsAll);
                cantrips.AddRange(VeteranCantripsStyle.TryGetValue(b.Name, out var byName) ? byName : VeteranCantripsStyle[b.Style]);
                foreach (var skill in b.Specialized.Concat(b.Trained))
                    if (VeteranAptitudes.TryGetValue(skill, out var id) && (skill == b.Specialized[0] || skill == Skill.Shield && b.Shield))
                        cantrips.Add(id);
                foreach (var id in cantrips.Distinct())
                    p.EnchantmentManager.Add(new Spell(BandCantrip(id)), p, null, true);
            }

            var mode = b.Style == "melee" ? CombatMode.Melee : b.Style == "missile" ? CombatMode.Missile : CombatMode.Magic;
            SetCombatMode(p, mode);
            p.CurrentMotionState = new Motion(p.GetCombatStance());
            p.PowerLevel = 1.0f;
            p.AccuracyLevel = 1.0f;
            p.AttackHeight = AttackHeight.Medium;
            p.Health.Current = p.Health.MaxValue;
            p.Stamina.Current = p.Stamina.MaxValue;
            p.Mana.Current = p.Mana.MaxValue;

            // the defense roll reads the player's physics state (Player.IsJumping); give the bench player one, standing on the ground,
            // only now: with physics, equipping would broadcast to the session a bench player doesn't have
            p.InitPhysicsObj();
            p.PhysicsObj.TransientState |= ACE.Server.Physics.TransientStateFlags.OnWalkable | ACE.Server.Physics.TransientStateFlags.Contact;
            return p;
        }

        /// <summary>
        /// A buff at a spell level (SpellLevelProgression: the family's spells by level), for characters under 150 whose buffs aren't Incantations
        /// </summary>
        private static uint BuffAt(uint incantation, int spellLevel)
        {
            if (spellLevel >= 8)
                return incantation;
            var family = Factories.Tables.SpellLevelProgression.GetSpellLevels((SpellId)incantation);
            return family != null && family.Count >= spellLevel && spellLevel >= 1 ? (uint)family[spellLevel - 1] : incantation;
        }

        private static int WeaponVersions(SurvivalBuild b, SurvivalGear gear) => b.Name == "alchemist" ? gear.Bombs.Count : b.Name == "atlatl" ? gear.Atlatls.Count : b.Name == "thrown" ? gear.Thrown.Count : b.Name == "twohand" ? gear.TwoHanders.Count : b.Name == "dualmix" ? gear.Swords.Count * gear.Swords.Count : b.Style == "melee" ? gear.Swords.Count : b.Style == "missile" ? gear.Bows.Count : gear.Casters.Count;

        private static Creature MakeMonster(uint wcid, bool elite, AcxInvasions.Town town = null)
        {
            var creature = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(wcid), new ObjectGuid(SurvivalMonsterGuid)) as Creature;
            if (creature == null)
                return null;
            creature.Location = new Position(BenchCell, 50.0f, 51.0f, 0.0f, 0, 0, 1, 0);   // a metre in front of the player, facing it
            if (elite)
            {
                creature.AcxMakeElite();
                if (town != null)
                    AcxInvasions.ApplyTownStrength(creature, town);   // the town's healthScale / damageScale (towns.json), as AcxInvasions spawns it
            }
            PrepareFighter(creature);
            return creature;
        }

        private static Creature MakePet(uint wcid)
        {
            var pet = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(wcid), new ObjectGuid(SurvivalPetGuid)) as Creature;
            if (pet == null)
                return null;
            pet.Location = new Position(BenchCell, 50.8f, 51.6f, 0.0f, 0, 0, 1, 0);   // beside the player, on the monster
            PrepareFighter(pet);
            return pet;
        }

        private static void PrepareFighter(Creature c)
        {
            if (c.CombatTable == null)
                c.GetCombatTable();
            SetCombatMode(c, c.GetEquippedMissileWeapon() != null ? CombatMode.Missile : CombatMode.Melee);
            c.CurrentMotionState = new Motion(c.GetCombatStance());
            c.Health.Current = c.Health.MaxValue;
            c.Mana.Current = c.Mana.MaxValue;
            c.Stamina.Current = c.Stamina.MaxValue;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Extra gear (JJ 2026-11-16: "armor set bonuses per class type", "aetheria", "trinkets", "cloaks"): 1 = + the armor set,
        // 2 = + aetheria, 3 = + cloak and trinket; set spells through WorldObject.GetSpellSet (the server's set tiers)
        // ---------------------------------------------------------------------------------------------------------------

        [ThreadStatic] private static int survivalGear;
        [ThreadStatic] private static int survivalLootTier;   // 0: the level's (TierFor)
        [ThreadStatic] private static string survivalBombImbue;   // bombimbue=: null none, "rend" the bomb's own rend, "cs", "cb", "ar"
        [ThreadStatic] private static EquipmentSet? survivalSetOverride;
        [ThreadStatic] private static string survivalAetheria;   // "defense" (default) or "offense"
        [ThreadStatic] private static string survivalTrinket;    // "scarab" (default), or a looted sigil trinket with a Major armature: diamond, gromnie, ruby, amber, sapphire

        // the Major Item Tinkering armature recipes on a sigil trinket (cook_book, target Compass): each adds one spell and the Spellbook imbue
        private static readonly Dictionary<string, uint> MajorArmatures = new()
        {
            { "ruby", 8372 },      // Augmented Health III (+15 health)
            { "amber", 8375 },     // Augmented Stamina III
            { "diamond", 8378 },   // Augmented Damage III (+3 damage rating)
            { "gromnie", 8381 },   // Augmented Damage Reduction III (+3 damage resist rating)
            { "sapphire", 8387 },  // Augmented Mana III
        };
        private const uint CompassWcid = 41483;

        /// <summary>
        /// An armature recipe's int mods, as RecipeManager.ModifyInt applies them (its UpdateProperty calls write to the player's session,
        /// so the same operations are done here on the item): set, add, add spell, bits on / off
        /// </summary>
        private static void ApplyRecipeIntMods(WorldObject item, uint recipeId)
        {
            var recipe = DatabaseManager.World.GetCachedRecipe(recipeId);
            if (recipe == null)
                return;
            foreach (var mod in recipe.RecipeMod)
                foreach (var intMod in mod.RecipeModsInt)
                {
                    var prop = (PropertyInt)intMod.Stat;
                    switch ((ModificationOperation)intMod.Enum)
                    {
                        case ModificationOperation.SetValue: item.SetProperty(prop, intMod.Value); break;
                        case ModificationOperation.Add: item.SetProperty(prop, (item.GetProperty(prop) ?? 0) + intMod.Value); break;
                        case ModificationOperation.AddSpell: item.Biota.GetOrAddKnownSpell(intMod.Stat, item.BiotaDatabaseLock, out _); break;
                        case ModificationOperation.SetBitsOn: item.SetProperty(prop, (item.GetProperty(prop) ?? 0) | intMod.Value); break;
                        case ModificationOperation.SetBitsOff: item.SetProperty(prop, (item.GetProperty(prop) ?? 0) & ~intMod.Value); break;
                    }
                }
        }

        // the loot armor set each build wears: no End of Retail source ranks them, so all eight loot sets were benched at L275 (fresh, 10
        // fights x 22 monsters, 2026-11-16) and the best kept: Defender's for both melee builds (tied with Soldier's with kits, better
        // without), Dexterous for the archer (a hair over Archer's / Soldier's), Adept's for the mage (clear)
        private static readonly Dictionary<string, EquipmentSet> SurvivalArmorSets = new()
        {
            { "melee", EquipmentSet.Defenders }, { "meleeshield", EquipmentSet.Defenders }, { "archer", EquipmentSet.Dexterous }, { "mage", EquipmentSet.Adepts },
            { "twohand", EquipmentSet.Soldiers }, { "dualwield", EquipmentSet.Defenders }, { "dualmix", EquipmentSet.Defenders },   // benched the same way: Soldier's (two-hand), Defender's (dual wield)
            { "alchemist", EquipmentSet.Dexterous },   // the archer's, not benched yet (2026-11-16)
            { "atlatl", EquipmentSet.Dexterous }, { "thrown", EquipmentSet.Dexterous },   // the archer's (comparison builds)
        };

        private static readonly Dictionary<Skill, EquipmentSet> SurvivalCloakSets = new()
        {
            { Skill.HeavyWeapons, EquipmentSet.CloakHeavyWeapons }, { Skill.LightWeapons, EquipmentSet.CloakLightWeapons }, { Skill.FinesseWeapons, EquipmentSet.CloakFinesseWeapons },
            { Skill.TwoHandedCombat, EquipmentSet.CloakTwoHandedCombat }, { Skill.MissileWeapons, EquipmentSet.CloakMissileWeapons }, { Skill.WarMagic, EquipmentSet.CloakWarMagic },
        };

        private const uint CrimsonScarabWcid = 52036, LootCloakWcid = 44840;
        private static readonly uint[] AetheriaColors = { 42635, 42637, 42636 };   // blue, yellow, red (coalesced aetheria; Aetheria.GetColor)

        private static void AddSetSpells(Player p, List<WorldObject> items, Loadout loadout, string label)
        {
            // GetSpellSet builds its spells without their DB half; Player.CreateItemSpell reloads them by id, and so does this
            var spells = WorldObject.GetSpellSet(items).Select(sp => new Spell(sp.Id)).Where(sp => !sp.NotFound).ToList();
            foreach (var spell in spells)
                p.EnchantmentManager.Add(spell, items[0], null, true);
            loadout.Extras.Add($"{label}: {(spells.Count > 0 ? string.Join(", ", spells.Select(sp => sp.Name)) : "no spells")}");
        }

        private static void DressExtras(Player p, SurvivalBuild b, List<WorldObject> armor, Loadout loadout)
        {
            // armor sets and aetheria only for characters whose loot tier is 7 or 8 (2026-11-16, JJ: "what gear does it use at level 100?
            // that won't be set gear and certainly not all aetheria"): loot sets roll on T7/T8 body armor only, and aetheria comes with
            // that end game; below it a character wears plain loot (the cloak and trinket keep their own level 150 gate)
            var endGame = TierFor(p.Level ?? 1) >= 7;

            // the armor set on every body armor piece, from level 250 (JJ 2026-11-16: "set pieces from level 250 and up to 275":
            // sets open at 180 but take a long time to collect)
            if ((p.Level ?? 1) >= SetsFromLevel)
            {
                var set = survivalSetOverride ?? SurvivalArmorSets[b.Name];
                foreach (var piece in armor)
                    piece.EquipmentSetId = set;
                AddSetSpells(p, armor, loadout, $"{set} set, {armor.Count} pieces");
            }
            else
                loadout.Extras.Add($"no armor set or aetheria (loot tier {TierFor(p.Level ?? 1)}: sets roll on T7/T8)");

            if (survivalGear >= 2 && endGame)
            {
                // three level 5 aetheria (the top roll) in one sigil set, surges per the preset, as Aetheria.ActivateSigil sets them up
                var offense = (survivalAetheria ?? (b.Name == "twohand" ? "offense" : "defense")) == "offense";   // benched: offense only won for two-hand
                var aetheriaSet = offense ? EquipmentSet.AetheriaDestruction : EquipmentSet.AetheriaDefense;
                var surges = offense ? new[] { SpellId.AetheriaProcDamageBoost, SpellId.AetheriaProcDamageBoost, SpellId.AetheriaProcDamageBoost }
                    : new[] { SpellId.AetheriaProcDamageReduction, SpellId.AetheriaProcDamageReduction, SpellId.AetheriaProcHealthOverTime };
                // the sigil slots open at levels 75 (blue), 150 (yellow), 225 (red)
                var slots = (p.Level ?? 0) >= 225 ? 3 : (p.Level ?? 0) >= 150 ? 2 : (p.Level ?? 0) >= 75 ? 1 : 0;
                for (var i = 0; i < slots; i++)
                {
                    var a = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(AetheriaColors[i]), new ObjectGuid(0xFFFFFDD0 + (uint)i));
                    a.ItemMaxLevel = 5;
                    a.ItemTotalXp = 40_000_000_000;
                    a.EquipmentSetId = aetheriaSet;
                    a.SetProperty(PropertyDataId.ProcSpell, (uint)surges[i]);
                    if (Aetheria.SurgeTargetSelf[surges[i]])
                        a.SetProperty(PropertyBool.ProcSpellSelfTargeted, true);
                    var mask = Aetheria.ColorToMask[Aetheria.GetColor(AetheriaColors[i]).Value];
                    a.SetProperty(PropertyInt.ValidLocations, (int)mask);
                    // not put in the sigil slot: equipping runs the item's wield emotes, which need a world; everything aetheria does
                    // here (set spells, surges) works from the item itself
                    loadout.Aetheria.Add(a);
                }
                if (loadout.Aetheria.Count > 0)
                {
                    AddSetSpells(p, loadout.Aetheria, loadout, $"{aetheriaSet} x{loadout.Aetheria.Count}, level {loadout.Aetheria[0].ItemLevel}");
                    loadout.Extras.Add("surges: " + string.Join(", ", surges.Take(loadout.Aetheria.Count).Select(sp => new Spell(sp).Name)));
                }
            }

            if (survivalGear >= 3 && (p.Level ?? 0) >= 150)   // a level 5 cloak wields at 150
            {
                // a tier 8 loot cloak, at its top level (5), in the build's weapon / magic skill set, with the damage reduction proc
                var cloak = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(LootCloakWcid), new ObjectGuid(0xFFFFFDD8));
                LootGenerationFactory.MutateItem(cloak, new TreasureDeath { Tier = 8, LootQualityMod = 0 }, true);
                cloak.ItemMaxLevel = 5;
                cloak.ItemTotalXp = 40_000_000_000;
                var cloakSet = SurvivalCloakSets.TryGetValue(b.Specialized[0], out var cs) ? cs : EquipmentSet.CloakMeleeDefense;
                cloak.EquipmentSetId = cloakSet;
                cloak.SetProperty(PropertyInt.CloakWeaveProc, 2);
                cloak.RemoveProperty(PropertyDataId.ProcSpell);
                // not put in the cloak slot (wield emotes need a world): its set spells and proc work from the item
                loadout.Cloak = cloak;
                AddSetSpells(p, new List<WorldObject> { cloak }, loadout, $"{cloakSet} cloak level {cloak.ItemLevel}, 200 damage reduction proc");

                // the trinket slot: the Purified Crimson Scarab (Aun Ol'tra's quest), or a tier 8 loot sigil trinket (JewelryWcids: Compass,
                // Goggles, Pocket Watch, Puzzle Box, Mechanical Scarab, Top; no server code tells them apart, so the Compass) with a Major
                // armature applied from its recipe
                // each build's trinket from the L275 comparison (10 fights x 22 monsters, full gear): Gromnie hide armature (melee), Diamond
                // (archer), the Crimson Scarab (melee + shield, mage)
                var choice = survivalTrinket ?? (b.Name == "melee" ? "gromnie" : b.Name == "archer" || b.Name == "alchemist" || b.Name == "atlatl" || b.Name == "thrown" || b.Name == "twohand" || b.Name.StartsWith("dual") ? "diamond" : "scarab");   // benched per build (the alchemist: the archer's)
                if (choice == "scarab" && (p.Level ?? 0) < 180)   // the Crimson Scarab wields at 180; under that, a loot trinket
                    choice = "ruby";
                WorldObject trinket;
                string label;
                if (MajorArmatures.TryGetValue(choice, out var recipeId))
                {
                    trinket = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(CompassWcid), new ObjectGuid(0xFFFFFDD9));
                    LootGenerationFactory.MutateItem(trinket, new TreasureDeath { Tier = 8, LootQualityMod = 0 }, false);
                    ApplyRecipeIntMods(trinket, recipeId);
                    label = $"loot {trinket.Name} + {choice} Major armature";
                }
                else
                {
                    trinket = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(CrimsonScarabWcid), new ObjectGuid(0xFFFFFDD9));
                    label = trinket?.Name;
                }
                // not put in the trinket slot (the Crimson Scarab has a wield emote that needs a world): its spells worn from the item
                if (trinket != null)
                {
                    var spells = trinket.Biota.GetKnownSpellsIds(trinket.BiotaDatabaseLock).Select(id => new Spell((uint)id)).Where(sp => !sp.NotFound).ToList();
                    foreach (var spell in spells)
                        p.EnchantmentManager.Add(spell, trinket, null, true);
                    loadout.Extras.Add($"{label}: {string.Join(", ", spells.Select(sp => sp.Name))}");
                }
            }
        }

        /// <summary>
        /// Aetheria surges on each attack (WorldObject.TryProcEquippedItems / TryProcItem with Aetheria.CalcProcRate). The surge spells
        /// would be cast through a session the bench player doesn't have, so their effects are applied here from the spells' own values:
        /// Destruction / Protection add their rating for the spell's duration (a new surge refreshes, as the enchantment does);
        /// Regeneration / Affliction tick their amount 5 times over their DoT duration; Festering (cuts healing) has nothing to cut here.
        /// </summary>
        private static void TrySurges(Fight f)
        {
            foreach (var a in f.Gear.Aetheria)
            {
                if (ThreadSafeRandom.Next(0.0f, 1.0f) >= Aetheria.CalcProcRate(a, f.P))
                    continue;
                f.Surges++;
                var spellId = (SpellId)(a.GetProperty(PropertyDataId.ProcSpell) ?? 0);
                var spell = new Spell(spellId);
                var value = (int)spell.StatModVal;
                switch (spellId)
                {
                    case SpellId.AetheriaProcDamageBoost:
                        RatingSurge(f, PropertyInt.DamageRating, value, spell.Duration);
                        break;
                    case SpellId.AetheriaProcDamageReduction:
                        RatingSurge(f, PropertyInt.DamageResistRating, value, spell.Duration);
                        break;
                    case SpellId.AetheriaProcHealthOverTime:
                        for (var k = 1; k <= 5; k++)
                        {
                            var healed = f.P;
                            f.At(f.T + k * Math.Max(1, spell.DotDuration) / 5.0, () => { if (!f.Over && !f.Dead.Contains(healed)) healed.Health.Current = Math.Min(healed.Health.MaxValue, healed.Health.Current + (uint)value); });
                        }
                        break;
                    case SpellId.AetheriaProcDamageOverTime:
                        for (var k = 1; k <= 5; k++)
                            f.At(f.T + k * Math.Max(1, spell.DotDuration) / 5.0, () => Hurt(f, f.M, value, f.P));
                        break;
                }
            }
        }

        private static void RatingSurge(Fight f, PropertyInt prop, int value, double duration)
        {
            var player = f.P;
            var ends = f.RatingBoostEnds;
            var active = ends.TryGetValue(prop, out var end) && end > f.T;
            if (!active)
                player.SetProperty(prop, (player.GetProperty(prop) ?? 0) + value);
            var newEnd = f.T + Math.Max(1, duration);
            ends[prop] = newEnd;
            f.At(newEnd, () =>
            {
                if (ends.TryGetValue(prop, out var e) && Math.Abs(e - newEnd) < 1e-6)
                {
                    player.SetProperty(prop, (player.GetProperty(prop) ?? 0) - value);
                    ends.Remove(prop);
                }
            });
        }

        // ---------------------------------------------------------------------------------------------------------------
        // The fight
        // ---------------------------------------------------------------------------------------------------------------

        private class MobAi
        {
            public Creature C;
            public double Prev = -100, Next = 0.5, AnimEnd = 0;
            public Creature Target;
        }

        private class Fight
        {
            public bool Heal;
            public double T, Max, HealAt;
            public Player P;
            public Creature M, Pet;
            public SurvivalBuild Build;
            public Loadout Gear;
            public WorldObject Projectile;
            public double KillTime = double.NaN, DeathTime = double.NaN;
            public bool PetDead, Over;
            public double DmgByPlayer, DmgByPet, PlayerDamageTaken;
            public bool LastDamagerPet;
            public double Melee, Missile, Magic, Dot, Drain;
            public int OtherKits, PlayerAttacks, PlayerLands, MobSwings, MobLands, SpellsAtPlayer, SpellsResisted, Kits, StaminaOut, ManaOut, AttacksOnPet, MobAttacks;
            public Dictionary<string, int> Unhandled = new();
            public List<(double End, double Tick, DamageType Type, Creature Caster, Creature Target)> Dots = new();
            public PriorityQueue<Action, double> Q = new();
            public Healer HealthKit, StaminaKit, ManaKit;
            public double CastCycle, CastLand, HealTime;
            public double LastCloakProc = -1000;
            public int CloakProcs, Surges;
            public Dictionary<PropertyInt, double> RatingBoostEnds = new();   // aetheria surges: Destruction / Protection rating boosts

            // duo fights (JJ 2026-11-16: "I want at least two players to be able to beat a elite"): every player side, switched into the
            // fields above while it acts; every creature on the players' side the monster may target; who is dead; damage dealt to the monster
            public List<Side> Sides;
            public List<Creature> Allies;
            public HashSet<Creature> Dead = new();
            public Dictionary<Creature, double> DmgToMonster = new();
            public Creature LastDamager;

            // pack fights (the Alchemist's splash, 2026-11-16): every monster, all on the player; f.M is the one the player attacks,
            // the next living one when it dies; the fight is won when all are dead
            public List<Creature> Pack;
            public int BlastHits, DebuffsLanded, DebuffTries;

            public void At(double time, Action action) => Q.Enqueue(action, time);
        }

        private class Side
        {
            public Player P;
            public Creature Pet;
            public SurvivalBuild Build;
            public Loadout Gear;
            public WorldObject Projectile;
            public Healer HealthKit, StaminaKit, ManaKit;
            public double CastCycle, CastLand, HealTime, LastCloakProc = -1000;
            public Dictionary<PropertyInt, double> RatingBoostEnds = new();
        }

        /// <summary>
        /// Runs one player's action with that player's side in the fight's player fields (the solo code reads f.P, f.Gear and so on)
        /// </summary>
        private static void Act(Fight f, Side s)
        {
            if (s == null)
            {
                PlayerAct(f, null);
                return;
            }
            if (f.Over || f.Dead.Contains(s.P))
                return;
            var keep = (f.P, f.Gear, f.Build, f.Projectile, f.HealthKit, f.StaminaKit, f.ManaKit, f.CastCycle, f.CastLand, f.HealTime, f.LastCloakProc, f.RatingBoostEnds);
            (f.P, f.Gear, f.Build, f.Projectile, f.HealthKit, f.StaminaKit, f.ManaKit, f.CastCycle, f.CastLand, f.HealTime, f.LastCloakProc, f.RatingBoostEnds)
                = (s.P, s.Gear, s.Build, s.Projectile, s.HealthKit, s.StaminaKit, s.ManaKit, s.CastCycle, s.CastLand, s.HealTime, s.LastCloakProc, s.RatingBoostEnds);
            try { PlayerAct(f, s); }
            finally
            {
                s.LastCloakProc = f.LastCloakProc;
                (f.P, f.Gear, f.Build, f.Projectile, f.HealthKit, f.StaminaKit, f.ManaKit, f.CastCycle, f.CastLand, f.HealTime, f.LastCloakProc, f.RatingBoostEnds) = keep;
            }
        }

        private static double Quantize(double t) => Math.Ceiling(t / 0.2 - 1e-9) * 0.2;   // monsters think every 0.2 s (Monster_Tick)

        /// <summary>
        /// Damage to a fighter. The monster never drops under 1 (the time it would have died is recorded and the fight goes on, so the
        /// player's time to die is measured too); the player's death ends the fight.
        /// </summary>
        private static void Hurt(Fight f, Creature target, double amount, Creature source, bool cloakable = false)
        {
            if (amount <= 0 || f.Over)
                return;
            var side = f.Sides?.FirstOrDefault(x => x.P == target);
            if (side != null && f.Dead.Contains(side.P))
                return;
            var cloakGear = side?.Gear ?? f.Gear;
            if ((target == f.P || side != null) && cloakable && cloakGear.Cloak != null && ACE.Server.Entity.Cloak.HasDamageProc(cloakGear.Cloak))
            {
                // Player.TakeDamage's cloak proc (Cloak.RollProc / GetReducedAmount), its real-time cooldown run on the bench's clock
                var cloak = cloakGear.Cloak;
                var last = side?.LastCloakProc ?? f.LastCloakProc;
                cloak.UseTimestamp = Time.GetUnixTime() - (f.T - last);
                if (ACE.Server.Entity.Cloak.RollProc(cloak, (float)(amount / ((Creature)target).Health.MaxValue)))
                {
                    amount = ACE.Server.Entity.Cloak.GetReducedAmount(source, (float)amount);
                    if (side != null) side.LastCloakProc = f.T; else f.LastCloakProc = f.T;
                    f.CloakProcs++;
                }
            }
            if (side != null)
            {
                f.PlayerDamageTaken += amount;
                var left = (int)side.P.Health.Current - (int)Math.Round(amount);
                if (left > 0)
                    side.P.Health.Current = (uint)left;
                else
                {
                    side.P.Health.Current = 0;
                    f.Dead.Add(side.P);
                    if (side.Pet != null)
                        f.Dead.Add(side.Pet);   // a pet goes when its owner dies
                    if (f.Sides.All(x => f.Dead.Contains(x.P)))
                    {
                        f.DeathTime = f.T;
                        f.Over = true;
                    }
                }
            }
            else if (f.Allies != null && f.Allies.Contains(target) && target is not Player)
            {
                var left = (int)target.Health.Current - (int)Math.Round(amount);
                if (left > 0)
                    target.Health.Current = (uint)left;
                else
                {
                    target.Health.Current = 0;
                    f.Dead.Add(target);
                    f.PetDead = true;
                }
            }
            else if (target == f.P)
            {
                f.PlayerDamageTaken += amount;
                var left = (int)f.P.Health.Current - (int)Math.Round(amount);
                if (left <= 0)
                {
                    f.P.Health.Current = 0;
                    f.DeathTime = f.T;
                    f.Over = true;
                    return;
                }
                f.P.Health.Current = (uint)left;
            }
            else if (target == f.Pet)
            {
                var left = (int)f.Pet.Health.Current - (int)Math.Round(amount);
                if (left <= 0)
                {
                    f.Pet.Health.Current = 0;
                    f.PetDead = true;
                }
                else
                    f.Pet.Health.Current = (uint)left;
            }
            else if (f.Pack != null && f.Pack.Contains(target))
            {
                if (source == f.Pet) f.DmgByPet += amount; else f.DmgByPlayer += amount;
                var left = (long)target.Health.Current - (long)Math.Round(amount);
                if (left > 0)
                    target.Health.Current = (uint)left;
                else
                {
                    target.Health.Current = 0;
                    f.Dead.Add(target);
                    var next = f.Pack.FirstOrDefault(c => !f.Dead.Contains(c));
                    if (next == null)
                    {
                        f.KillTime = f.T;
                        f.Over = true;
                    }
                    else if (target == f.M)
                        f.M = next;
                }
            }
            else if (target == f.M)
            {
                if (source != null)
                {
                    f.DmgToMonster[source] = f.DmgToMonster.GetValueOrDefault(source) + amount;
                    f.LastDamager = source;
                }
                if (source == f.Pet) { f.DmgByPet += amount; f.LastDamagerPet = true; }
                else { f.DmgByPlayer += amount; f.LastDamagerPet = false; }
                var left = (long)f.M.Health.Current - (long)Math.Round(amount);
                if (left <= 0)
                {
                    if (double.IsNaN(f.KillTime))
                        f.KillTime = f.T;
                    left = 1;
                }
                f.M.Health.Current = (uint)left;
            }
        }

        private static bool Alive(Fight f, Creature c) => c != null && !f.Dead.Contains(c) && (f.Sides != null ? !f.Over || c == f.M : c == f.P ? !f.Over : c == f.Pet ? !f.PetDead : true);

        private static void Unhandled(Fight f, string what) => f.Unhandled[what] = f.Unhandled.GetValueOrDefault(what) + 1;

        /// <summary>
        /// Stamina a player loses on evading a melee or missile attack (Player.OnEvade: a trained defense and Endurance save it sometimes)
        /// </summary>
        private static void EvadeStamina(Player p, CombatType type)
        {
            var skill = p.GetCreatureSkill(type == CombatType.Missile ? Skill.MissileDefense : Skill.MeleeDefense);
            var lose = true;
            if (skill.AdvancementClass >= SkillAdvancementClass.Trained)
            {
                var end = (int)p.Endurance.Base;
                var save = Math.Clamp(end * end * 0.000005f + end * 0.00124f - 0.07f, 0.0f, 0.75f);
                lose = save <= ThreadSafeRandom.Next(0.0f, 1.0f);
            }
            if (lose && p.Stamina.Current > 0)
                p.Stamina.Current--;
        }

        /// <summary>
        /// A monster's (or pet's) next attack, as Monster_Tick / Monster_Combat decide it: a spell roll from its spellbook, else melee or
        /// missile; executed when its timers allow (melee/missile: NextAttackTime; magic: PrevAttackTime + AiUseMagicDelay), never
        /// during its previous animation, on its 0.2 s think tick
        /// </summary>
        private static void MobDecide(Fight f, MobAi ai, double t)
        {
            if (f.Over || !Alive(f, ai.C))
                return;
            var c = ai.C;
            var type = c.GetNextAttackType();
            var spell = type == CombatType.Magic ? CurrentSpellProperty.GetValue(c) as Spell : null;
            var magicDelay = c.GetProperty(PropertyFloat.AiUseMagicDelay) ?? 3.0;
            var when = type == CombatType.Magic ? Math.Max(ai.Prev + magicDelay, ai.AnimEnd) : Math.Max(ai.Next, ai.AnimEnd);
            when = Quantize(Math.Max(when, t));
            f.At(when, () => MobExecute(f, ai, type, spell));
        }

        private static void MobExecute(Fight f, MobAi ai, CombatType type, Spell spell)
        {
            if (f.Over || !Alive(f, ai.C))
                return;
            var c = ai.C;
            var t = f.T;
            var target = ai.Target;
            if (f.Pack != null && c == f.Pet && !Alive(f, target))
                target = ai.Target = f.M;   // a pack: the pet moves on to the player's next monster
            if (f.Allies != null && !Alive(f, target) && c == f.M)
                target = ai.Target = f.Allies.FirstOrDefault(x => x is Player && Alive(f, x)) ?? f.Allies.FirstOrDefault(x => Alive(f, x));
            else if (target == f.Pet && f.PetDead)
                target = ai.Target = f.P;
            if (target == null || !Alive(f, target))
            {
                f.At(t + 0.2, () => MobDecide(f, ai, f.T));
                return;
            }
            if (c == f.M)
            {
                f.MobAttacks++;
                if (target is not Player)
                    f.AttacksOnPet++;
            }

            var powerup = (float)(c.PowerupTime ?? 1.0f);
            var stance = c.CurrentMotionState.Stance;

            if (type == CombatType.Magic && spell != null)
            {
                var aiUsesMana = c.GetProperty(PropertyBool.AiUsesMana) ?? true;
                if (aiUsesMana)
                {
                    var manaTarget = spell.IsSelfTargeted || spell.NonComponentTargetType == ItemType.None ? c : target;
                    var cost = c.CalculateManaUsage(c, spell, manaTarget);
                    if (cost > c.Mana.Current)
                    {
                        // MagicAttack returns without attacking; the next tick rolls again
                        f.At(t + 0.2, () => MobDecide(f, ai, f.T));
                        return;
                    }
                    c.Mana.Current -= cost;
                }
                var preCast = MonsterPreCast(c, spell);
                f.At(t + preCast, () => CastAt(f, c, target, spell));
                var postCast = c.GetPostCastTime(spell);
                ai.Prev = t + preCast;
                ai.AnimEnd = t + preCast + postCast;
                ai.Next = ai.Prev + postCast + ThreadSafeRandom.Next(0.0f, powerup);
                f.At(t, () => MobDecide(f, ai, f.T));
                return;
            }

            if (type == CombatType.Missile)
            {
                var weapon = c.GetEquippedMissileWeapon();
                var ammo = weapon == null ? null : weapon.IsAmmoLauncher ? c.GetEquippedAmmo() : weapon;
                if (weapon != null && ammo != null)
                {
                    c.CurrentAttack = CombatType.Missile;
                    c.AttackHeight = c.ChooseAttackHeight();
                    var launch = MotionTable.GetAnimationLength(c.MotionTableId, stance, MotionCommand.AimLevel, 1.0f);
                    var reload = MotionTable.GetAnimationLength(c.MotionTableId, stance, MotionCommand.Reload, c.GetAnimSpeed());
                    var link = MotionTable.GetAnimationLength(c.MotionTableId, stance, reload > 0 ? MotionCommand.Reload : MotionCommand.AimLevel, MotionCommand.Ready);
                    var projectile = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(ammo.WeenieClassId), new ObjectGuid(SurvivalProjectileGuid));
                    projectile.ProjectileSource = c;
                    projectile.ProjectileTarget = target;
                    projectile.ProjectileLauncher = c.GetEquippedMissileLauncher();
                    projectile.ProjectileAmmo = ammo;
                    projectile.Location = new Position(c.Location);
                    var height = c.AttackHeight;
                    f.At(t + launch + 0.1, () =>
                    {
                        if (f.Over || !Alive(f, target))
                            return;
                        c.CurrentAttack = CombatType.Missile;
                        c.AttackHeight = height;
                        var de = DamageEvent.CalculateDamage(c, target, projectile);
                        if (c == f.M) f.MobSwings++;
                        if (de.HasDamage && !de.GeneralFailure)
                        {
                            if (c == f.M) { f.MobLands++; if (target is Player) f.Missile += de.Damage; }
                            Hurt(f, target, de.Damage, c, true);
                        }
                        else if (target is Player evader && de.Evaded)
                            EvadeStamina(evader, CombatType.Missile);
                    });
                    ai.Prev = t;
                    ai.AnimEnd = t + launch + reload + link;
                    ai.Next = t + launch + reload + link + Creature.MissileDelay;
                    f.At(t, () => MobDecide(f, ai, f.T));
                    return;
                }
                Unhandled(f, "missile weapon without ammunition: melee instead");
            }

            // melee (also the fallback)
            c.CurrentAttack = CombatType.Melee;
            var motion = c.GetCombatManeuver();
            if (motion == null)
            {
                Unhandled(f, "no combat maneuver");
                ai.Next = t + 1.0;
                f.At(t + 1.0, () => MobDecide(f, ai, f.T));
                return;
            }
            var animSpeed = c.GetAnimSpeed() * (c.IsDualWieldAttack ? 1.2f : 1.0f);
            var animLength = MotionTable.GetAnimationLength(c.MotionTableId, stance, motion.Value, animSpeed);
            var frames = MotionTable.GetAttackFrames(c.MotionTableId, stance, motion.Value);
            if (frames.Count == 0)
                frames = new List<(float time, AttackHook attackHook)> { (1.0f / 3.0f, null) };
            var meleeWeapon = c.GetEquippedWeapon();
            var hook = frames[0].attackHook;
            var attackHeight = c.AttackHeight;
            var attackType = c.AttackType;
            foreach (var frame in frames)
            {
                f.At(t + frame.time * animLength, () =>
                {
                    if (f.Over || !Alive(f, target))
                        return;
                    c.CurrentAttack = CombatType.Melee;
                    c.AttackHeight = attackHeight;
                    c.AttackType = attackType;
                    var de = DamageEvent.CalculateDamage(c, target, meleeWeapon, motion.Value, hook);
                    if (c == f.M) f.MobSwings++;
                    if (de.HasDamage && !de.GeneralFailure)
                    {
                        if (c == f.M) { f.MobLands++; if (target is Player) f.Melee += de.Damage; }
                        Hurt(f, target, de.Damage, c, true);
                    }
                    else if (target is Player evader && de.Evaded)
                        EvadeStamina(evader, CombatType.Melee);
                });
            }
            ai.Prev = t;
            ai.AnimEnd = t + Math.Max(animLength, 0.5f);
            ai.Next = t + Math.Max(animLength, 0.5f) + ThreadSafeRandom.Next(0.0f, powerup);
            f.At(t, () => MobDecide(f, ai, f.T));
        }

        /// <summary>
        /// Monster_Magic.PreCastMotion's length, without the broadcast: the CastSpell motion at speed 2, or a human caster's windup
        /// and cast gestures
        /// </summary>
        private static float MonsterPreCast(Creature c, Spell spell)
        {
            var stance = c.CurrentMotionState.Stance;
            if (c.GetProperty(PropertyBool.AiUseHumanMagicAnimations) ?? false)
            {
                spell.Formula.GetMonsterFormula();
                var cast = MotionTable.GetAnimationLength(c.MotionTableId, stance, spell.Formula.CastGesture, 2.0f);
                if (cast > 0)
                    return cast + spell.Formula.WindupGestures.Sum(g => MotionTable.GetAnimationLength(c.MotionTableId, stance, g, 2.0f));
            }
            return MotionTable.GetAnimationLength(c.MotionTableId, stance, MotionCommand.CastSpell, 2.0f);
        }

        /// <summary>
        /// How many projectiles of a spell hit one target standing a metre or two in front of the caster (an estimate from the spell's
        /// layout, as Creature.CalculateProjectileOrigins spreads them): rings and fans hit with the middle one; side-by-side rows (a
        /// volley) with the middle column; projectiles queued one behind another (a wave) all hit
        /// </summary>
        private static int ProjectileHits(Spell spell)
        {
            var n = Math.Max(1, (int)spell.NumProjectiles);
            if (n == 1 || spell.SpreadAngle > 0)
                return 1;
            var dimsX = Math.Max(1, (int)(spell._spell.DimsOriginX ?? n));
            var dimsY = Math.Max(1, (int)(spell._spell.DimsOriginY ?? 1));
            var dimsZ = Math.Max(1, (int)(spell._spell.DimsOriginZ ?? 1));
            var made = Math.Min(n, dimsX * dimsY * dimsZ);
            return Math.Max(1, (int)Math.Ceiling(made / (double)dimsX));
        }

        /// <summary>
        /// A spell lands, as Creature.CastSpell / WorldObject.HandleCastSpell route it, on a player, pet or monster
        /// </summary>
        private static void CastAt(Fight f, Creature caster, Creature target, Spell spell)
        {
            if (f.Over || !Alive(f, caster))
                return;
            var self = spell.IsSelfTargeted || spell.IsBeneficial && !spell.IsHarmful;
            var to = self ? caster : target;
            if (to == null || !Alive(f, to))
                return;
            if (caster == f.M && to == f.P)
                f.SpellsAtPlayer++;

            if (spell.NumProjectiles > 0)
            {
                uint lifeDamage = 0;
                if (spell.School == MagicSchool.LifeMagic)
                {
                    // the caster pays for a life projectile (WorldObject.HandleCastSpell_Projectile)
                    if (spell.DamageType.HasFlag(DamageType.Mana))
                    {
                        lifeDamage = (uint)Math.Round(caster.Mana.Current * spell.DrainPercentage);
                        caster.Mana.Current -= Math.Min(caster.Mana.Current, lifeDamage);
                    }
                    else if (spell.DamageType.HasFlag(DamageType.Stamina))
                    {
                        lifeDamage = (uint)Math.Round(caster.Stamina.Current * spell.DrainPercentage);
                        caster.Stamina.Current -= Math.Min(caster.Stamina.Current, lifeDamage);
                    }
                    else if (spell.DamageType.HasFlag(DamageType.Health))
                    {
                        lifeDamage = (uint)Math.Round(caster.Health.Current * spell.DrainPercentage);
                        caster.Health.Current = (uint)Math.Max(1, (long)caster.Health.Current - lifeDamage);
                    }
                }
                var hits = ProjectileHits(spell);
                for (var i = 0; i < hits; i++)
                    SpellHit(f, caster, to, spell, lifeDamage, caster.GetEquippedWand());
                return;
            }

            // non-projectile spells can be resisted on landing (WorldObject.TryResistSpell)
            if (!self && to != caster && Resisted(caster, to, spell, null))
            {
                if (to == f.P)
                    f.SpellsResisted++;
                return;
            }

            switch (spell.MetaSpellType)
            {
                case SpellType.Transfer:
                    Transfer(f, caster, to, spell);
                    break;
                case SpellType.Boost:
                    Boost(f, caster, to, spell);
                    break;
                case SpellType.Enchantment:
                    if (spell.School == MagicSchool.ItemEnchantment && !self)
                    {
                        Unhandled(f, $"item debuff {spell.Name}");
                        break;
                    }
                    var result = to.EnchantmentManager.Add(spell, caster, caster.GetEquippedWand());
                    if (spell.IsDamageOverTime && result?.Enchantment != null)
                    {
                        var type = result.Enchantment.StatModKey == (uint)PropertyInt.NetherOverTime ? DamageType.Nether : DamageType.Undef;
                        f.Dots.Add((f.T + result.Enchantment.Duration, result.Enchantment.StatModValue, type, caster, to));
                    }
                    break;
                default:
                    Unhandled(f, $"{spell.MetaSpellType} {spell.Name}");
                    break;
            }
        }

        /// <summary>
        /// The magic defense roll (WorldObject.MagicDefenseCheck: caster's skill in the school, or the casting item's spellcraft, against the
        /// target's effective magic defense)
        /// </summary>
        private static bool Resisted(Creature caster, Creature target, Spell spell, WorldObject itemCaster)
        {
            if (!spell.IsResistable && spell.Category != SpellCategory.ManaConversionModLowering || spell.IsSelfTargeted)
                return false;
            var skill = itemCaster?.ItemSpellcraft != null ? (uint)itemCaster.ItemSpellcraft.Value : caster.GetCreatureSkill(spell.School).Current;
            return WorldObject.MagicDefenseCheck(skill, target.GetEffectiveMagicDefense(), out _);
        }

        /// <summary>
        /// One spell projectile hitting: the resist roll, then SpellProjectile.CalculateDamage (the server's war/void/life projectile
        /// damage). CalculateDamage rolls the resist again and, on a resist, writes to the target player's session, which a bench player
        /// doesn't have; so the roll is made here and the inner one is made to pass (the casting item's spellcraft, or the caster's skill
        /// for the call, raised out of reach; neither enters the damage of a creature's or a caster item's spell).
        /// </summary>
        private static double SpellHit(Fight f, Creature caster, Creature target, Spell spell, uint lifeDamage, WorldObject launcher)
        {
            if (!Alive(f, target))
                return 0;
            if (Resisted(caster, target, spell, null))
            {
                if (target == f.P)
                    f.SpellsResisted++;
                return 0;
            }
            if (WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(spell.Wcid), new ObjectGuid(SurvivalProjectileGuid)) is not SpellProjectile sp)
            {
                Unhandled(f, $"no projectile for {spell.Name}");
                return 0;
            }
            sp.Spell = spell;
            sp.SpellType = SpellProjectile.GetProjectileSpellType(spell.Id);
            sp.ProjectileSource = caster;
            sp.ProjectileTarget = target;
            sp.ProjectileLauncher = launcher;
            sp.LifeProjectileDamage = lifeDamage;
            sp.Location = new Position(caster.Location);

            float? damage;
            bool crit = false, critDefended = false, overpower = false;
            if (launcher != null && caster is Player)
            {
                var old = launcher.ItemSpellcraft;
                sp.IsWeaponSpell = true;
                launcher.ItemSpellcraft = 1000000;
                try { damage = sp.CalculateDamage(caster, target, ref crit, ref critDefended, ref overpower); }
                finally { launcher.ItemSpellcraft = old; }
            }
            else
            {
                var skill = caster.GetCreatureSkill(spell.School);
                skill.InitLevel += 1000000;
                try { damage = sp.CalculateDamage(caster, target, ref crit, ref critDefended, ref overpower); }
                finally { skill.InitLevel -= 1000000; }
            }
            if (damage == null)
                return 0;
            if (spell.MetaSpellType == SpellType.EnchantmentProjectile)
            {
                target.EnchantmentManager.Add(spell, caster, launcher);
                return 0;
            }
            if (caster == f.M && target == f.P)
                f.Magic += damage.Value;
            Hurt(f, target, damage.Value, caster, true);
            return damage.Value;
        }

        private static WorldObjects.Entity.CreatureVital Vital(Creature c, PropertyAttribute2nd v) => v == PropertyAttribute2nd.Mana || v == PropertyAttribute2nd.MaxMana ? c.Mana
            : v == PropertyAttribute2nd.Stamina || v == PropertyAttribute2nd.MaxStamina ? c.Stamina : c.Health;

        /// <summary>
        /// WorldObject.HandleCastSpell_Transfer (drains), on the bench's vitals
        /// </summary>
        private static void Transfer(Fight f, Creature caster, Creature target, Spell spell)
        {
            var source = spell.TransferFlags.HasFlag(TransferFlags.CasterSource) ? caster : target;
            var destination = spell.TransferFlags.HasFlag(TransferFlags.CasterDestination) ? caster : target;
            var isDrain = spell.TransferFlags.HasFlag(TransferFlags.TargetSource | TransferFlags.CasterDestination);
            var drainMod = isDrain ? (float)source.GetResistanceMod((ResistanceType)DrainResistMethod.Invoke(null, new object[] { spell.Source })) : 1.0f;
            var srcVital = Vital(source, spell.Source);
            var srcChange = (uint)Math.Round(srcVital.Current * spell.Proportion * drainMod);
            if (spell.TransferCap != 0 && srcChange > spell.TransferCap)
                srcChange = (uint)spell.TransferCap;
            var boostMod = isDrain ? (float)destination.GetResistanceMod((ResistanceType)BoostResistVitalMethod.Invoke(null, new object[] { spell.Destination })) : 1.0f;
            var destChange = (uint)Math.Round(srcChange * (1.0f - spell.LossPercent) * boostMod);
            var destVital = Vital(destination, spell.Destination);
            var maxDest = destVital.Missing;
            if (spell.TransferCap != 0 && maxDest > spell.TransferCap)
                maxDest = (uint)spell.TransferCap;
            if (destChange > maxDest)
            {
                var scalar = destChange > 0 ? (float)maxDest / destChange : 0;
                srcChange = (uint)Math.Round(srcChange * scalar);
                destChange = maxDest;
            }
            if (srcVital == source.Health)
            {
                if (source == f.P && caster == f.M)
                    f.Drain += srcChange;
                Hurt(f, source, srcChange, caster);
            }
            else
                srcVital.Current -= Math.Min(srcVital.Current, srcChange);
            destVital.Current = Math.Min(destVital.MaxValue, destVital.Current + destChange);
        }

        /// <summary>
        /// WorldObject.HandleCastSpell_Boost (harm / heal), on the bench's vitals
        /// </summary>
        private static void Boost(Fight f, Creature caster, Creature target, Spell spell)
        {
            var min = Math.Min(spell.Boost, spell.MaxBoost);
            var max = Math.Max(spell.Boost, spell.MaxBoost);
            var resistMethod = min > 0 ? BoostResistTypeMethod : DrainResistTypeMethod;
            var resistType = (ResistanceType)resistMethod.Invoke(null, new object[] { spell.VitalDamageType });
            var boost = (int)Math.Round(ThreadSafeRandom.Next(min, max) * target.GetResistanceMod(resistType));
            var vital = spell.VitalDamageType == DamageType.Mana ? target.Mana : spell.VitalDamageType == DamageType.Stamina ? target.Stamina : target.Health;
            if (boost < 0 && vital == target.Health)
            {
                if (target == f.P && caster == f.M)
                    f.Drain += -boost;
                Hurt(f, target, -boost, caster);
            }
            else if (boost < 0)
                vital.Current -= Math.Min(vital.Current, (uint)-boost);
            else
                vital.Current = Math.Min(vital.MaxValue, vital.Current + (uint)boost);
        }

        /// <summary>
        /// Every ~5 seconds: vitals regenerate (Creature.VitalHeartBeat's formula: regen rate x attribute x stance x enchantment mods),
        /// DoTs tick (EnchantmentManager.ApplyDamageTick's formula), an elite's heartbeat fires, the monster may pick a new target
        /// </summary>
        private static void Heartbeat(Fight f, MobAi monsterAi)
        {
            if (f.Over)
                return;
            var regen = f.Allies != null ? f.Allies.Where(a => Alive(f, a)).Append(f.M).ToArray() : new[] { (Creature)f.P, f.M, f.PetDead ? null : f.Pet };
            if (f.Pack != null)
                regen = regen.Where(c => c != f.M).Concat(f.Pack.Where(c => Alive(f, c))).ToArray();
            foreach (var c in regen)
            {
                if (c == null)
                    continue;
                foreach (var vital in new[] { c.Health, c.Stamina, c.Mana })
                {
                    if (vital.RegenRate == 0 || vital.Current >= vital.MaxValue)
                        continue;
                    var tick = vital.RegenRate * c.GetAttributeMod(vital) * c.GetStanceMod(vital) * c.EnchantmentManager.GetRegenerationMod(vital);
                    var add = (int)Math.Round(tick);
                    if (add > 0)
                        vital.Current = Math.Min(vital.MaxValue, vital.Current + (uint)add);
                }
            }

            f.Dots.RemoveAll(d => d.End < f.T);
            foreach (var dot in f.Dots.ToList())
            {
                if (!Alive(f, dot.Target))
                    continue;
                var target = dot.Target;
                var mod = target.GetResistanceMod(dot.Type, dot.Caster, null) * target.GetDamageResistRatingMod(CombatType.Magic, target is Player)
                    * Creature.GetNegativeRatingMod(target.GetDotResistanceRating());
                var amount = dot.Tick * mod;
                if (target == f.P && dot.Caster == f.M)
                    f.Dot += amount;
                Hurt(f, target, amount, dot.Caster);
            }

            if (f.M.IsAcxElite && !f.Over)
                EliteHeartbeat(f, monsterAi);

            // targeting (Monster_Awareness.FindNextTarget every 5 s): a tactic drawn from the monster's own (default Random | TopDamager)
            if (f.Allies != null)
            {
                // Monster_Awareness.FindNextTarget over everyone on the players' side (all stand a metre or so away, so Random / Nearest are even)
                var candidates = f.Allies.Where(a => Alive(f, a)).ToList();
                if (candidates.Count > 0)
                {
                    var tactics = f.M.TargetingTactic == TargetingTactic.None ? TargetingTactic.Random | TargetingTactic.TopDamager : f.M.TargetingTactic;
                    var flags = tactics.GetFlags();
                    var pick = (TargetingTactic)flags[flags.Count > 1 ? ThreadSafeRandom.Next(1, flags.Count - 1) : 0];
                    monsterAi.Target = pick switch
                    {
                        TargetingTactic.LastDamager when f.LastDamager is Creature last && candidates.Contains(last) => last,
                        TargetingTactic.TopDamager => candidates.OrderByDescending(c => f.DmgToMonster.GetValueOrDefault(c)).First(),
                        TargetingTactic.Weakest => candidates.OrderBy(c => c.Level ?? 0).First(),
                        TargetingTactic.Strongest => candidates.OrderByDescending(c => c.Level ?? 0).First(),
                        TargetingTactic.Focused when candidates.Contains(monsterAi.Target) => monsterAi.Target,
                        _ => candidates[ThreadSafeRandom.Next(0, candidates.Count - 1)],
                    };
                }
            }
            else if (f.Pet != null && !f.PetDead)
            {
                var tactics = f.M.TargetingTactic == TargetingTactic.None ? TargetingTactic.Random | TargetingTactic.TopDamager : f.M.TargetingTactic;
                var flags = tactics.GetFlags();
                var pick = (TargetingTactic)flags[flags.Count > 1 ? ThreadSafeRandom.Next(1, flags.Count - 1) : 0];
                switch (pick)
                {
                    case TargetingTactic.Random:
                    case TargetingTactic.Nearest:
                        // both stand a metre or so away, so the distance weighting is about even
                        monsterAi.Target = ThreadSafeRandom.Next(0, 1) == 0 ? f.P : f.Pet;
                        break;
                    case TargetingTactic.LastDamager:
                        monsterAi.Target = f.LastDamagerPet ? f.Pet : f.P;
                        break;
                    case TargetingTactic.TopDamager:
                        monsterAi.Target = f.DmgByPet > f.DmgByPlayer ? f.Pet : f.P;
                        break;
                    case TargetingTactic.Weakest:
                        monsterAi.Target = (f.Pet.Level ?? 0) < (f.P.Level ?? 0) ? f.Pet : f.P;
                        break;
                    case TargetingTactic.Strongest:
                        monsterAi.Target = (f.Pet.Level ?? 0) > (f.P.Level ?? 0) ? f.Pet : f.P;
                        break;
                }
            }
            else
                monsterAi.Target = f.P;

            f.At(f.T + 5.0, () => Heartbeat(f, monsterAi));
        }

        /// <summary>
        /// Creature_AcxElite.AcxEliteHeartbeat's attack and ward mods, through the bench's spell path
        /// </summary>
        private static void EliteHeartbeat(Fight f, MobAi ai)
        {
            var m = f.M;
            var mods = m.AcxEliteMods;
            var target = ai.Target ?? f.P;
            if (mods.HasFlag(AcxEliteMod.Warded))
            {
                // Creature.AcxSwitchWard's rule, without its shield effect broadcast: pick one of melee / missile / magic, then half the
                // time switch to one of the other two
                var ward = m.GetProperty(PropertyInt.AcxEliteWard) ?? 0;
                if (ward == 0)
                    ward = ThreadSafeRandom.Next(1, 3);
                else if (ThreadSafeRandom.Next(1, 100) >= 50)
                    ward = (ward + ThreadSafeRandom.Next(0, 1)) % 3 + 1;
                m.SetProperty(PropertyInt.AcxEliteWard, ward);
            }
            if (mods.HasFlag(AcxEliteMod.Disco) && ThreadSafeRandom.Next(1, 10) <= 7)
            {
                var spell = new Spell(m.Level > 99 ? SpellId.AcidArc5 : SpellId.AcidArc2);
                f.At(f.T + 2.5, () => CastAt(f, m, target, spell));
                f.At(f.T + 5.0, () => CastAt(f, m, target, spell));
            }
            if (mods.HasFlag(AcxEliteMod.Meteor) && ThreadSafeRandom.Next(1, 10) <= 5)
            {
                var spells = EliteSpells("AcxMeteorSpells");
                CastAt(f, m, target, new Spell(spells[ThreadSafeRandom.Next(0, spells.Length - 1)]));
            }
            if (mods.HasFlag(AcxEliteMod.Nova) && ThreadSafeRandom.Next(1, 10) <= 5)
            {
                var spells = EliteSpells("AcxNovaSpells");
                var spell = new Spell(spells[ThreadSafeRandom.Next(0, spells.Length - 1)]);
                if (!spell.NotFound)
                    CastAt(f, m, target, spell);
            }
            if (mods.HasFlag(AcxEliteMod.Support) && ThreadSafeRandom.Next(1, 10) <= 5)
            {
                var spells = EliteSpells(m.Level > 99 ? "AcxSupportSpellsHigh" : "AcxSupportSpellsLow");
                CastAt(f, m, target, new Spell(spells[ThreadSafeRandom.Next(0, spells.Length - 1)]));
            }
        }

        /// <summary>
        /// The player's next action: a kit (healing mode, under the health threshold; stamina / mana kits when nearly out), else an
        /// attack at its real cycle. Melee: the swing animation at the attack speed (ACX single weapon speed included) + the power bar
        /// refill; missile: aim + reload + back to ready + the accuracy bar; magic: windup and cast gestures at the cast speed + back
        /// to ready.
        /// </summary>
        private static void PlayerAct(Fight f, Side side)
        {
            if (f.Over)
                return;
            var p = f.P;
            var t = f.T;

            // kits: health only in the healing mode; stamina and mana kits in both (they are how a melee or mage keeps going, not healing)
            {
                Healer kit = null;
                if (f.Heal && p.Health.Current < p.Health.MaxValue * f.HealAt)
                    kit = f.HealthKit;
                else if (f.Build.Style != "magic" && p.Stamina.Current < p.Stamina.MaxValue * 0.2)
                    kit = f.StaminaKit;
                else if (f.Build.Style == "magic" && p.Mana.Current < 3 * p.CalculateManaUsage(p, f.Gear.Bolt, f.M))
                    kit = f.ManaKit;
                if (kit != null)
                {
                    if (kit == f.HealthKit) f.Kits++; else f.OtherKits++;
                    var vital = kit == f.HealthKit ? p.Health : kit == f.StaminaKit ? p.Stamina : p.Mana;
                    var missing = vital.Missing;
                    f.At(t + f.HealTime, () =>
                    {
                        if (f.Over)
                            return;
                        var difficulty = 0;
                        if (kit.DoSkillCheck(p, p, missing, ref difficulty))
                        {
                            var amount = kit.GetHealAmount(p, p, missing, out _, out var staminaCost);
                            p.Stamina.Current -= Math.Min(p.Stamina.Current, staminaCost);
                            vital.Current = Math.Min(vital.MaxValue, vital.Current + amount);
                        }
                        Act(f, side);
                    });
                    return;
                }
            }

            if (f.Build.Style != "magic")
            {
                var cost = (uint)p.GetAttackStamina(PowerAccuracy.High);
                if (p.Stamina.Current == 0)
                {
                    f.StaminaOut++;
                    f.At(t + 1.0, () => Act(f, side));
                    return;
                }
                p.Stamina.Current -= Math.Min(p.Stamina.Current, cost);
            }

            if (f.Gear.Aetheria.Count > 0)
                TrySurges(f);

            if (f.Build.Style == "melee")
            {
                var stance = p.CurrentMotionState.Stance;
                var dualWield = p.IsDualWieldAttack;
                var animSpeed = p.GetAnimSpeed() * p.GetAcxSingleWeaponAnimSpeedMod() * (dualWield ? 1.2f : 1.0f);   // Player_Melee: dual wield swings 20% faster
                var motion = p.GetSwingAnimation();   // alternates the hand for dual wield (Player.DualWieldAlternate)
                var animLength = MotionTable.GetAnimationLength(p.MotionTableId, stance, motion, animSpeed);
                var frames = MotionTable.GetAttackFrames(p.MotionTableId, stance, motion);
                if (frames.Count == 0)
                    frames = new List<(float time, AttackHook attackHook)> { (0.5f, null) };
                var weapon = p.GetEquippedMeleeWeapon() ?? f.Gear.Weapon;   // the swinging hand's weapon
                var bleeds = weapon.W_WeaponType == WeaponType.Dagger || (weapon.W_WeaponType == WeaponType.Sword && weapon.W_AttackType.IsMultiStrike());
                var scale = PropertyManager.GetDouble("acx_bleed_damage_scale").Item;
                var swing = 0.0;
                for (var i = 0; i < frames.Count; i++)
                {
                    var last = i == frames.Count - 1;
                    var first = i == 0;
                    f.At(t + frames[i].time * animLength, () =>
                    {
                        if (f.Over)
                            return;
                        f.PlayerAttacks++;
                        var cleaveFrom = f.M;
                        var de = DamageEvent.CalculateDamage(p, f.M, weapon);
                        if (de.HasDamage && !de.GeneralFailure)
                        {
                            f.PlayerLands++;
                            swing += de.Damage;
                            Hurt(f, f.M, de.Damage, p);
                        }
                        // a pack: a cleaving weapon also strikes its CleaveTargets of the others (Player_Melee: DamageTarget at
                        // acx_extra_target_damage, no procs)
                        if (f.Pack != null && weapon.IsCleaving)
                            foreach (var other in f.Pack.Where(c => c != cleaveFrom && Alive(f, c)).Take(weapon.CleaveTargets).ToList())
                            {
                                var ce = DamageEvent.CalculateDamage(p, other, weapon);
                                if (ce.HasDamage && !ce.GeneralFailure)
                                    Hurt(f, other, ce.Damage * Player.GetAcxExtraTargetDamageMod(), p);
                            }
                        // Project ACX dual wield "double hit" (Player.AcxDualWieldFollowUp, called by Player_Melee after the first strike): the
                        // other hand's weapon strikes too, for acx_dual_wield_follow_up of a hit; DamageTarget's own path (CalculateDamage with
                        // the other weapon as the damage source, then its damage mod), no bleed
                        if (first && dualWield)
                        {
                            var share = PropertyManager.GetDouble("acx_dual_wield_follow_up").Item;
                            var main = p.GetEquippedMeleeWeapon(true);
                            var other = weapon == main ? p.GetDualWieldWeapon() : main;
                            if (share > 0 && other != null && other != weapon)
                            {
                                f.PlayerAttacks++;
                                // DamageEvent takes the weapon (imbue, crit, cleave, damage type) from the hand DualWieldAlternate names, so flip it to
                                // the other hand for the follow-up and back, as Player.AcxDualWieldFollowUp does
                                p.DualWieldAlternate = !p.DualWieldAlternate;
                                DamageEvent follow;
                                try { follow = DamageEvent.CalculateDamage(p, f.M, other); }
                                finally { p.DualWieldAlternate = !p.DualWieldAlternate; }
                                if (follow.HasDamage && !follow.GeneralFailure)
                                {
                                    f.PlayerLands++;
                                    Hurt(f, f.M, follow.Damage * share, p);
                                }
                            }
                        }
                        // bleeds at their steady rate (as /acxbench): 1 in 10 attacks cashes in the running bleeds
                        if (last && bleeds && swing > 0)
                            Hurt(f, f.M, 0.9 * Math.Round((5 + (int)Math.Round(swing)) * scale), p);
                    });
                }
                f.At(t + animLength + p.PowerLevel * p.GetAcxSingleWeaponRefillMod() * (dualWield ? 0.8f : 1.0f), () => Act(f, side));   // dual wield's bar refills 20% faster
                return;
            }

            if (f.Build.Style == "missile")
            {
                var stance = p.CurrentMotionState.Stance;
                var aim = MotionTable.GetAnimationLength(p.MotionTableId, stance, MotionCommand.AimLevel, p.AcxThrowSpeedMod(f.Gear.Ammo));
                var reload = MotionTable.GetAnimationLength(p.MotionTableId, stance, MotionCommand.Reload, p.GetAnimSpeed() * p.AcxThrowSpeedMod(f.Gear.Ammo));
                var link = MotionTable.GetAnimationLength(p.MotionTableId, stance, MotionCommand.Reload, MotionCommand.Ready);
                var projectile = f.Projectile;
                f.At(t + aim + 0.1, () =>
                {
                    if (f.Over || f.Dead.Contains(p))
                        return;
                    f.PlayerAttacks++;
                    var target = f.M;
                    var de = DamageEvent.CalculateDamage(p, target, projectile);
                    var hit = de.HasDamage && !de.GeneralFailure;
                    if (hit)
                    {
                        f.PlayerLands++;
                        Hurt(f, target, de.Damage, p);
                    }
                    if (AcxBombWcids.IsDamageBomb(projectile.WeenieClassId))
                        BenchBombBlast(f, p, projectile, target, hit);
                });
                f.At(t + aim + reload + link + p.AccuracyLevel, () => Act(f, side));
                return;
            }

            // magic
            var manaCost = p.CalculateManaUsage(p, f.Gear.Bolt, f.M);
            if (p.Mana.Current < manaCost)
            {
                f.ManaOut++;
                f.At(t + 1.0, () => Act(f, side));
                return;
            }
            p.Mana.Current -= manaCost;
            var bolt = f.Gear.Bolt;
            var wand = f.Gear.Weapon;
            f.At(t + f.CastLand, () =>
            {
                if (f.Over)
                    return;
                f.PlayerAttacks++;
                if (!f.Dead.Contains(p) && SpellHit(f, p, f.M, bolt, 0, wand) > 0)
                    f.PlayerLands++;
            });
            f.At(t + f.CastCycle, () => Act(f, side));
        }

        /// <summary>
        /// A player spell's cycle (Player_Magic: windup gestures and the cast gesture at CastSpeed 2, then back to ready), and when the
        /// spell leaves the hand
        /// </summary>
        private static (double Cycle, double Land) CastTiming(Player p, Spell spell)
        {
            var stance = MotionStance.Magic;
            var windups = spell.Formula.WindupGestures.Where(g => g != MotionCommand.Invalid && (uint)g != 0x80000000u)
                .Sum(g => MotionTable.GetAnimationLength(p.MotionTableId, stance, g, Player.CastSpeed));
            var gesture = spell.Formula.CastGesture == MotionCommand.Invalid ? MotionCommand.Ready : spell.Formula.CastGesture;
            var cast = MotionTable.GetAnimationLength(p.MotionTableId, stance, gesture, Player.CastSpeed);
            var back = MotionTable.GetAnimationLength(p.MotionTableId, stance, gesture, MotionCommand.Ready);
            return (windups + cast + back, windups + cast);
        }

        /// <summary>
        /// The character's attack cycle and health kit time, for the sheet (the same pieces PlayerAct and RunFight use)
        /// </summary>
        private static (double Attack, double Kit) Cycles(Player p, SurvivalBuild b, Loadout l)
        {
            var stance = p.CurrentMotionState.Stance;
            double attack;
            if (b.Style == "melee")
                attack = MotionTable.GetAnimationLength(p.MotionTableId, stance, p.GetSwingAnimation(), p.GetAnimSpeed() * p.GetAcxSingleWeaponAnimSpeedMod()) + p.PowerLevel * p.GetAcxSingleWeaponRefillMod();
            else if (b.Style == "missile")
                attack = MotionTable.GetAnimationLength(p.MotionTableId, stance, MotionCommand.AimLevel, p.AcxThrowSpeedMod(l.Ammo)) + MotionTable.GetAnimationLength(p.MotionTableId, stance, MotionCommand.Reload, p.GetAnimSpeed() * p.AcxThrowSpeedMod(l.Ammo))
                    + MotionTable.GetAnimationLength(p.MotionTableId, stance, MotionCommand.Reload, MotionCommand.Ready) + p.AccuracyLevel;
            else
                attack = CastTiming(p, l.Bolt).Cycle;
            var kit = MotionTable.GetAnimationLength(p.MotionTableId, stance, MotionCommand.SkillHealSelf) + MotionTable.GetAnimationLength(p.MotionTableId, stance, MotionCommand.SkillHealSelf, MotionCommand.Ready);
            return (attack, kit);
        }

        /// <summary>
        /// One fight on the timeline. Returns null if the setup failed.
        /// </summary>
        private static Fight RunFight(SurvivalBuild b, int level, int lumPercent, SurvivalGear gear, int version, uint monsterWcid, bool elite, uint petWcid, bool heal, double maxSeconds, double healAt = 0.55)
        {
            var p = DressSurvivor(b, level, lumPercent, gear, version, out var loadout);
            var m = MakeMonster(monsterWcid, elite);
            if (m == null)
                return null;
            var f = new Fight { Heal = heal, Max = maxSeconds, HealAt = healAt, P = p, M = m, Build = b, Gear = loadout };
            if (petWcid != 0)
                f.Pet = MakePet(petWcid);

            if (b.Style == "missile")
            {
                var projectile = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(loadout.Ammo.WeenieClassId), new ObjectGuid(ProjectileGuid));
                projectile.ProjectileSource = p;
                projectile.ProjectileTarget = m;
                projectile.ProjectileLauncher = loadout.Weapon;
                projectile.ProjectileAmmo = loadout.Ammo;
                AcxThrownWcids.CarryRoll(loadout.Ammo, projectile);
                projectile.Location = new Position(p.Location);
                f.Projectile = projectile;
            }
            if (b.Style == "magic")
                (f.CastCycle, f.CastLand) = CastTiming(p, loadout.Bolt);

            Healer Kit(uint wcid) => WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(wcid), new ObjectGuid(SurvivalKitGuid)) as Healer;
            f.HealthKit = Kit(TreatedHealingKit);
            f.StaminaKit = Kit(EternalStaminaKit);
            f.ManaKit = Kit(EternalManaKit);
            var stance = p.CurrentMotionState.Stance;
            f.HealTime = MotionTable.GetAnimationLength(p.MotionTableId, stance, MotionCommand.SkillHealSelf) + MotionTable.GetAnimationLength(p.MotionTableId, stance, MotionCommand.SkillHealSelf, MotionCommand.Ready);
            if (f.HealTime <= 0)
                f.HealTime = MotionTable.GetAnimationLength(p.MotionTableId, MotionStance.NonCombat, MotionCommand.SkillHealSelf) + 0.5;

            var monsterAi = new MobAi { C = m, Target = p };
            f.At(0, () => Act(f, null));
            f.At(0, () => MobDecide(f, monsterAi, 0));
            if (f.Pet != null)
            {
                var petAi = new MobAi { C = f.Pet, Target = m };
                f.At(0, () => MobDecide(f, petAi, 0));
            }
            f.At(5.0, () => Heartbeat(f, monsterAi));

            var events = 0;
            while (!f.Over && f.Q.TryDequeue(out var action, out var time))
            {
                if (time > maxSeconds)
                    break;
                f.T = time;
                action();
                if (++events > 200000)
                {
                    Unhandled(f, "event cap");
                    break;
                }
            }
            return f;
        }

        /// <summary>
        /// A group fight: several characters (each with its pet, if the build has one and is 180 or over) against one monster that picks its
        /// target among all of them by its own tactic every 5 seconds. Win: the monster dies before every player has.
        /// </summary>
        private static Fight RunGroupFight(List<(SurvivalBuild Build, int Version, uint Pet)> members, int level, SurvivalGear gear, uint monsterWcid, bool elite, bool heal, double maxSeconds, AcxInvasions.Town town = null)
        {
            var m = MakeMonster(monsterWcid, elite, town);
            if (m == null)
                return null;
            var f = new Fight { Heal = heal, Max = maxSeconds, HealAt = 0.55, M = m, Sides = new List<Side>(), Allies = new List<Creature>() };
            Healer Kit(uint wcid) => WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(wcid), new ObjectGuid(SurvivalKitGuid)) as Healer;
            for (var i = 0; i < members.Count; i++)
            {
                var (b, version, petWcid) = members[i];
                var p = DressSurvivor(b, level, -1, gear, version, out var loadout);
                p.Location = new Position(BenchCell, 50.0f + 0.9f * i, 50.0f, 0.0f, 0, 0, 0, 1);
                var side = new Side { P = p, Build = b, Gear = loadout, HealthKit = Kit(TreatedHealingKit), StaminaKit = Kit(EternalStaminaKit), ManaKit = Kit(EternalManaKit) };
                if (b.Style == "missile")
                {
                    var projectile = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(loadout.Ammo.WeenieClassId), new ObjectGuid(ProjectileGuid));
                    projectile.ProjectileSource = p;
                    projectile.ProjectileTarget = m;
                    projectile.ProjectileLauncher = loadout.Weapon;
                    projectile.ProjectileAmmo = loadout.Ammo;
                    AcxThrownWcids.CarryRoll(loadout.Ammo, projectile);
                    projectile.Location = new Position(p.Location);
                    side.Projectile = projectile;
                }
                if (b.Style == "magic")
                    (side.CastCycle, side.CastLand) = CastTiming(p, loadout.Bolt);
                var stance = p.CurrentMotionState.Stance;
                side.HealTime = MotionTable.GetAnimationLength(p.MotionTableId, stance, MotionCommand.SkillHealSelf) + MotionTable.GetAnimationLength(p.MotionTableId, stance, MotionCommand.SkillHealSelf, MotionCommand.Ready);
                if (side.HealTime <= 0)
                    side.HealTime = MotionTable.GetAnimationLength(p.MotionTableId, MotionStance.NonCombat, MotionCommand.SkillHealSelf) + 0.5;
                f.Sides.Add(side);
                f.Allies.Add(p);
                if (petWcid != 0 && MakePet(petWcid) is Creature pet)
                {
                    pet.Location = new Position(BenchCell, 49.6f + 0.9f * i, 51.6f, 0.0f, 0, 0, 1, 0);
                    side.Pet = pet;
                    f.Allies.Add(pet);
                    f.Pet ??= pet;
                }
            }
            (f.P, f.Gear, f.Build) = (f.Sides[0].P, f.Sides[0].Gear, f.Sides[0].Build);

            var monsterAi = new MobAi { C = m, Target = f.Sides[0].P };
            foreach (var side in f.Sides)
            {
                var s = side;
                f.At(0, () => Act(f, s));
                if (s.Pet != null)
                {
                    var petAi = new MobAi { C = s.Pet, Target = m };
                    f.At(0, () => MobDecide(f, petAi, 0));
                }
            }
            f.At(0, () => MobDecide(f, monsterAi, 0));
            f.At(5.0, () => Heartbeat(f, monsterAi));

            var events = 0;
            while (!f.Over && f.Q.TryDequeue(out var action, out var time))
            {
                if (time > maxSeconds)
                    break;
                f.T = time;
                action();
                if (++events > 400000)
                    break;
            }
            return f;
        }

        public class DuoOptions
        {
            public int Fights = 10;
            public double MaxSeconds = 900;
            public bool Towns = true;      // also the town elites, one per tier, at the town's level (solo and duo)
            public bool Top = true;        // the two top elites against L275 characters
            public List<string> Only;      // just these towns (by name)
            public bool Parties;           // also a party of four and of five
        }

        // the duos (coordinator / JJ 2026-11-16): mage + archer, mage + melee with shield, melee + melee, archer + archer, two-hand + archer
        // 2026-11-16: the Alchemist with a melee and with a mage (AcxBench_SurvivalAlchemist)
        private static readonly (string A, string B)[] Duos = { ("mage", "archer"), ("mage", "meleeshield"), ("melee", "melee"), ("archer", "archer"), ("twohand", "archer"), ("alchemist", "melee"), ("alchemist", "mage") };

        private static readonly string[][] Parties =
        {
            new[] { "meleeshield", "twohand", "archer", "mage" }, new[] { "meleeshield", "twohand", "archer", "mage", "melee" },
        };

        public static string StartDuo(DuoOptions o, Action<string> reply)
        {
            if (running)
                return "A bench is already running.";
            running = true;
            Task.Run(() =>
            {
                try { reply(RunDuo(o)); }
                catch (Exception e) { log.Error($"AcxBench duo failed: {e}"); reply($"The duo bench failed: {e.Message}"); }
                finally { running = false; }
            });
            return "Duo bench started. Results go to Logs/acxbench/.";
        }

        private static string RunDuo(DuoOptions o)
        {
            var started = DateTime.Now;
            survivalGear = 3;
            survivalLootTier = 0;
            survivalBombImbue = null;
            survivalSetOverride = null;
            survivalAetheria = null;
            survivalTrinket = null;
            // the elites: the two top invasion elites against L275 characters (fresh and veteran), and per invasion tier one town's elite
            // (towns.json: the tier's highest-level invader) against characters of the invader's level (fresh)
            var cases = new List<(string Label, uint Wcid, int Level, string Profile)>();
            if (o.Top)
                cases.AddRange(new (string, uint, int, string)[]
            {
                ("Wai Jhou: Spirit of Protection (tier 5, L250)", 48774, 275, "fresh"), ("Wai Jhou: Spirit of Protection (tier 5, L250)", 48774, 275, "veteran"),
                ("Stonehold: Sclavus Sentinel (tier 5, L225)", 72803, 275, "fresh"), ("Stonehold: Sclavus Sentinel (tier 5, L225)", 72803, 275, "veteran"),
            });
            // every town with invasions on (towns.json via AcxInvasions.Towns), its invader at the invader's level
            if (o.Towns && AcxInvasions.Towns != null)
                cases.AddRange(AcxInvasions.Towns.Where(t => t.Invasions).OrderBy(t => t.Tier).ThenBy(t => t.InvaderLevel)
                    .Select(t => ($"{t.Name}: {t.InvaderName} (tier {t.Tier}, L{t.InvaderLevel})", t.Invader, Math.Max(1, t.InvaderLevel), "fresh")));
            var sb = new StringBuilder("Elite,Wcid,PlayerLevel,Profile,Group,Mode,Fights,WinPct,KillSecMedian,DieSecMedian,MonsterAttacksOnPlayersPct\n");
            var summary = new StringBuilder();
            string Sec(double v) => double.IsInfinity(v) || double.IsNaN(v) ? "never" : F(v, "0");
            var gears = new Dictionary<int, SurvivalGear>();
            if (o.Only != null)
                cases = cases.Where(c => o.Only.Any(n => c.Label.Split(':')[0].Equals(n.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();
            foreach (var (label, wcid, level, profile) in cases)
            {
                survivalVeteran = profile == "veteran";
                // the town's strength from towns.json (AcxInvasions.Towns: /acxinvasion reload picks up edits)
                var townName = label.Split(':')[0];
                var town = AcxInvasions.Towns?.FirstOrDefault(t => t.Name.Equals(townName, StringComparison.OrdinalIgnoreCase));
                if (!gears.TryGetValue(level, out var gear))
                    gears[level] = gear = RollSurvivalGear(level);
                // each build's weapon element and pet against this elite (a short trial each, as the solo bench picks them)
                var picks = new Dictionary<string, (int Version, uint Pet)>();
                foreach (var b in SurvivalBuilds)
                {
                    var best = 0; var bestDps = -1.0;
                    for (var v = 0; v < WeaponVersions(b, gear); v++)
                    {
                        var trial = RunFight(b, level, -1, gear, v, wcid, true, 0, false, 20);
                        var dps = trial == null ? 0 : trial.DmgByPlayer / Math.Max(1.0, Math.Min(20, trial.T));
                        if (dps > bestDps) (best, bestDps) = (v, dps);
                    }
                    uint pet = 0;
                    if (b.Pet && level >= 180)   // a level 180-200 summon needs a character of about that level
                        pet = SurvivalPets200[0];
                    picks[b.Name] = (best, pet);
                }

                var groups = SurvivalBuilds.Select(b => new[] { b.Name }).Concat(Duos.Select(d => new[] { d.A, d.B })).ToList();
                if (o.Parties)   // party=1: a party of four and of five (JJ 2026-11-16: "I hope the elites at the endgame area really take a party of 4 or 5")
                    groups.AddRange(Parties);
                var duoWins = new List<(string Group, double Win)>();
                var soloWins = new List<(string Group, double Win)>();
                foreach (var group in groups)
                    foreach (var heal in new[] { false, true })
                    {
                        var members = group.Select(n => (SurvivalBuilds.First(x => x.Name == n), picks[n].Version, picks[n].Pet)).ToList();
                        var kills = new List<double>(); var deaths = new List<double>();
                        double wins = 0, onPlayers = 0, attacks = 0; var fights = 0;
                        for (var i = 0; i < o.Fights; i++)
                        {
                            Fight f;
                            try { f = RunGroupFight(members, level, gear, wcid, true, heal, o.MaxSeconds, town); }
                            catch (Exception e) { log.Warn($"[ACXSURVIVAL] duo fight failed: {e.Message}"); continue; }
                            if (f == null) continue;
                            fights++;
                            var kill = double.IsNaN(f.KillTime) ? double.PositiveInfinity : f.KillTime;
                            var death = double.IsNaN(f.DeathTime) ? double.PositiveInfinity : f.DeathTime;
                            kills.Add(kill); deaths.Add(death);
                            if (kill < death) wins++;
                            attacks += f.MobAttacks; onPlayers += f.MobAttacks - f.AttacksOnPet;
                        }
                        if (fights == 0) continue;
                        var win = wins * 100 / fights;
                        var name = string.Join(" + ", group);
                        sb.AppendLine(string.Join(",", Csv(label), wcid, level, profile, Csv(name), heal ? "kits" : "no healing", fights, F(win, "0"), Sec(MedianOf(kills)), Sec(MedianOf(deaths)),
                            F(attacks > 0 ? onPlayers * 100 / attacks : 0, "0")));
                        if (heal)
                            (group.Length > 1 ? duoWins : soloWins).Add((name, win));
                    }
                var bestDuo = duoWins.OrderByDescending(d => d.Win).FirstOrDefault();
                var worstDuo = duoWins.OrderBy(d => d.Win).FirstOrDefault();
                var bestSolo = soloWins.OrderByDescending(d => d.Win).FirstOrDefault();
                var probeElite = MakeMonster(wcid, true, town);
                summary.AppendLine($"  {label}: town healthScale {town?.HealthScale ?? 1} damageScale {town?.DamageScale ?? 1}; elite HealthRate {probeElite?.GetProperty(PropertyFloat.HealthRate)} (acx_elite_regen_scale {PropertyManager.GetDouble("acx_elite_regen_scale").Item}), health {probeElite?.Health.MaxValue}");
                summary.AppendLine($"  {label}, L{level} {profile}: solo best {bestSolo.Group} {bestSolo.Win:0}% (kits); duo best {bestDuo.Group} {bestDuo.Win:0}%, worst {worstDuo.Group} {worstDuo.Win:0}%");
            }

            var dir = Path.Combine("Logs", "acxbench");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"acxsurvival-duo-{started:yyyyMMdd-HHmmss}.csv");
            File.WriteAllText(file, sb.ToString());
            return $"Duo bench done in {(DateTime.Now - started).TotalSeconds:0}s ({o.Fights} fights per group and mode, full gear by level gates), written to {file}.\n" + summary.ToString().TrimEnd();
        }

        // ---------------------------------------------------------------------------------------------------------------
        // The run
        // ---------------------------------------------------------------------------------------------------------------

        private class SurvivalRow
        {
            public int Level;
            public string Profile, Build, Monster, Weapon, Pet = "", Mode;
            public uint Wcid;
            public int MonsterLevel, Fights, Failed;
            public bool Elite;
            public int Spots;
            public double MonsterHealth, WinPct, KillMedian, DieMedian, KitsPerFight, OtherKitsPerFight, SurgesPerFight, CloakProcsPerFight, PlayerHitPct, MobHitPct, ResistPct, PetShare, PetDiedPct, OnPetPct, StaminaOut, ManaOut;
            public double MeleeShare, MissileShare, MagicShare, DotShare, DrainShare, TakenPerSecond, DealtPerSecond;
            public string Unhandled = "";
        }

        private static double MedianOf(List<double> values) => values.Count == 0 ? double.NaN : values.OrderBy(v => v).ElementAt(values.Count / 2);

        private static string RunSurvival(SurvivalOptions o)
        {
            var started = DateTime.Now;
            survivalGear = o.Gear;
            survivalSetOverride = o.ArmorSet != null && Enum.TryParse<EquipmentSet>(o.ArmorSet, true, out var forcedSet) ? forcedSet : null;
            survivalAetheria = o.Aetheria;
            survivalTrinket = o.Trinket;
            survivalMeleeKind = o.MeleeKind;
            survivalLootTier = o.LootTier;
            survivalBombImbue = o.BombImbue;
            var builds = SurvivalBuilds.Where(b => o.Builds.Contains(b.Name)).ToList();
            if (MeleeClass(o.MeleeKind) is var (_, kindSkill))
                builds = builds.Select(b => b.Style == "melee" && b.Name != "twohand" ? BuildForSkill(b, kindSkill) : b).ToList();
            if (o.ThrownShield)
                builds = builds.Select(WithShield).ToList();
            var monsterList = (o.Monsters ?? SurvivalMonsters.ToList()).Select(w => (Wcid: w, Elite: false)).ToList();
            if (o.Elites && o.Monsters == null)
                monsterList.AddRange(SurvivalElites.Select(w => (Wcid: w, Elite: true)));
            var spawned = SpawnedMonsters().ToDictionary(mi => mi.Wcid, mi => mi);

            var rows = new List<SurvivalRow>();
            var sheets = new StringBuilder("Level,Profile,Build,Source,XpTotal,XpLeft,CreditsAvailable,CreditsForSkills,Raised,XpAugs,LuminancePct,Health,Stamina,Mana,Str,End,Coord,Quick,Focus,Self,AttackSkill,MeleeDefense,MissileDefense,MagicDefense,Healing,Shield,Summoning,AttackCycleSec,HealthKitSec,PetTier,HitCheck,Weapon,GearExtras,Gear\n");
            var summary = new StringBuilder();

            foreach (var level in o.Levels)
            {
                var gear = RollSurvivalGear(level);
                log.Info($"[ACXSURVIVAL] L{level} gear {gear.Describe}");
                foreach (var profile in o.Profiles)
                foreach (var b in builds)
                {
                    survivalVeteran = profile == "veteran";
                    // the sheet, from one dressed character
                    var sheet = new Sheet();
                    var probe = DressSurvivor(b, level, o.LumLevel, gear, 0, out var probeLoadout, sheet);
                    var attackSkill = b.Specialized[0];
                    uint Sk(Skill s) => probe.GetCreatureSkill(s).Current;
                    // the level 200 summon (570 Summoning) for pet builds: End of Retail builds reached 570 with cantrips and auras the bench
                    // leaves out (JJ: "Mages always take summoning eventually"), so it is given whatever the bench character's skill
                    var petTier = SurvivalPets200;
                    var cycle = Cycles(probe, b, probeLoadout);

                    // the hit roll's inputs against the first monster on the list, for a sanity check (DamageEvent.GetEvadeChance /
                    // WorldObject.MagicDefenseCheck: chance = SkillCheck.GetSkillChance(attack, defense))
                    var hitCheck = "";
                    var check = MakeMonster(monsterList[0].Wcid, false);
                    if (check != null)
                    {
                        if (b.Style == "magic")
                        {
                            var war = probe.GetCreatureSkill(Skill.WarMagic).Current;
                            var md = check.GetEffectiveMagicDefense();
                            hitCheck = $"War Magic {war} vs {check.Name}'s effective magic defense {md}: {SkillCheck.GetSkillChance(war, md):P0} of bolts not resisted";
                        }
                        else
                        {
                            var raw = probe.GetCreatureSkill(attackSkill).Current;
                            var offense = WorldObject.GetWeaponOffenseModifier(probe);
                            var atk = probe.GetEffectiveAttackSkill();
                            var def = check.GetEffectiveDefenseSkill(b.Style == "missile" ? CombatType.Missile : CombatType.Melee);
                            hitCheck = $"{attackSkill} {raw} x offense {offense:0.###} (weapon's Bonus to Attack Skill {probeLoadout.Weapon.WeaponOffense ?? 1:0.###} + Heart Seeker{(b.Style == "missile" ? ", x1.6 full accuracy" : "")}) = {atk} vs {check.Name}'s effective {(b.Style == "missile" ? "missile" : "melee")} defense {def}: hit {SkillCheck.GetSkillChance(atk, def):P0}";
                        }
                        check.CurrentAttack = check.GetEquippedMissileWeapon() != null ? CombatType.Missile : CombatType.Melee;
                        var monsterAttack = check.GetEffectiveAttackSkill();
                        var playerDefense = probe.ApplyAcxSingleWeaponDefense(probe.GetEffectiveDefenseSkill(check.CurrentAttack == CombatType.Missile ? CombatType.Missile : CombatType.Melee));
                        hitCheck += $"; its attack {monsterAttack} vs the player's effective defense {playerDefense}: it lands {SkillCheck.GetSkillChance(monsterAttack, playerDefense):P0}";
                    }
                    sheets.AppendLine(string.Join(",", level, profile, b.Name, Csv(b.Source), sheet.XpTotal, sheet.XpLeft, sheet.CreditsTotal, sheet.CreditsUsed, Csv(string.Join("; ", sheet.Raised)),
                        Csv(string.Join("; ", sheet.Augs)), sheet.Lum, probe.Health.MaxValue, probe.Stamina.MaxValue, probe.Mana.MaxValue,
                        probe.Strength.Current, probe.Endurance.Current, probe.Coordination.Current, probe.Quickness.Current, probe.Focus.Current, probe.Self.Current,
                        Sk(attackSkill), Sk(Skill.MeleeDefense), Sk(Skill.MissileDefense), Sk(Skill.MagicDefense), Sk(Skill.Healing), Sk(Skill.Shield), Sk(Skill.Summoning),
                        F(cycle.Attack, "0.##"), F(cycle.Kit, "0.##"), b.Pet ? Csv($"200 (needs 570 Summoning, has {Sk(Skill.Summoning)}{(Sk(Skill.Summoning) < 570 ? ": given anyway" : "")})") : "", Csv(hitCheck), Csv(probeLoadout.Name), Csv(string.Join(" | ", probeLoadout.Extras)), Csv(gear.Describe)));
                    summary.AppendLine($"L{level} {profile} {b.Name}: {hitCheck}");
                    summary.AppendLine($"L{level} {profile} {b.Name}: health {probe.Health.MaxValue}, {attackSkill} {Sk(attackSkill)}, melee D {Sk(Skill.MeleeDefense)}, missile D {Sk(Skill.MissileDefense)}, magic D {Sk(Skill.MagicDefense)}, credits {sheet.CreditsUsed}/{sheet.CreditsTotal}, XP left {sheet.XpLeft:N0}, augs {sheet.Augs.Count}");

                    foreach (var (wcid, elite) in monsterList)
                    {
                        var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
                        if (weenie == null)
                            continue;
                        try
                        {
                            // the weapon version (element) and pet that do best against this monster: a short real-hit trial each
                            var best = 0;
                            var bestDps = -1.0;
                            for (var v = 0; v < WeaponVersions(b, gear); v++)
                            {
                                var trial = RunFight(b, level, o.LumLevel, gear, v, wcid, elite, 0, false, 20);
                                var dps = trial == null ? 0 : trial.DmgByPlayer / Math.Max(1.0, double.IsNaN(trial.DeathTime) ? Math.Min(20, trial.T) : trial.DeathTime);
                                if (dps > bestDps)
                                    (best, bestDps) = (v, dps);
                            }
                            uint pet = 0;
                            if (b.Pet && o.Pets && petTier.Length > 0)
                            {
                                var bestPet = -1.0;
                                foreach (var candidate in petTier)
                                {
                                    var trial = RunFight(b, level, o.LumLevel, gear, best, wcid, elite, candidate, false, 20);
                                    var dps = trial == null ? 0 : trial.DmgByPet;
                                    if (dps > bestPet)
                                        (pet, bestPet) = (candidate, dps);
                                }
                            }

                            foreach (var heal in new[] { false, true })
                            {
                                var row = new SurvivalRow { Level = level, Profile = profile, Build = b.Name, Wcid = wcid, Elite = elite, Mode = heal ? "kits" : "no healing", Spots = spawned.TryGetValue(wcid, out var info) ? info.Spots : 0 };
                                var kills = new List<double>();
                                var deaths = new List<double>();
                                double wins = 0, kits = 0, otherKits = 0, surges = 0, cloakProcs = 0, attacks = 0, lands = 0, swings = 0, mobLands = 0, spells = 0, resists = 0, petDmg = 0, allDmg = 0, petDied = 0, onPet = 0, mobAttacks = 0, stamOut = 0, manaOut = 0;
                                double melee = 0, missile = 0, magic = 0, dot = 0, drain = 0, taken = 0, time = 0, dealt = 0;
                                var unhandled = new Dictionary<string, int>();
                                for (var i = 0; i < o.Fights; i++)
                                {
                                    Fight f;
                                    try
                                    {
                                        f = RunFight(b, level, o.LumLevel, gear, best, wcid, elite, pet, heal, o.MaxSeconds, o.HealAt);
                                    }
                                    catch (Exception e)
                                    {
                                        row.Failed++;
                                        log.Warn($"[ACXSURVIVAL] fight failed L{level} {b.Name} vs {wcid}: {e.Message}\n{e.StackTrace}");
                                        continue;
                                    }
                                    if (f == null)
                                    {
                                        row.Failed++;
                                        continue;
                                    }
                                    row.Fights++;
                                    row.Monster = f.M.Name;
                                    row.MonsterLevel = f.M.Level ?? 0;
                                    row.MonsterHealth = f.M.Health.MaxValue;
                                    row.Weapon = f.Gear.Name;
                                    row.Pet = f.Pet?.Name ?? "";
                                    var kill = double.IsNaN(f.KillTime) ? double.PositiveInfinity : f.KillTime;
                                    var death = double.IsNaN(f.DeathTime) ? double.PositiveInfinity : f.DeathTime;
                                    kills.Add(kill);
                                    deaths.Add(death);
                                    if (kill < death)
                                        wins++;
                                    var span = Math.Min(f.T, o.MaxSeconds);
                                    time += span;
                                    kits += f.Kits;
                                    otherKits += f.OtherKits;
                                    surges += f.Surges;
                                    cloakProcs += f.CloakProcs;
                                    attacks += f.PlayerAttacks; lands += f.PlayerLands;
                                    swings += f.MobSwings; mobLands += f.MobLands;
                                    spells += f.SpellsAtPlayer; resists += f.SpellsResisted;
                                    petDmg += f.DmgByPet; allDmg += f.DmgByPet + f.DmgByPlayer;
                                    dealt += f.DmgByPlayer + f.DmgByPet;
                                    if (f.PetDead) petDied++;
                                    onPet += f.AttacksOnPet; mobAttacks += f.MobAttacks;
                                    stamOut += f.StaminaOut > 0 ? 1 : 0; manaOut += f.ManaOut > 0 ? 1 : 0;
                                    melee += f.Melee; missile += f.Missile; magic += f.Magic; dot += f.Dot; drain += f.Drain; taken += f.PlayerDamageTaken;
                                    foreach (var (k, v) in f.Unhandled)
                                        unhandled[k] = unhandled.GetValueOrDefault(k) + v;
                                }
                                if (row.Fights == 0)
                                {
                                    rows.Add(row);
                                    continue;
                                }
                                row.WinPct = wins * 100 / row.Fights;
                                row.KillMedian = MedianOf(kills);
                                row.DieMedian = MedianOf(deaths);
                                row.KitsPerFight = kits / row.Fights;
                                row.OtherKitsPerFight = otherKits / row.Fights;
                                row.SurgesPerFight = surges / row.Fights;
                                row.CloakProcsPerFight = cloakProcs / row.Fights;
                                row.PlayerHitPct = attacks > 0 ? lands * 100 / attacks : 0;
                                row.MobHitPct = swings > 0 ? mobLands * 100 / swings : 0;
                                row.ResistPct = spells > 0 ? resists * 100 / spells : 0;
                                row.PetShare = allDmg > 0 ? petDmg * 100 / allDmg : 0;
                                row.PetDiedPct = f_PetCount(pet) ? petDied * 100 / row.Fights : 0;
                                row.OnPetPct = mobAttacks > 0 ? onPet * 100 / mobAttacks : 0;
                                row.StaminaOut = stamOut * 100 / row.Fights;
                                row.ManaOut = manaOut * 100 / row.Fights;
                                var sum = melee + missile + magic + dot + drain;
                                if (sum > 0)
                                {
                                    row.MeleeShare = melee * 100 / sum; row.MissileShare = missile * 100 / sum; row.MagicShare = magic * 100 / sum;
                                    row.DotShare = dot * 100 / sum; row.DrainShare = drain * 100 / sum;
                                }
                                row.TakenPerSecond = time > 0 ? taken / time : 0;
                                row.DealtPerSecond = time > 0 ? dealt / time : 0;
                                row.Unhandled = string.Join("; ", unhandled.OrderByDescending(u => u.Value).Select(u => $"{u.Key} x{u.Value}"));
                                rows.Add(row);
                            }
                        }
                        catch (Exception e)
                        {
                            log.Warn($"[ACXSURVIVAL] L{level} {b.Name} vs {wcid} failed: {e}");
                        }
                    }
                    log.Info($"[ACXSURVIVAL] L{level} {b.Name} done ({(DateTime.Now - started).TotalSeconds:0}s)");
                }
            }

            string Sec(double s) => double.IsInfinity(s) || double.IsNaN(s) ? "never" : F(s, "0.#");

            // verdicts per level, build and monster, from both modes
            string Verdict(SurvivalRow plain, SurvivalRow kits)
            {
                if (plain == null || kits == null || plain.Fights == 0 || kits.Fights == 0)
                    return "no data";
                if (plain.WinPct >= 90)
                    return "easy";
                if (kits.WinPct >= 90)
                    return "needs kits";
                var ratio = kits.DieMedian / kits.KillMedian;
                if (kits.WinPct >= 10 || ratio >= 0.5)
                    return "close: a skilled player can win";
                return "you lose even toe to toe";
            }

            var sb = new StringBuilder("Level,Profile,Build,Monster,Wcid,MonsterLevel,Elite,SpawnSpots,MonsterHealth,Mode,Fights,Failed,WinPct,KillSecMedian,DieSecMedian,DieOverKill,Verdict,Weapon,Pet,"
                + "PlayerHitPct,MonsterLandPct,SpellsResistedPct,PlayerDealtPerSec,PlayerTookPerSec,HealthKitsPerFight,StaminaManaKitsPerFight,MeleeDmgPct,MissileDmgPct,MagicDmgPct,DotDmgPct,DrainDmgPct,"
                + "PetDmgPct,PetDiedPct,MonsterAttacksOnPetPct,StaminaOutPct,ManaOutPct,SurgesPerFight,CloakProcsPerFight,Unhandled\n");
            foreach (var row in rows)
            {
                var pair = rows.Where(r => r.Level == row.Level && r.Profile == row.Profile && r.Build == row.Build && r.Wcid == row.Wcid && r.Elite == row.Elite).ToList();
                var verdict = Verdict(pair.FirstOrDefault(r => r.Mode == "no healing"), pair.FirstOrDefault(r => r.Mode == "kits"));
                var ratio = row.KillMedian > 0 && !double.IsInfinity(row.KillMedian) ? (double.IsInfinity(row.DieMedian) ? "inf" : F(row.DieMedian / row.KillMedian, "0.##")) : "0";
                sb.AppendLine(string.Join(",", row.Level, row.Profile, row.Build, Csv((row.Elite ? "Elite " : "") + row.Monster), row.Wcid, row.MonsterLevel, row.Elite ? 1 : 0, row.Spots, F(row.MonsterHealth, "0"), row.Mode,
                    row.Fights, row.Failed, F(row.WinPct, "0"), Sec(row.KillMedian), Sec(row.DieMedian), ratio, Csv(verdict), Csv(row.Weapon), Csv(row.Pet),
                    F(row.PlayerHitPct, "0"), F(row.MobHitPct, "0"), F(row.ResistPct, "0"), F(row.DealtPerSecond, "0.#"), F(row.TakenPerSecond, "0.#"), F(row.KitsPerFight, "0.#"), F(row.OtherKitsPerFight, "0.#"),
                    F(row.MeleeShare, "0"), F(row.MissileShare, "0"), F(row.MagicShare, "0"), F(row.DotShare, "0"), F(row.DrainShare, "0"),
                    F(row.PetShare, "0"), F(row.PetDiedPct, "0"), F(row.OnPetPct, "0"), F(row.StaminaOut, "0"), F(row.ManaOut, "0"), F(row.SurgesPerFight, "0.#"), F(row.CloakProcsPerFight, "0.#"), Csv(row.Unhandled)));
            }

            var dir = Path.Combine("Logs", "acxbench");
            Directory.CreateDirectory(dir);
            var stamp = started.ToString("yyyyMMdd-HHmmss");
            var tag = (o.MeleeKind != null ? $"-{o.MeleeKind}" : "") + (o.Gear > 0 ? $"-gear{o.Gear}" : "") + (o.ArmorSet != null ? $"-{o.ArmorSet}" : "") + (o.Gear >= 2 && o.Aetheria != "defense" ? $"-{o.Aetheria}" : "");
            var file = Path.Combine(dir, $"acxsurvival-{string.Join("_", o.Levels)}{tag}-{stamp}.csv");
            File.WriteAllText(file, sb.ToString());
            var sheetFile = Path.Combine(dir, $"acxsurvival-builds-{string.Join("_", o.Levels)}{tag}-{stamp}.csv");
            File.WriteAllText(sheetFile, sheets.ToString());

            foreach (var group in rows.Where(r => r.Mode == "kits").GroupBy(r => (r.Level, r.Profile, r.Build)))
            {
                var verdicts = group.Select(r => Verdict(rows.FirstOrDefault(x => x.Level == r.Level && x.Profile == r.Profile && x.Build == r.Build && x.Wcid == r.Wcid && x.Elite == r.Elite && x.Mode == "no healing"), r)).ToList();
                summary.AppendLine($"L{group.Key.Level} {group.Key.Profile} {group.Key.Build}: " + string.Join(", ", verdicts.GroupBy(v => v).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")));
            }
            return $"Survival bench done in {(DateTime.Now - started).TotalSeconds:0}s, written to {file} and {sheetFile}.\n" + summary.ToString().TrimEnd();
        }

        private static bool f_PetCount(uint pet) => pet != 0;
    }
}
