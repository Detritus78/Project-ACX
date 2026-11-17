#!/usr/bin/env bash
# Project ACX: the weekly offsite copy (JJ 2026-11-17: "yes set it up on github"). Live's newest backup on the desktop's
# HDD, encrypted with gpg (AES256; the passphrase in ~/.config/acx/backup-passphrase, which JJ also keeps written down) and
# uploaded as a release file to the private repo Detritus78/acx-backups; the newest 12 releases kept. Run daily by
# acx-backup-offsite.timer and uploads only when the last upload is a week old, so a day the HDD was missing just
# moves it to the next day.
# To restore one: gh release download <tag> -R Detritus78/acx-backups, then
#   gpg --batch --pinentry-mode loopback --passphrase-file ~/.config/acx/backup-passphrase -d <file>.gpg > <file>
set -euo pipefail

repo=Detritus78/acx-backups
keep=12
hdd=/run/media/jj/HDDStorage/acx-backups
passphrase="$HOME/.config/acx/backup-passphrase"

[[ -d "$hdd" ]] || { echo "acx-backup-offsite: the HDD isn't mounted" >&2; exit 1; }
[[ -s "$passphrase" ]] || { echo "acx-backup-offsite: $passphrase missing" >&2; exit 1; }

last=$(gh release list -R "$repo" --limit 1 --json createdAt -q '.[0].createdAt // empty')
if [[ -n "$last" ]] && (( $(date +%s) - $(date -d "$last" +%s) < 7 * 86400 - 3600 )); then
    echo "not due (last upload $last)"
    exit 0
fi

# the nightlies, or before those exist the commit backups (acx-live-<date>-<time>, not acx-live-config-...)
src=$(ls -1t "$hdd"/nightly/acx-live-*.sql.gz "$hdd"/acx-live-[0-9]*.sql.gz 2>/dev/null | head -1 || true)
[[ -n "$src" ]] || { echo "acx-backup-offsite: no Live backup on the HDD" >&2; exit 1; }
name=$(basename "$src" .sql.gz)
tag="${name#acx-live-}"
if gh release view "$tag" -R "$repo" >/dev/null 2>&1; then
    echo "acx-backup-offsite: $name is already uploaded and no newer backup exists" >&2
    exit 1
fi

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
gpg --batch --yes --pinentry-mode loopback --passphrase-file "$passphrase" --symmetric --cipher-algo AES256 \
    -o "$tmp/$name.sql.gz.gpg" "$src"
gh release create "$tag" "$tmp/$name.sql.gz.gpg" -R "$repo" --title "Live $tag" \
    --notes "Project ACX Live: auth, shard and world databases (mysqldump, gzip, gpg AES256). sha256 of the .sql.gz: $(sha256sum "$src" | cut -d' ' -f1)" >/dev/null

gh release list -R "$repo" --limit 100 --json tagName,createdAt -q "sort_by(.createdAt) | reverse | .[$keep:] | .[].tagName" \
    | while read -r old; do gh release delete "$old" -R "$repo" --yes --cleanup-tag; done
echo "uploaded $name"
