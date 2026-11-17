-- Project ACX (2026-11-17): a runed chest with tier 6 loot (JJ: "I am seeing low level rune chests in high level areas like
-- tou tou"). A procedural chest (WorldObjects/Creature_AcxDeploy.cs) takes the loot tier of the monster whose spawn rolled
-- it, but retail's best runed chest, the extreme one (22566, lock 400), carries tier 5 loot (treasure profile 413), so tier
-- 6-8 areas got tier 5 chests. Tier 6 areas now get this copy of it with tier 6 loot (treasure profile 900906 = 413 with only
-- the tier changed: 4-8 items and 4-8 magic items, quality +0.2); tier 7 and 8 areas get no chest at all, as in retail (JJ
-- picked that over 700/800 locks: a maxed picker with Incantation of Weaken Lock, -250, opens any lower lock).
--   900906  Runed Chest with tier 6 loot (treasure profile 900906) and a 500 lock (JJ: "tier 6 chest I would up the lockpick to
--           500"; the tier 5 one stays 400)
-- Re-runnable; it also removes the tier 7 and 8 copies (900907-900908) that stood here briefly.
DROP TEMPORARY TABLE IF EXISTS acx_chest_tier;
CREATE TEMPORARY TABLE acx_chest_tier (tier INT PRIMARY KEY, class_Id INT UNSIGNED);
INSERT INTO acx_chest_tier VALUES (6, 900906);

DELETE FROM treasure_death WHERE treasure_Type BETWEEN 900906 AND 900908;
INSERT INTO treasure_death (treasure_Type, tier, loot_Quality_Mod, unknown_Chances, item_Chance, item_Min_Amount, item_Max_Amount,
    item_Treasure_Type_Selection_Chances, magic_Item_Chance, magic_Item_Min_Amount, magic_Item_Max_Amount, magic_Item_Treasure_Type_Selection_Chances,
    mundane_Item_Chance, mundane_Item_Min_Amount, mundane_Item_Max_Amount, mundane_Item_Type_Selection_Chances)
SELECT t.class_Id, t.tier, d.loot_Quality_Mod, d.unknown_Chances, d.item_Chance, d.item_Min_Amount, d.item_Max_Amount,
    d.item_Treasure_Type_Selection_Chances, d.magic_Item_Chance, d.magic_Item_Min_Amount, d.magic_Item_Max_Amount, d.magic_Item_Treasure_Type_Selection_Chances,
    d.mundane_Item_Chance, d.mundane_Item_Min_Amount, d.mundane_Item_Max_Amount, d.mundane_Item_Type_Selection_Chances
FROM treasure_death d JOIN acx_chest_tier t WHERE d.treasure_Type = 413;

DELETE FROM weenie WHERE class_Id BETWEEN 900906 AND 900908;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT t.class_Id, CONCAT('acx-chest-runed-tier', t.tier), w.type, NOW() FROM acx_chest_tier t JOIN weenie w ON w.class_Id = 22566;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT t.class_Id, p.type, p.value FROM acx_chest_tier t JOIN weenie_properties_int p ON p.object_Id = 22566;
UPDATE weenie_properties_int SET value = 500 WHERE object_Id = 900906 AND type = 38;   -- ResistLockpick
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT t.class_Id, p.type, p.value FROM acx_chest_tier t JOIN weenie_properties_float p ON p.object_Id = 22566;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT t.class_Id, p.type, p.value FROM acx_chest_tier t JOIN weenie_properties_bool p ON p.object_Id = 22566;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT t.class_Id, p.type, p.value FROM acx_chest_tier t JOIN weenie_properties_string p ON p.object_Id = 22566;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT t.class_Id, p.type, p.value FROM acx_chest_tier t JOIN weenie_properties_d_i_d p ON p.object_Id = 22566;
INSERT INTO weenie_properties_generator (object_Id, probability, weenie_Class_Id, delay, init_Create, max_Create, when_Create, where_Create, stack_Size, palette_Id, shade, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z)
SELECT t.class_Id, g.probability, t.class_Id, g.delay, g.init_Create, g.max_Create, g.when_Create, g.where_Create, g.stack_Size, g.palette_Id, g.shade, g.obj_Cell_Id, g.origin_X, g.origin_Y, g.origin_Z, g.angles_W, g.angles_X, g.angles_Y, g.angles_Z
FROM acx_chest_tier t JOIN weenie_properties_generator g ON g.object_Id = 22566;   -- its treasure: the tier's profile

DROP TEMPORARY TABLE acx_chest_tier;
