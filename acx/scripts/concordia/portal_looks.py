#!/usr/bin/env python3
"""Project ACX (2026-11-18): Concordia's town portals by tier (JJ: "I want to group portals to towns in a certain way with
different colors to make them easier to find in the portal room"; "The first set the thin ones look fantastic"; "replace
330F with red use orange instead of yellow and lets do it!").

Each town portal in Concordia becomes a Concordia-only copy of its retail portal weenie (the retail ones stand in the town
network too) wearing the Portalling Device's frame (retail 29958: setup 0x02000EAD with its spinning ring, motion table
0x09000120, sounds 0x20000014), and gets a glowing card in the ring: the South Gate's picture card (setup 0x020017B9) at
half size, 2.277 m up (the ring's middle 1.577 m + 0.7 m; JJ set it with /acxportalmix 17 30 0.5 - 0.7), painted with the
tier's thin-tendril picture. Tiers come from acx/Content/invasions/towns.json.

Reads the Preview world DB for the portal list and writes acx/Content/concordia/95-portal-looks.sql, which copies and
places by query (positions follow the earlier Concordia files). Usage: portal_looks.py (Preview must be running)."""
import json, math, os, re, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
OUT = os.path.join(ROOT, "acx", "Content", "concordia", "95-portal-looks.sql")
TOWNS = os.path.join(ROOT, "acx", "Content", "invasions", "towns.json")

# tier -> (color, thin-tendril picture); 6 = the hardest areas (red), no portal assigned yet
TIERS = {1: ("silver", 0x05001E90), 2: ("green", 0x05001E94), 3: ("blue", 0x05001E91), 4: ("purple", 0x05001E8E),
         5: ("orange", 0x05001E8F), 6: ("red", 0x05001E92)}
CARD_WCID = 901000          # + tier - 1
FRAME_WCID = 901010         # one per town portal
CARD_GUID = 0x7018B100      # one per town portal
# JJ: "yeah much bigger. hmm what to do.." (the frame is 4 m wide against a normal portal's 2.83 m, and poked past the
# alcoves): the frame at 70% is a normal portal's width, and the card shrinks and sits lower with it
FRAME_SCALE = 0.7
CARD_SCALE = 0.5   # bigger than the 70% frame's ring, so the color reads across the room (JJ: "that looks a lot better")
CARD_LIFT = 2.29 * FRAME_SCALE   # the spinning ring's middle (parts 2-4 of 0x02000EAD sit at 0, 0, 2.29; JJ's +0.7 put it at 2.277)
CARD_PICTURE = 0x05001BC9   # the South Gate card's own picture
# JJ moved Portal to Al-Arqas out of its alcove to where they all should stand ("they are to far in. I moved this portal out
# to where they all should sit from their original locations"): 1.55 m forward along its facing. Every portal moves that
# far from its original spot (portal_spots.json, so a rerun never moves them twice), into whichever cell has floor there.
STEP_OUT = 1.55
FLOOR_PORTALS = (900727, 900728)   # Concordia Hub (up from the arrival hall), First Floor (back down): see the end
HUB_PICTURE = 0x0500303D            # their hub's nether picture: dark purple-black static
SPOTS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "portal_spots.json")
HAVENCELLS = os.path.expanduser("~/Work/ac/tools/havencells/bin/Release/net10.0/havencells")
DATS = "/mnt/games/AC-Data"


def query(sql):
    out = subprocess.run(["docker", "exec", "-i", "acx-preview-db-1", "sh", "-c", 'exec mysql -uroot -p"$MYSQL_ROOT_PASSWORD" -N ace_world'],
                         input=sql, capture_output=True, text=True)
    if out.returncode: sys.exit(out.stderr)
    out = out.stdout
    return [line.split("\t") for line in out.splitlines() if line]


def key(name):
    return re.sub(r"[^a-z]", "", name.lower())


