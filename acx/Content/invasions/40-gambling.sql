-- Project ACX: the gambling halls (JJ 2026-11-13). The Gamesmasters (Aluvian/Gharu'ndim/Sho; Low/Mid/High-Stakes tokens
-- 44715/44716/44717 bought from Monty for an M / MM / 2 MMD note) roll one prize per token from a cumulative table.
--  * Each table's top slot sent the roll to a second table of ~60 monster paintings (Low 8.6%, Mid 2.1%, High 4.3%).
--    ACX Marks take the bottom of that slot, so no other prize's odds change: Low 2%, Mid 2%, High 4%; what is left of
--    the slot (Low 6.6%, Mid 0.1%, High 0.3%) gives a salvage sack instead of a painting (JJ: "getting a bag of salvage
--    makes me more happy than getting a picture frame"). The casino chests stay the best Mark chance (Grand Casino
--    Chest 25%, 30-casino-chest.sql).
--  * Junk prizes become salvage sacks of the table's tier (Managers/AcxSalvageSack.cs: used, a sack becomes a bag of random
--    salvage): toys, pet wings, festival candy, poison. Kept: the Golden Gromnie (the casinos' signature prize), The
--    Orphanage and the Antiquated Compass (both summon a portal), the buff foods and potions, the cash trophies.
--   900721 Small Salvage Sack (Low: 25-75 units, workmanship 3-6)   900722 Salvage Sack (Mid: 50-100, 5-8)
--   900723 Heavy Salvage Sack (High: a full bag, 7-10)
-- Re-runnable. Restart the server after applying.

DELETE FROM weenie WHERE class_Id BETWEEN 900721 AND 900723;

DROP TEMPORARY TABLE IF EXISTS acx_sacks;
CREATE TEMPORARY TABLE acx_sacks (class_Id INT UNSIGNED PRIMARY KEY, class_Name VARCHAR(100), name VARCHAR(100), plural VARCHAR(100), tier INT, value INT);
INSERT INTO acx_sacks VALUES
    (900721, 'acx-salvage-sack-small', 'Small Salvage Sack', 'Small Salvage Sacks', 1, 500),
    (900722, 'acx-salvage-sack',       'Salvage Sack',       'Salvage Sacks',       2, 1000),
    (900723, 'acx-salvage-sack-heavy', 'Heavy Salvage Sack', 'Heavy Salvage Sacks', 3, 2500);

-- a Gem (so using it runs code, like the contract gem) without a spell, looking like a salvage bag
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT s.class_Id, s.class_Name, w.type, NOW() FROM acx_sacks s JOIN weenie w ON w.class_Id = 8976;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT s.class_Id, p.type, p.value FROM acx_sacks s JOIN weenie_properties_int p ON p.object_Id = 8976 WHERE p.type NOT IN (11, 12, 19, 33, 114, 267, 280);   -- not its SharedCooldown
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT s.class_Id, p.type, p.value FROM acx_sacks s JOIN weenie_properties_float p ON p.object_Id = 8976 WHERE p.type <> 167;   -- nor its 15 s CooldownDuration (JJ: "remove the timer for opening those sacks")
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT s.class_Id, p.type, p.value FROM acx_sacks s JOIN weenie_properties_bool p ON p.object_Id = 8976;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT s.class_Id, p.type, p.value FROM acx_sacks s JOIN weenie_properties_d_i_d p ON p.object_Id = 8976 WHERE p.type NOT IN (1, 3, 6, 7, 8, 22, 28, 50, 52);
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT s.class_Id, p.type, p.value FROM acx_sacks s JOIN weenie_properties_d_i_d p ON p.object_Id = 21034 WHERE p.type IN (1, 3, 6, 7, 8, 22);   -- a salvage bag's look
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 11, 25 FROM acx_sacks;      -- MaxStackSize
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 12, 1 FROM acx_sacks;       -- StackSize
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 19, value FROM acx_sacks;   -- Value
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 30019, tier FROM acx_sacks; -- AcxSalvageSack
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT class_Id, 1, name FROM acx_sacks;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT class_Id, 20, plural FROM acx_sacks;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT class_Id, 14, 'Use the sack to empty it: a bag of salvage.' FROM acx_sacks;
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900721, 16, 'A gambler''s sack of salvage: part of a bag, of fair workmanship.'),
    (900722, 16, 'A gambler''s sack of salvage: half a bag or more, of good workmanship.'),
    (900723, 16, 'A gambler''s heavy sack of salvage: a full bag, of fine workmanship.');

-- the old ACX Mark prizes (a re-run)
DELETE e FROM weenie_properties_emote e JOIN weenie_properties_emote_action a ON a.emote_Id = e.id
WHERE e.object_Id BETWEEN 9495 AND 9503 AND e.category = 6 AND a.type = 3 AND a.weenie_Class_Id = 900710;

-- Pack Drudge, Ash Gromnie Wings, Licorice Rats -> 900721
UPDATE weenie_properties_emote_action a JOIN weenie_properties_emote e ON e.id = a.emote_Id SET a.weenie_Class_Id = 900721, a.stack_Size = 1
WHERE e.object_Id IN (9498, 9499, 9500) AND e.category = 6 AND e.weenie_Class_Id = 44715 AND a.type = 3 AND a.weenie_Class_Id IN (9172, 28211, 36447);

INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id) SELECT 9498, 6, ROUND(MAX(probability) + 0.02, 4), 44715 FROM weenie_properties_emote WHERE object_Id = 9498 AND category = 6 AND weenie_Class_Id = 44715 AND probability < 1;
SET @e = LAST_INSERT_ID();
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, message, destination_Type, weenie_Class_Id, stack_Size, palette, shade) VALUES
    (@e, 0, 12, 0, 1, NULL, NULL, NULL, NULL, NULL, NULL),
    (@e, 1, 10, 1, 1, 'Let''s see what Dame Fortune has in store for you!', NULL, NULL, NULL, NULL, NULL),
    (@e, 2, 3, 0, 1, NULL, 0, 900710, 1, 0, 0);

INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id) SELECT 9499, 6, ROUND(MAX(probability) + 0.02, 4), 44715 FROM weenie_properties_emote WHERE object_Id = 9499 AND category = 6 AND weenie_Class_Id = 44715 AND probability < 1;
SET @e = LAST_INSERT_ID();
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, message, destination_Type, weenie_Class_Id, stack_Size, palette, shade) VALUES
    (@e, 0, 12, 0, 1, NULL, NULL, NULL, NULL, NULL, NULL),
    (@e, 1, 10, 1, 1, 'Let''s see what Dame Fortune has in store for you!', NULL, NULL, NULL, NULL, NULL),
    (@e, 2, 3, 0, 1, NULL, 0, 900710, 1, 0, 0);

INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id) SELECT 9500, 6, ROUND(MAX(probability) + 0.02, 4), 44715 FROM weenie_properties_emote WHERE object_Id = 9500 AND category = 6 AND weenie_Class_Id = 44715 AND probability < 1;
SET @e = LAST_INSERT_ID();
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, message, destination_Type, weenie_Class_Id, stack_Size, palette, shade) VALUES
    (@e, 0, 12, 0, 1, NULL, NULL, NULL, NULL, NULL, NULL),
    (@e, 1, 10, 1, 1, 'Let''s see what Dame Fortune has in store for you!', NULL, NULL, NULL, NULL, NULL),
    (@e, 2, 3, 0, 1, NULL, 0, 900710, 1, 0, 0);

-- Pack Golem, Azure Gromnie Wings, Candy Corn, Poison -> 900722
UPDATE weenie_properties_emote_action a JOIN weenie_properties_emote e ON e.id = a.emote_Id SET a.weenie_Class_Id = 900722, a.stack_Size = 1
WHERE e.object_Id IN (9501, 9502, 9503) AND e.category = 6 AND e.weenie_Class_Id = 44716 AND a.type = 3 AND a.weenie_Class_Id IN (9179, 28212, 32207, 25407);

INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id) SELECT 9501, 6, ROUND(MAX(probability) + 0.02, 4), 44716 FROM weenie_properties_emote WHERE object_Id = 9501 AND category = 6 AND weenie_Class_Id = 44716 AND probability < 1;
SET @e = LAST_INSERT_ID();
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, message, destination_Type, weenie_Class_Id, stack_Size, palette, shade) VALUES
    (@e, 0, 12, 0, 1, NULL, NULL, NULL, NULL, NULL, NULL),
    (@e, 1, 10, 1, 1, 'Let''s see what Dame Fortune has in store for you!', NULL, NULL, NULL, NULL, NULL),
    (@e, 2, 3, 0, 1, NULL, 0, 900710, 1, 0, 0);

INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id) SELECT 9502, 6, ROUND(MAX(probability) + 0.02, 4), 44716 FROM weenie_properties_emote WHERE object_Id = 9502 AND category = 6 AND weenie_Class_Id = 44716 AND probability < 1;
SET @e = LAST_INSERT_ID();
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, message, destination_Type, weenie_Class_Id, stack_Size, palette, shade) VALUES
    (@e, 0, 12, 0, 1, NULL, NULL, NULL, NULL, NULL, NULL),
    (@e, 1, 10, 1, 1, 'Let''s see what Dame Fortune has in store for you!', NULL, NULL, NULL, NULL, NULL),
    (@e, 2, 3, 0, 1, NULL, 0, 900710, 1, 0, 0);

INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id) SELECT 9503, 6, ROUND(MAX(probability) + 0.02, 4), 44716 FROM weenie_properties_emote WHERE object_Id = 9503 AND category = 6 AND weenie_Class_Id = 44716 AND probability < 1;
SET @e = LAST_INSERT_ID();
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, message, destination_Type, weenie_Class_Id, stack_Size, palette, shade) VALUES
    (@e, 0, 12, 0, 1, NULL, NULL, NULL, NULL, NULL, NULL),
    (@e, 1, 10, 1, 1, 'Let''s see what Dame Fortune has in store for you!', NULL, NULL, NULL, NULL, NULL),
    (@e, 2, 3, 0, 1, NULL, 0, 900710, 1, 0, 0);

