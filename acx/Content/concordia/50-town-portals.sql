-- Project ACX (2026-11-14): The town portals Concordia lacked (JJ: "add the portals from that
-- list ... See if you can find empty spots where other portals were that we removed"): the seven towns of the 52 with
-- no portal here (Sanamar, Westwatch, Neydisa, Silyun, Beach Fort, Fiun, Eastwatch) and the Facility Hub (JJ:
-- "Definetly leave facility hub"; not the Marketplace: "we won't need that"). Each stands where a removed portal stood
-- (40-cleanup.sql), facing the same way; Aerlinthe's old spot (0x018A025F [125.33 -55.84 0]) is free again. Retail's own portal weenies; Beach Fort's (30395) wasn't on the Town Network.
-- 2026-11-17: JJ moved Westwatch's (was 0x7018AF13 at 0x018A0201 [66.36 -96.43]) to 0x7018AF66 and Silyun's (was 0x7018AF14 at
-- 0x018A0274 [136.46 -96.36], by the gem rooms; JJ: "yes remove it") to 0x7018AF67, both in 80-placements.sql. Re-runnable.
DELETE FROM landblock_instance WHERE guid BETWEEN 0x7018AF10 AND 0x7018AF18;
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    (0x7018AF11, 42851, 0x018A0215,  94.6209, -55.4297,  0.005,  0.930508, 0, 0, -0.366273, 0),   -- Portal to Facility Hub (was Singularity Caul)
    (0x7018AF12, 42835, 0x018A0211,  94.3815, -24.6313,  0.005,  0.449595, 0, 0, -0.893232, 0),   -- Portal to Sanamar (was Vesayen Isles)
    (0x7018AF15, 42828, 0x018A0121, 120.073,  -23.0238, -11.995, 1,        0, 0,  0,        0),   -- Portal to Neydisa Castle (was Abandoned Mine)
    (0x7018AF16, 42839, 0x018A012B, 119.906,  -56.9696, -11.995, 1,        0, 0,  0,        0),   -- Portal to Eastwatch (was a Surface exit)
    (0x7018AF17, 42999, 0x018A0167, 126.969,  -30.1143, -11.995, 0.707107, 0, 0, -0.707107, 0),   -- Fiun Outpost (was Jungle Shadows)
    (0x7018AF18, 30395, 0x018A0169, 126.966,  -50.0365, -11.995, 0.707107, 0, 0, -0.707107, 0);   -- Portal to the Beach Fort (was The Floating City)
