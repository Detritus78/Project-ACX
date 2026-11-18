#!/usr/bin/env python3
"""Project ACX (2026-11-18): the portals in the towns that lead to Concordia.

JJ: "take two of those and put one 90deg to the other in the center", "I was thinking about making it the portal from
towns to here"; the plain cross and the shared-ball version "look the same so whichever one is easier to make"; "I wonder
if we can bring the outside ones back to full size?"; then "we could put a destroyed portal in the town network portal
block and place this beside it. That kind of goes with the lore", "No lets just do it."

Retail's four Concordia Portal weenies (42852, 43065, 43066, 43067; they lead only to Concordia, 54 placements) get
Concordia's floor-portal look (portal_looks.py: the Portalling Device frame with a nether hub), at full size. Each one:
  - its old spot (in the town's obelisk circle) gets a ruin: retail's Destroyed Portal model (0x020019E4, as on
    "Destroyed Ahurenga Portal" 1013), scenery (901008);
  - the portal itself moves STEP_OUT in front of it, onto the ground there (or JJ's hand-set spot, 'placed');
  - a second frame turned 90 degrees (901006, scenery) and the red spark (901007) stand with it.
All four pieces are AcxExactPlacement: ACE would otherwise slide them off whatever they overlap.

Original spots are in town_portal_spots.json (so a rerun never moves a portal twice). Writes
acx/Content/concordia/96-town-concordia-portals.sql and, for the Town Tour gem on Preview, town-tour.json next to this
script (copy it to Preview's Logs/acx-town-tour.json). Instance guids: 0x7LLLLE00 + n in each landblock (LLLL), free
there on 2026-11-18."""
import json, math, os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
OUT = os.path.join(ROOT, "acx", "Content", "concordia", "96-town-concordia-portals.sql")
TOUR = os.path.join(HERE, "town-tour.json")
SPOTS = os.path.join(HERE, "town_portal_spots.json")
TOWNS = os.path.join(ROOT, "acx", "Content", "invasions", "towns.json")
HAVENCELLS = os.path.expanduser("~/Work/ac/tools/havencells/bin/Release/net10.0/havencells")
DATS = "/mnt/games/AC-Data"

PORTALS = (42852, 43065, 43066, 43067)
FRAME_SCALE = 1.0            # full size out in the towns; Concordia's are 0.7
HUB_PICTURE = 0x0500303D     # as portal_looks.py's floor portals
CROSS_WCID = 901006          # the second frame
SPARK_WCID = 901007          # red spark at the frame's scale: Concordia's (901005) is 0.5 in a 0.7 frame
SPARK_SCALE = 0.5 / 0.7 * FRAME_SCALE
SPARK_LIFT = 2.29 * FRAME_SCALE
RUIN_WCID = 901008
NORMAL_WCID = 901009         # retail's own look, for the one spot that isn't a town (Adventure Haven)
STEP_OUT = 5.0               # the portal stands this far in front of its ruin (clear of the circle's stone platform)
TOUR_STEP = 1.5              # the Town Tour drops you this far in front of the ruin: between it and the portal


def run(*args):
    out = subprocess.run([HAVENCELLS, DATS, *args], capture_output=True, text=True)
    if out.returncode:
        sys.exit(out.stderr)
    return out.stdout.split()


def forward(s):
    heading = math.atan2(2 * s["w"] * s["rz"], 1 - 2 * s["rz"] * s["rz"])
    return -math.sin(heading), math.cos(heading)


def ground(s, dist):
    """the spot dist in front of s: (cell hex, x, y, z) on the ground (outdoors) or on the floor of the right cell (indoors)"""
    fx, fy = forward(s)
    x, y = s["x"] + fx * dist, s["y"] + fy * dist
    if int(s["cell"], 16) & 0xFFFF >= 0x100:
        found = run("findcell", s["cell"], f"{x:.4f}", f"{y:.4f}", f"{s['z']:.3f}")
        if len(found) < 5 or found[0] not in ("same", "cell"):
            sys.exit(f"no floor {dist} m in front of {s}")
        return found[1], x, y, float(found[4])
    cell, gx, gy, gz = run("groundz", s["cell"], f"{x:.4f}", f"{y:.4f}")
    return cell, float(gx), float(gy), float(gz)


spots = json.load(open(SPOTS))["spots"]
towns = json.load(open(TOWNS))["towns"]


def town_near(cell):
    bx, by = int(cell[:2], 16), int(cell[2:4], 16)
    best = min(towns, key=lambda t: (int(t["cell"][:2], 16) - bx) ** 2 + (int(t["cell"][2:4], 16) - by) ** 2)
    far = (int(best["cell"][:2], 16) - bx) ** 2 + (int(best["cell"][2:4], 16) - by) ** 2
    return best["name"] if far <= 4 else f"landblock {cell[:4]}"


