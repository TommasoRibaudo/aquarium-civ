# Ecology Lab spike (#228): status

Branch `p8/ecology-lab`, worktree `F:\GameDev-wt\ecology-lab`. Run prompt: `docs/prompts/ecology-lab-run.md`.
No Unity, no edits under `AquariumCiv/Assets/**` or `Tools/SimTests/**`.

## Checklist
- [ ] Read §2; the plan written at the top of `docs/design/spikes/ecology-lab.md`
- [ ] `Tools/EcologyLab` builds; model v1 runs 40 years for 1 seed
- [ ] PNG frames + contact sheets + CSV
- [ ] All 4 strategies × 3 seeds; contact sheets looked at
- [ ] Tuning rounds until the arc reads (each round logged)
- [ ] Tests green
- [ ] Write-up done
- [ ] Pushed, PR open, CI green, merged; results on #228
- [ ] Final report

## Log
- 2026-11-02 16:50 started. Read the prompt, keepers-hands, research 15, #228, growth-lab, Terrain/SiteMap.
  Notes: `*.csproj` is git-ignored at the root except SimTests, so the lab's csproj files are added with `git add -f`
  (no root .gitignore edit). CI's sim job runs only `Tools/SimTests`; the lab's tests run locally (not wired into CI,
  that would touch ci.yml).
