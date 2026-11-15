-- Project ACX experiment: loot test chests in the Marketplace (Preview only; never in acx/Content).
-- One chest per weapon class (JJ 2026-11-15: "create chests that spawn only missile weapons, heavy weapons, light weapons,
-- finesse weapons, twohand? and you can get rid of the chests we have now in the marketplace and replace them"):
--   900021  Missile Weapon Chest    bow, crossbow, atlatl or thrown, a quarter each
--   900022  Heavy Weapon Chest
--   900023  Light Weapon Chest
--   900024  Finesse Weapon Chest
--   900025  Two Handed Weapon Chest
-- Each is a Reinforced Mahogany Chest (23085) without its lock, with its own treasure profile (same number as the chest): a
-- copy of profile 421 (tier 6, 20-25 magic items) rolling weapons only, and the server picks them from the chest's class
-- (Factories/Tables/Wcids/Weapons/AcxClassChestWcids.cs; no casters). Fresh loot every time it is closed
-- (ChestRegenOnClose); anything left inside is cleared at that point.
-- They replace the 2026-11-11 chests (900013 Tier 6 Loot Chest x2, 900014 Tier 6 Weapon Chest x2, treasure 900001), in a
-- row 4 m in front of the Marketplace arrival point (016C01BC [49.2 -31.9 0.005]), facing it.
-- Re-runnable. Restart the server (or /reload-landblock in the Marketplace) after applying.

DELETE FROM landblock_instance WHERE guid BETWEEN 0x7016C001 AND 0x7016C004 OR guid BETWEEN 0x7016C010 AND 0x7016C014;
DELETE FROM weenie WHERE class_Id IN (900013, 900014) OR class_Id BETWEEN 900021 AND 900025;
DELETE FROM treasure_death WHERE treasure_Type = 900001 OR treasure_Type BETWEEN 900021 AND 900025;

DROP TEMPORARY TABLE IF EXISTS acx_chest;
CREATE TEMPORARY TABLE acx_chest (class_Id INT UNSIGNED PRIMARY KEY, class_Name VARCHAR(100), name VARCHAR(100), guid INT UNSIGNED, y FLOAT);
INSERT INTO acx_chest VALUES
    (900021, 'acx-test-chest-missile', 'Missile Weapon Chest', 0x7016C010, -27.0),
    (900022, 'acx-test-chest-heavy', 'Heavy Weapon Chest', 0x7016C011, -28.5),
    (900023, 'acx-test-chest-light', 'Light Weapon Chest', 0x7016C012, -30.0),
    (900024, 'acx-test-chest-finesse', 'Finesse Weapon Chest', 0x7016C013, -31.5),
    (900025, 'acx-test-chest-two-handed', 'Two Handed Weapon Chest', 0x7016C014, -33.0);

-- copy the Reinforced Mahogany Chest
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT c.class_Id, c.class_Name, w.type, NOW() FROM acx_chest c JOIN weenie w ON w.class_Id = 23085;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT c.class_Id, p.type, p.value FROM acx_chest c JOIN weenie_properties_int p ON p.object_Id = 23085 WHERE p.type <> 38;   -- no ResistLockpick
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT c.class_Id, p.type, p.value FROM acx_chest c JOIN weenie_properties_float p ON p.object_Id = 23085;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT c.class_Id, p.type, p.value FROM acx_chest c JOIN weenie_properties_bool p ON p.object_Id = 23085 WHERE p.type NOT IN (3, 35, 86);
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT c.class_Id, p.type, p.value FROM acx_chest c JOIN weenie_properties_d_i_d p ON p.object_Id = 23085;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT c.class_Id, p.type, p.value FROM acx_chest c JOIN weenie_properties_string p ON p.object_Id = 23085 WHERE p.type NOT IN (1, 12);   -- not its name or LockCode
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT class_Id, 1, name FROM acx_chest;
-- no lock: Locked (bool 3) and DefaultLocked (bool 35) off; fresh loot each time it is closed: ChestRegenOnClose (bool 86)
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT class_Id, 3, 0 FROM acx_chest;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT class_Id, 35, 0 FROM acx_chest;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT class_Id, 86, 1 FROM acx_chest;
-- its loot: the chest's own treasure profile in place of 23085's
INSERT INTO weenie_properties_generator (object_Id, probability, weenie_Class_Id, delay, init_Create, max_Create, when_Create, where_Create, stack_Size, palette_Id, shade, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z)
SELECT c.class_Id, g.probability, c.class_Id, g.delay, g.init_Create, g.max_Create, g.when_Create, g.where_Create, g.stack_Size, g.palette_Id, g.shade, g.obj_Cell_Id, g.origin_X, g.origin_Y, g.origin_Z, g.angles_W, g.angles_X, g.angles_Y, g.angles_Z
FROM acx_chest c JOIN weenie_properties_generator g ON g.object_Id = 23085;

-- treasure 421 with magic item type profile 1 (Weapon 100%)
INSERT INTO treasure_death (treasure_Type, tier, loot_Quality_Mod, unknown_Chances, item_Chance, item_Min_Amount, item_Max_Amount, item_Treasure_Type_Selection_Chances, magic_Item_Chance, magic_Item_Min_Amount, magic_Item_Max_Amount, magic_Item_Treasure_Type_Selection_Chances, mundane_Item_Chance, mundane_Item_Min_Amount, mundane_Item_Max_Amount, mundane_Item_Type_Selection_Chances, last_Modified)
SELECT c.class_Id, t.tier, t.loot_Quality_Mod, t.unknown_Chances, t.item_Chance, t.item_Min_Amount, t.item_Max_Amount, t.item_Treasure_Type_Selection_Chances, t.magic_Item_Chance, t.magic_Item_Min_Amount, t.magic_Item_Max_Amount, 1, t.mundane_Item_Chance, t.mundane_Item_Min_Amount, t.mundane_Item_Max_Amount, t.mundane_Item_Type_Selection_Chances, NOW()
FROM acx_chest c JOIN treasure_death t ON t.treasure_Type = 421;

-- five in a row facing west, toward the arrival point
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child)
SELECT guid, class_Id, 0x016C01BC, 53.5, y, 0.005, 0.7071068, 0, 0, 0.7071068, 0 FROM acx_chest;

DROP TEMPORARY TABLE acx_chest;