-- Pack Scarecrow, Ivory Gromnie Wings, Candy Corn, Chocolate Gromnies, Licorice Rats -> 900723
UPDATE weenie_properties_emote_action a JOIN weenie_properties_emote e ON e.id = a.emote_Id SET a.weenie_Class_Id = 900723, a.stack_Size = 1
WHERE e.object_Id IN (9495, 9496, 9497) AND e.category = 6 AND e.weenie_Class_Id = 44717 AND a.type = 3 AND a.weenie_Class_Id IN (9177, 28213, 32207, 36445, 36447);

INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id) SELECT 9495, 6, ROUND(MAX(probability) + 0.04, 4), 44717 FROM weenie_properties_emote WHERE object_Id = 9495 AND category = 6 AND weenie_Class_Id = 44717 AND probability < 1;
SET @e = LAST_INSERT_ID();
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, message, destination_Type, weenie_Class_Id, stack_Size, palette, shade) VALUES
    (@e, 0, 12, 0, 1, NULL, NULL, NULL, NULL, NULL, NULL),
    (@e, 1, 10, 1, 1, 'Let''s see what Dame Fortune has in store for you!', NULL, NULL, NULL, NULL, NULL),
    (@e, 2, 3, 0, 1, NULL, 0, 900710, 1, 0, 0);

INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id) SELECT 9496, 6, ROUND(MAX(probability) + 0.04, 4), 44717 FROM weenie_properties_emote WHERE object_Id = 9496 AND category = 6 AND weenie_Class_Id = 44717 AND probability < 1;
SET @e = LAST_INSERT_ID();
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, message, destination_Type, weenie_Class_Id, stack_Size, palette, shade) VALUES
    (@e, 0, 12, 0, 1, NULL, NULL, NULL, NULL, NULL, NULL),
    (@e, 1, 10, 1, 1, 'Let''s see what Dame Fortune has in store for you!', NULL, NULL, NULL, NULL, NULL),
    (@e, 2, 3, 0, 1, NULL, 0, 900710, 1, 0, 0);

INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id) SELECT 9497, 6, ROUND(MAX(probability) + 0.04, 4), 44717 FROM weenie_properties_emote WHERE object_Id = 9497 AND category = 6 AND weenie_Class_Id = 44717 AND probability < 1;
SET @e = LAST_INSERT_ID();
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, message, destination_Type, weenie_Class_Id, stack_Size, palette, shade) VALUES
    (@e, 0, 12, 0, 1, NULL, NULL, NULL, NULL, NULL, NULL),
    (@e, 1, 10, 1, 1, 'Let''s see what Dame Fortune has in store for you!', NULL, NULL, NULL, NULL, NULL),
    (@e, 2, 3, 0, 1, NULL, 0, 900710, 1, 0, 0);

-- the painting slot -> a salvage sack (44715)
UPDATE weenie_properties_emote_action a JOIN weenie_properties_emote e ON e.id = a.emote_Id
SET a.type = 3, a.message = NULL, a.destination_Type = 0, a.weenie_Class_Id = 900721, a.stack_Size = 1, a.palette = 0, a.shade = 0
WHERE e.object_Id IN (9498, 9499, 9500) AND e.category = 6 AND e.weenie_Class_Id = 44715 AND a.type = 67 AND a.message = 'Painting';

-- the painting slot -> a salvage sack (44716)
UPDATE weenie_properties_emote_action a JOIN weenie_properties_emote e ON e.id = a.emote_Id
SET a.type = 3, a.message = NULL, a.destination_Type = 0, a.weenie_Class_Id = 900722, a.stack_Size = 1, a.palette = 0, a.shade = 0
WHERE e.object_Id IN (9501, 9502, 9503) AND e.category = 6 AND e.weenie_Class_Id = 44716 AND a.type = 67 AND a.message = 'Painting';

-- the painting slot -> a salvage sack (44717)
UPDATE weenie_properties_emote_action a JOIN weenie_properties_emote e ON e.id = a.emote_Id
SET a.type = 3, a.message = NULL, a.destination_Type = 0, a.weenie_Class_Id = 900723, a.stack_Size = 1, a.palette = 0, a.shade = 0
WHERE e.object_Id IN (9495, 9496, 9497) AND e.category = 6 AND e.weenie_Class_Id = 44717 AND a.type = 67 AND a.message = 'Painting';

-- one sack per prize: retail's High-Stakes candy bundle gave three candies, which the swap above made three Heavy
-- Salvage Sacks (JJ 2026-11-14: "gave me three heavy bags of salvage from one turn in. It should only be one")
DELETE a FROM weenie_properties_emote_action a
JOIN weenie_properties_emote e ON e.id = a.emote_Id
JOIN (SELECT emote_Id, MIN(`order`) first FROM weenie_properties_emote_action WHERE type = 3 AND weenie_Class_Id BETWEEN 900721 AND 900723 GROUP BY emote_Id) f ON f.emote_Id = a.emote_Id
WHERE e.object_Id BETWEEN 9495 AND 9503 AND e.category = 6 AND a.type = 3 AND a.weenie_Class_Id BETWEEN 900721 AND 900723 AND a.`order` > f.first;
