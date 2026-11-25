-- Project ACX: Jhen-Ma Shih the Shopkeep (1147, Baishi hometown) also sells the nine town portal gems that
-- Archmage Cindrue in Holtburg sells (JJ 2026-11-25). Re-runnable.
DELETE FROM weenie_properties_create_list WHERE object_Id = 1147 AND destination_Type = 4 AND weenie_Class_Id IN (8973, 8976, 8977, 8978, 8979, 8980, 8981, 8983, 8984);
INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond)
SELECT 1147, 4, c.weenie_Class_Id, c.stack_Size, c.palette, c.shade, c.try_To_Bond FROM weenie_properties_create_list c
WHERE c.object_Id = 2304 AND c.destination_Type = 4 AND c.weenie_Class_Id IN (8973, 8976, 8977, 8978, 8979, 8980, 8981, 8983, 8984);
