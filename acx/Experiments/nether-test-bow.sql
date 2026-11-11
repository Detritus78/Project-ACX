-- Project ACX experiment: nether missile test (Preview only; never in acx/Content).
--   900012  Nether Test Bow: a Longbow (306) with nether as its element.
-- Arrows carry the damage type, except Prismatic Arrows (43952, damage type Base), which take the bow's element,
-- so fire Prismatic Arrows from it to deal nether damage. Re-runnable.
-- Apply: docker compose -p acx-preview ... exec -T db mysql ace_world < this file

DELETE FROM weenie WHERE class_Id = 900012;

INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
SELECT 900012, 'acx-test-nether-bow', type, NOW() FROM weenie WHERE class_Id = 306;

-- copy the Longbow's properties
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900012, type, value FROM weenie_properties_int WHERE object_Id = 306;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900012, type, value FROM weenie_properties_float WHERE object_Id = 306;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900012, type, value FROM weenie_properties_bool WHERE object_Id = 306;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900012, type, value FROM weenie_properties_d_i_d WHERE object_Id = 306;

-- name
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES (900012, 1, 'Nether Test Bow');

-- nether element (DamageType 45 = 1024), nether icon border (UiEffects 18 = 0x1000)
DELETE FROM weenie_properties_int WHERE object_Id = 900012 AND type IN (18, 45);
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES (900012, 45, 1024), (900012, 18, 0x1000);
