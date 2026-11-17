#!/usr/bin/env bash
# Project ACX: copies the Live laptop's nightly backups (acx-live-nightly.sh) to the desktop's HDD over Tailscale, run by
# acx-backup-pull.timer (5 AM, or at the next start if the desktop was off). Keeps every one (23 MB apiece). Fails, and so
# raises the desktop alert, when the HDD isn't mounted or the laptop's newest backup is over 36 hours old.
set -euo pipefail

hdd=/run/media/jj/HDDStorage/acx-backups
dest="$hdd/nightly"

[[ -d "$hdd" ]] || { echo "acx-backup-pull: the HDD isn't mounted (udisksctl mount -b /dev/disk/by-label/HDDStorage)" >&2; exit 1; }
mkdir -p "$dest"
rsync -a --ignore-existing acx-live:acx-backups/ "$dest/"

newest=$(ls -1t "$dest"/acx-live-*.sql.gz 2>/dev/null | head -1)
[[ -n "$newest" ]] || { echo "acx-backup-pull: no backups on the laptop yet" >&2; exit 1; }
age=$(( ($(date +%s) - $(stat -c %Y "$newest")) / 3600 ))
(( age <= 36 )) || { echo "acx-backup-pull: Live's newest backup, $(basename "$newest"), is $age hours old" >&2; exit 1; }
echo "pulled; newest $(basename "$newest")"
