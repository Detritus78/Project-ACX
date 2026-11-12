-- Project ACX: Silk adds 10 armor level to underclothes, and the protection salvages work on them too.
-- Underclothes = the looted shirts, pants, tunics, breeches and the like (26 items: Baggy Breeches, Baggy Pants, Baggy Shirt, Baggy Tunic, Doublet, Flared Pants, Flared Shirt, Flared Tunic, Jerkin, Lace Shirt, Loose Breeches, Loose Pants, Loose Tunic, Pantaloons, Pants, Poet's Shirt, Puffy Shirt, Puffy Tunic, Shirt, Smock, Trousers, Tunic, Vest, Viamontian Pants, Wide Breeches).
-- They start at 0 armor level and take up to 10 tinks like anything else. Silk's retail job (removing allegiance
-- rank requirements) is gone with those requirements; its effect is the mutation script 0x38000036 in the code.
-- Re-runnable.

DROP TEMPORARY TABLE IF EXISTS acx_underclothes;
CREATE TEMPORARY TABLE acx_underclothes (wcid INT UNSIGNED PRIMARY KEY);
INSERT INTO acx_underclothes (wcid) VALUES
    (124),
    (127),
    (130),
    (134),
    (2587),
    (2588),
    (2589),
    (2590),
    (2591),
    (2592),
    (2593),
    (2594),
    (2595),
    (2596),
    (2597),
    (2598),
    (2599),
    (2600),
    (2601),
    (2602),
    (2603),
    (2604),
    (28606),
    (28607),
    (28608),
    (28609);

-- Silk (recipe 4431): its targets become the underclothes, it no longer needs an allegiance rank requirement,
-- and it uses Armor Tinkering (skill 29) like Steel
DROP TEMPORARY TABLE IF EXISTS acx_silk_sources;
CREATE TEMPORARY TABLE acx_silk_sources AS SELECT DISTINCT source_W_C_I_D AS wcid FROM cook_book WHERE recipe_Id = 4431;
DELETE FROM cook_book WHERE recipe_Id = 4431;
INSERT INTO cook_book (recipe_Id, source_W_C_I_D, target_W_C_I_D, last_Modified)
SELECT 4431, s.wcid, u.wcid, NOW() FROM acx_silk_sources s CROSS JOIN acx_underclothes u;
DELETE FROM recipe_requirements_int WHERE recipe_Id = 4431 AND stat = 110;   -- ItemAllegianceRankLimit
UPDATE recipe SET skill = 29, success_Message = 'You apply the silk, adding armor to the garment.' WHERE id = 4431;

-- protection salvages also work on underclothes: Alabaster 3846, Armoredillo Hide 3847, Bronze 3849, Ceramic 3850,
-- Marble 3856, Reedshark Hide 3859, Wool 3862
DROP TEMPORARY TABLE IF EXISTS acx_new_cook_book;
CREATE TEMPORARY TABLE acx_new_cook_book AS
SELECT DISTINCT c.recipe_Id, c.source_W_C_I_D, u.wcid AS target_W_C_I_D
FROM cook_book c CROSS JOIN acx_underclothes u
WHERE c.recipe_Id IN (3846, 3847, 3849, 3850, 3856, 3859, 3862);
DELETE n FROM acx_new_cook_book n JOIN cook_book c ON c.recipe_Id = n.recipe_Id AND c.source_W_C_I_D = n.source_W_C_I_D AND c.target_W_C_I_D = n.target_W_C_I_D;
INSERT INTO cook_book (recipe_Id, source_W_C_I_D, target_W_C_I_D, last_Modified)
SELECT recipe_Id, source_W_C_I_D, target_W_C_I_D, NOW() FROM acx_new_cook_book;

-- the client only offers a salvage on items whose type is in its TargetType (int 94); Silk's retail value
-- (weapons, armor, jewelry, casters) left out clothing, so it now targets clothing (ItemType.Clothing = 4) only
UPDATE weenie_properties_int t JOIN weenie_properties_string n ON n.object_Id = t.object_Id AND n.type = 1
SET t.value = 4
WHERE t.type = 94 AND n.value = 'Salvaged Silk';
