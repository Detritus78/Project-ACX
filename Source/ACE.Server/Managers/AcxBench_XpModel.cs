using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Physics.Animation;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: XP model (JJ 2026-11-13, for tuning progression: level 126 in about 6 months at 2 hours a night, 5 nights a
    /// week). For each character level it fights the monsters that really spawn a few levels above it (JJ: players hunt
    /// a little over their level, with the best gear they can find and good buffs; generator tables and landblock
    /// placements, weighted by spawn spots): Str/Coord/Quick 100 plus the level's attribute buffs, the best of 20 loot
    /// rolls of the level's tier Heavy Long Sword in each element (the best one per monster, as players swap), Blood
    /// Drinker and Swift Killer for the level, full power, every attack hitting. The real monster's health, armor and
    /// resistances go through DamageEvent. Out: time to kill and XP per hour of fighting, per
    /// monster and per level, to Logs/acxbench/acxxpmodel-*.csv. Travel, looting, buffing and misses are left to the
    /// progression sheet, which turns this into hours to level. /acxxpmodel in ACXBenchCommands.cs.
    /// </summary>
    public static partial class AcxBench
    {
        private const uint LongSwordWcid = 351;
        private const uint MonsterGuid = 0xFFFFFFF4;

        // buffs a well-buffed player has at a level: spell level III..VIII, and what each gives
        private static int BuffLevelFor(int level) => level < 15 ? 3 : level < 30 ? 4 : level < 50 ? 5 : level < 80 ? 6 : level < 150 ? 7 : 8;
        private static readonly int[] AttributeBuff = { 0, 10, 15, 20, 25, 30, 35, 40, 45 };      // Strength/Coordination/Quickness Self
        private static readonly int[] BloodDrinkerBuff = { 0, 2, 4, 8, 12, 16, 20, 22, 24 };      // Aura of Blood Drinker
        private static readonly int[] SwiftKillerBuff = { 0, 10, 20, 30, 40, 50, 60, 70, 80 };    // Aura of Swift Killer (speed down)

        // a spell of each buff's line (level VI), for its spell category
        private const uint BloodDrinkerSpell = 1616, SwiftKillerSpell = 1627;
        private static SpellCategory BuffCategory(uint spellId) => new ACE.Server.Entity.Spell(spellId).Category;
        private static Dictionary<SpellCategory, PropertyAttribute> attributeSpells;
        private static Dictionary<SpellCategory, PropertyAttribute> AttributeSpells => attributeSpells ??= new Dictionary<SpellCategory, PropertyAttribute>()
        {
            { BuffCategory(1332), PropertyAttribute.Strength },       // Strength Self VI
            { BuffCategory(1378), PropertyAttribute.Coordination },   // Coordination Self VI
            { BuffCategory(1402), PropertyAttribute.Quickness },      // Quickness Self VI
        };

        // loot tier a player's weapon comes from at a level (the Foundry level buttons, one per tier)
        private static int TierFor(int level)
        {
            var tier = 1;
            for (var t = 0; t < TierLevels.Length; t++)
                if (level >= TierLevels[t])
                    tier = t + 1;
            return tier;
        }

        /// <summary>
        /// What-if gear on the model's sword (JJ 2026-11-15, an overkill check: "I am trying to avoid a overkill situation
        /// where someone at thier proper level hunting their proper area is one hitting everything"): a rend or cleave
        /// value (applied as fixed cleaving of the sword's own element, which the server combines with rending as the
        /// larger of the two), a slayer against each monster fought, Biting Strike / Crushing Blow, and extra damage
        /// (Legendary Blood Thirst is +10). Out, beside time to kill: the average hit and hits to kill.
        /// </summary>
        public class XpSetup
        {
            public double Rend = 1.0, Slayer = 1.0, CritChance, CritMultiplier;
            public int Damage;

            // a given weapon instead of the loot Long Swords (quest and named weapons: JJ "lets include a atlan type
            // weapon ... the singularity wepons at different levels of upgrades and finally the end game weapons"), and
            // the weapon's own spells cast on it as a wielded item's are (cantrips such as Legendary Blood Thirst)
            public uint Wcid;
            public bool Spells;

            public string Tag => (Wcid != 0 ? $"-w{Wcid}" : "") + (Spells ? "-spells" : "") + (Rend > 1 ? $"-rend{Rend}" : "") + (Slayer > 1 ? $"-slayer{Slayer}" : "")
                + (CritChance > 0 ? $"-crit{CritChance}" : "") + (CritMultiplier > 0 ? $"-critx{CritMultiplier}" : "") + (Damage > 0 ? $"-dmg{Damage}" : "");
        }

        public static string StartXpModel(int fromLevel, int toLevel, int step, Action<string> reply) => StartXpModel(fromLevel, toLevel, step, new XpSetup(), reply);

        public static string StartXpModel(int fromLevel, int toLevel, int step, XpSetup setup, Action<string> reply)
        {
            if (running)
                return "A bench is already running.";
            running = true;

            Task.Run(() =>
            {
                try
                {
                    reply(RunXpModel(fromLevel, toLevel, step, setup));
                }
                catch (Exception e)
                {
                    log.Error($"AcxBench XP model failed: {e}");
                    reply($"The XP model failed: {e.Message}");
                }
                finally
                {
                    running = false;
                }
            });
            return $"XP model started: levels {fromLevel}-{toLevel} every {step}. Results go to Logs/acxbench/.";
        }

        private class MonsterInfo
        {
            public uint Wcid;
            public string Name;
            public int Level;
            public long Xp;
            public int Spots;
        }

        private static string RunXpModel(int fromLevel, int toLevel, int step, XpSetup setup)
        {
            var started = DateTime.Now;
            var monsters = SpawnedMonsters();
            countMisses = false;

            var scale = PropertyManager.GetDouble("acx_bleed_damage_scale").Item;

            var sb = new StringBuilder();
            sb.AppendLine("PlayerLevel,Tier,BloodDrinker,MonsterWcid,Monster,MonsterLevel,Spots,Health,XpPerKill,DPS,SecondsToKill,XpPerFightingHour,AvgHit,HitsToKill");
            var summary = new StringBuilder();

            for (var level = fromLevel; level <= toLevel; level += step)
            {
                var buff = BuffLevelFor(level);
                // gear comes from what the monsters drop: the tier of the hunting level
                var tier = TierFor(level);
                var bloodDrinker = BloodDrinkerBuff[buff];
                var attacker = CreateAttacker(level, new Options { Str = 100 + AttributeBuff[buff], Coord = 100 + AttributeBuff[buff], Quick = 100 + AttributeBuff[buff] });
                foreach (var skill in new[] { Skill.HeavyWeapons, Skill.LightWeapons, Skill.FinesseWeapons, Skill.TwoHandedCombat })
                    attacker.GetCreatureSkill(skill).InitLevel = 1000;   // every attack hits; misses are a progression-sheet setting

                // best of 20 loot rolls of the level's Long Sword, in each element (or the given weapon as it comes)
                var weapons = new List<WorldObject>();
                var weaponSpells = new Dictionary<WorldObject, List<ACE.Server.Entity.Spell>>();
                var lootSwords = ((Factories.Entity.ChanceTable<Factories.Enum.WeenieClassName>)typeof(Factories.Tables.Wcids.HeavyWeaponWcids)
                    .GetField("LongSwords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null)).Select(e => (uint)e.result);
                foreach (var wcid in setup.Wcid != 0 ? new[] { setup.Wcid } : lootSwords)
                {
                    var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
                    WorldObject best = null;
                    for (var i = 0; i < (setup.Wcid != 0 ? 1 : 20); i++)
                    {
                        var weapon = WorldObjectFactory.CreateWorldObject(weenie, new ObjectGuid(WeaponGuid));
                        if (setup.Wcid == 0)
                            LootGenerationFactory.MutateItem(weapon, new TreasureDeath { Tier = tier, LootQualityMod = 0 }, false);
                        if (best == null || (weapon.Damage ?? 0) * (1 - (weapon.DamageVariance ?? 0) / 2) > (best.Damage ?? 0) * (1 - (best.DamageVariance ?? 0) / 2))
                            best = weapon;
                    }
                    best.Damage = (best.Damage ?? 0) + bloodDrinker + setup.Damage;
                    best.WeaponTime = Math.Max(0, (best.WeaponTime ?? 0) - SwiftKillerBuff[buff]);
                    if (setup.Rend > 1)
                    {
                        best.ResistanceModifierType = best.W_DamageType;
                        best.ResistanceModifier = setup.Rend - 1;
                    }
                    if (setup.CritChance > 0)
                        best.CriticalFrequency = setup.CritChance;
                    if (setup.CritMultiplier > 0)
                        best.SetProperty(PropertyFloat.CriticalMultiplier, setup.CritMultiplier);
                    weapons.Add(best);
                    weaponSpells[best] = setup.Spells ? ResolveWeaponSpells(best, best.Biota.GetKnownSpellsIds(best.BiotaDatabaseLock).Select(id => (uint)id), attacker, buff)
                        : new List<ACE.Server.Entity.Spell>();
                }

                // a few levels under the monsters (monster levels bunch up, e.g. at 80, 100, 110, so the window reaches 15% / 8 levels)
                var low = level + 1;
                var high = level + Math.Max(8, (int)Math.Ceiling(level * 0.15));
                var rows = new List<(MonsterInfo Monster, double Health, double Dps, double Xph, double Hits)>();

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
                    creature.Location = new Position(BenchCell, 50.0f, 51.0f, 0.0f, 0, 0, 1, 0);
                    var health = (double)creature.Health.MaxValue;
                    if (health <= 0)
                        continue;

                    // the element that works best on this monster
                    var dps = 0.0;
                    var avgHit = 0.0;
                    foreach (var weapon in weapons)
                    {
                        if (setup.Slayer > 1)
                        {
                            weapon.SlayerCreatureType = creature.CreatureType;
                            weapon.SlayerDamageBonus = setup.Slayer;
                        }
                        if (!attacker.TryEquipObject(weapon, weapon.ValidLocations ?? EquipMask.MeleeWeapon))
                            continue;
                        foreach (var spell in weaponSpells[weapon])
                        {
                            // added straight to the target CreateItemSpell picks: its "You cast" line needs a session
                            var onWeapon = spell.School == MagicSchool.ItemEnchantment && !spell.HasItemCategory && !spell.IsPortalSpell;
                            (onWeapon ? weapon : attacker).EnchantmentManager.Add(spell, weapon, weapon, true);
                        }
                        var sums = new Sums();
                        BenchSwings(attacker, weapon, creature, sums, 1.0f, 30, scale);
                        attacker.TryDequipObject(weapon.Guid, out _, out _);
                        if (sums.Time > 0 && (sums.Direct + sums.Bleed) / sums.Time > dps)
                        {
                            dps = (sums.Direct + sums.Bleed) / sums.Time;
                            avgHit = sums.Swings > 0 ? sums.Direct / sums.Swings : 0;   // per attack, as a player counts hits
                        }
                    }
                    if (dps <= 0)
                        continue;

                    var seconds = health / dps;
                    var xph = monster.Xp / seconds * 3600;
                    var hits = avgHit > 0 ? health / avgHit : 0;
                    rows.Add((monster, health, dps, xph, hits));
                    sb.AppendLine(string.Join(",", level, tier, bloodDrinker, monster.Wcid, Csv(monster.Name), monster.Level, monster.Spots, F(health, "0"), monster.Xp, F(dps, "0.#"), F(seconds, "0.#"), F(xph, "0"), F(avgHit, "0.#"), F(hits, "0.##")));
                }

                if (rows.Count > 0)
                {
                    // spawn-weighted median, and the top quarter (players pick good spots)
                    var weighted = rows.OrderBy(r => r.Xph).ToList();
                    var total = weighted.Sum(r => r.Monster.Spots);
                    double Percentile(double p)
                    {
                        var target = total * p;
                        var acc = 0.0;
                        foreach (var r in weighted)
                        {
                            acc += r.Monster.Spots;
                            if (acc >= target)
                                return r.Xph;
                        }
                        return weighted.Last().Xph;
                    }
                    // overkill: the spawn-weighted share of monsters that die to one or two average hits
                    var oneHit = rows.Where(r => r.Hits <= 1).Sum(r => r.Monster.Spots) * 100.0 / total;
                    var twoHits = rows.Where(r => r.Hits <= 2).Sum(r => r.Monster.Spots) * 100.0 / total;
                    var byHits = rows.OrderBy(r => r.Hits).ToList();
                    var medianHits = byHits.First().Hits;
                    var acc = 0.0;
                    foreach (var r in byHits)
                    {
                        acc += r.Monster.Spots;
                        if (acc >= total / 2.0) { medianHits = r.Hits; break; }
                    }
                    summary.AppendLine($"L{level} (T{tier}, buffs {buff}): {rows.Count} monster kinds, XP/fighting hour median {Percentile(0.5):N0}, top quarter {Percentile(0.75):N0}; hits to kill median {medianHits:0.#}, one hit {oneHit:0}%, two hits {twoHits:0}%");
                }
            }

            var dir = Path.Combine("Logs", "acxbench");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"acxxpmodel-{fromLevel}-{toLevel}{setup.Tag}-{started:yyyyMMdd-HHmmss}.csv");
            File.WriteAllText(file, sb.ToString());
            return $"XP model done in {(DateTime.Now - started).TotalSeconds:0}s: {monsters.Count} spawned monster kinds, written to {file}.\n" + summary.ToString().TrimEnd();
        }

        /// <summary>
        /// A wielded weapon's spells, on a weapon that already has the level's buffs: a Blood Drinker, Swift Killer or
        /// Strength/Coordination/Quickness one shares its category with the buff and only the stronger counts in game, so it
        /// adds just what it has over the buff (to the weapon or the attacker); the rest (cantrips and the like) come back to
        /// be cast in the fight loop, after equipping
        /// </summary>
        private static List<ACE.Server.Entity.Spell> ResolveWeaponSpells(WorldObject weapon, IEnumerable<uint> spellIds, Player attacker, int buff)
        {
            var spells = new List<ACE.Server.Entity.Spell>();
            foreach (var spellId in spellIds)
            {
                var spell = new ACE.Server.Entity.Spell(spellId);
                if (spell.NotFound)
                    continue;
                if (spell.Category == BuffCategory(BloodDrinkerSpell))
                    weapon.Damage += Math.Max(0, (int)spell.StatModVal - BloodDrinkerBuff[buff]);
                else if (spell.Category == BuffCategory(SwiftKillerSpell))
                    weapon.WeaponTime = Math.Max(0, weapon.WeaponTime.Value - Math.Max(0, -(int)spell.StatModVal - SwiftKillerBuff[buff]));
                else if (AttributeSpells.TryGetValue(spell.Category, out var attribute))
                    attacker.Attributes[attribute].StartingValue += (uint)Math.Max(0, (int)spell.StatModVal - AttributeBuff[buff]);
                else
                    spells.Add(spell);
            }
            return spells;
        }

        /// <summary>
        /// Every attackable monster that really spawns (placed directly, or by a placed generator) with its spawn spots
        /// </summary>
        private static List<MonsterInfo> SpawnedMonsters()
        {
            var spots = new Dictionary<uint, int>();
            using (var ctx = new WorldDbContext())
            {
                var placed = ctx.LandblockInstance.GroupBy(i => i.WeenieClassId).Select(g => new { Wcid = g.Key, Count = g.Count() }).ToDictionary(x => x.Wcid, x => x.Count);
                foreach (var (wcid, count) in placed)
                    spots[wcid] = spots.GetValueOrDefault(wcid) + count;
                foreach (var row in ctx.WeeniePropertiesGenerator.Select(g => new { g.ObjectId, g.WeenieClassId }).ToList())
                    if (placed.TryGetValue(row.ObjectId, out var count))
                        spots[row.WeenieClassId] = spots.GetValueOrDefault(row.WeenieClassId) + count;
            }

            var monsters = new List<MonsterInfo>();
            foreach (var (wcid, count) in spots)
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
                if (weenie == null || weenie.WeenieType != WeenieType.Creature)
                    continue;
                var xp = weenie.GetProperty(PropertyInt.XpOverride) ?? 0;
                var level = weenie.GetProperty(PropertyInt.Level) ?? 0;
                if (xp <= 0 || level <= 0 || weenie.GetProperty(PropertyBool.Attackable) == false)
                    continue;
                monsters.Add(new MonsterInfo { Wcid = wcid, Name = weenie.GetProperty(PropertyString.Name), Level = level, Xp = xp, Spots = count });
            }
            return monsters;
        }

        /// <summary>
        /// The spawn-weighted median melee and missile defense of the monsters a player of this level hunts (a little above it)
        /// </summary>
        private static (int Melee, int Missile) MedianMonsterDefense(List<MonsterInfo> monsters, int level)
        {
            var rows = new List<(int Melee, int Missile, int Spots)>();
            foreach (var monster in monsters.Where(m => m.Level >= level && m.Level <= level + Math.Max(8, (int)Math.Ceiling(level * 0.15))))
            {
                if (WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(monster.Wcid), new ObjectGuid(MonsterGuid)) is not Creature creature)
                    continue;
                rows.Add(((int)creature.GetCreatureSkill(Skill.MeleeDefense).Current, (int)creature.GetCreatureSkill(Skill.MissileDefense).Current, monster.Spots));
            }
            if (rows.Count == 0)
                return (0, 0);
            int Median(Func<(int Melee, int Missile, int Spots), int> pick)
            {
                var sorted = rows.OrderBy(pick).ToList();
                var half = sorted.Sum(r => r.Spots) / 2.0;
                var acc = 0;
                foreach (var r in sorted)
                    if ((acc += r.Spots) >= half)
                        return pick(r);
                return pick(sorted.Last());
            }
            return (Median(r => r.Melee), Median(r => r.Missile));
        }

        /// <summary>
        /// Full-power melee swings at medium height, as BenchMelee (swing animation + bar refill, strikes, bleed)
        /// </summary>
        private static void BenchSwings(Player attacker, WorldObject weapon, Creature target, Sums sums, float power, int swings, double bleedScale)
        {
            var stance = attacker.GetCombatStance();
            attacker.CurrentMotionState = new Motion(stance);
            attacker.PowerLevel = power;
            attacker.AttackHeight = AttackHeight.Medium;
            attacker.PrevMotionCommand = MotionCommand.Invalid;
            var bleeds = weapon.W_WeaponType == WeaponType.Dagger || (weapon.W_WeaponType == WeaponType.Sword && weapon.W_AttackType.IsMultiStrike());

            for (var swing = 0; swing < swings; swing++)
            {
                var animSpeed = attacker.GetAnimSpeed() * attacker.GetAcxSingleWeaponAnimSpeedMod();
                var motion = attacker.GetSwingAnimation();
                var animLength = MotionTable.GetAnimationLength(attacker.MotionTableId, stance, motion, animSpeed);
                if (animLength <= 0)
                    continue;
                var frames = MotionTable.GetAttackFrames(attacker.MotionTableId, stance, motion);
                var strikes = attacker.GetNumStrikes(attacker.GetWeaponAttackType(weapon));
                if (strikes != frames.Count)
                    strikes = frames.Count;

                var swingDamage = 0.0f;
                for (var s = 0; s < strikes; s++)
                {
                    var damageEvent = Hit(attacker, target, weapon, sums);
                    if (damageEvent != null)
                        swingDamage += damageEvent.Damage;
                }
                if (bleeds && swingDamage > 0)
                    sums.Bleed += 0.9 * Math.Round((5 + (int)Math.Round(swingDamage)) * bleedScale);
                sums.Direct += swingDamage;
                sums.Time += animLength + power * attacker.GetAcxSingleWeaponRefillMod();
                sums.Swings++;
            }
        }
    }
}
