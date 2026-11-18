-- Project ACX (2026-11-18): things floating in the starter dungeons, found by JJ's friend (JJ: "My friend said a apple in
-- the starter area is floating"). Only the four academies new characters start in (CharGen's first location per town:
-- Holtburg 8602, Shoushi 7F03, Yaraq 8C04, Sanamar 7202; JJ: "only the main academies need it moved. The rest are shut down").
--   The Bruised Apple (5090) in the arrival room (01AD) was placed 1.5 m up, past the edge of the desk beside it (DAT
--   object 0x02000272 at 7.58 -29.99, 0.92 m tall): kept on its corner (JJ: "don't center it ... it just needs to go back so it sits on corner of the table"), lowered onto the desk and moved its own width (0.2 m) back from the edge it hung over (JJ: "so however cm wide it is just move it back that much"); its middle is 0.13 m above its bottom (model 0x020000EB), so 1.05 rests it on the 0.92 m top. Re-runnable.
-- JJ then placed it by hand in Holtburg's ("its placed and its perfect"): 8.039 -30.103 0.944, copied to all four.
UPDATE landblock_instance SET origin_X = 8.039, origin_Y = -30.103, origin_Z = 0.944
WHERE weenie_Class_Id = 5090 AND obj_Cell_Id IN (0x860201AD, 0x7F0301AD, 0x8C0401AD, 0x720201AD);
-- The apple stays the Linkable Item Generator's child (JJ unhooked it to place it by hand, then: "yes spawner"), so it comes
-- back for each new player: its link rows, restored in case a re-run follows an unhooked state.
DELETE FROM landblock_instance_link WHERE child_GUID IN (0x786020A3, 0x77F030A9, 0x78C040A6, 0x772020A9);
INSERT INTO landblock_instance_link (parent_GUID, child_GUID, last_Modified) VALUES
    (0x7860209E, 0x786020A3, NOW()), (0x77F030A4, 0x77F030A9, NOW()), (0x78C040A4, 0x78C040A6, NOW()), (0x772020A4, 0x772020A9, NOW());
UPDATE landblock_instance SET is_Link_Child = 1 WHERE guid IN (0x786020A3, 0x77F030A9, 0x78C040A6, 0x772020A9);
