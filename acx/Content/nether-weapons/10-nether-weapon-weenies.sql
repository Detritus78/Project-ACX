-- Project ACX: nether versions of every loot weapon that comes in elements (84 weapons, wcids 900100-900183).
-- Generated from the weapon loot tables by acx/scripts/nether-weapons/; the server rolls them in
-- AcxNetherWeaponWcids.cs. Each copies the plain weapon (its look, stats and mutation data) or, for the bows,
-- crossbows and slingshot that have no plain version, the slashing one; then deals nether damage, shows the
-- nether icon border and is worth what the frost version is. Re-runnable.

DELETE FROM weenie WHERE class_Id BETWEEN 900100 AND 900199;

DROP TEMPORARY TABLE IF EXISTS acx_nether;
CREATE TEMPORARY TABLE acx_nether (wcid INT UNSIGNED, src INT UNSIGNED, frost INT UNSIGNED, class_name VARCHAR(100), name VARCHAR(100));
INSERT INTO acx_nether VALUES
    (900100, 41036, 41040, 'acx-nether-assagai', 'Nether Assagai'),
    (900101, 12463, 29256, 'acx-nether-atlatl', 'Nether Atlatl'),
    (900102, 30606, 30609, 'acx-nether-bastone', 'Nether Bastone'),
    (900103, 301, 3753, 'acx-nether-battle-axe', 'Nether Battle Axe'),
    (900104, 31774, 31773, 'acx-nether-board-with-nail', 'Nether Board with Nail'),
    (900105, 29244, 29242, 'acx-nether-bow', 'Nether Bow'),
    (900106, 350, 3880, 'acx-nether-broad-sword', 'Nether Broad Sword'),
    (900107, 308, 3765, 'acx-nether-budiaq', 'Nether Budiaq'),
    (900108, 4190, 4193, 'acx-nether-cestus', 'Nether Cestus'),
    (900109, 31784, 31783, 'acx-nether-claw', 'Nether Claw'),
    (900110, 309, 3769, 'acx-nether-club', 'Nether Club'),
    (900111, 31798, 31803, 'acx-nether-compound-bow', 'Nether Compound Bow'),
    (900112, 31805, 31810, 'acx-nether-compound-crossbow', 'Nether Compound Crossbow'),
    (900113, 40818, 40822, 'acx-nether-corsesca', 'Nether Corsesca'),
    (900114, 29251, 29249, 'acx-nether-crossbow', 'Nether Crossbow'),
    (900115, 313, 3777, 'acx-nether-dabus', 'Nether Dabus'),
    (900116, 314, 3781, 'acx-nether-dagger', 'Nether Dagger'),
    (900117, 31759, 31758, 'acx-nether-dericost-blade', 'Nether Dericost Blade'),
    (900118, 22440, 22444, 'acx-nether-dirk', 'Nether Dirk'),
    (900119, 30561, 30565, 'acx-nether-dolabra', 'Nether Dolabra'),
    (900120, 45099, 45103, 'acx-nether-epee', 'Nether Epee'),
    (900121, 30576, 30578, 'acx-nether-flamberge', 'Nether Flamberge'),
    (900122, 30586, 30590, 'acx-nether-flanged-mace', 'Nether Flanged Mace'),
    (900123, 41057, 41061, 'acx-nether-great-star-mace', 'Nether Great Star Mace'),
    (900124, 41052, 41056, 'acx-nether-greataxe', 'Nether Greataxe'),
    (900125, 303, 3757, 'acx-nether-hand-axe', 'Nether Hand Axe'),
    (900126, 45118, 45122, 'acx-nether-hand-wraps', 'Nether Hand Wraps'),
    (900127, 30556, 30560, 'acx-nether-hatchet', 'Nether Hatchet'),
    (900128, 319, 3797, 'acx-nether-jambiya', 'Nether Jambiya'),
    (900129, 321, 3805, 'acx-nether-jitte', 'Nether Jitte'),
    (900130, 22158, 22157, 'acx-nether-jo', 'Nether Jo'),
    (900131, 324, 3813, 'acx-nether-kaskara', 'Nether Kaskara'),
    (900132, 325, 3817, 'acx-nether-kasrullah', 'Nether Kasrullah'),
    (900133, 326, 3821, 'acx-nether-katar', 'Nether Katar'),
    (900134, 327, 3825, 'acx-nether-ken', 'Nether Ken'),
    (900135, 41062, 41066, 'acx-nether-khanda-handled-mace', 'Nether Khanda-handled Mace'),
    (900136, 328, 3829, 'acx-nether-khanjar', 'Nether Khanjar'),
    (900137, 329, 3833, 'acx-nether-knife', 'Nether Knife'),
    (900138, 30611, 30614, 'acx-nether-knuckles', 'Nether Knuckles'),
    (900139, 31794, 31793, 'acx-nether-lancet', 'Nether Lancet'),
    (900140, 351, 3884, 'acx-nether-long-sword', 'Nether Long Sword'),
    (900141, 31764, 31763, 'acx-nether-lugian-hammer', 'Nether Lugian Hammer'),
    (900142, 331, 3837, 'acx-nether-mace', 'Nether Mace'),
    (900143, 41041, 41045, 'acx-nether-magari-yari', 'Nether Magari Yari'),
    (900144, 30581, 30584, 'acx-nether-mazule', 'Nether Mazule'),
    (900145, 332, 3938, 'acx-nether-morning-star', 'Nether Morning Star'),
    (900146, 22163, 22162, 'acx-nether-nabut', 'Nether Nabut'),
    (900147, 7771, 7795, 'acx-nether-naginata', 'Nether Naginata'),
    (900148, 4195, 4198, 'acx-nether-nekode', 'Nether Nekode'),
    (900149, 40760, 40764, 'acx-nether-nodachi', 'Nether Nodachi'),
    (900150, 336, 3845, 'acx-nether-ono', 'Nether Ono'),
    (900151, 30591, 30595, 'acx-nether-partizan', 'Nether Partizan'),
    (900152, 41046, 41050, 'acx-nether-pike', 'Nether Pike'),
    (900153, 30596, 30599, 'acx-nether-poniard', 'Nether Poniard'),
    (900154, 40623, 40627, 'acx-nether-quadrelle', 'Nether Quadrelle'),
    (900155, 22168, 22167, 'acx-nether-quarter-staff', 'Nether Quarter Staff'),
    (900156, 6853, 45107, 'acx-nether-rapier', 'Nether Rapier'),
    (900157, 30566, 30569, 'acx-nether-sabra', 'Nether Sabra'),
    (900158, 45108, 45112, 'acx-nether-schlager', 'Nether Schlager'),
    (900159, 339, 3852, 'acx-nether-scimitar', 'Nether Scimitar'),
    (900160, 340, 3856, 'acx-nether-shamshir', 'Nether Shamshir'),
    (900161, 41067, 41071, 'acx-nether-shashqa', 'Nether Shashqa'),
    (900162, 352, 3888, 'acx-nether-short-sword', 'Nether Short Sword'),
    (900163, 342, 3860, 'acx-nether-shou-ono', 'Nether Shou-ono'),
    (900164, 344, 3868, 'acx-nether-silifi', 'Nether Silifi'),
    (900165, 345, 3872, 'acx-nether-simi', 'Nether Simi'),
    (900166, 31812, 31817, 'acx-nether-slingshot', 'Nether Slingshot'),
    (900167, 30571, 30573, 'acx-nether-spada', 'Nether Spada'),
    (900168, 40618, 40622, 'acx-nether-spadone', 'Nether Spadone'),
    (900169, 348, 3876, 'acx-nether-spear', 'Nether Spear'),
    (900170, 7768, 7787, 'acx-nether-spiked-club', 'Nether Spiked Club'),
    (900171, 31779, 31778, 'acx-nether-spine-glaive', 'Nether Spine Glaive'),
    (900172, 31788, 31792, 'acx-nether-stick', 'Nether Stick'),
    (900173, 30601, 30604, 'acx-nether-stiletto', 'Nether Stiletto'),
    (900174, 353, 3892, 'acx-nether-tachi', 'Nether Tachi'),
    (900175, 354, 3896, 'acx-nether-takuba', 'Nether Takuba'),
    (900176, 40635, 40639, 'acx-nether-tetsubo', 'Nether Tetsubo'),
    (900177, 356, 3900, 'acx-nether-tofun', 'Nether Tofun'),
    (900178, 7772, 7791, 'acx-nether-trident', 'Nether Trident'),
    (900179, 357, 3904, 'acx-nether-tungi', 'Nether Tungi'),
    (900180, 31769, 31768, 'acx-nether-war-axe', 'Nether War Axe'),
    (900181, 359, 3908, 'acx-nether-war-hammer', 'Nether War Hammer'),
    (900182, 361, 3912, 'acx-nether-yaoji', 'Nether Yaoji'),
    (900183, 362, 3916, 'acx-nether-yari', 'Nether Yari');

INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
SELECT n.wcid, n.class_name, w.type, NOW() FROM acx_nether n JOIN weenie w ON w.class_Id = n.src;

INSERT INTO weenie_properties_int (object_Id, type, value) SELECT n.wcid, p.type, p.value FROM acx_nether n JOIN weenie_properties_int p ON p.object_Id = n.src;
INSERT INTO weenie_properties_int64 (object_Id, type, value) SELECT n.wcid, p.type, p.value FROM acx_nether n JOIN weenie_properties_int64 p ON p.object_Id = n.src;
INSERT INTO weenie_properties_float (object_Id, type, value) SELECT n.wcid, p.type, p.value FROM acx_nether n JOIN weenie_properties_float p ON p.object_Id = n.src;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT n.wcid, p.type, p.value FROM acx_nether n JOIN weenie_properties_bool p ON p.object_Id = n.src;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT n.wcid, p.type, p.value FROM acx_nether n JOIN weenie_properties_string p ON p.object_Id = n.src;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT n.wcid, p.type, p.value FROM acx_nether n JOIN weenie_properties_d_i_d p ON p.object_Id = n.src;
INSERT INTO weenie_properties_i_i_d (object_Id, type, value) SELECT n.wcid, p.type, p.value FROM acx_nether n JOIN weenie_properties_i_i_d p ON p.object_Id = n.src;
INSERT INTO weenie_properties_spell_book (object_Id, spell, probability) SELECT n.wcid, p.spell, p.probability FROM acx_nether n JOIN weenie_properties_spell_book p ON p.object_Id = n.src;

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
