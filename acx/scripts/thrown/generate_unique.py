#!/usr/bin/env python3
"""Project ACX: writes acx/Content/thrown/45-eternal-unique.sql, the Eternal copies of retail's quest, drop and crafted
thrown weapons (2026-11-17).

    python3 -I acx/scripts/thrown/generate_unique.py
"""
import pathlib

OUT = pathlib.Path(__file__).resolve().parents[2] / "Content/thrown/45-eternal-unique.sql"

# Each row: wcid, retail source, own damage, damage multiplier, value, Missile Weapons skill to wield. The numbers are the median untinked loot thrown
# weapon of the tier the item belongs to (/acxgear class, 2026-11-17: tier 1 12 x2.0, tier 2 12 x2.37, tier 3 28 x2.35,
# tier 4 29 x2.43, tier 5 55 x2.43, tier 6 57 x2.5, tier 7 62 x2.55, tier 8 69 x2.45), set per item below and benched
# with /acxgear class <level> items=<wcid,...>. The wield requirement is that of loot of the same strength (tier 1 none,
# tiers 2-3 250, tier 4 270, tier 5 315, tier 6 315-335, tier 7 360, tier 8 375; JJ: "some of those might not be accessible
# at certain levels"), so a strong one handed to a low character waits for the skill, as a loot weapon does.
SPIKE, GREATER, DEADLY, GREATER_DEADLY = (12, 2.0, 25, 0), (28, 2.35, 50, 250), (34, 2.4, 100, 290), (55, 2.45, 200, 315)
ROWS = []


def add(first, sources, numbers):
    for i, source in enumerate(sources):
        ROWS.append((first + i, source) + numbers)


# crafted with Fletching: a recipe makes one. Spike, Acid, Blunt, Broad, Lightning, Fire, Frost
add(900910, (23873, 23874, 23875, 23876, 23877, 23878, 23879), SPIKE)            # no wield requirement: tier 1
add(900917, (23866, 23867, 23868, 23869, 23870, 23871, 23872), GREATER)          # Missile Weapons 250: tier 3
add(900924, (23859, 23860, 23861, 23862, 23863, 23864, 23865), DEADLY)           # 280: between tiers 4 and 5
add(900931, (44375, 44371, 44382, 44215), GREATER_DEADLY)                        # 310 (Greater Deadly, Blunt, Broad; Burning Sands): tier 5
add(900935, (34999,), (62, 2.55, 1000, 360))    # Channeling Stone Hatchet (360; Stone Hatchet + Mucor-altered Mahogany): tier 7
# quest rewards: one, where retail gave 10 or 20
add(900936, (3709,), (12, 2.4, 300, 250))       # Ivory Tooth Dart (Ivory Crafters): tier 2
add(900937, (28220,), (12, 2.4, 300, 250))      # Ruddy Tooth Spiketail (Ivory Crafters): tier 2
add(900938, (6887,), (12, 2.6, 300, 250))       # Oswald's Throwing Dagger (Jourgensson), electric: tier 2
# drops: one
add(900939, (3791,), (12, 2.6, 300, 250))       # Lightning Djarid (Impious undead, level 44-53, 1 in 5): tier 2
add(900940, (10997,), (38, 2.45, 300, 270))     # Ebon Spine Harpoon (Aun Ralirea, level 80), frost, slow, a wide damage range: tier 4
add(900941, (23130,), (57, 2.5, 300, 335))      # Frost Throwing Club (Banderling Guard Champions, level 165-170): tier 6; dropped by code (AcxThrownWcids.EternalDrops)
add(900942, (8525,), (69, 2.7, 300, 375))       # Asmolum's Throwing Dagger (Asmolum the Worm, level 277): end-game, see below
# the Apprentices' gifts for a Skeleton's Skull or a Pumpkin Head (Alexander's, Janda's, Tsua's; 100 before), fire. JJ 2026-11-19:
# "It needs a thrown conversion right?", "lets convert and bench as is and see where it lands in the t1-t4 range. I'm okay
# with it being between 1 & 2 tiers like a tier 1.25". Benched at L15 against a tier 1 loot fire dart: retail's 25 x1.0 -34%,
# 12 x2.1 -13% (like-for-like bench, no transfers at that level); 12 x2.3 (JJ: "okay raise it"), still under tier 2's x2.37.
# No wield requirement, like tier 1
add(900943, (12218, 12217), (12, 2.3, 300, 0))  # Flaming Skull, Flaming Pumpkinhead

