# 07 — Art direction: a simple, good-looking, cheap look

Research date: **2026-10-29**. Target: Unity **6.3 LTS + URP**, iOS/Android portrait, mid-range phones (iPhone 11 /
a 2021 mid Android). Input from the user: *"we use low poly assets or something you build and from there materials,
shaders and all of that good stuff comes together to generate a simple, good looking and performing aesthetics."*

Markers: **[V]** checked in a primary source (vendor, Unity docs, developer interview). **[S]** secondary source only.
**[obs]** my own observation from screenshots or trailers, not a documented technique. **[est]** my estimate; measure it.
This doc gives options and a recommendation. It doesn't decide anything.

---

## 0. TL;DR

- **The look comes from the shared style kit, more than from the meshes.** The cheap stylised games share one palette
  texture, one or two master shaders, one sun with soft ambient, baked (not screen-space) occlusion and a small post
  stack (§2). KayKit already gives us clean meshes. What we lack is everything around them.
- **Weakest parts today:** the glass doesn't read as glass in a still frame (only the dark posts show), the island
  floats in an empty box with no soil or substrate, the grass is a sour yellow-green on a flat beige void, and the post
  stack pays for Bokeh depth of field that barely shows (§1).
- **Recommended pipeline: mixed (option d).** One palette texture and one master Shader Graph for everything. KayKit
  stays for the detailed hero pieces (houses, people). The agent builds the rest procedurally in C#: terrarium
  substrate, terrain, paths, fields, fences, simple era structures, glass and frame, all UV'd onto the same palette
  with baked vertex AO. Blender scripting comes later and only if procedural C# hits a wall (§3).
- **This refines vertical-slice-plan §6 option B** ("KayKit + a custom era layer"). The era layer is built by the agent
  from code instead of AI or hand modelling, and it is held together by option A's polish (glass, plinth, backdrop) (§6).
- **First spike (1–2 agent days):** the "style kit spike". A master material, glass v2, a procedural substrate and
  backdrop, a cheap tilt-shift, and one Camp-vs-Village pair of the same box. It passes if the user picks it over
  today's look test in a blind side-by-side and an iPhone 11 dev build holds 60 fps with ≤ 10 ms GPU time (§7).

---

## 1. What exists now (look test, prototypes, URP settings)

Sources: `AquariumCiv/Assets/Editor/Agent/DioramaBuilder.cs`, `LookTestBuilder.cs`, `Assets/Art/*`,
`Assets/Settings/Mobile_RPAsset.asset`, `Assets/Prototypes/_Shared/Shaders/GlassGrime.shader`, screenshot
`AquariumCiv/Temp/agent/look3.png` and the app icon.

| Area | What is there | What's weak |
|---|---|---|
| Ground | KayKit hex grass/water tiles, radius 3, on a brown plinth cube | Hex prisms float over an empty plinth; no soil, roots or pebbles visible through the glass. The top half of the box is empty air [obs] |
| Props/people | 23 KayKit Medieval Hexagon props (green set), 8 KayKit Adventurers posed idle | Cohesive (one artist), but small at phone size. Adventurers are fantasy heroes, not villagers |
| Materials | Every KayKit FBX imports its own embedded material. **13 byte-identical copies of the 1024² `hexagons_medieval.png` atlas** are in the project; each Adventurer has its own texture | Many materials and texture copies for what is one look. No single place to tune the palette |
| Glass (look test) | 4 URP Lit cubes, transparent, alpha 0.10, smoothness 0.97, no reflection probe; dark frame posts | In a still frame the box reads as a **wireframe**: no reflection, rim or tint on the panes [obs] |
| Glass (prototypes) | `AquariumCiv/GlassGrime` HLSL: object-space UV grime mask, noise, fresnel rim, wipe glint; `UnityPerMaterial` CBUFFER (SRP Batcher friendly) | A good base. It still has no reflection, no edge thickness and no condensation |
| Lighting | One directional sun (soft shadows), Trilight ambient | Flat; no contact shadows or AO, so props don't sit on the ground [obs] |
| Background | Camera solid colour, beige | A void. Nothing says "a box on a shelf in a room" |
| Post (`LookTestProfile`) | **Bokeh** DOF (focal 120, f/4), bloom 0.25 @ threshold 1.1, vignette 0.22, saturation −12, contrast +6, warm filter | Bokeh is URP's slow DOF mode ([Unity DOF](https://docs.unity3d.com/Manual/urp/depth-of-field-volume-override.html)) [V] and the blur hardly shows in look3.png [obs]. With threshold 1.1 almost nothing blooms, but the passes still run |
| URP Mobile asset | Render scale 0.8, HDR on, MSAA off, 1 shadow cascade @ 1024, soft shadows Medium, shadow distance 50, SRP Batcher on, Forward, no renderer features | MSAA off means jagged low-poly edges. The 50 m shadow distance is wide for a box seen from far away with a 22° FOV, so most of the shadow map covers empty frustum [est] |

