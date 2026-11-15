-- Project ACX (2026-11-14): the Wheel of Fortune game for Concordia's gambling hub (JJ: "I like your 20hr idea i'll
-- probably put one in the center with some deco around it"): a copy of retail's Wheel of Fortune (10706) that
-- AcxFortuneWheel.cs spins once per character every 20 hours for gambling tokens; JJ places it with ACX Foundry
-- (Build > Gambling). Retail's own wheels stay decoration. Re-runnable.
DELETE FROM weenie WHERE class_Id = 900788;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900788, 'acx-wheel-of-fortune', type, NOW() FROM weenie WHERE class_Id = 10706;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900788, type, value FROM weenie_properties_int WHERE object_Id = 10706;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900788, type, value FROM weenie_properties_float WHERE object_Id = 10706;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900788, type, value FROM weenie_properties_bool WHERE object_Id = 10706;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900788, type, value FROM weenie_properties_string WHERE object_Id = 10706;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900788, type, value FROM weenie_properties_d_i_d WHERE object_Id = 10706;
INSERT INTO weenie_properties_i_i_d (object_Id, type, value) SELECT 900788, type, value FROM weenie_properties_i_i_d WHERE object_Id = 10706;
DELETE FROM weenie_properties_string WHERE object_Id = 900788 AND type = 14;
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES (900788, 14, 'Spin it once every 20 hours for gambling tokens.');   -- Use
DELETE FROM weenie_properties_bool WHERE object_Id = 900788 AND type = 24;   -- not UiHidden: retail's wheels are scenery players can't select (JJ: "the center wheel isnt targetable")

DELETE FROM quest WHERE name = 'AcxWheelOfFortune';
INSERT INTO quest (name, min_Delta, max_Solves, message) VALUES ('AcxWheelOfFortune', 72000, -1, 'Spun the Wheel of Fortune.');

-- the Wheel of Knowledge (34282, in the hub by the high-stakes gamesmasters: JJ "so they can spin it"): retail spins it
-- with a use emote, which ACE skips for a plain object (no motion state), so it never turned (JJ: "the wheel of knowledge
-- is not spinning"); it plays the same spin straight from its use instead (Animate, ActivationAnimation)
UPDATE weenie_properties_int SET value = 4 WHERE object_Id = 34282 AND type = 83;   -- ActivationResponse: Animate, was Emote
DELETE FROM weenie_properties_d_i_d WHERE object_Id = 34282 AND type = 19;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES (34282, 19, 0x10000051);   -- ActivationAnimation: the Fortune wheel's spin
DELETE FROM weenie_properties_bool WHERE object_Id = 34282 AND type = 24;   -- not UiHidden, so players can click it too (JJ: "wheel of knowledge cant be clicked either")
