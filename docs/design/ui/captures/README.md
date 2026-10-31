# UI captures (#163, 2026-10-31)

One image per state in [../../ui-inventory.md](../../ui-inventory.md), taken from the real Slice scene by
`UiCaptureTour` (`AquariumCiv/Assets/Game/View/PlayTests`). Tall phone: 1179x2556. `small/`: 750x1334. Every image was
looked at on a contact sheet, not at full size. The tablet size wasn't captured.

To run it, the command needs both flags, or it reports 0/0 and does nothing:
`unity command test run --mode PlayMode --filter UiCapture --filter_type category --include_explicit true --async_tests true`

## What the captures show

Bugs and problems visible in the images, for #163, #107 and the retrofit tickets:

| Seen in | Problem |
|---|---|
| `3.2-plate-named`, `3.7-firstrain-button`, `3.7-naming-keep` | Two speech bubbles overlap and one can't be read |
| `3.8-feedback-queued`, `3.8-feedback-sent` | The feedback toast sits on top of the HUD toast: two toast systems collide |
| `3.4-person-best`, `small/3.4-person-notable` | The subtitle runs under the Pin button ("the grump", "the joker") |
| `small/3.6-watch-with-toast` | The pin chip clips a long label ("Piers (remembered)") |
| `small/3.4-person-notable` | Tie labels cover the person's own name tag |
| `3.3-stack-era`, `3.3-stack-legend`, `3.3-stack-wish` | Two primary buttons side by side (Watch and Next, Remember and Next, Give and Next) |
| `3.5-caption-no-person`, `3.5-timelapse-at-pin` | "Village" era card over "Year 1 · Camp". May be timing, needs checking |
| `3.5-*` | Two years on screen: HUD 45, panel 1 |
| `3.3-recall-asked`, `3.8-feedback-empty` | Sheets sit high on the screen, out of the thumb zone |
| `3.4-tray-*` | The tray covers most of the box, and the dim hides the rest |
| `3.1-photo-camera-mode` against `3.3-peek-shown` | "Your box" and the other tall-phone peek shots have no portrait |
| `3.7-firstcard-done`, `3.7-firstcard-showme` | The camera stays zoomed in after the first card |
| `3.6-shelf-list`, `3.6-shelf-open-button` | Skip buttons and the Shelf button appear with no link to what's around them |

## Not captured

Take back chip in the world, snapshot toast, person card while pinned. Each needs a small fix to the tour. None of them
blocks the wireframes.

## Index

