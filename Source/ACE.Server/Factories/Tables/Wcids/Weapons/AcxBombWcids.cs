using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories.Tables.Wcids
{
    /// <summary>
    /// Project ACX: the Alchemist's bombs (JJ 2026-11-16: "lets entertain alchemy class", then "Yes you got it all right. Lets
    /// put it in and bench it and see how that goes"; design in ~/Work/ac/research/alchemist-class-design-2026-11-16.md).
    /// Eternal thrown weapons (one item, never used up, swapped like any thrown weapon: "switching will require the same
    /// thing as thrown does") that need specialized Alchemy, which is also their attack skill and their blast's power
    /// ("throwing harder doesnt make a grenade blow up any stronger"; Creature.GetAttributeMod). A loot bomb is an
    /// eternal thrown weapon with Potency, its damage multiplier ("its a modifier for the physical damage"), raised by
    /// Obsidian ("what if we added potency and a new salvage"; RecipeManager_Acx).
    /// The blast is physical (Missile Defense, armor, resistances; Player_AcxBombs): it hits its target and, for
    /// acx_alchemist_splash_share, every other monster within acx_alchemist_splash_radius, and after the damage puts a
    /// vulnerability of its own damage type on everything it hit ("lets go with B": one rule; the Nether bomb, whose type
    /// has no vulnerability, puts Imperil), resisted by the thrower's Alchemy against Magic Defense ("I shouldnt be able
    /// to take a level 1 character and land on a level 300 a vuln").
    /// The Healing bomb (sold by the Mark Trader) does no damage: players and pets in its burst heal over time, monsters
    /// fester ("Fester and HOT could combine. So that on a monster it festers on a player it heals").
    ///   acx/Content/alchemist/10-bombs.sql: 900890-900897 the eight damage bombs, 900898 the Healing bomb.
    /// </summary>
    public static class AcxBombWcids
    {
        public const uint Fire = 900890, Frost = 900891, Acid = 900892, Lightning = 900893, Razor = 900894, Shrapnel = 900895, Concussion = 900896, Nether = 900897, Healing = 900898;

        private const uint First = Fire, LastDamage = Nether;

        public static bool IsBomb(uint wcid) => wcid >= First && wcid <= Healing;

        public static bool IsDamageBomb(uint wcid) => wcid >= First && wcid <= LastDamage;

        public static bool IsHealingBomb(uint wcid) => wcid == Healing;

        public static bool Enabled => PropertyManager.GetBool("acx_alchemist_enabled").Item;

        // the Mark Trader's nine eternal phials (invasions/20-mark-vendor.sql: Imperil, Fester and the seven vulnerabilities),
        // which the bombs replace: the Nether bomb carries Imperil and the Healing bomb Fester (JJ 2026-11-16: "Yes you got it
        // all right")
        private const uint FirstReplacedPhial = 900712, LastReplacedPhial = 900720;

        /// <summary>
        /// Whether a shop sells this: with acx_alchemist_enabled, bombs and not the phials they replace; without, the reverse
        /// </summary>
        public static bool ShopSells(uint wcid)
        {
            if (IsBomb(wcid))
                return Enabled;
            if (wcid >= FirstReplacedPhial && wcid <= LastReplacedPhial)
                return !Enabled;
            return true;
        }

        // the three physical bombs, which come before the elements in the rule acx_alchemist_low_tier_rule = physical
        private static readonly uint[] PhysicalBombs = { Razor, Shrapnel, Concussion };
        private static readonly uint[] OtherBombs = { Fire, Frost, Acid, Lightning, Nether };

        /// <summary>
        /// The rule for bombs below loot tier 5 (acx_alchemist_low_tier_rule), benched both ways for JJ (2026-11-16: "I hate to get
        /// rid of the elementals", then "lets bench it like you suggest it. and see how it comes out"):
        ///   t3grade:  every type from tier 1 ("Elements from tier 1"); a tier 4 bomb's damage rolls as tier 3's (DamageTier)
        ///   physical: Razor, Shrapnel and Concussion only through tier 4; at tier 5 half of them physical, half the elements and
        ///             Nether (the atlatl's tier 5 mix, 51% plain); all eight evenly from tier 6
        /// </summary>
        public static bool PhysicalLowTiers => PropertyManager.GetString("acx_alchemist_low_tier_rule").Item == "physical";

        // any other value (none): every type from tier 1 and a tier 4 bomb is a tier 4 bomb
        private static bool Tier3GradeAtTier4 => PropertyManager.GetString("acx_alchemist_low_tier_rule").Item == "t3grade";

        /// <summary>
        /// A loot bomb of a tier
        /// </summary>
        public static uint Roll(int tier)
        {
            if (PhysicalLowTiers && tier <= 5)
            {
                if (tier <= 4 || ThreadSafeRandom.Next(0.0f, 1.0f) < 0.51f)
                    return PhysicalBombs[ThreadSafeRandom.Next(0, PhysicalBombs.Length - 1)];
                return OtherBombs[ThreadSafeRandom.Next(0, OtherBombs.Length - 1)];
            }
            return First + (uint)ThreadSafeRandom.Next(0, (int)(LastDamage - First));
        }

        /// <summary>
        /// Whether loot of a tier can drop this bomb (the bench rolls only what a character could find)
        /// </summary>
        public static bool DropsAtTier(uint wcid, int tier)
        {
            if (!IsDamageBomb(wcid))
                return false;
            return !PhysicalLowTiers || tier >= 5 || System.Array.IndexOf(PhysicalBombs, wcid) >= 0;
        }

        /// <summary>
        /// The tier a bomb's damage rolls at: with t3grade, a tier 4 bomb's own damage and quality, Potency and debuff level are tier
        /// 3's (JJ 2026-11-16: "why dont we skip tier 4 for the bombs and tier 3 is used all the way up to 5?"), so a level 100
        /// Alchemist's elemental bombs aren't a tier ahead of the plain bows, atlatls and thrown weapons of tier 4; the rest of the
        /// bomb (defense bonuses, spells and cantrips, wield requirement, workmanship, mana) is tier 4's, so it is still an upgrade
        /// (JJ: "we could bump the defense modifiers and spells on tier 4 but not touch damage")
        /// </summary>
        public static int DamageTier(uint wcid, int tier)
        {
            return IsBomb(wcid) && tier == 4 && Tier3GradeAtTier4 ? 3 : tier;
        }

        // a vulnerability's levels I-VI and VII (the "Gift" spells of the Mana Phials, x2.85; a mage's Incantation is x3.1), by damage
        // type; the Nether bomb's Imperil I-VI (-20 to -200 armor: its VII, Gossamer Flesh, is as strong as the Incantation's -225)
        private static readonly Dictionary<DamageType, uint[]> DebuffLevels = new Dictionary<DamageType, uint[]>()
        {
            { DamageType.Fire,     new uint[] { 21, 1104, 1105, 1106, 1107, 1108, 2170 } },
            { DamageType.Cold,     new uint[] { 1060, 1061, 1062, 1063, 1064, 1065, 2168 } },
            { DamageType.Acid,     new uint[] { 521, 522, 523, 524, 525, 526, 2162 } },
            { DamageType.Electric, new uint[] { 1084, 1085, 1086, 1087, 1088, 1089, 2172 } },
            { DamageType.Slash,    new uint[] { 1127, 1128, 1129, 1130, 1131, 1132, 2164 } },
            { DamageType.Pierce,   new uint[] { 1151, 1152, 1153, 1154, 1155, 1156, 2174 } },
            { DamageType.Bludgeon, new uint[] { 1048, 1049, 1050, 1051, 1052, 1053, 2166 } },
            { DamageType.Nether,   new uint[] { 25, 1323, 1324, 1325, 1326, 1327 } },
        };

        // The debuff's level by the bomb's loot tier 1-8: x1.25, 1.75, 2.0, 2.5, 2.5, 2.85, 2.85, 2.85 damage of its type.
        // A bomb takes no imbue (RecipeManager_AcxPairs): its vulnerability is its rend, so its level follows what a rend gives a
        // character of the tier's levels (x1.3-1.7 below level 50, 2.0-2.2 at 50-114, 2.3-2.5 after), about a level under what a
        // mage of those levels casts. Until 2026-11-16 it was the tier's own number to tier 6 (I-VI: x1.1 to 2.5), which with
        // realistic characters left the Alchemist at 57-72% of an archer on one target through level 180 against 84-91% from
        // 220 (/acxsurvival, Training Dummy, JJ: "balanced throughout its life cycle").
        private static readonly int[] DebuffLevelByTier = { 2, 4, 5, 6, 6, 7, 7, 7 };

        /// <summary>
        /// The Healing bomb's debuff on monsters: Fester VII, Decrepitude's Grasp (health regeneration x0.4; a mage's
        /// Incantation is x0.25)
        /// </summary>
        public const uint FesterSpell = 2178;

        public static uint? DebuffSpell(DamageType damageType, int tier)
        {
            if (!DebuffLevels.TryGetValue(damageType, out var levels))
                return null;
            return levels[System.Math.Min(DebuffLevelByTier[System.Math.Clamp(tier, 1, 8) - 1], levels.Length) - 1];
        }

        /// <summary>
        /// A bomb's debuff: its type's at the loot tier it dropped at (AcxBombTier; a vendor or /ci bomb: tier 8), by today's
        /// table, so a retune reaches bombs already dropped. The Nether bomb's Imperil is held to acx_alchemist_imperil_max_level
        /// (armor off every weapon hit stacks with everything, so it is the bench's knob for the Nether bomb)
        /// </summary>
        public static uint? GetDebuffSpell(WorldObject bomb)
        {
            if (bomb == null || !IsDamageBomb(bomb.WeenieClassId))
                return null;
            var tier = bomb.GetProperty(PropertyDataId.AcxBombTier) ?? 8;
            var spell = tier <= 8 ? DebuffSpell(bomb.W_DamageType, (int)tier) : tier;   // (above 8: a spell id, as bombs carried before 2026-11-16)
            if (spell != null && bomb.W_DamageType == DamageType.Nether)
            {
                var imperil = DebuffLevels[DamageType.Nether];
                var level = System.Array.IndexOf(imperil, spell.Value) + 1;
                var cap = (int)System.Math.Clamp(PropertyManager.GetLong("acx_alchemist_imperil_max_level").Item, 1, 6);
                if (level > cap)
                    spell = imperil[cap - 1];
            }
            return spell;
        }

        /// <summary>
        /// Loot: a bomb's wield requirements become specialized Alchemy, and the Missile Weapons level the atlatl script rolled
        /// becomes an Alchemy level; it keeps its tier, which sets its debuff (GetDebuffSpell)
        /// </summary>
        public static void MutateLoot(WorldObject wo, int tier)
        {
            if (wo.WieldRequirements == WieldRequirement.RawSkill || wo.WieldRequirements == WieldRequirement.Skill)
            {
                wo.WieldSkillType = (int)Skill.Alchemy;
                wo.WieldRequirements2 = WieldRequirement.Training;
                wo.WieldSkillType2 = (int)Skill.Alchemy;
                wo.WieldDifficulty2 = (int)SkillAdvancementClass.Specialized;
            }
            else
            {
                wo.WieldRequirements = WieldRequirement.Training;
                wo.WieldSkillType = (int)Skill.Alchemy;
                wo.WieldDifficulty = (int)SkillAdvancementClass.Specialized;
            }

            if (IsDamageBomb(wo.WeenieClassId))
                wo.SetProperty(PropertyDataId.AcxBombTier, (uint)System.Math.Clamp(tier, 1, 8));
        }

        /// <summary>
        /// The other monsters a blast hits: the nearest acx_alchemist_splash_targets of those in range (0: all of them), each for
        /// SplashShare of a hit, so a blast is worth at most the hit plus targets x share (3 x 0.43: 2.3 hits; a cleaving weapon is
        /// worth 2). JJ 2026-11-16: "I might end up putting a max of 3 monsters hit by splash damage", then "yeah lets lower the
        /// share of splash". Before: 0.65 each for up to 2, a bigger crowd sharing those 1.3 hits, which held the Alchemist on
        /// packs to about the best class's damage from level 180; with no limit a pack of four gave it 1.2 to 1.4 times that
        /// (/acxsurvival pack; JJ: "near the top end but not all the way at the top")
        /// </summary>
        public static List<Creature> SplashTargets(IEnumerable<Creature> nearestFirst)
        {
            var max = (int)PropertyManager.GetLong("acx_alchemist_splash_targets").Item;
            return (max > 0 ? nearestFirst.Take(max) : nearestFirst).ToList();
        }

        /// <summary>
        /// The share of a hit each other monster in a blast takes (each rolls its own Missile Defense, like a cleave target)
        /// </summary>
        public static float SplashShare => (float)PropertyManager.GetDouble("acx_alchemist_splash_share").Item;
    }
}