# retail's own two unlimited thrown weapons (Missile Weapons 385, five spells, a critical strike bonus): the same model, in place
# End-game quest weapons (these two and Asmolum's dagger; JJ 2026-11-17: "if its endgame which they are. It can come out over
# ... the exception is loot with legendary blood thirst and fully tinked should come out over the end game not by a whole
# lot though"): with their imbue and two transfers they bench a little behind a fully tinkered tier 8 loot thrown weapon
# with Legendary Blood Thirst, and ahead of anything less.
# The Storming Portal Axe aims at 95% of that loot weapon's hit, both electric against monsters weak to it (JJ: "I want the
# portal axe as close to 95 as we can get it"); Oswald's dagger and the Lightning Djarid were raised the same way.
IN_PLACE = [(52732, 69, 2.75), (52733, 69, 2.45)]   # Storming Portal Axe, Vortex Thorn

values = ",\n".join(f"    ({w}, {s}, {d}, {m}, {v}, {q})" for w, s, d, m, v, q in ROWS) + ";"
first, last = ROWS[0][0], ROWS[-1][0]
in_place = "\n".join(
    f"UPDATE weenie_properties_int SET value = {d} WHERE object_Id = {w} AND type = 44;\n"
    f"DELETE FROM weenie_properties_float WHERE object_Id = {w} AND type = 63;\n"
    f"INSERT INTO weenie_properties_float (object_Id, type, value) VALUES ({w}, 63, {m});" for w, d, m in IN_PLACE)

