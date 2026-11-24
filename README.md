# AC:Unreal client plugins

Lua plugins for AC:Unreal's plugin API (api 2), written while playing on our ACE server. They're
not server-specific: they work on any server AC:Unreal connects to. Work in progress; shared as we build them.

## Install

Copy a plugin's folder (e.g. `autosell`) into AC:Unreal's `Saved/ClientPlugins/Installed`, open **Plugins**, press
**Refresh** and enable it. Enabling grants the permissions listed in its `plugin.json`; there is no prompt.

## The plugins

| Plugin | Version | What it does | Status |
|---|---|---|---|
| [Auto Sell](autosell) | 0.5.4 | Sells your packs to the open vendor with a preview: an "Only sell" filter (`Essence`), select an item to hold it back for this sale or keep it always, Exceptions with an X to remove, and an optional UCM loot profile whose Keep rules protect items. | Working |
