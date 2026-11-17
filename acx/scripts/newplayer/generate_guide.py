#!/usr/bin/env python3
# Usage: python3 acx/scripts/newplayer/generate_guide.py acx/Content/newplayer/10-acx-guide.sql
# Writes acx/Content/newplayer/10-acx-guide.sql: the ACX Guide book (JJ 2026-11-16: "Write the guide book like a book
# for dummies", then on the draft: "its a story and it flows. it should be a little entertaining to read", "in Asherons
# call fashion I don't tell them everything I just dance around it", "These will all be vet players"; most pages are JJ's
# own words). Each page must fit the book window's 1,000 characters.
import sys

OUT = sys.argv[1]
WCID = 900900

PAGES = [
'''Welcome to ACX!

This is considered a slow roll server, so take your time. Make new friends, even if it's just the NPCs. That's okay: NPC lives matter too!

Inside this book:
2   Your skills
3   Thrown weapons
4   The Alchemist
5   Fighting styles
6   When you die
7   Jobs, contracts and Marks
8   Concordia
9   Invasions
10  Scraps and scrolls
11  Money and the bank
12  Fellowships and T.A.R.
13  Commands''',

'''Your skills

Good news first: the trade skills are on the house. Cooking and all four Tinkering skills are trained for you the moment you arrive, so save your credits for the fight.

All but Alchemy and Fletching, which feed the fight. Alchemists throw bombs (page 4), and only a specialized Alchemist can handle them. Fletching makes an archer's best arrows.

Regret your weapon already? Up to level 20, the Thrown Weapons Trainer and the Alchemist Trainer by Holtburg's lifestone offer one fresh start: every skill cleared, theirs specialized, a starter weapon, and a trip to Concordia to rebuild the rest.

Older and wiser? The gems beside Concordia's lifestone still let you rework your skills and attributes, one gem of each kind an hour. Up to level 20 there's no wait at all.''',

'''Thrown weapons

Javelins, darts, throwing axes, daggers and friends. They never run out: a thrown weapon is its own ammo and finds its way back to your hand after every throw.

They use Missile Weapons, and Strength puts the muscle behind them. Carry a shield if you like.

Picked Missile Weapons without Fletching? Then you started with one already. Wanted a bow after all? Any bowyer sells them, and Fletching makes their best arrows.

WARNING: A shield makes you throw slower.

TIP: Mahogany salvage raises the Damage Modifier, just like on a bow.

Weapon shops sell plain ones. The better ones are out there in the loot, and from tier 5 some of them carry an element.''',

'''The Alchemist

Bombs need Alchemy, specialized. They never run out, and Alchemy, not Strength, makes them hit harder.

What a bomb does: it vulns and physically damages your target and the monsters around it.

The eight bombs: Fire, Frost, Acid, Lightning, Razor, Shrapnel, Concussion and Nether. A Nether bomb strips armor instead.

Potency is a bomb's damage multiplier. Examine a bomb and it shows as Damage Modifier.

REMEMBER: A monster can shrug off the vuln. The physical damage still lands.

TIP: Obsidian salvage adds 4% Potency each time you apply it.

WARNING: Bombs can't be imbued. The mixture inside is their imbue.

Bombs throw a little faster than other thrown weapons, and a shield slows them down like any thrown weapon.

The Healing Bomb (from the Mark Trader) hurts nothing. It heals you and the players and pets near where it lands, and slows the healing of monsters caught in it.''',

'''Fighting styles

No off-hand? No problem. A one-handed weapon with nothing in the other hand is faster, and it helps your evasion.

Two-handed weapons are smooth and true. They seem to land crits more than others.

Spears: one-handed spears are so powerful they can hit the enemy behind your target.

Dual wield: you are the sewing machine of the server, but it comes at a cost. Kill it quick or you will be lost.

Axes have been refined with granite. No more 0-point hits.

TIP: Every style is balanced, so pick what calls you and explore Dereth again.''',

'''When you die

Below level 10, fret not: Asheron is looking after you.

Above level 10, don't worry about clutching your coins like a miser. If you die, you only need to collect them. No misers are needed here, so spend your XP elsewhere.

Don't worry about vitae if you have a friend, for they can come and rescue you with no punishment to be had.
No friend? That's okay. Recover your corpse for a break on the vitae.
Unable to recover? That's okay too. It's only coins, but the evidence will remain.

If you come back as a ghost, fret not! It only lasts 5 minutes, and you can always visit the lifestone with /release.

Make sure you and your buddy have visited the Life Magic scrivener in Arwic or the other dire towns and purchased some insurance for your travels.''',

'''Jobs, contracts and Marks

Nothing to do? Don't know what to do? Visit a job board and get to work. /job take and the job's number send you off on adventures with lots of useful rewards.

Ignore that cranky Job Runner. I just overheard him say FML, so he's likely in a mood.

Out of jobs, or is your significant other in Target? That's okay: you can pick up three contracts a day from a Town Crier near you. Don't get too excited, though. The gem he gives you crumbles in 24 hours, so don't delay. Get them done! Feel free to bring a buddy: type /sc to share your contract if your buddy hasn't done theirs.

ACX Marks are the coin of our land, and you can earn them almost any way. Don't forget to look for exploration markers, go fishing, or gamble if you feel lucky. Then visit the Mark Trader to see what he has.''',

'''Concordia

Unfortunately, the places once called the Marketplace and the Town Network were so neglected that they collapsed in on themselves, and they can no longer be reached. But fret not: Concordia has been founded, and it's even better. Look for its portal in any town, then explore all it has to offer.

The houses of Dereth were left abandoned for so long that roaches took over. Their vaults were salvaged and brought to Concordia, where they're available for lease. You may lease up to two. Make a key to give to a friend, or rekey the lock and kick them out of your chest.''',

'''Invasions

Dereth is not safe, not even in towns. Every now and then a challenger arrives. Our scouts keep a lookout and will give you a warning.

I hear the attackers carry special loot, like stolen Marks. If you don't make it in time, don't worry. I'm sure there will be plenty to find once you finally arrive.''',

'''Scraps and scrolls

So you found a weapon and it's almost perfect, if only you could add this spell or change its element. Perhaps some scraps from the items you collect can give you what you need.

Maybe if you type /smartsalvage, you can make it a little easier.

Technology has advanced, and the archmages have been hard at work. They've developed special scrolls of all kinds: scrolls that help a buddy, scrolls that make changes to your weapons, and maybe a boost to tap into your inner Heisenberg. "Say my name!"

If you get that feeling and you need that component healing, try /autofillcomps for your satisfaction.''',

'''Money and the bank

Your bank goes everywhere with you. No banker needed.
- /bank shows your balance.
- /b d a deposits everything you carry: pyreals, trade notes and other currencies.
- /b w <amount> takes pyreals out.
- /b w n MMD takes out a 250,000 note.
- /b t <amount> <name> sends pyreals to a friend.

Wealth rank: what you have banked in pyreals sets your place on the wealth leaderboard (/top wealth).

Selling: the more you trade with a town's shops, the more they pay for your items.

REMEMBER: Pyreals in your pack drop on your corpse when you die. Pyreals in the bank are safe.''',

'''Fellowships and T.A.R.

Fellowships share experience by level. Friends close in level share evenly, and the wider the gap, the less there is to go around. Past level 180, though, everyone is a peer, and all share equally.

T.A.R. (Type, Area, Rest) is there to help us all stop and smell the roses. Hunt the same kind of monster in the same spot, hour after hour, and the rewards slowly fade. Wander, try something new or take a break, and they come right back.

/tar shows where you stand, and /xph shows your experience per hour.''',

'''Commands cheat sheet

/job  your town jobs
/contract  your contracts
/sc  share a contract
/bank or /b  your bank
/vault  your vaults
/invasion  invasion going on
/top  leaderboards
/explored  how much you've seen
/tar  your T.A.R. numbers
/xph  experience per hour
/myquests  your quests and timers
/ghost  time left as a ghost
/release  go to your lifestone
/smartsalvage  Ust settings
/guarantees  crafting guarantees
/autofillcomps  component levels
/fi  fixes invisible things
/mr  mansion recall
/who  allegiance online

That's it. Have fun out there!

Think you've got it all figured out? Show this book to a Town Crier.''',
]


