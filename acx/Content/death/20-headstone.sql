-- Project ACX: the "Here lies" headstone a corpse leaves when its owner's vitae is gone (WorldObjects/Corpse_AcxVitae.cs).
-- A small, ethereal copy of retail's Old Gravestone (34104, "Here lies Drago the Flat..."), without its use emote.
-- The server names each one "Here lies <name>", writes who killed them and when into its description, and saves it
-- for good (it never decays).
--   900620  Headstone
-- Re-runnable. Restart the server after applying.

DELETE FROM weenie WHERE class_Id = 900620;

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) VALUES (900620, 'acx-headstone', 1, NOW());

INSERT INTO weenie_properties_int (object_Id, type, value) VALUES
    (900620, 1, 128),      -- ItemType: misc
    (900620, 5, 0),        -- EncumbranceVal
    (900620, 16, 1),       -- ItemUseable: no (assess to read it)
    (900620, 19, 0),       -- Value
    (900620, 93, 1044);    -- PhysicsState: ethereal, gravity

INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES
    (900620, 1, 1),        -- Stuck
    (900620, 12, 0),       -- ReportCollisions
    (900620, 13, 1),       -- Ethereal
    (900620, 14, 1);       -- GravityStatus

INSERT INTO weenie_properties_float (object_Id, type, value) VALUES
    (900620, 39, 0.7);     -- DefaultScale: small

INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES
    (900620, 1, 0x02001631 + 0),   -- Setup (Old Gravestone)
    (900620, 2, 0x09000021 + 0),   -- MotionTable
    (900620, 8, 0x06006548 + 0);   -- Icon

INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900620, 1, 'Headstone'),
    (900620, 16, 'Here lies someone who met a monster before they found a lifestone.');