| Image | Result | Note |
|---|---|---|
| `3.1-title-default-name` | check | Named box, not the default. For "Your box" see `3.5-timelapse-empty` |
| `3.1-hud-default` | ok |  |
| `3.1-clean-on` | ok |  |
| `3.1-clean-auto-off` | ok |  |
| `3.1-photo-camera-mode` | ok |  |
| `3.1-pin-pinned` | ok |  |
| `3.1-hud-pin-dot` | ok |  |
| `3.1-history-dot` | ok |  |
| `3.3-peek-dismissed` | ok |  |
| `3.1-debug-dock-closed` | ok |  |
| `3.1-debug-dock-open-dbg` | ok |  |
| `3.1-debug-dock-open-sim` | ok |  |
| `3.8-settings-sheet` | ok |  |
| `extra-hud-default-night` | ok |  |
| `extra-hud-default-dirty` | ok |  |
| `3.4-cleanmode-on` | ok |  |
| `3.4-cleanmode-spotless` | ok |  |
| `3.4-tray-tools` | ok |  |
| `3.4-tray-locked` | ok |  |
| `3.4-tray-empty` | ok |  |
| `3.4-shapingmode-armed` | ok |  |
| `3.4-shapingmode-refused` | check | The refusal is the toast, and it's visible |
| `3.4-shapingmode-wish-armed` | ok |  |
| `3.4-shapingmode-placed` | ok |  |
| `3.2-takeback-visible` | not captured | **Not captured:** shows an ambient bubble and no chip, which may be behind it. The tray's list is in `3.4-tray-takeback-list` |
| `3.4-tray-capped` | ok |  |
| `3.4-tray-takeback-list` | ok |  |
| `3.2-bubble-shown` | check | No bubble in this frame. For a bubble see `3.2-takeback-visible` or `3.7-firstrain-armed` |
| `3.3-peek-shown` | ok |  |
| `3.3-stack-story` | check | Shows the era card. For a story see `small/3.3-stack-story` |
| `3.1-hud-tools-hidden` | ok |  |
| `3.3-showme-running` | ok |  |
| `3.3-stack-last-page` | ok |  |
| `3.3-also-lines` | ok |  |
| `3.3-stack-era` | ok |  |
| `3.3-stack-relic` | ok |  |
| `3.3-stack-legend` | ok |  |
| `3.3-stack-wish` | ok |  |
| `3.3-stack-payoff` | ok |  |
| `3.3-stack-endline` | ok |  |
| `3.3-recall-asked` | ok |  |
| `3.3-recall-answered` | ok |  |
| `3.3-recall-not-now` | ok |  |
| `3.4-person-notable` | ok |  |
| `3.2-marker-name-tag` | ok |  |
| `3.4-person-recent` | ok |  |
| `3.4-person-best` | ok |  |
| `3.4-person-pinned` | not captured | **Not captured:** the card has closed |
| `3.2-pin-tag-gold` | check | No tag in this frame. See `3.1-pin-pinned` |
| `3.2-tie-lines` | ok |  |
| `3.4-person-legend` | ok |  |
| `3.5-timelapse-paused` | ok |  |
| `3.5-eracard-shown` | ok |  |
| `3.5-timelapse-playing` | ok |  |
| `3.5-timelapse-at-pin` | ok |  |
| `3.5-caption-with-person` | ok |  |
| `3.5-caption-no-person` | ok |  |
| `3.5-snapshot-toast` | not captured | **Not captured:** the toast has gone. The file shows the end of a playthrough |
| `3.5-timelapse-empty` | ok |  |
| `3.6-watch-hidden` | ok |  |
| `3.6-watch-ready` | ok |  |
| `3.6-watch-with-toast` | ok |  |
| `3.6-watch-wipe-first` | ok |  |
| `3.6-moment-hint` | ok |  |
| `3.1-hud-hidden` | ok |  |
| `3.6-moment-bubble` | ok |  |
| `3.6-moment-caption` | ok |  |
| `3.6-notification-ask` | ok |  |
| `3.6-finish-hidden` | ok |  |
| `3.6-finish-ready` | ok |  |
| `3.6-last-hint` | ok |  |
| `3.6-last-bubble` | ok |  |
| `3.6-last-roll-call` | ok |  |
| `3.6-last-caption` | ok |  |
| `3.6-shelf-open-button` | ok |  |
| `3.6-shelf-list` | ok |  |
| `3.6-shelf-visiting` | ok |  |
| `3.7-hints-wipe` | ok |  |
| `3.7-skip-row` | ok |  |
| `extra-history-plain-sheet` | ok |  |
| `3.7-hints-patch` | ok |  |
| `3.2-plate-blank` | ok |  |
| `3.7-naming-suggested` | ok |  |
| `3.7-naming-another` | ok |  |
| `3.7-naming-invalid` | ok |  |
| `3.7-naming-later` | ok |  |
| `3.7-naming-keep` | ok |  |
| `3.2-plate-named` | ok |  |
| `3.7-firstrain-button` | ok |  |
| `3.7-firstrain-armed` | ok |  |
| `3.7-firstyear-running` | ok |  |
| `3.7-firstcard-shown` | ok |  |
| `3.7-firstcard-showme` | ok |  |
| `3.7-firstcard-done` | ok |  |
| `3.8-feedback-empty` | ok |  |
| `3.8-feedback-filled` | ok |  |
| `3.8-feedback-queued` | ok |  |
| `3.8-feedback-sent` | ok |  |
| `small/3.1-hud-default` | ok |  |
| `small/3.4-person-notable` | ok |  |
| `small/3.1-hud-pin-dot` | ok |  |
| `small/3.3-peek-shown` | ok |  |
| `small/3.3-stack-story` | ok |  |
| `small/3.4-tray-tools` | ok |  |
| `small/3.5-timelapse-paused` | ok |  |
| `small/3.5-timelapse-at-pin` | ok |  |
| `small/3.6-watch-with-toast` | ok |  |
