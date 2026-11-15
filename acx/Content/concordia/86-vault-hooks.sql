-- Project ACX (2026-11-14): a Vault Hook on the wall behind every vault in Concordia (JJ: "none of the hooks are on a
-- wall they are just floating"; they used to hang 1.3 m above the vault, where they also got in the way of moving it).
-- Each hook is flush on the nearest wall of the vault's room that isn't a doorway, facing into the room (the model's
-- front is its -Y side), just above the vault's lid and at least 1.5 m up. A hook belongs to its nearest vault (AcxVaults, within 4 m). Generated from the vaults'
-- places on Preview and the DAT's walls by Claude's vault_hooks.py; regenerate after vaults move. Guids
-- 0x7018AA00-0x7018AAFF. Re-runnable.
DELETE FROM landblock_instance WHERE guid BETWEEN 0x7018AA00 AND 0x7018AAFF;
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    (0x7018AA00, 900701, 0x018A0251, 98.660, -124.440, 1.505, 0.000000, 0, 0, -1.000000, 0),   -- hook for vault 0x7018A80D, 0.4 m behind it
    (0x7018AA01, 900701, 0x018A0251, 101.340, -124.440, 1.505, 0.000000, 0, 0, 1.000000, 0),   -- hook for vault 0x7018A810, 0.4 m behind it
    (0x7018AA02, 900701, 0x018A0251, 95.560, -121.340, 1.505, 0.707107, 0, 0, 0.707107, 0),   -- hook for vault 0x7018A813, 0.4 m behind it
    (0x7018AA03, 900701, 0x018A0251, 95.560, -118.660, 1.505, 0.707107, 0, 0, 0.707107, 0),   -- hook for vault 0x7018A816, 0.4 m behind it
    (0x7018AA04, 900701, 0x018A0251, 95.560, -123.737, 1.505, 0.707107, 0, 0, 0.707107, 0),   -- hook for vault 0x7018A818, 0.8 m behind it
    (0x7018AA05, 900701, 0x018A0251, 95.560, -116.263, 1.505, 0.707107, 0, 0, 0.707107, 0),   -- hook for vault 0x7018A81A, 0.8 m behind it
    (0x7018AA06, 900701, 0x018A0251, 104.440, -123.737, 1.505, 0.707107, 0, 0, -0.707107, 0),   -- hook for vault 0x7018A81C, 0.8 m behind it
    (0x7018AA07, 900701, 0x018A0251, 103.737, -115.560, 1.505, 1.000000, 0, 0, -0.000000, 0),   -- hook for vault 0x7018A81E, 0.8 m behind it
    (0x7018AA08, 900701, 0x018A0267, 108.660, -124.440, 1.505, 0.000000, 0, 0, -1.000000, 0),   -- hook for vault 0x7018A82D, 0.4 m behind it
    (0x7018AA09, 900701, 0x018A0267, 111.340, -124.440, 1.505, 0.000000, 0, 0, 1.000000, 0),   -- hook for vault 0x7018A830, 0.4 m behind it
    (0x7018AA0A, 900701, 0x018A0267, 105.560, -123.737, 1.505, 0.707107, 0, 0, 0.707107, 0),   -- hook for vault 0x7018A838, 0.8 m behind it
    (0x7018AA0B, 900701, 0x018A0267, 106.264, -115.560, 1.505, 1.000000, 0, 0, 0.000000, 0),   -- hook for vault 0x7018A83A, 0.8 m behind it
    (0x7018AA0C, 900701, 0x018A0267, 114.440, -123.737, 1.505, 0.707107, 0, 0, -0.707107, 0),   -- hook for vault 0x7018A83C, 0.8 m behind it
    (0x7018AA0D, 900701, 0x018A0267, 114.440, -116.263, 1.505, 0.707107, 0, 0, -0.707107, 0),   -- hook for vault 0x7018A83E, 0.8 m behind it
    (0x7018AA0E, 900701, 0x018A027E, 124.440, -121.340, 1.505, 0.707107, 0, 0, -0.707107, 0),   -- hook for vault 0x7018A847, 0.4 m behind it
    (0x7018AA0F, 900701, 0x018A027E, 124.440, -118.660, 1.505, 0.707107, 0, 0, -0.707107, 0),   -- hook for vault 0x7018A84A, 0.4 m behind it
    (0x7018AA10, 900701, 0x018A027E, 118.629, -124.440, 1.505, 0.000000, 0, 0, -1.000000, 0),   -- hook for vault 0x7018A84D, 0.4 m behind it
    (0x7018AA11, 900701, 0x018A027E, 121.340, -124.440, 1.505, 0.000000, 0, 0, 1.000000, 0),   -- hook for vault 0x7018A850, 0.4 m behind it
    (0x7018AA12, 900701, 0x018A01A8, 143.712, -57.291, -10.495, 0.884926, 0, 0, -0.465731, 0),   -- hook for vault 0x7018A858, 2.4 m behind it
    (0x7018AA13, 900701, 0x018A01A8, 136.289, -62.711, -10.495, 0.465731, 0, 0, 0.884926, 0),   -- hook for vault 0x7018A85F, 2.4 m behind it
    (0x7018AA14, 900701, 0x018A01AD, 143.711, -77.289, -10.495, 0.884926, 0, 0, -0.465731, 0),   -- hook for vault 0x7018A866, 2.4 m behind it
    (0x7018AA15, 900701, 0x018A01AD, 136.289, -82.711, -10.495, 0.465731, 0, 0, 0.884926, 0),   -- hook for vault 0x7018A86D, 2.4 m behind it
    (0x7018AA16, 900701, 0x018A01EA, 153.711, -57.289, -10.495, 0.884926, 0, 0, -0.465731, 0),   -- hook for vault 0x7018A874, 2.4 m behind it
    (0x7018AA17, 900701, 0x018A01EA, 146.289, -62.711, -10.495, 0.465731, 0, 0, 0.884926, 0),   -- hook for vault 0x7018A87B, 2.4 m behind it
    (0x7018AA18, 900701, 0x018A01EF, 153.711, -77.289, -10.495, 0.884926, 0, 0, -0.465731, 0),   -- hook for vault 0x7018A882, 2.4 m behind it
    (0x7018AA19, 900701, 0x018A01EF, 146.289, -82.711, -10.495, 0.465731, 0, 0, 0.884926, 0),   -- hook for vault 0x7018A889, 2.4 m behind it
    (0x7018AA1A, 900701, 0x018A01F4, 163.711, -57.289, -10.495, 0.884926, 0, 0, -0.465731, 0),   -- hook for vault 0x7018A890, 2.4 m behind it
    (0x7018AA1B, 900701, 0x018A01F4, 156.289, -62.711, -10.495, 0.465731, 0, 0, 0.884926, 0),   -- hook for vault 0x7018A897, 2.4 m behind it
    (0x7018AA1C, 900701, 0x018A01F9, 163.711, -77.289, -10.495, 0.884926, 0, 0, -0.465731, 0),   -- hook for vault 0x7018A89E, 2.4 m behind it
    (0x7018AA1D, 900701, 0x018A01F9, 156.289, -82.711, -10.495, 0.465731, 0, 0, 0.884926, 0),   -- hook for vault 0x7018A8A5, 2.4 m behind it
    (0x7018AA1E, 900701, 0x018A027E, 123.595, -115.560, 1.505, 1.000000, 0, 0, -0.000000, 0),   -- hook for vault 0x7018AF3A, 0.9 m behind it
    (0x7018AA1F, 900701, 0x018A027E, 124.440, -123.805, 1.505, 0.707107, 0, 0, -0.707107, 0),   -- hook for vault 0x7018AF3C, 0.7 m behind it
    (0x7018AA20, 900701, 0x018A027E, 116.264, -124.440, 1.505, 0.000000, 0, 0, -1.000000, 0),   -- hook for vault 0x7018AF3E, 0.8 m behind it
    (0x7018AA21, 900701, 0x018A027E, 115.560, -115.980, 1.505, 0.707107, 0, 0, 0.707107, 0);   -- hook for vault 0x7018AF40, 0.5 m behind it
