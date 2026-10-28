# 02 — Idle / Incremental Game Design Research

*Discovery research for the "terrarium civilization" idle builder. Compiled 2026-10-28.*
*Scope update from the user: **mobile (iOS/Android)**, solo **Unity** project, **3D diorama** style, and the "aquarium" is a metaphor for a **glass terrarium on land**. This document leans toward mobile, but the general theory sections stay as originally planned.*

Verification legend: unless marked otherwise, each claim has a cited source that was checked during this research. **[unverified]** marks claims taken from memory or secondary aggregators that I could not confirm against a primary source. Sales figures for Steam and mobile titles are third-party estimates unless the developer disclosed them.

---

## 0. TL;DR for this project

- In an idle game, **the math is the narrative**. Each new system or "paradigm shift" works as a story beat, so the beats need to be designed before the curves are tuned ([Spiel et al., CHI PLAY 2019](https://par.nsf.gov/servlets/purl/10174274)).
- Good idle games move the player **from playing to planning**. Expert players interact less and think more ([Alharthi et al., CHI 2018](https://par.nsf.gov/servlets/purl/10061230)). Our equivalent is moving the player from tending the civ to *shaping* it.
- The "care/cleaning or it degrades" premise is **the single biggest design risk**. It is the FarmVille withering mechanic, which critics called a forced check-in ([Engadget 2010](https://www.engadget.com/2010-03-22-the-fringe-farming-games.html)). The idle genre's moral contract is that waiting does **not** put existing progress at risk ([Alharthi et al.](https://par.nsf.gov/servlets/purl/10061230)). The fix is to make neglect **reduce gains and change the mood of the world, but never destroy progress**, and to cap the effect.
- On mobile, idle players run about **5 sessions/day of about 8 minutes** (GameAnalytics 2018–19 data, [GWO](https://gameworldobserver.com/2020/12/22/gameanalytics-names-benchmark-engagement-metrics-idle-games)). Design the "return moment" (what changed, what the people did) as the main content.
- Monetization players praise: Egg, Inc.'s optional rewarded ads and honest offline caps ([PocketGamer.biz](https://www.pocketgamer.biz/egg-inc-idle-games-gold-standard/)), premium / free-to-try plus a one-time unlock (Melvor, Islanders Mobile, Kittens iOS), and cosmetics (Rusty's Retirement's Supporter Pack had about an 11% attach rate). Players hate paying to skip artificial waits ([Buergi 2024](https://czasopisma.uni.lodz.pl/Replay/article/view/23588)) and gacha/FOMO (the IdleOn complaints).

---

## 1. Foundational theory

### 1.1 Anthony Pecorella (Kongregate): "The Math of Idle Games" + GDC talks

The series was originally posted on Kongregate's developer blog and is mirrored on Game Developer and GameAnalytics. Pecorella produced AdVenture Capitalist mobile at Kongregate. The series was built from his MIGS 2016 talk.

**Part I: growth, cost, generators** ([link](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-i))
- Cost of the next generator: `cost_next = base × rate^owned`. Production: `base_prod × owned × multipliers`. AdVenture Capitalist's Lemonade Stand uses rate = **1.07**, base cost about 4, and production 1.67/s, with ×2 multipliers at 25 and 50 owned.
- The core "seesaw" is **exponential costs vs. linear/polynomial production**. Costs always win eventually, and **multipliers** (milestone bonuses, upgrades) periodically push production back ahead. That oscillation is the pacing.
- There are closed-form formulas for bulk-buy cost and max-affordable (useful for a "buy max" button).
- **Generator relevancy:** if the newest generator dominates on a log scale, older generators become meaningless. Vary the multipliers per generator so priorities keep shifting.
- Spreadsheets: [kon.gg/idle-math-spreadsheets](https://kon.gg/idle-math-spreadsheets)

**Part II: alternative growth models** ([link](https://www.gamedeveloper.com/game-platforms/the-math-of-idle-games-part-ii))
- **Cascading generators** (Gen 2 produces Gen 1, and so on, as in Derivative Clicker and Shark Game): n tiers give `xⁿ/n!`, which approaches but never reaches eˣ. Production stays sub-exponential, so exponential costs still bound it.
- Fix for "cheap lower tiers made for free" irrelevance: tie cost to **manually bought** units and give each manual purchase a small global boost (Derivative Clicker gives +0.05%).
- Encourages multiple currencies and generators that feed each other. This fits a civilization with food, materials, culture, and so on.

**Part III: prestige** ([link](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-iii))
- Prestige currency is commonly based on lifetime earnings (Cookie Clicker, AdCap), max earnings (Realm Grinder), current-run earnings (Egg, Inc.), or upgrades bought (Clicker Heroes).
- Formulas: Cookie Clicker `∛(lifetime/1e12)`; AdCap `150·√(lifetime/1e15)`; Egg, Inc. `(run/1e6)^0.14`. To double prestige currency takes about 8× (Cookie Clicker), 3–4× (AdCap), 4× (Realm Grinder), or about 128× (Egg, Inc.) more earnings.
- Run-independent formulas (Egg, Inc.) produce a "reset at a set point" rhythm. Lifetime-based formulas reward pushing further.
- Egg, Inc. caps offline earnings (2 h in the version analysed), which reduces reliance on idle gains.
- Prestige timing should have fast and slow phases. Multiplier thresholds cause purchase bursts. Spreadsheets are guides, not simulations, so **iterate on feel**.

**Talks**
- *Idle Games: The Mechanics and Monetization of Self-Playing Games*, GDC 2015 ([Vault](https://gdcvault.com/play/1022065/Idle-Games-The-Mechanics-and)): core/meta loops, why retention is high, F2P viability, Kongregate metrics.
- *Idle Chatter: What We Can Learn From Self-Playing Games*, GDC 2016 (follow-up, referenced via [GDC news](https://gdconf.com/news/learn-why-idle-games-click-with-players-at-gdc-europe-2016)).
- *Quest for Progress: The Math and Design of Idle Games*, GDC Europe 2016 ([slides](https://www.slideshare.net/slideshow/quest-for-progress-gdc-europe-2016/65405507)). This is explicitly a design and math talk, not a monetization talk, and it is the basis of the blog series.

### 1.2 Academic literature

**Alharthi, Alsaedi, Toups Dugas, Tanenbaum, Hammer, "Playing to Wait: A Taxonomy of Idle Games," CHI 2018** ([full text PDF](https://par.nsf.gov/servlets/purl/10061230), [Monash record](https://research.monash.edu/en/publications/playing-to-wait-a-taxonomy-of-idle-games/))
- 66 idle games and 10 controls, grounded theory. Idle games are defined as games that **progress without interaction for some period**. *Incremental* games are a subset: generate, wait, spend resources to **automate** generation.
- Incremental subtypes include **Micromanagement** (multiple resources, internal economy, slow, many decision vectors; e.g. Kittens, CivClicker) and **Single-resource** (Cookie Clicker, Clicker Heroes). There is also an **interactivity spectrum** from clicker to zero-player, and most games **shift along it across phases**.
- Key design points:
  - "Playful idling": actions cost resources that are slow to earn and fast to spend, so the longer you're away the more options you have when you return.
  - Idle games **don't penalise absence**. You forgo gains but "typically do[] not place their existing successes at risk." That distinguishes them from FarmVille-style social games, where waiting is a penalty.
  - **Playing at planning**: experts interact less but plan more ("self-obviating play").
  - **Cognitive offloading**: returning is cognitively costly, so add **queues** so players can leave plans for the game to execute.
  - Interfaces: many idle games were "unplayable because the interface was incomprehensible." Decide what information to show and when.

**Spiel, Alharthi, Cen, Hammer, Nacke, Toups Dugas, Tanenbaum, "'It Started as a Joke': On the Design of Idle Games," CHI PLAY 2019** ([PDF](https://par.nsf.gov/servlets/purl/10174274))
- Interviews with the designers of Kittens Game (bloodrizer), Universal Paperclips (Frank Lantz), Spaceplan (Jake Hollands), Cow Clicker (Ian Bogost), and others.
- Themes:
  - (1) Attending to the absurd.
  - (2) **Non-committal invitations to play**: worlds that invite participation but don't require it.
  - (3) **Paradigm shifts as narrative beats**: "you start with a single button and end with an rpg system" (bloodrizer). Rising complexity works as rising action, and Paperclips uses a three-act structure with an interface shift per act.
  - (4) **Care**: ethics, monetisation, sustainability. Many of the designers considered microtransactions unethical for idle games.
  - (5) Personal play.
- Guidelines (directly actionable):
  - Span narrative beats across larger time intervals, which lowers addictive potential.
  - **Design plateaus that encourage temporary disengagement.**
  - Limit transactions per time window, and offer fixed-cost or free play.
  - Be sparse with CPU and battery.
  - **Write the storyline before setting the math**, and let narrative beats drive the functions.
  - Keep variables semantically coherent with the world.
  - Let players disengage at any point **without substantial consequences for the trajectory**.
- Rejects "endlessness" as a defining trait. Idle games can have endings, and **paradigm shifts and resets act as serialised temporary endings**.

**Buergi, "Idle Games: A Cozy Genre Turned Exploitative," *Replay* 1(12), 2024** ([article](https://czasopisma.uni.lodz.pl/Replay/article/view/23588))
- Argues idle games can belong to the cozy genre, but selling real-money shortcuts preys on impatience and undermines their calm appeal.

**Alharthi et al., *The Pleasure of Playing Less* (Play Story Press, 2025)** ([publisher](https://playstorypress.org/2025/05/15/the-pleasure-of-playing-less/)): a book-length study built around a collective playthrough of Kittens Game.

Also relevant: *Progress Wars: Idle Games and the Demarcation of "Real" Games* (DiGRA, [link](https://dl.digra.org/index.php/dl/article/view/851)), and Bogost's Cow Clicker as satire of the forced check-in ([NECSUS](https://necsus-ejms.org/cows-clicks-ciphers-and-satire/)).

### 1.3 Other practitioner writing
- *Numbers Getting Bigger: The Design and Math of Incremental Games* (Envato Tuts+, [link](https://code.tutsplus.com/numbers-getting-bigger-the-design-and-math-of-incremental-games--cms-24023a)): a solid primer.
- *The rise of games you mostly don't play* (Game Developer, [link](https://www.gamedeveloper.com/design/the-rise-of-games-you-mostly-don-t-play)).
- *Lessons of my first incremental game* (Gamasutra postmortem, [link](https://gamasutra.com/design/lessons-of-my-first-incremental-game)).
- **"Eric Guan" essay:** I could not find any idle-design essay under this name in two searches. Either the attribution is wrong or the essay isn't indexed. Ask the user for a link.

---

## 2. Core mechanics & pacing

### 2.1 Growth curves
- Base rule: **costs grow geometrically** (rate about 1.07–1.15 is common; AdCap's first generator is 1.07, Derivative Clicker 1.1), while **production grows polynomially**, with step multipliers at milestones ([Pecorella I](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-i)).
- Cascading production gives near-exponential "acceleration" while staying bounded ([Pecorella II](https://www.gamedeveloper.com/game-platforms/the-math-of-idle-games-part-ii)). A civ maps naturally onto this: farmers → food → population → more workers.
- **Mapping to this project:** don't show the raw numbers as the hero. Express growth physically in the diorama (more huts, then a village, then terraces, then a temple), and keep the numbers in a ledger. The 3D diorama gets denser and more elaborate as the curve climbs, so **visual scale-shift becomes the reward** (this is the Paperclips "scale shift per act" idea from [Spiel et al.](https://par.nsf.gov/servlets/purl/10174274)).

### 2.2 Prestige / reset layers
- Purposes: a ladder-climbing feeling, and keeping numbers manageable ([Pecorella III](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-iii)).
- Layered prestige is the genre's depth engine: Antimatter Dimensions runs Infinity → Eternity → Reality ([Steam](https://store.steampowered.com/app/1399720/Antimatter_Dimensions/)), and Kittens Game has two prestige systems plus challenges ([Steam](https://store.steampowered.com/app/1097410/Kittens_Game/)).
- **Thematic fit for a terrarium:** a reset could be an "age" or "era" ending, such as a flood, an ice age, migration to a new terrarium, or the civilization's "ascension." The player keeps relics, myths, or a seed bank. Each reset should **change rules** (new biome, new constraint), not just multiply. Antimatter Dimensions' challenges and Kittens' challenges ([TV Tropes summary](https://www.tvtropes.org/pmwiki/pmwiki.php/VideoGame/KittensGame)) show that rule-changing runs are what keep resets fresh.
- **Caution for mobile and cozy:** players attached to *their* little people may hate wiping them. Consider **non-destructive prestige**: start a new terrarium on the shelf while the old one stays as a living museum, or keep named "legendary" citizens.

### 2.3 Offline progress
- Standard approach: simulate or approximate the elapsed time on resume, with a **cap**. Egg, Inc. caps offline earnings openly and lets you extend the cap. Its transparency was praised, and absence pauses earnings rather than causing losses ([PocketGamer.biz](https://www.pocketgamer.biz/egg-inc-idle-games-gold-standard/), [Pecorella III](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-iii)).
- For this game, offline progress is the **content**. The player opens the lid to see what happened, so generate an **event log or "chronicle"** of what the people did (births, a festival, a new building, a storm weathered). Spiel et al.'s guideline to make narrative beats key off the math applies here.
- Technical: compute offline time deterministically from a server or trusted clock, guard against clock manipulation, and use a coarse simulation (aggregate rates plus sampled events) rather than ticking every frame. This also follows the "sparse with computational cycles" guideline ([Spiel et al.](https://par.nsf.gov/servlets/purl/10174274)).

### 2.4 Session design & check-in loops (mobile)
- Benchmarks: idle players averaged **5.3 sessions/day and about 8 min/session** (top 10% of games: 5.8 sessions, about 7 min; D1 retention 45.6%), vs. 4.6 sessions and 4.5 min for hyper-casual. This is GameAnalytics data from Q3 2018–Q2 2019 ([GWO summary](https://gameworldobserver.com/2020/12/22/gameanalytics-names-benchmark-engagement-metrics-idle-games)). For 2024 across all genres, the median session was 5–6 min and the top quartile 8–9 min, at about 4 sessions/day ([GameDev Reports](https://gamedevreports.substack.com/p/gameanalytics-benchmarks-in-mobile)).
- Implied layered cadence:
  1. **Glance (10–60 s):** open, see the chronicle and the visual change, collect, do one decision, close.
  2. **Tend (3–8 min):** care tasks, planning, placing buildings, queuing.
  3. **Deep (15+ min, occasional):** prestige or era transition, a new biome, a big unlock.
- "Appointment" mechanics: let the **player set the appointment** (choose a 1 h, 8 h, or 24 h project), not the game. Neko Atsume's appeal is explicitly that it doesn't take much time or energy ([Wikipedia](https://en.wikipedia.org/wiki/Neko_Atsume)). Cats come and go while you're away, and the reward is *discovery on return*, not punishment for absence.
- **Queues** for cognitive offloading ([Alharthi et al.](https://par.nsf.gov/servlets/purl/10061230)): let the player leave standing orders ("build the granary after the wall", "prioritize food in winter").

### 2.5 Unlock pacing, gating, walls
- Paradigm shifts: reveal new systems progressively so the UI itself grows. A Dark Room starts as one button in a dark room and expands into a world map, and it tells its story through environmental cues ([Wikipedia](https://en.wikipedia.org/wiki/A_Dark_Room)). Candy Box and Sandcastle Builder are cited by bloodrizer ([Spiel et al.](https://par.nsf.gov/servlets/purl/10174274)).
- **Walls** are the points where exponential cost overtakes production. Good walls are **plateaus that invite disengagement** (Spiel et al. guideline) or **signposts toward a new system** (prestige, a new resource). Bad walls exist to sell a skip ([Buergi](https://czasopisma.uni.lodz.pl/Replay/article/view/23588)).
- Designers alternate "periods of heavy clicking activity and enforced idleness" to produce an ebb and flow of narrative tension ([Spiel et al.](https://par.nsf.gov/servlets/purl/10174274)).
- Pecorella's tip: vary prestige length, with fast and slow phases.

### 2.6 Automation as progression
- The incremental definition *is* automating your own labour ([Alharthi et al.](https://par.nsf.gov/servlets/purl/10061230)). Early on, the player does things by hand. Later, citizens and institutions do them. In-fiction, the **civ learns to care for itself**: early on you clean the moss and redirect water by hand, and later the people build aqueducts and sanitation. This turns the "care or it degrades" chore into a **progression arc that gradually retires the chore**. The chore never disappears entirely, but it moves upward to higher-level care (culture, diplomacy, disasters).
- Rusty's Retirement simplifies core actions to planting seeds and **placing automation pieces**, which gives min-maxers depth without punishing casual players ([GameDiscoverCo](https://newsletter.gamediscover.co/p/how-rustys-retirement-idle-farmed)).

### 2.7 The "care/degradation" mechanic: specific analysis
- **Anti-pattern:** FarmVille withering. The crop dies after a grace period, and reviewers called it a forced check-in mechanic serving ad revenue ([Engadget](https://www.engadget.com/2010-03-22-the-fringe-farming-games.html), [FarmVille wiki](https://farmville.fandom.com/wiki/Wither?oldid=7231)). Harsh virtual-pet death drives some players away ([virtualpetlist forum](https://www.virtualpetlist.com/threads/worst-virtual-pet-designs-or-mechanics-what-doesnt-work.3080/)).
- **Pro-pattern:** Viridi, a real-time succulent pot that needs occasional watering, sold as "a place you can return to for a moment of peace" ([Wikipedia](https://en.wikipedia.org/wiki/Viridi), [Kill Screen](https://www.killscreen.com/viridi-promises-capture-soothing-art-potted-plant-care/)). Kittens Game has seasonal stakes (you can lose kittens if you neglect catnip before winter), but these come from **in-session planning**, not real-world absence ([Tildes anecdote via search](https://tildes.net/user/MacGuges)) **[unverified detail]**.
- Recommended rules:
  1. Degradation is **bounded**. It plateaus at a "neglected but alive" state and never deletes buildings or people.
  2. Degradation affects **rate and mood**, not stock: grime lowers output and happiness, and the visuals change (fogged glass, overgrowth).
  3. Recovery is **fast and satisfying**: a cleaning session works as a juicy mini-interaction, ASMR-like.
  4. Offer a **vacation mode** or "dormancy," framed as hibernation in the fiction.
  5. Let automation progressively take over the basic care (see 2.6).
  6. **Never monetize the cure** ("pay to clean"). That is the exact pattern Buergi criticises.

---

## 3. Good vs shallow: case studies

The "Looks at" column describes what the player actually watches.

| Game | Platform / model | What it does well | Looks at |
|---|---|---|---|
| **Cookie Clicker** (Orteil, 2013; Steam 2021) | Web free; Steam $4.99, about 2.5M estimated Steam copies ([Raijin est.](https://raijin.gg/app/1454400/Cookie_Clicker); [Wikipedia](https://en.wikipedia.org/wiki/Cookie_Clicker)); about 50k concurrent players days after the Steam launch ([mein-mmo](https://mein-mmo.de/50000-spieler-zocken-game-auf-kekse-klickt/)) | Archetype. Absurdist escalation (grandmas → eldritch), golden-cookie bursts reward attention without requiring it, minigames for layered depth, lifetime-cube-root prestige | One big cookie, a building list, and the building "rows" as animated scenery |
| **A Dark Room** (Doublespeak, 2013) | Web free; premium on iOS/Android/Switch ([Wikipedia](https://en.wikipedia.org/wiki/A_Dark_Room)) | **Narrative via progressive reveal.** Text-only, story through environmental cues, a finite arc with an ending | Text log and growing panels; the log *is* the world |
| **Universal Paperclips** (Lantz, 2017) | Web free; mobile paid | 450k players in 11 days, most to completion ([Wikipedia](https://en.wikipedia.org/wiki/Universal_Paperclips)). Three acts, each with an interface paradigm shift; theme (AI alignment) delivered *through* the systems; it ends | Numbers and buttons, whose changing *kind* is the spectacle |
| **Kittens Game** (bloodrizer, 2014) | Web free; iOS $2.99 with no ads or IAP ([App Store](https://apps.apple.com/us/app/id1198099725)); Steam | Micromanagement depth: 50+ resources, about 200 techs, seasons, religion, two prestige layers, challenges ([Steam](https://store.steampowered.com/app/1097410/Kittens_Game/)). Seasons create real planning; it's a **civilization** idle, the closest ancestor to this concept | Resource table and a village log |
| **Melvor Idle** (Games by Malcs / Jagex Partners) | Premium on Steam (about 424k estimated copies, [Raijin](https://raijin.gg/app/1267910/Melvor_Idle)); **mobile free-to-try plus one-time full unlock, paid expansions** ([wiki mirror](https://dcmpx.remotevs.com/com/melvoridle/wiki/PL/w/DLC), [App Store](https://apps.apple.com/app/id1518963622)); 1M+ players at 1.0 ([Game Developer](https://www.gamedeveloper.com/press-release/melvor-idle-version-10-on-pc-and-mobile-marks-official-end-of-early-access)) | Borrows RuneScape's skill fantasy; long-tail goals; modding (1M mod downloads, [ModDB](https://www.moddb.com/games/melvor-idle/features/melvor-idle-surpasses-1-million-mod-downloads-powered-by-modio)); **a fair mobile monetization template** | Skill progress bars and the mastery grid |
| **Antimatter Dimensions** (Hevipelle, 2016) | Web; Steam F2P since Dec 2022 with an optional shop, about 92% positive ([Steam](https://store.steampowered.com/app/1399720/Antimatter_Dimensions/)) | Deepest layered prestige; challenges change rules; reveals a secret late-game story | Pure numbers; the dimension rows ticking |
| **NGU Idle** (4G) | Steam F2P **[details unverified]** | Many parallel systems, a comedic tone, generous long-term goals | Dense menus |
| **Trimps** (GreenSatellite) | Web; Steam alpha 2022 ([TV Tropes](https://www.tvtropes.org/pmwiki/pmwiki.php/VideoGame/Trimps)) | Combat-plus-housing loop, maps as optional sub-runs, heavy automation unlocks | Population counts and a battle grid |
| **Idle Slayer** (Pablo Leban) | Mobile F2P with ads, Remove-Ads IAP $9.99 (or $3.99/month), gem packs up to $99.99; 4.8★ from about 26k ratings ([App Store](https://apps.apple.com/app/id1526599527)) | Light active play (jumping runner) layered on idle; the "active bonus" is a clear reason to watch | The running character: an **animated avatar** |
| **Legends of IdleOn** (LavaFlame2) | F2P PC/mobile | Huge content breadth, multiple characters working in parallel | Many characters across a world. **Cautionary tale:** recurring player complaints of pay-to-win gems, gacha, and limited-event FOMO ([Steam discussions](https://steamcommunity.com/app/1476970/discussions/0/4146194159904171286), [Giga](https://www.giga.de/games/kostenloses-rpg-stuermt-steam-charts-obwohl-ihr-es-gar-nicht-zocken-sollt--01KC1G85808SB6WFXZFPK5EFA1)) |
| **Egg, Inc.** (Auxbrain, 2016) | Mobile F2P | "Gold standard" monetization: rewarded ads give *small extra* bonuses, never required; piggy-bank IAP; transparent offline cap with no loss for absence ([PocketGamer.biz](https://www.pocketgamer.biz/egg-inc-idle-games-gold-standard/)); run-independent prestige ([Pecorella III](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-iii)) | **The farm diorama**: chickens running into hen houses, delivery trucks. Physical, legible visual feedback for numbers |
| **Idle Miner Tycoon** (Kolibri / Ubisoft) | Mobile F2P with ads and IAP; 150M+ downloads by 2022 ([Kolibri](https://kolibrigames.com/press/kolibri-games-hit-title-idle-miner-tycoon-surpasses-150-million-downloads)) | Mass-market template: managers as automation, clear spatial layout of shafts | Little miners shuttling ore, a cross-section diorama |
| **Spaceplan** (Jake Hollands, 2017) | Paid desktop + mobile ([Spiel et al.](https://par.nsf.gov/servlets/purl/10174274)) | Short, authored, comedic, has an ending; interviewed in CHI PLAY 2019 | A single planet/potato view that changes |
| **Neko Atsume** (Hit-Point, 2014) | Mobile F2P, light IAP and ads; 19M+ downloads ([Wikipedia](https://en.wikipedia.org/wiki/Neko_Atsume)) | **Closest emotional model.** Place items, leave, return to see who visited; photo album; no fail state | A small yard diorama with cats in it |
| **Viridi** (Ice Water Games, 2015/16) | Free, plants cost cents, a weekly free seed ([Wikipedia](https://en.wikipedia.org/wiki/Viridi)) | Real-time care with low stakes; deliberately avoided turning plants into unlock goals ([Kill Screen](https://www.killscreen.com/viridi-promises-capture-soothing-art-potted-plant-care/)) | One pot and a snail: **a single intimate 3D object** |
| **Cats & Soup** (Hidea / Neowiz, 2021) | Mobile F2P; 50M+ downloads ([Pocket Tactics](https://www.pockettactics.com/cats-and-soup/50-million-downloads)), 1.2M DAU at month 1 ([Game Developer](https://gamedeveloper.com/press-release/catssoup-meows-and-slurps-its-way-to-12m-daily-users)) | Cozy idle at mass scale: cats animate stations in a forest diorama, decoration as the meta | Animated cats cooking: **character animation as the payoff** |
| **Usagi Shima** (Jess Yu, solo, Godot, 2023) | Mobile F2P with optional IAP; "3M downloads" claimed in the Steam launch PR ([PR Times](https://prtimes.jp/main/html/rd/p/000000001.000192858.html), [Godot showcase](https://godotengine.org/article/godot-showcase-usagi-shima/)) | **A solo-dev precedent**: place items, bunnies visit, decorate the island | Bunnies on an island |
| **Pocket Build** (MoonBear) | Mobile F2P with an optional subscription; $2M total sales as of 2021 ([MoonBear investors](https://moonbear.com/investors), [App Store](https://apps.apple.com/app/id1293681293)) | Open sandbox diorama building, 4.6★ | Your own 3D build. Note: retention was "below average" by the developer's own admission, so a sandbox without goals doesn't retain |
| **Islanders: Mobile** (Coatsink, Nov 2025) | **Premium $4.99, no ads, IAP, or timers** ([App Store](https://apps.apple.com/app/id6745312000), [droidgamers](https://www.droidgamers.com/news/islanders-mobile/)) | Shows premium cozy builders port to mobile | Minimalist 3D islands |
| **Animal Crossing: Pocket Camp Complete** (Nintendo, Dec 2024) | **Converted from F2P to premium** ($19.99, intro $9.99), removing Leaf Tickets, the subscription, and multiplayer ([Nintendo Life](https://www.nintendolife.com/news/2024/10/animal-crossing-pocket-camp-complete-arrives-on-mobile-this-december), [ACWorld](https://animalcrossingworld.com/2024/10/animal-crossing-pocket-camp-complete-removes-microtransactions-costs-10-launching-in-december/)) | Evidence that a cozy audience will pay up front | Campsite diorama |
| **Pokémon Sleep** (2023) | F2P plus Premium Pass ($9.99/month); about $100M in year one ([VGC](https://videogameschronicle.com/news/pokemons-sleep-tracking-app-has-generated-100m-in-a-year)) | Real-life cadence (sleep) as the appointment; a subscription in a cozy frame | A Snorlax island diorama with visitors |

**Cozy desktop idle wave (PC, a useful signal of audience appetite)**
- **Rusty's Retirement** (Apr 2024, $6.99): a bottom-of-screen farm bar. About 330k Steam copies in about 4 months, about $5 net per unit, and a $4 Supporter Pack with an **11% attach rate**. It had 140k launch wishlists and was strong in Asia ([GameDiscoverCo](https://newsletter.gamediscover.co/p/how-rustys-retirement-idle-farmed)). Wikipedia cites 550k by July 2025 per the developer ([Wikipedia](https://en.wikipedia.org/wiki/Rusty%27s_Retirement)).
- **Bongo Cat** (Mar 2025, free): peak 194,508 concurrent ([Steambase](https://steambase.io/games/bongo-cat)), but the developer said it was **not profitable**, at about $2–4k/month net, and that about half the online players were bots farming tradeable hat drops ([wnhub](https://wnhub.io/news/stores-and-publishing/item-48117)). **Lesson:** CCU from item-farming doesn't equal a business.
- **Ropuka's Idle Island** (Jan 2025, about £2): about 96% positive on about 4.1k reviews per a third-party tracker ([professionalmoron](https://professionalmoron.com/2025/11/16/ropukas-idle-island-game/); review figure via nodal.gg, **[tracker, unverified]**).
- **Tiny Pasture** (CaveLiquid, Feb 2025, about $6): about 92% positive on about 7k reviews; pets walk on your taskbar ([CogConnected](https://cogconnected.com/2025/02/tiny-pasture-is-launching-today-on-steam/), [Notebookcheck](https://www.notebookcheck.net/Cozy-new-game-has-pets-strolling-on-your-desktop.963357.0.html)).
- **Chill Corner** (Dec 2021, free with paid music DLC): about 91% positive on about 9.6k reviews; a lo-fi room you furnish with "Chill Time" ([Automaton](https://automaton-media.com/en/news/20220111-8306/)).
- The trend continues in 2025–26: PC Gamer calls it a desktop pet resurgence ([PC Gamer](https://pcgamer.com/games/life-sim/desktop-pets-are-so-back-baby)), Steam ran a Taskbar Treasures Week in Dec 2025, and *Desktop Island: Idle Together!* was announced Aug 2026 ([Game Developer](https://www.gamedeveloper.com/press-release/a-desktop-idler-you-can-play-with-friends-desktop-island-idle-together-announced-for-steam)).
- Relevance to mobile: these games sell **ambient presence plus cute animation** at a low price. The mobile analogue is a home-screen **widget or Live Activity** showing the terrarium **[idea, not researched]**.

**Pattern: what separates good from shallow**
1. **The world changes in kind, not just in number** (Paperclips, A Dark Room, Kittens).
2. **There is something alive to watch** (Egg, Inc. chickens, Cats & Soup, Neko Atsume, Idle Slayer runner). The shallowest idlers show only counters.
3. **Real decisions with trade-offs** (Kittens seasons, Melvor skill choice) vs. "buy the next thing."
4. **Authored arcs and endings** (Paperclips, Spaceplan, A Dark Room) vs. infinite padding.
5. **Respect for absence and wallet** (Egg, Inc., Melvor, Kittens iOS) vs. pay-to-skip and FOMO (the IdleOn complaints).

---

## 4. Retention & monetization (mobile-weighted)

### 4.1 Ethical vs predatory patterns

| Usually accepted | Usually resented |
|---|---|
| Premium or free-to-try with a one-time unlock (Melvor mobile, Islanders Mobile $4.99, Kittens iOS $2.99, Pocket Camp Complete) | Paying to skip timers that exist to be skipped ([Buergi](https://czasopisma.uni.lodz.pl/Replay/article/view/23588)) |
| **Opt-in rewarded ads giving a *small* extra** (Egg, Inc.). Players rate rewarded ads the most acceptable format: 64% "acceptable" (Gameloft, Jul 2025, [link](https://www.gameloft.com/for-brands/news/rewarded-ads-the-format-players-trust-and-remember)), and Unity found 46% prefer them vs. 5% for interstitials ([GamesBeat](https://gamesbeat.com/unity-the-majority-of-mobile-gamers-are-open-to-engaging-with-incentivized-video-ads/)) | Forced interstitials; ads that gate progress |
| Cosmetic DLC/IAP (Rusty's Supporter Pack, 11% attach; Bongo Cat cosmetic packs; Chill Corner music albums) | Gacha for power, limited-time power items, pay-to-win (IdleOn complaints) |
| Paid expansions with clear content (Melvor *Throne of the Herald* $4.99) | Withering or decay that monetizes recovery (FarmVille pattern, [Engadget](https://www.engadget.com/2010-03-22-the-fringe-farming-games.html)) |
| Transparent caps you can raise permanently (Egg, Inc. offline cap) | Subscriptions that feel like rent on core features. Pocket Build's sub *works* but retention was weak ([MoonBear](https://moonbear.com/investors)); Pokémon Sleep's pass worked at scale but is criticised for whale dependence ([Gfinity](https://www.gfinityesports.com/article/pokemon-sleep-revenue-proves-pikachu-can-sell-anything)) |
| A "Remove ads" one-off IAP (Idle Slayer $9.99) | Big gem bundles ($99.99), starter-pack pop-ups, aggressive daily-login streak loss |

Spiel et al.'s designer guideline: **limit transactions per time window and always offer a fixed-cost or free path.** Many interviewed idle designers considered microtransactions unethical ([PDF](https://par.nsf.gov/servlets/purl/10174274)).

### 4.2 Recommended monetization shape for this project (solo dev, cozy, mobile)
Option A, preferred for brand fit: **free demo (the first era) plus a one-time unlock** of about $4.99–9.99, then optional cosmetic packs (terrarium glass styles, biomes, citizen outfits) and later paid "biome expansions." This follows Melvor and Islanders.
Option B: F2P with **optional rewarded ads only** (e.g. "the people throw a festival: ×2 output for 1 h"), with a daily cap, a **one-off "Supporter" IAP that removes ads and adds a cosmetic**, and cosmetics. **No energy, no premium skip currency, no gacha.**
Avoid battle passes unless they are fully cosmetic and non-expiring. A cozy audience reads expiring passes as FOMO. *(Inference from the IdleOn and Pocket Camp evidence; there is no direct cozy-battle-pass dataset.)*

### 4.3 Market data (limited)
- Mobile game IAP revenue was $81.75B in 2025 (+1.3%), with downloads −7.2%. Hybrid-casual IAP grew 20% to $4.2B (Sensor Tower via [GWO](https://gameworldobserver.com/2026/01/21/in-2025-the-revenue-of-the-mobile-gaming-market-grew-but-only-slightly-analysis)).
- Idle RPG made up 15% of the top-1000 grossing RPGs in 2024 (AppFigures, [GameDev Reports](https://gamedevreports.substack.com/p/appfigures-mobile-gaming-market-in?open=false)).
- US iOS idlers in Q1 2025 had weekly revenue peaks of about $190–310k (Gold & Goblins, Carnival Tycoon, The Tower) ([Sensor Tower](https://sensortower.com/blog/2025-q1-ios-top-5-idler%20games-revenue-us-602ae7fb241bc16eb874f8e1)).
- Steam premium idle: Cookie Clicker about 2.5M estimated, Melvor about 424k estimated, Rusty's about 330k–550k. The typical "desktop idle" price point is $2–7 ([GameDiscoverCo](https://newsletter.gamediscover.co/p/how-rustys-retirement-idle-farmed) notes idlers at $0–5 and farming sims at $10–15).
- Market-size reports such as "idle games market $13.2B" ([Marketintelo](https://marketintelo.com/report/idle-games-market)) have unclear methodology. Don't rely on them.

### 4.4 Notifications & FOMO
- **Platform rules:**
  - iOS: Apple guideline 4.5.4 says push must not be required for the app to function, and marketing pushes need explicit opt-in consent plus an in-app opt-out ([as reproduced, 2020](https://betanews.com/2020/03/05/apple-iphone-ipad-push-notification-ads); check the current text at [developer.apple.com](https://developer.apple.com/app-store/review/guidelines/)).
  - Android 13+: notifications are **opt-in** via the `POST_NOTIFICATIONS` runtime permission, and the app chooses when to ask ([AOSP](https://source.android.com/docs/core/display/notification-perm)). After repeated denials, the user must go to Settings ([Leanplum](https://docs.leanplum.com/changelog/android-13-notification-opt-in)).
- **Ethical notification design for this game:**
  1. Ask for permission **after** the first meaningful moment (the first night passes, so offer "Want to know when your people finish the temple?"), never at launch.
  2. Send **player-scheduled** notifications only: the things the player started (a project done, an expedition back).
  3. Send at most about 1 per day by default, with **quiet hours** and per-category toggles.
  4. **Never send guilt or decay pings** ("your people are starving!"). Diegetic and warm tone only ("The harvest festival begins").
  5. Don't use streak-loss mechanics. Use returning bonuses that **accumulate** (Alharthi notes returning bonuses; make them never-lose).
- FOMO pitfalls to avoid: limited-time power items, expiring passes, events that permanently lock content. These are the main complaints about IdleOn and a reason Pocket Camp's F2P version gave way to a premium edition.

### 4.5 What reviews praise / hate (synthesis)
- **Praised:**
  - Respects my time and wallet (Egg, Inc., Kittens iOS, Islanders Mobile marketing copy: "no timers, no micro-transactions, no stress").
  - Runs while I do other things (Rusty's, Chill Corner).
  - Cute, alive visuals (Cats & Soup, Neko Atsume).
  - Depth for min-maxers without punishing casual players (Rusty's).
  - Low CPU and battery use (the Ropuka review: "only a sliver of your CPU", [How-To Geek via Yahoo](https://tech.yahoo.com/articles/6-desktop-idle-games-easily-143014417.html)).
- **Hated:** pay-to-win, FOMO events, forced ads, punishing absence, incomprehensible UIs ([Alharthi](https://par.nsf.gov/servlets/purl/10061230)), bland "pointless" loops (Gamezebo on Egg, Inc. via [search summary] **[unverified quote]**).

---

## 5. Design principles for the terrarium civilization

1. **Write the story arc first, then fit the math to the beats.** Each era of the civ (tribe → village → city → … → something absurd at terrarium scale) is an act with an interface or system paradigm shift. *Source: [Spiel et al. 2019](https://par.nsf.gov/servlets/purl/10174274); Paperclips' three acts ([Wikipedia](https://en.wikipedia.org/wiki/Universal_Paperclips)).*
2. **The return moment is the core loop.** Every session opens on "what happened while you were gone": a chronicle plus visible change in the diorama. *Source: Neko Atsume's design ([Wikipedia](https://en.wikipedia.org/wiki/Neko_Atsume)); Alharthi's "rewarding players for waiting."*
3. **Absence never destroys progress.** Neglect is bounded and affects rate and mood, not assets, and recovery is quick and satisfying. *Source: [Alharthi et al.](https://par.nsf.gov/servlets/purl/10061230) ("does not place existing successes at risk"); the FarmVille counterexample ([Engadget](https://www.engadget.com/2010-03-22-the-fringe-farming-games.html)).*
4. **Turn care into progression.** Manual cleaning becomes something citizens do via tech (sanitation, irrigation), and the player's care moves up the abstraction ladder. Automation is the progression. *Source: Alharthi's incremental definition; [Rusty's Retirement](https://newsletter.gamediscover.co/p/how-rustys-retirement-idle-farmed) automation pieces.*
5. **Exponential costs vs polynomial production, with milestone multipliers.** Tune in a spreadsheet, then iterate on feel. Keep older buildings relevant through per-building multipliers and synergies. *Source: [Pecorella I](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-i), [II](https://www.gamedeveloper.com/game-platforms/the-math-of-idle-games-part-ii).*
6. **Show growth physically, and keep numbers secondary.** The diorama's density and complexity are the progress bar, and the 3D scale-shift per era is the spectacle. *Source: Egg, Inc. and Idle Miner visual feedback; Paperclips scale shifts ([Spiel et al.](https://par.nsf.gov/servlets/purl/10174274)).*
7. **Prestige changes rules, not just multipliers, and isn't destructive to attachment.** Use eras or new terrariums with persistent relics and myths, and keep the old terrarium on the shelf. *Source: [Pecorella III](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-iii); Antimatter Dimensions and Kittens challenges.*
8. **Move the player from tending to planning.** Add queues, standing orders, and policies so plans execute offline. *Source: [Alharthi et al.](https://par.nsf.gov/servlets/purl/10061230) (cognitive offloading, playing at planning).*
9. **Design plateaus as invitations to leave, not walls to sell.** *Source: Spiel et al. guideline; [Buergi 2024](https://czasopisma.uni.lodz.pl/Replay/article/view/23588).*
10. **Session ladder: glance (<1 min), tend (3–8 min), deep (rare).** Every session type must be satisfying. *Source: [GameAnalytics idle benchmarks](https://gameworldobserver.com/2020/12/22/gameanalytics-names-benchmark-engagement-metrics-idle-games).*
11. **Player-owned appointments.** The player chooses project durations, and notifications only report what the player started, opt-in, warm, and capped. *Source: [Apple 4.5.4](https://developer.apple.com/app-store/review/guidelines/); [Android 13 opt-in](https://source.android.com/docs/core/display/notification-perm).*
12. **Transparent offline rules.** Show the cap and how to raise it permanently through gameplay. *Source: Egg, Inc. ([PocketGamer.biz](https://www.pocketgamer.biz/egg-inc-idle-games-gold-standard/)).*
13. **Monetize the shelf, not the wait.** Premium unlock or a supporter IAP, cosmetics, and expansions. Rewarded ads, if used at all, are optional, small, and capped. No gacha, no skip currency, no expiring power. *Source: Melvor, Islanders Mobile, Rusty's 11% cosmetic attach; [Spiel et al.](https://par.nsf.gov/servlets/purl/10174274).*
14. **Something alive to watch.** Citizens with readable routines and small stories (names, lineages, quirks). Character animation is the cozy-idle payoff. *Source: Cats & Soup, Neko Atsume, Usagi Shima.*
15. **Real trade-offs.** Seasons, scarcity, and competing priorities (Kittens-style winter planning), but resolved by planning, not by real-time vigilance. *Source: [Kittens Game](https://store.steampowered.com/app/1097410/Kittens_Game/).*
16. **Allow endings.** Era finales and a "true ending" are fine. Serialised closed arcs beat infinite padding. *Source: [Spiel et al.](https://par.nsf.gov/servlets/purl/10174274) (reject endlessness), A Dark Room, Spaceplan.*
17. **Clear information design.** Show what the player needs to decide, when they need it, and nothing more. *Source: [Alharthi et al.](https://par.nsf.gov/servlets/purl/10061230).*
18. **Battery and CPU frugality.** Use coarse offline simulation and low-frame-rate idle rendering. *Source: [Spiel et al.](https://par.nsf.gov/servlets/purl/10174274) (sustainability); review praise for low-CPU idlers.*
19. **Sandbox needs goals.** Free-form decorating alone retains poorly. *Source: Pocket Build's developer-reported below-average retention ([MoonBear](https://moonbear.com/investors)).*
20. **Scope for a solo developer.** Solo precedents exist (Usagi Shima in Godot; Rusty's Retirement). Keep the system count small and deepen it through eras. *Source: [Godot showcase](https://godotengine.org/article/godot-showcase-usagi-shima/), [GameDiscoverCo](https://newsletter.gamediscover.co/p/how-rustys-retirement-idle-farmed).*

---

## 6. Curated reading list (ranked by value for this project)

1. **Spiel et al., "It Started as a Joke": On the Design of Idle Games** (CHI PLAY 2019): designer interviews and actionable guidelines on narrative, care, and ethics. [PDF](https://par.nsf.gov/servlets/purl/10174274)
2. **Pecorella, The Math of Idle Games Parts I–III** plus spreadsheets. [I](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-i) · [II](https://www.gamedeveloper.com/game-platforms/the-math-of-idle-games-part-ii) · [III](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-iii) · [sheets](https://kon.gg/idle-math-spreadsheets)
3. **Alharthi et al., Playing to Wait: A Taxonomy of Idle Games** (CHI 2018). [PDF](https://par.nsf.gov/servlets/purl/10061230)
4. **Pecorella, Quest for Progress** (GDC Europe 2016 slides). [SlideShare](https://www.slideshare.net/slideshow/quest-for-progress-gdc-europe-2016/65405507)
5. **GameDiscoverCo, How Rusty's Retirement idle-farmed its way to 300k Steam sales.** [link](https://newsletter.gamediscover.co/p/how-rustys-retirement-idle-farmed)
6. **PocketGamer.biz, Why Egg, Inc is the new gold standard for idle game monetization** (2016). [link](https://www.pocketgamer.biz/egg-inc-idle-games-gold-standard/)
7. **Buergi, Idle Games: A Cozy Genre Turned Exploitative** (Replay, 2024, open access). [link](https://czasopisma.uni.lodz.pl/Replay/article/view/23588)
8. **Play these:** Universal Paperclips ([web](https://www.decisionproblem.com/paperclips/)), Kittens Game, A Dark Room, Egg, Inc., Neko Atsume, Cats & Soup.
9. **Pecorella, Idle Games: The Mechanics and Monetization of Self-Playing Games** (GDC 2015). [Vault](https://gdcvault.com/play/1022065/Idle-Games-The-Mechanics-and)
10. **Alharthi et al., The Pleasure of Playing Less** (2025 book, Kittens Game). [link](https://playstorypress.org/2025/05/15/the-pleasure-of-playing-less/)
11. **Numbers Getting Bigger: The Design and Math of Incremental Games** (Tuts+). [link](https://code.tutsplus.com/numbers-getting-bigger-the-design-and-math-of-incremental-games--cms-24023a)
12. **r/incremental_games** (community, its wiki, and its "Feedback Friday" threads). [link](https://www.reddit.com/r/incremental_games/)
13. **GameAnalytics idle benchmarks** (sessions, retention). [GWO summary](https://gameworldobserver.com/2020/12/22/gameanalytics-names-benchmark-engagement-metrics-idle-games)
14. **Godot showcase: Usagi Shima**, a solo-dev cozy mobile postmortem. [link](https://godotengine.org/article/godot-showcase-usagi-shima/)
15. **The rise of games you mostly don't play** (Game Developer). [link](https://www.gamedeveloper.com/design/the-rise-of-games-you-mostly-don-t-play)
16. **Kittens Game wiki** (a civ-idle systems reference). [link](https://wiki.kittensgame.com/) **[URL not verified]**
17. **Antimatter Dimensions** (play it for layered prestige and challenge design). [Steam](https://store.steampowered.com/app/1399720/Antimatter_Dimensions/)
18. **Bongo Cat profitability report** (a cautionary tale on CCU vs revenue). [wnhub](https://wnhub.io/news/stores-and-publishing/item-48117)
19. **Animal Crossing: Pocket Camp Complete** coverage (a F2P-to-premium cozy pivot). [Nintendo Life](https://www.nintendolife.com/news/2024/10/animal-crossing-pocket-camp-complete-arrives-on-mobile-this-december)
20. **Platform notification rules:** [Apple App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/) (4.5.4) and [Android notification permission](https://source.android.com/docs/core/display/notification-perm)
21. **Engadget, The Fringe: Farming games** (2010): the withering critique, i.e. what not to do. [link](https://www.engadget.com/2010-03-22-the-fringe-farming-games.html)
22. **Progress Wars: Idle Games and the Demarcation of "Real" Games** (DiGRA). [link](https://dl.digra.org/index.php/dl/article/view/851)

---

## 7. Open gaps / follow-ups
- "Eric Guan" essay: not found. Ask the user for the link.
- No recent (2023+) **idle-specific** mobile session or retention benchmarks were found. The idle-specific figures are 2018–19.
- No public data on push opt-in rates for cozy games, or on cozy battle-pass performance.
- NGU Idle, IdleOn revenue, and Legend of Mushroom figures are unverified.
- Worth researching next: home-screen widgets and iOS Live Activities as a "glance at the terrarium" surface (a mobile analogue of the desktop idle wave), and Unity offline-simulation architecture.
