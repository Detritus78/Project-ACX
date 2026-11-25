# AC:Unreal client plugins

Lua plugins for AC:Unreal's plugin API (api 2), written while playing on our ACE server. They're
not server-specific: they work on any server AC:Unreal connects to. Work in progress; shared as we build them.

## Install

Download a plugin's zip from [Releases](https://github.com/Detritus78/Project-ACX/releases) and unzip it into
AC:Unreal's `Saved/ClientPlugins/Installed` (it unpacks as the plugin's folder), or copy the folder from this branch.
Then open **Plugins**, press **Refresh** and enable it.

## The plugins

| Plugin | Version | What it does | Status |
|---|---|---|---|
| [Auto Sell](autosell) | 0.6.1 | Sells your packs to the open vendor with a preview: an "Only sell" filter (`Essence`), select an item to hold it back for this sale or keep it always, Exceptions with an X to remove, and an optional UCM loot profile whose Keep rules protect items. | Working |
| [PackSort](packsort) | 2.1.0 | Moves every item of one type (jewelry, gems, salvage, armor, essences...), or only the items you pick, between your packs, the main pack and an open chest or vault, or sorts a pack by any property with MudSort's sort strings (a port of Mudzereli's MudSort). Can also hand items to a player or NPC. Needs release 166+. | Working |
