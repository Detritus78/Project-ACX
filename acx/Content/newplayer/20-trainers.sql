-- Project ACX (2026-11-16): the Alchemist Trainer and the Thrown Weapons Trainer by Holtburg's lifestone (JJ: "We may need
-- to create a npc in the starter building that gives players an opt to switch right there to thrown or alchemy", then
-- "put the trainers in holtburg by the lifestone"). Talking to one is handled in code (Managers/AcxTrainers.cs): a
-- character of level unlimited_respec_max_level (20) or lower trades its specialized weapon skill for the trainer's after
-- a Yes/No popup, and gets a starter weapon once. Copies of retail town NPCs, as plain NPCs (type Creature) without their
-- emotes or shop lists:
--   900901  Alchemist Trainer         (Journeyman Alchemist, 28917)
--   900902  Thrown Weapons Trainer    (the Aluvian Weaponsmith, 1393; not the Master Archer 38839, which is a level 220
--           monster)
-- Placed 4 m west of the lifestone (A9B40019 [81.3 11.8 94]), facing it; JJ moves them with ACX Foundry if needed.
-- Re-runnable. Restart the server after applying (the landblock keeps its old instances until it reloads).
DELETE FROM landblock_instance WHERE weenie_Class_Id IN (900901, 900902);
DELETE FROM weenie WHERE class_Id IN (900901, 900902);

DROP TEMPORARY TABLE IF EXISTS acx_trainer;
CREATE TEMPORARY TABLE acx_trainer (class_Id INT UNSIGNED PRIMARY KEY, original INT UNSIGNED, class_Name VARCHAR(100), name VARCHAR(100));
INSERT INTO acx_trainer VALUES
    (900901, 28917, 'acx-trainer-alchemist', 'Alchemist Trainer'),
    (900902, 1393,  'acx-trainer-thrown',    'Thrown Weapons Trainer');

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT o.class_Id, o.class_Name, 10, NOW() FROM acx_trainer o JOIN weenie w ON w.class_Id = o.original;   -- 10 = Creature: a vendor's own use would open its shop
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_trainer o JOIN weenie_properties_int p ON p.object_Id = o.original;
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_trainer o JOIN weenie_properties_int64 p ON p.object_Id = o.original;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_trainer o JOIN weenie_properties_float p ON p.object_Id = o.original;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_trainer o JOIN weenie_properties_bool p ON p.object_Id = o.original;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_trainer o JOIN weenie_properties_string p ON p.object_Id = o.original WHERE p.type <> 5;   -- not the retail title
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_trainer o JOIN weenie_properties_d_i_d p ON p.object_Id = o.original;
INSERT INTO weenie_properties_i_i_d (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_trainer o JOIN weenie_properties_i_i_d p ON p.object_Id = o.original;
INSERT INTO weenie_properties_attribute (object_Id, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) SELECT o.class_Id, p.`type`, p.`init_Level`, p.`level_From_C_P`, p.`c_P_Spent` FROM acx_trainer o JOIN weenie_properties_attribute p ON p.object_Id = o.original;
INSERT INTO weenie_properties_attribute_2nd (object_Id, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) SELECT o.class_Id, p.`type`, p.`init_Level`, p.`level_From_C_P`, p.`c_P_Spent`, p.`current_Level` FROM acx_trainer o JOIN weenie_properties_attribute_2nd p ON p.object_Id = o.original;
INSERT INTO weenie_properties_skill (object_Id, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) SELECT o.class_Id, p.`type`, p.`level_From_P_P`, p.`s_a_c`, p.`p_p`, p.`init_Level`, p.`resistance_At_Last_Check`, p.`last_Used_Time` FROM acx_trainer o JOIN weenie_properties_skill p ON p.object_Id = o.original;
INSERT INTO weenie_properties_body_part (object_Id, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`)
SELECT o.class_Id, p.`key`, p.`d_Type`, p.`d_Val`, p.`d_Var`, p.`base_Armor`, p.`armor_Vs_Slash`, p.`armor_Vs_Pierce`, p.`armor_Vs_Bludgeon`, p.`armor_Vs_Cold`, p.`armor_Vs_Fire`, p.`armor_Vs_Acid`, p.`armor_Vs_Electric`, p.`armor_Vs_Nether`, p.`b_h`, p.`h_l_f`, p.`m_l_f`, p.`l_l_f`, p.`h_r_f`, p.`m_r_f`, p.`l_r_f`, p.`h_l_b`, p.`m_l_b`, p.`l_l_b`, p.`h_r_b`, p.`m_r_b`, p.`l_r_b` FROM acx_trainer o JOIN weenie_properties_body_part p ON p.object_Id = o.original;
-- their clothes and gear only (2 = wielded), none of a vendor's or collector's other lists
INSERT INTO weenie_properties_create_list (object_Id, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) SELECT o.class_Id, p.`destination_Type`, p.`weenie_Class_Id`, p.`stack_Size`, p.`palette`, p.`shade`, p.`try_To_Bond` FROM acx_trainer o JOIN weenie_properties_create_list p ON p.object_Id = o.original WHERE p.destination_Type = 2;

UPDATE weenie_properties_string s JOIN acx_trainer o ON s.object_Id = o.class_Id SET s.value = o.name WHERE s.type = 1;

-- 4 m west of Holtburg's lifestone, facing east toward it
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    (0x7A9B4F10, 900901, 0xA9B40019, 77.6, 10.0, 94.0, 0.7071068, 0, 0, -0.7071068, 0),
    (0x7A9B4F11, 900902, 0xA9B40019, 77.6, 13.6, 94.0, 0.7071068, 0, 0, -0.7071068, 0);

DROP TEMPORARY TABLE acx_trainer;
