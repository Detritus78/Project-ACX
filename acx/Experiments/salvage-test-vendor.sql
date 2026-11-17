-- Project ACX experiment: a free salvage vendor in the Marketplace for tinkering tests (Preview only; never in acx/Content).
--   900020  Salvage Vendor: a copy of the Grocer (22724) whose shop is one full bag of every salvage material.
--           Its SellPrice is 0, but vendors still charge at least 1 pyreal: the free-vendor code was removed so it can
--           never reach Live (JJ 2026-11-12). Free bags come from ACX Foundry's "Get full bag" instead.
--   900200-900259  full salvage bags: copies of the 60 salvage materials that have tinkering recipes, 100 units at
--           workmanship 10 (ItemWorkmanship 10 / NumItemsInMaterial 1). Recipes match salvage by item number, so each bag
--           gets a copy of its original's cook_book rows: RE-RUN THIS FILE after any content change to salvage recipes.
-- NOT PLACED: AC:Unreal's vendor window shows only the first bag (no icon) and stops, although the server sends all 60
-- (2026-11-12, cause not found yet). The bags are handed out by ACX Foundry's "Get full bag" button (/ci) instead.
-- To try the vendor again, restore the landblock_instance insert at the end. Restart the server after applying.

DELETE FROM landblock_instance WHERE guid = 0x7016C008;
DELETE FROM cook_book WHERE source_W_C_I_D BETWEEN 900200 AND 900299;
DELETE FROM weenie WHERE class_Id = 900020 OR class_Id BETWEEN 900200 AND 900299;

DROP TEMPORARY TABLE IF EXISTS acx_bags;
CREATE TEMPORARY TABLE acx_bags (bag INT UNSIGNED PRIMARY KEY, original INT UNSIGNED);
INSERT INTO acx_bags VALUES
    (900200, 20983),   -- Salvaged Ceramic
    (900201, 21067),   -- Salvaged Porcelain
    (900202, 20987),   -- Salvaged Linen
    (900203, 21076),   -- Salvaged Silk
    (900204, 20994),   -- Salvaged Velvet
    (900205, 20995),   -- Salvaged Wool
    (900206, 21034),   -- Salvaged Agate
    (900207, 21035),   -- Salvaged Amber
    (900208, 21037),   -- Salvaged Aquamarine
    (900209, 21038),   -- Salvaged Azurite
    (900210, 21039),   -- Salvaged Black Garnet
    (900211, 21040),   -- Salvaged Black Opal
    (900212, 21041),   -- Salvaged Bloodstone
    (900213, 21043),   -- Salvaged Carnelian
    (900214, 21044),   -- Salvaged Citrine
    (900215, 21046),   -- Salvaged Diamond
    (900216, 21048),   -- Salvaged Emerald
    (900217, 21049),   -- Salvaged Fire Opal
    (900218, 21050),   -- Salvaged Green Garnet
    (900219, 21053),   -- Salvaged Hematite
    (900220, 21054),   -- Salvaged Imperial Topaz
    (900221, 21056),   -- Salvaged Jet
    (900222, 21057),   -- Salvaged Lapis Lazuli
    (900223, 21058),   -- Salvaged Lavender Jade
    (900224, 21060),   -- Salvaged Malachite
    (900225, 21062),   -- Salvaged Moonstone
    (900226, 21065),   -- Salvaged Opal
    (900227, 21066),   -- Salvaged Peridot
    (900228, 21069),   -- Salvaged Red Garnet
    (900229, 21070),   -- Salvaged Red Jade
    (900230, 21071),   -- Salvaged Rose Quartz
    (900231, 21072),   -- Salvaged Ruby
    (900232, 21074),   -- Salvaged Sapphire
    (900233, 21078),   -- Salvaged Smoky Quartz
    (900234, 21079),   -- Salvaged Sunstone
    (900235, 21086),   -- Salvaged White Sapphire
    (900236, 21088),   -- Salvaged Yellow Topaz
    (900237, 21089),   -- Salvaged Zircon
    (900238, 21055),   -- Salvaged Ivory
    (900239, 21059),   -- Salvaged Leather
    (900240, 20981),   -- Salvaged Armoredillo Hide
    (900241, 21052),   -- Salvaged Gromnie Hide
    (900242, 20991),   -- Salvaged Reedshark Hide
    (900243, 21042),   -- Salvaged Brass
    (900244, 20982),   -- Salvaged Bronze
    (900245, 21045),   -- Salvaged Copper
    (900246, 20984),   -- Salvaged Gold
    (900247, 20986),   -- Salvaged Iron
    (900248, 21068),   -- Salvaged Pyreal
    (900249, 21077),   -- Salvaged Silver
    (900250, 20993),   -- Salvaged Steel
    (900251, 20980),   -- Salvage
    (900252, 20985),   -- Salvaged Granite
    (900253, 21061),   -- Salvaged Marble
    (900254, 21073),   -- Salvaged Sandstone
    (900255, 21047),   -- Salvaged Ebony
    (900256, 20988),   -- Salvaged Mahogany
    (900257, 20989),   -- Salvaged Oak
    (900258, 20990),   -- Salvaged Pine
    (900259, 21080);   -- Salvaged Teak

