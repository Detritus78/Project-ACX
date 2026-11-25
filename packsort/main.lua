-- PackSort, was Pack Mover (JJ 2026-11-24: "a pack mover by type like jewelry, gem, armor, salvage, or whatever other types there are ...
-- So I can move from main to side pack or side pack to side pack"). Select a pack, or your character for the main pack,
-- and click Set Source; select the destination and click Set Destination; pick a type and click Move. Items of that type
-- move one at a time; anything that doesn't fit stays. Equipped items, packs and pyreals never move.
-- 1.2.0 (AC:Unreal 166): the main pack, an open chest or vault (select it) and the side packs inside it can be the source
-- or the destination. A storage choice is kept while it's closed; Move needs it open again.
-- 2.0.0 (JJ 2026-11-24: "see if you can copy/duplicate all of mudsort onto our pack mover"; "Lets rename it to pack sorter", then "call it PackSort"): our MudSort port (new code
-- that does what Mudzereli's MudSort 1.6.7 for Decal does, https://github.com/mudzereli/mudsort, Copyright (c) Mudzereli
-- 2022; its sort strings and two-letter codes) is folded in as the Sort, Build and Options tabs. With 166's placement it
-- also does MudSort's Insert Position, sorts into the main pack and in and out of storage, and sorts a pack in place.
-- Commands while open: /acueplugin packsort start | stop | set source [player|pack <n>|storage|storage pack <n>] |
-- set dest [the same] | set flags <sort string> | set ocfilter <class> | clear ocfilter. With nothing after set source or
-- set dest, the selected pack (or player/NPC, for dest) is used.

local TYPES = {
    {"Jewelry", 0x8}, {"Gems", 0x800}, {"Salvage", 0x40000000}, {"Armor", 0x2}, {"Clothing", 0x4},
    {"Weapons (all)", 0x1 + 0x100 + 0x8000}, {"Melee weapons", 0x1}, {"Missile weapons", 0x100}, {"Casters", 0x8000},
    {"Summoning essences", "pet"}, {"Spell components", 0x1000}, {"Mana stones", 0x80000}, {"Food", 0x20},
    {"Keys", 0x4000}, {"Scrolls and books", 0x2000}, {"Trade notes", 0x40000}, {"Alchemy", 0xC00000},
    {"Fletching", 0xA000000}, {"Cooking", 0x400000}, {"Misc", 0x80}, {"Useless", 0x400}, {"Everything", "all"},
}
local TYPE_NAMES = {}
for i, t in ipairs(TYPES) do TYPE_NAMES[i] = t[1] end

-------------------------------------------------------------------------------------------------------------------------
-- MudSort's sort keys and sort strings (from our MudSort 1.1.0 port)
-------------------------------------------------------------------------------------------------------------------------
-- every MudSort sort key: code, MudSort's name, kind, property id (MudSort's own ids: below 1,000 the game's property)
local CODES = {
  {"OC", "ObjectClass", "custom", 0},
  {"TR", "CalcedTotalRatings", "custom", 0},
  {"BW", "BuffedWeaponDamage", "custom", 0},
  {"BE", "BuffedElementalDamage", "custom", 0},
  {"BD", "BuffedMeleeDefense", "custom", 0},
  {"BA", "BuffedAttackBonus", "custom", 0},
  {"BP", "BuffedArmorLevel", "custom", 0},
  {"BM", "BuffedManaConversion", "custom", 0},
  {"TM", "TotalMissileDamage", "custom", 0},
  {"TS", "TotalSummonDamage", "custom", 0},
  {"NM", "Name", "string", 1},
  {"IN", "Inscription", "string", 7},
  {"IB", "InscribedBy", "string", 8},
  {"SD", "SimpleDescription", "string", 15},
  {"FD", "FullDescription", "string", 16},
  {"OA", "OnlyActivatedBy", "string", 25},
  {"PD", "PortalDestination", "string", 38},
  {"LT", "LastTinkeredBy", "string", 39},
  {"IM", "ImbuedBy", "string", 40},
  {"SN", "SecondaryName", "string", 184549376},
  {"BR", "Burden", "long", 5},
  {"ES", "EquippedSlots", "long", 10},
  {"RI", "RareId", "long", 17},
  {"VL", "Value", "long", 19},
  {"AL", "ArmorLevel", "long", 28},
  {"BN", "Bonded", "long", 33},
  {"UN", "Unenchantable", "long", 36},
  {"WE", "WandElemDmgType", "long", 45},
  {"ML", "MinLevelRestrict", "long", 86},
  {"MR", "MaxLevelRestrict", "long", 87},
  {"HK", "HealKitSkillBonus", "long", 90},
  {"UT", "UsesTotal", "long", 91},
  {"UR", "UsesRemaining", "long", 92},
  {"WR", "Workmanship", "long", 105},
  {"SP", "Spellcraft", "long", 106},
  {"CM", "CurrentMana", "long", 107},
  {"MM", "MaximumMana", "long", 108},
  {"LR", "LoreRequirement", "long", 109},
  {"RR", "RankRequirement", "long", 110},
  {"AT", "Attuned", "long", 114},
  {"SL", "SkillLevelReq", "long", 115},
  {"MT", "Material", "long", 131},
  {"WT", "WieldReqType", "long", 158},
  {"WA", "WieldReqAttribute", "long", 159},
  {"WV", "WieldReqValue", "long", 160},
  {"SS", "SlayerSpecies", "long", 166},
  {"NT", "NumberTimesTinkered", "long", 171},
  {"AR", "ActivationReqSkillId", "long", 176},
  {"ID", "Imbued", "long", 179},
  {"ED", "ElementalDmgBonus", "long", 204},
  {"AS", "ArmorSet", "long", 265},
  {"SM", "SummoningMastery", "long", 362},
  {"DR", "DamRating", "long", 370},
  {"DM", "DamResistRating", "long", 371},
  {"CR", "CritRating", "long", 372},
  {"CT", "CritResist", "long", 373},
  {"CD", "CritDamRating", "long", 374},
  {"CS", "CritDamResistRating", "long", 375},
  {"HB", "HealBoostRating", "long", 376},
  {"VR", "VitalityRating", "long", 379},
  {"TY", "Type", "long", 218103808},
  {"IC", "Icon", "long", 218103809},
  {"IS", "ItemSlots", "long", 218103812},
  {"PS", "PackSlots", "long", 218103813},
  {"SC", "StackCount", "long", 218103814},
  {"ST", "StackMax", "long", 218103815},
  {"AC", "AssociatedSpell", "long", 218103816},
  {"SG", "SlotLegacy", "long", 218103817},
  {"WS", "WieldingSlot", "long", 218103819},
  {"CV", "Coverage", "long", 218103821},
  {"EQ", "EquipableSlots", "long", 218103822},
  {"ET", "EquipType", "long", 218103823},
  {"IO", "IconOutline", "long", 218103824},
  {"MS", "MissileType", "long", 218103825},
  {"MD", "Model", "long", 218103830},
  {"CG", "Category", "long", 218103834},
  {"WP", "WeapSpeed", "long", 218103839},
  {"EP", "EquipSkill", "long", 218103840},
  {"DT", "DamageType", "long", 218103841},
  {"MX", "MaxDamage", "long", 218103842},
  {"IV", "IconOverlay", "long", 218103849},
  {"IU", "IconUnderlay", "long", 218103850},
  {"MO", "ManaRateOfChange", "double", 5},
  {"MB", "MeleeDefenseBonus", "double", 29},
  {"ME", "ManaTransferEfficiency", "double", 87},
  {"HR", "HealingKitRestoreBonus", "double", 100},
  {"MC", "ManaStoneChanceDestruct", "double", 137},
  {"MN", "ManaCBonus", "double", 144},
  {"BS", "BitingStrikeChance", "double", 147},
  {"MI", "MissileDBonus", "double", 149},
  {"MG", "MagicDBonus", "double", 150},
  {"EV", "ElementalDamageVersusMonsters", "double", 152},
  {"SH", "SlashProt", "double", 167772160},
  {"PP", "PierceProt", "double", 167772161},
  {"BL", "BludgeonProt", "double", 167772162},
  {"AP", "AcidProt", "double", 167772163},
  {"LP", "LightningProt", "double", 167772164},
  {"FP", "FireProt", "double", 167772165},
  {"CP", "ColdProt", "double", 167772166},
  {"HD", "Heading", "double", 167772167},
  {"AD", "ApproachDistance", "double", 167772168},
  {"SW", "SalvageWorkmanship", "double", 167772169},
  {"VN", "Variance", "double", 167772171},
  {"AB", "AttackBonus", "double", 167772172},
  {"RN", "Range", "double", 167772173},
  {"DB", "DamageBonus", "double", 167772174},
  {"HV", "HookVisibility", "bool", 24},
  {"UU", "UnlimitedUses", "bool", 63},
  {"CB", "CanBeSold", "bool", 69},
  {"RT", "Retained", "bool", 91},
  {"IR", "Ivoryable", "bool", 99},
  {"DY", "Dyeable", "bool", 100},
  {"IL", "Inscribable", "bool", 201326593},
}
local BY_CODE = {}
for _, row in ipairs(CODES) do BY_CODE[row[1]] = {code=row[1], name=row[2], kind=row[3], id=row[4]} end
-- codes MudSort's own default string carried that never matched a key (its error log's crash): taken as meant
local ALIASES = {DG="DB", MA="MT"}


