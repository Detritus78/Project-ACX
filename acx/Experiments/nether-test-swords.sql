-- Project ACX experiment: nether melee visual test (Preview only; never in acx/Content).
-- Three copies of the Long Sword (351) that deal nether damage:
--   900001  A: Penumbral Horror shadow effect  (effect table 0x34000063, script 0x56)
--   900002  B: Umbral Mukkir shadow effect     (effect table 0x340000B9, script 0x55)
--   900003  C: plain nether, no lasting effect (control)
-- All three get the nether icon border. Re-runnable.
-- Apply: docker compose -p acx-preview ... exec -T db mysql ace_world < this file

DELETE FROM weenie WHERE class_Id IN (900001, 900002, 900003);

DROP TEMPORARY TABLE IF EXISTS acx_test;
CREATE TEMPORARY TABLE acx_test (wcid INT UNSIGNED, class_name VARCHAR(100), name VARCHAR(100), fx_table INT UNSIGNED, fx_script INT UNSIGNED);
INSERT INTO acx_test VALUES
    (900001, 'acx-test-nether-sword-a', 'Nether Test Sword A (Penumbral)', 0x34000063, 0x56),
    (900002, 'acx-test-nether-sword-b', 'Nether Test Sword B (Umbral)',    0x340000B9, 0x55),
    (900003, 'acx-test-nether-sword-c', 'Nether Test Sword C (plain)',     NULL,       NULL);

INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
SELECT t.wcid, t.class_name, w.type, NOW() FROM acx_test t JOIN weenie w ON w.class_Id = 351;

-- copy the Long Sword's properties
INSERT INTO weenie_properties_int (object_Id, type, value)
SELECT t.wcid, p.type, p.value FROM acx_test t JOIN weenie_properties_int p ON p.object_Id = 351;
INSERT INTO weenie_properties_float (object_Id, type, value)
SELECT t.wcid, p.type, p.value FROM acx_test t JOIN weenie_properties_float p ON p.object_Id = 351;
INSERT INTO weenie_properties_bool (object_Id, type, value)
SELECT t.wcid, p.type, p.value FROM acx_test t JOIN weenie_properties_bool p ON p.object_Id = 351;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
SELECT t.wcid, p.type, p.value FROM acx_test t JOIN weenie_properties_d_i_d p ON p.object_Id = 351;

-- name
INSERT INTO weenie_properties_string (object_Id, type, value)
SELECT wcid, 1, name FROM acx_test;

-- nether damage (DamageType 45 = 1024), damage 40 so it can fight, nether icon border (UiEffects 18 = 0x1000)
UPDATE weenie_properties_int p JOIN acx_test t ON p.object_Id = t.wcid SET p.value = 1024 WHERE p.type = 45;
UPDATE weenie_properties_int p JOIN acx_test t ON p.object_Id = t.wcid SET p.value = 40 WHERE p.type = 44;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT wcid, 18, 0x1000 FROM acx_test;

-- lasting effect: PhysicsEffectTable (DID 22), PhysicsScript (DID 30), PhysicsScriptIntensity (float 77)
UPDATE weenie_properties_d_i_d p JOIN acx_test t ON p.object_Id = t.wcid SET p.value = t.fx_table WHERE p.type = 22 AND t.fx_table IS NOT NULL;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT wcid, 30, fx_script FROM acx_test WHERE fx_script IS NOT NULL;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT wcid, 77, 1.0 FROM acx_test WHERE fx_script IS NOT NULL;

DROP TEMPORARY TABLE acx_test;
