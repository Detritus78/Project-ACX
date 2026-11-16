-- Project ACX (2026-11-15): Mahogany on Eternal thrown weapons (JJ: "We want to be able to use mahogany on it like bows
-- to increase the damage"). A thrown weapon is a launcher and its own ammunition, with a damage multiplier like a bow's
-- (10-eternal-thrown.sql; Factories/Tables/Wcids/Weapons/AcxThrownWcids.cs), and Mahogany adds 4% to it per tinker, as on
-- a bow (recipe 3855: needs workmanship, at most 10 tinkers). Salvage finds its recipe by cook_book row, one per
-- weapon, as for retail's launchers: every Mahogany item that works on an Atlatl (12463) gets a row for each of the 42
-- thrown weapons. (RecipeManager_New.cs also lets Mahogany through for a thrown weapon without a row.)
-- Re-runnable. The Preview test bags copy these rows: re-run acx/Experiments/salvage-test-vendor.sql after this file.
DELETE FROM cook_book WHERE recipe_Id = 3855 AND target_W_C_I_D BETWEEN 900810 AND 900851 AND source_W_C_I_D < 900000;
INSERT INTO cook_book (recipe_Id, source_W_C_I_D, target_W_C_I_D, last_Modified)
SELECT c.recipe_Id, c.source_W_C_I_D, w.class_Id, NOW()
FROM cook_book c JOIN weenie w ON w.class_Id BETWEEN 900810 AND 900851
WHERE c.recipe_Id = 3855 AND c.target_W_C_I_D = 12463 AND c.source_W_C_I_D < 900000;
