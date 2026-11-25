-- Project ACX (2026-11-24): the Facility Hub Portal Gem (49563) goes on a house hook (JJ: "can you make it so the facility
-- hub gem can go on a hook on live?"). It had no HookType; it gets what retail's hookable portal gems carry (the Al-Arqas
-- Portal Gem 8973): HookType 2 (PropertyInt 151) and HookPlacement 103 (PropertyInt 150). Gems that already exist:
-- shard-migrations/2026-11-24-facility-hub-gem-hook.sql. Re-runnable.
DELETE FROM weenie_properties_int WHERE object_Id = 49563 AND type IN (150, 151);
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES (49563, 151, 2), (49563, 150, 103);
