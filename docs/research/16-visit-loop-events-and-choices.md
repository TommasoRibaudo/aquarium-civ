# 16 · The visit loop: events you see, choices you own

Written 2026-11-06, no Unity, nothing run on a phone. Tags: **[V]** read in a primary source (the developer speaking,
their slides, vendor docs), **[S]** secondary (wiki, press, forum), **[I]** my own inference from this repo or from
general practice. **[est]** marks a number I made up to start tuning from. Repo facts are cited by file.

## 1. The problem

The user's playtest notes (2026-11-06, paraphrased closely):

- They check in every 4 to 5 hours. Each return opens a stack of story cards. They **read** about events and never
  **see** them.
- A dilemma sits in the stack that **already resolved while they were away**, with mild results. "I don't care about the
  event or the consequences because I didn't make the choice."
- Lots of stories, few that land. What did land was **visible change** (seeing the new chief).
- The Camp era runs about 20 watched hours and feels like nothing happens.
- Their direction: events should happen where the player can see them. Fewer stories, bigger ones, never all at once,
  and how often they come should grow with the box. A meaningful choice on every visit, or close to it. Use events and
  choices to set the pace. Their question: **should a choice be non-skippable?**

### Our numbers today (code and docs)

| Thing | Value | Source |
|---|---|---|
| Time | 1 real hour = 1 game year, so 1 game month ≈ 5 real minutes | `decisions.md` 2026-10-28 |
| Cards on a return | up to 3 after < 1 h away, 5 after 1 to 8 h, 7 after more, plus an "Also…" card | `SimCore.CardCap`, `chronicle-and-history.md` §2.2 |
| Dilemma rate | one per 6 to 10 game years (`dilemmaMinYears`/`MaxYears`), 8-month gap after each, shares the wish slot | `SimState.cs`, `P7.md` Pacing |
| Dilemma lapse | 24 game months = **2 real hours**, then the people decide | `SimCore.Dilemmas.cs` (`DilemmaLapseMonths`) |
| Camp length | camp starts about year 3.5 (with care), village about year 24.5: **~21 real hours** | `keepers-hands.md` §2.2 |
| Camp dilemmas written | 4 (`camp_fire`, `camp_winter`, `camp_remember`, `camp_travellers`) | `P7.md` §Dilemmas |

