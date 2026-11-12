-- Project ACX test kit. Install: copy this folder to Saved/ClientPlugins/Installed/acx_tools,
-- then Manage Plugins > Reload installed plugins, enable ACX Tools and open its window.
-- Buttons queue server commands; the client sends at most one per second.

local commands = {
  god = '/god', ungod = '/ungod',
  market = '/teleloc 0x016C01BC [49.2 -31.9 0.005]',
  dummy = '/create 900015', debug = '/debugdamage defense', fi = '/fi',
  sword = '/ci 351', shield = '/ci 91', nodachi = '/ci 40760', spear = '/ci 348', yari = '/ci 362',
  lootsword = '/lootgen 351 6', nsword = '/lootgen 900003 6', nbow = '/lootgen 900012 6',
  naxe = '/lootgen 900103 6', arrows = '/ci 43952 200',
  dps_start = '/dps start', dps_show = '/dps', dps_stop = '/dps stop',
}

local pending, replies = {}, {}
local next_send, serial, status = 0, 0, 'Ready'

local function button(id, text, tooltip)
  return {id = id, type = 'button', text = text, tooltip = tooltip or commands[id]}
end

return function(s, p)
  local now, requests = s.time or 0, {}

  for _, e in ipairs(s.events or {}) do
    if e.type == 'click' and commands[e.id] and #pending < 16 then
      pending[#pending + 1] = commands[e.id]
      status = 'Queued ' .. commands[e.id]
    elseif e.type == 'click' and e.id == 'clear' then
      replies = {}
    elseif e.type == 'result' then
      status = e.ok and 'Sent' or (e.error or 'Command rejected')
    end
  end

  -- server replies (system lines, no sender), newest last
  for _, e in ipairs(s.chat_events or {}) do
    if (e.sender or '') == '' then
      replies[#replies + 1] = (e.text or e.raw_text or ''):sub(1, 600)
      if #replies > 12 then table.remove(replies, 1) end
    end
  end

  if #pending > 0 and now >= next_send then
    serial = serial + 1
    local text = table.remove(pending, 1)
    requests[1] = {id = 'send_' .. serial, service = 'chat', operation = 'send', text = text}
    status = 'Sending ' .. text
    next_send = now + 1.1
  end

  return {
    requests = requests,
    ui = {
      {id = 'setup', type = 'section', text = 'Setup', expanded = true, children = {
        {id = 'setup_row', type = 'row', children = {
          button('god', 'God mode'), button('ungod', 'Normal mode'),
          button('market', 'Go to test chests', 'Marketplace loot chests')}},
        {id = 'target_row', type = 'row', children = {
          button('dummy', 'Spawn target dummy', '1,000,000 health, never fights back (/create 900015)'),
          button('debug', 'Debug damage', 'Appraise a dummy first, then press (/debugdamage defense)'),
          button('fi', 'Fix invisible', '/fi')}}}},
      {id = 'weapons', type = 'section', text = 'Weapons (into your pack)', expanded = true, children = {
        {id = 'plain_row', type = 'row', children = {
          button('sword', 'Long Sword'), button('shield', 'Kite Shield'), button('nodachi', 'Nodachi'),
          button('spear', 'Spear'), button('yari', 'Yari')}},
        {id = 'loot_row', type = 'row', children = {
          button('lootsword', 'Looted sword T6', 'Tier 6 magic Long Sword, to check Arcane Lore (/lootgen 351 6)'),
          button('nsword', 'Nether sword'), button('nbow', 'Nether bow'), button('naxe', 'Nether axe'),
          button('arrows', 'Prismatic arrows')}}}},
      {id = 'dps', type = 'section', text = 'DPS test (60 seconds from your first attack)', expanded = true, children = {
        {id = 'dps_row', type = 'row', children = {
          button('dps_start', 'Start'), button('dps_show', 'Show'), button('dps_stop', 'Stop')}}}},
      {id = 'status', text = status},
      {id = 'replies', type = 'section', text = 'Server replies', expanded = true, children = {
        {id = 'reply_lines', text = #replies > 0 and table.concat(replies, '\n') or '(none yet)'},
        button('clear', 'Clear', 'Clear this list')}},
    }
  }
end
