# Aquarium Civ — Game Definition (draft v0.1, 2026-10-28)

Status: **approved 2026-10-28: all four variants go through prototyping.** Built from the Phase C interview (answers logged in
[../decisions.md](../decisions.md)) and the research in [../research/](../research/).
Anything tagged **[H → Pn]** is a hypothesis that prototype *Pn* in [prototype-plan.md](prototype-plan.md)
must confirm before we treat it as decided.

---

## 1. One-liner and core fantasy

**A tiny civilization lives out its whole history inside a glass box on your shelf. You are its Keeper:
keep the glass clean and the world tended, nudge it now and then, and come back to find out what happened.**

Core fantasy: *being the quiet god of a world in a jar.* You don't rule them. You look after the box they
live in, and in exchange you get to watch a history unfold: families, founders, feuds, festivals, eras.
The history is saved and becomes something you can browse and show off (the WorldBox itch, made cozy).

Unifying idea, **care = clarity**: grime and fog on the glass literally hide the history from you.
Cleaning is how you get to *see* again. **[H → P1, P2]**

## 2. Pillars

1. **A history worth watching.** The civ runs itself and produces stories: named notables, lineages,
   firsts, disasters weathered. The history is kept (Book of Ages) and shown in a way worth sharing.
   *Reference: WorldBox.*
2. **Care is a toy, not a chore.** Mess is visible, a bit comic, reversible and capped. Cleaning gestures
   feel good even when nothing needs fixing. **[H → P1]**
3. **Keeper, not King.** You tend the environment and nudge; the people decide what to build and who to be.
4. **Absence is welcomed.** No guilt pings, no deaths from neglect, no collapse. The people wave when you return.
5. **The glass is the interface.** Grime, fog, condensation and overgrowth show the tank's state without HUD bars.
6. **Fair to your time and wallet.** No timers to skip, no ads farm, no gacha, never sell relief from neglect.

### Anti-references (we must not become)
- **FarmVille / Godus**: withering, forced check-ins, timers built to be skipped.
- **Tamagotchi guilt**: being afraid to open the app.
- **Menu idle civs** (CivIdle, My Civilization): numbers and menus with nothing alive to watch.
- **F2P ad-farms** (Terrarium: Garden Idle, IdleOn): interstitials, gacha, FOMO events, gem bundles.

## 3. Loops

| Loop | Length | What happens |
|---|---|---|
| **Glance** | ≤ 60 s | Open → people wave at the glass → glance at the top chronicle card → maybe one wipe → close. |
| **Check-in** (main) | ~3 min | Chronicle (3–7 curated "while you were away" cards, fly-to camera) → clean (wipe glass, clear 1–3 messes) → one nudge or project → leave. |
| **Deep session** | ~20 min, occasional | Witness an **era transition live** (the box visibly transforms), watcher-only events (festivals, rituals, photo mode), and **terraforming / decorating** the box. |
| **Long term** | weeks–months | Eras inside one box → the box **grows** (lid opens, annexes) → a final era, then a **new box on the shelf**; the old one stays alive as a museum, relics carry over. |

## 4. The civ

- **Population:** an anonymous crowd (dozens early, hundreds later) plus **5–12 named notables** with a face,
  one trait, a lineage and remembered events. Pin a favorite to follow. Dead notables go to a legends roster.
- **Death:** old age only. Neglect never kills, and the civ never collapses.
- **Time:** **1 real hour ≈ 1 game year** (tunable). A 2-day absence is about 48 years of story.
  **Notables age at half speed** (decided 2026-10-28), so a named person lives about 5 real days instead of ~2.5;
  the crowd ages at full speed. P3 still checks that this is enough for attachment.
- **Two clocks:** civ time is compressed. Care/mess time runs in **real hours**, so grime is tied to your
  real cadence, not to game years.
- **Simulation:** an aggregate economy (food, materials, mood, population, mess) stepped coarsely (one game month),
  with event templates filled from sim state ("Narrative Legos") and ranked by salience for the chronicle.
  Offline catch-up runs the same coarse sim, deterministic from a seed.

## 5. Care / neglect mechanic

**What gets messy** (all real-time, all capped at ~48 h of neglect):

