# Read-through of a 120-year box, as a player (#61)

2026-10-29 · lane A (no Unity) · branch `vs/61-readthrough`

Until the box draws the sim, the chronicle is the playable surface, so I read three boxes the way a player would read
them. I judged them against [game-definition](../game-definition.md), [decisions](../../decisions.md) (eras, jobs and
roles, the inherited lord, the slice lifespan of 55–80, retirement at 65, apprentices from 50, families, the 3/5/7
cards with good news first, the evening talk, the glass stages, relics, sweepers), [notable-roles](../notable-roles.md),
[chronicle-and-history](../chronicle-and-history.md), [relics-and-glass-voice](../relics-and-glass-voice.md),
[first-session](../first-session-and-check-in.md), [life-in-the-box](../life-in-the-box.md) and
[game-design-practices](../../game-design-practices.md).

**[real]** = wrong or broken, fixed in the sim on this branch. **[polish]** = taste, left for you to pick.

## How it was read

- **Tool:** `Tools/SimTests/Readthrough/ReadthroughDump.cs`, an `[Explicit]` NUnit test (Unity doesn't compile it).
  Run it with `dotnet test Tools/SimTests --filter "FullyQualifiedName~ReadthroughDump"`.
- **Seeds:** 1, 1234 and 77, each for 120 game years.
- **The player:**
  - Away 3 h, then a 3-min check-in.
  - Away 3 h, then a 15-min session that ends with the evening talk (`OpenTeaser`).
  - That pair twice, then 8 h overnight. Every third night is a 48 h weekend away.
  - Every return shows `ReturnCards`, the peek (the top card's headline) and the teaser line under it (`CurrentTeaser`).
  - When an era is waiting, the player watches it at the next return.
  - Each 15-min session shapes the land once, with the era's first gift tool that's ready.
- **Transcripts** in [readthrough/](readthrough/), one set per seed:
  - `seed-N-chronicle.txt`: the full chronicle, with year, era, glass stage, kind, salience and text.
  - `seed-N-returns.txt`: every return as the player sees it, the evening talks, and how each teaser ended.
  - `seed-N-notables.txt`: the notables at years 0, 10, 25, 50, 75, 100 and 120 (age, lifespan, trait, job, role,
    parent, partner, friend and rival).
  - `seed-N-stats.txt`: counts and automatic flags.
- **Before and after:** the transcripts were made after the fixes. The counts below compare runs without shaping,
  before and after, so the two are like for like.

## Counts

| | seed 1 before → after | seed 1234 before → after | seed 77 before → after |
|---|---|---|---|
| Eras (village / town) | Y12 / Y50 | Y20 / Y84 | Y12 / **Y92** |
| Chronicle entries | 331 → 315 | 269 → 258 | 261 → 262 |
| Term lines (role-holder flavour) | 96 → 68 | 54 → 40 | 51 → 49 |
| Distinct texts told more than once | **31** → 10 | **21** → 4 | **15** → 7 |
| Camp lines with village or town words | 5 → 0 | 7 → 0 | 3 → 0 |
| "Bright One" after the camp | 1 → 0 | 1 → 0 | 3 → 0 |
| Storms or pranks in a well, field or camp | 2 → 0 | 3 → 0 | 4 → 0 |
| Gossip pairing relatives or married people | 4 of 7 → 0 | none told | 6 of 6 → 0 |
| Weddings in 120 years | 9 → 7 | 8 → 6 | 9 → 6 |

Cards per decade, before:

| Seed | Per decade |
|---|---|
| 1 | 24, 24, 16, 23, 23, 35, 28, 25, 19, 29, 23, 35 |
| 1234 | 21, 12, 19, 24, 12, 20, 18, 19, 28, 34, 18, 20 |
| 77 | 15, 16, 23, 24, 19, 10, 18, 20, 14, 24, 28, 29 |

After, the counts are about the same, with a little less term-line padding.

Pacing on return:

| Time away | Returns per seed | New entries | Cap | Overflow |
|---|---|---|---|---|
| 3 h | 23 | about 6 (1–12) | 5 | about half the returns spill into "Also…" |
| 8 h | 4 | about 16 (9–28) | 5 | always |
| 48 h | 1 | 23–49 | 7 | "Also…" holds the rest, e.g. "+34 more" |

The cap does its job and the stacks read well. Nothing piles up, and no return is empty.

## [real] findings, all fixed

1. **Weddings across generations.** Seed 1, year 18: "Dov Vane married Lorn Gale among the spring blossoms."
   - Dov was 35, a founder. Lorn was 17, the daughter of his two friends.
   - Seed 1234, year 102: "Gus Stile married Bo Marsh" (45 and 17).
   - Seed 1, year 23: Otto (17) married Kit (31).
   - **Fix:** both partners 18 to 50, at most 12 years apart, not kin (two generations checked), different family
     names. Widows may remarry.
2. **Full siblings had different surnames.** Seed 1: "Lorn Gale was born to Greta Gale", then "Tilda Hearth was born to
   Moss Hearth". Both are children of the Greta and Moss marriage. The headline even said "Moss and Greta had a baby,
   Tilda".
   - **Fix:** a couple's first child takes the rarer of the two names, and later children take the same one. The line
     names both parents: "Tilda Gale was born to Moss Hearth and Greta Gale."
3. **The gossip matched parents with children and married people.**
   - Seed 1, years 117 and 120: "Quill Wick the master builder introduced Edda Marsh to Bo Marsh… They talked until
     the lamps went out." Edda is Bo's daughter.
   - Seed 1, year 93: "the whole town knew about Quill Wick and Greta Wick before Quill Wick and Greta Wick did."
     That is a mother and her son.
   - Seed 77, year 64: "Lune Hearth the headman introduced Joss Mill to Wim Hearth." They were already married.
   - **Fix:** the matchmaking and rumour lines only pair two single, unrelated adults of an age. With no such pair, the
     line isn't told.
4. **A role-holder sorting out their own feud.** Seed 77, year 24: "Lorn Gale and Dina Reed fell out… Dina Reed the
   miller has promised to sort it out." Seed 77, year 45: "Joss Mill and Lune Hearth fell out… Lune Hearth the headman
   told everyone by supper."
   - **Fix:** the coda skips the holder if they're either party.
5. **Wrong era words.**
   - "The whole street sighed with relief" in the camp. Seed 1, year 11, and 6 to 8 times a box in every era.
   - "counted the people of the village" and "hid every spoon in the village" in the camp, and the same spoon line in
     the town.
   - The weathervane in the camp.
   - "whose turn it was at the well" in the camp, where there is no well.
   - "shut the door very loudly" for a chief contest in tents.
   - "the lamps went out" at the camp fire.
   - The stand-ins "{0} swears the Bright One sneezed… Half the camp believes it" and "Some say the Bright One was
     looking for something" were told in the village and town (seed 77, years 23, 78 and 118).
   - Seed 1, year 0: "Moss Hearth planted the first field of grain", in a hunter-gatherer camp, ten years before "the
     elders talk of staying put and sowing seed". The camp also built a "lumber mill". The camp's farm and lumber mill
     are gathering grounds and a woodpile (decisions, #23).
   - **Fix:**
     - The era's noun is used everywhere.
     - "The Bright One" is said in the camp only.
     - The camp gets gathering grounds and a woodpile, in the first lines, the headlines and the Keeper's credit lines.
     - A hut is a cottage in the village and a house in the town.
     - Quarrels about the well become fetching water in the camp.
     - The camp has the fire burning low and losers stomping off to their tent.
6. **Roofless buildings.**
   - "A great storm rattled the glass. The people huddled together in the camp." (Seed 1, year 93, in the town.) Also in
     the well, the farm and the lumber mill (seed 1234, year 7, in the camp).
   - "Juna Hearth hid every spoon in the village inside the camp." (Seed 77, years 60 and 80.)
   - "climbed the farm roof".
   - **Fix:** storms and trait stories use a roofed building (the drawn one if it has a roof, else the next one).
7. **Word-for-word repeats.** 31, 21 and 15 distinct texts were told twice or more per box.
   - Trait stories picked one of two lines at random, although the code says "two lines each so a long life doesn't
     repeat". Example: "Moss Hearth sneezed so hard the weathervane spun round" at years 2 and 22.
   - The world's events had one line each: ducks ×3, the tavern sheep ×3 ("It has a stool now"), "A dry summer…" ×3,
     the eldest's winter story twice in four years.
   - A 46-year master builder "made an announcement… every morning" three times.
   - **Fix:**
     - Trait lines are told in turn per person.
     - Storms, fog, rainbows, droughts, harvests, ducks, the sheep, the eldest, making up and festivals get 2–3 wordings,
       told in turn.
     - Each one-line term story got a second line, and a kind rests longer each time its lines have all been told.
     - A pair never falls out over the same thing twice in a row, and a lost wanderer never brings back the same find
       twice in a row.
     - Sore losers vary.
     - All of it is chosen after the dice, so the story stream is unchanged.
8. **"Glass" said too early.** Seed 1, year 84, at the evening talk: "Pell Gale measured the glass again. It is still
   there, and still glass." There was also "The astronomers have asked for a bigger lens" before any lens existed.
   Decisions #58 say the people first say "glass" when the Lens is set up.
   - **Fix:** before the Lens, the town's slices of life use the line bank's "wall" lines (29–31). The glass lines come
     after it.
9. **Teasers that paid off with the wrong subject or people.**
   - Seed 77: "Wim Hearth knows something about Juna Hearth, and is dying to tell." It was paid off by a line about Joss
     and Alma.
   - Seed 77: "Flora Fernby is still not changing their mind about a goat." It was paid off by a row over a fence post.
   - The gift teaser was always "The gatherers keep checking the wet ground", even for an outcrop in a town.
   - After a new gift of the same kind, the evening talk said the same words session after session ("Everyone keeps
     going down to look at the new water" 14 times in three boxes).
   - **Fix:**
     - The office teasers no longer name a subject or a person.
     - Each gift tool has its own line.
     - The evening talk never repeats the last line's words.
10. **A priest named on their sixteenth birthday.** Seed 1, year 111: "Before the end, Pim Wick named Edda Marsh the new
    priest", in the month Edda came of age.
    - **Fix:** naming and appointment pass over the under-25s while someone older can stand (notable-roles §3, 25+).
11. **Small ones.**
    - Headlines were cut mid-word ("…at the camp f…"). They now cut at a word.
    - "The settlement grew to 20 people" years after it was named. It now uses the name.
    - Placing the Lamp never called the `KeeperLampUsed` hook. Nothing did, so the strange light could never become the
      Keeper's lamp.
    - "Under lord X the market steps is swept" is now "someone sweeps the market steps".

Checks I made that came back clean:

- Nobody dead acts. My flag for a name in a line belonging to nobody alive only fired on lines about the dead, e.g.
  "With no one of X's blood left".
- Retirement happens at 65, deaths at 55–80, and apprentices are taken from 50.
- The leader line goes chief → headman → lord, and the lordship passes in the family ("When old Lorn Gale died, their
  eldest child, Pell Gale, became lord").
- Roles, offices and sweepers appear in the right era. The 3/5/7 cap holds, good news comes first, and deaths come
  after it.
- No line guilt-trips the player, and mess is never mentioned.

### Economy

Fixes 1–2 (who can marry) change which stories the story stream tells, and with them where its weather lands (storms,
rainbows, droughts, harvests). That moves the economy slightly. The economy's own rules are untouched, and roles still
draw no dice (`MeddlingWithRolesLeavesTheEconomyAlone` still holds). The .NET-only pins were re-measured on purpose:

- JobTests: camp, town, town with sweepers.
- RoleTests: the pre-roles pins for seeds 1, 1234 and 77.

Every other fix is text, roles or hashes, chosen after the dice, so it left the economy exactly as it was. The
fingerprints changed because they hash the chronicle text. The reasons are in the commit messages.

### Tests added

`AquariumCiv/Assets/Game/Tests/ReadthroughTests.cs` runs under Unity and dotnet. It checks:

- each era's own words over a century, for three seeds;
- no sentence told twice in a row;
- gossip pairs;
- codas never about the holder's own feud;
- weddings of an age;
- a couple's children share a name;
- the wall before the Lens;
- seasoned priests;
- headline cuts;
- the Lamp hook;
- the gift teasers;
- the camp gathering before it sows.

## [polish], for you to pick

1. **The village can last 80 years** (seed 77: village at year 12, town at year 92). Two decades there had only 11–15
   cards. This is era pacing, which is economy tuning, so I left it.
2. **A founding name can take over.** Seed 1 at year 120: 10 of 14 notables are Gales. Before the fix it was 6–8 of 14
   in two seeds. Fewer surnames make people harder to tell apart and remember. Options:
   - more family names;
   - newcomers bring new names more often;
   - a child sometimes takes the other parent's name.
3. **Fewer weddings now:** 6–7 a box instead of 8–9, because of the age and kin rules.
4. **Term lines still repeat once over a very long term.** Example: "Dina Brook the master builder has not called a
   meeting in two years" twice in a 40-year tenure, about 12 years apart. Fix with a third line per kind, or caps on
   tenure-long repeats.
5. **Codas repeat with the same holder.** "Zeb Vane the innkeeper organised the sack race" ran at every festival for 30
   years. "Ulf Fernby the miller made the afternoon off official" appeared three times.
6. **The Keeper's credit lines repeat** for the same person and tool: "After the Keeper dug a channel from the pond, Gus
   Marsh planted a new field beside it" ×3.
7. **The town's merchant and captain of the watch are never named.** The era line says "…and 2 more took up offices".
   Name them, or add a second clause for the lord's appointments.
8. **The lord's death isn't a Legend card** when the succession line tells it ("Also: Lorn died", while "When old Lorn
   Gale died…" leads). Consider a Legend card for every leader-line death.
9. **Apprenticeships don't follow the master's trade.** A fisher takes an apprentice, who becomes a brewer.
10. **Humble jobs for the great.** "Kit Gale put down their work as a sweeper": the lord was a sweeper all along. Funny,
    or should the leader's job be a "better" one?
11. **The dead's names come back.** Seed 77 has a second Lune Hearth, as lord. "Named after" would make it a story;
    otherwise avoid the name.
12. **Year turns bunch up.** Deaths, births, newcomers and coming of age all land in month 0, so a return can open with
    two deaths and a birth together.
13. **A couple teaser fades silently** when one of the two marries someone else. The design allows it, but a line
    ("Mira married Bo instead. Tob took up the fiddle.") would pay it off.
14. **The four "know too much" trait lines** (the Keeper's cat, the city beyond the glass…) start at the town, not
    after the Lens. The design table marks them as the later half. The sim has no relic events yet, so gating them on
    the Lens would hide them for the whole slice.
15. **Spouses "became fast friends" after marrying,** and in seed 1 a wife beat her husband for headman ("…went home and
    shut the door very loudly", into their shared house). Charming or odd?
16. **Only one parent is stored per child.** The book's family trees will want both (chronicle §4.1, the P4 note).
17. **A tied vote reads oddly.** "won on a tied vote" should say who broke the tie.

## Sample lines

Best, after the fixes:

- "Dov Vane and Moss Hearth both wanted to be chief. The camp chose Dov Vane; Moss Hearth stomped off to their tent and
  stayed there until supper." (seed 1, year 0)
- "Dov Vane threw a harvest festival. Someone danced with a goat, and the goat led." (seed 1, year 2)
- "Dina Brook was made master builder while asleep at the meeting. They have not yet been told." (seed 1, year 59)
- "Greta Gale, the village's miller, spent a night on the hill watching the sky, with Tilda Gale holding the blanket."
  (seed 1, year 24)
- "Hob Barrow, the camp's chief, had extra lanterns hung round the camp fire, and a fence put round the fence." (seed
  1234, year 1)
- "Bram Brook stood against guildmaster Nils Gale and won, 3 votes to 2. Nils Gale spent the evening telling the goats
  it was rigged." (seed 1, year 99)
- "A thick fog sat in the glass for days. Bram Vane said good morning to a bush." (seed 1234, year 4)
- "Hester Gale, the town's merchant, gave a toast at the market steps so loud the ducks took off." (seed 1, year 75)

Worst, before the fixes:

- "Quill Wick the master builder introduced Edda Marsh to Bo Marsh at the timber yard. They talked until the lamps
  went out." (seed 1, year 117: a father and his daughter)
- "Dov Vane married Lorn Gale among the spring blossoms." (seed 1, year 18: 35 and 17)
- "Lorn Gale and Dina Reed fell out… Dina Reed the miller has promised to sort it out." (seed 77, year 24)
- "A great storm rattled the glass. The people huddled together in the camp." (seed 1, year 93, in the town)
- "Pell Gale measured the glass again. It is still there, and still glass." (seed 1, year 84: no Lens yet)
