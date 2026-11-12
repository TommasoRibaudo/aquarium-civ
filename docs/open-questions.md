# Open questions for the user (kept current)

Everything waiting on a decision by the user, in one place. Answered items move to [decisions.md](decisions.md) and are
deleted here. Recommended options come first and are marked ★. On GitHub: issues labelled `needs:user`.

_Last updated 2026-11-11._

## Feedback 5 (build 25 notes), 2026-11-11
Issues #569-#578 (notes #544-#567). Run log: `overnight/FEEDBACK-5-STATUS.md`. The Editor walkthrough (menu **Aquarium Civ/Open
questions sign-off**) stages every look item below with the cheats and walks the decisions and calls.

### Sign-offs (look)
- **#569 the misted patch greens**: the mister's water now makes trees grow and seed faster where it falls. Mist one half of the box for three years and see the misted half greener and denser than the other (the lab: about 2x the young and grown trees). The before/after pacing table is the first stop. Target: the village at year 25 for an active player.
- **#570 locked seeds and the village line**: in Kit, Life, the seeds that are not ready show greyed with what each needs; a tap on one says its need; a line says what the camp (or village) waits for.
- **#571 population on the title**: the title line reads "Year N · Season · P people".
- **#572 the chief's office**: tap the title, the Offices sheet opens; tap the chief's name and the chief's card opens (every office holder's name does the same).
- **#573 the chopping spot and the wood**: nobody chops beside the Keeper's tree (a grown wild tree away from it is the woods spot, two choppers a tree); chopping shows chips, logs fall, and a chopper walks a log to the stash.
- **#574 a rabbit hunt and the egg gather**: a hunter is made when a camp has rabbits to spare, so a hunt is seen; in spring someone walks to the reeds with a basket and takes an egg to the pile.
- **#575 rename on the card**: on a person's card, About tab, rename them (first name only). The new name shows on the card, the label and in later chronicle lines; old lines keep the old name.
- **#576 the four seasons**: spring, summer, autumn and winter each read at a glance on the grass, the leaves and the snow, with the season word on the title. One stop per season.
- **#577 the glass and the lid**: no outline from hidden panes over dry land or the lit camp at night; a glass lid on top that you look through at the default view and that fades as the camera looks down. Four stops: dry land close, lit camp at night, lid at the default view, lid top-down.
- **#578 the travellers' wood and their joining**: with no woods the question names the meadow (or the pond); their cutter carries logs to their pile, and when the village is made they pack up, walk in and join, the camp cleared. Two live stops with Set up again.

### Decisions
- **#569 the habit player lands later**: a player who visits every 4.5 hours (the habit of the player who tested) reaches the village about 5.8 years after the active player (lab: 29-33 against 24-27 years), because the visit loop allows one placement per visit and the chain has seven steps. ★ Leave it (the one-placement-per-visit chain of #434). Alternative: let one visit place two jars in a row, which would put that player about 3 years behind but changes #434.
- **The season word's place on the title line (#576)**: it sits between the year and the people: "Year 3 · Autumn · 5 people". ★ Keep it in the title line. Alternative: show the seasons only through the colours, with no word.
- **The lid's look (#577)**: a faint haze at the default view, fading out from a camera pitch of about 68 degrees (top-down hides it) and when you zoom in. ★ Keep it. Alternative: a stronger lid, or no lid.

- **A new box starts in winter (#576)**: the sim's first month is winter, so after the first run a new box shows "Year 0 · Winter" with light snow on bare sand (the tester found it a cold first impression). The first run itself now plays in a neutral summer look. ★ Start new boxes in spring (the first month moves; pacing re-checked). Alternative: keep winter, the land's honest clock.

### Tester polish (2026-11-11, no [real] findings)
- #573 close-up chopping: the chopper hides behind the tree's crown; chips and the log on the ground are a few pixels.
- #576 winter at the default zoom: people a few pixels, tents pale against the snow.
- #571 the title pill resizes with its text, so the sun button shifts sideways as the line changes.
- #570 at year 0 the Kit already says "The camp waits for: a meadow · rabbits in the meadow": maybe too early for a new player.
- #575 after a rename the card is scrolled so "Call them something else" sits under the tab row; the rename field is half scrolled off at the top while Keep shows.
- #570 a tapped locked seed looks darker (selected) and could read as available.

### Calls I made without you
- #570: the waits-for line sits on the Kit sheet (not History: History opens the time-lapse); a 'camp waits for' stage before the camp; the chooser now opens whenever a jar is shut (one ready jar = two taps).
- #572: 'the office of chief' is the Offices sheet row (the title chip), not a building; every office holder's name opens their card.
- #571: the population is on the title line ('Year N · P people'), not in History (History needs a tap).
- #577: the lid fades out from a pitch of about 68 degrees (top-down hides it).
- #574: eggs only in spring (the model's nests); the egg settle-once marker isn't saved.
- #575: first name only; two living people can't share a first name (refused with a line).
- #574: a minimal egg gather (a gatherer at the reeds with a basket, TakeEgg()), built so #428 can grow it later.
- #569: the wild seed from outside is halved (0.08 to 0.04) so one lucky early wild tree no longer makes a box 8 years early; the village gate stays at 8 trees.
- #573: the CampRhythm test fixture now plants 5 wild trees (re-seeded: a real wood has several, and the one-tree fixture put every chopper in step at one tree).
- #574: the HuntThrow test is bound only where the clock rate is the same at both ends of the walk (a test bound, not a re-seed).
- #576: winter snow is light and patchy, on flat natural ground only (the first version was a full white blanket); spring is yellower and summer deeper so the two differ.
- #569: the view now draws a young tree on about 80 % of young-tree cells and a seedling on about 75 % (was 45 % / 55 %): the forest the sim grew was half invisible at phone size. Every box looks a little more wooded.
- #573: when every grown tree already has two choppers, the extra woodcutters do chores at home (sending them to the tribe's work put them all on one berry spot).
- #578: the first load says "The notch in the axe handle works. Come and see the pile!"; on arrival "Here we are, then. We brought our own logs." They join at the first calm moment while you watch (never while you're away); a group that built the Keeper's tower doesn't join.

## Waiting on you (after the 2026-11-10 sign-off)
Everything else was answered in the Editor walkthrough on 2026-11-10 and moved to [decisions.md](decisions.md)
(verdicts: `overnight/open-questions-signoff.md`).

- **Being built now**: #543 bigger fresh fish on the rack; #558 the seed in the life jar; #566 one feather pole at the camp's edge where people go to pray.

