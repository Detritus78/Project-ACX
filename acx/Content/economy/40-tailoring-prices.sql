-- Project ACX (2026-11-24): tailoring 98% cheaper (JJ: "Tailoring is a fun part of the game and these prices ruin it. I
-- want to slash prices by 98%", "Everything on Qing", "its 50 MMD's"). The four tailors (Ianto, Iqbal, Qing, Ciriaco)
-- sold in Trade Notes (250,000), PropertyDataId.AlternateCurrency 57 = 20630, with the item's Value as the note count:
-- 1 MMD for the kits and layering tools, 50 for the reduction tools. One note is the smallest step, so they now sell
-- for pyreals at 2% of the old price: 250,000 x 1 x 2% = 5,000; 250,000 x 50 x 2% = 250,000, then 25,000 (JJ: "drop the reduction down to 25k"). No other vendor sells
-- these. Re-runnable; restart, or clearcache and reload the tailors' landblocks.
DELETE FROM weenie_properties_d_i_d WHERE type = 57 AND object_Id IN (42428, 42429, 42430, 42431);
UPDATE weenie_properties_int SET value = 5000 WHERE type = 19 AND object_Id IN (41956, 51445, 42724, 42726);    -- Armor/Weapon Tailoring Kit, Armor Layering Tool (Top/Bottom)
UPDATE weenie_properties_int SET value = 25000 WHERE type = 19 AND object_Id IN (42622, 44880, 44879);         -- Armor Main/Middle/Lower Reduction Tool
