-- Project ACX experiment: nether melee visual test (Preview only; never in acx/Content).
-- Seven copies of the Long Sword (351) that deal nether damage:
--   900001  A: Penumbral Horror shadow effect  (effect table 0x34000063, script 0x56)
--   900002  B: Umbral Mukkir shadow effect     (effect table 0x340000B9, script 0x55)
--   900003  C: plain nether, no lasting effect (control)
--   900004  D: black blade (palette Black, shade 0) and black icon
--   900005  E: black blade (palette Black, shade 1) and black icon
--   900006  F: black blade, 35% see-through (Translucency, like retail's crystal weapons)
--   900007  G: black blade, 60% see-through
-- All seven get the nether icon border. Re-runnable.
-- Apply: docker compose -p acx-preview ... exec -T db mysql ace_world < this file

DELETE FROM weenie WHERE class_Id IN (900001, 900002, 900003, 900004, 900005, 900006, 900007);

DROP TEMPORARY TABLE IF EXISTS acx_test;
CREATE TEMPORARY TABLE acx_test (wcid INT UNSIGNED, class_name VARCHAR(100), name VARCHAR(100), fx_table INT UNSIGNED, fx_script INT UNSIGNED, palette INT UNSIGNED, shade DOUBLE, icon INT UNSIGNED, translucency DOUBLE);
INSERT INTO acx_test VALUES
    (900001, 'acx-test-nether-sword-a', 'Nether Test Sword A (Penumbral)', 0x34000063, 0x56, NULL, NULL, NULL, NULL),
    (900002, 'acx-test-nether-sword-b', 'Nether Test Sword B (Umbral)',    0x340000B9, 0x55, NULL, NULL, NULL, NULL),
    (900003, 'acx-test-nether-sword-c', 'Nether Test Sword C (plain)',     NULL,       NULL, NULL, NULL, NULL, NULL),
    (900004, 'acx-test-nether-sword-d', 'Nether Test Sword D (black 0)',   NULL,       NULL, 39,   0.0,  0x06001669, NULL),
    (900005, 'acx-test-nether-sword-e', 'Nether Test Sword E (black 1)',   NULL,       NULL, 39,   1.0,  0x06001669, NULL),
    (900006, 'acx-test-nether-sword-f', 'Nether Test Sword F (shadow 35)', NULL,     NULL, 39,   0.0,  0x06001669, 0.35),
    (900007, 'acx-test-nether-sword-g', 'Nether Test Sword G (shadow 60)', NULL,     NULL, 39,   0.0,  0x06001669, 0.6);

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

-- black blade: PaletteTemplate (int 3) = Black (39, offered by the Long Sword's clothing table 0x10000139), Shade (float 12), black icon (DID 8)
UPDATE weenie_properties_int p JOIN acx_test t ON p.object_Id = t.wcid SET p.value = t.palette WHERE p.type = 3 AND t.palette IS NOT NULL;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT wcid, 12, shade FROM acx_test WHERE shade IS NOT NULL;
UPDATE weenie_properties_d_i_d p JOIN acx_test t ON p.object_Id = t.wcid SET p.value = t.icon WHERE p.type = 8 AND t.icon IS NOT NULL;

-- see-through blade: Translucency (float 76), as retail's crystal weapons use
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT wcid, 76, translucency FROM acx_test WHERE translucency IS NOT NULL;

DROP TEMPORARY TABLE acx_test;

-- Texture swaps (weenie_properties_texture_map, part 0). The Long Sword uses SurfaceTextures 05000C53/54/55;
-- each of H/I/J paints one of them deep purple (0500303D, avg #2F004E) to find the blade, K paints all three
-- near-black violet (050030C6, avg #0C032B). Copies of C (900003).
DELETE FROM weenie WHERE class_Id IN (900008, 900009, 900010, 900011);
DROP TEMPORARY TABLE IF EXISTS acx_tex;
CREATE TEMPORARY TABLE acx_tex (wcid INT UNSIGNED, class_name VARCHAR(100), name VARCHAR(100));
INSERT INTO acx_tex VALUES
    (900008, 'acx-test-nether-sword-h', 'Nether Test Sword H (purple C53)'),
    (900009, 'acx-test-nether-sword-i', 'Nether Test Sword I (purple C54)'),
    (900010, 'acx-test-nether-sword-j', 'Nether Test Sword J (purple C55)'),
    (900011, 'acx-test-nether-sword-k', 'Nether Test Sword K (all violet)');
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT t.wcid, t.class_name, w.type, NOW() FROM acx_tex t JOIN weenie w ON w.class_Id = 900003;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT t.wcid, p.type, p.value FROM acx_tex t JOIN weenie_properties_int p ON p.object_Id = 900003;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT t.wcid, p.type, p.value FROM acx_tex t JOIN weenie_properties_float p ON p.object_Id = 900003;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT t.wcid, p.type, p.value FROM acx_tex t JOIN weenie_properties_bool p ON p.object_Id = 900003;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT t.wcid, p.type, p.value FROM acx_tex t JOIN weenie_properties_d_i_d p ON p.object_Id = 900003;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT wcid, 1, name FROM acx_tex;
INSERT INTO weenie_properties_texture_map (object_Id, `index`, old_Id, new_Id) VALUES
    (900008, 0, 0x05000C53, 0x0500303D),
    (900009, 0, 0x05000C54, 0x0500303D),
    (900010, 0, 0x05000C55, 0x0500303D),
    (900011, 0, 0x05000C53, 0x050030C6),
    (900011, 0, 0x05000C54, 0x050030C6),
    (900011, 0, 0x05000C55, 0x050030C6);
DROP TEMPORARY TABLE acx_tex;
