# P8 polish run: status (orchestrator memory)

Prompt: docs/prompts/P8-polish-run.md. Integration branch `p8/polish` lives in worktree `F:\GameDev-wt\ecology-lab`.
Lane B (Editor) work happens in `F:\GameDev` on `vs/<n>-<slug>` branches cut from `p8/polish`.

## Checklist
- [x] Branch `p8/polish` from `origin/dev`; this prompt committed
- [x] 1 · #268 #278 #272 (Opus bundle) merged, closed
- [x] 2 · #271 + #266 sim merged
- [ ] 3 · #285 → #267 merged, closed
- [ ] 4 · #287 merged, closed
- [x] 5 · #266 → #277 merged (#266 open for sign-off)
- [x] 6 · #269 → #270 merged (open for sign-off)
- [x] 7 · #279 → #286 merged
- [x] 8 · #282 merged
- [x] 9 · #283 merged
- [x] 10 · #284 merged
- [x] 11 · #288 merged
- [x] 12 · #281 merged (open for shade sign-off)
- [x] 13 · #280 merged (open for a listen)
- [x] 14 · DROPPED by the user 20:07 ("bringing the fire where?"): no fire move. #248 closed earlier; #246/#249 check comments posted, left open
- [ ] 15 · #273 merged (open for sign-off)
- [ ] 16 · #274 merged
- [ ] 17 · #275 → #276 merged
- [ ] 18 · #208–#211 (Opus bundle) merged, closed
- [ ] Linked player feedback closed for every closed work issue
- [ ] Tester runs on cadence; [real] findings fixed; [polish] listed
- [ ] Full EditMode suite + `SlicePlaythroughSmoke` + `EcoPhonePlaythrough` green on `p8/polish`; ecology off = the slice is unchanged
- [ ] The final play-through done by touch, every beat screenshotted and looked at
- [ ] `P8.md` updated: what's in, the tests, the build command
- [ ] PR `p8/polish` → `dev`, CI green, merged
- [ ] Final report

## Log (one line per finished step, with wall clock)
- 2026-11-03 17:20 · branch p8/polish from origin/dev 86c9176; prompt + status + missing EcoPhonePlaythrough.cs.meta committed.

- 17:55 · #287 phase 1 merged (fbb298b → 2646a08), dotnet 1166/1166. Phase 2 (View/TimeLapse drawing) still to do, lane B. No thinning added: 625–956 B/yr.
- 18:05 · #285+#267 phase 1 merged (532ca34 → a4e6c92), dotnet 1171/1171. Phase 2 (cards JobLabel/tribe text, muddy bucket colour, DebugJobsReadout in panel) = lane B item 3B. Agent a7a1dabd6294a00bb can be resumed for it.
- 18:06 · started #288 phase 1 (planner) Sonnet, F:\GameDev-wt\288 vs/288-berries, id aa18817aba416ed2b
- PENDING: full EditMode suite after sim merges (Editor busy with item 1).

## Calls made without the user (→ docs/open-questions.md at the end)

