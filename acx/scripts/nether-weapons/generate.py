"""Project ACX: generate the nether weapon loot content (SQL) and the loot-table map (C#) from weapons.json."""
import json, re

rows = json.load(open('weapons.json'))
src = '/home/jj/Work/ac/project-acx/Source/ACE.Server/Factories'
enum_by_id = {}
for m in re.finditer(r'^\s*(\w+)\s*=\s*(\d+),', open(src + '/Enum/WeenieClassName.cs').read(), re.M):
    enum_by_id.setdefault(int(m.group(2)), m.group(1))

by_name = {}
for r in rows:
    by_name.setdefault(r['name'], r)

PREFIXES = ['Acid', 'Lightning', 'Flaming', 'Fire', 'Electric', 'Frost', 'Slashing', 'Piercing', 'Blunt']
families = []
for frost in sorted((r for r in rows if r['dmg'] == 8), key=lambda r: r['name']):
    base = frost['name'][len('Frost '):]
    members = [by_name[n] for n in [base] + [f'{p} {base}' for p in PREFIXES] if n in by_name]
    source = by_name.get(base) or by_name.get(f'Slashing {base}') or frost
    families.append(dict(base=base, frost=frost, source=source, members=members))

FIRST = 900100
for i, f in enumerate(families):
    f['wcid'] = FIRST + i
    f['cls'] = 'acx-nether-' + re.sub(r'[^a-z0-9]+', '-', f['base'].lower()).strip('-')
    f['name'] = 'Nether ' + f['base']

# ---- SQL ----
sql = []
sql.append(f"""-- Project ACX: nether versions of every loot weapon that comes in elements ({len(families)} weapons, wcids {FIRST}-{FIRST + len(families) - 1}).
-- Generated from the weapon loot tables by acx/scripts/nether-weapons/; the server rolls them in
-- AcxNetherWeaponWcids.cs. Each copies the plain weapon (its look, stats and mutation data) or, for the bows,
-- crossbows and slingshot that have no plain version, the slashing one; then deals nether damage, shows the
-- nether icon border and is worth what the frost version is. Re-runnable.

DELETE FROM weenie WHERE class_Id BETWEEN {FIRST} AND {FIRST + 99};

DROP TEMPORARY TABLE IF EXISTS acx_nether;
CREATE TEMPORARY TABLE acx_nether (wcid INT UNSIGNED, src INT UNSIGNED, frost INT UNSIGNED, class_name VARCHAR(100), name VARCHAR(100));
INSERT INTO acx_nether VALUES""")
sql.append(',\n'.join(f"    ({f['wcid']}, {f['source']['wcid']}, {f['frost']['wcid']}, '{f['cls']}', '{f['name']}')" for f in families) + ';')
sql.append("""
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
SELECT n.wcid, n.class_name, w.type, NOW() FROM acx_nether n JOIN weenie w ON w.class_Id = n.src;
""")
for t in ['int', 'int64', 'float', 'bool', 'string', 'd_i_d', 'i_i_d']:
    sql.append(f"INSERT INTO weenie_properties_{t} (object_Id, type, value) SELECT n.wcid, p.type, p.value FROM acx_nether n JOIN weenie_properties_{t} p ON p.object_Id = n.src;")
sql.append("INSERT INTO weenie_properties_spell_book (object_Id, spell, probability) SELECT n.wcid, p.spell, p.probability FROM acx_nether n JOIN weenie_properties_spell_book p ON p.object_Id = n.src;")
sql.append("""
-- name
UPDATE weenie_properties_string p JOIN acx_nether n ON p.object_Id = n.wcid SET p.value = n.name WHERE p.type = 1;

-- nether damage (DamageType 45 = 1024) and the nether icon border (UiEffects 18 = 0x1000)
DELETE p FROM weenie_properties_int p JOIN acx_nether n ON p.object_Id = n.wcid WHERE p.type IN (18, 45);
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT wcid, 45, 1024 FROM acx_nether;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT wcid, 18, 0x1000 FROM acx_nether;

-- worth what the frost version is (Value 19)
UPDATE weenie_properties_int p JOIN acx_nether n ON p.object_Id = n.wcid JOIN weenie_properties_int f ON f.object_Id = n.frost AND f.type = 19
SET p.value = f.value WHERE p.type = 19;

DROP TEMPORARY TABLE acx_nether;
""")
open('10-nether-weapon-weenies.sql', 'w').write('\n'.join(sql))

# ---- C# ----
cs = []
cs.append("""using System.Collections.Generic;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Server.Managers;

using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Factories.Tables.Wcids
{
    /// <summary>
    /// Project ACX: nether versions of every loot weapon that comes in elements (acx/Content/nether-weapons).
    /// Any weapon of such a family, plain or elemental, turns into its nether version with chance acx_nether_weapon_chance,
    /// at loot tier acx_nether_weapon_min_tier and up. Generated from the weapon loot tables by acx/scripts/nether-weapons/.
    /// </summary>
    public static class AcxNetherWeaponWcids
    {
        private static readonly Dictionary<WeenieClassName, uint> netherVersions = new Dictionary<WeenieClassName, uint>()
        {""")
for f in families:
    cs.append(f"            // {f['name']}")
    for m in sorted(f['members'], key=lambda r: r['name']):
        cs.append(f"            {{ WeenieClassName.{enum_by_id[m['wcid']]}, {f['wcid']} }},")
cs.append("""        };

        public static WeenieClassName TryRollNether(TreasureDeath treasureDeath, WeenieClassName wcid)
        {
            if (treasureDeath.Tier < PropertyManager.GetLong("acx_nether_weapon_min_tier").Item || !netherVersions.TryGetValue(wcid, out var netherWcid))
                return wcid;

            if (ThreadSafeRandom.Next(0.0f, 1.0f) >= PropertyManager.GetDouble("acx_nether_weapon_chance").Item)
                return wcid;

            return (WeenieClassName)netherWcid;
        }
    }
}
""")
open('AcxNetherWeaponWcids.cs', 'w').write('\n'.join(cs))

print(len(families), 'families,', sum(len(f['members']) for f in families), 'member wcids')
for f in families:
    if f['source'] is f['frost'] or not by_name.get(f['base']):
        print('  source', f['source']['name'], 'for', f['name'])