def sql_str(s):
    return "'" + s.replace("\\", "\\\\").replace("'", "''").replace("\n", "\\n") + "'"


bad = [(i + 1, len(p)) for i, p in enumerate(PAGES) if len(p) > 1000]
if bad:
    sys.exit(f"pages over 1,000 characters: {bad}")

lines = [
    "-- Project ACX (2026-11-16): the ACX Guide, a book every character gets once at its first login (WorldObjects/",
    "-- Player_AcxGuide.cs; JJ: \"We can hand off a book as part of the new player set up when they first login\", then \"Write",
    "-- the guide book like a book for dummies\", then \"its a story and it flows\"; most pages are JJ's words). Prewritten pages nobody can edit; worth nothing, not attuned.",
    f"--   {WCID}  ACX Guide: retail's The Wedding Guide (6437) with ACX's pages ({len(PAGES)} pages, at most 1,000 characters each)",
    "-- GENERATED by acx/scripts/newplayer/generate_guide.py (the page texts live there): edit the pages there, or",
    "-- here by hand and keep each under 1,000 characters. Copies already handed out keep their old pages. Re-runnable.",
    f"DELETE FROM weenie WHERE class_Id = {WCID};",
    f"INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT {WCID}, 'acx-guide', type, NOW() FROM weenie WHERE class_Id = 6437;",
    f"INSERT INTO weenie_properties_int (object_Id, type, value) SELECT {WCID}, type, value FROM weenie_properties_int WHERE object_Id = 6437 AND type NOT IN (5, 19, 33, 114);   -- not burden, value, bonded, attuned",
    f"INSERT INTO weenie_properties_int (object_Id, type, value) VALUES ({WCID}, 5, 25), ({WCID}, 19, 0);   -- light, worthless; not attuned (JJ 2026-11-16: \"It doesnt have to be\")",
    f"INSERT INTO weenie_properties_float (object_Id, type, value) SELECT {WCID}, type, value FROM weenie_properties_float WHERE object_Id = 6437;",
    f"INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT {WCID}, type, value FROM weenie_properties_bool WHERE object_Id = 6437;",
    f"INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT {WCID}, type, value FROM weenie_properties_d_i_d WHERE object_Id = 6437;",
    f"INSERT INTO weenie_properties_string (object_Id, type, value) SELECT {WCID}, type, value FROM weenie_properties_string WHERE object_Id = 6437 AND type NOT IN (1, 15, 16);",
    f"INSERT INTO weenie_properties_string (object_Id, type, value) VALUES ({WCID}, 1, 'ACX Guide'),",
    f"    ({WCID}, 16, 'The easy way to learn what''s new on ACX: skills, thrown weapons and bombs, death and ghosts, jobs, Marks, Concordia and the commands.');",
    f"INSERT INTO weenie_properties_book (object_Id, max_Num_Pages, max_Num_Chars_Per_Page) VALUES ({WCID}, {len(PAGES)}, 1000);",
    "INSERT INTO weenie_properties_book_page_data (object_Id, page_Id, author_Id, author_Name, author_Account, ignore_Author, page_Text) VALUES",
]
rows = [f"    ({WCID}, {i}, 4294967295, '', 'prewritten', 0, {sql_str(p)})" for i, p in enumerate(PAGES)]
lines.append(",\n".join(rows) + ";")
open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print(f"{len(PAGES)} pages, longest {max(len(p) for p in PAGES)} characters -> {OUT}")
