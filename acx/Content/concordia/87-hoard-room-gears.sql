-- Project ACX (2026-11-14): the hoard room's decoration (JJ: "we need something low and unique that we havent used yet",
-- "gears small ones?"): small gears beside and in front of each of its eight vaults, never under one: a pile of gears
-- or a core fragment at each side, a cogwheel or a standing bronze gear at each front corner, each turned at random.
-- Generated from the vaults' places by Claude's ~/Work/ac/tools/hoard_gears.py; regenerate if the vaults move.
-- Guids 0x7018AB00-0x7018AB3F. Re-runnable.
DELETE FROM landblock_instance WHERE guid BETWEEN 0x7018AB00 AND 0x7018AB3F;
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    (0x7018AB00, 900761, 0x018A027E, 124.073, -120.51, 0.005, 0.870576, 0, 0, 0.492034, 0),   -- Pile of Gears by vault 0x7018A847
    (0x7018AB01, 900761, 0x018A027E, 124.2, -122.27, 0.005, 0.999996, 0, 0, 0.002682, 0),   -- Pile of Gears by vault 0x7018A847
    (0x7018AB02, 900763, 0x018A027E, 123.454, -120.744, 0.005, 0.333974, 0, 0, 0.942582, 0),   -- Bronze Gear by vault 0x7018A847
    (0x7018AB03, 900762, 0x018A027E, 123.539, -121.875, 0.005, 0.305492, 0, 0, -0.952195, 0),   -- Cogwheel by vault 0x7018A847
    (0x7018AB04, 900761, 0x018A027E, 124.123, -117.857, 0.005, 0.469066, 0, 0, 0.883163, 0),   -- Pile of Gears by vault 0x7018A84A
    (0x7018AB05, 900764, 0x018A027E, 124.053, -119.431, 0.005, 0.954593, 0, 0, -0.297914, 0),   -- Core Fragment by vault 0x7018A84A
    (0x7018AB06, 900763, 0x018A027E, 123.515, -118.031, 0.005, 0.973786, 0, 0, -0.227466, 0),   -- Bronze Gear by vault 0x7018A84A
    (0x7018AB07, 900763, 0x018A027E, 123.539, -119.212, 0.005, 0.049840, 0, 0, -0.998757, 0),   -- Bronze Gear by vault 0x7018A84A
    (0x7018AB08, 900761, 0x018A027E, 119.4, -124.065, 0.005, 0.736966, 0, 0, 0.675930, 0),   -- Pile of Gears by vault 0x7018A84D
    (0x7018AB09, 900761, 0x018A027E, 117.815, -124.073, 0.005, 0.465949, 0, 0, 0.884812, 0),   -- Pile of Gears by vault 0x7018A84D
    (0x7018AB0A, 900762, 0x018A027E, 119.144, -123.402, 0.005, 0.210824, 0, 0, 0.977524, 0),   -- Cogwheel by vault 0x7018A84D
    (0x7018AB0B, 900762, 0x018A027E, 118.12, -123.378, 0.005, 0.288519, 0, 0, 0.957474, 0),   -- Cogwheel by vault 0x7018A84D
    (0x7018AB0C, 900761, 0x018A027E, 122.103, -124.02, 0.005, 0.092016, 0, 0, -0.995757, 0),   -- Pile of Gears by vault 0x7018A850
    (0x7018AB0D, 900764, 0x018A027E, 120.498, -124.152, 0.005, 0.685394, 0, 0, 0.728172, 0),   -- Core Fragment by vault 0x7018A850
    (0x7018AB0E, 900763, 0x018A027E, 121.799, -123.511, 0.005, 0.566036, 0, 0, -0.824380, 0),   -- Bronze Gear by vault 0x7018A850
    (0x7018AB0F, 900762, 0x018A027E, 120.836, -123.559, 0.005, 0.303019, 0, 0, -0.952984, 0),   -- Cogwheel by vault 0x7018A850
    (0x7018AB10, 900761, 0x018A027E, 123.021, -115.8, 0.005, 0.509636, 0, 0, 0.860390, 0),   -- Pile of Gears by vault 0x7018AF3A
    (0x7018AB11, 900761, 0x018A027E, 124.144, -117.007, 0.005, 0.208437, 0, 0, 0.978036, 0),   -- Pile of Gears by vault 0x7018AF3A
    (0x7018AB12, 900763, 0x018A027E, 122.763, -116.499, 0.005, 0.245859, 0, 0, -0.969306, 0),   -- Bronze Gear by vault 0x7018AF3A
    (0x7018AB13, 900762, 0x018A027E, 123.469, -117.252, 0.005, 0.625541, 0, 0, 0.780191, 0),   -- Cogwheel by vault 0x7018AF3A
    (0x7018AB14, 900764, 0x018A027E, 124.2, -123.161, 0.005, 0.854243, 0, 0, -0.519874, 0),   -- Core Fragment by vault 0x7018AF3C
    (0x7018AB15, 900761, 0x018A027E, 123.263, -124.2, 0.005, 0.439391, 0, 0, -0.898296, 0),   -- Pile of Gears by vault 0x7018AF3C
    (0x7018AB16, 900762, 0x018A027E, 123.657, -122.934, 0.005, 0.994433, 0, 0, 0.105375, 0),   -- Cogwheel by vault 0x7018AF3C
    (0x7018AB17, 900763, 0x018A027E, 123.051, -123.769, 0.005, 0.581735, 0, 0, 0.813379, 0),   -- Bronze Gear by vault 0x7018AF3C
    (0x7018AB18, 900761, 0x018A027E, 116.882, -124.2, 0.005, 0.625068, 0, 0, -0.780570, 0),   -- Pile of Gears by vault 0x7018AF3E
    (0x7018AB19, 900764, 0x018A027E, 115.8, -123.149, 0.005, 0.204793, 0, 0, 0.978805, 0),   -- Core Fragment by vault 0x7018AF3E
    (0x7018AB1A, 900763, 0x018A027E, 117.213, -123.59, 0.005, 0.749908, 0, 0, 0.661542, 0),   -- Bronze Gear by vault 0x7018AF3E
    (0x7018AB1B, 900762, 0x018A027E, 116.217, -122.905, 0.005, 0.987411, 0, 0, -0.158176, 0),   -- Cogwheel by vault 0x7018AF3E
    (0x7018AB1C, 900764, 0x018A027E, 115.8, -116.62, 0.005, 0.143479, 0, 0, -0.989653, 0),   -- Core Fragment by vault 0x7018AF40
    (0x7018AB1D, 900764, 0x018A027E, 116.628, -115.8, 0.005, 0.617908, 0, 0, 0.786251, 0),   -- Core Fragment by vault 0x7018AF40
    (0x7018AB1E, 900763, 0x018A027E, 116.11, -116.827, 0.005, 0.687285, 0, 0, 0.726388, 0),   -- Bronze Gear by vault 0x7018AF40
    (0x7018AB1F, 900763, 0x018A027E, 116.827, -116.018, 0.005, 0.545322, 0, 0, -0.838226, 0);   -- Bronze Gear by vault 0x7018AF40