-- the bags: copies of the originals, full and at the best workmanship
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT b.bag, CONCAT('acx-test-bag-', w.class_Name), w.type, NOW() FROM acx_bags b JOIN weenie w ON w.class_Id = b.original;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT b.bag, p.type, p.value FROM acx_bags b JOIN weenie_properties_int p ON p.object_Id = b.original;
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT b.bag, p.type, p.value FROM acx_bags b JOIN weenie_properties_int64 p ON p.object_Id = b.original;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT b.bag, p.type, p.value FROM acx_bags b JOIN weenie_properties_float p ON p.object_Id = b.original;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT b.bag, p.type, p.value FROM acx_bags b JOIN weenie_properties_bool p ON p.object_Id = b.original;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT b.bag, p.type, p.value FROM acx_bags b JOIN weenie_properties_string p ON p.object_Id = b.original;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT b.bag, p.type, p.value FROM acx_bags b JOIN weenie_properties_d_i_d p ON p.object_Id = b.original;
INSERT INTO weenie_properties_i_i_d (object_Id, type, value) SELECT b.bag, p.type, p.value FROM acx_bags b JOIN weenie_properties_i_i_d p ON p.object_Id = b.original;
DELETE p FROM weenie_properties_int p JOIN acx_bags b ON p.object_Id = b.bag WHERE p.type IN (91, 92, 105, 170);
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT bag, 91, 100 FROM acx_bags;    -- MaxStructure
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT bag, 92, 100 FROM acx_bags;    -- Structure (units)
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT bag, 105, 10 FROM acx_bags;    -- ItemWorkmanship
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT bag, 170, 1 FROM acx_bags;     -- NumItemsInMaterial
INSERT INTO cook_book (recipe_Id, source_W_C_I_D, target_W_C_I_D, last_Modified) SELECT c.recipe_Id, b.bag, c.target_W_C_I_D, NOW() FROM acx_bags b JOIN cook_book c ON c.source_W_C_I_D = b.original;
-- Salvage whose recipe the server finds in code by item number (imbue gems, iron, granite, velvet, armor and jewelry
-- salvage, ...) has no cook_book row for most items; the server looks a test bag up as its original
-- (RecipeManager_New.AcxTestBagOriginal, 2026-11-16), so the bags need only the copied rows above.

-- the vendor: the Grocer's body, clothes and settings, a shop of bags, free
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900020, 'acx-test-salvage-vendor', type, NOW() FROM weenie WHERE class_Id = 22724;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900020, type, value FROM weenie_properties_int WHERE object_Id = 22724;
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT 900020, type, value FROM weenie_properties_int64 WHERE object_Id = 22724;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900020, type, value FROM weenie_properties_float WHERE object_Id = 22724;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900020, type, value FROM weenie_properties_bool WHERE object_Id = 22724;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900020, type, value FROM weenie_properties_string WHERE object_Id = 22724;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900020, type, value FROM weenie_properties_d_i_d WHERE object_Id = 22724;
INSERT INTO weenie_properties_i_i_d (object_Id, type, value) SELECT 900020, type, value FROM weenie_properties_i_i_d WHERE object_Id = 22724;
INSERT INTO weenie_properties_attribute (object_Id, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) SELECT 900020, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM weenie_properties_attribute WHERE object_Id = 22724;
INSERT INTO weenie_properties_attribute_2nd (object_Id, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) SELECT 900020, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM weenie_properties_attribute_2nd WHERE object_Id = 22724;
INSERT INTO weenie_properties_skill (object_Id, `type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) SELECT 900020, `type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM weenie_properties_skill WHERE object_Id = 22724;
INSERT INTO weenie_properties_body_part (object_Id, `key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) SELECT 900020, `key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM weenie_properties_body_part WHERE object_Id = 22724;
INSERT INTO weenie_properties_create_list (object_Id, `destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`) SELECT 900020, `destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond` FROM weenie_properties_create_list WHERE object_Id = 22724 AND destination_Type <> 4;
INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond) SELECT 900020, 4, bag, -1, 0, 0, 0 FROM acx_bags;
UPDATE weenie_properties_string SET value = 'Salvage Vendor' WHERE object_Id = 900020 AND type = 1;
UPDATE weenie_properties_float SET value = 0 WHERE object_Id = 900020 AND type IN (37, 38);   -- BuyPrice, SellPrice
UPDATE weenie_properties_int SET value = value | 0x40000000 WHERE object_Id = 900020 AND type = 74;   -- MerchandiseItemTypes += TinkeringMaterial

-- -- in the Marketplace room with the dummies, against the west side, facing east
-- INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
--     (0x7016C008, 900020, 0x016C0146, 6.2, -13.0, 0.005, 0.7071068, 0, 0, -0.7071068, 0);
