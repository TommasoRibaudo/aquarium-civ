# Feedback batch 3: batched Editor pass, results

Branch `vs/feedback3-pass` (= `p9/feedback-3` at bdfceb7). Editor pass by the worker-pass agent, 2026-11-09.
Session interrupted once at ~18:55 (Unity quit, Temp/agent wiped); everything below is from after the 19:08 restart.

## Runs
- 19:25 restart (twice: first reload 24.5 s > limit), health 0. Recompile: **0 errors**; 4 warnings, none new
  (Input Manager deprecation; CS0108 ApartGroupsPlayTests/CameraGlassFeelPlayTests, CS0219 ApartTowerPlayTests: files the batch doesn't touch).
- EditMode AquariumCiv.Sim.Tests: 970/972 passed, 0 failed, 2 skipped
- EditMode AquariumCiv.Life.Tests: 385/385
- EditMode AquariumCiv.View.Tests: 315/316, 1 inconclusive = BoxLeakTests.AfterADomainReload_OneBoxAndNoOrphans (inconclusive by design in an interactive Editor)
- EditMode AquariumCiv.Audio.Tests: 53/53
- EditMode AquariumCiv.ShowMe.Tests: 124/124
- EditMode AquariumCiv.Cards.Tests: 51/51
- EditMode AquariumCiv.Moments.Tests: 44/44
- EditMode AquariumCiv.Shaping.Tests: 66/66
- EditMode AquariumCiv.TimeLapseScreen.Tests: 39/39
- EditMode AquariumCiv.Game.UI.Tests: 39/39
- PlayMode SeedGhostPlayTests: 4/4
- PlayMode LifeSlotPlayTests: 4/4
- PlayMode LifeArrivalPlayTests: 8/8
- PlayMode TweezersPlayTests: 5/5
- PlayMode EcoMistViewPlayTests: 1/2. FAIL Stroke_RebuildBudget_BeforeAndAfter: p99 rebuild 10.3 ms (limit 9), worst 22.3 (to check on base)
- PlayMode CarePlayTests: 3/4. FAIL Glass_CleanIsClear_DirtyIsObvious_AndAWipeShows: '0.8 grime is obviously dirty' 29.3 (> 45 wanted) (to check on base)
- PlayMode GestureEdgePlayTests: 15/15
- PlayMode FirstRunPlayTests: 3/3
- PlayMode PeopleWiringPlayTests: 16/16
- PlayMode CaptionEveryLinePlayTests: 6/6
- PlayMode CardStackPlayTests: 22/22
- PlayMode MistDuskPlayTests: 1/4. FAIL Spraying_NeverLightensTheLand(12/20/21): 1.092 / 1.234 / 1.208 (limit 1.08) (to check on base)
- PlayMode Tester1FixPlayTests: 3/3
- PlayMode Tester2FixPlayTests: 2/6. FAIL FirstRunWipe_EdgeToEdgeSwipesWipe_AndNeverTurnTheCamera x4: swipes leave grime 0.08-0.12 (to check on base; #518 risk)
- PlayMode PondLifePlayTests: 3/3
- PlayMode GlassUiPlayTests: 14/14
- PlayMode WorldTapPlayTests: 7/7
- PlayMode HuntViewPlayTests: 2/2
- PlayMode NewGrassPlayTests: 1/1
- PlayMode SmokePlay (category, = SlicePlaythroughSmoke's 2 tests): 0/2. Both stuck at the first-run Wipe step (same cause as Tester2Fix)
- PlayMode SlicePlaythroughSmoke (rerun by class): 1/2. FAIL BarrenBoxFirstRunToCampGate: stuck at Wipe

## Base comparison (CareView, EcoSurface, EcoMistView swapped to origin/dev, recompiled)
- Tester2FixPlayTests on base: 6/6 → the 4 wipe failures (and the smoke's stuck Wipe) are ours (#518's 0.1 s second-finger grace in CareView)
- MistDuskPlayTests on base view files: 1/4, the same 3 fail (1.096 / 1.224 / 1.200): not EcoMistView/EcoSurface; full-dev check below
- CarePlayTests on base view files: 4/4 (rerun on the branch after the fix decides: ours or flaky)
- EcoMistViewPlayTests on base view files: 2/2 (rerun on the branch decides)
- Fix commit 4fe9a59: CareView replays the pending path. Tester2FixPlayTests with the fix: 6/6
- CarePlayTests rerun on the branch (with the fix): 4/4 → the glass-grime failure was flaky (render read 29 vs 45 once; passes on base and branch)
- EcoMistViewPlayTests rerun on the branch: 2/2 → the rebuild-budget failure (p99 10.3 ms vs 9) was a slow-frame flake
- SlicePlaythroughSmoke with the fix: 2/2

## Touch play-through (Feedback3Playthrough, Explicit, category Feedback3Play)
- Run 1 aborted: the advance holds aimed at land off screen after the camp close-up (test bug; now resets to the default view).
- Run 2 aborted after 15 years: with the mister held at 5 spots each season (190 mist pulses) the land was still 62 % bare and the
  camp gate shut at year 15.8. Time alone does not reach the camp gate in a pass-sized run: **blocker for a time-only play-through**.
  Run 3 uses flagged state cheats (camp gate, seed, jars, pond, tree stage) only where 8-16 seasons of misting don't reach a beat;
  every feature beat itself is played by touch.
- Run 3: **1/1 passed, 0 soft failures**, all at 1179x2556, seed 4242, real HUD taps. State cheats (flagged in the log): camp gate
  (not open at year 15), the seed (not unlocked by year 17), a pond (EcoDebug.Table: no pond by year 19), the ducks' jar, the tree's
  stage 3 (stage 1 at year 22). Beats:
  - a) wipe by touch (grime 0.04), held mist, plate → name sheet → kept: `f3_a4_named`.
  - b) first morning 08:15 / 09:00 / 09:45: diggers 0 / 2 (Stream[1], Stream[0]) / 1; others knap, cook, tip loads; at 09:45 four
    stand at the fire in a wary gaze (a reaction to the Keeper, not work). `f3_b_morning_09_00`, `f3_b_morning_09_75`.
  - c) weeds: 18 checks, 47 weed/vine pieces seen, **0 on the fire's ring or settled ground**. `f3_c_weeds_camp`.
  - d) 13 caption lines, 3 crowd lines, **3/3 named** (Ida, Hodge, Ida), 0 "someone". `f3_d_crowd_caption` caught the line mid-fade.
  - e) mister and seed: Kit button "✕ Put away" with each; two-finger pans mist nothing (pulses 1→1) and plant nothing; the seed's
    first tap shows the ghost, the second plants. `f3_e1_mister_putaway`, `f3_e3_seed_ghost`, `f3_e4_seed_planted`.
  - f) thicket then rabbits by touch: pale species silhouettes (bush shapes, rabbit shapes), hints "Tap again to set the bushes / the
    rabbits down here." `f3_f_thicket_glow`, `f3_f_thicket_ghost`, `f3_f_rabbits_ghost`, `f3_f_rabbits_placed`.
  - g) mist at the front-left glass: wet cells 1002→1183, edge cell wet 0.48; the close shot shows a darker strip under the lip of
    the soil face (subtle). `f3_g_wet_default`, `f3_g_wet_close`.
  - h) no pond (year 15): duck ground 0, ducks 0, no duck story. The ducks' jar readied with no water stays without ducks (ducks 0).
    With a pond (424 cells): duck ground 41, all open water; placed by touch: 2 ducks shown, home on water; still no old duck story.
    `f3_h_ducks_on_water`.
  - i) 22:30 with the tree at stage 3: a faint warm patch under the canopy; the fire's pool next to it is far stronger.
    `f3_i_tree_night_default`, `f3_i_tree_night_close`.
  - j) BarrenWind status over 7 s: A 0.65→0.76 @ pitch 1.017→1.025, B 0.70→0.72 @ 0.868→0.859: two voices, gains moving.

## MistDusk on full origin/dev (whole Assets tree checked out from origin/dev, branch-only files moved aside, recompiled)
- MistDuskPlayTests on dev: 2/4; (20) 1.218 and (21) 1.198 fail as on the branch (1.234 / 1.208): **already failing on dev**.
  (12) passed on dev (branch 1.092, base view files 1.096 vs limit 1.08): a marginal case, flaky per the 2026-11-07 note. Not ours.
- Tree restored to the branch, offline csc 0 errors (12 assemblies), Unity recompile clean (scriptCompilationFailed False).

## Summary
- Ours, fixed: Tester2FixPlayTests wipe x4 + both smoke tests (#518 grace swallowed a swipe's first ~0.1 s) → 4fe9a59.
- Flaky, pass on rerun: CarePlayTests glass grime render, EcoMistViewPlayTests rebuild budget.
- Already failing on dev: MistDuskPlayTests (20/21 h), (12 h) marginal.
