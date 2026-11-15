-- Project ACX (2026-11-14): the respec gems in the two rooms beside Concordia's lifestone (JJ: "lets put the gems back
-- and place a 1hr pick up timer on them. And I won't place the portals to the temples"). The gems are retail's own
-- (Gems of Forgetfulness lower a skill, Gems of Enlightenment specialize one, plus Gems of Lowering/Raising for each
-- attribute), placed by the world's two item generators 0x7018A01A and 0x7018A0F6; they're bonded and attuned, worth
-- nothing, and respawn 10 seconds after they're taken. Picking one up stamps a quest for its kind, each with its own
-- 1 h timer, so a character takes one Forgetfulness gem, one Enlightenment gem, one Lowering gem and one Raising gem an
-- hour (JJ 2026-11-15: "make it so that you can pick up one enlight and one forget and a raise and lower attribute set.
-- so each type gets their own timer"); a Lowering and a Raising gem combine into a transfer gem (retail's recipes).
-- Retail stamped all skill gems with one shared quest (and only 13 of them carried it); they get ClassicACE's split
-- names here, which unlimited_respec already knows. At level 20 and below there's no timer at all (unlimited_respec;
-- on Preview unlimited_respec_max_level is 9999, so no timers there at all: JJ "remove the gem pick up limits for
-- preview but keep them for live").
-- Re-runnable.
DELETE s FROM weenie_properties_string s JOIN weenie w ON w.class_Id = s.object_Id
WHERE s.type = 33 AND w.type = 62 AND s.object_Id IN (SELECT object_Id FROM (
    SELECT object_Id FROM weenie_properties_string WHERE type = 1 AND (value LIKE '% Gem of Forgetfulness' OR value LIKE '% Gem of Enlightenment')) g);
INSERT INTO weenie_properties_string (object_Id, type, value)
SELECT n.object_Id, 33, IF(n.value LIKE '% Gem of Forgetfulness', 'SkillForgetfulnessGemPickedUp', 'SkillEnlightenmentGemPickedUp')
FROM weenie_properties_string n JOIN weenie w ON w.class_Id = n.object_Id
WHERE n.type = 1 AND w.type = 62 AND (n.value LIKE '% Gem of Forgetfulness' OR n.value LIKE '% Gem of Enlightenment');

DELETE FROM quest WHERE name IN ('SkillForgetfulnessGemPickedUp', 'SkillEnlightenmentGemPickedUp');
INSERT INTO quest (name, min_Delta, max_Solves, message, last_Modified) VALUES
    ('SkillForgetfulnessGemPickedUp', 3600, -1, 'Picked up a Gem of Forgetfulness.', NOW()),
    ('SkillEnlightenmentGemPickedUp', 3600, -1, 'Picked up a Gem of Enlightenment.', NOW());
UPDATE quest SET min_Delta = 3600 WHERE name IN ('AttributeLoweringGemPickedUp', 'AttributeRaisingGemPickedUp');

-- The layout (JJ: "arrange them in a logical order"): the world's set had gems for six skills retired at end of retail
-- (Crossbow, Mace, Spear, Staff, Thrown Weapon, Unarmed Combat) and none for the newer ones, so the skill gems are
-- placed fresh, one Forgetfulness and one Enlightenment gem for each of the 37 skills a character can train, side by
-- side along the walls of the rooms' corners. West room: combat (weapons, fighting styles, defenses, body and
-- allegiance); east room, by the lifestone: magic (schools, lore and awareness) and trades (tinkering, crafts). The
-- attribute gems in the middle of each room become Lowering/Raising pairs: Strength, Endurance and Coordination in the
-- west room, Quickness, Focus and Self in the east. Guids 0x7018AC00-0x7018AC7F, linked to the room's generator.
-- Cells are the DAT's own for each spot (the world data's cells for this landblock are stale).
DELETE c, k FROM landblock_instance_link k JOIN landblock_instance c ON c.guid = k.child_GUID JOIN weenie w ON w.class_Id = c.weenie_Class_Id
WHERE k.parent_GUID IN (0x7018A01A, 0x7018A0F6) AND w.type = 62;
DELETE FROM landblock_instance_link WHERE child_GUID BETWEEN 0x7018AC00 AND 0x7018AC7F;
DELETE FROM landblock_instance WHERE guid BETWEEN 0x7018AC00 AND 0x7018AC7F;
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES
    -- west room, northwest corner: weapons
    (0x7018AC00, 22318, 0x018A021D, 57.2, -85.95, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Light Weapons Gem of Forgetfulness
    (0x7018AC01, 22353, 0x018A021D, 58, -85.95, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Light Weapons Gem of Enlightenment
    (0x7018AC02, 22345, 0x018A021D, 59.8, -85.95, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Heavy Weapons Gem of Forgetfulness
    (0x7018AC03, 22380, 0x018A021D, 60.6, -85.95, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Heavy Weapons Gem of Enlightenment
    (0x7018AC04, 22324, 0x018A021D, 62.4, -85.95, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Finesse Weapons Gem of Forgetfulness
    (0x7018AC05, 22359, 0x018A021D, 63.2, -85.95, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Finesse Weapons Gem of Enlightenment
    (0x7018AC06, 41739, 0x018A021D, 55.95, -87.2, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Two Handed Combat Gem of Forgetfulness
    (0x7018AC07, 41740, 0x018A021D, 55.95, -88, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Two Handed Combat Gem of Enlightenment
    (0x7018AC08, 22319, 0x018A021D, 55.95, -89.8, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Missile Weapons Gem of Forgetfulness
    (0x7018AC09, 22354, 0x018A021D, 55.95, -90.6, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Missile Weapons Gem of Enlightenment
    -- west room, northeast corner: fighting styles
    (0x7018AC0A, 45376, 0x018A023B, 92.8, -85.95, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Dual Wield Gem of Forgetfulness
    (0x7018AC0B, 45381, 0x018A023B, 92, -85.95, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Dual Wield Gem of Enlightenment
    (0x7018AC0C, 45378, 0x018A023B, 90.2, -85.95, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Shield Gem of Forgetfulness
    (0x7018AC0D, 45383, 0x018A023B, 89.4, -85.95, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Shield Gem of Enlightenment
    (0x7018AC0E, 45379, 0x018A023B, 87.6, -85.95, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Sneak Attack Gem of Forgetfulness
    (0x7018AC0F, 45384, 0x018A023B, 86.8, -85.95, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Sneak Attack Gem of Enlightenment
    (0x7018AC10, 45375, 0x018A023B, 94.05, -87.2, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Dirty Fighting Gem of Forgetfulness
    (0x7018AC11, 45380, 0x018A023B, 94.05, -88, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Dirty Fighting Gem of Enlightenment
    (0x7018AC12, 45377, 0x018A023B, 94.05, -89.8, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Recklessness Gem of Forgetfulness
    (0x7018AC13, 45382, 0x018A023B, 94.05, -90.6, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Recklessness Gem of Enlightenment
    -- west room, southwest corner: defenses
    (0x7018AC14, 22339, 0x018A0222, 57.2, -124.05, 0.005, 0.522633, 0, 0, -0.852558, 1),   -- Melee Defense Gem of Forgetfulness
    (0x7018AC15, 22374, 0x018A0222, 58, -124.05, 0.005, 0.522633, 0, 0, -0.852558, 1),   -- Melee Defense Gem of Enlightenment
    (0x7018AC16, 22340, 0x018A0222, 59.8, -124.05, 0.005, 0.522633, 0, 0, -0.852558, 1),   -- Missile Defense Gem of Forgetfulness
    (0x7018AC17, 22375, 0x018A0222, 60.6, -124.05, 0.005, 0.522633, 0, 0, -0.852558, 1),   -- Missile Defense Gem of Enlightenment
    (0x7018AC18, 22336, 0x018A0222, 55.95, -122.8, 0.005, 0.522633, 0, 0, -0.852558, 1),   -- Magic Defense Gem of Forgetfulness
    (0x7018AC19, 22371, 0x018A0222, 55.95, -122, 0.005, 0.522633, 0, 0, -0.852558, 1),   -- Magic Defense Gem of Enlightenment
    -- west room, southeast corner: body and allegiance
    (0x7018AC1A, 22327, 0x018A0241, 92.8, -124.05, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Healing Gem of Forgetfulness
    (0x7018AC1B, 22362, 0x018A0241, 92, -124.05, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Healing Gem of Enlightenment
    (0x7018AC1C, 22342, 0x018A0241, 90.2, -124.05, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Run Gem of Forgetfulness
    (0x7018AC1D, 22377, 0x018A0241, 89.4, -124.05, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Run Gem of Enlightenment
    (0x7018AC1E, 22330, 0x018A0241, 87.6, -124.05, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Jump Gem of Forgetfulness
    (0x7018AC1F, 22365, 0x018A0241, 86.8, -124.05, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Jump Gem of Enlightenment
    (0x7018AC20, 22331, 0x018A0241, 94.05, -122.8, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Leadership Gem of Forgetfulness
    (0x7018AC21, 22366, 0x018A0241, 94.05, -122, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Leadership Gem of Enlightenment
    (0x7018AC22, 22334, 0x018A0241, 94.05, -120.2, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Loyalty Gem of Forgetfulness
    (0x7018AC23, 22369, 0x018A0241, 94.05, -119.4, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Loyalty Gem of Enlightenment
    -- east room, northwest corner: magic schools
    (0x7018AC24, 22348, 0x018A028D, 127.2, -85.95, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- War Magic Gem of Forgetfulness
    (0x7018AC25, 22383, 0x018A028D, 128, -85.95, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- War Magic Gem of Enlightenment
    (0x7018AC26, 22332, 0x018A028D, 129.8, -85.95, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Life Magic Gem of Forgetfulness
    (0x7018AC27, 22367, 0x018A028D, 130.6, -85.95, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Life Magic Gem of Enlightenment
    (0x7018AC28, 22322, 0x018A028D, 132.4, -85.95, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Creature Enchantment Gem of Forgetfulness
    (0x7018AC29, 22357, 0x018A028D, 133.2, -85.95, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Creature Enchantment Gem of Enlightenment
    (0x7018AC2A, 22329, 0x018A028D, 125.95, -87.2, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Item Enchantment Gem of Forgetfulness
    (0x7018AC2B, 22364, 0x018A028D, 125.95, -88, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Item Enchantment Gem of Enlightenment
    (0x7018AC2C, 43402, 0x018A028D, 125.95, -89.8, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Void Magic Gem of Forgetfulness
    (0x7018AC2D, 43400, 0x018A028D, 125.95, -90.6, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Void Magic Gem of Enlightenment
    (0x7018AC2E, 49481, 0x018A028D, 125.95, -92.4, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Summoning Gem of Forgetfulness
    (0x7018AC2F, 49484, 0x018A028D, 125.95, -93.2, 0.005, -0.233293, 0, 0, -0.972407, 1),   -- Summoning Gem of Enlightenment
    -- east room, northeast corner: lore and awareness
    (0x7018AC30, 22338, 0x018A02AB, 162.8, -85.95, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Mana Conversion Gem of Forgetfulness
    (0x7018AC31, 22373, 0x018A02AB, 162, -85.95, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Mana Conversion Gem of Enlightenment
    (0x7018AC32, 22316, 0x018A02AB, 160.2, -85.95, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Arcane Lore Gem of Forgetfulness
    (0x7018AC33, 22351, 0x018A02AB, 159.4, -85.95, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Arcane Lore Gem of Enlightenment
    (0x7018AC34, 22321, 0x018A02AB, 157.6, -85.95, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Assess Creature Gem of Forgetfulness
    (0x7018AC35, 22356, 0x018A02AB, 156.8, -85.95, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Assess Creature Gem of Enlightenment
    (0x7018AC36, 22341, 0x018A02AB, 164.05, -87.2, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Assess Person Gem of Forgetfulness
    (0x7018AC37, 22376, 0x018A02AB, 164.05, -88, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Assess Person Gem of Enlightenment
    (0x7018AC38, 22325, 0x018A02AB, 164.05, -89.8, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Deception Gem of Forgetfulness
    (0x7018AC39, 22360, 0x018A02AB, 164.05, -90.6, 0.005, -0.852558, 0, 0, -0.522632, 1),   -- Deception Gem of Enlightenment
    -- east room, southwest corner: tinkering
    (0x7018AC3A, 22317, 0x018A0293, 127.2, -124.05, 0.005, 0.522633, 0, 0, -0.852558, 1),   -- Armor Tinkering Gem of Forgetfulness
    (0x7018AC3B, 22352, 0x018A0293, 128, -124.05, 0.005, 0.522633, 0, 0, -0.852558, 1),   -- Armor Tinkering Gem of Enlightenment
    (0x7018AC3C, 22349, 0x018A0293, 129.8, -124.05, 0.005, 0.522633, 0, 0, -0.852558, 1),   -- Weapon Tinkering Gem of Forgetfulness
    (0x7018AC3D, 22384, 0x018A0293, 130.6, -124.05, 0.005, 0.522633, 0, 0, -0.852558, 1),   -- Weapon Tinkering Gem of Enlightenment
    (0x7018AC3E, 22328, 0x018A0293, 125.95, -122.8, 0.005, 0.522633, 0, 0, -0.852558, 1),   -- Item Tinkering Gem of Forgetfulness
    (0x7018AC3F, 22363, 0x018A0293, 125.95, -122, 0.005, 0.522633, 0, 0, -0.852558, 1),   -- Item Tinkering Gem of Enlightenment
    (0x7018AC40, 22337, 0x018A0293, 125.95, -120.2, 0.005, 0.522633, 0, 0, -0.852558, 1),   -- Magic Item Tinkering Gem of Forgetfulness
    (0x7018AC41, 22372, 0x018A0293, 125.95, -119.4, 0.005, 0.522633, 0, 0, -0.852558, 1),   -- Magic Item Tinkering Gem of Enlightenment
    -- east room, southeast corner: crafts
    (0x7018AC42, 22315, 0x018A02B0, 162.8, -124.05, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Alchemy Gem of Forgetfulness
    (0x7018AC43, 22350, 0x018A02B0, 162, -124.05, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Alchemy Gem of Enlightenment
    (0x7018AC44, 22320, 0x018A02B0, 160.2, -124.05, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Cooking Gem of Forgetfulness
    (0x7018AC45, 22355, 0x018A02B0, 159.4, -124.05, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Cooking Gem of Enlightenment
    (0x7018AC46, 22326, 0x018A02B0, 164.05, -122.8, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Fletching Gem of Forgetfulness
    (0x7018AC47, 22361, 0x018A02B0, 164.05, -122, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Fletching Gem of Enlightenment
    (0x7018AC48, 22333, 0x018A02B0, 164.05, -120.2, 0.005, 0.972407, 0, 0, -0.233293, 1),   -- Lockpick Gem of Forgetfulness
    (0x7018AC49, 22368, 0x018A02B0, 164.05, -119.4, 0.005, 0.972407, 0, 0, -0.233293, 1);   -- Lockpick Gem of Enlightenment
INSERT INTO landblock_instance_link (parent_GUID, child_GUID) VALUES
    (0x7018A01A, 0x7018AC00),
    (0x7018A01A, 0x7018AC01),
    (0x7018A01A, 0x7018AC02),
    (0x7018A01A, 0x7018AC03),
    (0x7018A01A, 0x7018AC04),
    (0x7018A01A, 0x7018AC05),
    (0x7018A01A, 0x7018AC06),
    (0x7018A01A, 0x7018AC07),
    (0x7018A01A, 0x7018AC08),
    (0x7018A01A, 0x7018AC09),
    (0x7018A01A, 0x7018AC0A),
    (0x7018A01A, 0x7018AC0B),
    (0x7018A01A, 0x7018AC0C),
    (0x7018A01A, 0x7018AC0D),
    (0x7018A01A, 0x7018AC0E),
    (0x7018A01A, 0x7018AC0F),
    (0x7018A01A, 0x7018AC10),
    (0x7018A01A, 0x7018AC11),
    (0x7018A01A, 0x7018AC12),
    (0x7018A01A, 0x7018AC13),
    (0x7018A01A, 0x7018AC14),
    (0x7018A01A, 0x7018AC15),
    (0x7018A01A, 0x7018AC16),
    (0x7018A01A, 0x7018AC17),
    (0x7018A01A, 0x7018AC18),
    (0x7018A01A, 0x7018AC19),
    (0x7018A01A, 0x7018AC1A),
    (0x7018A01A, 0x7018AC1B),
    (0x7018A01A, 0x7018AC1C),
    (0x7018A01A, 0x7018AC1D),
    (0x7018A01A, 0x7018AC1E),
    (0x7018A01A, 0x7018AC1F),
    (0x7018A01A, 0x7018AC20),
    (0x7018A01A, 0x7018AC21),
    (0x7018A01A, 0x7018AC22),
    (0x7018A01A, 0x7018AC23),
    (0x7018A0F6, 0x7018AC24),
    (0x7018A0F6, 0x7018AC25),
    (0x7018A0F6, 0x7018AC26),
    (0x7018A0F6, 0x7018AC27),
    (0x7018A0F6, 0x7018AC28),
    (0x7018A0F6, 0x7018AC29),
    (0x7018A0F6, 0x7018AC2A),
    (0x7018A0F6, 0x7018AC2B),
    (0x7018A0F6, 0x7018AC2C),
    (0x7018A0F6, 0x7018AC2D),
    (0x7018A0F6, 0x7018AC2E),
    (0x7018A0F6, 0x7018AC2F),
    (0x7018A0F6, 0x7018AC30),
    (0x7018A0F6, 0x7018AC31),
    (0x7018A0F6, 0x7018AC32),
    (0x7018A0F6, 0x7018AC33),
    (0x7018A0F6, 0x7018AC34),
    (0x7018A0F6, 0x7018AC35),
    (0x7018A0F6, 0x7018AC36),
    (0x7018A0F6, 0x7018AC37),
    (0x7018A0F6, 0x7018AC38),
    (0x7018A0F6, 0x7018AC39),
    (0x7018A0F6, 0x7018AC3A),
    (0x7018A0F6, 0x7018AC3B),
    (0x7018A0F6, 0x7018AC3C),
    (0x7018A0F6, 0x7018AC3D),
    (0x7018A0F6, 0x7018AC3E),
    (0x7018A0F6, 0x7018AC3F),
    (0x7018A0F6, 0x7018AC40),
    (0x7018A0F6, 0x7018AC41),
    (0x7018A0F6, 0x7018AC42),
    (0x7018A0F6, 0x7018AC43),
    (0x7018A0F6, 0x7018AC44),
    (0x7018A0F6, 0x7018AC45),
    (0x7018A0F6, 0x7018AC46),
    (0x7018A0F6, 0x7018AC47),
    (0x7018A0F6, 0x7018AC48),
    (0x7018A0F6, 0x7018AC49);

UPDATE landblock_instance SET weenie_Class_Id = 22942 WHERE guid = 0x7018A099;   -- Gem of Lowering Strength
UPDATE landblock_instance SET weenie_Class_Id = 22948 WHERE guid = 0x7018A01B;   -- Gem of Raising Strength
UPDATE landblock_instance SET weenie_Class_Id = 22938 WHERE guid = 0x7018A098;   -- Gem of Lowering Endurance
UPDATE landblock_instance SET weenie_Class_Id = 22944 WHERE guid = 0x7018A09A;   -- Gem of Raising Endurance
UPDATE landblock_instance SET weenie_Class_Id = 22937 WHERE guid = 0x7018A01C;   -- Gem of Lowering Coordination
UPDATE landblock_instance SET weenie_Class_Id = 22943 WHERE guid = 0x7018A01D;   -- Gem of Raising Coordination
UPDATE landblock_instance SET weenie_Class_Id = 22940 WHERE guid = 0x7018A0F2;   -- Gem of Lowering Quickness
UPDATE landblock_instance SET weenie_Class_Id = 22946 WHERE guid = 0x7018A0F4;   -- Gem of Raising Quickness
UPDATE landblock_instance SET weenie_Class_Id = 22939 WHERE guid = 0x7018A0F1;   -- Gem of Lowering Focus
UPDATE landblock_instance SET weenie_Class_Id = 22945 WHERE guid = 0x7018A0F3;   -- Gem of Raising Focus
UPDATE landblock_instance SET weenie_Class_Id = 22941 WHERE guid = 0x7018A0F5;   -- Gem of Lowering Self
UPDATE landblock_instance SET weenie_Class_Id = 22947 WHERE guid = 0x7018A0F7;   -- Gem of Raising Self
