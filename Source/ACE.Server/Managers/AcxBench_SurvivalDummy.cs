using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ACE.Database;
using ACE.Database.Adapter;
using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Mutations;
using ACE.Server.Factories;
using ACE.Server.Physics.Animation;
using ACE.Server.WorldObjects;

using Spell = ACE.Server.Entity.Spell;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: the survival bench's dummy mode (JJ 2026-11-16: he metered weapons on the Training Dummy with /dps, on a level 999
    /// character with 9999 in everything, so attribute and skill spells on the weapons counted for nothing; "the same test done by your
    /// realistic builds"; "I did not test cleaving with AR or cleaving with bs or cleaving with CB. That would be interest too").
    /// Each listed item is copied out of the shard DB (the original is never touched: a new guid, same properties, spells, tinks and
    /// imbues), worn by the build's realistic character (its weapon skill follows the item's; wield requirements are not checked),
    /// with the item's own spells as worn spells and the build's self buffs on top, and swung at a spawned Training Dummy (900809)
    /// for 60 seconds from the first strike, counted the way Player_AcxDps counts. transfers=1 adds a row with the free transfer
    /// slots filled as the gear bench does. Variants: JJ's untinked [AC] Partizan imbued and tinked with the game's mutation scripts.
    /// Out: Logs/acxbench/acxsurvival-dummy-*.csv.
    /// </summary>
    public static partial class AcxBench
    {
        public class DummyOptions
        {
            public List<uint> Items = new();
            public bool Variants = true;
            public List<int> Levels = new() { 275 };
            public List<string> Builds = new() { "melee" };
            public double Seconds = 60;
            public bool Transfers;                  // also a row with the free transfer slots filled
            public int Runs = 10;                   // 60-second tests per row, averaged (one test swings with crit luck)
            public bool Veteran;                    // the "maxed veteran" character instead of the fresh one
        }

        private const uint TrainingDummyWcid = 900809;
        private const uint DummyCopyGuid = 0xFFFFFDE0;

        private static readonly Dictionary<Skill, uint[]> DummyMasteries = new()
        {
            { Skill.LightWeapons, new uint[] { 4518, 4590, 4614, 4620, 4631 } },
            { Skill.FinesseWeapons, new uint[] { 4538 } },
            { Skill.TwoHandedCombat, new uint[] { 5032 } },
        };

        public static string StartDummy(DummyOptions options, Action<string> reply)
        {
            if (running)
                return "A bench is already running.";
            running = true;

            Task.Run(() =>
            {
                try
                {
                    reply(RunDummy(options));
                }
                catch (Exception e)
                {
                    log.Error($"AcxBench dummy failed: {e}");
                    reply($"The dummy bench failed: {e.Message}");
                }
                finally
                {
                    running = false;
                }
            });
            return $"Dummy bench started: {options.Items.Count} items{(options.Variants ? " + the [AC] Partizan variants" : "")}, levels {string.Join(",", options.Levels)}, builds {string.Join(",", options.Builds)}, {options.Runs} x {options.Seconds:0} s each. Results go to Logs/acxbench/.";
        }

        /// <summary>
        /// A copy of a shard item: its biota read from the shard DB (not cached, not saved, never written back) under a bench guid
        /// </summary>
        private static WorldObject CopyShardItem(uint guid)
        {
            var dbBiota = DatabaseManager.Shard.BaseDatabase.GetBiota(guid, true);
            if (dbBiota == null)
                return null;
            var biota = BiotaConverter.ConvertToEntityBiota(dbBiota);
            biota.Id = DummyCopyGuid;
            var wo = WorldObjectFactory.CreateWorldObject(biota);
            if (wo == null)
                return null;
            // loose in the bench, not in anyone's pack
            wo.ContainerId = null;
            wo.WielderId = null;
            wo.Location = null;
            return wo;
        }

        private class DummyItem
        {
            public string Label, Notes = "";
            public uint Guid;
            public Func<WorldObject> Make;   // a fresh copy, variant work done
        }

        /// <summary>
        /// The [AC] Partizan variants (JJ 2026-11-16): an imbue gem's mutation script (1 tink), the best iron/granite split for the rest
        /// (BestIron, with Blood Thirst if the transfer row plans it), optional extra mutations as LootGenerationFactory_AcxMutations sets
        /// them
        /// </summary>
        private const uint PartizanWcid = 30591;

        /// <summary>
        /// The Partizan variants (JJ 2026-11-16), all from one base: the median tier 8 loot Partizan (RollLootTier, as the gear bench rolls
        /// loot: no ACX extra mutation or slayer), untinked; then the built-in(s) set as LootGenerationFactory_AcxMutations sets them
        /// (Armor Cleaving: IgnoreArmor 0.625; Biting Strike: CriticalFrequency 0.15; Crushing Blow: CriticalMultiplier 2.0), the imbue
        /// gem's mutation script (1 tink) and the best iron/granite split for the rest (BestIron)
        /// </summary>
        private static List<DummyItem> DummyVariants(WorldObject baseRoll)
        {
            var list = new List<DummyItem>();
            void AC(WorldObject w) => w.IgnoreArmor = 0.625f;
            void BS(WorldObject w) => w.CriticalFrequency = 0.15f;
            void CB(WorldObject w) => w.SetProperty(PropertyFloat.CriticalMultiplier, 2.0);
            void Add(string label, string notes, Action<WorldObject> extras, ImbuedEffectType? imbue)
            {
                list.Add(new DummyItem
                {
                    Label = label,
                    Notes = notes,
                    Guid = 0,
                    Make = () =>
                    {
                        var w = CloneRolled(baseRoll, WeaponGuid);
                        extras?.Invoke(w);
                        if (imbue != null)
                            ImbueReal(w, imbue.Value);
                        return w;
                    },
                });
            }
            // the full matrix (JJ: "My guess is that BS or CS + CB or the CB type will be the winner over any combo plus AR"): each built-in x
            // each imbue on the same base; the pairs the ACX pair rule refuses (RecipeManager_AcxPairs) stay in as reference rows
            var builtIns = new (string Tag, Action<WorldObject> Set)[] { ("[AC]", AC), ("[BS]", BS), ("[CB]", CB) };
            var imbues = new (string Name, ImbuedEffectType Imbue)[]
            {
                ("Armor Rending (Sunstone)", ImbuedEffectType.ArmorRending), ("Pierce Rend (Black Garnet)", ImbuedEffectType.PierceRending),
                ("Critical Strike (Black Opal)", ImbuedEffectType.CriticalStrike), ("Crippling Blow (Fire Opal)", ImbuedEffectType.CripplingBlow),
            };
            var blocked = new Dictionary<(string, ImbuedEffectType), string>
            {
                { ("[AC]", ImbuedEffectType.ArmorRending), "Armor Cleaving refuses Sunstone" },
                { ("[BS]", ImbuedEffectType.CriticalStrike), "Biting Strike refuses Black Opal" },
                { ("[CB]", ImbuedEffectType.CripplingBlow), "Crushing Blow refuses Fire Opal" },
            };
            Add("[AC] untinked", "untinked reference", AC, null);
            foreach (var (tag, set) in builtIns)
                foreach (var (name, imbue) in imbues)
                    Add($"{tag} + {name}", blocked.TryGetValue((tag, imbue), out var why) ? $"matrix; blocked in game, reference only: {why}" : "matrix", set, imbue);
            var rare = "rare roll: loot gets at most 2 of AC/BS/CB, about 2% each";
            Add("[AC][BS] + Crippling Blow (Fire Opal)", rare, w => { AC(w); BS(w); }, ImbuedEffectType.CripplingBlow);
            Add("[AC][CB] + Critical Strike (Black Opal)", rare, w => { AC(w); CB(w); }, ImbuedEffectType.CriticalStrike);
            return list;
        }

        private class DummyStats
        {
            public double Damage, Bleed, Strikes, Hits, Misses, Crits, Seconds, Swings;
        }

        /// <summary>
        /// Full-power swings at the dummy (PlayerAct's melee cycle: swing animation at the attack speed with ACX's single weapon speed, +
        /// the power bar refill), every strike through DamageEvent, counted as Player_AcxDps counts: from the first strike, for the
        /// test's length; hits, misses (evaded strikes), crits; bleeds (dagger / multi-strike sword) are not on the meter, so apart
        /// </summary>
        private static DummyStats SwingAtDummy(Player p, WorldObject weapon, Creature dummy, double seconds)
        {
            var stats = new DummyStats();
            var stance = p.CurrentMotionState.Stance;
            p.PowerLevel = 1.0f;
            p.AttackHeight = AttackHeight.Medium;
            p.PrevMotionCommand = MotionCommand.Invalid;
            var bleeds = weapon.W_WeaponType == WeaponType.Dagger || (weapon.W_WeaponType == WeaponType.Sword && weapon.W_AttackType.IsMultiStrike());
            var scale = PropertyManager.GetDouble("acx_bleed_damage_scale").Item;

            var t = 0.0;
            double? first = null;
            while (true)
            {
                var animSpeed = p.GetAnimSpeed() * p.GetAcxSingleWeaponAnimSpeedMod();
                var motion = p.GetSwingAnimation();
                var animLength = MotionTable.GetAnimationLength(p.MotionTableId, stance, motion, animSpeed);
                if (animLength <= 0)
                    animLength = 1.0f;
                var frames = MotionTable.GetAttackFrames(p.MotionTableId, stance, motion);
                var times = frames.Count > 0 ? frames.Select(fr => (double)fr.time).ToList() : new List<double> { 0.5 };
                var swing = 0.0;
                var stop = false;
                foreach (var ft in times)
                {
                    var at = t + ft * animLength;
                    first ??= at;
                    if (at - first.Value > seconds)
                    {
                        stop = true;
                        break;
                    }
                    stats.Strikes++;
                    var de = DamageEvent.CalculateDamage(p, dummy, weapon);
                    if (de.HasDamage && !de.GeneralFailure)
                    {
                        stats.Hits++;
                        stats.Damage += de.Damage;
                        swing += de.Damage;
                        if (de.IsCritical)
                            stats.Crits++;
                    }
                    else
                        stats.Misses++;
                    dummy.Health.Current = dummy.Health.MaxValue;   // the dummy never runs out
                }
                if (bleeds && swing > 0)
                    stats.Bleed += 0.9 * Math.Round((5 + (int)Math.Round(swing)) * scale);
                if (stop)
                    break;
                stats.Swings++;
                t += animLength + p.PowerLevel * p.GetAcxSingleWeaponRefillMod();
            }
            stats.Seconds = seconds;
            return stats;
        }

        /// <summary>
        /// The build with its weapon skill switched to the item's (same place in the XP order, specialized, the matching mastery buff)
        /// </summary>
        private static SurvivalBuild BuildForSkill(SurvivalBuild b, Skill skill)
        {
            var main = b.Style == "melee" ? Skill.HeavyWeapons : b.Style == "missile" ? Skill.MissileWeapons : Skill.WarMagic;
            if (skill == main || skill == Skill.None)
                return b;
            return new SurvivalBuild
            {
                Name = b.Name, Source = b.Source, Style = b.Style, Shield = b.Shield, Pet = b.Pet,
                Str = b.Str, End = b.End, Coord = b.Coord, Quick = b.Quick, Focus = b.Focus, Self = b.Self,
                Specialized = b.Specialized.Select(s => s == main ? skill : s).ToArray(),
                Trained = b.Trained.Where(s => s != skill).ToArray(),
                Spend = b.Spend.Select(s => s == $"s:{main}" ? $"s:{skill}" : s).ToArray(),
                SkilledAug = b.SkilledAug,
            };
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Atlan grades against loot (JJ 2026-11-16: "It looks like the end game atlan is the best in the game. I wonder if it is the best
        // at every wield level you can get one?"): each Atlan sword grade at its own wield level, on the realistic melee character of the
        // matching level in its full gear, in each forge flavor (set as AcxAtlans.SetFlavor sets it; the flavor fills its imbue slot),
        // its 2 transfers filled; against the loot Heavy weapons of the same wield requirement, rolled through the loot code at the tiers
        // that give it, each with [AC] / [BS] / [CB] + the rend of its damage type, and [CB] + Critical Strike, imbue + the best 9
        // iron/granite, transfers filled. Out: Logs/acxbench/acxsurvival-atlans-*.csv.
        // ---------------------------------------------------------------------------------------------------------------

        private static readonly (uint Wcid, int Wield, int[] Levels)[] AtlanGrades =
        {
            (46091, 325, new[] { 115 }), (46090, 350, new[] { 155 }), (46089, 370, new[] { 175 }), (46101, 400, new[] { 200, 275 }),
        };
        private static readonly ImbuedEffectType[] AtlanFlavors = { ImbuedEffectType.ArmorRending, ImbuedEffectType.CriticalStrike, ImbuedEffectType.CripplingBlow };

        // the transfers: the Atlan gets Legendary Blood Thirst + Legendary Heart Thirst (JJ: "lets assume someone puts leg blood thirst on it and
        // leg heart seeker"); loot fills every slot with the legendaries that add the most damage, best first (JJ: "assume the player will add
        // the legendary that increases the most damage on the loot"): the gear bench's DamageTransfers (Blood Thirst, the damage attribute's,
        // Quickness), then Heart Thirst if a slot is left
        private const uint LegendaryHeartThirst = 6094;
        private static List<uint> AtlanTransfers(WorldObject w, int slots) =>
            new[] { LegendaryBloodThirst, LegendaryHeartThirst }
                .Where(id => !w.Biota.GetKnownSpellsIds(w.BiotaDatabaseLock).Contains((int)id)).Take(Math.Max(0, slots)).ToList();
        private static List<uint> LootTransfers(WorldObject w, int slots)
        {
            var owned = w.Biota.GetKnownSpellsIds(w.BiotaDatabaseLock).Select(id => (uint)id).ToList();
            var list = DamageTransfers(w, owned, slots);
            if (list.Count < slots && !owned.Contains(LegendaryHeartThirst))
                list.Add(LegendaryHeartThirst);
            return list;
        }

        public static string StartAtlans(int runs, int gearLevel, Action<string> reply)
        {
            if (running)
                return "A bench is already running.";
            running = true;
            Task.Run(() =>
            {
                try { reply(RunAtlans(runs, gearLevel)); }
                catch (Exception e) { log.Error($"AcxBench atlans failed: {e}"); reply($"The Atlan bench failed: {e.Message}"); }
                finally { running = false; }
            });
            return $"Atlan bench started: {AtlanGrades.Length} grades, {runs} x 60 s per row. Results go to Logs/acxbench/.";
        }

        /// <summary>
        /// The median (by average hit) of 41 real loot rolls of a weapon at a wield requirement, from the tiers that roll it, kept whole
        /// </summary>
        private static WorldObject RollAtWield(uint wcid, int wield)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie == null || !WieldGear.TryGetValue(wield, out var gearInfo))
                return null;
            var rolls = new List<WorldObject>();
            for (var attempt = 0; attempt < 20000 && rolls.Count < LootRollsPerKind; attempt++)
            {
                var wo = WorldObjectFactory.CreateWorldObject(weenie, new ObjectGuid(WeaponGuid));
                LootGenerationFactory.MutateItem(wo, new TreasureDeath { Tier = gearInfo.Tiers[attempt % gearInfo.Tiers.Length], LootQualityMod = 0 }, false);
                if (wo.WieldDifficulty != wield || wo.IgnoreArmor != null || wo.CriticalFrequency != null || wo.GetProperty(PropertyFloat.CriticalMultiplier) != null || wo.SlayerCreatureType != null)
                    continue;
                rolls.Add(wo);
            }
            return rolls.Count == 0 ? null : rolls.OrderBy(r => (r.Damage ?? 0) * (1 - (r.DamageVariance ?? 0) / 2)).ElementAt(rolls.Count / 2);
        }

        private static double MonsterHealthOf(uint wcid) => MakeMonster(wcid, false)?.Health.MaxValue ?? 0;

        private static string RunAtlans(int runs, int gearLevel)
        {
            var started = DateTime.Now;
            survivalVeteran = false;
            survivalGear = gearLevel;
            survivalLootTier = 0;
            survivalBombImbue = null;
            survivalSetOverride = null;
            survivalAetheria = "defense";
            survivalTrinket = null;
            var build = SurvivalBuilds.First(x => x.Name == "melee");
            var sb = new StringBuilder("Level,Wield,Side,Weapon,Kind,Version,Damage,Variance,Imbue,Tinks,Transfers,WieldMet,DummyDps,DummyAverageHit,DummyCritPctOfHits,AttacksPerMinute,Monster,MonsterLevel,MonsterDps,MonsterHitPct,MonsterKillSec\n");
            var summary = new StringBuilder();

            var spawnedAll = SpawnedMonsters();
            var heavyKinds = typeof(Factories.Tables.Wcids.HeavyWeaponWcids).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(Factories.Entity.ChanceTable<Factories.Enum.WeenieClassName>))
                .Select(f => (Name: f.Name, Wcid: ((Factories.Entity.ChanceTable<Factories.Enum.WeenieClassName>)f.GetValue(null)).Select(e => (uint)e.result)
                    .FirstOrDefault(w => MatchesElement(DatabaseManager.World.GetCachedWeenie(w), DamageType.Undef))))
                .Where(k => k.Wcid != 0).ToList();

            foreach (var (atlanWcid, wield, levels) in AtlanGrades)
            {
                var lootBases = heavyKinds.Select(k => (k.Name, Roll: RollAtWield(k.Wcid, wield))).Where(k => k.Roll != null).ToList();
                foreach (var level in levels)
                {
                    var gear = RollSurvivalGear(level);
                    var buff = BuffLevelFor(level);
                    // a real monster of the level band: the most-spawned kind 1-15% above the level (as /acxxpmodel picks them)
                    var band = spawnedAll.Where(m => m.Level > level && m.Level <= level + Math.Max(8, (int)Math.Ceiling(level * 0.15))).OrderByDescending(m => m.Spots).FirstOrDefault();
                    var rows = new List<(string Side, string Weapon, string Kind, string Version, double Dps, double AvgHit, double Crit, string Met, double MobDps)>();

                    (double Dps, double AvgHit, double Crit, double Apm, string Met, WorldObject Probe, double MobDps, double MobHitPct) Bench(Func<WorldObject> make)
                    {
                        double dmg = 0, secs = 0, hits = 0, crits = 0, strikes = 0, mobDmg = 0, mobSecs = 0, mobHits = 0, mobStrikes = 0;
                        string met = "";
                        WorldObject probe = null;
                        for (var r = 0; r < runs; r++)
                        {
                            var weapon = make();
                            probe ??= weapon;
                            var p = DressSurvivor(build, level, -1, gear, 0, out var loadout, null, weapon);
                            if (r == 0)
                            {
                                var skill = p.GetCreatureSkill(weapon.WeaponSkill);
                                met = (weapon.WieldDifficulty ?? 0) <= skill.Base ? "yes" : $"no: base {weapon.WeaponSkill} {skill.Base} < {weapon.WieldDifficulty} (equipped anyway)";
                            }
                            var dummy = MakeMonster(TrainingDummyWcid, false);
                            var st = SwingAtDummy(p, loadout.Weapon, dummy, 60);
                            dmg += st.Damage; secs += st.Seconds; hits += st.Hits; crits += st.Crits; strikes += st.Strikes;
                            if (band != null && MakeMonster(band.Wcid, false) is Creature mob)
                            {
                                // the same swings at the real monster: its defenses count, so misses (and Heart Thirst) show; it doesn't fight back
                                var ms = SwingAtDummy(p, loadout.Weapon, mob, 60);
                                mobDmg += ms.Damage; mobSecs += ms.Seconds; mobHits += ms.Hits; mobStrikes += ms.Strikes;
                            }
                        }
                        return (dmg / secs, hits > 0 ? dmg / hits : 0, hits > 0 ? crits * 100 / hits : 0, strikes / secs * 60, met, probe,
                            mobSecs > 0 ? mobDmg / mobSecs : 0, mobStrikes > 0 ? mobHits * 100 / mobStrikes : 0);
                    }

                    void Row(string side, WorldObject w, string kind, string version, (double Dps, double AvgHit, double Crit, double Apm, string Met, WorldObject Probe, double MobDps, double MobHitPct) res, string transfers)
                    {
                        rows.Add((side, w.Name, kind, version, res.Dps, res.AvgHit, res.Crit, res.Met, res.MobDps));
                        sb.AppendLine(string.Join(",", level, wield, side, Csv(w.Name), kind, Csv(version), w.Damage ?? 0, F(w.DamageVariance ?? 0, "0.###"), w.ImbuedEffect, w.NumTimesTinkered,
                            Csv(transfers), Csv(res.Met), F(res.Dps, "0.0"), F(res.AvgHit, "0.0"), F(res.Crit, "0"), F(res.Apm, "0.0"),
                            Csv(band?.Name ?? ""), band?.Level ?? 0, F(res.MobDps, "0.0"), F(res.MobHitPct, "0"), band != null && res.MobDps > 0 ? F(MonsterHealthOf(band.Wcid) / res.MobDps, "0.0") : ""));
                    }

                    // the Atlan in each flavor, 2 transfers
                    foreach (var flavor in AtlanFlavors)
                    {
                        var transfers = "";
                        WorldObject MakeAtlan()
                        {
                            var w = WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(atlanWcid), new ObjectGuid(WeaponGuid));
                            w.ImbuedEffect = (w.ImbuedEffect & ~(ImbuedEffectType.ArmorRending | ImbuedEffectType.CriticalStrike | ImbuedEffectType.CripplingBlow)) | flavor;
                            var owned = w.Biota.GetKnownSpellsIds(w.BiotaDatabaseLock).Select(id => (uint)id).ToList();
                            var added = TransferReal(w, AtlanTransfers(w, Math.Max(0, w.MaxExtraSpellsCount - (w.ExtraSpellsCount ?? 0))));
                            transfers = string.Join(" ", added.Select(id => new Spell(id).Name.Replace("Legendary ", "L.")));
                            return w;
                        }
                        var res = Bench(MakeAtlan);
                        Row("Atlan", res.Probe, "Atlan", $"flavor {flavor}", res, transfers);
                    }

                    // loot: every Heavy kind at this wield, each combo; built-ins as LootGenerationFactory_AcxMutations sets them
                    var combos = new (string Name, Action<WorldObject> Builtin, ImbuedEffectType Imbue)[]
                    {
                        ("[AC] + rend", w => w.IgnoreArmor = 0.625f, ImbuedEffectType.Undef),
                        ("[BS] + rend", w => w.CriticalFrequency = 0.15f, ImbuedEffectType.Undef),
                        ("[CB] + rend", w => w.SetProperty(PropertyFloat.CriticalMultiplier, 2.0), ImbuedEffectType.Undef),
                        ("[CB] + Critical Strike", w => w.SetProperty(PropertyFloat.CriticalMultiplier, 2.0), ImbuedEffectType.CriticalStrike),
                    };
                    foreach (var (kindName, roll) in lootBases)
                        foreach (var (comboName, builtin, imbue) in combos)
                        {
                            var transfers = "";
                            WorldObject MakeLoot()
                            {
                                var w = CloneRolled(roll, WeaponGuid);
                                builtin(w);
                                ImbueReal(w, imbue);
                                var planned = LootTransfers(w, w.MaxExtraSpellsCount);
                                var tinks = Math.Max(0, MaxTinks - w.NumTimesTinkered);
                                var iron = BestIron(w, BloodDrinkerBuff[buff] + (planned.Contains(LegendaryBloodThirst) ? 10 : 0), tinks);
                                for (var i = 0; i < iron; i++)
                                    MutationCache.GetMutation(IronMutation).TryMutate(w);
                                for (var i = 0; i < tinks - iron; i++)
                                    MutationCache.GetMutation(GraniteMutation).TryMutate(w);
                                var added = TransferReal(w, planned);
                                transfers = string.Join(" ", added.Select(id => new Spell(id).Name.Replace("Legendary ", "L.")));
                                return w;
                            }
                            var res = Bench(MakeLoot);
                            Row("Loot", res.Probe, kindName, comboName, res, transfers);
                        }

                    var bestAtlan = rows.Where(r => r.Side == "Atlan").OrderByDescending(r => r.Dps).First();
                    var bestLoot = rows.Where(r => r.Side == "Loot").OrderByDescending(r => r.Dps).FirstOrDefault();
                    var bestPerCombo = rows.Where(r => r.Side == "Loot").GroupBy(r => r.Version).Select(g => g.OrderByDescending(r => r.Dps).First());
                    var bestAtlanMob = rows.Where(r => r.Side == "Atlan").OrderByDescending(r => r.MobDps).First();
                    var bestLootMob = rows.Where(r => r.Side == "Loot").OrderByDescending(r => r.MobDps).FirstOrDefault();
                    if (band != null && bestLootMob.MobDps > 0)
                        summary.AppendLine($"  wield {wield}, L{level} vs {band.Name} (L{band.Level}, real defenses): best Atlan {bestAtlanMob.Version} {bestAtlanMob.MobDps:0} DPS, best loot {bestLootMob.Kind} {bestLootMob.Version} {bestLootMob.MobDps:0} DPS ({(bestAtlanMob.MobDps / bestLootMob.MobDps - 1) * 100:+0;-0}% for the Atlan)");
                    summary.AppendLine($"  wield {wield}, L{level} (dummy): best Atlan {bestAtlan.Weapon} ({bestAtlan.Version}) {bestAtlan.Dps:0} DPS; best loot {bestLoot.Weapon} {bestLoot.Kind} {bestLoot.Version} {bestLoot.Dps:0} DPS "
                        + $"({(bestAtlan.Dps / bestLoot.Dps - 1) * 100:+0;-0}% for the Atlan); per loot combo: {string.Join("; ", bestPerCombo.Select(r => $"{r.Version} {r.Dps:0} ({r.Kind})"))}; wield met: Atlan {bestAtlan.Met}, loot {bestLoot.Met}");
                }
            }

            var dir = Path.Combine("Logs", "acxbench");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"acxsurvival-atlans-gear{gearLevel}-{started:yyyyMMdd-HHmmss}.csv");
            File.WriteAllText(file, sb.ToString());
            return $"Atlan bench done in {(DateTime.Now - started).TotalSeconds:0}s (fresh melee character, gear level {gearLevel}, {runs} x 60 s per row, Training Dummy), written to {file}.\n" + summary.ToString().TrimEnd();
        }

        private static string RunDummy(DummyOptions o)
        {
            var started = DateTime.Now;
            var items = new List<DummyItem>();
            foreach (var guid in o.Items)
                items.Add(new DummyItem { Guid = guid, Make = () => CopyShardItem(guid) });
            survivalVeteran = o.Veteran;
            survivalLootTier = 0;
            survivalBombImbue = null;
            WorldObject partizan = null;
            if (o.Variants)
            {
                partizan = RollWeaponFull(PartizanWcid, 8);
                if (partizan != null)
                    items.AddRange(DummyVariants(partizan));
            }

            var sb = new StringBuilder("Level,Build,Guid,Item,Variant,Transfers,Row,WeaponSkill,CharacterSkill,WieldRequirement,WieldMet,Damage,Variance,Imbue,Tinks,Spells,"
                + "SecondsPerTest,Tests,TotalDamage,DamagePerSecond,AttacksPerMinute,Hits,Misses,MissPct,AverageHit,Crits,CritPctOfHits,BleedPerSecondNotOnMeter,Notes\n");
            var summary = new StringBuilder();
            var missing = new List<string>();

            foreach (var level in o.Levels)
            {
                var gear = RollSurvivalGear(level);
                foreach (var buildName in o.Builds)
                {
                    var baseBuild = SurvivalBuilds.FirstOrDefault(x => x.Name == buildName);
                    if (baseBuild == null || baseBuild.Style != "melee")
                    {
                        summary.AppendLine($"  {buildName}: the dummy mode swings melee weapons; use a melee build.");
                        continue;
                    }
                    foreach (var item in items)
                    {
                        var probe = item.Make();
                        if (probe == null)
                        {
                            missing.Add($"{item.Guid:X8}{(item.Label != null ? $" ({item.Label})" : "")}");
                            continue;
                        }
                        var label = item.Label ?? "as is";
                        var skill = probe.WeaponSkill;
                        var build = BuildForSkill(baseBuild, skill);

                        foreach (var withTransfers in o.Transfers ? new[] { false, true } : new[] { false })
                        {
                            var totals = new DummyStats();
                            string transfersDone = "", spells = "", wieldText = "", wieldMet = "", charSkill = "", imbueText = "", tinksText = "";
                            int damage = 0;
                            double variance = 0;
                            for (var run = 0; run < o.Runs; run++)
                            {
                                var weapon = item.Make();
                                var buff = BuffLevelFor(level);
                                var owned = weapon.Biota.GetKnownSpellsIds(weapon.BiotaDatabaseLock).Select(id => (uint)id).ToList();
                                var free = Math.Max(0, weapon.MaxExtraSpellsCount - (weapon.ExtraSpellsCount ?? 0));
                                var planned = withTransfers ? DamageTransfers(weapon, owned, free) : new List<uint>();

                                // variants: the tinks left after the imbue as the best iron/granite split (BestIron), as TinkReal does
                                if (item.Label != null && !item.Label.EndsWith("untinked"))
                                {
                                    var tinks = Math.Max(0, MaxTinks - weapon.NumTimesTinkered);
                                    var iron = BestIron(weapon, BloodDrinkerBuff[buff] + (planned.Contains(LegendaryBloodThirst) || owned.Contains(LegendaryBloodThirst) ? 10 : 0), tinks);
                                    for (var i = 0; i < iron; i++)
                                        MutationCache.GetMutation(IronMutation).TryMutate(weapon);
                                    for (var i = 0; i < tinks - iron; i++)
                                        MutationCache.GetMutation(GraniteMutation).TryMutate(weapon);
                                    tinksText = $"{weapon.NumTimesTinkered} ({iron} iron, {tinks - iron} granite after the imbue)";
                                }
                                else
                                    tinksText = (weapon.NumTimesTinkered).ToString();
                                var added = withTransfers ? TransferReal(weapon, planned) : new List<uint>();
                                transfersDone = string.Join(" ", added.Select(id => new Spell(id).Name.Replace("Legendary ", "L.")));

                                var p = DressSurvivor(build, level, -1, gear, 0, out var loadout, null, weapon);
                                var dummy = MakeMonster(TrainingDummyWcid, false);
                                if (dummy == null)
                                    return "The Training Dummy (900809) is missing from the world database.";

                                if (run == 0)
                                {
                                    damage = weapon.Damage ?? 0;
                                    variance = weapon.DamageVariance ?? 0;
                                    imbueText = weapon.ImbuedEffect == ImbuedEffectType.Undef ? "" : weapon.ImbuedEffect.ToString();
                                    spells = string.Join(" ", weapon.Biota.GetKnownSpellsIds(weapon.BiotaDatabaseLock).Select(id => new Spell((uint)id)).Where(sp => !sp.NotFound).Select(sp => sp.Name));
                                    var cs = p.GetCreatureSkill(skill);
                                    charSkill = $"{skill} {cs.Current} (base {cs.Base})";
                                    var req = weapon.WieldRequirements;
                                    var diff = weapon.WieldDifficulty ?? 0;
                                    wieldText = req == WieldRequirement.Invalid ? "none" : $"{req} {(weapon.WieldSkillType != null ? ((Skill)weapon.WieldSkillType).ToString() + " " : "")}{diff}";
                                    wieldMet = req switch
                                    {
                                        WieldRequirement.RawSkill => p.GetCreatureSkill((Skill)(weapon.WieldSkillType ?? 0)).Base >= diff ? "yes" : "no (equipped anyway)",
                                        WieldRequirement.Skill => p.GetCreatureSkill((Skill)(weapon.WieldSkillType ?? 0)).Current >= diff ? "yes" : "no (equipped anyway)",
                                        WieldRequirement.Level => (p.Level ?? 0) >= diff ? "yes" : "no (equipped anyway)",
                                        WieldRequirement.Invalid => "yes",
                                        _ => "not checked (equipped anyway)",
                                    };
                                }

                                var s = SwingAtDummy(p, loadout.Weapon, dummy, o.Seconds);
                                totals.Damage += s.Damage; totals.Bleed += s.Bleed; totals.Strikes += s.Strikes; totals.Hits += s.Hits;
                                totals.Misses += s.Misses; totals.Crits += s.Crits; totals.Seconds += s.Seconds; totals.Swings += s.Swings;
                            }

                            var tests = o.Runs;
                            var dps = totals.Damage / totals.Seconds;
                            var perTest = totals.Damage / tests;
                            var attacksPerMinute = totals.Strikes / totals.Seconds * 60;
                            var avgHit = totals.Hits > 0 ? totals.Damage / totals.Hits : 0;
                            var critPct = totals.Hits > 0 ? totals.Crits * 100 / totals.Hits : 0;
                            var missPct = totals.Strikes > 0 ? totals.Misses * 100 / totals.Strikes : 0;
                            var row = withTransfers ? "transfers filled" : "as is";
                            var notes = string.Join("; ", new[] { item.Notes, wieldMet.StartsWith("no") ? "wield requirement not met, equipped anyway" : "",
                                withTransfers && transfersDone == "" ? "no free transfer slots" : "" }.Where(n => n != ""));
                            sb.AppendLine(string.Join(",", level, buildName, item.Guid == 0 ? "rolled T8" : $"0x{item.Guid:X8}", Csv(probe.Name), Csv(label), Csv(transfersDone), row, skill, Csv(charSkill), Csv(wieldText), Csv(wieldMet),
                                damage, F(variance, "0.###"), imbueText, Csv(tinksText), Csv(spells), F(o.Seconds, "0"), tests, F(perTest, "0"), F(dps, "0.0"), F(attacksPerMinute, "0.0"),
                                F(totals.Hits / tests, "0.#"), F(totals.Misses / tests, "0.#"), F(missPct, "0"), F(avgHit, "0.0"), F(totals.Crits / tests, "0.#"), F(critPct, "0"),
                                F(totals.Bleed / totals.Seconds, "0.0"), Csv(notes)));
                            summary.AppendLine($"  L{level} {buildName} {probe.Name} {label} {row}: {dps:0.0} DPS, {attacksPerMinute:0} attacks/min, avg hit {avgHit:0}, crits {critPct:0}%{(transfersDone != "" ? $" (+{transfersDone})" : "")}");
                        }
                    }
                }
            }

            var dir = Path.Combine("Logs", "acxbench");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"acxsurvival-dummy-{string.Join("_", o.Levels)}-{started:yyyyMMdd-HHmmss}.csv");
            File.WriteAllText(file, sb.ToString());
            var head = $"Dummy bench ({(o.Veteran ? "maxed veteran" : "fresh")} character) done in {(DateTime.Now - started).TotalSeconds:0}s, written to {file}. Weapons equipped regardless of wield requirement; the character's weapon skill follows each item's.";
            if (o.Variants)
                head += partizan == null ? "\nThe Partizan base couldn't be rolled." : $"\nVariant base: the median of 41 tier 8 loot Partizans (wcid {PartizanWcid}), damage {partizan.Damage}, variance {partizan.DamageVariance:0.###}, speed {partizan.WeaponTime}, workmanship {partizan.ItemWorkmanship}, wield {partizan.WieldDifficulty}, attack bonus {partizan.WeaponOffense ?? 1:0.###}, untinked, no built-in.";
            if (missing.Count > 0)
                head += $"\nNot found in the shard DB (unsaved, or another guid): {string.Join(", ", missing.Distinct())}";
            return head + "\n" + summary.ToString().TrimEnd();
        }
    }
}
