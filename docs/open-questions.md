# Open questions for the user (kept current)

Everything waiting on a decision by the user, in one place. Answered items move to [decisions.md](decisions.md) and are
deleted here. Recommended options come first and are marked ★. On GitHub: issues labelled `needs:user`.

_Last updated 2026-10-30._


## Overnight run 2026-10-29/30 (agents picked the ★ option and went on; change any of them)

### #82 Shaping charges and caps (sim, `SimCore.Shaping.cs`, `SimConfig.shaping*`)
- ★ Charges start at 2, cap 3 + 1 per era; refill +0.5/year while mood ≥ 0.6, plus by cleaning (full glass 0.5, all
  overgrowth 0.4, 0.08 per trash heap). First guesses for E2 to tune.
- ★ Caps per box: 1 rain until answered, 4 groves, 3 channels (doc says "≤3 [est]"), 2 herds with at most 1 fish shoal
  (one pond).
- ★ Take back is allowed until the people answer (not only until they "use" it).
- Era names only for now (orchard, mill race, goats/wool flock): their sim effect is the old answer logic.

### #86 Era change, festival, peak (sim, `SimCore.EraMoment.cs`)
- ★ The festival year lifts the mood target by +5 % (changes the economy a little after each era). Alternative: party
  only, no lift.
- Peak rule: town ≥ 40 years old + all 7 town seats held once + box full 3 years. On 12 seeds only 5 peak by year 120
  (at years 71–110); the design expected ~100–120. ★ Keep 40 for now; tune with E2 (lower the town-age or "full" bar if
  too many boxes never peak).
- ★ Player-facing years start at 1 ("years 1–118" on the plate).
- The cleared-hearth line says "old fire ring", not the design's "old camp ring" (a test forbids "camp" in town text).

### #74 Relics and the people's quotes (sim, `SimCore.Relics.cs`)
- ★ Sky Crumb fallback (the design has none): the first shaping after the camp.
- ★ The Left Sandal can also be lost in the town if no "lost" story came earlier, so it can't be missed.
- ★ In the camp, line 28 says "Ask the sky" instead of "the window".
- ★ A wipe counts once per game month for the Keeper Doll's prop, so the cloth will often win. Alternative: weight
  shapings higher.