towns = json.load(open(TOWNS))["towns"]
# the retail portal behind each Concordia portal: itself, or (after a run of this) the one its acx-cc- copy was made from
portals = query(f"""SELECT LPAD(HEX(li.guid),8,'0'), IFNULL(src.class_Id, li.weenie_Class_Id) wcid, n.value FROM landblock_instance li
JOIN weenie w ON w.class_Id = li.weenie_Class_Id AND w.type = 7
LEFT JOIN weenie src ON li.weenie_Class_Id BETWEEN {FRAME_WCID} AND {FRAME_WCID + 89} AND src.class_Name = SUBSTRING(w.class_Name, 8)
LEFT JOIN weenie_properties_string n ON n.object_Id = IFNULL(src.class_Id, li.weenie_Class_Id) AND n.type = 1
WHERE (li.obj_Cell_Id >> 16) = 0x018A AND (li.weenie_Class_Id < 900000 OR li.weenie_Class_Id BETWEEN {FRAME_WCID} AND {FRAME_WCID + 89}) ORDER BY n.value;""")
tables = {t: cols.split(",") for t, cols in query("""SELECT TABLE_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY ORDINAL_POSITION) FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'ace_world' AND TABLE_NAME LIKE 'weenie_properties%' GROUP BY TABLE_NAME;""")}

def short(name):
    return key(re.sub(r"^Portal to (the )?|( Portal| Outpost| Castle)$", "", name))


exact = {short(name): t for _, _, name in portals for t in towns if key(t["name"]) == short(name)}
matched, skipped = [], []
for guid, wcid, name in portals:
    town = exact.get(short(name))
    if town is None:   # "Oolutanga's Refuge", "Base of the Timaru Plateau": the town named inside it that no portal has exactly
        inside = [t for t in towns if key(t["name"]) in key(name) and t not in exact.values()]
        town = max(inside, key=lambda t: len(t["name"])) if inside else None
    (matched if town else skipped).append((guid, int(wcid), name, town))
by_town = {}
for m in matched:
    by_town.setdefault(m[3]["name"], []).append(m[2])
twice = {t: n for t, n in by_town.items() if len(n) > 1}
missing = [t["name"] for t in towns if t["name"] not in by_town]
if twice or missing:
    sys.exit(f"town matching is off: twice {twice}, missing {missing}")
emotes = query("SELECT COUNT(*) FROM weenie_properties_emote WHERE object_Id IN (%s);" % ",".join(str(m[1]) for m in matched))
if emotes[0][0] != "0":
    sys.exit("a town portal has emotes; the copy doesn't handle them")

spots = json.load(open(SPOTS))["spots"]


def stepped_out(guid):
    """the portal's spot moved STEP_OUT forward (AC: heading from the z rotation, forward = (-sin, cos)), with the cell
    whose floor is under it"""
    s = spots[guid]
    if "placed" in s:   # set by hand in game
        return s["placed"]["cell"], s["placed"]["x"], s["placed"]["y"], s["placed"]
    heading = math.atan2(2 * s["w"] * s["rz"], 1 - 2 * s["rz"] * s["rz"])
    step = s.get("step", STEP_OUT) * s["out"]   # toward the room: some portals face it, some face their alcove's back wall
    x, y = s["x"] - math.sin(heading) * step, s["y"] + math.cos(heading) * step
    found = subprocess.run([HAVENCELLS, DATS, "findcell", s["cell"], f"{x:.4f}", f"{y:.4f}", f"{s['z']:.3f}"], capture_output=True, text=True).stdout.split()
    if len(found) < 2 or found[0] not in ("same", "cell"):
        sys.exit(f"no floor {STEP_OUT} m in front of {guid} ({s})")
    return found[1], x, y, s


