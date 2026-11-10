# Feedback batch 4: batched Editor pass, results

Branch `vs/feedback4-pass` (= `p9/feedback-4`). Editor pass 2026-11-10.

Note: run-tests.sh printed the PREVIOUS run's totals for several short runs (Life = 973, Audio = 417: tooling.md 2026-11-08 trap).
The counts below are checked against the Editor console's `Run finished: N total` lines.

## EditMode (restart, recompile 0 errors; warnings only: Input Manager, CS0108 DayLightDriver, CS0414 PondLife.poolCells)
- Sim 971/973 (2 skipped), Life 417/417, Audio 53/53, ShowMe 125/125, Cards 51/51, Moments 44/44, Shaping 66/66,
  TimeLapseScreen 39/39, Game.UI 39/39
- View 318/320: 1 inconclusive (BoxLeakTests, by design in an interactive Editor), 1 FAIL
  FishRackPiecesTests.TheNewActivitiesHavePosesAndWords ("Expected Arms but was Arms": `PropSlot` resolved to View's
  enum, not Life's). Ours (#543 test). Fixed by qualifying `AquariumCiv.Life.PropSlot`.

## PlayMode (counts checked per run)
- FisherShorePlayTests 1/1 (after a test fix), FishingPlayTests 2/2, PondLifePlayTests 4/4, ApartPondPlayTests 1/1,
  ApartGroupsPlayTests 4/4, KidsTogetherPlayTests 1/1, CameraGlassFeel 3/3, CameraModePlayTests 4/4, PondPlacingPlayTests 2/2,
  LifeSlotPlayTests 4/4, LifeArrivalPlayTests 8/8, SeedGhostPlayTests 4/4, GhostFramePlayTests 3/3, WorldTapPlayTests 7/7,
  CampStashPlayTests 1/1, Tester2FixPlayTests 6/6, SmokePlay 2/2, FishChainPlay 1/1 (after test fixes),
  Fb4CampPropsShots 1/1 (explicit), Feedback3Playthrough 1/1 (explicit)
- CarePlayTests 3/4, twice: Glass_CleanIsClear_DirtyIsObvious_AndAWipeShows (37.4 > 40 wanted; then 26.4 > 45 wanted). Known flake
  (same test failed in the batch-3 pass; render-dependent); not #495..#543.

## Failures that were ours (all test-side, commit on this branch)
- FishRackPiecesTests: `PropSlot` resolved to View's enum. Qualified `AquariumCiv.Life.PropSlot`.
- FishChainPlayTests: fishers keep fishing during the test, so a late catch made rack = 7 not 6; counts now read off the sim, and
  DebugRack sets exactly 6 dry fish (asserts say 6).
- FisherShorePlayTests: counted a fisher the frame it stopped "moving", while still 0.48 u from the water. Now waits for 10 steady frames
  with every fisher within 0.3 u of water. The product code was fine.
- #543 look: dried fish colours darkened (they vanished against the sand).

## Open looks (polish)
- Dried fish on the food pile: three layers of fish stacked in 0.012 u steps read as one tan plank beside the logs. FishSpots could spread.
- AnimLab 543-fish-rack sheet: framing is wrong (the doll sits at the left edge behind a huge grey polygon); no motion judgement possible.
