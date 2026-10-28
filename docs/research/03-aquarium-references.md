# 03 — "Living Aquarium" references: caretaking, observing, emergent stories

Research date: 2026-10-28. Scope: the caretaking, observing and emergent-story side of the concept. Idle math and engines are covered in a separate doc.

**Scoping update applied:** the target is **mobile**, the look is a **3D rotatable diorama in glass**, and "aquarium" is a metaphor for a glass terrarium or box on land. Underwater and fish-tank references are kept short and appear only where a mechanic carries over.

Confidence legend: claims with a link were checked against that source during this research. **[mem]** marks well-known facts from general knowledge that I did not re-check this session; verify them before relying on them.

---

## 0. TL;DR

- **No shipped product combines all four pillars:** mobile, 3D glass diorama, autonomous civilization and care/neglect. Nearby products each cover one or two pillars. Idle civ games (CivIdle, Idle Civilization, My Civilization) are menu-and-number games with almost no observation. Terrarium games (Terrarium: Garden Idle, Tiny Terrariums, Terrarium by Traveler Studios) have glass and coziness but no society. God sims (WorldBox, Genesis of a Small God) have autonomy and observation but no idle/offline play or care loop. Ant-farm idlers (*ANT*, A cozy ant farm, PixAnt Farm) are the nearest structural match: an autonomous colony in glass that runs while you're away. All of them are PC-first, and the ones that look strongest are still unreleased.
- **Neglect works best as reversible visible mess, not death.** The modern consensus runs Tamagotchi → Animal Crossing → Neko Atsume → Kinder World/Finch: absence produces things you can see and clean up (weeds, cockroaches, empty food bowls) plus a warm welcome back. Permanent loss is either removed or made slow and warned about well in advance.
- **Observation pays off only when it is curated.** Simulators produce a lot of noise. Successful games surface stories through named individuals, logs and chronicles, and pacing directors (RimWorld storytellers). James Ryan's dissertation argues that curation is the missing piece (see §3).
- **On mobile the core interaction is: open the app → "while you were away" reel → tap a little person → inspect → one or two care gestures → leave.** It has to feel complete in 30–90 seconds and reward longer lingering.

---

## 1. Caretaking & neglect loops

### 1.1 Reference games

