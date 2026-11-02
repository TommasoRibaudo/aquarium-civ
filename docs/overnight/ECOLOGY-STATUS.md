# Ecology Lab spike (#228): status

Branch `p8/ecology-lab`, worktree `F:\GameDev-wt\ecology-lab`. Run prompt: `docs/prompts/ecology-lab-run.md`.
No Unity, no edits under `AquariumCiv/Assets/**` or `Tools/SimTests/**`.

## Checklist
- [x] Read §2; the plan written at the top of `docs/design/spikes/ecology-lab.md`
- [x] `Tools/EcologyLab` builds; model v1 runs 40 years for 1 seed
- [x] PNG frames + contact sheets + CSV
- [x] All 4 strategies × 3 seeds; contact sheets looked at
- [x] Tuning rounds until the arc reads (each round logged)
- [x] Tests green
- [x] Write-up done
- [x] Pushed, PR open, CI green, merged; results on #228
- [ ] Final report

## Log
- 2026-11-02 16:50 started. Read the prompt, keepers-hands, research 15, #228, growth-lab, Terrain/SiteMap.
  Notes: `*.csproj` is git-ignored at the root except SimTests, so the lab's csproj files are added with `git add -f`
  (no root .gitignore edit). CI's sim job runs only `Tools/SimTests`; the lab's tests run locally (not wired into CI,
  that would touch ci.yml).
- 17:0x model v1 builds and runs (12 runs, frames, in ~5 s).
- Tuning rounds 1-7 (details in docs/design/spikes/ecology-lab.md §6): v1 water too early + speckled forest + (a) no tree;
  mister soak down (no effect); broke a runaway loop (capillary-wet soil soaked back into the table) + soak cap per season;
  showers up, bird seed for absence (+ a roll-at-1.0 bug fixed), village gate pond 20 / trees 8; wild trees slower;
  seeds land near trees, fewer bushes, pond keeps growing; seed at first bushes, table starts deeper. Final: arc in order
  for (c) on all 3 seeds; (b) tree 2.7× slower; (a) village Y68-91.
- Tests: 20/20 green first time (`dotnet test Tools/EcologyLab/Tests`). Commit 06ffe79 + write-up commit.
