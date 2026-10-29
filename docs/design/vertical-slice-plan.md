# Vertical slice plan (draft, 2026-10-29)

**Status: active (2026-10-29).** Gates 1–3 are decided ([decisions.md](../decisions.md)); §3 now records the chosen
path for each gate, with the other options kept below it for reference. Art direction (§6) is still open.
Scope comes from [prototype-plan.md](prototype-plan.md) Gate 3: **one box, eras 1–3, final art direction, real
save/catch-up architecture.** Design source: [game-definition.md](game-definition.md).

---

## 1. What the slice must prove
A stranger installs it from TestFlight, keeps it for a week with no explanation, and:
- opens it a few times a day without being asked (target from game-definition §9: 2–5 check-ins/day);
- can tell you a story about someone in their tank after a few days (the history pillar);
- watches at least one era arrive live and wants the next one;
- never describes cleaning as a chore or the app as guilt (care pillar, Gate 1).

The slice is the first build where we stop A/B testing mechanics and test the *whole loop* (glance → check-in → deep
session) for a week.

## 2. Locked inputs (already decided)
| Area | Decision | Source |
|---|---|---|
| Cleaning gesture | Drag-to-wipe, the first touch decides wipe vs toss | P1, P2 (decisions.md) |
| Care consequence | Mess dims sunlight and slows work pace (−25 % at the 48 h cap), mood shows it | Gate 1. Ready as `CareConfig.Gate1` + `CareMoodView` on `vs/prep` |
| Time | 1 real hour = 1 game year; notables age at half speed (fix the shown age vs calendar) | Gate 2 (recheck in the slice week) |
| Return presentation | Chronicle cards (P3 A); relationships visible in the world | Gate 2 |
| History view | Time-lapse (P4 A) + a small, polished book of major events and people | Gate 2 |
| Nudge form | Shape the land (P5 B): rain, groves, channels; no omens | Gate 3 |
| Progression shape | New box on the shelf (P6 B); the full box stays alive as a museum; era moments play live | Gate 3 |
| Citizens | Crowd + 5–12 named notables, pin a favourite | decisions.md |
| Assets | KayKit gradient-atlas kit (CC0), Kenney gap-fill | research/06-assets.md Kit A |
| Monetization | None in the slice (free first era is the hypothesis; no store code yet) | game-definition §10 |

