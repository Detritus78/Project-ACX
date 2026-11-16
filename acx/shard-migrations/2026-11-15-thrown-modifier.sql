-- Project ACX: one-time fix for Eternal thrown weapons that already exist (JJ 2026-11-15: "how can we get a modifier on
-- our eternal ammo so that it behaves and is treated similar to bows for damage"). A thrown weapon is now a launcher and
-- its own ammunition: Damage is its own damage (the ammunition's part) and DamageMod a multiplier like an atlatl's
-- (Factories/Tables/Wcids/Weapons/AcxThrownWcids.cs). Weapons made before that carry everything in Damage:
--   loot dropped on 2026-11-15 (the multiplier folded into Damage, no DamageMod), and the first loot of 2026-11-14
--   (Damage 10 with a DamageMod of up to 13): Damage x DamageMod is split into a multiplier of about 2.5 (2.1 for a
--   wield requirement of 250 or none: tiers 1-2) and the damage that goes with it, so an unbuffed throw hits as before;
--   vendor ones (Damage 10, no DamageMod, no workmanship): the new weenie's Damage 12 and DamageMod 1.7.
-- Only weapons without a DamageMod, or with one over 3, are touched, so running it twice changes nothing.
-- Run against ace_shard with the server stopped or the owners offline; on Live at commit (Preview done 2026-11-15).
-- Damage = PropertyInt 44, DamageMod = PropertyFloat 63, ItemWorkmanship = PropertyInt 105, WieldDifficulty = PropertyInt 160.
DROP TEMPORARY TABLE IF EXISTS acx_thrown_fix;
CREATE TEMPORARY TABLE acx_thrown_fix AS
SELECT b.id,
       d.value * COALESCE(m.value, 1) AS total,
       m.value IS NOT NULL AS has_mod,
       wk.value IS NULL AND d.value * COALESCE(m.value, 1) <= 12 AS vendor,
       CASE WHEN w.value IS NULL OR w.value <= 250 THEN 2.1 ELSE 2.5 END AS target_mod
FROM biota b
JOIN biota_properties_int d ON d.object_Id = b.id AND d.type = 44
LEFT JOIN biota_properties_float m ON m.object_Id = b.id AND m.type = 63
LEFT JOIN biota_properties_int wk ON wk.object_Id = b.id AND wk.type = 105
LEFT JOIN biota_properties_int w ON w.object_Id = b.id AND w.type = 160
WHERE b.weenie_Class_Id BETWEEN 900810 AND 900851 AND (m.value IS NULL OR m.value > 3);

ALTER TABLE acx_thrown_fix ADD COLUMN new_damage INT, ADD COLUMN new_mod DOUBLE;
UPDATE acx_thrown_fix SET new_damage = IF(vendor, 12, GREATEST(1, ROUND(total / target_mod)));
UPDATE acx_thrown_fix SET new_mod = IF(vendor, 1.7, ROUND(total / new_damage, 3));

UPDATE biota_properties_int d JOIN acx_thrown_fix f ON f.id = d.object_Id SET d.value = f.new_damage WHERE d.type = 44;
UPDATE biota_properties_float m JOIN acx_thrown_fix f ON f.id = m.object_Id SET m.value = f.new_mod WHERE m.type = 63;
INSERT INTO biota_properties_float (object_Id, type, value) SELECT id, 63, new_mod FROM acx_thrown_fix WHERE NOT has_mod;

SELECT COUNT(*) AS thrown_weapons_converted FROM acx_thrown_fix;
DROP TEMPORARY TABLE acx_thrown_fix;
