# Open questions for the user (kept current)

Everything waiting on a decision by the user, in one place. Answered items move to [decisions.md](decisions.md) and are
deleted here. Recommended options come first and are marked ★. On GitHub: issues labelled `needs:user`.

_Last updated 2026-10-29._

## A. Notable roles (#24, [design/notable-roles.md](design/notable-roles.md))
1. **Role lists.** Camp: chief. Village: headman, miller, priest. Town: mayor, priest, miller, guildmaster, master
   builder, merchant, captain of the watch. OK?
2. **Town leader:** ★ elected mayor · inherited lord (family drama, feudal feel) · burgomaster.
3. **7th town role:** ★ keep the miller · innkeeper · schoolmaster · physician.
4. **No king, no general** (no war; a captain of the watch stands in for the general). OK, or do you want them?
5. **How roles are won:** ★ each role its own way (contest / election / succession / appointment) · one election rule
   for all.
6. **Empty roles:** ★ one role per notable; an empty role pulls a new notable from the crowd · it stays vacant.

## B. Shaping the land ([research 10](research/10-shaping-the-land.md))
1. **Two kinds of tool** (★ "gifts" with a cooldown that the people answer + free "landscaping"), or every tool the same?
2. **Limit:** cooldown per tool · ★? shared charges refilled by cleaning (to test in E2) · free with natural caps.
3. **Tool set per era** (camp: rain, grove, herd · village: channel, hill & hollow, meadow flowers · town: outcrop,
   wind, spring · decorate any time). Right, or swap in pond, fish, sun/lamp, flood, orchard?
4. **Cut "smooth/flatten" and "mist"?** (★ yes: smooth makes you the plot-maker, mist hides the world.)
5. **Animals / herds:** in or out? (Code-built animals, off-screen hunting.)
6. **Gestures:** one gesture per tool (hold to raise, drag a channel, rub for rain) · keep tap-to-place.
7. **Undo** a shaping until the people have used it?
8. **Notables' wishes** in cards ("Ivo dreams of deer"): nice, or a chore?
9. **Setting tools replace** standard ones (oasis: dune shift, palm spring; mountain: terraces, stream)? OK?
10. **Decorations** free with a small beauty effect, and later cosmetic unlockables there?
11. Test **"name a place"** in experiment E1?

## C. Relics and power-ups ([research 09](research/09-relics-and-powerups.md))
1. **Which go in the slice?** Suggested: Sky Crumb, Keeper Doll, Left Sandal, Keeper's Lamp, Era Festival.
2. **Carry-over:** a fixed few (~3) · all, with only some active.
3. **Giant objects from your room** (crumb, lens, hair…): is the comic scale right? Rule any out?
4. Should the people **slowly work out what the glass is**, as a story across boxes?
5. **Seasonal relics:** ★ game seasons only · real-world seasons (charming, but soft FOMO).
6. **Keeper's Nod** (nudges a person, never a building): still "Keeper, not King"?
7. **Shelf Neighbours** (finished boxes trade stories): worth it, or does it make the $0.99 unlock look like it sells
   power?
8. **Relic book:** ★ silhouettes with in-world hints · fully hidden.
- Also: research 10 proposes that it **owns all the tools** (Lamp, Storm, Orchard… move from 09 to shaping) and 09 keeps
  the rewards. OK?

## D. Older, still open
1. **Life stages** for the 110–160-year lifespans: adult 16, kids born to 20–40-year-olds, marry ≤ 50, elders 45/60 are
   unchanged. Stretch them (e.g. elders at 90/120), or leave them?
2. **Phone performance check** of the art (#35): needs a TestFlight run, your go.
3. **Slice build settings** (#35): the slice build should leave out the prototypes, which means changing Build Settings
   that the 0.3.x TestFlight builds use. When?
4. **CI job for the fast sim tests** (#41): OK to touch `ci.yml` while the `ci-hardening` work is open?

## E. Growth Lab results ([design/spikes/growth-lab.md](design/spikes/growth-lab.md))
1. **Blind test:** you + 2 others watch the shuffled clips (`docs/design/spikes/img/growth-lab/blind/set{1,2,3}_{A,B}.mp4`,
   15 s each) and pick which "grew like a real village"; then name the era in `era-test/still_01..09.jpg`. Key and
   instructions: growth-lab.md §6. When?
2. **The box fills up by year 100–120.** Is a full box the "box complete" moment, or should growth slow and keep
   commons (woods, a meadow) open?
3. **Town growth:** the economy plateaus at ~20 buildings; the spike added its own town extension (workshops, cottages,
   shops). Put town demand into the real sim in M2/M6? (★ yes, in M6 with the town buildings.)
4. **Phone cost:** 245 ms for a 48 h catch-up in the Editor (budget 300 ms). ★ a cheaper catch-up (path wear every 6
   months during long absences) now, and a phone timing with the next TestFlight.
- Decided by the orchestrator (tell me if not): placement results are **stored in the save**, not re-derived (layouts
  differed slightly between .NET runtimes); nudge answers pick by era and land (M2); the two small cheat gaps are fixed
  in M2.
