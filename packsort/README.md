# PackSort for AC:Unreal

Moves items between your packs, your main pack and an open chest or vault: every item of one type (jewelry, gems,
salvage, armor, essences and more), or a whole pack sorted by any property with MudSort's sort strings. It can also hand
the items to a player or NPC. Needs AC:Unreal release 166 or later.

Sorting is a port of Mudzereli's MudSort for Decal (MudSort 1.6.7, Copyright (c) Mudzereli 2022): new code, the same
idea and the same sort strings, so a MudSort string like `OC,MX-,NM` works here too.

## Install

Copy this `packsort` folder into `Saved/ClientPlugins/Installed`, open **Plugins**, press **Refresh** and enable
**PackSort**, then **Open PackSort**.

## Source and destination

Both tabs share them. Select one of these and click **Set Source** or **Set Destination**:

- one of your packs, or your character for the main pack
- an open chest or vault, or a pack inside it
- for the destination only: a player or NPC, to give the items to

The destination shows how many slots are free. A chest or vault choice is remembered while it's closed; open it again
before you start.

## Move tab

Pick a **Type** and click **Move**: Jewelry, Gems, Salvage, Armor, Clothing, Weapons (all, melee, missile or casters),
Summoning essences, Spell components, Mana stones, Food, Keys, Scrolls and books, Trade notes, Alchemy, Fletching,
Cooking, Misc, Useless, or Everything. It stops when the destination is full ("Moved 18; Pack 3 is full, 4 stayed.").

**Add Selected Item** narrows it to exact item kinds: select an item in your pack (a Red Letter, say) and click it; add
as many kinds as you like. With any picked, Move takes only those, whatever the Type. With a player as the destination,
only those items are given. **Clear** goes back to the Type.

## Sort tab

- **Sort String**: MudSort codes, comma separated; a `-` after a code means highest first. **Copy** and **Paste** move it
  to and from the stored strings on Options.
- **Insert Position**: where in the destination the sorted items start (0 is the front).
- **Object Class Filter**: sort only one kind of item.
- **Activate** identifies what the sort needs, then moves the items into place in order. Click it again to cancel.

Source and destination can be the same pack: that sorts it in place.

## Build tab

Builds the sort string by clicking: **Up**/**Dn** reorder, **Rem** removes, **Lo**/**Hi** flips the order, **Add** adds a
key. The filter shows commonly used keys, all keys, or one kind. Hover a code for its full name (`OC` shows Object Class), the Key letter for its kind
(S string, L long, D double, B boolean, C custom), and the column header for what each column does.

## Options tab

Identify your items while the window is open, completely reverse the sort, a chat message when a sort is done,
**Property Dump Selection** (every sortable property of the selected item, by code), and three stored sort strings.
Settings save by themselves, per character.

## Sort codes

Common ones: `OC` item type, `NM` name, `MX` max damage, `DB` damage bonus (a launcher's multiplier), `ED` elemental
damage bonus, `VN` variance, `AB` attack bonus, `AL` armor level, `BR` burden, `VL` value, `MT` material, `WR`
workmanship, `WV` wield requirement value, `ES` equipped slots, `EQ` equipable slots, `CV` coverage, `ML` minimum level,
`TR` total ratings. All 113 of MudSort's codes are in `main.lua` (`CODES`).

## Commands

While it's open, `/acueplugin packsort <command>`: `start` and `stop` (a sort), `set source` and `set dest` (the selected
pack, or `player`, `pack <n>`, `storage`, `storage pack <n>`), `set flags <sort string>`, `set ocfilter <class>`,
`clear ocfilter`.

## Notes

- About one item a second: AC:Unreal allows a plugin one item move per second. Identifying for a sort runs at about the
  same speed the first time.
- Equipped items, packs, foci and pyreals never move.
- The "buffed" sort keys use the item's own numbers; buffs aren't worked out.
- Opening PackSort stops UCM (AC:Unreal runs one automation plugin at a time); start UCM again when you're done, which
  also stops PackSort. The window's X only hides it.
