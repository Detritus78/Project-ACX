-- Project ACX experiment: loot test chests in the Marketplace (Preview only; never in acx/Content).
--   900013  Tier 6 Loot Chest: a Reinforced Mahogany Chest (23085) without its lock.
--           Loot profile 421 = tier 6, 20-25 magic items. Fresh loot every time it is closed (ChestRegenOnClose);
--           anything left inside is cleared at that point.
-- Four of them stand in a row 4 m in front of the Marketplace arrival point (016C01BC [49.2 -31.9 0.005], facing east).
-- Re-runnable. Restart the server (or /reload-landblock in the Marketplace) after applying.

DELETE FROM landblock_instance WHERE guid BETWEEN 0x7016C001 AND 0x7016C004;
DELETE FROM weenie WHERE class_Id = 900013;

INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
SELECT 900013, 'acx-test-loot-chest-t6', type, NOW() FROM weenie WHERE class_Id = 23085;

-- copy the Reinforced Mahogany Chest
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900013, type, value FROM weenie_properties_int WHERE object_Id = 23085;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900013, type, value FROM weenie_properties_float WHERE object_Id = 23085;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900013, type, value FROM weenie_properties_bool WHERE object_Id = 23085;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900013, type, value FROM weenie_properties_d_i_d WHERE object_Id = 23085;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900013, type, value FROM weenie_properties_string WHERE object_Id = 23085;
INSERT INTO weenie_properties_generator (object_Id, probability, weenie_Class_Id, delay, init_Create, max_Create, when_Create, where_Create, stack_Size, palette_Id, shade, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z)
SELECT 900013, probability, weenie_Class_Id, delay, init_Create, max_Create, when_Create, where_Create, stack_Size, palette_Id, shade, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z
FROM weenie_properties_generator WHERE object_Id = 23085;

-- name
UPDATE weenie_properties_string SET value = 'Tier 6 Loot Chest' WHERE object_Id = 900013 AND type = 1;

-- no lock: Locked (bool 3) and DefaultLocked (bool 35) off, no LockCode (string 12), no ResistLockpick (int 38)
UPDATE weenie_properties_bool SET value = 0 WHERE object_Id = 900013 AND type IN (3, 35);
DELETE FROM weenie_properties_string WHERE object_Id = 900013 AND type = 12;
DELETE FROM weenie_properties_int WHERE object_Id = 900013 AND type = 38;

-- fresh loot each time it is closed: ChestRegenOnClose (bool 86)
DELETE FROM weenie_properties_bool WHERE object_Id = 900013 AND type = 86;
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (900013, 86, 1);

-- four chests facing west, toward the arrival point
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    (0x7016C001, 900013, 0x016C01BC, 53.5, -27.0, 0.005, 0.7071068, 0, 0, 0.7071068, 0),
    (0x7016C002, 900013, 0x016C01BC, 53.5, -29.0, 0.005, 0.7071068, 0, 0, 0.7071068, 0),
    (0x7016C003, 900013, 0x016C01BC, 53.5, -31.0, 0.005, 0.7071068, 0, 0, 0.7071068, 0),
    (0x7016C004, 900013, 0x016C01BC, 53.5, -33.0, 0.005, 0.7071068, 0, 0, 0.7071068, 0);
