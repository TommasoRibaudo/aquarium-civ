# 13 · Lighting and a real night (#175, #113)

Written 2026-10-31, no Unity, nothing measured on a phone. Tags: **[V]** read in Unity's own docs or this repo's code,
**[S]** secondary, **[I]** my inference. All "ms" figures are guesses to rank options, not measurements; per
[ui/limits.md](../design/ui/limits.md) even Editor numbers only rank things. The issue asks for a note under
`docs/design/spikes/`; it lives here beside the other research and `design/spikes/style-kit.md` is its starting point.

Direction already decided by the user (#113 comment, 2026-10-30): **soft blue dusk, people stay readable, lamps glow.**
Sign-off on the result is still theirs.

## 1. What the light does today [V, read from the code]

| Part | Today |
|---|---|
| Sun | One directional light, colour (1, .95, .85), intensity 1.3, pitch 42°, yaw -40°, soft shadows at strength 0.85 (`SliceSceneBuilder`) |
| Ambient | Trilight: sky (.74, .80, .92), equator (.68, .65, .60), ground (.42, .36, .30). No fog |
| Material light | `Stylized.shader`: wrapped diffuse into a soft two-band ramp, cool shadow tint, `SampleSH` ambient × `_Ambient` 0.75 × baked AO. Lamps: `_AC_LampLevel` (0 day, 1 night) ramps each lamp on at its own moment with flicker; `_LampBoost` 2.2, warm tint |
| Post | Neutral tonemap, contrast +10, saturation +5, split toning (shadows (.42,.5,.6), highlights warm, balance -15), vignette .26, bloom .5 at threshold 1.3, quarter res, HQ filtering off |
| URP asset | HDR on, render scale 0.8, MSAA off, 1 cascade at 1024, shadow distance 50, LUT 32, additional lights per pixel with a per-object limit of 4 (nothing uses them), no additional-light shadows |
| Clock | `LifeClock`: dawn 5-7 h = 20 s real, evening 17-20 h = 50 s, night 20-5 h = 45 s. A day is about 5 minutes. `PeopleView` owns the hour. Sessions start at 9 h |
| Night | `NightLamps.Level` is set only by the inspector driver or the debug button. Nothing links it to the hour. `MomentLight.Dusk` only moves the people's hour to 19.5 |
| Mess dimming | `CareView.UpdateSun` multiplies the sun intensity by `SunScale(mess)`: **at most -25 %**, and only the sun |

## 2. Problems, mostly at phone size

1. **Dusk looks like day** because nothing but the people's hour changes (#113). Sun, ambient and grade are constant. [V]
2. **A two-band ramp plus a big ambient floor is flat.** Ambient is bright and blue-white at day values, so the
   difference between lit and shadowed faces is small. At a few dozen pixels per person, value contrast between a person
   and the ground is what reads (practices §2), and the current light gives little of it. [I]
3. **Mess dimming would vanish at night.** It only scales the sun, by 25 %. If the night sun is 0.3, the dimming moves
   it by 0.075. Day dimming works, night dimming would be invisible. [V for the maths, I for the visual verdict]
4. **Warm lamps need a cool surround to read as light.** Today's neutral light gives lamps no contrast to glow against.
   Dusk reads mostly through warm-against-cool colour contrast, not darkness alone. [I, general colour practice]
5. **Bloom is the only glow.** Windows and lanterns exceed the 1.3 threshold only because of `_LampBoost`; the fire has
   its own halo. Fine in principle, but it is a full-screen pass that tilers dislike (see 07 §4.6 [V]), so do not lean on
   it more than today.
6. **Time moves fast.** Dusk is 50 s, night 45 s. Transitions are seen, so they must be smooth, and a time-lapse or a
   fast-forward must not strobe the light (practices §12, no flicker). [I]

## 3. What the engine gives us on a mid-range phone

- **Real extra lights are limited exactly where we are weak.** In URP's Forward path each object is lit by the main light
  plus **up to eight** additional lights, and the camera sees at most **32** on mobile (16 on OpenGL ES 3.0 and older) ([Unity 6.3 light limits](https://docs.unity.com/en-us/engine/current/manual/lighting-overview/lighting/light-limits-in-urp) [V]). Our asset sets a per-object limit of 4. The kit bakes the ground into **one static mesh** (`style-kit.md`), so a lamp pool would have to be one of those 4 lights for the *whole* ground, which means lamps would pop on and off as the nearest-four change. Forward+ lifts the per-object limit but is a heavier path whose mobile cost I did not verify; treat it as a desktop comparison only. [I]
- **Point lights cast no cheap shadows.** Unity says shadow-casting point lights use a large share of a mobile frame and recommends few or none, and suggests cookies for mostly static areas ([URP performance](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal%4016.0/manual/configure-for-better-performance.html) [V]). So no shadowed lamps.
- **Soft shadows can be costly on tilers**; the docs suggest Low or off ([same page] [V]). Today's asset has the soft-shadow switch off; the sun's "soft" setting is moot until it is on. Check that, don't change it. [V]
- **Post:** the project already ships the cheap set from 07 §4.6 (grade, vignette, tonemap, limited bloom) [V]. Changing values at runtime on the existing profile adds no pass. [I]
- **A light model made to be cheap:** Unity's Shader Graph custom-lighting sample says its "Lit Basic" model is quick enough for low-end mobile, and its "Colorize" sample keeps the main light grey and brings colour only from point lights, which is the same idea as a lamp-lit night ([sample page](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Shader-Graph-Sample-Custom-Lighting-Lighting-Models.html) [V]). Our own `Stylized.shader` is hand HLSL, so we copy the idea, not the sample.
- **Reference approach from shipped games:** Monument Valley used a simple custom lighting model, baked occlusion and "faked volumetric glows" with overdraw, no realtime GI ([MCV](https://www.mcvuk.com/development/unity-focus-monument-valley) [V per 07 §3]). Glows by additive quads and emissive masks, not real lights, is the common cheap route ([GlowingQuad plugin, Unreal, idea only](https://github.com/gamecoder/GlowingQuad) [S]).
- I did not find a published breakdown of how any named cozy builder grades its dusk, so nothing below claims to match a particular game.

## 4. Options, with cost

| # | Option | What it does | Phone cost [est] | Risk |
|---|---|---|---|---|
| L0 | **Today** | Control | baseline | Dusk looks like day |
| L1 | **Clock-graded light** | Key table by hour drives sun colour, intensity, pitch, trilight ambient, and the post grade (one profile, values set in code) | Near zero: a few floats per frame on the CPU, no new pass | Looks flat if the table is timid |
| L2 | **L1 + lamp light in the shader** | The lamp positions (up to 8) go into a global shader array; `Stylized` adds a warm, distance-faded term per vertex, scaled by `_AC_LampLevel`. Lights ground, houses and people near lamps without URP additional lights | Vertex ALU only: 60k vertices × 8 lamps is small for any phone GPU | Faceted ground shows a gradient per triangle (fits low poly); needs a shader edit |
| L3 | **Painted pools and halos** | One batched mesh of soft additive discs on the ground under each lamp and a halo around each lantern, alpha follows lamp level and flicker | One draw, small overdraw | Pools do not light people; pair with L2 |
| L4 | **Moonlit** | At night the sun becomes a cool "moon" from another angle with fewer, softer shadows | Same as L1 (it is the same light) | Needs to stay readable: moon shadows over dark grime |
| L5 | **Real URP point lights** | One light per lamp | Per-pixel loop over lights on every lit fragment; the per-object limit problem above | Pop-in on the one-mesh ground; I would not ship it |
| L6 | **Fog tint** | Low-density tinted fog at dusk | A little ALU per fragment if the custom shader samples fog; Stylized doesn't today | Hazes the people. Skip |
| L7 | **Heavier post for night** (more bloom, a second volume) | Glow and mood | Bloom cost rises with intensity and iterations on tilers [S, 07] | Skip; keep bloom as is, ramp the intensity only |

## 5. Dusk and night: what each needs

A dusk "that reads as dusk" needs three things at once [I]: (1) the **hue** moves: warm gold, then rose, then soft blue;
(2) **lamps are warmer and brighter than their surround**, so the surround must go cool; (3) **luminance drops slowly**,
not to black. The user's "soft blue" is mostly (2) and (3): a blue ambient with a floor high enough to see the people.

Night's rule for readability, stated so it can be tested: **the whole-frame mean luminance at 22 h should be at least
about 0.30 of the 12 h value, and a person should differ from the ground behind them by at least a 1.5:1 luminance
ratio** (my numbers, to be tuned by looking). The Editor agent measures both on screenshots at the default zoom, and fully
zoomed out, and with dark grime.

## 6. The glass, the frost and the UI at night

- **Glass tint can fog the night.** The clean pane is a white-ish tint at alpha 0.05 over the world. By day it is
  invisible; at night the same white lifts the dark interior into a milky haze. Multiply the clean tint (and the
  condensation) by a night factor so the tint follows the ambient. [I, from the `Glass` material values in the scene builder]
- **Grime must stay readable at night.** Grime is the box's state (pillar 5). Dark grime over a dark world loses contrast,
  so by night the grime should catch light: a faint lamp or moon sheen on it, or a lighter grime tone scaled by the night
  level. Test with maximum grime at 22 h. [I]
- **Keep the glass edge and rim alive at night.** A thin pale edge and one soft streak read as glass against a dark
  interior; they cost nothing. [I]
- **Frost (the frosted sheet) was built on the world behind it.** It copies the frame to a small texture, blurs it, and
  puts a tint on top ([ui/limits.md](../design/ui/limits.md) [V]). So at night the blurred backdrop is darker, and a
  light tint with partial alpha comes out darker too. Light tint at 60 % alpha over backdrop luminance 0.6 gives about
  0.84; over 0.2 gives about 0.68 (simple blend arithmetic). Dark text still passes there, but the gap shrinks as the
  night deepens. Design the sheet tint for the **night worst case** and check the text there. [I]
- **Contrast targets.** Normal text 4.5:1, large text 3:1, icons and controls 3:1 against what is behind them
  ([W3C WCAG 2.2, SC 1.4.3 and 1.4.11](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) [V for the numbers; I read the figures through a search summary and the 2.1 page]).
- **Recommendation for the UI: do not re-theme the UI at night in this slice.** Keep the cards and chips looking the same,
  only raise tint opacity to the night-safe value found by measurement, and make bare text on the tank (toasts, labels)
  carry a scrim or the existing hard text shadow. A dark-mode UI is a later, separate decision for the user.
- **Mess dimming at night** needs a different lever (problem 3): apply the same `SunScale` value to the ambient floor
  too, or as a small negative `postExposure`, so a messy box is visibly gloomier at night as well as by day. Cap the
  total loss so the readability rule in §5 still holds.

## 7. Recommended safe version (the Editor agent builds this without the user's phone)

**"Soft Blue Dusk" = L1 + L2 + L3**, with the sun staying the one shadow caster (no L5, L6, L7). It costs a few floats a
frame, one batched mesh and a vertex loop; it keeps the single-sun design from 07 §3 and does not touch the URP asset.

### What to build

1. **`DayLight` driver** in `View` (next to `NightLamps`). Reads the view hour from `PeopleView` (`Now.hour`). Each frame
   it evaluates a key table (below), smooths it with `1 - exp(-k·dt)` (k about 3, so a time-lapse never strobes), and
   writes: `sun.color`, `sun` rotation, `RenderSettings.ambientSkyColor/Equator/Ground`, the shader global `_AC_LampLevel`
   (through `NightLamps.SetLevel`), and a few values on the cached post overrides (`postExposure`, `saturation`, split-
   toning colours, vignette, bloom intensity). No allocations: cache the override references, use `Color.Lerp`.
2. **Compose with mess dimming, don't fight it.** `CareView.UpdateSun` keeps owning `sun.intensity`, but its `sunBase`
   becomes "the driver's current intensity" (expose `DayLight.SunIntensity`). Also apply `SunScale` to the ambient (§6).
   In play mode the driver is the source; in edit mode the old inspector value still works.
3. **Verify the ambient update.** Changing the trilight colours at runtime should update `SampleSH`, but I did not verify
   it on 6000.3. If a screenshot shows the ambient not following, add global shader colours (`_AC_AmbSky`, ...) or a single
   `_AC_AmbientTint` that `Stylized` multiplies in, which is deterministic. [I]
4. **Lamps (L2).** Collect lamp world positions when the mesh is built (the builder already flags lamps in UV1). Put up to
   8 into `Shader.SetGlobalVectorArray("_AC_LampPos", ...)` once per rebuild; the vertex stage adds
   `warm × saturate(1 - d/R)² × _AC_LampLevel × lampOn(phase)` to the vertex light, R about 1.6 to 2.2 box units. Use
   the same `LampOn` flicker as the glows. More than 8 lamps: pick the 8 nearest the camera focus.
5. **Pools (L3).** One mesh, soft radial alpha, additive or soft-light, y just above ground, `_AC_LampLevel` × flicker.
6. **Glass.** Night factor on the clean tint and condensation, a faint sheen on grime, keep the rim.
7. **Moments, time-lapse, stills.** `MomentLight.Dusk` already sets 19.5 h; the driver follows it. Time-lapse: clamp the
   light's change rate (the smoothing in 1) and hold the lamp flicker while the clock runs fast. `EraStills.Take` sets a
   fixed still hour per era (suggest 17.5 h, the golden hour, for share images), then restores it.
8. **Debug panel** (existing rule): an hour scrubber, jump buttons 6 / 12 / 17.5 / 19.5 / 22 h, a **night floor slider**
   (the ambient multiplier at night, 0.45 to 0.9), a pause-the-clock toggle, and the look switch in §8.
9. **Tests.** Edit-mode: the key table is monotonic where it should be, smoothing converges, mess dimming composes (mean
   luminance with mess < without, at 12 h and at 22 h), no allocation per frame (a test that calls the driver 1000 times
   and checks `GC.GetTotalMemory` stays flat). Screenshots at 6, 12, 18, 19.5, 22 h at default zoom, fully zoomed out,
   and with max grime, plus the mean-luminance and person-versus-ground ratios from §5.

### Starting values (tune from screenshots)

Hours are view hours. Interpolate linearly between keys, smoothly across midnight. "Lamp" is `_AC_LampLevel`. Sun pitch is
the angle above the horizon seen from the box; keep it above about 5° so shadows stay sane.

| Hour | Sun colour | Sun intensity | Pitch / yaw | Ambient sky | Ambient equator | Ambient ground | Lamp | Post (exposure / sat / vignette) |
|---|---|---|---|---|---|---|---|---|
| 5 dawn | (.95, .66, .55) | 0.55 | 10° / -40° | (.55, .60, .82) | (.58, .52, .55) | (.30, .27, .30) | 0.6 | -0.15 / +0 / .30 |
| 7 | (1, .85, .70) | 1.0 | 25° / -40° | (.70, .76, .90) | (.66, .62, .58) | (.40, .35, .30) | 0 | 0 / +3 / .27 |
| 9 to 15 (day) | (1, .95, .85) | 1.3 | 42° / -40° | (.74, .80, .92) | (.68, .65, .60) | (.42, .36, .30) | 0 | 0 / +5 / .26 |
| 17 | (1, .78, .52) | 1.1 | 24° / -40° | (.68, .72, .90) | (.66, .58, .55) | (.38, .32, .30) | 0.15 | -0.05 / +6 / .28 |
| 18.5 | (1, .62, .45) | 0.75 | 12° / -40° | (.55, .60, .88) | (.55, .50, .62) | (.30, .27, .36) | 0.55 | -0.15 / +6 / .30 |
| 19.5 dusk | (.82, .68, .88) | 0.45 | 7° / -40° | (.48, .56, .90) | (.44, .47, .72) | (.25, .26, .40) | 0.85 | -0.25 / +4 / .33 |
| 22 night | (.55, .66, 1.0) | 0.30 | 35° / +30° | (.36, .44, .76) | (.32, .36, .58) | (.20, .22, .34) | 1 | -0.35 / +2 / .36 |

Other values: shader `_Ambient` stays 0.75 by day, raise to about 0.9 at night so the blue ambient is the night floor
(this is the "night floor" slider); lamp boost 2.2 stays, bloom intensity 0.5 by day to about 0.7 at night with the
threshold unchanged at 1.3; sun shadow strength 0.85 by day to 0.55 at night so the moon shadows are soft; lamp ramp:
0 until 17 h, 1 at 20 h, down from 5 h to 7 h (per #113); lamp pool radius 1.6 to 2.2 box units, pool alpha 0.35 at full
lamp level; vertex lamp term colour (1, .78, .45), strength about 0.9; glass night factor: tint × (0.35 + 0.65 × day
brightness). Dusk (19.5 h) must show as: a cool blue wash, the warm windows and pools clearly warmer, people still
readable; if it still looks like day, lower the 19.5 h ambient equator and ground first, then the sun intensity.

## 8. What to leave as a debug toggle for the user's phone comparison

The safe version above is the default. Keep these behind `LightLook` in the debug panel until the user compares them on
the final build, then delete the rest (the issue's step 5):

| Look | What differs | Why the user must see it on a phone |
|---|---|---|
| **Today** (control) | Old fixed light | Baseline for the comparison |
| **Soft Blue Dusk** (default) | §7 as written | The user's pick; check the darkness |
| **Soft Blue + Moon** | L4: at night the "moon" has a stronger shadow, lamp pools lower | Is a visible moon shadow charming or just dark |
| **Lamplight** | L2/L3 stronger (bigger pools, lamp term 1.4×), ambient floor lower | The brightest lamps on a darker night: the mood end of the range |

Plus the **night floor slider** (0.45 to 0.9), because "how dark may the night get" is the user's open call (#113) and a
slider answers it faster than a fourth variant. Screens are the thing a phone changes: brightness, auto-brightness, OLED
black levels. I cannot judge those from a desktop capture, so the user's phone is the final test of night darkness. Frame
time is the other thing only a phone answers: measure Today vs Soft Blue Dusk vs Lamplight on the phone profile; I expect
the difference to be within noise [I, not measured].

## As built (2026-11-01, branch vs/light-camera; measured in the Editor on `Mobile_RPAsset`, not on a phone)

The safe version in section 7 is in, Soft Blue Dusk the default, and `LightLook` on the debug panel. What differs from the note:

- **Sun and moon:** the keys swap them at 20.7 to 20.9 h and 03:00 to 03:12, at intensity 0.06 with no shadow, so nothing sweeps.
- **Ambient:** the trilight does follow at runtime (open risk 1: SH L0 logged at each hour). No `_AC_AmbientTint`.
- **`_Ambient`:** a global added to it (`_AC_AmbientBoost`), because the solid material is an asset.
- **Lamp reach:** 1.25 box units, not 1.6 to 2.2: more lit the whole box. Pool radii 0.62 (lantern) and 0.36 (a building's door), on a 5x5 grid laid on the land, in the Glows mesh: no extra draw.
- **The room:** the camera's clear colour falls with the sky, or the floor stays lit under a dark night.
- **HUD at night:** the name and icons take the backing they have off home when Daylight < 0.75. Dark ink on the night wall was unreadable.
- **Readability rule (section 5):** the whole frame at 22 h is 0.48 of 12 h (Town, 1080x1920), or 0.33 at night floor 0.45. Rule of thumb 0.30 holds. The 1.5:1 person-to-ground ratio was not measured, only looked at.
- **Risk 2 (vertex lamps on faceted ground):** it reads as low-poly facets at zoom 0.2. Looks fine in the shots; the user's call.

## Open risks to check first

1. Does changing `RenderSettings` trilight colours update the SH ambient at runtime in 6000.3? (§7.3)
2. Do 8 vertex lamps on the faceted ground look like light, or like a flat triangle flicker? If bad, drop to pools only.
3. Is the post volume's override access allocation-free in the player? (test in §7.9)
4. Night with max grime: does the glass still show the box's state?

## Sources

- Unity 6.3, light limits in URP: https://docs.unity.com/en-us/engine/current/manual/lighting-overview/lighting/light-limits-in-urp [V]
- Unity URP, configure for better performance: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal%4016.0/manual/configure-for-better-performance.html [V, version 16 page; the 6000.3 page is the same advice per search]
- Unity Shader Graph custom-lighting sample (Basic, Colorize, Simple, Toon, URP): https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Shader-Graph-Sample-Custom-Lighting-Lighting-Models.html [V]
- Monument Valley lighting (MCV): https://www.mcvuk.com/development/unity-focus-monument-valley [V via 07]
- GlowingQuad (fake glow idea, Unreal): https://github.com/gamecoder/GlowingQuad [S]
- W3C WCAG contrast: https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html [V for the 4.5:1 and 3:1 values, via summary]
- Repo: `design/spikes/style-kit.md`, `research/07-art-direction.md` §3 and §4.2/4.6, `design/ui/limits.md`, `game-design-practices.md` §2, §12;
  code read: `CareView.UpdateSun`, `NightLamps`, `Stylized.shader`, `SliceSceneBuilder` (light, post), `LifeClock`, `Mobile_RPAsset.asset`
- Not found: any published grading breakdown of a named cozy builder's dusk; Forward+ mobile limits on 6000.3 (the page
  returned 404); anything measured on a phone.
