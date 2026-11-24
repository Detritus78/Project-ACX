# Pack Mover for AC:Unreal

Moves every item of one type from a pack into a side pack: jewelry, gems, salvage, armor, summoning essences and more.

## Install

Copy this `packmover` folder into `Saved/ClientPlugins/Installed`, open **Plugins**, press **Refresh** and enable
**Pack Mover**, then **Open Pack Mover**.

## Use

1. Select a pack (or your character, for the main pack) and click **Set Source**.
2. Select a side pack and click **Set Destination**. It shows how many slots are free.
3. Pick a **Type** and click **Move**. **Stop** halts it.

Types: Jewelry, Gems, Salvage, Armor, Clothing, Weapons (all, melee, missile or casters), Summoning essences, Spell
components, Mana stones, Food, Keys, Scrolls and books, Trade notes, Alchemy, Fletching, Cooking, Misc, Useless, or
Everything.

It stops when the destination is full ("Moved 18; Pack 3 is full, 4 stayed.") and never moves equipped items, packs or
pyreals. Your source, destination and type are remembered.

## Notes

- About one item a second: AC:Unreal allows a plugin one item move per second.
- The main pack can be a source but not a destination: plugins can only move items into your own side packs.