**Why the user missed the choice, in numbers [I].** A dilemma is raised at a random moment in the sim. It is answered
only if the next visit comes within 2 real hours. With a visit every 4.5 hours, that is about **2 / 4.5 ≈ 44 %** of
daytime dilemmas, and about **0 %** of ones raised in the first 6 hours of a night. Over a day the user meets roughly
one dilemma in three while it is still open. The P7 lab could not show this: there a dilemma was raised while the tester
was watching and lapsed after 4 real minutes (`P7.md` #238). **The lapse window, not the choice design, is what broke
ownership.**

**Why the cards don't land [I].** Away time runs the sim at full speed, so a 4.5-hour absence is 4.5 game years of
weddings, births and role changes. The 5-card cap keeps the list short but every card is a past event in text. The echo
("Show me") exists but is opt-in per card, and the camp has few people, so most cards are small.

## 2. What other games do

### 2.1 Storytellers and directors: pacing events against attention

| Game | How it paces events | Evidence |
|---|---|---|
| **Left 4 Dead** (AI Director) | Tracks each player's "intensity", which rises with damage and nearby kills and decays over time. Four states: **Build Up** (full threats) until intensity peaks; **Sustain Peak** 3 to 5 s; **Peak Fade** (minimal threats until intensity drops, waiting for "a natural break in the action"); **Relax**, minimal threats for **30 to 45 s**. The reason given: "Constant, unchanging combat is fatiguing. Long periods of inactivity are boring. Unpredictable peaks and valleys of intensity create a powerfully compelling … experience." Boss encounters are not throttled | Mike Booth, *The AI Systems of Left 4 Dead*, Valve 2009 slides [V] ([PDF](https://steamcdn-a.akamaihd.net/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf)) |
| **RimWorld** (storytellers) | **Cassandra**: 4.6 days "on", 6 days "off", 1 or 2 major threats per "on" phase, at least **1.9 days** between major threats, a minor event every ~4.8 days on average. **Phoebe**: 8 days on, 8 off, exactly one major threat per cycle, so **8 to 24 days** apart. **Randy**: no cycle. Threat size scales with colony wealth, colonist count and time since the last big event; a "population intent" curve makes new-colonist events likely at 0 to 5 colonists and close to zero at 12+ | Community wiki reading the game's data [S] ([Cassandra](https://rimworldwiki.com/wiki/Cassandra), [Phoebe](https://rimworldwiki.com/wiki/Phoebe_Chillax), [storytellers](https://rimworldwiki.com/wiki/AI_Storytellers)). Sylvester calls RimWorld a story generator in his GDC 2017 talk [S] ([GDC Vault](https://gdcvault.com/play/1024232/-RimWorld-Contrarian-Ridiculous-and)) |
| **Crusader Kings 3** | Random events come in weighted **pulses**, one set every 1 to 5 years, and most pulses include a weight for **no event at all**. Ignored choices pick the first option after 4 months. Court events are different: "the player always **opts into** a court-type event and thus can't have one pop up unexpectedly"; ignored ones get a neutral option after a while | Paradox wiki [S] ([Events](https://ck3.paradoxwikis.com/Events)); dev diary 75 via press [S] ([GameWatcher](https://www.gamewatcher.com/news/crusader-kings-3-dev-diary-75-court-type-events-duels)) |
| **Stellaris** | Closing an event pop-up without choosing picks the first option | Paradox wiki [S] ([Events](https://stellaris.paradoxwikis.com/Events)) |
| **Reigns** | The next card comes from a "bag": cards whose conditions fail are removed, **recently played cards are removed** (a cooldown), bigger cards take more of the bag. When a storyline ends its cards leave the bag. Players read meaning into chance links, which the team then leaned into | Game Developer deep dive by the designer [V] ([link](https://www.gamedeveloper.com/design/game-design-deep-dive-creating-an-adaptive-narrative-in-i-reigns-i-)) |
| **Wildermyth** | Picks from all events that fit the situation, weighted against what this player has recently seen; most themed events run **once per campaign**. Between chapters an "interval" of years passes with short "tidings" | Official wiki [S] ([Event](https://wildermyth.com/wiki/Event), [Event Types](https://www.wildermyth.com/wiki/Event_Types)) |
| **Frostpunk 1 / 2** | Laws have a cooldown before the next (Frostpunk 2: about 10 weeks). Storms are forecast before they hit | Community wikis [S] ([Frostpunk 2 laws](https://www.gamerguides.com/frostpunk-2/guide/communities-and-factions/laws)) |
| **Timberborn** | Droughts and badtides are forecast before they arrive, so the event has a build-up the player sees | Official wiki [S] ([Drought](https://timberborn.wiki.gg/wiki/Drought)) |

**What this tells us.**
- Nobody fires big events on a flat random clock. They use a **cycle with rest** (L4D, Cassandra, Phoebe), a
  **minimum gap** (RimWorld), a **cooldown on repeats** (Reigns, Wildermyth), and often a **weighted "nothing"** (CK3).
  [V/S]
- Frequency scales with the size of what you have (RimWorld: wealth and colonists). Phoebe, the "chill" storyteller,
  is the closest to our pillars: one big thing per cycle, long gaps. [S]
- Directors measure the player, not only the world. L4D's whole point is "is the player busy right now?" Our
  equivalent is "is the Keeper looking at the box right now?" [V for L4D, I for us]

### 2.2 Presence: what happens while away vs while watching

| Game | Pattern | Evidence |
|---|---|---|
| **Our own decisions** | Eras **wait** for the player to watch the moment (P6); **no relics are found while the player is away** (2026-10-30); the evening talk plays only in a long live session | `era-moment.md`, `decisions.md` [V] |
| **Animal Crossing: New Horizons** | Special visitors come one or two a day on a random schedule; you meet whoever is there when you play. A villager's move-out ping reportedly **comes back every day until you answer it**, and only one villager pings per day | Visitor guide [S] ([Nintendo Wire](https://nintendowire.com/guides/animal-crossing-new-horizons/special-visitor-appearance-rates/)); player reports [S, folklore] ([Bell Tree](https://www.belltreeforums.com/threads/telling-the-difference-in-pings.333537), [Bell Tree](https://www.belltreeforums.com/threads/villager-moves-out-ping-question.290484)) |
| **Animal Crossing**, the counter-example | Isabelle's daily announcement on first load cannot be skipped; fans asked for a skip button and called the "intro jabber" a step back | Comments reported by Nintendo Life [S, anecdotal] ([link](https://www.nintendolife.com/news/2020/07/random_isabelle_reclaims_the_official_animal_crossing_twitter_account_but_fans_have_mixed_feelings)) |
| **Cozy Grove** | Story is released in daily portions; Spry Fox's David Edery: "less about placing limits on the player" than "pacing the narrative content so that we deliver an enjoyable episodic experience over months of time", so you have "something to look forward to tomorrow". Some reviewers found the daily cap thin | Edery to Vice [V, interview] ([Vice](https://www.vice.com/en/article/bvx9vw/cozy-grove-is-ok-telling-players-to-log-off-its-delightful)); reviews [S] ([GameSpew](https://www.gamespew.com/2021/04/cozy-grove-review/)) |
| **Fallen London** | A small hand of opportunity cards (2 to 5 by lodgings) that stay in the hand until played or discarded; the deck refills over time. The story waits for you, capped by hand size | Fan wiki [S] ([Cards](https://fallenlondon.wiki/wiki/Cards)); Emily Short on the opportunity deck as "narrative deck-building" [V] ([blog](https://emshort.blog/2016/04/12/beyond-branching-quality-based-and-salience-based-narrative-structures/)) |
| **Fallout Shelter** | Raids and incidents happen only while the app is open; production timers run while closed. Early versions that let disasters run offline drew "everyone died overnight" complaints | Player reports [S, unofficial] ([Steam](https://steamcommunity.com/app/588430/discussions/0/135512931359677750), [gamesas](https://www.gamesas.com/post5152788.html)) |
| **Neko Atsume** | Cats come and go while the app is closed; you only ever see who is there now and the gifts they left. No events, no text log | [S] ([Fanlore](https://fanlore.org/wiki/Neko_Atsume)) |
| **Viridi** | Plants grow in real time; a snail may sit on the pot. No events at all | [S] ([Wikipedia](https://en.wikipedia.org/wiki/Viridi), [review](https://acto.squarespace.com/viridi-review)) |

**What this tells us.**
- The split "economy runs while away, **drama** runs while watching" is common (Fallout Shelter, our relics and eras).
  It costs little in honesty: the box still grows, only the *moments* wait. [S/I]
- Things that need you **wait for you** (AC's ping, Fallen London's hand, our eras). None of these lapse silently
  while you're gone. [S/V]
- A daily (per visit) dose is a known cozy pattern, and it is framed as **pacing, not a limit** (Edery). [V]

### 2.3 Choices in async play

| Game | Choice shape | If you don't answer | Evidence |
|---|---|---|---|
| **King of Dragon Pass / Six Ages**, **Banner Saga**, **Reigns** | A scene stops play and asks | Can't happen: time is frozen until you answer. It is "non-skippable" only because nothing else moves | Turn-based by design [I]; KoDP/Six Ages in research 14 §2.1 |
| **Crusader Kings 3** (random events) | Pop-up with 2 to 4 options | First option after 4 months | [S] above |
| **Crusader Kings 3** (court events) | The player opens them | Neutral option after a while; never a surprise | [S] above |
| **Stellaris** | Pop-up | Closing it picks the first option | [S] above |
| **Animal Crossing** (move-out) | A villager asks you | Asks again the next day | [S] above |
| **Fallen London** | Card in hand | Stays until you play or discard it | [S] above |
| **Frostpunk** | You propose a law when you choose to | Nothing; it waits | [S] above |
| **Against the Storm** | Glade events in the world, with two ways to solve them | Many have a timer and a penalty, which players dislike when it is hidden | Wiki and forum [S] ([events](https://wiki.hoodedhorse.com/Against_the_Storm/Glade_Events), [Steam](https://steamcommunity.com/app/1336490/discussions/0/3884974960173034050)) |

On **owned** choices, research 14 §2.9 already covers the four parts (awareness, gameplay consequence, reminders,
permanence) and The Walking Dead's "people remember" ([Game Developer](https://www.gamedeveloper.com/design/meaningful-choice-in-games-practical-guide-case-studies)).
The new point from the user's note: **a choice the player never saw is not a choice at all**, however good its
consequences. No source above lets a real-time choice expire **unseen** and still credits it as the player's; the ones
with auto-picks (CK3, Stellaris) show the pop-up while you play, so you saw it and let it go. [I from the table]

**Forced modals.** NN/g: modals are "compulsory and require the user to act immediately", "force users away from the
tasks they were working on", and "block the content in the background, which can hide information needed to answer";
avoid them for "nonessential information" and for decisions that need what the modal hides
([NN/g](https://www.nngroup.com/articles/modal-nonmodal-dialog/)) [V]. Our dilemma's context is the box itself (the two
candidates at the fire), so a modal would hide exactly what the player needs to choose. A first-session talk lists "too
many in-game pop-ups that break immersion" as a retention problem ([PocketGamer.biz](https://www.pocketgamer.biz/news/77289/pgc-digital-how-to-enhance-the-first-time-user-experience/))
[S]. **I found no measured study of a forced choice on app open in mobile games.** The evidence against is general UX
guidance plus anecdote (AC's Isabelle); the evidence for "waits until answered" is design precedent, not data.

### 2.4 Making events visible

| Game | How a story event shows | Evidence |
|---|---|---|
| **The Sims 4 (Growing Together)** | Milestones (first steps, a wedding, a job loss) happen on the Sim in the world, then a small notification and a permanent entry on the Sim's panel | Fan guides [S] ([SimsCommunity](https://simscommunity.info/2023/03/24/sims-4-growing-together-milestones/)) |
| **Left 4 Dead** | The event *is* the world (the horde arrives); no text | [V] above |
| **Timberborn, Frostpunk** | The forecast is the build-up; the event is weather and light you see | [S] above |
| **Our P6 era moment** | The tester said "Yes yes yes!"; the change was small at default zoom, so a wave, a guided camera and a longer moment were added | `era-moment.md` §1 [V] |
| **Our P3 cards** | The user recalled a notable and his friends and enemies from cards | `decisions.md` Gate 2 [V] |
| **This playtest** | The new chief, seen in the box, was the one thing that landed | user, 2026-11-06 [V] |

What "meaningful on screen" has in common [I]: **people gather** (the crowd tells you where to look), **one thing
changes and stays changed** (a feather on the new chief, a roof on the woodpile), it lasts a few seconds and needs no
text to read, and a single line names it.

## 3. Patterns that fit us, and ones that don't

| Pattern | Fit | Why |
|---|---|---|
| Drama waits for presence; the economy runs while away | **Fits** | Already our rule for eras and relics. Absence still pays off: the box grew. Pillar 4 holds as long as nothing *bad* waits for you |
| A cycle with rest and a minimum gap (L4D, Phoebe) | **Fits** | Directly answers "not all at once or in quick succession". Phoebe's one-big-thing-per-cycle is the cozy version |
| Rate grows with the box (RimWorld wealth and population) | **Fits** | The user's "low pop: one, then another after a long play; big town: sometimes several" |
| Choices that wait until answered (AC ping, Fallen London hand, Frostpunk laws) | **Fits** | Fixes the unseen lapse. No timer, no guilt |
| Choice staged in the world, answered by touch | **Fits** | Already built in P7 (`ChoiceScene`); pillar 5 |
| A daily dose framed as pacing (Cozy Grove) | **Fits, per visit** | We have no calendar day; a visit is our unit. "Something new each visit" is practices §8 |
| Opt-in choices (CK3 court) | **Partly** | Good as a fallback (open it from the peek), but the user wants the choice to find them |
| Auto-pick on ignore (CK3 random, Stellaris) | **Only after it was seen** | Fine if the player saw it and walked away; wrong when they never saw it (the bug today) |
| Forced modal on open (Isabelle; turn-based scenes) | **Doesn't fit** | Pillar 2/4 ("never blocks you: you can leave at any beat", `first-session-and-check-in.md` §3), NN/g, and it hides the box the choice is about |
| Event timers with penalties (Against the Storm) | **Doesn't fit** | Pillar 6: no timers built to rush |
| Threat scaling (RimWorld raids) | **Doesn't fit** | We have no threats; we borrow the *pacing*, not the danger |

## 4. A recommended model: "hold, stage, ask"

One idea: **the sim keeps living while you're away, but the moments wait for you.** Three channels per visit.

### 4.1 Two kinds of story

| Kind | Examples | While away | While watching |
|---|---|---|---|
| **Moments** (presence-gated) | a wedding; a notable's newborn shown to the glass; a new chief or headman taking the seat; a callback of a Keeper's choice; a festival; a dilemma being asked; a relic found (already) | The *fact* may happen in the sim (a couple pairs, a baby is born), but the **scene waits** in a small queue | The director releases them one at a time, staged in the box |
| **Life** (not gated) | births of others, jobs, friendships, quarrels, old-age deaths of non-notables, weather | Happens and is recorded | Happens; small marks only (research 12's bubbles) |

Sim rates stay the same [I]: pairing and births run as now, so a rarely watched box doesn't grow slower. What waits is
the **ceremony** (the wedding at the fire, the baby held up, the chief's feather passed) and the **decisions** that
belong to the Keeper. Fully gated in the sim, as relics already are: dilemmas, festivals, role contests' final vote
[est, check per kind].

### 4.2 The director: budget, spacing, scaling [est]

A "moment" queue holds pending scenes, ranked by today's salience score. The director releases them by these rules.

| Box | Population | Live moments | Min gap between moments | Queue cap (older ones become cards) |
|---|---|---|---|---|
| Tribe | ≤ 6 | 1 per ~15 watched minutes, at most 2 a session | 8 min (≈ 1.5 game months) | 2 |
| Camp | 6 to 10 | 1 per ~10 min, at most 3 a session | 6 min | 3 |
| Village | 10 to 22 | 1 per ~8 min; a pair may share one scene | 4 min | 4 |
| Town | 22+ | 1 per ~6 min; now and then a "busy day": 2 or 3 at once (a wedding during the market) | 3 min | 5 |

- **The first minute of a visit** is the welcome: if the queue has anything, the top moment plays **on open**, in the
  box, as part of the wave (the people are already gathered; no camera grab). This is the "kept for you" beat.
- **Rest after each moment** (L4D's Relax, Phoebe's off phase): the min gap above, and never while a sheet, the choice
  scene, the evening talk or a wipe is in progress (wait for a natural break, L4D's Peak Fade).
- **No same-kind repeats in a row** (Reigns' cooldown; today's −0.15 rule extends to moments).
- **Scale with the box**: rate and cap from the table, linear in population between rows.
- **Maximum hold**: a moment older than **8 game years** (8 real hours) resolves off-screen and becomes a card, so a
  long absence never freezes the world and no old wedding is staged as new. Births are "shown to the glass" only while
  the baby is under 2 [est].
- **A short visit (3 min)** sees the welcome moment and the choice. **A long play** sees more moments, spaced out. A big
  town sometimes gives several at once. That is the user's shape.

### 4.3 What happens to away-time stories

- **Cards cut to 1 / 2 / 3** (< 1 h / 1 to 8 h / > 8 h away) [est], down from 3 / 5 / 7. The rest go straight to the
  history: the time-lapse and the "Also…" list move into the History screen, not the return stack.
- The top story is no longer a card if it is a moment: it **plays** on open instead, with its one-line caption on the
  peek. Cards are for what can't be staged (a death, a storm, a death of a teased thread).
- The era-ready card stays (it is already a held moment).

### 4.4 The choice: cadence, and the answer to "non-skippable?"

**Cadence.** About **one choice per visit**, never more than one open. Two weights [est]:

| Weight | Examples | How often |
|---|---|---|
| **Big** (today's dilemmas: who, which way, tradition, strangers) | the old chief's heir; baskets or woodpile | every second visit or so: one per **~6 to 10 watched or away hours**, unchanged |
| **Small** (one touch, still owned) | **name a notable's newborn** (the parents offer two names; the baby is held up to the glass); where the next tent goes (two stakes); which pair dances first at the festival | the visits in between |

Small choices are cheap to write, visible at once and come back for decades (the child you named grows up and is
credited, "Pip, named by the Keeper"). For the Camp's ~21 hours (~5 daytime visits plus nights) that means about **3
big and 3 small** camp choices, so 4 written camp dilemmas are nearly enough; the small ones carry the rest.

**When it is raised.** Only **while the Keeper is watching**, or **held for the next open** and asked as the welcome
moment. Never raised and closed inside an absence.

**Non-skippable? No: unmissable, not unskippable.**

- **On open**, the asker and the two options are **staged in the box** (P7's choice scene), the peek shows the question,
  and the people look up at the glass. It is the first thing you see, but the box is still yours: you can wipe, look
  around or leave.
- **It waits.** The lapse clock runs **only while the question has been seen**: it never resolves unseen. It closes when
  (a) you answer, (b) you tap a third option, **"Let them decide"**, which is itself a Keeper's choice and is credited
  as one ("The Keeper let them choose; they chose Ada"), or (c) it has been **shown on 3 visits** unanswered [est],
  when the people decide with today's neutral line.
- **The world can still close it**, gently: if the cast leaves (the chief dies, a candidate dies), it closes as
  today, and the card says what happened without blame. This is rare once questions are only raised in your presence.

Why not a forced modal, the strongest evidence:
1. **The real failure was unseen lapses, not skippability.** Shown dilemmas got answered in P7's agent runs (4 of 5
   answered, median 7.6 s, `P7.md`, tester numbers, not humans), and the user's complaint is about a choice resolved
   before they arrived. Waiting fixes that as fully as a modal would. [V repo, I]
2. **Real-time games that keep running never force a choice; they wait or auto-pick after you saw it.** CK3 court
   events are opt-in so they "can't pop up unexpectedly"; AC asks again tomorrow; Fallen London keeps the card in your
   hand. Forced scenes exist only in games where time stops (KoDP, Reigns, Banner Saga). [S]
3. **A modal hides the context the choice needs.** NN/g: modals "block the content in the background, which can hide
   information needed to answer". Our answer is "touch one of the two people at the fire". [V]
4. **Pillars.** "Never blocks you: you can leave at any beat" is a check-in rule; a gate on open is a nag, and the
   Isabelle complaints are the cozy-genre warning. [V repo, S]

What would change my mind: if the human P7 diary shows players walking past a staged, waiting choice visit after visit,
test a stronger *ask* (the people stand at the glass until answered, a soft chime) before any modal.

### 4.5 How consequences become visible

Rules for every option (adds to P7's rules 1 to 7):

1. **At the answer**, a reaction in the box: the chosen glow and the cheer, the unchosen one's shrug or sulk by trait
   (already in P7).
2. **Within one game year** (≈ one hour), a **thing in the box** that stays: a feather on the new chief, the woodpile's
   roof, the stone or the tree at the fire, the strangers' tent with its odd colour. If an option has no prop, it needs
   one before it ships.
3. **The callback is a moment, not just a card**: it goes into the director's queue, plays in the box (the people at
   the roofed woodpile), and its caption credits the Keeper. Its 5 to 30-year delay stays.
4. **The person carries it**: their person card shows "chosen by the Keeper" or "named by the Keeper".

### 4.6 What the history keeps

- **Everything** stays in the chronicle as today; the History screen holds what used to be the "Also…" list.
- The book gains a **"The Keeper's choices"** page per era: each answered or delegated choice, what came of it, the
  callback line. Lapsed ones are listed plainly as "the people chose".
- The time-lapse gets the **hand pins** P7 deferred: a small hand at the year of each choice.
- Moments that played live get a small "seen" mark in the history, so the player knows which ones they watched [est;
  could be cut].

### 4.7 The Camp, paced by this [I]

The camp is ~21 hours of slow land change. With the rules above, a day of 4 to 5 visits gives: one welcome moment per
visit (a wedding, a baby held up, the woodpile finished), one choice per visit (alternating big and small), and in a
long session one or two more moments. The land beats of `keepers-hands.md` §2 (crickets, rabbit tracks, birds, frogs)
fill the gaps without text. That is roughly **10 to 15 seen moments and 5 to 6 choices across the camp**, against
today's ~100 card lines and ~2 answered choices [est from the numbers in §1].

## 5. Options for the user

| | Option | What changes | For | Against |
|---|---|---|---|---|
| **A ★** | **Hold, stage, ask** (§4) | Moments queue and play live with spacing scaled by population; choices raised in your presence, wait, "Let them decide"; small choices (naming) between big ones; cards 1/2/3 | Answers every point in the feedback; reuses Show me's echo, the choice scene, salience ranking | Medium build: a director, moment staging per kind, ~6 small-choice storylets; tuning needs a diary test |
| **B** | **Just fix the choice** | Dilemmas raised only while watching or on open; never lapse unseen; "Let them decide"; cards 1/2/3. No moment queue | Small build; fixes the "I didn't make the choice" problem | Events are still read, not seen; the camp still feels flat |
| **C** | **Forced question on open** | A non-skippable choice sheet on open when one is pending | Guarantees a choice per visit | Against pillars 2/4 and NN/g; hides the box; risk of the Isabelle effect; I recommend against |
| **D** | **A + a full storyteller later** | A, then a Phoebe-style cycle with "busy days" and festivals in the town, and the Keeper's character leanings (research 14 §2.6) | The richest shape for the town era | Wait for A's diary result first |

Sub-choices inside A (★ = default):
- "Let them decide" as a third option: ★ yes / no.
- Small choices: ★ naming newborns first / also tent spots / none.
- Lapse after being shown on: 2 visits / ★ 3 visits / never.
- Welcome moment on open: ★ plays in place at the default view / a short camera nudge to it.

## 6. Open questions

1. **Does holding the ceremony feel honest?** A wedding staged on your return for a couple who paired three game years
   ago. The 8-year hold cap and the "shown to the glass" framing (the people *wait to show the Keeper*) may cover it;
   needs a playtest.
2. **Which kinds are sim-gated and which only view-gated?** Dilemmas, festivals and relics clearly sim-gated; weddings
   and births view-gated. Role contests: does the vote itself wait for the Keeper? (It changes who leads, which is
   gameplay.)
3. **Rates.** Every number in §4.2 is a guess. The diary should log moments seen per visit, gaps, and "did you see it
   happen?"
4. **Recall test.** Does P3's recall question score higher for moments seen live than for cards read? That is the
   hypothesis behind all of this; it is not yet measured.
5. **Writing load.** Small choices need ~6 per era, each with a visible result and one callback. Naming is the
   cheapest; are the others worth writing before the town?
6. **Thin evidence.** Most game mechanics above come from wikis and player reports, not developer statements; only
   L4D (slides), Reigns (designer article), Cozy Grove (interview) and NN/g are primary. No source measures forced
   choices on app open.
7. **Overnight.** A night away (8+ hours) fills the queue to its cap; the rest become cards. Is one welcome moment
   enough after a night, or should a long absence earn two, spaced a few minutes apart?

## Sources

- Booth, *The AI Systems of Left 4 Dead* (Valve, 2009): https://steamcdn-a.akamaihd.net/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf
- RimWorld wiki: https://rimworldwiki.com/wiki/Cassandra, https://rimworldwiki.com/wiki/Phoebe_Chillax, https://rimworldwiki.com/wiki/AI_Storytellers
- Sylvester, GDC 2017: https://gdcvault.com/play/1024232/-RimWorld-Contrarian-Ridiculous-and
- CK3 events: https://ck3.paradoxwikis.com/Events; dev diary 75: https://www.gamewatcher.com/news/crusader-kings-3-dev-diary-75-court-type-events-duels
- Stellaris events: https://stellaris.paradoxwikis.com/Events
- Reigns deep dive: https://www.gamedeveloper.com/design/game-design-deep-dive-creating-an-adaptive-narrative-in-i-reigns-i-
- Wildermyth wiki: https://wildermyth.com/wiki/Event, https://www.wildermyth.com/wiki/Event_Types
- Frostpunk 2 laws: https://www.gamerguides.com/frostpunk-2/guide/communities-and-factions/laws
- Timberborn drought: https://timberborn.wiki.gg/wiki/Drought
- ACNH visitors: https://nintendowire.com/guides/animal-crossing-new-horizons/special-visitor-appearance-rates/; pings:
  https://www.belltreeforums.com/threads/telling-the-difference-in-pings.333537,
  https://www.belltreeforums.com/threads/villager-moves-out-ping-question.290484; Isabelle comments:
  https://www.nintendolife.com/news/2020/07/random_isabelle_reclaims_the_official_animal_crossing_twitter_account_but_fans_have_mixed_feelings
- Cozy Grove, Edery to Vice: https://www.vice.com/en/article/bvx9vw/cozy-grove-is-ok-telling-players-to-log-off-its-delightful; review: https://www.gamespew.com/2021/04/cozy-grove-review/
- Fallen London cards: https://fallenlondon.wiki/wiki/Cards; Emily Short, Beyond Branching: https://emshort.blog/2016/04/12/beyond-branching-quality-based-and-salience-based-narrative-structures/
- Fallout Shelter reports: https://steamcommunity.com/app/588430/discussions/0/135512931359677750, https://www.gamesas.com/post5152788.html
- Neko Atsume: https://fanlore.org/wiki/Neko_Atsume; Viridi: https://en.wikipedia.org/wiki/Viridi, https://acto.squarespace.com/viridi-review
- Against the Storm: https://wiki.hoodedhorse.com/Against_the_Storm/Glade_Events, https://steamcommunity.com/app/1336490/discussions/0/3884974960173034050
- The Sims 4 milestones: https://simscommunity.info/2023/03/24/sims-4-growing-together-milestones/
- NN/g, modal and nonmodal dialogs: https://www.nngroup.com/articles/modal-nonmodal-dialog/
- PGC Digital on first-time UX: https://www.pocketgamer.biz/news/77289/pgc-digital-how-to-enhance-the-first-time-user-experience/
- Meaningful choice (Game Developer): https://www.gamedeveloper.com/design/meaningful-choice-in-games-practical-guide-case-studies
- Repo: `docs/decisions.md`, `docs/design/results/P7.md`, `docs/design/chronicle-and-history.md`,
  `docs/design/first-session-and-check-in.md`, `docs/design/keepers-hands.md`, `docs/research/14-dilemmas-and-living-land.md`,
  `AquariumCiv/Assets/Game/Sim/SimCore.Cards.cs` (`CardCap`), `SimCore.Dilemmas.cs`, `SimState.cs`
