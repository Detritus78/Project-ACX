-- Project ACX experiment: a target dummy for combat testing (Preview only; never in acx/Content).
--   900015  Target Dummy: the Straw Target Drudge (6075) with 1,000,000 health, no regeneration, rooted in place.
--           Like the straw drudges it never fights back (Tolerance NoAttack) and has light armor (10).
--   900016  Target Dummy 2 and 900017 Target Dummy 3: identical copies with their own numbers, so several can be
--           placed as instances and chat shows which one was hit.
-- Spawn with /create 900015 (or 900016, 900017). Re-runnable.

DELETE FROM weenie WHERE class_Id = 900015;

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900015, 'acx-test-target-dummy', type, NOW() FROM weenie WHERE class_Id = 6075;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900015, type, value FROM weenie_properties_int WHERE object_Id = 6075;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900015, type, value FROM weenie_properties_float WHERE object_Id = 6075;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900015, type, value FROM weenie_properties_bool WHERE object_Id = 6075;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900015, type, value FROM weenie_properties_string WHERE object_Id = 6075;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900015, type, value FROM weenie_properties_d_i_d WHERE object_Id = 6075;
INSERT INTO weenie_properties_attribute (object_Id, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) SELECT 900015, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM weenie_properties_attribute WHERE object_Id = 6075;
INSERT INTO weenie_properties_attribute_2nd (object_Id, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) SELECT 900015, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM weenie_properties_attribute_2nd WHERE object_Id = 6075;
INSERT INTO weenie_properties_skill (object_Id, `type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) SELECT 900015, `type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM weenie_properties_skill WHERE object_Id = 6075;
INSERT INTO weenie_properties_body_part (object_Id, `key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) SELECT 900015, `key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM weenie_properties_body_part WHERE object_Id = 6075;

-- name
UPDATE weenie_properties_string SET value = 'Target Dummy' WHERE object_Id = 900015 AND type = 1;

-- never attacks (Tolerance int 67 = NoAttack), rooted in place (AiImmobile bool 52)
DELETE FROM weenie_properties_int WHERE object_Id = 900015 AND type = 67;
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES (900015, 67, 1);
DELETE FROM weenie_properties_bool WHERE object_Id = 900015 AND type = 52;
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (900015, 52, 1);

-- 1,000,000 health (MaxHealth vital 1), no regeneration (HealthRate float 3)
UPDATE weenie_properties_attribute_2nd SET init_Level = 1000000, current_Level = 1000000 WHERE object_Id = 900015 AND type = 1;
DELETE FROM weenie_properties_float WHERE object_Id = 900015 AND type = 3;
INSERT INTO weenie_properties_float (object_Id, type, value) VALUES (900015, 3, 0);

-- copies: Target Dummy 2 (900016) and Target Dummy 3 (900017)
DELETE FROM weenie WHERE class_Id IN (900016, 900017);
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900016, 'acx-test-target-dummy-2', type, NOW() FROM weenie WHERE class_Id = 900015;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900016, type, value FROM weenie_properties_int WHERE object_Id = 900015;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900016, type, value FROM weenie_properties_float WHERE object_Id = 900015;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900016, type, value FROM weenie_properties_bool WHERE object_Id = 900015;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900016, type, value FROM weenie_properties_string WHERE object_Id = 900015;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900016, type, value FROM weenie_properties_d_i_d WHERE object_Id = 900015;
INSERT INTO weenie_properties_attribute (object_Id, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) SELECT 900016, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM weenie_properties_attribute WHERE object_Id = 900015;
INSERT INTO weenie_properties_attribute_2nd (object_Id, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) SELECT 900016, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM weenie_properties_attribute_2nd WHERE object_Id = 900015;
INSERT INTO weenie_properties_skill (object_Id, `type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) SELECT 900016, `type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM weenie_properties_skill WHERE object_Id = 900015;
INSERT INTO weenie_properties_body_part (object_Id, `key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) SELECT 900016, `key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM weenie_properties_body_part WHERE object_Id = 900015;
UPDATE weenie_properties_string SET value = 'Target Dummy 2' WHERE object_Id = 900016 AND type = 1;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900017, 'acx-test-target-dummy-3', type, NOW() FROM weenie WHERE class_Id = 900015;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900017, type, value FROM weenie_properties_int WHERE object_Id = 900015;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900017, type, value FROM weenie_properties_float WHERE object_Id = 900015;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900017, type, value FROM weenie_properties_bool WHERE object_Id = 900015;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900017, type, value FROM weenie_properties_string WHERE object_Id = 900015;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900017, type, value FROM weenie_properties_d_i_d WHERE object_Id = 900015;
INSERT INTO weenie_properties_attribute (object_Id, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) SELECT 900017, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM weenie_properties_attribute WHERE object_Id = 900015;
INSERT INTO weenie_properties_attribute_2nd (object_Id, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) SELECT 900017, `type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM weenie_properties_attribute_2nd WHERE object_Id = 900015;
INSERT INTO weenie_properties_skill (object_Id, `type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) SELECT 900017, `type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM weenie_properties_skill WHERE object_Id = 900015;
INSERT INTO weenie_properties_body_part (object_Id, `key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) SELECT 900017, `key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM weenie_properties_body_part WHERE object_Id = 900015;
UPDATE weenie_properties_string SET value = 'Target Dummy 3' WHERE object_Id = 900017 AND type = 1;
