#!/usr/bin/env python3
"""Project ACX: writes acx/Content/thrown/10-eternal-thrown.sql, the Eternal thrown weapons (2026-11-14).

    python3 -I acx/scripts/thrown/generate.py

One weapon per retail thrown family, plain, acid / fire / frost / electric, and nether. The plain ones copy the retail
vendor weapon; the elemental ones take the model and icon of retail's old elemental thrown loot (Flaming Javelin,
Frost Throwing Axe, ...) so they glow; the nether ones look plain and get ACX's nether aura from their damage type
(LootGenerationFactory_AcxLooks.cs). The server side is Factories/Tables/Wcids/Weapons/AcxThrownWcids.cs.
"""
import pathlib

OUT = pathlib.Path(__file__).resolve().parents[2] / "Content/thrown/10-eternal-thrown.sql"

# key, name, retail vendor weenie, own damage (12 for all, an Atlatl Dart's; loot rolls its own), TsysMutationData, physical
# damage type, its icon glow, and the retail elemental weenie whose model and icon each element borrows (acid, fire, frost,
# electric)
FAMILIES = [
    ("dart", "Throwing Dart", 316, 12, 0x11080702, 2, 2048, (3786, 3788, 3789, 3787)),
    ("shouken", "Shouken", 343, 12, 0x11080702, 2, 2048, (3861, 3863, 3864, 3862)),
    ("dagger", "Throwing Dagger", 315, 12, 0x11080702, 2, 2048, (3782, 3784, 3785, 3783)),
    ("club", "Throwing Club", 310, 12, 0x1108070C, 4, 512, (3770, 3772, 3773, 3771)),
    ("djarid", "Djarid", 317, 12, 0x1108070A, 2, 2048, (3790, 3792, 3793, 3791)),
    ("javelin", "Javelin", 320, 12, 0x1108070A, 2, 2048, (3798, 3800, 3801, 3799)),
    ("axe", "Throwing Axe", 304, 12, 0x1108070A, 1, 1024, (3758, 3760, 3761, 3759)),
]
ELEMENTS = [("acid", "Acid", 32, 256), ("fire", "Fire", 16, 32), ("frost", "Frost", 8, 128), ("electric", "Electric", 64, 64)]
FIRST_PLAIN, FIRST_ELEMENTAL, FIRST_NETHER = 900810, 900817, 900845
DAMAGE_MOD = 1.7   # the unrolled weapon's damage multiplier: a vendor Atlatl's

rows = []   # wcid, source, look, class name, name, damage type, ui effects, damage, tsys
for i, (key, name, src, dmg, tsys, dtype, ui, looks) in enumerate(FAMILIES):
    rows.append((FIRST_PLAIN + i, src, src, f"acx-eternal-{key}", name, dtype, ui, dmg, tsys))
for i, (key, name, src, dmg, tsys, dtype, ui, looks) in enumerate(FAMILIES):
    for e, (ekey, ename, edtype, eui) in enumerate(ELEMENTS):
        rows.append((FIRST_ELEMENTAL + i * len(ELEMENTS) + e, src, looks[e], f"acx-eternal-{key}-{ekey}", f"{ename} {name}", edtype, eui, dmg, tsys))
for i, (key, name, src, dmg, tsys, dtype, ui, looks) in enumerate(FAMILIES):
    rows.append((FIRST_NETHER + i, src, src, f"acx-eternal-{key}-nether", f"Nether {name}", 1024, 0x1000, dmg, tsys))
last = rows[-1][0]

