-- Project ACX: component and gem pouches (WorldObjects/Container_AcxPouch.cs), ported from ClassicACE's Spell Component
-- Pouch (its 50009: a 102-slot side pack, value 65). Each only holds one kind of item (AcxPouchItemTypes, int 30011),
-- and a pouch and its contents are safe on death.
--   900630  Spell Component Pouch (spell components)     sold by every vendor that sells spell components
--   900631  Gem Pouch (gems: portal, spell and plain)     sold alongside it
-- Re-runnable. Restart the server after applying.

DELETE FROM weenie WHERE class_Id IN (900630, 900631);
DELETE FROM weenie_properties_create_list WHERE weenie_Class_Id IN (900630, 900631);

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) VALUES
    (900630, 'acx-component-pouch', 21, NOW()),
    (900631, 'acx-gem-pouch', 21, NOW());

INSERT INTO weenie_properties_int (object_Id, type, value) VALUES
    (900630, 1, 512), (900630, 5, 15), (900630, 6, 102), (900630, 8, 200), (900630, 9, 0), (900630, 16, 56), (900630, 19, 65), (900630, 93, 1044),
    (900630, 30011, 4096),   -- AcxPouchItemTypes: spell components
    (900631, 1, 512), (900631, 5, 15), (900631, 6, 102), (900631, 8, 200), (900631, 9, 0), (900631, 16, 56), (900631, 19, 65), (900631, 93, 1044),
    (900631, 30011, 2048);   -- AcxPouchItemTypes: gems

INSERT INTO weenie_properties_float (object_Id, type, value) VALUES
    (900630, 39, 1.75), (900631, 39, 1.75);

INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES
    (900630, 1, 0x02000152 + 0), (900630, 3, 0x20000014 + 0), (900630, 6, 0x04000BEF + 0), (900630, 7, 0x100004F5 + 0), (900630, 8, 0x06001011 + 0), (900630, 22, 0x3400002B + 0),
    (900631, 1, 0x02000152 + 0), (900631, 3, 0x20000014 + 0), (900631, 6, 0x04000BEF + 0), (900631, 7, 0x100004F5 + 0), (900631, 8, 0x06001011 + 0), (900631, 22, 0x3400002B + 0);

INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900630, 1, 'Spell Component Pouch'),
    (900630, 15, 'Holds only spell components. A pouch and what''s in it are safe when you die.'),
    (900631, 1, 'Gem Pouch'),
    (900631, 15, 'Holds only gems. A pouch and what''s in it are safe when you die.');

-- shops: both pouches at every vendor (WeenieType 12) that sells spell components (WeenieType 32): archmages,
-- provisioners, grocers. The Gem Pouch holds anything that is a gem, portal and spell gems included (as ClassicACE's).
INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond)
SELECT DISTINCT c.object_Id, 4, p.wcid, -1, 0, 0, 0
FROM weenie_properties_create_list c JOIN weenie v ON v.class_Id = c.object_Id AND v.type = 12 JOIN weenie i ON i.class_Id = c.weenie_Class_Id AND i.type = 32
CROSS JOIN (SELECT 900630 wcid UNION ALL SELECT 900631) p
WHERE c.destination_Type = 4;
