# 06 — Art assets, crowd animation and AI asset tools

Research date: **2026-10-28**. Target: Unity **6.3 LTS + URP**, iOS/Android. Prices are in USD and were checked live on the date above. Store prices change often and some were on sale (the Halloween sale on itch.io, for example). **[V]** marks a figure verified on the vendor's own page. **[S]** marks a figure taken only from a secondary source; check it before you spend money.

---

## 0. TL;DR

- **Recommended base: Kit A, "KayKit gradient-atlas".** KayKit packs are CC0, use one 1024² gradient atlas each, and have low tri counts. Medieval Hexagon covers primitive and medieval eras, City Builder Bits covers town and modern, Forest Nature covers nature, and Adventurers plus Character Animations cover citizens. The industrial era and terrarium props are gaps, filled by Kenney (CC0) or by AI props recoloured onto the same atlas. **Assets cost $0–99. Including tools, the total is about $160–210** (see §4).
- **Synty is a strong option, but read its licence before choosing it.** SyntyPass costs **$40/mo or $360/yr, with a 3-month minimum**. After cancelling, you may not "substantially develop" the product any further, which suits a live idle game poorly. Both Synty EULAs also **forbid "generation of 3D models utilising Generative AI Programs"** with Synty assets. If you pick Synty, buy one-time licences for the Simple and MINI packs, about $150 (Kit B).
- **Crowds:** use a VAT (vertex-animation-texture) bake with GPU instancing for 100+ citizens. Add procedural bob-and-squash for the tiniest LOD. Most older GPU bakers are dead or Built-in-only (see §2).
- **AI 3D:** **Meshy Pro ($20/mo)** has an API, a `target_polycount` parameter, and triangle "smart topology" down to 100 tris. **Tripo Pro ($20/mo billed annually)** is the alternative. **TRELLIS.2 (MIT)** is the self-hostable fallback but needs a 24 GB GPU. Avoid Hunyuan3D open weights if you live in the EU, UK or South Korea, because the licence excludes those territories.
- **Audio:** ElevenLabs (SFX on Starter, about $6/mo; commercial use, API). Music from **Suno Pro ($8/mo, commercial rights)** or ElevenLabs Music. **Avoid Udio**: downloads have been disabled since the UMG deal and it is moving to a "walled garden".
- **Disclosure:** Steam requires disclosure of pre-generated AI content that players see or hear (Jan 2026 rules). I found no general AI-labelling rule for static game assets on Apple or Google Play; their 2026 rules target apps that *generate* AI content.

---

## 1. Base art packs

### 1.1 Per-pack comparison

