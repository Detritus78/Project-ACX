-- Project ACX (2026-11-14): more decor pieces for Concordia's vault rooms (JJ: "Find new pieces and make variations"),
-- made the same way as 70-decor-piles.sql: fixed scenery that can't be picked up, worth nothing, walk-through. Retail
-- models, sized to sit low on the floor around a vault:
--   900752/900753 rocks (two shapes, about 1.5 m across, 0.4 m high); 900754 a flat rock; 900755 a cave-in rock;
--   900756 skull and bones; 900757 an auroch skull; 900758 a large urn; 900759 a crystal nodule; 900760 a skeleton
--   lying on the floor; and small gears for the hoard room (JJ: "we need something low and unique that we havent used
--   yet", "gears small ones?"): 900761 a pile of Gearknight parts, 900762 a cogwheel, 900763 a bronze gear standing on its
--   edge, 900764 a Gearknight core fragment; and three rugs, one in the middle of each square vault room (JJ: "lets put
--   a different rug in each of the three rooms", "in the middle"): 900765 a bear-skin rug, 900766 a white mattekar hide,
--   900767 a square ursuin rug; the bear rug and the hide at 2.6 and 2.2 times retail's size, at least as big as
--   the square rug at 1.3 (JJ: "need to be much wider and longer. Atleast the same size as the ursuin rug"); JJ then:
--   "the ursuin and the mattekar doe not look good", so 900768 is retail's worn Aluvian Rug (25767: a hanging rug's
--   model, 3.8 m x 5.4 m, lying flat by its Resting placement); JJ: "that looks great ... is there a ghu and sho rug like
--   this": 900769 retail's Gharu'ndim Rug (22254) and, as there is no Sho rug, 900770 the white-and-blue Jubilee Rug
--   (36910), both 2 m rugs at twice that, flat by the same placement. JJ places them with ACX Foundry like the others; 85-vault-rooms.sql builds
--   heaps from them and 87-hoard-room-gears.sql scatters the gears.
-- Re-runnable.
DELETE FROM weenie WHERE class_Id BETWEEN 900752 AND 900770;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) VALUES
    (900752, 'acx-piece-rocks', 1, NOW()),
    (900753, 'acx-piece-rocks-2', 1, NOW()),
    (900754, 'acx-piece-flat-rock', 1, NOW()),
    (900755, 'acx-piece-cave-in-rock', 1, NOW()),
    (900756, 'acx-piece-skull-and-bones', 1, NOW()),
    (900757, 'acx-piece-auroch-skull', 1, NOW()),
    (900758, 'acx-piece-urn', 1, NOW()),
    (900759, 'acx-piece-crystal-nodule', 1, NOW()),
    (900760, 'acx-piece-skeleton', 1, NOW()),
    (900761, 'acx-piece-gearknight-parts', 1, NOW()),
    (900762, 'acx-piece-cogwheel', 1, NOW()),
    (900763, 'acx-piece-bronze-gear', 1, NOW()),
    (900764, 'acx-piece-core-fragment', 1, NOW()),
    (900765, 'acx-piece-bear-rug', 1, NOW()),
    (900766, 'acx-piece-white-hide', 1, NOW()),
    (900767, 'acx-piece-square-rug', 1, NOW()),
    (900768, 'acx-piece-aluvian-rug', 1, NOW()),
    (900769, 'acx-piece-gharundim-rug', 1, NOW()),
    (900770, 'acx-piece-jubilee-rug', 1, NOW());
