-- Project ACX: the ACX Mark (JJ 2026-11-13), the tradeable currency for coin-only goods (eternal items, big bags).
-- One per event: one per player from an elite invader's corpse (it carries 10), one per exploration marker, contract
-- and job, and 25% of Grand Casino Chest openings (30-casino-chest.sql); treasure maps later. Spent at the ACX Mark vendor in Arwic (acx/Content/invasions/20-mark-vendor.sql).
-- Looks like retail's Rare Coin (45493, the Arwic rare-weapon swap, left alone), but tradeable, stacks to 1,000,
-- and vendors won't buy it.
--   900710  ACX Mark
-- Re-runnable. Restart the server after applying.

DELETE FROM weenie WHERE class_Id = 900710;

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) VALUES (900710, 'acx-mark', 51, NOW());

INSERT INTO weenie_properties_int (object_Id, type, value) VALUES
    (900710,   1, 128),    -- ItemType: misc
    (900710,   5, 1),      -- EncumbranceVal
    (900710,  11, 1000),   -- MaxStackSize
    (900710,  12, 1),      -- StackSize
    (900710,  13, 1),      -- StackUnitEncumbrance
    (900710,  15, 0),      -- StackUnitValue
    (900710,  16, 1),      -- ItemUseable: no
    (900710,  19, 0),      -- Value
    (900710,  93, 1044);   -- PhysicsState: ethereal, ignore collisions, gravity

INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES
    (900710, 69, 0);       -- IsSellable: no

INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES
    (900710,  1, 0x02001709),   -- Setup (Rare Coin)
    (900710,  3, 0x20000014),   -- SoundTable
    (900710,  8, 0x06006635),   -- Icon
    (900710, 22, 0x3400002B),   -- PhysicsEffectTable
    (900710, 50, 0x0600710D);   -- IconOverlay

INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900710,  1, 'ACX Mark'),
    (900710, 15, 'A heavy coin earned the hard way: from elite invaders, the ends of dungeons, contracts and jobs. Traders deal in them for goods pyreals cannot buy.'),
    (900710, 20, 'ACX Marks');
