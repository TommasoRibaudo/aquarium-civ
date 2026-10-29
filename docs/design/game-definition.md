# Aquarium Civ — Game Definition (v0.2, 2026-10-29)

Status: **approved by the user 2026-10-29.** v0.1 (2026-10-28) defined four concept variants and the prototypes that
tested them. v0.2 folds in the prototype gates (Gates 1–3, [decisions.md](../decisions.md)), the art spike and the
two-batch interview of 2026-10-29 ([game-definition-interview.md](game-definition-interview.md)). Items marked
**[slice]** are hypotheses the vertical slice's week-long test checks ([vertical-slice-plan.md](vertical-slice-plan.md)).
The v0.1 text, with the four variants, is in git history.

---

## 1. One-liner and core fantasy

**A tiny civilization lives out its whole history inside a glass box on your shelf. You are its Keeper:
keep the glass clean, shape the land now and then, and come back to find out what happened.**

Core fantasy: *being the quiet god of a world in a jar.* You don't rule them. You look after the box they live in, and
in exchange you watch a history unfold: families, founders, friendships and feuds, festivals, eras. The history is
kept and becomes something you can browse and show off (the WorldBox itch, made cozy).

Unifying idea, **care = clarity**: grime on the glass dims the people's sunlight and hides their world from you.
Cleaning is how you both get the light back.

## 2. Pillars

1. **A history worth watching.** The civ runs itself and produces stories about named people. It is kept, and shown
   in a way worth sharing. *Reference: WorldBox.*
2. **Care is a toy, not a chore.** Mess is visible, a bit comic, reversible and capped. Cleaning feels good even when
   nothing needs fixing.
3. **Keeper, not King.** You tend and shape the land; the people decide what to build, where, and who to be.
4. **Absence is welcomed.** No guilt pings, no deaths from neglect, no collapse. The people wave when you return.
5. **The glass is the interface.** Grime, condensation and overgrowth show the box's state without HUD bars.
6. **Fair to your time and wallet.** No timers to skip, no ads, no gacha, never sell relief from neglect.

### Anti-references (we must not become)
- **FarmVille / Godus**: withering, forced check-ins, timers built to be skipped.
- **Tamagotchi guilt**: being afraid to open the app.
- **Menu idle civs** (CivIdle, My Civilization): numbers and menus with nothing alive to watch.
- **F2P ad-farms**: interstitials, gacha, FOMO events, gem bundles.

## 3. Loops

| Loop | Length | What happens |
|---|---|---|
| **Glance** | ≤ 60 s | Open → people wave at the glass → the top chronicle card → maybe one wipe → close |
| **Check-in** (main) | ~3 min | **Chronicle cards** (3–7 "while you were away" stories, fly-to camera) → clean (wipe the glass, clear 1–3 messes) → maybe shape the land once → leave |
| **Deep session** | ~20 min, occasional | Any of: **shape the land** (terraform, rivers, groves, decorate the box), **browse history** (time-lapse, the small book, family trees, snapshots), **follow a person** (pin a notable and follow their day), **collections** (relics found in the box), and **an era arriving live** |
| **Long term** | 1–2 weeks per box | Camp → farming village → industrious town inside one box; at the town's peak the **box completes**, stays alive on the shelf as a museum, and a **new box** starts in a new setting with descendants and relics carried over |