look = []
for w in PORTALS + (CROSS_WCID,):
    look += [f"DELETE FROM weenie_properties_d_i_d WHERE object_Id = {w} AND type IN (1, 2, 3);",
             f"INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES ({w}, 1, 0x02000EAD), ({w}, 2, 0x09000120), ({w}, 3, 0x20000014);",
             f"DELETE FROM weenie_properties_float WHERE object_Id = {w} AND type = 39;",
             f"INSERT INTO weenie_properties_float (object_Id, type, value) VALUES ({w}, 39, {FRAME_SCALE:g});",
             f"DELETE FROM weenie_properties_bool WHERE object_Id = {w} AND type IN (30002, 30017);",
             f"INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES ({w}, 30002, 1), ({w}, 30017, 1);   -- AcxCustomAppearance, AcxExactPlacement",
             f"DELETE FROM weenie_properties_texture_map WHERE object_Id = {w};",
             f"INSERT INTO weenie_properties_texture_map (object_Id, `index`, old_Id, new_Id) VALUES ({w}, 2, 0x05001773, 0x{HUB_PICTURE:08X});   -- nether hub"]

lines = [f"""-- Project ACX (2026-11-18): the town portals to Concordia, beside their town network ruins. GENERATED by
-- acx/scripts/concordia/town_portals_to_concordia.py; see there. Re-runnable. Runs after 95-portal-looks.sql (901005, copied).
DELETE FROM landblock_instance WHERE (guid & 0xF0000F00) = 0x70000E00 AND weenie_Class_Id IN ({CROSS_WCID}, 901005, {SPARK_WCID}, {RUIN_WCID});
DELETE FROM landblock_instance WHERE guid = 0x7BC9F435;   -- Cragstone's second Concordia portal, a duplicate on 7BC9F075's spot (JJ: "no delete one")
-- Adventure Haven's portal (01AC) keeps retail's look: a copy of 42852 as it was (JJ: "leave this one all normal")
DELETE FROM weenie WHERE class_Id = {NORMAL_WCID};
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT {NORMAL_WCID}, 'acx-concordia-portal-retail-look', type, NOW() FROM weenie WHERE class_Id = 42852;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT {NORMAL_WCID}, type, value FROM weenie_properties_int WHERE object_Id = 42852;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT {NORMAL_WCID}, type, value FROM weenie_properties_float WHERE object_Id = 42852 AND type <> 39;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT {NORMAL_WCID}, type, value FROM weenie_properties_bool WHERE object_Id = 42852 AND type NOT IN (30002, 30017);
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT {NORMAL_WCID}, type, value FROM weenie_properties_string WHERE object_Id = 42852;
INSERT INTO weenie_properties_position (object_Id, position_Type, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z) SELECT {NORMAL_WCID}, position_Type, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z FROM weenie_properties_position WHERE object_Id = 42852;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES ({NORMAL_WCID}, 1, 0x020001B3), ({NORMAL_WCID}, 2, 0x09000003), ({NORMAL_WCID}, 8, 0x0600106B);   -- 42852's own (backup of 2026-11-18)
-- other things JJ found wrong on the Town Tour (2026-11-18)
UPDATE landblock_instance SET obj_Cell_Id = 0xE63D0015, origin_Z = 86.005 WHERE guid = 0x7E63D03D;   -- Nanto's Small Creepy Statue (battle dungeon portal NPC 25987): 0.3 m up, in cell 0000; ground is 86
DELETE FROM weenie WHERE class_Id IN ({CROSS_WCID}, {SPARK_WCID}, {RUIN_WCID});
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) VALUES
    ({CROSS_WCID}, 'acx-concordia-portal-cross', 1, NOW()), ({SPARK_WCID}, 'acx-portal-glow-red-large', 1, NOW()), ({RUIN_WCID}, 'acx-town-network-ruin', 1, NOW());
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES
    ({CROSS_WCID}, 1, 128), ({CROSS_WCID}, 5, 0), ({CROSS_WCID}, 16, 1), ({CROSS_WCID}, 19, 0), ({CROSS_WCID}, 93, 2068),
    ({RUIN_WCID}, 1, 128), ({RUIN_WCID}, 5, 0), ({RUIN_WCID}, 16, 1), ({RUIN_WCID}, 19, 0), ({RUIN_WCID}, 93, 2068);   -- scenery: not usable, walk-through, lit
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES ({CROSS_WCID}, 1, 1), ({RUIN_WCID}, 1, 1), ({RUIN_WCID}, 30017, 1);   -- stuck; AcxExactPlacement
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES ({CROSS_WCID}, 8, 0x0600106B),
    ({RUIN_WCID}, 1, 0x020019E4), ({RUIN_WCID}, 2, 0x09000172), ({RUIN_WCID}, 8, 0x0600106B);   -- retail's Destroyed Portal model
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES ({CROSS_WCID}, 1, 'Concordia Portal'), ({RUIN_WCID}, 1, 'Ruined Town Network Portal');
"""] + [f"INSERT INTO weenie_properties_{t} (object_Id, {c}) SELECT {SPARK_WCID}, {c} FROM weenie_properties_{t} WHERE object_Id = 901005;"
        for t, c in (("int", "type, value"), ("bool", "type, value"), ("d_i_d", "type, value"), ("string", "type, value"),
                     ("texture_map", "`index`, old_Id, new_Id"))] + [
    f"INSERT INTO weenie_properties_float (object_Id, type, value) VALUES ({SPARK_WCID}, 39, {SPARK_SCALE:.3f});"] + look + [""]

