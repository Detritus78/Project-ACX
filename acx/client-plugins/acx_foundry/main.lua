-- ACX Foundry: AC:Unreal's bundled Dereth Foundry (ClientMods/foundry/main.lua, AC:Unreal build of 2026-11-11)
-- copied unchanged into a user plugin, plus Project ACX tabs (marked "Project ACX": ACX test kit, Concordia building). Being a user plugin,
-- it survives launcher updates. Install: copy this folder to Saved/ClientPlugins/Installed/acx_foundry.
-- Dereth Foundry: Lua owns the workshop; the host exposes bounded content
-- file browsing and ACE's existing administrator commands, never a database.
local c,root="", ""
local files,total,page={},0,0
local queue,serial={},0
local status="Choose content or select a world object to begin."
local history,pending={},nil
local defaults={wcid="",landblock="",quest="",recipe="",generator="",child="",parent="",
    kind="weenies",format="json",filter="",file="",distance=.1,heading=0}
local types={weenies="weenie",landblocks="landblock",quests="quest",recipes="recipe"}
local function trim(v)
    local t=tostring(v or "");local a,b=1,#t
    while a<=b and t:byte(a)<=32 do a=a+1 end
    while b>=a and t:byte(b)<=32 do b=b-1 end
    return t:sub(a,b)
end
local function token(v)
    local t=trim(v);if t=="" or #t>128 then return nil end
    for i=1,#t do local ch=t:byte(i);if ch<=32 or ch==127 then return nil end end
    return t
end
local transfers_by_id={}
for kind in pairs(types) do for _,verb in ipairs({"import","export"}) do for _,format in ipairs({"json","sql"}) do
    transfers_by_id[verb.."_"..format.."_"..kind]={verb,format,kind}
end end end
local function file_id(path)
    local start=1;for i=1,#path do if path:sub(i,i)=="/" then start=i+1 end end
    local name=path:sub(start);local stop=#name
    for i=1,#name-2 do if name:sub(i,i+2)==" - " then return name:sub(1,i-1) end end
    for i=#name,1,-1 do if name:sub(i,i)=="." then stop=i-1;break end end
    return name:sub(1,stop)
end
local function hex(v) return string.format("%08X",tonumber(v) or 0) end
local function note(t) status=t;table.insert(history,1,t);while #history>8 do table.remove(history) end end
local function req(op,data)
    serial=serial+1;data=data or {};data.service="content";data.operation=op;data.id="content_"..serial
    if op=="list" then for i=#queue,1,-1 do if queue[i].operation=="list" then table.remove(queue,i) end end end
    if #queue<16 then queue[#queue+1]=data else note("Finish the pending requests first.") end
end
local function scan() req("list",{kind=c.kind,format=c.format,filter=c.filter,offset=page*128}) end
local function command(text,target,danger)
    if danger then pending={text=text,target=target};note("Review the command below, then confirm.");return end
    req("command",{text=text,target=target})
