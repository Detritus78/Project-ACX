-- Project ACX (2026-11-15): the Mark Trader's specials (JJ: "special back packs, eternal mana charge, eternal heal kit,
-- mana kit, stamina kit, eternal arrows what else..??? Slayer imbues a eternal concordia portal gem", "Eternal dyes",
-- "special salvage barrel that holds a lot of salvage", then: arrows "the same damage as prismatic instead of deadly
-- prismatic", portal gem "10 second cool down", "Backpacks the same as your main inventory on the character. And a
-- salvage pack double that", "Yes on eternal lockpick yes on eternal recall gems. make the gem use instant use no
-- animation", "We can adjust prices later"). He sells for ACX Marks at each item's Value (prices are placeholders).
--
-- Retail's own eternal items, sold here, Value = price:
--   30247-30249  Eternal Health / Mana / Stamina Kit (unlimited, +60% healing bonus)     15
--   30254        Eternal Mana Charge                                                     15
--   30253        Limitless Lockpick (the eternal lockpick)                               10
--   30151 30165  Rune of Lifestone Recall, Rune of Portal Recall (unlimited, instant, 5 s cooldown)   10
--   30082-30091  the nine Perennial dyes (never used up; not the Botched one)            5
-- New (900860-900879):
--   900860       Eternal Concordia Portal Gem: retail's Concordia Portal Gem (43020), but a recall to Concordia instead of
--                a summoned portal (PropertyBool AcxConcordiaRecall, Player.AcxConcordiaRecall; JJ: "it isnt a recall"),
--                never used up, 10 s cooldown                                                                    20
--   900861-2     Rune of Primary / Secondary Portal Recall: the Rune of Portal Recall for the tied portals        10
--   900863-5     Eternal Arrow / Quarrel / Atlatl Dart: Prismatic ammo (tier 5-6), one item, never used up      20
--   900866       Salvage Barrel: a 120-slot pouch for salvage only, safe on death (Container_AcxPouch): JJ asked for
--                double a main pack (204), but AC:Unreal handles about 20 rows of 6 (JJ: "6x20 I think is the max")    30
--   900870-9     Slayer stones: Olthoi, Undead, Tumerok, Golem, Lugian, Virindi, Banderling, Drudge, Mosswart,
--                Shadow; retail's Greater Mukkir Slayer Stone (33688) and its recipe (8752, 543 weapons) with the
--                creature changed and the bonus +50% (retail's middle Mukkir stone; Greater is +75%, so quest stones
--                stay ahead: JJ 2026-11-15), recipes 9400-9409                                                    10
-- The Eternal Pack (900711, 20-mark-vendor.sql) holds 102 like a character's main pack.
-- Re-runnable. Restart the server after applying.

-- retail items: price
UPDATE weenie_properties_int SET value = 15 WHERE type = 19 AND object_Id IN (30247, 30248, 30249, 30254);
UPDATE weenie_properties_int SET value = 10 WHERE type = 19 AND object_Id IN (30253, 30151, 30165);
UPDATE weenie_properties_int SET value = 5 WHERE type = 19 AND object_Id BETWEEN 30082 AND 30091 AND object_Id <> 30084;
INSERT INTO weenie_properties_int (object_Id, type, value)
SELECT w.class_Id, 19, IF(w.class_Id IN (30247, 30248, 30249, 30254), 15, IF(w.class_Id IN (30253, 30151, 30165), 10, 5))
FROM weenie w WHERE (w.class_Id IN (30247, 30248, 30249, 30254, 30253, 30151, 30165) OR (w.class_Id BETWEEN 30082 AND 30091 AND w.class_Id <> 30084))
  AND NOT EXISTS (SELECT 1 FROM weenie_properties_int p WHERE p.object_Id = w.class_Id AND p.type = 19);

