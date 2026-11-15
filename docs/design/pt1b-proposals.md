# Playtest 1, batch B: four proposals for the user to decide

Issues #708, #709, #710 (with #658) and #687. One page each: what it is, cost, a ★ recommendation, an alternative, and the one
question. Nothing here is built. Sizes: S under 1 agent-day, M 1-3, L 3-8. Counts and measurements were taken headless on the
real sim (see "How the numbers were taken" under #710 and #687).

---

## #710 + #658: too many animals, and a deeper box

**What it is.** Mid-camp the box feels crowded and more kinds are coming. You already chose both fixes for #658: better framing
first, then a deeper box (HZ 2.2 to 3.0, 6 x 6 land, old boxes keep their size). This adds the missing piece: caps per kind.

**What a normal player's box holds (sim count, and what is drawn).** Headless run of the pacing lab's "Active" player (six visits
a day, places every jar the moment it opens, six 3 s strokes per visit), seeds 7, 21, 42, 3, 5, one run to year 40. Per kind, the
range over the five seeds; "drawn" is what the view caps at today.

| Year | Rabbits (drawn) | Deer (drawn) | Ducks | Fish (drawn, cap 8) | Wolves | Dogs | Frogs (view only) | Birds (sim, not drawn) |
|---|---|---|---|---|---|---|---|---|
| 5 | 11-22 (12) | 0 | 0 | 0 | 0 | 0 | 2-6 | 0-1 |
| 10 | 44-47 (12) | 0 | 0 | 0 | 0 | 0 | 2-6 | 2-4 |
| 15 | 56-59 (12) | 0 | 2-8 | 0-15 (0-8) | 0 | 0 | 2-6 | 4-9 |
| 20 | 61-65 (12) | 0 | 8 | 0-36 (0-8) | 0 | 0 | 2-6 | 13-26 |
| 25 | 63-66 (12) | 4-8 | 8 | 39-41 (8) | 0 | 0 | 2-6 | 28-56 |
| 30 | 64-66 (12) | 8 | 8 | 43-44 (8) | 0 | 0 | 2-6 | 70-136 |

So in a year-20 camp the player can see up to 12 rabbits, 8 ducks, up to 8 fish (none drawn swimming yet, #429 is open) and 2-6
frogs: about 22-26 animals, plus 17 people in the tester's box. Deer come around year 25 and make it about 34. Wolves did not
appear before year 40 here (4 in one seed); no dog was adopted in any run (dogs need the pup story). The sim's rabbit count is
five times what is drawn, so the "large numbers" are the drawn caps, not the sim.

**Cost.**
- Caps (S, 0.5 d): the shown caps are constants (`RabbitHerd.DefaultCap` 12, `DeerHerd.DefaultCap` 8, `SimCore.DucksShownCap` 8,
  `FishShownCap` 8, `PondLife.MaxFrogs` 6). Lowering them touches ~5 constants and the tests that pin them (PlacedLifeTests).
  Ducks and deer also have sim caps (`duckCap`, `deerMax`) that feed hunting and eggs: lower those only with a pacing re-run.
- Framing (#658 option 2, S-M, 1 d): default camera and home framing only (`BoxCamera`, `CampFrame`); no sim change.
- Deeper box (L, 5-8 d, its own milestone): `Terrain.HZ`, `SiteMap.NZ/N` and `EcoLand.N` are compile-time constants used in about
  123 places across 108 files, 57 of them tests. "Old boxes keep their size" means they become per-box values set at load (arrays
  sized for the larger grid, the depth saved), then glass, shelf, camera reach, presets, layout and pacing (36 percent more land
  to green) are re-checked. Risk: the pacing lab gates (village at year 25 +- 3) move.

**★ Recommendation.** A visible-animal budget, not one cap per kind: about 24 drawn animals on today's box, 34 on the 6 x 6 box
(same density, about 0.9 per u2). Today's kinds: rabbits 8, deer 6, ducks 6, fish 5 (when drawn), frogs 4, wolves 4 plus pups.
Sum 33, so a new kind must take its share from these, and the table lives in one place. Do the framing now, the caps now, the
deeper box as the milestone you named.

**Alternative.** Caps only, and decide the deeper box later after seeing the new framing on a phone.

**Question.** Is a budget of about 24 drawn animals now (34 on the deeper box) the right feel, or higher?

---

## #687: water rework (the payoff of a misting)

**What it is.** The tester saw the pond grow but did not link it to their misting, because it grows too little and too late.
Your read: the fix is a clear payoff soon after the stroke more than explaining the need.

**What it does today (measured).**
- The wetness is already instant: the stroked area goes to full wet at the stroke and is half gone after about 6 game months
  (30 real minutes), gone by about 18 months. Wet is not the slow part.
- The pond is. One stroke sends only 0.225 units down to the water table (`mistDeep` 0.15 x amount 1.5). The sim takes that at
  the next season step (a quarter real hour), then adds 0.0003 per unit, shared among the pond's cells. In paired runs, six
  extra strokes at year 10 moved the first pond by 3-6 game months (15-30 real minutes) and added at most 5 cells two years on; 18 extra
  strokes changed nothing more.
- The pond appears at year 10-11 in lumps of 11-20 cells, then adds 1-3 cells a year (year 15: 86-93; year 20: 107-111; year 30:
  134-135). A player sees at most one cell change per visit, and never right after the stroke. Without any mist: no pond.

**Cost.** M (2-3 d). `EcoModel.Mist`/`Step` (take the soak monthly, and show the level move), `BoxView.Eco` water mesh (ease the
level over a few seconds, rebuilt only while easing), optional glass-voice line (#564), PacingLabTests re-run. Risk: the pacing
lab pins the pond20 gate and the village at year 25 +- 3; keep the total water the same and only move when it lands. Test impact:
EcoTests pond tests, PacingLab.

**★ Recommendation.** Make the pond answer the stroke: the soak lands at the next game month (5 real minutes) instead of the
next season, and the water edge visibly rises over a few seconds after a stroke that fed it (a ripple of the level, one cell at
most). Same total water, so the pacing holds. Dry-ground cues (paling, wilting) come as a later ticket.

**Alternative.** Keep the sim, add only the view cue (the edge rises a little at every stroke, a glass-voice line says "the pond
drank it"). Cheaper (S) but not true to what the sim does.

**Question.** Is "the pond rises a little within minutes of a misting, same total water" enough, or should a long dry spell
also cost something (slower growth only, or a wilting look)?

---

## #709: the bird from the bushes

**What it is.** At the first bush a grey bird flies in, sits and leaves (`BushView` sign), plus one chronicle line. It is never
seen again. The sim's bird count (13-26 at year 20, hundreds later) only spreads tree seeds and is not drawn.

**Cost.**
- Drop it (XS, 0.25 d): remove the sign and line; the seed spread is unaffected.
- Visits only (S, 1 d): the same bird returns one spring day a year at the thicket; no new state beyond a day flag.
- Nest and eggs (M, 2-3 d): a nest prop in the thicket (a view prop, not an animal, so it does not touch the #710 budget), a
  spring clutch flag in the sim, a gathering and a looking use in the planner (copy the duck-egg path in `HuntsAndEggs`), a
  cheat, tests. The parent bird is the existing bird doll, perched in spring. Hunting the bird needs a bird count to hunt: not
  proposed.

**Weighed.** #691 (people use everything) says a thing shown must be used, which rules out "shown once and forgotten". The nest
adds one use for work (eggs) and one for life (kids peer into the nest) and costs no animal slots.

**★ Recommendation.** Nest and eggs, no hunting: one nest in the placed thicket, one parent bird in spring, kids look, a
gatherer takes an egg now and then (never the last).

**Alternative.** Visits only, if eggs feel like a second source of what ducks already give.

**Question.** Nest with eggs gathered (no hunting), visits only, or drop the bird?

---

## #708: a deeper pond seen through the glass, with fish

**What it is.** From the side you see the soil layers; the pond could show its water body the same way, with fish swimming in it.

**Cost.** Already there: when the pond reaches the box edge a water wall stands at the pane (`WaterWall`, #269), so the section
exists at today's depth. Missing: fish drawn at all (`EcoFishShown` has no view; #429 fish shadows is open, no swimming fish
exists). Deeper means a lower basin in the presets, the table thresholds (`capillary*`, `tableStartBelow`), the cut-face art and
the ice. Fish: M (2 d) as 5-8 pooled low-poly fish like the frogs and ducks in `PondLife`, one mesh. Deeper water: M-L (3-5 d).
Phone: the tester logs show 25-30 fps already, so no new transparent layers; a second water layer or
alpha over the glass would add overdraw. Use opaque vertex-colour water and fish shapes inside the existing wall.

**★ Recommendation.** Do the fish first, inside today's water wall: fish seen from the side where the pond meets a pane (and as
shadows from above, #429). Take the deeper basin after the deeper box (#658), when the presets are re-cut anyway. No new
transparent pass.

**Alternative.** Deeper pond now with no fish; or never (keep the section as is).

**Question.** Fish in today's side view now, deeper pond with the deeper box: yes, or later or no?

---

## How the numbers were taken

Throwaway headless tests in `scratchpad/probe` (not committed), compiled against the repo's real Sim and Life sources with the
pacing lab (`Tools/SimTests/PacingLabTests.cs`, #569). Player: "Active" (visits at 08:00, 08:30, 10:00, 12:30, 16:00, 19:00,
22:30), `EcoTests.PlaceReadyJars` after each visit start (every jar placed at once), six 3 s strokes per visit, default
`EcoTuning`. Seeds 7, 21, 42, 3, 5; 42 game years; counts read each January. #687: a fresh run to year 10, then 0, 6 and 18 extra
strokes round the camp, pond cells read every 3 months for 2 years; wet read for 18 months after one stroke. Deer are the
stags, does and fawns; wolves include pups and yearlings; frogs are the view's (2 by day, 5-6 at night, never more than 6).
Floats differ between .NET and Mono, so read the counts as ranges.

---

## Ready-to-post comments (paraphrased, public-safe)

**#710** (and #658): Counted a normal player's box on the real sim (six visits a day, every jar placed at once, five seeds, to year
40). By year 20 the box can show about 12 rabbits, 8 ducks, up to 8 fish and 2-6 frogs, around 22-26 animals; deer add up to 8
from year 25. The sim's rabbit count is far higher than what is drawn. Proposal: a budget of about 24 drawn animals on today's box
and 34 on a 6 x 6 box, with per-kind caps that leave room for new kinds. Better framing first, the deeper box as its own
milestone (old boxes keep their size; the depth constants are used in ~120 places). Needs the user's call on the target number.

**#687**: Measured today: the wetness is instant and fades over about 30 real minutes, so that part works. The pond is the
problem: a stroke feeds the water table only at the next season step and shifts the pond by a few cells at most, so the link is
invisible. Proposal: apply the soak monthly and show the water edge rising within minutes of a misting, keeping the same total
water so the pacing holds. Dry-ground cues later. Needs the user's call on whether a dry spell should also cost something.

**#709**: Today the bird is a one-time visit with no follow-through. Proposal: a nest in the placed thicket with one parent bird
in spring, kids looking in, a gatherer taking an egg now and then, no hunting; it adds no animals to the box's count. Cheaper
options: a yearly visit only, or dropping it. Needs the user's call.

**#708**: The side-on water wall already shows the pond at the glass; what is missing is fish drawn in it (#429). Proposal: fish
first, inside the existing wall with no new transparent layer (the phones in the playtest already run at 25-30 fps); the deeper
basin together with the deeper box. Needs the user's call: now, later, or no.
