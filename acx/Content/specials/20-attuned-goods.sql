-- Project ACX (2026-11-16): the Mark Trader's goods are attuned to whoever buys them (JJ: "lets attune the items except
-- for the potential tradables like salvage. so the eternal mana is attuned same for the lockpick. We don't need them
-- bonded. leave the marks tradable"), so alts can't buy them and pass them on. ACX Marks stay tradable.
-- Retail's eternal items (30247-30249, 30254, 30253, 30151, 30165) are also tier 5 rares that drop and trade, so the
-- Trader sells attuned copies instead and the rares stay as they are:
--   900880-900882  Eternal Health / Mana / Stamina Kit     100
--   900883         Eternal Mana Charge                     300
--   900884         Limitless Lockpick                      400
--   900885         Rune of Lifestone Recall                 50
--   900886         Rune of Portal Recall                    75
-- (JJ's prices from the Mark Trader Prices sheet; kits, charge, lockpick and runes cut 2026-11-25.) ACX's own goods are attuned in place: the Eternal Concordia Portal Gem
-- and the Primary / Secondary Portal Recall runes (900860-900862), Eternal Arrow / Quarrel / Atlatl Dart (900863-900865),
-- the Gold Phials (900712-900720) and the ACX slayer stones (900870-900879). None is bonded.
-- Still tradable for now: the Eternal Pack, the Salvage Barrel, the Foolproof gems, retail's slayer and skull gems, the
-- rabbit's foot and the dyes. Attuned = PropertyInt 114, Bonded = 33. Applies after 10-mark-specials.sql. Re-runnable.

DROP TEMPORARY TABLE IF EXISTS acx_attuned_copy;
CREATE TEMPORARY TABLE acx_attuned_copy (class_Id INT UNSIGNED PRIMARY KEY, original INT UNSIGNED, class_Name VARCHAR(100), price INT);
INSERT INTO acx_attuned_copy VALUES
    (900880, 30247, 'acx-mark-eternal-health-kit', 25),
    (900881, 30248, 'acx-mark-eternal-mana-kit', 25),
    (900882, 30249, 'acx-mark-eternal-stamina-kit', 25),
    (900883, 30254, 'acx-mark-eternal-mana-charge', 60),
    (900884, 30253, 'acx-mark-limitless-lockpick', 60),
    (900885, 30151, 'acx-mark-rune-lifestone-recall', 15),
    (900886, 30165, 'acx-mark-rune-portal-recall', 15);

DELETE FROM weenie WHERE class_Id BETWEEN 900880 AND 900886;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT o.class_Id, o.class_Name, w.type, NOW() FROM acx_attuned_copy o JOIN weenie w ON w.class_Id = o.original;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_attuned_copy o JOIN weenie_properties_int p ON p.object_Id = o.original WHERE p.type NOT IN (15, 19, 33, 114);   -- not StackUnitValue, Value, Bonded, Attuned
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_attuned_copy o JOIN weenie_properties_int64 p ON p.object_Id = o.original;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_attuned_copy o JOIN weenie_properties_float p ON p.object_Id = o.original;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_attuned_copy o JOIN weenie_properties_bool p ON p.object_Id = o.original;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_attuned_copy o JOIN weenie_properties_string p ON p.object_Id = o.original;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_attuned_copy o JOIN weenie_properties_d_i_d p ON p.object_Id = o.original;
INSERT INTO weenie_properties_i_i_d (object_Id, type, value) SELECT o.class_Id, p.type, p.value FROM acx_attuned_copy o JOIN weenie_properties_i_i_d p ON p.object_Id = o.original;
INSERT INTO weenie_properties_spell_book (object_Id, spell, probability) SELECT o.class_Id, p.spell, p.probability FROM acx_attuned_copy o JOIN weenie_properties_spell_book p ON p.object_Id = o.original;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 19, price FROM acx_attuned_copy;   -- Value = price in ACX Marks

-- attuned, not bonded: the copies and ACX's own goods
DELETE FROM weenie_properties_int WHERE type IN (33, 114) AND (object_Id BETWEEN 900880 AND 900886 OR object_Id BETWEEN 900860 AND 900865
    OR object_Id BETWEEN 900712 AND 900720 OR object_Id BETWEEN 900870 AND 900879);
INSERT INTO weenie_properties_int (object_Id, type, value)
SELECT class_Id, 114, 1 FROM weenie WHERE class_Id BETWEEN 900880 AND 900886 OR class_Id BETWEEN 900860 AND 900865
    OR class_Id BETWEEN 900712 AND 900720 OR class_Id BETWEEN 900870 AND 900879;

-- the Mark Trader sells the copies instead of the retail rares
DELETE FROM weenie_properties_create_list WHERE object_Id = 45501 AND destination_Type = 4
    AND (weenie_Class_Id IN (30247, 30248, 30249, 30254, 30253, 30151, 30165) OR weenie_Class_Id BETWEEN 900880 AND 900886);
INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond)
SELECT 45501, 4, class_Id, -1, 0, 0, 0 FROM acx_attuned_copy ORDER BY class_Id;

DROP TEMPORARY TABLE acx_attuned_copy;
