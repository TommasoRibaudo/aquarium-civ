# Playtest 1, batch 2 — run status

Prompt: `docs/prompts/playtest-1-batch-2-run.md`. Integration: `p9/playtest1-b` in `F:\GameDev-wt\pt1b`.

## Checklist
- [x] `p9/playtest1-b` from `origin/dev`; this prompt committed (already on dev via #712)
- [x] 1 · #697 → #696 cause written in both issues; merged (b043834 + 74e22c4); close after the batched pass
- [x] 2 · #699 merged (lane A; Unity check pending)
- [x] 3 · #703 merged (lane A; jar look in Unity pending) — call: jar shut while frozen
- [x] 4 · #700 rule merged 969c8b2; beat B27 built on vs/700-beat (9ed15c4) for sign-off only
- [x] 5 · #698 merged 760c9d3 (cause: tent-bound doll drawn Hidden short of its slot when the way was blocked; original tester vanish not reproduced directly)
- [x] 6 · #669 merged (buildings too) — call: live animals and berries hidden in a replay (no per-year record); rack/pile/frame recorded from first live sight
- [x] 7 · #702 merged (1ada892)
- [x] 8 · #707 merged 2d1156e (open for look sign-off)
- [x] 9 · #704 merged (open for look sign-off)
- [x] 10 · #701 merged (open for look sign-off) — short: small/grown pond shots unreachable on seed 4242 (pond floods to 424 cells); rule asserted on the mesh
- [ ] 11 · #676 stew by kind merged (open for look sign-off)
- [x] 12 · #706 merged (open for look sign-off)
- [x] 13 · #705 specced, built and clipped, on vs/705-build (e6f7597), for sign-off only
- [ ] 15 · #655 numbers in the issue (if time)
- [~] Design proposals for #708, #709, #710 + #658, #687 written (open-questions.md + issue comments)
- [ ] Every issue's *Done when* ticked with evidence, or listed as short after 4 rounds
- [x] Batched Editor pass at 408c38b: EditMode 10 assemblies 2372 run, 0 failed; PlayMode 26 classes + 3 categories, smoke 2/2. Baseline (fail on d0c2008 too): DeerWolf 2 (under crowns), BoxComplete clockHours. Care glass 1 fail then 4/4 on rerun (flake). Hugs 1 inconclusive (known).
- [x] `Playtest1BPlaythrough` 14/14 by touch at 1179×2556 (shots in the scratchpad pt1b-play). Open: ducks not visible on the pond in 03e/03g (count holds); History end needs the caption X before Close; last sleeper walk-in not proven, only "nobody up".
- [ ] Tester run(s) on cadence and at the end; [real] fixed; [polish] listed
- [ ] Walkthrough: branch, open-questions.md section, `Staged()` stops, auto run clean, every shot looked at
- [ ] Work issues closed (sign-off ones left open with a comment); answered feedback notes closed
- [ ] `docs/tooling.md` entry for any tooling problem solved; Run In Background ON; lock released; Editor ready
- [ ] PR `p9/playtest1-b` → `dev`, CI green, merged
- [ ] Final report to the user

## Extra queue (user, 2026-11-15: chain more well-defined easy todos tonight)
Picked: open Polish issues with a clear Done-when, no `needs:user`, not out of scope, not built yet.
- [x] #689 (merged; smoke in the batched pass) a village house's card says the year it was built (lane A, vs/689-built-year)
- [x] #690 (merged; smoke in the batched pass) deer only when the wood is dense (lane A, vs/690-dense-wood; wolves' chain #437 checked)
- [x] #684 (merged; not fixed before: stroke ended at the ground; live stance still to watch) axe meets the trunk (user: probably fixed; check first, close if so)
- [x] #561 (merged; call: Skip all dismisses the stack) Skip all clears the History dot (check still true first)
- [x] #638 (merged; test drags started off the glass) TelemetryPlayTests finger wipe (90-min time box)
- [x] #562 (merged: lone-word captions balanced; 6 already fine, 2 not reproducible; tablet hint not checked) small UI tidy from tester run 4 (check each still true)
- [x] #563 (merged: own icons, dots shrink, trait speaker named, toast above the bar; callback sheet height left for the user) Keeper's dilemmas card and bar polish (check each still true)
Skipped: #560/#672 (built in #678, waiting for a phone check), #660–#662 (inside #668), needs:user and design issues, P10.

## Log
- 2026-11-14 ~19:30 · run started (orchestrator in the session opened at F:\GameDev); worktree pt1b at d0c2008. C: 97% full (19 GB free) — watch editor-health exit 2.

- 19:25 · design proposals (#708 #709 #710+#658 #687) by a Sonnet docs worker: 86092c4, merged 4d32530 into p9/playtest1-b. Still to do: copy them into open-questions.md (§6) and post the issue comments.
- 19:30 · F:\GameDev holds p9/signoff with the Editor of the other session (Dev, idle). Rather than use it, I seeded pt1b's Library by robocopy (2.4 GB) and made the Editor scripts take PROJ/EDITOR_LOG/LOCK/UNITY_LIB (b8253e4, tooling.md entry). The second Editor opens on F:/GameDev-wt/pt1b/AquariumCiv with its own log Editor-pt1b.log. The lock for it is F:\GameDev-wt\pt1b\.agent-editor.lock.
- 19:20 · #697→#696 Sonnet worker started (lane A, F:\GameDev-wt97, vs/697-696-replans).
- 19:28 · pt1b Editor ready (warnings only). Layout: **integration merges in F:\GameDev-wt\pt1b-int** (p9/playtest1-b); **F:\GameDev-wt\pt1b is the Editor folder**, switched to each lane B worker's vs/<n> branch (from p9/playtest1-b). Editor-worker addendum: scratchpad editor-worker-rules.md.
- 19:28 · Lane B started early with #702 (UI, no Life overlap) while #697 is in lane A; order otherwise as the prompt. Stall watcher running.
- 20:05 · #702 round 1 accepted: pill in FeedbackUI's own doc (sort 100); FeedbackPillPlayTests 1/1 (7 panels, real touch), smoke 2/2, UI 6/6, Settings 10/10, Glass 14/14, Pulse 5/5. Looked at card.png + moment.png: pill on top. Open: BoxCompletePlayTests 2/3 (clockHours string) not run on base → check in the batched pass; pill has no backing in the card shot → passed to #707. Merged 1ada892. Tooling: run-tests.sh exports UNITY_PROJECT_PATH (8d79379).
- 20:08 · #707 (+#649 gear) worker started in the Editor folder, vs/707-hud-top.
- 20:40 · #697/#696 round 1 (lane A) accepted: 53a139d; my SimTests run 1921/1921; diff read (pin moved into ClearDays, keeps sleep/walk segment, standing → short walk). Merged b043834. Cause posted on both issues. #696 not reproducible headless (base passes night tests; unprotected paths were direct ClearDays). Lane B part (ReplanKeepsPlansPlayTests 20 cases, LifePlannerTests, TentSleepers, 10 game min watched, smoke) after #707.
- 20:42 · lane A started in parallel: #699 (F:\GameDev-wt99), #703 (703; call ★ jar shut while frozen), #700 (700; rule + spec only).
- 21:05 · #707 round 1 accepted: 0df81b6; HudTopRowPlay 1/1, UI 6/6, Settings 10/10, pill 1/1, Glass 14/14, smoke 2/2 on rerun (1st run failed 'rain button' FirstRun — flake? check in batched pass). Looked at day/night crops: one look, no backing, title ink. #649 gear already a gear (72c9a6c). Walkthrough stop: off-home readability of bare icons. Merged 2d1156e.
- 21:07 · #697/#696 round 2 (Editor play check) sent to the same worker on vs/697-696-play.
- 21:35 · #699 round 1 accepted: ff303eb; my SimTests 1924/1924. Cause: DeerView walked a straight line to its goal. Fix: DeerHerd.Field.Steer (0.9 u shy ring round the fire, slides round lot/tent cells), every deer step through it. To check in Unity: smoke, DeerHunt/DeerScenes play tests, cheat "Deer at the camp" close shot (no jitter/stuck at the ring). Merged.
- 21:35 · #700 round 1: rule + quiet-moment + spec B27 accepted; round 2 sent (wire ConsequenceProps path check; full SimTests count; dirt redraw?).
- 22:10 · #700 round 2 accepted: ConsequenceProps path-aware, props move at night; SimTests 1927. Merged 969c8b2. Merged branch: SimTests 1930/1930, offline csc 0 errors (Sim, Life, Life.Tests, Game, Game.UI, View, View.Tests, View.PlayTests).
- 22:15 · #703 round 1 accepted: c11423a+81aec5c, SimTests 1924. Call (recorded): ★ the duck jar is shut while the pond is ice; need line "The pond is ice. Ducks will come when it thaws."; a placing made before the freeze is kept (ducks leave for winter per #618, come back). Test change reviewed: EcoTests' once-a-year winter Keeper now places a thawed duck jar outside its visit (lab artifact); PlacedLifeTests accepts 'shut under ice'. Risk for the user: a winter-only player waits until spring for ducks (village gate up to 9 months later). Merged.
- 22:45 · #676 lane A round 1 accepted: affcc34 (+ my .meta commit; the worker made none). StewKinds.Decide: rabbit (hunt this/last month) > fish (pile has fish) > berries/roots (food) > thin. MealDirector.LookOf per kind. SimTests 1941. Merged 0d00f73. Lane B part (StewLookTests, StewKindTests, MealDirectorTests; fish + rabbit stew shots day/dusk) queued for the Editor.
- 23:00 · #697/#696 round 2 (Editor): b6eeab7. ReplanKeepsPlansPlayTests 22/22, LifePlanner 24/24, TentSleepers 4/4, EcoTent 10/10, smoke 2/2, day watch (year 17, ~10 clock min, triggers every 30 s) PASS → #697 done. New #696 cause found+fixed: ChooseBuilders took sleeping adults to raise a night tent (test fails before/passes after). Night watch still FAILS: sleeper 23 shown at the tent at a chronicle+nudges+belief replan while the plan says Sleep=Home → a view path (scene/reaction) shows sleepers. Round 3 sent (instrument doll visibility, fix at source; 90-min box).
- 23:40 · #696 round 3 accepted: 406644d. View cause: a replan/new sleeper renumbers the home's slots; the hidden doll 'walked' to its new slot and the walk style replaced Hidden (PeopleView.Drive). Fix: asleep/indoors dolls take a changed slot at once, never the walk style. Night watch 1/1 (0 sleeper frames), day watch 1/1, ReplanKeepsPlans 22/22, smoke 2/2. Merged 74e22c4; view cause posted on #696. Merged-branch SimTests (after #676) 1941/1941.
- 23:42 · Editor restart before #698 (3 tickets since start: #702, #707, #697; a recompile returned 400).
- 23:55 · #698 Editor worker started (after an Editor restart). #705 spec B28 written on vs/705-laundry (a53f2df → round 2 977f0f4: cloths 0.11×0.13 u ≈ 14–16 px at the default camera, oversized vs dolls on purpose; rain dash dropped (polish phase)). Not built yet; the build + AnimLab clip go on the same branch in the Editor; user signs off spec then clip. Design-proposal comments posted on #708 #709 #710 #687.

- 2026-11-14 23:15 · #698 merged (760c9d3) after 3 rounds

## Round logs

### #706
- round 1: tags existed but carried opaque labels and the bar said only "Touch one."; new option `touch` words, bar "Touch one: A, or B." for 8 dilemmas + 3 small choices. ChoiceScene 10/10, ChoiceTagLayout 9/9, DilemmaTests 35/35, smoke 2/2. Newcomers shot looked at. Village choices and naming not shot (unreachable at year 9). Accepted.

### #690
- round 1 (lane A): WoodTrees (grown tree with 4+ others within 3 cells); jar at 12. 8-seed lab: deer jar ~8 game years later (y23–29), wolf jar same year. SimTests 1947/1947. Accepted. Call for the user: numbers [est], deer scenes arrive later.

### #689
- round 1 (lane A): cause = the camp tent rebuilt in place as the house kept its camp month; card reads BuiltMonthOf (village start for hut/farm/lumbermill; old month-0 saves take the lot month). SimTests 1947/1947. Accepted.

### Tester run 1
- [real] rack cheat stack overflow (DebugRackAt → BasePlace → PlanBase loop): fixed 7b66c93 + regression test (crashes without the guard).
- [polish→fixed] History timeline end hid berries and rabbits: fixed 7b66c93.
- [polish, for the user] glass edge line crosses year line and gear when close (#707); Feedback pill alone in photo mode: #702 asks for it there (binding, tested), #673 says no button in photo mode: DECISION for the user; fish vs thin stew close at mid zoom (#676); touch-hint ring covers "name" on the naming step.
- Not checked: kid seen walking into tent (#698), real winter ice for #703, return welcome. Box mesh filters 36 vs 14 after runs: to check.

### #704 + #701 (one worker, one branch)
- round 1: #704 cause = clearing outer band blended to fixed grassB; now ground colour. #701 cause = whole-cell water mesh; now 3×3 pieces under the level by 1.2 cm, ice shares it. EcoView 7/7, PondIce 7/7, EcoWater 1/1, PondLife 4/4, smoke 2/2, RimShots explicit pass. Shots looked at. Accepted with the small-pond gap noted.

### #669
- round 1: ReplayGate + ThingSince; HistoryBuilt 1/1, TimeLapsePlay 5/5 (perf step 12.2 ms flaked once, passed alone), TimeLapseScreen 3/3, smoke 2/2, SimTests green. Shots looked at. Accepted.

### #698
- round 1: inTent snap (from #696) fixed with TentArrived guard; ChaseEndPlayTests 2/2. Original cause not found.
- round 2: ruled out several paths; nothing caught.
- round 3: ChaseLong (8 natural chases, ~42.8k frames, 6–18 h) caught kids drawn Hidden 0.14–0.75 u short of the slot (blocked way → goal fell back to the place middle). Fixed: walk until there. ChaseEnd 2/2, ReplanKeepsPlans 22/22, smoke 2/2. Accepted.
### #702
- round 1: all boxes ticked with evidence; accepted.

### #707
- round 1: accepted; look sign-off pending (off-home icon contrast to check).

### #699
- round 1: accepted (lane A). Unity checks in the batched pass.

### #700
- beat round 1 (vs/700-beat 9ed15c4): RackMove plan + beats + cheat "Rack moves now"; RackMoveTests 24/24, RackPath 6/6, FishChain 13/13, RackMovePlay pass, smoke 2/2. Spec timing (0.5 view h) wrong: ~22 s real. AnimLab sheets lost to an Editor restart (Temp wiped); re-render on the walkthrough branch.
- round 1: ConsequenceProps not wired, full SimTests not finished → round 2.

### #703
- round 1: accepted; Unity look of the shut jar in the chooser pending.

### #676
- round 1 (lane A): accepted; meta files added by me. Look pending in Editor.

### #705
- build round 1: Laundry rules + beat + cheats; LaundryTests 20/20, Laundry play 3/3, smoke 2/2; AnimLab hang/take-in/fade sheets look right. Calls: DawnStagger village worst limit 12→13 (chore stretch); take-in rare (5 of 113 in 40 village days, the rest fade); line small at full-box zoom.
- spec round 1: cloths ~7 px (unreadable), rain dash (new beat) → round 2: fixed. Spec accepted for the user's sign-off.

### #697/#696
- round 1: all lane A boxes ticked; #696 guard tests didn't fail on base (noted). Lane B verification pending.
- round 3: view cause found + fixed; all boxes ticked.
- round 2: #697 boxes ticked (day watch). #696: night-builder cause fixed, night watch red (view shows a sleeper at a chronicle/nudge/belief replan). Sent back.

