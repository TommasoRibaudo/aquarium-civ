# Design brief: Claude Design in the UI rework (2026-10-31)

How Claude Design is used in epic #103, and what every design pass must be given. Decided by the user 2026-10-31
(decisions.md).

"Claude Design" here means Claude's **Design** and **Design System** artifact types. `DesignSync` is not for making
designs, and standalone claude.ai/design closes on December 14, 2026, so nothing here depends on it.

## Where it's used

| Stage | Use it? | How |
|---|---|---|
| #163 hierarchy and wireframes | No | Stay grey, so style isn't judged before structure |
| #106 directions | **Yes, the first trial** | 2–3 directions on the same screens over real captures, compared on the phone |
| #107 proposal | Yes, if #106 proves it | Every piece and state in the chosen direction |
| Design system spec | Probably | The Design System type as the one source for colour, type, spacing and components |
| #108 build | Reference only | The code stays in UI Toolkit: tokens and components are translated into USS |

## Required inputs

No design pass starts without all of these:

1. [hierarchy.md](hierarchy.md), signed off, with its rules (one primary per moment)
2. [../ui-inventory.md](../ui-inventory.md), including the duplicates to merge
3. [captures/](captures/README.md), so designs sit on the actual game
4. The research findings from #104 (`docs/research/ui/README.md`)
5. The style kit palette: `Game.uss` `:root`, `View/Palette.cs`, [../spikes/style-kit.md](../spikes/style-kit.md)
6. **The limits sheet from #164 (`limits.md`)**

**Never give a design pass third-party screenshots.** Game UI Database prohibits any use of its content for AI asset generation or machine learning, so nothing from it enters this project. No research pictures are merged into the repo at all (decisions.md). Designs get the written findings and our own captures.

The limits sheet is the guard against the main risk: design tools can draw what Unity can't render. USS has no `gap` or
`line-height`, and Unity 6.3 has no drop-shadow filter, so soft shadows and frosted glass can look great in a mockup and
fail in the game. A direction that needs anything the sheet rules out doesn't go to the user.

## Checks

- Contrast is 4.5:1 for text. Plaster on today's terracotta is 2.86:1 and on moss 3.6:1: both fail (research §4).
- Compare on the phone, at phone size, over real captures.
- **#106 trial:** record whether it beat hand-built HTML mockups. If it didn't, #107 goes back to HTML.
- **#107's hero piece built in Unity stays the final check.** A mockup is never the proof.
