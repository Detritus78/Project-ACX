-- Project ACX: tusker tusks can be picked up once a week, like the Olthoi pincers and Eater jaws (JJ 2026-11-13; this
-- world data had 20 hours). Brighteyes, the Tailor pays XP for each tusk with no timer of her own: the timer is the
-- pickup quest on each tusk.
-- Re-runnable. Restart the server after applying (quests are cached).

UPDATE quest SET min_Delta = 604800, last_Modified = NOW()
WHERE name IN (
    'TuskFemalePickup', 'TuskMalePickup', 'TuskCrimsonbackPickup', 'TuskGoldenbackPickup', 'TuskRedeemerPickup',
    'TuskLiberatorPickup', 'TuskSlavePickup', 'TuskGuardPickup', 'TuskSilverPickup', 'TuskArmoredPickup',
    'TuskRampagerPickup', 'TuskPlatedPickup', 'TuskAssailerPickup', 'TuskDevastatorPickup', 'TuskSnowPickup_0513');