copy_tables = [t for t in sorted(tables) if t not in ("weenie_properties_emote", "weenie_properties_emote_action")]
lines = [f"""-- Project ACX (2026-11-18): Concordia's town portals by tier. GENERATED by acx/scripts/concordia/portal_looks.py; see there.
-- Tiers (towns.json): 1 silver, 2 green, 3 blue, 4 purple, 5 orange thin tendrils; red (901005) for the hardest areas, unused yet.
--   {CARD_WCID}-{CARD_WCID + 5}  the glowing cards, one per color
--   {FRAME_WCID}-{FRAME_WCID + len(matched) - 1}  the town portals' Concordia copies, in the Portalling Device's frame
-- Runs after 10/50/80 (which place the retail portals). Re-runnable. Not changed: {", ".join(s[2] for s in skipped)}.
DELETE FROM weenie WHERE class_Id BETWEEN {CARD_WCID} AND {CARD_WCID + 99};
DELETE FROM landblock_instance WHERE guid BETWEEN 0x{CARD_GUID:08X} AND 0x{CARD_GUID + 0xFF:08X};
""", "INSERT INTO weenie (class_Id, class_Name, type, last_Modified) VALUES"]
cards = []
for tier, (color, _) in TIERS.items():
    cards.append(f"    ({CARD_WCID + tier - 1}, 'acx-portal-glow-{color}', 1, NOW())")
lines.append(",\n".join(cards) + ";")
ids = [CARD_WCID + t - 1 for t in TIERS]
lines.append("INSERT INTO weenie_properties_int (object_Id, type, value) VALUES\n" + ",\n".join(
    f"    ({i}, 1, 128), ({i}, 5, 0), ({i}, 16, 1), ({i}, 19, 0), ({i}, 93, 2068)" for i in ids)
    + ";   -- misc, no burden, not usable, no value; ethereal, no collisions, lit (the South Gate's 2060 + ignore collisions)")
lines.append("INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES\n" + ",\n".join(
    f"    ({i}, 1, 1), ({i}, 30002, 1), ({i}, 30017, 1)" for i in ids) + ";   -- stuck; AcxCustomAppearance (sends the texture map); AcxExactPlacement")
lines.append("INSERT INTO weenie_properties_float (object_Id, type, value) VALUES\n" + ",\n".join(
    f"    ({i}, 39, {CARD_SCALE:g})" for i in ids) + ";   -- size, with the frame's")
lines.append("INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES\n" + ",\n".join(
    f"    ({i}, 1, 0x020017B9), ({i}, 8, 0x0600106B)" for i in ids) + ";   -- the South Gate's card; portal icon")
lines.append("INSERT INTO weenie_properties_string (object_Id, type, value) VALUES\n" + ",\n".join(
    f"    ({i}, 1, 'Portal Light')" for i in ids) + ";")
lines.append("INSERT INTO weenie_properties_texture_map (object_Id, `index`, old_Id, new_Id) VALUES\n" + ",\n".join(
    f"    ({CARD_WCID + t - 1}, {part}, 0x{CARD_PICTURE:08X}, 0x{pic:08X})" for t, (_, pic) in TIERS.items() for part in (0, 1))
    + ";   -- both faces of the card")
lines.append("")

# Which town stands in which spot (JJ 2026-11-18: "come up with a better system to organize the portals in concordia since we
# have them all placed where they need to be"; tried the floors by difficulty, then: "No lets just put them in a proper
# alphabetical order. That will make finding them a lot easier. where you have beach fort will start the A's", "Whole room
# A-Z"): all 52 towns A to Z, starting at START_SPOT on the top floor and going clockwise around it, then on around the
# middle floor (from the spot nearest the same direction), then the bottom. The spots don't move; only where each leads.
START_SPOT = "7018A0B4"   # top floor: where the by-floor layout had Beach Fort when JJ said this (7018AF18, Beach Fort's
                          # retail spot, is on the bottom floor)

