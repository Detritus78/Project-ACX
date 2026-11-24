-- Pack Mover (JJ 2026-11-24: "a pack mover by type like jewelry, gem, armor, salvage, or whatever other types there are ...
-- So I can move from main to side pack or side pack to side pack"). Select a pack, or your character for the main pack,
-- and click Set Source; select a side pack and click Set Destination; pick a type and click Move. Items of that type
-- move one at a time; anything that doesn't fit stays. Equipped items, packs and pyreals never move.
-- AC:Unreal lets plugins move items only into your own side packs (store_item), so the main pack can be a source but
-- not a destination.

local TYPES = {
    {"Jewelry", 0x8}, {"Gems", 0x800}, {"Salvage", 0x40000000}, {"Armor", 0x2}, {"Clothing", 0x4},
    {"Weapons (all)", 0x1 + 0x100 + 0x8000}, {"Melee weapons", 0x1}, {"Missile weapons", 0x100}, {"Casters", 0x8000},
    {"Summoning essences", "pet"}, {"Spell components", 0x1000}, {"Mana stones", 0x80000}, {"Food", 0x20},
    {"Keys", 0x4000}, {"Scrolls and books", 0x2000}, {"Trade notes", 0x40000}, {"Alchemy", 0xC00000},
    {"Fletching", 0xA000000}, {"Cooking", 0x400000}, {"Misc", 0x80}, {"Useless", 0x400}, {"Everything", "all"},
}
local TYPE_NAMES = {}
for i, t in ipairs(TYPES) do TYPE_NAMES[i] = t[1] end

local st            -- saved: {source, source_name, dest, dest_name, kind}
local job           -- {queue = {ids}, next, pending, moved, failed, full}
local message = ""
local log = {}

local function note(text)
    table.insert(log, 1, text)
    while #log > 6 do table.remove(log) end
end

local function has_bits(value, mask)
    -- any shared bit, without the bit operators the sandbox may not have
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

local function pack_name(s, id)
    if id == s.player then return "Main pack" end
    for n, bag in ipairs(s.packs or {}) do if bag.id == id then return "Pack " .. n end end
    return nil
end

local function free_slots(s, id)
    for _, bag in ipairs(s.packs or {}) do if bag.id == id then return bag.free or 0 end end
    return 0
end

local function selected_pack(s)
    local sel = s.selection
    if not sel or not sel.id then return nil end
    local name = pack_name(s, sel.id)
    if name then return sel.id, name end
    return nil
end

local function start(s)
    if not st.source then return "Set a source first." end
    if not st.dest then return "Set a destination first." end
    if st.source == st.dest then return "Source and destination are the same pack." end
    local kind = kind_of(st.kind or TYPE_NAMES[1])
    local queue = {}
    for _, item in ipairs(s.inventory or {}) do
        if item.container == st.source and not item.equipped and is_kind(item, kind) then queue[#queue + 1] = item.id end
    end
    if #queue == 0 then return "No " .. string.lower(st.kind or TYPE_NAMES[1]) .. " in " .. st.source_name .. "." end
    if free_slots(s, st.dest) == 0 then return st.dest_name .. " is full." end
    job = {queue = queue, next = 1, moved = 0, failed = 0, sent = {}}
    return nil
end

local function finish(text)
    note(text)
    message = text
    job = nil
end

-- moves go out as fast as the client allows (one store_item a second) without waiting for each to land; landings are
-- counted as they show up, and a move that hasn't landed in 6 s counts as not moved
local function step(s)
    local items = {}
    for _, item in ipairs(s.inventory or {}) do items[item.id] = item end
    local still = {}
    for _, sent in ipairs(job.sent) do
        local item = items[sent.id]
        if item and item.container == st.dest then job.moved = job.moved + 1
        elseif (s.time or 0) >= sent.until_time then job.failed = job.failed + 1
        else still[#still + 1] = sent end
    end
    job.sent = still
    if job.next <= #job.queue and free_slots(s, st.dest) - #job.sent <= 0 then
        if #job.sent > 0 then return nil end
        finish("Moved " .. job.moved .. "; " .. st.dest_name .. " is full, " .. (#job.queue - job.next + 1) .. " stayed.")
        return nil
    end
    if (s.time or 0) >= (job.next_time or 0) then
        while job.next <= #job.queue do
            local id = job.queue[job.next]
            job.next = job.next + 1
            local item = items[id]
            if item and item.container == st.source and not item.equipped then
                job.sent[#job.sent + 1] = {id = id, until_time = (s.time or 0) + 6}
                job.next_time = (s.time or 0) + 1
                return {action = "store_item", item = id, target = st.dest}
            end
        end
    end
    if job.next > #job.queue and #job.sent == 0 then
        finish("Moved " .. job.moved .. (job.failed > 0 and ("; " .. job.failed .. " didn't move") or "") .. ".")
    end
    return nil
end

local function ui(s)
    local function button(id, text, enabled) return {id = id, type = "button", text = text, enabled = enabled ~= false} end
    local function text(id, t) return {id = id, type = "text", text = t} end
    local children = {
        text("help", "Select a pack (or your character for the main pack) and click Set Source. Select a side pack and click Set Destination."),
        {id = "r_set", type = "row", children = {button("b_src", "Set Source", job == nil), button("b_dst", "Set Destination", job == nil)}},
        text("t_src", "From: " .. (st.source_name or "(none)")),
        text("t_dst", "To: " .. (st.dest_name and (st.dest_name .. " (" .. free_slots(s, st.dest) .. " free)") or "(none)")),
        {id = "kind", type = "select", text = "Type", value = st.kind or TYPE_NAMES[1], options = TYPE_NAMES},
        button("b_go", job and "Stop" or "Move"),
        text("t_msg", job and ("Moving " .. (job.moved + job.failed) .. "/" .. #job.queue .. "...") or (message ~= "" and message or " ")),
        text("t_log", #log > 0 and table.concat(log, "\n") or " "),
    }
    return {{id = "tab", type = "tabs", children = {{id = "t_main", type = "column", text = "Pack Mover", children = children}}}}
end

return function(s, p)
    if not st then
        st = {}
        for k, v in pairs(s.state or {}) do st[k] = v end
    end
    local dirty = false
    for _, e in ipairs(s.events or {}) do
        if e.type == "change" and e.id == "kind" then
            st.kind, dirty = e.value, true
        elseif e.type == "click" then
            if e.id == "b_src" or e.id == "b_dst" then
                local id, name = selected_pack(s)
                if not id then message = "Select one of your packs, or your character for the main pack, first."
                elseif e.id == "b_src" then st.source, st.source_name, dirty, message = id, name, true, "From: " .. name
                elseif id == s.player then message = "The main pack can't be a destination: AC:Unreal lets plugins move items into side packs only."
                else st.dest, st.dest_name, dirty, message = id, name, true, "To: " .. name end
            elseif e.id == "b_go" then
                if job then finish("Stopped: " .. job.moved .. " moved.")
                else message = start(s) or "" end
            end
        end
    end
    -- packs are found again each session; a saved pack that's gone is cleared
    if st.source and not pack_name(s, st.source) then st.source, st.source_name, dirty = nil, nil, true end
    if st.dest and not pack_name(s, st.dest) then st.dest, st.dest_name, dirty = nil, nil, true end
    if st.source_name and pack_name(s, st.source) ~= st.source_name then st.source_name, dirty = pack_name(s, st.source), true end
    if st.dest_name and pack_name(s, st.dest) ~= st.dest_name then st.dest_name, dirty = pack_name(s, st.dest), true end

    local out = {}
    if job and s.ready ~= false then out = step(s) or {} end
    out.status = job and ("Moving " .. (job.moved + job.failed) .. "/" .. #job.queue) or "Ready"
    out.ui = ui(s)
    if dirty then out.state = {source = st.source, source_name = st.source_name, dest = st.dest, dest_name = st.dest_name, kind = st.kind} end
    return out
end
