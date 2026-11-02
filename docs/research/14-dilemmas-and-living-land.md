# 14 — Meaningful play: the Keeper's dilemmas (route 4) and the living land (routes 2 + 5)

Research date: **2026-11-02**. Question from the user: the game "lacks meaningful gameplay". Of the routes compared
(hand + satchel, biomes/expansion, puzzle shaping, card dilemmas, ecosystem, threat moments) the user liked **4 (card
dilemmas)** and **2 + 5 (biome generation / expansion + an ecosystem)** and asked to explore them further.

Markers: **[K]** a reference game as I know it, not re-checked online for this note; **[code]** read in the repo today;
**[est]** my guess, to tune or test. Recommended options are marked ★.

**Status:** research, not a decision. Both routes reopen approved text in [game-definition.md](../design/game-definition.md)
(§3 loops, §6 control) and go past the current polish-only phase. That's the user's call.

---

## 0. TL;DR

- **What's missing today is choice.** Cleaning has one right answer, gifts have no cost beyond charges, and
  history happens *to* the player. Both routes add choices whose consequences the player can see and remembers.
- **Route 4, the Keeper's dilemmas:** now and then the people put a real question to the glass ("Ada or Bo for the
  mill?", "Drain the marsh for fields?"). The player answers **in the box** by touching one of two people or
  places, not by pressing a menu button. Neither answer is wrong; they lead to different histories. The answer is credited,
  remembered, and **pays off later** in cards ("Years after the Keeper chose her, Ada's mill…"). Across a box the
  choices add up to **the Keeper's character** in the people's eyes, which the town shows in shrines, songs and the
  book. Cheap: it builds on wishes, teaser threads and the story templates that already exist. **The cost is writing.**
- **Routes 2 + 5, the living land:** the box becomes a small ecosystem. Every cell of the site grid carries a
  **biome** (meadow, woods, marsh, scrub, rock…) that **spreads and succeeds by itself** from water, light and soil.
  **Species** (plants and animals) arrive when their conditions are met and settle if they stay met. The people **use**
  the wild (hunters, fishers, beekeepers) and **press on it** (fields and roads eat meadow, hunting thins herds). The
  box is small, so **space is the tension**: every field is a meadow that's gone. The Keeper still never places
  biomes or buildings; the existing shaping tools change the *conditions*. Biome generation makes each box's land
  different; expansion comes as **wild edges the people open up**, a **dig into the soil** in the town era, and, as the
  shelf meta, **tanks joined by tubes**.
