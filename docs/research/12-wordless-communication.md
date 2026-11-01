# 12 · Talking without words (#144, #161)

Written 2026-10-31, no Unity, nothing run on a phone. Tags: **[V]** read in a primary source (vendor docs, the developer
speaking), **[S]** secondary (wiki, article, forum), **[I]** my own inference from this repo or from general practice.
No game footage or screenshots are copied; the games below are named only for what they do.

## 1. The problem

At the fire people "seem to be talking" but nothing shows it (#134). Real speech bubbles stay rare on purpose (one per
20 s at most, so a group is mostly silent). The people are peg dolls with no faces, seen tiny, so a mouth or a facial
expression cannot carry anything [I, from `spikes/style-kit.md`]. We need a cheap, constant, readable "they are chatting"
signal, plus the same signal for short reactions like the `!` after a knock (#161).

Hard rules from our own docs: sound alone never carries information, many people play muted
(`game-design-practices.md` §10); no flicker or harsh repetition (§12); pillar 2 and 4, nothing may nag or guilt; free
assets only, logged in `docs/assets-log.md`.

## 2. What other games do

| Approach | What it does | Evidence |
|---|---|---|
| Gibberish voice from letters (Animal Crossing "Animalese") | Each letter or kana maps to a short sound; pitch depends on personality, mood (happy higher, sad or angry lower) and, in later games, species. One entry (Wild World) maps letters to random syllables and is unintelligible on purpose. Speed follows the text speed | Nookipedia, a fan wiki [S]. The "per letter, pitch-shifted" mechanism is the wiki's description, not a Nintendo statement |
| Invented language (The Sims, "Simlish") | Voice actors improvised for about five days with sounds from languages they did not speak; the team then cut and combined clips to fit requested emotions and lengths. Goal: give Sims a voice without dictating their stories and without tying the game to one language. Tone (happy, angry, flirty) carries the meaning | Robi Kauker, Maxis audio director, as reported by [PopSci](https://www.popsci.com/story/technology/speak-sims-language/) [S: interview report]; same point in [games.gg](https://games.gg/news/most-popular-simlish-phrases-from-the-sims-4/) [S] |
| Blip speech (Banjo-Kazooie style) | Short per-character grunts played with the text. Each character gets its own pitch and rhythm, which gives personality for free. The Godot "Babble Engine" asset generates this kind of critter chatter | [Godot asset page](https://godotengine.org/asset-library/asset/5567) [S] for the idea; the designers' reasons were not found |
| Symbol or emote bubbles | A glyph over the head (heart, exclamation, question, ellipsis, note). Silent, language-free, works at any volume setting | General practice [I]. I found no postmortem with numbers for size at phone scale, so the size advice below is my own |
| Gesture only | Turn to face, head bob, a hand wave, taking turns | General practice [I]. Needs animation on a body with stub arms and no face |
| Music stingers | A short tune when something social happens | Already used for *moments* (Kenney jingles, flute/drum tag in `assets-log.md`) |

## 3. Cost and fit for us

"Phone cost" is an estimate [I]; none of this is measured on a device.

| Option | Phone cost | Fits tiny peg dolls? | Silent play | Verdict |
|---|---|---|---|---|
| Symbol bubbles, drawn in code | One small quad or mesh per live bubble, one shared material, billboarded. At most 4 to 6 alive: a few draws, no textures. Reuse the existing world-bubble placement | **Yes**, if the glyph is a bold flat shape (see §5). Dolls have no faces, so this is the only readable speech marker | Full. It is information that needs no ears | **Core** |
| Gibberish voice | One synthesized clip bank in memory (about 1 MB at 22 kHz mono 16-bit for ~24 clips) and one `AudioSource` play per bubble. Negligible CPU | Not applicable to sight; it makes the camp feel alive when zoomed in | None: must be garnish only | **Garnish, zoom-gated** |
| Gesture only | Free in draws; costs animation work in `DollMotion` and tuning per era | Weak. Stub arms and a head bob read poorly when the doll is a few dozen pixels tall | Full, but weak | Variant to compare, not the base |
| Music stingers | Cheap | Tune says "something happened", not "they are chatting"; a stinger per chat would repeat within minutes and fight the sparse music rules in [11-music-playback.md](11-music-playback.md) | Needs sound | Keep for moments only |
| Real text lines | Already built | Illegible tiny | Full | Stays rare (#120 is out of scope) |

## 4. Silent play and the knock (#161)

- Everything meaningful rides on the bubble. The babble starts exactly when the bubble pops and carries nothing the bubble
  does not. Muted, the camp still chats. [I, from practices §10]
- The silent switch: Apple documents that the *Ambient* and *Solo Ambient* audio session categories are silenced by the
  Ring/Silent switch and that an app cannot read the switch's state ([Apple audio session guide](https://developer.apple.com/library/ios/documentation/Audio/Conceptual/AudioSessionProgrammingGuide/AudioSessionCategoriesandModes/AudioSessionCategoriesandModes.html) [V, via the search summary; I did not open the page myself]). So the babble must sit on the same bus and category as the other world cues, never on a "playback" category. The Editor agent should check what the existing `AudioDirector` already sets before adding anything.
- Knock reaction: a `!` pops over each person who reacts **before** the body moves (about 0.15 s of pop with a small scale
  overshoot, held about 1.2 s, then a fade). That gives the "they heard that" beat #161 asks for, and works with the sound
  off. A tiny two-note "uh-oh" blip may go with it, again as garnish. The gentler body motion in and out is #161's own job.

## 5. Recommended option

**Symbol bubbles are the carrier, a synthesized babble is a zoomed-in garnish, and both come from one "chatter" system.**
Bubble marks are the same in silent play; the babble only adds warmth.

### What to build

1. **Marks.** Five glyphs, drawn from the existing `MeshBuilder` (no textures, no fonts): ellipsis `…` (talking), note `♪`
   (content, near the fire), heart `♥` (bonding), `!` (surprise or alert, the knock), `?` (curious). White bubble with a dark
   glyph, so value contrast does the work and colour is only a bonus (practices §12). Silhouettes must differ at a glance:
   dots in a row, a note flag, a heart, a bar-and-dot, a hook-and-dot.
2. **Size.** Keep the bubble at least about 20 pt tall on screen at the default zoom, strokes at least 2 px [I; my number,
   to be confirmed by screenshot on a phone-sized capture]. Clamp the on-screen size between a minimum and maximum, so
   perspective does not shrink them into dots.
3. **Rate.** Separate from the real-line limit. One speaker at a time per group, turn-taking: a mark every 3 to 6 s per
   group, never every person at once. At most 4 live bubbles in the box. A real line always wins: a chatter mark in flight
   fades fast when a real bubble starts. Same quiet rules as the world bubbles (sheets, moments, clean mode).
4. **Zoom gating.** Fully zoomed out: show at most one mark for the whole box, or none (a column of tiny glyphs is noise
   [I]). Default zoom: marks yes, babble off. Zoomed in on a group: marks yes, babble on. The existing zoom value in
   `BoxCamera` supplies the gate; fade the babble volume over a zoom range, not a hard switch.
5. **Babble, synthesized in code.** Follow the repo's existing pattern (`CareSound`, `proc:` cues; flies, cheer, swish are
   already made this way and logged as "made in code"). One syllable is 60 to 110 ms: a soft triangle or sine plus a
   quieter second harmonic, two band-limited formant-style resonances for a vowel-ish colour, a quick pitch glide up or
   down, a fast attack and exponential decay. A "word" is 2 to 5 syllables; a "sentence" is 1 to 3 words. Pre-render
   about 4 voices × 6 clips at 22 kHz mono, once at start (`AudioClip.Create` with a non-streamed reader callback runs the
   fill once; [Unity docs](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audioclip/pcmreadercallback) [V]).
   Voice per person: base pitch from age and size (kids higher, as Animalese does by size [S]); contour from the mark
   (`?` rises at the end, `!` is short and sharp, `♪` is two or three held notes, `♥` is a soft falling pair).
6. **Cue plumbing.** New cues in `CueLibrary` on `SoundBus.World`: low volume (about 0.2 to 0.3), `Jitter(1)` for pitch,
   a `Limit` (no more than 2 voices at once, at least 0.4 s apart), low priority so real lines, moments and music duck
   it, and `Ducks()` off. It must obey the sound settings and the silent switch like every other cue.
7. **Knock.** Spawn the `!` through the same system with a "reaction" priority that skips the group turn-taking; play
   the short blip with it.
8. **Debug panel.** Cheats: force a group to chat now, force each of the five marks on the nearest person, and the style
   switch in §6. (Project rule: every new testable state gets a cheat.)
9. **Tests.** Gating tests: no babble when zoomed out, none when muted or in quiet cases, a real line cancels chatter, the
   live-bubble cap holds, the rate is separate from the real-line limit.

### Where the audio comes from (assets-log, free-assets rule)

Nothing to download. I looked for a CC0 gibberish pack and found none that fits: OpenGameArt's "talking synthesizer" is
CC-BY, not CC0 ([page](https://opengameart.org/content/talking-synthesizer) [S]); "Baby Sound Effect" is CC0 but is a
real recording of a child, not babble ([page](https://opengameart.org/node/66037) [S]); the Godot Babble Engine is MIT
code for a different engine, useful as an idea only. So **synthesize it in code**, as for the flies and the cheer. Log it
in `docs/assets-log.md` as one line, "Chatter babble, made in code (#144), ours, no third-party source", the same
treatment as the flies note. Fallback if the synthesized sound disappoints: Kenney's CC0 interface and music-jingle packs
(already in the log) pitched down for a "hmm" and up for a "?", but they sound like UI, not talking.

## 6. Variants to compare if a playtest is cheap

Build all behind one debug setting, `ChatterStyle`: **Off / Marks / MarksBabble / GestureBabble**, default MarksBabble.
It is cheap because Marks and MarksBabble share the same system and the gesture variant only adds motion.

| Variant | What the player gets | Why compare it |
|---|---|---|
| **A · Marks** | Bubbles only, silent | Is the babble worth its memory and risk of annoyance? Most people play muted, so this is the safe floor |
| **B · Marks + babble** (recommended default) | Bubbles with a soft babble when zoomed in | The expected winner: warm, readable muted, charming zoomed in |
| **C · Gesture + babble** | No bubbles. Speaker turns toward the listener and bobs the head in time with the babble; the listener nods | Tests the "no UI over the world" idea. I expect it to read weakly on stub-armed dolls at tiny size [I]; it is the one to drop if the playtest agrees |

Order for the Editor agent: build the system and A, then B, and C last. The Editor agent should leave `ChatterStyle` in
the debug panel until the user picks; then delete the losers.

## Sources

- Nookipedia, Animalese: https://nookipedia.com/wiki/Animalese [S]
- PopSci, the Sims language: https://www.popsci.com/story/technology/speak-sims-language/ [S, reports the Maxis audio director]
- games.gg, Simlish: https://games.gg/news/most-popular-simlish-phrases-from-the-sims-4/ [S]
- Godot Babble Engine (idea only): https://godotengine.org/asset-library/asset/5567 [S]
- Unity, `AudioClip.PCMReaderCallback` (6000.3): https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audioclip/pcmreadercallback [V]
- Apple, audio session categories: https://developer.apple.com/library/ios/documentation/Audio/Conceptual/AudioSessionProgrammingGuide/AudioSessionCategoriesandModes/AudioSessionCategoriesandModes.html [V, from search summary]
- OpenGameArt, Talking Synthesizer (CC-BY, rejected): https://opengameart.org/content/talking-synthesizer
- OpenGameArt, Baby Sound Effect (CC0, not babble): https://opengameart.org/node/66037
- Repo: `docs/game-design-practices.md` §10 and §12, `docs/assets-log.md`, `docs/research/11-music-playback.md`, `docs/design/spikes/style-kit.md`
- Not verified: the designers' stated reasons for Banjo-Kazooie's gibberish, Nintendo's own description of Animalese,
  any number for emote-bubble size at phone scale.
