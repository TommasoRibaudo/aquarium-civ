# 10 — Shaping the land: what the Keeper could do, and what to test

Research date: **2026-10-29**. Question from the user: shaping the land (rain, plant a grove, dig a channel) "needs
expanding": "do an agent to research what we could add in the game to test and see."

Context: the Keeper never places buildings (pillar 3, "Keeper, not King"). Gate 3 chose shaping the land over omens
(P5 variant B, "much better and clearer to players"). Today a shaping edits the site grid of research 08 §3.2, the
people answer within 1–3 game years (1 game year = 1 real hour), and the chronicle credits the Keeper. Eras: camp →
farming village → industrious town. New box settings so far: desert oasis, mountain village. Everything is code-built
in the style kit's one palette ([style-kit.md](../design/spikes/style-kit.md)).

Markers: **[V]** read in a primary source (the developer's or publisher's own words, official page). **[S]** secondary
source only (press, wiki, guide, search summary). **[est]** my guess, to measure. §0–§6 give the options and a
recommendation. The decision is the section just below.

---

## Decision (2026-10-29): the toolset per era

Decided by an agent the user delegated it to (#42: "Spawn an agent to research this and make a decision"). It works
inside the user's rules (decisions.md 2026-10-29, "Shaping the land", "Notable roles", "Relics"): two kinds of tool,
animals in, one gesture per tool, undo until the people use a shaping, wishes in cards, setting tools replace standard
ones, free decorations with a small beauty effect, smooth and mist cut. Where this differs from §4, this section wins.
**Not decided here:** the limit model (experiment E2, §5.4) and "name a place" (tested in E1, §5.3).

### The set: 3 new tools per era, plus decorating
| Era | Tool | Kind | The one gesture (in shaping mode) | What it changes | Who reacts (job · **role**) | Natural cap |
|---|---|---|---|---|---|---|
| **Camp** | **Rain** | Gift | Rub in circles over a spot; rub longer for heavier rain | moisture → fertility (08 §3.6) | Gatherer (mushrooms), later farmer · **chief** | One cloud at a time |
| | **Grove** | Gift | Tap open ground | forest | Woodcutter | Forest cover cap |
| | **Herd** (animals) | Gift | Flick in from the box edge. On grass it's deer; flicked into water it's a shoal of fish | game, or fish | Hunter, fisher · **chief** leads the first great hunt | ≤ 2 herds; 1 shoal per water body |
| **Village** | **Channel** | Gift | Drag a line from water; it snaps downhill and routes round lots | waterDist | Farmer · **headman** shares out the water | ≤ 3 channels [est] |
| | **Keeper's Lamp** (= 09 #1) | Gift | Press and drag the pool of light to a spot; let go and it hangs there | sun (local): crops under it ripen faster for a season; people bask | Farmer · **headman** calls a feast | One lamp; moving it is a new use |
| | **Hill & hollow** | Landscaping | Press and pull up (a hill) or push down (a hollow): one drag, its direction picks | height, slope | Kids run up it · the **priest** climbs it; a **miller**'s windmill may choose it; rain fills a hollow into a pond (fisher) | ≤ 3 Keeper hills and hollows |
| **Town** | **Outcrop** | Gift | Press and hold: rock pushes up through the turf. The land picks the kind: stone on a hill, clay by water, ore deep in a slope | deposit | Mason, potter, smith · **master builder**, **guildmaster** | ≤ 3 deposits; they run out over decades |
| | **Spring** | Gift | Tap a hillside: water breaks out and finds its own way down | waterDist (source + stream) | A second well and a new quarter · **mayor** | ≤ 2 per box |
| | **Dye meadow** (flowers, = 09 #7) | Gift | Sweep one stroke across the grass; colour follows the finger | bloom, beauty | Beekeeper, dyer · **guildmaster** (the dyers), **merchant** (honey and dye at the market) | Meadow cover cap |
| **Any time** | **Decorate** | Landscaping, free | Tap a slot in the glass margin or the soil cross-section | beauty (small): people stroll there; a courting spot, weddings | Nobody's job; never a building | 6–10 slots [est] |

- **Gift** = the people answer it with a credited job site, building or harvest; it spends one *use* (below).
  **Landscaping** = changes the look and the layers a little, nothing promised, never spends a use, only its cap.
  The placement scores still read every layer, so a windmill may pick your hill, but nobody owes you that.
- **The tray** has two rows, grouped by kind (WorldBox's lesson, §1.1): gifts (8 by the town) and landscaping (hill &
  hollow, decorate). Older tools stay and level up. That is more than §4.3's 6–7 [est], so E1 checks the tray at phone
  size. Each cap shows on its tray icon (practices §8: caps are stated, not discovered).
- **Unlock pacing** as §4.3: rain in the first session after the first wipe, then grove, then herd once the camp has a
  chief. In the village and the town the first tool comes with the era moment (channel, outcrop), the other two over
  the next ~10 game years [est], each with a one-line hint the first time. Decorate from the first deep session.

### Each tool levels up with the era
| Tool | Camp | Village | Town |
|---|---|---|---|
| Rain | A shower: mushrooms for the gatherers | Steady rain ripens a field; it fills a Keeper's hollow into a pond (fisher) | Storm (09 #3): it fills any hollow into a pond; the **priest** blesses it |
| Grove | Woods for the woodcutter | Tap your own grove and it blossoms into an **orchard** (09 #4) | A timber yard for the **master builder**; the orchard's cider goes to the **innkeeper** |
| Herd | Deer for the hunter; fish for the fisher | Goats and sheep, a pasture fence, a shepherd; the **headman** settles a grazing quarrel | Wool for the weavers (**guildmaster**); a fish stall (**merchant**) |
| Channel | — | Irrigation ditches; the farmers line it with fields | Mill race (09 #5): a little fall where a mill may go; wet trades (dyers, tanners) for the **guildmaster** |
| Lamp | — | A sunbeam on a field | At dusk it hangs over the square as a lantern: the **captain of the watch** starts the night round from it, the **innkeeper** carries tables out |
| Hill & hollow | — | A high place (shrine, windmill); a pond after rain | A lookout or bell-tower site for the **captain of the watch** |

Every role in every era has at least one tool that calls it into view: the chief (rain, herd), the headman, miller and
priest (channel, lamp, herd, hill), and all seven town roles (mayor: spring; priest: storm; innkeeper: lamp, cider;
guildmaster: outcrop, wool, mill race, dyes; master builder: outcrop, timber; merchant: dyes, fish; captain of the
watch: lamp, lookout). The camp's tools stay worth using in the town (The Universim's lesson, §1.2 point 8).

### The limit: built so either E2 arm works
- Every gift spends **one use**. Under E2's arm A a use starts that tool's cooldown (suggested: rain and lamp 2 game
  years; grove, herd, channel, dye meadow 3; outcrop and spring 4 [est]). Under arm B a use takes one shared charge.
  The tool table above is the same in both arms; only the meaning of "a use" changes.
- The natural caps hold in both arms. A level-up never costs extra: turning your grove into an orchard is one grove use.
- Landscaping never spends a use.

### Undo
With the tool selected, tap a feature you made: a "Take back" chip shows while the people haven't used it. **Used**
means a lot, a path (tier ≥ path) or a job site touches it, or a notable has worked it (hunted the herd, picked the
rain's mushrooms, harvested under the lamp). People walking over to look doesn't count. Taking back refunds the use
(the cooldown resets, or the charge comes back), and the chronicle never mentions it. Moving the lamp before anything
under it has ripened is its undo. In E1 the same chip also offers "Name it".

### Wishes in cards
Each gift has an owner in the "who reacts" column, and that notable may **wish** for it in a chronicle card ("Ivo the
hunter dreams of deer on the east meadow"). At most one wish per return [est], never a timer, never a penalty, and the
wish stops once the gift is given.

### Setting tools
A setting tool takes the **slot, era and kind** of the tool it replaces, so the tray, the unlock pacing and both E2
arms are the same in every box.

| Setting | Replaces | With | Era | Kind | The one gesture | Who reacts |
|---|---|---|---|---|---|---|
| Desert oasis | Rain | **Palm spring** (tool 23) | Camp | Gift | Press and hold in a hollow: water wells up and a ring of palms grows | Gatherers (dates) · **chief**; village: date gardens; town: the shared well and the water-judge story (09's Water-Judge's Cup) |
| Desert oasis | Hill & hollow | **Dune shift** (tool 22) | Village | Landscaping | Drag across sand: the dune slides that way and may uncover an old wall or an Echo Shard; it never buries a lot | Kids, the curious · **headman** |
| Desert oasis | Spring | **Desert rain** | Town | Gift | Rub, as rain | The dunes bloom for a season: gatherers, beekeepers · the **priest** leads a rain festival |
| Mountain village | Channel | **Mountain stream** (tool 25) | Village | Gift | Tap a snowfield: it melts into a stream that finds its own way down | Farmer · **miller** (a mill on the fall); town: a sawmill |
| Mountain village | Hill & hollow | **Terraces** (tool 24) | Village | Landscaping | Drag down a slope: it steps into strips with dry-stone edges | Farmers usually take them for fields, nothing promised · no avalanches, ever. ≤ 3 flights |

The oasis turns the water order round: the spring comes first, rain last and rare. Look variants (same tool, same
gesture, a new look, not a replacement): in the oasis the Lamp gives **shade** (crops don't wilt, people rest at
noon), the grove is palms and tamarisk, the herd is goats; in the mountain the herd is goats from the camp on and the
grove is pines.

### The alternatives the user named
| Alternative | Verdict | Why |
|---|---|---|
| **Pond** | Out as a tool, in as a result | Rain fills a Keeper's hollow (village) and the storm fills any hollow (town). The pond is a combination the player discovers (Reus's deer-on-shrubs lesson, §1.1); its own icon would duplicate two tools. The fisher still gets it |
| **Fish** | **In, inside the herd** | "Animals in": flick the herd into water and it's a shoal. The camp already has fishers and a pond. The land picks the kind, as with the outcrop, and it costs no tray icon |
| **Sun / lamp** | **In, village, as the Keeper's Lamp** | Already in the slice (the Relics row), and this doc owns every tool. It takes wind's slot. One-finger drag, because two fingers are the camera and everything must work one-handed (practices §12). In the town it gives the innkeeper and the captain of the watch a night beat |
| **Flood** | Out | Even gentle, it reads as a disaster (§1.4 destruction); it needs a pond first (three steps to learn); once a decade is too rare to learn; its effect (rich soil) is rain's. Revisit for a river-delta setting |
| **Orchard** | **In, as the grove's village level-up** | 09 #4 already calls it an upgrade. Tap your own grove; no tray icon. In the town its cider feeds the innkeeper |

Also out: **wind** (below), **snow** (a game-winter look and 09's Gentle Winter for the mountain box later, not a
tool), **berry patch** (the gatherers already have bushes, and rain gives them mushrooms), **drain** (needs a marsh in
the starting land; revisit with a delta setting). **Smooth** and **mist**: cut by the user.

### Why: the hard calls
1. **Wind out, the Lamp in.** Wind is box-wide, so there's no spot for the answer to land near (Growth Lab's
   criterion 2, §5.2) and nothing to take back. The town no longer has a miller role (the innkeeper took the 7th seat),
   so wind's reactor became a plain job. It also sits close to "the glass is always yours" (09 #8). The Lamp is already
   chosen for the slice, has a spot, a one-finger gesture, and at dusk gives the two town roles nothing else reaches
   (innkeeper, captain of the watch) a beat of their own.
2. **Meadow flowers move to the town, as a dye meadow.** With the Lamp in the village, keeping flowers there would make
   four new village tools and leave the town two. In the town the flowers become woad and madder for the dyers, a
   renaissance craft, so they fit "crafts and guilds", colour the town's cloth and banners, and give the guildmaster
   and merchant a gift of their own. The village loses its bees; the beekeeper arrives with the town.
3. **Fish inside the herd, the pond as a result, the orchard as a level-up.** All three of the user's named
   alternatives that are good ideas get in without adding icons, because the tray is already at its limit (8 gifts by
   the town). The rule: a new icon only for a new kind of change on the land; variations ride on a tool's context
   (water, an existing grove) or on its era.
4. **Setting tools keep the slot and the kind.** Terraces were a gift in §3.7; here they replace hill & hollow, so they
   stay landscaping: the mountain box keeps a free deep-session toy, and E2's arms don't change between boxes.

---

## 0. TL;DR

- **What makes indirect control feel good** (§1.2): the land answers your finger *at once*, the people *look* within
  seconds, and they *build* later, for reasons you can see on the land. Reus is the closest model: giants place
  plants, animals and minerals, and humans settle where the land is good. Townscaper's "one simple gesture, the system
  picks the details" is the right split of authorship.
- **The traps** (§1.4) are well documented. Godus made shaping the chore: layer-by-layer dragging with a finger that
  "just isn't a fine enough tool", a tapped-up currency that you could also buy, and timers. Populous turns flattening
  into building plots, which makes the player the builder. Reus ends up as micromanaging many villages' needs. In From
  Dust the villagers ignore the paths you made.
- **25 candidate tools** in §3: terrain (hill, hollow, smooth), water (channel, spring, pond, flood, drain), weather
  (rain, sun, wind, snow, mist), nature (grove, orchard, meadow flowers and bees, berry patch, herd, fish), materials
  (one outcrop tool, where the land picks stone, clay or ore), decorating the box, and four setting tools (dune shift,
  palm spring, terraces, mountain stream). Each tool feeds one sim layer and one job or role.
- **The key design call** (§2.3): split the tools into **gifts** and **landscaping**. Gifts (rain, a herd, an outcrop)
  change the economy and the people answer them, so they get a cooldown in game years, as P5 did. Landscaping (hills,
  hollows, decoration) is a free deep-session toy on empty land, with a small effect on the layers but no "answer".
  Without the split, a 3-year cooldown leaves a 20-minute deep session (a third of a game year) with nothing to do.
- **Recommended toolset** (§4; **superseded by the Decision above**): **camp** rain, grove, herd · **village** channel, hill & hollow, meadow flowers ·
  **town** outcrop, wind, spring. Each tool levels up with the era (rain → storm fills a pond; grove → orchard; herd →
  pasture → wool for the weavers; channel → mill race; flowers → dye plants for the dyers). Decorating is outside the
  count, free from the start.
- **Test plan** (§5): three cheap experiments on top of Growth Lab. **E1 New tools and attribution:** do players
  notice and credit the answers to herd, hill and outcrop? Presentation A (credit only) vs B (credit plus a named
  notable's reaction). **E2 Limits:** a cooldown per tool vs shared charges refilled by a full wipe. **E3 Gestures:**
  tap-to-place vs one gesture per tool. Growth Lab already proves the *layer → placement* half for rain, grove and
  channel, headless. It has no terrain edits, no new layers, no players, no notables and no touch.

---

## 1. Research: god games and cozy sandboxes with indirect or terrain control

### 1.1 Reference table
| Game | What you shape | How the people answer | Gesture / input | Limit | Lesson for us | Source |
|---|---|---|---|---|---|---|
| **Populous** (1989) | Raise/lower land, a tile at a time | Followers build on flat land; bigger flat area → bigger settlement (a castle needs a wide flat square). More houses → more mana → more powers | Click a tile up or down | Mana from followers | **The clear cause → answer loop** (flat land → houses) works. But it makes the player a plot-preparer: you're building by proxy. Molyneux says the raise-land tool exists because the people couldn't walk around the coast | [Wikipedia](https://en.wikipedia.org/wiki/Populous_(video_game)) [S], [Scientific Gamer](https://scientificgamer.com/bullfrog-time-machine-populous/) [S] |
| **Black & White** (2001) | Miracles (food, wood, water, heal), the creature, the hand | Villagers worship at a site and make prayer power; your influence grows with the village | Mouse gestures drawn on screen | Prayer power | **The hand in the world is a great idea.** But drawn glyphs were "inspired" to some and "clumsy and didn't always work" to others: gesture recognition has to be forgiving | [Pointing device gesture](https://en.wikipedia.org/wiki/Pointing_device_gesture) [S], [MetaFilter 2001](https://metafilter.com/6921) [S], [Giant Bomb](https://www.giantbomb.com/games/3030-18623/) [S], [B&W wiki](https://blackandwhite.fandom.com/wiki/Worship_Site) [S] |
| **From Dust** (2011) | Pick up and drop sand, water, lava ("the Breath") | The tribe settles at totems; you only set destinations, they walk there | Absorb a ball of matter, drop it below the reticle; ball size capped | Ball size; powers from totems | Shaping matter with physics gives awe "when the world works just the way you want it to". **The main complaint: the AI ignores the paths you made.** If the people don't use what you shaped, the player feels cheated | [CGM](https://www.cgmagonline.com/review/game/from-dust/) [S], [GameSpot](https://gamespot.com/reviews/from-dust-review/1900-6325105/) [S], [Digitally Downloaded](https://www.digitallydownloaded.net/2011/08/review-from-dust-xbla-2.html) [S], [Gamereactor](https://embed.gamereactor.eu/from-dust-review/) [S] |
| **Godus** (2013–) | Sculpt land in layers | Followers build houses on flat ground; you tap houses to collect belief | Tap and drag the landscape | Belief (tapped from houses, also bought for cash); upper layers cost more; timers | **What went wrong, below (§1.4).** Molyneux himself said early versions were boring because players were given only two things to do [S: search summary of the interview] | [Pocket Gamer review](https://www.pocketgamer.com/godus/review/) [S, read], [Hardcore Gamer](https://hardcoregamer.com/features/articles/in-which-godus-utterly-fails-at-almost-everything/55688/) [S], [USgamer/Eurogamer interview](https://www.usgamer.net/articles/peter-molyneux-and-the-godus-delusion) [S: 503, search summary], [PG GDC hands-on](https://www.pocketgamer.com/godus/gdc-2014-hands-on-with-peter-molyneuxs-godus/) [S] |
| **Reus** (2013) | Giants make biomes, then place plants, animals and minerals | Humans settle where the land is rich; placed things combine (deer grazing on shrubs gives more food than either alone) | Pick a giant, pick an ability, tap a tile | Giants' abilities and positions; human greed | **The closest model to ours:** you shape the land, they choose to live there. But reviewers call the late game micromanagement of many villages' needs, "a jarringly reined in pace" | [Indie Game Reviewer](https://indiegamereviewer.com/reus-an-indie-game-review/) [S], [PCGamesN](https://www.pcgamesn.com/indie/reus-pc-review) [S], [Nintendo eShop](https://ec.nintendo.com/NZ/en/titles/70010000100262) [S] |
| **WorldBox** (2012–) | Hundreds of god powers: biomes, rain, seeds, fruit bushes, sponge, creatures, disasters | Civs grow on their own; rain helps grass and trees on fertile soil | Pick a power, tap/drag on the map | **None**: no mana, no resources | Free powers work when the *consequence* is the limit. But the toolbox is huge and mostly destruction. Players found seeds oddly filed next to disasters: grouping matters | [Wikipedia](https://en.wikipedia.org/wiki/WorldBox) [S], [Pro Game Guides: nature powers](https://progameguides.com/guides/how-to-use-nature-and-disaster-powers-in-worldbox-god-simulator/) [S], [shaping the world](https://progameguides.com/guides/how-to-shape-the-world-in-worldbox-god-simulator/) [S], [Steam thread](https://steamcommunity.com/app/1206560/discussions/0/4703539236099296474) [S] |
| **The Universim** (2018–) | Rain, lightning, healing, grabbing, meteors | Mostly direct, single-target interventions; the Nuggets carry out your plans | Pick a power, click | God points from worship | **Powers go stale as the civ grows**: "the Nuggets no longer needed me to put out their fires". Our tools should *level up* with the era, not retire | [Boiling Steam](https://boilingsteam.com/the-universim-review/) [S], [Ladies Gamers](https://ladiesgamers.com/the-universim-review/) [S], [GamingOnLinux](https://www.gamingonlinux.com/2020/05/the-universim-gets-toxic-rain/) [S] |
| **Townscaper** (2020) | Add or remove a block, pick a colour | The system "assesses the current shape" and adds the architecture: arches, stairs, gardens in enclosed spaces | Tap | None; "more of a toy than a game"; impossible placements "fail silently" | **One simple gesture, the system picks the details.** A single contextual block becomes land, a house or a garden from its neighbours. Our outcrop tool should work like that | [Game Developer](https://www.gamedeveloper.com/game-platforms/how-townscaper-works-a-story-four-games-in-the-making) [S, read], [Retroware](https://articles.retroware.com/2021/12/24/townscaper-and-the-art-of-minimalism/) [S], [Bloomberg](https://www.bloomberg.com/news/articles/2020-07-24/the-video-game-where-you-build-an-empty-town) [S] |
| **Dorfromantik** (2021–) | Place landscape tiles | No people; quests and new tile types | Place a tile, rotate | Tile stack | Calm pacing ("a serene trance of dropping tiles"); **pulling the camera back to see the whole landscape is the reward**. A touch-first mobile version is announced with a "new gameplay flow" | [OpenCritic](https://opencritic.com/game/11335/dorfromantik) [S], [GamesMarket](https://www.gamesmarket.global/games-and-products/new-platforms-dorfromantik-comes-to-mobile-phones-and-more-consoles-with-the-help-of-clockstone-and-headup-04f6c37bb4b4e2c646fda3aa72551721) [S] |
| **Viridi** (2015/16) | Water succulents in a pot; a snail goes round the rim | Plants grow in real time | Tap the spray bottle; swipe to turn the camera | Over-watering kills plants | Tending "twice or three times a week" feels like a safe haven. **Plant death from over-care is the anti-pattern** | [Wikipedia](https://en.wikipedia.org/wiki/Viridi) [S], [CGM](https://www.cgmagonline.com/review/game/viridi-pc-review/) [S], [MEL](https://melmagazine.com/en-us/story/app-ed-the-slow-grow) [S] |
| **Terra Nil** (2023) | Restore ecosystems with machines, then recycle every machine | Plants and animals come back when conditions are right | Place, then remove | Resources; a Zen mode drops them | **Leaving no trace is satisfying**: "touring the map to recycle your machines feels satisfying". Some steps feel permanent, which reviewers disliked | [Shacknews](https://shacknews.com/article/134777/terra-nil-review-nature-appreciation) [S], [GameLuster](https://gameluster.com/review-terra-nil-rebuilding-the-planet-from-the-ground-up/) [S], [Science Friday](https://www.sciencefriday.com/articles/terra-nil-video-game-climate-change/) [S] |
| **Cities: Skylines (2)** | Shift (raise/lower), level, soften, slope brushes | Zoning builds on what you shaped | Brush with size and strength | Can't shape under buildings or roads; no undo | Four brushes cover all terrain needs. **"Start with low strength, there's no undo"** is a guide warning, a sign that undo matters | [GGRecon](https://ggrecon.com/guides/cities-skylines-2-how-to-terraform) [S], [Prima](https://primagames.com/tips/how-to-terraform-your-city-in-cities-skylines-2) [S] |
| **Spore** (2008) | Space-stage terraforming: atmosphere, temperature, plants, creatures, sculpting, colour | Habitability (T-score) decides how many colonies fit | Tools push a dot on a 2-axis chart | Tool purchases | **A visible state chart** shows how a tool moves you. Colour and sculpting have *no* effect on habitability: cosmetic and sim tools kept apart | [Spore wiki: Terraforming](https://spore.fandom.com/wiki/Terraforming) [S] |
| **Minecraft** villagers | Place a bed and a job-site block (lectern, composter, loom…) | An unemployed villager nearby claims the block and takes that profession | Place a block | — | **One placed thing → one job.** It reads instantly. Our version: one shaped feature → one notable's everyday job (herd → hunter) | [allthings.how](https://allthings.how/minecraft-villager-jobs-and-their-job-site-blocks-explained/) [S], [west-games](https://west-games.com/minecraft-job-site-blocks/) [S] |
| **Tiny Glade** (2024) | Draw walls and paths, raise buildings | The game adds doors where a path meets a wall, beams under raised floors, flags, chimneys | Drag | None; "no wrong answers or failure states. You can change your mind at any time" | **Draw coarsely, the system adds the fine detail.** Same split as Townscaper | [Steam page](https://store.steampowered.com/app/2198150/Tiny_Glade/) [V], [Notebookcheck](https://www.notebookcheck.net/New-building-game-scores-96-positive-ratings-on-Steam-for-its-creative-freedom.897138.0.html) [S] |
| **Prune** (2015, mobile) | Swipe to cut branches; the tree grows toward light | The tree blooms when it reaches the sun | Swipe | Level design | **The best one-gesture touch toy**: a pointing finger shows where to swipe, almost no text | [Boing Boing](https://boingboing.net/2015/07/27/mobile-game-of-the-week-prune.html) [S], [School Library Journal](https://www.schoollibraryjournal.com/story/meditative-gaming-prune-touch-and-go) [S] |
| **GodFinger** (2009, iOS) | Sun, rain, lightning, fire, flood with a finger | Worshippers work and pray | Finger gestures | Social/F2P | An early phone god game built on finger weather. It shut down in 2013 (a service game) | [Wikipedia](https://www.wikipedia.org/wiki/GodFinger) [S], [Macworld](https://www.macworld.com/article/202251/godfinger.html) [S] |

### 1.2 What makes indirect control feel good
1. **Three beats: the land now, the people soon, the answer later.** Populous, Reus and From Dust all answer
   the finger at once (land moves, a biome appears). The people answering later is fine if something visible happens
   in between. P5 already did it (people walk over to look for 20 s). Our three beats: **the land answers in a frame,
   the people come to look within seconds, they build within 1–3 game years** [est: the middle beat matters most in a
   deep session, where the build won't come before the player leaves].
2. **One tool → one layer → one job.** Minecraft's job blocks and Reus's "animals attract hunters" read instantly. Each
   of our tools should change one site-grid layer (research 08 §3.2) and call one everyday job or role into view (herd
   → the lead hunter goes out).
3. **The people use what you made.** From Dust's biggest complaint is villagers ignoring your paths. Growth Lab's
   criterion 2 (an answer within 1.0 u of the spot in ≥ 80 % of seeds) is exactly this guard. Keep it for every tool.
4. **Coarse gesture, fine result.** Townscaper and Tiny Glade take a rough input and add the detail. Godus asked for
   precision a finger can't give. Our tools should **stamp whole features** (a hill, a pond, a meadow), never paint
   cells.
5. **The limit is the world, not a meter.** WorldBox has no mana. Research 09 already set the rule: "powers cost
   nothing but have a natural limit". Godus's belief meter (also sold for cash) is the counter-example.
6. **The state is visible on the land.** Spore shows a habitability chart; Terra Nil shows where conditions are met.
   Our version, true to "the glass is the interface": while a tool is held, the land tints where that layer is high
   (greener grass where it's fertile, darker soil where it's wet) [est: a soft overlay, only in shaping mode].
7. **Change your mind.** Tiny Glade: "no wrong answers". Cities guides warn there's no undo. Terra Nil players disliked
   permanent mistakes. Our shaping should be undoable until the people have used it (§2.4).
8. **Tools grow with the civ.** The Universim's powers stop mattering as the Nuggets advance. Ours should level up per
   era instead (§4.2).

### 1.3 Touch gesture patterns for a phone
Our gestures are already taken: **drag = wipe the glass** (P1), **tap = tidy trash**, **pinch = zoom**, **two-finger
drag = orbit** [est: check against the slice's camera]. So shaping needs a **mode**: open the shaping tray (P5 had one),
pick a tool, and the lid lifts a crack (research 09 §2.3 already uses the open lid). In shaping mode one finger means
the tool and two fingers mean the camera. Closing the tray puts the lid back.

| Pattern | Seen in | Good for | Note |
|---|---|---|---|
| **Tap to place** | P5, Townscaper, WorldBox | Grove, herd, outcrop, fish, decorations | The most reliable. P5's agent playtest passed on real touch |
| **Drag a line** | Tiny Glade paths, P5 channel (tap), Godus | Channel, drain, terraces, wind | Snap the line to the land (downhill for water) so a shaky finger still makes a clean result |
| **Press and hold to grow** | Godus, Cities' strength slider | Hill, hollow, spring, pond | Hold longer = bigger, up to a cap. Show the outline growing. No precision needed |
| **Rub / circle** | Viridi's spray, cleaning in P1 | Rain (rub the cloud for heavier rain), smooth | Close to the wipe: only allowed in shaping mode, or it fights the care gesture |
| **Flick** | Prune's swipe | Herd (flick deer in from the box edge), fish, seeds | Fun, but direction is fuzzy. Land them at the nearest good spot |
| **Drawn glyphs** | Black & White | — | **Avoid.** Recognition fails, and it's a lot to learn |
| **Tilt / shake the phone** | Snow Globe relic (09) | One cosmetic toy at most | Not for tools: it fights holding the phone still, and it's a reach for accessibility (§12 one-handed) |

### 1.4 Traps
| Trap | Seen in | How it shows up for us | Guard |
|---|---|---|---|
| **Shaping becomes the chore** | Godus: "your finger just isn't a fine enough tool", "you always end up picking and dragging bits you didn't want to"; layers you have to peel one by one | A player feels the village won't grow unless they shape every check-in | **The civ grows fine with no shaping at all.** Shaping changes *what* and *where*, and only nudges *how fast* (P5's food ×1.35 for 3 years is the upper end) [est] |
| **Tap-to-collect currency** | Godus belief bubbles, B&W prayer power, Universim god points | A "Keeper energy" you gather by tapping | No currency. Limits are cooldowns, natural caps or nothing (§2.2) |
| **Selling the limit** | Godus: belief "you can buy more of it with real cash"; timers | — | Pillar 6. Never sell charges or cooldown skips |
| **The player becomes the builder** | Populous flattening plots for castles; Cities zoning; Manor Lords plots | A "flatten" tool used to make house sites; the player learns "flat square → house here" | No tool reserves a building or a lot. Flat land invites fields and the green, not a house the player picks. Flag *smooth* as high risk (§3.1) |
| **Servicing needs** | Reus: "micromanaging many pockets of civilization" | Notables demanding water or deer in cards ("We need rain!") | The people can *wish* (a line in a card), never *request* with a timer or a penalty. Wishes are optional hints [est: test it] |
| **Answers that don't come** | From Dust AI ignoring paths | The player digs a channel and nothing happens for hours, or something happens far away | Placement closeness term (08 §3.5) plus the look beat; the chronicle names the place ("beside the new channel") |
| **Too many tools** | WorldBox's huge toolbox; seeds filed with disasters | A tray of 20 icons on a phone | ≤ 3 new tools per era (§4); grouped by kind; setting tools *replace* standard ones, not add |
| **Destruction** | WorldBox meteors, From Dust tsunamis | Flood, avalanche, storm wrecking houses | Water only fills empty hollows and fields; no avalanches; nothing the Keeper does damages a building or hurts a person |
| **Care farming** | (09 §1.3) | Shaping charges refilled by cleaning → let the glass get dirty to bank charges | If charges come from care, refill on *any* full wipe, not by grime amount (E2 tests it) |

---

## 2. Design rules for our tools (from §1)

### 2.1 Every tool must have
1. **A gesture** that fits the shaping mode (§1.3).
2. **One sim layer** it edits: the 08 §3.2 layers (height, slope, waterDist, fertility, forest, wear, occupancy) or a
   new one from the table below.
3. **A visible answer in three beats** (§1.2) and **one job or role that reacts**.
4. **A chronicle line** that credits the Keeper and names a person and a place.
5. **An era** it unlocks in, and how it levels up.
6. **A limit** that isn't a currency.
7. **A safety rule**: it only edits open land (no lot, no green, no path tier ≥ path), never moves a building, never
   hurts anyone. If it can't happen, it says why (practices §4: no silent no-ops; Townscaper's "fail silently" doesn't
   fit a game where people answer).

New layers the candidates need (all pure C#, small grids or scalars, in the 08 `SiteMap`):

| Layer | Kind | Used by |
|---|---|---|
| `game` | grid, 0–1 animal density, drifts toward grass and water, decays slowly | herd |
| `fish` | per water body, 0–1 | fish, pond, channel |
| `bloom` | grid, flowers; bees follow it | meadow flowers |
| `deposit` | grid, kind (stone / clay / ore) + amount | outcrop |
| `moisture` | grid, short-lived (rain, mist, snowmelt) feeding fertility | rain, mist, snow, flood |
| `snow` | grid, seasonal cover | snow, mountain stream |
| `marsh` | grid, wet ground nobody builds on | drain, flood |
| `beauty` | grid, from decorations, flowers, water edges | decorate, flowers |
| `wind`, `sun` | box-wide scalars per season | wind, sun |

### 2.2 Limit models (no currency)
| Model | How | Good | Bad |
|---|---|---|---|
| **A. Cooldown per tool, in game years** | P5: each kind again after 3 game years (= 3 real hours) | Matches the check-in cadence (2–5 a day: about one shaping per check-in); already built and tested | Dead in a deep session: 20 min is a third of a game year, so each tool works once |
| **B. A few shared charges, refilled by care** | e.g. 3 "Keeper's hands"; a full wipe refills one [est] | Ties the two Keeper loops; cleaning gets a reward | Risk of care farming (§1.4); care becomes a means, not a toy |
| **C. Free, natural caps** | The land limits it: max 3 groves, a hill only on empty ground, a spot must "settle" for a season before it's reshaped | Generous; WorldBox-like; best for a deep session | Spam can flood the box with features; the answers stack up and get noisy |
| **D. Free, but answers are rationed** | Shape as much as you like; the people answer one shaping per N years (the newest or the biggest) | Toy freedom without sim spam | Players may not see why one feature got no answer |

### 2.3 Recommendation: two classes of tool
- **Gifts** change the economy and the people answer them with a credited building or job: rain, grove, herd,
  channel, spring, outcrop, fish, flowers, wind, sun. **Model A** (a cooldown per tool, 2–3 game years [est]), plus
  natural caps.
- **Landscaping** changes the look and the layers only a little, with no credited answer: hill, hollow, smooth, decorate.
  **Model C** (free, empty land only, a cap on features). The people still notice (kids roll down a new hill), and the
  placement scores use the layers as usual, so a windmill may later pick your hill, but nothing is promised.

This gives the check-in one meaningful gift and the deep session a free toy. It's the same split Spore makes between
terraforming tools (which change habitability) and colour/sculpt (which don't) [S].

### 2.4 Undo
Until the people have used a feature (a path crosses it, a lot touches it, a herd is hunted), the Keeper can take it
back with the same tool (hold on it → "smooth it away"). After that, it's history. The chronicle never mentions an
undone shaping [est].

---

## 3. Candidate tools (25)

Columns: **Gesture** (in shaping mode) · **Layer** (what it edits) · **The people's answer** (now → soon → later; the
job or role that reacts) · **Chronicle line** · **Era** · **Limit** (G = gift with cooldown, L = free landscaping) ·
**Pillar risk**. "09 #n" = the same idea as power-up n in [research 09](09-relics-and-powerups.md) §3.1. Names in lines
are examples.

### 3.1 Terrain
| Tool | Gesture | Layer | The people's answer | Chronicle line | Era | Limit | Pillar risk |
|---|---|---|---|---|---|---|---|
| **1. Hill** (= 09 #6 raise) | Press and hold on open ground; it bulges up, higher the longer you hold, to a cap | height, slope | Now: kids run up it. Later: placement's "domination" term (08 §3.3) likes it, so the shrine, the windmill or, in camp, the chief's lookout may go there. Chief / priest | "The Keeper raised a hill east of the well. Mara the priest says it was made for the gods." | Village | L: ≤ 3 Keeper hills per box [est] | Medium: if "hill → shrine" is too reliable, it becomes "place a shrine". Keep the answer the people's choice |
| **2. Hollow** (= 09 #6 lower) | Press and hold, then drag down | height, slope | Now: rainwater pools in it after the next rain (pond, tool 6). Later: a sheltered spot; a camp moves its fire there in winter | "Rain filled the Keeper's hollow, and the children named it Duck Pond." | Village | L, same cap | Low |
| **3. Smooth** (flatten) | Rub over rough ground | slope ↓ | Later: fields like flat land; or the green grows | "The Keeper smoothed the stony slope. By spring, Ada Lund's barley grew there." | Town (late) | L | **High: Populous plot-making.** If kept: only on slopes > a threshold, never next to the green, and fields (not houses) score on it first. My lean: drop it, terraces (tool 24) cover the mountain need |

### 3.2 Water
| Tool | Gesture | Layer | The people's answer | Chronicle line | Era | Limit | Pillar risk |
|---|---|---|---|---|---|---|---|
| **4. Channel** (today) | Drag a line from water; it snaps downhill and routes round lots (Growth Lab already does this) | waterDist, occupancy (water) | Now: people walk over, kids splash. Later: fields line it; in the town a watermill and wet trades (08 §3.6); a footbridge where the path crosses. Farmer, miller | "After the Keeper's channel, Ivo Brand dug the first irrigation ditch." | Village | G, 3 years | Low |
| **5. Spring** | Press and hold on a hillside; water wells up and runs downhill on its own | waterDist (a source cell + its stream) | Now: someone fills a jug. Later: a second well and a small cluster of houses (a hamlet) forms there; a wash place. Headman settles disputes about it | "A spring broke from the Keeper's hillside. The Lund family built beside it within the year." | Town (oasis: camp, as tool 23) | G, 4 years; ≤ 2 per box | Low. It may split the village: fine, it's organic |
| **6. Pond** | Press and hold in a hollow until it fills (or: a long rain fills it, 09 #3 Storm) | water cells, fish | Now: ducks, kids swim. Later: a fisher's jetty, reeds for thatch. Fisher | "The Keeper's pond gave Tam the fisher his first catch." | Village | G, 4 years; only in a hollow | Low |
| **7. Flood** (gentle) | Swipe down on the pond: it brims over onto the low meadow for one season | moisture ↑↑, then fertility ↑ (like the Nile) | Now: people move the goats uphill, kids splash. Later: the richest fields in the box on the flood ground. Farmer, headman | "The Keeper let the pond spill over the low meadow. Next year's harvest was the best in memory." | Village | G, once per decade [est] | **Medium: reads as a disaster.** Water never enters a lot or a path; it only covers empty low cells and fields between harvests |
| **8. Drain** | Drag a line from a marsh to the water | marsh ↓, fertility ↑ | Later: the marsh becomes a meadow, then fields. Farmer | "The Keeper drained the reed marsh; old Bo said his grandfather once lost a boot there." | Village | G, 3 years; only where marsh exists | Low. Needs marsh in the starting land (a terrain preset) |

Level-up, not a separate tool: **waterfall / mill race** (= 09 #5): in the town, a channel that crosses a drop gets a
little fall, and the miller may build a mill there.

### 3.3 Weather
| Tool | Gesture | Layer | The people's answer | Chronicle line | Era | Limit | Pillar risk |
|---|---|---|---|---|---|---|---|
| **9. Rain** (today) | Rub in circles: a cloud forms under the lid; rub longer for heavier rain | moisture, fertility +0.4 in 0.8 u, fading over 3–5 years (08 §3.6) | Now: people run indoors, kids stay out. Soon: gatherers pick mushrooms. Later: a field there. Gatherer → farmer | "After the Keeper's rain, Ada Lund planted a new field." | Camp | G, 3 years | Low. Level-up: storm fills a hollow (09 #3) |
| **10. Sun** (= 09 #1 Keeper's Lamp) | Hold two fingers apart over a spot: a warm beam | sun (local), crop speed ↑ for a season | Now: people bask, cats nap, laundry dries. Later: a drying rack, a bench. Gatherer / farmer | "The Keeper's sunbeam ripened the barley a month early; the headman called a feast." | Village | G, 2 years; one beam at a time | Low. In the oasis it's the reverse: give *shade* |
| **11. Wind** (= 09 #8 Breeze) | Swipe across the top of the box: the lid opens and a breeze blows through | wind (box-wide, a season) | Now: leaves blow, laundry flaps, kids fly kites. Later: a windmill on the windward hill; seeds blow and spread meadow flowers. Miller | "The Keeper's wind turned the new sails; Pell the miller ground three harvests in one." | Town | G, 3 years | Must not clear grime or condensation (the glass is always yours, 09 #8) |
| **12. Snow** (= 09 #14 Gentle Winter) | Pinch-sprinkle from the top (two fingers rub together) | snow, then moisture at the thaw | Now: snowmen, sledging, people gather at the fire for stories. Later: a meltwater pond; a good spring. Chief tells stories (camp) / priest (village) | "Under the Keeper's snow the camp told stories all winter; Ivo's tale of the deer is still told." | Any, winter only | G, once per game winter | Low. Never kills crops or people; no cold slow-down |
| **13. Mist** | Slow two-finger drag along the low ground | moisture (low cells), a `mystery` flag | Now: the low meadow goes soft and grey for a morning. Later: mushrooms; a ghost-story card; foragers. Gatherer | "Mist lay on the Keeper's meadow. The children swore they saw the Keeper's eye in it." | Camp | G, 2 years | **Medium-high: it hides the world**, close to condensation (a mess). Keep it low, short, and never on the glass. Lean: cut |

### 3.4 Nature
| Tool | Gesture | Layer | The people's answer | Chronicle line | Era | Limit | Pillar risk |
|---|---|---|---|---|---|---|---|
| **14. Grove** (today) | Tap open ground; saplings pop up and grow over a year | forest | Later: a woodcutter's camp / lumbermill at its edge, a path to it (08 §3.6). Woodcutter | "Beside the Keeper's grove, Ivo Brand built a woodcutter's hut." | Camp | G, 3 years; forest cover cap | Low |
| **15. Orchard** (= 09 #4) | Tap a grove, then "blossom" | forest → fruit (a sub-kind) | Now: blossom; kids climb. Later: a cider press or fruit market stall. Gatherer → farmer; merchant (town) | "The Keeper's grove flowered. Every autumn since, the Brands have pressed apples." | Village | G, 3 years; only on an existing grove | Low |
| **16. Meadow flowers** (= 09 #7) | Flick seeds across the grass; colour spreads | bloom, beauty | Now: bees, butterflies, picnics. Later: hives and a beekeeper; honey festivals; in the town, dye plants for the dyers. Beekeeper, dyer | "Where the Keeper scattered flowers, Lia kept the first hives." | Village | G, 2 years | Low |
| **17. Berry patch** | Tap the edge of the woods | forage (fertility sub-layer) | Now: gatherers go out with baskets. Later: a drying rack, a path. Gatherer | "The Keeper's berries fed the camp through the long winter." | Camp | G, 2 years | Low. Fades in the village unless it becomes a garden (people's choice) |
| **18. Herd** (wild animals) | Flick from the box edge: a few deer (camp) or goats/sheep (village+) trot in | game | Now: the lead hunter crouches and stalks; kids watch. Later: a hunting camp at the herd's range, hides on drying racks; in the village a pasture fence and a shepherd; in the town wool for the weavers. Lead hunter → shepherd → weavers' guild | "When the Keeper sent the deer, Ivo the hunter led the camp's first great hunt." | Camp | G, 3 years; ≤ 2 herds | **Medium: hunting means animals die**, and the art needs code-built animals. Keep the kill off-screen (they come back with a hide) [est] |
| **19. Fish** | Flick a shimmer into the water | fish | Now: ripples, a heron. Later: a jetty, nets drying, a fish stall. Fisher | "The Keeper's fish filled the pond; Tam's nets have never been empty since." | Camp | G, 3 years; needs water | Low |

### 3.5 Materials
| Tool | Gesture | Layer | The people's answer | Chronicle line | Era | Limit | Pillar risk |
|---|---|---|---|---|---|---|---|
| **20. Outcrop** (one tool, the land picks the kind) | Press and hold: rock pushes up through the turf. On a hill it's **stone**, by water it's **clay**, deep in a slope it's **ore** (Townscaper's contextual block) | deposit (kind, amount) | Now: someone taps it with a tool. Later: stone → a quarry, and stone houses replace timber nearby; clay → a potter's kiln; ore → a smithy. Master builder, potter, smith; guildmaster | "Where the Keeper raised grey stone, Hal the mason cut the first stone house." | Town (clay from the village) | G, 4 years; ≤ 3 deposits; runs out over decades | Medium: it changes the look of the town a lot. That's the point, but don't let one tap pick the look of every house: only lots within ~1 u switch material [est] |

### 3.6 Decorating the box
| Tool | Gesture | Layer | The people's answer | Chronicle line | Era | Limit | Pillar risk |
|---|---|---|---|---|---|---|---|
| **21. Decorate** | Tap a slot in the glass margin (the 0.25 u no-build edge, 08 §3.3) or in the soil cross-section: moss, a driftwood log, pebbles, a shell, a mossy stone, a tiny ruin | beauty (margin and soil only) | Now: people stroll to it; kids play on the log. Later: a courting couple's spot, weddings by the shell. Never a building | "Every summer the young people of the village walk to the Keeper's shell." | Any (from the first box) | L, free; slots fill (≈ 6–10 [est]) | Low in play. **Business risk:** decorations are the obvious "small unlockable" later. Selling looks is allowed by §10, but it must never touch the sim |

Extra, not a tool: **name a place.** Long-press a feature you made, type a name; the chronicle uses it ("by
Granny's Hill"). Free, any era. It makes the credit line yours [est: cheap, try it in E1].

### 3.7 Setting tools (they replace a standard tool in that box)
| Tool | Setting | Gesture | Layer | The people's answer | Chronicle line | Era | Limit | Pillar risk |
|---|---|---|---|---|---|---|---|---|
| **22. Dune shift** (replaces hill/hollow) | Desert oasis | Drag across sand: the dune slides that way | height (sand cells only), a `cover` flag | Now: something is uncovered (a stone, an old wall, an Echo Shard from 09). Later: the dune shelters a new quarter from the wind; a windbreak wall. Headman | "The Keeper moved the great dune and uncovered the old wall; the Sarra family built in its lee." | Village | L, free; only sand | Low. The burying direction never covers a lot |
| **23. Palm spring** (replaces spring; earlier) | Desert oasis | Press and hold in a hollow: water wells up and a ring of palms grows | waterDist, forest (palm) | Later: date gatherers, a shaded market, the water-judge role (09's Water-Judge's Cup). Gatherer; water-judge | "At the Keeper's spring the Sarras planted twelve palms and swore to share the water." | Camp | G, 4 years; ≤ 2 | Low |
| **24. Terraces** (replaces smooth) | Mountain village | Drag down a slope in a zigzag: it steps into flat strips with dry-stone edges | slope ↓ in strips, fertility ↑ | Later: terrace fields, stone walls, a goat path. Farmer, master builder. **No avalanches** in the mountain box at all | "The Keeper stepped the south slope; the Alders have farmed its terraces for five generations." | Village | G, 3 years | Medium: the same plot-making risk as smooth, but terraces only take fields |
| **25. Mountain stream** (replaces channel) | Mountain village | Tap a snowfield: it melts into a stream that finds its own way down | snow ↓, waterDist | Now: a goat drinks. Later: a sawmill or a watermill on the fall, a footbridge. Miller | "The Keeper's stream came down the mountain; Pell built his mill where it fell." | Village | G, 3 years | Low |

### 3.8 Where this overlaps research 09
| 09 power-up | Here | Proposal |
|---|---|---|
| #1 Keeper's Lamp | Sun (10) | **The Keeper's Lamp, a village gift** (Decision). 09's name; one-finger drag, not two fingers; in the town it hangs over the square as a dusk lantern. In the oasis it gives shade |
| #3 Storm | Rain level-up, pond (6) | Rain's town level-up: it fills a hollow into a pond. The pond is not a tool |
| #4 Orchard | Orchard (15) | The grove's village level-up (tap your own grove), not a tray icon |
| #5 Mill Race | Channel level-up | The channel's town level-up |
| #6 Raise and Lower | Hill (1), hollow (2) | **Hill & hollow**, village landscaping, **one gesture** (pull up or push down) |
| #7 Wildflower Seeds | Meadow flowers (16) | **Moved to the town as the dye meadow** (woad and madder for the dyers; bees too) |
| #8 Breeze | Wind (11) | **Cut** (Decision, hard call 1): no spot to credit, the town has no miller role, and the glass stays the Keeper's |
| #13 Fireflies | — | Stays a 09 watcher's bonus (grove near water), not a tool |
| #14 Gentle Winter | Snow (12) | Not a tool. Stays a 09 idea for the mountain box's game winters, later |
| #2 Rainbow, #9 Era Festival, #10 Bright Day | — | Watcher's bonuses that follow a tool; not tools |

**Decided** (decisions.md 2026-10-29, "Shaping the land"): research 09 §3 keeps the *rewards* (rainbow, festival,
sweepers, carry-over); this doc owns every *shaping tool*. Where 09 calls something a power-up that is really a tool,
the tool definition here wins.

---

## 4. Recommended toolset per era

The recommendation before the decision. The decided set is in **Decision (2026-10-29)** at the top.

### 4.1 The set (about 3 new per era)
| Era | New tools | Why these | Jobs and roles they show |
|---|---|---|---|
| **Camp** | **Rain**, **grove**, **herd** | Rain and grove are built and tested (P5). The herd gives the camp's one role, the chief, and its main job, the hunter, something the Keeper caused | Gatherer (after rain), woodcutter (grove), lead hunter (herd), chief (the camp's hunt) |
| **Farming village** | **Channel**, **hill & hollow** (one tray icon, two gestures), **meadow flowers** | Water for farms is the village's story; the hill gives the priest and the windmill a high place; flowers bring the first new trade (bees) and festivals | Farmer, beekeeper; headman, miller, priest |
| **Industrious town** | **Outcrop**, **wind**, **spring** | Stone, clay and ore are what crafts and guilds are made of; wind drives the mills; a spring grows a second quarter | Mason, potter, smith, weaver; master builder, guildmaster, merchant |
| Any era | **Decorate** (free, landscaping) | The deep session's toy from day one | — |

Kept as candidates, not in the first set: pond and fish (they follow from a hollow and a channel anyway), flood, drain,
sun/lamp, orchard (a level-up), snow (setting/season), berry patch, mist (lean: cut), smooth (lean: cut).

### 4.2 Each tool levels up with the era
| Tool | Camp | Village | Town |
|---|---|---|---|
| Rain | A shower: fertility, mushrooms | Steady rain: a field ripens | Storm: fills a hollow into a pond (09 #3) |
| Grove | Woods for the woodcutter | Orchard (09 #4) | Managed woodland: timber for the master builder |
| Herd | Deer for the hunter | Goats/sheep: a pasture and a shepherd | Wool for the weavers' guild |
| Channel | — | Irrigation | Mill race and wet trades (09 #5) |
| Hill & hollow | — | A high place, a pond | A lookout or bell tower site |
| Flowers | — | Bees and honey | Dye plants for the dyers |

So the camp's tools are still worth using in the town (§1.2 point 8), and each level-up is a visible "they've learned
to use your gift better" beat, credited in a card.

### 4.3 Unlock pacing
- **First session:** rain only, offered after the first wipe (the slice's first run: "name your box, first wipe, first
  shaping"). The grove in the second check-in, the herd once the camp has a chief [est: year 3–6].
- **New tools arrive with the era moment** (P6: eras wait until you watch). The people's new skill *is* the unlock:
  "The villagers have learned to dig. The Keeper can dig channels now." One tool at the era moment, the second and third
  over the next ~10 game years [est], each with a one-line hint the first time (practices §13).
- **Setting tools** replace their standard tool from the start of that box (an oasis has dune shift instead of hill &
  hollow), so a new box feels different on day one without a longer tray.
- The tray never shows more than **6–7 tools** [est]; older tools stay and level up rather than leaving.

---

## 5. Test plan: what to put in a cheap spike

### 5.1 What Growth Lab already covers (branch `vs/growth-lab`, `Assets/Spikes/Growth/`)
| Covered | Not covered |
|---|---|
| Rain, grove and channel as **layer edits** (`GrowthWorld.Shape`: rain list → fertility; grove → forest cells; channel → Dijkstra from the pond round lots and the green, with bridges) | **Terrain edits**: height and slope are static per box (`SiteMap` builds them once from `TerrainSpec`); hill, hollow, terraces, dune need a recompute |
| The **answer lands near the spot** (`GrowthChecks.RunNudgeTrial`, criterion 2) | **New layers**: game, fish, bloom, deposit, moisture, snow, marsh, beauty |
| A **scripted Keeper** in the time-lapse (`DefaultScript`), open-spot snapping like a player | **Players**, touch, gestures, the shaping mode and tray |
| **Live = catch-up** fingerprint, sanity checks | **Notables and jobs** reacting (Growth Lab has buildings, not people) |
| Three terrain presets | **Limits** (cooldown, charges, caps), **undo**, the three-beat presentation, chronicle wording |

So Growth Lab is the right *engine* for the experiments: add layers and tools to `SiteMap` / `GrowthWorld`, and the
placement answer comes for free. The experiments add a player on top.

### 5.2 Step 0 (headless, agent-only, before any human test)
For each new tool (herd, hill, outcrop at least): extend Growth Lab's criterion 2. **Pass:** in 30 seeded runs the
answer building or job site lands within 1.0 u of the shaped spot in ≥ 80 %, and live = catch-up still holds. If this
fails, no human test will rescue the tool.

### 5.3 Experiment E1: new tools and attribution
- **Question:** with six tools (rain, grove, channel + herd, hill & hollow, outcrop), do players notice the answer,
  credit it to their shaping, and still feel they nudged rather than built?
- **Build:** one portrait scene, style kit, Growth Lab's sim at lab speed (×60, a real minute is a game year as in
  P5), the shaping tray with tap-to-place for all six (gestures are E3's question). A minimal notable view: the job that
  reacts walks to the spot with a name label. Debug panel: each tool, answer now, reset cooldowns, skip 10 years (per
  CLAUDE.md).
- **A/B (within-subject, like P5):** **A** = today's credit (banner + "The Keeper's doing!" + a chronicle line).
  **B** = A + the look beat with a named notable ("Ivo the hunter goes to see the deer") + a fly-to on the answer
  + "name a place".
- **Metrics:** uses per tool per session (`nudge` events, as P5); answers seen live vs only in a card
  (`nudge_effect.seen_live`); a recall question after the session: "What came of your herd / hill / stone?" (correct
  attribution %); survey: "I felt I shaped what happened" (P5 Q4) and a new item "I felt I was building the village
  myself" (1–5); free text on which tool they liked most.
- **Pass:** correct attribution ≥ 70 % of answers in B, and B ≥ A on it; every new tool used at least once by ≥ 4 of
  5 testers; "building it myself" median ≤ 2; "I shaped" median ≥ 4.

### 5.4 Experiment E2: the limit model
- **Question:** which limit keeps shaping a gift without making it a chore, and does it bend the care loop?
- **A/B:** **A** = a cooldown per tool (3 game years) for gifts, landscaping free (§2.3). **B** = 3 shared charges,
  one refilled by any full wipe, landscaping free. (Optional **C** = everything free with natural caps, if a third
  arm is cheap.)
- **Run:** diary, not lab: 3 days, real time (1 h = 1 year), the variant alternating by day as in P5's diary plan. Lab
  speed hides the cost of waiting.
- **Metrics:** shapings per check-in (`checkin`, the P5 measure "a nudge in ≥ 60 % of check-ins"); how dirty the glass
  is when they wipe (grime level at wipe start, from the care log): does B make people wait to wipe, or wipe more?;
  survey: "I wanted to shape but couldn't" (1–5), "shaping felt like a chore" (1–5).
- **Pass:** shaping in ≥ 60 % of check-ins; "chore" median ≤ 2; grime at wipe start within ±15 % of the care-only
  baseline (care not distorted). If B wins on fun but distorts care, prefer A.

### 5.5 Experiment E3: gestures
- **Question:** do one-gesture-per-tool controls (hold to raise, drag a channel, rub for rain, flick a herd) feel
  better than tap-to-place, without fighting the wipe and the camera?
- **A/B:** **A** = tap to place everything (P5). **B** = the gestures of §3 inside a shaping mode (lid lifts, one
  finger = tool, two = camera).
- **Metrics:** failed attempts (the gesture did nothing, or did the wrong thing: a wipe, a camera turn), time to first
  successful use of each tool, survey "fun to do" (1–5) and the A/B preference. The tester agent can also measure
  wrong-gesture rates by driving real touch.
- **Pass:** B's failed attempts < 10 %, no accidental wipes or camera turns in shaping mode, B preferred by ≥ 3 of 5.
  If B fails on errors but wins on fun, keep tap-to-place plus *one* signature gesture (rubbing for rain).

### 5.6 Cost and order
E1 first (it decides which tools are worth building). E3 can share E1's build (a switch in the debug panel). E2
needs real days, so it runs as the slice's diary week or alongside it [est: E1 + E3 ≈ 3–4 agent days on Growth Lab;
E2 ≈ 1 day of build plus 3 diary days].

---

## 6. Questions for the user
1. **Two classes of tool?** Gifts (credited, cooldown) vs landscaping (free, no promised answer, for the deep session).
   Or should every tool work the same way?
2. **The limit:** a cooldown per tool, shared charges refilled by cleaning, or free with natural caps? (E2 tests the
   first two.)
3. **The recommended set:** camp rain/grove/herd, village channel/hill & hollow/flowers, town outcrop/wind/spring. Any
   you'd swap in (pond, fish, sun/lamp, flood, orchard)?
4. **Smooth (flatten) and mist:** I lean to cutting both (smooth makes you the plot-maker, mist hides the world). Agree?
5. **Animals:** herds mean hunting (animals die, off-screen) and code-built animals in the style kit. In or out? If
   in, deer only in the camp, then goats/sheep?
6. **Gestures:** one gesture per tool in a shaping mode, or keep P5's tap-to-place? (E3.)
7. **Undo:** may the Keeper take back a shaping until the people use it?
8. **People's wishes:** may a notable *wish* for something in a card ("Ivo dreams of deer"), as an optional hint? Or is
   that already a request and a chore?
9. **Setting tools replace standard ones** (oasis: dune shift for hill & hollow), rather than adding to the tray. OK?
10. **Decorate:** pure looks with a small "beauty" effect, free. Is it also where later small unlockables could live
    (cosmetic only), or keep decorations entirely free?
11. **Name a place:** worth testing in E1?

---

## Sources
1. Populous, Wikipedia — https://en.wikipedia.org/wiki/Populous_(video_game)
2. Bullfrog Time Machine: Populous, Scientific Gamer — https://scientificgamer.com/bullfrog-time-machine-populous/
3. Pointing device gesture (Black & White), Wikipedia — https://en.wikipedia.org/wiki/Pointing_device_gesture
4. Black & White thread, MetaFilter 2001 — https://metafilter.com/6921
5. Black & White, Giant Bomb — https://www.giantbomb.com/games/3030-18623/
6. Worship Site, Black and White wiki — https://blackandwhite.fandom.com/wiki/Worship_Site
7. From Dust review, CGM — https://www.cgmagonline.com/review/game/from-dust/
8. From Dust review, GameSpot — https://gamespot.com/reviews/from-dust-review/1900-6325105/
9. From Dust review, Digitally Downloaded — https://www.digitallydownloaded.net/2011/08/review-from-dust-xbla-2.html
10. From Dust review, Gamereactor — https://embed.gamereactor.eu/from-dust-review/
11. Godus review, Pocket Gamer — https://www.pocketgamer.com/godus/review/
12. In which Godus utterly fails at almost everything, Hardcore Gamer — https://hardcoregamer.com/features/articles/in-which-godus-utterly-fails-at-almost-everything/55688/
13. Peter Molyneux and the Godus delusion (Eurogamer interview reprint), USgamer — https://www.usgamer.net/articles/peter-molyneux-and-the-godus-delusion (503 when fetched; search summary only)
14. GDC 2014 hands-on with Godus, Pocket Gamer — https://www.pocketgamer.com/godus/gdc-2014-hands-on-with-peter-molyneuxs-godus/
15. Godus test, GamePro — https://www.gamepro.de/artikel/godus,3058785.html
16. Reus review, Indie Game Reviewer — https://indiegamereviewer.com/reus-an-indie-game-review/
17. Reus PC review, PCGamesN — https://www.pcgamesn.com/indie/reus-pc-review
18. Reus, Nintendo eShop — https://ec.nintendo.com/NZ/en/titles/70010000100262
19. WorldBox, Wikipedia — https://en.wikipedia.org/wiki/WorldBox
20. WorldBox nature and disaster powers, Pro Game Guides — https://progameguides.com/guides/how-to-use-nature-and-disaster-powers-in-worldbox-god-simulator/
21. How to shape the world in WorldBox, Pro Game Guides — https://progameguides.com/guides/how-to-shape-the-world-in-worldbox-god-simulator/
22. WorldBox Steam discussion (seeds next to disasters) — https://steamcommunity.com/app/1206560/discussions/0/4703539236099296474
23. The Universim review, Boiling Steam — https://boilingsteam.com/the-universim-review/
24. The Universim review, Ladies Gamers — https://ladiesgamers.com/the-universim-review/
25. The Universim gets toxic rain, GamingOnLinux — https://www.gamingonlinux.com/2020/05/the-universim-gets-toxic-rain/
26. How Townscaper works, Game Developer — https://www.gamedeveloper.com/game-platforms/how-townscaper-works-a-story-four-games-in-the-making
27. Townscaper and the art of minimalism, Retroware — https://articles.retroware.com/2021/12/24/townscaper-and-the-art-of-minimalism/
28. The video game where you build an empty town, Bloomberg — https://www.bloomberg.com/news/articles/2020-07-24/the-video-game-where-you-build-an-empty-town
29. Dorfromantik, OpenCritic — https://opencritic.com/game/11335/dorfromantik
30. Dorfromantik comes to mobile, GamesMarket — https://www.gamesmarket.global/games-and-products/new-platforms-dorfromantik-comes-to-mobile-phones-and-more-consoles-with-the-help-of-clockstone-and-headup-04f6c37bb4b4e2c646fda3aa72551721
31. Viridi, Wikipedia — https://en.wikipedia.org/wiki/Viridi
32. Viridi review, CGM — https://www.cgmagonline.com/review/game/viridi-pc-review/
33. The slow grow (Viridi), MEL Magazine — https://melmagazine.com/en-us/story/app-ed-the-slow-grow
34. Terra Nil review, Shacknews — https://shacknews.com/article/134777/terra-nil-review-nature-appreciation
35. Terra Nil review, GameLuster — https://gameluster.com/review-terra-nil-rebuilding-the-planet-from-the-ground-up/
36. Terra Nil, Science Friday — https://www.sciencefriday.com/articles/terra-nil-video-game-climate-change/
37. How to terraform in Cities: Skylines 2, GGRecon — https://ggrecon.com/guides/cities-skylines-2-how-to-terraform
38. How to terraform your city in Cities: Skylines 2, Prima Games — https://primagames.com/tips/how-to-terraform-your-city-in-cities-skylines-2
39. Terraforming, Spore wiki — https://spore.fandom.com/wiki/Terraforming
40. Minecraft villager jobs and job site blocks, allthings.how — https://allthings.how/minecraft-villager-jobs-and-their-job-site-blocks-explained/
41. Minecraft job site blocks, West Games — https://west-games.com/minecraft-job-site-blocks/
42. Tiny Glade, Steam — https://store.steampowered.com/app/2198150/Tiny_Glade/
43. Tiny Glade, Notebookcheck — https://www.notebookcheck.net/New-building-game-scores-96-positive-ratings-on-Steam-for-its-creative-freedom.897138.0.html
44. Prune, Boing Boing — https://boingboing.net/2015/07/27/mobile-game-of-the-week-prune.html
45. Prune, School Library Journal — https://www.schoollibraryjournal.com/story/meditative-gaming-prune-touch-and-go
46. GodFinger, Wikipedia — https://www.wikipedia.org/wiki/GodFinger
47. GodFinger, Macworld — https://www.macworld.com/article/202251/godfinger.html
48. In this repo: [08-organic-settlement-growth.md](08-organic-settlement-growth.md) (site grid, scoring, Growth Lab),
    [09-relics-and-powerups.md](09-relics-and-powerups.md) (power-ups, rejected ideas),
    [results/P5.md](../design/results/P5.md) (the nudge prototype), [game-definition.md](../design/game-definition.md)