end
-- Project ACX: test-kit buttons send ordinary chat commands (say permission) through the same queue
local acx_commands={
    acx_god="/god",acx_ungod="/ungod",acx_market="/teleloc 0x016C01BC [49.2 -31.9 0.005]",
    acx_lv10="/acxlevel 10",acx_lv20="/acxlevel 20",acx_lv50="/acxlevel 50",acx_lv80="/acxlevel 80",acx_lv115="/acxlevel 115",
    acx_lv150="/acxlevel 150",acx_lv200="/acxlevel 200",acx_lv275="/acxlevel 275",
    acx_dummy="/create 900015",acx_dummy2="/create 900016",acx_dummy3="/create 900017",acx_debug="/debugdamage defense",acx_fi="/fi",
    acx_shield="/ci 91",acx_shirt="/lootgen 130 6",acx_pants="/lootgen 127 6",
    -- plain Heavy Weapons, one per type: the base items loot is built from (no spells, no wield requirements)
    acx_hw_battleaxe="/ci 301",acx_hw_lugianhammer="/ci 31764",acx_hw_silifi="/ci 344",acx_hw_waraxe="/ci 31769",acx_hw_dirk="/ci 22440",acx_hw_jambiya="/ci 319",acx_hw_stiletto="/ci 30601",acx_hw_flangedmace="/ci 30586",acx_hw_mace="/ci 331",acx_hw_mazule="/ci 30581",acx_hw_morningstar="/ci 332",acx_hw_partizan="/ci 30591",acx_hw_spineglaive="/ci 31779",acx_hw_trident="/ci 7772",acx_hw_nabut="/ci 22163",acx_hw_stick="/ci 31788",acx_hw_flamberge="/ci 30576",acx_hw_ken="/ci 327",acx_hw_longsword="/ci 351",acx_hw_schlager="/ci 45108",acx_hw_tachi="/ci 353",acx_hw_takuba="/ci 354",acx_hw_cestus="/ci 4190",acx_hw_nekode="/ci 4195",
    acx_dps_start="/dps start",acx_dps_show="/dps",acx_dps_stop="/dps stop",
    -- Concordia building (2026-11-14): everything is placed where you stand, as a permanent instance (/createinst)
    acx_cc_vault="/createinst 900700",acx_cc_keeper="/createinst 900707",acx_cc_trader="/createinst 45501",
    acx_cc_head_up="/createinst 22755",acx_cc_head_down="/createinst 22759",
    acx_cc_godmode="/god",acx_cc_ungod="/ungod",acx_cc_reload="/reload-landblock",acx_cc_myloc="/myloc",acx_cc_go="/telepoi Concordia",
    acx_cc_p740="/createinst 900740",acx_cc_p741="/createinst 900741",acx_cc_p742="/createinst 900742",acx_cc_p743="/createinst 900743",
    acx_cc_p744="/createinst 900744",acx_cc_p745="/createinst 900745",acx_cc_p746="/createinst 900746",acx_cc_p747="/createinst 900747",
    acx_cc_p748="/createinst 900748",acx_cc_p749="/createinst 900749",acx_cc_p750="/createinst 900750",acx_cc_p751="/createinst 900751",
    -- Gambling halls (2026-11-14): chests, games, hall furniture and farm animals; the halls' staff on the Gamblers tab
    acx_gb_reload="/reload-landblock",acx_gb_myloc="/myloc",
    acx_gb_monty="/createinst 9460",acx_gb_arshid="/createinst 9461",acx_gb_ganzo="/createinst 9462",acx_gb_grand="/createinst 38942",acx_gb_exquisite="/createinst 52032",
    acx_gb_wheel="/createinst 900788",acx_gb_miniwheel="/createinst 900789",acx_gb_wheelk="/createinst 34282",acx_gb_chess="/createinst 14341",
    acx_gb_table="/createinst 900777",acx_gb_bench="/createinst 900778",acx_gb_stool="/createinst 900779",acx_gb_keg="/createinst 900780",
    acx_gb_crate="/createinst 900781",acx_gb_tallcrate="/createinst 900771",acx_gb_plankcrate="/createinst 900772",
    acx_gb_fixture="/createinst 900774",acx_gb_fixturel="/createinst 900773",
    acx_gb_lamp="/createinst 900782",acx_gb_brazier="/createinst 900783",acx_gb_lantern="/createinst 900776",acx_gb_lanternt="/createinst 900775",
    acx_gb_cow="/createinst 900784",acx_gb_cow2="/createinst 900785",acx_gb_chicken="/createinst 900786",acx_gb_chicken2="/createinst 900787",
    acx_gm_reload="/reload-landblock",
    acx_gm_monty="/createinst 9492",acx_gm_arshid="/createinst 9493",acx_gm_ganzo="/createinst 9494",
    acx_gm_boddry="/createinst 9504",acx_gm_ashadi="/createinst 9505",acx_gm_haya="/createinst 9506",
    acx_gm_al_lo="/createinst 9498",acx_gm_al_mid="/createinst 9501",acx_gm_al_hi="/createinst 9495",
    acx_gm_gh_lo="/createinst 9499",acx_gm_gh_mid="/createinst 9502",acx_gm_gh_hi="/createinst 9496",
    acx_gm_sh_lo="/createinst 9500",acx_gm_sh_mid="/createinst 9503",acx_gm_sh_hi="/createinst 9497",
    acx_gm_rare_al="/createinst 40463",acx_gm_rare_gh="/createinst 40462",acx_gm_rare_sh="/createinst 40464",acx_gm_pawn="/createinst 12308"}
