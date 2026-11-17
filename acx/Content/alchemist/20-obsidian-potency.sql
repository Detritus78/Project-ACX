-- Project ACX (2026-11-16): Obsidian raises an Alchemist's bomb's Potency, its damage multiplier, by 4% per tinker, as
-- Mahogany does a bow's (JJ: "what if we added potency and a new salvage? ... its a modifier for the physical damage",
-- then "yes" to Obsidian: the client knows it and nothing used it). Recipe 9317 is Mahogany's (3855: Weapon Tinkering,
-- its difficulty, workmanship, at most 10 tinkers) with the ACX mutation 0x3800100D (AcxTinker.Potency, 13); the code
-- (Managers/RecipeManager_Acx.cs) matches any Obsidian bag to a damage bomb, and Mahogany refuses bombs
-- (RecipeManager_New.cs). Re-runnable.
DELETE FROM recipe WHERE id = 9317;
INSERT INTO recipe (id, unknown_1, skill, difficulty, salvage_Type, success_W_C_I_D, success_Amount, success_Message,
    fail_W_C_I_D, fail_Amount, fail_Message,
    success_Destroy_Source_Chance, success_Destroy_Source_Amount, success_Destroy_Source_Message,
    success_Destroy_Target_Chance, success_Destroy_Target_Amount, success_Destroy_Target_Message,
    fail_Destroy_Source_Chance, fail_Destroy_Source_Amount, fail_Destroy_Source_Message,
    fail_Destroy_Target_Chance, fail_Destroy_Target_Amount, fail_Destroy_Target_Message,
    data_Id, last_Modified)
SELECT 9317, t.unknown_1, t.skill, t.difficulty, t.salvage_Type, 0, 0, 'You apply the obsidian, raising the bomb''s Potency.',
    0, 0, 'You apply the obsidian, but in the process you destroy the target.',
    t.success_Destroy_Source_Chance, t.success_Destroy_Source_Amount, t.success_Destroy_Source_Message,
    t.success_Destroy_Target_Chance, t.success_Destroy_Target_Amount, t.success_Destroy_Target_Message,
    t.fail_Destroy_Source_Chance, t.fail_Destroy_Source_Amount, t.fail_Destroy_Source_Message,
    t.fail_Destroy_Target_Chance, t.fail_Destroy_Target_Amount, t.fail_Destroy_Target_Message,
    t.data_Id, NOW()
FROM recipe t WHERE t.id = 3855;

INSERT INTO recipe_mod (recipe_Id, executes_On_Success, health, stamina, mana, unknown_7, data_Id, unknown_9, instance_Id)
-- 939528205 = 0x3800100D (a hex literal in IF() is a byte string to MySQL)
SELECT 9317, m.executes_On_Success, m.health, m.stamina, m.mana, m.unknown_7, IF(m.data_Id <> 0, 939528205, 0), m.unknown_9, m.instance_Id
FROM recipe_mod m WHERE m.recipe_Id = 3855 ORDER BY m.id;

INSERT INTO recipe_mods_string (recipe_Mod_Id, `index`, stat, value, enum, source)
SELECT n.id, s.`index`, s.stat, s.value, s.enum, s.source
FROM recipe_mod n JOIN recipe_mod m ON m.recipe_Id = 3855 AND (m.data_Id <> 0) = (n.data_Id <> 0)
JOIN recipe_mods_string s ON s.recipe_Mod_Id = m.id
WHERE n.recipe_Id = 9317;

INSERT INTO recipe_mods_d_i_d (recipe_Mod_Id, `index`, stat, value, enum, source)
SELECT n.id, d.`index`, d.stat, d.value, d.enum, d.source
FROM recipe_mod n JOIN recipe_mod m ON m.recipe_Id = 3855 AND (m.data_Id <> 0) = (n.data_Id <> 0)
JOIN recipe_mods_d_i_d d ON d.recipe_Mod_Id = m.id
WHERE n.recipe_Id = 9317;

INSERT INTO recipe_requirements_int (recipe_Id, `index`, stat, value, enum, message)
SELECT 9317, r.`index`, r.stat, r.value, r.enum, r.message FROM recipe_requirements_int r WHERE r.recipe_Id = 3855;

-- the Obsidian bags: offered on missile weapons (TargetType 0x100), usable from the pack onto an item, and say what they do
DROP TEMPORARY TABLE IF EXISTS acx_obsidian_bags;
CREATE TEMPORARY TABLE acx_obsidian_bags AS
SELECT m.object_Id AS wcid FROM weenie_properties_int m
JOIN weenie_properties_int it ON it.object_Id = m.object_Id AND it.type = 1 AND it.value = 0x40000000   -- TinkeringMaterial
WHERE m.type = 131 AND m.value = 69;                                                                    -- Obsidian
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT wcid, 94, 0x100 FROM acx_obsidian_bags ON DUPLICATE KEY UPDATE value = VALUES(value);
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT wcid, 16, 0x80008 FROM acx_obsidian_bags ON DUPLICATE KEY UPDATE value = VALUES(value);
INSERT INTO weenie_properties_string (object_Id, type, value)
SELECT wcid, 14, 'Apply this material to a treasure-generated Alchemist''s bomb to increase its Potency (its damage multiplier) by 4%.' FROM acx_obsidian_bags
ON DUPLICATE KEY UPDATE value = VALUES(value);
DROP TEMPORARY TABLE acx_obsidian_bags;