-- new items: copies of a retail weenie
DELETE FROM weenie WHERE class_Id BETWEEN 900860 AND 900879;
DROP TEMPORARY TABLE IF EXISTS acx_special;
CREATE TEMPORARY TABLE acx_special (class_Id INT UNSIGNED PRIMARY KEY, original INT UNSIGNED, class_Name VARCHAR(100), name VARCHAR(100), price INT, descr TEXT);
INSERT INTO acx_special VALUES
    (900860, 43020, 'acx-eternal-concordia-gem', 'Eternal Concordia Portal Gem', 20, 'Recalls you to Concordia. Never used up; it can be used again 10 seconds later. Traded for ACX Marks.'),
    (900861, 30165, 'acx-rune-primary-portal-recall', 'Rune of Primary Portal Recall', 10, 'Recalls you to the portal you tied as your primary. Never used up. Traded for ACX Marks.'),
    (900862, 30165, 'acx-rune-secondary-portal-recall', 'Rune of Secondary Portal Recall', 10, 'Recalls you to the portal you tied as your secondary. Never used up. Traded for ACX Marks.'),
    (900863, 43952, 'acx-eternal-arrow', 'Eternal Arrow', 20, 'An arrow that returns to your quiver: as strong as a Prismatic Arrow, and never used up. Traded for ACX Marks.'),
    (900864, 43958, 'acx-eternal-quarrel', 'Eternal Quarrel', 20, 'A quarrel that returns to your quiver: as strong as a Prismatic Quarrel, and never used up. Traded for ACX Marks.'),
    (900865, 43955, 'acx-eternal-atlatl-dart', 'Eternal Atlatl Dart', 20, 'A dart that returns to your quiver: as strong as a Prismatic Atlatl Dart, and never used up. Traded for ACX Marks.'),
    (900870, 33688, 'acx-slayer-stone-olthoi', 'Olthoi Slayer Stone', 10, NULL),
    (900871, 33688, 'acx-slayer-stone-undead', 'Undead Slayer Stone', 10, NULL),
    (900872, 33688, 'acx-slayer-stone-tumerok', 'Tumerok Slayer Stone', 10, NULL),
    (900873, 33688, 'acx-slayer-stone-golem', 'Golem Slayer Stone', 10, NULL),
    (900874, 33688, 'acx-slayer-stone-lugian', 'Lugian Slayer Stone', 10, NULL),
    (900875, 33688, 'acx-slayer-stone-virindi', 'Virindi Slayer Stone', 10, NULL),
    (900876, 33688, 'acx-slayer-stone-banderling', 'Banderling Slayer Stone', 10, NULL),
    (900877, 33688, 'acx-slayer-stone-drudge', 'Drudge Slayer Stone', 10, NULL),
    (900878, 33688, 'acx-slayer-stone-mosswart', 'Mosswart Slayer Stone', 10, NULL),
    (900879, 33688, 'acx-slayer-stone-shadow', 'Shadow Slayer Stone', 10, NULL);

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT o.class_Id, o.class_Name, w.type, NOW() FROM acx_special o JOIN weenie w ON w.class_Id = o.original;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_special o JOIN weenie_properties_int p ON p.object_Id = o.original WHERE p.type NOT IN (11, 12, 15, 19, 33, 114);   -- not stack sizes, StackUnitValue, Value, Bonded, Attuned
    -- (a stackable's Value is StackUnitValue x StackSize, Stackable.cs; with it the gem cost retail's 500)
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_special o JOIN weenie_properties_int64 p ON p.object_Id = o.original;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_special o JOIN weenie_properties_float p ON p.object_Id = o.original WHERE NOT (o.class_Id = 900860 AND p.type = 167);
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_special o JOIN weenie_properties_bool p ON p.object_Id = o.original WHERE p.type <> 63;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_special o JOIN weenie_properties_string p ON p.object_Id = o.original WHERE p.type NOT IN (1, 16, 20);   -- not the name, LongDesc, PluralName
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_special o JOIN weenie_properties_d_i_d p ON p.object_Id = o.original WHERE NOT (o.class_Id IN (900860, 900861, 900862) AND p.type = 28);   -- not the spell: the runes get their own, the Concordia gem none
INSERT INTO weenie_properties_i_i_d (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_special o JOIN weenie_properties_i_i_d p ON p.object_Id = o.original;
INSERT INTO weenie_properties_spell_book (object_Id, spell, probability) SELECT o.class_Id, p.spell, p.probability FROM acx_special o JOIN weenie_properties_spell_book p ON p.object_Id = o.original;

INSERT INTO weenie_properties_string (object_Id, type, value) SELECT class_Id, 1, name FROM acx_special;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT class_Id, 16, descr FROM acx_special WHERE descr IS NOT NULL;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 19, price FROM acx_special;              -- Value = price in ACX Marks
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 11, 1 FROM acx_special WHERE class_Id <= 900865;   -- MaxStackSize: one item, not a stack
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 12, 1 FROM acx_special WHERE class_Id <= 900865;   -- StackSize
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT class_Id, 63, 1 FROM acx_special WHERE class_Id <= 900865;  -- UnlimitedUse: never used up
INSERT INTO weenie_properties_float (object_Id, type, value) VALUES (900860, 167, 10);                                         -- CooldownDuration: 10 s (cooldown group 1000, the Concordia gem's)
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES (900861, 28, 48), (900862, 28, 2647);                     -- SpellDID: Primary / Secondary Portal Recall
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (900860, 30014, 1);                                         -- AcxConcordiaRecall: a recall, not retail's portal summon

-- the Salvage Barrel: a pouch (Container_AcxPouch) like the Spell Component Pouch (crafting/20-pouches.sql), 120 slots
-- (the client's most), holding only salvage (ItemType TinkeringMaterial), with retail's Barrel (173) for its look
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) VALUES (900866, 'acx-salvage-barrel', 21, NOW());
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES
    (900866, 1, 512), (900866, 5, 15), (900866, 6, 120), (900866, 8, 200), (900866, 9, 0), (900866, 16, 56), (900866, 19, 30), (900866, 93, 1044),
    (900866, 30011, 1073741824);   -- AcxPouchItemTypes: salvage
INSERT INTO weenie_properties_float (object_Id, type, value) VALUES (900866, 39, 1.75);
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES (900866, 1, 0x02000152), (900866, 3, 0x20000014), (900866, 22, 0x3400002B);
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900866, 8, value FROM weenie_properties_d_i_d WHERE object_Id = 173 AND type = 8;   -- the barrel icon
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900866, 1, 'Salvage Barrel'),
    (900866, 16, 'Holds only salvage: 120 bags, more than a main pack. A barrel and what''s in it are safe when you die. Traded for ACX Marks.');

-- the slayer stones' recipes: retail's 8752 (Greater Mukkir Slayer Stone) with the creature and message changed
DROP TEMPORARY TABLE IF EXISTS acx_slayer;
CREATE TEMPORARY TABLE acx_slayer (stone INT UNSIGNED PRIMARY KEY, recipe INT UNSIGNED, creature INT, plural VARCHAR(40));
INSERT INTO acx_slayer VALUES
    (900870, 9400, 1, 'Olthoi'), (900871, 9401, 14, 'Undead'), (900872, 9402, 6, 'Tumeroks'), (900873, 9403, 13, 'Golems'),
    (900874, 9404, 5, 'Lugians'), (900875, 9405, 19, 'Virindi'), (900876, 9406, 2, 'Banderlings'), (900877, 9407, 3, 'Drudges'),
    (900878, 9408, 4, 'Mosswarts'), (900879, 9409, 22, 'Shadows');

DELETE FROM cook_book WHERE recipe_Id BETWEEN 9400 AND 9409;
DELETE i FROM recipe_mods_int i JOIN recipe_mod m ON m.id = i.recipe_Mod_Id WHERE m.recipe_Id BETWEEN 9400 AND 9409;
DELETE f FROM recipe_mods_float f JOIN recipe_mod m ON m.id = f.recipe_Mod_Id WHERE m.recipe_Id BETWEEN 9400 AND 9409;
DELETE s FROM recipe_mods_string s JOIN recipe_mod m ON m.id = s.recipe_Mod_Id WHERE m.recipe_Id BETWEEN 9400 AND 9409;
DELETE FROM recipe_mod WHERE recipe_Id BETWEEN 9400 AND 9409;
DELETE FROM recipe_requirements_int WHERE recipe_Id BETWEEN 9400 AND 9409;
DELETE FROM recipe WHERE id BETWEEN 9400 AND 9409;

INSERT INTO recipe (id, unknown_1, skill, difficulty, salvage_Type, success_W_C_I_D, success_Amount, success_Message, fail_W_C_I_D, fail_Amount, fail_Message,
    success_Destroy_Source_Chance, success_Destroy_Source_Amount, success_Destroy_Source_Message, success_Destroy_Target_Chance, success_Destroy_Target_Amount, success_Destroy_Target_Message,
    fail_Destroy_Source_Chance, fail_Destroy_Source_Amount, fail_Destroy_Source_Message, fail_Destroy_Target_Chance, fail_Destroy_Target_Amount, fail_Destroy_Target_Message, data_Id, last_Modified)
SELECT s.recipe, r.unknown_1, r.skill, r.difficulty, r.salvage_Type, r.success_W_C_I_D, r.success_Amount, CONCAT('You enchant the weapon with the ability to slay ', s.plural, '.'), r.fail_W_C_I_D, r.fail_Amount, r.fail_Message,
    r.success_Destroy_Source_Chance, r.success_Destroy_Source_Amount, r.success_Destroy_Source_Message, r.success_Destroy_Target_Chance, r.success_Destroy_Target_Amount, r.success_Destroy_Target_Message,
    r.fail_Destroy_Source_Chance, r.fail_Destroy_Source_Amount, r.fail_Destroy_Source_Message, r.fail_Destroy_Target_Chance, r.fail_Destroy_Target_Amount, r.fail_Destroy_Target_Message, r.data_Id, NOW()
FROM acx_slayer s JOIN recipe r ON r.id = 8752;

INSERT INTO recipe_requirements_int (recipe_Id, `index`, stat, value, enum, message)
SELECT s.recipe, q.`index`, q.stat, q.value, q.enum, q.message FROM acx_slayer s JOIN recipe_requirements_int q ON q.recipe_Id = 8752;

INSERT INTO recipe_mod (recipe_Id, executes_On_Success, health, stamina, mana, unknown_7, data_Id, unknown_9, instance_Id)
SELECT s.recipe, m.executes_On_Success, m.health, m.stamina, m.mana, m.unknown_7, m.data_Id, m.unknown_9, m.instance_Id FROM acx_slayer s JOIN recipe_mod m ON m.recipe_Id = 8752;

INSERT INTO recipe_mods_int (recipe_Mod_Id, `index`, stat, value, enum, source)
SELECT nm.id, i.`index`, i.stat, IF(i.stat = 166, s.creature, i.value), i.enum, i.source   -- 166 SlayerCreatureType
FROM acx_slayer s JOIN recipe_mod nm ON nm.recipe_Id = s.recipe JOIN recipe_mod om ON om.recipe_Id = 8752 JOIN recipe_mods_int i ON i.recipe_Mod_Id = om.id;
INSERT INTO recipe_mods_float (recipe_Mod_Id, `index`, stat, value, enum, source)
SELECT nm.id, f.`index`, f.stat, IF(f.stat = 138, 1.5, f.value), f.enum, f.source   -- 138 SlayerDamageBonus: +50%
FROM acx_slayer s JOIN recipe_mod nm ON nm.recipe_Id = s.recipe JOIN recipe_mod om ON om.recipe_Id = 8752 JOIN recipe_mods_float f ON f.recipe_Mod_Id = om.id;
INSERT INTO recipe_mods_string (recipe_Mod_Id, `index`, stat, value, enum, source)
SELECT nm.id, t.`index`, t.stat, t.value, t.enum, t.source
FROM acx_slayer s JOIN recipe_mod nm ON nm.recipe_Id = s.recipe JOIN recipe_mod om ON om.recipe_Id = 8752 JOIN recipe_mods_string t ON t.recipe_Mod_Id = om.id;

INSERT INTO cook_book (recipe_Id, source_W_C_I_D, target_W_C_I_D, last_Modified)
SELECT s.recipe, s.stone, c.target_W_C_I_D, NOW() FROM acx_slayer s JOIN cook_book c ON c.source_W_C_I_D = 33688 AND c.recipe_Id = 8752;
-- and ACX's own weapons (the nether versions, the Eternal thrown): melee, launcher, missile and caster weenies from
-- 900000; the recipe still wants a looted one (workmanship)
INSERT INTO cook_book (recipe_Id, source_W_C_I_D, target_W_C_I_D, last_Modified)
SELECT s.recipe, s.stone, w.class_Id, NOW() FROM acx_slayer s JOIN weenie w ON w.class_Id >= 900000 AND w.type IN (3, 4, 6, 35);

-- slayer stones' description
INSERT INTO weenie_properties_string (object_Id, type, value)
SELECT s.stone, 16, CONCAT('Use it on a looted weapon to let it slay ', s.plural, '. The stone is used up, and a weapon takes only one slayer. Traded for ACX Marks.') FROM acx_slayer s;

-- the Mark Trader sells them all
DELETE FROM weenie_properties_create_list WHERE object_Id = 45501 AND (weenie_Class_Id BETWEEN 900860 AND 900879
    OR weenie_Class_Id IN (30247, 30248, 30249, 30254, 30253, 30151, 30165) OR weenie_Class_Id BETWEEN 30082 AND 30091);
INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond)
SELECT 45501, 4, class_Id, -1, 0, 0, 0 FROM weenie WHERE class_Id BETWEEN 900860 AND 900879
    OR class_Id IN (30247, 30248, 30249, 30254, 30253, 30151, 30165) OR (class_Id BETWEEN 30082 AND 30091 AND class_Id <> 30084)
ORDER BY class_Id;

DROP TEMPORARY TABLE acx_special;
DROP TEMPORARY TABLE acx_slayer;
