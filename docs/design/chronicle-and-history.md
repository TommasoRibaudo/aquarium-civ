# Chronicle cards, the time-lapse and the small book (draft, 2026-10-29)

Status: **draft for the user's confirmation** (issue #56, milestone M4). Expands [game-definition.md](game-definition.md)
§7 "History". Binding inputs from [decisions.md](../decisions.md): **Gate 2** (cards = P3 A; time-lapse = P4 A + a small,
polished book of major events and people), **Relics** (the people work out what the glass is only through what they
say, never stated outright), **First session** (the glance is a one-line peek of the top card), **Session close** (the
evening talk after ~15 min, with a true teaser). Also [first-session-and-check-in.md](first-session-and-check-in.md),
[life-in-the-box.md](life-in-the-box.md), [notable-roles.md](notable-roles.md) (inherited lord, innkeeper),
[research 09](../research/09-relics-and-powerups.md) and the [Growth Lab](spikes/growth-lab.md).
**[est]** = my guess, to tune in play.

---

## Choices for the user

Recommended option first, marked ★.

1. **How many cards per return.** ★ **Scale with time away: up to 3 after < 1 h, 5 after 1–8 h, 7 after longer**;
   anything past the cap goes into one **"Also…" card** of three short lines · always 3–7 by salience, no "Also…" card ·
   no cap (every story is a card).
2. **Linked stories.** ★ **Merge into one card** when they share a thread (a wedding and the couple's first child; a
   lost sandal and its finding; a teased rivalry and the vote it led to) · one card per chronicle entry.
3. **The peek when nothing new happened** (decided: the glance shows a one-line peek). ★ **Show the current teaser
   line** ("Mira and Tob keep sitting together at the fire") · show nothing.
4. **"Show me".** ★ **Fly to the place, then a 3–5 s echo** by the people involved (the newlyweds sit on the bench, the
   new chief stands at the fire); if they've died, the place plus their remembering stone · fly to the place only ·
   a staged replay of the whole event.
5. **Time-lapse play speed.** ★ **The whole history in about 20 s, whatever its length**, with a 1 s pause at each era
   marker · a fixed rate (5 years a second: a 120-year box takes 24 s, a 10-year one 2 s).
6. **People in the time-lapse.** ★ **Still figures**: the crowd as that year's head count of peg dolls around homes
   and the green, and that year's living notables standing at their seat or home with their markers · no people
   (buildings and paths only) · people walking (costly, and too busy at that speed).
