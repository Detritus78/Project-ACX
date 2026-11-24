-- Auto Sell (Project ACX, 2026-11-19). JJ's user story: "I have been out hunting for a couple of hrs. I have full packs.
-- It's time to sell ... This app tries to sell everything except what's in the exceptions or what is imbued or has special
-- properties or has a inscription or has been tinkered or matches a rule to a loot profile." JJ's answers: "Set it up so I
-- can make the rules ... More user control is better", supplies are sold too unless excepted, unassessed items are skipped,
-- and a .utl or an exception list can protect items. With a vendor open, items it won't buy (the client's can_sell: the
-- vendor's buy categories, and the item tradeable) go to "This vendor doesn't buy" instead of Will sell.
--
-- An item sells when it matches the Sell list and no Exception, and the optional .utl profile doesn't protect it (its first
-- matching rule decides: Sell lets it go, any other action keeps it). Always kept: equipped, retained, packs, pyreals, and
-- gear not assessed yet (other items go by name, type and value). Sales go out back to back through Item Tools' it_sell (0.1 s apart), up to 10 awaiting the server;
-- one that doesn't leave the packs within 6 s (the vendor won't buy it) is counted as not sold and the rest carry on.

local KINDS = {"Everything", "Name contains", "Name is", "Spell contains", "Item type", "Value at least", "Value below",
    "Imbued", "Tinkered", "Inscribed", "Set item", "Rare", "Attuned", "Bonded", "Has spells", "Summoning essence"}
local NEEDS_TEXT = {["Name contains"] = true, ["Name is"] = true, ["Spell contains"] = true}
local NEEDS_NUMBER = {["Value at least"] = true, ["Value below"] = true}
local TYPES = {
    {"Melee weapon", 0x1}, {"Missile weapon", 0x100}, {"Caster", 0x8000}, {"Armor", 0x2}, {"Clothing", 0x4},
    {"Jewelry", 0x8}, {"Gem", 0x800}, {"Food", 0x20}, {"Mana stone", 0x80000}, {"Spell components", 0x1000},
    {"Trade note", 0x40000}, {"Key", 0x4000}, {"Salvage", 0x40000000}, {"Misc", 0x80}, {"Useless", 0x400},
    {"Writable", 0x2000}, {"Alchemy", 0xC00000}, {"Fletching", 0xA000000}, {"Cooking", 0x400000},
}
local TYPE_NAMES = {}
for i, t in ipairs(TYPES) do TYPE_NAMES[i] = t[1] end

-- weapons, casters, armor, clothing, jewelry: the only things that can be imbued, tinkered, inscribed or carry spells, so the
-- only things that must be assessed before selling (JJ: peas and the like are never assessed and were never sold)
local GEAR = 0x1 + 0x100 + 0x8000 + 0x2 + 0x4 + 0x8

local DEFAULT_SELL = {{kind = "Everything", on = true}}
local DEFAULT_KEEP = {
    {kind = "Imbued", on = true}, {kind = "Tinkered", on = true}, {kind = "Inscribed", on = true},
    {kind = "Set item", on = true}, {kind = "Rare", on = true},
}