- **They interlock:** route 5 *creates* the tensions and route 4 is *how the player decides them* ("The farmers want
  the marsh drained. The fishers say no."). Each alone is weaker: dilemmas without a living land are mostly social;
  an ecosystem without dilemmas is a pretty system you can only nudge.
- **Order ★:** prototype dilemmas first (small, about a week). Then ecosystem v1 (biome layer + 6 species + field guide).
  Then seeded land generation. Then expansion.

---

## 1. Where the game is today [code]

What the routes build on, so neither starts from zero:

| Piece | Where | What it gives us |
|---|---|---|
| Aggregate economy: food, materials, mood, population, mess, stepped monthly, deterministic from a seed | `SimCore.cs`, `SimState.cs` | Effects of an answer or a species are small changes to these numbers; offline catch-up still works |
| **Leans** (`Lean.Food` / `Lean.Craft`) set by a shaping for 36 months | `SimCore.Nudges.cs` | A ready-made "direction of the settlement" knob for dilemma outcomes |
| **Charges** for shaping, earned by care and happy years | `SimConfig.shaping*` | The economy of the Keeper's hand; the ecosystem can feed it too |
| **Wishes**: one open at a time, lapse quietly after 24 months, never a penalty | `SimCore.Wishes.cs` | The exact shape a dilemma needs: open → answered or lapsed, a card, no timer |
| **Teaser threads** (+0.2 salience when a later entry pays off a teaser) | chronicle-and-history §2.3 | The payoff machinery for "years later…" callbacks |
| ~30 trait-driven story templates; storm, drought, bumper harvest events | `SimCore.Story.cs` | The writing voice and the event hooks a dilemma can hang on |
| Roles and successions, ties (friend, rival, mentor) | `SimCore.Roles.cs`, `SimCore.People.cs` | "Who" dilemmas: offices, apprenticeships, marriages, feuds |
| **Site grid 60 × 44 cells** (box 6 × 4.4 units): height, slope, base fertility, water distance, flags (forest, meadow, rock, canal) | `Settle/SiteMap.cs` | Where a biome layer lives: one more byte per cell |
| Terrain = 3 river-valley presets + oasis + mountain, parametric (pond spot, mirror, noise offset) | `Settle/Terrain.cs` | The seam where seeded generation goes |
| Shaping tools per era: rain, grove, herd / channel, lamp, hill & hollow / outcrop, spring, dye meadow | research 10 "Decision" | The player's verbs on the ecosystem. No new verbs needed for v1 |

---

## 2. Route 4: the Keeper's dilemmas

### 2.1 What other games do [K]

| Game | How choices work | Lesson for us |
|---|---|---|
| **King of Dragon Pass / Six Ages** | A clan meets an illustrated scene; advisors (each with a personality) give opinions; you pick from 3–5 responses; consequences, some years later, come back as new scenes | **The closest model.** Advisors = our notables with traits; delayed callbacks are what make it memorable. Their failing for us: long text, many options, a lot of reading |
| **Reigns** | Swipe left/right on one card; four meters; extremes end your reign | Two options and one gesture fit a phone perfectly. Avoid the meters and the death spiral (pillar 4) |
| **Crusader Kings** events | Trait-flavoured events with 2–3 options; options gated by traits; outcomes change opinions and relationships | The *people's* opinions of a choice are the consequence, not just resources |
| **Frostpunk** laws, **Banished** | Policy choices with lasting effects on a settlement | A choice that shapes the settlement's character is satisfying; Frostpunk's is grim and stressful (anti-reference for tone) |
| **Animal Crossing** villagers' requests | Small asks with no penalty for ignoring | Silence must be safe |
| **Black & White** | The creature and the villagers react to how you behave; the world's look follows your alignment | **Your choices show up in the look of the world**: the "Keeper's character" idea (§2.6) |
| **WorldBox** | No choices, pure watching | What we have now: fun, but nothing you own |

### 2.2 The shape of a dilemma

- **Two options, both good.** Never a trap, never an obviously best answer. The options differ in *flavour*:
  which person, which direction, which tradition. "No wrong answer, a different history" is the rule.
- **Asked by the people, about the people.** A notable (or two, on opposite sides) brings it, in their trait's
  voice. The Keeper is a god the people half-understand (relics research), so the question is a ritual, not a form.
- **One per return, at most** ★, sharing the wish slot (a wish *or* a dilemma, never both) so a check-in still fits
  in ~3 minutes. Frequency is about one every 6–10 game years [est], so roughly 2–4 a day at the target cadence.
- **Silence is an answer.** An unanswered dilemma lapses like a wish (24 months ★). The people then decide by
  the role-holder's trait ("The stern headman chose the field"). The card says so neutrally and never as blame.
  There are no timers, counters or guilt lines (pillars 4 and 6).

### 2.3 How the player answers ★ in the box

The project's rule is "the glass is the interface", and research 12 keeps the people wordless in the world. So:

1. The card states the question in one line, with two portraits or two place icons. *"The mill needs a miller. Ada (the
   stubborn) or Bo (the dreamer)?"*
2. **Show me** flies to the scene: the two candidates stand on either side of the fire (or two stakes mark two
   sites, or two offerings sit on the shrine stone). They look up at the glass.
3. **The player touches one.** The chosen person (or place) glows; the people cheer; the other shrugs or sulks, by
   trait. That's the whole gesture, one-handed.
