-- Pack Mover (Project ACX test, 2026-11-18; JJ: "write a one tab plugin that moves from a targeted [pack] to another
-- ... it doesn't care what pack it is. even the main pack. Just a small simple mover"). Select a pack (or your character
-- for the main pack), click Set Source; select another, click Set Destination; Move Weapons moves every melee, missile
-- and caster weapon from the one to the other, one at a time, and says which moves the client or server refused.
-- Nothing is blocked here on purpose: the main pack is allowed both ways, so the test shows what AC:Unreal really does.

local WEAPON = {[1] = "melee", [9] = "missile", [31] = "caster"}   -- object_class (Decal's ObjectClass)

local st            -- {source=id, source_name, dest=id, dest_name}
local job           -- {queue={ids}, next, pending, until_time, moved, failed, reasons}
local message = ""
local log = {}

local function note(text)
    table.insert(log, 1, text)
    while #log > 8 do table.remove(log) end
end

local function pack_name(s, id)
    if id == s.player then return "Main pack" end
    for n, bag in ipairs(s.packs or {}) do if bag.id == id then return "Pack " .. n end end
    return nil
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
    local queue = {}
    for _, item in ipairs(s.inventory or {}) do
        if item.container == st.source and not item.equipped and WEAPON[item.object_class or 0] then
            queue[#queue + 1] = item.id
        end
    end
    if #queue == 0 then return "No weapons in " .. st.source_name .. "." end
    job = {queue = queue, next = 1, moved = 0, failed = 0}
    note("Moving " .. #queue .. " weapons: " .. st.source_name .. " -> " .. st.dest_name)
    return nil
end

local function step(s)
    local items = {}
    for _, item in ipairs(s.inventory or {}) do items[item.id] = item end
    if job.pending then
        local item = items[job.pending.id]
        if item and item.container == st.dest then
            job.moved, job.pending = job.moved + 1, nil
        elseif s.activity_failure and s.activity_failure.status and job.pending.reported ~= true and (s.time or 0) > job.pending.sent then
            -- the client refused it before sending: say why (e.g. "AutoCram destination is unavailable or full")
            note("Refused: " .. (job.pending.name or "?") .. " - " .. tostring(s.activity_failure.status))
            job.failed, job.pending = job.failed + 1, nil
        elseif (s.time or 0) >= job.pending.until_time then
            note("Didn't move: " .. (job.pending.name or "?") .. " (no change in 6 s)")
            job.failed, job.pending = job.failed + 1, nil
        else
            return nil
        end
    end
    while job.next <= #job.queue do
        local id = job.queue[job.next]
        job.next = job.next + 1
        local item = items[id]
        if item and item.container == st.source and not item.equipped then
            job.pending = {id = id, name = item.name, sent = s.time or 0, until_time = (s.time or 0) + 6}
            return {action = "store_item", item = id, target = st.dest}
        end
    end
    note("Done: " .. job.moved .. " moved, " .. job.failed .. " not.")
    job = nil
    return nil
end

local function ui(s)
    local function button(id, text, enabled) return {id = id, type = "button", text = text, enabled = enabled ~= false} end
    local function text(id, t) return {id = id, type = "text", text = t} end
    local children = {
        text("help", "Select a pack, or your character for the main pack, then click Set Source or Set Destination."),
        {id = "r_set", type = "row", children = {button("b_src", "Set Source"), button("b_dst", "Set Destination")}},
        text("t_src", "Source: " .. (st.source_name or "(none)")),
        text("t_dst", "Destination: " .. (st.dest_name or "(none)")),
        {id = "r_go", type = "row", children = {button("b_go", job and "Stop" or "Move Weapons")}},
        text("t_msg", message ~= "" and message or " "),
        text("t_log", #log > 0 and table.concat(log, "\n") or "(nothing yet)"),
    }
    return {{id = "tab", type = "tabs", children = {{id = "t_main", type = "column", text = "Mover", children = children}}}}
end

return function(s, p)
    if not st then
        st = {}
        for k, v in pairs(s.state or {}) do st[k] = v end
    end
    local dirty = false
    for _, e in ipairs(s.events or {}) do
        if e.type == "click" then
            if e.id == "b_src" or e.id == "b_dst" then
                local id, name = selected_pack(s)
                if not id then message = "Select one of your packs, or your character for the main pack, first."
                elseif e.id == "b_src" then st.source, st.source_name, dirty, message = id, name, true, "Source: " .. name
                else st.dest, st.dest_name, dirty, message = id, name, true, "Destination: " .. name end
            elseif e.id == "b_go" then
                if job then note("Stopped: " .. job.moved .. " moved."); job = nil
                else message = start(s) or "" end
            end
        end
    end
    -- packs are re-identified each session; a saved name for a pack that's gone is cleared
    if st.source and not pack_name(s, st.source) then st.source, st.source_name, dirty = nil, nil, true end
    if st.dest and not pack_name(s, st.dest) then st.dest, st.dest_name, dirty = nil, nil, true end

    local out = {}
    if job and s.ready ~= false then out = step(s) or {} end
    out.status = job and ("Moving " .. (job.moved + job.failed) .. "/" .. #job.queue) or "Ready"
    out.ui = ui(s)
    if dirty then out.state = {source = st.source, source_name = st.source_name, dest = st.dest, dest_name = st.dest_name} end
    return out
end
