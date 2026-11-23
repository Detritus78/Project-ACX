-- Project ACX (2026-11-17): the gambling hall's Pawn Shopkeep greets you with the truth (JJ: "We could have him say
-- something like. You won't get rich selling to me. But I will! and make him do a laugh emote."): he pays a flat
-- acx_concordia_buy_rate, under any town's (AcxVendorReputation). Retail's Pawn Shopkeep (12308) also stands in three
-- towns, so Concordia's gets its own copy, 900909: the same shop, clothes and lines, except the greeting when you open his
-- shop, which is now that line and a laugh. The instance JJ placed (0x7018AF5E, 80-placements.sql) is switched to it.
-- Re-runnable.
DELETE FROM weenie WHERE class_Id = 900909;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900909, 'acx-pawn-shopkeep-concordia', type, NOW() FROM weenie WHERE class_Id = 12308;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900909, type, value FROM weenie_properties_int WHERE object_Id = 12308;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900909, type, value FROM weenie_properties_float WHERE object_Id = 12308;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900909, type, value FROM weenie_properties_bool WHERE object_Id = 12308;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900909, type, value FROM weenie_properties_string WHERE object_Id = 12308;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900909, type, value FROM weenie_properties_d_i_d WHERE object_Id = 12308;
INSERT INTO weenie_properties_attribute (object_Id, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) SELECT 900909, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent` FROM weenie_properties_attribute WHERE object_Id = 12308;
INSERT INTO weenie_properties_attribute_2nd (object_Id, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) SELECT 900909, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level` FROM weenie_properties_attribute_2nd WHERE object_Id = 12308;
INSERT INTO weenie_properties_body_part (object_Id, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`)
SELECT 900909, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b` FROM weenie_properties_body_part WHERE object_Id = 12308;
INSERT INTO weenie_properties_create_list (object_Id, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) SELECT 900909, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond` FROM weenie_properties_create_list WHERE object_Id = 12308 ORDER BY id;
-- and the lesser stamina and mana kits (JJ 2026-11-23: "pawnshop keep needs stamina a mana kits lesser")
INSERT INTO weenie_properties_create_list (object_Id, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
    (900909, 4, 44711, -1, 0, 0, 0),   -- Lesser Stamina Kit
    (900909, 4, 44713, -1, 0, 0, 0);   -- Lesser Mana Kit

-- his lines, all but the greeting (vendor_Type 1 = the shop opens); each of his emotes has its own category, vendor type and
-- probability, which pairs the copies with their actions
INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id, style, substyle, quest, vendor_Type, min_Health, max_Health)
SELECT 900909, category, probability, weenie_Class_Id, style, substyle, quest, vendor_Type, min_Health, max_Health FROM weenie_properties_emote
WHERE object_Id = 12308 AND NOT (category = 2 AND vendor_Type = 1) ORDER BY id;
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, motion, message, test_String, min, max, min_64, max_64, min_Dbl, max_Dbl, stat, display, amount, amount_64, hero_X_P_64, percent, spell_Id, wealth_Rating, treasure_Class, treasure_Type, p_Script, sound, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z)
SELECT n.id, a.`order`, a.type, a.delay, a.extent, a.motion, a.message, a.test_String, a.min, a.max, a.min_64, a.max_64, a.min_Dbl, a.max_Dbl, a.stat, a.display, a.amount, a.amount_64, a.hero_X_P_64, a.percent, a.spell_Id, a.wealth_Rating, a.treasure_Class, a.treasure_Type, a.p_Script, a.sound, a.destination_Type, a.weenie_Class_Id, a.stack_Size, a.palette, a.shade, a.try_To_Bond, a.obj_Cell_Id, a.origin_X, a.origin_Y, a.origin_Z, a.angles_W, a.angles_X, a.angles_Y, a.angles_Z
FROM weenie_properties_emote o
JOIN weenie_properties_emote n ON n.object_Id = 900909 AND n.category = o.category AND n.vendor_Type <=> o.vendor_Type AND n.probability = o.probability
JOIN weenie_properties_emote_action a ON a.emote_Id = o.id
WHERE o.object_Id = 12308 ORDER BY a.id;

-- the greeting: the line (a tell, as retail's greetings), then a laugh (MotionCommand.Laugh 0x13000080)
INSERT INTO weenie_properties_emote (object_Id, category, probability, vendor_Type) VALUES (900909, 2, 1, 1);
SET @greeting = LAST_INSERT_ID();
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, motion, message)
SELECT @greeting, 0, a.type, a.delay, a.extent, a.motion, 'You won''t get rich selling to me. But I will!'
FROM weenie_properties_emote e JOIN weenie_properties_emote_action a ON a.emote_Id = e.id
WHERE e.object_Id = 12308 AND e.category = 2 AND e.vendor_Type = 1 AND e.probability = 1;
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, motion)
SELECT @greeting, 1, a.type, a.delay, a.extent, 0x13000080
FROM weenie_properties_emote e JOIN weenie_properties_emote_action a ON a.emote_Id = e.id
WHERE e.object_Id = 12308 AND e.category = 2 AND e.vendor_Type = 5 ORDER BY a.id LIMIT 1;   -- the row of one of his idle gestures

UPDATE landblock_instance SET weenie_Class_Id = 900909 WHERE guid = 0x7018AF5E;
