-- Project ACX: fishing rewards (JJ 2026-11-13). Retail's Tackle Masters (23333-23336; placed: 23336 x6) buy fish: 25%
-- pyreals by colour, 75% an "Expired" potion; counted catches earn title tokens. Changed here:
--  * An ACX Mark at every milestone, with its title token. The counters cap at the milestone and reset when it pays,
--    so they repeat: every 100 Guppies / Mollies / Fish, every 300 Large Fish, every 600 Minnows, and the first 400 Minnows.
--    Each milestone's Mark comes at most once a day per character.
--  * An ACX Mark gift from any fish handed in (even a Minnow): 1% of hand-ins pay their pyreals plus a Mark, at most one
--    such Mark a day per character (JJ: "I hate to make someone wait two hrs to get a ACX Mark": about half an hour of
--    fishing finds it; the daily cap keeps bots to 6 a day).
--  * Pyreal payments +50%: Minnow 15-150, Guppy 75-300, Molly 150-750, Fish 300-1,500, Large Fish 750-3,000.
-- A creature can also come out of the hole (Managers/AcxFishing.cs, acx_fishing_mob_chance).
-- Re-runnable. Restart the server after applying.

-- Each Mark is at most once per character per 20 hours (JJ: "one milestone mark per day per milestone"; the same for the
-- rare gift, so a fishing bot can't farm them): the emotes stamp AcxFishMark_* quests, and Managers/AcxFishing.TryMark
-- gives the Mark only when that quest's 20-hour timer allows.
INSERT INTO quest (name, min_Delta, max_Solves, message) VALUES
    ('AcxFishMark_Minnow400', 72000, -1, 'ACX Mark for the 400-Minnow milestone'),
    ('AcxFishMark_Minnow600', 72000, -1, 'ACX Mark for the 600-Minnow milestone'),
    ('AcxFishMark_Guppy', 72000, -1, 'ACX Mark for the 100-Guppy milestone'),
    ('AcxFishMark_Molly', 72000, -1, 'ACX Mark for the 100-Molly milestone'),
    ('AcxFishMark_Fish', 72000, -1, 'ACX Mark for the 100-Fish milestone'),
    ('AcxFishMark_LargeFish', 72000, -1, 'ACX Mark for the 300-Large-Fish milestone'),
    ('AcxFishMark_Gift', 72000, -1, 'ACX Mark from a fish handed to a Tackle Master')
ON DUPLICATE KEY UPDATE min_Delta = VALUES(min_Delta), max_Solves = VALUES(max_Solves), message = VALUES(message);

-- milestone Marks (a re-run removes the old ones first)
DELETE a FROM weenie_properties_emote_action a JOIN weenie_properties_emote e ON e.id = a.emote_Id
WHERE e.object_Id BETWEEN 23333 AND 23336 AND e.category IN (12, 13)
  AND ((a.type = 3 AND a.weenie_Class_Id = 900710) OR (a.type = 22 AND a.message LIKE 'AcxFishMark\_%'));

INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, message)
SELECT e.id, (SELECT MAX(a.`order`) + 1 FROM weenie_properties_emote_action a WHERE a.emote_Id = e.id), 22, 0, 1,
       CASE e.quest WHEN 'FishCounter' THEN 'AcxFishMark_Fish' WHEN 'FishLargeCounter' THEN 'AcxFishMark_LargeFish'
                    WHEN 'GuppyCounter' THEN 'AcxFishMark_Guppy' WHEN 'MollyCounter' THEN 'AcxFishMark_Molly'
                    WHEN 'MinnowCounter@2' THEN 'AcxFishMark_Minnow600' ELSE 'AcxFishMark_Minnow400' END
FROM weenie_properties_emote e
WHERE e.object_Id BETWEEN 23333 AND 23336
  AND ((e.category = 12 AND e.quest IN ('FishCounter', 'FishLargeCounter', 'GuppyCounter', 'MollyCounter', 'MinnowCounter@2'))
    OR (e.category = 13 AND e.quest = 'GotMinnowToken'));

