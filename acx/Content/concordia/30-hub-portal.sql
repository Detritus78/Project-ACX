-- Project ACX (2026-11-14): Concordia's hub is the Town Crier's hall on the second floor (JJ:
-- "Lets make the area with the crier the hub"). Arrivals land on the ground floor; a portal where Jeeves and Master
-- Yado stand takes you straight up to the hub (JJ: "I want a portal where jeeves and yado is standing to port directly
-- to the second floor"); Jeeves and Yado move up to the hub.
--   900727  Concordia Hub (a copy of the Town Network portal, 43065)
--   900728  First Floor: the way back down from the hub to the arrival hall (JJ 2026-11-14: "return back to first
--           floor"); JJ places it upstairs with /createinst 900728 (captured into 80-placements.sql)
-- Instance guids 0x7018AF00-0x7018AFFF are Project ACX's in Concordia (landblock 018A). Re-runnable.

DELETE FROM landblock_instance WHERE guid = 0x7018AF00;
DELETE FROM weenie WHERE class_Id IN (900727, 900728);

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900727, 'acx-concordia-hub-portal', type, NOW() FROM weenie WHERE class_Id = 43065;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900727, type, value FROM weenie_properties_int WHERE object_Id = 43065;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900727, type, value FROM weenie_properties_float WHERE object_Id = 43065;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900727, type, value FROM weenie_properties_bool WHERE object_Id = 43065;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900727, type, value FROM weenie_properties_string WHERE object_Id = 43065;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900727, type, value FROM weenie_properties_d_i_d WHERE object_Id = 43065;
UPDATE weenie_properties_string SET value = 'Concordia Hub' WHERE object_Id = 900727 AND type = 1;
-- it stands by the doorway up from downstairs, so only stepping into its middle ports you; brushing its edge doesn't
-- (JJ: "shrink the area that reacts to your prescence ... but leave the visual the same size"; AcxPortalTriggerRadius)
INSERT INTO weenie_properties_float (object_Id, type, value) VALUES (900727, 30005, 0.75);
-- to the Town Crier's hall, at the spot and facing JJ picked in game (/myloc 2026-11-14)
INSERT INTO weenie_properties_position (object_Id, position_Type, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z)
VALUES (900727, 2, 0x018A02C2, 19.85433, -24.465927, 6.005, 0.003231, 0, 0, 0.999995);

-- the portal where Jeeves and Master Yado stood, facing the way they did (JJ: "the portal will go in their spot")
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child)
VALUES (0x7018AF00, 900727, 0x018A0225, 96.2, -79.06, 0.005, 0.707107, 0, 0, -0.707107, 0);
-- Jeeves and Master Yado, who stood in the hub, are gone from Concordia (40-cleanup.sql, 2026-11-17).
-- the Town Crier and the Agent of the Arcanum where JJ placed them in the hub (read from Preview 2026-11-17)
UPDATE landblock_instance SET obj_Cell_Id = 0x018A02C8, origin_X = 18.75385, origin_Y = -35.20388, origin_Z = 6.005, angles_W = -0.004204, angles_X = 0, angles_Y = 0, angles_Z = -0.999991 WHERE guid = 0x7018A10D;   -- Town Crier
UPDATE landblock_instance SET obj_Cell_Id = 0x018A029F, origin_X = 17.3676, origin_Y = -42.6008, origin_Z = 6.005, angles_W = 0.911639, angles_X = 0, angles_Y = 0, angles_Z = -0.410993 WHERE guid = 0x7018A10E;   -- Agent of the Arcanum

-- First Floor: back to the arrival hall, where the town portals drop you
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900728, 'acx-concordia-first-floor-portal', type, NOW() FROM weenie WHERE class_Id = 43065;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900728, type, value FROM weenie_properties_int WHERE object_Id = 43065;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900728, type, value FROM weenie_properties_float WHERE object_Id = 43065;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900728, type, value FROM weenie_properties_bool WHERE object_Id = 43065;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900728, type, value FROM weenie_properties_string WHERE object_Id = 43065;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900728, type, value FROM weenie_properties_d_i_d WHERE object_Id = 43065;
UPDATE weenie_properties_string SET value = 'First Floor' WHERE object_Id = 900728 AND type = 1;
INSERT INTO weenie_properties_position (object_Id, position_Type, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z)
VALUES (900728, 2, 0x018A0273, 120.0, -79.9, 0.005, -0.707107, 0, 0, -0.707107);
