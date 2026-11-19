-- Asuger Temple (0x010B, the level 20 portal of the Regicide quest): the five level 80 Banderling Berserkers (7085, 30,000 XP
-- each) become Banderling Ogres (7087, level 20, 3,500 XP), in line with the dungeon's level 23 Hollow Minions and its 5,000 XP
-- turn-in. Same retail spots, angles and 3-minute generator link; only the weenie changes.
UPDATE landblock_instance SET weenie_Class_Id = 7087
WHERE landblock = 0x010B AND weenie_Class_Id = 7085
  AND guid IN (0x7010B038, 0x7010B039, 0x7010B03A, 0x7010B03B, 0x7010B03C);
