# 09 — Relics and power-ups: what the people find, and what the Keeper can give

Research date: **2026-10-29**. Question from the user (Q15 in [game-definition.md](../design/game-definition.md)):
"look for: 1 cool ideas for items, they can be a bit comic, 2. cool ideas for powerups and propose them to me."

Context: the civ lives in a glass box on a shelf. The player is the Keeper: they clean, shape the land (rain, grove,
channel) and watch. They never place buildings. A box runs camp → farming village → crafts town, then completes and
stays on the shelf as a museum. A new box starts in a new setting (desert oasis, mountain village, ...) with
descendants and relics carried over. Business model: free first box, one $0.99 unlock, no ads, no timers, no gacha,
care is never sold.

Markers: **[V]** read in a primary source (official page, the game's own text). **[S]** secondary source only (wiki,
fan guide, press, search summary). **[est]** my guess, to test. This doc proposes options. It doesn't decide anything.

---

## 0. TL;DR

- **The best model is Neko Atsume's "gift left behind"**: the little people make, lose and find things while you're
  away, and you discover them on your return. Add **Animal Crossing's appraisal voice** (every relic gets a funny
  in-world label) and **its museum laid out as a timeline** (the shelf museum, by era) (§1).
- **Relics should come from the sim's own stories**, not from a random drop table: a founder dies and leaves a spoon,
  a rivalry ends in a cheese contest, the town's astronomer points a lens at the Keeper. Every relic has a chronicle
  card behind it. No rarity tiers, no drop rates, nothing you can miss forever (§1.3).
- **34 relic ideas** in five groups (§2): things the people make, things they lose or misunderstand, giant things that
  fall in from the Keeper's room, things dug from the soil, and setting-only relics. **Top 8** in §2.6.
- **18 power-up ideas** (§3), all free and earned in play: Keeper's gifts, shaping upgrades, era festivals, watcher's
  bonuses and relic effects. **Top 6** in §3.2. **12 rejected** ideas with reasons in §3.3.
- **Hooks** (§4): a relic is born as a chronicle card, sits in a collection book and on the shelf, and up to a few
  ride along to the next box as a small start or a story. The $0.99 unlock opens more boxes (so more relics), never
  better ones.

---

## 1. How other games handle found items and boosts

