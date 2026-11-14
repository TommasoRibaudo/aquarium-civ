# Open questions for the user (kept current)

Everything waiting on a decision by the user, in one place. Answered items move to [decisions.md](decisions.md) and are
deleted here. Recommended options come first and are marked ★. On GitHub: issues labelled `needs:user`.

_Last updated 2026-11-14._

## Playtest 1 (first outside player, 2026-11-13/14)
Everything built from the first outside playtest, and what tonight's run adds. Look stops are staged in the box; judge each one at
phone size. Merged to dev: #646 (#643-#645), #678 (quick wins). Tonight: the polish batch and the new start (its own PR, not merged).

### Sign-offs on the phone
- #643 placing the wolves: the chooser always opens, the wolf jar waits until the den has room, the camera brings the den ground into view and the hint names what's in hand.
- #644 fields follow the seasons: green in spring, wheat in summer, stubble in autumn, bare in winter. Step through the seasons.
- #645 name fields: Save closes the iPhone keyboard and no second input bar shows. Phone only (the Editor has no iPhone keyboard): judge on the next build.
- #647 the first run: the Photo button stays hidden during the first run, and photo mode can't hide the UI while the steps go on.
- #648 quieter sound: Effects and Ambience start at 0.6 (Music unchanged). Listen at the default view.
- #649 the Settings icon: a gear, not a sun.
- #650 the Feedback button: full ink, like its neighbours.
- #651 no kindling without wood: nobody chops kindling while no wild wood stands, and in winter farmers hoe instead.
- #652 the gatherers' place: "the dry hollow", or "the water's edge" once there's a pond, never a stream that isn't there.
- #654 Settings sliders: drag Music, Effects and Ambience sideways, also after scrolling the sheet. The list must not move while you drag.
- #657 grass tufts: each tuft's base sits in the ground (no floating) at a close zoom.
- #659 the spoken line at the top: the caption plate stays below the title line (year, season, people) on a tall phone.
- #669 History replay: no berries, rabbits, deer or wolves from today in the old years.
- #670 History on a young box: the whole span plays in about 12 s; slow speeds read like "9 d/s" and Close stays on screen.
- #672 the evening talk: when it ends, the camera goes back to where you had it; it never starts while you're zoomed in watching.
- #673 photo mode: it starts where the camera is (no jump to the pinned person), and its pill fades out fully.
- #674 the person card: its first view shows a row of family and friend chips again.
- #680 the hunter tries again: after a miss, one more stalk and throw before walking home.
- #675 the seed in the life jar: a seed icon and a word, and the hint says one seed grows into the box's one tree.
- #676 the stew pot: a darker pot, a stew that looks like stew, steam and a few bubbles.
- #665 rabbit tracks: fresh prints keep appearing while rabbits live.
- #671 faded glass up close: zoomed in, the faded near glass no longer cuts the wet or snow look of the ground.
- #656 the new start, arrival: the people walk in carrying a basket of food and a bundle of sticks and set them down where they settle; the HUD numbers show only after that. (Branch p9/new-start, not merged.)
- #677 the new start, fire and wood: a small grass-and-twig fire before any wood; the first logs from the first grown wild tree are carried home and the fire becomes a log fire. (Branch p9/new-start, not merged.)
- #658 framing the action: two or three default-camera framings that fill a tall screen with the camp. Pick one (the Framing A, B and C buttons, or Set up again, switch between them).

### Decisions needed
- #677 the fire before the first wild wood: the first wild tree grows at about year 15-23 for an active player, so a fire only from wild wood leaves the camp 12-20 hours with no fire. ★ A grass-and-twig fire early, a log fire with the first wild wood. Alternative: No fire at all until the first wild wood.
- #656 the carried food: the basket holds food for a while, then runs down until berries or rabbits. ★ Keep 12 food in the basket (about a year). Alternative: A smaller basket that empties within the first year.
- #658 which framing becomes the default: from the framing stop. ★ The framing you liked best in the look stop (write its letter in the note). Alternative: Keep today's default view.
- #679 a notification when the Keeper's tree grows: today only a ready era notifies (decided 2026-10-29). ★ Notify for the next tree stage or era, whichever comes first, one at a time. Alternative: Keep the rule, no tree notification.
- #680 the hunter's retry: one more throw after a miss. ★ Keep one retry. Alternative: No retry, the hunter walks home after a miss.

