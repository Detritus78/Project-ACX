-- Project ACX: every Phantom weapon comes with Biting Strike (JJ 2026-11-15: "what if we cut it but add a [biting strike]
-- cleave to everyone after its made", then "lets to 85% with the biting strike"). Phantom weapons are made by Bloodhunter Oil
-- on a Ravenous weapon; the oil's recipes hand out these weenies, so new ones carry it. 15% critical chance, the same as the
-- ACX loot Biting Strike roll (LootGenerationFactory_AcxMutations). Their armor ignore is cut to 85% in code
-- (WorldObject.AcxIgnoreAllArmorMod).
-- Re-runnable.
DELETE FROM weenie_properties_float WHERE type = 147 AND object_Id IN (21353, 21354, 21355, 21356, 21357, 21358, 21359, 41708, 21963, 21964, 21965, 31705);
INSERT INTO weenie_properties_float (object_Id, type, value) VALUES
    (21353, 147, 0.15),   -- Phantom Axe
    (21354, 147, 0.15),   -- Phantom Dagger
    (21355, 147, 0.15),   -- Phantom Katar
    (21356, 147, 0.15),   -- Phantom Mace
    (21357, 147, 0.15),   -- Phantom Staff
    (21358, 147, 0.15),   -- Phantom Spear
    (21359, 147, 0.15),   -- Phantom Sword
    (41708, 147, 0.15),   -- Phantom Two Handed Spear
    (21963, 147, 0.15),   -- Phantom Atlatl
    (21964, 147, 0.15),   -- Phantom Bow
    (21965, 147, 0.15),   -- Phantom Crossbow
    (31705, 147, 0.15);   -- Phantom Bow