per_block, values, tour = {}, [], []
for guid, s in sorted(spots.items(), key=lambda kv: kv[1]["cell"]):
    if s.get("normal"):
        lines.append(f"UPDATE landblock_instance SET weenie_Class_Id = {NORMAL_WCID}, obj_Cell_Id = 0x{s['cell']}, origin_X = {s['x']:g}, origin_Y = {s['y']:g}, origin_Z = {s['z']:g} WHERE guid = 0x{guid};   -- not a town: retail's look, where it was")
        continue
    block = int(s["cell"], 16) >> 16
    n = per_block.get(block, 0)
    per_block[block] = n + 3
    if n + 3 > 0x100:
        sys.exit(f"too many in landblock {block:04X}")
    base = 0x70000000 | (block << 12) | 0xE00
    p = s.get("placed")
    indoors = int(s["cell"], 16) & 0xFFFF >= 0x100   # no obelisk circle indoors (one, in 01AC): stays put, no ruin
    if p:
        cell, x, y, z = p["cell"], p["x"], p["y"], p["z"]
    elif indoors:
        cell, x, y, z = s["cell"], s["x"], s["y"], s["z"]
    else:   # in front; behind the ruin instead when in front crosses into the next landblock (Khayyaban, Freehold)
        for step in (STEP_OUT, -STEP_OUT):
            cell, x, y, z = ground(s, step)
            if int(cell, 16) >> 16 == block:
                break
    w, rz = (p["w"], p["rz"]) if p else (s["w"], s["rz"])
    if int(cell, 16) >> 16 != block:
        sys.exit(f"{town_near(s['cell'])}'s portal lands in the next landblock ({cell[:4]}) both ways: set its spot by hand ('placed')")
    c, sn = math.cos(math.pi / 4), math.sin(math.pi / 4)   # the second frame: turned 90 degrees about z
    lines.append(f"UPDATE landblock_instance SET obj_Cell_Id = 0x{cell}, origin_X = {x:.4f}, origin_Y = {y:.4f}, origin_Z = {z:.3f}, "
                 f"angles_W = {w:g}, angles_X = 0, angles_Y = 0, angles_Z = {rz:g} WHERE guid = 0x{guid};   -- {town_near(s['cell'])}")
    if not indoors:
        values.append(f"    (0x{base + n:08X}, {RUIN_WCID}, 0x{s['cell']}, {s['x']:g}, {s['y']:g}, {s['z']:g}, {s['w']:g}, 0, 0, {s['rz']:g}, 0),   -- {town_near(s['cell'])}: the ruin")
    values.append(f"    (0x{base + n + 1:08X}, {CROSS_WCID}, 0x{cell}, {x:.4f}, {y:.4f}, {z:.3f}, {w * c - rz * sn:.6f}, 0, 0, {w * sn + rz * c:.6f}, 0)")
    values.append(f"    (0x{base + n + 2:08X}, {SPARK_WCID}, 0x{cell}, {x:.4f}, {y:.4f}, {z + SPARK_LIFT:.3f}, {w:g}, 0, 0, {rz:g}, 0)")
    tcell, tx, ty, tz = (cell, x + forward(s)[0] * 2, y + forward(s)[1] * 2, z) if indoors else ground(s, TOUR_STEP)
    tour.append({"name": town_near(s["cell"]), "cell": int(tcell, 16), "x": tx, "y": ty, "z": tz + 0.1})
lines.append("INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES")
lines.append(",\n".join(values) + ";")
open(OUT, "w").write("\n".join(lines) + "\n")
tour.sort(key=lambda t: t["name"])
json.dump(tour, open(TOUR, "w"), indent=1)
print(f"{len(spots)} town portals to Concordia in {len(per_block)} landblocks -> {OUT}; tour of {len(tour)} -> {TOUR}")