### Calls I made: the new start (#656, #677)
- The new start applies to living-land boxes only; old boxes keep their stock.
- The basket holds 12 food and the people eat about 0.2 each a month from it, so it lasts about a year; at 0 they're hungry (slower, looking up) and nobody dies.
- Before the first bush there is no fire (the wanderers sit together); the grass-and-twig fire starts with the first bush and is fed with armfuls of dry grass and twigs from the meadow edge.
- Wild wood: a grown wild tree in the woods gives about 0.6 wood a month (chopped); a grown wild tree near the Keeper's tree gives about 0.3 as fallen branches gathered by hand, never chopped. The Keeper's own tree gives nothing. First logs then come at year 15-23 for an active player, before the village (24-27).
- With no work on bare land, one founder is the toolmaker and the rest hold no job; the gatherer opens with the first berries or rabbits. Their day: chores and water, looking over the land from the hill, grass and twigs for the fire, sitting at the camp.
- The lean-winter line in a camp talks of baskets, not granaries.

### Polish noted, not fixed
- #665 rabbit trails on winter ground: the brown dot pairs read more like droppings than prints, and at the default zoom they can't be seen (tester, 2026-11-14).
- #674 the person card at 750 wide, card pulled tall: big gaps (~70 px) between the chip rows.
- #653 the doing tag ("Marta · cheering") can crowd the relationship labels at the default zoom: you chose to keep it as is (2026-11-14).

## Older items still labelled needs:user
Issues from before the playtest that still carry `needs:user`: close them or keep them open.

### Signed off, still open

- #249 Kit button and tool rail: signed off 2026-11-10 in the Editor walkthrough, but the first outside playtest found the kit incoherent, and the redesign is #668. ★ Close it into #668, the kit redesign. Alternative: Keep it open until #668 is built.
- #279 Pond's first water moment: signed off 2026-11-10 (walkthrough stop 37), only a phone look remained. ★ Close it, signed off 2026-11-10. Alternative: Keep it open for a phone check.
- #516 Placing life ghost and glow: signed off 2026-11-10 (stop 17); the phone look is the only thing left. ★ Close it, signed off 2026-11-10. Alternative: Keep it open for a phone check.
- #518 Seed places like the life jar: the two-tap flow was built, but on 2026-11-10 you said the seed should be part of the life jar. ★ Close it and fold the seed jar into #668. Alternative: Keep it open and build the seed in the jar now.
- #520 Barren wind repeat: signed off 2026-11-10 (stop 19), a listen on the phone is all that remains. ★ Close it, signed off 2026-11-10. Alternative: Keep it open for a phone listen.
- #524 Wet ground at the glass: signed off 2026-11-10 (stop 20), only a phone look remained. ★ Close it, signed off 2026-11-10. Alternative: Keep it open for a phone look.
- #525 Keeper's tree night glow: signed off 2026-11-10 (stop 21), only a phone look remained. ★ Close it, signed off 2026-11-10. Alternative: Keep it open for a phone look.
- #526 Poles by the tents: signed off 2026-11-10, and the feather pole was redone in #566 after your call. ★ Close it, signed off 2026-11-10 and carried on in #566. Alternative: Keep it open for a phone look.
- #538 Free camera pan: signed off 2026-11-10 (stop 1), only the phone feel check remains. ★ Close it, signed off 2026-11-10. Alternative: Keep it open for a phone check.
- #543 Fish basket, rack and food pile: signed off 2026-11-10 except the fresh fish on the rack, which you asked to make bigger. ★ Keep it open until the bigger rack fish is seen. Alternative: Close it and track the fish size separately.
- #587 One finger pans, two fingers turn: your swap call is in decisions (2026-11-11), built in PR #610 with the pinch dead zone, and only the phone feel check remains. ★ Keep it open for the phone check. Alternative: Close it as decided and built.

