# Playtest 1, batch 2 — run status

Prompt: `docs/prompts/playtest-1-batch-2-run.md`. Integration: `p9/playtest1-b` in `F:\GameDev-wt\pt1b`.

## Checklist
- [x] `p9/playtest1-b` from `origin/dev`; this prompt committed (already on dev via #712)
- [ ] 1 · #697 → #696 cause written in both issues; merged
- [x] 2 · #699 merged (lane A; Unity check pending)
- [x] 3 · #703 merged (lane A; jar look in Unity pending) — call: jar shut while frozen
- [~] 4 · #700 rule merged 969c8b2 (racks + Keeper's fire props); spec B27 written; beat not built (needs user sign-off)
- [ ] 5 · #698 merged
- [ ] 6 · #669 merged (buildings too)
- [x] 7 · #702 merged (1ada892)
- [x] 8 · #707 merged 2d1156e (open for look sign-off)
- [ ] 9 · #704 merged (open for look sign-off)
- [ ] 10 · #701 merged (open for look sign-off)
- [ ] 11 · #676 stew by kind merged (open for look sign-off)
- [ ] 12 · #706 merged (open for look sign-off)
- [ ] 13 · #705 specced, built and clipped, on its branch
- [ ] 15 · #655 numbers in the issue (if time)
- [~] Design proposals for #708, #709, #710 + #658, #687 written (open-questions.md + issue comments)
- [ ] Every issue's *Done when* ticked with evidence, or listed as short after 4 rounds
- [ ] Batched Editor pass green (EditMode 10 assemblies, PlayMode classes, smoke); failures fixed or noted as baseline
- [ ] `Playtest1BPlaythrough` + the touch play-through done, every beat looked at
- [ ] Tester run(s) on cadence and at the end; [real] fixed; [polish] listed
- [ ] Walkthrough: branch, open-questions.md section, `Staged()` stops, auto run clean, every shot looked at
- [ ] Work issues closed (sign-off ones left open with a comment); answered feedback notes closed
- [ ] `docs/tooling.md` entry for any tooling problem solved; Run In Background ON; lock released; Editor ready
- [ ] PR `p9/playtest1-b` → `dev`, CI green, merged
- [ ] Final report to the user

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

## Round logs
### #702
- round 1: all boxes ticked with evidence; accepted.

### #707
- round 1: accepted; look sign-off pending (off-home icon contrast to check).

### #699
- round 1: accepted (lane A). Unity checks in the batched pass.

### #700
- round 1: ConsequenceProps not wired, full SimTests not finished → round 2.

### #703
- round 1: accepted; Unity look of the shut jar in the chooser pending.

### #697/#696
- round 1: all lane A boxes ticked; #696 guard tests didn't fail on base (noted). Lane B verification pending.

