-- Project ACX (2026-11-16): the Mark Trader's prices, in ACX Marks (an item's Value; a stackable's StackUnitValue), from
-- JJ's Mark Trader Prices sheet (https://claude.ai/artifact/BDfyhFEu954ReMEyhzoB7m). One price set in a group stands for
-- the whole group (JJ: "assume they are all the same at the price I set one"). Retail's slayer gems are priced by their
-- damage against the ACX stones' 500 for +50% (JJ: "price the ones that do more damage accordingly"). The Gold Phials
-- keep their placeholder for now (JJ: "Ignore the phials for now", an alchemy throwing class is being considered).
-- The attuned copies of retail's eternal items (900880-900886) are priced in 20-attuned-goods.sql. Applies last.
-- 2026-11-25: prices cut to about 1/5-1/10 (JJ: "go with your prices"): the economy log showed about 1 Mark per hour
-- played, so the old prices took 30-1000 hours.
-- Preview sells everything for 1 Mark for testing (acx/Experiments/mark-specials-preview-prices.sql). Re-runnable.

DROP TEMPORARY TABLE IF EXISTS acx_mark_price;
CREATE TEMPORARY TABLE acx_mark_price (class_Id INT UNSIGNED PRIMARY KEY, price INT, stackable BOOL);
INSERT INTO acx_mark_price VALUES
    (900711, 10, 0),     -- Eternal Pack
    (900866, 2, 0),      -- Salvage Barrel
    (900860, 25, 0),     -- Eternal Concordia Portal Gem
    (900861, 15, 0),     -- Rune of Primary Portal Recall
    (900862, 15, 0),     -- Rune of Secondary Portal Recall
    (900863, 150, 0),   -- Eternal Arrow
    (900864, 150, 0),   -- Eternal Quarrel
    (900865, 150, 0),   -- Eternal Atlatl Dart
    (900870, 50, 0), (900871, 50, 0), (900872, 50, 0), (900873, 50, 0), (900874, 50, 0),   -- ACX slayer stones (+50%)
    (900875, 50, 0), (900876, 50, 0), (900877, 50, 0), (900878, 50, 0), (900879, 50, 0),
    (30082, 10, 0), (30083, 10, 0), (30085, 10, 0), (30086, 10, 0), (30087, 10, 0),             -- Perennial dyes
    (30088, 10, 0), (30089, 10, 0), (30090, 10, 0), (30091, 10, 0),
    (45502, 10, 1), (45503, 10, 1), (45504, 10, 1), (45505, 10, 1), (45506, 10, 1),             -- Foolproof gems (JJ: "leave the
    (45507, 10, 1), (45508, 10, 1), (45509, 10, 1), (45510, 10, 1), (45511, 10, 1),             -- imbues at 30"; 10 since 11-25)
    (45512, 75, 1),     -- Greater Mukkir Slayer Stone Gem: Mukkir slayer +75%
    (45515, 70, 1),     -- A'nekshay Slayer Stone Gem: A'nekshay slayer +70%
    (45514, 50, 1),     -- Black Skull of Xikma Gem: Undead slayer +50%
    (45513, 50, 1),     -- Spectral Skull Gem: Skeleton slayer +50%
    (45516, 15, 1),      -- Lucky White Rabbit's Foot Gem: damage range 20% tighter, one per weapon
    (20908, 50, 0);     -- Major Prismatic Stone: turns a plain Isparian weapon Shimmering (kept when used, attuned)

DELETE p FROM weenie_properties_int p JOIN acx_mark_price m ON m.class_Id = p.object_Id WHERE p.type = 19 OR (p.type = 15 AND m.stackable);
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 19, price FROM acx_mark_price;                     -- Value
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 15, price FROM acx_mark_price WHERE stackable;     -- StackUnitValue

-- also sold: the Major Prismatic Stone, which nothing in the world gives out (JJ 2026-11-16: "Lets make it purchasable")
DELETE FROM weenie_properties_create_list WHERE object_Id = 45501 AND destination_Type = 4 AND weenie_Class_Id = 20908;
INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond) VALUES (45501, 4, 20908, -1, 0, 0, 0);

DROP TEMPORARY TABLE acx_mark_price;
