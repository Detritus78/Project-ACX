-- Project ACX: mana stones can be used on a pack, charging every item in it (WorldObjects/ManaStone_AcxPack.cs).
-- Adds containers (ItemType 0x200) to every mana stone's TargetType (int 94), so the client offers packs as targets.
-- Re-runnable. Restart the server after applying.

UPDATE weenie_properties_int t JOIN weenie w ON w.class_Id = t.object_Id
SET t.value = t.value | 512
WHERE w.type = 37 AND t.type = 94;   -- WeenieType 37 = ManaStone
