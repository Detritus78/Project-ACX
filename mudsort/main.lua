-- MudSort for AC:Unreal (JJ 2026-11-17: "I need a mudsort plugin ... lets see if we can build it for ac unreal"; "I mostly use
-- it to organize by damage or weight or value or by item to make selling easier"; then, on a settings-list first version:
-- "yeah I don't like that ... look for pictures of mudsort so you can see how its set up").
-- New code that does what Mudzereli's MudSort does (https://github.com/mudzereli/mudsort, MudSort 1.6.7 for Decal,
-- Copyright (c) Mudzereli 2022), laid out like its window: Sort, Build, Options and About tabs, and its sort strings (the
-- same two-letter codes, made by the same rule from the same key lists).
-- AC:Unreal runs a plugin that moves items only while it's started, so Start opens the MudSort window and Stop closes it.
-- Activate sorts: it identifies the items its sort keys need, orders them, and moves them one at a time into the
-- destination pack (each lands at the front, so they're moved in reverse) or hands them to the destination player or NPC.
-- AC:Unreal lets plugins move items into side packs, not into the main pack, or into chests.
-- Commands while open: /acueplugin mudsort start | stop | set source (pack <1-8>|player) | set dest (pack <1-8>) |
-- set flags <sort string> | set ocfilter <class> | clear ocfilter.

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

-- saved per character (state): settings, the three stored sort strings; never object ids (they're per session)
local DEFAULTS = {sort_string="OC,MX-,DB-,ED-,NM", oc_filter="None", oc_custom="", source="player", dest="pack 1",
    saved1="OC,EP,MA,AL-,MX-", saved2="AL-,MX-", saved3="BR-", reverse=false, notify=true, identify_open=false,
    list_filter="Commonly Used Keys"}

local st, dirty                     -- settings, and whether to save them
local clipboard = ""                -- Copy / Paste between the sort string and the stored strings
local give_target                   -- {id, name}: a player or NPC set as destination this session
local page, dump, message = 1, nil, ""
local job                           -- the sort in progress
local idle_identify                 -- {id, until_time} while identifying on open
local identify_tried = {}            -- items already asked for, so one that never answers is not asked again

local function set(key, value)
    if st[key] ~= value then st[key] = value; dirty = true end
end

-- "player", "pack 3", or the id of a pack / the main pack; nil if it isn't one of yours
local function resolve_container(s, text)
    text = trim(tostring(text or "")):lower()
    if text == "player" or text == "main" or text == "main pack" then return s.player, "Main pack" end
    local packs = s.packs or {}
    if starts_with(text, "pack ") then
        local n = tonumber(text:sub(6))
        if n and packs[n] then return packs[n].id, "Pack " .. n end
        return nil
    end
    local id = tonumber(text)
    if id == s.player then return s.player, "Main pack" end
    for n, bag in ipairs(packs) do if bag.id == id then return bag.id, "Pack " .. n end end
    return nil
end

local function selection_as_container(s)
    local sel = s.selection
    if not sel or not sel.id then return nil end
    if sel.id == s.player then return "player" end
    for n, bag in ipairs(s.packs or {}) do if bag.id == sel.id then return "pack " .. n end end
    return nil
end

local function oc_filter_name()
    local choice = st.oc_filter or "None"
    if choice == "Custom" then return trim(st.oc_custom or ""):lower() end
    if choice == "None" or choice:sub(1, 1) == "#" then return "" end
    return choice:lower()
end

-- the Activate button: everything a sort needs, or why it can't run
local function start_job(s)
    local keys, unknown = parse_sort(st.sort_string)
    if #keys == 0 then return nil, "Set a sort string first (Sort tab, or build one on the Build tab)." end
    local src_id, src_name = resolve_container(s, st.source)
    if not src_id then return nil, "Source isn't one of your packs. Select your character or a side pack and click Set Source." end

    local dest
    if st.dest == "give" then
        if not give_target then return nil, "Select the player or NPC and click Set Dest again (it isn't kept between sessions)." end
        dest = {give=true, id=give_target.id, name=give_target.name}
    else
        local id, name = resolve_container(s, st.dest)
        if not id then return nil, "Destination isn't one of your side packs. Select one and click Set Dest." end
        if id == s.player then return nil, "AC:Unreal lets plugins move items into side packs only, not the main pack. Pick a side pack." end
        local free = 0
        for _, bag in ipairs(s.packs or {}) do if bag.id == id then free = bag.free or 0 end end
        dest = {give=false, id=id, name=name, free=free}
    end

    local filter = oc_filter_name()
    local list, incoming = {}, 0
    for _, item in ipairs(s.inventory or {}) do
        local class = item.object_class or 0
        local class_name = (CLASS_NAMES[class] or "Unknown"):lower()
        if item.container == src_id and not item.equipped and class ~= CONTAINER and class ~= FOCI
            and (filter == "" or starts_with(class_name, filter)) then
            list[#list + 1] = item.id
            if not dest.give and item.container ~= dest.id then incoming = incoming + 1 end
        end
    end
    if #list == 0 then return nil, "Nothing to sort in " .. src_name .. (filter ~= "" and (" (filter: " .. filter .. ")") or "") .. "." end

    local identify = false
    for _, k in ipairs(keys) do if needs_identify(k) then identify = true end end
    local note = ""
    if #unknown > 0 then note = "Skipped unknown codes: " .. table.concat(unknown, ", ") .. ". " end
    if not dest.give and dest.free < incoming then note = note .. dest.name .. " has room for " .. dest.free .. " of " .. incoming .. ". " end
    return {keys=keys, list=list, dest=dest, src_name=src_name, stage=identify and "identify" or "order", next=1,
        done=0, failed=0, streak=0, total=#list, note=note, reverse=st.reverse}
end

local function index_inventory(s)
    local by_id = {}
    for _, item in ipairs(s.inventory or {}) do by_id[item.id] = item end
    return by_id
end

local function order_job(s)
    local items = index_inventory(s)
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
    -- "Completely reverse sort list"
    if job.reverse then
        local flipped = {}
        for n = #rows, 1, -1 do flipped[#flipped + 1] = rows[n] end
        rows = flipped
    end
    -- into a pack each item lands at the front, so the last one moved ends up first: move them in reverse
    job.queue = {}
    for n = 1, #rows do job.queue[n] = job.dest.give and rows[n] or rows[#rows + 1 - n] end
end

local function end_job(text)
    message = text
    local notice = st.notify and job and {action="notice", text="MudSort: " .. text} or nil
    job = nil
    return notice
end

-- one step of the sort; returns an intent or nil
local function step_job(s)
    local items = index_inventory(s)
    if job.stage == "identify" then
        while job.next <= #job.list do
            local id = job.list[job.next]
            local item = items[id]
            if not item or item.identified then
                job.next, job.pending = job.next + 1, nil
            elseif job.pending == id and (s.time or 0) < job.until_time then
                return nil
            elseif job.pending == id then
                job.next, job.pending = job.next + 1, nil   -- no answer in time: sort it with what's known
            else
                job.pending, job.until_time = id, (s.time or 0) + 4
                return {action="identify", item=id}
            end
        end
        job.stage, job.pending = "order", nil
    end
    if job.stage == "order" then
        order_job(s)
        job.stage, job.next = "move", 1
    end
    if job.pending then
        local item = items[job.pending.id]
        local moved
        if job.dest.give then moved = not item or (item.count or 1) < job.pending.count
        else moved = item and item.container == job.dest.id end
        if moved then
            job.done, job.streak, job.pending = job.done + 1, 0, nil
        elseif (s.time or 0) >= job.until_time then
            job.failed, job.streak, job.pending = job.failed + 1, job.streak + 1, nil
            if job.streak >= 3 then return end_job("Stopped: three items in a row didn't move (" .. job.dest.name .. " full, or out of reach?).") end
        else
            return nil
        end
    end
    while job.next <= #job.queue do
        local row = job.queue[job.next]
        job.next = job.next + 1
        local item = items[row.id]
        if item and not item.equipped then
            job.pending, job.until_time = {id=row.id, count=item.count or 1}, (s.time or 0) + 8
            if job.dest.give then return {action="give", item=row.id, target=job.dest.id, amount=item.count or 1} end
            return {action="store_item", item=row.id, target=job.dest.id}
        end
    end
    local text = "done sorting! " .. job.done .. " of " .. job.total .. (job.dest.give and (" given to " .. job.dest.name) or (" moved to " .. job.dest.name))
    if job.failed > 0 then text = text .. ", " .. job.failed .. " didn't move" end
    return end_job(text .. ".")
end

-- "Property Dump Selection": every sortable property of the selected item, by MudSort code
local function property_dump(s)
    local sel = s.selection
    if not sel or not sel.id then return "Select an item first." end
    local item = index_inventory(s)[sel.id]
    if not item then return "Select one of your own items." end
    local lines = {(item.name or "?") .. (item.identified and "" or "  (not identified yet: open it once, or Activate a sort)")}
    for _, row in ipairs(CODES) do
        local key = BY_CODE[row[1]]
        local v = value_of(key, item)
        if v ~= nil and v ~= "" then lines[#lines + 1] = key.code .. "  " .. key.name .. ": " .. fmt(v) end
    end
    return table.concat(lines, "\n")
end

local function handle_command(s, text)
    text = trim(text or "")
    local lower = text:lower()
    if lower == "start" then return "activate" end
    if lower == "stop" or lower == "cancel" then return "cancel" end
    if starts_with(lower, "set source") then
        local arg = trim(text:sub(11))
        if arg == "" then arg = selection_as_container(s) or "" end
        if resolve_container(s, arg) then set("source", arg:lower()); message = "Source: " .. arg else message = "Not one of your packs: " .. arg end
    elseif starts_with(lower, "set dest") then
        local arg = trim(text:sub(9))
        if arg == "" then arg = selection_as_container(s) or "" end
        if resolve_container(s, arg) then set("dest", arg:lower()); message = "Destination: " .. arg else message = "Not one of your side packs: " .. arg end
    elseif starts_with(lower, "set flags ") then set("sort_string", trim(text:sub(11))); message = "Sort string set."
    elseif starts_with(lower, "set ocfilter ") then set("oc_filter", "Custom"); set("oc_custom", trim(text:sub(14))); message = "Object class filter set."
    elseif lower == "clear ocfilter" or lower == "set ocfilter" then set("oc_filter", "None"); message = "Object class filter cleared."
    else message = "Commands: start, stop, set source (pack <1-8>|player), set dest (pack <1-8>), set flags <string>, set ocfilter <class>, clear ocfilter" end
    return nil
end

local function button(id, text, tooltip, enabled)
    return {id=id, type="button", text=text, tooltip=tooltip, enabled=enabled ~= false}
end

local function container_label(s, which)
    local text = st[which]
    if which == "dest" and text == "give" then return give_target and (give_target.name .. " (" .. string.format("%.0f", give_target.id) .. ")") or "player or NPC: set it again" end
    local id, name = resolve_container(s, text)
    return id and (name .. " (" .. string.format("%.0f", id) .. ")") or (text .. ": not found")
end

local function sort_tab(s)
    local running = job ~= nil
    local progress, label = 0, "idle"
    if job then
        if job.stage == "identify" then progress, label = (job.next - 1) / math.max(1, #job.list), "identifying " .. (job.next) .. " of " .. #job.list .. "..."
        else progress, label = job.done / math.max(1, job.total), "working... " .. job.done .. " of " .. job.total end
    elseif message ~= "" then progress, label = 1, message end
    local children = {
        {id="s_head", text="Select Source Range"},
        {id="s_src_row", type="row", children={
            button("s_set_src", "Set Source", "Your character (main pack) or a side pack: select it, then click"),
            {id="s_src", type="input", value=container_label(s, "source"), tooltip="player, pack 1-8, or select and click Set Source"}}},
        {id="s_dst_row", type="row", children={
            button("s_set_dst", "Set Dest", "A side pack, or another player or NPC to give the items to: select it, then click"),
            {id="s_dst", type="input", value=container_label(s, "dest"), tooltip="pack 1-8, or select and click Set Dest"}}},
        {id="s_ins_row", type="row", children={
            {id="s_ins_label", text="Insert Position"},
            {id="s_ins", text="0 (AC:Unreal always inserts at the front)"}}},
        {id="s_str_row", type="row", children={
            {id="s_str_label", text="Sort String"},
            button("s_copy", "Copy", "Copy the sort string (paste it into a stored slot on Options)"),
            button("s_paste", "Paste", "Paste the copied string here")}},
        {id="s_str", type="input", value=st.sort_string, tooltip="MudSort codes, comma separated; - after a code = highest first"},
        {id="s_oc", type="select", text="Object Class Filter", value=st.oc_filter, options=OC_OPTIONS},
    }
    if st.oc_filter == "Custom" then
        children[#children + 1] = {id="s_oc_custom", type="input", text="Class starts with", value=st.oc_custom, tooltip="e.g. manast for ManaStone"}
    end
    children[#children + 1] = {id="s_progress", type="progress", text=label, value=progress}
    children[#children + 1] = button("s_activate", running and "Cancel" or "Activate", running and "Stop sorting" or "Begin sorting")
    if job and job.note ~= "" then children[#children + 1] = {id="s_note", text=job.note, color="yellow"} end
    return {id="t_sort", type="column", text="Sort", children=children}
end

local function build_tab(s)
    local keys = parse_sort(st.sort_string)
    local added = {}
    for _, k in ipairs(keys) do added[k.code] = true end
    local children = {
        {id="b_head", text="Build a Sort String"},
        {id="b_filter", type="select", text="Sort List Filter", value=st.list_filter, options=LIST_FILTERS},
        {id="b_cols", text="Code  Name                         Up  Dn  A/R  Ord  Key", color="yellow"},
    }
    for n, k in ipairs(keys) do
        if n > MAX_ADDED then break end
        children[#children + 1] = {id="b_row_" .. k.code, type="row", children={
            {id="b_name_" .. k.code, text=k.code .. "  " .. k.name, wrap=false},
            button("b_up_" .. k.code, "Up", "Move up", n > 1),
            button("b_dn_" .. k.code, "Dn", "Move down", n < #keys),
            button("b_rm_" .. k.code, "Rem", "Remove from the sort string"),
            button("b_ord_" .. k.code, k.descending and "Hi" or "Lo", k.descending and "Highest first: click for lowest first" or "Lowest first: click for highest first"),
            {id="b_key_" .. k.code, text=KIND_LETTER[k.kind] or "?"}}}
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
            {id="b_name_" .. key.code, text=key.code .. "  " .. key.name, wrap=false},
            button("b_add_" .. key.code, "Add", "Add to the sort string (lowest first; click Lo/Hi to flip)"),
            {id="b_key_" .. key.code, text=KIND_LETTER[key.kind] or "?"}}}
    end
    children[#children + 1] = {id="b_pages", type="row", children={
        button("b_prev", "Prev", nil, page > 1),
        {id="b_page", text="Page " .. page .. " of " .. pages, align="center"},
        button("b_next", "Next", nil, page < pages)}}
    return {id="t_build", type="column", text="Build", children=children}
end

local function options_tab()
    local children = {
        {id="o_identify", type="checkbox", text="Identify inventory when MudSort opens.", value=st.identify_open},
        {id="o_reverse", type="checkbox", text="Completely reverse sort list.", value=st.reverse},
        {id="o_notify", type="checkbox", text="Tell me in chat when complete.", value=st.notify},
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
    children[#children + 1] = {id="o_saved_note", text="Settings save by themselves, per character."}
    return {id="t_options", type="column", text="Options", children=children}
end

local function about_tab()
    return {id="t_about", type="column", text="About", children={
        {id="a_1", text="MudSort for AC:Unreal: a port of Mudzereli's MudSort for Decal (github.com/mudzereli/mudsort)."},
        {id="a_2", text="AC:Unreal moves items into side packs only, and pauses UCM while MudSort is open."}}}
end

-- the Build tab's edits, by key code
local function edit_keys(action, code)
    local keys = parse_sort(st.sort_string)
    local at
    for n, k in ipairs(keys) do if k.code == code then at = n end end
    if action == "add" and not at then
        local key = BY_CODE[code]
        keys[#keys + 1] = {code=key.code, descending=false}
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
    local intent, activate, cancel

    for _, e in ipairs(s.events or {}) do
        local id = e.id or ""
        if e.type == "command" then
            local what = handle_command(s, e.text)
            if what == "activate" then activate = true elseif what == "cancel" then cancel = true end
        elseif e.type == "change" then
            if id == "s_str" then set("sort_string", trim(tostring(e.value or "")))
            elseif id == "s_src" then
                if resolve_container(s, e.value) then set("source", trim(tostring(e.value)):lower()) else message = "Source: type player or pack 1-8" end
            elseif id == "s_dst" then
                if resolve_container(s, e.value) then set("dest", trim(tostring(e.value)):lower()) else message = "Dest: type pack 1-8, or select a player and click Set Dest" end
            elseif id == "s_oc" then set("oc_filter", tostring(e.value))
            elseif id == "s_oc_custom" then set("oc_custom", trim(tostring(e.value or "")))
            elseif id == "b_filter" then set("list_filter", tostring(e.value)); page = 1
            elseif id == "o_identify" then set("identify_open", e.value == true)
            elseif id == "o_reverse" then set("reverse", e.value == true)
            elseif id == "o_notify" then set("notify", e.value == true)
            elseif starts_with(id, "o_saved") then set("saved" .. id:sub(8), tostring(e.value or "")) end
        elseif e.type == "click" then
            if id == "s_activate" then if job then cancel = true else activate = true end
            elseif id == "s_set_src" then
                local which = selection_as_container(s)
                if which then set("source", which); message = "Source: " .. which
                else message = "Select your character or one of your side packs, then click Set Source." end
            elseif id == "s_set_dst" then
                local sel = s.selection
                local which = selection_as_container(s)
                if which == "player" then message = "AC:Unreal can't move items into the main pack: pick a side pack."
                elseif which then set("dest", which); give_target = nil; message = "Destination: " .. which
                elseif sel and sel.id and sel.object_type ~= "item" then
                    set("dest", "give"); give_target = {id=sel.id, name=sel.name or "them"}; message = "Destination: give to " .. (sel.name or "them")
                else message = "Select a side pack, or a player or NPC, then click Set Dest." end
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

    if cancel and job then intent = end_job("cancelled: " .. job.done .. " of " .. job.total .. " moved.") end
    if activate and not job then
        local started, err = start_job(s)
        if started then job, message = started, "" else message = err end
    end

    if not intent and s.ready ~= false then
        if job then
            intent = step_job(s)
        elseif st.identify_open then
            -- "Identify inventory when MudSort opens": everything in your packs, one item at a time, each tried once
            local items = index_inventory(s)
            local waiting = idle_identify and items[idle_identify.id] and not items[idle_identify.id].identified
                and (s.time or 0) < idle_identify.until_time
            if not waiting then
                idle_identify = nil
                for _, item in ipairs(s.inventory or {}) do
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
    out.status = job and "Sorting..." or (message ~= "" and message or "Ready")
    out.ui = {{id="tabs", type="tabs", children={sort_tab(s), build_tab(s), options_tab(), about_tab()}}}
    if dirty then out.state = copy(st) end
    return out
end
