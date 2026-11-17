-- Project ACX (2026-11-16): the Alchemist's bombs (JJ: "lets entertain alchemy class", then "Yes you got it all right. Lets
-- put it in and bench it and see how that goes. It will need tweaking."; design ~/Work/ac/research/alchemist-class-design-2026-11-16.md;
-- code Factories/Tables/Wcids/Weapons/AcxBombWcids.cs, WorldObjects/Player_AcxBombs.cs).
-- Eternal thrown weapons (one item, never used up, a shield in the other hand) that need specialized Alchemy, which is
-- their attack skill and their blast's power (not Strength). Each looks like retail's Gold Phial of its debuff. Built
-- like the Eternal thrown weapons (thrown/10-eternal-thrown.sql): the weenie is a vendor-grade bomb, damage 12 and
-- Potency (DamageMod) +70%; loot rolls both, Obsidian raises the Potency (20-obsidian-potency.sql).
--   900890 Fire, 900891 Frost, 900892 Acid, 900893 Lightning: their element, and that element's vulnerability
--   900894 Razor (slash), 900895 Shrapnel (pierce), 900896 Concussion (bludgeon): their type's vulnerability
--   900897 Nether: Imperil (nether has no vulnerability spell; JJ: "we could rewrite imperil. It just wont have a vuln")
--   900898 Healing: no damage; heals players and pets, festers monsters (sold by the Mark Trader: specials/40-alchemist-trader.sql)
-- The debuff goes on after the blast, resisted by Magic Defense; its level comes from the loot tier (AcxBombSpell).
-- Retail's phial spell (ProcSpell), spellcraft and mana are left off: the bomb's debuff is the code's, on the thrower's
-- Alchemy. Re-runnable.
DELETE FROM weenie WHERE class_Id BETWEEN 900890 AND 900898;
DROP TEMPORARY TABLE IF EXISTS acx_bomb;
CREATE TEMPORARY TABLE acx_bomb (class_Id INT UNSIGNED PRIMARY KEY, source INT UNSIGNED, class_Name VARCHAR(100), name VARCHAR(100), damage_type INT, ui_effects INT, long_desc TEXT);
INSERT INTO acx_bomb VALUES
    (900890, 81034, 'acx-bomb-fire', 'Fire Bomb', 16, 32, 'An alchemist''s bomb of liquid fire. It bursts on impact, burning everything near its target and leaving it vulnerable to fire.'),
    (900891, 81033, 'acx-bomb-frost', 'Frost Bomb', 8, 128, 'An alchemist''s bomb of bitter frost. It bursts on impact, freezing everything near its target and leaving it vulnerable to cold.'),
    (900892, 81031, 'acx-bomb-acid', 'Acid Bomb', 32, 256, 'An alchemist''s bomb of caustic acid. It bursts on impact, eating into everything near its target and leaving it vulnerable to acid.'),
    (900893, 81036, 'acx-bomb-lightning', 'Lightning Bomb', 64, 64, 'An alchemist''s bomb of bottled lightning. It bursts on impact, shocking everything near its target and leaving it vulnerable to lightning.'),
    (900894, 81038, 'acx-bomb-razor', 'Razor Bomb', 1, 1024, 'An alchemist''s bomb packed with razor-sharp glass. It bursts on impact, slashing everything near its target and leaving it vulnerable to blades.'),
    (900895, 81037, 'acx-bomb-shrapnel', 'Shrapnel Bomb', 2, 2048, 'An alchemist''s bomb packed with iron shrapnel. It bursts on impact, piercing everything near its target and leaving it vulnerable to piercing.'),
    (900896, 81032, 'acx-bomb-concussion', 'Concussion Bomb', 4, 512, 'An alchemist''s bomb that bursts with a thunderous blow, battering everything near its target and leaving it vulnerable to bludgeoning.'),
    (900897, 80002, 'acx-bomb-nether', 'Nether Bomb', 1024, 4096, 'An alchemist''s bomb of distilled nether. It bursts on impact, withering everything near its target and stripping its natural armor.'),
    (900898, 81039, 'acx-bomb-healing', 'Healing Bomb', NULL, NULL, 'An alchemist''s restorative mixture. It bursts on impact: allies nearby heal over time, while monsters caught in it are left to fester.');
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT b.class_Id, b.class_Name, w.type, NOW() FROM acx_bomb b JOIN weenie w ON w.class_Id = b.source;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT b.class_Id, p.type, p.value FROM acx_bomb b JOIN weenie_properties_int p ON p.object_Id = b.source
    WHERE p.type NOT IN (5, 11, 12, 15, 18, 19, 44, 45, 48, 49, 106, 107, 108, 158, 159, 160, 169, 353);   -- not burden, stack, value, damage, skill, speed, spellcraft, mana, wield, tsys, type
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT b.class_Id, p.type, p.value FROM acx_bomb b JOIN weenie_properties_int64 p ON p.object_Id = b.source;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT b.class_Id, p.type, p.value FROM acx_bomb b JOIN weenie_properties_float p ON p.object_Id = b.source WHERE p.type NOT IN (22, 26, 63, 156);   -- not variance, velocity, DamageMod, ProcSpellRate
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT b.class_Id, p.type, p.value FROM acx_bomb b JOIN weenie_properties_bool p ON p.object_Id = b.source WHERE p.type NOT IN (22, 63);
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT b.class_Id, p.type, p.value FROM acx_bomb b JOIN weenie_properties_string p ON p.object_Id = b.source WHERE p.type NOT IN (1, 16, 20);   -- not name, description, plural
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT b.class_Id, p.type, p.value FROM acx_bomb b JOIN weenie_properties_d_i_d p ON p.object_Id = b.source WHERE p.type NOT IN (55);   -- not ProcSpell
INSERT INTO weenie_properties_palette (object_Id, sub_Palette_Id, offset, length) SELECT b.class_Id, p.sub_Palette_Id, p.offset, p.length FROM acx_bomb b JOIN weenie_properties_palette p ON p.object_Id = b.source;
INSERT INTO weenie_properties_texture_map (object_Id, `index`, old_Id, new_Id) SELECT b.class_Id, p.`index`, p.old_Id, p.new_Id FROM acx_bomb b JOIN weenie_properties_texture_map p ON p.object_Id = b.source;
INSERT INTO weenie_properties_anim_part (object_Id, `index`, animation_Id) SELECT b.class_Id, p.`index`, p.animation_Id FROM acx_bomb b JOIN weenie_properties_anim_part p ON p.object_Id = b.source;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT class_Id, 1, name FROM acx_bomb;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT class_Id, 16, long_desc FROM acx_bomb;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 11, 1 FROM acx_bomb;               -- MaxStackSize: one item
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 12, 1 FROM acx_bomb;               -- StackSize
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 5, 5 FROM acx_bomb;                -- EncumbranceVal: light (JJ: Strength is only for carrying)
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 19, 300 FROM acx_bomb;             -- Value (the Healing bomb's is its Mark price, set by specials/40-alchemist-trader.sql)
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 18, ui_effects FROM acx_bomb WHERE ui_effects IS NOT NULL;   -- UiEffects: its damage type's glow
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 44, 12 FROM acx_bomb WHERE damage_type IS NOT NULL;          -- Damage: its own, the ammunition's part
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT class_Id, 63, 1.7 FROM acx_bomb WHERE damage_type IS NOT NULL;       -- DamageMod: its Potency
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT class_Id, 22, 0.25 FROM acx_bomb WHERE damage_type IS NOT NULL;      -- DamageVariance, as thrown
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 45, damage_type FROM acx_bomb WHERE damage_type IS NOT NULL;  -- DamageType
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 48, 38 FROM acx_bomb;              -- WeaponSkill: Alchemy
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 49, 10 FROM acx_bomb;              -- WeaponTime: a dart's
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 353, 10 FROM acx_bomb;             -- WeaponType: Thrown
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 169, 0x11080706 FROM acx_bomb WHERE damage_type IS NOT NULL;  -- TsysMutationData: an atlatl's spells and gems (loot),
                                                                                                                 -- material code 6: ceramic / porcelain or stone (a flask; some salvage to Obsidian)
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 158, 8 FROM acx_bomb;              -- WieldRequirements: Training ...
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 159, 38 FROM acx_bomb;             -- ... of Alchemy ...
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 160, 3 FROM acx_bomb;              -- ... specialized (JJ: "Alchemist only")
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT class_Id, 26, 20 FROM acx_bomb;            -- MaximumVelocity: thrown's range
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT class_Id, 63, 1 FROM acx_bomb;              -- UnlimitedUse: never used up
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT class_Id, 22, 1 FROM acx_bomb;              -- Inscribable
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES (900898, 114, 1);                            -- the Healing bomb is attuned (a Mark Trader good)
DROP TEMPORARY TABLE acx_bomb;
