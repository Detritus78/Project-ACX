-- Project ACX (2026-11-14): The respec temple portals, the two big stone heads JJ places in
-- Concordia in place of the developers' loose skill/attribute gems: Temple of Enlightenment (22755) and Temple of
-- Forgetfulness (22759), shrunk to half size so they fit (JJ: "Lets shrink the head by 50%"). Retail never placed
-- these two portals, so changing their scale affects nothing else. Re-runnable.
DELETE FROM weenie_properties_float WHERE object_Id IN (22755, 22759) AND type = 39;
INSERT INTO weenie_properties_float (object_Id, type, value) VALUES (22755, 39, 0.5), (22759, 39, 0.5);   -- DefaultScale
