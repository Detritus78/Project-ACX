-- Project ACX (2026-11-24): garbage barrels around Concordia (JJ: "can we get a garbage barrel here", five /loc spots,
-- "normally you just hand your item to the garbage barrel ... kinda like a town crier").
-- Retail's Garbage Barrel (34726, "Throw items away by giving them to the barrel."; retail had them only at the Mana Forges)
-- accepts everything given to it and destroys it (AiAcceptEverything: Player.GiveObjectToNPC); attuned items it refuses.
-- Placed where and facing as JJ stood. Guids 0x7018B9A0-0x7018B9A4. Re-runnable; after applying, /reload-landblock in
-- Concordia or restart.
DELETE FROM landblock_instance WHERE guid BETWEEN 0x7018B9A0 AND 0x7018B9A4;
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    (0x7018B9A0, 34726, 0x018A0280, 115.001761, -118.885404, 0.005, 0.999978, 0, 0, -0.006600, 0),
    (0x7018B9A1, 34726, 0x018A0269, 105.084907, -118.813334, 0.005, 0.999927, 0, 0, 0.012101, 0),
    (0x7018B9A2, 34726, 0x018A0273, 119.866727, -75.980000, 0.005, 0.999228, 0, 0, 0.039278, 0),
    (0x7018B9A3, 34726, 0x018A011A, 110.712653, -108.191122, -11.994, 0.513177, 0, 0, 0.858283, 0),
    (0x7018B9A4, 34726, 0x018A02E2, 36.777346, -35.480000, 6.005, 0.995162, 0, 0, 0.098251, 0);