copies = {}
for n, (guid, wcid, name, town) in enumerate(sorted(matched, key=lambda m: (m[3]["tier"], m[3]["name"]))):
    copy = FRAME_WCID + n
    copies[guid] = copy
    lines.append(f"-- {name}: {town['name']}, tier {town['tier']} ({TIERS[town['tier']][0]})")
    lines.append(f"INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT {copy}, CONCAT('acx-cc-', class_Name), type, NOW() FROM weenie WHERE class_Id = {wcid};")
    for t in copy_tables:
        cols = [c for c in tables[t] if c not in ("id", "object_Id")]
        quoted = ", ".join(f"`{c}`" for c in cols)
        lines.append(f"INSERT INTO {t} (object_Id, {quoted}) SELECT {copy}, {quoted} FROM {t} WHERE object_Id = {wcid};")
    lines.append(f"DELETE FROM weenie_properties_d_i_d WHERE object_Id = {copy} AND type IN (1, 2, 3);")
    lines.append(f"INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES ({copy}, 1, 0x02000EAD), ({copy}, 2, 0x09000120), ({copy}, 3, 0x20000014);   -- the Portalling Device")
    lines.append(f"DELETE FROM weenie_properties_float WHERE object_Id = {copy} AND type = 39;")
    lines.append(f"INSERT INTO weenie_properties_float (object_Id, type, value) VALUES ({copy}, 39, {FRAME_SCALE:g});   -- a normal portal's width")
lines.append("")

# the spots: where each stands, which floor, which wall, and its place along it
slots = []
for guid, wcid, name, town in matched:
    cell, x, y, s = stepped_out(guid)
    o = spots[guid]
    heading = math.atan2(2 * o["w"] * o["rz"], 1 - 2 * o["rz"] * o["rz"])
    ox, oy = -math.sin(heading) * o["out"], math.cos(heading) * o["out"]   # toward the room
    floor = round(o["z"] / 6) * 6
    slots.append({"guid": guid, "cell": cell, "x": x, "y": y, "s": s, "floor": floor, "out": (ox, oy)})
cx = sum(sl["x"] for sl in slots) / len(slots)
cy = sum(sl["y"] for sl in slots) / len(slots)
for sl in slots:
    ox, oy = sl["out"]
    sl["wall"] = (round(math.degrees(math.atan2(oy, ox)) / 45) * 45 % 360, round((sl["x"] * ox + sl["y"] * oy) / 3))
    sl["around"] = math.atan2(sl["y"] - cy, sl["x"] - cx)               # where the wall is around the room
    sl["along"] = sl["x"] * oy - sl["y"] * ox                            # left to right, facing the wall (looking against out)

def alpha(m):
    return re.sub(r"[^a-z]", "", m[3]["name"].lower())


def floor_order(floor, start):
    """the floor's spots clockwise around the room, wall by wall, left to right along each, beginning at its spot nearest
    start (x, y)"""
    here = [sl for sl in slots if sl["floor"] == floor]
    walls = {}
    for sl in here:
        walls.setdefault(sl["wall"], []).append(sl)
    ordered = []
    for wall in sorted(walls.values(), key=lambda w: -sum(sl["around"] for sl in w) / len(w)):
        ordered += sorted(wall, key=lambda sl: sl["along"])
    ordered.reverse()   # JJ: "thats ccw from the way I see it. reverse the direction"
    first = min(range(len(ordered)), key=lambda i: (ordered[i]["x"] - start["x"]) ** 2 + (ordered[i]["y"] - start["y"]) ** 2)
    return ordered[first:] + ordered[:first]


start = next(sl for sl in slots if sl["guid"] == START_SPOT)
order = []
for floor in sorted({sl["floor"] for sl in slots}, reverse=True):   # top, middle, bottom
    order += floor_order(floor, start)
if order[0]["guid"] != START_SPOT:
    sys.exit(f"the route starts at {order[0]['guid']}, not {START_SPOT} (is it on the top floor?)")
