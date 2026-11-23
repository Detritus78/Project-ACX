using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories;
using ACE.Server.Entity.Mutations;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.Physics.Animation;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: a quest weapon against loot of its own kind (JJ 2026-11-15: "take an atlan with a major stone compare it
    /// to loot weapons that have the same or close to wield requirements. i.e. atlan spear vs a hw/lw/fin/twohand spear loot
    /// spear untinked and tinked. tinked means rended slayer optimal spell transfer ... granite iron up to the max tinks
    /// possible. which is 10"; "imbues and rends that arent already on it use up tinks.. a weapon can only be imbued one time.
    /// So to double up you need a mutation that isnt considered a imbue like biting strike or crushing blow or armor cleave.
    /// then you can add a imbue and a slayer"; "as you upgrade the atlan the wield requirements should change").
    /// At one character level, against the monsters /acxxpmodel fights there and with its buffs, the attack skill's base at
    /// the wield requirement (what rending reads; every attack still lands): the quest weapon as it comes, its own spells on,
    /// and tinked too if it takes tinkering; then in each class every loot kind of the same weapon type in the quest weapon's
    /// element, as the median of real loot rolls at the same wield requirement: untinked, tinked, and tinked on a roll that
    /// also came with the best pair of ACX extra mutations. Out: Logs/acxbench/acxgear-*.csv, one row per weapon, with the
    /// spawn-weighted median hits to kill and the share of spawns dying to one or two average hits. /acxgear in
    /// ACXBenchCommands.cs.
    /// </summary>
    public static partial class AcxBench
    {
        private const int MaxTinks = 10;
        private const uint LegendaryBloodThirst = 6089, LegendaryStrength = 6107, LegendaryCoordination = 6103, LegendaryQuickness = 6106;
        private const int LootRollsPerKind = 41;

        // the loot tiers that roll each melee wield requirement (Entity/Mutations/MeleeWeapons/Damage_WieldDifficulty_DamageVariance,
        // the same for every class and kind), and the level a player of that gear hunts at: the middle of those tiers' levels
        private static readonly Dictionary<int, (int[] Tiers, int Level)> WieldGear = new Dictionary<int, (int[] Tiers, int Level)>()
        {
            { 250, (new[] { 2, 3 }, 50) },
            { 300, (new[] { 3, 4 }, 80) },
            { 325, (new[] { 4, 5 }, 115) },
            { 350, (new[] { 5, 6 }, 155) },
            { 370, (new[] { 6 }, 175) },
            { 400, (new[] { 6, 7, 8 }, 200) },
            { 420, (new[] { 7, 8 }, 240) },
            { 430, (new[] { 8 }, 260) },
        };

        private static readonly (string Name, Skill Skill, Type Table)[] LootClasses =
        {
            ("Heavy", Skill.HeavyWeapons, typeof(HeavyWeaponWcids)),
            ("Light", Skill.LightWeapons, typeof(LightWeaponWcids)),
            ("Finesse", Skill.FinesseWeapons, typeof(FinesseWeaponWcids)),
            ("Two Handed", Skill.TwoHandedCombat, typeof(TwoHandedWeaponWcids)),
        };

        // two-handed loot is all WeaponType.TwoHanded, so its kinds go by table
        private static readonly Dictionary<string, WeaponType> TwoHandedKinds = new Dictionary<string, WeaponType>()
        {
            { "GreatAxes", WeaponType.Axe },
            { "GreatStarMaces", WeaponType.Mace }, { "KhandaHandledMaces", WeaponType.Mace }, { "Quadrelles", WeaponType.Mace }, { "Tetsubos", WeaponType.Mace },
            { "Assagais", WeaponType.Spear }, { "Corsecas", WeaponType.Spear }, { "MagariYaris", WeaponType.Spear }, { "Pikes", WeaponType.Spear },
            { "Nodachis", WeaponType.Sword }, { "Shashqas", WeaponType.Sword }, { "Spadones", WeaponType.Sword },
        };

        // the ACX extra mutations a loot weapon can roll, at most two (LootGenerationFactory_AcxMutations); none is an imbue
        private static readonly (string Tag, Action<WorldObject> Apply)[] ExtraMutations =
        {
            ("AC", w => w.IgnoreArmor = 0.625f),
            ("BS", w => w.CriticalFrequency = 0.15f),
            ("CB", w => w.SetProperty(PropertyFloat.CriticalMultiplier, 2.0)),
        };

        private const DamageType Elements = DamageType.Cold | DamageType.Fire | DamageType.Acid | DamageType.Electric;

        // the level's buffs as the spells a player casts, by spell level (BuffLevelFor; level VII has names of its own):
        // Aura of Blood Drinker, Aura of Swift Killer, Strength / Coordination / Quickness Self
        private static readonly uint[][] BuffSpells =
        {
            new uint[] { 0, 0, 0, 1613, 1614, 1615, 1616, 2096, 4395 },
            new uint[] { 0, 0, 0, 1624, 1625, 1626, 1627, 2116, 4417 },
            new uint[] { 0, 0, 0, 1329, 1330, 1331, 1332, 2087, 4325 },
            new uint[] { 0, 0, 0, 1375, 1376, 1377, 1378, 2059, 4297 },
            new uint[] { 0, 0, 0, 1399, 1400, 1401, 1402, 2081, 4319 },
        };

        // the debuffs a player lands on what they fight, by spell level as BuffSpells (JJ 2026-11-15, on Phantom "would it be
        // the weapon to use against everything?": "Let's bench it" with Imperil and vulns): Imperil Other, and the
        // vulnerability of the damage type the attack deals
        private static readonly uint[] ImperilSpells = { 0, 0, 0, 1324, 1325, 1326, 1327, 3090, 4312 };
        private static readonly Dictionary<DamageType, uint[]> VulnSpells = new Dictionary<DamageType, uint[]>()
        {
            { DamageType.Slash,    new uint[] { 0, 0, 0, 1129, 1130, 1131, 1132, 2164, 4475 } },
            { DamageType.Pierce,   new uint[] { 0, 0, 0, 1153, 1154, 1155, 1156, 2174, 4485 } },
            { DamageType.Bludgeon, new uint[] { 0, 0, 0, 1050, 1051, 1052, 1053, 2166, 4477 } },
            { DamageType.Fire,     new uint[] { 0, 0, 0, 1105, 1106, 1107, 1108, 2170, 4481 } },
            { DamageType.Cold,     new uint[] { 0, 0, 0, 1062, 1063, 1064, 1065, 2168, 4479 } },
            { DamageType.Acid,     new uint[] { 0, 0, 0, 523, 524, 525, 526, 2162, 4473 } },
            { DamageType.Electric, new uint[] { 0, 0, 0, 1086, 1087, 1088, 1089, 2172, 4483 } },
        };

        /// <summary>
        /// The damage type a weapon's attacks deal at the bench's full power: its element, else slash for a slash/thrust
        /// weapon (ThrustThreshold), else its one physical type
        /// </summary>
        private static DamageType AttackDamageType(WorldObject weapon)
        {
            var types = weapon.W_DamageType;
            return (types & Elements) != 0 ? types & Elements
                : types.HasFlag(DamageType.Slash) ? DamageType.Slash
                : types.HasFlag(DamageType.Pierce) ? DamageType.Pierce
                : DamageType.Bludgeon;
        }

        // the imbue gems' mutation data ids (Entity/Mutations/Recipes)
        private static readonly Dictionary<ImbuedEffectType, uint> ImbueMutations = new Dictionary<ImbuedEffectType, uint>()
        {
            { ImbuedEffectType.CriticalStrike, 0x38000023 }, { ImbuedEffectType.CripplingBlow, 0x38000024 }, { ImbuedEffectType.ArmorRending, 0x38000025 },
            { ImbuedEffectType.AcidRending, 0x3800003A }, { ImbuedEffectType.BludgeonRending, 0x3800003B }, { ImbuedEffectType.ColdRending, 0x3800003C },
            { ImbuedEffectType.ElectricRending, 0x3800003D }, { ImbuedEffectType.FireRending, 0x3800003E }, { ImbuedEffectType.PierceRending, 0x3800003F },
            { ImbuedEffectType.SlashRending, 0x38000040 },
        };
        private const uint IronMutation = 0x3800001A, GraniteMutation = 0x3800001C, MahoganyMutation = 0x3800001B;

        /// <summary>
        /// An imbue as the gem's recipe applies it (its mutation script), Undef meaning the rend of the weapon's damage type
        /// </summary>
        private static void ImbueReal(WorldObject weapon, ImbuedEffectType imbue)
        {
            MutationCache.GetMutation(ImbueMutations[imbue == ImbuedEffectType.Undef ? RendImbue(weapon) : imbue]).TryMutate(weapon);
        }

        /// <summary>
        /// Spell transfers through the transfer scroll's own code (InjectSpell: into the weapon's spells, counted against
        /// its slots); returns the ones that went on
        /// </summary>
        private static List<uint> TransferReal(WorldObject weapon, IEnumerable<uint> spellIds)
        {
            var added = new List<uint>();
            foreach (var spellId in spellIds.Select(BandCantrip))   // the survival bench's gear band: Major / Epic below level 181; the class bench's: none / Minor lower
            {
                if (spellId == 0)
                    continue;
                if (SpellTransferScroll.InjectSpell(weapon, (SpellId)spellId).Result == SpellTransferScroll.InjectSpellResult.Success)
                    added.Add(spellId);
                else
                    log.Warn($"AcxBench gear check: transfer {spellId} onto {weapon.Name} refused");
            }
            return added;
        }

        /// <summary>
        /// Tink as the game does it: the rend imbue's and iron/granite's mutation scripts (the same split Tink picks) and
        /// real transfers
        /// </summary>
        private static (List<uint> Transfers, int Iron, int Granite) TinkReal(WorldObject weapon, int buff, List<uint> owned)
        {
            var planned = DamageTransfers(weapon, owned, weapon.MaxExtraSpellsCount);
            ImbueReal(weapon, ImbuedEffectType.Undef);

            var tinks = Math.Max(0, MaxTinks - weapon.NumTimesTinkered);
            var iron = BestIron(weapon, BloodDrinkerBuff[buff] + (planned.Contains(LegendaryBloodThirst) || owned.Contains(LegendaryBloodThirst) ? BandBloodThirst : 0), tinks);
            for (var i = 0; i < iron; i++)
                MutationCache.GetMutation(IronMutation).TryMutate(weapon);
            for (var i = 0; i < tinks - iron; i++)
                MutationCache.GetMutation(GraniteMutation).TryMutate(weapon);

            return (TransferReal(weapon, planned), iron, tinks - iron);
        }

        private class LootRoll
        {
            public uint Wcid;
            public int Damage, WeaponTime, Workmanship, Wield;
            public double Variance;
            public double? DamageMod;
            public int? ElementalDamageBonus;
        }

        private class GearRow
        {
            public string Class, Kind, Weapon, Elements, State, Transfers = "", SlayerVs = "";
            public int Damage, Workmanship, Iron, Granite;
            public double Variance, Hits, OneHit, TwoHits, AvgHit, Seconds, FailPct;
        }

        public static string StartGear(uint questWcid, int level, bool allKinds, bool check, bool debuff, int lootWield, bool single, Action<string> reply)
        {
            if (running)
                return "A bench is already running.";
            running = true;

            Task.Run(() =>
            {
                try
                {
                    reply(check ? RunGearCheck(questWcid, level) : RunGear(questWcid, level, allKinds, debuff, lootWield, single));
                }
                catch (Exception e)
                {
                    log.Error($"AcxBench gear failed: {e}");
                    reply($"The gear bench failed: {e.Message}");
                }
                finally
                {
                    running = false;
                }
            });
            return $"Gear bench started for {questWcid}. Results go to Logs/acxbench/.";
        }

        private static string RunGear(uint questWcid, int level, bool allKinds, bool debuff, int lootWield = 0, bool single = false)
        {
            var started = DateTime.Now;
            countMisses = false;
            var monsters = SpawnedMonsters();
            var scale = PropertyManager.GetDouble("acx_bleed_damage_scale").Item;

            var questWeenie = DatabaseManager.World.GetCachedWeenie(questWcid);
            if (questWeenie == null)
                return $"No weenie {questWcid}.";
            var quest = WorldObjectFactory.CreateWorldObject(questWeenie, new ObjectGuid(WeaponGuid));
            var wield = quest.WieldDifficulty ?? 0;
            // the loot to compare against: at the quest weapon's wield requirement, or a given one (a low-wield weapon against
            // the loot a player of a higher level carries); the player's base skill is the higher of the two
            var gearWield = lootWield > 0 ? WieldGear.Keys.OrderBy(w => Math.Abs(w - lootWield)).First() : WieldGear.Keys.OrderBy(w => Math.Abs(w - wield)).First();
            var skill = Math.Max(wield, lootWield > 0 ? gearWield : 0);
            if (level <= 0)
                level = WieldGear[gearWield].Level;
            var kind = WeaponKind(quest, null);
            var buff = BuffLevelFor(level);

            // the quest weapon's element versions (an Atlan comes in four; the best one goes against each monster, as a player
            // picks the element for the area), and loot in the same elements
            // single: just this one weapon, not its element siblings (JJ 2026-11-15: "bench a single atlan element against phantom")
            var questVersions = (single ? new List<uint> { quest.WeenieClassId } : ElementVersions(quest)).Select(w => WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(w), new ObjectGuid(WeaponGuid))).ToList();
            var elements = questVersions.Select(v => v.W_DamageType).ToList();

            var rows = new List<GearRow>();
            List<uint> QuestSpells(WorldObject w) => w.Biota.GetKnownSpellsIds(w.BiotaDatabaseLock).Select(id => (uint)id).ToList();

            // the quest weapon as it comes, and tinked if it takes tinkering (Stormwood: workmanship, no imbue)
            var row = BenchGear(questVersions.Select(v => (v, QuestSpells(v))).ToList(), level, false, skill, monsters, scale, debuff: debuff);
            Describe(row, "Quest", quest, "as is", elements);
            rows.Add(row);
            if ((quest.ItemWorkmanship ?? 0) > 0)
            {
                var versions = new List<(WorldObject, List<uint>)>();
                (List<uint> Transfers, int Iron, int Granite) tink = default;
                foreach (var version in questVersions)
                {
                    var weapon = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(version.WeenieClassId), new ObjectGuid(WeaponGuid));
                    var spells = QuestSpells(weapon);
                    tink = Tink(weapon, buff, spells);
                    versions.Add((weapon, spells.Concat(tink.Transfers).ToList()));
                }
                row = BenchGear(versions, level, true, skill, monsters, scale, debuff: debuff);
                Describe(row, "Quest", quest, "tinked", elements, tink.Transfers, tink.Iron, tink.Granite);
                rows.Add(row);
            }

            // a quest weapon that takes no tinkering, given one imbue (the best it can take) and one or two spell transfers, no
            // tinks or slayer stone (JJ 2026-11-15: "Maybe allow one imbue on quest weapons and two spell transfers? Or one
            // spell transfer?")
            if ((quest.ItemWorkmanship ?? 0) == 0)
            {
                foreach (var transferCount in new[] { 1, 2 })
                {
                    GearRow best = null;
                    foreach (var (imbueName, imbue) in QuestImbues(quest))
                    {
                        var versions = new List<(WorldObject, List<uint>)>();
                        var transfers = new List<uint>();
                        foreach (var version in questVersions)
                        {
                            var weapon = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(version.WeenieClassId), new ObjectGuid(WeaponGuid));
                            var spells = QuestSpells(weapon);
                            if (imbue != null)
                                weapon.ImbuedEffect |= imbue == ImbuedEffectType.Undef ? RendImbue(weapon) : imbue.Value;
                            transfers = DamageTransfers(weapon, spells, transferCount);
                            versions.Add((weapon, spells.Concat(transfers).ToList()));
                        }
                        row = BenchGear(versions, level, false, skill, monsters, scale, debuff: debuff);
                        Describe(row, "Quest", quest, $"{imbueName} + {transferCount} transfer{(transferCount > 1 ? "s" : "")}", elements, transfers);
                        if (best == null || row.Hits < best.Hits)
                            best = row;
                    }
                    rows.Add(best);
                }
            }

            // loot of the same weapon type in every class, in the same elements, at the same wield requirement: a sample of
            // one kind per class (its first of the type; JJ: "we already know how most will bench. what we don't know is how
            // the quest weapons bench. So do a proper sampling without overdoing it"; kinds in a class came within about 15%
            // of each other untinked, multi-strike ones on top), or every kind
            foreach (var (className, _, table) in LootClasses)
            {
                var sampled = false;
                foreach (var field in table.GetFields(BindingFlags.NonPublic | BindingFlags.Static).Where(f => f.FieldType == typeof(Factories.Entity.ChanceTable<Factories.Enum.WeenieClassName>)))
                {
                    var wcids = ((Factories.Entity.ChanceTable<Factories.Enum.WeenieClassName>)field.GetValue(null)).Select(e => (uint)e.result).ToList();
                    var plain = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(wcids[0]), new ObjectGuid(WeaponGuid));
                    if (plain == null || WeaponKind(plain, field.Name) != kind)
                        continue;
                    var rolls = elements.Select(e => wcids.FirstOrDefault(w => MatchesElement(DatabaseManager.World.GetCachedWeenie(w), e))).Where(w => w != 0).Distinct()
                        .Select(w => RollLoot(w, gearWield)).Where(r => r != null).ToList();
                    if (rolls.Count == 0 || (sampled && !allKinds))
                        continue;
                    sampled = true;

                    GearRow Bench(string state, bool tinked, Action<WorldObject> extras = null)
                    {
                        var versions = new List<(WorldObject, List<uint>)>();
                        (List<uint> Transfers, int Iron, int Granite) tink = (new List<uint>(), 0, 0);
                        foreach (var roll in rolls)
                        {
                            var weapon = MakeWeapon(roll);
                            extras?.Invoke(weapon);
                            if (tinked)
                                tink = Tink(weapon, buff, new List<uint>());
                            versions.Add((weapon, tink.Transfers));
                        }
                        var result = BenchGear(versions, level, tinked, skill, monsters, scale, debuff: debuff);
                        Describe(result, className, plain, state, elements, tinked ? tink.Transfers : null, tink.Iron, tink.Granite);
                        return result;
                    }

                    rows.Add(Bench("untinked", false));
                    rows.Add(Bench("tinked", true));

                    // the same, on a roll that also came with two extra mutations: the better of Armor Cleaving + Crushing Blow and
                    // Biting Strike + Crushing Blow (Armor Cleaving + Biting Strike never came out on top)
                    GearRow best = null;
                    foreach (var (first, second) in new[] { (ExtraMutations[0], ExtraMutations[2]), (ExtraMutations[1], ExtraMutations[2]) })
                    {
                        row = Bench($"tinked +{first.Tag}+{second.Tag}", true, w => { first.Apply(w); second.Apply(w); });
                        if (best == null || row.Hits < best.Hits)
                            best = row;
                    }
                    rows.Add(best);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("Quest,QuestWcid,Level,Wield,Class,Kind,Weapon,Elements,State,Damage,Variance,Workmanship,Iron,Granite,Transfers,HitsToKill,OneHitPct,TwoHitPct,AvgHit,SecondsToKill,FailedStrikePct,SlayerStoneVs");
            foreach (var r in rows)
                sb.AppendLine(string.Join(",", Csv(quest.Name), questWcid, level, wield, r.Class, r.Kind, Csv(r.Weapon), r.Elements, r.State, r.Damage, F(r.Variance, "0.###"), r.Workmanship, r.Iron, r.Granite,
                    r.Transfers, F(r.Hits, "0.##"), F(r.OneHit, "0"), F(r.TwoHits, "0"), F(r.AvgHit, "0.#"), F(r.Seconds, "0.#"), F(r.FailPct, "0.#"), r.SlayerVs));
            var dir = Path.Combine("Logs", "acxbench");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"acxgear-{questWcid}-L{level}{(lootWield > 0 ? $"-w{gearWield}" : "")}{(single ? "-single" : "")}{(debuff ? "-debuff" : "")}-{started:yyyyMMdd-HHmmss}.csv");
            File.WriteAllText(file, sb.ToString());

            var summary = new StringBuilder();
            summary.AppendLine($"Gear bench done in {(DateTime.Now - started).TotalSeconds:0}s: {quest.Name} (wield {wield}) at L{level}{(debuff ? " with Imperil and vulns" : "")}, written to {file}.");
            foreach (var r in rows.Where(r => r.Class == "Quest"))
                summary.AppendLine($"  {r.Weapon} {r.State}: {r.Hits:0.#} hits, one hit {r.OneHit:0}%");
            foreach (var group in rows.Where(r => r.Class != "Quest").GroupBy(r => (r.Class, r.State.StartsWith("tinked +") ? "tinked + 2 extras" : r.State)))
            {
                var top = group.OrderBy(r => r.Hits).First();
                summary.AppendLine($"  best {group.Key.Class} {group.Key.Item2}: {top.Weapon} {top.Hits:0.#} hits, one hit {top.OneHit:0}%");
            }
            return summary.ToString().TrimEnd();
        }

        /// <summary>
        /// The bench checked against the game (JJ 2026-11-15: "This is all dependent on our bench being accurate", then "Yeah
        /// give it a try"): each decision row twice on the same loot rolls and 30 swings a monster, once as RunGear builds it
        /// (buffs as flat numbers, imbue and iron/granite set directly, transfers as enchantments) and once the game's way
        /// (the buff spells cast on the attacker, the imbue's and iron/granite's mutation scripts, transfers through
        /// SpellTransferScroll.InjectSpell, every weapon spell left to the enchantment registry). Rows that match say the
        /// shortcuts hold. Out: Logs/acxbench/acxgearcheck-*.csv.
        /// </summary>
        private static string RunGearCheck(uint questWcid, int level)
        {
            const int swings = 30;
            var started = DateTime.Now;
            countMisses = false;
            var monsters = SpawnedMonsters();
            var scale = PropertyManager.GetDouble("acx_bleed_damage_scale").Item;

            var questWeenie = DatabaseManager.World.GetCachedWeenie(questWcid);
            if (questWeenie == null)
                return $"No weenie {questWcid}.";
            var quest = WorldObjectFactory.CreateWorldObject(questWeenie, new ObjectGuid(WeaponGuid));
            var wield = quest.WieldDifficulty ?? 0;
            var gearWield = WieldGear.Keys.OrderBy(w => Math.Abs(w - wield)).First();
            if (level <= 0)
                level = WieldGear[gearWield].Level;
            var kind = WeaponKind(quest, null);
            var buff = BuffLevelFor(level);
            var versionWcids = ElementVersions(quest);
            var elements = versionWcids.Select(w => (DamageType)(DatabaseManager.World.GetCachedWeenie(w).GetProperty(PropertyInt.DamageType) ?? 0)).ToList();
            WorldObject Fresh(uint wcid) => WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(wcid), new ObjectGuid(WeaponGuid));
            List<uint> Spells(WorldObject w) => w.Biota.GetKnownSpellsIds(w.BiotaDatabaseLock).Select(id => (uint)id).ToList();

            var rows = new List<GearRow>();
            void Pair(string className, WorldObject named, string state, Func<bool, List<(WorldObject, List<uint>)>> build, bool slayer)
            {
                foreach (var real in new[] { false, true })
                {
                    var row = BenchGear(build(real), level, slayer, wield, monsters, scale, real, swings);
                    Describe(row, className, named, state + (real ? " [game]" : " [bench]"), elements);
                    rows.Add(row);
                }
            }

            // the quest weapon as it comes
            Pair("Quest", quest, "as is", real => versionWcids.Select(w => { var v = Fresh(w); return (v, Spells(v)); }).ToList(), false);

            // with the imbue the bench picks for it and two transfers
            if ((quest.ItemWorkmanship ?? 0) == 0)
            {
                GearRow pick = null;
                ImbuedEffectType? pickImbue = null;
                var pickName = "";
                foreach (var (imbueName, imbue) in QuestImbues(quest))
                {
                    var versions = versionWcids.Select(w =>
                    {
                        var v = Fresh(w);
                        if (imbue != null)
                            v.ImbuedEffect |= imbue == ImbuedEffectType.Undef ? RendImbue(v) : imbue.Value;
                        var own = Spells(v);
                        return (v, own.Concat(DamageTransfers(v, own, 2)).ToList());
                    }).ToList();
                    var row = BenchGear(versions, level, false, wield, monsters, scale, false, 10);
                    if (pick == null || row.Hits < pick.Hits)
                        (pick, pickImbue, pickName) = (row, imbue, imbueName);
                }
                Pair("Quest", quest, $"{pickName} + 2 transfers", real => versionWcids.Select(w =>
                {
                    var v = Fresh(w);
                    var own = Spells(v);
                    var transfers = DamageTransfers(v, own, 2);
                    if (real)
                    {
                        if (pickImbue != null)
                            ImbueReal(v, pickImbue.Value);
                        TransferReal(v, transfers);
                        return (v, Spells(v));
                    }
                    if (pickImbue != null)
                        v.ImbuedEffect |= pickImbue == ImbuedEffectType.Undef ? RendImbue(v) : pickImbue.Value;
                    return (v, own.Concat(transfers).ToList());
                }).ToList(), false);
            }

            // one loot kind a class, same elements, same rolls for both ways: untinked and tinked
            foreach (var (className, _, table) in LootClasses)
            {
                foreach (var field in table.GetFields(BindingFlags.NonPublic | BindingFlags.Static).Where(f => f.FieldType == typeof(Factories.Entity.ChanceTable<Factories.Enum.WeenieClassName>)))
                {
                    var wcids = ((Factories.Entity.ChanceTable<Factories.Enum.WeenieClassName>)field.GetValue(null)).Select(e => (uint)e.result).ToList();
                    var plain = Fresh(wcids[0]);
                    if (plain == null || WeaponKind(plain, field.Name) != kind)
                        continue;
                    var rolls = elements.Select(e => wcids.FirstOrDefault(w => MatchesElement(DatabaseManager.World.GetCachedWeenie(w), e))).Where(w => w != 0).Distinct()
                        .Select(w => RollLoot(w, gearWield)).Where(r => r != null).ToList();
                    if (rolls.Count == 0)
                        continue;

                    Pair(className, plain, "untinked", real => rolls.Select(r => (MakeWeapon(r), new List<uint>())).ToList(), false);
                    Pair(className, plain, "tinked", real => rolls.Select(r =>
                    {
                        var w = MakeWeapon(r);
                        if (real)
                        {
                            TinkReal(w, buff, new List<uint>());
                            return (w, Spells(w));
                        }
                        return (w, Tink(w, buff, new List<uint>()).Transfers);
                    }).ToList(), true);
                    break;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("Quest,QuestWcid,Level,Wield,Class,Kind,Weapon,Elements,State,Damage,Variance,HitsToKill,OneHitPct,AvgHit,SecondsToKill,FailedStrikePct");
            foreach (var r in rows)
                sb.AppendLine(string.Join(",", Csv(quest.Name), questWcid, level, wield, r.Class, r.Kind, Csv(r.Weapon), r.Elements, r.State, r.Damage, F(r.Variance, "0.###"),
                    F(r.Hits, "0.##"), F(r.OneHit, "0"), F(r.AvgHit, "0.#"), F(r.Seconds, "0.#"), F(r.FailPct, "0.#")));
            var dir = Path.Combine("Logs", "acxbench");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"acxgearcheck-{questWcid}-L{level}-{started:yyyyMMdd-HHmmss}.csv");
            File.WriteAllText(file, sb.ToString());

            var summary = new StringBuilder();
            summary.AppendLine($"Gear check done in {(DateTime.Now - started).TotalSeconds:0}s: {quest.Name} (wield {wield}) at L{level}, written to {file}.");
            for (var i = 0; i + 1 < rows.Count; i += 2)
                summary.AppendLine($"  {rows[i].Class} {rows[i].Weapon} {rows[i].State.Replace(" [bench]", "")}: bench {rows[i].Seconds:0.0}s, game {rows[i + 1].Seconds:0.0}s ({(rows[i + 1].Seconds / rows[i].Seconds - 1) * 100:+0;-0}%)");
            return summary.ToString().TrimEnd();
        }

        /// <summary>
        /// Sword, axe, mace or spear: two-handed loot by its table, other weapons by their type, and the two-handed quest
        /// weapons (no type of their own in the data) by name
        /// </summary>
        private static WeaponType WeaponKind(WorldObject weapon, string table)
        {
            if (table != null && TwoHandedKinds.TryGetValue(table, out var kind))
                return kind;
            var type = weapon.W_WeaponType;
            if (type != WeaponType.TwoHanded && type != WeaponType.Undef)
                return type;
            var name = weapon.Name ?? "";
            return name.Contains("Sword") ? WeaponType.Sword : name.Contains("Axe") ? WeaponType.Axe : name.Contains("Mace") ? WeaponType.Mace
                : name.Contains("Spear") ? WeaponType.Spear : WeaponType.Undef;
        }

        /// <summary>
        /// The loot version in the quest weapon's element, or the plain one for a physical quest weapon
        /// </summary>
        private static bool MatchesElement(ACE.Entity.Models.Weenie weenie, DamageType element)
        {
            var type = (DamageType)(weenie?.GetProperty(PropertyInt.DamageType) ?? 0);
            return (element & Elements) != 0 ? type == element : (type & Elements) == 0;
        }

        /// <summary>
        /// The median (by average hit) of real loot rolls of a weapon at a wield requirement, leaving out rolls that came
        /// with an ACX extra mutation or slayer (those are their own row)
        /// </summary>
        private static LootRoll RollLoot(uint wcid, int wield)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            var tiers = WieldGear[wield].Tiers;
            var rolls = new List<LootRoll>();
            for (var attempt = 0; attempt < 20000 && rolls.Count < LootRollsPerKind; attempt++)
            {
                var wo = WorldObjectFactory.CreateWorldObject(weenie, new ObjectGuid(WeaponGuid));
                LootGenerationFactory.MutateItem(wo, new TreasureDeath { Tier = tiers[attempt % tiers.Length], LootQualityMod = 0 }, false);
                if (wo.WieldDifficulty != wield || wo.IgnoreArmor != null || wo.CriticalFrequency != null || wo.GetProperty(PropertyFloat.CriticalMultiplier) != null || wo.SlayerCreatureType != null)
                    continue;
                rolls.Add(new LootRoll { Wcid = wcid, Damage = wo.Damage ?? 0, Variance = wo.DamageVariance ?? 0, WeaponTime = wo.WeaponTime ?? 0, Workmanship = wo.ItemWorkmanship ?? 0, Wield = wield });
            }
            return rolls.Count == 0 ? null : rolls.OrderBy(r => r.Damage * (1 - r.Variance / 2)).ElementAt(rolls.Count / 2);
        }

        private static WorldObject MakeWeapon(LootRoll roll)
        {
            var weapon = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(roll.Wcid), new ObjectGuid(WeaponGuid));
            weapon.Damage = roll.Damage;
            weapon.DamageVariance = roll.Variance;
            weapon.WeaponTime = roll.WeaponTime;
            weapon.ItemWorkmanship = roll.Workmanship;
            weapon.WieldDifficulty = roll.Wield;
            if (roll.DamageMod != null)
                weapon.DamageMod = roll.DamageMod;
            if (roll.ElementalDamageBonus != null)
                weapon.ElementalDamageBonus = roll.ElementalDamageBonus;
            return weapon;
        }

        /// <summary>
        /// JJ's "tinked": a rending imbue of the weapon's element (one tink; a weapon takes one imbue), the tinks left as
        /// the iron (+1 damage) / granite (variance x0.8) split with the best average hit, and the workmanship's spell
        /// transfer slots (one per 2 workmanship, WorldObject_SpellTransfer) filled with the damage spells it lacks:
        /// Legendary Blood Thirst, the damage attribute's Legendary cantrip, Legendary Quickness. No slayer stone: JJ left
        /// slayers out of the bench (BenchGear).
        /// </summary>
        private static (List<uint> Transfers, int Iron, int Granite) Tink(WorldObject weapon, int buff, List<uint> owned)
        {
            weapon.ImbuedEffect |= RendImbue(weapon);
            var transfers = DamageTransfers(weapon, owned, weapon.MaxExtraSpellsCount);

            var tinks = Math.Max(0, MaxTinks - weapon.NumTimesTinkered - 1);
            var iron = BestIron(weapon, BloodDrinkerBuff[buff] + (transfers.Contains(LegendaryBloodThirst) || owned.Contains(LegendaryBloodThirst) ? 10 : 0), tinks);
            var variance = weapon.DamageVariance ?? 0;
            weapon.Damage = (weapon.Damage ?? 0) + iron;
            weapon.DamageVariance = variance * Math.Pow(0.8, tinks - iron);
            weapon.NumTimesTinkered += tinks + 1;
            return (transfers, iron, tinks - iron);
        }

        /// <summary>
        /// The iron / granite split with the most damage a swing, so the most damage a second (neither changes speed; JJ
        /// 2026-11-15: "There's a optimal formula for granite vs iron that maximizes dps"; "10 tinks allowed.. one imbue so
        /// the diff is granite and iron typically"), by the server's own numbers (BaseDamageMod, GetWeaponCriticalChance,
        /// GetWeaponCritDamageMod): the top of the range is the weapon's damage + Blood Drinker (+ Blood Thirst); a normal hit
        /// averages top x (1 - variance / 2); a crit (10% unless the weapon says otherwise) hits top x (1 + multiplier, 1 by
        /// default). Iron raises the top, crits included; granite (variance x0.8) only helps normal hits.
        /// </summary>
        private static int BestIron(WorldObject weapon, int damageBonus, int tinks)
        {
            var damage = (weapon.Damage ?? 0) + damageBonus;
            var variance = weapon.DamageVariance ?? 0;
            var critChance = weapon.CriticalFrequency ?? 0.1;
            var critDamage = 1 + (weapon.GetProperty(PropertyFloat.CriticalMultiplier) ?? 1.0);
            return Enumerable.Range(0, tinks + 1)
                .OrderByDescending(i => (damage + i) * ((1 - critChance) * (1 - variance * Math.Pow(0.8, tinks - i) / 2) + critChance * critDamage)).First();
        }

        /// <summary>
        /// The rending imbue of the damage type a weapon's attacks deal (one imbue per weapon, and it must match): its
        /// element, else slash for a slash/thrust weapon (it slashes at the bench's full power, ThrustThreshold), else its
        /// one physical type
        /// </summary>
        private static ImbuedEffectType RendImbue(WorldObject weapon) => WorldObject.GetRendDamageType(AttackDamageType(weapon));

        /// <summary>
        /// The damage spells a weapon lacks, best first, for its transfer slots: Legendary Blood Thirst, the damage
        /// attribute's Legendary cantrip, Legendary Quickness
        /// </summary>
        private static List<uint> DamageTransfers(WorldObject weapon, List<uint> owned, int slots)
        {
            // the attribute the damage reads (Creature.GetAttributeMod): Coordination for bows, crossbows and finesse
            var attribute = weapon.IsBow || weapon.WeaponSkill == Skill.FinesseWeapons ? LegendaryCoordination : LegendaryStrength;
            return new[] { LegendaryBloodThirst, attribute, LegendaryQuickness }.Where(id => !owned.Contains(id)).Take(Math.Max(0, slots)).ToList();
        }

        /// <summary>
        /// The imbues a quest weapon could take (JJ 2026-11-15: "Maybe allow one imbue on quest weapons and two spell
        /// transfers? Or one spell transfer?"): rending of its element, Armor Rending, Crippling Blow, Critical Strike, less
        /// the pair of a special it already has (Armor Cleaving / Armor Rending, Biting Strike / Critical Strike, Crushing
        /// Blow / Crippling Blow, as on loot)
        /// </summary>
        private static List<(string Name, ImbuedEffectType? Imbue)> QuestImbues(WorldObject quest)
        {
            // a weapon with an imbue of its own takes no other (Phantom weapons: IgnoreAllArmor; a gem's script would replace it)
            if (quest.ImbuedEffect != ImbuedEffectType.Undef)
                return new List<(string Name, ImbuedEffectType? Imbue)> { ("its own imbue", null) };

            var imbues = new List<(string Name, ImbuedEffectType? Imbue)>();
            if (!quest.IsAcxAtlanWeapon)
                imbues.Add(("rend", ImbuedEffectType.Undef));   // Atlans take no rending (RecipeManager_AcxPairs)
            if (quest.IgnoreArmor == null)
                imbues.Add(("Armor Rending", ImbuedEffectType.ArmorRending));
            if (quest.GetProperty(PropertyFloat.CriticalMultiplier) == null)
                imbues.Add(("Crippling Blow", ImbuedEffectType.CripplingBlow));
            if (quest.CriticalFrequency == null)
                imbues.Add(("Critical Strike", ImbuedEffectType.CriticalStrike));
            return imbues;
        }

        private static void Describe(GearRow row, string className, WorldObject weapon, string state, List<DamageType> elements, List<uint> transfers = null, int iron = 0, int granite = 0)
        {
            row.Class = className;
            row.Kind = WeaponKind(weapon, null).ToString();
            row.Weapon = weapon.Name;
            row.Elements = string.Join(" ", elements.Select(e => (e & Elements) != 0 ? LootGenerationFactory.AcxElementTag(e) : "physical"));
            row.State = state;
            row.Workmanship = weapon.ItemWorkmanship ?? 0;
            row.Iron = iron;
            row.Granite = granite;
            if (transfers != null)
                row.Transfers = string.Join(" ", transfers.Select(id => new ACE.Server.Entity.Spell(id).Name.Replace("Legendary ", "L.")));
        }

        /// <summary>
        /// The damage type a missile attack deals, as DamageEvent picks it: the ammo's, except prismatic ammo (type Base),
        /// which takes the launcher's own (a Slashing Bow's slash) or pierce
        /// </summary>
        private static DamageType MissileDamageType(WorldObject weapon, WorldObject ammo)
        {
            if (weapon == ammo || ammo.W_DamageType != DamageType.Base)
                return AttackDamageType(ammo);
            return weapon.W_DamageType != DamageType.Undef ? AttackDamageType(weapon) : DamageType.Pierce;
        }

        /// <summary>
        /// A missile weapon's ammo, equipped: a thrown weapon is its own; a launcher gets the tier's arrows, quarrels or
        /// darts (as /acxbench); null for melee
        /// </summary>
        private static WorldObject MissileAmmo(Player attacker, WorldObject weapon, int tier)
        {
            var style = weapon.DefaultCombatStyle;
            if (style == CombatStyle.ThrownWeapon)
                return weapon;
            if (style != CombatStyle.Bow && style != CombatStyle.Crossbow && style != CombatStyle.Atlatl)
                return null;
            var ammoWcid = style == CombatStyle.Crossbow ? QuarrelsByTier[tier - 1] : style == CombatStyle.Atlatl ? DartsByTier[tier - 1] : ArrowsByTier[tier - 1];
            var ammo = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(ammoWcid), new ObjectGuid(AmmoGuid));
            attacker.TryEquipObject(ammo, EquipMask.MissileAmmo);
            return ammo;
        }

        /// <summary>
        /// Missile attacks at a monster, as BenchMissile does them: one projectile made as Creature.LaunchProjectile makes it,
        /// each shot aim + reload + back to ready + a full accuracy bar
        /// </summary>
        private static void BenchShots(Player attacker, WorldObject weapon, WorldObject ammo, Creature target, Sums sums, int shots)
        {
            var thrown = weapon.DefaultCombatStyle == CombatStyle.ThrownWeapon;
            var projectile = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(ammo.WeenieClassId), new ObjectGuid(ProjectileGuid));
            projectile.ProjectileSource = attacker;
            projectile.ProjectileTarget = target;
            projectile.ProjectileLauncher = thrown ? null : weapon;
            projectile.ProjectileAmmo = ammo;
            AcxThrownWcids.CarryRoll(ammo, projectile);
            projectile.Location = new Position(attacker.Location);

            var stance = attacker.GetCombatStance();
            attacker.CurrentMotionState = new ACE.Server.Entity.Motion(stance);
            attacker.AttackHeight = AttackHeight.Medium;
            attacker.AccuracyLevel = 1.0f;
            var cycle = MotionTable.GetAnimationLength(attacker.MotionTableId, stance, MotionCommand.AimLevel, 1.0f)
                + MotionTable.GetAnimationLength(attacker.MotionTableId, stance, MotionCommand.Reload, attacker.GetAnimSpeed())
                + MotionTable.GetAnimationLength(attacker.MotionTableId, stance, MotionCommand.Reload, MotionCommand.Ready) + 1.0f;

            for (var shot = 0; shot < shots; shot++)
            {
                var damageEvent = Hit(attacker, target, projectile, sums);
                if (damageEvent != null)
                    sums.Direct += damageEvent.Damage;
                sums.Time += cycle;
                sums.Strikes++;
                sums.Swings++;
            }
        }

        /// <summary>
        /// A missile weapon's attack cycle, part by part (aim, throw/reload at the attack speed, back to ready), for the class
        /// bench's summary; empty for melee
        /// </summary>
        private static string MissileCycle(int level, WorldObject weapon, int tier)
        {
            var buff = BuffLevelFor(level);
            var attacker = CreateAttacker(level, new Options { Str = 100, Coord = 100, Quick = 100 });
            foreach (var line in BuffSpells)
                attacker.EnchantmentManager.Add(new ACE.Server.Entity.Spell(line[buff]), attacker, null, false);
            var ammo = MissileAmmo(attacker, weapon, tier);
            if (ammo == null)
                return "";
            attacker.TryEquipObject(weapon, weapon.ValidLocations ?? EquipMask.MissileWeapon);
            var stance = attacker.GetCombatStance();
            var animSpeed = attacker.GetAnimSpeed();
            var aim = MotionTable.GetAnimationLength(attacker.MotionTableId, stance, MotionCommand.AimLevel, 1.0f);
            var reload = MotionTable.GetAnimationLength(attacker.MotionTableId, stance, MotionCommand.Reload, animSpeed);
            var link = MotionTable.GetAnimationLength(attacker.MotionTableId, stance, MotionCommand.Reload, MotionCommand.Ready);
            return $" | cycle aim {aim:0.00}s + throw/reload {reload:0.00}s (speed {animSpeed:0.00}, weapon {weapon.WeaponTime}) + ready {link:0.00}s + bar 1.00s";
        }

        /// <summary>
        /// A launcher (or a thrown weapon, its own ammunition) tinked as the game does it: the rend of its ammo's damage type,
        /// the tinks left as mahogany (damage multiplier +0.04 each; JJ: "Unless you are bow then it's all mahog"), and real
        /// transfers
        /// </summary>
        private static List<uint> TinkLauncherReal(WorldObject launcher, WorldObject ammo)
        {
            var planned = DamageTransfers(launcher, new List<uint>(), launcher.MaxExtraSpellsCount);
            MutationCache.GetMutation(ImbueMutations[WorldObject.GetRendDamageType(MissileDamageType(launcher, ammo))]).TryMutate(launcher);
            for (var i = Math.Max(0, MaxTinks - launcher.NumTimesTinkered); i > 0; i--)
                MutationCache.GetMutation(MahoganyMutation).TryMutate(launcher);
            return TransferReal(launcher, planned);
        }

        /// <summary>
        /// The median (by damage) of real loot rolls of a weapon at a tier, any wield requirement, leaving out rolls with an
        /// ACX extra mutation or slayer
        /// </summary>
        private static LootRoll RollLootTier(uint wcid, int tier)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            var rolls = new List<LootRoll>();
            for (var attempt = 0; attempt < 2000 && rolls.Count < LootRollsPerKind; attempt++)
            {
                var wo = WorldObjectFactory.CreateWorldObject(weenie, new ObjectGuid(WeaponGuid));
                LootGenerationFactory.MutateItem(wo, new TreasureDeath { Tier = tier, LootQualityMod = 0 }, false);
                if (wo.IgnoreArmor != null || wo.CriticalFrequency != null || wo.GetProperty(PropertyFloat.CriticalMultiplier) != null || wo.SlayerCreatureType != null)
                    continue;
                rolls.Add(new LootRoll { Wcid = wcid, Damage = wo.Damage ?? 0, Variance = wo.DamageVariance ?? 0, WeaponTime = wo.WeaponTime ?? 0, Workmanship = wo.ItemWorkmanship ?? 0,
                    Wield = wo.WieldDifficulty ?? 0, DamageMod = wo.DamageMod, ElementalDamageBonus = wo.ElementalDamageBonus });
            }
            // by what a hit is worth: own damage (a launcher's: the tier's dart, as a stand-in for its ammunition, plus its
            // elemental bonus) x multiplier
            var ammo = NominalAmmoDamage[Math.Clamp(tier, 1, 8) - 1];
            return rolls.Count == 0 ? null : rolls.OrderBy(r => ((r.Damage > 0 ? r.Damage * (1 - r.Variance / 2) : ammo) + (r.ElementalDamageBonus ?? 0)) * (r.DamageMod ?? 1)).ElementAt(rolls.Count / 2);
        }

        /// <summary>
        /// What the examine window gets for a thrown weapon, as the server builds it (AppraiseInfo), and the lines AC:Unreal
        /// prints from it (its ACEAppraisalFormatting.h WeaponDetails: an ammunition type in the appraisal makes it a
        /// launcher's "Damage Bonus" and "Damage Modifier"), so a bench run shows the numbers a player sees
        /// </summary>
        private static string ThrownExamine(WorldObject weapon, int level)
        {
            var info = new ACE.Server.Network.Structure.AppraiseInfo(weapon, CreateAttacker(level, new Options()));
            var profile = info.WeaponProfile;
            if (profile == null)
                return "examine: no weapon profile";
            info.PropertiesInt.TryGetValue(PropertyInt.AmmoType, out var ammoType);
            var low = profile.Damage * (1.0 - Math.Clamp(profile.DamageVariance, 0.0, 1.0));
            var damage = profile.Damage - low > 0.0002 ? $"{low.ToString(low >= 10 ? "G4" : "G3", System.Globalization.CultureInfo.InvariantCulture)} - {profile.Damage}" : profile.Damage.ToString();
            var lines = ammoType != 0
                ? $"Damage Bonus: {damage}\nDamage Modifier: {(profile.DamageMod < 1 ? "-" : "+")}{(int)Math.Round(Math.Abs(profile.DamageMod - 1) * 100)}%."
                : $"Damage: {damage}, {ACE.Entity.Enum.DamageTypeExtensions.GetName(profile.DamageType)}";
            return $"examine (appraisal ammunition type {ammoType}):\n{lines}";
        }

        // Atlatl Dart, Prismatic, Deadly Prismatic (DartsByTier)
        private static readonly int[] NominalAmmoDamage = { 12, 12, 28, 28, 52, 52, 52, 52 };

        public static string StartClassBench(int level, bool debuff, DamageType missileType, Action<string> reply, List<uint> items = null)
        {
            if (running)
                return "A bench is already running.";
            running = true;

            Task.Run(() =>
            {
                try
                {
                    classBandLevel = level;   // transfers of the level's grade on both sides (BandCantrip)
                    reply(RunClassBench(level, debuff, missileType, items));
                }
                catch (Exception e)
                {
                    log.Error($"AcxBench class bench failed: {e}");
                    reply($"The class bench failed: {e.Message}");
                }
                finally
                {
                    classBandLevel = 0;   // a pool thread: the next bench on it has no band
                    running = false;
                }
            });
            return $"Class bench started at level {level}. Results go to Logs/acxbench/.";
        }

        /// <summary>
        /// Every weapon class against the monsters of a level (JJ 2026-11-15: "lets do some more benching"): one physical
        /// weapon a class (heavy, light, finesse and two-handed swords, bow, crossbow, atlatl, Eternal thrown), the median
        /// loot roll of the level's tier, untinked and tinked the game's way (melee: rend + iron/granite + transfers;
        /// launchers and thrown: rend + mahogany + transfers; no slayer), buffs cast, base skill at the wield requirement
        /// of the level's gear. Out: Logs/acxbench/acxclass-*.csv.
        /// </summary>
        private static string RunClassBench(int level, bool debuff, DamageType missileType = DamageType.Pierce, List<uint> items = null)
        {
            var started = DateTime.Now;
            countMisses = false;
            var monsters = SpawnedMonsters();
            var scale = PropertyManager.GetDouble("acx_bleed_damage_scale").Item;
            var tier = TierFor(level);
            var buff = BuffLevelFor(level);
            var skillBase = WieldGear.OrderBy(g => Math.Abs(g.Value.Level - level)).First().Key;

            uint FirstPhysical(IEnumerable<uint> wcids) => wcids.FirstOrDefault(w => MatchesElement(DatabaseManager.World.GetCachedWeenie(w), DamageType.Undef));
            // the missile classes all deal one damage type, so monsters' resistances don't decide between them: piercing
            // (the piercing launcher, or the plain one, whose ammunition pierces, and the Throwing Dart) unless another is
            // asked for (slashing, to stand beside the swords: typed launchers come from tier 5, the Throwing Axe always)
            uint FirstPiercing(IEnumerable<uint> wcids)
            {
                var list = wcids.ToList();
                DamageType Type(uint w) => (DamageType)(DatabaseManager.World.GetCachedWeenie(w)?.GetProperty(PropertyInt.DamageType) ?? 0);
                var pick = list.FirstOrDefault(w => Type(w) == missileType);
                if (pick == 0)
                    pick = list.FirstOrDefault(w => Type(w) == DamageType.Undef);
                return pick != 0 ? pick : FirstPhysical(list);
            }
            IEnumerable<uint> Table(Type table, string field) => ((Factories.Entity.ChanceTable<Factories.Enum.WeenieClassName>)table.GetField(field, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Select(e => (uint)e.result);
            IEnumerable<uint> TierTable(Type table) => ((List<Factories.Entity.ChanceTable<Factories.Enum.WeenieClassName>>)table.GetFields(BindingFlags.NonPublic | BindingFlags.Static)
                .First(f => f.FieldType == typeof(List<Factories.Entity.ChanceTable<Factories.Enum.WeenieClassName>>)).GetValue(null))[tier - 1].Select(e => (uint)e.result);

            var classes = new List<(string Name, uint Wcid)>
            {
                ("Heavy", FirstPhysical(Table(typeof(HeavyWeaponWcids), "LongSwords"))),
                ("Light", FirstPhysical(Table(typeof(LightWeaponWcids), "BroadSwords"))),
                ("Finesse", FirstPhysical(Table(typeof(FinesseWeaponWcids), "Sabras"))),
                ("Two Handed", FirstPhysical(Table(typeof(TwoHandedWeaponWcids), "Nodachis"))),
                ("Bow", FirstPiercing(TierTable(typeof(BowWcids_Aluvian)))),
                ("Crossbow", FirstPiercing(TierTable(typeof(CrossbowWcids)))),
                ("Atlatl", FirstPiercing(TierTable(typeof(AtlatlWcids)))),
                ("Thrown", FirstPiercing(AcxThrownWcids.TierWcids(tier))),
            };

            var sb = new StringBuilder("Level,Tier,Class,Weapon,Wcid,State,Damage,DamageMod,ElementalBonus,Variance,Wield,Workmanship,HitsToKill,AvgHit,SecondsToKill,FailedStrikePct\n");
            var summary = new StringBuilder();
            foreach (var (name, wcid) in classes)
            {
                var roll = wcid != 0 ? RollLootTier(wcid, tier) : null;
                if (roll == null)
                {
                    summary.AppendLine($"  {name}: no roll");
                    continue;
                }
                var times = new List<string>();
                foreach (var tinked in new[] { false, true })
                {
                    var weapon = MakeWeapon(roll);
                    if (tinked)
                    {
                        // launchers, and thrown weapons (their own ammunition, a multiplier like a bow's): Mahogany
                        var style = weapon.DefaultCombatStyle;
                        if (style == CombatStyle.Bow || style == CombatStyle.Crossbow || style == CombatStyle.Atlatl || style == CombatStyle.ThrownWeapon)
                        {
                            var probe = CreateAttacker(level, new Options());
                            TinkLauncherReal(weapon, MissileAmmo(probe, weapon, tier));
                        }
                        else
                            TinkReal(weapon, buff, new List<uint>());
                    }
                    var spells = weapon.Biota.GetKnownSpellsIds(weapon.BiotaDatabaseLock).Select(id => (uint)id).ToList();
                    var row = BenchGear(new List<(WorldObject, List<uint>)> { (weapon, spells) }, level, false, skillBase, monsters, scale, real: true, swings: 30, debuff: debuff);
                    sb.AppendLine(string.Join(",", level, tier, name, Csv(weapon.Name), wcid, tinked ? "tinked" : "untinked", weapon.Damage ?? 0, F(weapon.DamageMod ?? 0, "0.###"),
                        weapon.ElementalDamageBonus ?? 0, F(weapon.DamageVariance ?? 0, "0.###"), roll.Wield, roll.Workmanship, F(row.Hits, "0.##"), F(row.AvgHit, "0.#"), F(row.Seconds, "0.#"), F(row.FailPct, "0.#")));
                    times.Add($"{row.Seconds:0.0}s");
                }
                summary.AppendLine($"  {name} ({roll.Wield}): untinked {times[0]}, tinked {times[1]}{MissileCycle(level, MakeWeapon(roll), tier)}");
                if (AcxThrownWcids.IsEternalThrown(wcid))
                    summary.AppendLine("  " + ThrownExamine(MakeWeapon(roll), level).Replace("\n", "\n  "));
            }

            // items=: weapons benched as they come, no loot roll and no tinkering (JJ 2026-11-17, the quest, drop and crafted
            // thrown weapons: "We definetly need to check and make sure they do not ruin balance"), beside the classes above;
            // a quest weapon also with what the quest weapon rule allows (the rend of its damage type and two transfers).
            // An elemental one is benched against the monsters weak to its element, and so is its reference: the tier's
            // loot thrown weapon of that element, untinked and tinked (rows "Loot <element>").
            var thrownWcid = classes.First(c => c.Name == "Thrown").Wcid;
            // each item row is also given as a percentage of its reference's average hit (JJ 2026-11-19: "for now on we give it in
            // %. The Benched item is +% better than or -% worse than?"): as it comes against the untinked loot weapon, upgraded
            // against the tinked one
            var references = new Dictionary<DamageType, (double Untinked, double Tinked)>();
            foreach (var item in items ?? new List<uint>())
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(item);
                if (weenie == null)
                {
                    summary.AppendLine($"  {item}: no such weenie");
                    continue;
                }
                var weapon = WorldObjectFactory.CreateWorldObject(weenie, new ObjectGuid(WeaponGuid));
                var element = (AttackDamageType(weapon) & Elements) != 0 ? AttackDamageType(weapon) : DamageType.Undef;
                var against = element != DamageType.Undef ? $" vs {element}-weak" : "";

                double Row(string className, WorldObject w, uint wcid, string state, double reference = 0)
                {
                    var known = w.Biota.GetKnownSpellsIds(w.BiotaDatabaseLock).Select(id => (uint)id).ToList();
                    var r = BenchGear(new List<(WorldObject, List<uint>)> { (w, known) }, level, false, skillBase, monsters, scale, real: true, swings: 30, debuff: debuff, weakTo: element);
                    sb.AppendLine(string.Join(",", level, tier, className, Csv(w.Name), wcid, state + against, w.Damage ?? 0, F(w.DamageMod ?? 0, "0.###"),
                        w.ElementalDamageBonus ?? 0, F(w.DamageVariance ?? 0, "0.###"), w.WieldDifficulty ?? 0, w.ItemWorkmanship ?? 0, F(r.Hits, "0.##"), F(r.AvgHit, "0.#"), F(r.Seconds, "0.#"), F(r.FailPct, "0.#")));
                    var percent = reference > 0 ? $" ({(r.AvgHit / reference - 1) * 100:+0;-0;0}% vs loot)" : "";
                    summary.AppendLine($"  {w.Name} [{wcid}] {state}{against}: {r.Seconds:0.0}s, average hit {r.AvgHit:0.#}{percent}");
                    return r.AvgHit;
                }

                // the reference: the tier's loot thrown weapon of the item's element (a physical item: the class row's piercing one)
                var versionWcid = thrownWcid;
                if (!references.ContainsKey(element) && (element == DamageType.Undef || AcxThrownWcids.TryGetVersion(thrownWcid, element, out versionWcid)))
                {
                    var referenceRoll = RollLootTier(versionWcid, tier);
                    if (referenceRoll != null)
                    {
                        var name = "Loot " + (element != DamageType.Undef ? element.ToString() : "physical");
                        var untinkedHit = Row(name, MakeWeapon(referenceRoll), versionWcid, "untinked");
                        var tinkedReference = MakeWeapon(referenceRoll);
                        var loot = TinkLauncherReal(tinkedReference, MissileAmmo(CreateAttacker(level, new Options()), tinkedReference, tier));
                        references[element] = (untinkedHit, Row(name, tinkedReference, versionWcid, loot.Count > 0 ? $"tinked (rend + mahogany + {string.Join(" + ", loot.Select(id => new ACE.Server.Entity.Spell(id).Name))})" : "tinked (rend + mahogany, no transfers)"));
                    }
                }
                references.TryGetValue(element, out var refHits);

                Row("Item", weapon, item, "as it comes", refHits.Untinked);
                if (!weapon.IsAcxQuestWeapon)
                    continue;
                ImbueReal(weapon, ImbuedEffectType.Undef);
                var added = TransferReal(weapon, DamageTransfers(weapon, weapon.Biota.GetKnownSpellsIds(weapon.BiotaDatabaseLock).Select(id => (uint)id).ToList(), WorldObject.AcxQuestWeaponTransfers));
                Row("Item", weapon, item, added.Count > 0 ? $"rend + {string.Join(" + ", added.Select(id => new ACE.Server.Entity.Spell(id).Name))}" : "rend, no transfers", refHits.Tinked);
            }

            var dir = Path.Combine("Logs", "acxbench");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"acxclass-L{level}{(missileType != DamageType.Pierce ? "-" + missileType.ToString().ToLowerInvariant() : "")}{(debuff ? "-debuff" : "")}-{started:yyyyMMdd-HHmmss}.csv");
            File.WriteAllText(file, sb.ToString());
            return $"Class bench done in {(DateTime.Now - started).TotalSeconds:0}s at L{level} (T{tier}){(debuff ? " with Imperil and vulns" : "")}, transfers {BandTransferGrade(level)}, written to {file}.\n" + summary.ToString().TrimEnd();
        }

        // the element words of the stone-upgraded lines, one weapon per element
        private static readonly string[][] ElementWords =
        {
            new[] { "Shivering", "Smoldering", "Sparking", "Stinging" },   // Atlan
            new[] { "Chilling", "Flaming", "Coruscating", "Dissolving" },  // Isparian
        };

        /// <summary>
        /// A quest weapon's element versions: an Atlan's or Isparian's four, others just itself
        /// </summary>
        private static List<uint> ElementVersions(WorldObject quest)
        {
            var name = quest.Name ?? "";
            var words = ElementWords.FirstOrDefault(set => set.Any(w => name.Contains(w)));
            var word = words?.FirstOrDefault(w => name.Contains(w));
            if (word == null)
                return new List<uint> { quest.WeenieClassId };

            var names = words.Select(w => name.Replace(word, w)).ToList();
            using (var ctx = new WorldDbContext())
            {
                var found = ctx.WeeniePropertiesString.Where(p => p.Type == (ushort)PropertyString.Name && names.Contains(p.Value)).Select(p => new { p.ObjectId, p.Value }).ToList();
                return names.Select(n => found.FirstOrDefault(f => f.Value == n)?.ObjectId ?? 0).Where(w => w != 0).Distinct().ToList();
            }
        }

        /// <summary>
        /// A weapon (in its element versions, the best one per monster) against the monsters a player of this level hunts,
        /// with the level's buffs, the attack skills' base at the given value, and optionally a slayer stone against each
        /// monster
        /// </summary>
        private static readonly DamageType[] BenchDamageTypes = { DamageType.Slash, DamageType.Pierce, DamageType.Bludgeon, DamageType.Cold, DamageType.Fire, DamageType.Acid, DamageType.Electric };

        /// <summary>
        /// A monster an elemental weapon is taken to: the element is the damage type it resists least, or one of them
        /// (JJ 2026-11-17: "we have to tell it to use mobs weak against that element to make it accurate")
        /// </summary>
        private static bool IsWeakTo(Creature creature, DamageType element)
        {
            var own = creature.GetResistanceMod(Creature.GetResistanceType(element));
            return BenchDamageTypes.All(type => creature.GetResistanceMod(Creature.GetResistanceType(type)) <= own + 0.0001);
        }

        private static GearRow BenchGear(List<(WorldObject Weapon, List<uint> Spells)> versions, int level, bool slayer, int skillBase, List<MonsterInfo> monsters, double scale, bool real = false, int swings = 10, bool debuff = false, DamageType weakTo = DamageType.Undef)
        {
            var buff = BuffLevelFor(level);
            var row = new GearRow { Damage = versions[0].Weapon.Damage ?? 0, Variance = versions[0].Weapon.DamageVariance ?? 0 };

            var setups = new List<(Player Attacker, WorldObject Weapon, List<ACE.Server.Entity.Spell> Spells, WorldObject Ammo)>();
            foreach (var (weapon, spellIds) in versions)
            {
                Player attacker;
                List<ACE.Server.Entity.Spell> spells;
                if (real)
                {
                    // the game's way: the level's buff spells cast on the attacker, and every weapon spell as cast, the
                    // enchantment registry keeping the stronger of a same-category pair
                    attacker = CreateAttacker(level, new Options { Str = 100, Coord = 100, Quick = 100 });
                    foreach (var line in BuffSpells)
                        attacker.EnchantmentManager.Add(new ACE.Server.Entity.Spell(line[buff]), attacker, null, false);
                    spells = spellIds.Select(id => new ACE.Server.Entity.Spell(id)).Where(sp => !sp.NotFound).ToList();
                }
                else
                {
                    attacker = CreateAttacker(level, new Options { Str = 100 + AttributeBuff[buff], Coord = 100 + AttributeBuff[buff], Quick = 100 + AttributeBuff[buff] });
                    weapon.Damage = (weapon.Damage ?? 0) + BloodDrinkerBuff[buff];
                    weapon.WeaponTime = Math.Max(0, (weapon.WeaponTime ?? 0) - SwiftKillerBuff[buff]);
                    spells = ResolveWeaponSpells(weapon, spellIds, attacker, buff);
                }

                // the base skill rending reads (capped at 400); misses are retried, so every attack still lands
                foreach (var skill in new[] { Skill.HeavyWeapons, Skill.LightWeapons, Skill.FinesseWeapons, Skill.TwoHandedCombat, Skill.MissileWeapons })
                {
                    var creatureSkill = attacker.GetCreatureSkill(skill);
                    creatureSkill.InitLevel = 0;
                    creatureSkill.InitLevel = (uint)Math.Max(0, skillBase - (int)creatureSkill.Base);
                }
                setups.Add((attacker, weapon, spells, MissileAmmo(attacker, weapon, TierFor(level))));
            }

            var low = level + 1;
            var high = level + Math.Max(8, (int)Math.Ceiling(level * 0.15));
            var results = new List<(double Hits, double AvgHit, double Seconds, int Spots)>();
            double strikes = 0, failures = 0;

            // no slayer stone on tinked loot (JJ 2026-11-15: "Slayers are rarity. Most people will not have a slayer on every
            // weapon. So leave slayer out of the benching"); a weapon's own slayer still counts, on its own creature type
            foreach (var monster in monsters.Where(m => m.Level >= low && m.Level <= high))
            {
                Creature creature;
                try
                {
                    creature = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(monster.Wcid), new ObjectGuid(MonsterGuid)) as Creature;
                }
                catch
                {
                    continue;
                }
                if (creature == null || creature.Invincible)
                    continue;
                if (weakTo != DamageType.Undef && !IsWeakTo(creature, weakTo))
                    continue;
                creature.Location = new Position(BenchCell, 50.0f, 51.0f, 0.0f, 0, 0, 1, 0);
                var health = (double)creature.Health.MaxValue;
                if (health <= 0)
                    continue;

                // the attack skill sits at the wield requirement (for rending), so the monster's melee defense goes to its
                // attribute part only: defense never enters damage, and every swing must land
                foreach (var defenseSkill in new[] { Skill.MeleeDefense, Skill.MissileDefense })
                {
                    var defense = creature.GetCreatureSkill(defenseSkill);
                    defense.InitLevel = 0;
                    defense.Ranks = 0;
                }

                if (debuff)
                {
                    var caster = setups[0].Attacker;
                    creature.EnchantmentManager.Add(new ACE.Server.Entity.Spell(ImperilSpells[buff]), caster, null);
                    foreach (var type in setups.Select(st => st.Ammo != null ? MissileDamageType(st.Weapon, st.Ammo) : AttackDamageType(st.Weapon)).Distinct())
                        creature.EnchantmentManager.Add(new ACE.Server.Entity.Spell(VulnSpells[type][buff]), caster, null);
                }

                var bestHit = 0.0;
                var bestDps = 0.0;
                foreach (var (attacker, weapon, spells, ammo) in setups)
                {
                    if (!attacker.TryEquipObject(weapon, weapon.ValidLocations ?? (ammo != null ? EquipMask.MissileWeapon : EquipMask.MeleeWeapon)))
                        continue;
                    foreach (var spell in spells)
                    {
                        // added straight to the target CreateItemSpell picks: its "You cast" line needs a session
                        var onWeapon = spell.School == MagicSchool.ItemEnchantment && !spell.HasItemCategory && !spell.IsPortalSpell;
                        (onWeapon ? weapon : attacker).EnchantmentManager.Add(spell, weapon, weapon, true);
                    }
                    var sums = new Sums();
                    if (ammo != null)
                        BenchShots(attacker, weapon, ammo, creature, sums, swings);
                    else
                        BenchSwings(attacker, weapon, creature, sums, 1.0f, swings, scale);
                    attacker.TryDequipObject(weapon.Guid, out _, out _);
                    strikes += sums.Swings;
                    failures += sums.Failures;
                    if (sums.Time <= 0 || sums.Swings <= 0)
                        continue;

                    var avgHit = sums.Direct / sums.Swings;   // per attack, as a player counts hits
                    if (avgHit > bestHit)
                    {
                        bestHit = avgHit;
                        bestDps = (sums.Direct + sums.Bleed) / sums.Time;
                    }
                }
                if (bestHit > 0)
                    results.Add((health / bestHit, bestHit, health / bestDps, monster.Spots));
            }

            row.FailPct = strikes > 0 ? failures * 100 / strikes : 0;   // attacks that never landed (should be 0)
            if (results.Count > 0)
            {
                var total = (double)results.Sum(r => r.Spots);
                double Median(Func<(double Hits, double AvgHit, double Seconds, int Spots), double> pick)
                {
                    var acc = 0.0;
                    foreach (var r in results.OrderBy(pick))
                    {
                        acc += r.Spots;
                        if (acc >= total / 2)
                            return pick(r);
                    }
                    return pick(results.Last());
                }
                row.Hits = Median(r => r.Hits);
                row.AvgHit = Median(r => r.AvgHit);
                row.Seconds = Median(r => r.Seconds);
                row.OneHit = results.Where(r => r.Hits <= 1).Sum(r => r.Spots) * 100 / total;
                row.TwoHits = results.Where(r => r.Hits <= 2).Sum(r => r.Spots) * 100 / total;
            }
            return row;
        }
    }
}
