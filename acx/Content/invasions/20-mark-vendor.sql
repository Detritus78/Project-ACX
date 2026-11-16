-- Project ACX: the ACX Mark vendor and its coin-only goods (JJ 2026-11-13). Retail's Weapon Imbue Vendor in Arwic
-- (45501, a dead end: nothing hands out its Imbue Swap Coins) now trades for ACX Marks (900710) as the Mark Trader. He
-- keeps his retail stock (Foolproof imbue gems, slayer stones, Lucky White Rabbit's Foot, at retail's 1 each) and adds:
--   900711  Eternal Pack: 102 slots like a character's main pack (JJ 2026-11-15: "Backpacks the same as your main
--           inventory on the character"), carryable (retail's 48-slot Pack 30936)
--   900712-900720  Eternal Gold Phials: thrown like retail's Gold Phials (Alchemy 225, Missile Weapons), never used up
-- A vendor's price in its alternate currency is the item's Value; the prices here are placeholders for the money pass.
-- Re-runnable. Restart the server after applying.

DELETE FROM weenie WHERE class_Id BETWEEN 900711 AND 900720;

DROP TEMPORARY TABLE IF EXISTS acx_mark_goods;
CREATE TEMPORARY TABLE acx_mark_goods (class_Id INT UNSIGNED PRIMARY KEY, original INT UNSIGNED, class_Name VARCHAR(100), name VARCHAR(100), price INT);
INSERT INTO acx_mark_goods VALUES
    (900711, 30936, 'acx-eternal-pack',                  'Eternal Pack', 20),
    (900712, 80002, 'acx-eternal-phial-imperil',         'Eternal Gold Phial of Imperil', 10),
    (900713, 81039, 'acx-eternal-phial-fester',          'Eternal Gold Phial of Fester', 10),
    (900714, 81031, 'acx-eternal-phial-acid',            'Eternal Gold Phial of Acid Vulnerability', 10),
    (900715, 81032, 'acx-eternal-phial-bludgeon',        'Eternal Gold Phial of Bludgeon Vulnerability', 10),
    (900716, 81033, 'acx-eternal-phial-cold',            'Eternal Gold Phial of Cold Vulnerability', 10),
    (900717, 81034, 'acx-eternal-phial-fire',            'Eternal Gold Phial of Fire Vulnerability', 10),
    (900718, 81036, 'acx-eternal-phial-lightning',       'Eternal Gold Phial of Lightning Vulnerability', 10),
    (900719, 81037, 'acx-eternal-phial-piercing',        'Eternal Gold Phial of Piercing Vulnerability', 10),
    (900720, 81038, 'acx-eternal-phial-blade',           'Eternal Gold Phial of Blade Vulnerability', 10);

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT o.class_Id, o.class_Name, w.type, NOW() FROM acx_mark_goods o JOIN weenie w ON w.class_Id = o.original;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_mark_goods o JOIN weenie_properties_int p ON p.object_Id = o.original;
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_mark_goods o JOIN weenie_properties_int64 p ON p.object_Id = o.original;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_mark_goods o JOIN weenie_properties_float p ON p.object_Id = o.original;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_mark_goods o JOIN weenie_properties_bool p ON p.object_Id = o.original;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_mark_goods o JOIN weenie_properties_string p ON p.object_Id = o.original;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_mark_goods o JOIN weenie_properties_d_i_d p ON p.object_Id = o.original;
INSERT INTO weenie_properties_i_i_d (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_mark_goods o JOIN weenie_properties_i_i_d p ON p.object_Id = o.original;
INSERT INTO weenie_properties_spell_book (object_Id, spell, probability) SELECT o.class_Id, p.spell, p.probability FROM acx_mark_goods o JOIN weenie_properties_spell_book p ON p.object_Id = o.original;

UPDATE weenie_properties_string s JOIN acx_mark_goods o ON s.object_Id = o.class_Id SET s.value = o.name WHERE s.type = 1;
DELETE p FROM weenie_properties_int p JOIN acx_mark_goods o ON p.object_Id = o.class_Id WHERE p.type IN (19, 33, 114, 11, 12, 15);   -- Value, Bonded, Attuned, MaxStackSize, StackSize,
    -- StackUnitValue (a stackable's Value is StackUnitValue x StackSize, Stackable.cs: the phials cost retail's 250 per unit)
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 19, price FROM acx_mark_goods;   -- Value = price in ACX Marks
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 11, 1 FROM acx_mark_goods WHERE class_Id >= 900712;   -- MaxStackSize: one phial
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 12, 1 FROM acx_mark_goods WHERE class_Id >= 900712;   -- StackSize
UPDATE weenie_properties_int SET value = 102 WHERE object_Id = 900711 AND type = 6;   -- ItemsCapacity: a main pack's 102

-- the phials are never used up (ACE's UnlimitedUse for thrown items)
DELETE p FROM weenie_properties_bool p JOIN acx_mark_goods o ON p.object_Id = o.class_Id WHERE p.type = 63;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT class_Id, 63, 1 FROM acx_mark_goods WHERE class_Id >= 900712;   -- UnlimitedUse

DELETE p FROM weenie_properties_string p JOIN acx_mark_goods o ON p.object_Id = o.class_Id WHERE p.type IN (16, 20);   -- LongDesc, PluralName
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900711, 16, 'A pack of fine waxed hide, roomy enough for 102 items, as much as your main pack. Traded for ACX Marks.');
INSERT INTO weenie_properties_string (object_Id, type, value)
SELECT class_Id, 16, CONCAT('A gold phial whose alchemical mixture never runs dry: throw it as often as you like. ', SUBSTRING_INDEX(name, 'Phial of ', -1), '. Traded for ACX Marks.') FROM acx_mark_goods WHERE class_Id >= 900712;

-- the Mark Trader: retail's Weapon Imbue Vendor, now dealing in ACX Marks. He moves to Concordia's second floor (JJ
-- 2026-11-14: "Mark trader goes on second floor"; JJ places him with ACX Foundry, placement in acx/Content/concordia/),
-- so his retail spot in Arwic (C6A9016E) is emptied.
DELETE FROM landblock_instance WHERE guid = 0x7C6A9049;
DELETE FROM weenie_properties_d_i_d WHERE object_Id = 45501 AND type = 57;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES (45501, 57, 900710);   -- AlternateCurrency: ACX Mark
UPDATE weenie_properties_string SET value = 'Mark Trader' WHERE object_Id = 45501 AND type = 1;
DELETE FROM weenie_properties_create_list WHERE object_Id = 45501 AND weenie_Class_Id BETWEEN 900711 AND 900720;
INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond)
SELECT 45501, 4, class_Id, -1, 0, 0, 0 FROM acx_mark_goods;
