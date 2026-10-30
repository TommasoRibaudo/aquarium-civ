# 11 — When music plays (Minecraft-style sparse music)

Status: first pass, 2026-10-29. Two tracks are imported (`Assets/Audio/Music/`, credits in `CREDITS.md`).
Nothing is wired up yet. The source for the Minecraft numbers is the [Minecraft Wiki "Music" page](https://minecraft.wiki/w/Music);
only the first ~100k characters of it were read, and the in-game delay is flagged `[verify]` on the wiki itself.

## What Minecraft does

- **Music is rare on purpose.** After a track ends, the game waits a random **10–20 minutes** (wiki, unverified) before
  the next one. Silence and the ambient sounds are most of the time.
- **Menu:** a random menu track starts **1–30 s** after the screen opens, and stops when the world loads.
- **Random pick, no fixed playlist.** Pools differ by place (menu, overworld, creative, underwater, Nether, End).
- **Music yields:** it fades out for a music disc, for special areas, and a boss track starts instantly in the End.
  Music stops when you leave the world.
- **Player control:** a music volume slider, and in newer Java versions a "Music Frequency" option
  (added 25w20a), and music now also plays while the game is paused.
- The wiki doesn't say whether tracks avoid repeating. Third-party descriptions of the sound (a Chrome extension
  imitating it) avoid back-to-back repeats.

## Why it works

Rare music keeps each track feeling like an event instead of wallpaper, avoids ear fatigue in a game people leave
open for long sessions, and leaves room for diegetic sound (here: people, fire, rain, the glass tap).

## Proposal for Aquarium Civ (to decide, not decided)

An idle game is opened in short check-ins and sometimes left running, so:

1. **Opening a session:** a short random delay (say 5–20 s), then ~50 % chance a track starts, fading in over ~4 s.
   Not every open has music, so it stays special.
2. **During a session:** after a track ends, a random gap of **3–8 minutes** (shorter than Minecraft's, since our
   sessions are shorter; tunable), then the next track, never the same one twice in a row.
3. **Moments beat music:** the era moment, the evening talk, a death/mourning evening and the glass tap duck or
   skip the music (fade down ~1 s, return after). Music never starts during a moment.
4. **Backgrounded / screen off / phone muted:** pause, don't continue to a silent timer. Respect the device silent
   switch on iOS. No music from a notification-triggered launch until the player acts.
5. **Controls (needed at the menu):** a music volume slider and a frequency setting (Rare / Normal / Off).
   Until there is a menu, the debug panel gets a "play music now" cheat and a music on/off toggle.
6. **Tracks:** Calm Piano 1 (Vaporware) and Forget Me Not both read as calm; pick by era later if we add more
   (e.g. camp = sparse piano, town = fuller). Needs a listen to check they match the glass-and-warmth look.

## Technical notes for later

- Import music as Streaming / Vorbis on mobile to keep memory low (the mp3 is 320 kbps, 6.6 MB; consider re-encoding
  to ogg to save build size). Files go through Git LFS.
- A small `MusicDirector` (scheduler with a seeded random gap, duck requests from moments) plus a mixer group
  for music; test the scheduler logic in EditMode with a fake clock.

## Built (2026-10-29)

- `Game/UI/MusicScheduler.cs`: pure logic (open delay 5–20 s with 50 % chance, else a 3–8 min gap; gap 3–8 min after a
  track; no same track twice; nothing starts while held; Off/Rare(×2 gap)/Normal) + `Tests/MusicSchedulerTests.cs`.
- `Game/UI/MusicDirector.cs`: loads `Assets/Audio/Resources/Music/*`, 4 s fade in, 3 s fade out, pauses with the app
  (a return from away rolls a fresh chance), `MusicDirector.Hold("who")` / `Release("who")` for moments (only the era rebuild wave calls it so far), bootstraps itself when a `GameClock` is in the scene.
- `GameSettings.MusicFrequency` / `MusicVolume` (PlayerPrefs, no UI yet); debug panel: "Music now", "Stop music",
  frequency cycle and a status line.
- `Game/UI/DeviceSilent.cs`: Android reads the ringer mode (silent or vibrate = no music, a playing track fades
  out). iOS relies on Unity's default Ambient audio session, which follows the silent switch; **verify on a real
  iPhone and an Android phone** (untested).
- Clips import as Streaming + Vorbis q0.6 (smaller build). Checked in play mode: a track fades in to the music volume,
  `Hold` fades it to 0 in ~1 s, `Release` brings it back; EditMode tests pass (UI 20, View 34).
- Still to do: call `Hold/Release` from the story scenes (evening talk, mourning, era moment UI) once the life planner
  is wired into the view; a settings UI for volume/frequency; a real-device check of the silent behaviour.

## Open questions

- Gap length and the 50 % opening chance: need real play sessions to tune.
- Verify the Minecraft 10–20 min figure (wiki marks it unverified) if it matters for the final numbers.
