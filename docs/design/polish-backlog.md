# Polish backlog (from the P3–P6 builds), ranked

Merged from the "Polish for the user to decide" lists in [results/P3.md](results/P3.md)–[P6.md](results/P6.md) and the
Test Hub notes (2026-10-29). **Nothing here is implemented**, and nothing changes during the 0.3.x test (it would change
what is being compared). You pick; the gate outcome decides which ones matter at all.

**Ranking:** player impact first (does it hurt the thing the gate measures, or the first minutes of the slice?), then
cost. Impact: **H** = a player notices it in the first session, **M** = noticeable when looking, **L** = cosmetic.
Cost: **S** < half a day, **M** about a day, **L** several days. "Depends on" = the gate outcome under which the item is
still worth doing ("any" = whatever the gates decide).

## Do first (high impact, whatever the gates decide)
| # | Item | From | Impact | Cost | Depends on | Why it ranks here |
|---|---|---|---|---|---|---|
| 1 | Panels cover the tank: the person card and chronicle cards cover the bottom half; P5's tray covers two thirds | P3, P5 | H | M | any | Breaks "panels never cover what they're talking about" (practices §6) in every view that talks about the world. One layout rule (shrink the world viewport like P4-A, or a slimmer card) fixes all three. |
| 2 | Taps on crowded named people open the neighbour's card (about 1 in 4 near misses) | P3 | H | M | any (notables stay) | People are the attachment hook; a wrong card reads as "the game didn't listen" (§4). Options: a "two people here" chooser, or prefer the person the camera is centred on. |
| 3 | Almost everyone is named at the start (9 of 10 visible carry a dot); the ledger counts 10 people while 12 named live | P3 | H | S | any | Named-vs-crowd is the design (decisions: crowd + 5–12 notables); if everyone is named, nobody stands out. Start with fewer notables or a bigger crowd, and count children. |
| 4 | Era moment is small at phone size (every building pops at once; Camp → Hamlet changes only the huts) | P6 | H | M | any (eras stay) | The live era is the deep-session payoff; right now the banner and confetti carry it. Stagger the rebuild, and change more than huts per era. |
| 5 | Feedback / Done are drawn (and maybe tappable) under the person card | P3 | M | S | any | Accidental "Done" ends the session (§4, §6). Cheap to block. |
| 6 | A tap that reaches a person through the screen-space pick doesn't re-centre the camera | P3 | M | S | any | Inconsistent with a direct hit; small fix. |

## If Gate 2 keeps chronicle cards (P3 A)
| # | Item | From | Impact | Cost | Why |
|---|---|---|---|---|---|
| 7 | Card `dwell_s` keeps running while a person card or fly-to covers it | P3 | M (data) | S | Metric only, but the slice should log clean dwell. |
| 8 | The person card's third story sits half-cut at the bottom of its scroll | P3 | L | S | Clipped text (§6). |

## If Gate 2 picks the time-lapse (P4 A)
| # | Item | From | Impact | Cost | Why |
|---|---|---|---|---|---|
| 9 | Snapshots include the lab strip and HUD | P4 | M | S | A snapshot is for sharing; hide UI for one frame. Applies to the book too. |
| 10 | The backdrop under the shrunken world lacks the vignette (faint seam) | P4 | L | S | Cosmetic. |
| 11 | Year label can be off by one with a late-year story on free drags | P4 | L | S | Fixed for pins already. |

## If Gate 2 picks the illustrated book (P4 B)
| # | Item | From | Impact | Cost | Why |
|---|---|---|---|---|---|
| 12 | Family trees list a couple's children under one parent (reads like half-siblings) | P4 | M | M | Lineage is the book's selling point. |
| 13 | Chapter titles repeat ("The Builders" twice) | P4 | L | S | Name chapters from their top story. |
| 9 | Snapshots include the HUD (as above) | P4 | M | S | |

## If Gate 3 picks omens (P5 A)
| # | Item | From | Impact | Cost | Why |
|---|---|---|---|---|---|
| 14 | The shrine is small at default zoom; a Church can later stand on its tile | P5 | M | S | The gold mark finds it, but an omen should read on its own (§2). Reserve the tile like groves do. |

## If Gate 3 picks shaping the land (P5 B)
No open polish items specific to B (channel visibility was fixed in run 1). Item 1 (tray covers the tank) still applies.

## If Gate 3 picks the box grows (P6 A)
| # | Item | From | Impact | Cost | Why |
|---|---|---|---|---|---|
| 15 | The annex is a second full glass box (double wall at the seam, clean glass beside grimy glass, people walk through walls) | P6 | H | L | Reads as two boxes glued together; the slice needs one continuous glass shell. Art and grime-mask work. |
| 16 | A resize (iPad split view, rotation) drops the wide framing | P6 | L | S | |

## If Gate 3 picks a new box on the shelf (P6 B)
| # | Item | From | Impact | Cost | Why |
|---|---|---|---|---|---|
| 17 | The new box's grime shows on the museum's glass | P6 | M | S | Each box needs its own grime mask. |
| 18 | People are specks in the shelf view; the relic flag is hard to see | P6 | M | M | Frame closer, or a highlight on the relic. |
| 19 | A variant switch keeps the camera's yaw | P6 | L | S | Lab-only. |

## Test Hub (lab tooling only, not the game)
| # | Item | Impact | Cost |
|---|---|---|---|
| 20 | The pair question has empty space; ticks say "played / surveyed" per session only; "Start diary" asks a second tap | L | S |

## Data and tooling items (not player-facing, see vs/prep spikes)
- The look-time sampler reads the screen centre only: look-time on nudges needs a tap (P5), and reads "empty" between
  boxes (P6). Covered by the look-sampler spike.
- `close` is logged under the new session when a return closes a history view (P4).
