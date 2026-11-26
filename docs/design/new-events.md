# New events: the ones that replace the flavour layer (#722)

Research 21 §7 asked for fewer events, each one heavier. Each event has to name someone, leave something different behind, happen
in the box where the Keeper can see it, and be remembered. This page specifies them for the user's sign-off. Each family becomes one
build issue once it is signed off. Every line follows the writing guide (`writing-events.md`) and is `[est]` until signed off.
Every scene gets an animation spec first and a clip after (the user's rule).

**Checked against the box as it is (2026-11-24).** The draft list in #722 is from 2026-11-14. Since then the box has gained the
rope swing, the boat, the goat pen, the dogs, the rituals, the nest, the washing line and the thirst signs. These changes follow:

- **The swing** on the Keeper's tree is cut. The swing hangs on a wild tree (the user's Q3) and is already built (B53.d).
- **The fallen branch becomes a bench** is cut. #332 took the log seats away and people sit on the ground.
- **A dry summer: the pond shrank** is rewritten as **the dry spell**. An established pond never shrinks in the sim. The thirst
  signs (#687) now show a dry spell.
- **The antlers** hang on a post beside the chief's door (the user's 2026-11-08 call), not over the tent.
- **Rivals' children become friends** and **the unchosen candidate's advice** are cut. The sim has no rivals, and the advice
  line can't be shown in the box.

**Pacing.** At most one new event a game year from these families, on top of the rituals. Each one plays once (the callbacks
play once per person). The pacing director (research 16) picks the slot, and the event waits if a moment is playing.

**Rating** (writing guide §2): N named, M matters, R remembered, S seen, W world fit. Each axis scores 1 to 5. Keep at 20 or more.

## 1. Things with a life

### 1.1 The tree's first fruit (22)
- **Gate:** the Keeper's tree is grown (stage 3 or more), it is summer, and it has never fruited before.
- **Cast:** {kid}, the kid nearest the tree who is free.
- **Scene:** {kid} spots one red fruit, stretches up on their toes and picks it. They run to the fire holding it up. Everyone
  there takes a bite, and the last bite goes to the youngest.
- **Line:** "The Keeper's tree gave its first fruit. {kid} picked it, and everyone had a bite."
- **Stays:** a few red fruits on the tree every summer after.
- **Read later:**
  - kids pick a fruit in summer (a life use at the tree);
  - a tree bubble: "The fruit is ripe";
  - the book.
- **Cheat:** "Tree: first fruit".
- **Rating:** N4 M3 R5 S5 W5.

### 1.2 Two marks on the tree (22)
- **Gate:** a wedding while the Keeper's tree is grown.
- **Cast:** the couple, {a} and {b}.
- **Scene:** at dusk after the wedding the couple walk to the tree. One of them cuts a small mark at hand height, and the other
  cuts one beside it. The kids watch from a step back.
- **Line:** "{a} and {b} cut their marks into the Keeper's tree, side by side."
- **Stays:** the two marks on the trunk. Each couple adds a pair, up to eight pairs.
- **Read later:**
  - each person's card: "Their mark is on the Keeper's tree."
  - their child's wedding: "{c} cut a mark under {a} and {b}'s."
- **Cheat:** "Tree: a couple's marks".
- **Rating:** N5 M3 R5 S4 W5.

### 1.3 The second stone (24)
- **Gate:** the name stone is full (nine names) and a tenth name is due.
- **Cast:** {who}, the strongest free grown-up, and {dead}, the one just lost.
- **Scene:** two grown-ups roll a rough stone from the edge of the camp. They set it upright beside the first one. {who} cuts
  {dead}'s name into it while the family stands round.
- **Line:** "The name stone was full. {who} rolled a second one beside it, and {dead}'s name went on first."
- **Stays:** the second runestone, the same kind as the first.
- **Read later:**
  - the remembering visit goes to both stones;
  - the gift at the stone (B51) can go to either.
- **Cheat:** "Stone: fill it".
- **Rating:** N5 M4 R5 S5 W5.

