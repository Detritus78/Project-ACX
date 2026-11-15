-- Project ACX: salvage that retail left without a use (or used for heritage requirements, which are gone) gets a job.
--   Yellow Garnet / White Quartz / Green Jade / Amethyst: a weapon's or caster's element becomes fire / cold / acid / lightning
--   Onyx / Tourmaline / White Jade: a weapon's damage type becomes piercing / slashing / bludgeoning
--   Turquoise: Arcane Lore requirement -5;  Serpentine: Arcane Lore requirement -10, and the item's mana cost doubles
--   Tiger Eye: imbues a caster with Elemental Rending (fire, cold, acid and lightning rending; no nether)
--   Teak / Ebony: a melee weapon's missile / magic defense bonus +1%
-- An item's damage type can be changed once, by any of the seven, and it takes on the look and name of that weapon's
-- version with the new type (a battle axe made fire becomes a Flaming Battle Axe; nether loses its smoke and sparks).
-- The code (Managers/RecipeManager_Acx.cs) picks these recipes by the salvage's material and applies the effects;
-- the rows here carry the skill, the messages and the requirements. Obsidian, Satin, Oak and Porcelain stay unused.
-- Re-runnable.

DELETE FROM recipe WHERE id BETWEEN 9301 AND 9316;   -- their mods and requirements go with them (ON DELETE CASCADE)

-- template 3848 (Brass) is a plain tinker; 4452 (Red Garnet) is an imbue and adds the imbue icon underlay
DROP TEMPORARY TABLE IF EXISTS acx_remap;
CREATE TEMPORARY TABLE acx_remap (
    recipe_Id INT UNSIGNED PRIMARY KEY,
    template_Id INT UNSIGNED NOT NULL,
    skill INT NOT NULL,                  -- 18 Item, 28 Weapon, 30 Magic Item Tinkering
    salvage_Type INT NOT NULL,           -- 1 tinker, 2 imbue
    data_Id INT NOT NULL,                -- 0x38001000 + the code's AcxTinker number
    material VARCHAR(32) NOT NULL,
    success_Message TEXT NOT NULL,
    damage_Type INT NULL                 -- the damage type it changes to: an item that already has it is refused
);
INSERT INTO acx_remap VALUES
    (9301, 3848, 28, 1, 0x38001001, 'yellow garnet', 'You apply the yellow garnet. The weapon now deals fire damage.', 16),
    (9302, 3848, 28, 1, 0x38001002, 'white quartz', 'You apply the white quartz. The weapon now deals cold damage.', 8),
    (9303, 3848, 28, 1, 0x38001003, 'green jade', 'You apply the green jade. The weapon now deals acid damage.', 32),
    (9304, 3848, 28, 1, 0x38001004, 'amethyst', 'You apply the amethyst. The weapon now deals lightning damage.', 64),
    (9305, 3848, 28, 1, 0x38001005, 'onyx', 'You apply the onyx. The weapon now deals piercing damage.', 2),
    (9306, 3848, 28, 1, 0x38001006, 'tourmaline', 'You apply the tourmaline. The weapon now deals slashing damage.', 1),
    (9307, 3848, 28, 1, 0x38001007, 'white jade', 'You apply the white jade. The weapon now deals bludgeoning damage.', 4),
    (9308, 3848, 18, 1, 0x38001008, 'turquoise', 'You apply the turquoise, lowering the item''s Arcane Lore requirement.', NULL),
    (9309, 3848, 18, 1, 0x38001009, 'serpentine', 'You apply the serpentine, lowering the item''s Arcane Lore requirement at the cost of more mana.', NULL),
    (9310, 4452, 30, 2, 0x3800100A, 'tiger eye', 'You apply the tiger eye, imbuing the implement with Elemental Rending.', NULL),
    (9311, 3848, 28, 1, 0x3800100B, 'teak', 'You apply the teak.', NULL),
    (9312, 3848, 28, 1, 0x3800100C, 'ebony', 'You apply the ebony.', NULL),
    -- the element changes on casters use Magic Item Tinkering
    (9313, 3848, 30, 1, 0x38001001, 'yellow garnet', 'You apply the yellow garnet. The implement now casts with fire.', 16),
    (9314, 3848, 30, 1, 0x38001002, 'white quartz', 'You apply the white quartz. The implement now casts with cold.', 8),
    (9315, 3848, 30, 1, 0x38001003, 'green jade', 'You apply the green jade. The implement now casts with acid.', 32),
    (9316, 3848, 30, 1, 0x38001004, 'amethyst', 'You apply the amethyst. The implement now casts with lightning.', 64);