4. A fallback: two buttons on the card itself for players who won't fly in (accessibility, the 30-second glance).

This makes the dilemma a **moment in the world**, not a menu (the risk flagged in the first comparison).
Alternatives: buttons on the card only (cheaper, reads like Reigns), or a swipe left/right (fast, but "swipe" already
means "next card" in the stack).

### 2.4 Kinds of dilemma, by era

| Kind | Camp | Village | Town | Effect in the sim |
|---|---|---|---|---|
| **Who** (office, honour, match) | Who leads the hunt | Ada or Bo as miller; bless a marriage between rival families | Who becomes guildmaster; which family gets the new quarter | Role pick (bypasses the normal succession roll), tie changes (the unchosen: rivalry chance or graceful by trait) |
| **Which way** (direction) | Winter by the pond or in the woods | Fields or orchard on the east slope | Market or guildhall first | A `Lean` for 36 months; a placement bias on one area of the site grid |
| **Tradition** (culture) | Bury the elder by the stone or the tree | Harvest festival: feast or offering | The bell rings at dawn or at dusk | A custom stored on the box: shows in the world (where the remembering stones go, which festival look plays) and in the book |
| **Strangers** | Lost travellers ask to join | A family from beyond the hill | Traders want a stall | +population or a new notable family; sometimes a rival brought in |
| **Land** (from route 5) | Hunt the last deer or let them be | Drain the marsh for fields | Quarry the hill or keep the old wood | Biome change vs species kept (§3) |
| **Relic** | — | Keep the Sky Crumb in the hut or bury it | Put the Lens in the tower or the temple | Where the relic shows; a later story |

### 2.5 Consequences the player can feel

What matters most for "meaningful" isn't the size of the effect but whether it's **visible, credited and remembered**:

1. **At once (seconds):** the scene reacts (the cheer, the sulk), and a chronicle line credits the Keeper.
2. **Soon (1–3 game years, the next return or two):** the visible result: Ada's mill turns, the orchard is planted,
   the strangers' huts appear. The card says *"Because the Keeper chose…"*.
3. **Later (5–30 game years): the callback.** A thread links the answer to future stories: the unchosen Bo opens a
   rival mill; the stranger's grandson becomes chief; the drained marsh floods in a storm year. These are the King of
   Dragon Pass moments. They use the teaser-thread payoff (+0.2 salience) as it stands. Aim for **at least one
   callback per answered dilemma** [est], written into the dilemma's data, not left to chance.
4. **Forever:** the book gets a "Keeper's choices" page; the time-lapse marks each answer with a small hand pin.

### 2.6 The Keeper's character ★

Each answer also nudges three hidden leanings about how the people see their Keeper, for example
**generous ↔ stern**, **wild ↔ tame** (favours nature or fields) and **old ways ↔ new ways**. Nothing is shown as
a number. It shows in the world:

- The camp's shrine stone, then the village shrine, then the town's temple take a look from the leanings (a fruit
  offering vs a stern carved face; vines vs tidy hedges).
- The people's songs and sayings in cards ("The Keeper favours the bold").
- The completed box's museum label: *"Westmere, a box kept by a wild and generous Keeper."* That's a strong
  share line, and a reason to play a second box differently.

It's cheap (three floats, a few look variants, a handful of lines) and it turns many small choices into one identity.
It's also Black & White's best idea without its moral bluntness.

### 2.7 Writing cost and data shape