### 1.1 Reference table
| Game | What you collect / get | How it's found | What it does | Lesson for us | Source |
|---|---|---|---|---|---|
| **Neko Atsume** (2014) | Fish (silver/gold) and **mementos** from each cat; a photo album | Cats visit while you're away and "may leave collectible mementos and gifts of fish". Mementos come after repeated visits, at random | Fish buy food and goodies. Mementos are pure collection, shown on the cat's profile and in a memento list. "There is no end game" | **The gift left behind.** Discovery on return, tied to a *character*. Mementos are funny and personal (an acorn, a rolling pin, Billy the Kitten's Lucky Coin) | [Wikipedia](https://en.wikipedia.org/wiki/Neko_Atsume) [S], [GameSkinny](https://www.gameskinny.com/culture/neko-atsume-mementos-list-guide-cats-can-show-love-too/) [S], [Twinfinite](https://twinfinite.net/guides/neko-atsume-how-to-get-all-mementos/) [S] |
| **Animal Crossing: New Horizons** | Fossils, assembled into skeletons; donated to the museum | Dig spots, up to 4 fossils a day on the home island [S] | Blathers appraises each one (with a fun-fact speech), then you donate it. The fossil hall is laid out **as a timeline** of life, ending with villager silhouettes | **An appraisal voice** gives each item a personality. **A museum ordered by history** is itself a story | [Nookipedia](https://nookipedia.com/wiki/Museum) [S], [iMore](https://www.imore.com/animal-crossing-new-horizons-fossil-guide) [S], [Play the Past](https://www.playthepast.org/?p=6880) [S] |
| **Stardew Valley** | 42 artifacts + 53 minerals, donated to Gunther's museum | Artifact spots dug with the hoe; **each area has its own artifacts**; also fishing chests, geodes | Pure collection, plus **milestone rewards** (seeds at 5, a statue at 15 artifacts, a Stardrop at all 95) | **Place-specific finds** give a reason to visit new places (our new settings). Milestones reward the collection, not single drops | [Stardew wiki](https://stardewvalleywiki.com/Museum) [S], [Destructoid](https://destructoid.com/stardew-valley-full-artifacts-list-and-where-to-find-them-all) [S] |
| **Kingdom Two Crowns** | Coins, gems; **statues/shrines** that bless your kingdom | Gems from gem chests unlock a statue; a one-off coin payment activates it | Effects (archers' accuracy, +2 farmers per farm, stronger walls) **persist across islands until the monarch dies**. The knight statue is double-edged | **A boon that carries between "lands"** is the shape of our carry-over relics. Small, specific, visible effects | [Magic Game World](https://www.magicgameworld.com/kingdom-two-crowns-statues/) [S] |
| **Cult of the Lamb** | Relics (37 added in 1.2.0); **corrupted relics** | Unlocked over the campaign; corrupted ones after the final boss | Strong one-shot effects on crusades. Corrupted relics pair a benefit with a drawback | Comic **double-edged** items are fun, but combat power is not our game. Take the tone, not the mechanic | [Kotaku AU](https://kotaku.com.au/?p=1364363) [S], [GamePressure](https://www.gamepressure.com/S013-amp.asp?ID=21757) [S] |
| **Townscaper** | No items; hidden **emergent builds** (a lighthouse, gardens, birds) | A three-block tower of different colours turns into a lighthouse; pigeons appear only once there are roofs, butterflies only near bushes | Delight, plus achievements | **Hidden conditions, no quest UI.** Things appear because the world is in a certain state. That's how our relics should trigger | [TrueAchievements](https://www.trueachievements.com/game/Townscaper/walkthrough) [S], [Supercheats](https://www.supercheats.com/townscaper-cheats-tips) [S] |
| **Dorfromantik** | Quest tiles, new tile types, biomes | Hidden quest tiles far from the start; finishing them unlocks achievements | Cosmetic rewards: "a clock tower village tile or an animal that wanders a forest", biomes, art styles. Ordinary quests give +5 tiles | **Rewards are new kinds of content**, not bigger numbers | [Wikipedia](https://en.wikipedia.org/wiki/Dorfromantik) [S], [GamePretty](https://gamepretty.com/dorfromantik-beginners-guide-quests-rewards-tile-placement/) [S] |
| **WorldBox** | God powers in groups (world creation, civs, creatures, nature and disasters, destruction); a "Blessed" status on units | Always in the toolbox | Powers "can be used without mana or resources" | Powers limited by **consequence and fun, not by a currency**. But most WorldBox powers are destruction, which we don't want | [Wikipedia](https://en.wikipedia.org/wiki/WorldBox) [S], [GamePretty](https://gamepretty.com/worldbox-god-simulator/) [S] |
| **Viridi** | Succulents in a pot | Free starter plants, a **free weekly seedling** from the nursery; more plants cost cents | Plants grow in real time; they can die from over- or under-watering | A **slow, free, recurring gift** feels kind. Plant death is the anti-pattern for us | [App Store](https://apps.apple.com/us/app/viridi/id1107708818) [V], [Kill Screen](https://www.killscreen.com/viridi-promises-capture-soothing-art-potted-plant-care/) [S] |
| **Tamagotchi** (V3/V4 toys) | **Souvenirs** (a trophy, a gold disc, a wedding dress) vs usable items | Passwords from the TamaTown site; some date-based | Souvenirs sit in a list; items are used. **Some souvenirs can't be re-earned if lost** | Collection separate from consumables is good. **"Gone forever" is the anti-pattern** | [TamaZone forum FAQ](https://www.tapatalk.com/groups/tamazone/-t3243.html) [S] |
| **Pokémon GO** | Incense, lures; **Daily Adventure Incense** | One free per day; you can hold only one; **disappears at 11:45 pm** if unused | 15 minutes of more spawns while walking | A free daily boost is generous, but **"use it or lose it today" is FOMO**. Avoid | [Bulbapedia](https://bulbapedia.bulbagarden.net/wiki/Daily_Adventure_Incense) [S], [GoDisageek](https://www.godisageek.com/2022/07/pokemon-go-is-getting-daily-adventure-incense/) [S] |
| **Pokémon Sleep** | Incense (Focus, Friend, Growth, Luck, Recovery), Good Camp Ticket | **Sold for diamonds** or swapped for Sleep Points | Double EXP/shards/energy for one night; the camp ticket is a 7-day boost | **Sold, time-boxed boosts**: the exact thing pillar 6 rules out | [Bulbapedia](https://bulbapedia.bulbagarden.net/wiki/List_of_items_in_Pok%C3%A9mon_Sleep) [S], [PokeCharms](https://pokecharms.com/guides/how-to-use-incense-in-pokemon-sleep) [S] |
| **Cats & Soup** (cozy idle) | Ad boosts: faster cooking, double the gold pile | Watch an ad | Temporary speed | **Ad-gated speed-ups**, plus reviews about "things constantly flashing and wiggling to get you to watch more ads" | [Justuseapp reviews](https://justuseapp.com/en/app/1581431235/cats-soup/reviews) [S] |

Comic reference for the "giant object falls in" group: in *The Gods Must Be Crazy* (1980) a pilot drops a Coke
bottle, a hunter finds it, the community finds dozens of uses for it, and then it causes trouble
([Wikipedia](https://en.wikipedia.org/wiki/The_Gods_Must_Be_Crazy) [S]). Our version keeps the joke warm: **we laugh
with the people, never at them.** They're clever with strange things, not foolish.

### 1.2 Patterns that work for us
1. **The gift left behind** (Neko Atsume). Things turn up while you're away; the return is a discovery.
2. **Every item belongs to someone** (Neko mementos per cat). Our relics belong to a notable or a family.
3. **An appraisal voice** (Blathers). Each relic gets a short label written by the people, often wrong in a funny way.
4. **The museum is a timeline** (ACNH fossil hall). The shelf museum shows relics by box and era.
5. **Place-specific finds** (Stardew artifact spots). Each setting has a few relics only it can make.
6. **Boons that carry over** (Kingdom statues). A relic brought to the next box gives a small, visible start.
7. **Hidden conditions, no quest log** (Townscaper birds and lighthouse). A relic appears because the world reached a
   state, not because a checklist said so.
8. **Rewards are new content, not bigger numbers** (Dorfromantik). Milestones unlock looks, festivals, stories.
9. **Powers cost nothing but have a natural limit** (WorldBox). Rain doesn't need mana; the land can only take so
   much rain.

### 1.3 Patterns that would break our pillars
| Pattern | Seen in | Breaks |
|---|---|---|
| Boosts sold for currency or time-boxed tickets | Pokémon Sleep incense and camp ticket | Pillar 6 (fair to wallet), anti-reference "F2P ad-farms" |
| Ad-gated speed-ups and double rewards | Cats & Soup | Pillar 6, "no ads" |
| A free item that **expires tonight** | Pokémon GO daily incense | FOMO; pillar 4 (absence welcomed) |
| Items that can never be re-earned | Tamagotchi souvenirs | FOMO, guilt |
| Random drop rates, rarity colours, mystery boxes | most gacha games | "No gacha"; also hides the story behind the item |
| The thing you care for dies if you're away | Viridi | Pillar 4, "death: old age only" |
| Destruction powers (meteors, plagues) | WorldBox | "The civ never collapses"; cozy tone |
| Relics that only drop from mess | (our own risk) | Players would let the box get dirty on purpose. Care must not become farming |

Rule for this doc: **relics come from stories, and stories come from the sim.** No relic is bought, rolled or timed.

---

## 2. Relics: 34 ideas

How to read the tables: **Found** = what in the sim or the player's actions makes it appear. **Does** = one of
**C** (collectible/story only), **S** (a small start bonus in the next box), **H** (a story hook: it changes what
happens or what the chronicle says later). Starred names (★) are my top 8.

Shared rules for all relics [est]:
- One relic per box at most from each idea, and each idea can only come up when its condition is true, so a box makes
  roughly **8–15 relics** over its 1–2 weeks [est].
- Every relic arrives with a **chronicle card** and a **label** written in the people's voice.
- Nothing is missable forever: a condition you didn't hit in this box can come up in the next.

### 2.1 Things the people make (heirlooms)
| Name | One line | Era / setting | Found | Does | Why it's fun |
|---|---|---|---|---|---|
| **★ The First Spoon** | The founder's carved wooden spoon, too big for anyone's mouth, passed down the family | Camp | The founding notable dies of old age; their eldest child keeps it | **H**: in the next box the founding family brings it; a card later says "Ada's great-great-granddaughter still stirs the soup with it" | A family line made into an object. It's the first relic most players will get |
| **Lumpy Pot No. 1** | The village's first pot. It leans. Everyone pretends it doesn't | Village (first pottery) | The first potter's workshop opens | **S**: the next box starts with one season of stored grain | The people's first craft, honestly bad |
| **The Rival Cheese** | A cheese wheel two rival families both swear they made | Village | Two rival notables are alive during a harvest festival | **H**: descendants of both families arrive in the next box still arguing; the rivalry continues | Feuds that outlive the people who started them |
| **★ The Keeper Doll** | A peg doll of what the people think the Keeper looks like: one huge hand, one huge eye | Any era (style changes by era) | The chronicle has credited the Keeper for 10+ shaping actions [est] | **H**: the doll holds what you do most (a cloth if you clean a lot, a watering can if you rain a lot). On the shelf it faces you | The people's view of *you*. Every player's doll is a little portrait of their play |
| **Map of the Box** | A town cartographer's map of the world. All four edges say "Here Be Glass" | Town | Town era and a notable with the curious/scholar trait [est] | **H**: in the next box an old copy shows "the old country", the previous box | They know their world has edges, and they're proud of it |
| **Festival Mask** | A mask from the first harvest festival, a bit scary, a bit silly | Village | The first harvest festival happens while you watch | **S**: the next box holds its first festival one era early | Rituals carry across boxes |
| **The Always-Wrong Clock** | The town clock maker tried to measure "Keeper time". The hand spins once a year | Town | The first clockmaker's workshop | **C**: on the shelf, its hand moves exactly one turn per real hour | A wink at 1 hour = 1 year. Sharp-eyed players will notice |
| **Guild Banner (with typo)** | The weavers' guild banner, beautifully stitched, motto misspelled | Town | The first guild forms | **C** | Small human pride, small human error |
| **Wedding Spoons** | Two carved spoons tied together | Village | Two notables marry | **H**: if both families carry over, their children found the new box's first house together | Love stories are rare in idle games |

### 2.2 Things the people lose, mix up or misunderstand
| Name | One line | Era / setting | Found | Does | Why it's fun |
|---|---|---|---|---|---|
| **★ The Left Sandal of Bo Ferris** | A notable lost a sandal 40 years ago. It became a legend. It was under the weeds all along | Any | A notable loses something (a story card); decades later the player **prunes the overgrowth** where it fell | **C** + a card: the old owner (or their grandchild) gets it back | Cleaning turns up a story. The find is seeded by the sim, so letting weeds grow doesn't make more of them |
| **The Sacred Rock** | The camp worshipped this rock for three generations. It is a rock | Camp | Camp era ends live; the people carry their holy object into the village | **H**: the village builds its first shrine around it (their choice) | Pure deadpan |
| **Pebble Collection** | A child's best pebbles, sorted by "goodness" | Camp / village | Found when you **tidy a trash heap** that a child notable played near | **C**; the label lists the ranking | A tiny person's treasure in your rubbish |
| **The Unsent Letter** | A love letter a notable never sent, to their rival | Village / town | A notable with a rival dies of old age | **H**: a card reveals it; in the next box a descendant reads it and the families make peace | Re-reads a whole relationship |
| **Square Wheel** | An inventor's first try. Also the second. And the third | Town | A notable with the tinkerer trait [est] starts work | **C** | Failure as a museum piece |
| **★ The Prize Turnip** | A turnip so big it needed its own cart; it grew where the Keeper's rain fell | Village | You **make it rain** on a field the summer before a harvest festival | **S**: the next box's first field yields a bit more [est: +10 % for one game year] | The player's action ends up as a trophy in the village |
| **The Treaty Stick** | Two families settled a border by breaking a stick in half. Both halves survive | Village | Two rival notables make peace | **C** | Peace, made comic and physical |

### 2.3 Giant things from the Keeper's room (they fall in)
How they fall in: the box's lid opens a crack whenever the Keeper shapes the land (rain, seed a grove, dig). Rarely,
something from the room drops in with it. **Each item can only fall once per box, only after a shaping action, and
never while the player isn't there** [est], so you always see it land.

| Name | One line | Era / setting | Found | Does | Why it's fun |
|---|---|---|---|---|---|
| **★ The Sky Crumb** | A biscuit crumb the size of a hut. The camp eats well for a week and tells the story for a century | Camp | Your first shaping action in the first box (a scripted wow moment), then rare after that | **H**: "the year of the Sky Bread" becomes a festival in that box; **C** on the shelf | The first time the player's world and theirs touch. Instantly readable |
| **The Button** | A four-holed coat button. The village uses it as a millstone | Village | Shaping near the river in the village era | **C**; the people may build it into the mill (their choice) | Scale joke you get at a glance |
| **The Paperclip** | Bent into the village's first plough, then a gate, then a sculpture | Village → town | Shaping in the village era | **C**; it changes shape each era (three looks) | One object, three eras of reuse |
| **★ The Lens** | A spectacle lens. The town builds an observatory around it and points it at the Keeper | Town | Shaping in the town era | **H**: from then on, town cards include "The astronomers report the Keeper looked tired today" when you return late [the line must stay warm, never guilt] | The people literally watch you back |
| **The Great Hair** | One long hair. The village braids rope from it for a decade | Village | Shaping in the village era | **C** | A bit gross, very funny |
| **The Giant Coin** | A coin from the Keeper's pocket. The town argues for years about whose face is on it | Town | Shaping in the town era | **H**: they decide it's the Keeper. A town notable writes "The Keeper, not a King" on the base | Our pillar as an in-world joke |
| **The Earring** | A small hoop earring becomes a wedding arch | Village | Shaping in the village era | **C**; weddings happen under it | Romantic, and cheap to build (one ring mesh) |
| **The Houseplant Seed** | A seed from the Keeper's own plant drops in when you plant a grove | Any | Planting a grove | **S**: the next box starts with one "Keeper's Tree", a big tree people gather under | The room behind the box becomes part of their world |

### 2.4 Dug from the soil (the cross-section behind the glass)
| Name | One line | Era / setting | Found | Does | Why it's fun |
|---|---|---|---|---|---|
| **★ The Echo Shard** | A shard of the *previous* box's pottery, found deep in the new box's soil | Box 2 onward, any era | You **dig a channel** and it cuts a soil layer with the shard; it shows in the soil cross-section | **H**: the people wonder who came before; if you look at the old box on the shelf, the matching pot is missing a piece | Boxes become one long history. The soil cross-section finally has a purpose |
| **Fossil Snail** | A stone snail. The camp's elder says it is "a very slow ancestor" | Camp | Digging a channel | **C** | Cheap and charming |
| **Time Capsule** | At the town's peak the people bury a box of their things for "whoever comes next" | Town peak, opened in the next box | The box completes | **H**: the next box's people dig it up mid-village, with a card listing what was inside (filled from real history: names, the top event) | The end of one box becomes a mid-game surprise in the next |
| **The Beetle Shell** | A beetle shell as big as a house; the camp lives in it for a winter | Camp | Digging, or pruning a big overgrowth patch | **C** | Scale, and a funny first house |

### 2.5 Setting-only relics
| Name | One line | Era / setting | Found | Does | Why it's fun |
|---|---|---|---|---|---|
| **Sand-Glass Bead** | The oasis glassmakers make their first bead and realise the sky wall is made of the same thing | Desert oasis, town | First glassmaker's workshop | **H**: the people start to suspect what the glass is | They're slowly working it out |
| **The Water-Judge's Cup** | The cup that measured every family's share of the spring | Desert oasis, village | You dig a channel in the desert | **S**: in the next box, channels reach a little further [est: +1 cell] | Water as fairness; a useful carry-over |
| **Mirage Painting** | An oasis painter paints a city in the sky. It's the Keeper's room, reflected in the glass | Desert oasis, any | A heat-wave event while you watch | **C** | The 4th wall, in paint |
| **★ The Snow Globe** | The mountain people make a tiny snow globe with their own village inside | Mountain village, town | Town era plus a winter event | **C**: on the shelf, tilting the phone makes it snow inside, inside your box | A box in a box. It's the whole game in one object |
| **The Long Horn** | An alphorn so long its end touches the glass. When they blow it, the glass hums | Mountain village, village | A festival in the mountain box | **C**: tapping it on the shelf plays one low note | A sound you can feel |
| **The Goat Statue** | A statue honouring the goat that tried to climb the glass | Mountain village, any | A goat event card | **C** | Every mountain village has one hero goat |

### 2.6 Top 8 relics
1. **The Keeper Doll**: the people's portrait of you, shaped by how you play.
2. **The Sky Crumb**: the first time your room touches their world; a scripted first relic.
3. **The Lens**: they build an observatory and watch you back.
4. **The Echo Shard**: the new box's soil holds the old box's pottery; boxes become one history.
5. **The Snow Globe**: a box inside a box, snowing when you tilt the phone.
6. **The First Spoon**: the founder's heirloom, carried to the next box by their family.
7. **The Left Sandal of Bo Ferris**: pruning weeds finds a decades-old lost thing and returns it to its owner.
8. **The Prize Turnip**: your rain grows the festival's winner.

Why these: each ties to something the player did or someone the player knows, they read at phone size (a crumb, a
doll, a lens, a globe), and together they cover all three eras, carry-over and one new setting.

---

## 3. Power-ups: 18 ideas

"Power-up" here means a **Keeper's gift**: a free, earned ability or effect. None is bought, none speeds up time,
none fixes neglect. Each has a natural limit (the land, the weather, the era), not a timer or a currency.

### 3.1 The ideas
| # | Name | What it does in the sim | How you get it | What the player sees | Pillar risk |
|---|---|---|---|---|---|
| 1 | **★ Keeper's Lamp** | Shine a lamp on one area: crops there grow faster for one season [est: +20 %], and mood rises for people standing in it | Unlocked in the village era (they've started to farm) | A warm cone of light; people walk into it to bask, cats nap in it | Could turn into tap-spam. Limit: one lamp at a time, it stays where you put it until you move it |
| 2 | **★ Rainbow** | After rain, if you're watching, a rainbow appears; the people stop work and hold a small celebration (mood up, a card) | Make it rain during the day while in the app | A rainbow arcs across the box, people point and dance | Low. It's a watcher's bonus, never needed |
| 3 | **Storm** (rain upgrade) | A long rain fills a hollow into a pond (fish, a new food) | Town era, or after rain 20 times [est] | Dark clouds inside the lid, a pond forms | Must not flood or ruin buildings: water only collects in empty hollows |
| 4 | **Orchard** (grove upgrade) | A grove you plant can become fruit trees; the people decide whether to harvest | Village era | Blossom, then fruit; kids climb the trees | Low |
| 5 | **Mill Race** (channel upgrade) | A channel with a drop gives the people a mill site; they may build a mill there | Town era | A small waterfall; a mill appears later, by the people's choice | Keeper, not King: you make the site, they choose. Never auto-place the mill |
| 6 | **Raise and Lower** (terraform) | Push a hill up or press a valley down | Deep-session tool, from the village era | The ground bulges under your finger | Must not move buildings or crush people: only on empty land |
| 7 | **Wildflower Seeds** | Scatter seeds; meadows appear, bees come, the village may start beekeeping (honey) | Village era | Colour spreads across the grass | Low |
| 8 | **Breeze** | Open the lid a crack: clears condensation, dries mud, windmills spin faster for a while | Town era (windmills exist) | Leaves blow, laundry flaps | Must not clean grime for you (the glass is always yours). It clears weather, not mess |
| 9 | **★ Era Festival** | When an era arrives live, the people hold a festival: one game year of higher mood and a "festival relic" chance | Crossing an era threshold while watching (it already waits for you) | Lanterns, music, a parade past the glass | Low. Never time-limited: the era waits, so the festival waits too |
| 10 | **Bright Day** | After a full clean, the people declare a holiday in the clear light; sunlight sits a little above normal for one game year [est: +5 %] | Wipe all the glass when it was at least half grimy [est] | People sunbathe, sing, wave at you | Makes cleaning a treat, not a duty. Risk: becomes a chore if it's too strong. Keep it small and never show a countdown |
| 11 | **★ Apprentice Sweepers** | Later-era sweepers and gardeners appear earlier, because "they learned by watching the Keeper" | Clean a lot in the camp and village eras (no exact number shown) | Little people with brooms copying your wipe motion | Must not sell or skip care. It's the planned "care moves up" rule, earned by playing |
| 12 | **Keeper's Nod** | Tap a notable and give them a nod: their next idea (a craft, a festival, a journey) comes a bit sooner | Once per check-in, from the village era | A tiny glow over their head; a card later says "Inspired by the Keeper's nod, ..." | Close to King. They still pick *what* to do; the nod only adds a push. Keep it to people, never to buildings |
| 13 | **Fireflies** | Summer nights: fireflies over the meadow; couples walk out to see them (more romance stories) | Plant a grove near water | Glowing dots at night | Low |
| 14 | **Gentle Winter** | One mild winter: no slowdown from cold, kids build snowmen | Mountain village setting, once per era | Light snow, snowmen | Low |
| 15 | **★ Founding Bundle** (relic effect) | Pick a few relics from the shelf to go with the founders into the new box; each gives its small start (**S**) or story (**H**) | Completing a box | The founders arrive carrying your chosen relics; the relics show up in the new village | Must stay small so box 2 doesn't feel "won" already. Cap the number |
| 16 | **★ Ancestor's Gift** | A legend's descendant who founds the new box starts with that legend's trait (a great farmer's grandchild farms well) | A legend notable's line carries over | A small badge on the founder; a card naming the ancestor | Low |
| 17 | **Shelf Neighbours** | Two finished boxes on the shelf next to each other trade: a recipe, a festival, a name | Owning two completed boxes | A tiny boat or bird crossing between boxes on the shelf | Could look like the $0.99 unlock sells power. Keep it to stories and looks |
| 18 | **Keeper's Lullaby** | When you close the app at night, the people put out their lanterns and go to bed; the morning card is a little nicer | Any time | Lights go out one by one as you close | Low. Pure goodbye moment; no effect on speed |

### 3.2 Top 6 power-ups
1. **Keeper's Lamp**: a warm patch of light that grows crops and draws people into it.
2. **Rainbow**: your rain plus your attention makes a celebration.
3. **Era Festival**: the live era moment gets a party and a relic chance.
4. **Apprentice Sweepers**: the people learn care from you, so care moves up sooner.
5. **Founding Bundle**: you pick which shelf relics travel to the new box.
6. **Ancestor's Gift**: a legend's trait passes to the founder of the next box.

Why these: two are new things to do in a check-in (lamp, rainbow), two make the big moments bigger (festival,
bundle), and two turn existing systems (care moving up, descendants) into earned rewards. None needs a currency.

### 3.3 Rejected ideas
| Idea | Why not |
|---|---|
| **Time skip / speed-up** ("make 10 years pass now") | Sells or gives away time; makes the hour = year pace meaningless. Pillar 6 |
| **Auto-clean** for a while | The glass is always yours; relief from care would be the thing people pay for. Pillar 6, care rule 6 |
| **Watch an ad for double output** | No ads. Pillar 6 |
| **Daily login gift / streak calendar** | Punishes days away. Pillar 4 |
| **A free boost that expires tonight** (Pokémon GO daily incense) | FOMO |
| **Limited-time event relics** | FOMO; Tamagotchi's "can't re-earn" problem |
| **Relic packs / mystery boxes / rarity tiers** | Gacha |
| **Smite, meteor, plague** (WorldBox) | The civ never collapses; not cozy |
| **Omens and totems** | Dropped at Gate 3 in favour of shaping the land |
| **"Bless a building" or "place a temple"** | Keeper, not King: the player never places buildings |
| **Paid golden glass that raises sunlight** | Pay-to-win, and sells relief from grime |
| **Relics that only drop from mess** | Players would let the box get dirty to farm finds; care becomes a chore |

---

## 4. How it hooks into what exists
- **Chronicle cards.** Every relic is born in a card ("Bo Ferris lost a sandal by the river"), and a later card closes
  the loop ("Found under the weeds 40 years later"). Power-ups are credited the same way the shaping is now ("After
  the Keeper's lamp, ..."). No new UI needed for the moment of finding.
- **Collections (deep session).** A **relic book**: one page per relic, with the people's label, the card that made
  it, the notable it belongs to and the year. Empty slots show a silhouette and a hint in the people's words ("The old
  ones say something fell from the sky once..."), never a checklist with percentages.
- **Shelf museum.** Relics stand on a small plinth in front of the box they came from, ordered by era like the ACNH
  fossil hall. A few are interactive on the shelf (the clock turns, the snow globe snows, the horn plays a note).
- **Carry-over to the next box.** The **Founding Bundle** picks a few relics [est: 3] to travel. Each gives a small
  start (S) or a story (H). Relics that stay behind are never lost; they stay on the shelf. The Echo Shard and the
  Time Capsule make the next box remember the last one even without a choice.
- **The $0.99 unlock.** It opens more boxes and settings, so more chances to make relics and the setting-only ones.
  It never sells a relic, a bigger bonus, a power-up or a drop rate. A free player who only ever has one box still
  gets the full relic and power-up set for the three eras of that box.

---

## 5. Questions for the user
1. **Which relics and power-ups go in the vertical slice?** My suggestion: the Sky Crumb, the Keeper Doll and the Left
   Sandal (one per relic type), plus the Lamp and the Era Festival.
2. **How many relics carry over** to the next box: a fixed few [est: 3], or all of them with only some "active"?
3. **Giant objects from the Keeper's room:** is the comic scale right for the tone? Any objects you'd rule out (the
   hair is the most borderline)?
4. **Should the people slowly figure out the glass** (the Sand-Glass Bead, the Map, the Lens)? It's a running gag that
   could become a quiet storyline across boxes.
5. **Real-world seasons:** should a winter relic only appear in real winter? That's charming, but it's a soft form of
   FOMO. I'd keep seasons in game time only.
6. **The Keeper's Nod:** a nudge on a person, not a building. Is that still "Keeper, not King" for you?
7. **Shelf Neighbours** (finished boxes trading stories): worth it, or does it make the $0.99 unlock look like power?
8. **Relic book hints:** show empty silhouettes with in-world hints, or keep undiscovered relics fully hidden?

---

## Sources
1. Neko Atsume, Wikipedia — https://en.wikipedia.org/wiki/Neko_Atsume
2. Neko Atsume mementos list, GameSkinny — https://www.gameskinny.com/culture/neko-atsume-mementos-list-guide-cats-can-show-love-too/
3. How to get all mementos, Twinfinite — https://twinfinite.net/guides/neko-atsume-how-to-get-all-mementos/
4. Museum (New Horizons), Nookipedia — https://nookipedia.com/wiki/Museum
5. ACNH fossil guide, iMore — https://www.imore.com/animal-crossing-new-horizons-fossil-guide
6. The Museum in Animal Crossing, Play the Past — https://www.playthepast.org/?p=6880
7. Museum, Stardew Valley wiki — https://stardewvalleywiki.com/Museum
8. Stardew artifacts list, Destructoid — https://destructoid.com/stardew-valley-full-artifacts-list-and-where-to-find-them-all
9. Kingdom Two Crowns statues, Magic Game World — https://www.magicgameworld.com/kingdom-two-crowns-statues/
10. Cult of the Lamb corrupted relics, Kotaku AU — https://kotaku.com.au/?p=1364363
11. Cult of the Lamb relics, GamePressure — https://www.gamepressure.com/S013-amp.asp?ID=21757
12. Townscaper walkthrough, TrueAchievements — https://www.trueachievements.com/game/Townscaper/walkthrough
13. Townscaper tips, Supercheats — https://www.supercheats.com/townscaper-cheats-tips
14. Dorfromantik, Wikipedia — https://en.wikipedia.org/wiki/Dorfromantik
15. Dorfromantik beginners' guide, GamePretty — https://gamepretty.com/dorfromantik-beginners-guide-quests-rewards-tile-placement/
16. WorldBox, Wikipedia — https://en.wikipedia.org/wiki/WorldBox
17. WorldBox guide, GamePretty — https://gamepretty.com/worldbox-god-simulator/
18. Viridi, App Store — https://apps.apple.com/us/app/viridi/id1107708818
19. Viridi preview, Kill Screen — https://www.killscreen.com/viridi-promises-capture-soothing-art-potted-plant-care/
20. Tamagotchi souvenir and password FAQ, TamaZone — https://www.tapatalk.com/groups/tamazone/-t3243.html
21. Daily Adventure Incense, Bulbapedia — https://bulbapedia.bulbagarden.net/wiki/Daily_Adventure_Incense
22. Daily Adventure Incense, GoDisageek — https://www.godisageek.com/2022/07/pokemon-go-is-getting-daily-adventure-incense/
23. List of items in Pokémon Sleep, Bulbapedia — https://bulbapedia.bulbagarden.net/wiki/List_of_items_in_Pok%C3%A9mon_Sleep
24. Incense in Pokémon Sleep, PokeCharms — https://pokecharms.com/guides/how-to-use-incense-in-pokemon-sleep
25. Cats & Soup reviews, Justuseapp — https://justuseapp.com/en/app/1581431235/cats-soup/reviews
26. The Gods Must Be Crazy, Wikipedia — https://en.wikipedia.org/wiki/The_Gods_Must_Be_Crazy
27. Earlier research in this repo: [02-idle-design.md](02-idle-design.md) (prestige that keeps relics), [03-aquarium-references.md](03-aquarium-references.md) (pattern 5 "the gift left behind", pattern 20 "watcher's bonus")
