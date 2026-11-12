-- Project ACX: salvage descriptions that match what the salvage does here. Re-runnable.
-- Silk adds armor to underclothes (10-silk-and-protections-on-underclothes.sql). Teak, Ebony and Porcelain changed an
-- item's heritage requirement; items no longer have heritage requirements, so they have no use until they are repurposed.
UPDATE weenie_properties_string d JOIN weenie_properties_string n ON n.object_Id = d.object_Id AND n.type = 1
SET d.value = 'Apply this material to treasure-generated shirts, pants and other underclothes to increase their armor level by 10.'
WHERE d.type = 14 AND n.value = 'Salvaged Silk';

UPDATE weenie_properties_string d JOIN weenie_properties_string n ON n.object_Id = d.object_Id AND n.type = 1
SET d.value = 'This material has no tinkering use at the moment.'
WHERE d.type = 14 AND n.value IN ('Salvaged Teak', 'Salvaged Ebony', 'Salvaged Porcelain');
