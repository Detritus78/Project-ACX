-- Project ACX experiment (Preview only; never in acx/Content): the Mark Trader's specials at 1 ACX Mark each, the lowest
-- a vendor charges, so JJ can try them before the price pass (JJ 2026-11-15: "go ahead and remove the prices for them for
-- now"). acx/Content keeps the placeholder prices (specials/10-mark-specials.sql, invasions/20-mark-vendor.sql); running
-- apply-content on Preview puts those back, so run this again after it. Restart (or reload the Mark Trader) after applying.
UPDATE weenie_properties_int SET value = 1 WHERE type = 19 AND (
       (object_Id BETWEEN 30082 AND 30091 AND object_Id <> 30084)   -- (not the retail eternal rares: the Trader sells copies, 900880-900886)
    OR object_Id BETWEEN 900711 AND 900720
    OR object_Id BETWEEN 900860 AND 900879
    OR object_Id BETWEEN 900880 AND 900886   -- the attuned copies (specials/20-attuned-goods.sql)
    OR object_Id BETWEEN 45502 AND 45516   -- the retail gems (specials/30-mark-prices.sql)
    OR object_Id = 20908                   -- the Major Prismatic Stone
    OR object_Id = 900898);                -- the Alchemist's Healing bomb (specials/40-alchemist-trader.sql)
UPDATE weenie_properties_int SET value = 1 WHERE type = 15 AND object_Id BETWEEN 45502 AND 45516;   -- their StackUnitValue
