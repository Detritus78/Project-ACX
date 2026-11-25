-- Project ACX: Facility Hub Portal Gems that already exist get the hook properties the weenie now has
-- (Content/specials/50-facility-hub-gem-hook.sql): HookType 2 (PropertyInt 151), HookPlacement 103 (PropertyInt 150).
-- Run against ace_shard with the server stopped or the owners offline; re-runnable. On Live 2026-11-24 for the gems of
-- offline players; the rest at the next push.
DELETE p FROM biota_properties_int p JOIN biota b ON b.id = p.object_Id WHERE b.weenie_Class_Id = 49563 AND p.type IN (150, 151);
INSERT INTO biota_properties_int (object_Id, type, value) SELECT id, 151, 2 FROM biota WHERE weenie_Class_Id = 49563;
INSERT INTO biota_properties_int (object_Id, type, value) SELECT id, 150, 103 FROM biota WHERE weenie_Class_Id = 49563;
