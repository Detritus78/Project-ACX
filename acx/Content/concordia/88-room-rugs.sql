-- Project ACX (2026-11-14): a rug in the middle of each of Concordia's three square vault rooms (JJ: "lets put a
-- different rug in each of the three rooms", "in the middle"; then "the ursuin and the mattekar doe not look good. maybe
-- we should just put all bear rugs in.. If there arent anymore rugs"; then "is there a ghu and sho rug like this"):
-- the red Gharu'ndim rug in the bone room and the hoard room (there is no Sho rug, and JJ: "jubilee is ugly ... I would
-- rather do the gharu rug in the bone room") and the worn Aluvian carpet in the gold room. The game only turns
-- placed objects left and right (tipping the rug over did nothing), so it lies flat by its weenie's Resting placement. Walk-over
-- scenery (75-more-pieces.sql). Guids 0x7018AB40-0x7018AB4F. Re-runnable.
DELETE FROM landblock_instance WHERE guid BETWEEN 0x7018AB40 AND 0x7018AB4F;
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    (0x7018AB40, 900769, 0x018A0251, 100.0, -120.0, 0.02, 1, 0, 0, 0, 0),                   -- Gharu'ndim Rug - bone room
    (0x7018AB41, 900768, 0x018A0267, 110.0, -120.0, 0.02, 1, 0, 0, 0, 0),                   -- Aluvian Rug - gold room
    (0x7018AB42, 900769, 0x018A027E, 120.0, -120.0, 0.02, 1, 0, 0, 0, 0);                    -- Gharu'ndim Rug - hoard room