## Agents running
- 17:35 item 1 (#268 #278 #272) Opus, lane B, F:\GameDev on vs/268-278-272-first-run-ui, id a8a20f9fb742ccfce; stall watcher b3b3dfect
- 17:35 item 2 (#271 + #266 sim) Sonnet, lane A, F:\GameDev-wt\271 vs/271-266-grass-pace, id aaf0f485ec25db2b6
- 17:35 item 3 phase 1 (#285 → #267 sim) Sonnet, lane A, F:\GameDev-wt\285 vs/285-267-jobs-land, id a7a1dabd6294a00bb
- 17:35 item 4 phase 1 (#287 records) Sonnet, lane A, F:\GameDev-wt\287 vs/287-history-land, id a329be9e7ff9375f1
- Worker rules: C:\Users\User\AppData\Local\Temp\claude\9364ab10143f1cb1\682069c3-67cc-4488-a463-4ee8f046dedc\scratchpad\WORKER-RULES.md
- Lane B order: 1 → 5 → 6 → 8 → 9 → 10 → 11 → 7 → 12 → 13 → 14 → 3B → 4B → 15 → 16 → 17 → 18. Tester after 6 lane B items / 4 h.

### 18:50 update
- 18:35 · item 1 merged f89ab41 (7d7d373). Closed #268 #278 #272 + feedback #260 #259 #234. Unrun at review: SettingsPlayTests, HudTapTests, PersonCard, Shaping (scroll change) → include in next full PlayMode batch. Polish: founder caption on screen during naming step.
- 18:45 · item 2 merged cabafe5 (#271+#266 sim; dotnet 1193/1193 on merged). Camp with care Y3.3–3.8 (lab 4.3); grassRate 0.5→0.1, campMeadow 0.15→0.20. #271 stays open until item 5's view shows the spread. Lab project keeps old numbers.
- 18:50 · running: item 5 lane B (#266 view → #277) Sonnet id a527ae9174550758a, F:\GameDev vs/266-277-misting, watcher bksoajjvr, runs full EditMode baseline first.
- 18:50 · running: #288 agent aa18817aba416ed2b resumed to merge p8/polish (EcoState conflicts) + fix metas.
- 18:50 · running: item 8 phase 1 (#282 rabbits sim) Sonnet a4a935b344f8b5417, wt/282 vs/282-rabbits.
- 18:50 · running: item 14 phase A (#246/#248/#249 check + fire to first green) Opus a03318bf658dcd2c7, wt/246 vs/246-248-249-leftovers.
- 19:10 · #288 phase 1 merged (e321875), dotnet 1203/1203. Unity save round-trip of new EcoState fields unchecked → next Editor batch (EcoTests). Phase 2 = lane B item 11.
- 19:30 · #282 phase 1 merged (79092a9, conflict in SimCore.Eco.cs EcoTell resolved by me), dotnet 1217/1217.
- 19:45 · #248 closed; kit-wish design gap filed as https://github.com/TommasoRibaudo/aquarium-civ/issues/292
- 19:50 · #281 phase 1 merged (5ac42e9), dotnet 1224 expected; FadeSeconds 180 s.
- 20:15 · #283 phase 1 merged (ab6882e), dotnet 1240/1240. 62% catch, floor 2, hides saved (HidesDrying/TakeHides).
- 20:40 · #284 phase 1 merged (ed5c436), dotnet 1249/1249. HidesPerTent=2. PlayTests may expect a tent w/o hides → phase 2 must check EcoPhonePlaythrough/Smoke camp step.
- (real clock from here; earlier times ran ahead) 19:26 · item 5 merged b492e23 (6000f64+71e9103, resolved PlaceKind: BushCell then Puddle). dotnet 1252/1252; EditMode all green on item 5; EcoPhonePlaythrough rewritten to wet/green-at-once checks (was failing after the #271 retune). Closed #277, #271, #264; #266 open for sign-off (+#263 #265). Fsss is code-made (→ #280). Puddle as muddy bucket source not wired (→ 3B).
- 19:28 · item 6 (#269 → #270) Sonnet lane B id ac165c58ab99e4649, F:\GameDev vs/269-270-water, restart first; watcher bap3n2wpt.
- Item 14 phase A agent a03318bf658dcd2c7 still merging p8/polish (will hit LifeTypes enum: OldFire must go AFTER BushCell, Puddle).
- Lane B items done: 1, 5 (2 of 6 before tester).
- 20:07 · USER: "drop item 14, bringing the fire where?" → item 14 agent stopped, branch vs/246-248-249-leftovers NOT merged (fire move dropped). keepers-hands §2/§2.1 struck. #246/#249 keep their check comments; #249 waits for sign-off.
- 20:07 · item 6 merged (dd15044 via resolved merge), dotnet 1252/1252; #269 #270 open for sign-off (+#261 #258).
- 20:15 · item 8 merged (699edf7), dotnet 1252/1252, RabbitView 2/2, EcoPhonePlaythrough 1/1 (sees rabbits). #282 open for sign-off. Lane B done: 1,5,6,8 (4/6 before tester).
- 20:17 · item 9 (#283 view) Sonnet lane B id a23d23b27c3512583, F:\GameDev vs/283-hunt-view (restart first); watcher bb8uc23jj.
- 20:58 · item 9 merged (668d017), dotnet 1252, HuntView 1/1, EcoPhonePlaythrough 1/1 (hunt beat), #283 open for sign-off. Polish: hunter tiny at default zoom; founder caption covers the box in default view. Lane B done: 1,5,6,8,9 (5/6).
- 21:08 · item 10: first worker ab97127 handed back twice unverified (stopped). EcoPhonePlaythrough with beat 9 PASSED 21:01 on its working tree. Finisher a1a9b165b4b30ce8f took the lock; watcher bij0k17v0. Polish: camera far in phone_12-15.
- 21:35 · item 10 merged (04f543b, a706ee0). Smoke 2/2, HuntView 2/2, EcoPhone 1/1. #284 open for sign-off. Lane B done: 1,5,6,8,9,10 → tester now.
- 21:36 · TESTER run 1 started, id a8f04d15562b0c3c8, watcher b9a4x0bwv. Lane A: #280 sound sourcing ab248ccd3f95ff33e (wt/280). Next lane B after tester: 11 (#288 view), then 7, 12, 13, 3B, 4B, 15, 16, 17, 18.
- 21:37 · #280 phase 1 done on vs/280-opening-sound c710609 (BigSoundBank + OGA rubberduck, all CC0; no .meta; no cracking-dirt CC0 found; mist_spray 4 s). NOT merged: item 13 lane B builds on this branch, commits metas.
- 21:46 · NOTE: subagents stop at a 120-turn limit (tester1 hit it; likely item 10's first worker too). Resume with SendMessage; brief workers to batch and write partial reports.
- 21:50 · #208 phase A merged (46cd2cc), dotnet 1256/1256. Phase B (look + smoke) with item 18.
- TESTER 1 (21:45) verdict fail: [real] → #294 (tent at night, #284), #295 (first-run mist no wet area/spray stops, #266), #296 (120–135 ms first-use frame, #277). Pass: 268 278 272 269/270(no line; pond not surfaced by time alone in 11 y) 285 282 283 284(day) old box.
  [polish] for the user: 4 rabbits tame/sit by the fire at night, hunt a few steps from camp; 5 grass spread reads as fading + square stepped blocks; 6 dark stair-stepped square patch by right front glass; 7 puddles look like slate stones; 8 tracks look like beetles, rabbits on dark-green discs; 9 spray looks like a white tuft at default zoom; 10 builder line card pale over the box; 11 name-step pointer ring covers plate words, Skip all on the rim at 1080x1920; 12 Year 0 HUD vs "chief since year 1"; 13 Cloth mode makes the front glass milky with "Nothing to clean". Pacing: first tent Y10.5–11.5 with 4–5 mists.
- 22:28 · USER: pond outline at the glass doesn't match the hollow's depth (screenshot) → #297; item 6 worker ac165c58ab99e4649 resumed: fix in wt/297 now, Editor after the #294-296 fixer (a5310c69164e1434c, watcher bng1bc93e) releases.
- 22:41 · #294-296 merged 73c3880, closed. Puddles still grey-blue (sign-off on #266). Smoke flake once on card-stack 'Skip all' tap.
- 22:46 · #297 merged c72338a, closed. Next lane B: item 11 (#288 view).
- 22:46 · item 11 (#288 view) Sonnet a5a9e0ed5dd98b13a, F:\GameDev vs/288-berries-view; watcher bs1bj4li2. Lane B since tester1: fixes, #297, 11.
- 23:41 · item 11 merged cb0e376 (#288 open for sign-off; 13 natural bushes at Y12.5). New bugs: #298 old smoke Patch→Rain on base (ecology leak?), #299 particle velocity curves error. Hunt both next (90-min box).
- 11-04 00:09 · #298 #299 merged 5f91bc2, closed. Editor p8.ecology pref now 0. Next lane B: item 7 (#279 → #286).
- 11-04 00:10 · item 7 (#279 → #286) Sonnet a6cb1c9440194e928, full EditMode baseline first; watcher bj5f3yz45.
- 11-04 01:01 · item 7 merged 0cbed7d. EditMode full baseline green (12 assemblies). Pond surfaced Y17 at 11 cells by time; #279 #286 open for sign-off. Next: item 12 (#281 view).
- 11-04 01:01 · item 12 (#281 view) Sonnet a61ce24ece69d3a38; watcher b35o4tu81. Lane B since tester1: fixes, #297, 11, #298/9, 7, 12 → tester 2 after 12.
- 11-04 01:14 · item 12 merged 031bc28. Next: tester run 2.
- 11-04 01:15 · TESTER 2 a65afbea3fc615d29 (watcher btng4qup8). Lane A: #273 phase A a796a3ff9b3123878 (wt/273), #275/#276 phase A a5d1b8d7eecf3fbb5 (wt/275).
- 11-04 01:30 · #275/#276 phase A merged c99c89b, dotnet 1268/1268. #273 phase A on vs/273-camera-glass (3f66925, dotnet 1263) NOT merged: phase B must run BoxCameraTests/FlyTests/CameraMode/Light + feel.
- 11-04 01:49 · TESTER 2 fail: [real] #300 (fresh green glows at night), #301 (first-run wipe: off-glass swipe turns camera). Passes: #294 #295 #296 #297 #288 #279 #286 #281(day) + run1 passes. Beats seed 5150: tracks y0.5, rabbits y1.5, hunts y2.5, bush/berries ~y4, camp+tent y4.8, seed y7.3, pond y24.5 (away), ducks 25 cells.
  [polish] puddles still stones/scorch halo; wording: harvest festival in a tribe, "another hut" in a tent camp, History "Year 1 made camp" vs gate y4.8; cloth toast mentions junk/weeds; first-mist wet area faint; Mister hint every pickup; night tent moment dark; first pond hard-edged rectangle; pond pacing 24.5 y.
- 11-04 02:19 · #300 #301 merged bf8f3b9, closed (+ cloth toast, mister hint once). Next lane B: item 13 (#280 sound, on vs/280-opening-sound).
- 11-04 02:19 · item 13 (#280 wiring) Sonnet a8e25e9a8a5f327d9; watcher br3yrzb48. Remaining lane B: 3B (#285 cards, #267 bucket colour + puddle source, jobs readout), 4B (#287 replay), 15 (#273 phase B on vs/273-camera-glass), 16 (#274), 17B (#275/#276 view), 18 (#208 look, #209 #210 #211). Then tester 3, final play-through, P8.md, PR.
- 11-04 02:32 · item 13 merged f8fc846 (open for a listen). Item 3B Sonnet ab715776317f14915 (watcher bf43q4nea).
- 11-04 02:37 · MY SLIP: merged #280 with a SimTests build break (AudioCueTests used UnityEngine.Resources); fixed with #if UNITY_2017_1_OR_NEWER, dotnet 1272/1272. Rule for me: read the test output BEFORE merging, never chain merge after test.
