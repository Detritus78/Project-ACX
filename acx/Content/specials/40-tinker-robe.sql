-- Project ACX (2026-11-24): Brokk's robe. JJ gave Brokk (the shared tinker character) the Empowered Empyrean Robe (35173,
-- the Saving Asheron raid reward) for its attribute buffs, then on seeing its armor ("it has armor level 650 lol"): "I don't
-- want someone to take this character and play it. So lets strip all the item spells off of it."
-- Weenie 901100: retail's Empowered Empyrean Robe with only its six attribute Incantations (Strength, Endurance,
-- Coordination, Quickness, Focus, Willpower Other); no Empyrean Aegis (+500 armor), Durances, vital gains or the rest. Base
-- armor 150 as retail. Given by console (acxgiveitem); attuned like the original. Re-runnable; clearcache weenie after.
DELETE FROM weenie WHERE class_Id = 901100;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 901100, 'acx-tinker-empyrean-robe', type, NOW() FROM weenie WHERE class_Id = 35173;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 901100, type, value FROM weenie_properties_int WHERE object_Id = 35173;
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT 901100, type, value FROM weenie_properties_int64 WHERE object_Id = 35173;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 901100, type, value FROM weenie_properties_float WHERE object_Id = 35173;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 901100, type, value FROM weenie_properties_bool WHERE object_Id = 35173;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 901100, type, value FROM weenie_properties_string WHERE object_Id = 35173;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 901100, type, value FROM weenie_properties_d_i_d WHERE object_Id = 35173;
INSERT INTO weenie_properties_i_i_d (object_Id, type, value) SELECT 901100, type, value FROM weenie_properties_i_i_d WHERE object_Id = 35173;
INSERT INTO weenie_properties_palette (object_Id, sub_Palette_Id, offset, length) SELECT 901100, sub_Palette_Id, offset, length FROM weenie_properties_palette WHERE object_Id = 35173;
INSERT INTO weenie_properties_texture_map (object_Id, `index`, old_Id, new_Id) SELECT 901100, `index`, old_Id, new_Id FROM weenie_properties_texture_map WHERE object_Id = 35173;
INSERT INTO weenie_properties_anim_part (object_Id, `index`, animation_Id) SELECT 901100, `index`, animation_Id FROM weenie_properties_anim_part WHERE object_Id = 35173;
INSERT INTO weenie_properties_spell_book (object_Id, spell, probability) VALUES
    (901100, 3977, 2), (901100, 3978, 2), (901100, 3979, 2), (901100, 4059, 2), (901100, 4060, 2), (901100, 4061, 2);