## 3. Gate-dependent parts
**Chosen (2026-10-29):** cards · time-lapse + small book · 1 h = 1 year · shape the land · new box on the shelf.
Slice work per choice is the matching "If … wins" paragraph below; the rest is kept for reference.
### Return presentation (Gate 2, P3)
- **If chronicle cards win:** cards are the check-in's first beat. Slice work: slimmer cards that don't cover the tank
  (polish #1), clean dwell logging (#7), top card visible within 2 s of opening (practices §11).
- **If the plain return wins:** the welcome line plus the changed village; stories live in the person cards and the
  history view. Slice work: make change itself visible in the world (new buildings glow briefly, a newborn's first
  walk), since nothing else tells the story on open.
- **If neither clearly wins:** ship cards with a "skip all" that remembers the choice, and measure.

### History view (Gate 2, P4)
- **If the time-lapse wins:** history is replayed in the glass. Needs: yearly snapshots (already in the sim), clean
  share snapshots without HUD (#9), an era marker on the timeline.
- **If the book wins:** history is pages. Needs: correct family trees (#12), chapter names from their top story (#13),
  a page per era instead of per 25 years.
- **Both cases:** the shelf museum (later) reuses the same view for old boxes.

### Time compression (Gate 2)
- **If 1 h = 1 year holds:** keep it. Notable lifespan ≈ 5 real days.
- **If people die too soon:** slow notables further (×0.35) rather than the world, so the village still changes visibly
  between check-ins.
- **If too little happens per day:** raise the story rate, not the clock.

### Nudge form (Gate 3, P5)
- **If omens win:** three totems (harvest, craft, faith), then one new omen per era. Reserve the omen's tile (#14).
- **If shaping the land wins:** rain, grove, channel, then terraform/decorate in the deep session (game-definition §6).
  This is the bigger build: placed nature must stay part of the sim.
- **If both score well:** omens for the check-in, shaping as the deep-session toy.

### Progression shape (Gate 3, P6)
- **If the box grows wins:** the slice's eras 1–3 end with one annex at Village; needs a single continuous glass shell
  (#15) and one grime mask over it.
- **If the new box on the shelf wins:** the slice ends at Village with a new box arriving; the old box becomes a
  museum. Needs per-box grime (#17) and a shelf view that keeps people readable (#18).
- **If it's a tie:** both stay (the current design: grow inside eras, shelf after the final era). The slice then only
  needs the annex; the shelf comes after the slice.

## 4. Scope
**In:** one box; eras Camp → Hamlet → Village, crossed live (plateaus); the care layer (Gate 1); the Gate 2 return and
history view; the Gate 3 nudge form; notables, pins, person cards; real save and offline catch-up; a first-run
experience (name your box, first wipe, first nudge); sound pass; settings (sound, haptics, reset).
**Out:** Town and later eras, monetization, notifications, widgets, cloud save, accounts, localization, the shelf
second box's own eras (the slice ends as the new box
arrives and the first one becomes a museum), the Test Hub and A/B machinery (the slice is one game, not variants).

## 5. Architecture
### 5.1 From prototypes to a game project
- New code under `Assets/Game/` with its own assemblies (`AquariumCiv.Sim` pure C#, `AquariumCiv.Game` views,
  `AquariumCiv.Sim.Tests`). Prototype folders stay frozen as references and are excluded from the slice build.
- Port, don't reference: copy `SimCore` (+ Story, Nudges for the chosen form), `SimState`, `CareConfig`, `WipeCleaner`,
  `GlassGrime`, `MessView`, `CrowdView`, `NotableView`, `OrbitCamera` into the new assemblies, deleting variant
  switches as they go. Keep the determinism and catch-up tests and add a fingerprint test for the slice world.
- Scenes are still built by editor builders (`Assets/Editor/Agent/`), so they stay cheap to regenerate.

### 5.2 Save and catch-up (the "real" architecture)
What the prototypes do today (`SimClock`): a JSON dump of `SimState` via `JsonUtility`, written in place every 30 s
and on pause; `lastSeenUtc` in both the file and PlayerPrefs (the PlayerPrefs copy wins); no version field; catch-up
steps month by month up to the full absence.
Slice changes:
- **Versioned save**: `{ "v": 1, "lastSeenUtc", "state" }`, with a migration step per version and a test per old
  version (see the save-versioning spike on `vs/prep`).
- **Atomic writes**: write `save.tmp`, then replace; keep `save.bak` (the last good save). A crash mid-write never loses
  the world.
- **One source of truth for time**: `lastSeenUtc` lives in the save file only.
- **Clock sanity**: a negative absence counts as 0 (already); a jump of more than 30 days is capped and logged, so a
  changed phone clock can't fast-forward a box by centuries.
- **Catch-up cost**: month steps are cheap (a week away is about 2,000 steps). Measure on a mid-range phone; if a long
  absence takes over ~200 ms, step years in aggregate after the first 48 h (mess is capped by then anyway) and keep
  month steps for the recent part so the chronicle stays rich.
- **Era plateaus during catch-up**: the world keeps living but waits at a threshold (P6's rule), so catch-up stops
  advancing the era, not the people.

### 5.3 UI
The prototypes are IMGUI, which automation can't click (tooling.md) and which can't do a polished look. The slice moves
to a UI the tester agent can drive (see the UI spike recommendation on `vs/prep`). Gameplay input stays on the Input
System (already tested by touch fixtures).

### 5.4 Telemetry
Keep the JSONL event log and the GitHub feedback flow (with a relay before any public build: feedback.md "Security
trade-off"). Add the week-long metrics: opens per day, D1/D3/D7, time to first clean, era watched live or skipped,
stories recalled (a one-question in-app prompt on day 3).

## 6. Art direction (to decide with you; options)
The look test and prototypes use KayKit Medieval Hexagon in a glass tank with tilt-shift. Final direction options:
- **A · Keep KayKit, polish the glass** (cheapest): one palette pass, better glass (refraction-lite, rim light,
  condensation), a plinth and a shelf backdrop, era-specific ground colour. All CC0.
- **B · KayKit + a custom era layer**: same base, plus era props made on the KayKit atlas (by hand or AI-assisted,
  recoloured), so each era changes the box *in kind* (polish #4).
- **C · Restyle**: a different base kit (Quaternius MegaKit Pro or Synty MINI) for more detail. Costs money and a
  week of re-dressing; only worth it if testers call the look generic.
Recommendation to discuss: **B**, because the era moment (deep-session payoff) needs bigger visual change per era than
KayKit's huts → homes, and B keeps the free, cohesive base.

## 7. Milestones (rough, agent days)
| # | Milestone | Days | Done when |
|---|---|---|---|
| 1 | Project skeleton: `Assets/Game`, ported sim + tests, versioned atomic save | 3 | Sim tests green, save survives a killed app (test) |
| 2 | Box, care, camera, crowd, notables in the new UI | 4 | Tester agent passes a first-session playtest by touch |
| 3 | Gate 2 return + history view | 3 | Recall and history metrics logged |
| 4 | Gate 3 nudge form | 3 | A nudge is answered and credited in diary time |
| 5 | Eras 1–3 live, progression shape at Village | 4 | The Village moment plays live after a 24 h away |
| 6 | First run, settings, sound, art pass (option chosen) | 4 | Tester agent and you sign off the look |
| 7 | Week-long diary build (TestFlight 0.4.0) | 1 + a week | Results note and go / no-go for production |
About 22 agent days before the week-long test.

## 8. Risks
- **"Nothing to do" between events** (P2 feedback #8: "there is nothing else to do so it feels a bit like doing
  nothing"). The slice must make every check-in end with something new to see; that's what the Gate 2/3 features are for.
  Watch opens per day from day 2.
- **Too few testers** for clear gate answers (3–6 people): gate results are directional; the slice week is the real test.
- **IMGUI → real UI** is the biggest engineering jump; do it first (milestone 2), not last.
- **Catch-up after long absences** on old phones: measure early.
