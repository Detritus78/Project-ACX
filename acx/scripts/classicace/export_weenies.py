#!/usr/bin/env python3
"""Project ACX: copies weenies from ClassicACE's world database into an ACX content SQL file.

ClassicACE's world dump (classicace-ref/Database/ACE-World-...-CustomDM-v3.06.sql.7z) loads into its own schema,
ace_world_customdm, on Preview's MySQL. This writes plain INSERTs (no auto-increment ids, class names prefixed
acx-classicace-) that apply to any ACX world database.
Run: python3 acx/scripts/classicace/export_weenies.py <out.sql> <header comment file> <wcid,wcid,...> [old=new loot profile ...]
"""
import subprocess, sys

out_path, header_path, ids = sys.argv[1], sys.argv[2], sys.argv[3]
remap = dict(a.split("=") for a in sys.argv[4:])

def query(sql):
    r = subprocess.run(["docker", "exec", "-i", "acx-preview-db-1", "sh", "-c", 'exec mysql -B -uroot -p"$MYSQL_ROOT_PASSWORD" ace_world_customdm 2>/dev/null'],
                       input=sql, capture_output=True, text=True, check=True)
    lines = r.stdout.rstrip("\n").split("\n")
    if not lines or not lines[0]:
        return [], []
    return lines[0].split("\t"), [l.split("\t") for l in lines[1:]]

def lit(v):
    if v == "NULL":
        return "NULL"
    try:
        float(v)
        return v
    except ValueError:
        return "'" + v.replace("\\", "\\\\").replace("'", "''") + "'"

out = [open(header_path).read(), f"DELETE FROM weenie WHERE class_Id IN ({ids});\n"]
cols, rows = query(f"SELECT class_Id, class_Name, type FROM weenie WHERE class_Id IN ({ids}) ORDER BY class_Id;")
out.append("INSERT INTO weenie (class_Id, class_Name, type, last_Modified) VALUES\n" +
           ",\n".join(f"    ({r[0]}, {lit('acx-classicace-' + r[1])}, {r[2]}, NOW())" for r in rows) + ";\n")

for table in ["weenie_properties_int", "weenie_properties_int64", "weenie_properties_float", "weenie_properties_bool", "weenie_properties_string",
              "weenie_properties_d_i_d", "weenie_properties_i_i_d", "weenie_properties_generator", "weenie_properties_create_list",
              "weenie_properties_palette", "weenie_properties_texture_map", "weenie_properties_anim_part", "weenie_properties_spell_book",
              "weenie_properties_book", "weenie_properties_attribute", "weenie_properties_attribute_2nd", "weenie_properties_skill", "weenie_properties_body_part"]:
    select = "object_Id, type, value+0 AS value" if table == "weenie_properties_bool" else "*"
    cols, rows = query(f"SELECT {select} FROM {table} WHERE object_Id IN ({ids}) ORDER BY object_Id;")
    if not rows:
        continue
    keep = [i for i, c in enumerate(cols) if c != "id"]
    if table == "weenie_properties_generator":
        wi = cols.index("weenie_Class_Id")
        for r in rows:
            r[wi] = remap.get(r[wi], r[wi])
    out.append(f"INSERT INTO {table} ({', '.join('`' + cols[i] + '`' for i in keep)}) VALUES\n" +
               ",\n".join("    (" + ", ".join(lit(r[i]) for i in keep) + ")" for r in rows) + ";\n")

open(out_path, "w").write("\n".join(out))
print("written", sum(o.count("\n    (") for o in out), "rows to", out_path)
