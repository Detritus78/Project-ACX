#!/usr/bin/env bash
# Project ACX: Live's nightly backup, run on the Live laptop by acx-live-backup.timer (4 AM). A full dump of ace_auth,
# ace_shard and ace_world (as acx.sh backup), gzipped into ~/acx-backups; the newest 14 kept. The desktop copies them to
# its HDD (acx-backup-pull.sh) and sends one a week to GitHub (acx-backup-offsite.sh). JJ 2026-11-17: full backups, not
# diffs (each one restores on its own; 23 MB apiece).
set -euo pipefail

dir="$HOME/acx-backups"
keep=14
container=acx-live-db-1

mkdir -p "$dir"
out="$dir/acx-live-$(date +%Y%m%d-%H%M%S).sql.gz"
trap 'rm -f "$out.part"' EXIT

docker exec "$container" sh -c 'exec mysqldump -uroot -p"$MYSQL_ROOT_PASSWORD" --single-transaction --routines --databases ace_auth ace_shard ace_world' 2>/dev/null \
    | gzip > "$out.part"
# a dump cut short has no closing line
zcat "$out.part" | tail -1 | grep -q "Dump completed" || { echo "acx-live-nightly: the dump is incomplete" >&2; exit 1; }
mv "$out.part" "$out"

ls -1t "$dir"/acx-live-*.sql.gz | tail -n +$((keep + 1)) | xargs -r rm --
echo "backup: $out ($(du -h "$out" | cut -f1))"
