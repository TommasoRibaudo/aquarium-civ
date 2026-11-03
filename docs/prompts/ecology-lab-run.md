# Prompt: the Ecology Lab spike (#228), alongside the running P7

Paste everything below the line into a **new** Claude Code session. It's written to run unattended and to survive
context compaction.

---

You're building and running the **Ecology Lab spike, issue #228**: a sim-only lab that grows a barren box into a living
one under four scripted players, and writes top-down time-lapse images so the user can *see* the model before
committing to it. Don't stop to ask: make the sensible call, write it down, and keep going.

## 0. Where you work: P7 is running at the same time
**Another session is building P7** in the main checkout `F:\GameDev` (branch `p7/dilemmas`), and it holds the Unity
Editor. So:
- **Work only in the worktree `F:\GameDev-wt\ecology-lab`** (branch `p8/ecology-lab`, already created; its first
  commit holds the design docs). `cd` there first, and use absolute paths under it.
- **Never** touch `F:\GameDev` (no checkout, no edits, no git commands there), `.agent-editor.lock`, the Unity Editor,
  `unity` commands, or other worktrees. No Unity at all: this spike is lane A.
- **Never edit** anything under `AquariumCiv/Assets/**` or `Tools/SimTests/**` (P7 is changing them). You may **read
  and compile** game files (e.g. `Sim/Settle/Terrain.cs`, `SiteMap.cs`) from your own project, read-only.
- Your code lives in **`Tools/EcologyLab/`**: a .NET 8 console runner + an NUnit test project of its own, outside
  `Assets/` (so Unity never imports it and no `.meta` files are needed).

## 1. Resume first
Read `docs/overnight/ECOLOGY-STATUS.md` in the worktree. If it exists, continue from the first unchecked step. If it
doesn't, create it from §5. Update it after every finished step (one line: what, commit, anything odd), and check the
wall clock each time.

## 2. Read before any code
- `CLAUDE.md` (the rules that apply without Unity: branches and PRs, `docs/tooling.md` lessons, free assets only).
- **The model: `docs/design/keepers-hands.md`** (§2 the arc, §4 the one tree, §5 care: humidity, the water table,
  the lowlands rule, absence, what overgrows). Where it differs from research 15, it wins: misting (no rain, **no
  storms**, no nibbling, no lid), the people are there from the start.
- `docs/research/15-barren-to-living-box.md` (§3 the ecology rules, §6 the systems, §9 the lab).
- The issue: `gh issue view 228`. Its What and Done when are binding, except **Touches**: use `Tools/EcologyLab/**`
  and `docs/design/spikes/**` instead of `Assets/Spikes/**` and `Tools/SimTests/**` (§0).
- Prior art: `docs/design/spikes/growth-lab.md` (how a spike is run, shown and written up here),
  `AquariumCiv/Assets/Game/Sim/Settle/Terrain.cs`, `SiteMap.cs` (the 60 × 44 grid, height, slope, fertility, water
  distance), `SimCore.cs` (`SimRandom`, the month step).

## 3. The work
1. **Model** (`Tools/EcologyLab/Lab/`):
   - Cell layers: soil, moisture, cover.
   - Box humidity and a water table.
   - Basins with spill levels (priority-flood).
   - The one tree (stages, needs: water, light = grime + droplets, weeds; four seasons; wilts and pauses, never dies).
   - The forest's spread by birds, and the forest's edge.
   - Rabbits and birds as counts.
   - Natural dew and rare showers.
   - The camp gate (meadow + rabbits) and the village gate (water at the surface + N grown trees).
   Deterministic: hash of seed + cell + season. One step per game season.
2. **Players:** (a) does nothing; (b) mists now and then, plants, never weeds or wipes; (c) mists steadily, plants,
   full tree care; (d) like (c) but over-mists. 40 game years, 3 terrain seeds (the river-valley presets) each.
3. **Output** (`docs/design/spikes/img/ecology-lab/`):
   - PNG frames every 2 years (cover colours per cell, water blue, the tree marked, a cross-section strip with the
     water table), from a tiny PNG writer (`System.IO.Compression`).
   - One contact sheet per strategy and seed.
   - CSV curves: grass %, bush %, trees, rabbits, humidity, water-table height, pond area, and the years of: camp
     start, tree grown, water surfacing, village gate.
4. **Look at every contact sheet** (open the PNGs). Tune the constants until the arc of keepers-hands §2 happens in
   order and the times fit a camp era of a few real days (1 real hour = 1 game year): camp start within the first
   game years with misting, the tree grown in a few years with care and 2–3× slower without, water surfacing before
   the village. Write down each tuning round in the status file.
5. **Tests** (`Tools/EcologyLab/Tests/`), as in #228's Done when:
   - determinism;
   - (a) still greens;
   - (c) grows the tree clearly sooner than (b), and (a) only slowly or never;
   - (d) surfaces water sooner than (c);
   - ponds only in basins;
   - nothing ever dies back;
   - the flood line is dry at the start.
   Run: `dotnet test Tools/EcologyLab/Tests`.
6. **Write-up** `docs/design/spikes/ecology-lab.md`, in the Growth Lab's shape:
   - the model in plain words;
   - the contact sheets inline;
   - the curves summarised in a table;
   - answers to the four questions (does barren → living read right over time; do the levers matter; does absence
     still progress; are the times right);
   - the tuned constants, and what surprised you;
   - what a P8 prototype with art would need.
7. **Ship:**
   - Commit as you go, push `p8/ecology-lab` (only this branch; never force-push).
   - Open a PR to `dev` with `gh pr create`. Body: what it is, the contact sheets, and "touches only `Tools/EcologyLab`
     and docs".
   - Let CI pass, then merge. If CI fails on something that also fails on `dev`, say so in the PR and leave it unmerged.
   - Comment the results on #228. Leave #228 **open**: the user's go / reshape / park closes it.

## 4. Models and effort
Do it yourself (Opus): tuning a model by eye is judgement work. Use a Sonnet sub-agent only for mechanical pieces (the
PNG writer, the CSV, the contact-sheet layout), with a tight brief.

## 5. Checklist (copy into ECOLOGY-STATUS.md)
- [ ] Read §2; the plan written at the top of `docs/design/spikes/ecology-lab.md`
- [ ] `Tools/EcologyLab` builds; model v1 runs 40 years for 1 seed
- [ ] PNG frames + contact sheets + CSV
- [ ] All 4 strategies × 3 seeds; contact sheets looked at
- [ ] Tuning rounds until the arc reads (each round logged)
- [ ] Tests green
- [ ] Write-up done
- [ ] Pushed, PR open, CI green, merged; results on #228
- [ ] Final report

## 6. Final report (to the user)
Short and plain:
- What was built, and the PR link.
- **The four contact sheets for one seed**, sent so the user can see them (SendUserFile), plus where the rest are.
- The answers to the four questions, in one line each, and the key years per strategy.
- What surprised you, and what you'd change in `keepers-hands.md`.
- What the user decides: go to a P8 prototype with art, reshape, or park.
