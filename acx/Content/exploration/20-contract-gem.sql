-- Project ACX: the Contract Portal Gem for exploration contracts (Managers/AcxContracts.cs). A copy of retail's
-- Holtburg Portal Gem (8976) without its spell: the server reads the contract (AcxContractId, string 30007, set on each
-- gem) and takes the player to that dungeon's entrance. Bonded and attuned, worth nothing, one per stack. It decays
-- 24 hours after it's handed out, like retail's Legendary Keys (Lifespan, int 267), and its contract ends with it.
--   900550  Contract Portal Gem
-- Re-runnable. Restart the server after applying.

DELETE FROM weenie WHERE class_Id = 900550;

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900550, 'acx-contract-portal-gem', type, NOW() FROM weenie WHERE class_Id = 8976;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900550, type, value FROM weenie_properties_int WHERE object_Id = 8976;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900550, type, value FROM weenie_properties_float WHERE object_Id = 8976;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900550, type, value FROM weenie_properties_bool WHERE object_Id = 8976;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900550, type, value FROM weenie_properties_string WHERE object_Id = 8976;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900550, type, value FROM weenie_properties_d_i_d WHERE object_Id = 8976;

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 900550 AND type = 28;                -- no spell (Spell)
DELETE FROM weenie_properties_string WHERE object_Id = 900550 AND type IN (14, 16);        -- Use, LongDesc: set per gem
DELETE FROM weenie_properties_int WHERE object_Id = 900550 AND type IN (11, 19, 33, 114, 267);  -- MaxStackSize, Value, Bonded, Attuned, Lifespan
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES (900550, 11, 1), (900550, 19, 0), (900550, 33, 1), (900550, 114, 1), (900550, 267, 86400);
UPDATE weenie_properties_string SET value = 'Contract Portal Gem' WHERE object_Id = 900550 AND type = 1;
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES (900550, 14, 'Use this gem to go to the entrance of your contract''s dungeon.');
