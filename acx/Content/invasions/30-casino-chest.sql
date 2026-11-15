-- Project ACX: the Grand Casino Chest (38942, in the gambling halls by Holtburg A9B2, Yaraq 7E64 and Shoushi DB54; its key
-- is a Mid-Stakes Gamesmaster prize) gives one item per opening. JJ 2026-11-13: "the chests you loot in gambling halls can
-- offer rare coins": an ACX Mark is now 25% of its table, and retail's items share the other 75% in their old proportions
-- (cumulative probabilities x 0.75: Mana Forge Key 18.75%, Falatacot Trinket 13.1%, Ornate Gear Marker 9.4%, A'nekshay
-- Token 8.8%, Colosseum Coin 7.5%, Mhoire Coin 5.6%, Venom Sac 5.6%, Gem of Greater Luminance 3.75%, Foolproof 2.4%).
-- Re-runnable. Restart the server after applying.

DELETE FROM weenie_properties_generator WHERE object_Id = 38942 AND where_Create = 8;
INSERT INTO weenie_properties_generator (object_Id, probability, weenie_Class_Id, delay, init_Create, max_Create, when_Create, where_Create, stack_Size, palette_Id, shade, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z) VALUES
    (38942, 0.001875, 36619, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.00375, 36620, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.005625, 36621, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.0075, 36622, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.009375, 36623, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.01125, 36624, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.013125, 36625, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.015, 36634, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.016875, 36626, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.01875, 36627, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.020625, 36628, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.0225, 36635, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.024375, 36636, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.080625, 35383, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.155625, 36518, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.211875, 36376, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.3, 44240, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.3375, 44720, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.525, 38456, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.65625, 34277, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 0.75, 43142, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
    (38942, 1, 900710, 1, 1, 1, 2, 8, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0);   -- the last row is the ACX Mark: the top 25%
