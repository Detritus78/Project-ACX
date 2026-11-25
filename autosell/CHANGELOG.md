# Auto Sell changelog

## 0.6.1
- Its own toolbar icon (a gold pyreal on green).

## 0.6.0
- **Scan appraises unassessed gear itself.** Weapons, armor, clothing and jewelry the client hasn't appraised yet used to be
  skipped silently; Scan now appraises them first (about one a second), then lists them.
- The status line says how many items still couldn't be appraised, so nothing disappears without a reason.

## 0.5.4
- **Only sell** box on the Sell page: blank sells everything, or words from item names (`Essence`, `Essence, Scarab`).
  Replaces the Sell list tab.
- **Selecting an item** in Will sell can remove it from this sale only (default) or keep it always (adds an Exception).
  Items removed from this sale are listed below and can be put back; removals clear when the sale ends or the vendor closes.
- Exceptions: one line per entry with an **X** on the right to remove it.
- Shorter Sell page: Kept, This vendor doesn't buy and Skipped lists removed; a one-line status.
- Shorter help text on the Exceptions and Profile tabs.

## 0.3.1
- First shared version: sells your packs to the open vendor by a sell list and exceptions, with an optional UCM loot
  profile or `.utl` whose Keep rules protect items.