| Mess | Source | Look | Clean gesture (to test) |
|---|---|---|---|
| **Glass grime / fog** | time + humidity | smudges, algae film, condensation hiding the view | drag-to-wipe the screen vs tap **[H → P1]** |
| **Waste / trash** | the civ's own activity (grows with population) | trash heaps, smog under the lid, clogged stream | tap/flick away |
| **Overgrowth / weather** | the terrarium itself | vines over roofs, weeds, dry patches, needs rain/light | prune, water, give light |

Disease is not in scope for now.

**Rules**
1. **Comic chaos + gentle guilt.** Neglect shows as visible, slightly funny mess, with grumpy people waving
   for help, and a **slight output drop**: capped around −25 %, and stock is never lost. **[H → P2: cap size, and whether the drop helps]**
2. **Capped.** After about 48 h, nothing gets worse. The tank sits at "struggling but fine".
3. **Recovery is fast and juicy.** A full clean takes under a minute, with sound, particles and people cheering.
4. **Welcome back.** The chronicle opens on good news first.
5. **Care moves up.** As eras advance, the people learn to handle their own trash and gardens (sanitation,
   gardeners). **The glass is always yours**: they can clean their world, never the outside of the glass.
6. **Never monetized.** Cleaning is never sold, sped up or ad-gated.
7. **Notifications** (opt-in, after the first night): only for things the player started, warm and diegetic,
   at most about one per day. Never decay pings.

## 6. Control: caretaker + nudges

