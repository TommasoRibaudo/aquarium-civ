# UI direction: Keeper's Glass (#106, 2026-10-31)

**Chosen by the user.** [The canvas](https://claude.ai/artifact/NKQ4MJu5YKCqwFfT2DDGv7) shows it with the two directions it came from. These files are copies of its
artboards; their images live on the canvas.

Keeper's Glass takes A's look and feel (Lora and PT Serif, warm ink, brass accents) and B's sleek shapes and frosted
surface.

| Token | Value | Use |
|---|---|---|
| Ink | `#3D2A1E` | Text on frost and paper |
| Ink soft | `#4A3324` | Button text |
| Frost | `#F3E9D6` at about 72 % over a blur | One surface per screen |
| Flat tint | `#F3E9D6` at about 92 % | Small pieces: tools, chips, tags, pin |
| Brass | `#B88A3E`, `#D9B873` | Hairline edges, portrait rings |
| Primary | `#9E5033`, text `#EFE3CC` (4.53:1) | One per moment |
| Portrait | `#526E3A` | Until real portraits exist |

These are starting values for #107. Contrast of text on frost depends on what's behind it, so it still needs checking
over the real game.

## Fonts

Lora and PT Serif, both SIL OFL 1.1. Lora's file is variable, so real bold needs static cuts. Add both to
`docs/assets-log.md` when they're imported.

## Fallback

If the frost fails on a phone, the same layout uses a flat tinted sheet. Nothing else changes.

## Claude Design trial

It worked for this stage: three directions on the same screens, and a mix made and picked in one more round. #107
continues in it. The boards can still draw what Unity can't: the frost here is CSS, while the game needs a URP pass
(limits.md).
