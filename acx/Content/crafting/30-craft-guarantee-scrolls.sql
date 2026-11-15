-- Project ACX: crafting guarantee scrolls (WorldObjects/Player_AcxCraftGuarantee.cs). Reading one makes one kind of craft
-- succeed 100% for 10 minutes; the kind is AcxCraftGuarantee (int 30012): 1 Tinkering, 2 Imbue, 3 Alchemy, 4 Cooking,
-- 5 Fletching. They look like retail's Scroll of Dark Rain, each with its own coloured icon background. 250,000 pyreals
-- (1 MMD) each to start; the copy a player buys resells for 1 (Player_AcxCommerce). Sold by the 97 mage vendors that
-- sell spell extraction scroll I (listed here so this file doesn't depend on load order).
--   900640-900644  Scroll of Sure Tinkering / Imbuing / Alchemy / Cooking / Fletching
-- Re-runnable. Restart the server after applying.

DELETE FROM weenie WHERE class_Id BETWEEN 900640 AND 900644;
DELETE FROM weenie_properties_create_list WHERE weenie_Class_Id BETWEEN 900640 AND 900644;

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) VALUES
    (900640, 'acx-craft-guarantee-tinkering', 1, NOW()),
    (900641, 'acx-craft-guarantee-imbuing', 1, NOW()),
    (900642, 'acx-craft-guarantee-alchemy', 1, NOW()),
    (900643, 'acx-craft-guarantee-cooking', 1, NOW()),
    (900644, 'acx-craft-guarantee-fletching', 1, NOW());

INSERT INTO weenie_properties_int (object_Id, type, value) VALUES
    (900640, 1, 128), (900640, 5, 30), (900640, 9, 0), (900640, 16, 8), (900640, 19, 250000), (900640, 93, 1044), (900640, 30012, 1),
    (900641, 1, 128), (900641, 5, 30), (900641, 9, 0), (900641, 16, 8), (900641, 19, 250000), (900641, 93, 1044), (900641, 30012, 2),
    (900642, 1, 128), (900642, 5, 30), (900642, 9, 0), (900642, 16, 8), (900642, 19, 250000), (900642, 93, 1044), (900642, 30012, 3),
    (900643, 1, 128), (900643, 5, 30), (900643, 9, 0), (900643, 16, 8), (900643, 19, 250000), (900643, 93, 1044), (900643, 30012, 4),
    (900644, 1, 128), (900644, 5, 30), (900644, 9, 0), (900644, 16, 8), (900644, 19, 250000), (900644, 93, 1044), (900644, 30012, 5);   -- misc, burden 30, use from the pack, value 250,000, AcxCraftGuarantee

INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES
(900640, 23, 1), (900641, 23, 1), (900642, 23, 1), (900643, 23, 1), (900644, 23, 1);   -- DestroyOnSell

INSERT INTO weenie_properties_float (object_Id, type, value) VALUES
(900640, 39, 1.5), (900641, 39, 1.5), (900642, 39, 1.5), (900643, 39, 1.5), (900644, 39, 1.5);

INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES
    (900640, 1, 0x02000B55 + 0), (900640, 8, 0x06002265 + 0), (900640, 22, 0x3400002B + 0), (900640, 52, 0x0600335C + 0),
    (900641, 1, 0x02000B55 + 0), (900641, 8, 0x06002265 + 0), (900641, 22, 0x3400002B + 0), (900641, 52, 0x06003358 + 0),
    (900642, 1, 0x02000B55 + 0), (900642, 8, 0x06002265 + 0), (900642, 22, 0x3400002B + 0), (900642, 52, 0x06003357 + 0),
    (900643, 1, 0x02000B55 + 0), (900643, 8, 0x06002265 + 0), (900643, 22, 0x3400002B + 0), (900643, 52, 0x0600335A + 0),
    (900644, 1, 0x02000B55 + 0), (900644, 8, 0x06002265 + 0), (900644, 22, 0x3400002B + 0), (900644, 52, 0x0600335B + 0);

INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900640, 1, 'Scroll of Sure Tinkering'),
    (900640, 14, 'Read this scroll and your salvage tinkering (weapon, armor, item and magic item tinkering) can''t fail for the next 10 minutes. /guarantees shows the time left.'),
    (900641, 1, 'Scroll of Sure Imbuing'),
    (900641, 14, 'Read this scroll and your imbuing can''t fail for the next 10 minutes. /guarantees shows the time left.'),
    (900642, 1, 'Scroll of Sure Alchemy'),
    (900642, 14, 'Read this scroll and your alchemy can''t fail for the next 10 minutes. /guarantees shows the time left.'),
    (900643, 1, 'Scroll of Sure Cooking'),
    (900643, 14, 'Read this scroll and your cooking can''t fail for the next 10 minutes. /guarantees shows the time left.'),
    (900644, 1, 'Scroll of Sure Fletching'),
    (900644, 14, 'Read this scroll and your fletching can''t fail for the next 10 minutes. /guarantees shows the time left.');

INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond)
SELECT v.id, 4, s.wcid, -1, 0, 0, 0
FROM (SELECT 675 AS id UNION ALL SELECT 692 UNION ALL SELECT 795 UNION ALL SELECT 809 UNION ALL SELECT 831 UNION ALL SELECT 856 UNION ALL SELECT 857 UNION ALL SELECT 984 UNION ALL SELECT 1048 UNION ALL SELECT 1369 UNION ALL SELECT 1370 UNION ALL SELECT 1371 UNION ALL SELECT 1812 UNION ALL SELECT 1824 UNION ALL SELECT 2220 UNION ALL SELECT 2246 UNION ALL SELECT 2247 UNION ALL SELECT 2248 UNION ALL SELECT 2249 UNION ALL SELECT 2250 UNION ALL SELECT 2290 UNION ALL SELECT 2302 UNION ALL SELECT 2303 UNION ALL SELECT 2304 UNION ALL SELECT 2305 UNION ALL SELECT 2306 UNION ALL SELECT 2307 UNION ALL SELECT 2314 UNION ALL SELECT 2498 UNION ALL SELECT 2501 UNION ALL SELECT 2537 UNION ALL SELECT 2540 UNION ALL SELECT 2543 UNION ALL SELECT 4435 UNION ALL SELECT 4539 UNION ALL SELECT 4547 UNION ALL SELECT 4561 UNION ALL SELECT 4677 UNION ALL SELECT 4689 UNION ALL SELECT 4694 UNION ALL SELECT 5422 UNION ALL SELECT 5425 UNION ALL SELECT 5435 UNION ALL SELECT 5490 UNION ALL SELECT 5639 UNION ALL SELECT 5646 UNION ALL SELECT 5834 UNION ALL SELECT 6826 UNION ALL SELECT 6854 UNION ALL SELECT 7588 UNION ALL SELECT 7589 UNION ALL SELECT 7590 UNION ALL SELECT 8224 UNION ALL SELECT 8432 UNION ALL SELECT 8492 UNION ALL SELECT 9018 UNION ALL SELECT 9414 UNION ALL SELECT 9678 UNION ALL SELECT 11375 UNION ALL SELECT 11387 UNION ALL SELECT 11392 UNION ALL SELECT 11397 UNION ALL SELECT 11400 UNION ALL SELECT 20923 UNION ALL SELECT 22721 UNION ALL SELECT 24588 UNION ALL SELECT 24594 UNION ALL SELECT 25994 UNION ALL SELECT 28531 UNION ALL SELECT 30031 UNION ALL SELECT 30032 UNION ALL SELECT 30033 UNION ALL SELECT 30034 UNION ALL SELECT 30035 UNION ALL SELECT 30036 UNION ALL SELECT 30037 UNION ALL SELECT 30038 UNION ALL SELECT 30039 UNION ALL SELECT 30040 UNION ALL SELECT 30047 UNION ALL SELECT 30048 UNION ALL SELECT 30049 UNION ALL SELECT 30050 UNION ALL SELECT 30051 UNION ALL SELECT 30052 UNION ALL SELECT 30053 UNION ALL SELECT 30054 UNION ALL SELECT 30065 UNION ALL SELECT 30066 UNION ALL SELECT 30067 UNION ALL SELECT 30068 UNION ALL SELECT 30069 UNION ALL SELECT 30070 UNION ALL SELECT 30071 UNION ALL SELECT 30072 UNION ALL SELECT 30445 UNION ALL SELECT 30446) v
CROSS JOIN (SELECT 900640 AS wcid UNION ALL SELECT 900641 UNION ALL SELECT 900642 UNION ALL SELECT 900643 UNION ALL SELECT 900644) s
JOIN weenie w ON w.class_Id = v.id AND w.type = 12;
