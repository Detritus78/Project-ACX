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