-- the rare turn-in gift: a copy of each fish's pyreal outcome (pyreals and milestone check kept) that also stamps
-- AcxFishMark_Gift, i.e. adds a Mark if today's is still due
DELETE e FROM weenie_properties_emote e JOIN weenie_properties_emote_action a ON a.emote_Id = e.id
WHERE e.object_Id BETWEEN 23333 AND 23336 AND e.category = 6
  AND ((a.type = 3 AND a.weenie_Class_Id = 900710) OR (a.type = 22 AND a.message = 'AcxFishMark_Gift'));

DROP TEMPORARY TABLE IF EXISTS acx_fish_src;
CREATE TEMPORARY TABLE acx_fish_src AS
SELECT e.id AS src, e.object_Id, e.weenie_Class_Id FROM weenie_properties_emote e
JOIN weenie_properties_emote_action a ON a.emote_Id = e.id AND a.type = 3 AND a.weenie_Class_Id = 273
WHERE e.object_Id BETWEEN 23333 AND 23336 AND e.category = 6 AND ABS(e.probability - 0.25) < 0.0001;

INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id)
SELECT object_Id, 6, 0.01, weenie_Class_Id FROM acx_fish_src;

DROP TEMPORARY TABLE IF EXISTS acx_fish_map;
CREATE TEMPORARY TABLE acx_fish_map AS
SELECT s.src, e.id AS dst FROM acx_fish_src s
JOIN weenie_properties_emote e ON e.object_Id = s.object_Id AND e.weenie_Class_Id = s.weenie_Class_Id AND e.category = 6 AND ABS(e.probability - 0.01) < 0.00001;

INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, motion, message, test_String, min, max, min_64, max_64, min_Dbl, max_Dbl, stat, display, amount, amount_64, hero_X_P_64, percent, spell_Id, wealth_Rating, treasure_Class, treasure_Type, p_Script, sound, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z)
SELECT m.dst, a.`order`, a.type, a.delay, a.extent, a.motion, a.message, a.test_String, a.min, a.max, a.min_64, a.max_64, a.min_Dbl, a.max_Dbl, a.stat, a.display, a.amount, a.amount_64, a.hero_X_P_64, a.percent, a.spell_Id, a.wealth_Rating, a.treasure_Class, a.treasure_Type, a.p_Script, a.sound, a.destination_Type, a.weenie_Class_Id, a.stack_Size, a.palette, a.shade, a.try_To_Bond, a.obj_Cell_Id, a.origin_X, a.origin_Y, a.origin_Z, a.angles_W, a.angles_X, a.angles_Y, a.angles_Z
FROM acx_fish_map m JOIN weenie_properties_emote_action a ON a.emote_Id = m.src;

INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, message)
SELECT m.dst, (SELECT MAX(a.`order`) FROM weenie_properties_emote_action a WHERE a.emote_Id = m.src) + 1, 22, 0, 1, 'AcxFishMark_Gift' FROM acx_fish_map m;

-- pyreals +50% (retail's amounts map to new ones that aren't retail amounts, so a re-run changes nothing)
UPDATE weenie_properties_emote_action a JOIN weenie_properties_emote e ON e.id = a.emote_Id
SET a.stack_Size = CASE a.stack_Size WHEN 10 THEN 15 WHEN 50 THEN 75 WHEN 100 THEN 150 WHEN 200 THEN 300 WHEN 500 THEN 750 WHEN 1000 THEN 1500 WHEN 2000 THEN 3000 ELSE a.stack_Size END
WHERE e.object_Id BETWEEN 23333 AND 23336 AND e.category = 6 AND a.type = 3 AND a.weenie_Class_Id = 273;

-- Creatures pulled from a fishing hole (Managers/AcxFishing.cs) may drop, each a separate chance on top of their own
-- loot: a salvage sack (900721-900723, 40-gambling.sql), a pearl, an ACX Mark (once a day for the fisher). Pearls by the
-- fisher's level, bought by any Tackle Master (prices are placeholders for the money pass):
--   900724 River Pearl (under 50) 3,000   900725 Lake Pearl (50-125) 7,500   900726 Deep Water Pearl (over 125) 15,000
INSERT INTO quest (name, min_Delta, max_Solves, message) VALUES ('AcxFishMark_Creature', 72000, -1, 'ACX Mark from a creature pulled from a fishing hole')
ON DUPLICATE KEY UPDATE min_Delta = VALUES(min_Delta), max_Solves = VALUES(max_Solves), message = VALUES(message);