INSERT INTO weenie_properties_int (object_Id, type, value)
SELECT w.class_Id, p.type, p.value FROM weenie w
CROSS JOIN (SELECT 1 type, 128 value UNION ALL SELECT 5, 0 UNION ALL SELECT 16, 1 UNION ALL SELECT 19, 0 UNION ALL SELECT 93, 1044) p
WHERE w.class_Id BETWEEN 900752 AND 900770;   -- misc, no burden, not usable, no value, ethereal
INSERT INTO weenie_properties_bool (object_Id, type, value)
SELECT class_Id, 1, 1 FROM weenie WHERE class_Id BETWEEN 900752 AND 900770;   -- Stuck
INSERT INTO weenie_properties_float (object_Id, type, value) VALUES
    (900752, 39, 1.0),
    (900753, 39, 1.0),
    (900754, 39, 1.0),
    (900755, 39, 1.5),
    (900756, 39, 2.0),
    (900757, 39, 0.8),
    (900758, 39, 1.0),
    (900759, 39, 2.0),
    (900760, 39, 0.9),
    (900761, 39, 1.2),
    (900762, 39, 2.0),
    (900763, 39, 1.2),
    (900764, 39, 1.5),
    (900765, 39, 2.6),
    (900766, 39, 2.2),
    (900767, 39, 1.3),
    (900768, 39, 1.0),
    (900769, 39, 2.0),
    (900770, 39, 2.0);   -- DefaultScale
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES
    (900752, 1, 0x02001A44), (900752, 8, 0x060012CF),   -- retail Rock 43785
    (900753, 1, 0x02001A45), (900753, 8, 0x060012CF),   -- Rock 43787
    (900754, 1, 0x02001A43), (900754, 8, 0x060012CF),   -- Rock 43786
    (900755, 1, 0x02000597), (900755, 8, 0x0600106C),   -- Big Cave-In Rock 11733
    (900756, 1, 0x02000959), (900756, 8, 0x06001E38),   -- Ancient Skull and Bones 7817
    (900757, 1, 0x02000CD2), (900757, 8, 0x06001312), (900757, 6, 0x04000962), (900757, 7, 0x100003A6),   -- Auroch Skull 19443
    (900758, 1, 0x02000293), (900758, 8, 0x060012CD),   -- Large Urn 158
    (900759, 1, 0x02000973), (900759, 8, 0x06001E8D),   -- Crystal Nodule 8369
    (900760, 1, 0x02001B1A), (900760, 8, 0x060012D3),   -- Skeleton 44770
    (900761, 1, 0x02001BC9), (900761, 8, 0x06006A70),   -- Pile of Gearknight Parts 51266
    (900762, 1, 0x02000FA7), (900762, 8, 0x06002D14), (900762, 6, 0x04000BEF),   -- Alloy Mechanism 25322
    (900763, 1, 0x02000CB1), (900763, 8, 0x060025BC),   -- Bronze Gear from a Statue 19215
    (900764, 1, 0x02001920), (900764, 8, 0x06006A87),   -- Gear Knight Core Fragment 41563
    (900765, 1, 0x02000A94), (900765, 8, 0x0600214C),   -- Ursuin Rug 11937
    (900766, 1, 0x02001763), (900766, 8, 0x06001A63), (900766, 6, 0x04000BD5),   -- Pristine White Mattekar Hide 36703
    (900767, 1, 0x02000450), (900767, 8, 0x060012D2),   -- Ursuin Rug Trap 1 22122 (its model only)
    (900768, 1, 0x02000FFD), (900768, 8, 0x06002FE6),   -- Aluvian Rug 25767
    (900769, 1, 0x02000E8A), (900769, 8, 0x06002973),   -- Gharu'ndim Rug 22254
    (900770, 1, 0x0200176E), (900770, 8, 0x06006761);   -- Jubilee Rug 36910
-- the Aluvian rug's model has an upright (hanging) pose and lying-flat ones; without a Placement the client shows the
-- upright one, so it rests on the floor with Placement 101 (Resting)
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES (900768, 65, 101), (900769, 65, 101), (900770, 65, 101);
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES (900762, 3, 21);   -- the cogwheel's palette
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
SELECT class_Id, 22, 0x3400002B FROM weenie WHERE class_Id BETWEEN 900752 AND 900770;   -- effects
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900752, 1, 'Rocks'),
    (900753, 1, 'Rocks'),
    (900754, 1, 'Flat Rock'),
    (900755, 1, 'Cave-In Rock'),
    (900756, 1, 'Skull and Bones'),
    (900757, 1, 'Auroch Skull'),
    (900758, 1, 'Urn'),
    (900759, 1, 'Crystal Nodule'),
    (900760, 1, 'Skeleton'),
    (900761, 1, 'Pile of Gears'),
    (900762, 1, 'Cogwheel'),
    (900763, 1, 'Bronze Gear'),
    (900764, 1, 'Core Fragment'),
    (900765, 1, 'Bear Rug'),
    (900766, 1, 'White Mattekar Hide'),
    (900767, 1, 'Ursuin Rug'),
    (900768, 1, 'Aluvian Rug'),
    (900769, 1, 'Gharu''ndim Rug'),
    (900770, 1, 'Jubilee Rug');
