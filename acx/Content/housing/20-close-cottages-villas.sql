-- Project ACX: cottages and villas are closed (JJ 2026-11-14: "remove the posts that sit in front where you buy them.
-- Leave the town portals though. We are just closing the homes. Leave mansions purchasable"). The sale posts
-- (slumlords, WeenieType 55, named "Cottage" or "Villa": 2,600 and 570 placed) go with their links to their houses, so
-- no cottage or villa can be bought; the buildings stay (they are client map data). Mansion posts (80) stay.
-- Apartments are out through 10-no-apartments.sql. Nobody owns housing (checked 2026-11-14). Re-runnable.
DELETE k FROM landblock_instance_link k
JOIN landblock_instance l ON l.guid = k.child_Guid
JOIN weenie w ON w.class_Id = l.weenie_Class_Id AND w.type = 55
JOIN weenie_properties_string n ON n.object_Id = l.weenie_Class_Id AND n.type = 1 AND n.value IN ('Cottage', 'Villa');

DELETE l FROM landblock_instance l
JOIN weenie w ON w.class_Id = l.weenie_Class_Id AND w.type = 55
JOIN weenie_properties_string n ON n.object_Id = l.weenie_Class_Id AND n.type = 1 AND n.value IN ('Cottage', 'Villa');