local acx_replies={}
-- full 100-unit, workmanship-10 salvage bags (Preview experiment acx/Experiments/salvage-test-vendor.sql, wcids 900200-900259)
local acx_bags={["Agate"]=21034,["Alabaster"]=20980,["Amber"]=21035,["Amethyst"]=21036,["Aquamarine"]=21037,["Armoredillo Hide"]=20981,["Azurite"]=21038,["Black Garnet"]=21039,["Black Opal"]=21040,["Bloodstone"]=21041,["Brass"]=21042,["Bronze"]=20982,["Carnelian"]=21043,["Ceramic"]=20983,["Citrine"]=21044,["Copper"]=21045,["Diamond"]=21046,["Ebony"]=21047,["Emerald"]=21048,["Fire Opal"]=21049,["Gold"]=20984,["Granite"]=20985,["Green Garnet"]=21050,["Green Jade"]=21051,["Gromnie Hide"]=21052,["Hematite"]=21053,["Imperial Topaz"]=21054,["Iron"]=20986,["Ivory"]=21055,["Jet"]=21056,["Lapis Lazuli"]=21057,["Lavender Jade"]=21058,["Leather"]=21059,["Linen"]=20987,["Mahogany"]=20988,["Malachite"]=21060,["Marble"]=21061,["Moonstone"]=21062,["Oak"]=20989,["Onyx"]=21064,["Opal"]=21065,["Peridot"]=21066,["Pine"]=20990,["Porcelain"]=21067,["Pyreal"]=21068,["Red Garnet"]=21069,["Red Jade"]=21070,["Reed Shark Hide"]=20991,["Rose Quartz"]=21071,["Ruby"]=21072,["Sandstone"]=21073,["Sapphire"]=21074,["Serpentine"]=21075,["Silk"]=21076,["Silver"]=21077,["Smokey Quartz"]=21078,["Steel"]=20993,["Sunstone"]=21079,["Teak"]=21080,["Tiger Eye"]=21081,["Tourmaline"]=21082,["Turquoise"]=21083,["Velvet"]=20994,["White Jade"]=21084,["White Quartz"]=21085,["White Sapphire"]=21086,["Wool"]=20995,["Yellow Garnet"]=21087,["Yellow Topaz"]=21088,["Zircon"]=21089}
local acx_bag_names={"Agate","Alabaster","Amber","Amethyst","Aquamarine","Armoredillo Hide","Azurite","Black Garnet","Black Opal","Bloodstone","Brass","Bronze","Carnelian","Ceramic","Citrine","Copper","Diamond","Ebony","Emerald","Fire Opal","Gold","Granite","Green Garnet","Green Jade","Gromnie Hide","Hematite","Imperial Topaz","Iron","Ivory","Jet","Lapis Lazuli","Lavender Jade","Leather","Linen","Mahogany","Malachite","Marble","Moonstone","Oak","Onyx","Opal","Peridot","Pine","Porcelain","Pyreal","Red Garnet","Red Jade","Reed Shark Hide","Rose Quartz","Ruby","Sandstone","Sapphire","Serpentine","Silk","Silver","Smokey Quartz","Steel","Sunstone","Teak","Tiger Eye","Tourmaline","Turquoise","Velvet","White Jade","White Quartz","White Sapphire","Wool","Yellow Garnet","Yellow Topaz","Zircon"}
local acx_bag="Silk"
local acx_build_page="Concordia" -- Project ACX: the Build tab's page
local function say(text)
    serial=serial+1
    if #queue<16 then queue[#queue+1]={service="chat",operation="send",text=text,id="acx_"..serial} else note("Finish the pending requests first.") end
end
local function required(value,label)
    local v=token(value);if not v then note("Enter "..label.." (one ID or classname).");return nil end;return v