INSERT INTO recipe (id, unknown_1, skill, difficulty, salvage_Type, success_W_C_I_D, success_Amount, success_Message,
    fail_W_C_I_D, fail_Amount, fail_Message,
    success_Destroy_Source_Chance, success_Destroy_Source_Amount, success_Destroy_Source_Message,
    success_Destroy_Target_Chance, success_Destroy_Target_Amount, success_Destroy_Target_Message,
    fail_Destroy_Source_Chance, fail_Destroy_Source_Amount, fail_Destroy_Source_Message,
    fail_Destroy_Target_Chance, fail_Destroy_Target_Amount, fail_Destroy_Target_Message,
    data_Id, last_Modified)
SELECT a.recipe_Id, t.unknown_1, a.skill, t.difficulty, a.salvage_Type, 0, 0, a.success_Message,
    0, 0, CONCAT('You apply the ', a.material, ', but in the process you destroy the target.'),
    t.success_Destroy_Source_Chance, t.success_Destroy_Source_Amount, t.success_Destroy_Source_Message,
    t.success_Destroy_Target_Chance, t.success_Destroy_Target_Amount, t.success_Destroy_Target_Message,
    t.fail_Destroy_Source_Chance, t.fail_Destroy_Source_Amount, t.fail_Destroy_Source_Message,
    t.fail_Destroy_Target_Chance, t.fail_Destroy_Target_Amount, t.fail_Destroy_Target_Message,
    t.data_Id, NOW()
FROM acx_remap a JOIN recipe t ON t.id = a.template_Id;

-- the template's two mods: the one with a mutation (now the ACX effect) and the one that records the tinkerer's name
INSERT INTO recipe_mod (recipe_Id, executes_On_Success, health, stamina, mana, unknown_7, data_Id, unknown_9, instance_Id)
SELECT a.recipe_Id, m.executes_On_Success, m.health, m.stamina, m.mana, m.unknown_7, IF(m.data_Id <> 0, a.data_Id, 0), m.unknown_9, m.instance_Id
FROM acx_remap a JOIN recipe_mod m ON m.recipe_Id = a.template_Id
ORDER BY a.recipe_Id, m.id;

INSERT INTO recipe_mods_string (recipe_Mod_Id, `index`, stat, value, enum, source)
SELECT n.id, s.`index`, s.stat, s.value, s.enum, s.source
FROM acx_remap a
JOIN recipe_mod n ON n.recipe_Id = a.recipe_Id
JOIN recipe_mod m ON m.recipe_Id = a.template_Id AND (m.data_Id <> 0) = (n.data_Id <> 0)
JOIN recipe_mods_string s ON s.recipe_Mod_Id = m.id;

INSERT INTO recipe_mods_d_i_d (recipe_Mod_Id, `index`, stat, value, enum, source)
SELECT n.id, d.`index`, d.stat, d.value, d.enum, d.source
FROM acx_remap a
JOIN recipe_mod n ON n.recipe_Id = a.recipe_Id
JOIN recipe_mod m ON m.recipe_Id = a.template_Id AND (m.data_Id <> 0) = (n.data_Id <> 0)
JOIN recipe_mods_d_i_d d ON d.recipe_Mod_Id = m.id;

-- requirements: the template's (workmanship, 10 tinks, a full bag, and for the imbue: not imbued already), plus
-- "not already that damage type" for the changes and "has an Arcane Lore requirement" for Turquoise and Serpentine
INSERT INTO recipe_requirements_int (recipe_Id, `index`, stat, value, enum, message)
SELECT a.recipe_Id, r.`index`, r.stat, r.value, r.enum, r.message
FROM acx_remap a JOIN recipe_requirements_int r ON r.recipe_Id = a.template_Id;

INSERT INTO recipe_requirements_int (recipe_Id, `index`, stat, value, enum, message)
SELECT recipe_Id, 0, 45, damage_Type, 6, 'The target item already deals that type of damage!'   -- DamageType Equal
FROM acx_remap WHERE damage_Type IS NOT NULL;

