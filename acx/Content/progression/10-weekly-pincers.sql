-- Project ACX: Olthoi pincers and Eater jaws can be picked up once a week (JJ 2026-11-13; this world data had 20 hours,
-- retail was longer). Their XP turn-ins (Behdo Yii, Clemenza) have no timer of their own: the timer is the pickup
-- quest on each item. Weekly keeps them a big weekly round instead of a nightly chore; at xp_modifier 1.4 they still
-- more than cover what T.A.R. takes from kill XP (bench/progression.py, scratchpad turnin-analysis.md).
-- The OlthoiHunting quests also steer Behdo Yii's "next hunt" talk, which now follows the same week.
-- Brood Matron's other parts (claw, crest, carapace, tail...) keep their 20 hours.
-- Re-runnable. Restart the server after applying (quests are cached).

UPDATE quest SET min_Delta = 604800, last_Modified = NOW()
WHERE name IN (
    'OlthoiHunting1', 'OlthoiHunting2', 'OlthoiHunting3', 'OlthoiHunting4',    -- Harvester, Gardener, Soldier, Legionary
    'OlthoiHunting5', 'OlthoiHunting6', 'OlthoiHunting7', 'OlthoiHunting8',    -- Eviscerator, Worker, Warrior, Mutilator
    'HiveEvisPincer_0513', 'HiveWarriorPincer_0513', 'BroodPincerPickup', 'BroodMatronPincerPickedUp',
    'engorgedeaterjaw', 'voraciouseaterjaw', 'abhorrenteaterjaw', 'ravenouseaterjaw',
    'insatiableeaterjaw', 'upperinsatiablejaw');