7. **What earns a page in the book.** ★ **A short rule, about 12–20 pages a box**: the founding, one spread per era,
   a page per **legend** (founders, leader-line role-holders, anyone with three big stories, the one you pinned
   longest, the eldest, a relic's owner; at most ~10), family trees only for legends' families, and the relics ·
   every notable gets a page (the P4 book, which felt "empty") · eras and relics only, no person pages.
8. **The book's look.** ★ **It changes with the era**: the camp's pages are painted stones and bark, the village's a
   hand-written book kept by the priest, the town's a printed, bound book · one polished book throughout.
9. **How the people's view of the glass grows.** ★ **Three stages tied to the eras, each pushed along by a relic**
   (the Sky Crumb, the Keeper Doll, the Lens: §5) · by era only · by relics only.
10. **Who says "the glass".** ★ **The card's narrator uses the player's words** ("the glass", "the Keeper's rain"),
    **the people's own quotes never say "glass"** until the town's scholars name it · both use the people's words
    only (the narrator says "the sky wall" too).
11. **Sharing.** ★ **A clean still from the time-lapse** (no HUD, a museum-style label: name, year, era) **for the
    slice; a short clip after the slice** · still and clip in the slice · no sharing yet.

---

## 1. What other games do (research)

Markers as in research 09: **[V]** primary source, **[S]** secondary (wiki, press, forum), **[est]** my reading.

| Game | How it keeps and shows history | What works | What to avoid | Lesson for us |
|---|---|---|---|---|
| **WorldBox** | Kingdoms rise, ally, war and rebel on their own [S]. A list of past kings, with who died [S]. A war log that **is lost when the war ends**; players ask for it to be kept and for kingdoms' lifetimes to be shown [S] | The world makes its own stories; players narrate them on Reddit [S] | History you saw happen and then can't find again | **Keep it, and make it findable.** Our reason to exist (pillar 1) |
| **Dwarf Fortress** (Legends mode) | Every figure who ever lived and died, every civ's history, a historical map; "a log of sorts for the entire history" [S]. Engravings in the fort **depict earlier events**, and the story behind each is in Legends [S] | Total recall; history shows up *in the world* (engravings). The famous stories (Boatmurdered) are retold by players [S] | A raw log you have to dig through; most players read it on fan tools [est] | **Curate.** Our book is the opposite of Legends: 15 pages, not 15 000 entries. Take the engravings idea: history visible in the box (a remembering stone, a relic on the shelf) |
| **Crusader Kings II** (the Chronicle) | Entries written by triggers: birth, death, marriage, battles, titles won or usurped, floods, bountiful harvests; it follows the line to the next character [S] | One chronicle per family line; events written as they happen | Most entries are dry ("X was born") [est] | **A trigger table per story kind**, which we already have (`SimCore.Story`). Write lines with a joke or a detail, never just the fact |
| **RimWorld** (art "tales") | Each sculpture gets a description drawn from **past events in the colony** (a fight, a recruitment, a surgery, someone on fire) plus stock phrases [S] | Old events resurface years later, in an object | — | **Old stories come back** through relics, book captions and the people's talk ("they still tell of the Sky Bread") |
| **Animal Crossing: NH** | A morning news broadcast; when nothing happened, a personal anecdote [S]. Villagers' letters in bottles on the beach, each personality writing its own [S] | A ritual opener; never "nothing happened"; **every character has a voice** | Weeds and villagers leaving while you're away [S] | **Each card in the people's voice**; a slice of life when there's no news (first-session §4) |
| **Neko Atsume** | Mementos each cat leaves; a photo album [S] (research 09) | Discovery on return, tied to a character | — | Relic cards belong to a person |
| **Civilization VI** (Endgame Map Replay mod) | Fans built a replay: the world from turn 1, cities founded, borders gained and lost, religions spreading, with controls to speed up and step back and forth [S]; other fan tools rebuild it from autosaves [S] | People want to **watch the map grow** after the fact | It needed mods and per-turn saves | **The time-lapse is playback of stored years**, not video. Store each year from the start |
| **Townscaper** | No history view; players record **time-lapse videos** of their towns growing [S] | The pleasure of watching a place assemble itself; small touches (birds on roofs, laundry lines) [S] | — | Our time-lapse should feel like that video: pop-ins, roofs, paths |
| **Our P3** (cards vs plain return) | A tester read all 7 cards (~10 s each) and recalled one notable by name with his friends and enemies; the plain return scored 1/1/1 | Named people with ties | Info dumps: "choose what to show and what to only show in the character sheet" (a tester) | Slim cards; detail lives on the person card |
| **Our P4** (time-lapse vs book) | The time-lapse was preferred ("clearer"); the book scored higher for "I'd show this to a friend", but "felt empty for something that should be important" (a tester) | Scrubbing (4 opens, ~172 years scrubbed, 5 pin taps) | A book listing everyone, with chapters of 25 years and repeated titles | Time-lapse first; a **short book of what matters** |

What we take: keep everything, show little (WorldBox's lesson against Dwarf Fortress's); triggers with a voice (CK2 +
Animal Crossing); old stories resurfacing (RimWorld, Dwarf Fortress engravings); replay from stored years (Civ VI
mods); the look of a town assembling itself (Townscaper videos).

---

## 2. Chronicle cards

### 2.1 Anatomy of a card

A slim card at the bottom, at most ~30 % of the screen height, so the box stays visible above it (P3 polish).

| Part | What | Example |
|---|---|---|
| **Portrait** | The person as a peg doll (style kit), in their family colour, with their role accent (sash, feather); two portraits overlap for a two-person story. Tap → their person card | Ada's doll with the chief's feather |
| **Headline** | One short line, also used by the peek (≤ ~50 characters) | "Ada and Bo had a daughter" |
| **Body** | 1–2 sentences from the story template, ≤ ~140 characters, with a detail or a joke | "They named her Pip. She already shouts louder than the loud one." |
| **Quote** (optional) | One line in the people's voice (§5), in italics, with who said it | *"The sky was bright the day she came." (Mira, the gossip)* |
| **Footer** | Year, era icon, the place | "Year 34 · by the well" |
| **Buttons** | **Show me** (§2.4), **Next** (or swipe), page dots, **Skip all**; per kind, one more (below) | |

Detail stays on the person card (traits, family, last stories), not on the chronicle card.

**Card kinds** (one look each; colour is never the only signal):

| Kind | Extra | Notes |
|---|---|---|
| Story | — | The common card |
| **Keeper's gift answered** | a small hand mark; "After the Keeper's rain…" | Already credited in the sim (`keeper`, salience 0.9) |
| **Wish** | **Give** (opens the tray with the tool and the spot) | At most one per return, no timer (research 10) |
| **Relic found** | the relic's picture and the people's label | Every relic is born in a card (research 09 §4) |
| **Legend** (a notable's death) | soft frame, **Remember** (opens their book page) | Warm, after the good news (decided) |
| **Era ready** | **Watch** | The era waits (P6). Also replaces the evening talk's teaser |
| **"Also…"** (Choice 1) | three short headlines, each tappable to its full card | Overflow only |

### 2.2 Ranking on return

1. **Collect** the unread entries since the last card the player saw. Never: mess, "came of age", job changes under
   0.5 salience. Unread cards wait for the next open; nothing is lost (first-session §3).
2. **Merge** entries that share a thread into one card (Choice 2); the card's score is its best entry's.
3. **Score** = salience + **0.15** if it's about the pinned person (P3) + **0.2** if it pays off a teaser the player
   heard (§2.3) + **0.05** if the player opened that person's card in the last session [est] − **0.15** for each card
   of the same kind already chosen (no three weddings in a row) [est].
4. **Pick** the top N (Choice 1). The rest go in the "Also…" card.
5. **Order** (good news first, decided):
   1. Era ready, if waiting (it's the biggest news, and it's good).
   2. The best good-news card (the headline of the return).
   3. The other good and neutral cards by score.
   4. Sad cards (deaths), warm, each with Remember.
   5. The wish card, last, so its Give leads into the shaping beat.
   6. "Also…".

Tone (good, neutral, sad) comes from a table per story kind (§6), not from the text.

### 2.3 Teaser payoffs from the evening talk

The evening talk (decided) says one true teaser drawn from the sim. The sim records it as a **thread** (kind, the
people involved, month). When a later entry resolves it (Mira and Tob marry; Ivo wins the chief's seat; the gift is
answered), the entry is linked to the thread and gets +0.2, so the payoff is on the next return, however late. The
card shows the old teaser as a small grey line above the headline: *"At the fire: Mira and Tob keep sitting
together."* No "quest complete", no counter. If a thread dies (a teased person dies first), the death card mentions it
gently instead ("Tob never did ask her").

### 2.4 The peek (the glance)

- One line at the bottom, above the safe area: the top card's headline, with its portrait. Doesn't cover the box.
- **Tap** → the full stack. **Swipe down** → hidden; the cards wait.
- On the first touch elsewhere (a wipe, a rotate) it shrinks into a small dot on the history button. A dot, never a
  number.
- With no new cards: the current teaser line (Choice 3).
- Under ~15 minutes away (P2's threshold) there's no peek and no wave, as decided.

### 2.5 "Show me" and its echo

1. **Fly-to** (≤ 1.2 s, eased): the camera frames the place in the part of the screen above the card. Place = the
   entry's building, else the person's current spot, else the spot where it happened (§6: positions).
2. **The echo** (3–5 s, Choice 4): the people involved interrupt what they're doing (life-in-the-box §10, a "story
   scene") and do a short pose of the story, then go back to their day.

| Story | Echo |
|---|---|
| Wedding | The couple sit side by side on the bench by the green |
| Birth | A parent holds the baby up; neighbours lean in |
| Friendship | The two chat, a shared bob |
| Rivalry / make-up | They turn their backs / share a pot |
| Role won | The new holder stands at the seat; a few people gather |
| Keeper's gift answered | The worker at the new field or building, with a tool raised |
| Invention, prank, turnip | The thing itself (the five-wheeled barrow, the pink roof, the turnip) next to its maker |
| Relic | The relic in the world, people around it pointing |
| Death | No echo: the remembering stone (life-in-the-box Choice 7) and a flower |
| Weather (storm, fog, snow) | No echo; the place only |

If the story is older than ~5 years [est] or the people have moved away from the place, it's fly-to only. The camera
returns to the default view when the stack closes.

---

## 3. The time-lapse screen

Opened from the **history** button (HUD, bottom row), or from a card's "Show me" once there's enough history
(first-session §2). The book is a tab on the same screen.

### 3.1 Layout (portrait)

- **The box** fills the top ~60 % (P4's layout, so panels never cover the village). One finger rotates, pinch zooms.
  No wiping here: the glass is shown clean and the light is a fixed late afternoon.
- **The year line** above the timeline: "Year 47 · Brightmere · the farming village · 23 people".
- **The timeline**, full width: year 0 at the left, now at the right.
  - **Era bands**: the bar is split into the camp, village and town stretches, each in its own colour *and* with an icon
    and a label at its start ("The village"). The cut is an **era marker**: a notch, the era's year, and the era card
    when you stop there.
  - **Story pins**: the top ~20–30 stories (salience, at most one pin per ~8 px [est]). A pin holds all the big stories
    of its year (P4's fix).
  - **The pinned person's life**: a thin gold band from birth to death (or now).
  - **The handle** with the year; drag anywhere on the bar; letting go near a pin snaps to it and shows its story in a
    slim card above the bar (same anatomy as §2.1, with "Show me" flying in the time-lapse world).
- **Buttons**: Play/Pause, **Now** (jump to today), **Snapshot**, the **Book** tab, Close.

### 3.2 Play

- About **20 s for the whole history** (Choice 5), with a **1 s pause at each era marker** and the era's name.
- A tap anywhere on the bar stops play there.
- Close returns to the live box exactly as it was (P4 checked this).

### 3.3 What's drawn per year

From the stored year (§6), between years a short pop-in (buildings scale up, paths fade up a tier):

| Drawn | From |
|---|---|
| Terrain, including the Keeper's shaping (channels, groves, hills, springs, meadows) | Shapings with their year and spot |
| Buildings on their lots, with **that year's look** (tent → hut → townhouse); gone or replaced ones removed | Lots with founded, retired and look-change years |
| Paths, trails and roads by tier, fields and crofts | The Growth Lab's tier grid, stored as yearly changes |
| The crowd: that year's head count as still peg dolls round homes, fields and the green | `population` |
| Named people alive that year, at their seat or home, with markers (gold for the pinned) | Born and died months, roles held that year |
| Relics lying in the world (the Sky Crumb until it's eaten) | Relic found and taken months |
| Remembering stones | Deaths |
| Not drawn: mess, weather, seasons | History looks its best; the year is one frame |

Tapping a building or a person in the time-lapse shows one line ("The mill, built in year 31 by Pell") and their
card; no actions.

### 3.4 Share

**Snapshot** saves a clean still (no HUD, no timeline), with a small museum-style label: the settlement name, the year,
the era. A short clip comes after the slice (Choice 11). The same screen shows a completed box in the shelf museum.

### 3.5 Cost notes (for M4)

The Growth Lab rebuilt the whole box per year in ~1 s in the Editor. The time-lapse needs incremental updates: going
forward, apply each year's changes; going back, rebuild from the nearest **keyframe** (every 10 years [est]) and
apply changes forward. Target: no hitch while scrubbing on a mid-range phone (practices §7).

---

## 4. The small book

Short and curated (decided). It's the history you'd show someone, not a log.

### 4.1 What earns a page (Choice 7)

| Page | Earned by | Shows |
|---|---|---|
| **Cover** | Always | The settlement name, the setting, years so far; a painted-look snapshot |
| **The founding** | Always | The founders, the first chief, the first line of the chronicle |
| **One spread per era** | The era began | A title from the era's top story ("The Years of the Sky Bread"), stored when the era ends so it never changes; a snapshot of the box at the era's end; 3–5 headlines; who led it |
| **A legend** | A notable who was a **founder**, held a **leader-line role** (chief, headman, lord), held any role ≥ 10 years, has **three stories at salience ≥ 0.7**, owns a **relic**, was the player's **pin for ≥ 1 real day** [est], or lived to be **the eldest**. At most ~10 a box; past that, the highest legend score (the sum of their top five saliences) wins | Portrait, name and an **epithet** from their best story ("Ada Lund, the Sky-Bread Chief"); born–died; job and roles with years; their 3 best stories; partner, children, friend, rival; their relic |
| **A family tree** | A family with a legend in it | Both parents shown (P4 polish: children listed under one parent read as half-siblings); legends highlighted |
| **Relics** | The first relic | Found ones with the people's label and their card; unfound as silhouettes with a hint in the people's words (decided C8) |
| **The end** | The box completes | The town at its peak, the time capsule, who carried on to the next box |

About 12–20 pages a box [est]. A new page shows a small ribbon on the Book tab; a legend's death card has
**Remember**, which opens their page.

### 4.2 The book's look (Choice 8)

★ The book changes as the people change: camp pages are **painted stones and bark** (pictures only, plus the
narrator's caption), the village's are **the priest's hand-written book**, the town's a **printed, bound book** with
the guild's woodcuts. All three live in the same book, so leafing through it is a walk through the eras.

---

## 5. The people's voice: the glass and the Keeper

Rule (decided): the people slowly work out what the glass is **only through what they say**, never stated outright.
What they say must reveal how they see the glass and the Keeper.

### 5.1 Rules for every line

- **Never stated.** No line explains the game. The people never say "aquarium", "box", "player", "phone" or "game".
- **The narrator and the people differ** (Choice 10): card bodies use the player's words ("the glass", "the Keeper's
  rain"), as the sim's lines already do. **Quotes, bubbles, teasers, relic labels and book captions** are the people
  and use theirs.
- **Belief moves forward only**, by era, and each stage is nudged along by a relic (Choice 9).
- **Not everyone agrees.** A believer and a sceptic in the same village is funnier and more human (the stubborn, the
  dreamer, the gossip and the grump are made for it).
- **Warm, always.** Mess is weather or a mood of the sky, never blame. A long absence makes them wonder, never
  complain ("The Keeper must be busy with very big things"). Laugh *with* them, never at them (research 09).
- **Era-gate the lines that know too much.** Today's `TraitStories` already include later-stage lines: "invented a
  mirror for signalling the Keeper", "tell the time from the glass's shadow", "a city beyond the glass", "the Keeper
  has a cat". Tag them town-only (§6).

### 5.2 The three stages

| Stage | When | What they think the glass is | What they think the Keeper is | Pushed along by |
|---|---|---|---|---|
| **1 · The sky wall** | Camp | The edge of the sky, hard and bright; it gets "dusty" and "clears" | Weather with a will: "the Bright One", someone who watches, a huge hand in the clouds | **The Sky Crumb**: things come *from* the sky |
| **2 · The Keeper's window** | Village | A window the Keeper looks through; the shrine faces it; the priest climbs the Keeper's hill to be closer | Someone, with moods and habits; you can wave and they wave back; the **Keeper Doll** (one huge hand, one huge eye) | **The Keeper Doll**: they've made a picture of you |
| **3 · A wall, and a room beyond** | Town | The scholars name it: **glass**. It has four edges ("Here be glass" on the map). Beyond it, a vast room | Debated: one Keeper or many? Does the Keeper sleep? Has a cat (the gossip insists). "The Keeper, not a King" | **The Lens**: they watch you back |

Box 2 onward: they speak of "the old country", and sometimes glance toward the old box on the shelf (research 09's
Echo Shard and Time Capsule).

### 5.3 Example lines

| Moment | Camp (the sky wall) | Village (the Keeper's window) | Town (a wall, and a room beyond) |
|---|---|---|---|
| **You return** (a wave) | "The Bright One is back!" | "Wave, it's the Keeper! Wave properly, Pip." | "Window's lit. Someone fetch the astronomer." |
| **A full wipe** | "Look at that sky!" (first session) | "The Keeper has cleaned the window. Hats off." | "The glass is clear: a good day for measuring." |
| **Grime, dim light** | "The sky's gone dusty again." | "The window's misted. The Keeper will see to it." | "Thick glass weather. The lamp-lighters start early." |
| **Rain** | "The sky is crying for us. Get the baskets!" | "Keeper's rain on the east field. Told you." | "Rain from a clear glass. The guild says it's condensation. The priest says otherwise." |
| **Away a long time** | "The sky was quiet a long time." | "The Keeper must be busy with very big things." | "The observatory reports: the room has been dark for days. They lit a candle for you." |
| **A death** | "Old Ada went up past the sky." | "We buried Ada facing the window, so she can see the Keeper." | "Ada asked to be buried by the glass. 'Closer to the room,' she said." |
| **Evening teaser** | "Ivo wants to speak at the fire. The chief has noticed." | "The elders say the Keeper likes the new mill. Nobody knows how they know." | "The astronomers have asked for a bigger lens." |
| **Relic label** | Sky Crumb: "Sky Bread. Fell when the Bright One made the trees. Delicious." | Keeper Doll: "The Keeper. Hands: very large. Eye: always open." | The Lens: "The Great Eye-Glass. Through it: a room, a lamp, and something with whiskers." |
| **Book caption** | (painted) "The year the sky gave bread." | "In the time of headman Bo, the Keeper sent water down the hill." | "Chronicle of Brightmere, printed in the year 96, by order of the lord." |

The relic book's hints use the same voice: "The old ones say something fell from the sky once…" (research 09 §4).

---

## 6. What the sim must record that it doesn't yet

Today: `ChronicleEntry` (month, kind, text, salience, notable, other notable, building, keeper), `Notable` (born, died,
family, partner, friend, rival, mentor, job), `YearSnapshot` (year, population, buildings count, notables count, mood,
era), `storyFlags`, `settlementName`, `pinnedNotableId`.

| Needed for | Add | Notes |
|---|---|---|
| Unread cards | `lastSeenEntryId` (or a `seen` flag per entry) | So skipped cards wait for the next open |
| Peek, "Also…" | A **headline** per entry (a short form per template) | ≤ ~50 characters |
| Good news first, echo, book | A **kind table** in code: tone (good / neutral / sad), echo, book-worthy, era gate for its lines | Code, not save data |
| People's quotes | `quote` + `speakerId` on an entry | Optional; filled from per-stage line lists |
| Merged cards, teasers | `threadId` on entries; a **Thread** list (id, kind, people, month, paid off) | The evening talk writes a thread; a resolving entry links to it |
| Fly-to and echo | **x, z** of where it happened | Needs the Growth Lab placement in the sim (M2; Growth Lab gap 6) |
| Roles in cards, book, time-lapse | **Role history**: role, holder, from, to, how won | #27 adds current seats; the book needs past holders too |
| Relics | A relic list (id, found month, owner, spot, taken month) and `relicId` on entries | Research 09 slice relics |
| The time-lapse | Per building: **lot x, z, yaw, size**, **retired month**, **look changes** (month, look). Per year: **path-tier and laid-cell changes**, shapings with x, z (channels, groves, hills…), remembering stones | Store changes per year, not full grids (Growth Lab: 2 × 2 640 bytes a year otherwise); keyframes every ~10 years for scrubbing back |
| The people's voice | **Belief stage** (1–3) and the relic flags that pushed it | `storyFlags` can hold the flags |
| Book | **Era titles** stored when an era ends; pin time per notable (for the "pinned ≥ 1 day" rule); era start months (all eras, not just the current one) | So pages never change after the fact |
| Chronicle size | Keep everything book-worthy; after ~30 years [est] drop low entries (< 0.5) that aren't on anyone's top 5 | A long box stays small to save |

Debug panel (CLAUDE.md rule), with M4: a return with N cards, each card kind now, a teaser and its payoff, force a
legend's death, add a relic, set the belief stage, jump the time-lapse to an era, earn every book page, clear "seen".

Metrics for M4's "done when": cards shown/read, dwell per card (paused while a person card or fly-to covers it, P3
polish), Show me, Skip all, peek taps; history opens (unprompted or not), scrub years, pins tapped, plays, snapshots,
book pages turned, Remember taps; the recall question in the slice survey.

---

## 7. UI elements (for M4)

All in the thumb zone and the safe area, 44 pt targets (practices §5–6).

| Element | Where | Notes |
|---|---|---|
| **Peek** | Bottom, one line | Portrait + headline; tap → stack; swipe down hides; shrinks to a dot on the history button |
| **Card stack** | Bottom, ≤ 30 % of the height | §2.1; page dots, Next/swipe, Skip all, Show me, per-kind button (Give, Remember, Watch) |
| "Also…" card | In the stack, last | Three tappable headlines |
| Teaser line on a card | Above the headline | Small, grey |
| Fly-to + echo | World | Camera frames above the card; story scene in the planner |
| **History button** | HUD bottom row | Opens the time-lapse; a dot for unread cards or a new page |
| **Time-lapse view** | Top ~60 % world | Clean glass, fixed light, rotate and zoom only |
| Year line | Above the timeline | Year · name · era · people |
| **Timeline** | Bottom | Era bands with icons and labels, era markers, story pins, the pin's gold life band, the handle |
| Pin story card | Above the timeline | Same anatomy as a chronicle card |
| Play/Pause, Now, Snapshot, Close | Bottom row | |
| Era card | Over the world at a marker | The era's name and title, 1 s during play |
| Snapshot label | In the saved image only | Name, year, era |
| **Book tab** | Same screen | Ribbon on new pages |
| Book pages | Full screen, swipe to turn | Cover, founding, era spreads, legends, family trees, relics, the end; the era look (Choice 8) |
| Person card link | From portraits anywhere | Existing person card (M3) |
| World bubbles | World | Quotes in the people's voice (§5) |

---

## Sources

- WorldBox: [Wikipedia](https://en.wikipedia.org/wiki/WorldBox) [S]; past kings list and war logs, [App Store changelog](https://apps.apple.com/app/id1450941371) [S]; war log lost after the war, [r/Worldbox update overview](https://reddit.fabiomanganiello.com/r/Worldbox/comments/11pogci/the_new_update_an_overview_by_someone_who_played) [S]; kingdom lifetimes requested, [WorldBox idea box](https://ideabox.featureupvote.com/?tag=UI) [S].
- Dwarf Fortress: [DF wiki, Legends](https://dwarffortresswiki.org/index.php/Legend) [S], [Giant Bomb](https://giantbomb.com/wiki/Games/Dwarf_Fortress) [S], [DF wiki, Engraving](https://www.dwarffortresswiki.org/index.php/DF2014:Engraving) [S], [Boatmurdered, Wikipedia](https://en.wikipedia.org/wiki/Boatmurdered) [S].
- Crusader Kings II Chronicle: [CK2 wiki, Chronicle](https://ck2.paradoxwikis.com/Chronicle) [S].
- RimWorld: [RimWorld wiki, Art](https://rimworldwiki.com/wiki/Art) [S].
- Animal Crossing: New Horizons: [Nookipedia, Message bottle](https://nookipedia.com/wiki/Message_bottle) [S], [Nookipedia, New day](https://nookipedia.com/wiki/New_day) [S].
- Neko Atsume: research 09 §1.1 sources [S].
- Civilization VI replays: [NME on the Endgame Map Replay mod](https://www.nme.com/news/civilization-6-endgame-mod-lets-players-relive-victories-in-minimalist-form-3105796) [S], [CivVIReplay, CivFanatics](https://forums.civfanatics.com/resources/civvireplay.28843) [S], [civ6egrm on GitHub](https://github.com/samuelh91/civ6egrm) [S].
- Townscaper: [Destructoid](https://destructoid.com/?p=258363) [S], [RAWG listing with player time-lapse videos](https://rawg.io/games/449164) [S].
- Our own data: [P3](results/P3.md), [P4](results/P4.md), [Growth Lab](spikes/growth-lab.md) §1, §7.