### 1.4 The antlers passed (21)
- **Gate:** the white stag's antlers hang on the post beside the chief's door, and a new chief (in the village, a headman) takes
  the seat.
- **Cast:** {old} (or their family, if {old} has died) and {new}.
- **Scene:** {old} lifts the antlers off the post and carries them to {new}'s door, then sets them on a post there. The old post
  stays empty.
- **Line:** "The antlers on the post went to {new}, with the seat."
- **Stays:** the antlers at the new door.
- **Read later:**
  - the seat's history in the book;
  - {new}'s person card.
- **Cheat:** "Chief: pass the antlers".
- **Built (2026-11-25, batch 3, B80.b):** the gate is either kind of antlers (the first stag's or the white stag's), the line as written. The line is written as the leader's seat changes; its moment plays right after the new-leader moment (the cheat plays it alone). Later passes are silent: the antlers follow the seat.
- **Rating:** N5 M3 R4 S4 W5.

### 1.5 A wedding across the pond (22)

**Cut by the user (2026-11-25).** Not built; kept here for the record.

- **Gate:** the far-shore family lives across the pond (the "there" answer in `village_hill_family`), their boat is moored, and
  each side has an unmarried grown-up.
- **Cast:** {a} from the far shore and {b} from {place}.
- **Scene:** {a} rows across in the boat. Half of {place} stands on the bank to watch. The wedding is at the fire, as usual.
- **Line:** "{a} rowed across the pond to marry {b}. Half of {place} watched from the bank."
- **Stays:** the boat is tied on this side more often, and the couple crosses to visit.
- **Read later:**
  - the pond family's lines;
  - the couple's person cards.
- **Cheat:** "Pond: a wedding across".
- **Rating:** N5 M4 R4 S5 W4. W is 4 because it needs the far-shore family to be living apart.

## 2. Trait moments that leave a thing

The traits are already in the sim (`NameData.Traits`). Each event fires once per box, for the first grown-up with that trait.

### 2.1 The tinkerer's bucket (21)
- **Gate:** a tinkerer, and people already fetching water.
- **Scene:** {0} sits by the fire and bends a spout onto a bucket. They try it at the pond, and it pours sideways.
- **Line:** "{0} made a bucket with a spout. It poured to the left, so everyone stood to the right."
- **Stays:** the crooked bucket at the pond. The water carriers use it, and the pour goes sideways.
- **Read later:** the water carry (a prop variant); {0}'s card.
- **Cheat:** "Trait: the tinkerer's bucket".
- **Built (2026-11-25, batch 3, B80.c):** "fetching water" is the living land's pond holding water (the scene is at its edge); the tinkerer carries the crooked bucket (their eldest child after them); the sideways pour shows in the watering uses (the water run has no pour of its own).
- **Rating:** N5 M3 R4 S4 W5.

### 2.2 The singer's song (22)
- **Gate:** a singer, and a ritual that has played at least once.
- **Scene:** at the ritual {0} starts a tune, and by the end everyone joins in.
- **Line:** "{0} made up a song for the {ritual}. After that, everyone called it {0}'s song."
- **Stays:** the song is sung at that ritual every year. It shows as a sing bubble with the song's name; see 4.3.
- **Read later:** that ritual, every year; the book.
- **Cheat:** "Trait: the singer's song".
- **Built (2026-11-25, batch 2):** the sim side only. The line is a card: the scene waits for its own animation spec. The song's
  name (4.3) shows on the ritual's caption, not in a bubble: ritual bubbles stay wordless (#276).
- **Built (2026-11-25, batch 3, B80.d):** the scene rides the ritual's own held moment: the singer's music notes first, everyone's by the end; the line is that moment's caption, and a card only if the ritual's moment is.
- **Rating:** N5 M3 R5 S4 W5.

### 2.3 The gardener's flowers (22)
- **Gate:** a gardener, and the name stone stands.
- **Scene:** {0} kneels at the stone and presses small flowers into the ground round it.
- **Line:** "{0} planted flowers round the name stone, and someone watered them every morning."
- **Stays:**
  - a ring of flowers at the stone;
  - a morning use: someone pours a cup of water on them.
