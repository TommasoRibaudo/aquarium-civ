# Writing events: checklist, style guide, cast of things

Every line a player reads in the box follows this page: Keeper's choices, chronicle stories, callbacks, office and role
lines, bubbles. It comes from research 21 (`docs/research/21-writing-events.md` §2, §4.4, §4.8) and issue #713.
Writing briefs point here. New text is `[est]` until the user signs it off.

## 1. The checklist (every ask, card, callback and bubble)

- [ ] Names a person, animal or thing in *this* box (from the cast list, §5)
- [ ] The premise survives a second look; closed world; the people use their era's words
- [ ] Ask 15 words or fewer; done line one sentence
- [ ] Each option leaves a **different** thing **and** a different life (no same-effect twins)
- [ ] No option changes the economy (decided 2026-11-14: the leans are gone)
- [ ] What it leaves is seen within a game year; something reads it later (a callback, a prop, the person card)
- [ ] Told once, on one surface (card *or* bubble)
- [ ] Gated on the state it talks about (era, building, animal, prior choice)

## 2. The five rating axes

Score 1 to 5 each, 25 at most. Keep at 20 or more; below that, fix it, rework it or cut it.

| Axis | Asks | 5 | 1 |
|---|---|---|---|
| **N · Named** | Does it name a specific person, animal or thing in this box? | the ask itself names them | "someone", "the people" |
| **M · Matters** | Do the options lead to different lives? Does anything change? | different futures | text only, or options that do the same |
| **R · Remembered** | Is it read again: a callback, a growing prop, a person-card line, a flag something reads? | it keeps coming back | forgotten |
| **S · Seen** | Does it happen in the box? | a staged scene and a lasting prop | text only |
| **W · World fit** | Is it true to this tank and era, about things that exist? | clean | names things that aren't there |

### Worked example: `lost_pup` (25 / 25)

> {child} found a wolf pup alone by the den. Bring it home, or leave it with its mother?

