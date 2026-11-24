# Auto Sell for AC:Unreal

Sells what's in your packs to the vendor you have open, with a preview first. You decide what goes; everything else
stays.

## Install

Copy this `autosell` folder into `Saved/ClientPlugins/Installed`, open **Plugins**, press **Refresh** and enable **Auto Sell**.

## Use

Open a vendor, then open Auto Sell.

**Sell tab**

- **Only sell**: leave blank to sell everything, or type words from item names to sell only those: `Essence`, or
  `Essence, Scarab`.
- **Scan** previews the sale: **Will sell** lists everything that would go, with an estimate of the pyreals. Gear the
  client hasn't appraised yet is appraised first (about one item a second).
- **Selecting an item** in Will sell does one of two things, your choice:
  - **Remove from this sale** (default): it's held back for this sale only and shows under **Removed from this sale**,
    where selecting it puts it back. Removals clear when the sale ends or you close the vendor.
  - **Keep always (Exception)**: it's added to your Exceptions and never sold.
- **Sell** sells the list, fast, and reports what sold. **Stop** halts it.

Scan looks at your main pack and every side pack. It never sells equipped items or the packs themselves, and only sees
what's on your character.

**Exceptions tab**

Items that are never sold, with an **X** to remove one. Add rules by name, type and more, or **Add selected item**.
Always kept as well: equipped and retained items, and gear that couldn't be appraised. Imbued, tinkered, inscribed, set
and rare items are kept by default.

**Profile tab**

Optional. Pick a UCM loot profile or an imported `.utl` file, and anything its rules would keep isn't sold. It's read
again at every scan, so new loot rules count straight away. Your Exceptions take priority.

## Notes

- Only items the open vendor buys are listed.
- An item that doesn't leave your packs within 6 seconds (the vendor refused it) is reported as not sold and the rest
  carry on.