---

## 2. Reference looks: what makes them work, and what a phone can afford

Few studios document their shading. Where I found nothing primary, the row says **[obs]**.

| Game (platform) | Techniques behind the look | Phone-affordable? |
|---|---|---|
| **Monument Valley** (ustwo, Unity, mobile) | "Writing all our own shaders, using a custom directional lighting model", combined with "Unity's standard lightmapping" for static AO, plus "hefty overdraw for vignetting and faked volumetric glows" ([MCV](https://www.mcvuk.com/development/unity-focus-monument-valley)) [V]. Levels built in Unity, textured in Blender/Photoshop, lightmap baked last ([BlenderNation](https://www.blendernation.com/2014/04/23/monument-valley-a-stunning-ios-game-designed-with-blender/)) [V]. MV3 used in-Unity cube modelling and "brush" decoration tools shared by art and design ([Unity](https://unity.com/en/resources/monument-valley-3-blurring-art-and-design)) [V] | Yes, it shipped on 2014 phones. The lesson: a custom, simple lighting model plus baked AO, with no realtime GI |
| **Bad North** (Plausible Concept, Unity; iOS/Android Oct 2019 ([Pocket Gamer](https://www.pocketgamer.com/articles/081328/bad-north-jotunn-edition-raw-furys-beautiful-rogue-lite-strategy-game-is-available-now-for-ios-and-android/))) | Procedurally assembled islands "merged into one mesh to minimise draw calls" ([Game Developer](https://www.gamedeveloper.com/game-platforms/how-townscaper-works-a-story-four-games-in-the-making)) [V]. Stålberg's AO: occlusion is reprojected to a texture on each change, blurred, and "sampled in the vertex shader" ([tweet](https://x.com/OskSta/status/1168571866405097472)) [V]. Desaturated muted palette, an island on an empty backdrop, tiny readable soldiers [obs] | Yes, shipped on mobile. **Closest technical model for us:** a small island, merged meshes, vertex-sampled AO |
| **Townscaper** (Stålberg; iOS/Android 2021) | Rendering tuned "to minimise aliasing and ensure outlines … look crisp" at any distance; windows cut with the stencil buffer ([Game Developer](https://www.gamedeveloper.com/game-platforms/how-townscaper-works-a-story-four-games-in-the-making)) [V]. Player-picked flat colours, soft AO, sea fog, wonky organic grid [obs] | Yes (shipped on mobile). Outlines plus soft AO are the expensive-looking parts; Stålberg baked rather than computed them per frame |
| **ISLANDERS: Mobile** (Grizzly/Coatsink, Nov 2025) | Minimalist pastel palette, an island on a plinth, flat-ish shading, strong single sun, soft gradient sky [obs]. No technical write-up found | Yes, ships on phones ([App Store](https://apps.apple.com/app/id6745312000)) [V] |
| **Dorfromantik** (Toukana; mobile announced) | Soft painterly palette, gentle height/colour gradients on tiles, ambient life (trains, boats) [obs]. No shader breakdown found | Probably; the mobile port is not out yet ([gamesmarket](https://www.gamesmarket.global/games-and-products/new-platforms-dorfromantik-comes-to-mobile-phones-and-more-consoles-with-the-help-of-clockstone-and-headup-04f6c37bb4b4e2c646fda3aa72551721)) |
| **Tiny Glade** (Pounce Light, PC) | Ray-traced GI and reflections ([forum, S](https://forum.quartertothree.com/t/tiny-glade-deserves-its-own-topic-now/162584?page=2)), custom engine, heavy procedural geometry | **No.** Use it as a mood reference only (warm bounce light, soft shadows). Fake the bounce with ambient and vertex AO |
| **Terra Nil** (Free Lives, Unity) | "Painterly art style, inspired by … Studio Ghibli" ([devlog](https://vfqd.itch.io/terra-nil/devlog/188191/the-future-of-terra-nil)) [V]; colour carries the progress from barren to lush [obs] | Partly; it is a PC game. **The lesson for eras:** a palette shift alone sells a big change |
| Alto, Mini Metro, Poly Bridge | 2D/flat gradients (Alto, Mini Metro); flat-shaded low poly (Poly Bridge) [obs] | Yes, but they say little about a 3D diorama |

**The common pattern** (my synthesis):
1. **One palette, few materials.** Flat or gradient colours from a small set, so meshes batch and recolour cheaply.
2. **Simple lighting model.** A custom or simplified directional light with soft ambient; no PBR detail.
3. **Baked occlusion.** Lightmap or vertex AO gives contact and depth for free at runtime (MV, Bad North).
4. **Composition over detail.** An object on a plinth against a soft backdrop, with a narrow FOV.
5. **A small post stack**: colour grade and vignette; DOF/tilt-shift only where it pays.

---

## 3. Asset pipeline options

| | (a) CC0 kits on one palette | (b) Agent-built procedural in Unity C# | (c) Agent-scripted Blender (bpy) | (d) Mixed: kits for hero pieces, (b) for the rest |
|---|---|---|---|---|
| What | KayKit + Kenney + Quaternius, UVs remapped to one shared palette/atlas | Meshes generated by editor scripts: parametric buildings, terrain, substrate, fences, fields, rocks; vertex colour + palette UVs | Headless `blender -b --python` scripts build and export FBX ([Blender CLI](https://docs.blender.org/manual/en/latest/advanced/command_line/arguments.html)); Cycles can bake AO to colour attributes ([Blender baking](https://docs.blender.org/manual/en/latest/render/cycles/baking.html)) | KayKit for houses and people, procedural for ground, substrate, glass, paths and era props |
| Cohesion | ★★★ within one artist (KayKit); ★★ across artists (needs a palette pass) | ★★ to ★★★: consistent with itself, but the risk is a "programmer art" look next to KayKit | ★★ to ★★★: real bevels and modifiers help it match KayKit | ★★★ if the procedural parts copy KayKit's chunky bevels and use the same palette |
| How far an agent gets alone | Far: importing, remapping UVs, placing. It can't make new *kinds* of things | **Far.** Everything is code: the agent can read, change and regenerate it, with the screenshot loop it already uses | Medium. Blender isn't installed on this machine (checked `C:\Program Files\Blender Foundation`, not present). It adds a second tool, and the agent can't see Blender's viewport, so judging means exporting and screenshotting in Unity | Far |
| Iteration speed | Fast for placement, zero for new shapes | **Fastest**: change a parameter, rebuild the scene in seconds | Slower: script → export → reimport → screenshot | Fast |
| Era variety "in kind" | Limited to what kits contain (Camp → Hamlet → Village only changes huts → homes today, polish #4) | High: tents, lean-tos, wattle huts, fences, fields, paths, palisades are all simple parametric shapes | High | High |
| Licence | CC0 (KayKit, Kenney, Quaternius [V] per [06-assets](06-assets.md)) | Original art → CC BY-SA 4.0 per [decisions.md](../decisions.md). Agent-made output may have thin copyright ([06 §3.4](06-assets.md)); this doesn't block use | Same as (b) | CC0 parts + CC BY-SA parts |
| Performance | Good (low tris, one atlas) | Good, and better if merged per chunk as Bad North does | Good | Good |
| Tools | Free. Optional paid: Kenney Asset Forge (paid, flag it) | Free: Mesh API ([Mesh.MeshData](https://docs.unity3d.com/ScriptReference/Mesh.MeshData.html)), [CombineMeshes](https://docs.unity3d.com/ScriptReference/Mesh.CombineMeshes.html), ProBuilder (free Unity package) | Free (Blender is GPL; its output belongs to us) | Free |

**A palette texture to standardise on (free):**
- **Keep KayKit's own 1024² gradient atlas as the master palette.** Procedural meshes UV into its swatches, so KayKit and
  agent geometry share one texture and material. This is the cheapest path and needs nothing new.
- **Or Imphenzia PixPal**: a CC0 palette texture set with emission and "attributes" (metal/rough/animation) textures, and
  a Unity 6.3 URP project ([forum post by the PixPal port author](https://forum.babylonjs.com/t/imphenzia-pixpal-palette-texture-pbr-node-material-example-updated-to-version-1-2/56066), [imphenzia.com](https://imphenzia.com/)) [S].
  It wants point filtering, no compression, no mips and Repeat wrap. Showcase models are **not** CC0. KayKit would need
  its UVs remapped to it.
- One shared palette means one material, so the SRP Batcher keeps all of it in one shader variant
  ([SRP Batcher](https://docs.unity3d.com/Manual/SRPBatcher.html)) [V], and a palette edit or a LUT recolours the world.

**Era variety, concretely (what option b/d makes possible):**

| Era | Ground and substrate | Structures (procedural unless noted) | Light and palette |
|---|---|---|---|
| Camp | Moss, bare soil patches, a few stones; wild grass | Tents and lean-tos (cones and prisms), fire pit, drying racks, a single KayKit tent | Warm fire glow at dusk (vertex emission), saturated greens |
| Hamlet | Trodden dirt paths (spline strips), first tilled fields (striped quads) | Wattle huts (cylinder + cone roof), fences (instanced posts), a well | Straw yellows enter the palette |
| Village | Cobble paths, hedges, crop patchwork | KayKit homes, mill, tavern; procedural walls and bridges | Lanterns at night; the palette warms and gets richer |

---

## 4. Materials and shaders in URP

### 4.1 Master material (everything opaque)
- **Start from Unity's Shader Graph Custom Lighting sample** (ships in 6.3, Package Manager → Shader Graph → Samples).
  It has Basic, Simple, Toon and URP lighting templates. Unity says **Lit Basic costs about half of URP lighting**, could
  improve scene performance by around 20 %, and "may be ideal for lower-end mobile". It works in Forward and Forward+;
  additional lights need HLSL in a Custom Function node
  ([Unity forum announcement](https://discussions.unity.com/t/shader-graph-custom-lighting-sample/1681765)) [V].
  Cyanilux's MIT sub-graphs are a community alternative ([GitHub](https://github.com/Cyanilux/URP_ShaderGraphCustomLighting)) [S].
- **Inputs:** palette texture (UV0) × **vertex colour** (RGB = tint/variation, A = baked AO) via the
  [Vertex Color node](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Vertex-Color-Node.html).
- **Lighting:** wrapped/half-Lambert diffuse into a soft 2–3 band ramp (Toon template), shadow tint toward a cool
  colour instead of black, a faint rim term, and a world-height gradient (darker and cooler near the substrate, warmer up
  top) to fake bounce. No specular, no reflection probes.
- **Shader Graph vs hand HLSL:** Shader Graph for the master material (the 6.3 sample does the lighting plumbing, and the
  agent can still edit the `.shadergraph` JSON or regenerate it). Hand HLSL stays for the glass, which already exists in
  HLSL and needs object-space mask maths. Keep every property inside `UnityPerMaterial` and avoid `#ifdef`s in it, or SRP
  Batcher compatibility breaks ([incompatibility page](https://docs.unity3d.com/Manual/SRPBatcher-Incompatible.html)) [V].
  Watch a known 6.3 bug: Boolean keywords with Dynamic Branch make Shader Graphs incompatible
  ([issue](https://issuetracker.unity.com/issues/9094/shadergraph-fails-to-make-shader-srp-batcher-compatible-when-boolean-keywords-are-used-with-dynamic-branch-definition)) [V].
- **Per-object colour:** don't use MaterialPropertyBlocks on SRP-batched renderers; they break the batcher "by design"
  ([Unity roadmap](https://activation.unity3d.com/roadmap/75-material-property-block-srp-batcher-compatibility)) [V].
  Put variation in vertex colour (baked) or in an instanced buffer for the crowd.

### 4.2 Occlusion and shadows
- **Baked vertex AO, not SSAO.** SSAO is a renderer feature that needs a depth or depth-normals prepass and extra
  full-screen passes ([URP SSAO](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/shadows-in-urp/post-processing-ssao/ssao)) [V],
  and it isn't on Unity's list of mobile-friendly effects ([URP post on mobile](https://docs-alpha.unity3d.com/Manual/urp/integration-with-post-processing.html)) [V].
  Instead an editor script ray-casts a hemisphere per vertex (or per tile corner) and writes AO into vertex alpha. Rerun
  it when a building is placed, as Bad North does for its whole island.
- **Realtime shadows:** keep one directional shadow, 1 cascade, soft shadows **Low**, and a max distance fitted to the
  camera distance plus the box radius. Enable *Conservative Enclosing Sphere* ([URP performance table](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/optimize-for-better-performance)) [V].
  Citizens get blob shadows (a blurred quad), as Unity suggests for characters
  ([Unity mobile tips](https://unity.com/how-to/mobile-game-optimization-tips-part-2)) [V].
- **Box contact shadow:** a baked soft shadow quad under the plinth onto the shelf. It costs nothing and grounds the box.

### 4.3 The glass (hero material)
Extend the existing `GlassGrime` HLSL rather than starting again. Grime is a mechanic, so we must own this shader ([06 §1.2](06-assets.md)).

| Layer | Cheap technique | Avoid |
|---|---|---|
| Body | Very low alpha tint, slightly green-blue | `_CameraOpaqueTexture` refraction: it needs the Opaque Texture copy, which Unity says to disable on mobile ([URP config](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/universal-render-pipeline/introduction/configure-for-better-performance)) [V] |
| Edges | **Thickness edge**: a tinted band near pane edges (distance-to-edge from object-space UV). This is what makes real glass read as glass [est] | Real geometry bevels on panes |
| Reflection | Fresnel rim + a tiny static cubemap or procedural "window" streaks (2–3 soft diagonal bands in view space) | Realtime reflection probes; probe blending and box projection (Unity says disable on low end) [V] |
| Refraction-lite | A slight darkening and colour shift where you look through two panes; a faint bottom waterline/condensation band | Screen-space distortion ([80.lv, S](https://80.lv/articles/making-stunning-glass-refraction-effects-in-unity) shows the opaque-texture method) |
| Grime | The existing mask + noise | — |
| Condensation | A second noise channel, revealed by a humidity value (fog on the glass = care state, 03 §2.1) | Particle droplets |
| Sorting | Back panes first, then front panes (fixed render queue offsets); ZWrite off | Sorting by distance per frame |

Cost: 4–5 transparent panes covering maybe half the screen means about two layers of blending [est]. That's fine on a
tiler if the fragment shader stays at 2–3 texture reads.

### 4.4 Water, foliage, ground life
- **Water:** an opaque or alpha-blended Shader Graph, vertex bob, scrolling noise for glints, foam from vertex colour at
  shore edges. No depth texture (Unity: disable it unless needed [V]). The paid Stylized Water 3 ($50) is overkill for a
  pond ([06](06-assets.md)).
- **Wind on grass/trees:** a vertex-shader sway masked by vertex colour or height, phased by world position so it
  survives static batching. Staggart's grass shader says "not suitable for mobile" ([06](06-assets.md)) [V].
- **Grass tufts:** a few hundred instanced cards or low-poly tufts drawn with `Graphics.RenderMeshInstanced`, sharing the
  master material with instancing on.

### 4.5 Batching and draw calls
- **SRP Batcher** (on already) plus **static meshes merged per area or era chunk** with `CombineMeshes` when an era
  rebuilds (Bad North's approach [V]). Crowd: `RenderMeshInstanced` ([06 §2](06-assets.md)).
- **GPU Resident Drawer** needs Forward+ and compute shaders, and is not supported on OpenGL ES
  ([Unity](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer)) [V].
  Our Mobile renderer is Forward and a box holds only a few hundred objects, so it isn't worth the build-time and
  variant cost yet [est].
- **Import remap:** an `AssetPostprocessor` that points every KayKit FBX at the one master material (`ModelImporter`
  external object remap) and deletes the 12 duplicate atlas copies.

### 4.6 Post-processing (URP, mobile costs)
Unity lists bloom (High Quality Filtering off), colour grading, vignette, chromatic aberration and lens distortion as
mobile-friendly. It recommends **Gaussian DOF** for lower-end devices and FXAA for anti-aliasing
([URP post on mobile](https://docs-alpha.unity3d.com/Manual/urp/integration-with-post-processing.html)) [V].

| Effect | Recommendation | Cost notes |
|---|---|---|
| Colour grading | **LUT, Grading Mode = LDR, small LUT (16–32)**, Fast sRGB on ([URP perf table](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/optimize-for-better-performance)) [V] | Cheap, and it is the main tool to hold the look together and shift palette per era |
| Vignette | Keep (0.2) | Cheap [V] |
| Bloom | Only for era glows (fire, lanterns, wipe glint). If on: Dual filter, Quarter downscale, HQ filtering off ([Bloom ref](https://docs.unity.com/en-us/engine/6000.3/manual/post-processing-and-full-screen-effects/urp/effect-list/bloom)) [V] | Bloom adds an extra GMEM load on tilers ([Unity XR perf](https://docs.unity3d.com/Manual/urp/understand-performance.html)) [S] |
| Tilt-shift | **Custom**: a Fullscreen Shader Graph via the Full Screen Pass Renderer Feature ([docs](https://docs.unity3d.com/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html)) [V] that blurs by **screen-Y band**, not depth, from a downsampled copy. Or drop it at play zoom and keep it for zoom-out and share shots (as [03 §2.1](03-aquarium-references.md) suggests) | URP's Gaussian DOF is "far-field" only ([DOF](https://docs.unity3d.com/Manual/urp/depth-of-field-volume-override.html)) [V], so it can't blur the foreground band of a top-down miniature. Bokeh is the slow mode [V] |
| Anti-aliasing | Try **MSAA 2×/4×** vs FXAA on device. On tilers MSAA can be cheap: Imagination calls 2× "virtually free" and 4× noticeable ([IMG](https://docs.imgtec.com/performance-guides/graphics-recommendations/html/topics/msaa-performance.html)); Meta measured 4× as "extremely cheap" on Quest ([Meta](https://developers.meta.com/horizon/documentation/unity/gpu-improved-algorithms/)) [V] | Unity's generic advice is to reduce MSAA ([URP config](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/universal-render-pipeline/introduction/configure-for-better-performance)) [V], so measure |
| SSAO, SSR, motion blur | Off | Use baked AO instead |
| HDR | Off if bloom is off. If on, use 32-bit precision ([URP config](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/universal-render-pipeline/introduction/configure-for-better-performance)) [V] | Memory bandwidth |
| Volume updates | Volume Update Mode = Via Scripting (update on era change only) [V] | CPU |

Unity 6.3 also has **on-tile post-processing** for colour grading, vignette and tonemapping. Its docs are XR-focused and
it needs URP's integrated post disabled ([on-tile](https://docs.unity.com/en-us/engine/6000.3/manual/xr/graphics/on-tile-post-processing)) [V].
It is worth one test later and not needed for the spike.

---

## 5. Performance budget and how to measure it

### 5.1 Budget (mid-range phone, portrait) [est], built from the sources below; validate in the spike

| Item | Budget | Why |
|---|---|---|
| Frame rate | 60 fps while touching or the camera moves; 30 fps while idly watching | Idle watching is long; battery and heat matter. 33.3 ms / 16.6 ms budgets ([Unity mobile tips](https://unity.com/how-to/mobile-game-optimization-tips-part-2)) [V]; use `Application.targetFrameRate` / [OnDemandRendering](https://docs.unity3d.com/ScriptReference/Rendering.OnDemandRendering.html) |
| GPU time | ≤ 10 ms on iPhone 11 at 60 fps | Headroom for thermal throttling over a long session [est] |
| Visible triangles | ≤ 150 k (box + crowd) | KayKit props are a few hundred to low thousands each; chibi citizens are about 300–800 verts ([06](06-assets.md)) |
| Batches / SetPass | ≤ 150 batches, ≤ 25 SetPass | One master shader means few SetPass calls; merged chunks keep batches down. No official phone number exists; Arm says to count triangles per state change, not raw draws ([Arm slides, S](https://www.slideshare.net/OwenWu/mobile-graphics-optimization-guides)) |
| Texture memory | ≤ 32 MB for the scene | 1 palette (1024² or smaller), 4–5 grime masks (256² R8), 1 noise, 1 LUT, UI |
| Shadows | 1 directional, 1 cascade, 1024², soft Low, fitted distance | URP perf table [V] |
| Post | ≤ 1.5 ms total (LUT, vignette, tilt-shift at quarter res, optional bloom) | [est] |
| Render scale | 0.8 (current), bilinear upscale | Below 1.0 is Unity's advice [V]; iPhone 11 is 828×1792, so about 662×1434 internally |
| Transparent overdraw | ≤ 3 layers anywhere (glass + glint + UI) | Unity: prefer opaque; overdraw costs GPU [V] |

### 5.2 How to measure in Unity 6
- **Frame Debugger** (Editor): step through draws, check SRP Batcher batches and why they break.
- **Rendering Debugger** → Overdraw mode (Opaque/Transparent/All) for the glass and UI layers
  ([URP Rendering Debugger](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/understand-performance/rendering-debugger.md)) [V].
- **Profiler on device**: a Development Build with autoconnect; the GPU module plus Rendering stats.
- **FrameTimingManager** in the build: CPU and GPU frame time on iOS Metal and Android Vulkan. Note that on Apple TBDR
  GPUs the GPU time can exceed the frame time ([docs](https://docs.unity.com/en-us/engine/current/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager/manager)) [V].
  **Log it to the existing JSONL telemetry**, so TestFlight testers' phones report real frame times.
- **Xcode GPU frame capture** works for Metal ([Unity](https://docs.unity3d.com/Manual/XcodeFrameDebuggerIntegration.html)) [V],
  but **needs a Mac**. The project builds iOS in cloud CI without one ([decisions.md](../decisions.md)), so in practice
  the tools are the Profiler, FrameTimingManager and, for Android, Arm Performance Studio or Android GPU Inspector
  (both free).

---

## 6. Relation to vertical-slice-plan §6

| §6 option | What this research says |
|---|---|
| **A · Keep KayKit, polish the glass** | Necessary but not enough. Glass v2, plinth, backdrop and palette are all in the style kit below. Alone, eras still change only huts (polish #4) |
| **B · KayKit + custom era layer** | **The recommendation refines B:** the era layer is **agent-built procedural geometry on KayKit's atlas** (pipeline d), not AI-generated or hand-modelled props. It costs $0, the agent can change it, and it gives each era a new *kind* of ground, structure and light |
| **C · Restyle (Quaternius / Synty MINI)** | Not needed: the look problem is lighting, glass and composition, not mesh detail. Synty is paid and forbids gen-AI use ([06](06-assets.md)). Quaternius stays a CC0 source for missing props |

---

## 7. Recommendation and staged plan

### 7.1 The style kit (what to build)
1. **Palette:** KayKit's 1024² atlas as the master palette (or PixPal if we outgrow it), one copy, point-filtered.
   Plus a **per-era LUT** (Camp cool-green, Hamlet straw, Village warm).
2. **Master material:** one Shader Graph from the 6.3 Custom Lighting *Toon/Simple* template: palette × vertex colour,
   vertex-alpha AO, wrapped ramp with tinted shadows, height gradient, faint rim, optional wind and vertex emission
   (fire, windows). SRP Batcher compatible, with instancing on for crowd and grass.
3. **Lighting rig:** one warm sun (soft Low, fitted shadow distance), gradient ambient (sky/equator/ground) tuned to the
   LUT, a baked contact shadow under the box, blob shadows for people. No SSAO, no realtime GI.
4. **Glass v2:** `GlassGrime` + thickness edge, fresnel rim, procedural reflection streaks, condensation channel,
   fixed back-to-front queue.
5. **Post stack:** LDR LUT grading + vignette; custom screen-band tilt-shift (zoomed-out and share shots); bloom only
   for glows (Dual, Quarter); MSAA 4× vs FXAA decided on device.
6. **Asset pipeline:** an `AssetPostprocessor` remapping all KayKit to the master material; a C# `ProceduralKit`
   (substrate, terrain, paths, fields, fences, tents, huts, walls) emitting palette UVs, vertex colour and baked AO;
   merged per chunk on era change. All under `Assets/Editor/Agent/` and `Assets/Game/`.
7. **Composition:** a terrarium substrate cross-section (pebbles, soil, moss lip) visible through the glass, the box
   filled closer to the rim, and a backdrop (shelf plank + soft wall gradient + window light) instead of the void.

### 7.2 Stages
| Stage | Content | Agent days [est] |
|---|---|---|
| 0 · Spike | §7.3, judged by the user | 1–2 |
| 1 · Kit in the slice | Master material + remap, glass v2, rig, post, substrate/backdrop in the slice scene builder | 2 |
| 2 · Era layer | Procedural Camp and Hamlet sets, the Village mix, per-era LUT, staggered rebuild (polish #4) | 3 |
| 3 · Perf pass | FrameTimingManager telemetry, device profiling, MSAA/AA choice, chunk merging | 1 |
| Later | Blender (bpy) only if a needed shape is too organic for C# | — |

Stages 1–3 fit the slice's milestone 6 "art pass" (vertical-slice-plan §7), which is budgeted at 4 days including
other work, so the era layer may need its own slot.

### 7.3 First spike: "style kit spike" (1–2 agent days)
**Build** (a throwaway `StyleSpikeBuilder` next to `LookTestBuilder`, same island and camera):
1. One master Shader Graph from the Custom Lighting sample; all KayKit in the scene remapped to it.
2. A vertex-AO baker (hemisphere raycasts → vertex alpha) for tiles and props.
3. Glass v2 (extend `GlassGrime`: edge, rim, streaks, condensation).
4. A procedural substrate cross-section, a contact shadow, and a shelf/wall backdrop.
5. The post stack: LDR LUT + vignette + screen-band tilt-shift; Bokeh DOF removed.
6. **The same box twice:** a Camp version built only from procedural parts (tents, fire pit, moss, dirt) and a Village
   version (KayKit + procedural paths/fences/fields), to test "in kind" change and cohesion between KayKit and
   agent-made geometry.

**Pass criteria:**
- **Looks (the user judges):** in a blind side-by-side at phone size (old look3 vs new; Camp vs Village), the user
  prefers the new look. Someone who hasn't seen it can say "glass box" from one still frame. Procedural pieces don't
  stand out as foreign next to KayKit. People stay readable at default zoom. The `tester` agent checks readability at
  portrait sizes.
- **Frame time:** a Development Build on the user's iPhone (TestFlight) holds **60 fps for 5 minutes** of orbit and
  wipe, with **GPU ≤ 10 ms** (FrameTimingManager, logged), **≤ 150 batches / ≤ 25 SetPass** (Frame Debugger in the
  Editor), and post ≤ 1.5 ms (Profiler GPU module, if it connects).
- **Pipeline:** the agent rebuilds both boxes from code in under a minute, with zero hand-edited assets.
- **Fail signals:** procedural parts look like grey-box placeholders, or GPU > 12 ms. Then fall back to option A polish
  plus kit props (Quaternius/Kenney) for the era layer.

---

## 8. Open questions and unverified points
- Bad North, Townscaper, ISLANDERS and Dorfromantik shading details beyond the quoted sources are **[obs]** only. I found
  no talk or breakdown for ISLANDERS, Dorfromantik or Tiny Glade rendering.
- Phone triangle and draw-call budgets are my estimates; no official Unity or Arm number for phones was found
  (a "~50 draws on Mali" figure circulates second-hand and I couldn't verify it).
- PixPal's licence and contents come from a third-party forum post quoting its readme; check imphenzia.com before use.
- Whether the Profiler can connect to an iOS dev build over Wi-Fi from this Windows machine is untested.
- MSAA cost on the user's actual iPhone is untested. The tiler sources are PowerVR and Quest, not Apple.

---

## Sources
- Unity — URP configure for performance: https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/universal-render-pipeline/introduction/configure-for-better-performance
- Unity — URP performance settings table: https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/optimize-for-better-performance
- Unity — Post-processing in URP (mobile section): https://docs-alpha.unity3d.com/Manual/urp/integration-with-post-processing.html
- Unity — Depth of Field override: https://docs.unity3d.com/Manual/urp/depth-of-field-volume-override.html
- Unity — Bloom override (6000.3): https://docs.unity.com/en-us/engine/6000.3/manual/post-processing-and-full-screen-effects/urp/effect-list/bloom
- Unity — Full Screen Pass Renderer Feature: https://docs.unity3d.com/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html
- Unity — On-tile post-processing (6000.3): https://docs.unity.com/en-us/engine/6000.3/manual/xr/graphics/on-tile-post-processing
- Unity — Understand performance (GMEM loads): https://docs.unity3d.com/Manual/urp/understand-performance.html
- Unity — SSAO in URP: https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/shadows-in-urp/post-processing-ssao/ssao
- Unity — GPU Resident Drawer: https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer
- Unity — SRP Batcher: https://docs.unity3d.com/Manual/SRPBatcher.html and incompatibility: https://docs.unity3d.com/Manual/SRPBatcher-Incompatible.html
- Unity — Shader Graph SRP Batcher bug (6.3): https://issuetracker.unity.com/issues/9094/shadergraph-fails-to-make-shader-srp-batcher-compatible-when-boolean-keywords-are-used-with-dynamic-branch-definition
- Unity — MPB vs SRP Batcher roadmap item: https://activation.unity3d.com/roadmap/75-material-property-block-srp-batcher-compatibility
- Unity — Shader Graph Custom Lighting sample (6.3): https://discussions.unity.com/t/shader-graph-custom-lighting-sample/1681765
- Unity — Vertex Color node: https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Vertex-Color-Node.html
- Unity — Mesh.MeshData: https://docs.unity3d.com/ScriptReference/Mesh.MeshData.html; CombineMeshes: https://docs.unity3d.com/ScriptReference/Mesh.CombineMeshes.html
- Unity — Mobile optimization tips part 2: https://unity.com/how-to/mobile-game-optimization-tips-part-2
- Unity — OnDemandRendering: https://docs.unity3d.com/ScriptReference/Rendering.OnDemandRendering.html
- Unity — FrameTimingManager: https://docs.unity.com/en-us/engine/current/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager/manager
- Unity — Rendering Debugger (6000.3): https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/understand-performance/rendering-debugger.md
- Unity — Xcode frame debugger integration: https://docs.unity3d.com/Manual/XcodeFrameDebuggerIntegration.html
- Unity — Monument Valley 3 case study: https://unity.com/en/resources/monument-valley-3-blurring-art-and-design
- MCV/UK — Unity focus, Monument Valley: https://www.mcvuk.com/development/unity-focus-monument-valley
- BlenderNation — Monument Valley designed with Blender: https://www.blendernation.com/2014/04/23/monument-valley-a-stunning-ios-game-designed-with-blender/
- Game Developer — How Townscaper works (Bad North, Townscaper): https://www.gamedeveloper.com/game-platforms/how-townscaper-works-a-story-four-games-in-the-making
- Oskar Stålberg — AO approach (X): https://x.com/OskSta/status/1168571866405097472
- Pocket Gamer — Bad North on iOS/Android: https://www.pocketgamer.com/articles/081328/bad-north-jotunn-edition-raw-furys-beautiful-rogue-lite-strategy-game-is-available-now-for-ios-and-android/
- App Store — ISLANDERS: Mobile: https://apps.apple.com/app/id6745312000
- gamesmarket — Dorfromantik mobile: https://www.gamesmarket.global/games-and-products/new-platforms-dorfromantik-comes-to-mobile-phones-and-more-consoles-with-the-help-of-clockstone-and-headup-04f6c37bb4b4e2c646fda3aa72551721
- Quarter to Three forum — Tiny Glade RTGI (secondary): https://forum.quartertothree.com/t/tiny-glade-deserves-its-own-topic-now/162584?page=2
- Terra Nil devlog: https://vfqd.itch.io/terra-nil/devlog/188191/the-future-of-terra-nil
- Imphenzia PixPal (via Babylon.js forum) and site: https://forum.babylonjs.com/t/imphenzia-pixpal-palette-texture-pbr-node-material-example-updated-to-version-1-2/56066, https://imphenzia.com/
- Cyanilux URP Shader Graph Custom Lighting: https://github.com/Cyanilux/URP_ShaderGraphCustomLighting
- 80.lv — Glass refraction in Unity: https://80.lv/articles/making-stunning-glass-refraction-effects-in-unity
- Imagination — MSAA performance: https://docs.imgtec.com/performance-guides/graphics-recommendations/html/topics/msaa-performance.html
- Meta — GPU improved algorithms (MSAA on tilers): https://developers.meta.com/horizon/documentation/unity/gpu-improved-algorithms/
- Arm — mobile graphics optimization slides (secondary): https://www.slideshare.net/OwenWu/mobile-graphics-optimization-guides
- Blender — command-line arguments: https://docs.blender.org/manual/en/latest/advanced/command_line/arguments.html; baking: https://docs.blender.org/manual/en/latest/render/cycles/baking.html
- Project docs: [06-assets.md](06-assets.md), [03-aquarium-references.md](03-aquarium-references.md), [00-synthesis.md](00-synthesis.md), [vertical-slice-plan.md](../design/vertical-slice-plan.md), [decisions.md](../decisions.md)
