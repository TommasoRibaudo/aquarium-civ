# Polish night 1: the words (#781, #773, #564)

For the user's sign-off. Each item has three options; the first (starred) is wired in code, and swapping is a
one-string change. All in the people's voice, closed world (research 21): they know a fire, a pot, a baby, the sky
(their name for the glass lid, see relics-and-glass-voice.md §2), never an outside world.

## #781 The song-name card's hint (story event 4.3)

Where: `Sim/SimCore.StoryChoices.cs` (`SongDef`, `RitualAt`). `{ritual}` is filled from the ritual the song belongs to
(berrying walk, harvest feast, offering, woodpile). Question and names are unchanged.

- Current: "Everyone sings {name} at it every year." (the ritual is never named).
- ★ **1. "Everyone sings {name} at the harvest feast, every year."** The ritual is named plainly; the tail stays the same
  rhythm as before. Per ritual: "on the berrying walk", "at the harvest feast", "at the offering", "when the woodpile is stacked".
- 2. "{name} is the song for the harvest feast, from now on." Reads as a ruling, a bit stiffer.
- 3. "Next feast they will sing {name}. So will every feast after." Warmer, but longer and ritual-specific wording is harder.

Wired: option 1.

## #773 The supper choice: one endless stew, or a fresh one each time

Where: `Sim/SimCore.SmallChoices.cs` (`SupperDef`). Current: question "Supper tonight: one big pot for everyone, or a
basket for each family?"; answers "one big pot" (Everyone eats together at the fire.) / "a basket each" (Each family
eats at home, quietly.).

**Does the sim match the new framing? Only partly.** The pick plays out tonight and tomorrow evening only
(`SupperPick`): the pot puts everyone at the fire around the stew pot; the baskets send each family home with a basket.
Nothing "keeps going forever" and "a fresh one each time" is not a repeating rule: it is two evenings. Baskets are not a
cooked fresh supper either. I did not add features. The words lean on what is seen (one pot, shared, topped up at the
fire / each family its own at home) and the question frames it as the choice for the next suppers. If the user wants
the stew to really persist, that is a new feature.

- ★ **1.** Q: "Supper: one stew kept going by the fire, or a fresh one each time?" A: "the stew kept going" (One pot by
  the fire, topped up. Everyone eats together.) / "a fresh one each time" (Each family has its own, new each evening,
  at home.). Closest to the idea in the user's words; the answers stay true to what the people then do.
- 2. Q: "The pot never leaves the fire, or does each supper start fresh?" A: "the pot that never empties" / "a new pot
  each night". Truer to "endless", but promises more than two evenings.
- 3. Q: "One old stew for all of us, or a fresh bowl each night?" A: "the old stew" / "a fresh bowl". Shortest;
  "old stew" reads as a little unappetising.

Also changed: the "let them decide" line is "Supper was {answer}." and the done lines are "The Keeper kept the stew going for
supper." / "The Keeper asked for a fresh supper each time."

## #564 The glass voice's first line, on the first mist

Where: `View/GlassVoice.cs` (`SayFirstMist`, `FirstMistLine`), triggered from `View/EcoMistView.cs` (`Pulse`) on the first
mist that greens ground; save flag `EcoState.firstMistWords`; said by the named person nearest the middle of the screen
(a crowd person if none), on the one caption as a relic-kind line (waits behind a line already up). Cheat: Slice debug
panel, "First-mist line" (clears the flag and says it).

**Closed world check.** The old plan said "the sky wept". The 2026-10-29 relics/glass-voice rows already make "the sky"
the camp's own word for the glass above them ("the edge of the sky", "the sky knocked!", the Sky Crumb), so "sky" is
inside the closed world: it is their lid. No "above the world", no weather outside. All options keep it.

- Original: "the sky wept".
- ★ **1. "The sky wept. The dust drank it."** The user's line with the one thing they see: the barren ground taking it.
  Seen-not-read: it names the picture under the mist.
- 2. "The sky wept! Look, the ground is drinking." More excited; longer for a phone caption.
- 3. "The sky is crying. Hold out your hands." A line a mother says to a child; it asks for the pose they strike.

Wired: option 1. Not shown in a catch-up, once per box (an old save with earlier mists may hear it once).
