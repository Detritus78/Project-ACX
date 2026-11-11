# Project ACX

Custom end-of-retail ACE server. `main` = ACEmulator/ACE (`upstream` remote) plus ACX changes.
ACX-only code lives in files named `*_SpellTransfer.cs` etc.; edits to ACE's own files are kept to a few
marked lines so upstream merges stay easy.

## Instances

| Instance | Game port | DB port (localhost) | Data |
|---|---|---|---|
| Live | 9000 | 3306 | `~/acx-data/live` |
| Preview | 9010 | 3307 | `~/acx-data/preview` |

`acx/scripts/acx.sh` runs everything (`up`, `down`, `logs`, `console`, `status`, `backup`,
`apply-content`, `clone-live-to-preview`). Passwords are in `acx/live.env` / `acx/preview.env`
(gitignored). Both instances have `AutoUpdateWorldDatabase` off: world DB updates go through Preview.

## Custom content

SQL files under `acx/Content/` (applied in path order with `acx.sh apply-content <instance>`).
Every file must be re-runnable: delete its own rows, then insert.

Custom properties use IDs 30000+ so they never collide with ACE's.

## Updating ACE

1. `git fetch upstream && git switch -c update/<version> main && git merge upstream/master`
2. `acx.sh backup live`, then `acx.sh clone-live-to-preview`
3. Rebuild Preview from the update branch: `acx.sh up preview`, then `acx.sh apply-content preview`
4. Test on port 9010. If good: merge to `main`, `acx.sh up live`, `acx.sh apply-content live`

## Features

- Spell extraction / transfer scrolls (ClassicACE): extraction scrolls I-VII sold by 97 mage vendors.
