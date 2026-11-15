-- Project ACX (2026-11-15): vendors sell the Eternal thrown weapons instead of retail's consumable stacks (JJ: "convert
-- the vendor ones to eternal"). Same name, model and base damage as the retail one, but a single weapon that is never
-- used up (10-eternal-thrown.sql, the plain ones); unlike a loot drop it isn't rolled, so it keeps the weenie's base
-- damage 10, a starter weapon. Only shop lists (destination_Type 4) change: monsters and NPCs that wield retail thrown
-- stacks (destination_Type 2) keep them, as monsters throw from a stack.
-- Re-runnable: a second run finds no retail ids left in shop lists.
UPDATE weenie_properties_create_list SET weenie_Class_Id = CASE weenie_Class_Id
        WHEN 316 THEN 900810   -- Throwing Dart
        WHEN 343 THEN 900811   -- Shouken
        WHEN 315 THEN 900812   -- Throwing Dagger
        WHEN 310 THEN 900813   -- Throwing Club
        WHEN 317 THEN 900814   -- Djarid
        WHEN 320 THEN 900815   -- Javelin
        WHEN 304 THEN 900816   -- Throwing Axe
    END
WHERE destination_Type = 4 AND weenie_Class_Id IN (304, 310, 315, 316, 317, 320, 343);
