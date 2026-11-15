-- Project ACX: leased vaults (Managers/AcxVaults.cs), alpha (JJ 2026-11-13): the weenies. Since 2026-11-14 the vaults
-- stand in Concordia, placed by JJ with ACX Foundry; their placements (vaults, hooks, the Vault Keeper) are in
-- acx/Content/concordia/. Adventurer's Haven is retired: its furniture is in the client's cell data and can't be cleared
-- (JJ didn't want it), so the old Haven placements (20 vaults, their hooks, the keeper, and the "Adventurer's Haven"
-- portals by the four starter-town lifestones, which Concordia Portals replace) are removed below.
-- All storage is physical. A vault belongs to an account and opens for anyone carrying one of its keys.
-- The Vault Keeper sells the four vault items; each is used on a vault:
--   Vault Deed 100,000 (lease a free vault, 30 days paid), Vault Rent Writ 15,000 (+30 days, anyone may pay),
--   Blank Vault Key 1,000 (leaseholder cuts a key), Vault Lock 5,000 (leaseholder changes the lock, gets one new key).
-- Prices are placeholders for the money pass. Every item resells for 1 pyreal.
--   900700 Vault (retail house storage 9687)      900701 Vault Hook (retail wall hook 9686)
--   900702 Vault Deed   900703 Vault Rent Writ   900704 Blank Vault Key   900705 Vault Lock   900706 Vault Key (cut by code)
--   900707 Vault Keeper (retail Adventurer's Haven Cottages Merchant 26696)   900708 Adventurer's Haven portal (retail 2080)
-- Re-runnable. Restart the server after applying. A leased vault keeps its saved position: moving one later also needs
-- its shard biota moved.

DELETE FROM landblock_instance WHERE guid BETWEEN 0x701ACF00 AND 0x701ACFFF OR guid IN (0x7A9B4F20, 0x7DA55F20, 0x77D64F20, 0x733D9F20);
DELETE FROM weenie WHERE class_Id BETWEEN 900700 AND 900708;

DROP TEMPORARY TABLE IF EXISTS acx_vault_objects;
CREATE TEMPORARY TABLE acx_vault_objects (class_Id INT UNSIGNED PRIMARY KEY, original INT UNSIGNED, class_Name VARCHAR(100), name VARCHAR(100));
INSERT INTO acx_vault_objects VALUES
    (900700,  9687, 'acx-vault',        'Vault'),
    (900701,  9686, 'acx-vault-hook',   'Vault Hook'),
    (900707, 26696, 'acx-vault-keeper', 'Vault Keeper'),
    (900708,  2080, 'acx-haven-portal', 'Adventurer''s Haven');

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT o.class_Id, o.class_Name, w.type, NOW() FROM acx_vault_objects o JOIN weenie w ON w.class_Id = o.original;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_vault_objects o JOIN weenie_properties_int p ON p.object_Id = o.original;
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_vault_objects o JOIN weenie_properties_int64 p ON p.object_Id = o.original;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_vault_objects o JOIN weenie_properties_float p ON p.object_Id = o.original;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_vault_objects o JOIN weenie_properties_bool p ON p.object_Id = o.original;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_vault_objects o JOIN weenie_properties_string p ON p.object_Id = o.original;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_vault_objects o JOIN weenie_properties_d_i_d p ON p.object_Id = o.original;
INSERT INTO weenie_properties_i_i_d (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_vault_objects o JOIN weenie_properties_i_i_d p ON p.object_Id = o.original;
INSERT INTO weenie_properties_position (object_Id, position_Type, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z)
SELECT o.class_Id, p.position_Type, p.obj_Cell_Id, p.origin_X, p.origin_Y, p.origin_Z, p.angles_W, p.angles_X, p.angles_Y, p.angles_Z FROM acx_vault_objects o JOIN weenie_properties_position p ON p.object_Id = o.original;
INSERT INTO weenie_properties_attribute (object_Id, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) SELECT o.class_Id, p.`type`, p.`init_Level`, p.`level_From_C_P`, p.`c_P_Spent` FROM acx_vault_objects o JOIN weenie_properties_attribute p ON p.object_Id = o.original;
INSERT INTO weenie_properties_attribute_2nd (object_Id, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) SELECT o.class_Id, p.`type`, p.`init_Level`, p.`level_From_C_P`, p.`c_P_Spent`, p.`current_Level` FROM acx_vault_objects o JOIN weenie_properties_attribute_2nd p ON p.object_Id = o.original;
INSERT INTO weenie_properties_skill (object_Id, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) SELECT o.class_Id, p.`type`, p.`level_From_P_P`, p.`s_a_c`, p.`p_p`, p.`init_Level`, p.`resistance_At_Last_Check`, p.`last_Used_Time` FROM acx_vault_objects o JOIN weenie_properties_skill p ON p.object_Id = o.original;
INSERT INTO weenie_properties_body_part (object_Id, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`)
SELECT o.class_Id, p.`key`, p.`d_Type`, p.`d_Val`, p.`d_Var`, p.`base_Armor`, p.`armor_Vs_Slash`, p.`armor_Vs_Pierce`, p.`armor_Vs_Bludgeon`, p.`armor_Vs_Cold`, p.`armor_Vs_Fire`, p.`armor_Vs_Acid`, p.`armor_Vs_Electric`, p.`armor_Vs_Nether`, p.`b_h`, p.`h_l_f`, p.`m_l_f`, p.`l_l_f`, p.`h_r_f`, p.`m_r_f`, p.`l_r_f`, p.`h_l_b`, p.`m_l_b`, p.`l_l_b`, p.`h_r_b`, p.`m_r_b`, p.`l_r_b` FROM acx_vault_objects o JOIN weenie_properties_body_part p ON p.object_Id = o.original;

UPDATE weenie_properties_string s JOIN acx_vault_objects o ON s.object_Id = o.class_Id SET s.value = o.name WHERE s.type = 1;
-- a vault hook has a (placeholder) house owner: the client only lets items be hung on a hook whose house has an owner
-- (retail ItemHolder::CheckHookStatus, as AC:Unreal does it), and vault hooks have no house, so nothing would hang (JJ
-- 2026-11-14: "its not letting me hang any weapons or anything in my pack"). Who may use the hook is still the vault's
-- leaseholder and key holders (AcxVaults.CheckHookUse); what hangs is still retail's rule: items made for wall hooks.
INSERT INTO weenie_properties_i_i_d (object_Id, type, value) VALUES (900701, 32, 1);   -- HouseOwner
-- the vault: a main-pack-sized compartment with bag slots (the code also sets both from acx_vault_item_slots/bag_slots)
UPDATE weenie_properties_int SET value = 102 WHERE object_Id = 900700 AND type = 6;   -- ItemsCapacity
UPDATE weenie_properties_int SET value = 7 WHERE object_Id = 900700 AND type = 7;     -- ContainersCapacity
-- the keeper sells at the items' values and only the vault items
UPDATE weenie_properties_float SET value = 1.0 WHERE object_Id = 900707 AND type = 38;   -- SellPrice
UPDATE weenie_properties_string SET value = 'Vault Keeper' WHERE object_Id = 900707 AND type = 5;   -- Title
INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond) VALUES
    (900707, 4, 900702, -1, 0, 0, 0),
    (900707, 4, 900703, -1, 0, 0, 0),
    (900707, 4, 900704, -1, 0, 0, 0),
    (900707, 4, 900705, -1, 0, 0, 0);
-- the Vault Keeper wears what retail's Covenant-armoured Warden (42139) wears: the cottage merchant he's copied from has
-- no clothes in the world data (JJ 2026-11-14: "I put a vault keeper downstairs and hes naked")
INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond)
SELECT 900707, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond FROM weenie_properties_create_list WHERE object_Id = 42139 AND destination_Type = 2;

-- the vault items: plain generic items carrying AcxVaultTool (int 30016): 1 deed, 2 rent writ, 3 blank key, 4 lock;
-- the cut key carries AcxVaultKeyFor/AcxVaultLockCode (set by code)
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) VALUES
    (900702, 'acx-vault-deed', 1, NOW()),
    (900703, 'acx-vault-rent-writ', 1, NOW()),
    (900704, 'acx-vault-blank-key', 1, NOW()),
    (900705, 'acx-vault-lock', 1, NOW()),
    (900706, 'acx-vault-key', 1, NOW());

INSERT INTO weenie_properties_int (object_Id, type, value)
SELECT w.id, p.type, CASE p.type WHEN 19 THEN w.price WHEN 30016 THEN w.tool WHEN 16 THEN w.useable ELSE p.value END
FROM (SELECT 900702 id, 1 tool, 100000 price, 2097160 useable UNION ALL SELECT 900703, 2, 15000, 2097160
      UNION ALL SELECT 900704, 3, 1000, 2097160 UNION ALL SELECT 900705, 4, 5000, 2097160 UNION ALL SELECT 900706, 0, 1, 1) w
CROSS JOIN (SELECT 1 type, 128 value          -- ItemType: misc
  UNION ALL SELECT 5, 5                       -- EncumbranceVal
  UNION ALL SELECT 9, 0                       -- ValidLocations
  UNION ALL SELECT 16, 0                      -- ItemUseable: from the pack, on a vault (the key isn't used)
  UNION ALL SELECT 19, 0                      -- Value
  UNION ALL SELECT 93, 1044                   -- PhysicsState
  UNION ALL SELECT 94, 512                    -- TargetType: containers (the vault)
  UNION ALL SELECT 30016, 0                   -- AcxVaultTool
) p
WHERE NOT (w.tool = 0 AND p.type IN (94, 30016));

INSERT INTO weenie_properties_bool (object_Id, type, value)
SELECT id, 23, 1 FROM (SELECT 900702 id UNION ALL SELECT 900703 UNION ALL SELECT 900704 UNION ALL SELECT 900705 UNION ALL SELECT 900706) w;   -- DestroyOnSell

INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES
    (900702, 1, 0x02000B8B), (900702, 8, 0x060022DD), (900702, 22, 0x3400002B),                            -- deed: Writ of Refuge look
    (900703, 1, 0x02000B8B), (900703, 8, 0x060022DD), (900703, 22, 0x3400002B), (900703, 52, 0x06003355),  -- rent writ: same, coloured
    (900704, 1, 0x02000160), (900704, 8, 0x0600105E), (900704, 22, 0x3400002B),                            -- blank key: retail key look
    (900705, 1, 0x02000160), (900705, 8, 0x0600105E), (900705, 22, 0x3400002B), (900705, 52, 0x06003359),  -- lock: same, coloured
    (900706, 1, 0x02000160), (900706, 8, 0x0600105E), (900706, 22, 0x3400002B), (900706, 52, 0x06003353);  -- cut key: same, coloured

INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900702, 1, 'Vault Deed'),
    (900703, 1, 'Vault Rent Writ'),
    (900704, 1, 'Blank Vault Key'),
    (900705, 1, 'Vault Lock'),
    (900706, 1, 'Vault Key'),
    (900702, 14, 'Use this on a free vault in Concordia to lease it, with 30 days of rent paid. The vault opens for every character on your account and for anyone carrying one of its keys.'),
    (900703, 14, 'Use this on a leased vault in Concordia to pay 30 more days of rent. Anyone can pay a vault''s rent; an overdue vault is lost once its grace runs out.'),
    (900704, 14, 'Use this on the vault you lease to cut a key to it. Whoever carries the key can open the vault and its hook.'),
    (900705, 14, 'Use this on the vault you lease to change its lock: every old key stops working, and you get one new key.'),
    (900706, 14, 'Whoever carries this key can open the vault named on it, and that vault''s hook, until its lock is changed.'),
    (900702, 16, 'A lease on one of the vaults in Concordia.'),
    (900703, 16, 'A month''s rent for a vault in Concordia.'),
    (900704, 16, 'An uncut key for a vault in Concordia.'),
    (900705, 16, 'A new lock for a vault in Concordia.'),
    (900706, 16, 'A key to a vault in Concordia.');
