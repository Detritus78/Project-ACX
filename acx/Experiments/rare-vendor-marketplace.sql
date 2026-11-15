-- Project ACX experiment: a copy of retail's Melee Rare Vendor (45485, normally in Arwic) in the Marketplace, so JJ can see
-- what it sells (Preview only; never in acx/Content). It sells 35 rares for 1 pyreal each (SellPrice 0), and they can be
-- sold back to vendors, which is the money loophole being looked at.
-- It stands beside the salvage test vendors (016C0146); the spot by the test chests at the arrival point was blocked.
-- Re-runnable. Restart the server (or /reload-landblock in the Marketplace) after applying.

DELETE FROM landblock_instance WHERE guid = 0x7016C009;
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    (0x7016C009, 45485, 0x016C0146, 12.2, -11.4, 0.005, 0, 0, 0, 1, 0);
