Game UI Database material was excluded because of its terms.

# UI research: how other games solve our open problems (#104)

Taken 2026-10-31 for the UI rework (#103), against the pieces in [ui-inventory.md](../../design/ui-inventory.md) and
[game-design-practices.md](../../game-design-practices.md). 42 reference images in `img/`, listed in
[sources.md](sources.md). Internal reference only: nothing here ships.

**Read §9 before using this for #106.** 33 of the 42 images come from Apple and Steam pages, whose terms also restrict
copying. That decision is still open.

**Marks.** [obs] = I saw it in an image kept here. [S] = a source says so. "Rec" = my recommendation, for #163 and the
user to judge.

**How far to trust this.** I played nothing and watched no video. Every [obs] comes from one still image. Store shots
are marketing images and can be staged. Sizes are estimates scaled from those stills to a 375 pt wide phone. 17 of the
42 images are PC or landscape.

## 1. Problem table

| Problem | Patterns seen | Best references | Rec for us | Avoid |
|---|---|---|---|---|
| **P1** Sparse HUD | One labelled button (Neko); a faint icon row in one corner (AbyssRium); no HUD in the shot (Viridi); one big verb plus labelled icons (Pocket City 2) | `neko-atsume-2-hud-gifts-await`, `abyssrium-hud-icons`, `pocket-city-2-hud` | One primary verb in the thumb zone, and few always-on things. Label icons with a word | Currency rows and side stacks of badges (`cats-soup-hud-clutter`) |
| **P2** Return feed | Peek as a speech pill (Neko "Gifts Await"); a year-headed log (BitLife, Freeciv.net); one full-screen result card (Pokémon Sleep) | `neko-atsume-2-hud-gifts-await`, `bitlife-year-feed` | Peek as a one-line pill with a tail, low on the screen. Year as the heading of an entry | A full-screen report; a score for time away (`pokemon-sleep-morning-card`) |
| **P3** One character everywhere | The figure's own picture on its card (Neko, Pokémon Sleep, WorldBox); sprite, portrait and name together on one screen (Wesnoth) | `wesnoth-tutorial-line`, `neko-atsume-cat-page`, `worldbox-unit-card` | Draw every surface's picture from the doll itself, with the name beside it | A portrait that can't be matched to the figure in the world |
| **P4** Tool tray, placing | Low pill tray over open water (Islanders); tray with undo joined on (Terra Nil); corner panel (Mindustry Android); stock on an edge rail (Mini Metro); dimmed entries (Mindustry, Unknown Horizons) | `islanders-hud`, `terra-nil-tray-undo`, `mindustry-v6-android-build-panel`, `mini-metro-hud` | A low tray or a corner panel, so the land stays clear. Charges as a badge on the tool. Undo next to the tools | A tray that stays tall while placing (ours is 60–80 %); dense unlabelled icon walls (`worldbox-toolbar-undo`) |
| **P5** Following one character | Small portrait in a bottom corner (Pocket City 2); star and eye on the unit card (WorldBox); a small live view in a window, with a Location button on another (OpenTTD); a bar of everyone (RimWorld) | `pocket-city-2-hud`, `worldbox-unit-card`, `openttd-windows-vehicle-view` | One small portrait in a fixed slot, with a way to jump to that person | A bar of many portraits (`rimworld-colonist-bar`) |
| **P6** Scrubbing history | A hairline with a diamond playhead and character marks over the scene (Timelie) | `timelie-scrubber`; `terra-nil-world-progress` for a progress bar with life on it | A hairline over the world, with one year label on the playhead and a hit strip far bigger than the line | A second year readout (known bug) |
| **P7** Book and silhouettes | "????" with a dark shape and a blank date (Neko); photo card (Pokémon Sleep) | `neko-atsume-catbook-silhouette`, `pokemon-sleep-dex-card` | Silhouette, "????" and a blank line keep the page's shape. "New" tag on fresh entries | Stat lists (`cats-soup-collection`) |
| **P8** Settings, destructive confirm | Phone sheet of plain rows with Done at the top (Go Map!!); Delete in the same row and style as New (Minetest) | `go-map-ios-settings-sheet` | Plain rows with a clear Done. Keep the destructive action apart from everyday ones, and styled differently | `minetest-delete-beside-play`; sign-in rows in a game's settings |
| **P9** First-run hints | Pointing hand on the choice (BitLife); one line from a character while the world and the highlighted unit stay visible (Wesnoth) | `bitlife-choice-hint`, `wesnoth-tutorial-line` | One cue, on the thing itself, with one short line | |
| **P10** Big event waits for a tap | A gauge that lets the player stay as long as they like (Islanders [S]); a next-step button in a fixed corner (Wesnoth End Turn, Freeciv.net Turn Done) | `islanders-hud` | Watch in a fixed slot with no timer. The player can stay and keep watching for as long as they like | A countdown on the button (`freeciv-net-log-turn-done`) |
| **P11** Bubbles and labels | One-line white tags with a tail (Pokémon Sleep); longer speech in a named box away from the speaker (Pocket Camp, Wesnoth); name signs on the map (OpenTTD, Freeciv.net) | `pokemon-sleep-world-labels`, `pocket-camp-dialogue`, `wesnoth-tutorial-line` | Two sizes: a short tag at the speaker, longer speech in a caption with a name | Big bubbles over the speaker (#143) |
| **P12** Mode states | Armed tool outlined, red X in a fixed corner, dashed area on the world (WorldBox); labelled exit, "BACK To City Mode" (Pocket City 2); a named tool window (OpenTTD) | `worldbox-tool-armed`, `worldbox-brush-bounds`, `pocket-city-2-avatar-mode` | Show the mode on the world as well as in the UI. One exit in one fixed place, and say where it goes | A mode shown only by a banner |
| **P13** Warm type and style | Cream rounded pills (Pocket Camp); parchment panels with serif headings (Unknown Horizons); plain type on paper (Mini Metro); colour with no UI (Monument Valley 2, Viridi) | `pocket-camp-hud`, `unknown-horizons-build-panel`, `mini-metro-hud` | See §4 | See §5 |

## 2. Coverage

Counted after the exclusion. A target of 3 references applied to P1–P5, P10, P11 and P12.

| Problem | Images | Games | Status |
|---|---:|---:|---|
| P1 | 13 | 11 | OK |
| P2 | 5 | 4 | OK; no real "while you were away" screen |
| P3 | 8 | 6 | OK |
| P4 | 10 | 7 | OK; nothing on confirming a placement by the finger |
| P5 | 4 | 4 | OK, thin; three of the four are PC or a card button |
| P6 | 2 | 2 | **Below 3**; one true scrubber |
| P7 | 4 | 3 | OK, thin |
| P8 | 2 | 2 | **Below 3**; one is not a game; no destructive confirm |
| P9 | 2 | 2 | **Below 3** |
| P10 | 4 | 4 | Meets 3 on count, weak on fit: gauge and turn buttons, no staged event |
| P11 | 6 | 5 | OK; speech above tiny figures rests on Pokémon Sleep |
| P12 | 6 | 4 | OK; no photo or clean mode reference |
| P13 | 9 | 9 | OK |

## 3. Per-reference notes

Each note was re-checked against its image or a re-read source on 2026-10-31.

**Neko Atsume / 2** (App Store, portrait)
- Good [obs]: one paw button labelled "Menu", two small counters, and a "Gifts Await" pill with a tail. Closest thing
  to our peek: it says something is ready without taking the world.
- Good [obs]: Catbook keeps an unknown cat as "????" with a dark shape and a blank date. Known cats carry a picture of
  that cat, and new ones a "NEW" tag.
- Bad for us: the pill names no story. We have a real headline, so use it.

**Pokémon Sleep** (App Store, portrait)
- Good [obs]: one-line white tags float above each sleeper, each pointing at its owner. The best P11 reference.
- Good [obs]: dex card is a picture, number chip and name.
- Bad for us [obs]: the result card fills the screen and shows "88". A score for time away goes against pillar 4.

**Tap Tap Fish AbyssRium** (App Store, portrait)
- Good [obs]: faint icons in one row at the bottom right, so the scene dominates. Closest subject match, a little
  world in a container.
- Bad [obs]: small, unlabelled, low contrast on the pale scene. Event and gift badges appear in the other shot.

**Viridi** (App Store, portrait)
- [obs]: the kept shot has no HUD, only the pot and its room. A store shot may hide UI, so it isn't proof.

**Pocket City 2** (App Store, landscape)
- Good [obs]: a big "Build" pill, a clear primary verb, and four icons with word labels. Avatar mode has "BACK To City
  Mode", which says where the button goes.
- Bad [obs]: level, money, population, date and more icons across the top. Too much for us.

**ISLANDERS** (Steam PC shots; the mobile store shots showed no UI)
- Good [obs]: tray is a low dark pill over the water with count badges. Score ring in one corner, next island in the
  other. All three stay off the island.
- [S] The gauge fills as points are scored, then the player can click it. Players "are free to remain" and keep
  building until they decide to move on ([Wikipedia](https://en.wikipedia.org/wiki/Islanders_(video_game))).

**Terra Nil** (Steam PC shots)
- Good [obs]: undo is a big orange button joined to the end of the tray. Tier tabs and a cost under each tool. A model
  for tool rows plus the landscaping row.
- Good [obs]: the restoration bar carries leaves and flowers.
- Bad [obs]: the top corners carry gauges, which pillar 5 forbids.

**WorldBox** (App Store and Steam)
- Good [obs]: the armed tool is outlined, a red X stays in the corner, dashed lines mark an area.
- Good [obs]: the unit card ends in a star and an eye. I read these as favourite and follow, from the icons only.
- Bad [obs]: dense pages of small unlabelled icons. Pillar 1's reference for history, not for UI.

**Mindustry** (Commons; v6 is an Android shot, 3.5 is landscape)
- Good [obs]: on Android the build panel sits in the lower right corner, leaving most of the portrait screen clear.
  Category icons on the right edge, tools beside them, the active category boxed, three actions along the bottom.
- Bad [obs]: unlabelled icons, and a wave timer that doesn't suit us.

**The Battle for Wesnoth** (Commons, PC)
- Good [obs]: one character at once as a map sprite, a side-panel picture with name, and a large portrait with the name
  above their line. The clearest P3 reference left. The world stays visible over the line of speech.
- Bad [obs]: heavy side panel and a top bar of numbers. The large portrait is painted and differs from the sprite.

**Unknown Horizons** (Commons, PC)
- Good [obs]: parchment panel, serif heading, tier tabs I–III, dimmed entries for what isn't available.
- Bad [obs]: the panel covers a large part of the screen and is ornate.

**OpenTTD** (Commons, PC)
- Good [obs]: a window holds a small live view of one vehicle, and a station window has a Location button.
- Bad [obs]: floating windows everywhere. Useful for the idea only, and an anti-example for density.

**Freeciv.net** (Commons, browser)
- Good [obs]: the messages log has a plain "Year: 1768 AD" line among events.
- Bad [obs]: "Turn Done (59s)" puts a countdown on the button. Log lines in red read as failures, against practices §11.

**Go Map!!** (Commons, iOS app, not a game)
- Good [obs]: a phone sheet of plain rows, grouped under small headings, with Done top right. No game covers this
  here, so treat it as layout only.

**Minetest** (Commons, PC)
- Bad [obs]: Delete sits in one row with Configure and New, same size and style, right above Play Game.

**Mini Metro** (App Store, landscape)
- Good [obs]: stock on the edge as small icons, line colours as dots, clock only. Very plain.
- Bad for us: cool and abstract. Use its discipline, not its look.

**BitLife** (App Store, portrait)
- Good [obs]: a plain log with "Age: 1 year" as a heading, and a pointing hand on a choice.
- Bad [obs]: store art is loud. Take the structure only.

**Timelie** (Steam PC)
- Good [obs]: a thin line, diamond playhead, play icon and small character marks along the bottom. The world stays
  whole. [S] "a time bar at the bottom", controlled "in the form of a media player"
  ([Wikipedia](https://en.wikipedia.org/wiki/Timelie)).
- Bad for touch: the line needs a far bigger hit strip than it shows.

**Animal Crossing: Pocket Camp Complete** (App Store, portrait)
- Good [obs]: speech sits at the top with a name chip, leaving both characters in view. Cream pills read well on a busy
  ground. Speech at the top is further from the thumb, though nothing needs tapping there except to continue.

**Stardew Valley mobile** (App Store, landscape)
- Good [obs]: tools on an edge rail, in thumb reach.

**Monument Valley 2** [obs]: one image, kept for colour and diorama framing only. The shot has no UI.

**Looked at, nothing kept:** Townscaper and Islanders Mobile (store shots show no UI), Tiny Tower and Fallout Shelter
(mostly marketing art), Alto's Odyssey (art only).

## 4. Our colours: contrast

Tokens from `Game.uss` `:root`. The first seven ratios were measured by the coordinator, and I reproduced them with the
WCAG formula. WCAG 1.4.3 AA asks for 4.5:1, or 3:1 for large text, which is at least 18 pt, or 14 pt bold
([W3C](https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum.html)).

| Text on background | Ratio | Normal text | Large text |
|---|---:|---|---|
| Plaster `#EFE3CC` on terracotta `#C66F4E` | 2.86 | **fails** | **fails** |
| White on terracotta | 3.63 | **fails** | passes |
| Frame `#2E2A27` on terracotta | 3.92 | **fails** | passes |
| Plaster on moss `#5E7F43` | 3.60 | **fails** | passes |
| Timber `#6A4B36` on plaster | 6.19 | passes | passes |
| Timber on canvas `#E2D3B3` | 5.31 | passes | passes |
| Frame on thatch `#CFA65E` | 6.28 | passes | passes |
| **Plaster on darker terracotta `#9E5033`** | **4.53** | passes | passes |
| **Plaster on darker moss `#526E3A`** | **4.53** | passes | passes |

**The current primary button text fails 4.5:1 and even 3:1.**

**Rec:** a button terracotta of `#9E5033` (same hue and saturation, lower lightness) passes with plaster at 4.53. It's
visibly browner, so the user should judge it. Keep `#C66F4E` for fills and accents that carry no text. `#526E3A` does
the same job for moss.

## 5. Type and style

**Colours sampled** from kept images [obs]; JPEG, so approximate: Pocket Camp pill `#F8F7E5`, Neko button `#ECB745`.

**Type.** I couldn't confirm any game's font. Patterns [obs]: warm games use a rounded or soft sans on cream (Pocket
Camp, Pokémon Sleep), hand-made lettering for titles (Neko), a serif for headings on parchment (Unknown Horizons),
plain neutral type where calm matters (Mini Metro).

Rec, to try rather than a finding: a soft sans for all controls, a warm serif only for story text and the book, so
stories feel written and buttons feel like buttons. Check licences for anything picked and log them in
`docs/assets-log.md`.

**Panels with a 3D diorama** [obs]: Islanders' low pill and Pocket Camp's rounded pills sit over the world without
reading as a box laid on top. Unknown Horizons' ornate panel suits its look but takes a lot of space.

**Anti-examples** (pillars 5 and 6, practices §3, §6, §14)
- `cats-soup-hud-clutter`: currencies, rows of badged icons down both sides and a food strip. Charming art, yet the
  world is a background. Red dots on many buttons pull you to chores.
- `idle-miner-hud-store-shot`: the number is the point. Store art, so a partial view.
- `rimworld-colonist-bar`: fine for its audience, but shows what P5 turns into if pins stack up.
- `openttd-windows-vehicle-view`: windows covering the world.
- Event gifts and red dots in `abyssrium-event-badges`: small, but a FOMO habit starts here.

## 6. Motion notes

**I saw no motion.** No video was played. Only two claims rest on a source, both on Wikipedia: Timelie's time bar and
Islanders' gauge, quoted in §3. Practices §12 still applies: no flicker or harsh repetition. Motion is an open gap.

## 7. Accessibility

**Targets.** Our rule stays 44 pt / 48 dp (practices §5). I dropped the outside citations because they weren't primary.

Estimated visible sizes on a 375 pt phone [obs]:

| Control | Visible size | Note |
|---|---:|---|
| Neko menu button | ~54 pt | clears 44 |
| AbyssRium row icons | ~30 pt | small; the hit area may be larger, can't tell |
| Cats & Soup side icons | ~30 pt | small and crowded |
| Pokémon Sleep world tags | ~22 pt tall | fine if labels only |

Rec: a small visible mark can have a bigger invisible hit area, which keeps the timeline and small icons delicate.

**Contrast.** See §4.

**Text size.** Practices §12 asks that text respects system size where possible. Rec: test all cards at larger sizes.

**Colour alone.** Pins, ties and era bands need a second signal (practices §12). Mini Metro leans on colour alone for
its lines [obs].

## 8. Recommendations in short

1. **Placing keeps the land clear:** a low tray or corner panel (Islanders, Terra Nil, Mindustry). Our 60–80 % tray is
   the opposite.
2. **Every picture of a person comes from the doll,** with the name beside it (Neko, WorldBox, Wesnoth's side panel).
3. **The peek is a pill with a tail** carrying the top headline (Neko).
4. **Show the mode on the world and give it one fixed exit** (WorldBox, Pocket City 2).
5. **Watch waits with no timer,** and the player can stay as long as they like (Islanders [S]).
6. **Darken the button terracotta** (§4).

## 9. Source terms (read before #106)

All read 2026-10-31. The coordinator's rule was to re-source only from pages whose terms don't forbid this.

| Source | Terms read | What they say | Result |
|---|---|---|---|
| Game UI Database | [about.php](https://www.gameuidatabase.com/about.php), confirmed by the coordinator; not reopened | Content prohibited for AI asset generation or machine learning | All removed |
| Interface In Game | [Terms and conditions](https://interfaceingame.com/terms-and-conditions/) | No AI wording; bans "spider, crawl, or scrape" | Skipped; one image removed |
| Nintendo | [Terms of use](https://www.nintendo.com/us/terms-of-use/) | No AI wording; excludes downloading, bars scrapers for copying content, personal use only | **Not captured** |
| Google Play | [Play terms](https://play.google.com/about/play-terms/) | No AI wording; content is for personal, non-commercial use | **Not captured** |
| Apple sites | [Site terms](https://www.apple.com/legal/internet-services/terms/site.html) | No AI wording; bars copying and "page-scrape", "robot" tools; commercial use needs consent | **No new captures; 26 old images kept pending a decision** |
| Steam / Valve | [Subscriber Agreement](https://store.steampowered.com/subscriber_agreement/), [site terms](https://www.valvesoftware.com/en/legal) | No AI wording; bar copying, republishing and derivative works, and bots on Steam content | **No new captures; 7 old images kept pending a decision** |
| Thunder Lotus press kit | [Press kit page](https://thunderlotusgames.com/press-kits/spiritfarer-press-kit/) | No licence stated; site terms page not found | Not used: no grant to rely on |
| 11 bit studios, Toukana | Press URLs tried returned 404 | Terms not found | Not used |
| Wikimedia Commons | [Licensing](https://commons.wikimedia.org/wiki/Commons:Licensing), [Terms of use](https://foundation.wikimedia.org/wiki/Policy:Terms_of_Use) | Licences must allow commercial use and derivatives; reuse is welcome subject to each licence | **Used: 9 new images,** each licence in `sources.md` |
| Wikipedia, W3C | Wikipedia under the same Wikimedia terms; W3C licence not read | Text facts only, short quotes, nothing copied in bulk | Used for 3 facts |

The Apple and Steam images were brought over because the rework said to. They were captured in an automated browser,
which those terms may also cover. Their restrictions don't name AI, but they do limit copying, so they deserve the same
review as the excluded site before anything goes to #106. They're in a separate commit so they can be dropped alone.
This is not legal advice.

## 10. Gaps

- **Below 3 references:** P6, P8, P9.
- **P8:** no destructive confirm from any game, so the confirm wording stays open.
- **P4:** no reference for confirm or cancel at the finger.
- **P2:** no idle game's return screen. The return is the least evidenced part.
- **P10:** no staged event that waits for the player.
- **P12:** no photo or clean mode reference.
- **P5:** nothing shows a persistent marker on a tiny figure in the world. The eye button's meaning is inferred.
- **P6:** era bands and markers come from reasoning, not a reference.
- **Portrait:** Islanders, Terra Nil, Timelie, Wesnoth and others are landscape, so layouts need redrawing.
- **Style:** the free-software references are mostly not warm or cozy, so they support structure, not look.
- **Motion and type:** no motion seen, no font identified.