OUT.write_text(f"""-- Project ACX (2026-11-17): the rest of retail's thrown weapons, Eternal (JJ: "I completely forgot about quest items like
-- discus and items you can create with fletching called spikes ... Theres also the ebon harpoon. We need to adjust these
-- also and I assume make them eternal? We definetly need to check and make sure they do not ruin balance"; "if its a
-- consumable thrown weapon.. And I'm not talking about mugs and dishe's it should probably be eternal").
-- Each is a copy of the retail item (name, look, damage type, speed, wield requirement, damage range) made a single
-- weapon with UnlimitedUse and ACX's thrown damage model (own Damage x DamageMod, 10-eternal-thrown.sql), and whoever hands
-- the retail stack to a player now hands over one copy: the Fletching recipes (spikes, 100 or 1,000 a craft before), the
-- quest givers, and the monsters' corpses. Monsters that throw them keep retail's.
-- The numbers are those of the median untinked loot thrown weapon of the tier the item belongs to (retail's were the
-- whole hit of a consumable: a Spike 50, an Ebon Spine Harpoon 45, Asmolum's Throwing Dagger 14). None has a workmanship,
-- so none can be tinkered: a tinkered loot weapon stays about three times as strong (/acxgear class).
-- Left as retail's: what players never get (the Viamontian guards' Throwing Axes and the Ruschk's Ice Shards are
-- destroyed with their owner), the Stone Hatchet (an ingredient), and the joke items (Throwing Pancake, snowballs, dishes).
--   {first}-{last}; code: AcxThrownWcids.IsUnique. Generated by acx/scripts/thrown/generate_unique.py. Re-runnable.
DELETE FROM weenie WHERE class_Id BETWEEN {first} AND {last};
DROP TEMPORARY TABLE IF EXISTS acx_unique;
CREATE TEMPORARY TABLE acx_unique (class_Id INT UNSIGNED PRIMARY KEY, source INT UNSIGNED, damage INT, damage_mod FLOAT, value INT, wield INT);
INSERT INTO acx_unique VALUES
{values}
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT u.class_Id, CONCAT('acx-eternal-', w.class_Name), w.type, NOW() FROM acx_unique u JOIN weenie w ON w.class_Id = u.source;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT u.class_Id, p.type, p.value FROM acx_unique u JOIN weenie_properties_int p ON p.object_Id = u.source WHERE p.type NOT IN (11, 12, 15, 19, 36, 44, 48, 158, 159, 160, 353);   -- not 15 StackUnitValue: a stackable's Value comes from it; not 36 ResistMagic: retail's
    -- Ebon Spine Harpoon and Asmolum's Throwing Dagger refuse enchantments, and Blood Drinker is a large part of a thrown weapon's hit here
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT u.class_Id, p.type, p.value FROM acx_unique u JOIN weenie_properties_int64 p ON p.object_Id = u.source;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT u.class_Id, p.type, p.value FROM acx_unique u JOIN weenie_properties_float p ON p.object_Id = u.source WHERE p.type NOT IN (26, 63);
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT u.class_Id, p.type, p.value FROM acx_unique u JOIN weenie_properties_bool p ON p.object_Id = u.source WHERE p.type NOT IN (22, 63);
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT u.class_Id, p.type, p.value FROM acx_unique u JOIN weenie_properties_string p ON p.object_Id = u.source WHERE p.type <> 20;   -- not its plural
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT u.class_Id, p.type, p.value FROM acx_unique u JOIN weenie_properties_d_i_d p ON p.object_Id = u.source;
INSERT INTO weenie_properties_spell_book (object_Id, spell, probability) SELECT u.class_Id, p.spell, p.probability FROM acx_unique u JOIN weenie_properties_spell_book p ON p.object_Id = u.source;
INSERT INTO weenie_properties_palette (object_Id, sub_Palette_Id, offset, length) SELECT u.class_Id, p.sub_Palette_Id, p.offset, p.length FROM acx_unique u JOIN weenie_properties_palette p ON p.object_Id = u.source;
INSERT INTO weenie_properties_texture_map (object_Id, `index`, old_Id, new_Id) SELECT u.class_Id, p.`index`, p.old_Id, p.new_Id FROM acx_unique u JOIN weenie_properties_texture_map p ON p.object_Id = u.source;
INSERT INTO weenie_properties_anim_part (object_Id, `index`, animation_Id) SELECT u.class_Id, p.`index`, p.animation_Id FROM acx_unique u JOIN weenie_properties_anim_part p ON p.object_Id = u.source;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 11, 1 FROM acx_unique;             -- MaxStackSize: one item, not a stack
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 12, 1 FROM acx_unique;             -- StackSize
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 19, value FROM acx_unique;         -- Value: low, so crafting or farming them for vendors pays nothing
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 44, damage FROM acx_unique;        -- Damage: its own, the ammunition's part
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT class_Id, 63, damage_mod FROM acx_unique;  -- DamageMod: the launcher's part
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 48, 47 FROM acx_unique;            -- WeaponSkill: Missile Weapons
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 353, 10 FROM acx_unique;           -- WeaponType: Thrown
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 158, 2 FROM acx_unique WHERE wield > 0;    -- WieldRequirements: base skill
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 159, 47 FROM acx_unique WHERE wield > 0;   -- WieldSkillType: Missile Weapons
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 160, wield FROM acx_unique WHERE wield > 0; -- WieldDifficulty
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT class_Id, 26, 20 FROM acx_unique;          -- MaximumVelocity: the thrown range the others show
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT class_Id, 63, 1 FROM acx_unique;            -- UnlimitedUse: never used up
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT class_Id, 22, 1 FROM acx_unique;            -- Inscribable

-- one Eternal copy in place of the stack: the recipes, the quest givers, the corpses and containers
UPDATE recipe r JOIN acx_unique u ON r.success_W_C_I_D = u.source SET r.success_W_C_I_D = u.class_Id, r.success_Amount = 1;
UPDATE recipe r JOIN weenie_properties_string n ON n.object_Id = r.success_W_C_I_D AND n.type = 1 SET r.success_Message = CONCAT('You craft one ', n.value, '. It never runs out.')
WHERE r.success_W_C_I_D BETWEEN {first} AND {last} AND r.success_Message LIKE 'You make%';   -- was "You make a bundle of spikes." 
UPDATE weenie_properties_emote_action a JOIN acx_unique u ON a.weenie_Class_Id = u.source SET a.weenie_Class_Id = u.class_Id, a.stack_Size = 1 WHERE a.type = 3;   -- Give
UPDATE weenie_properties_create_list c JOIN acx_unique u ON c.weenie_Class_Id = u.source SET c.weenie_Class_Id = u.class_Id, c.stack_Size = 1 WHERE c.destination_Type IN (1, 8, 9);   -- contain / treasure, not what a monster wields
DROP TEMPORARY TABLE acx_unique;

-- Storming Portal Axe and Vortex Thorn: already unlimited in retail; the same damage model, in place
{in_place}
""")
print(len(ROWS), "weapons ->", OUT)