First run, glance and check-in beat by beat, and how a check-in ends (Q13): [first-session-and-check-in.md](first-session-and-check-in.md) (draft, needs the user's confirmation).

## 4. The civ

- **Population:** an anonymous crowd plus **2–12 named notables** (growing with the era, below), each with a name, one trait, a family, friends,
  rivals and remembered events. Pin a favourite to follow. Dead notables go to a legends roster.
  **Relationships show in the world** (friends meet and talk, rivals argue), not only in cards (Gate 2 note).
- **Jobs and roles (notables only;** user, 2026-10-29). The economy stays aggregate: the crowd is a count, not workers.
  Every notable holds an **everyday job** (hunter, gatherer, farmer, woodcutter, potter, weaver…) that you see them
  doing in the box. A few **important roles** per era are what notables compete for: **camp 1, farming village 3,
  industrious town 7** (e.g. camp: chief; village: headman, miller, priest; town: lord, captain of the watch,
  guildmaster, master builder, merchant, …; exact lists TBD). Traits drive who aspires to what, and rivals compete.
  Successions, elections and feuds feed the chronicle ("John the hunter became chief of the camp"). Notable counts
  grow with the eras: about **2–4 in the camp, 4–8 in the village, up to 12 in the town**.
  **A role-holder's trait colours their time in office** (user, 2026-10-29), in story and behaviour only, not the
  economy: a cheerful chief means more festivals, a stern headman a tidy, quiet village, a quarrelsome guildmaster more
  feuds and chronicle drama.
  Jobs, role lists, how roles are won and trait aspirations (draft for approval): [notable-roles.md](notable-roles.md).
- **Life in the box** (user, 2026-10-29: the people "need to go around and be doing things, not just walk around").
  Every person in the view follows a **day of activities**, not random wandering: go to work at their job site and
  visibly work (chop, hoe, carry, fish, hammer, cook), carry goods along the paths, eat and gather at the fire/green,
  chat with friends, argue with rivals, play (kids), rest at home at night. Notables do their everyday job and their
  role (the chief speaks at the fire, the miller works the mill); the crowd's activities come from the buildings
  (a farm puts farmers in its field). The paths they walk are the ones the sim wears in (research 08), and later
  sweepers and gardeners do the care work (§5). It's view-side: the aggregate economy is unchanged.
  Activities per job and role, the day and seasons, social moments, reactions to the Keeper (draft for approval): [life-in-the-box.md](life-in-the-box.md).
- **Death:** old age only. Neglect never kills, and the civ never collapses.
- **Time:** **1 real hour = 1 game year** (Gate 2). **Notables age with the calendar and live 110–160 years** (user,
  2026-10-29: half-speed aging confused people), so a notable lives about 5–7 real days. The crowd ages normally.
- **Two clocks:** civ time is compressed; care/mess time runs in **real hours**, so grime follows your real cadence.
- **Simulation:** an aggregate economy (food, materials, mood, population, mess) stepped one game month at a time, with
  ~30 story templates filled from sim state ("Narrative Legos") and ranked by salience for the chronicle. Live play and
  offline catch-up run the same deterministic steps from a seed.
- **Where things get built:** the people choose sites themselves and the settlement **grows organically**: around
  water and a well or square, along paths that become roads, fields at the edge, workshops by the mill stream. Existing
  buildings never jump around; new ones infill and extend. Approach: research 08 (in progress) → a placement spike.

## 5. Care / neglect

**What gets messy** (real time, all capped at 48 h of neglect):

| Mess | Source | Look | Clean |
|---|---|---|---|
| **Glass grime** | time | smudges and film on the panes | drag to wipe (P1) |
| **Trash** | the people's activity (grows with population) | crates, heaps | tap / flick (P1) |
| **Overgrowth** | the terrarium itself | weeds, vines over roofs | prune |

**Rules**
1. **Mess dims the sunlight and slows the work pace** (linear, at most −25 % at the 48 h cap), and the people's mood
   shows it in how they walk and what they say: wistful or comic, never a demand on the player (Gate 1, P2 variant B).
   Stock is never lost.
2. **Capped.** After 48 h nothing gets worse.
3. **Recovery is fast and juicy.** A full clean takes under a minute, with sound and the people cheering.
4. **Welcome back.** The chronicle opens on good news first.
5. **Care moves up.** In later eras the people handle their own trash and gardens, and you **see them doing it**
   (sweepers, gardeners at work in the box). **The glass is always yours.**
6. **Never monetized.** Cleaning is never sold, sped up or ad-gated.

## 6. Control: Keeper + shaping the land

You never place buildings. Your tools:
- **Care:** wipe, tidy, prune.
- **Shape the land** (Gate 3, P5 variant B; toolset decided 2026-10-29, #42, [research 10](../research/10-shaping-the-land.md)
  "Decision"). Three new tools per era, each with **one gesture** in a shaping mode (one finger = the tool, two = the
  camera), and each levels up with the era instead of retiring:

  | Era | Tools |
  |---|---|
  | Camp | **rain** (rub), **grove** (tap), **herd** (flick in from the edge: deer on grass, fish in water) |
  | Village | **channel** (drag), **Keeper's Lamp** (drag the light to a spot), **hill & hollow** (pull up / push down) |
  | Town | **outcrop** (hold: stone, clay or ore, the land picks), **spring** (tap a hillside), **dye meadow** (sweep) |
  | Any time | **decorate** (tap a slot in the glass margin or the soil): free, a small beauty effect |

  **Gifts** (all but hill & hollow and decorate) are answered by the people within 1–3 game years with a credited job
  site, building or harvest ("After the Keeper's rain, Ada Lund planted a new field") and have a limit (cooldown or
  charges: experiment E2) plus natural caps. **Landscaping** (hill & hollow, decorate) is free, with nothing promised.
  Placed nature is part of the sim and changes where the people choose to build. Pond, orchard and storm come as
  results and level-ups, not tools. Every role has a tool that calls it into view (the chief leads the hunt, the
  captain of the watch starts the night round from the Lamp). A shaping can be **taken back until the people have used
  it**. Notables may **wish** for a gift in a card, never with a timer. New settings **replace** tools in the same
  slot: the oasis has palm spring, dune shift and desert rain; the mountain village has a mountain stream and
  terraces. Cut: smooth, mist, wind, flood. Omens and totems are dropped.
- Everything you shape follows the art style (e.g. a canal is code-built in the same palette, cut into the terrain).

## 7. History: the "cool way to show it"

1. **Live ticker:** thought bubbles and tiny floating text in the box.
2. **Chronicle cards** on each return, with fly-to camera (Gate 2, P3 variant A): the check-in's first beat.
3. **Time-lapse** (Gate 2, P4 variant A): replay the box growing over the years, scrub the timeline, era markers.
4. **A small, polished book** of the *major* events and people (legends, family trees), not a full log.
5. **Shelf museum:** completed boxes keep living, and their history stays browsable.

Enabler: a compact snapshot per game year (already in the sim), so the time-lapse is playback, not video.

## 8. Progression

- **Three eras per box:** **hunter-gatherer camp → farming village → industrious town** (crafts and guilds,
  renaissance-style: mills, workshops, a market square; not industrial). Each era changes the box *in kind*.
- **Eras are plateaus, crossed live.** The civ keeps living offline but waits at an era threshold until you're
  watching, so you never miss the big moment (P6).
- **The box completes** at the industrious town's peak. It stays alive on the shelf as a **museum** you can visit.
- **A new box** (Gate 3, P6 variant B) starts in a **new setting** (river delta, island, mountain valley, steppe…;
  same style kit, different layout and palette accents), chosen by the game. **Descendants carry over** (a few families
  or a notable's line found it) and **relics carry over** (found in a box, kept on the shelf, giving a small start or a
  story hook).
- **Automation retires basic chores** in later eras (section 5, rule 5).

## 9. Session design

- Target cadence: 2–5 check-ins a day, ~3 min each; a deep session a few times a week.
- Every session must be satisfying on its own, including a 30-second glance.
- **Offline:** the world runs at full speed for the first **8 hours** away, then at **a quarter speed** (a weekend away
  ≈ 18 game years instead of 48). Mess caps at 48 h. Eras still wait for you.
- **Notifications:** opt-in, **only when an era is ready to arrive**. No daily nags, no decay pings.
  Home-screen widget: after the slice.

## 10. Business model (hypothesis)

- **Free first box, one unlock at $0.99** for the shelf: more boxes and settings (user, 2026-10-29). No ads, no
  timers, no skip currency, no gacha, never sell care.
- Later, maybe more small unlockables (cosmetic glass, plinths, settings). Decided after the slice, validated by the
  slice test and the market, not by prototypes.

## 11. Art direction

**The code-built style kit** (decided 2026-10-29, [spikes/style-kit.md](spikes/style-kit.md)): every mesh generated in
code from one palette and one stylised shader. Flat-shaded low poly, soft banded light with cool shadows, baked AO,
peg-doll villagers (ordinary people, no fantasy gear), a soil cross-section behind the glass, the box on a shelf in a
room. Everything new follows it.

## 12. Open questions

| # | Question | How we answer it |
|---|---|---|
| Q11 | Organic placement: which algorithm, and does a 3-era time-lapse "grow like a real village"? | Research 08 → Growth Lab spike (running) |
| Q12 | Is 1 h = 1 year with a quarter-speed tail after 8 h the right pace? (Opens per day, "too much / too little happened") | The user's own multi-day test once a base game exists, then the [slice] week-long test |
| Q13 | What does a check-in end on, so it never feels like "nothing to do" (P2 feedback #8)? | [slice]. User: the P2 tester had no families, chronicle, people doing things or shaping; the slice has all four |
| Q14 | Which settings for boxes 2–4, and how many descendants/relics carry over? | Settings so far (user): **desert oasis**, **mountain village**. Carry-over amounts: design, after the slice |
| Q15 | Which relics exist, how are they found, what do they do? Plus power-up ideas | Ideas research (relics, a bit comic; power-ups) → the user picks |
| Q16 | Home-screen widget | **Yes** (user): a small version of the box, closed and slightly tilted. Built after the slice |

Resolved since v0.1: Q1 drag-to-wipe (P1); Q2 the pace drop, framed as sunlight (Gate 1); Q3 calendar aging, doubled
lifespans; Q4 cards + time-lapse + small book (Gate 2); Q5 shape the land (Gate 3); Q6 new box on the shelf (Gate 3);
Q7 offline slows to a quarter after 8 h; Q8 three eras, the box completes; Q10 TestFlight.
