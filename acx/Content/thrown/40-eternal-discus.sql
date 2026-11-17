-- Project ACX (2026-11-17): the Discus, Eternal (JJ: "theres a quest disc too. that should be eternal also", "they drop it
-- for loot"). Retail's Discus (8211) is a stack the Mite Warrior Princess (level 30, 40 of them) and the Mite Warrior Queen
-- (level 60, 250, beside her quest letter) always drop. Now each drops one Eternal Discus, built like the families in
-- 10-eternal-thrown.sql: a single item with UnlimitedUse, Missile Weapons, launcher-style damage (its own Damage x
-- DamageMod), Inscribable. It keeps retail's look, speed, slashing damage and its edge: Damage 16 where a vendor's Eternal
-- thrown weapon has 12, with the same +70% multiplier, unrolled. Code knows it as AcxThrownWcids.Discus (no elemental or
-- nether versions, not in the loot pool).
-- Re-runnable.
DELETE FROM weenie WHERE class_Id = 900852;

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 900852, 'acx-eternal-discus', type, NOW() FROM weenie WHERE class_Id = 8211;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 900852, type, value FROM weenie_properties_int WHERE object_Id = 8211 AND type NOT IN (11, 12, 15, 18, 19, 44, 45, 48, 169, 353);
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT 900852, type, value FROM weenie_properties_int64 WHERE object_Id = 8211;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT 900852, type, value FROM weenie_properties_float WHERE object_Id = 8211 AND type NOT IN (26, 63);
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 900852, type, value FROM weenie_properties_bool WHERE object_Id = 8211 AND type NOT IN (22, 63);
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 900852, type, value FROM weenie_properties_string WHERE object_Id = 8211 AND type <> 20;   -- not its plural
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 900852, type, value FROM weenie_properties_d_i_d WHERE object_Id = 8211;
INSERT INTO weenie_properties_palette (object_Id, sub_Palette_Id, offset, length) SELECT 900852, sub_Palette_Id, offset, length FROM weenie_properties_palette WHERE object_Id = 8211;
INSERT INTO weenie_properties_texture_map (object_Id, `index`, old_Id, new_Id) SELECT 900852, `index`, old_Id, new_Id FROM weenie_properties_texture_map WHERE object_Id = 8211;
INSERT INTO weenie_properties_anim_part (object_Id, `index`, animation_Id) SELECT 900852, `index`, animation_Id FROM weenie_properties_anim_part WHERE object_Id = 8211;

INSERT INTO weenie_properties_int (object_Id, type, value) VALUES
    (900852, 11, 1),      -- MaxStackSize: one item, not a stack
    (900852, 12, 1),      -- StackSize
    (900852, 19, 300),    -- Value: as the other Eternal thrown weapons
    (900852, 18, 1024),   -- UiEffects: the slashing glow, as the Throwing Axe's
    (900852, 44, 16),     -- Damage: its own, the ammunition's part (retail's 16)
    (900852, 45, 1),      -- DamageType: Slash
    (900852, 48, 47),     -- WeaponSkill: Missile Weapons
    (900852, 353, 10),    -- WeaponType: Thrown
    (900852, 158, 2),     -- WieldRequirements: base skill
    (900852, 159, 47),    -- WieldSkillType: Missile Weapons
    (900852, 160, 250);   -- WieldDifficulty: as loot of its strength (tier 2). It is a quest weapon (one imbue, two transfers:
                          -- AcxThrownWcids.IsQuestThrown), and with those it benches a little over tinkered tier 2 loot
INSERT INTO weenie_properties_float (object_Id, type, value) VALUES
    (900852, 63, 1.7),    -- DamageMod: the launcher's part, a vendor Eternal's
    (900852, 26, 20);     -- MaximumVelocity: the thrown range the others show
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES
    (900852, 63, 1),      -- UnlimitedUse: never used up
    (900852, 22, 1);      -- Inscribable

-- one Eternal Discus in place of the stack
UPDATE weenie_properties_create_list SET weenie_Class_Id = 900852, stack_Size = 1
WHERE weenie_Class_Id = 8211 AND object_Id IN (8215, 24029) AND destination_Type = 1;