| Game | Platform | Care/neglect mechanic | What happens on neglect | Recovery | Lesson for us |
|---|---|---|---|---|---|
| **Tamagotchi** (Bandai, 1996) | Toy | Feed, clean droppings, discipline, medicine | Pet dies. Early Japanese models showed a ghost and gravestone; US/EU versions softened this to an angel or UFO ([Vice](https://develop.vice.com/en/article/the-tamagotchi-cemetery/), [Wellcome Collection](https://wellcomecollection.org/articles/WsT4Ex8AAHruGfWb)) | Start over | Death gave every feeding weight ([M+](https://stories.mplus.org.hk/en/blog/from-the-collections-tamagotchi-1997-hong-kong-collectors-edition/)), but it causes guilt and players quit. Rosstin Murphy contrasts Tamagotchi's intent (attachment) with FarmVille's (monetizing pain) ([Game Developer, 2013](https://gamedeveloper.com/design/tamagotchi-farmville-and-quot-fun-pain-quot-)) |
| **Neko Atsume** (Hit-Point, 2014) | Mobile | Refill the food bowl and place toys. Cats visit while the app is closed and leave fish and mementos | Bowl empties and cats stop coming. Nothing dies | Refill and they return | 10M downloads by Dec 2015 ([Wikipedia](https://en.wikipedia.org/wiki/Neko_Atsume)). The pleasure is in what happened while you were away (who visited, photos for the album). The developers' guiding idea is to be "loosely enjoyable" ([Meta interview](https://www.meta.com/blog/neko-atsume-interview/)). Neko Atsume 2 shipped in Oct 2024 |
| **Viridi** (Ice Water Games, 2015/2016) | PC → iOS/Android | Water succulents in real time, pull weeds, sing to plants to speed growth. A snail circles the pot | Over- or under-watering → "waterlogged" → dies ([Wikipedia](https://en.wikipedia.org/wiki/Viridi)) | Free seedling every week | Pitched as "Tamagotchi with a lot less nagging". The team went F2P because gating plants behind goals made the game "just another chore" ([Threadless](https://blog.threadless.com/theres-a-game-where-you-just-take-care-of-succulents/), [Kill Screen](https://www.killscreen.com/viridi-promises-capture-soothing-art-potted-plant-care/)). **Swipe to rotate the pot on mobile is our exact camera idea at tiny scale** |
| **Kinder World** (Lumi Interactive) | Mobile | Water houseplants, linked to real self-care exercises | **Plants never die**, presented as a feature ([App Store](https://apps.apple.com/us/app/-/id1541796687)) | n/a | More than 2M downloads and about 22k DAU ([FutureFive](https://futurefive.co.nz/story/exclusive-how-mobile-game-kinder-world-is-fostering-mental-wellbeing)). Retention is the main metric, and the guide character nudges players **not** to stay too long ([TrendWatching](https://www.trendwatching.com/innovation-of-the-day/houseplant-game-kinder-world-blends-play-with-science-backed-self-care)) |
| **Finch** | Mobile | A pet bird gains energy from the user's real self-care | No punishment. The bird is happy when you return ([IU blog](https://geneticslab.medicine.iu.edu/blogs/md-student-news/wellness-corner-a-digital-pet-is-your-self-care-best-friend)) | n/a | Absence is met with welcome, not guilt |
| **Animal Crossing: New Horizons** | Switch | Weeds, a house overrun by cockroaches, villagers thinking about moving out | Weeds lower the island rating. Cockroaches fill the house. After about 15 days or more of absence, villagers *ask* to move out but don't leave on their own ([Sportskeeda](https://sportskeeda.com/esports/what-happens-play-animal-crossing-new-horizons-month), [Destructoid](https://destructoid.com/?p=292182)) | Stomp the roaches, pull the weeds, persuade the villager to stay | **The gold standard for neglect.** It is visible, a little funny, fully reversible, and the return trip is a satisfying cleanup session. The weeds were mourned when they were absent ([Kill Screen](https://killscreen.com/rip-animal-crossing-weeds)) |
| **Terrarium: Garden Idle** (Green Panda) | Mobile | Plants in glass jars produce O₂ per second offline. Boosts (fertilizer, watering, repellent) run on cooldowns | No decay | n/a | 4.8★ from about 1.4k reviews on iOS. The main complaint is ad frequency ([App Store](https://apps.apple.com/app/id1401837582), [InkedGaming](https://inkedgaming.com/blogs/news/mobile-phone-game-reviews-by-sarah-terrarium-garden-idle)). Shows a "glass jar on a shelf" look on mobile, but nothing in the jar is alive in a social sense |
| **The Wandering Village** (Stray Fawn) | PC/Switch | A village lives on the back of Onbu, a giant creature you must feed, cure of toxins (Onbu Doctor) and bond with (pet it or harm it) | Health drops, and a hostile or exhausted Onbu can doom the village ([TechRaptor](https://Techraptor.net/gaming/guides/wandering-village-onbu-guide), [Xbox Wire](https://news.xbox.com/en-us/2023/07/21/the-wandering-village-creating-onbu/)) | Treatment buildings | **The world itself is the patient.** That maps directly onto "the glass box needs cleaning" |
| **Insaniquarium / Fishdom / Tap Tap Fish AbyssRium** | PC/mobile | Feed fish, collect coins, decorate the tank (Insaniquarium: fish starve and aliens attack **[mem]**) | Starvation in Insaniquarium **[mem]**. AbyssRium has no decay | n/a | Deprioritized (underwater). AbyssRium shows that a calm idle clicker with a tank to admire can sustain itself on mobile (4.5★ from 24k reviews, [AppPricingLab](https://apppricinglab.com/app/apple/1068366937)) |
| **Chillquarium / Idlequarium** | PC (desktop idle) | Real-time fish growth, feeding, Auto-Feeder Ticket | No cleaning mechanic | n/a | Chillquarium: 93% positive from 5.3k reviews ([Steam](https://store.steampowered.com/app/2276930/Chillquarium/)). Idlequarium: 75% from 240 ([Steam](https://store.steampowered.com/app/3689560/Idlequarium/)). The appeal is that watching is the product. Both skip any cleaning or dirt loop, so they leave that part of the aquarium fantasy unused |

Not re-verified this session: Pou (needs bars with a mild nag) **[mem]**. **"Biotope"** could not be confirmed as a shipped ecosystem game. Ecosystem (Tom Johnson / Slug Disco, full release Nov 2024, about 77% positive from 606 reviews) is an evolving-creature ocean sandbox with no care loop ([SteamPulse](https://steampulse.org/game/1133120), [ModDB](https://www.moddb.com/games/ecosystem/news/ecosystem-swims-into-early-access-on-march-16-2021)). Equilinox, Terra Nil, Niche and Cloud Gardens are relevant mood references (Terra Nil for restoration as the core verb, Cloud Gardens for overgrowth as an aesthetic) **[mem]**.

### 1.2 How neglect matters without punishing harshly (synthesis)

1. **Make decay visible and slightly comic, not lethal.** Animal Crossing weeds and roaches, Viridi weeds. In the glass box this could be algae or grime on the glass, garbage heaps, overgrown vines, sick people coughing, smog inside the lid.
2. **Separate the visible state from the progression state.** Neko Atsume and Kinder World never take away what you've collected. Mess slows growth, but it never deletes buildings or named people unless the player has been gone a very long time and was warned.
3. **Cap the damage over time.** Absence beyond N hours does no additional harm (as in idle offline caps). That avoids the "I'm afraid to open the app" spiral, the Tamagotchi guilt.
4. **Turn the returning visit into a payoff.** The cleanup session should be short and tactile (swipe the glass, flick away a pest, drop medicine) and give a reward burst. Neglect then becomes deferred fun, not debt.
5. **Warn early and give agency.** AC villagers *ask* before leaving. Use push notifications sparingly and in the world's voice ("The river is clogging", "A fever in the east quarter") and only for events that can still be reversed.
6. **The return greeting matters.** Finch's bird is happy you came back. The little people could cheer, look up at the glass, or light a beacon.
7. **Optional hard mode.** RimWorld-style difficulty choice (see §2). Let hardcore players turn on permanent consequences, collapse of the civ or extinction, and keep the default gentle.

---

## 2. Observing autonomous societies, god games, tiny 3D dioramas

### 2.1 Tiny-world / 3D diorama games (priority: how watching works on a small screen)

| Game | Platform | Relevance | Observation design |
|---|---|---|---|
| **Townscaper** (Oskar Stålberg) | PC, **iOS/Android (Oct 2021, $4.99)** ([Pocket Gamer](https://www.pocketgamer.com/articles/087899/townscaper-is-a-relaxing-city-building-game-coming-to-ios-and-android-in-october/)) | A 3D diorama on mobile with excellent results | Self-described "more of a toy than a game", with no goal beyond watching a pretty town grow ([Bloomberg](https://www.bloomberg.com/news/articles/2020-07-24/the-video-game-where-you-build-an-empty-town)). The algorithm makes placed blocks look lived-in ([Edge via Magzter](https://magzter.com/nb/stories/Puzzle-Gaming/Edge/Toy-town)). The main criticism is that the town is empty. **Our game adds the inhabitants Townscaper lacks** |
| **ISLANDERS: Mobile** (Grizzly Games/Coatsink) | iOS/Android, Nov 20 2025, €5.99 premium, no IAP ([appgefahren](https://www.appgefahren.de/tag/islanders-mobile), [App Store](https://apps.apple.com/app/id6745312000)) | A tiny-island diorama works as a premium mobile port | Minimalist palette, an island on a plinth, rotate and zoom |
| **Tiny Glade** (Pounce Light) | PC | More than 500k players in about 2 weeks after its Sept 2024 launch ([WN Hub](https://wnhub.io/news/indie/item-45742)) | A diorama with no management or combat. Life appears in response to building (sheep, lit windows) **[mem]**. Proves the commercial appeal of the miniature look |
| **Dorfromantik** | PC/consoles, mobile in development (announced 2025) ([gamesmarket](https://www.gamesmarket.global/games-and-products/new-platforms-dorfromantik-comes-to-mobile-phones-and-more-consoles-with-the-help-of-clockstone-and-headup-04f6c37bb4b4e2c646fda3aa72551721)) | Tile diorama | Small animated life (trains, boats) rewards zooming in **[mem]** |
| **Pocket City 2** (Codebrew) | iOS/Android, 2023, $4.99 | 3D mobile city with an avatar mode and free roam ([Android Police](https://www.androidpolice.com/pocket-city-2-release/), [TouchArcade](https://toucharcade.com/2023/04/18/pocket-city-2-review-mobile-iphone-ipad-premium-city-builder/)) | "Drop into your city" is a strong pattern for observation-led play. You could follow one citizen instead of controlling one |
| **Tiny Terrariums** (Argent Realms) | PC, Nov 2024 | Glass bowls, tanks and bottles with Scandinavian forest gnomes (Tomtar) who play with what you place. You can switch to a gnome's eye view ([Steam](https://store.steampowered.com/app/2363120/Tiny_Terrariums/)) | **Closest look match** (glass with tiny inhabitants), but tiny reach (4 reviews), no civilization and no idle loop |
| **Terrarium** (Traveler Studios) | PC, Jan 2025 | A bottle-diorama sandbox with a "Play Mode" for viewing ([Steam](https://store.steampowered.com/app/3262910/Terrarium/)) | 80% positive from 15 reviews. Shows demand for the bottle look, not proof of a market |
| **Fallout Shelter** (Bethesda) | Mobile | A side-on "ant farm" cutaway of a vault ([TheSixthAxis](https://www.thesixthaxis.com/2015/06/15/manage-your-own-fallout-shelter-on-ios-tonight/)) | Proves on mobile that watching tiny named people in a cutaway is the hook. Tap a dweller to see stats and lines of dialogue |

**Camera and readability on a phone (patterns plus evidence):**
- **Tilt-shift / miniature faking.** A narrow band in focus, a high viewing angle, and boosted saturation and contrast make a scene read as a scale model ([Wikipedia: Miniature faking](https://en.wikipedia.org/wiki/Miniature_faking)). In-game it focuses attention but costs frame rate and narrows the field of view ([student study](https://brycekerrabraham.myportfolio.com/final-project)). On mobile, prefer cheap fakes: depth-based blur only at max zoom-out, or a vignette plus a gradient blur in screen space.
- **The orbit-pot camera (Viridi).** One-finger swipe rotates around a fixed pivot, two fingers zoom. The glass box is the frame, and clamping the pitch keeps it readable.
- **Zoom-to-inspect.** Tap a person and the camera eases in and follows them, with a nameplate card (name, job, mood, current thought). Fallout Shelter and Pocket City 2 avatar mode are the reference points.
- **Silhouette scale.** People should be readable at about 3–6 px of height when zoomed out. Use exaggerated heads and a color-coded profession hat or tunic so a crowd reads as a society. Emoji-style thought bubbles above heads are cheap and legible.
- **Life signals at every zoom level.** Far: smoke plumes, lit windows at night, flocks, the glass fogging over. Middle: carts moving on roads. Near: individual gestures and arguments.
- **Glass as UI.** Fingerprints and grime on the glass show neglect, condensation shows humidity, and the player cleans by wiping the screen. This is the diegetic form of the care meter, and it is distinctive.

### 2.2 God games and colony sims (attention, story surfacing, control)

| Game | Control level | How attention is rewarded / stories surfaced |
|---|---|---|
| **WorldBox** (Maxim Karpenko) | Indirect, plus sandbox powers | Started on mobile (Android 2018). Steam EA Dec 2021 ([Game Developer](https://www.gamedeveloper.com/press-release/worldbox-early-access-launching-december-2-on-steam)). Estimated about 1.2M Steam copies and 95% positive from 55k reviews ([Raijin](https://raijin.gg/app/1206560/WorldBox__God_Simulator), a third-party estimate). Named kings and wars and an inspector for units, kingdoms and cultures **[mem]**. **The strongest proof that watching tiny pixel civilizations is loved, including on mobile**, but it is pixel 2D, a sandbox with no care loop and no offline growth |
| **Genesis of a Small God** (Pierre Nury) | Indirect: villagers choose what to build. You terraform, gather and perform miracles. Faith depends on food and shelter, and you collect souls to prevent angry ghosts | 79% from 62 reviews ([Steam](https://store.steampowered.com/app/2328710/Genesis_of_a_Small_God/)). A voxel diorama island that is **structurally very close** (caretaker god plus autonomous villagers). PC/VR, no idle mode |
| **RimWorld** (Ludeon) | Direct priorities, emergent outcomes | Framed as a "story generator". AI storytellers (Cassandra: rising tension; Phoebe: more downtime; Randy: random) are modeled on the Left 4 Dead AI Director ([Wikipedia](https://en.wikipedia.org/wiki/RimWorld), [GDC 2017 talk listing](https://gdconf.com/news/gdc-youll-see-rimworld-devs-succeeded-everything-wrong)). **A storyteller preset can stand in for a difficulty setting.** |
| **Dwarf Fortress** (Bay 12) | Indirect designations | World generation produces thousands of historical figures, and Legends mode lets you browse that history ([USC exhibit](https://scalar.usc.edu/works/game-changers-2016-exhibit/dwarf-fortress.13)). Tarn Adams's test for a feature: if you're seeing a little narrative forming, it works ([PC Gamer, 2026](https://www.pcgamer.com/gaming-industry/game-development/dwarf-fortress-creator-says-we-have-a-natural-lean-towards-human-made-stories-but-but-were-all-going-to-be-very-sorely-tested-in-the-coming-years-by-ai/)) |
| **Sapiens** (Dave Frampton) | Order-based, with moods, needs and skills per sapien | Tribes of up to about 100 ([press kit](https://majic-jungle.prezly.com/sapiens-press-kit), [IndieGameFans](https://indiegamefans.com/experience-prehistorical-colony-simulation-with-sapiens/)). Individual skill growth to watch |
| **The Universim** (Crytivo) | God powers, indirect citizens | Civilization across eras on a tiny planet ([Steam](https://store.steampowered.com/app/352720/The_Universim/)). The planet works as a diorama |
| **Godus** (22cans) | Sculpt the land; followers autonomous | Shows the risks in our space: a move to mobile with timers, overpromising, and staff departures ([Kotaku](https://kotaku.com/why-peter-molyneuxs-godus-is-such-a-disaster-1685539932), [MCV](https://mcvuk.com/molyneux-on-godus-i-made-some-horrendous-mistakes/)). Mobile timers on a god game felt like chores **[mem: widely reported]** |
| **Black & White, Populous, SimEarth, SimAnt, Spore, Kenshi, Timberborn, Kingdoms & Castles, Before We Leave** | Varying | **[mem]** B&W: the creature as a proxy you train, which suits "the civ reacts to how you treat it". SimAnt: an ant farm as a god game. Before We Leave: a cozy civ on tiny planets with no combat. Timberborn: beavers handle water, so environmental care is the core skill. Kenshi: the world goes on without you |

**How much control? There is a clear spectrum:** sandbox powers (WorldBox) → indirect influence (Genesis, Godus, B&W) → priorities (RimWorld, DF) → direct orders (Sapiens). The brief ("caretaker, not micromanager") sits at **indirect influence plus environmental care**, closest to Genesis of a Small God and The Wandering Village.

---

## 3. Emergent narrative tools

- **Curation is the missing layer.** James Ryan, *Curating Simulated Storyworlds* (UCSC), [eScholarship, open access](https://escholarship.org/uc/item/1340j5h2). Simulations produce mostly boring events and work at awkward granularity, so a curator (human or AI) has to extract and present the good stories. Emily Short's chapter-by-chapter notes: [ch 1–3](https://emshort.blog/2019/05/21/curating-simulated-storyworlds-james-ryan/), [ch 4–5](https://emshort.blog/2019/05/23/curating-simulated-storyworlds-james-ryan-2/), [ch 6f](https://emshort.blog/2019/05/28/curating-simulated-storyworlds-james-ryan-ch-6f/). **This is the theoretical basis for a "while you were away" highlight reel.**
- **Chronicles / legends.** DF Legends mode (a browsable history of figures, sites and wars). CK3 generates character histories and dynasties **[mem]**. Wildermyth turns procedural heroes into a "legends" roster you carry across campaigns. The developers judge features by whether they lead to more iconic heroes ([Turn Based Lovers interview](https://turnbasedlovers.com/10-turns-interview/with-wildermyth-developer)).
- **Storyteller / director.** RimWorld storytellers pace incidents for narrative arcs rather than pure randomness (see §2.2).
- **Narrative Legos.** Ken Levine, GDC 2014: build narrative from small recombinable pieces ([GDC Vault](https://gdcvault.com/play/1020211/Narrative), [Game Informer](https://gameinformer.com/b/news/archive/2014/03/22/ken-levine-talks-player-participation-and-breaking-away-from-linear-narratives)). Applied here: event templates such as "X fell in love with Y", "the well ran dry" and "a prophet arose" get filled from simulation state.
- **The book.** *Procedural Storytelling in Game Design* (Short & Adams, eds.) includes Tarn Adams on emergent narrative in DF and chapters on The Sims ([Routledge](https://www.routledge.com/Procedural-Storytelling-in-Game-Design-1st-Edition/Short-Adams/p/book/9781138595309)).
- **Named-individual attachment.** Fallout Shelter dwellers, RimWorld pawns, Wildermyth heroes and Neko Atsume's named cats with mementos all work because of a **name + face + one distinctive trait + a remembered event**. Keep a small "notable" cast of about 5–12 people out of a crowd of hundreds, and let the player **pin a favorite** to follow.

**Proposed surfacing stack for this game:**
1. **Ticker** (live, ambient): thought bubbles and tiny floating text in-world.
2. **Daily Chronicle** (on login): 3–7 curated cards ranked by salience (births and deaths of notables, firsts, disasters, milestones, funny incidents), each with "fly camera there". A replay ghost or time-lapse of the box growing is the ideal version.
3. **Book of Ages** (long-term): an era-by-era history the player can scroll, with portraits of notables. Doubles as a retention and screenshot/share surface.
4. **Push notifications** written in the world's voice, rare and actionable.

---

## 4. Gaps & competitor scan

### 4.1 Closest competitors (by pillar overlap)

| Title | Platform / status | Signals | Overlap | What it misses |
|---|---|---|---|---|
| ***ANT*** (Fruitdish) | PC, **planned Feb 2027** | Pre-release ([Steam](https://store.steampowered.com/app/5140560/)) | Five ants in a **glass habitat** growing autonomously, idle income with up to 8h offline, desktop companion, colony grows out of the tank into walls and soil | Ants, not a civilization. Click-boost economy. No care or neglect loop. PC only |
| **A cozy ant farm** | PC, planned Sep 30 2026 | ([Steam](https://store.steampowered.com/app/5188280/)) | Nest on your desk that grows as long as you play | Same as above |
| **PixAnt Farm (Idle)** | PC, Jun 2024 | Watch ants explore the screen autonomously ([SteamSpy](https://steamspy.com/app/2745340)) | Watching an idle colony | 2D pixel, no civilization |
| **Pibcremental** (Arcane Cabinet) | PC, 2026 | About 311 reviews, 84% positive ([Wasdland](https://www.wasdland.com/game/pibcremental-4573090/)) | Grow a tiny-creature **civilization in a giant tree**, idle | Clicker or assignment play, 2D, no care loop |
| **Genesis of a Small God** | PC/VR, 2024 | 79% / 62 | Caretaker god, autonomous voxel villagers | Not idle, not mobile, no glass |
| **WorldBox** | Mobile + PC | Estimated about 1.2M Steam copies, 95% | Watching an autonomous civilization | Sandbox destruction, 2D, no offline growth, no care |
| **Tiny Terrariums / Terrarium** | PC | Very low reach | Glass diorama, tiny inhabitants | No society, no idle play |
| **Terrarium: Garden Idle** / **My Little Terrarium** | **Mobile** | 4.8★ from about 1.4k (iOS) | Glass jars, idle offline production | Plants only, no inhabitants to watch, heavy ads |
| **Idle civ games:** CivIdle (Steam, Dec 2025), Idle Civilization (2015), Civilization Idle (coming soon), **My Civilization: Idle** (Android), **Tiny Tribe** (Android, island idle with workers and offline progress) | PC/mobile | ([CivIdle](https://store.steampowered.com/app/2181940/CivIdle/), [Idle Civilization](https://store.steampowered.com/app/418910/Idle_Civilization/), [My Civilization](https://play.google.com/store/apps/details?id=com.my.civilization.idle&hl=en&gl=US), [Tiny Tribe](https://play.google.com/store/apps/details?id=com.atianyiinteractiveentertainment.tinytribe&hl=en_US)) | Civilization idle progression | Menus and numbers. Little or no observation, no named people, no care loop |
| **Tiny Civilization** (喜人一个) | PC, 2022 | 94% / 205 ([Steam](https://store.steampowered.com/app/2230280/Tiny_Civilization/)) | "Tiny civ" name and tag | Match-3, not idle or autonomous |
| **Desktop idlers** (Rusty's Retirement, Chillquarium) | PC | Rusty's: 100k sold in 5 days, 96% from 7.8k ([TechRaptor](https://Techraptor.net/gaming/news/rustys-retirement-sales), [PCGamesN](https://www.pcgamesn.com/rustys-retirement/high-sales)) | Strong market for "a little world running while I do other things" | On PC, not phones |

Searches for "world in a jar", "kingdom/village/civilization in a bottle" and "terrarium civilization" on mobile turned up **no standalone game**. The phrase is dominated by Minecraft survival maps ([CurseForge](https://www.curseforge.com/minecraft/worlds/world-in-a-jar-remastered)), which incidentally shows the fantasy appeals. A tiny free itch.io game, "Tiny Gods" (caretaker of small gods in glass terrariums), turned up in search, but its page returned 404, so it is unverified. Petit Planet (HoYoverse, a planet-cultivation life sim slated for 2026) is a potential adjacent heavyweight; its release timing could not be confirmed this session.

### 4.2 What the market is missing (opportunity)

1. **A 3D glass diorama with a *living society* on mobile.** Townscaper and Islanders prove the diorama look works on mobile, and WorldBox and Fallout Shelter prove people love watching tiny people. Nobody has combined them.
2. **A care/neglect loop wired to an idle civilization.** Idle civ games have no hygiene, ecology or disease. Care games have no society.
3. **Curated observation.** Every idle civ lacks a "while you were away" story. That is the main way to stand out.
4. **The premium-or-fair mobile lane.** Islanders Mobile (€5.99, no IAP), Townscaper and Pocket City 2 ($4.99) show a premium audience for cozy dioramas. Terrarium: Garden Idle's main complaint is ads. Kinder World monetizes cosmetics only.
5. **Risks:** Godus (timers on a god game felt like chores). Godus and Viridi (gating turns care into a chore). Tamagotchi (guilt causes churn). Thermals and battery on mobile for a constantly simulating 3D scene: simulate offline analytically and render only when the app is open.

---

## 5. Pattern library

1. **Visible, reversible mess.** Neglect shows up as world clutter (grime, weeds, pests, smog), not as numbers dropping. *Ex:* AC:NH weeds and roaches, Viridi weeds. *Apply:* glass grime, trash heaps, vines, sick villagers. Each cleans up in 1–3 gestures.
2. **Capped decay.** Damage stops after a maximum and never deletes core progress by default. *Ex:* Neko Atsume, Kinder World. *Apply:* after about 48h the box plateaus as "struggling", and nothing worse happens.
3. **Ask before loss.** Irreversible events are announced in advance with a chance to intervene. *Ex:* AC villagers moving out. *Apply:* "The western clan plans to leave the box" gives a window to fix the cause.
4. **Welcome back.** Returning is celebrated, not shamed. *Ex:* Finch. *Apply:* people wave at the glass and the chronicle opens with good news first.
5. **The gift left behind.** Autonomous agents produce collectibles while you're away. *Ex:* Neko Atsume fish and mementos. *Apply:* artifacts, inventions and relics the civ makes, stored in a cabinet next to the box.
6. **"While you were away" reel.** A curated digest of salient events with camera jumps. *Ex:* the Ryan curation thesis, RimWorld letters **[mem]**. *Apply:* the Daily Chronicle (§3).
7. **Notables in a crowd.** A small named cast tracked with portraits and histories inside an anonymous crowd. *Ex:* Wildermyth, RimWorld, Fallout Shelter. *Apply:* 5–12 notables, a pin-to-follow favorite, and a legends roster after death.
8. **Storyteller presets as difficulty.** Pacing personalities instead of easy/hard. *Ex:* RimWorld Cassandra/Phoebe/Randy. *Apply:* "Gentle Keeper", "Tides", "Wild Glass".
9. **The world as the patient.** The care target is the container or ecosystem, not individuals. *Ex:* The Wandering Village (Onbu), Terra Nil **[mem]**. *Apply:* the box has health (water, air, soil), and the civ's industry pollutes it, which creates a natural tension between growth and care.
10. **Toy, not game.** Satisfying, goal-free interaction as the core. *Ex:* Townscaper, Tiny Glade. *Apply:* every care gesture (wiping glass, rain, light) gets satisfying sound and particles even when nothing needs fixing.
11. **Orbit-the-pot camera.** Swipe-rotate around a fixed object and pinch to zoom. *Ex:* Viridi mobile. *Apply:* the core camera, with the glass edges as the frame.
12. **Zoom-to-inspect / follow-cam.** Tap an agent to get a card and a camera that follows them. *Ex:* Fallout Shelter, Pocket City 2. *Apply:* person cards showing a thought, job, relationships and their latest chronicle line.
13. **Miniature-faking look.** Tilt-shift blur, a high angle, saturated colors. *Ex:* miniature-faking photography, Tiny Glade and Townscaper aesthetics. *Apply:* a cheap screen-space fake on mobile.
14. **Readable life signals at every zoom level.** Smoke, lights, movement, bubbles. *Ex:* Dorfromantik and Townscaper ambient life **[mem]**. *Apply:* budget an LOD of behavior, not just meshes.
15. **Indirect divine influence.** Influence the environment and let the people decide. *Ex:* Genesis of a Small God, Godus, B&W. *Apply:* rain, sun, seeds, plague cures, a "nudge" (a shrine or omen) instead of build orders.
16. **Faith or reputation as feedback.** The people's attitude toward the caretaker reflects the quality of care. *Ex:* Genesis (faith depends on living conditions), B&W. *Apply:* a "Keeper's reputation" that unlocks gentler miracles, plus statues and murals of the player inside the box.
17. **Diegetic glass UI.** Use the container itself as the status display. *Ex:* (novel; Viridi's pot is close). *Apply:* fog means humidity, smudges mean neglect, cracks mean a crisis, and the lid opening means an expansion.
18. **Expand the container.** Growth extends beyond the original glass. *Ex:* *ANT* (tank → cabinet → walls → underground). *Apply:* the box connects to a second box, a shelf of boxes, or a cabinet of worlds.
19. **Session-shaped care.** A complete loop in 30–90 seconds with optional depth. *Ex:* Neko Atsume, Kinder World's "don't stay too long". *Apply:* open → chronicle → 1–3 cleanups → collect a gift → leave. Lingering can be its own reward (rare events only happen while you watch).
20. **Watcher's bonus.** Some things only happen, or happen faster, while you observe. *Ex:* Viridi singing speeds growth; Rusty's Focus Mode slows growth, the inverse **[source](https://techraptor.net/gaming/news/idle-desktop-farming-sim-rustys-retirement-release-date)**. *Apply:* festivals and rituals visible only live, and a "photo mode" catches rare moments for a collection album (as in Neko Atsume).
21. **Ethical monetization of care.** Never sell relief from neglect guilt. *Ex:* Murphy's "fun pain" critique, Viridi's chore worry, Kinder World's cosmetics-only model. *Apply:* sell boxes, biomes and cosmetics, never "cure the plague now for $".

---

## 6. Curated reference list

**Emergent narrative / curation**
- James Ryan, *Curating Simulated Storyworlds* (dissertation, UCSC). https://escholarship.org/uc/item/1340j5h2
- Emily Short's chapter notes on Ryan: https://emshort.blog/2019/05/21/curating-simulated-storyworlds-james-ryan/ · https://emshort.blog/2019/05/23/curating-simulated-storyworlds-james-ryan-2/
- Short & Adams (eds.), *Procedural Storytelling in Game Design* (Routledge). https://www.routledge.com/Procedural-Storytelling-in-Game-Design-1st-Edition/Short-Adams/p/book/9781138595309
- Ken Levine, "Narrative Legos", GDC 2014. https://gdcvault.com/play/1020211/Narrative
- Tynan Sylvester, "RimWorld: Contrarian, Ridiculous, and Impossible Design Methods", GDC 2017. https://gdconf.com/news/gdc-youll-see-rimworld-devs-succeeded-everything-wrong (also his book *Designing Games* **[mem]**)
- Tarn Adams on simulation and narrative (PC Gamer, Gamescom 2026). https://www.pcgamer.com/gaming-industry/game-development/dwarf-fortress-creator-says-we-have-a-natural-lean-towards-human-made-stories-but-but-were-all-going-to-be-very-sorely-tested-in-the-coming-years-by-ai/ · Bay 12 talk links: https://www.bay12games.com/dwarves/links.html
- Wildermyth developer interview (designing for "iconic heroes"). https://turnbasedlovers.com/10-turns-interview/with-wildermyth-developer

**Care, neglect, real-time**
- Rosstin Murphy, "Tamagotchi, FarmVille and 'Fun Pain'" (Game Developer). https://gamedeveloper.com/design/tamagotchi-farmville-and-quot-fun-pain-quot-
- "The Tamagotchi cemetery" (Vice). https://develop.vice.com/en/article/the-tamagotchi-cemetery/
- Kill Screen on Viridi's design intent. https://www.killscreen.com/viridi-promises-capture-soothing-art-potted-plant-care/
- Kill Screen, "Please don't take the weeds out of Animal Crossing". https://killscreen.com/rip-animal-crossing-weeds
- Kinder World: retention-first wellbeing design (FutureFive). https://futurefive.co.nz/story/exclusive-how-mobile-game-kinder-world-is-fostering-mental-wellbeing
- Neko Atsume developers interview (Meta, 2026). https://www.meta.com/blog/neko-atsume-interview/

**Dioramas, tiny worlds, god games**
- Bloomberg on Townscaper as a "toy". https://www.bloomberg.com/news/articles/2020-07-24/the-video-game-where-you-build-an-empty-town
- The Wandering Village: creating Onbu (Xbox Wire). https://news.xbox.com/en-us/2023/07/21/the-wandering-village-creating-onbu/
- Pocket City 2 developer interview (TouchArcade). https://toucharcade.com/2023/04/11/pocket-city-2-mobile-interview-price-legend-of-zelda-city-builder-game-future-codebrew-games/
- Godus as a cautionary tale (Kotaku, MCV). https://kotaku.com/why-peter-molyneuxs-godus-is-such-a-disaster-1685539932 · https://mcvuk.com/molyneux-on-godus-i-made-some-horrendous-mistakes/
- Noclip documentary on Fallout Shelter (referenced). https://www.gamereactor.eu/noclip-gives-the-details-on-how-fallout-shelter-came-to-be/
- Miniature faking (tilt-shift) basics. https://en.wikipedia.org/wiki/Miniature_faking
- Oskar Stålberg's talks on WFC and Townscaper's grid (search his EPC 2018 / Townscaper talks) **[mem — not verified this session]**

**Market signals**
- Rusty's Retirement, 100k sold in 5 days (TechRaptor). https://Techraptor.net/gaming/news/rustys-retirement-sales
- WorldBox estimates (Raijin). https://raijin.gg/app/1206560/WorldBox__God_Simulator
- Tiny Glade, 500k players (WN Hub). https://wnhub.io/news/indie/item-45742
- Islanders Mobile premium pricing (appgefahren). https://www.appgefahren.de/tag/islanders-mobile
