-- Project ACX (2026-11-14): Decorative piles for the vault rooms (JJ: "add the gold piles and any
-- other piles you can find so I can make them look different per room. i'll drop the pile then put the chest in it";
-- "the flatter to the ground the better"). Fixed scenery: can't be picked up, worth nothing, walk-through (ethereal), so
-- a chest can sit in a pile and players can step in to reach it. Retail models scaled to about 1.5-2 m across:
--   900740/900741 gold heap (retail Treasure 22614's model) at 35% / 50%; 900742/900743 the other gold heap (24518) at
--   40% / 55%; 900744 a hoard (Yaruldi's Hoard's model); 900745/900746 rubble; 900747/900748 bone piles; 900749 trunk
--   of bones; 900750 pile of parts; 900751 pile of presents. JJ places them with ACX Foundry (Concordia tab).
-- Re-runnable.
DELETE FROM weenie WHERE class_Id BETWEEN 900740 AND 900751;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) VALUES
    (900740, 'acx-pile-gold-small', 1, NOW()),
    (900741, 'acx-pile-gold', 1, NOW()),
    (900742, 'acx-pile-gold-mound-small', 1, NOW()),
    (900743, 'acx-pile-gold-mound', 1, NOW()),
    (900744, 'acx-pile-hoard', 1, NOW()),
    (900745, 'acx-pile-rubble', 1, NOW()),
    (900746, 'acx-pile-tomb-rubble', 1, NOW()),
    (900747, 'acx-pile-bones', 1, NOW()),
    (900748, 'acx-pile-bones-2', 1, NOW()),
    (900749, 'acx-pile-trunk-bones', 1, NOW()),
    (900750, 'acx-pile-parts', 1, NOW()),
    (900751, 'acx-pile-presents', 1, NOW());
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES
    (900740, 1, 128), (900740, 5, 0), (900740, 16, 1), (900740, 19, 0), (900740, 93, 1044),
    (900741, 1, 128), (900741, 5, 0), (900741, 16, 1), (900741, 19, 0), (900741, 93, 1044),
    (900742, 1, 128), (900742, 5, 0), (900742, 16, 1), (900742, 19, 0), (900742, 93, 1044),
    (900743, 1, 128), (900743, 5, 0), (900743, 16, 1), (900743, 19, 0), (900743, 93, 1044),
    (900744, 1, 128), (900744, 5, 0), (900744, 16, 1), (900744, 19, 0), (900744, 93, 1044),
    (900745, 1, 128), (900745, 5, 0), (900745, 16, 1), (900745, 19, 0), (900745, 93, 1044),
    (900746, 1, 128), (900746, 5, 0), (900746, 16, 1), (900746, 19, 0), (900746, 93, 1044),
    (900747, 1, 128), (900747, 5, 0), (900747, 16, 1), (900747, 19, 0), (900747, 93, 1044),
    (900748, 1, 128), (900748, 5, 0), (900748, 16, 1), (900748, 19, 0), (900748, 93, 1044),
    (900749, 1, 128), (900749, 5, 0), (900749, 16, 1), (900749, 19, 0), (900749, 93, 1044),
    (900750, 1, 128), (900750, 5, 0), (900750, 16, 1), (900750, 19, 0), (900750, 93, 1044),
    (900751, 1, 128), (900751, 5, 0), (900751, 16, 1), (900751, 19, 0), (900751, 93, 1044);   -- misc, no burden, not usable, no value, ethereal
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES
    (900740, 1, 1),
    (900741, 1, 1),
    (900742, 1, 1),
    (900743, 1, 1),
    (900744, 1, 1),
    (900745, 1, 1),
    (900746, 1, 1),
    (900747, 1, 1),
    (900748, 1, 1),
    (900749, 1, 1),
    (900750, 1, 1),
    (900751, 1, 1);   -- Stuck
INSERT INTO weenie_properties_float (object_Id, type, value) VALUES
    (900740, 39, 0.35),
    (900741, 39, 0.5),
    (900742, 39, 0.4),
    (900743, 39, 0.55),
    (900744, 39, 0.8),
    (900745, 39, 1.5),
    (900746, 39, 0.8),
    (900747, 39, 1.8),
    (900748, 39, 1.8),
    (900749, 39, 0.7),
    (900750, 39, 2.0),
    (900751, 39, 0.45);   -- DefaultScale
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES
    (900740, 1, 0x02000EC4), (900740, 3, 0x20000014), (900740, 8, 0x06002A24), (900740, 22, 0x3400002B),
    (900741, 1, 0x02000EC4), (900741, 3, 0x20000014), (900741, 8, 0x06002A24), (900741, 22, 0x3400002B),
    (900742, 1, 0x02000F79), (900742, 3, 0x20000014), (900742, 8, 0x06002A24), (900742, 22, 0x3400002B),
    (900743, 1, 0x02000F79), (900743, 3, 0x20000014), (900743, 8, 0x06002A24), (900743, 22, 0x3400002B),
    (900744, 1, 0x02001093), (900744, 3, 0x20000014), (900744, 8, 0x06002A24), (900744, 22, 0x3400002B),
    (900745, 1, 0x020008FF), (900745, 3, 0x20000014), (900745, 8, 0x06002A24), (900745, 22, 0x3400002B),
    (900746, 1, 0x02001024), (900746, 3, 0x20000014), (900746, 8, 0x06002A24), (900746, 22, 0x3400002B),
    (900747, 1, 0x020003CC), (900747, 3, 0x20000014), (900747, 8, 0x06002A24), (900747, 22, 0x3400002B),
    (900748, 1, 0x020003CD), (900748, 3, 0x20000014), (900748, 8, 0x06002A24), (900748, 22, 0x3400002B),
    (900749, 1, 0x02000F03), (900749, 3, 0x20000014), (900749, 8, 0x06002A24), (900749, 22, 0x3400002B),
    (900750, 1, 0x02001906), (900750, 3, 0x20000014), (900750, 8, 0x06002A24), (900750, 22, 0x3400002B),
    (900751, 1, 0x02001769), (900751, 3, 0x20000014), (900751, 8, 0x06002A24), (900751, 22, 0x3400002B);   -- Setup, sounds, icon, effects
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900740, 1, 'Pile of Gold'),
    (900741, 1, 'Heap of Gold'),
    (900742, 1, 'Pile of Gold'),
    (900743, 1, 'Mound of Gold'),
    (900744, 1, 'Hoard'),
    (900745, 1, 'Rubble'),
    (900746, 1, 'Rubble'),
    (900747, 1, 'Pile of Bones'),
    (900748, 1, 'Pile of Bones'),
    (900749, 1, 'Trunk of Bones'),
    (900750, 1, 'Pile of Parts'),
    (900751, 1, 'Pile of Presents');
