#!/usr/bin/env python3
"""Project ACX: capture what JJ placed in Concordia (landblock 018A) with /createinst on Preview into
acx/Content/concordia/80-placements.sql. Vault hooks are no longer made here: they hang on the wall behind each vault
(86-vault-hooks.sql, JJ 2026-11-14: "none of the hooks are on a wall they are just floating"); the old hook range is
still cleared.

/createinst numbers new objects after the highest guid on the landblock, so JJ's placements start right after the
gambling dens (0x7018AF37) and run to 0x7018AFFF. The file is re-runnable; run this again after every placing session, before apply-content, or a re-apply
deletes placements made since the last capture.

    python3 -I acx/scripts/concordia/capture.py [--apply]

--apply also loads the file into Preview's world DB (then /reload-landblock in Concordia, or restart).
"""
import argparse
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
OUT = ROOT / "acx/Content/concordia/80-placements.sql"

PLACED_FIRST, PLACED_LAST = 0x7018AF38, 0x7018AFFF
HOOK_FIRST, HOOK_LAST = 0x7018AE00, 0x7018AEFF
VAULT, HOOK = 900700, 900701

NAMES = {
    900700: "Vault", 900701: "Vault Hook", 900707: "Vault Keeper", 45501: "Mark Trader",
    22755: "Temple of Enlightenment head", 22759: "Temple of Forgetfulness head",
}
NAMES.update({wcid: "decor pile" for wcid in range(900740, 900752)})


def db(sql, apply_file=None):
    cmd = ["docker", "exec", "-i", "acx-preview-db-1", "sh", "-c",
           'exec mysql -uroot -p"$MYSQL_ROOT_PASSWORD" -N -B ace_world 2>/dev/null']
    stdin = apply_file.read_text() if apply_file else sql
    return subprocess.run(cmd, input=stdin, capture_output=True, text=True, check=True).stdout


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    rows = [line.split("\t") for line in db(
        "SELECT guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z "
        f"FROM landblock_instance WHERE guid BETWEEN {PLACED_FIRST} AND {PLACED_LAST} AND is_Link_Child = 0 ORDER BY guid;"
    ).splitlines() if line]

    if not rows:
        sys.exit(f"nothing placed in 0x{PLACED_FIRST:08X}-0x{PLACED_LAST:08X} on Preview; {OUT.name} left as it is")

    vaults = [r for r in rows if int(r[1]) == VAULT]

    lines = [
        "-- Project ACX: what JJ placed in Concordia with ACX Foundry (/createinst), captured from Preview by",
        "-- acx/scripts/concordia/capture.py. Generated: edit by placing in game and",
        "-- capturing again, not by hand. Re-runnable.",
        "",
        f"DELETE FROM landblock_instance WHERE guid BETWEEN 0x{PLACED_FIRST:08X} AND 0x{PLACED_LAST:08X};",
        f"DELETE FROM landblock_instance WHERE guid BETWEEN 0x{HOOK_FIRST:08X} AND 0x{HOOK_LAST:08X};",
    ]

    values = []
    for guid, wcid, cell, x, y, z, w, ax, ay, az in rows:
        name = NAMES.get(int(wcid), "")
        values.append(f"    (0x{int(guid):08X}, {wcid}, 0x{int(cell):08X}, {x}, {y}, {z}, {w}, {ax}, {ay}, {az}, 0)"
                      + (f"  -- {name}" if name else ""))

    if values:
        lines.append("INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, "
                     "angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES")
        # comments sit after each row's comma, so the SQL stays valid
        body = []
        for i, v in enumerate(values):
            row, _, comment = v.partition("  -- ")
            body.append(row + ("," if i < len(values) - 1 else ";") + (f"  -- {comment}" if comment else ""))
        lines.extend(body)

    OUT.write_text("\n".join(lines) + "\n")
    counts = {}
    for r in rows:
        label = NAMES.get(int(r[1]), f"wcid {r[1]}")
        counts[label] = counts.get(label, 0) + 1
    print(f"{OUT.relative_to(ROOT)}: {len(rows)} placements, {len(vaults)} vaults")
    for label, count in sorted(counts.items()):
        print(f"  {count:3} {label}")

    if args.apply:
        db(None, apply_file=OUT)
        print("applied to Preview's world DB")


if __name__ == "__main__":
    main()
