-- Project ACX: no heritage or allegiance rank requirements on items.
-- Heritage restrictions sat on 45 items (the heritage robes and a few quest items such as King's Helm and Pwyll's Crown);
-- allegiance rank limits on 13 (such as the Staff of Aerfalle), plus 0-value entries on 757 casters. Re-runnable.
DELETE FROM weenie_properties_string WHERE type = 19;   -- ItemHeritageGroupRestriction
DELETE FROM weenie_properties_int WHERE type = 110;     -- ItemAllegianceRankLimit