| Pack (source) | Price | Licence: commercial mobile / private git | URP / Unity 6 | Poly budget & texturing | Era coverage | Notes |
|---|---|---|---|---|---|---|
| **KayKit Medieval Hexagon** ([itch](https://kaylousberg.itch.io/kaykit-medieval-hexagon)) | PWYW $0; Extra ≥$9.99, Source ≥$14.99 [S] | CC0 ✔ / ✔ | Generic FBX/glTF; use URP Lit/Simple Lit | 200+ low-poly tiles, buildings, props | Primitive→medieval | Remaster of the legacy [Medieval Builder Pack](https://kaylousberg.itch.io/kaykit-medieval-builder-pack) (CC0, frozen) |
| **KayKit City Builder Bits / Forest Nature / Resource Bits** ([catalog](https://kaylousberg.itch.io/)) | $0 or more [V]; **The Complete KayKit $99** [V] | CC0 ✔ / ✔ | Generic | Same style family | Town/modern + nature + resources | Same artist, so the most cohesive free set |
| **KayKit Adventurers + Character Animations** ([itch](https://kaylousberg.itch.io/kaykit-adventurers)) | Free; Extra/Source $3.97–11.95 (page inconsistent) [V] | CC0 ✔ / ✔ | FBX/glTF, Humanoid-compatible rig | **One 1024² gradient atlas, downscalable to 128²** [V] | Fantasy citizens; "Mystery Monthly" packs $13.39 add more | Chibi proportions read well at tiny scale |
| **Kenney** Fantasy Town Kit v2, City Kit (Roads/Commercial/Industrial), Factory Kit, Mini Forest ([3D list](https://kenney.nl/assets/category:3D), [Fantasy Town](https://kenney.nl/assets/fantasy-town-kit)) | Free (optional donation) | **CC0** ✔ / ✔ ([support](https://kenney.nl/support)) | Generic | Very low-poly, colour-map textures | Medieval town→city→**industrial** (City Kit Industrial v2.0, 2026 [S]) | Best free source for the industrial era |
| **Kenney Mini Characters** ([kenney](https://kenney.nl/assets/mini-characters), [OGA](https://opengameart.org/content/mini-character-1)) | Free | CC0 ✔ / ✔ | Generic | Tiny chibi; 12 chars, 32 anims [S, OGA] | Modern-ish civilians | Good crowd base. Different proportions from KayKit |
| **Quaternius Medieval Village MegaKit** ([site](https://quaternius.com/packs/medievalvillagemegakit.html), [itch](https://quaternius.itch.io/medieval-village-megakit)) | Standard free (170 assets); **Pro ≥$9.99; Source ≥$14.99** [V] | CC0 ✔ / ✔ | Source tier ships Unity **URP** implementation [V] | 304 modular pieces, textured | Medieval | Modular walls and roofs; more detailed than KayKit |
| **Quaternius Stylized Nature MegaKit** ([site](https://quaternius.com/packs/stylizednaturemegakit.html)) | Std free / Pro / Source (prices on itch) | CC0 ✔ / ✔ | Source has URP shaders | 116 models; 7 swappable leaf types | Nature (all eras) | |
| **Quaternius Ultimate Modular Characters** ([site](https://quaternius.com/packs/ultimatemodularcharacters.html)) / **Universal Animation Library** 120+ anims ([site](https://quaternius.com/packs/universalanimationlibrary.html)) | Free / Pro tiers | CC0 ✔ / ✔ | Humanoid, retargetable | Universal Base Characters average **~13k tris** ([site](https://quaternius.com/packs/universalbasecharacters.html)), too heavy for crowds | Generic | Use the **animation library** for retargeting onto chibi characters |
| **Synty SIMPLE Town / City / Buildings / Farm** ([Simple Town](https://syntystore.com/products/simple-town-cartoon-assets)) | $19.99 / $9.99 / $9.99 / $9.99 [V] | One-time EULA: perpetual, **5 seats per licence** ✔ commercial; private repo OK for seat holders. **No gen-AI 3D generation** ([EULA](https://syntystore.com/pages/one-time-purchase-licence)) | Unity 2021.3+, **URP + Built-in** [V]; Unity 6 needs URP material conversion ([Starter Pack note](https://syntystore.com/products/polygon-starter-pack)) | Very low-poly cube style, single atlas | Town/modern; farm | "Mobile ready" |
| **Synty SIMPLE People 1/2/3, SIMPLE Citizens** ([People](https://syntystore.com/products/simple-people-cartoon-assets)) | $19.99 each [V] | Same | Unity 2020.3+, URP + Built-in [V] | Cube people, Mecanim rig, **no anims** | Modern civilians | Pair with Mixamo or Synty animation packs |
| **Synty POLYGON MINI Fantasy / MINI City** ([Mini Fantasy](https://syntystore.com/products/polygon-mini-fantasy-pack)) | $19.99 each; Mini Fantasy Characters $39.99 [V] | Same | Unity 2022.3+, URP + Built-in [V] | "Mobile game assets", atlas | Medieval + modern city | Closest Synty style to a "tiny diorama" |
| **Synty POLYGON Town / Nature / Farm** | $49.99 each; Fantasy Kingdom $349.99 [V] | Same | URP + Built-in | Mid-poly POLYGON style | Wide | Heavier, more realistic |
| **SyntyPass** ([store](https://syntystore.com/products/syntypass)) | **$40/mo** (3-month prepaid minimum, $120) or **$360/yr** [V, converted from the store's Shopify rate] | Subscription EULA: commercial OK; **after expiry you may only make minor bug fixes, with no substantial development** ([EULA](https://syntystore.com/pages/standard-subscription-licence) §12.2); gen-AI 3D generation forbidden (§1.4) | Same as packs | All 150+ packs + **Sidekick** modular character tool; $10/mo store credit | Everything | A poor fit for a live idle game that keeps receiving updates |
| **polyperfect Low Poly Animated People** ([Asset Store](https://assetstore.unity.com/packages/3d/characters/humanoids/low-poly-animated-people-156748)) | $30 [V] | Standard Asset Store EULA ✔ | Built-in/URP/HDRP on **6000.0.36** [V]; v3.02 (Nov 2025) | **~500–800 verts per character** [V] | Modern civilians (many professions) | Good crowd source; flat-colour style fits Kenney/KayKit |
| **Polytope Low Poly Modular Village** ([Asset Store](https://assetstore.unity.com/packages/slug/258073)) | $59.99 [V] | Asset Store EULA | Built-in + URP (2021.3.40), **not HDRP** [V] | **Complete buildings 2,458–58,403 tris** [V], too heavy for mobile at scale | Medieval | Skip for mobile, or decimate heavily |
| Asset Store "low poly village" cheapies ([90535](https://marketplace.unity.com/packages/3d/environments/low-poly-village-asset-90535), [57263](https://assetstore.unity.com/packages/3d/low-poly-medieval-village-57263)) | $5.99 / $4.99 [S] | Asset Store EULA | Unverified for Unity 6 | Flat-shaded | Medieval | Inconsistent style; not recommended as a base |

**Licence notes**
- CC0 packs (KayKit, Kenney, Quaternius) can live in a private, or even public, git repo. No attribution is needed. KayKit asks only that you don't resell unmodified copies.
- The Asset Store EULA does not explicitly cover private git. Unity staff on the forums say a private repo shared with the project team is fine, except for per-seat or "Restricted" assets ([forum](https://forum.unity.com/threads/can-paid-assets-be-pushed-to-private-git-repos.535371/), [EULA FAQ](https://assetstore.unity.com/browse/eula-faq)). A solo developer with a private repo is safe.
- **The Synty AI clause matters for an agent pipeline.** Do not feed Synty models or screenshots into Meshy/Tripo image-to-3D. Use AI only on references you made yourself or that are CC0.

### 1.2 Terrarium glass, water and foliage (URP)

| Need | Option | Price | URP / Unity 6 | Mobile | Verdict |
|---|---|---|---|---|---|
| Jar glass + grime | **Custom Shader Graph** (fresnel rim, fake reflection cubemap, grime mask in a RenderTexture painted and erased at runtime) | $0 | ✔ | ✔ if you skip `_CameraOpaqueTexture` refraction | **Recommended.** Grime is a gameplay mechanic, so you must own the shader |
| Glass reference shaders | [Daniel Ilett Shader Toolbox (URP)](https://danielilett.itch.io/shader-toolbox-for-unity-urp), [Glass Shader URP/LWRP](https://assetstore.unity.com/packages/vfx/shaders/glass-shader-urp-lwrp-203694) | varies | URP | Refraction is costly | Use for learning or as a base only |
| Water (pond, river) | **Stylized Water 3** (Staggart) ([store](https://assetstore.unity.com/packages/vfx/shaders/stylized-water-3-287769)) | **$50** [V] | **URP only**, Unity 6000.0.68+, v3.3.3 (Oct 15 2026), Render Graph [V] | Features can be disabled "making it scalable to use on mobile" [V] | Optional; a small pond could use a simple custom shader |
| Grass/foliage wind | Staggart **Stylized Grass Shader** ([store](https://assetstore.unity.com/packages/vfx/shaders/stylized-grass-shader-143830)) | $25 | URP (2021–2023) | **"Not suitable for mobile platforms"** [V] | **Skip.** Use vertex-colour wind in your own Shader Graph |

---

## 1.3 Proposed kits

| | **Kit A — KayKit gradient-atlas (recommended)** | **Kit B — Synty Simple + MINI (one-time)** | **Kit C — Quaternius + Kenney CC0** |
|---|---|---|---|
| Primitive / huts | KayKit Medieval Hexagon (tents, huts, resources) + AI huts on the KayKit atlas | Simple Farm + AI | Quaternius Medieval Village (simplest pieces) + Kenney Mini Forest |
| Village / medieval | KayKit Medieval Hexagon | POLYGON MINI Fantasy | Quaternius Medieval Village MegaKit Pro |
| Town / modern | KayKit City Builder Bits | SIMPLE Town / City / Buildings | Kenney City Kits + Fantasy Town v2 |
| Industrial | Kenney City Kit Industrial + Factory Kit, recoloured to the KayKit palette | AI-generated (or SyntyPass for a month) | Kenney City Kit Industrial + Factory Kit |
| Nature | KayKit Forest Nature | Simple / MINI nature props | Quaternius Stylized Nature MegaKit |
| Citizens | KayKit Adventurers + Character Animations (+ Mystery series); fallback Kenney Mini Characters | SIMPLE People 1–3 + Mixamo | Kenney Mini Characters + Quaternius Universal Animation Library |
| Cohesion | ★★★ one artist, one gradient-atlas workflow | ★★ Simple and MINI are two Synty sub-styles | ★★ two artists, so a palette pass is needed |
| Licence risk | none (CC0) | AI-3D clause; Unity 6 URP conversion needed | none (CC0) |
| **Asset cost** | **$0** (fair support: Complete KayKit $99) | ≈ **$150** (Town 19.99 + City 9.99 + Buildings 9.99 + Farm 9.99 + MINI Fantasy 19.99 + People 19.99 + People 2 19.99 + MINI Fantasy Chars 39.99 = $149.92), or $360 for one year of SyntyPass | **≈$10–35** (Medieval Village Pro $9.99 + nature/character Pro tiers) |

---

## 2. Animating many tiny characters on mobile

| Approach | Product | Price | Unity 6 / URP | Status | Fit |
|---|---|---|---|---|---|
| VAT baker + GPU instancing | **VATMachine – GPU Animator & VAT Baker** ([store](https://assetstore.unity.com/packages/tools/animation/vatmachine-gpu-animator-vat-baker-378396)) | $59.99 [V] (snapshots show 50% sales) | URP + HDRP, **not Built-in** [V] | v1.0.0, Sep 17 2026; **submitted on 6000.4.11**, so confirm it imports into 6.3 LTS before buying | Best modern fit: LODs, shared clock, frame interpolation, GPU instancing |
| GPU skinning crowd | **GPU Instancer Pro + Crowd Animations** ([GPUI Pro](https://assetstore.unity.com/packages/tools/utilities/gpu-instancer-pro-290293), [Crowd](https://assetstore.unity.com/packages/tools/animation/gpu-instancer-pro-crowd-animations-323280)) | $128 + $128 (Crowd requires GPUI Pro) [V] | Built-in/URP/HDRP; Unity 6000.0.23+ ([forum](https://discussions.unity.com/t/released-gpu-instancer-pro/1488733)) | Active (v0.12.20 / 0.10.6, Oct 7 2026) | Overkill for 2–300 chibis; strong if you scale to thousands |
| GPU ECS Animation Baker | [store](https://assetstore.unity.com/packages/tools/animation/gpu-ecs-animation-baker-250425) | — | — | **No longer available** [V] | ✘ |
| GPU Animation Baker Basic/Pro | [Basic](https://assetstore.unity.com/packages/tools/animation/gpu-animation-baker-basic-135791) | $19.99 / $49.99 [S] | No URP claim; Standard shaders | Old | ✘ |
| Unity Animation Instancing | [GitHub](https://github.com/Unity-Technologies/Animation-Instancing) | Free | Unity 5.4-era, Built-in | Last push 2024-03, licence NOASSERTION | ✘ |
| codewriter mesh-animation | [GitHub](https://github.com/codewriter-packages/mesh-animation) | MIT | Needs a URP shader port | Last push 2022-12; 2048-vertex cap | Fine for a DIY reference |
| **DIY VAT** | Editor script bakes `SkinnedMeshRenderer.BakeMesh` per frame into an RGBAHalf texture; Shader Graph samples by `SV_VertexID` + time; `Graphics.RenderMeshInstanced` | $0 | ✔ | — | Chibis are ~300–800 verts, so a 512×N texture is easy. Agent-friendly |
| Animation source | **Mixamo** ([Adobe FAQ](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html)) | Free with an Adobe ID | Humanoid FBX | Unmaintained but up in 2026 [S] | Royalty-free in games; don't redistribute the raw files (a private repo is fine) |
| | KayKit Character Animations / Quaternius UAL (CC0) | Free | Humanoid | Active | Preferred because they are CC0 |
| **Procedural bob & squash** | Vertex shader: `y += abs(sin(t·f + id))·h`, scale XZ/Y squash, yaw toward velocity; no skeleton | $0 | ✔ | — | **Use this for the farthest LOD and for idle/walk at ≤20 px tall.** Effectively free on GPU; stylistically on-brand (Townscaper / ISLANDERS feel) |

**Recommended crowd stack:** a static mesh with a vertex-shader bob/squash for all citizens. When the camera zooms in, switch to VAT clips: walk, work, carry, celebrate, cough (pollution). Simulation stays in plain C# or Jobs and rendering goes through `RenderMeshInstanced`. No Animator components.

---

## 3. AI tools for minor assets

### 3.1 3D generation

| Tool | Price | Commercial licence | Low-poly control | API / agent use | Notes |
|---|---|---|---|---|---|
| **Meshy** ([pricing](https://www.meshy.ai/pricing), [API](https://docs.meshy.ai/en/api/text-to-3d)) | Free 100 cr (CC BY 4.0); **Pro $20/mo, 1,000 cr ≈100 assets**; Premium $40; Ultra $100; Studio $70+$10/seat [V] | Paid plans: private, customer-owned [V] | **`model_type: smart-topology`, `meshy-t2.5`, `target_polycount` 100–30,000**; remesh 100–300k; `decimation_mode` [V]. Note that `lowpoly` mode **retires 30 Oct 2026** and `art_style` is now ignored | REST API from Pro, 20 req/s, assets kept 3 days [V] | Models: meshy-6, **meshy-7.1** (latest) |
| **Tripo** ([pricing](https://www.tripo3d.ai/pricing)) | Free 200 cr (**non-commercial**); **Pro $20/mo billed annually ($240/yr), 3,000 cr ≈200 models**; Max $90/mo billed annually; Team $55/seat/mo [V] | Paid: private, commercial [V] | Retopology in the free toolkit; Smart Mesh on Pro [V] | Separate developer API, credit-based ([AWS listing](https://aws.amazon.com/marketplace/pp/prodview-hwlbvx7vubh7c): $0.01/credit overage) [S] | Model v3.0 [S] |
| **Hyper3D Rodin** ([pricing](https://hyper3d.ai/pricing)) | Free (pay $1.50/credit on download); Creator $30/mo ($24 annual); Business $120/mo ($96 annual) [V] | Paid plans: "unlimited export and any use" [V] | Not documented for low-poly | **API only on Business** [V] | Gen-2 / 2.5; expensive for a solo dev |
| **Hunyuan3D** ([2.0 licence](https://huggingface.co/tencent/Hunyuan3D-2/blob/main/LICENSE)) | Open weights 2.0/2.1 free; 3.0/3.1 hosted only ([Scenario](https://help.scenario.com/articles/5967392966-hunyuan-3d-models-the-essentials)) | Community licence **excludes EU, UK and South Korea**; above 1M MAU you need Tencent's permission | Polygen mode for quad topology (3.x) [S] | Tencent Cloud API; self-host | Avoid if you live in the EU |
| **TRELLIS.2** ([HF card](https://huggingface.co/microsoft/TRELLIS.2-4B/blob/main/README.md)) | Free | **MIT** (GitHub repo spdx MIT, last push Jul 2026) | None; dense mesh, may have holes [S] | Self-host: Linux + **≥24 GB NVIDIA GPU**; also hosted on Krea [S] | Use as a fallback; needs a decimation pass |
| Newer 2026 / aggregators | Krea 3D (Hunyuan3D 3.1 Pro, TRELLIS 2, Tripo; Pro $35/mo) ([Krea blog, Oct 2026](https://www.krea.ai/blog/best-ai-3d-model-generators-2026)); Unity AI mesh generator (≈8–13 credits/mesh) ([Unity credits](https://docs.unity.com/ai/credits/credits-about)) | | | | Unity AI generates inside the Editor; credits are monthly, with no rollover ([terms](https://unity.com/en/legal/unity-ai-credits-terms)) |

**Quality for low-poly stylized props.** In my reading of the vendor docs (not hands-on tests), none of these tools output clean 200-tri "KayKit-like" props on their own. They produce PBR-textured blobs. Smart topology (Meshy t2.5) or retopology (Tripo) plus a Blender decimate and palette bake (§4) is mandatory. Simple props such as barrels, crates, rocks, stumps and trash bags work well. Architecture should come from the packs.

### 3.2 Textures, icons, UI

| Tool | Price | Commercial | API | Notes |
|---|---|---|---|---|
| **Unity AI** generators (sprite, texture, PBR, sound) ([docs](https://docs.unity3d.com/Manual/unity-ai.html), [credits](https://docs.unity.com/ai/credits/credits-about)) | Credits included per seat; Personal ≈$10/mo for 1,000 credits [S] | Check the [AI credit terms](https://unity.com/en/legal/unity-ai-credits-terms) | In-Editor; the Assistant is scriptable | Texture 1–5 credits, sprite/image 1–6 credits [V, "representative"] |
| **Scenario** ([help](https://help.scenario.com/en/articles/pricing-plans/), [API pricing](https://docs.scenario.com/page/api-pricing)) | Starter $15/mo, Pro $45/mo [V-help] | Commercial use [S] | **Yes**, per-generation API; custom style training | Best option for training a style LoRA on your own icon set |
| **Midjourney** ([plans](https://docs.midjourney.com/docs/plans)) | $10 / $30 / $60 / $120 per month [S] | Yes; companies with **>$1M revenue must use Pro or Mega** | **No public API** | Concept art only |
| **Leonardo** | Apprentice $12, Artisan $30, Maestro $60 [S] | Paid tiers [S] | Separate pay-as-you-go Production API [S] | |
| **Stable Diffusion / FLUX (local)** | Free (GPU) | Depends on the model licence (check each model card) | ComfyUI HTTP API | Fully agent-scriptable |

### 3.3 SFX and music

| Tool | Price | Commercial | API | Status Oct 2026 |
|---|---|---|---|---|
| **ElevenLabs** SFX + Music ([pricing](https://elevenlabs.io/pricing)) | Starter $6/mo (30k cr), Creator $22 (121k), Pro $99 [V] | Starter and up: commercial, including "Music commercial use"; Free: no [V] | Yes (REST) | SFX **200 cr/generation** (Starter ≈150 SFX/mo); music 900 cr/min [V] |
| **Suno** ([pricing](https://suno.com/pricing)) | Free (no downloads, no commercial use); **Pro $8/mo** (2,500 cr, 20 downloads); Premier $24 [V] | Pro/Premier: commercial; post-WMG terms give a *licence* to exploit rather than ownership [S] | No official public API | Licensed v6 models after the WMG settlement ([MBW](https://www.musicweek.com/labels/read/warner-music-group-settles-lawsuit-and-signs-ai-licensing-deal-with-suno/093129)); GEMA lost a case against Suno in Germany; UMG has not settled [S] |
| **Udio** | — | — | — | **Avoid.** Downloads disabled since the UMG settlement (Oct 2025); licensed "walled garden" platform pending ([heise](https://heise.de/-10983564), [MBW Apr 2026](https://www.musicbusinessworldwide.com/after-universal-warner-and-merlin-deals-now-udio-inks-licensing-agreement-with-kobalt)) |

### 3.4 Store disclosure rules

- **Steam:** the Content Survey must disclose pre-generated AI content that ships to players (art, audio, text, store page). Since the Jan 2026 rewrite, dev-efficiency tools such as code assistants are exempt ([Notebookcheck](https://www.notebookcheck.net/Steam-updates-AI-disclosure-form-requiring-developers-to-report-visible-and-in-game-AI-but-not-background-tools.1206103.0.html), [legal summary](https://legalmoveslawfirm.com/steam-ai-policy/)). AI props and SFX in this game **must be disclosed** on Steam.
- **Apple:** the 16 Oct 2026 guideline update targets AI-*generation* apps (NCII, filtering, reporting) and hidden features (2.5.19) ([ppc.land](https://ppc.land/apples-app-store-rules-now-require-apps-to-remove-ai-intimate-imagery/)). I found no labelling requirement for pre-made AI assets.
- **Google Play:** the AI-Generated Content policy covers apps that generate content at runtime (in-app reporting) ([Android Dev Blog, Aug 2026](https://android-developers.googleblog.com/2026/08/ensuring-safety-genai-preventing-non-consensual-intimate-content.html)). There is no explicit disclosure rule for static assets, but store listings must not mislead ([Revera](https://revera.legal/en/info-centr/news-and-analytical-materials/2114-ii-v-mobilnyx-prilozheniyax-trebovaniya-google-play/amp/)).
- **Copyright:** purely AI-generated output may not be copyrightable in the US. Hand-edited, palette-baked props strengthen authorship ([promise.legal](https://blog.promise.legal/ai-game-assets-copyright-steam-disclosure-2026/)).

---

## 4. Recommendation

**Kit A (KayKit + Kenney industrial + KayKit/Kenney citizens), crowd rendering through a VAT pipeline, and AI only for props.**

| Item | Cost |
|---|---|
| KayKit packs (Medieval Hexagon, City Builder Bits, Forest Nature, Resource Bits, Adventurers, Character Animations) | $0. Recommended: **The Complete KayKit $99** (sources, extras, supports the artist) |
| Kenney City Kit Industrial, Factory Kit, Mini Characters | $0 |
| VATMachine (or DIY VAT at $0) | $59.99 (check that it imports into 6.3 first) |
| Stylized Water 3 (optional) | $50 |
| **One-time total** | **$0 minimum / ≈$160–210 recommended** |
| Meshy Pro (props via API) | $20/mo while producing |
| ElevenLabs Starter → Creator (SFX + music) | $6–22/mo |
| Suno Pro (alternative music) | $8/mo |
| Scenario Starter (icons, optional) or Unity AI credits | $0–15/mo |
| **Recurring during production (3–4 months)** | ≈ **$35–65/mo**, ≈ $150–250 total |

### Consistency workflow for AI props (agent-runnable)

1. **Lock the palette.** Extract KayKit's 1024² gradient atlas and store it as `Assets/Art/Palette/palette.png`, with a JSON swatch list (≈32 colours plus era tints). Every asset samples this one texture, so one material and one SRP Batcher batch serve the whole scene.
2. **Generate.** Meshy API: `text-to-3d` or `image-to-3d` with `model_type: smart-topology`, `ai_model: meshy-t2.5`, `target_polycount` 150–600 for props (≤1,500 for hero buildings). The prompt suffix is fixed: "low-poly, flat shaded, chunky, miniature diorama, no text". Image references come from your own concept art or CC0 renders only, never Synty assets.
3. **Clean up in Blender headless** (`blender -b -P clean.py`): merge by distance, Decimate (planar 5° then collapse to budget), recalculate normals, apply transforms, set the pivot to the bottom centre, and scale to grid units (1 hex or tile = 1 m).
4. **Palette bake.** Delete AI textures. For each face, take the average texture colour and pick the nearest palette swatch in ΔE (CIELAB). Write UVs pointing to that swatch's texel, which is the KayKit gradient-atlas technique. Optionally take vertex colour for AO.
5. **Validate.** A Unity editor script checks the tri budget, single material = palette, bounds against the grid, and that no textures are imported. Then it renders a 256² thumbnail next to a reference KayKit prop for a human glance.
6. **Grime and era variation in the shader, not the mesh.** Use a world-space noise or grime mask and a moss/overgrowth vertex-colour channel, plus era tints that shift the palette row. One asset then serves clean, dirty and overgrown states.
7. **Log provenance.** Write the tool, prompt and date to `Assets/Art/AI/_provenance.csv` for the Steam survey and copyright records.
