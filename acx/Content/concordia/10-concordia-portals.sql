-- Project ACX: Concordia (JJ 2026-11-13: "what if we replaced the town network with here?"; "its more like a City";
-- name "Concordia", where the three peoples meet). The developers' Hotel Swank (landblock 018A: ~45 town portals, crafters, a lifestone, 118 bare rooms) becomes
-- the travel hub in place of the Town Network: every town's Town Network portal (4 portal weenies, 54 placed in 53
-- towns) is renamed and now leads to Concordia's arrival point. The Town Network itself stays, unreached.
-- The arrival point is retail's Hotel Swank point of interest, (120, -79.9) facing as it did, but in room 0x0273: the
-- POI's own cell id (0x024F) is wrong, which is why /teleloc to it ended in portal space (only /telepoi corrected it).
-- Re-runnable. Restart the server after applying.

-- the town portals: new name, new destination
UPDATE weenie_properties_string SET value = 'Concordia Portal' WHERE object_Id IN (42852, 43065, 43066, 43067) AND type = 1;
UPDATE weenie_properties_position
SET obj_Cell_Id = 0x018A0273, origin_X = 120, origin_Y = -79.9, origin_Z = 0.005, angles_W = -0.707107, angles_X = 0, angles_Y = 0, angles_Z = -0.707107
WHERE object_Id IN (42852, 43065, 43066, 43067) AND position_Type = 2;

-- the Town Network Portal Gem (sold by the archmages and Beldin) summons portal 42852, which now leads to Concordia
UPDATE weenie_properties_string SET value = 'Concordia Portal Gem' WHERE object_Id = 43020 AND type = 1;
UPDATE weenie_properties_string SET value = 'Use this gem to summon a short-lived portal to Concordia. This portal summoning gem works best if used outside in a relatively unobstructed area.' WHERE object_Id = 43020 AND type = 16;

-- the teleport list: Concordia by name (the Hotel names stay; the Town Network names now lead to Concordia as well)
INSERT INTO points_of_interest (name, weenie_Class_Id) VALUES ('Concordia', 43065)
ON DUPLICATE KEY UPDATE weenie_Class_Id = VALUES(weenie_Class_Id);

-- TO DO after JJ's walk-through (a second file): strip the developer leftovers (66 skill gems, 12 attribute gems,
-- 21 loot-test chests, 53 baby test monsters, Jeeves/Master Yado?, shortcut portals to Aerlinthe, Singularity Caul,
-- the Floating City, Vesayen Isles, Desert March, Jungle Shadows, Abandoned Mine?), add the missing town portals
-- (Fiun, Eastwatch, Neydisa Castle, Silyun, Westwatch, Sanamar, Marketplace, Facility Hub), move the vaults, the
-- Vault Keeper and the Mark Trader in, and point the starter towns' Adventurer's Haven portals (900708) here.