-- Decal's ObjectClass numbers, as AC:Unreal gives them (object_class)
local CLASS_NAMES = {[0]="Unknown", "MeleeWeapon", "Armor", "Clothing", "Jewelry", "Monster", "Food", "Money", "Misc",
    "MissileWeapon", "Container", "Gem", "SpellComponent", "Key", "Portal", "TradeNote", "ManaStone", "Plant", "BaseCooking",
    "BaseAlchemy", "BaseFletching", "CraftedCooking", "CraftedAlchemy", "CraftedFletching", "Player", "Vendor", "Door",
    "Corpse", "Lifestone", "HealingKit", "Lockpick", "WandStaffOrb", "Bundle", "Book", "Journal", "Sign", "Housing", "Npc",
    "Foci", "Salvage", "Ust", "Services", "Scroll"}
local CONTAINER, FOCI = 10, 38

local function prop(t, id)
    if type(t) ~= "table" then return nil end
    local v = t[tostring(id)]
    if v == nil then v = t[id] end
    return v
end

-- MudSort's keys that aren't the game's own property ids, from what AC:Unreal gives a plugin
local PSEUDO_LONG = {
    [218103808]=function(i) return i.type end,                                     -- Type
    [218103809]=function(i) return i.icon end,                                     -- Icon
    [218103814]=function(i) return i.count end,                                    -- StackCount
    [218103815]=function(i) return i.max_stack end,                                -- StackMax
    [218103821]=function(i) return prop(i.int_properties, 4) end,                  -- Coverage
    [218103822]=function(i) return prop(i.int_properties, 9) or i.slots end,      -- EquipableSlots
    [218103838]=function(i) return type(i.spell_ids) == "table" and #i.spell_ids or 0 end,  -- SpellCount
    [218103839]=function(i) return i.weapon_time end,                              -- WeapSpeed
    [218103840]=function(i) return i.weapon_skill end,                             -- EquipSkill
    [218103841]=function(i) return i.damage_type end,                              -- DamageType
    [218103842]=function(i) return i.damage end,                                   -- MaxDamage
}
-- Decal's protections (slash, pierce, bludgeon, acid, lightning, fire, cold) are the armor's ArmorModVs properties
local PROTECTIONS = {[167772160]=13, [167772161]=14, [167772162]=15, [167772163]=18, [167772164]=19, [167772165]=17, [167772166]=16}
local PSEUDO_DOUBLE = {
    [167772171]=function(i) return i.variance or prop(i.float_properties, 22) end,      -- Variance
    [167772172]=function(i) return prop(i.float_properties, 62) end,                     -- AttackBonus (weapon offense)
    [167772174]=function(i) return i.damage_mod or prop(i.float_properties, 63) end,    -- DamageBonus (a launcher's multiplier)
}
local function ratings(i)
    local sum = 0
    for _, id in ipairs({370, 371, 372, 373, 374, 375, 376, 379}) do sum = sum + (tonumber(prop(i.int_properties, id)) or 0) end
    return sum
end
-- MudSort's own keys; the "buffed" ones use the item's numbers without working out buffs
local CUSTOM = {
    OC=function(i) return CLASS_NAMES[i.object_class or 0] or "Unknown" end,
    TR=ratings,
    BW=function(i) return i.damage end,
    BE=function(i) return prop(i.float_properties, 152) end,
    BD=function(i) return prop(i.float_properties, 29) end,
    BA=function(i) return prop(i.float_properties, 62) end,
    BP=function(i) return prop(i.int_properties, 28) end,
    BM=function(i) return prop(i.float_properties, 144) end,
    TM=function(i) return ((tonumber(i.damage) or 0) + (tonumber(prop(i.int_properties, 204)) or 0)) * (tonumber(i.damage_mod) or 1) end,
    TS=function(i)
        local d, c, cd = tonumber(prop(i.int_properties, 370)) or 0, tonumber(prop(i.int_properties, 372)) or 0, tonumber(prop(i.int_properties, 374)) or 0
        return (0.625 * (1 + d / 100) * (0.9 - c / 100) + 2 * (1 + (d + cd) / 100) * (0.1 + c / 100)) / 0.01365
    end,
}

local function value_of(key, i)
    if key.kind == "custom" then return CUSTOM[key.code](i) end
    if key.kind == "string" then
        if key.id == 1 then return i.name end
        return prop(i.string_properties, key.id)
    end
    if key.kind == "long" then
        if PSEUDO_LONG[key.id] then return PSEUDO_LONG[key.id](i) end
        if key.id == 19 then return i.value or prop(i.int_properties, 19) end
        if key.id == 131 then return i.material or prop(i.int_properties, 131) end
        return prop(i.int_properties, key.id)
    end
    if key.kind == "double" then
        if PROTECTIONS[key.id] then return prop(i.float_properties, PROTECTIONS[key.id]) end
        if PSEUDO_DOUBLE[key.id] then return PSEUDO_DOUBLE[key.id](i) end
        return prop(i.float_properties, key.id)
    end
    return nil   -- bool keys: AC:Unreal gives plugins no bool properties
end

-- what can be read before an item is identified
local function needs_identify(key)
    if key.kind == "custom" then return key.code ~= "OC" end
    if key.kind == "string" then return key.id ~= 1 end
    if key.kind == "long" then
        return not (key.id == 19 or key.id == 131 or key.id == 218103808 or key.id == 218103809 or key.id == 218103814 or key.id == 218103815)
    end
    return true
end

local function trim(text)
    local a, b = 1, #text
    while a <= b and text:sub(a, a) == " " do a = a + 1 end
    while b >= a and text:sub(b, b) == " " do b = b - 1 end
    return text:sub(a, b)
end

-- "OC,MX-,NM" -> keys; unknown codes are reported, not fatal
local function parse_sort(text)
    local keys, unknown, piece = {}, {}, ""
    text = (text or "") .. ","
    for n = 1, #text do
        local c = text:sub(n, n)
        if c == "," then
            piece = trim(piece):upper()
            if #piece > 0 then
                local descending = piece:sub(-1) == "-"
                local code = descending and trim(piece:sub(1, -2)) or piece
                code = ALIASES[code] or code
                local key = BY_CODE[code]
                if key then keys[#keys + 1] = {code=key.code, name=key.name, kind=key.kind, id=key.id, descending=descending}
                else unknown[#unknown + 1] = piece end
            end
            piece = ""
        else
            piece = piece .. c
        end
    end
    return keys, unknown
end

-- natural order for text ("Sword 2" before "Sword 10"), numbers as numbers, missing values as 0 / ""
local function is_digit(b) return b and b >= 48 and b <= 57 end
local function natural_less(a, b)
    a, b = a:lower(), b:lower()
    local i, j = 1, 1
    while i <= #a and j <= #b do
        local ca, cb = a:byte(i), b:byte(j)
        if is_digit(ca) and is_digit(cb) then
            local si, sj = i, j
            while is_digit(a:byte(i)) do i = i + 1 end
            while is_digit(b:byte(j)) do j = j + 1 end
            local na, nb = tonumber(a:sub(si, i - 1)), tonumber(b:sub(sj, j - 1))
            if na ~= nb then return na < nb end
        else
            if ca ~= cb then return ca < cb end
            i, j = i + 1, j + 1
        end
    end
    return #a - i < #b - j
end
local function compare(x, y)   -- -1, 0, 1
    if type(x) == "number" or type(y) == "number" then
        x, y = tonumber(x) or 0, tonumber(y) or 0
        if x == y then return 0 end
        return x < y and -1 or 1
    end
    x, y = tostring(x or ""), tostring(y or "")
    if x == y then return 0 end
    return natural_less(x, y) and -1 or 1
end


-- the Object Class Filter list, as MudSort's (its "####" lines are headings and filter nothing)
local OC_OPTIONS = {"None", "####### EQUIPMENT #######", "Armor", "Clothing", "Jewelry", "######## WEAPONS ########",
    "MeleeWeapon", "MissileWeapon", "WandStaffOrb", "####### CRAFTING ########", "Food", "Key", "Lockpick", "Plant", "Salvage",
    "####### CURRENCY ########", "Money", "TradeNote", "##### MISCELLANEOUS #####", "Book", "Gem", "Scroll", "SpellComponent",
    "HealingKit", "ManaStone", "Misc", "Unknown", "Custom"}

local LIST_FILTERS = {"Commonly Used Keys", "All Keys", "String Keys", "Long Keys", "Double Keys", "Boolean Keys", "Other Keys"}
local KIND_OF_FILTER = {["String Keys"]="string", ["Long Keys"]="long", ["Double Keys"]="double", ["Boolean Keys"]="bool", ["Other Keys"]="custom"}
local KIND_LETTER = {string="S", long="L", double="D", bool="B", custom="C"}
local KIND_WORDS = {string="String: a text property", long="Long: a whole-number property",
    double="Double: a decimal-number property", bool="Boolean: a yes/no property",
    custom="Custom: worked out by PackSort from several properties"}
-- Hover text for a sort key: "Object Class" for OC. Splits MudSort's CamelCase names into words and spells out its
-- short forms (no string.gsub in the plugin sandbox, so it walks the name one character at a time).
local WORD_FIX = {Dmg="Damage", Req="Requirement", Reqs="Requirements", Calced="Calculated", Id="ID", Min="Minimum",
    Max="Maximum", Restrict="Restriction", Mod="Modifier", Def="Defense", Num="Number", Lvl="Level", Pct="Percent",
    Elem="Elemental", Dam="Damage", Crit="Critical", Weap="Weapon", Prot="Protection", Destruct="Destruction",
    DBonus="Defense Bonus", CBonus="Conversion Bonus"}
local function spelled(name)
    local words, cur = {}, ""
    for i = 1, #name do
        local c = name:sub(i, i)
        local prev = name:sub(i - 1, i - 1)
        local upper = c >= "A" and c <= "Z"
        local digit = c >= "0" and c <= "9"
        local prev_lower = prev >= "a" and prev <= "z"
        local prev_digit = prev >= "0" and prev <= "9"
        if cur ~= "" and ((upper and (prev_lower or prev_digit)) or (digit and not prev_digit)) then
            words[#words + 1] = cur
            cur = ""
        end
        cur = cur .. c
    end
    if cur ~= "" then words[#words + 1] = cur end
    for i, w in ipairs(words) do words[i] = WORD_FIX[w] or w end
    return table.concat(words, " ")
end
local function key_tip(key)
    return spelled(key.name)
end
-- MudSort's "Commonly Used Keys"
local COMMON = {}
for _, name in ipairs({"ObjectClass", "EquipableSlots", "Coverage", "ArmorLevel", "AttackBonus", "DamageBonus",
    "ElementalDmgBonus", "MinLevelRestrict", "Material", "Name", "ActivationReqSkillId", "ArmorSet", "Attuned", "Bonded",
    "Burden", "CurrentMana", "DamageType", "Icon", "MaxDamage", "RareId", "SlayerSpecies", "StackCount", "Value",
    "WieldReqAttribute", "WieldReqType", "WieldReqValue", "Workmanship", "MeleeDefenseBonus", "Variance"}) do COMMON[name] = true end

local PAGE = 12        -- unused keys shown per Build page (the window holds 256 controls in all)
local MAX_ADDED = 12   -- sort keys shown on the Build tab

local function copy(t)
    local out = {}
    for k, v in pairs(t or {}) do out[k] = type(v) == "table" and copy(v) or v end
    return out
end

local function fmt(v)
    if type(v) == "number" then
        if v == math.floor(v) and math.abs(v) < 1e15 then return string.format("%d", v) end
        return string.format("%.4f", v)
    end
    return tostring(v)
end

local function keys_to_string(keys)
    local parts = {}
    for _, k in ipairs(keys) do parts[#parts + 1] = k.code .. (k.descending and "-" or "") end
    return table.concat(parts, ",")
end

local function starts_with(text, prefix)
    return prefix ~= "" and text:sub(1, #prefix) == prefix
end

-------------------------------------------------------------------------------------------------------------------------
-- where items can go: your main pack and side packs, an open chest or vault and the packs inside it, or a player or NPC
-------------------------------------------------------------------------------------------------------------------------
local CAPACITY = "218103812"   -- int_properties: ItemsCapacity

local function storage_open(s) return s.container and s.container ~= 0 and not s.container_is_corpse end

local function storage_packs(s)
    local list = {}
    for _, row in ipairs(s.contents or {}) do
        local cap = row.int_properties and row.int_properties[CAPACITY] or 0
        if cap ~= 0 then list[#list + 1] = row end
    end
    return list
end

-- the name of a pack the plugin can use, or nil when it isn't available right now
local function pack_name(s, id)
    if not id then return nil end
    if id == s.player then return "Main pack" end
    for n, bag in ipairs(s.packs or {}) do if bag.id == id then return "Pack " .. n end end
    if storage_open(s) then
        if id == s.container then return "Storage" end
        for n, bag in ipairs(storage_packs(s)) do if bag.id == id then return "Storage pack " .. n end end
    end
    return nil
end

local function is_storage_name(name) return name and name:sub(1, 7) == "Storage" end

-- "player", "pack 3", "storage", "storage pack 2" -> id, name
local function pack_by_text(s, text)
    text = trim(tostring(text or "")):lower()
    if text == "player" or text == "main" or text == "main pack" then return s.player, "Main pack" end
    if starts_with(text, "pack ") then
        local n = tonumber(text:sub(6))
        local bag = n and (s.packs or {})[n]
        if bag then return bag.id, "Pack " .. n end
        return nil
    end
    if storage_open(s) then
        if text == "storage" or text == "chest" or text == "vault" then return s.container, "Storage" end
        if starts_with(text, "storage pack ") then
            local n = tonumber(text:sub(14))
            local bag = n and storage_packs(s)[n]
            if bag then return bag.id, "Storage pack " .. n end
        end
    end
    return nil
end

-- free item slots, or nil when the client doesn't say (the open chest itself: the server refuses a move when it's full)
local function free_slots(s, id)
    if id == s.player then return s.main_pack_slots end
    for _, bag in ipairs(s.packs or {}) do if bag.id == id then return bag.free or 0 end end
    for _, bag in ipairs(storage_packs(s)) do
        if bag.id == id then
            local cap, used = bag.int_properties[CAPACITY], 0
            if cap < 0 then return nil end
            for _, row in ipairs(s.external_pack_contents or {}) do if row.container == id then used = used + 1 end end
            return math.max(0, cap - used)
        end
    end
    return nil
end

local function all_items(s)
    local list = {}
    for _, key in ipairs({"inventory", "contents", "external_pack_contents"}) do
        for _, row in ipairs(s[key] or {}) do list[#list + 1] = row end
    end
    return list
end

local function index_items(s)
    local by_id = {}
    for _, item in ipairs(all_items(s)) do by_id[item.id] = item end
    return by_id
end

local function failure_key(s)
    local f = s.activity_failure
    if type(f) ~= "table" then return "" end
    return tostring(f.activity) .. "|" .. tostring(f.status)
end

-------------------------------------------------------------------------------------------------------------------------
-- settings (saved per character) and the session
-------------------------------------------------------------------------------------------------------------------------
local DEFAULTS = {source=0, source_name="", dest=0, dest_name="", kind="Jewelry",
    sort_string="OC,MX-,DB-,ED-,NM", oc_filter="None", oc_custom="", insert=0,
    saved1="OC,EP,MT,AL-,MX-", saved2="AL-,MX-", saved3="BR-", reverse=false, notify=true, identify_open=false,
    list_filter="Commonly Used Keys", picks=""}

local st, dirty
local give_target                   -- {id, name}: a player or NPC as destination, this session only
local clipboard = ""
local page, dump, message = 1, nil, ""
local log = {}
local job
local idle_identify
local identify_tried = {}

local function set(key, value)
    if st[key] ~= value then st[key] = value; dirty = true end
end

local function note(text)
    table.insert(log, 1, text)
    while #log > 6 do table.remove(log) end
end

local function set_place(s, which, id, name)
    set(which, id); set(which .. "_name", name)
    if which == "dest" then give_target = nil end
    message = (which == "source" and "From: " or "To: ") .. name
end

-- Set Source / Set Destination from the selection: a pack, your character (main pack), an open chest or vault or a pack in
-- it; for the destination also a player or NPC
local function set_from_selection(s, which)
    local sel = s.selection
    if not sel or not sel.id then message = "Select a pack, your character, or an open chest or vault (or a pack in it) first."; return end
    local name = pack_name(s, sel.id)
    if name then set_place(s, which, sel.id, name); return end
    if which == "dest" and sel.object_type ~= "item" and sel.id ~= s.player then
        give_target = {id=sel.id, name=sel.name or "them"}
        message = "To: give to " .. give_target.name
        return
    end
    message = "Select a pack, your character, or an open chest or vault (or a pack in it) first."
end

-- 2.1.0 (JJ 2026-11-25: "only give red letters to my other character", "be able to add the item via a button click"):
-- picked item kinds, by weenie class, kept as "wcid|name;wcid|name". With any picked, Move takes only those kinds.
local function pick_list()
    local list, rest = {}, st.picks or ""
    while rest ~= "" do
        local cut = string.findplain(rest, ";")
        local entry = cut and rest:sub(1, cut - 1) or rest
        rest = cut and rest:sub(cut + 1) or ""
        local bar = string.findplain(entry, "|")
        if bar then list[#list + 1] = {wcid=tonumber(entry:sub(1, bar - 1)), name=entry:sub(bar + 1)} end
    end
    return list
end

local function add_pick(s)
    local sel = s.selection
    local item
    if sel and sel.id then for _, it in ipairs(all_items(s)) do if it.id == sel.id then item = it end end end
    if not item or not item.wcid then message = "Select an item in your pack first, then click Add Selected Item."; return end
    local list = pick_list()
    for _, p in ipairs(list) do if p.wcid == item.wcid then message = (item.name or "That item") .. " is already on the list."; return end end
    local name = ""   -- ";" and "|" separate the stored list
    for i = 1, #(item.name or "?") do
        local c = (item.name or "?"):sub(i, i)
        if c ~= ";" and c ~= "|" then name = name .. c end
    end
    local parts = {}
    for _, p in ipairs(list) do parts[#parts + 1] = p.wcid .. "|" .. p.name end
    parts[#parts + 1] = item.wcid .. "|" .. name
    set("picks", table.concat(parts, ";"))
    message = "Added " .. name .. "."
end

local function picks_text()
    local list = pick_list()
    if #list == 0 then return "Only: (nothing picked: every item of the type)" end
    local names = {}
    for _, p in ipairs(list) do names[#names + 1] = p.name end
    return "Only: " .. table.concat(names, ", ")
end

local function where(s, which)
    if which == "dest" and give_target then return "give to " .. give_target.name end
    local id, name = st[which], st[which .. "_name"]
    if not id or id == 0 or name == "" then return "(none)" end
    if not pack_name(s, id) then return name .. " (not open)" end
    local free = which == "dest" and free_slots(s, id)
    return free and (name .. " (" .. free .. " free)") or name
end

-- the checks both Move and Sort make before starting
local function check_places(s)
    if not st.source or st.source == 0 then return "Set a source first." end
    if not give_target and (not st.dest or st.dest == 0) then return "Set a destination first." end
    if not pack_name(s, st.source) then return "Open " .. st.source_name .. " first." end
    if not give_target and not pack_name(s, st.dest) then return "Open " .. st.dest_name .. " first." end
    return nil
end

local function dest_label()
    return give_target and give_target.name or st.dest_name
end

-------------------------------------------------------------------------------------------------------------------------
-- the Move tab: every item of one type
-------------------------------------------------------------------------------------------------------------------------
local function has_bits(value, mask)
    local bit = 1
    while bit <= mask and bit <= 0x80000000 do
        if math.floor(mask / bit) % 2 == 1 and math.floor((value or 0) / bit) % 2 == 1 then return true end
        bit = bit * 2
    end
    return false
end

local function kind_of(name)
    for _, t in ipairs(TYPES) do if t[1] == name then return t[2] end end
    return TYPES[1][2]
end

local function is_kind(item, kind)
    if has_bits(item.type, 0x200) or has_bits(item.type, 0x40) or item.wcid == 273 then return false end   -- packs, money
    if kind == "all" then return true end
    if kind == "pet" then return item.pet_device == true end
    return has_bits(item.type, kind)
end

local function start_move(s)
    local problem = check_places(s)
    if problem then return nil, problem end
    if not give_target and st.source == st.dest then return nil, "Source and destination are the same pack." end
    local kind = kind_of(st.kind)
    local picked = {}
    for _, p in ipairs(pick_list()) do if p.wcid then picked[p.wcid] = true end end
    local any_picked = next(picked) ~= nil
    local queue = {}
    for _, item in ipairs(all_items(s)) do
        if item.container == st.source and not item.equipped
            and ((any_picked and picked[item.wcid]) or (not any_picked and is_kind(item, kind))) then queue[#queue + 1] = item.id end
    end
    if #queue == 0 then
        return nil, "No " .. (any_picked and "picked items" or st.kind:lower()) .. " in " .. st.source_name .. "."
    end
    if not give_target and free_slots(s, st.dest) == 0 then return nil, st.dest_name .. " is full." end
    return {what="move", stage="move", queue=queue, next=1, done=0, failed=0, streak=0, total=#queue, sent={},
        pipelined=not give_target, give=give_target, dest=st.dest, fail_seen=failure_key(s), note=""}
end

-------------------------------------------------------------------------------------------------------------------------
-- the Sort tab: MudSort
-------------------------------------------------------------------------------------------------------------------------
local function oc_filter_name()
    local choice = st.oc_filter or "None"
    if choice == "Custom" then return trim(st.oc_custom or ""):lower() end
    if choice == "None" or choice:sub(1, 1) == "#" then return "" end
    return choice:lower()
end

local function start_sort(s)
    local keys, unknown = parse_sort(st.sort_string)
    if #keys == 0 then return nil, "Set a sort string first (Sort tab, or build one on the Build tab)." end
    local problem = check_places(s)
    if problem then return nil, problem end
    local filter = oc_filter_name()
    local list, incoming = {}, 0
    for _, item in ipairs(all_items(s)) do
        local class = item.object_class or 0
        local class_name = (CLASS_NAMES[class] or "Unknown"):lower()
        if item.container == st.source and not item.equipped and class ~= CONTAINER and class ~= FOCI
            and (filter == "" or starts_with(class_name, filter)) then
            list[#list + 1] = item.id
            if give_target or item.container ~= st.dest then incoming = incoming + 1 end
        end
    end
    if #list == 0 then return nil, "Nothing to sort in " .. st.source_name .. (filter ~= "" and (" (filter: " .. filter .. ")") or "") .. "." end
    local identify = false
    for _, k in ipairs(keys) do if needs_identify(k) then identify = true end end
    local text = ""
    if #unknown > 0 then text = "Skipped unknown codes: " .. table.concat(unknown, ", ") .. ". " end
    local free = not give_target and free_slots(s, st.dest)
    if free and free < incoming then text = text .. st.dest_name .. " has room for " .. free .. " of " .. incoming .. ". " end
    return {what="sort", keys=keys, list=list, stage=identify and "identify" or "order", next=1, done=0, failed=0, streak=0,
        total=#list, sent={}, give=give_target, dest=st.dest, insert=math.max(0, math.floor(tonumber(st.insert) or 0)),
        reverse=st.reverse, fail_seen=failure_key(s), note=text}
end

local function order_sort(s)
    local items = index_items(s)
    local rows = {}
    for _, id in ipairs(job.list) do
        local item = items[id]
        if item then
            local values = {}
            for n, k in ipairs(job.keys) do values[n] = value_of(k, item) end
            rows[#rows + 1] = {id=id, values=values}
        end
    end
    table.sort(rows, function(a, b)
        for n, k in ipairs(job.keys) do
            local c = compare(a.values[n], b.values[n])
            if c ~= 0 then
                if k.descending then return c > 0 end
                return c < 0
            end
        end
        return a.id < b.id
    end)
    job.queue = {}
    for n = 1, #rows do job.queue[n] = job.reverse and rows[#rows + 1 - n].id or rows[n].id end
end

-------------------------------------------------------------------------------------------------------------------------
-- running a job: Move sends a move a second without waiting for each to land; Sort (each item into its exact place) and
-- giving wait for each one
-------------------------------------------------------------------------------------------------------------------------
local function end_job(text)
    message = text
    note(text)
    local notice = st.notify and job and job.what == "sort" and {action="notice", text="PackSort: " .. text} or nil
    job = nil
    return notice
end

local function finished_text()
    local verb = job.give and (" given to " .. job.give.name) or (" moved to " .. dest_label())
    local text = (job.what == "sort" and "Done sorting: " or "Done: ") .. job.done .. " of " .. job.total .. verb
    if job.failed > 0 then text = text .. ", " .. job.failed .. " didn't move" end
    return text .. "."
end

local function landed(item, sent, job_)
    if job_.give then return not item or (item.count or 1) < sent.count end
    return not item or item.container == job_.dest   -- gone: merged into a stack already there
end

-- the place for the next sorted item: right after the one before it (which shifts down one if this item was ahead of it
-- in the same pack), or the insert position for the first
local function sort_place(items, id)
    if job.next == 1 or not job.prev then return job.insert end
    local prev = items[job.prev]
    local item = items[id]
    if not prev or prev.container ~= job.dest or (prev.placement or -1) < 0 then return job.insert + job.next - 1 end
    if item and item.container == job.dest and (item.placement or -1) >= 0 and item.placement < prev.placement then
        return prev.placement
    end
    return prev.placement + 1
end

local function step(s)
    local items = index_items(s)
    local now = s.time or 0

    if job.stage == "identify" then
        while job.next <= #job.list do
            local id = job.list[job.next]
            local item = items[id]
            if not item or item.identified then job.next, job.pending = job.next + 1, nil
            elseif job.pending == id and now < job.until_time then return nil
            elseif job.pending == id then job.next, job.pending = job.next + 1, nil   -- no answer: sort it with what's known
            else
                job.pending, job.until_time = id, now + 4
                return {action="identify", item=id}
            end
        end
        job.stage, job.pending = "order", nil
    end
    if job.stage == "order" then
        order_sort(s)
        job.stage, job.next = "move", 1
    end

    local fail = failure_key(s)
    if fail ~= job.fail_seen then
        job.fail_seen = fail
        if string.findplain(fail, "no available slot") then
            return end_job("Moved " .. job.done .. "; " .. dest_label() .. " is full, " .. (#job.queue - job.next + 1 + #job.sent) .. " stayed.")
        end
    end

    local still = {}
    for _, sent in ipairs(job.sent) do
        local item = items[sent.id]
        if landed(item, sent, job) then job.done, job.streak, job.prev = job.done + 1, 0, sent.id
        elseif now >= sent.until_time then
            job.failed, job.streak = job.failed + 1, job.streak + 1
            if not job.pipelined and job.streak >= 3 then
                return end_job("Stopped: three items in a row didn't move (" .. dest_label() .. " full, or out of reach?).")
            end
        else still[#still + 1] = sent end
    end
    job.sent = still

    if job.pipelined then
        local free = free_slots(s, job.dest)
        if job.next <= #job.queue and free and free - #job.sent <= 0 then
            if #job.sent > 0 then return nil end
            return end_job("Moved " .. job.done .. "; " .. dest_label() .. " is full, " .. (#job.queue - job.next + 1) .. " stayed.")
        end
        if now < (job.next_time or 0) then return nil end
    elseif #job.sent > 0 then
        return nil
    end

    while job.next <= #job.queue do
        local id = job.queue[job.next]
        local item = items[id]
        if item and item.container == st.source and not item.equipped then
            local intent
            if job.give then
                intent = {action="give", item=id, target=job.give.id, amount=item.count or 1}
            elseif job.what == "sort" then
                intent = {action="store_item", item=id, target=job.dest, placement=sort_place(items, id)}
            else
                intent = {action="store_item", item=id, target=job.dest}
            end
            job.next = job.next + 1
            job.sent[#job.sent + 1] = {id=id, count=item.count or 1, until_time=now + (job.give and 8 or 6)}
            job.next_time = now + 1
            return intent
        end
        job.next = job.next + 1
    end
    if #job.sent == 0 then return end_job(finished_text()) end
    return nil
end

-------------------------------------------------------------------------------------------------------------------------
-- Property Dump Selection
-------------------------------------------------------------------------------------------------------------------------
local function property_dump(s)
    local sel = s.selection
    if not sel or not sel.id then return "Select an item first." end
    local item = index_items(s)[sel.id]
    if not item then return "Select one of your items, or one in the open chest or vault." end
    local lines = {(item.name or "?") .. (item.identified and "" or "  (not identified yet: Activate a sort, or turn on Identify)")}
    for _, row in ipairs(CODES) do
        local key = BY_CODE[row[1]]
        local v = value_of(key, item)
        if v ~= nil and v ~= "" then lines[#lines + 1] = key.code .. "  " .. key.name .. ": " .. fmt(v) end
    end
    return table.concat(lines, "\n")
end

-------------------------------------------------------------------------------------------------------------------------
-- commands: /acueplugin packsort <command>
-------------------------------------------------------------------------------------------------------------------------
local function handle_command(s, text)
    text = trim(text or "")
    local lower = text:lower()
    if lower == "start" then return "sort" end
    if lower == "stop" or lower == "cancel" then return "cancel" end
    for _, which in ipairs({"source", "dest"}) do
        local prefix = "set " .. which
        if starts_with(lower, prefix) then
            local arg = trim(text:sub(#prefix + 1))
            if arg == "" then set_from_selection(s, which); return nil end
            local id, name = pack_by_text(s, arg)
            if id then set_place(s, which, id, name) else message = "Not a pack: " .. arg end
            return nil
        end
    end
    if starts_with(lower, "set flags ") then set("sort_string", trim(text:sub(11))); message = "Sort string set."
    elseif starts_with(lower, "set ocfilter ") then set("oc_filter", "Custom"); set("oc_custom", trim(text:sub(14))); message = "Object class filter set."
    elseif lower == "clear ocfilter" or lower == "set ocfilter" then set("oc_filter", "None"); message = "Object class filter cleared."
    else message = "Commands: start, stop, set source/dest [player|pack <n>|storage|storage pack <n>], set flags <string>, set ocfilter <class>, clear ocfilter" end
    return nil
end

-------------------------------------------------------------------------------------------------------------------------
-- the window
-------------------------------------------------------------------------------------------------------------------------
local function button(id, text, tooltip, enabled)
    return {id=id, type="button", text=text, tooltip=tooltip, enabled=enabled ~= false}
end

local function places(s, prefix)
    return {
        {id=prefix .. "_r_set", type="row", children={
            button(prefix .. "_src", "Set Source", "Select a pack, your character (main pack), an open chest or vault, or a pack in it, then click", job == nil),
            button(prefix .. "_dst", "Set Destination", "The same, or a player or NPC to give the items to", job == nil)}},
        {id=prefix .. "_from", text="From: " .. where(s, "source")},
        {id=prefix .. "_to", text="To: " .. where(s, "dest")},
    }
end

local function progress_text()
    if not job then return message ~= "" and message or " " end
    if job.stage == "identify" then return "Identifying " .. job.next .. " of " .. #job.list .. "..." end
    return (job.what == "sort" and "Sorting " or "Moving ") .. (job.done + job.failed) .. "/" .. job.total .. "..."
end

local function move_tab(s)
    local children = places(s, "m")
    children[#children + 1] = {id="m_kind", type="select", text="Type", value=st.kind, options=TYPE_NAMES}
    children[#children + 1] = {id="m_r_pick", type="row", children={
        button("m_pick", "Add Selected Item", "Select an item in your pack, then click: Move takes only the kinds you add", job == nil),
        button("m_pick_clear", "Clear", "Back to every item of the Type", job == nil and st.picks ~= "")}}
    children[#children + 1] = {id="m_picks", text=picks_text()}
    children[#children + 1] = button("m_go", job and "Stop" or "Move", nil, job == nil or job.what == "move")
    children[#children + 1] = {id="m_msg", text=progress_text()}
    children[#children + 1] = {id="m_log", text=#log > 0 and table.concat(log, "\n") or " "}
    return {id="t_move", type="column", text="Move", children=children}
end

local function sort_tab(s)
    local children = places(s, "s")
    local progress = 0
    if job and job.what == "sort" then
        progress = job.stage == "identify" and (job.next - 1) / math.max(1, #job.list) or (job.done + job.failed) / math.max(1, job.total)
    elseif message ~= "" then progress = 1 end
    local more = {
        {id="s_ins_row", type="row", children={
            {id="s_ins_label", text="Insert Position"},
            {id="s_ins", type="input", value=tostring(st.insert), tooltip="Where in the destination the sorted items start: 0 = the front"}}},
        {id="s_str_row", type="row", children={
            {id="s_str_label", text="Sort String"},
            button("s_copy", "Copy", "Copy the sort string (paste it into a stored slot on Options)"),
            button("s_paste", "Paste", "Paste the copied string here")}},
        {id="s_str", type="input", value=st.sort_string, tooltip="MudSort codes, comma separated; - after a code = highest first"},
        {id="s_oc", type="select", text="Object Class Filter", value=st.oc_filter, options=OC_OPTIONS},
    }
    for _, c in ipairs(more) do children[#children + 1] = c end
    if st.oc_filter == "Custom" then
        children[#children + 1] = {id="s_oc_custom", type="input", text="Class starts with", value=st.oc_custom, tooltip="e.g. manast for ManaStone"}
    end
    children[#children + 1] = {id="s_progress", type="progress", text=progress_text(), value=progress}
    children[#children + 1] = button("s_activate", job and "Cancel" or "Activate", job and "Stop" or "Begin sorting", job == nil or job.what == "sort")
    if job and job.note ~= "" then children[#children + 1] = {id="s_note", text=job.note, color="yellow"} end
    return {id="t_sort", type="column", text="Sort", children=children}
end

local function build_tab()
    local keys = parse_sort(st.sort_string)
    local added = {}
    for _, k in ipairs(keys) do added[k.code] = true end
    local children = {
        {id="b_head", text="Build a Sort String"},
        {id="b_filter", type="select", text="Sort List Filter", value=st.list_filter, options=LIST_FILTERS},
        {id="b_cols", text="Code  Name                         Up  Dn  A/R  Ord  Key", color="yellow",
            tooltip="Code: MudSort's two-letter code (hover a row for its full name). Up/Dn: move it. A/R: add or remove. Ord: Lo = lowest first, Hi = highest first. Key: S string, L long, D double, B boolean, C custom."},
    }
    for n, k in ipairs(keys) do
        if n > MAX_ADDED then break end
        children[#children + 1] = {id="b_row_" .. k.code, type="row", children={
            {id="b_name_" .. k.code, text=k.code .. "  " .. k.name, wrap=false, tooltip=key_tip(k)},
            button("b_up_" .. k.code, "Up", "Move up", n > 1),
            button("b_dn_" .. k.code, "Dn", "Move down", n < #keys),
            button("b_rm_" .. k.code, "Rem", "Remove from the sort string"),
            button("b_ord_" .. k.code, k.descending and "Hi" or "Lo", k.descending and "Highest first: click for lowest first" or "Lowest first: click for highest first"),
            {id="b_key_" .. k.code, text=KIND_LETTER[k.kind] or "?", tooltip=KIND_WORDS[k.kind]}}}
    end
    if #keys > MAX_ADDED then children[#children + 1] = {id="b_more", text=(#keys - MAX_ADDED) .. " more in the sort string"} end
    local filter_kind = KIND_OF_FILTER[st.list_filter]
    local remaining = {}
    for _, row in ipairs(CODES) do
        local key = BY_CODE[row[1]]
        local wanted = (st.list_filter == "All Keys") or (st.list_filter == "Commonly Used Keys" and COMMON[key.name]) or (filter_kind and key.kind == filter_kind)
        if wanted and not added[key.code] then remaining[#remaining + 1] = key end
    end
    local pages = math.max(1, math.ceil(#remaining / PAGE))
    if page > pages then page = pages end
    children[#children + 1] = {id="b_sep", text="Add to the sort string:", color="yellow"}
    for n = (page - 1) * PAGE + 1, math.min(#remaining, page * PAGE) do
        local key = remaining[n]
        children[#children + 1] = {id="b_row_" .. key.code, type="row", children={
            {id="b_name_" .. key.code, text=key.code .. "  " .. key.name, wrap=false, tooltip=key_tip(key)},
            button("b_add_" .. key.code, "Add", "Add to the sort string (lowest first; click Lo/Hi to flip)"),
            {id="b_key_" .. key.code, text=KIND_LETTER[key.kind] or "?", tooltip=KIND_WORDS[key.kind]}}}
    end
    children[#children + 1] = {id="b_pages", type="row", children={
        button("b_prev", "Prev", nil, page > 1),
        {id="b_page", text="Page " .. page .. " of " .. pages, align="center"},
        button("b_next", "Next", nil, page < pages)}}
    return {id="t_build", type="column", text="Build", children=children}
end

local function options_tab()
    local children = {
        {id="o_identify", type="checkbox", text="Identify inventory while PackSort is open.", value=st.identify_open},
        {id="o_reverse", type="checkbox", text="Completely reverse sort list.", value=st.reverse},
        {id="o_notify", type="checkbox", text="Tell me in chat when a sort is done.", value=st.notify},
        button("o_dump", "Property Dump Selection", "Every sortable property of the selected item"),
    }
    if dump then children[#children + 1] = {id="o_dump_text", text=dump} end
    children[#children + 1] = {id="o_store_head", text="Sort String Storage"}
    for n = 1, 3 do
        children[#children + 1] = {id="o_row" .. n, type="row", children={
            {id="o_saved" .. n, type="input", value=st["saved" .. n]},
            button("o_copy" .. n, "Copy", "Copy this string (paste it on the Sort tab)"),
            button("o_paste" .. n, "Paste", "Store the copied string here")}}
    end
    children[#children + 1] = {id="o_about", text="Sorting is a port of Mudzereli's MudSort for Decal (github.com/mudzereli/mudsort), with its sort strings."}
    return {id="t_options", type="column", text="Options", children=children}
end

local function edit_keys(action, code)
    local keys = parse_sort(st.sort_string)
    local at
    for n, k in ipairs(keys) do if k.code == code then at = n end end
    if action == "add" and not at then
        local key = BY_CODE[code]
        if key then keys[#keys + 1] = {code=key.code, descending=false} end
    elseif at and action == "rm" then table.remove(keys, at)
    elseif at and action == "up" and at > 1 then keys[at], keys[at - 1] = keys[at - 1], keys[at]
    elseif at and action == "dn" and at < #keys then keys[at], keys[at + 1] = keys[at + 1], keys[at]
    elseif at and action == "ord" then keys[at].descending = not keys[at].descending end
    set("sort_string", keys_to_string(keys))
end

return function(s, p)
    if not st then
        st = copy(DEFAULTS)
        for k, v in pairs(s.state or {}) do if DEFAULTS[k] ~= nil and type(v) == type(DEFAULTS[k]) then st[k] = v end end
    end
    dirty = false
    local intent, start_what, cancel

    for _, e in ipairs(s.events or {}) do
        local id = e.id or ""
        if e.type == "command" then
            local what = handle_command(s, e.text)
            if what == "sort" then start_what = "sort" elseif what == "cancel" then cancel = true end
        elseif e.type == "change" then
            if id == "m_kind" then set("kind", tostring(e.value))
            elseif id == "s_str" then set("sort_string", trim(tostring(e.value or "")))
            elseif id == "s_ins" then set("insert", math.max(0, math.floor(tonumber(e.value) or 0)))
            elseif id == "s_oc" then set("oc_filter", tostring(e.value))
            elseif id == "s_oc_custom" then set("oc_custom", trim(tostring(e.value or "")))
            elseif id == "b_filter" then set("list_filter", tostring(e.value)); page = 1
            elseif id == "o_identify" then set("identify_open", e.value == true)
            elseif id == "o_reverse" then set("reverse", e.value == true)
            elseif id == "o_notify" then set("notify", e.value == true)
            elseif starts_with(id, "o_saved") then set("saved" .. id:sub(8), tostring(e.value or "")) end
        elseif e.type == "click" then
            if id == "m_src" or id == "s_src" then set_from_selection(s, "source")
            elseif id == "m_pick" then add_pick(s)
            elseif id == "m_pick_clear" then set("picks", ""); message = "Picked items cleared."
            elseif id == "m_dst" or id == "s_dst" then set_from_selection(s, "dest")
            elseif id == "m_go" then if job then cancel = true else start_what = "move" end
            elseif id == "s_activate" then if job then cancel = true else start_what = "sort" end
            elseif id == "s_copy" then clipboard = st.sort_string; message = "Copied."
            elseif id == "s_paste" then if clipboard ~= "" then set("sort_string", clipboard) end
            elseif id == "o_dump" then dump = property_dump(s)
            elseif starts_with(id, "o_copy") then clipboard = st["saved" .. id:sub(7)] or ""; message = "Copied stored string " .. id:sub(7) .. "."
            elseif starts_with(id, "o_paste") then if clipboard ~= "" then set("saved" .. id:sub(8), clipboard) end
            elseif id == "b_prev" then page = math.max(1, page - 1)
            elseif id == "b_next" then page = page + 1
            elseif starts_with(id, "b_") then
                local action, code = id:sub(3, 4), id:sub(-2)
                local map = {up="up", dn="dn", rm="rm", ["or"]="ord", ad="add"}
                if map[action] then edit_keys(map[action], code) end
            end
        end
    end

    -- saved packs are found again each session: one of yours that's gone is cleared, a storage one is kept for when it's
    -- open again
    for _, k in ipairs({"source", "dest"}) do
        local id, saved = st[k], st[k .. "_name"]
        local now = pack_name(s, id)
        if id and id ~= 0 and not now and not is_storage_name(saved) then set(k, 0); set(k .. "_name", "")
        elseif now and now ~= saved then set(k .. "_name", now) end
    end

    if cancel and job then intent = end_job("Stopped: " .. job.done .. " of " .. job.total .. " moved.") end
    if start_what and not job then
        local started, err
        if start_what == "move" then started, err = start_move(s) else started, err = start_sort(s) end
        if started then job, message = started, "" else message = err end
    end
    if job and not intent and not (pack_name(s, st.source) and (job.give or pack_name(s, job.dest))) then
        intent = end_job("Stopped: storage closed, " .. job.done .. " moved.")
    end

    if not intent and s.ready ~= false then
        if job then
            intent = step(s)
        elseif st.identify_open then
            -- "Identify inventory while open": everything in your packs and the open storage, one at a time, each once
            local items = index_items(s)
            local waiting = idle_identify and items[idle_identify.id] and not items[idle_identify.id].identified
                and (s.time or 0) < idle_identify.until_time
            if not waiting then
                idle_identify = nil
                for _, item in ipairs(all_items(s)) do
                    if not item.identified and not item.equipped and item.object_class ~= CONTAINER and not identify_tried[item.id] then
                        identify_tried[item.id] = true
                        idle_identify = {id=item.id, until_time=(s.time or 0) + 4}
                        intent = {action="identify", item=item.id}
                        break
                    end
                end
            end
        end
    end

    local out = intent or {}
    out.status = job and progress_text() or "Ready"
    out.ui = {{id="tabs", type="tabs", children={move_tab(s), sort_tab(s), build_tab(), options_tab()}}}
    if dirty then out.state = copy(st) end
    return out
end
