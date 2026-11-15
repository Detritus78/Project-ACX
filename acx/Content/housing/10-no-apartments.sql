-- Project ACX: no apartments (JJ 2026-11-14: "A lot of people complained about housing", keep only mansions).
-- Retail's 3,000 apartments sit in 50 apartment blocks, reached only through 5 lobbies ("Residential Halls": Atrium,
-- Haven, Oriel, Sanctum, Victory). JJ: "only remove the portals going to the lobbies from the towns ... that will do
-- it": these 15 town portals go; the lobbies and their portals into the blocks stay, unreached. Nobody owns
-- housing on Live or Preview (checked 2026-11-14). Cottages and villas are buildings in the client's map data and
-- can't be removed by the server. Re-runnable.
DELETE FROM landblock_instance WHERE guid IN (
    0x79722036,   -- Atrium Residential Halls Portal (in 9722, to lobby 5661)
    0x7A1A4048,   -- Atrium Residential Halls Portal (in A1A4, to lobby 5661)
    0x7CD41051,   -- Atrium Residential Halls Portal (in CD41, to lobby 5661)
    0x7A26001E,   -- Haven Residential Halls Portal (in A260, to lobby 5656)
    0x7BF80050,   -- Haven Residential Halls Portal (in BF80, to lobby 5656)
    0x7F559006,   -- Haven Residential Halls Portal (in F559, to lobby 5656)
    0x749B6039,   -- Oriel Residential Halls Portal (in 49B6, to lobby 565E)
    0x79E4401A,   -- Oriel Residential Halls Portal (in 9E44, to lobby 565E)
    0x7E532027,   -- Oriel Residential Halls Portal (in E532, to lobby 565E)
    0x78090029,   -- Sanctum Residential Halls Portal (in 8090, to lobby 5465)
    0x7BC9F03C,   -- Sanctum Residential Halls Portal (in BC9F, to lobby 5465)
    0x7E74D001,   -- Sanctum Residential Halls Portal (in E74D, to lobby 5465)
    0x7977B046,   -- Victory Residential Halls Portal (in 977B, to lobby 5650)
    0x79E43025,   -- Victory Residential Halls Portal (in 9E43, to lobby 5650)
    0x7DA3B02C);   -- Victory Residential Halls Portal (in DA3B, to lobby 5650)
