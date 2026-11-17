-- Project ACX (2026-11-16): a training dummy in the middle of Concordia's target range (JJ: "one quick mod I want atleast
-- one test dummy to place in the middle with a million health and 1.0 defenses across the board", "average defenses if
-- thats 1.0 thats fine", "Right in the middle of the target range", "it should have 0 melee defense missile defense and
-- magic defense", "I would prefer a unique mob").
-- A scarecrow (retail Shady Scarecrow 8274's looks, a mob no other range target uses) named Training Dummy:
--   1,000,000 health;
--   every damage type at 1.0: resistances 1.0 and body armor the same against every type, at 400 (about the average base
--   armor of the range's level 160-185 targets), so armor rending and cleaving still show;
--   melee, missile and magic defense 0: skills at 0, and Quickness, Coordination, Focus and Self at 1, since a usable
--   skill adds its attribute formula on top (CreatureSkill.Base), so every attack and spell lands.
-- Like the other targets it never attacks or moves, leaves no corpse and pays nothing (AcxPracticeTarget, bool 30013);
-- its own retail 10-second generator (28282) respawns it. Kill quests and death treasure are not copied.
-- Weenie 900809; guids 0x7018ABA0 (generator) and 0x7018ABA1 (dummy), on the range's middle spot [50 -70 6.005] in cell
-- 0x018A02F1. Re-runnable; after applying, /reload-landblock in Concordia (Foundry's Land tab) or restart.
DELETE FROM weenie WHERE class_Id = 900809;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900809, 'acx-training-dummy', type, NOW() FROM weenie WHERE class_Id = 8274;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900809, type, value FROM weenie_properties_int WHERE object_Id = 8274 AND type NOT IN (67, 146, 332);   -- not Tolerance, XpOverride, LuminanceAward
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT 900809, type, value FROM weenie_properties_int64 WHERE object_Id = 8274;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900809, type, value FROM weenie_properties_float WHERE object_Id = 8274 AND type NOT IN (64, 65, 66, 67, 68, 69, 70, 166);   -- not its resistances
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900809, type, value FROM weenie_properties_bool WHERE object_Id = 8274 AND type NOT IN (29, 52, 30013);   -- not NoCorpse, AiImmobile, AcxPracticeTarget
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900809, type, value FROM weenie_properties_string WHERE object_Id = 8274 AND type NOT IN (1, 45, 49, 50);   -- not its name or kill quests
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900809, type, value FROM weenie_properties_d_i_d WHERE object_Id = 8274 AND type NOT IN (35, 61);   -- not its death treasure
INSERT INTO weenie_properties_i_i_d (object_Id, type, value) SELECT 900809, type, value FROM weenie_properties_i_i_d WHERE object_Id = 8274;
INSERT INTO weenie_properties_attribute (object_Id, type, init_Level, level_From_C_P, c_P_Spent) SELECT 900809, type, IF(type IN (3, 4, 5, 6), 1, init_Level), IF(type IN (3, 4, 5, 6), 0, level_From_C_P), IF(type IN (3, 4, 5, 6), 0, c_P_Spent) FROM weenie_properties_attribute WHERE object_Id = 8274;   -- Quickness, Coordination, Focus, Self at 1
INSERT INTO weenie_properties_attribute_2nd (object_Id, type, init_Level, level_From_C_P, c_P_Spent, current_Level) SELECT 900809, type, IF(type = 1, 1000000, init_Level), level_From_C_P, c_P_Spent, IF(type = 1, 1000000, current_Level) FROM weenie_properties_attribute_2nd WHERE object_Id = 8274;   -- MaxHealth a million
INSERT INTO weenie_properties_skill (object_Id, type, level_From_P_P, s_a_c, p_p, init_Level, resistance_At_Last_Check, last_Used_Time) SELECT 900809, type, level_From_P_P, s_a_c, p_p, init_Level, resistance_At_Last_Check, last_Used_Time FROM weenie_properties_skill WHERE object_Id = 8274 AND type NOT IN (6, 7, 15);
INSERT INTO weenie_properties_skill (object_Id, type, level_From_P_P, s_a_c, p_p, init_Level, resistance_At_Last_Check, last_Used_Time) VALUES
    (900809, 6, 0, 2, 0, 0, 0, 0), (900809, 7, 0, 2, 0, 0, 0, 0), (900809, 15, 0, 2, 0, 0, 0, 0);   -- melee, missile, magic defense 0 (trained, no levels)
INSERT INTO weenie_properties_body_part (object_Id, `key`, d_Type, d_Val, d_Var, base_Armor, armor_Vs_Slash, armor_Vs_Pierce, armor_Vs_Bludgeon, armor_Vs_Cold, armor_Vs_Fire, armor_Vs_Acid, armor_Vs_Electric, armor_Vs_Nether, b_h, h_l_f, m_l_f, l_l_f, h_r_f, m_r_f, l_r_f, h_l_b, m_l_b, l_l_b, h_r_b, m_r_b, l_r_b)
SELECT 900809, `key`, d_Type, d_Val, d_Var, 400, 400, 400, 400, 400, 400, 400, 400, 400, b_h, h_l_f, m_l_f, l_l_f, h_r_f, m_r_f, l_r_f, h_l_b, m_l_b, l_l_b, h_r_b, m_r_b, l_r_b FROM weenie_properties_body_part WHERE object_Id = 8274;   -- armor 400 against every type
INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond) SELECT 900809, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond FROM weenie_properties_create_list WHERE object_Id = 8274 AND destination_Type NOT IN (1, 8, 9);   -- what it wears, not loot
INSERT INTO weenie_properties_palette (object_Id, sub_Palette_Id, offset, length) SELECT 900809, sub_Palette_Id, offset, length FROM weenie_properties_palette WHERE object_Id = 8274;
INSERT INTO weenie_properties_texture_map (object_Id, `index`, old_Id, new_Id) SELECT 900809, `index`, old_Id, new_Id FROM weenie_properties_texture_map WHERE object_Id = 8274;
INSERT INTO weenie_properties_anim_part (object_Id, `index`, animation_Id) SELECT 900809, `index`, animation_Id FROM weenie_properties_anim_part WHERE object_Id = 8274;
INSERT INTO weenie_properties_event_filter (object_Id, event) SELECT 900809, event FROM weenie_properties_event_filter WHERE object_Id = 8274;

INSERT INTO weenie_properties_string (object_Id, type, value) VALUES (900809, 1, 'Training Dummy');   -- Name
-- resistances: slash, pierce, bludgeon, fire, cold, acid, electric, nether all 1.0
INSERT INTO weenie_properties_float (object_Id, type, value) VALUES
    (900809, 64, 1.0), (900809, 65, 1.0), (900809, 66, 1.0), (900809, 67, 1.0),
    (900809, 68, 1.0), (900809, 69, 1.0), (900809, 70, 1.0), (900809, 166, 1.0);
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES (900809, 67, 1);   -- Tolerance: NoAttack, never fights back
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES
    (900809, 52, 1),      -- AiImmobile
    (900809, 29, 1),      -- NoCorpse
    (900809, 30013, 1);   -- AcxPracticeTarget: no XP, luminance, kill task, contract, invasion, T.A.R. or /xph

DELETE FROM landblock_instance_link WHERE parent_GUID = 0x7018ABA0;
DELETE FROM landblock_instance WHERE guid IN (0x7018ABA0, 0x7018ABA1);
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    (0x7018ABA0, 28282, 0x018A02F1, 50.0, -70.0, 6.005, 1, 0, 0, 0, 0),    -- its 10-second generator
    (0x7018ABA1, 900809, 0x018A02F1, 50.0, -70.0, 6.005, 1, 0, 0, 0, 1);   -- the dummy
INSERT INTO landblock_instance_link (parent_GUID, child_GUID) VALUES (0x7018ABA0, 0x7018ABA1);