A dilemma is data: kind, era, conditions (who exists, what's built, which biome), the asker(s) and trait lines, two
options each with effects (role pick, lean, ties, custom, population, biome) and **one or two callback stories** with
their own conditions and delays. The slice needs about **24 dilemmas** (8 per era) [est] to avoid repeats over a 1–2
week box. With 2 options and about 2 callbacks per option, that's roughly 100 short pieces of text. Writing is
the main cost; the code is about 1 agent-week [est] (a `Dilemma` entry alongside `Wish`, the in-box choice scene,
effects, callbacks, the leanings and the save version).

### 2.8 Risks

| Risk | Mitigation |
|---|---|
| Reads as a menu or a quiz | Answer in the box (§2.3); two options; one line of text |
| Players hunt for the "right" answer | Both options good; consequences in story and look, not in a better economy; no numbers shown |
| Fatigue | One at most per return, shared slot with wishes; none in the first session |
| Repetition | ~24 for the slice, conditions so each fits its moment; the leanings change the wording |
| Determinism | The answer is an input event with a month, like a shaping; unanswered → the trait default at the lapse month |
| Breaks "Keeper, not King" | The people ask; the Keeper only picks between their own proposals; silence lets them decide |

### 2.9 Research for P7 (2026-11-02)

Markers: **[S]** secondary source (press, interview, essay; the primary talk or paper wasn't readable), **[V]** read.

| Source | Finding | What P7 takes |
|---|---|---|
| Meaningful choice, a practical guide (Game Developer) [V] | Four parts: **awareness** (the player sees there's a choice and its alternatives), **gameplay consequences** (not only a changed line), **reminders** (the choice comes back), **permanence** (no cheap undo). *The Walking Dead*: Kenny remembers whether you backed him | All four become rules in [P7.md](../design/results/P7.md): an option hint, a real effect, a callback, no undo |
| Branching-narrative analysis of *The Walking Dead* (Game Developer) [S] | A choice is meaningful only if the player has a basis to predict its consequence; even when the big event is fixed, people's *reactions* carry the consequence | A one-line hint per option in the people's voice; the unchosen person's reaction (sulk, grudge, grace) is a consequence in itself |
| Sarian, "No Going Back: the Telltale Model" (*Eludamos*, 2018) [S] | Telltale's consequences are pushed to "some unforeseen future"; with weak consequences the choice becomes a test of the player | Both options good, no right answer, so it isn't a test; callbacks *do* arrive, not forever deferred |
| Designer essays on delayed consequences [S] | Delay produces "wait, is this because of what I did?" moments, anticipation and regret | Callbacks at 5–30 years (4–15 in P7), credited with "Because the Keeper chose…" so the link is never missed |
| David Dunham (King of Dragon Pass, Six Ages): interviews [S] | Choices are trade-offs that shape economic and political outcomes, often "which clan do you upset"; he arrived at storylets on his own | "Who to upset" = our unchosen notable; dilemmas as storylets |
| Emily Short, "Storylets: you want them" (2019), via secondary write-ups [S] | A storylet = prerequisites + content + effects on world state; when several are valid, pick by salience | Our data shape: conditions → card + scene → effects; callbacks are storylets gated on a past answer; selection uses the existing salience ranking |
| Reigns: Alliot's GDC 2017 talk, via coverage [S] | Two-way choice on a card a character sits in needs no tutorial; showing *which* things a choice touches (not how much) keeps it readable | Two options; the hint says what changes, never by how much |
| Wildermyth: interviews and reviews [S] | Handcrafted beginnings and ends around procedural middles; reviewers note repetition | Hand-written dilemmas and callbacks; P7's 8 dilemmas never repeat in a box; the full game needs ~24 |

Sources: [Meaningful choice in games](https://www.gamedeveloper.com/design/meaningful-choice-in-games-practical-guide-case-studies),
[Meaningful decisions in branching narratives](https://gamedeveloper.com/design/meaningful-decisions-in-branching-narratives),
[Sarian, *Eludamos* 2018](https://eludamos.org/index.php/eludamos/article/view/vol9no1-2),
[Dunham interview, *Loading…*](https://loading.journals.publicknowledgeproject.org/index.php/loading/article/view/166),
[IF50: King of Dragon Pass](https://if50.substack.com/p/1999-king-of-dragon-pass),
[Six Ages GDC talk listing](https://gdcvault.com/play/1026289/contactUs),
[Emily Short on storylets](https://emshort.blog/tag/quality-based-narrative/),
[Yarn Spinner: storylets and saliency](https://docs.yarnspinner.dev/write-yarn-scripts/advanced-scripting/storylets-and-saliency-a-primer),
[Reigns GDC 2017](https://www.gdcvault.com/play/1024278),
[PC Gamer on Reigns' design](https://www.pcgamer.com/au/how-reigns-and-its-forthcoming-sequel-make-their-tinderocracy-work),
[Game Maker's Notebook: Wildermyth](https://gamemakersnotebook.libsyn.com/balancing-procedural-and-intimate-storytelling-in-wildermyth).

### 2.10 Prototype P7 ★ (~3–5 agent days)

Gray-box in the real scene: 6 dilemmas over a camp-to-village run, answered in the box, with one callback each,
compressed time. A/B against today's build (wishes only). Measure: does the tester recall *a choice they made* by
name on the next day (P3's recall question); do they say "I did that"; return intent; do they feel there's a wrong
answer (they shouldn't); taps on Show me vs answering from the card.

---

## 3. Routes 2 + 5: the living land

### 3.1 What other games do [K]

| Game | How it works | Lesson for us |
|---|---|---|
| **Reus** | Giants (you) shape land into forest, desert, ocean, mountain; place plants, animals and minerals with **adjacency synergies**; humans settle and grow, and get greedy if pushed | **The closest model.** Nature shaped by a god, people living off it. Their weakness for us: you *place* every species (we only shape conditions), and greed punishes |
| **From Dust** | Sculpt sand, water and lava; vegetation **spreads by itself** next to water; fire burns it | Self-spreading biomes from simple rules. Rules readable at a glance |
| **Equilinox** | Plants and animals each need conditions (biome, other species, space); meet them and they thrive, evolve | **Requirements per species** as the core loop; a calm, no-fail ecosystem |
| **Viva Piñata** | Meet a species' visit conditions → it visits; meet the resident conditions → it stays and breeds; a journal | **Visit → resident → thriving** gives every species three small wins. A journal completes the set |
| **Terra Nil** | Reverse city builder: restore a wasteland's biomes, then leave no trace | Restoration as satisfying; biomes as a puzzle of order (water before trees) |
| **Neko Atsume** | Put out items; the right cats arrive while you're away | Arrivals while away are the perfect check-in surprise (our card) |
| **Dorfromantik** | Tiles with biome edges; matching scores and quests | Biome adjacency as an easy-to-read rule |
| **Islanders** | Each island is new; you move to the next one | Expansion by "next island" = our next box |
| **A real aquarium / terrarium** | Cycle the tank, balance plants, light and stock; algae when out of balance | The hobby fantasy itself: *a balanced little world you tuned* |

### 3.2 The core: biome cover + succession

Add one **biome byte per cell** of the existing 60 × 44 site grid, stepped once a game season in `SimCore` (deterministic):

| Biome | Grows where | Succession (unused, over years) | The people use it for |
|---|---|---|---|
| **Meadow** | fertile, open, dry-ish | → scrub → woods | grazing, bees, flowers (dye) |
| **Woods** | fertile, moist; the grove tool | stays; old woods after ~30 y | wood, game, mushrooms |
| **Marsh / reeds** | next to water, low | stays while wet; dries to meadow | fish, reeds (thatch), frogs and birds |
| **Scrub / heath** | poor soil, slope | → woods if rained on | berries, goats |
| **Rock / scree** | steep slope; the outcrop tool | stays | stone, lizards, the lookout |
| **Fields, yards, paths** | the people's lots (Settle) | → meadow if abandoned | — (they're the town) |
| Setting biomes | oasis: palm grove, dune, salt flat; mountain: pine, alpine meadow, snowfield | | |

Rules: a biome spreads to a neighbour cell when the neighbour's conditions suit it better (From Dust style), a few
cells per season, capped. Settle's placement scores read the biome (fields want meadow, the lumbermill wants woods).
The shaping tools now change **conditions**, and the biomes follow: rain lifts moisture (meadow → woods sooner, a
hollow → marsh), the grove seeds woods, the channel extends marsh along it, the outcrop makes rock, the dye meadow
makes flowers. **No new verbs** for v1.

The overgrowth *mess* stays a mess on the glass and roofs (care), but it can now come from the nearest biome
(reeds near the marsh, ivy near the woods). That's a free bit of coherence.

### 3.3 Species: arrive, settle, thrive

Each setting has about **8–12 species per era** [est] (the slice: ~6 for the river valley). Each has conditions,
mostly on biomes, which the player can read in the field guide:

| Species (river valley) | Visits when | Stays when | Gives the people | Look in the box |
|---|---|---|---|---|
| Deer | woods ≥ 15 % of the box | woods ≥ 25 %, quiet woods (far from the houses) | hunter's job, a feast story | 2–4 deer at the wood's edge at dawn |
| Bees | flowers or meadow ≥ 10 % | + an orchard or a dye meadow | beekeeper, honey (mood), the merchant's stall | a buzz, tiny dots over flowers |
| Frogs and herons | marsh ≥ 5 % | marsh ≥ 10 % and a pond | fisher's luck, a song story | heron stalking the reeds, frog chorus at night |
| Fish | the pond; the herd tool's shoal | a channel or spring keeps the water moving | fisher, fish stall | jumping fish, rings on the water |
| Goats | scrub or rock ≥ 10 % | + a shepherd (village) | wool → weavers (town) | goats on the hill |
| Owls | old woods (30 y) | old woods + the town's tower | a night story, the watch's mascot | an owl on the tower at night |

States: **visiting** (a card: "A heron landed by the reeds!"), **resident** (a card + a field-guide stamp),
**thriving** (a second stamp, a small permanent effect: e.g. +mood, a job's output up, a festival). If the conditions
go, the species **moves on** gently ("The deer have moved beyond the hills") and **can always come back**. Nothing
dies of the player's choices (pillar 4). The field guide is a page set in the small book: silhouettes of species not
found yet, each with a hint in the people's words ("Old Mira says herons like still, shallow water").

Why this adds play:
- **Goals the player sets themselves** (Viva Piñata): "I want owls" → let a wood grow old near where the tower will be.
- **Arrivals while away** make a check-in card the player caused (Neko Atsume).
- **Trade-offs in a small space**: the woods the deer need are where the farmers want fields. Pushing one biome
  costs another. That's where route 4 comes in.

### 3.4 How the two routes meet

The settlement grows into the wild by itself (Settle). When growth would cost a species its home, or a job wants a
change, a **land dilemma** is raised (§2.4): "The farmers want the old wood cleared. Hunter Ivo says the deer will
go." The player decides; the land and the history follow; the field guide and the chronicle remember it. The Keeper
never paints biomes or places buildings, so pillar 3 holds, but now the player's hand matters in a way they can see.

### 3.5 Biome generation (each box's land is new)

Today each box gets one of five fixed terrain specs [code]. Proposal ★: **a seeded generator per setting** that
draws the land from a few parameters (where the pond or oasis sits, a river's course, hills, the soil's fertility
noise, rock) and the **starting biome mix** (e.g. a wooded valley vs an open meadow valley vs a marshy delta). The
settlement's founding spot is still picked by Settle's scores.

- Each new box (Gate 3: new box in a new setting) is a new puzzle: different species are easy or hard there.
- It has to pass the Growth Lab checks (organic growth reads well) on, say, 50 seeds per setting before it ships;
  bad seeds are rejected by a validator (pond reachable, enough flat ground, founding spot exists) [est].
- Cost: medium. Terrain is already a function of a spec; the generator fills the spec plus the biome layer.

### 3.6 Expansion: four forms

| Form | How | For | Against |
|---|---|---|---|
| **★ Wild edges** | The box's outer band starts as untamed wild (thick woods, scree, mist). As eras arrive the people open it up (paths, clearings), and new species live there | Expansion inside the fixed glass; feels like discovery; cheap (a flag per cell) | Not "bigger", but the land feels bigger |
| **★ Into the soil** (town era) | The soil cross-section behind the glass opens: a mine, a cellar, a cave with its own species (bats, glow-worms, crystals) | Unique to a glass diorama; the cross-section already exists; a strong era moment | A second camera framing; town-era only |
| **★ Connected tanks** (shelf meta) | Completed or later boxes join by glass tubes; species and a few people migrate along them (with a card); a species found in one box can be "shared" to the next | Ties the ecosystem to the shelf and the $0.99 unlock; a reason to collect settings | After the slice; only a few boxes to connect |
| A bigger box | The glass literally grows in later eras | Simple to explain | Breaks the fixed diorama, the glass panes and the camera; costly on mobile (and the last glass-pane leak cost hours) |

### 3.7 Readability without a HUD (pillar 5)

The box must show the ecosystem without bars: biome colour and props, animals in sight at their times of day, sounds
(frogs, bees, birds by biome). The field guide is the only "screen" for it. A quick "Look" gesture (hold on the land)
could tint biomes for a second [est]. That's for testing, not for v1.

### 3.8 Cost and risks

| Part | Cost [est] | Risk |
|---|---|---|
| Biome layer + succession in the sim, save version | Medium (1–2 agent-weeks) | Spreads too fast or too slow; tune in compressed time |
| Biome look (cell colour/texture on the terrain mesh, instanced props) | Medium | Phone perf: a 2 640-cell grid is small; fine with vertex colours |
| Species (6 for the slice): conditions, states, cards, field guide | Medium | Code-built animals in the style kit are new art work |
| Settle reads biomes; land dilemmas | Small–medium | Re-check Growth Lab: organic growth must still read well |
| Seeded land generation + validator | Medium | Bad seeds; Growth Lab criteria per setting |
| Wild edges | Small | — |
| Into the soil | Large | Second framing, new pieces |
| Connected tanks | Large | Shelf design isn't settled |
| **Complexity creep** | | The biggest risk: the box must stay a calm thing to watch. Keep 5–6 biomes and ~6 species per box in the slice |

### 3.9 Prototype P8 ★ (~1–2 agent-weeks)

A biome layer with 4 biomes and succession, the existing tools changing conditions, 4 species with visit/stay, a
bare field guide, compressed time. Measure: does a tester set themselves a goal ("I want the herons") without being
told; do they read the land's state without help; does the field guide pull them back; does it stay calm to watch.

---

## 4. Recommendation and order

1. ★ **P7, the dilemmas prototype**, first: it's the cheapest, tests the core question ("does choosing make it
   mine?") and needs no new art.
2. ★ **P8, the living-land prototype**, next or in parallel (no-Unity sim work can run in a worktree while P7 has the
   Editor).
3. If both pass: **land dilemmas** join them (§3.4), then **seeded generation** (§3.5), then **wild edges**. **Into
   the soil** and **connected tanks** come with the town era and the shelf work.

## 5. Choices for the user

1. **How a dilemma is answered.** ★ In the box (touch a person or place), with buttons on the card as a fallback ·
   buttons on the card only · swipe left/right.
2. **Silence.** ★ It lapses after 24 game months; the role-holder decides by trait · it waits for the Keeper forever
   (and holds up nothing) · it lapses with no decision.
3. **Keeper's character from choices.** ★ Yes: three hidden leanings that change shrines, sayings and the museum label ·
   no, each choice stands alone.
4. **Species leaving.** ★ They move on and can come back; never die from the player's choices · they stay once
   resident (no loss at all).
5. **Expansion.** ★ Wild edges now, into the soil in the town era, connected tanks with the shelf · a bigger box ·
   no expansion, generation only.
6. **Scope vs the polish phase.** ★ Prototype both (P7, P8) before any design change is approved · finish polish
   first, then prototype.
