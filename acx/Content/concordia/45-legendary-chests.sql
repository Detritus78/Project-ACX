-- Project ACX (2026-11-14): Legendary chests in Concordia's bottom room, below the lifestone (JJ: "Legendary chests are
-- called legendary chests. They can be mixed weapon armor magical. I want to fill that bottom room with those kind of
-- chests"; "The test chest room should be the legendary chests, and the vaults should be going in the other rooms").
-- They stand on the spots of the developers' loot-test chests that 40-cleanup.sql removes (positions and facing copied
-- from Live, with the DAT's own cells: Live's are stale), in sets of one Weapon (48744), one Armor (48741) and one Magic (48742) chest: 5 of each. JJ removed the
-- 6 scattered ones in game (0x7018AD0C-0x7018AD11), keeping the rows along the east wall, the north side and the
-- south-east corner. Any Legendary Key opens any of them; the keys come from quests and bosses,
-- and each chest refills a second after it's opened, as in retail. Guids 0x7018AD00-0x7018AD14. Re-runnable.
DELETE FROM landblock_instance WHERE guid BETWEEN 0x7018AD00 AND 0x7018AD3F;
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    -- east wall, north to south
    (0x7018AD00, 48744, 0x018A01F1, 145.472, -98.344, -11.995, 0.707107, 0, 0, -0.707107, 0),
    (0x7018AD01, 48741, 0x018A01F1, 145.513, -99.8548, -11.995, 0.707107, 0, 0, -0.707107, 0),
    (0x7018AD02, 48742, 0x018A01F1, 145.613, -101.551, -11.995, 0.707107, 0, 0, -0.707107, 0),
    (0x7018AD03, 48744, 0x018A01F2, 145.429, -108.406, -11.995, 0.707107, 0, 0, -0.707107, 0),
    (0x7018AD04, 48741, 0x018A01F2, 145.484, -110.077, -11.995, 0.707107, 0, 0, -0.707107, 0),
    (0x7018AD05, 48742, 0x018A01F2, 145.524, -111.66, -11.995, 0.707107, 0, 0, -0.707107, 0),
    (0x7018AD06, 48744, 0x018A01F3, 145.495, -118.481, -11.995, 0.707107, 0, 0, -0.707107, 0),
    (0x7018AD07, 48741, 0x018A01F3, 145.511, -119.966, -11.995, 0.707107, 0, 0, -0.707107, 0),
    (0x7018AD08, 48742, 0x018A01F3, 145.574, -121.597, -11.995, 0.707107, 0, 0, -0.707107, 0),
    -- north side
    (0x7018AD09, 48744, 0x018A01AF, 138.529, -94.6752, -11.995, 1, 0, 0, 0, 0),
    (0x7018AD0A, 48741, 0x018A01AF, 139.935, -94.6081, -11.995, 1, 0, 0, 0, 0),
    (0x7018AD0B, 48742, 0x018A01AF, 141.407, -94.5947, -11.995, 1, 0, 0, 0, 0),
    -- south-east corner
    (0x7018AD12, 48744, 0x018A01DB, 140.031, -123.393, -11.995, -0.020047, 0, 0, -0.999799, 0),
    (0x7018AD13, 48741, 0x018A01DA, 141.437, -124.44, -11.995, 0, 0, 0, -1, 0),
    (0x7018AD14, 48742, 0x018A01DA, 139.217, -124.61, -11.995, 0, 0, 0, -1, 0);
