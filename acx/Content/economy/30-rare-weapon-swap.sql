-- Project ACX: the rare weapon swap (JJ 2026-11-15: "So essentially if you get a weapon you cant use. you give it to the
-- emissary and he lets you buy one you can use", "so theres no way to be able to trade in missile weapons too?", "yes do
-- all 61"). Retail's Emissary of Asheron (45492, Arwic) took only the OLD versions of the melee rares (30310-30344) for a
-- Rare Coin (45493), to buy the updated ones from the Melee Rare Vendor (45485, Arwic and the Marketplace); the loot rolls
-- only the new ones: the 61 tier-6 rares (LootGenerationFactory_Rare.cs), all weapons, all Value 50,000. Now:
--   the Emissary takes any of the 61 for a Rare Coin (his own Coin reply: "May your new weapon suit you well.");
--   the vendor, renamed Rare Weapon Vendor, sells all 61 (melee, bows, crossbows, atlatls, casters) for 1 coin each
--   (its SellPrice x Value: 0.00002 x 50,000 = 1; Vendor.GetSellCost).
--   the Rare Coin glows purple (UiEffects Lightning), the colour players read as rare (JJ: "yeah purple works, add it"):
--   the ACX Mark (900710) was copied from its look, and JJ keeps that look on the Mark.
-- Re-runnable. Standing NPCs pick it up when their landblock reloads (/reload-landblock, or a restart).
DROP TEMPORARY TABLE IF EXISTS acx_rare_weapons;
CREATE TEMPORARY TABLE acx_rare_weapons (wcid INT UNSIGNED PRIMARY KEY);
INSERT INTO acx_rare_weapons (wcid) VALUES (30302),(30303),(30304),(30305),(30306),(30307),(30308),(30309),(30345),(30346),(30347),(30348),(30349),(30350),(30351),(30374),(30375),(30376),(30377),(30378),(42662),(42663),(42664),(42665),(42666),(43848),(45436),(45437),(45438),(45439),(45440),(45441),(45442),(45443),(45444),(45445),(45446),(45447),(45448),(45449),(45450),(45451),(45452),(45453),(45454),(45455),(45456),(45457),(45458),(45459),(45460),(45461),(45462),(45463),(45464),(45465),(45466),(45467),(45468),(45469),(45470);

-- the Emissary: a Give emote (category 6) per rare, whose one action goes to his Coin emote (type 67, Goto)
DELETE a FROM weenie_properties_emote_action a JOIN weenie_properties_emote e ON e.id = a.emote_Id
    JOIN acx_rare_weapons r ON r.wcid = e.weenie_Class_Id WHERE e.object_Id = 45492 AND e.category = 6;
DELETE e FROM weenie_properties_emote e JOIN acx_rare_weapons r ON r.wcid = e.weenie_Class_Id WHERE e.object_Id = 45492 AND e.category = 6;
INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id) SELECT 45492, 6, 1, wcid FROM acx_rare_weapons;
INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, message)
SELECT e.id, 0, 67, 0, 1, 'Coin' FROM weenie_properties_emote e JOIN acx_rare_weapons r ON r.wcid = e.weenie_Class_Id
WHERE e.object_Id = 45492 AND e.category = 6;

-- the vendor: every rare weapon in its shop, 1 coin each, and a name that fits
INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond)
SELECT 45485, 4, r.wcid, -1, 0, 0, 0 FROM acx_rare_weapons r
WHERE NOT EXISTS (SELECT 1 FROM weenie_properties_create_list c WHERE c.object_Id = 45485 AND c.destination_Type = 4 AND c.weenie_Class_Id = r.wcid);
UPDATE weenie_properties_float SET value = 0.00002 WHERE object_Id = 45485 AND type = 37;
UPDATE weenie_properties_string SET value = 'Rare Weapon Vendor' WHERE object_Id = 45485 AND type = 1;

-- the Rare Coin: a purple glow on its icon, so it doesn't look like the ACX Mark
DELETE FROM weenie_properties_int WHERE object_Id = 45493 AND type = 18;
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES (45493, 18, 64);
