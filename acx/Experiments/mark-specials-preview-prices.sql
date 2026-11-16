-- Project ACX experiment (Preview only; never in acx/Content): the Mark Trader's specials at 1 ACX Mark each, the lowest
-- a vendor charges, so JJ can try them before the price pass (JJ 2026-11-15: "go ahead and remove the prices for them for
-- now"). acx/Content keeps the placeholder prices (specials/10-mark-specials.sql, invasions/20-mark-vendor.sql); running
-- apply-content on Preview puts those back, so run this again after it. Restart (or reload the Mark Trader) after applying.
UPDATE weenie_properties_int SET value = 1 WHERE type = 19 AND (
       object_Id IN (30247, 30248, 30249, 30254, 30253, 30151, 30165)
    OR (object_Id BETWEEN 30082 AND 30091 AND object_Id <> 30084)
    OR object_Id BETWEEN 900711 AND 900720
    OR object_Id BETWEEN 900860 AND 900879);
