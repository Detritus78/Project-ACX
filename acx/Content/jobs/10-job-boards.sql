-- Project ACX: town job boards (Managers/AcxJobBoard.cs, jobs in Content/jobs/jobs.json).
-- Each town gets a Job Board (a copy of its retail Bulletin Board, a Book; Holtburg's where the town has none). The four
-- starter towns also have a Job Runner (a copy of a Town Crier without his rumours); the board pays on its own, so the
-- other towns have no runner (JJ 2026-11-16). Both carry AcxJobTown (string 30003) = the town's name in jobs.json; the
-- server builds the board's pages per character and handles the runner in code.
--   900300-900349  job boards        900350-900399  job runners
-- The four starter towns (2026-11-12); 41 more towns (2026-11-16, every town in invasions/towns.json with a job), each
-- board 2 m beside the town's Town Crier facing the way he faces, or 4 m east of the portal drop facing it where the town
-- has no crier (or its crier is over 300 m out). Objects face their -Y side. JJ moves them in game as he likes.
-- Re-runnable. Restart the server after applying.

DELETE FROM landblock_instance WHERE weenie_Class_Id BETWEEN 900300 AND 900399
    OR guid BETWEEN 0x7A9B4F00 AND 0x7A9B4F01 OR guid IN (0x7DA55F00, 0x7DA55F01, 0x77D64F00, 0x77D64F01, 0x733D9F00, 0x733D9F01);
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
    (900353,  5772, 'acx-job-runner-sanamar',  'Job Runner',         'Sanamar'),
    (900304,   488, 'acx-job-board-cragstone',             'Cragstone Job Board',             'Cragstone'),  -- Cragstone Bulletin Board
    (900305,  1137, 'acx-job-board-greenspire',            'Greenspire Job Board',            'Greenspire'),  -- Holtburg Bulletin Board
    (900306,  1137, 'acx-job-board-lytelthorpe',           'Lytelthorpe Job Board',           'Lytelthorpe'),  -- Holtburg Bulletin Board
    (900307,   490, 'acx-job-board-rithwic',               'Rithwic Job Board',               'Rithwic'),  -- Rithwic Bulletin Board
    (900308,  1137, 'acx-job-board-kryst',                 'Kryst Job Board',                 'Kryst'),  -- Holtburg Bulletin Board
    (900309,  1137, 'acx-job-board-nanto',                 'Nanto Job Board',                 'Nanto'),  -- Holtburg Bulletin Board
    (900310,  1142, 'acx-job-board-yanshi',                'Yanshi Job Board',                'Yanshi'),  -- Yanshi Bulletin Board
    (900311,  1137, 'acx-job-board-bluespire',             'Bluespire Job Board',             'Bluespire'),  -- Holtburg Bulletin Board
    (900312,  1137, 'acx-job-board-redspire',              'Redspire Job Board',              'Redspire'),  -- Holtburg Bulletin Board
    (900313,  2268, 'acx-job-board-baishi',                'Baishi Job Board',                'Baishi'),  -- Baishi Bulletin Board
    (900314,   489, 'acx-job-board-eastham',               'Eastham Job Board',               'Eastham'),  -- Eastham Bulletin Board
    (900315,  1135, 'acx-job-board-glenden-wood',          'Glenden Wood Job Board',          'Glenden Wood'),  -- Glenden Wood Bulletin Board
    (900316,  1137, 'acx-job-board-lin',                   'Lin Job Board',                   'Lin'),  -- Holtburg Bulletin Board
    (900317,  1138, 'acx-job-board-mayoi',                 'Mayoi Job Board',                 'Mayoi'),  -- Mayoi Bulletin Board
    (900318,  2283, 'acx-job-board-sawato',                'Sawato Job Board',                'Sawato'),  -- Sawato Bulletin Board
    (900319,  1137, 'acx-job-board-tou-tou',               'Tou-Tou Job Board',               'Tou-Tou'),  -- Holtburg Bulletin Board
    (900320,  1137, 'acx-job-board-xarabydun',             'Xarabydun Job Board',             'Xarabydun'),  -- Holtburg Bulletin Board
    (900321,  1144, 'acx-job-board-zaikhal',               'Zaikhal Job Board',               'Zaikhal'),  -- Zaikhal Bulletin Board
    (900322,   487, 'acx-job-board-arwic',                 'Arwic Job Board',                 'Arwic'),  -- Arwic Bulletin Board
    (900323,  1137, 'acx-job-board-westwatch',             'Westwatch Job Board',             'Westwatch'),  -- Holtburg Bulletin Board
    (900324,  1137, 'acx-job-board-freehold',              'Freehold Job Board',              'Freehold'),  -- Holtburg Bulletin Board
    (900325,  1139, 'acx-job-board-qalabar',               'Qalabar Job Board',               'Qalabar'),  -- Qalaba'r Bulletin Board
    (900326,  1137, 'acx-job-board-ahurenga',              'Ahurenga Job Board',              'Ahurenga'),  -- Holtburg Bulletin Board
    (900327,  1137, 'acx-job-board-bandit-castle',         'Bandit Castle Job Board',         'Bandit Castle'),  -- Holtburg Bulletin Board
    (900328,  1137, 'acx-job-board-danbys-outpost',        'Danby''s Outpost Job Board',      'Danby''s Outpost'),  -- Holtburg Bulletin Board
    (900329,  2238, 'acx-job-board-dryreach',              'Dryreach Job Board',              'Dryreach'),  -- Dryreach Bulletin Board
    (900330,  1137, 'acx-job-board-kara',                  'Kara Job Board',                  'Kara'),  -- Holtburg Bulletin Board
    (900331,  1137, 'acx-job-board-linvak-tukal',          'Linvak Tukal Job Board',          'Linvak Tukal'),  -- Holtburg Bulletin Board
    (900332,  1137, 'acx-job-board-neydisa',               'Neydisa Job Board',               'Neydisa'),  -- Holtburg Bulletin Board
    (900333,  1137, 'acx-job-board-plateau',               'Plateau Job Board',               'Plateau'),  -- Holtburg Bulletin Board
    (900334,  1137, 'acx-job-board-refuge',                'Refuge Job Board',                'Refuge'),  -- Holtburg Bulletin Board
    (900335,  1137, 'acx-job-board-silyun',                'Silyun Job Board',                'Silyun'),  -- Holtburg Bulletin Board
    (900336,  1137, 'acx-job-board-timaru',                'Timaru Job Board',                'Timaru'),  -- Holtburg Bulletin Board
    (900337,  1137, 'acx-job-board-beach-fort',            'Beach Fort Job Board',            'Beach Fort'),  -- Holtburg Bulletin Board
    (900338,  1137, 'acx-job-board-candeth-keep',          'Candeth Keep Job Board',          'Candeth Keep'),  -- Holtburg Bulletin Board
    (900339,  1137, 'acx-job-board-fiun',                  'Fiun Job Board',                  'Fiun'),  -- Holtburg Bulletin Board
    (900340,  2323, 'acx-job-board-fort-tethana',          'Fort Tethana Job Board',          'Fort Tethana'),  -- Fort Tethana Bulletin Board
    (900341,  1137, 'acx-job-board-stonehold',             'Stonehold Job Board',             'Stonehold'),  -- Holtburg Bulletin Board
    (900342,  1137, 'acx-job-board-wai-jhou',              'Wai Jhou Job Board',              'Wai Jhou'),  -- Holtburg Bulletin Board
    (900343,  1137, 'acx-job-board-ayan-baqur',            'Ayan Baqur Job Board',            'Ayan Baqur'),  -- Holtburg Bulletin Board
    (900344,  1137, 'acx-job-board-eastwatch',             'Eastwatch Job Board',             'Eastwatch');  -- Holtburg Bulletin Board

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
-- 2026-11-18: Neydisa, Nanto, Eastham, Cragstone, Kara, Plateau and Baishi's boards set onto the ground (they floated 0.1-0.4 m or sank up to 1.2 m; JJ: "fix any job boards those are ours")
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

-- The 41 towns added 2026-11-16 (board only; the board pays). Cell, spot and facing per town:
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    (0x7BB9FF00, 900304, 0xBB9F003F, 173.5, 159.96, 55.345, 0.8733272, 0, 0, 0.4871341, 0),   -- Cragstone: 2 m to the right of the Town Crier (via, 8 m from the drop), facing the way he faces
    (0x72BB5F00, 900305, 0x2BB5003C, 183.0, 81.77, 0.005, 0.2377911, 0, 0, -0.9713163, 0),   -- Greenspire: 2 m to the right of the Town Crier (instance, 6 m from the drop), facing the way he faces
    (0x7C080F00, 900306, 0xC0800007, 4.88, 152.38, 33.65, 0.5913979, 0, 0, 0.8063799, 0),   -- Lytelthorpe: 2 m to the right of the Town Crier (via, 8 m from the drop), facing the way he faces
    (0x7C98CF00, 900307, 0xC98C0028, 102.4, 184.74, 22.005, 0.715616, 0, 0, 0.698494, 0),   -- Rithwic: 2 m to the right of the Town Crier (via, 12 m from the drop), facing the way he faces
    (0x7E822F00, 900308, 0xE8220027, 115.35, 146.98, 0.005, 0.416783, 0, 0, -0.909006, 0),   -- Kryst: 2 m to the right of the Town Crier (via, 109 m from the drop), facing the way he faces
    (0x7E63EF00, 900309, 0xE63E0022, 99.94, 31.83, 76.048, 1.0, 0, 0, 0.0, 0),   -- Nanto: 2 m to the right of the Town Crier (via, 8 m from the drop), facing the way he faces
    (0x7B470F00, 900310, 0xB4700021, 110.0, 10.0, 42.0, 1.0, 0, 0, 0.0, 0),   -- Yanshi: 2 m to the right of the Town Crier (via, 86 m from the drop), facing the way he faces
    (0x721B0F00, 900311, 0x21B0000E, 41.73, 138.64, 0.005, 0.20683, 0, 0, 0.9783769, 0),   -- Bluespire: 2 m to the right of the Town Crier (instance, 28 m from the drop), facing the way he faces
    (0x717B2F00, 900312, 0x17B2002A, 133.53, 33.94, 44.005, 0.9803446, 0, 0, -0.1972929, 0),   -- Redspire: 2 m to the right of the Town Crier (instance, 8 m from the drop), facing the way he faces
    (0x7CE42F00, 900313, 0xCE420001, 7.92, 21.38, 52.665, 0.9238798, 0, 0, -0.3826829, 0),   -- Baishi: 2 m to the right of the Town Crier (via, 59 m from the drop), facing the way he faces
    (0x7CE94F00, 900314, 0xCE940036, 148.99, 123.48, 18.711, 0.2381501, 0, 0, -0.9712284, 0),   -- Eastham: 2 m to the right of the Town Crier (via, 11 m from the drop), facing the way he faces
    (0x7A0A4F00, 900315, 0xA0A40025, 106.84, 112.85, 58.127, 0.7849033, 0, 0, 0.6196183, 0),   -- Glenden Wood: 2 m to the right of the Town Crier (via, 12 m from the drop), facing the way he faces
    (0x7DC3BF00, 900316, 0xDC3B0010, 43.1, 182.03, 20.0, 0.936989, 0, 0, -0.349359, 0),   -- Lin: 2 m to the right of the Town Crier (via, 27 m from the drop), facing the way he faces
    (0x7E632F00, 900317, 0xE6320019, 92.73, 6.25, 28.0, 0.3826829, 0, 0, -0.9238798, 0),   -- Mayoi: 2 m to the right of the Town Crier (via, 17 m from the drop), facing the way he faces
    (0x7C85AF00, 900318, 0xC85A003F, 173.51, 163.6, 12.005, 0.3826829, 0, 0, 0.9238798, 0),   -- Sawato: 2 m to the right of the Town Crier (via, 44 m from the drop), facing the way he faces
    (0x7F559F00, 900319, 0xF5590034, 156.59, 80.8, 20.005, 0.7071068, 0, 0, -0.7071068, 0),   -- Tou-Tou: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x7934BF00, 900320, 0x934B0029, 131.46, 7.69, 15.165, 0.3757189, 0, 0, 0.9267337, 0),   -- Xarabydun: 2 m to the right of the Town Crier (via, 22 m from the drop), facing the way he faces
    (0x78090F00, 900321, 0x80900013, 58.75, 59.3, 124.005, 0.9238798, 0, 0, 0.3826829, 0),   -- Zaikhal: 2 m to the right of the Town Crier (via, 7 m from the drop), facing the way he faces
    (0x7C6A9F00, 900322, 0xC6A9000B, 43.19, 68.38, 42.005, 0.9236339, 0, 0, -0.383276, 0),   -- Arwic: 2 m to the right of the Town Crier (via, 63 m from the drop), facing the way he faces
    (0x723DAF00, 900323, 0x23DA0034, 144.9, 86.0, 2.005, 0.7071068, 0, 0, -0.7071068, 0),   -- Westwatch: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x7F224F00, 900324, 0xF224001A, 85.8, 33.0, 0.0, 0.7071068, 0, 0, -0.7071068, 0),   -- Freehold: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x79722F00, 900325, 0x97220032, 162.61, 27.44, 102.0, 0.9238798, 0, 0, 0.3826829, 0),   -- Qalabar: 2 m to the right of the Town Crier (via, 6 m from the drop), facing the way he faces
    (0x70FB9F00, 900326, 0x0FB90009, 47.0, 8.6, 0.005, 0.7071068, 0, 0, -0.7071068, 0),   -- Ahurenga: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x7BDD0F00, 900327, 0xBDD00006, 20.9, 120.5, 115.1, 0.7071068, 0, 0, -0.7071068, 0),   -- Bandit Castle: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x75B9CF00, 900328, 0x5B9C0025, 106.74, 107.13, 14.005, 0.0, 0, 0, 1.0, 0),   -- Danby's Outpost: 2 m to the right of the Town Crier (via, 275 m from the drop), facing the way he faces
    (0x7DA75F00, 900329, 0xDA75002B, 134.69, 56.75, 18.005, 0.7071068, 0, 0, 0.7071068, 0),   -- Dryreach: 2 m to the right of the Town Crier (via, 3 m from the drop), facing the way he faces
    (0x7BA17F00, 900330, 0xBA170039, 185.2, 3.2, 168.938, 0.7071068, 0, 0, -0.7071068, 0),   -- Kara: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x7A21EF00, 900331, 0xA21E001A, 87.0, 38.0, 560.4, 0.7071068, 0, 0, -0.7071068, 0),   -- Linvak Tukal: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x795D6F00, 900332, 0x95D60033, 150.9, 71.3, 99.43, 0.7071068, 0, 0, -0.7071068, 0),   -- Neydisa: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x749B7F00, 900333, 0x49B70021, 104.1, 20.8, 238.947, 0.7071068, 0, 0, -0.7071068, 0),   -- Plateau: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x7F681F00, 900334, 0xF6810017, 65.16, 162.28, 51.591, 0.8704221, 0, 0, -0.4923061, 0),   -- Refuge: 2 m to the right of the Town Crier (instance, 114 m from the drop), facing the way he faces
    (0x726ECF00, 900335, 0x26EC003D, 179.93, 110.33, 80.005, 0.7071068, 0, 0, -0.7071068, 0),   -- Silyun: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x71DB6F00, 900336, 0x1DB6001E, 75.39, 134.29, 90.005, 0.7071068, 0, 0, -0.7071068, 0),   -- Timaru: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x742DEF00, 900337, 0x42DE000C, 29.0, 84.5, 0.005, 0.7071068, 0, 0, -0.7071068, 0),   -- Beach Fort: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x72B11F00, 900338, 0x2B11001E, 74.85, 132.52, 48.005, 0.1314451, 0, 0, 0.9913235, 0),   -- Candeth Keep: 2 m to the right of the Town Crier (instance, 77 m from the drop), facing the way he faces
    (0x738F7F00, 900339, 0x38F7001B, 95.0, 54.0, -0.095, 0.7071068, 0, 0, -0.7071068, 0),   -- Fiun: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x72581F00, 900340, 0x2581003C, 168.58, 77.63, 220.005, 0.236218, 0, 0, 0.9717001, 0),   -- Fort Tethana: 2 m to the right of the Town Crier (instance, 107 m from the drop), facing the way he faces
    (0x764D5F00, 900341, 0x64D5000B, 34.0, 50.0, 78.0, 0.7071068, 0, 0, -0.7071068, 0),   -- Stonehold: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x73F31F00, 900342, 0x3F31000F, 27.0, 149.6, -0.1, 0.7071068, 0, 0, -0.7071068, 0),   -- Wai Jhou: no Town Crier: 4 m east of the town's portal drop, facing west toward it
    (0x71134F00, 900343, 0x1134002D, 121.58, 110.5, 42.005, 0.757006, 0, 0, 0.653408, 0),   -- Ayan Baqur: 2 m to the right of the Town Crier (instance, 142 m from the drop), facing the way he faces
    (0x749F0F00, 900344, 0x49F0001B, 74.0, 70.0, 170.005, 0.7071068, 0, 0, -0.7071068, 0);   -- Eastwatch: no Town Crier: 4 m east of the town's portal drop, facing west toward it
