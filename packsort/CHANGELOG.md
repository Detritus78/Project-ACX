# PackSort changelog

## 2.0.2
- One Open/Close button instead of two side by side. The window's X just hides it; PackSort stops by itself
  when UCM or another automation plugin starts.

## 2.0.1
- Build tab: hover a code for its full name (OC shows Object Class), the Key letter for its kind, and the column
  header for what each column does.

## 2.0.0
- Renamed from Pack Mover, with its own toolbar icon.
- MudSort is built in: the Sort, Build and Options tabs sort a pack by any property with MudSort's sort strings.
- Sorting works into the main pack, into and out of an open chest or vault, and in place; Insert Position picks where
  the sorted items start.
- A player or NPC can be the destination: the items are handed to them.
- Commands: `/acueplugin packsort start`, `stop`, `set source`, `set dest`, `set flags`, `set ocfilter`, `clear ocfilter`.

## 1.2.0
- Needs AC:Unreal release 166 or later.
- The main pack can now be a destination.
- An open chest or vault, and the side packs inside it, can be the source or the destination: select it and click Set
  Source or Set Destination. The choice is kept while it's closed; Move needs it open again.
- A stack that merges into one already in the destination counts as moved.

## 1.1.1
- Moves go out as fast as the client allows (one a second) instead of waiting for each to land first.

## 1.0.0
- First version: moves every item of one type (jewelry, gems, salvage, armor, essences and more) from a pack or the main
  pack into a side pack, stopping when it's full.
