-- Project ACX: Atlan revamp (JJ 2026-11-12). With Managers/AcxAtlans.cs, which rolls each newly forged Atlan's flavor
-- (Armor Rending, Critical Strike or Crippling Blow; wands Critical Strike or Crippling Blow) and carries it through
-- every stone change.
--   Minor stones add only the element: the 24 Minor Atlans lose their resistance cleaving.
--   Major, Black Fire and Enhanced stones keep element plus cleave.
--   Stones are used up when applied. The Stone Tool still resets a weapon to plain, and the stone is lost.
--   The four Minor and four Major stones lie in the world; each now sits on an item generator and carries a quest
--   stamp, so picking one up starts a one-week timer for that stone (one timer per stone, JJ).
-- Re-runnable. Restart the server after applying.

-- Minor Atlans: no resistance cleaving (ResistanceModifierType int 263, ResistanceModifier float 157)
DELETE p FROM weenie_properties_int p JOIN weenie_properties_string n ON n.object_Id = p.object_Id AND n.type = 1
WHERE p.type = 263 AND n.value LIKE 'Minor % Atlan %' AND (p.object_Id BETWEEN 46037 AND 46141 OR p.object_Id BETWEEN 71262 AND 71268);
DELETE p FROM weenie_properties_float p JOIN weenie_properties_string n ON n.object_Id = p.object_Id AND n.type = 1
WHERE p.type = 157 AND n.value LIKE 'Minor % Atlan %' AND (p.object_Id BETWEEN 46037 AND 46141 OR p.object_Id BETWEEN 71262 AND 71268);

-- stones are used up: Minor and Major stones (6318-6321, 6123-6126), Black Fire (7469), Enhanced Black Fire (46035)
UPDATE recipe r JOIN cook_book c ON c.recipe_Id = r.id
SET r.success_Destroy_Source_Chance = 1, r.success_Destroy_Source_Amount = 1
WHERE c.source_W_C_I_D IN (6318, 6319, 6320, 6321, 6123, 6124, 6125, 6126, 7469, 46035);

-- one-week pickup timer per stone: Quest (string 33) on the stone, and its quest row
DROP TEMPORARY TABLE IF EXISTS acx_atlan_stones;
CREATE TEMPORARY TABLE acx_atlan_stones (wcid INT UNSIGNED PRIMARY KEY, quest VARCHAR(100), stone_Guid INT UNSIGNED, generator_Guid INT UNSIGNED);
INSERT INTO acx_atlan_stones VALUES
    (6318, 'AcxAtlanStoneMinorSparking',    0x7DD34002, 0x7DD34F20),
    (6319, 'AcxAtlanStoneMinorStinging',    0x7BA60011, 0x7BA60F20),
    (6320, 'AcxAtlanStoneMinorSmoldering',  0x7A66C000, 0x7A66CF20),
    (6321, 'AcxAtlanStoneMinorShivering',   0x7BDB4001, 0x7BDB4F20),
    (6123, 'AcxAtlanStoneMajorShivering',   0x7011704B, 0x70117F20),
    (6124, 'AcxAtlanStoneMajorSmoldering',  0x7011B02B, 0x7011BF20),
    (6125, 'AcxAtlanStoneMajorSparking',    0x7011900E, 0x70119F20),
    (6126, 'AcxAtlanStoneMajorStinging',    0x7011A0BE, 0x7011AF20);

DELETE p FROM weenie_properties_string p JOIN acx_atlan_stones s ON s.wcid = p.object_Id WHERE p.type = 33;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT wcid, 33, quest FROM acx_atlan_stones;

DELETE q FROM quest q JOIN acx_atlan_stones s ON s.quest = q.name;
INSERT INTO quest (name, min_Delta, max_Solves, message, last_Modified)
SELECT quest, 604800, -1, 'Picked up this Atlan stone (one a week)', NOW() FROM acx_atlan_stones;

-- each stone becomes the linked child of a Linkable Item Generator (15759, 10 s respawn) at the same spot:
-- ACE only checks an item's quest timer when a generator controls it
DELETE l FROM landblock_instance_link l JOIN acx_atlan_stones s ON s.generator_Guid = l.parent_GUID;
DELETE i FROM landblock_instance i JOIN acx_atlan_stones s ON s.generator_Guid = i.guid;
INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child, last_Modified)
SELECT s.generator_Guid, 15759, i.obj_Cell_Id, i.origin_X, i.origin_Y, i.origin_Z, i.angles_W, i.angles_X, i.angles_Y, i.angles_Z, 0, NOW()
FROM acx_atlan_stones s JOIN landblock_instance i ON i.guid = s.stone_Guid;
UPDATE landblock_instance i JOIN acx_atlan_stones s ON s.stone_Guid = i.guid SET i.is_Link_Child = 1;
INSERT INTO landblock_instance_link (parent_GUID, child_GUID, last_Modified) SELECT generator_Guid, stone_Guid, NOW() FROM acx_atlan_stones;
