-- Project ACX: one-time fix for axes that already exist (JJ 2026-11-13). New loot axes roll a tighter damage range
-- (acx_heavy/light/finesse_axe_variance_mod, LootGenerationFactory_AcxMutations.AcxAdjustAxeVariance); this applies the same
-- factors to looted one-handed axes already in the shard. Only variance >= 0.65 is touched: retail loot axes roll ~0.75-0.95,
-- store and /ci axes are 0.5, and a fixed axe drops below 0.65, so running it twice changes nothing.
-- Run against ace_shard on Live at commit (Preview done 2026-11-13). Damage variance = PropertyFloat 22.
-- Heavy axes (20 loot wcids, plain and elemental)
UPDATE biota_properties_float f JOIN biota b ON b.id = f.object_Id SET f.value = f.value * 0.58
WHERE f.type = 22 AND f.value >= 0.65 AND b.weenie_Class_Id IN (301, 344, 3750, 3751, 3752, 3753, 3865, 3866, 3867, 3868, 31763, 31764, 31765, 31766, 31767, 31768, 31769, 31770, 31771, 31772);
-- Light axes (20 loot wcids, plain and elemental)
UPDATE biota_properties_float f JOIN biota b ON b.id = f.object_Id SET f.value = f.value * 0.67
WHERE f.type = 22 AND f.value >= 0.65 AND b.weenie_Class_Id IN (303, 336, 359, 3754, 3755, 3756, 3757, 3842, 3843, 3844, 3845, 3905, 3906, 3907, 3908, 30561, 30562, 30563, 30564, 30565);
-- Finesse axes (15 loot wcids, plain and elemental)
UPDATE biota_properties_float f JOIN biota b ON b.id = f.object_Id SET f.value = f.value * 0.73
WHERE f.type = 22 AND f.value >= 0.65 AND b.weenie_Class_Id IN (342, 357, 3857, 3858, 3859, 3860, 3901, 3902, 3903, 3904, 30556, 30557, 30558, 30559, 30560);
-- Spears, all three classes (35 loot wcids): acx_spear_variance_mod 0.85 (JJ 2026-11-13 nudge). Loot spears roll ~0.65-0.75;
-- only >= 0.62 is touched, so store spears (0.5) are left alone and a second run changes nothing.
UPDATE biota_properties_float f JOIN biota b ON b.id = f.object_Id SET f.value = f.value * 0.85
WHERE f.type = 22 AND f.value >= 0.62 AND b.weenie_Class_Id IN (308, 348, 362, 3762, 3763, 3764, 3765, 3873, 3874, 3875, 3876, 3913, 3914, 3915, 3916, 7771, 7772, 7791, 7792, 7793, 7794, 7795, 7796, 7797, 7798, 30591, 30592, 30593, 30594, 30595, 31778, 31779, 31780, 31781, 31782);
