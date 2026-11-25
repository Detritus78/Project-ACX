-- Project ACX (2026-11-16): the Mark Trader (45501) also lists the Alchemist's Healing bomb (alchemist/10-bombs.sql; JJ:
-- "Fester and HOT could combine"), attuned like its other goods. Its rows for the nine eternal phials (900712-900720:
-- Imperil, Fester, the seven vulnerabilities; invasions/20-mark-vendor.sql) stay: the shop shows the Healing bomb and hides
-- those phials only while acx_alchemist_enabled is on (AcxBombWcids.ShopSells, read when the vendor loads), so the
-- Alchemist ships switched off.
-- Price: 150 Marks since 2026-11-25 (was 1,000 (JJ 2026-11-16: "Healing bomb price lets put it at close to 1k"); JJ 11-25 "go with your prices"), the Eternal Arrow's. Runs after 10-30
-- here and the vendor file.
-- Preview sells it for 1 Mark (acx/Experiments/mark-specials-preview-prices.sql). Re-runnable.
DELETE FROM weenie_properties_create_list WHERE object_Id = 45501 AND destination_Type = 4 AND weenie_Class_Id = 900898;
INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond) VALUES (45501, 4, 900898, -1, 0, 0, 0);
DELETE FROM weenie_properties_int WHERE object_Id = 900898 AND type = 19;
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES (900898, 19, 150);   -- Value = price in ACX Marks