local st            -- saved: {sell = rules, keep = rules, profile = ".utl file name" or UCM_PREFIX .. UCM loot profile name}
local profile_rules -- the loaded profile as UCM rules (nil when none)
local profile_note = ""
local profile_files = {}
local ucm_profiles = {}
-- a UCM loot profile is read through Auto Sell's own Profiles/autosell/Library/Loot, which is a link to UCM's
-- Profiles/Loot (JJ 2026-11-20: "the profile needs to match across both plugins"), so it is the very file UCM loots by
local UCM_PREFIX = "UCM loot: "
local requests = {}
local draft = {sell = {kind = "Name contains", text = "", type = TYPE_NAMES[1]}, keep = {kind = "Name contains", text = "", type = TYPE_NAMES[1]}}
local plan          -- {i, cursor = {rule, scan}, sell = {}, keep = {}, skipped = {}, then_sell, vendor}
local job           -- {queue, next, sent, sold, failed, vendor}
local message = ""
local will_names = {}    -- the Will sell buttons' names, by button number
-- names skipped for this sale only (JJ 2026-11-24: "click an item and it just removes it from that instance of selling ...
-- I don't want to sale all my ghost essences yet"); cleared when the sale finishes or the vendor window changes
local skip_names = {}
local unskip_names = {}  -- the Skipped this sale buttons' names, by button number
local last_vendor
local CLICK_MODES = {"Remove from this sale", "Keep always (Exception)"}
local log = {}

local function note(text)
    table.insert(log, 1, text)
    while #log > 8 do table.remove(log) end
end

local function copy_rules(rules)
    local out = {}
    for i, r in ipairs(rules) do out[i] = {kind = r.kind, text = r.text, value = r.value, type = r.type, on = r.on} end
    return out
end

local function lower(t) return string.lower(t or "") end
local function contains(text, part) return part ~= "" and string.findplain(lower(text), lower(part)) ~= nil end
local function has_bits(value, mask)
    -- any shared bit, without the bit operators the sandbox may not have
    local bit = 1
    while bit <= mask and bit <= 0x80000000 do
        if math.floor(mask / bit) % 2 == 1 and math.floor((value or 0) / bit) % 2 == 1 then return true end
        bit = bit * 2
    end
    return false
end
local function int(item, key) return (item.int_properties or {})[tostring(key)] or 0 end

local function type_mask(name)
    for _, t in ipairs(TYPES) do if t[1] == name then return t[2] end end
    return 0
end

local function describe(r)
    if NEEDS_TEXT[r.kind] then return r.kind .. ": " .. (r.text or "") end
    if NEEDS_NUMBER[r.kind] then return r.kind .. ": " .. tostring(r.value or 0) end
    if r.kind == "Item type" then return "Item type: " .. (r.type or "?") end
    return r.kind
end

local function matches(r, item)
    local k = r.kind
    if k == "Everything" then return true end
    if k == "Name contains" then return contains(item.name, r.text or "") end
    if k == "Name is" then return lower(item.name) == lower(r.text) end
    if k == "Item type" then return has_bits(item.type, type_mask(r.type)) end
    if k == "Value at least" then return (item.value or 0) >= (r.value or 0) end
    if k == "Value below" then return (item.value or 0) < (r.value or 0) end
    if k == "Spell contains" then
        for _, name in ipairs(item.spell_names or {}) do if contains(name, r.text or "") then return true end end
        return false
    end
    if k == "Imbued" then return int(item, 179) ~= 0 end
    if k == "Tinkered" then return int(item, 171) > 0 end
    if k == "Inscribed" then return ((item.string_properties or {})["7"] or "") ~= "" end
    if k == "Set item" then return int(item, 265) > 0 end
    if k == "Rare" then return int(item, 17) > 0 end
    if k == "Attuned" then return int(item, 114) > 0 end
    if k == "Bonded" then return int(item, 33) ~= 0 end
    if k == "Has spells" then return #(item.spell_names or {}) > 0 or #(item.spell_ids or {}) > 0 end
    if k == "Summoning essence" then return item.pet_device == true end
    return false
end

-- the Sell page's "Only sell" box (JJ 2026-11-24, replacing the Sell list tab): blank sells everything; otherwise only items
-- whose name contains one of its comma-separated words ("Essence", "Essence, Scarab")
local function trim(t)
    local a, b = 1, #t
    while a <= b and string.sub(t, a, a) == " " do a = a + 1 end
    while b >= a and string.sub(t, b, b) == " " do b = b - 1 end
    return string.sub(t, a, b)
end

local function only_sell(item)
    local text = st.only or ""
    local any, start = false, 1
    while start <= #text + 1 do
        local comma = string.findplain(text, ",", start)
        local word = trim(string.sub(text, start, comma and (comma - 1) or #text))
        if word ~= "" then
            any = true
            if contains(item.name, word) then return true end
        end
        if not comma then break end
        start = comma + 1
    end
    return not any
end

local function first_match(rules, item)
    for _, r in ipairs(rules) do if r.on and matches(r, item) then return r end end
    return nil
end

-- the first pass of a scan: everything except the .utl, which may need several callbacks
local function is_ucm(name) return name and string.sub(name, 1, #UCM_PREFIX) == UCM_PREFIX end

local function load_profile(name)
    if is_ucm(name) then
        requests[#requests + 1] = {id = "ucmread", service = "profiles", operation = "read", group = "Loot",
            name = string.sub(name, #UCM_PREFIX + 1), field = "loot_rules"}
        profile_note = "Loading " .. name .. "..."
    elseif name and name ~= "" and name ~= "(none)" then
        requests[#requests + 1] = {id = "convert", service = "import", operation = "convert", name = name}
        profile_note = "Loading " .. name .. "..."
    else
        profile_rules, profile_note = nil, "No profile: only your lists decide."
    end
end

local function begin_scan(s, then_sell)
    local packs = {}
    for _, bag in ipairs(s.packs or {}) do packs[bag.id] = true end
    local todo, skipped, keep, elsewhere = {}, {}, {}, {}
    local open = s.vendor and s.vendor ~= 0   -- can_sell (this vendor buys it, and it can be traded) only means something then
    for _, item in ipairs(s.inventory or {}) do
        local why
        if item.equipped then why = nil -- worn gear isn't listed at all
        elseif packs[item.id] or has_bits(item.type, 0x200) then why = nil
        elseif item.wcid == 273 or has_bits(item.type, 0x40) then why = nil
        elseif item.retained then why = "retained"
        elseif not item.identified and has_bits(item.type, GEAR) then why = "not assessed"
        elseif open and item.can_sell == false then
            elsewhere[#elsewhere + 1] = {name = item.name}
        else
            if only_sell(item) then
                local r = first_match(st.keep, item)
                if skip_names[item.name] then keep[#keep + 1] = {name = item.name, why = "skipped this sale"}
                elseif r then keep[#keep + 1] = {name = item.name, why = describe(r)}
                else todo[#todo + 1] = item end
            end
        end
        if why then skipped[#skipped + 1] = {name = item.name, why = why} end
    end
    plan = {i = 1, todo = todo, sell = {}, keep = keep, skipped = skipped, elsewhere = elsewhere, then_sell = then_sell, vendor = s.vendor}
    -- UCM's profile may have been edited since it was loaded: read it again and wait for it
    if is_ucm(st.profile) then plan.waiting = true; load_profile(st.profile) end
end

-- the .utl pass: lootmatch may answer "pending" (out of work for this callback), so it resumes where it stopped
local function continue_scan(s)
    if plan.waiting then return false end
    while plan.i <= #plan.todo do
        local item = plan.todo[plan.i]
        local verdict = "sell"
        if profile_rules then
            plan.cursor = plan.cursor or {rule = 1, scan = {}}
            while plan.cursor.rule <= #profile_rules do
                local r = profile_rules[plan.cursor.rule]
                local result = lootmatch(r, item, s, plan.cursor.scan)
                if result == "pending" then return false end
                if result == true then
                    -- Skip means "not wanted" (UCM leaves it on the corpse), so like Sell it lets the item go
                    verdict = (r.action == "sell" or r.action == "skip") and "sell" or ("profile: " .. (r.label or r.name or r.action or "rule"))
                    break
                elseif result == nil then
                    verdict = "profile needs assessment"
                    break
                end
                plan.cursor.rule, plan.cursor.scan = plan.cursor.rule + 1, {}
            end
            plan.cursor = nil
        end
        if verdict == "sell" then
            plan.sell[#plan.sell + 1] = {id = item.id, name = item.name, pays = (item.sell_unit or 0) * math.max(1, item.count or 1)}
        else plan.keep[#plan.keep + 1] = {name = item.name, why = verdict} end
        plan.i = plan.i + 1
    end
    return true
end

local function start_selling(s)
    if not s.vendor or s.vendor == 0 then message = "Open a vendor first."; return end
    if #plan.sell == 0 then message = "Nothing to sell."; return end
    local queue = {}
    for _, f in ipairs(plan.sell) do queue[#queue + 1] = f.id end
    job = {queue = queue, next = 1, sent = {}, sold = 0, failed = 0, vendor = s.vendor}
    note("Selling " .. #queue .. " items to " .. (s.vendor_name or "the vendor"))
end

local function sell_step(s)
    if s.vendor ~= job.vendor then
        note("Vendor closed: " .. job.sold .. " sold, stopped.")
        job = nil
        return nil
    end
    local items = {}
    for _, item in ipairs(s.inventory or {}) do items[item.id] = item end
    local still = {}
    for _, sent in ipairs(job.sent) do
        if not items[sent.id] then job.sold = job.sold + 1
        elseif (s.time or 0) >= sent.until_time then
            note("Not sold: " .. (sent.name or "?"))
            job.failed = job.failed + 1
        else still[#still + 1] = sent end
    end
    job.sent = still
    while job.next <= #job.queue and #job.sent < 10 do
        local id = job.queue[job.next]
        job.next = job.next + 1
        local item = items[id]
        if item and not item.equipped and not item.retained then
            job.sent[#job.sent + 1] = {id = id, name = item.name, until_time = (s.time or 0) + 6}
            return {action = "it_sell", item = id, vendor = job.vendor, amount = math.max(1, item.count or 1)}
        end
    end
    if job.next > #job.queue and #job.sent == 0 then
        note("Done: " .. job.sold .. " sold, " .. job.failed .. " not.")
        plan, job = nil, nil
        skip_names = {}
    end
    return nil
end

local function selected_item(s)
    local id = s.selected or (s.selection and s.selection.id)
    for _, item in ipairs(s.inventory or {}) do if item.id == id then return item end end
    return nil
end

local function handle(s, e, dirty)
    local id = e.id or ""
    local side = (string.sub(id, 1, 1) == "s") and "sell" or "keep"
    local rules = st[side]
    local tail = string.sub(id, 2)
    if e.type == "change" then
        if tail == "_kind" then draft[side].kind = e.value
        elseif tail == "_text" then draft[side].text = e.value
        elseif tail == "_type" then draft[side].type = e.value
        elseif tail == "_pick" then
            local dot = string.findplain(e.value or "", ".")
            draft[side].pick = dot and tonumber(string.sub(e.value, 1, dot - 1)) or 1
        elseif string.sub(tail, 1, 4) == "_on_" then
            local n = tonumber(string.sub(tail, 5))
            if rules[n] then rules[n].on = e.value == true or e.value == "true"; return true end
        elseif id == "m_click_mode" then
            st.click_mode = e.value; return true
        elseif id == "m_only" then
            st.only = e.value; return true
        elseif id == "profile_pick" then
            st.profile = e.value; load_profile(e.value); return true
        end
        return dirty
    end
    if e.type ~= "click" then return dirty end
    if string.sub(id, 1, 2) == "w_" and plan and not job then
        -- a Will sell entry: keep every item of that name from now on (an exception), and take them out of this sale
        local name = will_names[tonumber(string.sub(id, 3)) or 0]
        if not name then return dirty end
        local always = st.click_mode == CLICK_MODES[2]
        if always then st.keep[#st.keep + 1] = {kind = "Name is", text = name, on = true}
        else skip_names[name] = true end
        local rest = {}
        for _, f in ipairs(plan.sell) do
            if f.name == name then plan.keep[#plan.keep + 1] = {name = f.name, why = always and ("Name is " .. name) or "skipped this sale"}
            else rest[#rest + 1] = f end
        end
        plan.sell = rest
        message = always and ("Kept from now on: " .. name) or ("Skipped for this sale: " .. name)
        return true
    end
    if id == "b_unskip" or (string.sub(id, 1, 2) == "u_" and not job) then
        if id == "b_unskip" then skip_names = {}; message = "All put back; scanning again..."
        else
            local name = unskip_names[tonumber(string.sub(id, 3)) or 0]
            if not name then return dirty end
            skip_names[name] = nil
            message = "Put back: " .. name .. "; scanning again..."
        end
        if plan and not job then begin_scan(s, false) end
        return dirty
    end
    if id == "b_scan" then if not job then begin_scan(s, false); message = "Scanning..." end
    elseif id == "b_sell" then
        if job then note("Stopped: " .. job.sold .. " sold."); plan, job = nil, nil
        elseif not s.vendor or s.vendor == 0 then message = "Open a vendor first."
        else begin_scan(s, true); message = "Scanning..." end
    elseif id == "profile_refresh" then
        requests[#requests + 1] = {id = "list", service = "import", operation = "list"}
        requests[#requests + 1] = {id = "ucmlist", service = "profiles", operation = "list", group = "Loot"}
        load_profile(st.profile)
    elseif tail == "_add" then
        local d = draft[side]
        local r = {kind = d.kind, on = true}
        if NEEDS_TEXT[d.kind] then
            if (d.text or "") == "" then message = "Type the text for that rule first."; return dirty end
            r.text = d.text
        elseif NEEDS_NUMBER[d.kind] then r.value = tonumber(d.text) or 0
        elseif d.kind == "Item type" then r.type = d.type end
        rules[#rules + 1] = r
        message = "Added: " .. describe(r)
        return true
    elseif tail == "_toggle" or tail == "_remove" then
        local n = draft[side].pick or 1
        local r = rules[n]
        if not r then return dirty end
        if tail == "_toggle" then r.on = not r.on; message = describe(r) .. (r.on and " is on" or " is off")
        else message = "Removed: " .. describe(r); table.remove(rules, n); draft[side].pick = math.max(1, n - 1) end
        return true
    elseif tail == "_addsel" then
        local item = selected_item(s)
        if not item then message = "Select an item in your packs first."; return dirty end
        rules[#rules + 1] = {kind = "Name is", text = item.name, on = true}
        message = "Added: Name is " .. item.name
        return true
    elseif string.sub(tail, 1, 4) == "_rm_" then
        local n = tonumber(string.sub(tail, 5))
        if rules[n] then message = "Removed: " .. describe(rules[n]); table.remove(rules, n); return true end
    end
    return dirty
end

local function rule_page(side, title, help)
    local function text(id, t) return {id = id, type = "text", text = t} end
    local p = string.sub(side, 1, 1)
    local children = {text(p .. "_help", help)}
    -- one row per rule with an X to remove it (JJ 2026-11-24: "just add a clickable X to remove something")
    for n, r in ipairs(st[side]) do
        if n > 80 then children[#children + 1] = text(p .. "_more", "... and " .. (#st[side] - 80) .. " more"); break end
        children[#children + 1] = {id = p .. "_row_" .. n, type = "row", children = {
            text(p .. "_t_" .. n, describe(r) .. (r.on and "" or "  (off)")),
            {id = p .. "_rm_" .. n, type = "button", text = "X", tooltip = "Remove", auto_width = true},
        }}
    end
    if #st[side] == 0 then children[#children + 1] = text(p .. "_none", "(none)") end
    local d = draft[side]
    children[#children + 1] = {id = p .. "_kind", type = "select", text = "New rule", value = d.kind, options = KINDS}
    if d.kind == "Item type" then
        children[#children + 1] = {id = p .. "_type", type = "select", text = "Type", value = d.type, options = TYPE_NAMES}
    elseif NEEDS_TEXT[d.kind] or NEEDS_NUMBER[d.kind] then
        children[#children + 1] = {id = p .. "_text", type = "input", text = NEEDS_NUMBER[d.kind] and "Pyreals" or "Text", value = d.text or "", live = true}
    end
    children[#children + 1] = {id = p .. "_btns", type = "row", children = {
        {id = p .. "_add", type = "button", text = "Add rule"},
        {id = p .. "_addsel", type = "button", text = "Add selected item"},
    }}
    return {id = "page_" .. side, type = "column", text = title, children = children}
end

local function list_text(rows, limit)
    local out = {}
    for i, r in ipairs(rows) do
        if i > limit then out[#out + 1] = "... and " .. (#rows - limit) .. " more"; break end
        out[#out + 1] = r.why and (r.name .. "  (" .. r.why .. ")") or r.name
    end
    return #out > 0 and table.concat(out, "\n") or "(none)"
end

local function will_buttons()
    will_names = {}
    if not plan then return {{id = "m_will_t", type = "text", text = "(scan first)"}} end
    local count, order = {}, {}
    for _, f in ipairs(plan.sell) do
        if not count[f.name] then count[f.name] = 0; order[#order + 1] = f.name end
        count[f.name] = count[f.name] + 1
    end
    if #order == 0 then return {{id = "m_will_t", type = "text", text = "(nothing)"}} end
    local out = {}
    for i, name in ipairs(order) do
        if i > 60 then out[#out + 1] = {id = "m_will_more", type = "text", text = "... and " .. (#order - 60) .. " more"}; break end
        will_names[i] = name
        out[#out + 1] = {id = "w_" .. i, type = "button", text = name .. (count[name] > 1 and ("  x" .. count[name]) or ""),
            tooltip = (st.click_mode == CLICK_MODES[2]) and ("Select to keep: adds \"Name is " .. name .. "\" to Exceptions")
                or ("Select to remove " .. name .. " from this sale"), enabled = job == nil}
    end
    return out
end

local function ui(s)
    local function text(id, t) return {id = id, type = "text", text = t} end
    local open = s.vendor and s.vendor ~= 0
    local status
    if job then status = "Selling " .. (job.sold + job.failed) .. "/" .. #job.queue
    elseif plan and not plan.done then status = "Scanning " .. plan.i .. "/" .. #plan.todo
    elseif plan then
        local pays = 0
        for _, f in ipairs(plan.sell) do pays = pays + (f.pays or 0) end
        status = #plan.sell .. " to sell" .. (pays > 0 and (" for about " .. pays .. " pyreals") or "")
    else status = "Scan to preview, or Sell." end
    -- the items clicked away this sale, each a button that puts it back
    unskip_names = {}
    local skipped = {}
    for name in pairs(skip_names) do skipped[#skipped + 1] = name end
    table.sort(skipped)
    local skip_children = {}
    for i, name in ipairs(skipped) do
        if i > 40 then break end
        unskip_names[i] = name
        skip_children[#skip_children + 1] = {id = "u_" .. i, type = "button", text = name, tooltip = "Select to put it back in this sale", enabled = job == nil}
    end
    if #skipped > 0 then skip_children[#skip_children + 1] = {id = "b_unskip", type = "button", text = "Put all back", enabled = job == nil} end
    local sell_page = {id = "page_main", type = "column", text = "Sell", children = {
        text("m_vendor", "Vendor: " .. (open and (s.vendor_name or "open") or "(none open)")),
        {id = "m_only", type = "input", text = "Only sell", value = st.only or "", live = true,
            tooltip = "Blank sells everything. Or words from the name, comma separated: Essence, Scarab"},
        {id = "m_btns", type = "row", children = {
            {id = "b_scan", type = "button", text = "Scan", enabled = job == nil},
            {id = "b_sell", type = "button", text = job and "Stop" or "Sell"},
        }},
        text("m_status", status),
        text("m_msg", message ~= "" and message or " "),
        {id = "m_click_mode", type = "select", text = "Selecting an item", value = (st.click_mode == CLICK_MODES[2]) and CLICK_MODES[2] or CLICK_MODES[1], options = CLICK_MODES},
        {id = "m_will", type = "section", text = (st.click_mode == CLICK_MODES[2]) and "Will sell (select an item to keep it always)"
            or "Will sell (select an item to remove it from this sale)", expanded = true, children = will_buttons()},
        #skipped > 0 and {id = "m_skipped", type = "section", text = "Removed from this sale (select to put back)", expanded = true, children = skip_children}
            or text("m_skipped_none", " "),
        text("m_log", #log > 0 and table.concat(log, "\n") or " "),
    }}
    local options = {"(none)"}
    for _, f in ipairs(ucm_profiles) do options[#options + 1] = UCM_PREFIX .. f end
    for _, f in ipairs(profile_files) do options[#options + 1] = f end
    local listed = false
    for _, o in ipairs(options) do if o == st.profile then listed = true end end
    if st.profile and not listed then options[#options + 1] = st.profile end
    local profile_page = {id = "page_profile", type = "column", text = "Profile", children = {
        text("p_help", "Uses your loot profile to decide what's sold. Your Exceptions take priority."),
        {id = "profile_refresh", type = "button", text = "Refresh list"},
        {id = "profile_pick", type = "select", text = "Profile", value = st.profile or "(none)", options = options},
        text("p_note", profile_note ~= "" and profile_note or " "),
    }}
    return {{id = "tab", type = "tabs", children = {
        sell_page,
        rule_page("keep", "Exceptions", "Loot never sold. Also always kept: equipped and retained items, unassessed items, and anything your loot profile keeps."),
        profile_page,
    }}}
end

return function(s, p)
    if not st then
        st = {}
        for k, v in pairs(s.state or {}) do st[k] = v end
        st.sell = st.sell or copy_rules(DEFAULT_SELL)
        st.keep = st.keep or copy_rules(DEFAULT_KEEP)
        requests[#requests + 1] = {id = "list", service = "import", operation = "list"}
        requests[#requests + 1] = {id = "ucmlist", service = "profiles", operation = "list", group = "Loot"}
        load_profile(st.profile)
    end
    local dirty = false
    if s.vendor ~= last_vendor then
        if not job then skip_names = {} end
        last_vendor = s.vendor
    end
    for _, e in ipairs(s.events or {}) do
        if e.type == "result" then
            if e.id == "list" and e.ok then
                profile_files = {}
                for _, f in ipairs(e.files or {}) do
                    if lower(string.sub(f, -4)) == ".utl" then profile_files[#profile_files + 1] = f end
                end
            elseif e.id == "ucmlist" then
                ucm_profiles = {}
                for _, f in ipairs((e.ok and e.names) or {}) do ucm_profiles[#ucm_profiles + 1] = f end
            elseif e.id == "ucmread" then
                if e.ok and type(e.data) == "table" then
                    profile_rules = e.data
                    profile_note = #e.data .. " rules loaded from UCM"
                    if plan then plan.waiting = nil end
                else
                    -- never sell by a profile that couldn't be read: its Keep rules would be missing
                    profile_rules = nil
                    profile_note = "Couldn't read the UCM profile: " .. tostring(e.error or "unknown")
                    if plan and plan.waiting then plan, message = nil, "Scan stopped: the UCM loot profile couldn't be read." end
                end
            elseif e.id == "convert" then
                local rules = e.data and e.data.loot_rules
                if rules then
                    profile_rules = rules
                    profile_note = #rules .. " rules loaded" .. ((e.issues and #e.issues > 0) and (", " .. #e.issues .. " not supported (ignored)") or "")
                else
                    profile_rules = nil
                    profile_note = "Couldn't load: " .. tostring(e.error or (e.issues and e.issues[1]) or "unknown")
                end
            end
        else
            dirty = handle(s, e, dirty)
        end
    end

    local out = {}
    if plan and not job and not plan.done then
        if continue_scan(s) then
            plan.done = true
            message = ""
            if plan.then_sell then start_selling(s) end
        end
    end
    if job and s.ready ~= false then out = sell_step(s) or {} end
    if #requests > 0 then
        out.requests = {}
        for i = 1, math.min(4, #requests) do out.requests[i] = table.remove(requests, 1) end
    end
    out.status = job and ("Selling " .. (job.sold + job.failed) .. "/" .. #job.queue) or "Ready"
    out.ui = ui(s)
    if dirty then out.state = {sell = st.sell, keep = st.keep, profile = st.profile, click_mode = st.click_mode, only = st.only} end
    return out
end
