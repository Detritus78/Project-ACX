-- Project ACX (2026-11-14): Clearing the developers' leftovers out of Concordia (Hotel Swank,
-- landblock 018A), per JJ:
--  * "lose the dev jewelry, lose the spawns": the generator of the 3 jewellery pieces and 27 baby test monsters, and the
--    monster generator of 26 more babies, go with their children and links. The respec gems by the lifestone stay (JJ,
--    after first removing them: "lets put the gems back and place a 1hr pick up timer on them", 25-respec-gems.sql).
--    The NPCs we keep that two monster generators spawned (0x7018A0BB: Master Yado; 0x7018A10A: Town Crier, Agent of
--    the Arcanum, Aun Xathurea the Leather Crafter) stand on their own instead and the generators go (JJ: "I can't move
--    master yoda and the town crier in the hub"): /nudge only moves landblock instances, a generator's spawns get new
--    guids each time, and an NPC never needs respawning.
--  * Portals: "V isles goes sing caul goes floating city goes surface goes", and Jungle Shadows ("Get rid of jungle
--    shadows then"), and the Abandoned Mine ("get rid of abandoned mine then also. So just keep desert march"); every
--    one of those places keeps its normal entrance elsewhere. Desert March stays: the gateway to the Direlands.
--  * The 21 loot-test chests in the bottom room below the lifestone ("remove those chests"; "The gambling chests will
--    go by the gamblers": the dens in 60-gambling-dens.sql have their own Golden Chests). Legendary chests take their
--    spots (45-legendary-chests.sql).
-- Re-runnable (deleting what is already gone changes nothing).

DELETE c FROM landblock_instance c JOIN landblock_instance_link k ON k.child_Guid = c.guid
WHERE k.parent_Guid IN (0x7018A0BA, 0x7018A119);
DELETE FROM landblock_instance_link WHERE parent_Guid IN (0x7018A0BA, 0x7018A119);
DELETE FROM landblock_instance WHERE guid IN (0x7018A0BA, 0x7018A119);

UPDATE landblock_instance SET is_Link_Child = 0 WHERE guid IN (0x7018A0B6, 0x7018A10D, 0x7018A10E, 0x7018A10F);   -- Yado, Crier, Agent, Aun Xathurea
DELETE FROM landblock_instance_link WHERE parent_Guid IN (0x7018A0BB, 0x7018A10A);
DELETE FROM landblock_instance WHERE guid IN (0x7018A0BB, 0x7018A10A);

DELETE FROM landblock_instance WHERE guid IN (
    0x7018A0A1,   -- Vesayen Isles Portal
    0x7018A0A2,   -- Singularity Caul
    0x7018A068,   -- The Floating City
    0x7018A061,   -- Surface
    0x7018A097,   -- Surface
    0x7018A0F0,   -- Surface
    0x7018A066,   -- Jungle Shadows
    0x7018A060,   -- Abandoned Mine (the old Subway: its town portals are retail's destroyed, unusable ones)
    0x7018A0D5,   -- Aerlinthe Island Portal ("remove aerlinthe")
    0x7000707E);  -- the Town Network's portal to the Marketplace (JJ: remove "any portals going to" the Marketplace)

DELETE FROM landblock_instance WHERE guid IN (
    0x7018A072, 0x7018A073, 0x7018A074,   -- Steel Chest (Sturdy Iron Key)
    0x7018A075, 0x7018A076, 0x7018A077,   -- Sturdy Steel Chest (Sturdy Steel Key)
    0x7018A06B, 0x7018A06C, 0x7018A06D,   -- Reinforced Mahogany Chest (Granite Key)
    0x7018A06F, 0x7018A070, 0x7018A071,   -- Reinforced Oaken Chest (Marble Key)
    0x7018A063, 0x7018A069, 0x7018A062,   -- Monty's, Arshid's and Gan-Zo's Golden Chests (the dens have their own)
    0x7018A064, 0x7018A065,               -- Directive's Cache, Master's Holding
    0x7018A06A, 0x7018A06E,               -- Singularity Trove
    0x7018A12B, 0x7018A12C);              -- Black Marrow Reliquary
