-- Project ACX: the retail Free Weapon Vendor (45489, landblock C6A9) is taken out of the world (JJ 2026-11-15: "Remove
-- the free weapon vendor that isn't actually free cause it takes rare coins"). It sold wield-400 Shadowfire Isparian weapons
-- that bench well above loot of their wield requirement. Only its one placement goes; the weenie stays.
-- Re-runnable.
DELETE FROM landblock_instance_link WHERE parent_GUID = 0x7C6A904A OR child_GUID = 0x7C6A904A;
DELETE FROM landblock_instance WHERE guid = 0x7C6A904A AND weenie_Class_Id = 45489;
