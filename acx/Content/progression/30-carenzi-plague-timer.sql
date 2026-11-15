-- Project ACX: the Carenzi Plague (using a Diseased Carenzi Liver for 35-40% of a level) can be done once every 20 hours
-- instead of every 2 (JJ 2026-11-13), like the other big repeatable XP quests.
-- Re-runnable. Restart the server after applying (quests are cached).

UPDATE quest SET min_Delta = 72000, last_Modified = NOW() WHERE name = 'CarenziPlagueStarted';