- **N 5:** the ask names the child who found it, and the pup is a real animal at a real den.
- **M 5:** bring it home and the box has a dog for generations (the first dog's line); leave it and the pack keeps it.
- **R 5:** the dog comes back in the litter, the grey muzzle and the guard lines, and on the child's person card.
- **S 5:** the child carries the pup to the fire while the Keeper watches; the dog then lives in the box.
- **W 5:** a camp, a den, a fire. Nothing in it is missing from the box.

## 3. The voice

A short form of `chronicle-and-history.md` §5. Read that section for the full table.

- **Closed world.** The people live in the tank. No line mentions a city, the sea, other countries or "the world
  beyond". Only the narrator says "tank", and only in "a far-away tank" for newcomers.
- **Never stated.** No line explains the game. The people never say aquarium, box, player, phone or game.
- **The narrator and the people differ.** Card bodies are the narrator and may say "the glass" and "the Keeper".
  Quotes, bubbles, teasers and asks are the people, and use their era's words (the glossary below).
- **Belief moves forward by era.** Camp: the sky wall and the Bright One. Village: the Keeper's window. Town: the
  glass, and a room beyond it.
- **Seen, not read.** An event matters when it happens in the box while the Keeper watches. The text is its caption.
- **Warm.** Mess is weather, never blame. A long absence makes them wonder, never complain.

## 4. The style guide

The voice sample is the game's best two lines, the first dog and the hunting rule:

> {who} carried the wolf pup home in both arms. Everyone crowded round it at the fire, and the children named it
> {dog}. It was the first dog.

> There were only a few deer left at the edge of the trees. {name} stood up at the fire and said: leave the does in
> spring. Nobody hunted deer in spring after that.

1. **Say who did what.** A named person does a concrete thing. Past tense for history and cards; present tense when
   the people speak.
2. **One picture per line.** One detail the player could see in the box. A small turn at the end is fine, but not on
   every line: a run of punchlines reads as written by a machine.
3. **Plain, short words.** "Set it down", "went up", "came home". Never vibrant, testament, journey, cherish,
   bustling or "a sense of".
4. **One word per thing, every time.** No synonyms. The glossary:
   - the fire;
   - tents in the camp, huts in the village, houses in the town;
   - the Bright One (camp people) and the Keeper (everyone else);
   - the edge of the sky (camp), the window (village), the glass (town people, and the narrator always);
   - the Keeper's tree, the name stone, the woodpile.
5. **Punctuation.** No em or en dashes: use a full stop, a comma or a colon. Straight quotes. British spelling (round,
   colour, neighbours).
6. **Lengths.**

   | Text | Limit |
   |---|---|
   | An ask | 15 words or fewer |
   | A hint | 12 or fewer |
   | A done line | 14 or fewer |
   | A card line | 1 or 2 sentences, 30 words or fewer |
   | A bubble | 4 lines of about 26 characters (about 12 words) |

7. **Read it with the names in.** Try a long name and a short one. A name appears once per sentence. A line never
   starts with a placeholder that may be lower case ({place} can be "the camp").
8. **Warm humour.** Laugh with the people, never at them.
9. **Only real things.** A line names only what exists in this box in this era. Need a thing? Take it from the cast
   list, or gate the line on it.
10. **Run the humanizer skill** (embedded mode) before sign-off, and read the line aloud once.

## 5. The cast of things (with "appears in")

Stories hang on these. New text points at a row; a thing that isn't here needs a gate and a row. Row names follow
`people-and-things.md` (the orchestrator may copy this column into that file's tables; it is kept here so the two
docs can be edited in parallel). Keys are chronicle kinds, dilemma ids and callback ids in `AquariumCiv/Assets/Game/Sim`.

| Thing | Appears in |
|---|---|
| The fire | `camp_fire` (who leads), `camp_travellers` opt 0 (stew pot), `village_hill_family` opt 0 (their boat), festival lines, `wedding`, elder story, supper choice |
| The Keeper's tree | `camp_remember` opt 1 (charms), `remember_tree_*` callbacks, office pilgrim lines (gated on the tree), tree bubbles, offering ritual (#721) |
| The name stone | `camp_remember` opt 0, `remember_stone_*` callbacks, the Left Sandal relic |
| The woodpile | `camp_winter` opt 1 (woodpile roof), `winter_wood_*` callbacks, the travellers' log pile |
| Berry bushes, baskets | `camp_winter` opt 0, `winter_baskets_*` callbacks, the berrying walk (#721) |
| The first dog and its line | `lost_pup`, dog lines (first dog, litter, grey muzzle, guard), `role_appointed` captain (with a dog) |
| Wolves, the meat rack | `wolf_thief`, `wolf_kill`, white wolf moment |
| Deer, the white stag, the chief's antlers | `white_stag` (antlers over the chief's tent), restraint rulings |
| The travellers' camp | `camp_travellers` opt 1, `travellers_coal`, `travellers_wave`, first-load line |
| The far-shore family, their boat | `village_hill_family`, `hill_*` callbacks, the lookout tower |
| The pond | `village_hill_family`, sandal loss, office feast lines (gated), `remember_stone_polish` |
| Feather posts | `camp_fire` (feather), feather pole |
| The well | `village_headman` (`headman_*_meetings`, gated on the well) |
| The windmill, flour sacks | `village_spring` opt 0, `spring_grain_*` callbacks |
| Roofs, the roof bird | `village_spring` opt 1, `spring_timber_*` callbacks, `role_masterwork` |
| The long table | `village_harvest` opt 0, `harvest_feast_*` callbacks, harvest feast (#721) |
| The offering basket | `village_harvest` opt 1, `harvest_offering_*` callbacks, the offering (#721) |
| The tavern | `harvest_feast_table` (gated) |
| Ducks on the pond | office "loud" toast (gated on ducks) |
| Everything else in `people-and-things.md` | none yet |