towns_az = sorted(matched, key=alpha)
assigned = {sl["guid"]: (sl, m) for sl, m in zip(order, towns_az)}
if len(assigned) != len(matched):
    sys.exit(f"{len(assigned)} of {len(matched)} spots assigned")

for n, (guid, (sl, m)) in enumerate(sorted(assigned.items())):
    town = m[3]
    s = sl["s"]
    angles = f"{s['w']:g}, {s['rx']:g}, {s['ry']:g}, {s['rz']:g}"
    lines.append(f"UPDATE landblock_instance SET weenie_Class_Id = {copies[m[0]]}, obj_Cell_Id = 0x{sl['cell']}, origin_X = {sl['x']:.4f}, origin_Y = {sl['y']:.4f}, origin_Z = {s['z']:g}, "
                 f"angles_W = {s['w']:g}, angles_X = {s['rx']:g}, angles_Y = {s['ry']:g}, angles_Z = {s['rz']:g} WHERE guid = 0x{guid};   -- {town['name']} (tier {town['tier']}), floor {sl['floor']}")
    lines.append(f"INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) VALUES "
                 f"(0x{CARD_GUID + n:08X}, {CARD_WCID + town['tier'] - 1}, 0x{sl['cell']}, {sl['x']:.4f}, {sl['y']:.4f}, {s['z'] + CARD_LIFT:.3f}, {angles}, 0);")
lines.append("")

# The portals between Concordia's floors (JJ: "For the portals between floors can we do parts 0, 1, 3 and 4, plus red sparkly
# center?", "could swap it for nether particles of some sort"): the same frame, the hub (part 2, 0x01002D45, which wears the
# see-through swirl 0x05001773) repainted with a nether picture, and the red spark. They're ACX's own portals
# (30-hub-portal.sql), changed in place, standing where JJ put them.
lines.append("-- the portals between floors: the frame, a nether hub, the red spark")
for i, wcid in enumerate(FLOOR_PORTALS):
    lines.append(f"DELETE FROM weenie_properties_d_i_d WHERE object_Id = {wcid} AND type IN (1, 2, 3);")
    lines.append(f"INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES ({wcid}, 1, 0x02000EAD), ({wcid}, 2, 0x09000120), ({wcid}, 3, 0x20000014);")
    lines.append(f"DELETE FROM weenie_properties_float WHERE object_Id = {wcid} AND type = 39;")
    lines.append(f"INSERT INTO weenie_properties_float (object_Id, type, value) VALUES ({wcid}, 39, {FRAME_SCALE:g});")
    lines.append(f"DELETE FROM weenie_properties_bool WHERE object_Id = {wcid} AND type = 30002;")
    lines.append(f"INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES ({wcid}, 30002, 1);   -- AcxCustomAppearance")
    lines.append(f"DELETE FROM weenie_properties_texture_map WHERE object_Id = {wcid};")
    lines.append(f"INSERT INTO weenie_properties_texture_map (object_Id, `index`, old_Id, new_Id) VALUES ({wcid}, 2, 0x05001773, 0x{HUB_PICTURE:08X});")
    lines.append(f"INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child) "
                 f"SELECT 0x{CARD_GUID + 0xF0 + i:08X}, {CARD_WCID + 5}, obj_Cell_Id, origin_X, origin_Y, origin_Z + {CARD_LIFT:.3f}, angles_W, angles_X, angles_Y, angles_Z, 0 "
                 f"FROM landblock_instance WHERE weenie_Class_Id = {wcid} LIMIT 1;")
lines.append("")

open(OUT, "w").write("\n".join(lines))
print(f"{len(matched)} town portals -> {OUT}; not changed: {[s[2] for s in skipped]}")
for tier in sorted(TIERS):
    names = sorted(m[3]["name"] for m in matched if m[3]["tier"] == tier)
    if names:
        print(f"  tier {tier} {TIERS[tier][0]} ({len(names)}): {', '.join(names)}")
