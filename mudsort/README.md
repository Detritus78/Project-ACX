# MudSort for AC:Unreal

Sorts the items in a pack by any property into a side pack, or hands them to a selected player or NPC. A port of
Mudzereli's MudSort for Decal (MudSort 1.6.7, Copyright (c) Mudzereli 2022): new code, the same idea, and the same sort
strings, so a MudSort string like `OC,MX-,NM` works here too.

**Status: work in progress (1.1.0).** Sorting a pack into one of your own side packs works. What it can't do yet is held
back by the plugin API, not the plugin: AC:Unreal's `store_item` action (ACEPluginAutomation.cpp) only moves an item you
own into one of your own side packs, always at the front. So no sorting into the main pack, no chests or house storage
(in or out), and no insert position. If `store_item` accepted an open external container and the main pack, this could
also move items between storage and your packs.

## Install

Copy this `mudsort` folder into `Saved/ClientPlugins/Installed`, open **Plugins**, press **Refresh**, enable **MudSort**
and allow its permissions (inventory, data, ui, state).

## Use

Click **MudSort** on the plugin bar, then **Open MudSort**. The window has MudSort's tabs:

- **Sort**: **Set Source** (select your character for the main pack, or a side pack, then click) and **Set Dest** (a side
  pack, or a player or NPC to hand the items to). You can also type `player` or `pack 1`-`pack 8`. The **Sort String**
  takes MudSort codes (a `-` after a code means highest first), with **Copy** and **Paste**. **Object Class Filter**
  sorts only one kind of item. **Activate** identifies what the sort needs, moves the items in order, and stops; click
  it again to cancel.
- **Build**: builds the sort string by clicking: **Up**/**Dn** reorder, **Rem** removes, **Lo**/**Hi** flips the order,
  **Add** adds a key. The filter picks commonly used keys, all keys, or one kind.
- **Options**: identify your inventory while the window is open, completely reverse the sort, a chat message when done,
  **Property Dump Selection** (every sortable property of the selected item), and three stored sort strings with
  Copy/Paste. Settings save by themselves, per character.

**Close MudSort** when you're done; UCM pauses while it's open. Equipped items, packs and foci never move. Each item lands
at the front of the destination pack, so the sorted list starts at the front.

While it's open, `/acueplugin mudsort <command>` takes MudSort's commands: `start`, `stop`, `set source (pack <1-8>|player)`,
`set dest (pack <1-8>)`, `set flags <sort string>`, `set ocfilter <class>`, `clear ocfilter`.

## Sort codes

Common ones: `OC` item type, `NM` name, `MX` max damage, `DB` damage bonus (a launcher's multiplier), `ED` elemental
damage bonus, `VN` variance, `AB` attack bonus, `AL` armor level, `BR` burden, `VL` value, `MT` material, `WR`
workmanship, `WV` wield requirement value, `ES` equipped slots, `EQ` equipable slots, `CV` coverage, `ML` minimum level,
`TR` total ratings. All 113 of MudSort's codes are in `main.lua` (`CODES`). `DG` and `MA`, which MudSort's own default
string used but never recognized, are read as `DB` and `MT`.

## Differences from the Decal version

- AC:Unreal lets plugins move items into side packs, not into the main pack. The main pack can be the source.
- It runs as an automation plugin: AC:Unreal runs those only while started, so the window opens with **Open MudSort**, and
  UCM pauses while it's open.
- The "buffed" sort keys use the item's own numbers; buffs aren't worked out.
- Identifying runs at about one item a second, so the first sort of a full pack takes a minute or two.
- No Insert Position or chests: AC:Unreal always inserts at the front, and plugins can only move items into your own
  side packs.
