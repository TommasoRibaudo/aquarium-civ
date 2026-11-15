# Open questions for the user (kept current)

Everything waiting on a decision by the user, in one place. Answered items move to [decisions.md](decisions.md) and are
deleted here. Recommended options come first and are marked ★. On GitHub: issues labelled `needs:user`.

_Last updated 2026-11-15._

## Playtest 1, batch 2 (2026-11-14 notes)
> Built on `p9/playtest1-b` (PR to dev at the end of the run). The animation sign-offs (#700's take-down beat, #705's
> washing) are on `p9/playtest1-b-signoff` only and go to dev after your yes. Status and round logs:
> `overnight/PLAYTEST1-B-STATUS.md`. Fixed without a stop of their own (closed once the batched pass is green): #696 #697
> nobody gets up or turns back when the day replans, #698 a kid walks into its tent and never vanishes, #699 deer keep off
> the fire and tents, #702 the Feedback pill takes taps over every panel, #689 a village house's card says the year it went up.

### Sign-offs (look)
- #707 the HUD top: Settings, Feedback and Photo as bare icons in the title's ink, side by side. Judge them over sand, night, snow and the pond.
- #704 the fire on dry ground and snow: no green ring at its base.
- #701 the pond's rim: the water ends inside the dug dirt, also where it reaches the glass, and the ice shares that edge.
- #676 the stew by kind: rabbit, fish, berries and thin stew each look like what's in the pot, by day and at dusk.
- #706 the touch choices: each option in the box is marked and labelled, and the question names them.
- #703 the duck jar in winter: shut and greyed while the pond is ice ("The pond is ice. Ducks will come when it thaws."), open at the thaw.
- #669 History before the first house: scrub back and nothing placed or built later stands there (life, racks, the woodpile, props); at the timeline's end today's box is back.
- #700 a path over the fish or hide rack: the rack stays by day and moves off the path at night.
- #690 the deer jar: shut with "Deer need a thicker wood (n/12)" among scattered trees, open once a wood stands.
- #671 the faded glass on wet ground: zoomed in, the near glass doesn't cut the wet or snow look.

### Animation sign-offs
- #705 hanging out the washing: first the spec (B28 in `design/animation-specs.md`), then the AnimLab clip ("Set up again" to watch twice).
- #700 the rack's take-down and set-up beat: first the spec (B27), then the AnimLab clip.

### Decisions needed
- #710 with #658, too many animals for the box. ★ A visible-animal budget in one table (about 24 drawn on today's box: rabbits 8, deer 6, ducks 6, fish 5, frogs 4, wolves 4 plus pups, a new kind takes its share); framing and caps now, the deeper box later as its own milestone. Alternative: Caps only, and decide the deeper box after seeing the new framing on a phone.
- #687 the water rework. ★ The pond answers the stroke: the soak lands at the next game month instead of the next season, and the edge visibly rises a little after a stroke that fed it; same total water. Alternative: Keep the sim, add only the view cue (the edge rises a little at every stroke, a glass-voice line).
- #709 the bird from the bushes. ★ Nest and eggs, no hunting: one nest in the placed thicket, one parent in spring, kids look, a gatherer takes an egg now and then, never the last. Alternative: Visits only.
- #708 a deeper pond seen through the glass. ★ Fish first, inside today's water wall (seen from the side where the pond meets a pane, shadows from above); the deeper basin after the deeper box. Alternative: The deeper pond now with no fish, or never.
- #702 against #673, the Feedback pill in photo mode: #702 keeps it tappable everywhere, so it stays alone at the top right of the clean view; #673 said photo mode leaves no button. ★ Keep it while playtests run (testers report what they see in photo mode), hide it in release builds. Alternative: Hide it in photo mode now.
- #705 the washing's stay: one view hour (about 16 s real) after she leaves, then a housemate takes it in or it fades; no hanging in rain, snow or winter. ★ As specced. Alternative: Longer, up to the evening.

### Calls I made
- #703: the duck jar is shut while the pond is frozen, so a player who only visits in winter meets the ducks (and the village gate behind them) later. A lab Keeper in `EcoTests` places a thawed duck jar outside its winter-only visit.
- #700: the rack moves only at night, or in a quiet moment when nothing hangs on it.
- #669: live animals and berries are hidden in a replay (no record says where they stood each year); the fish rack, woodpile and hide frame show from the month they were first seen live, so on a box saved before tonight they appear in History only from now on.
- #689: the camp's tent rebuilt as the village house reads as built at the village; an old save's month-0 building takes its lot's month.
- #690: a tree counts as wood with 4 grown trees within 3 cells; the jar opens at 12 such trees (estimates). In the pacing lab the deer come about 8 game years later (year 23-29), the wolves in the same year as before.
- #701: the small and grown pond shots couldn't be reached on the test seed (it floods to the full pond at once); the rule is checked on the water mesh itself.
- #698: the cause found and fixed is a kid bound for its tent stopping short when a lot blocked its slot; the tester's exact moment wasn't reproduced.

### Polish noted, not fixed
- The glass edge line crosses the year line and the gear when the camera is close (no disc behind the icons now, #707).
- Fish and thin stew are close at mid zoom; only the white flecks tell them apart (#676).
- On the naming step the touch-hint ring covers the word "name".

Older sections (answered in the 2026-11-10, -11 and -14 sign-offs, or still waiting outside this batch) are in
[overnight/open-questions-older.md](overnight/open-questions-older.md), so the walkthrough shows only what is new.
