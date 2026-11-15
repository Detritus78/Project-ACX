-- Project ACX: town job boards (Managers/AcxJobBoard.cs, jobs in Content/jobs/jobs.json).
-- Each town gets a Job Board (a copy of its retail Bulletin Board, a Book) and a Job Runner (a copy of a Town Crier
-- without his rumours). Both carry AcxJobTown (string 30003) = the town's name in jobs.json; the server builds the
-- board's pages per character and handles the runner in code.
--   900300-900349  job boards        900350-900399  job runners
-- The four starter towns (2026-11-12); more towns follow the same pattern.
-- Re-runnable. Restart the server after applying.

DELETE FROM landblock_instance WHERE guid BETWEEN 0x7A9B4F00 AND 0x7A9B4F01 OR guid IN (0x7DA55F00, 0x7DA55F01, 0x77D64F00, 0x77D64F01, 0x733D9F00, 0x733D9F01);
DELETE FROM weenie WHERE class_Id BETWEEN 900300 AND 900399;

DROP TEMPORARY TABLE IF EXISTS acx_job_objects;
CREATE TEMPORARY TABLE acx_job_objects (class_Id INT UNSIGNED PRIMARY KEY, original INT UNSIGNED, class_Name VARCHAR(100), name VARCHAR(100), town VARCHAR(100));
INSERT INTO acx_job_objects VALUES
    (900300,  1137, 'acx-job-board-holtburg',  'Holtburg Job Board', 'Holtburg'),   -- Holtburg Bulletin Board
    (900301,  1141, 'acx-job-board-shoushi',   'Shoushi Job Board',  'Shoushi'),    -- Shoushi Bulletin Board
    (900302,  1143, 'acx-job-board-yaraq',     'Yaraq Job Board',    'Yaraq'),      -- Yaraq Bulletin Board
    (900303,  1137, 'acx-job-board-sanamar',   'Sanamar Job Board',  'Sanamar'),    -- (no Sanamar board in retail: Holtburg's)
    (900350,  5772, 'acx-job-runner-holtburg', 'Job Runner',         'Holtburg'),   -- Town Crier
    (900351,  5772, 'acx-job-runner-shoushi',  'Job Runner',         'Shoushi'),
    (900352,  5772, 'acx-job-runner-yaraq',    'Job Runner',         'Yaraq'),
    (900353,  5772, 'acx-job-runner-sanamar',  'Job Runner',         'Sanamar');

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT o.class_Id, o.class_Name, w.type, NOW() FROM acx_job_objects o JOIN weenie w ON w.class_Id = o.original;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_job_objects o JOIN weenie_properties_int p ON p.object_Id = o.original;
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_job_objects o JOIN weenie_properties_int64 p ON p.object_Id = o.original;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_job_objects o JOIN weenie_properties_float p ON p.object_Id = o.original;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_job_objects o JOIN weenie_properties_bool p ON p.object_Id = o.original;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_job_objects o JOIN weenie_properties_string p ON p.object_Id = o.original;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_job_objects o JOIN weenie_properties_d_i_d p ON p.object_Id = o.original;
INSERT INTO weenie_properties_i_i_d (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_job_objects o JOIN weenie_properties_i_i_d p ON p.object_Id = o.original;
INSERT INTO weenie_properties_book (object_Id, max_Num_Pages, max_Num_Chars_Per_Page) SELECT o.class_Id, p.max_Num_Pages, p.max_Num_Chars_Per_Page FROM acx_job_objects o JOIN weenie_properties_book p ON p.object_Id = o.original;
INSERT INTO weenie_properties_attribute (object_Id, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) SELECT o.class_Id, p.`type`, p.`init_Level`, p.`level_From_C_P`, p.`c_P_Spent` FROM acx_job_objects o JOIN weenie_properties_attribute p ON p.object_Id = o.original;
INSERT INTO weenie_properties_attribute_2nd (object_Id, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) SELECT o.class_Id, p.`type`, p.`init_Level`, p.`level_From_C_P`, p.`c_P_Spent`, p.`current_Level` FROM acx_job_objects o JOIN weenie_properties_attribute_2nd p ON p.object_Id = o.original;
INSERT INTO weenie_properties_skill (object_Id, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) SELECT o.class_Id, p.`type`, p.`level_From_P_P`, p.`s_a_c`, p.`p_p`, p.`init_Level`, p.`resistance_At_Last_Check`, p.`last_Used_Time` FROM acx_job_objects o JOIN weenie_properties_skill p ON p.object_Id = o.original;
INSERT INTO weenie_properties_body_part (object_Id, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`)
SELECT o.class_Id, p.`key`, p.`d_Type`, p.`d_Val`, p.`d_Var`, p.`base_Armor`, p.`armor_Vs_Slash`, p.`armor_Vs_Pierce`, p.`armor_Vs_Bludgeon`, p.`armor_Vs_Cold`, p.`armor_Vs_Fire`, p.`armor_Vs_Acid`, p.`armor_Vs_Electric`, p.`armor_Vs_Nether`, p.`b_h`, p.`h_l_f`, p.`m_l_f`, p.`l_l_f`, p.`h_r_f`, p.`m_r_f`, p.`l_r_f`, p.`h_l_b`, p.`m_l_b`, p.`l_l_b`, p.`h_r_b`, p.`m_r_b`, p.`l_r_b` FROM acx_job_objects o JOIN weenie_properties_body_part p ON p.object_Id = o.original;
INSERT INTO weenie_properties_create_list (object_Id, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) SELECT o.class_Id, p.`destination_Type`, p.`weenie_Class_Id`, p.`stack_Size`, p.`palette`, p.`shade`, p.`try_To_Bond` FROM acx_job_objects o JOIN weenie_properties_create_list p ON p.object_Id = o.original;

UPDATE weenie_properties_string s JOIN acx_job_objects o ON s.object_Id = o.class_Id SET s.value = o.name WHERE s.type = 1;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT class_Id, 30003, town FROM acx_job_objects;   -- AcxJobTown

-- Holtburg: just east of the town's portal drop (A9B40019 [84 7.1 94]), facing west toward it
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    (0x7A9B4F00, 900300, 0xA9B40019, 88.0,  9.0, 94.0, 0.7071068, 0, 0, 0.7071068, 0),
    (0x7A9B4F01, 900350, 0xA9B40019, 88.0, 11.5, 94.0, 0.7071068, 0, 0, 0.7071068, 0),
-- Shoushi, Yaraq, Sanamar: 4 m east of each town's portal drop, facing west toward it
    (0x7DA55F00, 900301, 0xDA55001D, 88.8, 101.5, 20.0,   0.7071068, 0, 0, 0.7071068, 0),
    (0x7DA55F01, 900351, 0xDA55001D, 88.8, 104.0, 20.0,   0.7071068, 0, 0, 0.7071068, 0),
    (0x77D64F00, 900302, 0x7D64000D, 35.9, 102.0, 11.9,   0.7071068, 0, 0, 0.7071068, 0),
    (0x77D64F01, 900352, 0x7D64000D, 35.9, 104.5, 11.9,   0.7071068, 0, 0, 0.7071068, 0),
    (0x733D9F00, 900303, 0x33D90015, 63.1,  98.5, 52.005, 0.7071068, 0, 0, 0.7071068, 0),
    (0x733D9F01, 900353, 0x33D90015, 63.1, 101.0, 52.005, 0.7071068, 0, 0, 0.7071068, 0);