You never place buildings for them. Your tools:
- **Care**: clean, water, light, prune.
- **Nudges**: gentle influence the people respond to, and which the chronicle credits to you ("After the
  Keeper's rain, the Lund family founded the first farm"). Two candidate forms **[H → P5]**:
  *omens* (place a shrine or totem → the civ leans toward farming, craft or faith) vs *environment shaping*
  (rain, plant a forest, divert the stream; the people adapt).
- **Projects / standing policies** (later eras): player-chosen durations (1 h / 8 h / 24 h), so the appointment
  belongs to the player.
- **Terraform & decorate** (deep sessions): reshape terrain and decorate the box and plinth. Placed nature becomes
  part of the sim (forest = wood, stream = farms).

## 7. History: the "cool way to show it"

The history is a headline feature, not a log. Layers:
1. **Live ticker**: thought bubbles and tiny floating text in the box.
2. **Chronicle**: curated cards on each return, with fly-to camera. **[H → P3]**
3. **Book of Ages**: the browsable full history. Candidate presentations **[H → P4]**: a **time-lapse scrubber**
   (replay the box growing over the years, with event pins) vs an **illustrated book** (era chapters, notable
   portraits, family trees).
4. **Shelf museum**: finished boxes keep living, and their history stays browsable.

Cheap enabler: record a compact snapshot per game year (buildings, population, notables) so time-lapse replay is
just playback. No video capture needed.

## 8. Progression

- **Eras inside a box** (draft): Camp → Hamlet → Village → Town (KayKit Medieval covers these). Later eras
  (city, industrial) need more art (Kenney kits). Each era changes the diorama *in kind* and adds a system.
- **Eras are plateaus, crossed live.** The civ keeps living offline, but it waits at an era threshold until you
  are watching, so you never miss the big beat. **[H → P6]**
- **The box grows**: lid opens, annex boxes attach, the plinth extends.
- **New box on the shelf** (prestige, non-destructive): a new biome or ruleset, relics and myths carry over, and the old box
  stays alive. **[H → P6: does growing the box or starting a new one feel better?]**
- **Automation retires basic chores** (section 5, rule 5).

## 9. Session design

- Target cadence: 2–5 check-ins/day, ~3 min each; a deep session a few times a week.
- Every session must be satisfying on its own, including a 30-second glance.
- Offline: catch-up is simulated in full, but **mess is capped at 48 h**. There is no offline-earnings cap in the
  Egg, Inc. sense (that's open, see section 11).
- Vacation / dormancy: optional "Hibernate the box" for long absences (no mess accrues).

## 10. Monetization hypothesis

- **Free first era + one-time unlock** (~$4.99–9.99) for the rest of the game and the shelf.
- Later: **cosmetic packs** (glass styles, plinths, lids, biomes) and **paid box expansions** (new biome/era sets).
- No ads, no skip currency, no gacha, no expiring passes, never sell care.
- Hypothesis only; it is validated by playtests and the market, not by prototypes.

## 11. Open questions

| # | Question | How we answer it |
|---|---|---|
| Q1 | Drag-to-wipe or tap-to-clean? | P1 |
| Q2 | Does the slight output drop help or create guilt? How big? | P2 |
| Q3 | ~~How fast should notables age?~~ Decided: half speed. P3 confirms attachment | P3 |
| Q4 | Which history presentation is the "cool" one? | P4 |
| Q5 | Omens or environment shaping as the nudge? | P5 |
| Q6 | Box grows vs new box on the shelf: which carries progression better? | P6 |
| Q7 | Does the civ's progress need any offline cap, or is "mess caps, history continues" enough? | Design, after P2/P3 |
| Q8 | How many eras, and does the game have an ending per box? | Design, after P6 |
| Q9 | Home-screen widget showing the tank? | Research later |
| Q10 | ~~Tester distribution~~ Decided: **iOS via TestFlight** (user has an Apple developer account; testers have iPhones) | Needs the iOS CI pipeline before the first diary test |

## 12. Concept variants (approved: all four are prototyped)

All four respect the decisions above. They differ on the **riskiest axes**: where the fun lives (caring vs
watching vs shaping) and what the unit of progress is (one long box vs many short boxes).

### Variant A: "Keeper's Glass" (care-forward)
- **Pitch:** a living tank that's a joy to keep clean.
- **Fantasy:** the satisfying ritual of tending a beautiful little world.
- **Loops:** *moment*: wipe, flick, prune, with heavy juice. *Session*: return → clean → watch the people
  celebrate → nudge. *Long term*: eras unlock new kinds of mess and new tools; automation takes over basic mess.
- **Care without chore:** cleaning is a tactile toy (ASMR-like). Mess is comic and capped, and it pays out instantly (cheer, gift).
- **Biggest risk:** the 20th wipe feels like a chore.
- **Prototype must answer:** *Is cleaning still satisfying on day 4?* (P1, P2)

### Variant B: "Chronicle Box" (watch-forward, history-first)
- **Pitch:** WorldBox in a jar: a tiny civ writes its own history while you're away.
- **Fantasy:** discovering what your people did, and owning their saga.
- **Loops:** *moment*: follow notables, read thoughts. *Session*: the chronicle reel is the main content,
  then the Book of Ages. *Long term*: a deep, shareable history across eras and boxes.
- **Care without chore:** care is minimal and tied to seeing: grime blurs the view, and a quick wipe "unlocks" the chronicle.
- **Biggest risk:** the generated history is samey or boring (curation quality; solo-dev content load).
- **Prototype must answer:** *Do players remember and care about what happened in their box?* (P3, P4)

### Variant C: "Gardener God" (shape-forward)
- **Pitch:** shape the terrarium; the people adapt to the world you make.
- **Fantasy:** a gentle god sculpting land, water and weather.
- **Loops:** *moment*: rain, plant, divert water. *Session*: tend overgrowth and weather, shape a new area,
  watch the people settle it. *Long term*: the box grows and the biomes diversify.
- **Care without chore:** care and creation are the same verbs (pruning is also shaping).
- **Biggest risk:** it drifts into a sandbox builder without goals (Pocket Build's weak retention).
- **Prototype must answer:** *Does shaping feel like influence on a living civ, not like building?* (P5)

### Variant D: "Shelf of Worlds" (many short boxes)
- **Pitch:** each box is a short civilization (1–2 weeks) that ends and joins your shelf.
- **Fantasy:** a collector's cabinet of finished little worlds, each with its own story.
- **Loops:** *moment*: same as A/B. *Session*: check the active box. *Long term*: complete boxes, collect
  biomes and endings, start a new box with new rules.
- **Care without chore:** each box is short, so care never becomes a months-long obligation.
- **Biggest risk:** attachment resets each box; content burn for a solo dev.
- **Prototype must answer:** *After a box ends, do players want a new one, or miss the old one?* (P6)

### Recommendation
Don't pick one now. The prototypes sequence them: **A is tested first** (P1–P2, the riskiest pillar), then **B**
(P3–P4, the user's WorldBox/history ask), then **C and D** as the control and progression layers (P5–P6).
Expected outcome: a **B core with A's glass care** ("care = clarity"), C as the deep-session layer, and D-lite
(the shelf) as prestige. If A fails at Gate 1, we fall back to B with care reduced to the glass only.
