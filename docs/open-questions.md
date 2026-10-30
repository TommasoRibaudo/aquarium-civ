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
