-- Project ACX: town records boards (Managers/AcxLeaderboards.cs). A copy of a town's retail Bulletin Board (a Book)
-- with AcxRecordsBoard (bool 30004); the server fills its pages with the leaderboards: wealth, jobs, deaths,
-- deadliest monsters, areas explored.
--   900400-900449  records boards
-- The four starter towns, beside their job boards. Re-runnable. Restart the server after applying.

DELETE FROM landblock_instance WHERE guid IN (0x7A9B4F02, 0x7DA55F02, 0x77D64F02, 0x733D9F02);
DELETE FROM weenie WHERE class_Id BETWEEN 900400 AND 900449;

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900400, 'acx-records-board-holtburg', type, NOW() FROM weenie WHERE class_Id = 1137;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900400, type, value FROM weenie_properties_int WHERE object_Id = 1137;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900400, type, value FROM weenie_properties_float WHERE object_Id = 1137;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900400, type, value FROM weenie_properties_bool WHERE object_Id = 1137;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900400, type, value FROM weenie_properties_string WHERE object_Id = 1137;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900400, type, value FROM weenie_properties_d_i_d WHERE object_Id = 1137;
INSERT INTO weenie_properties_book (object_Id, max_Num_Pages, max_Num_Chars_Per_Page) SELECT 900400, max_Num_Pages, max_Num_Chars_Per_Page FROM weenie_properties_book WHERE object_Id = 1137;
UPDATE weenie_properties_string SET value = 'Records of Dereth' WHERE object_Id = 900400 AND type = 1;
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (900400, 30004, 1);   -- AcxRecordsBoard

-- beside each starter town's job board, east of the town's portal drop, facing west (one board weenie serves every town)
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    (0x7A9B4F02, 900400, 0xA9B40019, 88.0,  6.5,  94.0,   0.7071068, 0, 0, 0.7071068, 0),
    (0x7DA55F02, 900400, 0xDA55001D, 88.8, 99.0,  20.0,   0.7071068, 0, 0, 0.7071068, 0),
    (0x77D64F02, 900400, 0x7D64000D, 35.9, 107.0, 11.9,   0.7071068, 0, 0, 0.7071068, 0),
    (0x733D9F02, 900400, 0x33D90015, 63.1, 103.5, 52.005, 0.7071068, 0, 0, 0.7071068, 0);