INSERT INTO recipe_requirements_int (recipe_Id, `index`, stat, value, enum, message)
SELECT recipe_Id, 0, 109, 1, 2, 'The target item has no Arcane Lore requirement!'            -- ItemDifficulty LessThan 1
FROM acx_remap WHERE recipe_Id IN (9308, 9309);

-- an item's damage type can be changed once (AcxDamageTypeChanged 30003, set by the code)
INSERT INTO recipe_requirements_bool (recipe_Id, `index`, stat, value, enum, message)
SELECT recipe_Id, 0, 30003, 1, 6, 'This item''s damage type has already been changed!'        -- Equal true
FROM acx_remap WHERE damage_Type IS NOT NULL;

-- the client only offers a salvage on the item types in its TargetType (int 94):
-- MeleeWeapon 0x1, Armor 0x2, Clothing 0x4, Jewelry 0x8, MissileWeapon 0x100, Caster 0x8000
DROP TEMPORARY TABLE IF EXISTS acx_remap_targets;
CREATE TEMPORARY TABLE acx_remap_targets (material INT PRIMARY KEY, target_Type INT NOT NULL, description TEXT NOT NULL);
INSERT INTO acx_remap_targets VALUES
    (48, 0x8101, 'Apply this material to a treasure-generated weapon or magic-casting implement to change its damage type to fire. Resistance cleaving on the item changes to fire as well.'),
    (46, 0x8101, 'Apply this material to a treasure-generated weapon or magic-casting implement to change its damage type to cold. Resistance cleaving on the item changes to cold as well.'),
    (24, 0x8101, 'Apply this material to a treasure-generated weapon or magic-casting implement to change its damage type to acid. Resistance cleaving on the item changes to acid as well.'),
    (12, 0x8101, 'Apply this material to a treasure-generated weapon or magic-casting implement to change its damage type to lightning. Resistance cleaving on the item changes to lightning as well.'),
    (32, 0x0101, 'Apply this material to a treasure-generated weapon to change its damage type to piercing. Resistance cleaving on the weapon changes to piercing as well.'),
    (43, 0x0101, 'Apply this material to a treasure-generated weapon to change its damage type to slashing. Resistance cleaving on the weapon changes to slashing as well.'),
    (45, 0x0101, 'Apply this material to a treasure-generated weapon to change its damage type to bludgeoning. Resistance cleaving on the weapon changes to bludgeoning as well.'),
    (44, 0x810F, 'Apply this material to a treasure-generated item to lower its Arcane Lore requirement by 5.'),
    (71, 0x810F, 'Apply this material to a treasure-generated item to lower its Arcane Lore requirement by 10. The item''s mana cost doubles.'),
    (42, 0x8000, 'Apply this material to a treasure-generated magic-casting implement to imbue it with Elemental Rending: fire, cold, acid and lightning rending together. Rending makes the opponent vulnerable to those elements; the amount of vulnerability depends on the attack skill of the wielder. This effect does not stack with vulnerability spells.'),
    (77, 0x0001, 'Apply this material to a treasure-generated melee weapon to increase the weapon''s missile defense bonus by 1%.'),
    (73, 0x0001, 'Apply this material to a treasure-generated melee weapon to increase the weapon''s magic defense bonus by 1%.');

DROP TEMPORARY TABLE IF EXISTS acx_remap_bags;
CREATE TEMPORARY TABLE acx_remap_bags AS
SELECT m.object_Id AS wcid, t.target_Type, t.description
FROM weenie_properties_int m
JOIN weenie_properties_int it ON it.object_Id = m.object_Id AND it.type = 1 AND it.value = 0x40000000   -- TinkeringMaterial
JOIN acx_remap_targets t ON t.material = m.value
WHERE m.type = 131;

INSERT INTO weenie_properties_int (object_Id, type, value)
SELECT wcid, 94, target_Type FROM acx_remap_bags
ON DUPLICATE KEY UPDATE value = VALUES(value);

-- retail made the unused bags not usable (ItemUseable 1); working salvage is 0x80008 (from the pack onto an item)
INSERT INTO weenie_properties_int (object_Id, type, value)
SELECT wcid, 16, 0x80008 FROM acx_remap_bags
ON DUPLICATE KEY UPDATE value = VALUES(value);

INSERT INTO weenie_properties_string (object_Id, type, value)
SELECT wcid, 14, description FROM acx_remap_bags
ON DUPLICATE KEY UPDATE value = VALUES(value);
