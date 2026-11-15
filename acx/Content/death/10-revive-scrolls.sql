-- Project ACX: revive scrolls I, II and III (Managers/AcxRevive.cs, WorldObjects/Player_AcxGhost.cs).
-- Used on the corpse of someone waiting beside it as a ghost. I: instant, back with 1 health. II: 10 s kneeling cast,
-- back with half. III: 20 s cast, back with full vitals. Each gives back that death's vitae.
-- Plain generic items (the look of retail's Scroll of Dark Rain, 12022) carrying AcxReviveTier (int 30009); each tier
-- has its own coloured icon background. Sold by Life Magic scriveners in Ayan Baqur, Fort Tethana, Candeth Keep,
-- Wai Jhou and Arwic (added in code, AcxRevive.ShopItems); the copy a player buys resells for 1 pyreal.
-- Prices are placeholders for the balance pass.
--   900600-900602  Revive Scroll I-III
-- Re-runnable. Restart the server after applying.

DELETE FROM weenie WHERE class_Id BETWEEN 900600 AND 900602;

INSERT INTO weenie (class_Id, class_Name, type, last_Modified) VALUES
    (900600, 'acx-revive-scroll-1', 1, NOW()),
    (900601, 'acx-revive-scroll-2', 1, NOW()),
    (900602, 'acx-revive-scroll-3', 1, NOW());

INSERT INTO weenie_properties_int (object_Id, type, value)
SELECT w.id, p.type, CASE p.type WHEN 19 THEN w.price WHEN 30009 THEN w.tier ELSE p.value END
FROM (SELECT 900600 id, 1 tier, 5000 price UNION ALL SELECT 900601, 2, 20000 UNION ALL SELECT 900602, 3, 50000) w
CROSS JOIN (SELECT 1 type, 128 value          -- ItemType: misc
  UNION ALL SELECT 5, 30                      -- EncumbranceVal
  UNION ALL SELECT 9, 0                       -- ValidLocations
  UNION ALL SELECT 16, 2097160                -- ItemUseable: SourceContainedTargetRemote (from the pack, on something in the world)
  UNION ALL SELECT 19, 0                      -- Value (per tier)
  UNION ALL SELECT 93, 1044                   -- PhysicsState
  UNION ALL SELECT 94, 512                    -- TargetType: containers (corpses)
  UNION ALL SELECT 30009, 0                   -- AcxReviveTier (per tier)
) p;

INSERT INTO weenie_properties_bool (object_Id, type, value)
SELECT id, 23, 1 FROM (SELECT 900600 id UNION ALL SELECT 900601 UNION ALL SELECT 900602) w;   -- DestroyOnSell

INSERT INTO weenie_properties_float (object_Id, type, value)
SELECT id, 39, 1.5 FROM (SELECT 900600 id UNION ALL SELECT 900601 UNION ALL SELECT 900602) w;  -- DefaultScale

INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
SELECT w.id, p.type, CASE p.type WHEN 52 THEN w.underlay ELSE p.value END
FROM (SELECT 900600 id, 0x06003355 + 0 underlay UNION ALL SELECT 900601, 0x06003353 + 0 UNION ALL SELECT 900602, 0x06003359 + 0) w
CROSS JOIN (SELECT 1 type, 0x02000B55 + 0 value   -- Setup (Scroll of Dark Rain)
  UNION ALL SELECT 8, 0x06002265 + 0            -- Icon
  UNION ALL SELECT 22, 0x3400002B + 0           -- PhysicsEffectTable
  UNION ALL SELECT 52, 0                      -- IconUnderlay (per tier)
) p;

INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
    (900600, 1, 'Revive Scroll I'),
    (900601, 1, 'Revive Scroll II'),
    (900602, 1, 'Revive Scroll III'),
    (900600, 14, 'Use this on the body of someone waiting beside it as a ghost: they come back to life at once, with 1 health, and their vitae from that death is lifted.'),
    (900601, 14, 'Use this on the body of someone waiting beside it as a ghost, then kneel still for 10 seconds: they come back with half their health, stamina and mana, and their vitae from that death is lifted. Moving cancels it and keeps the scroll.'),
    (900602, 14, 'Use this on the body of someone waiting beside it as a ghost, then kneel still for 20 seconds: they come back with full health, stamina and mana, and their vitae from that death is lifted. Moving cancels it and keeps the scroll.'),
    (900600, 16, 'A scroll of life magic that calls a spirit back into its body.'),
    (900601, 16, 'A scroll of life magic that calls a spirit back into its body.'),
    (900602, 16, 'A scroll of life magic that calls a spirit back into its body.');
