# AC:Unreal client plugins

Lua plugins for AC:Unreal's plugin API (api 2), written while playing on our ACE server. They're
not server-specific: they work on any server AC:Unreal connects to. Work in progress; shared as we build them.

## Install

Copy a plugin's folder (e.g. `mudsort`) into AC:Unreal's `Saved/ClientPlugins/Installed`, open **Plugins**, press
**Refresh**, enable it and allow the permissions it asks for.

## The plugins

| Plugin | Version | What it does | Status |
|---|---|---|---|
| [MudSort](mudsort) | 1.1.0 | A remake of Mudzereli's MudSort for Decal: sorts a pack by any property (damage, burden, value, type, name...) into a side pack, or hands the items to a player or NPC. Same sort strings (`OC,MX-,NM`). | Works into your own side packs; see its README for what the API holds back |
| [Auto Sell](autosell) | 0.3.1 | Sells what's in your packs to the open vendor by your own sell list and exceptions; a UCM loot profile or `.utl` file's Keep rules protect items. Imbued, inscribed and tinkered items are skipped. | Working |
| [Pack Mover](packmover) | 0.1.0 | A test tool: moves the weapons from one pack to another, the main pack included, and reports which moves the client or server refused. | A probe of the API, not a finished tool |

## The plugin API limit we keep hitting

Plugins move items with the `store_item` action. `ACEPluginAutomation.cpp` (around line 1528) only allows it when the item
is already in your own inventory and the target is one of your own side packs (`C->IsOwnedInventoryItem(Bag)`), and it
always places the item at the front (`SendPutItemInContainer(Id, Bag.Guid, 0)`). So a plugin can't move items into or out
of chests or house storage, can't target the main pack, and can't pick a slot. Opening `store_item` to an open external
container and the main pack would let MudSort move items between storage and your packs, and would make "drag a pack
onto an item slot to empty it" possible as a plugin.