- **Read later:** the remembering visit stops at the flowers.
- **Cheat:** "Trait: the gardener's flowers".
- **Built (2026-11-25, batch 3, B80.e):** six flowers in a ring; the morning water is a life use ("Stone: water the flowers"). Not wired yet: the remembering visit stopping at the flowers.
- **Rating:** N4 M3 R5 S5 W5.

### 2.4 The joker's face (21)
- **Gate:** a joker, and the woodpile stands.
- **Scene:** {0} carves a grin into the end of a log while two kids lean in to watch, laughing.
- **Line:** "{0} carved a grinning face into a log on the woodpile. Nobody would burn it."
- **Stays:** the face log at the front of the woodpile. When the pile is stacked again it goes back on top.
- **Read later:** kids pat it as they pass; the woodpile ritual.
- **Cheat:** "Trait: the joker's face".
- **Built (2026-11-25, batch 3, B80.f):** the face is a look on the woodpile under its roof; it goes on top when logs are laid; kids pat it ("Woodpile: a kid pats the face"). Not wired yet: the woodpile ritual reading it.
- **Rating:** N5 M2 R4 S5 W5.

## 3. Weather that happens in the box, told once

### 3.1 The first storm (22)

**Cut by the user (2026-11-25).** Not built; kept here for the record.

- **Gate:** the box's first storm day while there are tents.
- **Scene:**
  - in the storm, one tent's cover comes loose and flaps;
  - the next morning {a} and {b} sit at it and sew it back on.