end
return function(s)
    if type(c)~="table" then
        c={};for k,v in pairs(defaults) do c[k]=type((s.state or {})[k])==type(v) and s.state[k] or v end
        if not types[c.kind] then c.kind="weenies" end
        if c.format~="json" and c.format~="sql" then c.format="json" end
        c.distance=math.max(.001,math.min(100,tonumber(c.distance) or .1));c.heading=math.max(0,math.min(360,tonumber(c.heading) or 0))
    end
    local selection=s.selection or {};local selected=tonumber(selection.id) or 0
    local function target() if selected==0 or selection.object_type=="player" or selected==s.player then note("Select a non-player object first.");return nil end;return selected end
    local dirty=false
    if pending and pending.target and pending.target~=selected then pending=nil;note("Selection changed; command cancelled.") end
    for _,e in ipairs(s.events or {}) do
        if e.type=="content_root" then root=e.root or "";page=0;c.file="";files={};scan()
        elseif e.type=="result" and tostring(e.id):sub(1,8)=="content_" then
            if not e.ok then note(e.error or "Request failed.")
            elseif e.files then files=e.files;total=e.total or #files;root=e.root or root;note(total.." matching files.")
            elseif e.command then note("Sent "..e.command.." — see chat for the server result.")
            else note("Opened in your text editor.") end
        elseif e.type=="result" and tostring(e.id):sub(1,4)=="acx_" then -- Project ACX
            if not e.ok then note(e.error or "Command rejected.") end
        elseif e.type=="change" and e.id=="acx_bag" then acx_bag=e.value -- Project ACX
        elseif e.type=="change" and e.id=="acx_build_page" then acx_build_page=e.value -- Project ACX
        elseif e.type=="change" and defaults[e.id]~=nil then
            c[e.id]=e.value;dirty=true
            if e.id=="kind" or e.id=="format" or e.id=="filter" then page=0;c.file="";files={};scan() end
            if e.id=="file" then
                local prefix=file_id(c.file)
                local field=({weenies="wcid",landblocks="landblock",quests="quest",recipes="recipe"})[c.kind]
                c[field]=prefix
            end
        elseif e.type=="click" then
            local id=e.id
            if id=="confirm" and pending then command(pending.text,pending.target);pending=nil
            elseif id=="cancel" then pending=nil;note("Command cancelled.")
            elseif acx_commands[id] then -- Project ACX
                local cmd=acx_commands[id]
                -- creation goes through Foundry's content service like its own buttons; anything else is spoken as
                -- an @command, because ACE only reads commands from talk text that starts with @
                if cmd:sub(1,4)=="/ci " or cmd:sub(1,8)=="/create " then command(cmd) else say("@"..cmd:sub(2)) end
                note("Sent "..cmd.." — see Server replies.")
            elseif id=="acx_clear" then acx_replies={}
            elseif id=="acx_getbag" and acx_bags[acx_bag] then say("@acxbag "..acx_bags[acx_bag]);note("Sent /acxbag "..acx_bags[acx_bag].." ("..acx_bag.." bag).")
            elseif id=="scan" then scan()
            elseif id=="prev" then page=math.max(0,page-1);scan()
            elseif id=="next" then page=page+1;scan()
            elseif id=="edit" then if c.file~="" then req("edit",{name=c.file}) else note("Choose a file first.") end
            elseif id=="use_selected" then
                if selected~=0 then c.wcid=selection.wcid and string.format("%.0f",selection.wcid) or "";dirty=true else note("Select an object first.") end
            elseif id=="get_parent" then if target() then c.parent="0x"..hex(selected);dirty=true end
            elseif id=="current_block" then c.landblock=string.format("%04X",math.floor(((s.position or {}).cell or 0)/65536));dirty=true
            elseif id=="create" or id=="ci" or id=="createinst" then
                local v=required(c.wcid,"a WCID");if v then command("/"..id.." "..v,nil,id=="createinst") end
            elseif id=="generator_create" then local v=required(c.generator,"a generator WCID");if v then command("/createinst "..v,nil,true) end
            elseif id=="link" then
                local parent=required(c.parent,"a parent GUID");local child=required(c.child,"a child WCID")
                if parent and child and parent:sub(1,2):lower()=="0x" and #parent<=10 and tonumber(parent:sub(3),16) then command("/createinst -p "..parent.." -c "..child,nil,true)
                elseif parent then note("Parent GUID must use 0x followed by hexadecimal digits.") end
            elseif id=="addenc" then local v=required(c.child,"an encounter WCID");if v then command("/addenc "..v,nil,true) end
            elseif id=="removeinst" or id=="removeenc" or id=="delete" or id=="getinfo" then
                local t=target();if t then command("/"..id,t,id~="getinfo") end
            elseif id:sub(1,6)=="nudge_" then local t=target();if t then command("/nudge "..id:sub(7).." "..tostring(c.distance),t) end
            elseif id=="rotate" then local t=target();if t then command("/rotate "..tostring(c.heading),t) end
            elseif id=="rotate_here" then local t=target();if t then command("/rotate here",t) end
            elseif id=="myloc" or id=="clearcache" or id=="reload-landblock" then command("/"..id,nil,id~="myloc")
            elseif id=="server_folder" then command("/fetchstring content_folder")
            else
                local transfer=transfers_by_id[id]
                if transfer then
                    local verb,format,kind=table.unpack(transfer)
                    local field=({weenies="wcid",landblocks="landblock",quests="quest",recipes="recipe"})[kind]
                    local value=required(c[field],"the "..types[kind].." ID")
                    if value then command("/"..verb.."-"..format.." "..types[kind].." "..value,nil,verb=="import") end
                end
            end
        end
    end
    -- Project ACX: keep the latest server system lines (no sender) for the ACX tab
    for _,e in ipairs(s.chat_events or {}) do
        if (e.sender or "")=="" then
            acx_replies[#acx_replies+1]=(e.text or e.raw_text or ""):sub(1,600)
            while #acx_replies>12 do table.remove(acx_replies,1) end
        end
    end
    local function text(id,t) return {id=id,text=t} end
    local function button(id,t,enabled) return {id=id,type="button",text=t,enabled=enabled~=false} end
    local function input(id,t) return {id=id,type="input",text=t,value=c[id]} end
    local function row(id,children) return {id=id,type="row",children=children} end
    local function tab(id,t,children) return {id=id,type="column",text=t,children=children} end
    local function transfers(kind)
        return row(kind.."_transfers",{button("import_json_"..kind,"Import JSON"),button("import_sql_"..kind,"Import SQL"),button("export_json_"..kind,"Export JSON"),button("export_sql_"..kind,"Export SQL")})
    end
    local selected_text=selected~=0 and (selection.name or "Object").." · WCID "..(selection.wcid and string.format("%.0f",selection.wcid) or "?").." · 0x"..hex(selected) or "No object selected"
    local function acx(id,t,tip) return {id=id,type="button",text=t,tooltip=tip or acx_commands[id]} end
    local tabs={
        tab("acx","ACX",{ -- Project ACX
            text("acx_setup_label","Setup"),
            row("acx_setup",{acx("acx_god","God mode"),acx("acx_ungod","Ungod","Back to your normal character: /ungod"),acx("acx_market","Go to test chests","Marketplace loot chests (Preview)")}),
            text("acx_levels_label","Level up to a tier (experience left unassigned; levels only go up, and ungod first if you're in god mode)"),
            row("acx_levels",{acx("acx_lv10","10 (T1)","Starter towns: /acxlevel 10"),acx("acx_lv20","20 (T2)"),acx("acx_lv50","50 (T3)"),
                acx("acx_lv80","80 (T4)"),acx("acx_lv115","115 (T5)","Direlands towns: /acxlevel 115"),acx("acx_lv150","150"),acx("acx_lv200","200"),acx("acx_lv275","275 (max)")}),
            row("acx_targets",{acx("acx_dummy","Spawn target dummy","1,000,000 health, never fights back (Preview): /create 900015"),acx("acx_dummy2","Dummy 2"),acx("acx_dummy3","Dummy 3"),
                acx("acx_debug","Debug damage","Appraise a dummy first: /debugdamage defense"),acx("acx_fi","Fix invisible")}),
            text("acx_weapons_label","Heavy weapons, plain: no spells, no requirements (into your pack)"),
            row("acx_hwrow_axe",{text("acx_hwrow_axe_label","Axes"),acx("acx_hw_battleaxe","Battle Axe","Heavy axe: /ci 301"),acx("acx_hw_lugianhammer","Lugian Hammer","Heavy axe: /ci 31764"),acx("acx_hw_silifi","Silifi","Heavy axe: /ci 344"),acx("acx_hw_waraxe","War Axe","Heavy axe: /ci 31769")}),
            row("acx_hwrow_dagger",{text("acx_hwrow_dagger_label","Daggers (bleed)"),acx("acx_hw_dirk","Dirk","Heavy dagger: /ci 22440"),acx("acx_hw_jambiya","Jambiya","Heavy dagger, multi-strike: /ci 319"),acx("acx_hw_stiletto","Stiletto","Heavy dagger, multi-strike: /ci 30601")}),
            row("acx_hwrow_mace",{text("acx_hwrow_mace_label","Maces"),acx("acx_hw_flangedmace","Flanged Mace","Heavy mace: /ci 30586"),acx("acx_hw_mace","Mace","Heavy mace: /ci 331"),acx("acx_hw_mazule","Mazule","Heavy mace: /ci 30581"),acx("acx_hw_morningstar","Morning Star","Heavy mace: /ci 332")}),
            row("acx_hwrow_spear",{text("acx_hwrow_spear_label","Spears and staffs"),acx("acx_hw_partizan","Partizan","Heavy spear: /ci 30591"),acx("acx_hw_spineglaive","Spine Glaive","Heavy spear: /ci 31779"),acx("acx_hw_trident","Trident","Heavy spear: /ci 7772"),acx("acx_hw_nabut","Nabut","Heavy spear, staff: /ci 22163"),acx("acx_hw_stick","Stick","Heavy spear, staff: /ci 31788")}),
            row("acx_hwrow_sword",{text("acx_hwrow_sword_label","Swords (multi-strike ones bleed)"),acx("acx_hw_flamberge","Flamberge","Heavy sword: /ci 30576"),acx("acx_hw_ken","Ken","Heavy sword: /ci 327"),acx("acx_hw_longsword","Long Sword","Heavy sword: /ci 351"),acx("acx_hw_schlager","Schlager","Heavy sword, multi-strike: /ci 45108"),acx("acx_hw_tachi","Tachi","Heavy sword: /ci 353"),acx("acx_hw_takuba","Takuba","Heavy sword: /ci 354")}),
            row("acx_hwrow_unarmed",{text("acx_hwrow_unarmed_label","Unarmed"),acx("acx_hw_cestus","Cestus","Heavy unarmed: /ci 4190"),acx("acx_hw_nekode","Nekode","Heavy unarmed: /ci 4195")}),
            row("acx_clothes",{acx("acx_shield","Kite Shield"),acx("acx_shirt","Looted shirt","Tier 6 shirt you can tinker (Silk, hides...): /lootgen 130 6"),
                acx("acx_pants","Looted pants","Tier 6 pants you can tinker: /lootgen 127 6")}),
            row("acx_salvage",{{id="acx_bag",type="select",text="Salvage bag",value=acx_bag,options=acx_bag_names},
                {id="acx_getbag",type="button",text="Get full bag",tooltip="A 100-unit bag at workmanship 10, into your pack"}}),
            text("acx_dps_label","DPS test: 60 seconds from your first attack (Preview)"),
            row("acx_dps",{acx("acx_dps_start","Start"),acx("acx_dps_show","Show"),acx("acx_dps_stop","Stop")}),
            {id="acx_replies",type="section",text="Server replies",expanded=true,children={
                text("acx_reply_lines",#acx_replies>0 and table.concat(acx_replies,"\n") or "(none yet)"),
                {id="acx_clear",type="button",text="Clear"}}}}),
        -- Project ACX: Concordia, Gambling and Gamblers share one tab with a page picker, because AC:Unreal refuses a
        -- plugin window with more than 256 controls and the tab buttons don't tell the plugin which tab is showing; only
        -- the chosen page is built
        tab("acx_build","Build",(function()
            local pages={
                Concordia=function() return {
            text("acx_cc_help","Each button places a permanent object where you stand, facing your way (/createinst). Nudge or turn it on the Move tab. Drop a pile first, then the vault chest in it; Claude adds the hooks above the chests. Reload landblock to see data changes."),
            row("acx_cc_tools",{acx("acx_cc_godmode","God mode"),acx("acx_cc_ungod","Ungod","Back to your normal character: /ungod"),acx("acx_cc_go","Go to Concordia","/telepoi Concordia"),
                acx("acx_cc_reload","Reload landblock","Respawns everything on this landblock from the database (monsters and dropped items here vanish)"),acx("acx_cc_myloc","My location","/myloc: your position, for telling Claude a spot")}),
            text("acx_cc_vault_label","Vaults and their keepers"),
            row("acx_cc_vaults",{acx("acx_cc_vault","Vault chest","A leasable vault (900700): rooms and names come from where you place them"),
                acx("acx_cc_keeper","Vault Keeper","Sells deeds, rent writs, keys and locks (900707): by the lifestone"),
                acx("acx_cc_trader","Mark Trader","Trades for ACX Marks (45501): second floor")}),
            row("acx_cc_heads",{acx("acx_cc_head_up","Temple of Enlightenment","Respec portal head, half size (22755)"),acx("acx_cc_head_down","Temple of Forgetfulness","Respec portal head, half size (22759)")}),
            text("acx_cc_piles_label","Piles (walk-through scenery; set a chest in one)"),
            row("acx_cc_piles1",{acx("acx_cc_p740","Gold, small","Pile of Gold (900740)"),acx("acx_cc_p741","Gold heap","Heap of Gold (900741)"),acx("acx_cc_p742","Gold, small 2","Pile of Gold, other heap (900742)"),acx("acx_cc_p743","Gold mound","Mound of Gold (900743)")}),
            row("acx_cc_piles2",{acx("acx_cc_p744","Hoard","Hoard (900744)"),acx("acx_cc_p745","Rubble","Rubble (900745)"),acx("acx_cc_p746","Rubble 2","Tomb rubble (900746)"),acx("acx_cc_p747","Bones","Pile of Bones (900747)")}),
            row("acx_cc_piles3",{acx("acx_cc_p748","Bones 2","Pile of Bones (900748)"),acx("acx_cc_p749","Trunk of bones","Trunk of Bones (900749)"),acx("acx_cc_p750","Parts","Pile of Parts (900750)"),acx("acx_cc_p751","Presents","Pile of Presents (900751)")}),
            {id="acx_cc_replies",type="section",text="Server replies",expanded=false,children={
                text("acx_cc_reply_lines",#acx_replies>0 and table.concat(acx_replies,"\n") or "(none yet)")}}} end,
                Gambling=function() return {
            text("acx_gb_help","Each button places a permanent object where you stand, facing your way. Furniture and animals are fixed scenery (walk-through, can't be picked up). The large wooden fixture sits half in the floor: nudge it up 1.25 m."),
            row("acx_gb_tools",{acx("acx_gb_reload","Reload landblock","Respawns everything on this landblock from the database"),acx("acx_gb_myloc","My location","/myloc")}),
            text("acx_gb_chests_label","Gambling chests"),
            row("acx_gb_chests",{acx("acx_gb_monty","Monty's Golden","Monty's Golden Chest (9460): opened with a Golden Key from the games"),
                acx("acx_gb_arshid","Arshid's Golden","Arshid's Golden Chest (9461)"),acx("acx_gb_ganzo","Gan-Zo's Golden","Gan-Zo's Golden Chest (9462)")}),
            row("acx_gb_chests2",{acx("acx_gb_grand","Grand Casino","Grand Casino Chest (38942)"),acx("acx_gb_exquisite","Exquisite Casino","Exquisite Casino Chest (52032)")}),
            text("acx_gb_games_label","Games"),
            row("acx_gb_games",{acx("acx_gb_wheel","Wheel of Fortune","The Wheel of Fortune game (900788): one spin per character every 20 hours for gambling tokens"),acx("acx_gb_wheelk","Wheel of Knowledge","Wheel of Knowledge (34282): spins when used, no prize"),acx("acx_gb_miniwheel","Mini wheel","A small Wheel of Fortune just for show (900789)"),
                acx("acx_gb_chess","Chess board","Chess Board (14341)")}),
            text("acx_gb_furniture_label","Hall furniture"),
            row("acx_gb_furn1",{acx("acx_gb_table","Table","The halls' table (900777)"),acx("acx_gb_bench","Bench","Bench (900778)"),
                acx("acx_gb_stool","Stool","Stool (900779)"),acx("acx_gb_keg","Keg","Keg (900780)")}),
            row("acx_gb_furn2",{acx("acx_gb_crate","Crate","Crate (900781)"),acx("acx_gb_tallcrate","Tall crate","Iron-bound crate from the Aluvian hall (900771)"),
                acx("acx_gb_plankcrate","Plank crate","Plank crate from the Aluvian hall (900772)")}),
            row("acx_gb_furn3",{acx("acx_gb_fixture","Wooden fixture","Wooden fixture from the Gharu'ndim hall, 1.7 x 2 m (900774)"),
                acx("acx_gb_fixturel","Large fixture","Large wooden fixture from the Gharu'ndim hall, 2 x 2.7 m; nudge it up 1.25 m (900773)")}),
            row("acx_gb_light",{acx("acx_gb_lamp","Lamp","Lamp (900782)"),acx("acx_gb_brazier","Brazier","Brazier (900783)"),
                acx("acx_gb_lantern","Paper lantern","Standing paper lantern, 1.6 m (900776)"),acx("acx_gb_lanternt","Tall lantern","Standing paper lantern, 2.1 m (900775)")}),
            text("acx_gb_farm_label","Farm animals (they stand still)"),
            row("acx_gb_farm",{acx("acx_gb_cow","Cow","Cow (900784)"),acx("acx_gb_cow2","Cow 2","The other cow model (900785)"),
                acx("acx_gb_chicken","Chicken","Chicken (900786)"),acx("acx_gb_chicken2","Chicken 2","The other chicken model (900787)")})} end,
                Gamblers=function() return {
            text("acx_gm_help","The staff of retail's three gambling halls. Each button places one where you stand, facing your way."),
            row("acx_gm_tools",{acx("acx_gm_reload","Reload landblock","Respawns everything on this landblock from the database")}),
            text("acx_gm_sellers_label","Token sellers and shopkeepers"),
            row("acx_gm_sellers",{acx("acx_gm_monty","Monty","Monty the Munificent, Aluvian token seller (9492)"),acx("acx_gm_arshid","Arshid","Arshid al-Qiyid, Gharu'ndim token seller (9493)"),
                acx("acx_gm_ganzo","Mi Gan-Zo","Mi Gan-Zo, Sho token seller (9494)")}),
            row("acx_gm_shops",{acx("acx_gm_boddry","Boddry","Boddry the Chancy, Aluvian shopkeeper (9504)"),acx("acx_gm_ashadi","Ashadi","Ashadi bint Samaq, Gharu'ndim shopkeeper (9505)"),
                acx("acx_gm_haya","Haya Su Ka","Haya Su Ka, Sho shopkeeper (9506)")}),
            text("acx_gm_masters_label","Gamesmasters (low, mid, high stakes)"),
            row("acx_gm_al",{acx("acx_gm_al_lo","Aluvian low","Aluvian Low-Stakes Gamesmaster (9498)"),acx("acx_gm_al_mid","Aluvian mid","Aluvian Mid-Stakes Gamesmaster (9501)"),
                acx("acx_gm_al_hi","Aluvian high","Aluvian High-Stakes Gamesmaster (9495)")}),
            row("acx_gm_gh",{acx("acx_gm_gh_lo","Gharu'ndim low","Gharu'ndim Low-Stakes Gamesmaster (9499)"),acx("acx_gm_gh_mid","Gharu'ndim mid","Gharu'ndim Mid-Stakes Gamesmaster (9502)"),
                acx("acx_gm_gh_hi","Gharu'ndim high","Gharu'ndim High-Stakes Gamesmaster (9496)")}),
            row("acx_gm_sh",{acx("acx_gm_sh_lo","Sho low","Sho Low-Stakes Gamesmaster (9500)"),acx("acx_gm_sh_mid","Sho mid","Sho Mid-Stakes Gamesmaster (9503)"),
                acx("acx_gm_sh_hi","Sho high","Sho High-Stakes Gamesmaster (9497)")}),
            text("acx_gm_others_label","Also in the retail halls"),
            row("acx_gm_others",{acx("acx_gm_rare_al","Rare Exchanger","Rare Exchanger from the Aluvian hall (40463)"),acx("acx_gm_rare_gh","Rare Exchanger 2","Rare Exchanger from the Gharu'ndim hall (40462)"),
                acx("acx_gm_rare_sh","Rare Exchanger 3","Rare Exchanger from the Sho hall (40464)"),acx("acx_gm_pawn","Pawn Shopkeep","Pawn Shopkeep (12308)")})} end}
            local children={{id="acx_build_page",type="select",text="Page",value=acx_build_page,options={"Concordia","Gambling","Gamblers"}}}
            for _,child in ipairs((pages[acx_build_page] or pages.Concordia)()) do children[#children+1]=child end
            return children
        end)()),
        tab("content","Items",{input("wcid","WCID or classname"),button("use_selected","Use selected WCID"),
            row("spawn",{button("create","Create in world"),button("ci","Create in inventory"),button("createinst","Create instance")}),transfers("weenies"),
            row("object",{button("getinfo","Get info"),button("removeinst","Remove instance"),button("delete","Delete selected")}),
            text("content_help","Instances persist in the world database. Create in world/inventory creates a live object. Server responses appear in chat.")}),
        tab("placement","Move",{{id="distance",type="number",text="Nudge distance (m)",value=c.distance,min=.001,max=100,step=.001},
            row("north",{button("nudge_nw","Northwest"),button("nudge_n","North"),button("nudge_ne","Northeast")}),
            row("middle",{button("nudge_w","West"),button("nudge_here","Move here"),button("nudge_e","East")}),
            row("south",{button("nudge_sw","Southwest"),button("nudge_s","South"),button("nudge_se","Southeast")}),
            row("height",{button("nudge_up","Up"),button("nudge_down","Down")}),
            {id="heading",type="number",text="Heading (0 = north, 90 = west)",value=c.heading,min=0,max=360,step=1},
            row("rotation",{button("rotate","Rotate"),button("rotate_here","Face my direction")}),
            text("placement_help","Placement edits the selected persistent landblock instance using ACE's nudge/rotate commands.")}),
        tab("landblocks","Land",{input("landblock","Landblock ID (hex)"),button("current_block","Use current landblock"),transfers("landblocks"),
            row("land_tools",{button("reload-landblock","Reload landblock"),button("myloc","My location"),button("clearcache","Clear server cache")})}),
        tab("quests","Quests",{input("quest","Quest name"),transfers("quests"),input("recipe","Recipe ID"),transfers("recipes")}),
        tab("generators","Spawns",{input("generator","Generator WCID or classname"),button("generator_create","Create generator instance"),
            input("parent","Parent GUID (0x...)"),button("get_parent","Use selected parent"),input("child","Child / encounter WCID"),
            button("link","Link child instance"),row("encounters",{button("addenc","Add encounter"),button("removeenc","Remove encounter")}),
            text("generator_help","Create or select a generator, capture its GUID, then add a child. Encounters use your current outdoor cell.")}),
        tab("files","Files",{text("folder",root~="" and root or "Default: ClientMods/Content. Choose your local ACE content folder or refresh the list."),
            {id="choose_folder",type="button",text="Choose content folder",action="content_folder"},button("server_folder","Show server content folder"),
            {id="kind",type="select",text="Content type",value=c.kind,options={"weenies","landblocks","quests","recipes"}},
            {id="format",type="select",text="Format",value=c.format,options={"json","sql"}},input("filter","Filter filenames"),button("scan","Refresh files"),
            {id="file",type="select",text="File",value=c.file,options=files},
            row("file_page",{button("prev","Previous",page>0),button("next","Next",(page+1)*128<total),button("edit","Edit file",c.file~="")}),
            text("file_help","Selecting a file fills its ID on the matching tab. Local folders use json/weenies, sql/weenies, landblocks, quests and recipes. Imports read the SERVER's content folder; remote servers need the edited file copied there first. Linux uses ClientMods/Content.")}),
        tab("help","Help",{text("guide","1. Choose an ACE content folder in Files.\n2. Export an existing object or choose a JSON/SQL file, then edit it.\n3. Import the matching ID.\n4. Create a live preview or a persistent instance.\n5. Position it and export the landblock."),
            text("access","Your server must grant the appropriate developer/admin commands. Enabling Foundry does not grant server privileges. No commands run automatically."),
            text("results",table.concat(history,"\n"))})}
    local ui={text("selection",selected_text),{id="tabs",type="tabs",children=tabs},text("status",status)}
    if pending then
        table.insert(ui,{id="review",type="section",text="Review content change",children={text("review_command",pending.text..(pending.target and " on "..selected_text or "")),
            row("review_buttons",{button("confirm","Confirm"),button("cancel","Cancel")})}})
    end
    local requests
    if #queue>0 and (s.time or 0)>=(c.next_send or 0) then
        requests={table.remove(queue,1)};c.next_send=(s.time or 0)+1.05
    end
    -- Idle callbacks contain only the small selection and no inventories/spellbooks.
    return {state=dirty and c or nil,ui=ui,requests=requests,status=status}
end
