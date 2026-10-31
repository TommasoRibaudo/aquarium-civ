# Open questions for the user (kept current)

Everything waiting on a decision by the user, in one place. Answered items move to [decisions.md](decisions.md) and are
deleted here. Recommended options come first and are marked ★. On GitHub: issues labelled `needs:user`.

_Last updated 2026-10-31._

## Still open (2026-10-30)

### Camera lifts after the glass fix
- Show me, relic nudges and answer flights still rise to pitch 50 for a haze that turned out to be an Editor bug.
  ★ Keep them, they frame well. Alternative: go back to the player's pitch.

### Dirty glass look
- Olive smears and drips now. ★ Keep (you said it's fine for now; variety is #121). Alternative: heavier or mistier.

## From tester run 4, first play through clear glass (2026-10-31)
None of these block the build. ★ = file it and do it after the PR to dev.
- **Trash and weeds:** trash reads as boulders and buries the tents; weeds read as a pretty bush. ★ Rework both.
- **Citizens at default zoom:** about 30x41 px (2.5 mm), coloured pegs. ★ Scale up a little and test.
- **Relics:** Sky Crumb looks like a tent; Keeper Doll and Lens don't stand out; the Sandal had no prop. ★ Shared cue, fix the Sandal.
- **Camera after a fly-in:** never returns, so normal play opens cropped on the camp. ★ Glide back after a few seconds.
- **Wish Give then Cancel:** the card and dot vanish until the next return. ★ Bring the card back.
- **Skip all and History:** the dot stays and History reopens the stack, so a skipper never reaches the time-lapse. ★ Skip all clears the dot.
- **Two years on the time-lapse screen:** HUD 70, panel 71. ★ Hide the HUD year there.
- **Small UI tidy:** targets under 44 pt, pin numbers under the playhead, tray toast behind the sheet, faint grey text, tray margin, tablet hint overlap, stacked labels at the end of the first run, lone word in a bubble, snapshot toast over the town, "Next 1/7". ★ One ticket.
- **Tall sheets:** tray 60-80%, time-lapse caption about 45%. ★ Leave for the UI rework (#108).
