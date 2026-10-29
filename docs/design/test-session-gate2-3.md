# Test session: Gate 2 (history) and Gate 3 (control and progression)

A guide you can follow on your phone, with no help. It covers prototypes P3–P6 in the **Test Hub** build (0.3.0).
Specs: [prototype-plan.md](prototype-plan.md) §5. Build notes: [results/P3.md](results/P3.md),
[P4.md](results/P4.md), [P5.md](results/P5.md), [P6.md](results/P6.md).

---

## Before you start

**1. Decide Gate 1 first** (P2's diary and lab). This build replaces P2's build on your phone and your friends'
phones. Your P2 diary is not lost: the hub opens it straight away on launch and lists it under "Your diaries".

**2. Build it (one command):**
```
gh workflow run ios-testflight.yml --ref overnight/p3-p6 -f notes="0.3.0 Test Hub P3-P6"
```
- The version on this branch is already **0.3.0** (the next minor after P2's 0.2.0, see [testflight.md](../testflight.md)).
- The run takes about 40 minutes. When it's green, the build shows in TestFlight as "0.3.0 (<run number>)".
- Then add its row to the build log in testflight.md (the row is drafted there; fill in the build number).

**3. "What to Test" text** (App Store Connect → TestFlight → the build → Test Details):

> **Aquarium Civ 0.3: Test Hub.** The app now opens on a Test Hub (or straight into a diary you're already running).
> Each test has two versions, **A (teal)** and **B (sand)**. Tap one, play for about 7 minutes, tap "Done for now" and
> answer the short survey, then play the other one. After both, one last question: which did you prefer?
> A strip at the top always says what you're testing and has a "Hub" button.
> In lab tests, "Step away" pretends you left the tank for a while. Tell us anything with the Feedback button.

**4. Friends** only get it once you add the build to their **external** group (TestFlight → External Testing →
"Friends" → add build). Apple may run a short review first. Internal testers ("Team") get it automatically.

**Getting around:** the hub is the home screen. From any test: the **Hub** button on the top strip, or "Test hub" on
the thank-you screen after a survey. A diary opens on launch if "On launch: open the … diary" is set on the hub.
Switch a phone between diary and lab inside a test by **holding the top-left info box for 2 seconds**.

---

## Block 1 · Gate 2 (history): P3 and P4

Lab, about 30 minutes. Play A, then B (friends get the order the hub suggests; each variant takes about 7 minutes).

| ✓ | Open in the hub | What to do (≈ 7 min) | Look for | Survey Q4 | Compared with |
|---|---|---|---|---|---|
| ☐ | **P3 · A Chronicle cards** | Tap "Step away +8 h". Read every card, tap "Show me" on one or two. Tap two named people in the village (the ones with a teal dot), pin one. "Step away +24 h", read the cards again. Wipe the glass. Done for now. | Do the cards make you want to come back? Do you remember anyone afterwards? Do the pinned person's stories come first? | "I care about specific people in my tank" | B: plain return |
| ☐ | **P3 · B Plain return** | Same steps: +8 h, look around, tap two people, pin one, +24 h, wipe, Done. | With only a stats line, do you still find out what happened? Do you go looking? | same | A: chronicle cards |
| ☐ | Pair question | Pick A or B and say why. | | | |
| ☐ | **P4 · A Time-lapse** | Tap "History". Drag the timeline back to year 0, then forward. Tap Play. Find: the year the first windmill went up, the oldest person who ever lived. Take a Snapshot of something you'd send a friend. Close. Done for now. | Is replaying the village fun to watch? Do the story pins help? | "I'd show this to a friend" | B: illustrated book |
| ☐ | **P4 · B Illustrated book** | Tap "History". Swipe through every page: cover, chapters, legends, family trees. Same three finds. Snapshot. Close. Done. | Would you read this? Is it something to show someone? | same | A: time-lapse |
| ☐ | Pair question | Pick A or B and say why. | | | |

### P3 diary (4 days, real time)
- In the hub: P3 card → **Start the 4-day diary** (a second tap confirms if another diary is running).
- Use the app as you like for 4 days; no reminders. The version alternates by day (A one day, B the next).
- "Done for now" asks the survey once a day. Note anyone who dies "too soon" (notables live about 5 real days).
- Back to lab later: the hub; the diary keeps its own world, so lab tests never touch it.

### P4 diary (optional, 3 days)
- P4 card → **Start the 3-day diary**. Open History whenever you feel like it (that's the measure: unprompted opens).

---

## Block 2 · Gate 3 (control and progression): P5 and P6

Lab, about 30 minutes. A, then B.

| ✓ | Open in the hub | What to do (≈ 7 min) | Look for | Survey Q4 | Compared with |
|---|---|---|---|---|---|
| ☐ | **P5 · A Omens** | Time runs fast here (a minute is a year). Tap "Nudge", pick a totem or the shrine, tap a spot in the village. Watch the people walk over, and within a minute or two build something beside it ("After the Keeper raised…"). Place another kind. "Step away +8 h" and read the welcome line. Done. | Did you feel you shaped what happened? Too much control, too little? | "I felt I shaped what happened" + "too much or too little control?" | B: shape the land |
| ☐ | **P5 · B Shape the land** | Same: Nudge → rain on a spot, plant a grove, or dig a channel. Watch the answer. Another one. +8 h. Done. | Same | same | A: omens |
| ☐ | Pair question | | | | |
| ☐ | **P6 · A The box grows** | A young camp at a minute per year. Wait for "the people are ready to become a hamlet" (under a minute) and tap **Watch**. Then Step away +24 h: the Village moment waited for you; **Watch**: the box fills and an annex rises. Watch the village spread into it for a minute. Done. | Was the live era moment worth waiting for? Do you want to see what comes next? | "I want to see what comes next" | B: new box on the shelf |
| ☐ | **P6 · B New box on the shelf** | Same steps. At Village a new box arrives on a shelf; the old one stays as a museum. Visit the old box and the new one with the buttons. Done. | Does a fresh box excite you, or do you miss the old one? | same | A: the box grows |
| ☐ | Pair question | | | | |

### P5 diary (3 days, real time)
- P5 card → **Start the 3-day diary**. Nudge whenever you open it, or don't: an answer takes 1–3 real hours.
- The version alternates by day. One survey a day.

---

## Gate 2 decision sheet (history pillar + time compression)

| Question (prototype-plan.md) | Fun if | Where it comes from | Result |
|---|---|---|---|
| P3: does the curated chronicle make the return the highlight? | In A, ≥ ⅔ of testers recall a specific named person or event, and A is preferred by most | Survey Q3 (recall) per variant in the GitHub survey issues; `pair_pref` issues ("A/B preference from …"); logs `chronicle_view` (cards read, dwell, fly-to) | |
| P3: do people care about specific people? | Q4 median ≥ 4 in A | Survey Q4 issues | |
| P3: notables at half speed, too short a life? | Few "died too soon" remarks in the diary | Q5 text and Feedback issues during the 4-day diary | |
| P4: is history a reason to linger and share? | ≥ half of testers open history unprompted in the diary, and the winning view scores Q4 ≥ 4 | `history open` log lines with `mode` = Diary (and `since_hint_s`), dwell, `scrub_years` / `page_turns`, `snapshot`; Q4 issues; `pair_pref` | |
| Time compression: is 1 real hour = 1 game year right? | A diary day reads as a good chunk of story without losing people too fast | Diary returns (`return` log lines: years, stories), recall answers | |

- Survey answers and pair preferences arrive as GitHub issues labelled `feedback` (via `FeedbackSender`; see
  [feedback.md](../feedback.md)), tagged `[P3·A]`, `[P3·B]`, `[P3] A/B preference`, and so on.
- **Decision (for docs/decisions.md):** return presentation: ______ · history view: ______ · time compression: ______

## Gate 3 decision sheet (control + progression)

| Question (prototype-plan.md) | Fun if | Where it comes from | Result |
|---|---|---|---|
| P5: do nudges feel like authorship? | Q4 ≥ 4, and a nudge is used in ≥ 60 % of check-ins | Q4 + Q6 ("too much / too little control") issues; `nudge place` and `checkin` log lines (nudges per check-in), `nudge_effect` (time to the answer), look-time on `nudge` | |
| P5: omens or environment shaping? | Preference + Q4 | `pair_pref` issues | |
| P6: is a live era transition worth waiting for? | Q1 ≥ 4 for most testers | Q1 issues; `era watch` (seconds waited) and `era done` (seconds watched) | |
| P6: box grows or new box on the shelf? | One form wins preference clearly (≥ ⅔); a tie keeps both | `pair_pref` issues; in B, `old_box_s` (time spent revisiting the old box) | |

- **Decision (for docs/decisions.md):** nudge form: ______ · progression shape: ______ · then write the vertical-slice plan.
