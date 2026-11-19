-- Project ACX: Lesser Stamina Kit (44711) and Lesser Mana Kit (44713) at every healer that sells healing kits (JJ 2026-11-19:
-- "yes add them to healers at 1k a piece", then "About 1,000, varies by healer"). Retail only gave them as Mid-Stakes
-- Gamesmaster prizes. Value 645 (was 1,000): the common healer markup (SellPrice 1.55) charges exactly 1,000; the others
-- charge 806 (1.25) to 1,226 (1.9). Healers = shop vendors named "...Healer..." that stock a healing kit. Re-runnable.
-- A vendor's stock is read when it first loads: clearcache, then a restart (or the landblock reloading) shows them.

UPDATE weenie_properties_int SET value = 645 WHERE type = 19 AND object_Id IN (44711, 44713);

DELETE c FROM weenie_properties_create_list c
JOIN weenie_properties_string s ON s.object_Id = c.object_Id AND s.type = 1
WHERE c.destination_Type = 4 AND c.weenie_Class_Id IN (44711, 44713) AND s.value LIKE '%Healer%';

INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond)
SELECT h.object_Id, 4, k.wcid, -1, 0, 0, 0
FROM (SELECT DISTINCT c.object_Id
      FROM weenie_properties_create_list c
      JOIN weenie_properties_string s ON s.object_Id = c.object_Id AND s.type = 1
      WHERE c.destination_Type = 4 AND c.weenie_Class_Id IN (628, 629, 630, 631, 632, 13233) AND s.value LIKE '%Healer%') h
CROSS JOIN (SELECT 44711 AS wcid UNION ALL SELECT 44713) k;
