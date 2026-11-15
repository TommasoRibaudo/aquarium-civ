# 21 · Writing the tank's events and history: rules, ratings, cuts (research, 2026-11-14)

Research run only: no code changed. Sources:
- **AgentGameTemplate** (`TommasoRibaudo/AgentGameTemplate`, local copy in `Documents/AgentGameTemplate`):
  `music-events/narrative-bible.md`, `event-template.md`, `event-audit.md` (the five-axis audit), `factions.md`,
  `pacing-and-balance.md`, `.claude/skills/balancer/references/design-framework.md`, `.claude/plans/rewrite.md`.
- **This repo:** `docs/design/chronicle-and-history.md` §5 (the people's voice), research 16 (hold, stage, ask), the
  closed-world and "seen, not read" calls, `docs/design/people-and-things.md`.
- **The inventory:** [21-writing-events-inventory.md](21-writing-events-inventory.md). This is every player-facing
  text in the game, verbatim, with its trigger, its effect on the sim and its memory. It was compiled by a Sonnet
  agent from code only, on branch `p9/signoff` (2026-11-14).

---

## 1. What the AgentGameTemplate research says (and how it maps here)

The template's research came from a card-based music-manager game. Its audit found that every one of its 24 events
"appears, resolves, and leaves no trace". It traced that to five systemic patterns, and they map closely onto ours.

| AGT rule / finding | Where in AGT | What it means for the tank |
|---|---|---|
| **Concrete situation, named stakeholder with a motive.** "A venue is disputing X's fee", not "a dispute has emerged". | narrative-bible §2 (a)(b) | Every ask and story names a person, an animal or a thing that is really in the box (Ada, the dog Smoke, the name stone). Generic "the young ones" or "two travellers" is the weak form. |
| **A recurring cast, with an "appears in" index.** The same label or outlet comes back across cards instead of new names per card. | factions.md §7 | Our recurring cast is the notables plus the *things*: the Keeper's tree, the name stone, the first dog, the travellers' camp, the far-shore family. Stories should hang on them. people-and-things.md is the natural index. |
| **No choice is free (TQ axis).** One option that wins on every axis makes the choice decorative. The worst form is "pay to avoid", where both options do the same thing at different prices. | narrative-bible Rule 7, audit Pattern 5 | Our version is the **same-effect twin**: two options whose only difference is the line of text (`small_story`, `village_harvest`). Each option must leave a *different* life in the box. |
| **Consequence depth (CD axis): set a flag that a later card reads.** The fix was mechanical, not more prose: `sets_flags` / `requires_flags`, and "defaults never set flags". | audit Pattern 3, rewrite.md Phase 2 | We have the flags (`State.customs`), but **nothing reads them**. Same gap, half-built. |
| **Trigger fit / state awareness (TF, SA).** No "scathing review of recent work" when there is no recent work. Gate the event on the state it talks about. | audit Patterns 2 and 4 | Our version: text about things the tank doesn't have (hens, sheep, hills, chimneys, Hat Day, the Lamp, a cat). Global word swaps (`LandWords`) are a patch that misfires. |
| **Human consequence over the stat.** Say what the change looks like, never the number. | narrative-bible Rule 4 | Already our style. For us "what it looks like" should be a thing in the box, not only a sentence. |
| **Short.** Situation 8 to 15 words, result one sentence. One concrete image per line. No repeated words. | Rules 2, 3, 6 | Our asks are mostly good. The repetition problem here is the same story told on 2 or 3 surfaces (card, narrator bubble, first-person bubble). |
| **Cadence layers:** flavour (frequent, cheap), operational, crisis (rare), relief/payoff, **callback** (from a prior choice, after a delay). KPI: *narrative callback rate* = events that reference prior state / all events. | design-framework, pacing-and-balance | Our flavour layer (trait stories, term lines, weather) is large and remembers nothing. The callback layer is thin: 32 callback lines, all text. |

The tank adds three rules on top of the AGT ones (already decided here):
1. **Closed world.** The people live in the tank and only the narrator says "tank". Each era has its own beliefs
   (chronicle-and-history §5). Every premise has to survive a second look.
2. **Seen, not read.** An event matters if it happens in the box while the Keeper watches. Text is the caption, not
   the event.
3. **One owned choice per visit.** It is unmissable, never resolved unseen, and its consequence is a thing in the
   box that stays (research 16 §4.5).

## 2. The rating

Five axes, 1 to 5 each (25 max), adapted from AGT's TQ/SA/TF/CD/MF:

- **N · Named.** Does it name a specific person, animal or thing in *this* box? (5 = the ask itself names them; 1 = "someone", "the people")
- **M · Matters.** Does it change the box? For a choice: do the options lead to different lives? For a story: does
  anything change? (5 = different futures; 1 = text only, or options that do the same thing)
- **R · Remembered.** Is it read again later: a callback, a prop that grows, a person-card line, a flag that
  something reads? (5 = it keeps coming back; 1 = forgotten)
- **S · Seen.** Is it happening in the box? (5 = staged scene plus a lasting prop; 1 = text only)
- **W · World fit.** Is it true to *this* tank and era? Does it refer to things that exist? Does the premise hold up?
  (5 = clean; 1 = refers to things that don't exist, or nonsense after the word swap)

Verdicts: **Keep** (≥ 20), **Fix** (a quick win makes it good), **Rework** (needs a system change to earn its
place), **Cut** (irrelevant or unreachable; weed it out).

### 2.1 Keeper's choices (big, small, life)

| Item | N | M | R | S | W | Total | Verdict | Why |
|---|---|---|---|---|---|---|---|---|
| `lost_pup` (bring it home / leave it) | 5 | 5 | 5 | 5 | 5 | **25** | Keep | The model to copy: a named child, a dog that lives in the box for generations, and the choice is staged. |
| `camp_fire` (who leads) | 5 | 5 | 5 | 5 | 5 | **25** | Keep | The two named candidates become different futures (chief seat, rival or friend, feather post, callbacks). |
| `village_headman` | 5 | 5 | 5 | 4 | 4 | **23** | Fix | Same strengths as `camp_fire`. The callback "meetings by the well" assumes a well; gate it on the well. |
| `small_naming` | 5 | 4 | 4 | 4 | 5 | **22** | Keep (+ long term) | The name lasts a lifetime. It has no callback yet: the "Pip, named by the Keeper, grew up..." payoff is missing. |
| `camp_remember` (stone or tree) | 5 | 2 | 5 | 5 | 5 | **22** | Keep | No sim number changes, but the stone or tree gains a name or charm at every later death. Memory through a thing: exactly right. |
| `wolf_thief` | 1 | 5 | 5 | 5 | 5 | **21** | Fix | Real, lasting divergence (boldness, the grey wolf at the bones). The ask is unnamed: name the person at the rack. |
| `white_stag` | 1 | 4 | 4 | 5 | 5 | **19** | Fix | Strong scene. Two different "Let them decide" rules disagree (chief's trait vs a 50/50 hash). Its words are still marked `[est]`. |
| `camp_travellers` | 2 | 3 | 4 | 5 | 4 | **18** | Fix | The ask says "two lost travellers", but they have names. "Full packs" still ends with the same two joining later, so the options differ mostly in timing and a hidden wood bonus. |
| `village_hill_family` | 2 | 4 | 4 | 5 | 2 | **17** | Fix | The options really diverge (here vs a far-shore hut). On a living land the word swap turns "They moved in with a goat" into "...with the warmest spot in the tent". There is a goat pen for a goat the tank can't show. |
| `small_supper` | 1 | 3 | 1 | 4 | 5 | **14** | Keep (low) | Staged for two evenings, then gone. A fine one-touch filler; it shouldn't claim more than that. |
| `small_story` (who tells tonight's story) | 5 | 1 | 1 | 2 | 5 | **14** | Fix or Cut | **The pick does nothing.** `LifePlanner.Day.cs:191-206` chooses the teller and never reads the Keeper's choice. A choice that is ignored teaches the player that choices don't matter. |
| `village_spring` (miller or roofers) | 1 | 3 | 2 | 3 | 4 | **13** | Rework | "The young ones" are unnamed. The only real effect is an invisible 36-month production bonus, and the code says choices never give a better economy. |
| `wolf_kill` | 1 | 3 | 2 | 2 | 5 | **13** | Rework | Repeats per kill, unnamed, no scene for the choice. It reads as busywork next to `wolf_thief`. Fold it into a staged hunt or cut it. |
| `camp_winter` (baskets or woodpile) | 1 | 3 | 2 | 3 | 2 | **11** | Rework | The effect is a hidden economy bonus. The custom flag is never read. On a living land the word swap breaks the done line: "The Keeper chose the camp. The axes rang till the first snow." |
| `village_harvest` (feast or offering) | 1 | 1 | 2 | 2 | 4 | **10** | Rework or Cut | A **same-effect twin**: a prop plus 2 callback lines. The callbacks describe customs ("the feast lasts three days now", "the basket goes up the hill every autumn") that the box never performs. |
| `duck_pen` | 1 | 3 | 2 | 3 | 5 | — | Cut (for now) | **Unreachable:** nothing outside a debug cheat builds the duck pen. Bring it back with #431. |

### 2.2 The chronicle story stream (monthly, 7% a month)

| Item | N | M | R | S | W | Total | Verdict | Why |
|---|---|---|---|---|---|---|---|---|
| Wedding / birth | 5 | 4 | 5 | 5 | 5 | **24** | Keep | Named, changes the family tree, staged as a held ceremony, in the book. |
| Restraint rulings ("leave the does in spring") | 5 | 5 | 5 | 4 | 5 | **24** | Keep | A named person makes a rule the hunters follow from then on. The best-written "history" in the game. |
| Dog lines (first dog, litter, grey muzzle, guard) | 5 | 4 | 5 | 5 | 5 | **24** | Keep | A recurring named animal with a life arc. Copy this pattern. |
| Life-jar arrivals, pond, first green | 2 | 4 | 3 | 5 | 5 | **19** | Keep | Seen, closed-world, literal. "The sky sent game" is the people's belief done right. |
| Role and office lines (won, contest, succession) | 5 | 4 | 4 | 3 | 3 | **19** | Fix | Good bones. Out-of-world bits: "each built a chimney", "telling the goats it was rigged", "blessing on the goats", "a very large dog". |
| Evening teasers: seat, couple, rivalry | 5 | 3 | 4 | 3 | 4 | **19** | Fix | Setup and payoff that the cards actually score. "aren't speaking **again**" is said even the first time. |
| Relics: Mask, Doll, Sandal | 4 | 3 | 5 | 4 | 4 | **20** | Keep | The sandal is a perfect setup and payoff (lost, then found 20 years later). It assumes a "remembering stone" that may not exist. |
| First-building lines | 3 | 3 | 3 | 5 | 3 | **17** | Fix | Fine, but some claim a lot ("where the Keeper sometimes appears", "water tasted of glass" said in the narrator's voice). |
| Friendship / rivalry / reconcile / apprentice | 5 | 3 | 3 | 1 | 3 | **15** | Rework | Named and remembered, but never seen. The quarrel subjects are a goat, a fence post or a song lyric. These want a visible beat: they sit apart, or they share a bench again. |
| Anniversary / first snow / settlement naming | 2 | 2 | 2 | 3 | 5 | **14** | Keep | Rare milestones. Cheap and true. |
| Elder tells the founding story | 4 | 1 | 1 | 2 | 3 | **11** | Fix | Says "found the glass" in the camp. Could tie to the real winter storyteller scene. |
| **Term-in-office lines** (48 lines + 9 codas) | 5 | 1 | 1 | 2 | 2 | **11** | Cut half | Text-only flavour about things that aren't there: Hat Day, the sack race, egg-and-spoon, a tower "to see over the glass", "the far edge, where the world turns to glass", lamps, a library, a weathervane. Keep only lines whose thing exists in that era's box. |
| **Trait stories** (28 lines + 4 stand-ins) | 5 | 1 | 1 | 1 | 2 | **10** | Cut most | **The irrelevant layer.** A wheelbarrow with five wheels, a turnip the size of a sheep, a pink roof, a kitten on a roof, "the harvest festival" in a camp. None is seen or remembered, and several repeat bank lines word for word. |
| Weather: storm, rainbow, fog, drought, bumper | 1 | 1 | 1 | 1 | 4 | **8** | Rework or Cut | Generic and unseen. A storm "rattled the glass", but nothing happens in the box. Either stage it (rain, people under cover, wet hides) or keep it in the History list and off the return cards. |
| Lost in the hills / found with a thing | 5 | 1 | 1 | 1 | 1 | **9** | Cut | The tank has no hills. The objects (a very old boot, a crown of twigs) come from nowhere. Keep only the first-loss sandal. |
| Evening "slice of life" teasers (16) | 2 | 1 | 1 | 2 | 3 | **9** | Cut or make them pay off | A teaser with no payoff is a broken promise (AGT: setup needs a payoff). "The kids are building a raft." There is no raft. |
| Strange light / tavern sheep / ducks (old boxes) | 1 | 1 | 1 | 1 | 1 | **5** | Cut | The Lamp variant needs a shaping that a living land can't place. There is no sheep. The duck story only fires on old boxes. |

### 2.3 The people's own words (bubbles)

| Item | Verdict | Why |
|---|---|---|
| Held-moment lines (wedding, newborn, festival, new leader) | Keep, fix 2 | They are said during a staged scene, which is right. Fix "Long live the new chief!" for a headman or lord, and "Pass the bread" in a camp with no bread. |
| Tree lines (38) | Keep | Tied to a thing the Keeper planted, gated by its growth, by trait. The best bubble set. |
| Trait lines (42, by belief stage) | Keep, weed | Good voice. Weed "the dog barks" when there's no dog, and "the Keeper's lamp", "library" and "market" when they don't exist. |
| Town wants (28) | Keep | True to the sim's next need. Check the plank and chalk lines against the camp. |
| Story-voice first person (74) | Fix | It repeats the card next to the card. Pick **one** surface per story: the bubble *or* the card quote. |
| Line bank (44) | Weed | About 25 of the 44 are never requested by the view. "Pip" (bank 3, 27) and "Not you, goat" (17) name things the box doesn't have. The lines about whiskers and a cat leak the Lens before the Lens. |
| Care mood and cheer lines (10) | Cut | Dead: nothing reads them. They mention a sock, crops, a garden and hills. |
| Wish lines, nudge answers, gift teasers, Sky Crumb, Lens, tap-back | Park | Unreachable on a living land (the Shape button is hidden). Decide if shaping ever returns. If not, delete. |

## 3. Quick wins (text and small code; no new systems)

Ordered by value for the effort.

1. **Make `small_story` real, or cut it.** Have `LifePlanner.Day` use the Keeper's pick as tonight's teller and stage
   it with the hint ("slow, with all the voices" / "short, and gets a laugh"). An ignored choice is the most damaging
   item in the game.
2. **Stop the `LandWords` misfires.** Write per-land text for the lines it breaks instead of global swaps: "chose the
   camp" (camp_winter), "with the warmest spot in the tent" (hill family), "the camp grew so tall it got a roof".
3. **Weed the out-of-world lines.** One pass over §4.1, §6.1, §6.4, §5, §11 of the inventory. Remove: hens, fox,
   kitten, sheep, turnip "size of a sheep", "harvest festival" in the camp, Hat Day, sack race, egg-and-spoon, pies,
   chimney, library, "a very large dog", "tower to see over the glass", "where the world turns to glass", hills, a
   boot or crown found in the hills, rafters in a camp, "Pip", "Not you, goat", and whiskers or a cat before the Lens.
   Narrator "glass" in the camp and village is allowed (Choice 10), but the people must not say it. Check the elder line.
4. **Delete dead and unreachable text:** CareConfig mood and cheer lines, `duck_pen` (until #431), the `ducks` and
   `sheep` stories, and the Lamp lines of `strange_light`. Also the bank lines no view ever asks for.
5. **Name who is asked about** (AGT's "concrete situation, named stakeholder"):
   - `camp_travellers`: "Two travellers, {t1} and {t2}, lost at the edge of {land}. Can they stay?"
   - `village_spring`: "{y1} and {y2} are old enough to work. The miller or the roofers?"
   - `wolf_thief`: "{hunter} saw the wolves take meat from the rack again."
   - `wolf_kill`: the hunter who found the kill.
6. **One telling per story.** A trait or bank joke shows once: card *or* bubble, never card + narrator bubble +
   first-person bubble.
7. **Small truth fixes:** one "Let them decide" rule for the white stag; "Long live the new {role}!"; the rivalry
   teaser only says "again" on a repeat; gate `headman_*_meetings` on the well; gate the sandal's "remembering stone" on
   a stone that exists.
8. **Settle the lean contradiction (user call).** `DilemmaDefs.cs` says choices never give a better economy, but 4 of
   the 8 big choices apply a 36-month x1.35 food or x1.5 wood bonus. Either drop the leans, or make the lean
   *visible* (the baskets prop overflows for those 3 years) so the choice's effect can be seen.

## 4. Long-term improvements (systems)

1. **Read the custom flags (AGT Phase 2, adapted).** `State.customs` is written and never read. Give each tradition a
   recurring, *staged* payoff:
   - feast: the long table is set every autumn, as a held moment;
   - offering: someone carries the basket up the hill each autumn;
   - stone: children polish the names;
   - winter baskets: the berrying walk every autumn.

   That turns `village_harvest` and `camp_winter` from same-effect twins into choices that shape the year. Follow
   AGT's invariant: *defaults never set flags*, so only a Keeper's answer starts a custom.
2. **Stage it or drop it from the return cards.** Every chronicle kind either has a scene, or it is history-only (kept
   in the History list, never a card). This applies research 16 and "seen, not read" to the story stream. It also
   fixes the 7%-a-month flavour flood:
   - storm: people under cover, wet hides;
   - drought: the pond line drops, a bucket chain;
   - bumper: the basket pile grows;
   - friendship: two people share the bench;
   - rivalry: the same two sit apart.
3. **Turn trait stories into trait *things*.** Instead of "invented a bucket that pours itself" as text, the tinkerer
   leaves a real prop (a crooked bucket, a pointier stick) that the people then use. This follows the
   people-use-everything rule (people-and-things.md). A trait story earns a card only when it leaves something.
4. **A recurring cast of things, with an "appears in" index** (AGT factions.md §7). Keep one list in
   people-and-things.md of the named things stories may hang on: the Keeper's tree, the name stone, the first dog and
   its line, the travellers' camp, the far-shore family, the feather posts, the chief's antlers. Writers pull from it
   instead of inventing a goat. New text must point at a row.
5. **Callbacks for the small choices.**
   - Naming: when a Keeper-named child comes of age or marries, a line credits it ("{name}, named by the Keeper,
     married {b}").
   - Story teller: the one picked twice becomes the winter storyteller when old.

   Small choices then come back for decades, as research 16 §4.4 promised.
6. **A callback-rate check.** AGT's KPI: events that reference prior state, divided by all events, measured from a
   sim run (the SimCore tests can drive N years). Aim to raise it each pass. Today it is roughly 32 callback lines,
   the teasers and the relics, against hundreds of one-off lines.
7. **Content tests instead of word swaps:**
   - a banned-word list (outside world; things not in this box);
   - an era gate on narrator "glass" lines;
   - a check that every `{thing}` a line names exists (the well, the stone, the dog).

   The closed-world memory already asked for this.
8. **An authoring checklist for this game** (AGT narrative-bible §3, adapted). It would sit in `docs/design/` and be
   used by every writing brief:

   - [ ] Names a person, animal or thing in *this* box (from the cast list)
   - [ ] The premise survives a second look; closed world; the people use their era's words
   - [ ] Ask ≤ 15 words; done line one sentence
   - [ ] Each option leaves a **different** thing **and** a different life (no same-effect twins)
   - [ ] What it leaves is seen within a game year; something reads it later (callback, prop, person card)
   - [ ] Told once, on one surface
   - [ ] Gated on the state it talks about (era, building, animal, prior choice)

## 5. What to weed out, in one list

These are the "irrelevant events" that make up most of the noise. Cut, unless the rework above happens:
- **Trait stories:** about 24 of 28. Keep only the ones that leave a prop, once §4.3 exists.
- **Term-in-office lines:** about half. Keep the ones true to the seat and era.
- **Weather lines:** off the return cards, into history only.
- **Lost in the hills, and found with a thing:** all except the sandal.
- **Slice-of-life teasers:** all 16, unless each gets a payoff.
- **`strange_light`, `sheep`, `ducks` (old), CareConfig lines, `duck_pen`:** cut.
- **`small_story`, `village_harvest`:** cut, unless wired as in §3.1 and §4.1.
- **`wolf_kill`:** fold into a staged hunt, or cut.

## 6. The user's calls (2026-11-14)

1. **The leans: drop them.** No choice changes the economy, as `DilemmaDefs.cs` already claims.
2. **`village_harvest` and `camp_winter`: rework them as rituals** that replay through the rest of the playthrough
   (§7.1).
3. **Shaping-only text: delete** (wishes, nudge answers, gift teasers, Sky Crumb, Lens, tap-back, the Lamp lines).
4. **Weather and trait flavour: drop it.** It is gone from the cards and from History. New events replace it (§7).

## 7. The new events that replace the cuts (proposal, for sign-off)

Yes, there are new events, but fewer and heavier ones. The cuts remove the noise. What replaces it is a small set of
event families, each passing the §4.8 checklist: named, a different life per option, seen, remembered. The cast
list (§4.4) comes first, so every new event hangs on a real thing. All wording below is [est] until the user signs
it off.

### 7.1 Rituals (from choices, replayed each year)

A tradition the Keeper chose becomes a **held moment that plays every year in its season**. It grows with the box,
and later events can refer to it. Only an answered or delegated choice starts one; a lapse starts it too, but the
people get the credit.

| Ritual | Chosen in | Replays | Grows by era | Later hooks |
|---|---|---|---|---|
| **The berrying walk** | `camp_winter` "the baskets" | Every autumn, everyone walks out to the bushes together and comes home with purple fingers | Village: the walk ends at the long table | A child's first walk; the eldest's last walk |
| **Stacking the woodpile** | `camp_winter` "the woodpile" | Before the first snow, a line passes logs, and the roof on the pile is mended | Village: every new roof gets its first log from it | Who carries the first log (a small choice) |
| **The harvest feast** | `village_harvest` "a feast" | Every autumn, the long table at the fire gets a little longer each year | Town: the tavern keeps the table | The feast song (7.3); a couple who met at it |
| **The offering** | `village_harvest` "an offering" | Every autumn, someone carries the best basket up the hill and sets it down for the Keeper | Town: the priest leads it | Who carries it this year (a small choice); the empty basket in spring |
| **Remembering** (exists) | `camp_remember` | Each death adds a name or a charm (today). New: once a year, a quiet visit to the stone or the tree. This is the user's favourite kind of scene | The stone is polished, the charms are counted | The named dead are told at the visit |

### 7.2 Things with a life (from the cast list)

Short arcs on the box's named things. Each one is a beat you can see, with one line of caption.
- **The Keeper's tree:**
  - its first fruit, which the children share;
  - a couple carves their initials;
  - the kids hang a swing;
  - an old branch falls in a storm, and the people make something from it.
- **The first dog's line:** the existing lines, plus a pup that picks its child.
- **The name stone:** it runs out of room, and a second stone is set beside it.
- **The chief's antlers:** they pass down with the seat.
- **The travellers' camp and the far-shore family:** visits, a wedding across the pond, the tower.

### 7.3 Trait moments that leave a thing

They replace the 28 trait stories. Only a few are written, and each **leaves a prop the people then use**
(people-and-things.md):
- the tinkerer's crooked bucket at the pond, which others borrow;
- the singer's song, which gets a name and is sung at the next ritual;
- the gardener's flowers by the stone;
- the joker's stone face on the woodpile.

The card says who made it; the person card keeps it.

### 7.4 Weather that happens in the box

This replaces the dropped weather lines, and it is staged or it doesn't exist.
- The first storm: a tent cover blows loose, and next day two named people mend it.
- A dry summer: the pond line drops, and a bucket chain carries water.
- A rich autumn: the basket pile overflows.

Each is told once, at most, the first time. After that it is just weather.

### 7.5 More small owned choices

These are cheap to write, and each one feeds a ritual or a thing, so it comes back:
- who carries the offering, or the first log, this year;
- which couple dances first at the feast;
- the name of the singer's song;
- which child holds the new pup.

### 7.6 People callbacks

- A Keeper-named child comes of age or marries: "{name}, named by the Keeper, ...".
- Rivals' children become friends.
- The unchosen candidate's advice is finally taken.

### 7.7 Budget

Fewer cards than today, but a higher callback rate. Rituals and thing arcs carry most of the "history"; one-off
lines become rare. Pacing follows research 16's director, and no new event is added without its scene and its prop.

## 8. The work, as issues (filed 2026-11-14, Polish milestone)

The proposed replacement text for every line is inside each issue, marked [est] until the user signs it off. It was checked against the style guide in #713 and run through the humanizer skill: no em or en dashes, straight quotes, the glossary's fixed words.

| Order | Issue | Lane | What |
|---|---|---|---|
| 1 | #713 | docs | Writing guide: checklist, style guide, the cast of things |
| 2 (parallel) | #714 | sim | Choices: no leans, named asks, here or there, one white-stag rule |
| 2 (parallel) | #715 | sim | Story stream: drop weather, trait stories, slice teasers; no goats or pies |
| 2 (parallel) | #716 | sim | Office and role lines: only things that exist |
| 2 (parallel) | #717 | view | Tonight's storyteller: the Keeper's pick tells the story |
| 3 | #718 | sim + Editor | Delete the shaping-only text (after #714, #715) |
| 3 | #721 | view | Rituals: the harvest and winter choices replay every year (specs first; after #714; with #693) |
| 4 | #719 | view | The people's words: one telling per story (after #715, #718) |
| 5 | #720 | sim | LandWords: no global swaps (after #714, #715, #716, #719) |
| 6 | #723 | sim | Content test: every line true to the box and the era (last) |
| any | #722 | docs | New events that replace the flavour layer (design; after #713, #721) |
