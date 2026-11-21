-- Project ACX (2026-11-18), PREVIEW ONLY: the Entrance Tester (Managers/AcxEntranceTest.cs; JJ: "can we do a chest or
-- multiple chests with all the gems ... use them one by one until I find a broken one", then "yeah build both").
-- One gem, used again and again: each use goes to the next contract dungeon's entrance and Logs/acx-entrance-test.csv
-- records whether the player arrived or was left in portal space. A copy of the Contract Portal Gem's look, never used
-- up, worth nothing. Get it with /ci 900853. Re-runnable; apply by hand to Preview's world DB (apply-content skips
-- acx/Experiments), then restart or clearcache.
DELETE FROM weenie WHERE class_Id = 900853;

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900853, 'acx-entrance-tester', type, NOW() FROM weenie WHERE class_Id = 900550;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900853, type, value FROM weenie_properties_int WHERE object_Id = 900550 AND type NOT IN (33, 114, 267);   -- not bonded, attuned or decaying
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900853, type, value FROM weenie_properties_float WHERE object_Id = 900550;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900853, type, value FROM weenie_properties_bool WHERE object_Id = 900550 AND type <> 63;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900853, type, value FROM weenie_properties_string WHERE object_Id = 900550 AND type NOT IN (1, 14, 16);
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900853, type, value FROM weenie_properties_d_i_d WHERE object_Id = 900550;

INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (900853, 63, 1);   -- UnlimitedUse
-- no cooldown (JJ: "can we remove the timer on it?"): the portal gem's 15 s, shared with other portal gems
DELETE FROM weenie_properties_int WHERE object_Id = 900853 AND type = 280;      -- SharedCooldown
DELETE FROM weenie_properties_float WHERE object_Id = 900853 AND type = 167;    -- CooldownDuration
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900853, 1, 'Entrance Tester'),
    (900853, 14, 'Use it to go to the next contract dungeon''s entrance. /acxentrance shows how far you are.'),
    (900853, 16, 'A test tool: each use goes to the next of the contract dungeons'' entrances, and the server notes whether you arrived. If you''re left in portal space and disconnected, you log back in where you were.');

-- The Entrance Fixer (900854; JJ: "just give me a gem with the broken 17", "ones that get fixed we take out"): the same gem,
-- but each use tries the next entrance still stuck, moved 1.5 m then 3 m in each direction; Logs/acx-entrance-fix.csv.
DELETE FROM weenie WHERE class_Id = 900854;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900854, 'acx-entrance-fixer', type, NOW() FROM weenie WHERE class_Id = 900853;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900854, type, value FROM weenie_properties_int WHERE object_Id = 900853;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900854, type, value FROM weenie_properties_float WHERE object_Id = 900853;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900854, type, value FROM weenie_properties_bool WHERE object_Id = 900853;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900854, type, value FROM weenie_properties_string WHERE object_Id = 900853 AND type NOT IN (1, 14, 16);
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900854, type, value FROM weenie_properties_d_i_d WHERE object_Id = 900853;
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900854, 1, 'Entrance Fixer'),
    (900854, 14, 'Use it to try the next stuck contract entrance, moved a little.'),
    (900854, 16, 'A test tool: each use goes to an entrance that strands AC:Unreal, moved a meter or two. Ones that work drop out.');

-- The Portal Checker (900855; JJ: "lets check those other ones with a gem too"): each use goes to the next portal drop point
-- that acx/Content/exploration/30-portal-drop-cells.sql corrected, from Preview's Logs/acx-portal-check.json; results in
-- Logs/acx-portal-check.csv.
DELETE FROM weenie WHERE class_Id = 900855;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900855, 'acx-portal-checker', type, NOW() FROM weenie WHERE class_Id = 900853;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900855, type, value FROM weenie_properties_int WHERE object_Id = 900853;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900855, type, value FROM weenie_properties_float WHERE object_Id = 900853;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900855, type, value FROM weenie_properties_bool WHERE object_Id = 900853;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900855, type, value FROM weenie_properties_string WHERE object_Id = 900853 AND type NOT IN (1, 14, 16);
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900855, type, value FROM weenie_properties_d_i_d WHERE object_Id = 900853;
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900855, 1, 'Portal Checker'),
    (900855, 14, 'Use it to go to the next corrected portal drop point.'),
    (900855, 16, 'A test tool: each use goes to the next portal destination whose cell was corrected, once each.');

-- The Town Tour (900856; JJ: "create me a gem that goes to all the towns so I can see them"): each use goes to the next
-- town's Concordia portal, from Preview's Logs/acx-town-tour.json (town_portals_to_concordia.py writes it).
DELETE FROM weenie WHERE class_Id = 900856;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900856, 'acx-town-tour', type, NOW() FROM weenie WHERE class_Id = 900853;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900856, type, value FROM weenie_properties_int WHERE object_Id = 900853;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900856, type, value FROM weenie_properties_float WHERE object_Id = 900853;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900856, type, value FROM weenie_properties_bool WHERE object_Id = 900853;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900856, type, value FROM weenie_properties_string WHERE object_Id = 900853 AND type NOT IN (1, 14, 16);
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900856, type, value FROM weenie_properties_d_i_d WHERE object_Id = 900853;
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900856, 1, 'Town Tour'),
    (900856, 14, 'Use it to go to the next town''s Concordia portal.'),
    (900856, 16, 'A test tool: each use goes to the next town''s Concordia portal, all of them in turn.');

-- The Olthoi Tour (900857; JJ 2026-11-21: "lets a gem that ports me around to olthoi dungeons"): each use goes to the
-- next olthoi dungeon's entrance, lowest level first, from Preview's Logs/acx-olthoi-tour.json.
DELETE FROM weenie WHERE class_Id = 900857;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900857, 'acx-olthoi-tour', type, NOW() FROM weenie WHERE class_Id = 900853;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900857, type, value FROM weenie_properties_int WHERE object_Id = 900853;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900857, type, value FROM weenie_properties_float WHERE object_Id = 900853;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900857, type, value FROM weenie_properties_bool WHERE object_Id = 900853;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900857, type, value FROM weenie_properties_string WHERE object_Id = 900853 AND type NOT IN (1, 14, 16);
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900857, type, value FROM weenie_properties_d_i_d WHERE object_Id = 900853;
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900857, 1, 'Olthoi Tour'),
    (900857, 14, 'Use it to go to the next olthoi dungeon''s entrance.'),
    (900857, 16, 'A test tool: each use goes to the next olthoi dungeon, lowest level first.');
