using System;
using System.Linq;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;

using Spell = ACE.Server.Entity.Spell;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: an elite's mods (ClassicACE). Disco, Meteor, Nova and Support attack; Beefy, Warded and Tele defend.
    /// </summary>
    [Flags]
    public enum AcxEliteMod
    {
        None    = 0,
        Disco   = 0x01,   // chains acid arcs at its target
        Meteor  = 0x02,   // drops strike spells on its target (outdoors only; indoors it rolls Disco)
        Nova    = 0x04,   // casts ring spells
        Support = 0x08,   // debuffs its target
        Beefy   = 0x10,   // more health
        Warded  = 0x20,   // immune to melee, missile or magic, switching every few seconds
        Tele    = 0x40,   // blinks to its target
    }

    /// <summary>
    /// Project ACX: elites, ported from ClassicACE's Creature_Tick (2026-11-13) for the town invasions (Managers/AcxInvasions.cs).
    /// No random elites: only AcxInvasions makes them. Differences from ClassicACE: Warded really blocks the attack kind it
    /// shows (ClassicACE only announced it), with a coloured shield instead of a chat line per switch; Tele blinks with
    /// FakeTeleport (ClassicACE only rewrote the stored position); Split, Mirror and the Rare mod stay off as in ClassicACE;
    /// health multiplies the creature's whole health, so creatures whose health comes only from Endurance get it too;
    /// Beefy under level 100 gets x21 (ClassicACE's x7 left it weaker than a plain elite's x12); no coin drop.
    /// </summary>
    partial class Creature
    {
        public AcxEliteMod AcxEliteMods
        {
            get => (AcxEliteMod)(GetProperty(PropertyInt.AcxEliteMods) ?? 0);
            set { if (value == AcxEliteMod.None) RemoveProperty(PropertyInt.AcxEliteMods); else SetProperty(PropertyInt.AcxEliteMods, (int)value); }
        }

        public bool IsAcxElite => AcxEliteMods != AcxEliteMod.None;

        /// <summary>
        /// An elite's own damage multiplier on top of acx_elite_damage_mod, melee, missile and spells: its invaded town's
        /// damageScale (AcxInvasions.ApplyTownStrength, towns.json)
        /// </summary>
        public double AcxEliteDamageScale
        {
            get => GetProperty(PropertyFloat.AcxEliteDamageScale) ?? 1.0;
            set { if (value == 1.0) RemoveProperty(PropertyFloat.AcxEliteDamageScale); else SetProperty(PropertyFloat.AcxEliteDamageScale, value); }
        }

        /// <summary>
        /// The loot tier of the boss table an elite without its own death treasure drops from (the invaded town's tier)
        /// </summary>
        public int AcxEliteLootTier = 1;

        private const AcxEliteMod AcxEliteAttackMods = AcxEliteMod.Disco | AcxEliteMod.Meteor | AcxEliteMod.Nova | AcxEliteMod.Support;
        private const AcxEliteMod AcxEliteDefenceMods = AcxEliteMod.Beefy | AcxEliteMod.Warded | AcxEliteMod.Tele;

        /// <summary>
        /// The kind of attack a Warded elite is immune to right now: 0 none, 1 melee, 2 missile, 3 magic
        /// </summary>
        private int AcxEliteWard
        {
            get => GetProperty(PropertyInt.AcxEliteWard) ?? 0;
            set { if (value == 0) RemoveProperty(PropertyInt.AcxEliteWard); else SetProperty(PropertyInt.AcxEliteWard, value); }
        }

        private static readonly string[] AcxWardNames = { null, "melee", "missile", "magic" };
        private static readonly PlayScript[] AcxWardEffects = { PlayScript.Invalid, PlayScript.ShieldUpRed, PlayScript.ShieldUpYellow, PlayScript.ShieldUpBlue };

        /// <summary>
        /// Upgrades this creature to an elite. Call before it enters the world.
        /// </summary>
        public void AcxMakeElite()
        {
            var outdoors = Location == null || !Location.Indoors;

            // resistances: every element rerolled to 1-60% of normal damage; at least one strong (25% or less), shown as [+x]
            var resists = new (string Tag, Action<double> Set, Func<double?> Get)[]
            {
                ("a", v => ResistAcid = v, () => ResistAcid), ("f", v => ResistFire = v, () => ResistFire),
                ("c", v => ResistCold = v, () => ResistCold), ("e", v => ResistElectric = v, () => ResistElectric),
                ("p", v => ResistPierce = v, () => ResistPierce), ("b", v => ResistBludgeon = v, () => ResistBludgeon),
                ("s", v => ResistSlash = v, () => ResistSlash), ("n", v => ResistNether = v, () => ResistNether),
            };
            foreach (var r in resists)
                r.Set(Math.Max(0.01, ThreadSafeRandom.Next(0.0f, 0.6f)));
            if (!resists.Any(r => r.Get() <= 0.25))
                resists[ThreadSafeRandom.Next(0, resists.Length - 1)].Set(0.25);

            // armor against each damage type, where the creature has one
            ArmorModVsSlash += 1.5;
            ArmorModVsPierce += 1.5;
            ArmorModVsBludgeon += 1.5;
            ArmorModVsCold += 1.5;
            ArmorModVsFire += 1.5;
            ArmorModVsAcid += 1.5;
            ArmorModVsElectric += 1.5;

            RadarColor = ACE.Entity.Enum.RadarColor.Green;
            // Project ACX: scaled by acx_elite_regen_scale (JJ 2026-11-16: "lets drop the regen by 50%"; duos at the elite's level
            // outlasted it with kits but could never out-damage its regeneration, /acxsurvival duo)
            HealthRate = (NonProjectileMagicImmune ? 50 : 100) * PropertyManager.GetDouble("acx_elite_regen_scale").Item;
            ManaRate = 1000;
            StaminaRate = 1000;

            var high = (Level ?? 1) > 99;

            // Project ACX: every attribute up by a rolled share of itself, acx_elite_attribute_bonus_min..max (JJ 2026-11-16,
            // after /acxsurvival duo: ClassicACE's flat +25-75 / +70-200 roughly tripled a level-15 drudge, so two players of
            // the town's level died in seconds at tier 1, while it was a modest bump on a level-250 monster)
            var bonusMin = PropertyManager.GetDouble("acx_elite_attribute_bonus_min").Item;
            var bonusMax = Math.Max(bonusMin, PropertyManager.GetDouble("acx_elite_attribute_bonus_max").Item);
            foreach (var attribute in new[] { Strength, Endurance, Quickness, Coordination, Focus, Self })
                attribute.StartingValue += (uint)Math.Round(attribute.Base * (bonusMin + ThreadSafeRandom.Next(0.0f, 1.0f) * (bonusMax - bonusMin)));

            var mods = AcxRollEliteMods(outdoors);
            AcxEliteMods = mods;
            var modCount = Enum.GetValues<AcxEliteMod>().Count(m => m != AcxEliteMod.None && mods.HasFlag(m));

            var tags = string.Concat(resists.Where(r => r.Get() <= 0.25).Select(r => $"[+{r.Tag}]"));
            Name = $"Elite {Name} {tags}".TrimEnd();
            SetProperty(PropertyString.Template, string.Join(", ", Enum.GetValues<AcxEliteMod>().Where(m => m != AcxEliteMod.None && mods.HasFlag(m))));

            if ((mods & (AcxEliteMod.Disco | AcxEliteMod.Meteor | AcxEliteMod.Nova)) != 0)
            {
                var war = GetCreatureSkill(Skill.WarMagic);
                war.InitLevel += war.InitLevel > 100 && war.InitLevel < 200 ? 30u : 45u;
            }
            if (mods.HasFlag(AcxEliteMod.Support))
            {
                foreach (var skill in new[] { Skill.LifeMagic, Skill.CreatureEnchantment })
                {
                    var magic = GetCreatureSkill(skill);
                    magic.InitLevel += magic.InitLevel > 100 && magic.InitLevel < 200 ? 30u : 50u;
                }
            }

            // players can always assess it
            var deception = GetCreatureSkill(Skill.Deception, false);
            if (deception != null)
                deception.InitLevel = 0;

            XpOverride = (int)Math.Min(int.MaxValue, PropertyManager.GetLong(high ? "acx_elite_xp_per_mod_high" : "acx_elite_xp_per_mod").Item * modCount);

            // Project ACX: scaled by acx_elite_health_scale (JJ 2026-11-16: duos of the town's level outlasted high-tier elites
            // for minutes but could never finish 50,000-160,000 health)
            var multiplier = mods.HasFlag(AcxEliteMod.Beefy) ? (high ? (Health.MaxValue > 100000 ? 15 : 30) : 21) : (high ? 17 : 12);
            var scaledMultiplier = Math.Max(1.0, multiplier * PropertyManager.GetDouble("acx_elite_health_scale").Item);
            var health = Health.MaxValue;
            Health.StartingValue += (uint)Math.Min(uint.MaxValue - Health.StartingValue, (long)Math.Round(health * (scaledMultiplier - 1)));
            Health.Current = Health.MaxValue;
            Stamina.Current = Stamina.MaxValue;
            Mana.Current = Mana.MaxValue;

            CanGenerateRare = true;

            Level = ((Level ?? 1) + 15) * 2;
        }

        /// <summary>
        /// ClassicACE's mod roll: each mod has its own chance against one roll, up to 5, then at least one attack and one defence
        /// </summary>
        private static AcxEliteMod AcxRollEliteMods(bool outdoors)
        {
            var roll = ThreadSafeRandom.Next(1, 1000);
            var mods = AcxEliteMod.None;
            var count = 0;

            void Try(int chanceOutOf, AcxEliteMod mod)
            {
                if (count < 5 && roll <= ThreadSafeRandom.Next(1, chanceOutOf))
                {
                    mods |= mod;
                    count++;
                }
            }

            Try(650, AcxEliteMod.Disco);
            Try(450, AcxEliteMod.Beefy);
            Try(500, outdoors ? AcxEliteMod.Meteor : AcxEliteMod.Disco);
            Try(400, AcxEliteMod.Warded);
            Try(350, AcxEliteMod.Tele);
            Try(500, AcxEliteMod.Nova);
            Try(410, AcxEliteMod.Support);

            if ((mods & AcxEliteAttackMods) == 0)
            {
                mods |= ThreadSafeRandom.Next(1, 4) switch
                {
                    1 => AcxEliteMod.Disco,
                    2 => outdoors ? AcxEliteMod.Meteor : AcxEliteMod.Disco,
                    3 => AcxEliteMod.Nova,
                    _ => AcxEliteMod.Support,
                };
            }
            if ((mods & AcxEliteDefenceMods) == 0)
            {
                mods |= ThreadSafeRandom.Next(1, 4) switch
                {
                    3 => AcxEliteMod.Warded,
                    4 => AcxEliteMod.Tele,
                    _ => AcxEliteMod.Beefy,   // ClassicACE's 1 was Split (off), which falls back to Beefy
                };
            }
            return mods;
        }

        /// <summary>
        /// Every ~5 seconds while awake and fighting a player within 96 m: the elite's attack mods fire, a Warded elite may switch
        /// what it is immune to, and a Tele elite blinks to its target
        /// </summary>
        private void AcxEliteHeartbeat()
        {
            if (IsDead)
                return;

            AcxInvasions.OnInvaderHeartbeat(this);

            if (!IsAwake || AttackTarget is not Player target || target.Location == null || Location.DistanceTo(target.Location) > 96)
                return;

            var mods = AcxEliteMods;

            if (mods.HasFlag(AcxEliteMod.Warded))
                AcxSwitchWard();

            if (mods.HasFlag(AcxEliteMod.Disco) && ThreadSafeRandom.Next(1, 10) <= 7)
            {
                var spell = new Spell(Level > 99 ? SpellId.AcidArc5 : SpellId.AcidArc2);
                var chain = new ActionChain();
                for (var i = 0; i < 2; i++)
                {
                    chain.AddDelaySeconds(2.5f);
                    chain.AddAction(this, () =>
                    {
                        if (!IsDead && AttackTarget == target)
                            CreateSpellProjectiles(spell, target, this);
                    });
                }
                chain.EnqueueChain();
            }

            if (mods.HasFlag(AcxEliteMod.Meteor) && ThreadSafeRandom.Next(1, 10) <= 5)
                CreateSpellProjectiles(new Spell(AcxMeteorSpells[ThreadSafeRandom.Next(0, AcxMeteorSpells.Length - 1)]), target, this);

            if (mods.HasFlag(AcxEliteMod.Nova) && ThreadSafeRandom.Next(1, 10) <= 5)
            {
                var spell = new Spell(AcxNovaSpells[ThreadSafeRandom.Next(0, AcxNovaSpells.Length - 1)]);
                if (!spell.NotFound)
                    CreateSpellProjectiles(spell, target, this);
            }

            if (mods.HasFlag(AcxEliteMod.Support) && ThreadSafeRandom.Next(1, 10) <= 5)
            {
                var debuffs = Level > 99 ? AcxSupportSpellsHigh : AcxSupportSpellsLow;
                CastSpell(new Spell(debuffs[ThreadSafeRandom.Next(0, debuffs.Length - 1)]));
            }

            if (mods.HasFlag(AcxEliteMod.Tele) && Location.Landblock == target.Location.Landblock && Location.DistanceTo(target.Location) > 3)
                FakeTeleport(target.Location);
        }

        private void AcxSwitchWard()
        {
            var ward = AcxEliteWard;
            if (ward == 0)
                ward = ThreadSafeRandom.Next(1, 3);
            else if (ThreadSafeRandom.Next(1, 100) >= 50)
                ward = (ward + ThreadSafeRandom.Next(0, 1)) % 3 + 1;   // one of the other two
            else
                return;

            AcxEliteWard = ward;
            EnqueueBroadcast(new Network.GameMessages.Messages.GameMessageScript(Guid, AcxWardEffects[ward]));
        }

        /// <summary>
        /// True if this is a Warded elite currently immune to this kind of attack
        /// </summary>
        public bool AcxEliteWardBlocks(CombatType combatType)
        {
            var ward = AcxEliteWard;
            return ward != 0 && combatType == ward switch { 1 => CombatType.Melee, 2 => CombatType.Missile, _ => CombatType.Magic };
        }

        /// <summary>
        /// For assess: the elite's mods and what its ward blocks now
        /// </summary>
        public string AcxEliteDescription()
        {
            var ward = AcxEliteWard;
            return $"Elite mods: {GetProperty(PropertyString.Template)}." + (ward != 0 ? $" Its ward blocks {AcxWardNames[ward]} attacks right now." : "");
        }

        /// <summary>
        /// The elite's loot: its own death treasure (or a boss table of the given tier if it has none) with
        /// acx_elite_loot_count items at acx_elite_loot_quality (ClassicACE's elite_mob_loot_*)
        /// </summary>
        public TreasureDeath AcxEliteDeathTreasure(int fallbackTier)
        {
            var source = DeathTreasure ?? DatabaseManager.World.GetCachedDeathTreasure(AcxBossTreasure[Math.Clamp(fallbackTier, 1, AcxBossTreasure.Length - 1)]);
            if (source == null)
                return null;

            var count = (int)PropertyManager.GetLong("acx_elite_loot_count").Item;
            return new TreasureDeath
            {
                Id = source.Id,
                TreasureType = source.TreasureType,
                Tier = source.Tier,
                LootQualityMod = (float)PropertyManager.GetDouble("acx_elite_loot_quality").Item,
                UnknownChances = source.UnknownChances,
                ItemChance = 100,
                ItemMinAmount = count,
                ItemMaxAmount = count,
                ItemTreasureTypeSelectionChances = source.ItemTreasureTypeSelectionChances,
                MagicItemChance = source.MagicItemChance,
                MagicItemMinAmount = source.MagicItemMinAmount,
                MagicItemMaxAmount = source.MagicItemMaxAmount,
                MagicItemTreasureTypeSelectionChances = source.MagicItemTreasureTypeSelectionChances,
                MundaneItemChance = source.MundaneItemChance,
                MundaneItemMinAmount = source.MundaneItemMinAmount,
                MundaneItemMaxAmount = source.MundaneItemMaxAmount,
                MundaneItemTypeSelectionChances = source.MundaneItemTypeSelectionChances,
                LastModified = source.LastModified,
            };
        }

        // retail's boss loot tables (5-10 items, quality 0.25) by tier 1-6
        private static readonly uint[] AcxBossTreasure = { 0, 36, 34, 33, 31, 35, 32 };

        private static readonly SpellId[] AcxMeteorSpells = { SpellId.FrostStrike, SpellId.FlameStrike, SpellId.LightningStrike, SpellId.BladeStrike, SpellId.ForceStrike };

        private static readonly SpellId[] AcxNovaSpells = { SpellId.NetherRing, SpellId.AcidRing, SpellId.LightningRingLarge, SpellId.FrostRing, SpellId.FlameRing, SpellId.ForceRing, SpellId.BladeRing, SpellId.ShockwaveRing };

        private static readonly SpellId[] AcxSupportSpellsHigh =
        {
            SpellId.VulnerabilityOther5, SpellId.LightningVulnerabilityOther5, SpellId.ColdVulnerabilityOther5, SpellId.FireVulnerabilityOther5,
            SpellId.AcidVulnerabilityOther5, SpellId.PiercingVulnerabilityOther5, SpellId.BludgeonVulnerabilityOther5, SpellId.BladeVulnerabilityOther5,
            SpellId.ImperilOther5, SpellId.MagicYieldOther5, SpellId.SlownessOther5, SpellId.WeaknessOther5, SpellId.FrailtyOther5,
            SpellId.ClumsinessOther5, SpellId.FesterOther5, SpellId.LifeMagicIneptitudeOther5, SpellId.CreatureEnchantmentIneptitudeOther5,
        };

        private static readonly SpellId[] AcxSupportSpellsLow =
        {
            SpellId.VulnerabilityOther2, SpellId.LightningVulnerabilityOther2, SpellId.ColdVulnerabilityOther2, SpellId.FireVulnerabilityOther2,
            SpellId.AcidVulnerabilityOther2, SpellId.PiercingVulnerabilityOther2, SpellId.BludgeonVulnerabilityOther2, SpellId.BladeVulnerabilityOther2,
            SpellId.ImperilOther2, SpellId.MagicYieldOther2, SpellId.SlownessOther2, SpellId.WeaknessOther2, SpellId.FrailtyOther2,
            SpellId.ClumsinessOther2, SpellId.FesterOther2, SpellId.LifeMagicIneptitudeOther2, SpellId.CreatureEnchantmentIneptitudeOther2,
        };
    }
}