DELETE FROM weenie WHERE class_Id BETWEEN 900724 AND 900726;
DROP TEMPORARY TABLE IF EXISTS acx_pearls;
CREATE TEMPORARY TABLE acx_pearls (class_Id INT UNSIGNED PRIMARY KEY, class_Name VARCHAR(100), name VARCHAR(100), plural VARCHAR(100), pay INT, descr VARCHAR(255));
INSERT INTO acx_pearls VALUES
    (900724, 'acx-river-pearl',      'River Pearl',      'River Pearls',      3000,  'A small pearl from a river creature''s gullet. The Tackle Master pays well for these.'),
    (900725, 'acx-lake-pearl',       'Lake Pearl',       'Lake Pearls',       7500,  'A lustrous pearl from a lake creature''s gullet. The Tackle Master pays well for these.'),
    (900726, 'acx-deep-water-pearl', 'Deep Water Pearl', 'Deep Water Pearls', 15000, 'A large, dark pearl from a deep water creature. The Tackle Master pays very well for these.');

-- a plain item with the Fake Niffis Pearl's look (31336)
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT p.class_Id, p.class_Name, w.type, NOW() FROM acx_pearls p JOIN weenie w ON w.class_Id = 31336;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT p.class_Id, i.type, i.value FROM acx_pearls p JOIN weenie_properties_int i ON i.object_Id = 31336 WHERE i.type NOT IN (11, 12, 19, 33, 114);
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT p.class_Id, f.type, f.value FROM acx_pearls p JOIN weenie_properties_float f ON f.object_Id = 31336;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT p.class_Id, b.type, b.value FROM acx_pearls p JOIN weenie_properties_bool b ON b.object_Id = 31336;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT p.class_Id, d.type, d.value FROM acx_pearls p JOIN weenie_properties_d_i_d d ON d.object_Id = 31336;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 11, 100 FROM acx_pearls;   -- MaxStackSize
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 12, 1 FROM acx_pearls;     -- StackSize
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 19, 100 FROM acx_pearls;   -- Value: vendors pay little; the Tackle Master pays
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT class_Id, 1, name FROM acx_pearls;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT class_Id, 20, plural FROM acx_pearls;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT class_Id, 16, descr FROM acx_pearls;

-- the Tackle Masters buy them
DELETE FROM weenie_properties_emote WHERE object_Id BETWEEN 23333 AND 23336 AND category = 6 AND weenie_Class_Id BETWEEN 900724 AND 900726;
DROP TEMPORARY TABLE IF EXISTS acx_tackle;
CREATE TEMPORARY TABLE acx_tackle (object_Id INT UNSIGNED PRIMARY KEY);
INSERT INTO acx_tackle SELECT DISTINCT object_Id FROM weenie_properties_emote WHERE object_Id BETWEEN 23333 AND 23336 AND category = 6;
INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id)
SELECT t.object_Id, 6, 1, p.class_Id FROM acx_tackle t CROSS JOIN acx_pearls p;
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, message, destination_Type, weenie_Class_Id, stack_Size, palette, shade)
SELECT e.id, 0, 10, 0, 1, CONCAT('Now that''s a fine ', LOWER(p.name), '! Here''s ', FORMAT(p.pay, 0), ' pyreals for it.'), NULL, NULL, NULL, NULL, NULL
FROM weenie_properties_emote e JOIN acx_pearls p ON p.class_Id = e.weenie_Class_Id WHERE e.object_Id BETWEEN 23333 AND 23336 AND e.category = 6;
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, message, destination_Type, weenie_Class_Id, stack_Size, palette, shade)
SELECT e.id, 1, 3, 0, 1, NULL, 0, 273, p.pay, 0, 0
FROM weenie_properties_emote e JOIN acx_pearls p ON p.class_Id = e.weenie_Class_Id WHERE e.object_Id BETWEEN 23333 AND 23336 AND e.category = 6;