values = ",\n".join(f"    ({w}, {s}, {l}, '{c}', '{n}', {d}, {u}, {dm}, 0x{t:08X})" for w, s, l, c, n, d, u, dm, t in rows) + ";"
OUT.write_text(f"""-- Project ACX (2026-11-14): Eternal thrown weapons (JJ: "I want to make thrown weapon appealing more so then the others
-- since nobody has played it since it was made"; review ~/Work/ac/research/thrown-weapons-2026-11-14.md). One weapon per
-- retail thrown family (Throwing Dart, Shouken, Throwing Dagger, Throwing Club, Djarid, Javelin, Throwing Axe), plain,
-- acid / fire / frost / electric, or nether, each a single item with UnlimitedUse: thrown forever, never runs out, no
-- ammo, and a thrown weapon leaves the off hand free for a shield (ACE equip rules; launchers don't). Missile Weapons
-- skill, the family's speed; inscribable like retail's Vortex Thorn. Named like the retail weapon ("Javelin", "Fire
-- Javelin"): JJ first had "Eternal" in front, then: "Lets also remove the eternal from the names. Since they are all
-- eternal now".
-- Damage: a thrown weapon is a launcher and its own ammunition (JJ 2026-11-15: "it is ammo. Just eternal ammo"; "how can
-- we get a modifier on our eternal ammo so that it behaves and is treated similar to bows for damage"). Its Damage is the
-- ammunition's part and its DamageMod the launcher's: a hit is (Damage + Blood Drinker and the like) x DamageMod, an
-- atlatl's with its dart (Factories/Tables/Wcids/Weapons/AcxThrownWcids.cs). The weenie, which is what vendors sell,
-- is a vendor Atlatl with an Atlatl Dart: Damage 12 for every family (retail: Dart and Shouken 6, Dagger 8, Club,
-- Djarid and Javelin 10, Axe 12; families differ by speed), DamageMod {DAMAGE_MOD} (+{round((DAMAGE_MOD - 1) * 100)}%). Loot rolls both (6% of weapon drops: the
-- multiplier like an atlatl's, the damage the tier's dart with a range of its own; LootGenerationFactory_Missile.cs), and
-- Mahogany raises the multiplier (RecipeManager_New.cs). The examine window shows both (AppraiseInfo.cs).
-- Looks (JJ: "the items labled like fire/acid/frost they don't have their colors/animations"): the elemental ones take
-- the model and icon of retail's old elemental thrown loot (Flaming Javelin, Frost Throwing Axe, Acid Shouken, ...);
-- the nether ones (JJ: "do they come in nether also?") look plain and get ACX's nether aura from their damage type; a
-- drop turns nether like any elemental weapon (AcxNetherWeaponWcids).
--   {FIRST_PLAIN}-{FIRST_ELEMENTAL - 1} plain; {FIRST_ELEMENTAL}-{FIRST_NETHER - 1} each family's acid, fire, frost and electric version (family by family);
--   {FIRST_NETHER}-{last} each family's nether version.
-- Generated by acx/scripts/thrown/generate.py. Re-runnable.
DELETE FROM weenie WHERE class_Id BETWEEN {FIRST_PLAIN} AND {last};
DROP TEMPORARY TABLE IF EXISTS acx_eternal;
CREATE TEMPORARY TABLE acx_eternal (class_Id INT UNSIGNED PRIMARY KEY, source INT UNSIGNED, look INT UNSIGNED, class_Name VARCHAR(100), name VARCHAR(100), damage_type INT, ui_effects INT, damage INT, tsys INT UNSIGNED);
INSERT INTO acx_eternal VALUES
{values}
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT e.class_Id, e.class_Name, w.type, NOW() FROM acx_eternal e JOIN weenie w ON w.class_Id = e.source;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT e.class_Id, p.type, p.value FROM acx_eternal e JOIN weenie_properties_int p ON p.object_Id = e.source WHERE p.type NOT IN (11, 12, 15, 18, 19, 44, 45, 48, 169, 353);   -- not 15 StackUnitValue: a stackable's Value comes from it (Stackable.cs)
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT e.class_Id, p.type, p.value FROM acx_eternal e JOIN weenie_properties_int64 p ON p.object_Id = e.source;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT e.class_Id, p.type, p.value FROM acx_eternal e JOIN weenie_properties_float p ON p.object_Id = e.source WHERE p.type NOT IN (26, 63);
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT e.class_Id, p.type, p.value FROM acx_eternal e JOIN weenie_properties_bool p ON p.object_Id = e.source WHERE p.type NOT IN (22, 63);
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT e.class_Id, p.type, p.value FROM acx_eternal e JOIN weenie_properties_string p ON p.object_Id = e.source WHERE p.type NOT IN (1, 20);   -- not its name or plural
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT e.class_Id, p.type, p.value FROM acx_eternal e JOIN weenie_properties_d_i_d p ON p.object_Id = e.source WHERE p.type NOT IN (1, 8);
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT e.class_Id, p.type, p.value FROM acx_eternal e JOIN weenie_properties_d_i_d p ON p.object_Id = e.look WHERE p.type IN (1, 8);   -- model and icon: the look's
INSERT INTO weenie_properties_palette (object_Id, sub_Palette_Id, offset, length) SELECT e.class_Id, p.sub_Palette_Id, p.offset, p.length FROM acx_eternal e JOIN weenie_properties_palette p ON p.object_Id = e.look;
INSERT INTO weenie_properties_texture_map (object_Id, `index`, old_Id, new_Id) SELECT e.class_Id, p.`index`, p.old_Id, p.new_Id FROM acx_eternal e JOIN weenie_properties_texture_map p ON p.object_Id = e.look;
INSERT INTO weenie_properties_anim_part (object_Id, `index`, animation_Id) SELECT e.class_Id, p.`index`, p.animation_Id FROM acx_eternal e JOIN weenie_properties_anim_part p ON p.object_Id = e.look;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT class_Id, 1, name FROM acx_eternal;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 11, 1 FROM acx_eternal;               -- MaxStackSize: one item, not a stack
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 12, 1 FROM acx_eternal;               -- StackSize
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 19, 300 FROM acx_eternal;              -- Value: a vendor's eternal starter weapon costs a few hundred
                                                                                                                 -- pyreals, not retail's price for one dart (JJ 2026-11-15); loot builds its value on it
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 18, ui_effects FROM acx_eternal;      -- UiEffects: the icon glow of its damage type, like atlatls (nether: the nether border)
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 44, damage FROM acx_eternal;          -- Damage: its own, the ammunition's part
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT class_Id, 63, {DAMAGE_MOD} FROM acx_eternal;          -- DamageMod: the launcher's part
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 45, damage_type FROM acx_eternal;     -- DamageType
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 48, 47 FROM acx_eternal;              -- WeaponSkill: Missile Weapons (retail thrown still names the retired Thrown Weapon)
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 353, 10 FROM acx_eternal;             -- WeaponType: Thrown
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT class_Id, 169, tsys FROM acx_eternal;          -- TsysMutationData: what loot rolls on it (retail thrown never dropped, so has none):
                                                                                                                 -- missile spells, atlatl gems and colours, the material of its retail cousin (dagger 02, club 0C, atlatl/axe 0A)
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT class_Id, 26, 20 FROM acx_eternal;            -- MaximumVelocity: the server's default for thrown (about 41 m), written out so the
                                                                                                                 -- client's appraisal shows the range (without it, "Range: 1 yds"); JJ: "Keep the range reduced like it is"
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT class_Id, 63, 1 FROM acx_eternal;              -- UnlimitedUse: never used up
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT class_Id, 22, 1 FROM acx_eternal;              -- Inscribable
DROP TEMPORARY TABLE acx_eternal;
""")
print(len(rows), "weapons ->", OUT)
