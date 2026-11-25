-- Project ACX (2026-11-24): Baishi as the allegiance hometown (JJ: a town with the buildings closest to the meeting
-- hall, "and we can add what we need"). Retail weenies that retail never placed, so nothing is copied:
--   Jhen-Ma Shih the Shopkeep (1147, mayoishopkeep: Sho, a full shop, buys essences) - JJ: "add a race appropriate shopkeep here"
--   Collector (3920, collectorsho: the same 89 trophies as every town Collector) - JJ: "add a race apropriate collector here"
--   Qing (42430, the Sho copy of Holtburg's tailor Ianto: armor reduction tools, armor and weapon tailoring kits; the other
--   stands in Shoushi) - JJ: "add the holtburg tailor the special one that sales the pieces to reduce armor here"
--   Haruko (42722, the Sho copy of Holtburg's barber Ealdred; the other stands in Shoushi) - JJ: "lets add the holtburg
--   style barber here"
-- Placed where and facing as JJ stood. Guids 0x7CD41100-0x7CD41103. Re-runnable; after applying, acxreloadlandblock CD41
-- or restart.
DELETE FROM landblock_instance WHERE guid BETWEEN 0x7CD41100 AND 0x7CD41103;
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    (0x7CD41100, 1147, 0xCD410135, 79.275600, 130.933829, 54.005, 0.686932, 0, 0, -0.726722, 0),
    (0x7CD41101, 3920, 0xCD41012B, 81.855044, 126.455367, 54.005, 0.998037, 0, 0, -0.062622, 0),
    (0x7CD41102, 42430, 0xCD410134, 90.237479, 129.178684, 54.005, 0.688588, 0, 0, 0.725153, 0),
    (0x7CD41103, 42722, 0xCD41012B, 87.935457, 126.464511, 54.006, 0.842621, 0, 0, 0.538508, 0);
