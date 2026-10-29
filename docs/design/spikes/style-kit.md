# Spike: the style kit, everything built in code (2026-10-29, branch vs/art-spike)

**Question.** Can one palette, one stylised material and agent-built low-poly geometry give a simple, good-looking,
cheap look, without KayKit? (User, 2026-10-29: KayKit's houses and people are too fantasy-specific, "the people are all
warriors and mages".) Research behind it: [07-art-direction.md](../../research/07-art-direction.md) §7.3.

![Before and after](img/style-kit-overview.jpg)
![Close-ups](img/style-kit-closeups.jpg)

## What was built (throwaway, nothing in the prototypes or the slice uses it)
| Part | Where | Notes |
|---|---|---|
| Mesh kit | `Assets/Spikes/StyleKit/Runtime/MeshBuilder.cs` | Flat-shaded boxes, prisms, cones, jittered icospheres. Vertex colour = palette, vertex alpha = baked AO, UV0 = (wind sway, emission) |
| Palette | `Palette.cs` | About 60 hand-picked colours; no textures at all |
| The box | `StyleBox.cs` | Terrain from noise (hills at the back, a pond, a flat centre), a soil cross-section behind the glass (gravel, loam, soil, moss lip, pebbles), trees, bushes, rocks, grass tufts, flowers, reeds; **Camp**: teepees, a lean-to, a fire pit, log seats, a drying rack, a woodpile; **Village**: half-timbered houses (thatch, terracotta, slate), a well, a bench, paths, a fenced wheat field, a jetty. Plinth, frame, shelf and wall |
| Villagers | `Villager.cs` | Peg dolls: tapered tunic, belt, stub arms, round head, hair, bun or straw hat; kids at 0.68 scale. No faces, no gear. They amble near home in play mode |
| Master shader | `Shaders/Stylized.shader` (HLSL) | Wrapped diffuse into a soft two-band ramp, cool-tinted shadows, SH ambient × AO, height gradient, faint rim, vertex wind, emission. SRP Batcher compatible (all props in `UnityPerMaterial`) |
| Glass v2 | `Shaders/GlassV2.shader` | The prototypes' `GlassGrime` plus a green-blue thickness edge, two soft reflection streaks and a faint condensation band at the soil line |
| AO bake | `StyleBox.BakeAO` | 10 cosine-weighted rays per unique vertex against a temporary collider: ~300 ms per box in the Editor |
| Rig | `Assets/Editor/Agent/StyleSpikeBuilder.cs` | Menu *AquariumCiv/Agent/Style Spike/Build Camp + Village*. Camp: low warm morning sun; Village: higher midday sun. Trilight ambient, SMAA, post = Neutral tonemap, contrast, split toning, vignette, bloom only above 1.35 (fire, windows). `SpikeOrbit`: drag to orbit, scroll/pinch to zoom |

Scenes: `Assets/Spikes/StyleKit/StyleSpike_Camp.unity`, `StyleSpike_Village.unity`. The boxes generate on load (edit
and play mode); nothing generated is saved, so a rebuild is just reopening the scene.

## Numbers (Editor, not a phone)
- Camp: 61k vertices / 20k triangles in one static mesh; Village: 56k / 19k. Build incl. AO: 0.3–0.45 s.
- Draws: 1 static mesh + water + 4 glass panes + one per villager (9–15), plus the shadow pass. Within the research
  budget (≤ 150 batches, ≤ 150k triangles) by a wide margin. **Not measured on a device yet.**

## Findings
- **The look holds together without any asset pack.** One palette plus one shader makes the houses, trees, tents and
  people look like one set, and the eras differ *in kind* (forest and tents vs paths, fields and roofs).
- **The soil cross-section, shelf and wall do most of the "terrarium on a shelf" work.** The old look test was a floating
  hex island in a void.
- **Peg-doll villagers read as ordinary people** at close zoom, but they're tiny at full-box zoom (as all people would be).
  Camp people in hide colours get lost in the grass; brighter accent colours (a red headband, a coloured sash) would help.
- **Glass still reads mostly by its dark frame.** The edge tint and streaks help up close; at full view the frame does
  the work. Options: a lighter brass/wood frame, a stronger edge tint, or a top rim highlight.
- **Gotcha:** a `DepthOfField` override left in the volume profile (even with no parameters overridden) softened the
  whole frame. The spike runs without DOF; the screen-band tilt-shift (research §4.6) is a follow-up.
- Weak spots: the fire is too small to glow; the faceted ground is a little busy up close; the pond's sandy rim is
  jagged; the white teepee is plain.

## Not done
No device run (60 fps / GPU ≤ 10 ms check), no tilt-shift, no per-era LUT (split toning stands in), no tester-agent
pass (it's a look study for the user to judge, not a player-facing build), no Hamlet or Town era.

## Next, if the direction is liked
1. The user picks what to keep or change (palette, people, glass/frame, mood per era).
2. A phone build of the two scenes to check frame time (needs a TestFlight run: user's go).
3. Move the kit into `Assets/Game` as the slice's art layer: meshes generated from the sim state (buildings from
   `SimState.buildings`, people from notables and the crowd), per-chunk rebuild on change.