- **Line:** "The first storm blew a tent cover loose. In the morning {a} and {b} sewed it back on."
- **Stays:** a patch on that tent, in a different colour.
- **Read later:** the patched tent (a kid's peek at it); the two menders' cards.
- **Cheat:** "Weather: the first storm".
- **Rating:** N5 M3 R4 S5 W5.

### 3.2 The dry spell (22)
- **Gate:** the first dry spell (#687's thirst: most of the grass dry) with the Keeper's tree planted.
- **Scene:** while the grass is brown, every evening {who} fills a bucket at the pond, carries it to the Keeper's tree and pours
  it at its foot. Others look up at the glass for rain (the sky look, #687).
- **Line:** "The grass went brown in the dry spell. {who} carried water to the Keeper's tree every evening."
- **Stays:** in every later dry spell, {who} carries the water again; after them, their child does.
- **Read later:**
  - {who}'s card: "Waters the Keeper's tree in a dry spell";
  - the book.
- **Cheat:** "Weather: the dry spell" (the "Dry spell now" cheat plus the start).
- **Built (2026-11-25, batch 3, B80.g):** the dry spell is the thirst signs' own threshold (`SimCore.ThirstyBox`); the evening repeat is a life use ("Tree: the waterer's evening bucket").
- **Rating:** N5 M3 R4 S5 W5. It changes no sim numbers: the look only, as the thirst signs.

### 3.3 The first snow (20)
- **Gate:** the box's first snow.
- **Scene:** {kid} stops, tips their head back and catches a flake on their tongue. The others copy, then they roll a small snow
  figure by the fire.
- **Line:** "The first snow came. {kid} caught a flake on their tongue, and the others copied."
- **Stays:** the snow figure, until the thaw.
- **Read later:** the snowball game (it exists) starts near it.
- **Cheat:** "Weather: the first snow".
- **Built (2026-11-25, batch 3, B80.h):** the old first-snow line (kind "snow", which the weather reads) says this line with a kid and is a Story moment; with no kid it keeps a plain line and no scene. It comes when the snow does (its dice unchanged), so it is never held back, but it takes the year's slot from the other new events.
- **Rating:** N5 M2 R3 S5 W5.

## 4. Small owned choices

Each one is a Keeper's ask. It follows the checklist: 15 words or fewer, named, a different life per option, no economy change.

### 4.1 Who carries the offering (22)
- **Ask:** "Who carries the offering to the Keeper's tree this year, {a} or {b}?"
- **Options:**
  - {a}: {a} leads the offering walk (B73) every year until they die. Their card says so.
  - {b}: the same, for {b}.
- **Done line:** "{a} carried the offering to the Keeper's tree."
- **Gate:** the offering ritual (`village_harvest`, the offering), and two grown-ups who have not carried it yet.
- **Rating:** N5 M3 R4 S5 W5.

### 4.2 Who sets the first log (20)
- **Ask:** "Who sets the first log on the woodpile this autumn, {a} or {b}?"
- **Options:** that person starts the stacking line and sets the first log. Their card notes it.
- **Done line:** "{a} set the first log on the woodpile."
- **Gate:** the woodpile ritual (`camp_winter`, the woodpile roof).
- **Rating:** N5 M2 R3 S5 W5.

### 4.3 The song's name (20)
- **Ask:** "{0}'s song needs a name. {name1} or {name2}?"
- **The names** come from the ritual, two per ritual:
  - the berrying walk: "Purple Fingers" or "The Long Walk";
  - the harvest feast: "The Long Table" or "One More Bowl";
  - the offering: "To the Tree" or "The Best Basket";
  - the woodpile: "The First Log" or "Snow on the Roof".
- **Stays:** the name in the sing bubble at that ritual, and in the book.
- **Gate:** the singer's song (2.2).
- **Built (2026-11-25, batch 2):** "Stays" is the ritual's caption ("The harvest feast: One More Bowl") and the book, not the
  sing bubble (#276). Done line [est]: "The Keeper named {0}'s song {name}."
- **Rating:** N5 M2 R4 S4 W5.

### 4.4 Which child holds the new pup (24)
- **Ask:** "A new pup came. Which child holds it first: {k1} or {k2}?"
- **Options:** the pup's keeper is that child. It already trails its keeper (the pup's keeper in the dog code). It follows that
  child until it is grown, and lies at their feet by the fire after that.
- **Done line:** "{k1} held the new pup first. It followed them everywhere after that."
- **Gate:** a litter (the first dog's line) and two kids.
- **Rating:** N5 M4 R5 S5 W5.

## 5. People callbacks

Each one fires once per person.

### 5.1 The Keeper's child comes of age (20)
- **Gate:** a child the Keeper named (`namedByKeeper`) becomes a grown-up.
- **Scene:** at the fire an elder hands them their work's tool: an axe, a basket or a rod.
- **Line:** "{name}, named by the Keeper, came of age and took up work as {job}."
- **Stays:** they start that work; their card's line.
- **Cheat:** "People: the Keeper's child grows up".
- **Rating:** N5 M2 R4 S4 W5.

### 5.2 The Keeper's child marries (21)
- **Gate:** the same child marries.
- **Line:** "{name}, named by the Keeper, married {b} at the fire."
- **Seen:** the wedding scene (it exists), with this caption.
- **Rating:** N5 M2 R4 S5 W5.

## 6. Cut, with the reason

| Event | Why |
|---|---|
| The swing on the Keeper's tree | The swing hangs on a wild tree (Q3) and is built. |
| The fallen branch, a bench | #332: people sit on the ground; no seats. |
| A rich autumn ("the baskets ran out") | Names no one, changes nothing (17 / 25). |
| Rivals' children become friends | The sim has no rivals. |
| The unchosen candidate's advice | It can't be shown in the box (16 / 25). |
| 1.5 A wedding across the pond | Cut by the user (2026-11-25). |
| 3.1 The first storm | Cut by the user (2026-11-25). |

## 7. For the user

- **Which families to build, in what order.** A suggested order:
  1. things with a life (1.1 to 1.3), since they use things already in the box;
  2. the pup choice (4.4);
  3. the trait moments;
  4. the weather;
  5. the other choices and callbacks.
- **The text,** line by line.
- **The new things that come with them:**
  - fruit on the tree;
  - marks on the trunk;
  - the second stone;
  - the crooked bucket;
  - the flower ring;
  - the face log;
  - the tent patch;
  - the snow figure.

  Each gets an asset row and a clip.
