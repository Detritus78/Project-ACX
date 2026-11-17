-- Project ACX (2026-11-15): vendors sell the Eternal thrown weapons instead of retail's consumable stacks (JJ: "convert
-- the vendor ones to eternal"). Same name and model as the retail one, but a single weapon that is never used up
-- (10-eternal-thrown.sql, the plain ones); unlike a loot drop it isn't rolled, so it keeps the weenie's damage 12 and
-- multiplier +70% (a vendor Atlatl with an Atlatl Dart), a starter weapon. Only shop lists (destination_Type 4) change:
-- monsters and NPCs that wield retail thrown stacks (destination_Type 2) keep them, as monsters throw from a stack.
-- The bowyers' elemental darts and axes too (2026-11-17, JJ bought a retail stack on Live: "oh dear we didnt change all the
-- thrown weapons to eternals"): each becomes the Eternal one of its element (lightning = Electric), 300 pyreals like the
-- plain ones.
-- Re-runnable: a second run finds no retail ids left in shop lists.
UPDATE weenie_properties_create_list SET weenie_Class_Id = CASE weenie_Class_Id
        WHEN 316 THEN 900810   -- Throwing Dart
        WHEN 343 THEN 900811   -- Shouken
        WHEN 315 THEN 900812   -- Throwing Dagger
        WHEN 310 THEN 900813   -- Throwing Club
        WHEN 317 THEN 900814   -- Djarid
        WHEN 320 THEN 900815   -- Javelin
        WHEN 304 THEN 900816   -- Throwing Axe
        WHEN 3786 THEN 900817  -- Throwing Acid Dart -> Acid Throwing Dart
        WHEN 3788 THEN 900818  -- Throwing Fire Dart -> Fire Throwing Dart
        WHEN 3789 THEN 900819  -- Throwing Frost Dart -> Frost Throwing Dart
        WHEN 3787 THEN 900820  -- Throwing Lightning Dart -> Electric Throwing Dart
        WHEN 3758 THEN 900841  -- Acid Throwing Axe
        WHEN 3760 THEN 900842  -- Flaming Throwing Axe -> Fire Throwing Axe
        WHEN 3761 THEN 900843  -- Frost Throwing Axe
        WHEN 3759 THEN 900844  -- Lightning Throwing Axe -> Electric Throwing Axe
    END
WHERE destination_Type = 4 AND weenie_Class_Id IN (304, 310, 315, 316, 317, 320, 343, 3758, 3759, 3760, 3761, 3786, 3787, 3788, 3789);
