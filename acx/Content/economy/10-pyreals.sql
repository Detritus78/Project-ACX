-- Project ACX: pyreal tuning (JJ 2026-11-12). Monsters carry no coins: their loot roll's coins are re-rolled as another
-- mundane item in code (LootGenerationFactory_AcxCoins.cs), and the few creatures with a fixed coin drop lose it here.
-- Treasure chests keep their coins. Coin stacks hold 50,000 instead of 25,000 (the client can't show more than 65,535);
-- coins already in the game take the new size when loaded (Coin.cs).
-- Re-runnable.

-- fixed coin drops on creatures (Banderling Sentry, Bandit, the Pumpkin Buffer event creature, the test creature 1)
DELETE c FROM weenie_properties_create_list c JOIN weenie w ON w.class_Id = c.object_Id
WHERE c.weenie_Class_Id = 273 AND w.type IN (10, 15);   -- 273 Pyreal; Creature, Cow

-- coin stacks: MaxStackSize (int 11)
UPDATE weenie_properties_int SET value = 50000 WHERE object_Id = 273 AND type = 11;