### Decisions needed

- #99 Slice playthrough and phone check: the tester pass is done, and the phone numbers are still missing because you wanted one final build after the UI rework. ★ Keep it open until the phone build. Alternative: Close it and fold it into the playtest work.
- #102 Week results and go or no-go: the note is not written yet, and the first sessions were logged as comments (28 minutes, a nice catch-up, a short return). ★ Keep it open for your go or no-go. Alternative: Close it as replaced by the playtests.
- #103 UI rework epic: structure, directions and the Keeper's Glass design system were signed off, and the screen retrofits followed. ★ Close it, the rework is done. Alternative: Keep it open as the umbrella.
- #175 Lighting variants: Soft Blue Dusk is the default on main since 2026-11-02, left open for your compare on the phone. ★ Close it, Soft Blue Dusk stays the look. Alternative: Keep it open for a phone compare.
- #178 Frosted sheet phone test: the frost modes and frame readout are in the build behind the debug panel, and no phone numbers exist yet. ★ Keep it open for the phone numbers. Alternative: Close it and use flat tinted sheets.
- #228 Ecology Lab go, reshape or park: the lab answered yes and P8 was built from it, merged 2026-11-03 (PR #251). ★ Close it, the answer was go and P8 is built. Alternative: Keep it open as the reshape record.
- #366 Felling real trees: you deferred it on 2026-11-06 and 2026-11-07, and the axe-strikes-ground bug is noted with it. ★ Keep it open and deferred. Alternative: Close it and open a bug for the axe.
- #452 UI drawing board for expeditions: no comments or doc yet, and the expeditions spec file does not exist. ★ Keep it open, expeditions are parked. Alternative: Close it until expeditions restart.
- #453 Expeditions design spec: the spec file was never written, and the P10 decisions of 2026-11-07 are the only record. ★ Keep it open, expeditions are parked. Alternative: Close it until expeditions restart.
- #668 Keeper's kit one design: nothing is built yet, because it needs your pick of a mock-up and your sign-off on the words, and it folds in #660 to #662 and replaces #249. ★ Keep it open and ask for mock-ups. Alternative: Close it and drop the redesign.

## Feedback 5 (build 25 notes): still open after the 2026-11-11 sign-off
Everything else in this batch was signed off in the Editor walkthrough on 2026-11-11 and moved to [decisions.md](decisions.md)
(verdicts: `overnight/open-questions-signoff.md`). The rename card's changes: #584.

### Decisions
- _#576 answered 2026-11-12: a new box starts in spring (#603); see [decisions.md](decisions.md)._

### Tester polish (2026-11-11, no [real] findings)
- #573 close-up chopping: the chopper hides behind the tree's crown; chips and the log on the ground are a few pixels.
- #576 winter at the default zoom: people a few pixels, tents pale against the snow.
- #571 the title pill resizes with its text, so the sun button shifts sideways as the line changes.
- #570 at year 0 the Kit already says "The camp waits for: a meadow · rabbits in the meadow": maybe too early for a new player.
- #575 after a rename the card is scrolled so "Call them something else" sits under the tab row; the rename field is half scrolled off at the top while Keep shows.
- #570 a tapped locked seed looks darker (selected) and could read as available.

## Waiting on you (after the 2026-11-10 sign-off)
Everything else was answered in the Editor walkthrough on 2026-11-10 and moved to [decisions.md](decisions.md)
(verdicts: `overnight/open-questions-signoff.md`).

- **Being built now**: #543 bigger fresh fish on the rack; #558 the seed in the life jar; #566 one feather pole at the camp's edge where people go to pray.

