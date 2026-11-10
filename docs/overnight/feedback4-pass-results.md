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
