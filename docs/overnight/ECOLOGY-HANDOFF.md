# Handoff: Ecology Lab (#228) → the P8 prototype (2026-11-02)

## Where things stand
- **Ecology Lab done and merged** into `dev` (PR #230). It's a sim-only lab in `Tools/EcologyLab/` (.NET 8 + 20 NUnit
  tests). The write-up, contact sheets, curves and tuned constants are in `docs/design/spikes/ecology-lab.md`.
  Result: the arc of `keepers-hands.md` §2 happens in order. With care the village comes in about a real day,
  casually in about 2½, and with no player in 3–4.
- **The user's decisions after the lab** (PR #232, `keepers-hands.md` §2.1):
  - the tribe starts with one tent and a fire, and builds no new tent before the camp gate;
  - no time skip in the first run;
  - slow life before the first green;
  - an immediate reaction to every early mist;
  - signs on the land between the gates;
  - barren sounds that fade as the land greens.
- **Still open, the user's call:** #228's **go / reshape / park**. Nothing below starts in Unity before a "go". Also
  open from the write-up (§6), for the user:
  - the tree's light need is a weak lever (strengthen it, or drop it);
  - the people take over watering after 3 mistings (keep that as the story?);
  - the forest's edge needs a brake (woodcutters or the snips).
- Branches: `p8/ecology-lab` (merged), `p8/tribe-before-camp` (PR #232, docs only). Worktree
  `F:\GameDev-wt\ecology-lab`. The lab never touched `Assets/`, `Tools/SimTests` or the Editor.

## Left to do without the Editor (lane A, any time)
1. **Wire the lab's tests into CI**: add `dotnet test Tools/EcologyLab/Tests` to the `sim-tests` job in
   `.github/workflows/ci.yml`. It was skipped to keep the spike off shared files. It's about 10 s, needs no secrets,
   and `Tools/SimTests` shows the pattern.
2. **Port the model into the game's sim** (pure C#, testable in `Tools/SimTests`), as the start of a P8 sim issue:
   - `Eco` becomes a land layer on `SiteMap`;
   - cover per cell replaces the forest and meadow flags and the static pond (`WaterY`);
   - the main hollow's spill level becomes the flood line that `Placer` must respect (§5.3, never build below it);
   - the gates feed `SimConfig.villageAt` (camp: meadow ≥ 15 % + rabbits ≥ 2, so tents may be built; village: a
     pond ≥ 20 cells + ≥ 8 grown trees + today's population rules);
   - the save gains cover (bytes), soil and moisture, plus table, humidity, glass and tree state, with a save-version
     bump (`docs/design/spikes/save-versioning.md`);
   - catch-up: 192 seasons over 48 h is cheap; test live = catch-up as Growth Lab did;
   - carry over the lab's lessons: water wet from below is never recharge (the runaway loop), a soak cap per season,
     a pond that never shrinks;
   - **before-camp rule:** no new tent until the camp gate; the tribe's roots-and-fire jobs before it.
3. **Terrain:** a validator that guarantees one hollow that's smaller and deeper than today's dip, away from the
   founding flat. Today's flood line takes ~10 % of the box and a slice of flat land near the camp on presets 0
   and 2 (research 14 §3.5).
4. **Remove the charges economy and trash heaps in the sim** (`SimConfig.shaping*`, keepers-hands §3.3, §5.6), once
   the user says go.

## Left to do once the Editor is free (behind `.agent-editor.lock`, one at a time)
Order from keepers-hands §6: **the camp beats 0–6 first, in gray-box art**, then the village beats. Each one follows
CLAUDE.md's loop (recompile, console, screenshot, tests, `SlicePlaythroughSmoke`) and the tester cadence.

1. **Terrain tint by cover and moisture**: vertex colours from dry cracked dirt to green, with a spread animation
   when a mist greens a spot live.
2. **Vegetation in stages**, instanced, in the style kit: grass tufts, tall grass, 2 bush sizes, tree seedling /
   young / grown, reeds. The Keeper's tree gets its own look per stage and season (bare in winter, buds in spring,
   full in summer, colour in autumn) and shows one need at a time (droop for water, lean for light, weeds at its
   foot).
3. **Water:** the water table as a rising wet band in the existing soil cross-section, and a pond mesh that rises in
   the hollow, with reeds at the edge.
4. **The glass:** droplets and fog as the humidity gauge (keepers-hands §5.1), cleared by the cloth along with grime.
5. **The mister effect:** fine mist drifting down where the finger holds, with no hand or bottle in view.
6. **The tribe before the camp** (§2.1):
   - the start scene shows one tent and a fire;
   - before the first green: root-digging in the dry stream bed, sleeping and sitting by the fire;
   - **mist reactions**: stand in it with faces up, a child playing, the fire moved next to the first green, the
     glass voice's first line;
   - tents only after the camp gate (the era moment).
7. **Signs between the gates:** crickets with the first green, rabbit tracks before the rabbits, then rabbit and bird
   dolls driven by the counts, birds landing on the first bushes, and later frogs and ducks at the pond.
8. **Sound:** a barren bed (dry wind, sand hiss, cracking dirt, a loose stone) whose volume follows the bare share of
   the land, crossfading into crickets, then birds, then water and frogs. It never comes back (nothing dies back).
9. **UI: the kit button** with a tool rail (cloth, mister, seed, snips; the stick in the village) replacing Clean and
   Shape. This reworks the HUD signed off in #107, `ShapingTray` and one-handed controls, so it **needs the user's
   sign-off first** (keepers-hands §3.3).
10. **The first run:**
    - wipe the misted glass;
    - the barren box, its sounds, the tribe at its one tent;
    - the cloud pulses: the first mist, the green spreads, the people react;
    - name the box;
    - the first wish and evening as today, but **with no time skip**.
11. **Debug panel** (CLAUDE.md rule), in the same change as each feature:
    - give mist anywhere;
    - skip N seasons;
    - set humidity, the water table, the tree's stage and its needs;
    - spawn a shower or a bird's seed;
    - force the camp and village gates;
    - trigger each sign and each mist reaction;
    - toggle the barren sound.
12. **Tests in Unity:** an EditMode test that the view layer reads the sim cover; extend `SlicePlaythroughSmoke` to
    start barren and reach the camp gate with cheats. Then run the **tester** agent with a brief to play the first
    hour on a phone: does the barren start read, do the mists answer, is it ever dull, can you hear the land come
    alive.

## Don't
- Don't start any of the Unity work before the user's **go** on #228.
- Don't change the HUD (the kit button) without the user's sign-off.
- Don't close #228: the user closes it.
- Don't use the lab's numbers as final; they're tuned estimates for the sim port to start from.
