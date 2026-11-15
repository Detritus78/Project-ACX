-- Project ACX (2026-11-14): furniture and animals for building gambling halls in Concordia (JJ: "gambling hall
-- decorations from ghu and aluvian", "if we have counter tops and all that that would be great. some fence posts I wanna
-- put a permanent cow and a couple of chickens"), placed with the ACX Foundry Gambling tab. Fixed scenery like the
-- piles (70-decor-piles.sql): can't be picked up, worth nothing, walk-through.
--   900771-900776: models built into the retail Aluvian and Gharu'ndim halls (they have no placeable version): a tall
--     iron-bound crate, a plank crate, two big wooden fixtures (the large one's model is centred on its middle, so it
--     needs nudging up 1.25 m after placing), and two standing paper lanterns.
--   900777-900787: fixed copies of retail's placeable Desk, Bench, Stool, Keg, Crate, Lamp and Brazier, and the static
--     Cow and Chicken models (two of each), so players can't carry them off.
--   900789: a small Wheel of Fortune just for show (JJ: "I want to place a mini non functional wheel of fortune"); the
--     working one is 900788 (90-wheel-of-fortune.sql).
-- No fence or counter-top models exist as whole objects (they're parts of the map), so none here.
-- Re-runnable.
DELETE FROM weenie WHERE class_Id BETWEEN 900771 AND 900787 OR class_Id = 900789;   -- not 900788, the Wheel of Fortune game (90-wheel-of-fortune.sql)
DROP TEMPORARY TABLE IF EXISTS acx_hall_pieces;
CREATE TEMPORARY TABLE acx_hall_pieces (class_Id INT UNSIGNED PRIMARY KEY, class_Name VARCHAR(100), name VARCHAR(100), setup INT UNSIGNED, icon INT UNSIGNED, scale DOUBLE, placement INT);
INSERT INTO acx_hall_pieces VALUES
    (900771, 'acx-hall-tall-crate',     'Tall Crate',             0x02000267, 0x060012F8, NULL, NULL),
    (900772, 'acx-hall-plank-crate',    'Plank Crate',            0x020002FD, 0x060012F8, NULL, 0),   -- its first pose is another shape
    (900773, 'acx-hall-fixture-large',  'Large Wooden Fixture',   0x020004DE, 0x0600211D, NULL, NULL),
    (900774, 'acx-hall-fixture',        'Wooden Fixture',         0x020004DF, 0x0600211D, NULL, NULL),
    (900775, 'acx-hall-lantern-tall',   'Tall Paper Lantern',     0x020005D8, 0x060012FF, NULL, NULL),
    (900776, 'acx-hall-lantern',        'Paper Lantern',          0x020005D9, 0x060012FF, NULL, NULL),
    (900777, 'acx-hall-table',          'Table',                  0x02000272, 0x0600211D, NULL, NULL),   -- retail Desk 276
    (900778, 'acx-hall-bench',          'Bench',                  0x02000120, 0x060012CF, NULL, NULL),   -- Bench 268
    (900779, 'acx-hall-stool',          'Stool',                  0x02000123, 0x0600214B, 1.25, NULL),   -- Stool 253
    (900780, 'acx-hall-keg',            'Keg',                    0x020000A5, 0x06001027, NULL, NULL),   -- Keg 157
    (900781, 'acx-hall-crate',          'Crate',                  0x0200011E, 0x060012F8, NULL, NULL),   -- Crate 147
    (900782, 'acx-hall-lamp',           'Lamp',                   0x0200010B, 0x060012FF, NULL, NULL),   -- Lamp 283
    (900783, 'acx-hall-brazier',        'Brazier',                0x02000104, 0x060012D0, 0.75, NULL),   -- Brazier 239
    (900784, 'acx-farm-cow',            'Cow',                    0x020019DF, 0x060012D3, NULL, NULL),   -- Cow 42916
    (900785, 'acx-farm-cow-2',          'Cow',                    0x02001AF1, 0x060012D3, 0.9, NULL),    -- Cow 44729
    (900786, 'acx-farm-chicken',        'Chicken',                0x020019F1, 0x060012D3, NULL, NULL),   -- Chicken 43015
    (900787, 'acx-farm-chicken-2',      'Chicken',                0x02001AEF, 0x060012D3, 0.9, NULL),    -- Chicken 44727
    (900789, 'acx-hall-mini-wheel',     'Wheel of Fortune',       0x02000A31, 0x060010E8, 0.4, NULL);     -- the Wheel of Fortune's model, small and just for show
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT class_Id, class_Name, 1, NOW() FROM acx_hall_pieces;
INSERT INTO weenie_properties_int (object_Id, type, value)
SELECT h.class_Id, p.type, p.value FROM acx_hall_pieces h
CROSS JOIN (SELECT 1 type, 128 value UNION ALL SELECT 5, 0 UNION ALL SELECT 16, 1 UNION ALL SELECT 19, 0 UNION ALL SELECT 93, 1044) p;   -- misc, no burden, not usable, no value, walk-through
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 65, placement FROM acx_hall_pieces WHERE placement IS NOT NULL;   -- Placement
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT class_Id, 1, 1 FROM acx_hall_pieces;   -- Stuck
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT class_Id, 39, scale FROM acx_hall_pieces WHERE scale IS NOT NULL;   -- DefaultScale
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT class_Id, 1, setup FROM acx_hall_pieces;        -- Setup
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT class_Id, 8, icon FROM acx_hall_pieces;         -- Icon
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT class_Id, 22, 0x3400002B FROM acx_hall_pieces;  -- effects
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT class_Id, 1, name FROM acx_hall_pieces;
DROP TEMPORARY TABLE acx_hall_pieces;
