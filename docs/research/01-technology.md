# 01 — Technology Research: "Aquarium Civilization" Idle Builder

*Researched 2026-10-28. Time-sensitive facts (versions, prices, licensing) were checked against live sources and are cited inline. Anything marked **(unverified)** came from a secondary source or from general knowledge and needs re-checking before a decision depends on it.*

## 0. Project profile (as of scoping answers)

| Axis | Answer | Consequence |
|---|---|---|
| Platform | **Mobile (iOS + Android)** | Battery, thermal limits, build size, OS background limits and store cloud saves become the main constraints. Web and desktop overlay are now secondary. |
| Team | **Solo, already knows Unity/C#** | Learning a new engine costs months. That counts heavily against switching. |
| Visuals | **3D diorama**: a small 3D world in a glass terrarium that the player can rotate (Tiny Glade / Townscaper feel) | You need a 3D renderer with good mobile post-processing, cheap instancing for many small characters, and a glass shader. Most 2D web stacks drop out. |
| Fiction | "Aquarium" is a metaphor: a glass box on land, not underwater | No water/caustics rendering requirement. The glass enclosure plus a tilt-shift miniature look carry the theme. |

---

## 1. Engine candidates

### 1.1 Comparison table (weighted for solo, mobile, 3D diorama, Unity background)

| | **Unity 6 (URP)** | **Godot 4.7 / 4.8** | **Web stack (TS + Three.js/Babylon, or PixiJS/Phaser for 2D) + Capacitor/Tauri** | **Bevy 0.19** |
|---|---|---|---|---|
| Current version | 6.3 LTS is the latest LTS (support until Dec 2027). 6.0 LTS support ends Oct 2026. Update releases continue (the 6.6 ECS status post is dated June 2026) ([release overview](https://unity.com/releases/release-overview), [ECS 6.6 status](https://discussions.unity.com/t/ecs-development-status-for-unity-6-6/1724480)) | 4.7 stable shipped 19 Jun 2026, latest patch 4.7.2 on 18 Aug 2026. 4.8 beta 1 on 15 Oct 2026 ([Godot blog](https://godotengine.org/blog/)) | PixiJS v8.19 ([June 2026 update](https://pixijs.com/blog/june-2026)), with v8.20 reported ([gamedev.net](https://gamedev.net/news/5221-pixijs-v8200-released/)). Phaser 4.0 on 10 Apr 2026, 4.1 on 30 Apr 2026 ([phaser.io](https://phaser.io/download/stable)) | 0.19 on 19 Jun 2026 ([bevy.org/news](https://bevy.org/news/)) |
| Cost | Personal is free up to **$200k** revenue + funding, splash screen optional. Pro costs **$2,310/yr per seat** ($210/mo) from 12 Jan 2026. The **runtime fee was cancelled on 12 Sep 2024 and never took effect** ([Unity pricing updates](https://unity.com/products/pricing-updates)) | Free, MIT | Free (MIT libraries) | Free, MIT/Apache |
| iOS/Android maturity | Excellent. This is Unity's core market: IL2CPP, Metal/Vulkan, store tooling, notifications, IAP and ads packages | Better than before. The Foundation describes mobile as a recent focus ([mobile update Apr 2026](https://godotengine.org/article/godot-mobile-update-apr-2026/)). 4.7 added a stable Android build environment, virtual joystick and gyro ([80.lv](https://80.lv/articles/godot-4-7-has-been-released)). Mobile 3D performance reports are still mixed ([forum](https://forum.godotengine.org/t/now-im-worried-performance-on-android/124716)) | Runs through a WebView wrapper (Capacitor/Cordova). Fine for 2D. 3D WebGL inside a mobile WebView is the weakest option for battery and thermals | Experimental on mobile. No editor. Not realistic for a solo mobile release |
| Many small 3D agents | GPU Resident Drawer / BatchRendererGroup, `RenderMeshInstanced`, Jobs + Burst. Entities (ECS) is now a **core package** ([6.6 status](https://discussions.unity.com/t/ecs-development-status-for-unity-6-6/1724480)) | MultiMesh plus the low-level server APIs ([docs](https://docs.godotengine.org/en/latest/tutorials/performance/using_multimesh.html)). GDScript is slow for thousands of agents, so the simulation needs C# | Instanced meshes in Three.js. JS simulation is fine up to a few thousand agents | Best-in-class ECS, but you build everything else yourself |
| Diorama look (tilt-shift, glass, stylised lighting) | URP: Renderer Features, Full Screen Pass, Shader Graph, a large Asset Store | Possible (Mobile renderer, visual shaders), with fewer ready-made assets | Possible but manual. Mobile WebView GPU budget is tight | Fully custom. Tiny Glade reportedly runs on Bevy ECS with a custom wgpu renderer ([secondary source, unverified](https://foro3d.com/de/2026/mayo/rust-y-bevy-en-tiny-glade-construccion-procedural-sin-cuadricula.html)), built by a two-person team of graphics experts |
| Web export | Unity Web works on mobile browsers since Unity 6. WebGPU is still experimental ([Unity forum](https://discussions.unity.com/t/public-access-to-webgpu-experimental-in-unity-6-1/1572462)) | WebGL2 only (Compatibility renderer). **C# projects still cannot export to web** in 4.7, according to the official docs and user reports ([docs](https://docs.godotengine.org/en/latest/tutorials/export/exporting_for_web.html), [forum](https://forum.godotengine.org/t/is-there-an-update-on-exporting-c-projects-to-web/128821)) | Native strength | Possible (wasm/WebGPU) |
| Desktop overlay later (Steam) | Proven: *Rusty's Retirement* (500k+ sold) is built in Unity ([Wikipedia](https://en.wikipedia.org/wiki/Rusty%27s_Retirement)). See §2.8 | Built-in per-pixel transparency + mouse-passthrough region | Electron `setIgnoreMouseEvents` ([docs](https://www.electronjs.org/docs/latest/tutorial/window-customization)) | Possible through winit |
| Ramp-up for **this** dev | Already known | 1–3 months (C# carries over; the node and scene model is new) | 2–4 months, plus building the 3D tooling | 6+ months (Rust) |
| Verdict | **Recommended** | Credible fallback if you want to avoid Unity licensing risk | Only if the game becomes 2D-first and web-first | Not for this project |

### 1.2 Unity 6: detail for this profile

**Licensing.** Personal is free at or below $200k revenue + funding. Pro is required above that, at $2,310/seat/yr after the 5% increase on 12 Jan 2026. Enterprise applies above $25M ([source](https://unity.com/products/pricing-updates)). The runtime fee was cancelled and Unity's terms were updated on 10 Oct 2024 ([same page](https://unity.com/runtime-fee)). Unity also lists Havok Physics as no longer bundled with Pro from 6.3. That does not matter for this game.

**Which version to use.** Start on **6.3 LTS**, which is supported until Dec 2027 ([release overview](https://unity.com/releases/release-overview)). Move to the next LTS when it ships: the December 2025 ECS post says that will be 6.7 **(unverified)**. Update releases (6.4–6.6+) are where ECS and GameObject unification is landing.

**DOTS/ECS maturity.**
- Entities, Collections, Mathematics and Entities Graphics became **core packages** starting in 6.4. In 6.6 the EntityId change to 8 bytes was completed, managed components were deprecated, and the new Hierarchy became the default ([6.6 status post](https://discussions.unity.com/t/ecs-development-status-for-unity-6-6/1724480)). The "ECS for All" plan (every GameObject backed by an Entity) is still listed as *Planned*, with no date ([roadmap](https://activation.unity3d.com/roadmap/2700-ecs-for-all)).
- Entities Graphics on mobile requires **URP Forward+**, plus **Vulkan or GLES 3.1+ on Android** and **Metal on iOS**. It is **not supported on Web** ([requirements](https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.4/manual/requirements-and-compatibility.html)).
- **Practical advice:** a terrarium probably holds a few hundred to about 2k visible agents. You do not need full ECS for that. Keep agents as plain C# structs in `NativeArray`s, update them with **Jobs + Burst**, and draw them with **GPU instancing** (`Graphics.RenderMeshInstanced` / BRG, or the GPU Resident Drawer). This gives most of the ECS performance without the authoring friction. Switch to Entities only if profiling shows you need it.

**GPU Resident Drawer (GRD).** It needs Forward+ and compute-shader support, and **does not work on OpenGL ES**. Setup: BRG variants set to "Keep All", SRP Batcher on, GRD set to Instanced Drawing ([6.3 docs](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/gpu-resident-drawer.html)). Keeping all BRG variants makes builds slower. On Android, choose Vulkan-only (with a GLES fallback that doesn't use GRD) or test both. Android's own figures: about 85% of active devices support Vulkan, and every 64-bit device on Android 10+ has Vulkan 1.1 ([Android Vulkan overview](https://developer.android.com/games/develop/vulkan/overview)).

**Battery and thermal.**
- An idle game runs in long sessions with a mostly static scene. Lower `Application.targetFrameRate` to 30 (or 20 while nothing is happening). Use `OnDemandRendering.renderFrameInterval` to render less often while the input loop keeps running ([Unity API](https://docs.unity3d.com/ScriptReference/Rendering.OnDemandRendering.html)). This is the single biggest battery saving available.
- **Adaptive Performance** reads thermal and power state and scales quality accordingly. The documentation now lives in the Unity Manual; the 6.0 package contains only samples. Device and provider coverage, especially on iOS, needs verification ([Unity 6.6 manual](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/runtime-performance-scaling/adaptive-performance.md), [package 6.0](https://docs.unity3d.com/Packages/com.unity.adaptiveperformance@6.0/manual/index.html)).
- Budget a fixed simulation tick (for example 5–10 Hz for AI decisions) separately from rendering, and interpolate visuals between ticks.

**Build size.** Community reports put an empty Unity 6 IL2CPP ARM64 APK/AAB at roughly **22–32 MB**, mostly `libil2cpp.so` ([forum](https://discussions.unity.com/t/libil2cpp-so-is-too-big/1603857), [AAB thread](https://discussions.unity.com/t/aab-size-reduction-for-instant-apps-hard-to-keep-under-15mb-with-newer-unity-versions/1534684)). To keep it down: Strip Engine Code, a high managed stripping level (watch for reflection breakage, see the GameKit note in §2.5), remove unused built-in packages, enable shader-variant stripping, use ASTC textures, and use Addressables / Play Asset Delivery for optional content. Budget about 60–150 MB for a stylised 3D idle game.

**URP mobile docs:**
- [URP manual for 6.3](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/urp-introduction.html) and its "Optimize for better performance" section
- Unity e-book *"Optimize your mobile game performance"* (Unity 6 edition), available from [unity.com/resources](https://unity.com/resources) **(look up the latest edition)**
- [Mobile Notifications 2.4 manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/index.html). The latest patch is 2.4.3. It requires Android API 33+ SDK and Xcode with the iOS 15.2+ SDK.

### 1.3 Godot 4.x: fallback option
- **Pros:** free with no licence risk, small binaries, fast iteration, C# hot-reload on by default in 4.7 ([80.lv](https://80.lv/articles/godot-4-7-has-been-released)), and a desktop transparent window built in.
- **Cons for this profile:** mobile 3D on Godot has fewer shipped examples and some performance complaints ([forum](https://forum.godotengine.org/t/now-im-worried-performance-on-android/124716)). C# blocks web export entirely ([docs](https://docs.godotengine.org/en/latest/tutorials/export/exporting_for_web.html)); an unofficial workaround exists, [2dog.browser-wasm](https://www.nuget.org/packages/2dog.browser-wasm.debug), but it is single-threaded. Cloud saves, notifications and IAP depend on community plugins. You also lose your Unity experience.
- **Many agents:** use MultiMeshInstance3D for rendering and C# arrays for the simulation. The docs recommend going to the servers directly once you have thousands of processed instances ([MultiMesh docs](https://docs.godotengine.org/en/latest/tutorials/performance/using_multimesh.html)). MultiMesh has no per-instance culling, which doesn't matter in a single terrarium.
- **Docs:** [docs.godotengine.org](https://docs.godotengine.org/en/stable/), [Exporting for Android](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_android.html), [iOS](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_ios.html), [C# basics](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html).

### 1.4 Web stack (TypeScript)
- Strong for a **2D** or web-first idle game: PixiJS v8 (WebGPU path) and Phaser 4 (new renderer) are both current. For Steam, you would wrap it with Electron + [steamworks.js](https://github.com/ceifa/steamworks.js) (MIT; covers achievements and cloud). Tauri has no built-in Steamworks support, so you'd need a custom Rust plugin ([GDevelop forum](https://forum.gdevelop.io/t/implementing-steam-achievements-with-tauri/73202)).
- **For a 3D mobile diorama it is a poor fit.** Three.js/Babylon inside a Capacitor WebView costs more battery, has weaker thermal control, and lacks native notifications, cloud saves and IAP unless you add plugins. Not recommended for this profile.

### 1.5 Bevy and others
- **Bevy 0.19** (Jun 2026) introduced the BSN scene format ([gamefromscratch](https://gamefromscratch.com/bevy-0-19-released/)). It has an excellent ECS, but no editor, breaking changes every release, and an immature mobile toolchain. Not suitable for a solo mobile developer.
- **Unreal 5:** overkill for this scope, and its mobile build size and battery cost are poor for an idle game. **Defold:** a good fit for small mobile games, but mainly 2D.

---

## 2. Technical problems specific to this game

### 2.1 Simulating hundreds to thousands of autonomous agents cheaply
Layered approach, cheapest first:
1. **Separate the simulation from the visuals.** The authoritative state is aggregate: population counts by role, resource stocks, pollution, disease load. Individual agents are a *visual sample* of that state, at most N on screen, tied to real numbers through LOD. Only what the camera can see needs individual behaviour.
2. **Simulation LOD:** agents near the camera run full AI and pathing at 5–10 Hz. Far or hidden agents run "statistical" updates at 1 Hz or less. Off-screen work is just resource flows. When the camera turns, promote or demote agents.
3. **Data layout:** struct-of-arrays in `NativeArray`, processed with `IJobParallelFor` + Burst ([Burst manual](https://docs.unity3d.com/Packages/com.unity.burst@latest), [Job system](https://docs.unity3d.com/Manual/job-system.html)). Use time-slicing so each frame updates only 1/k of the agents.
4. **Rendering:** GPU instancing. For animated people, use **vertex animation textures (VAT)** or very simple procedural bob and lean in the shader, not `Animator` components (each `Animator` costs CPU per agent). Tiny low-poly figures with 2–3 animation cycles are enough at diorama scale.
5. **Pathfinding:** a small, bounded world. Use **flow fields** toward shared destinations (food, work, home) instead of per-agent A*, or a coarse grid A* cached per destination. Use [A* Pathfinding Project](https://arongranberg.com/astar/) only if you need it.
6. **Full ECS** (Entities + Entities Graphics) only if profiling shows you need it. See the [EntityComponentSystemSamples repo](https://github.com/Unity-Technologies/EntityComponentSystemSamples) for official patterns.

### 2.2 Offline progress (the core mechanic)
**Do not rely on the app running in the background.** iOS `BGAppRefreshTask` runs opportunistically and briefly, with only one pending refresh request at a time ([Apple docs](https://developer.apple.com/documentation/backgroundtasks/bgapprefreshtask), [guide](https://developer.apple.com/documentation/uikit/using-background-tasks-to-update-your-app)). Background tasks reportedly don't run after the user force-quits the app ([forum](https://developer.apple.com/forums/thread/719222)). Android WorkManager is likewise deferrable. **Compute the elapsed time when the app resumes.**

Recommended **hybrid** model:
- **Abstract/analytic catch-up** for the full absence, which may be hours or days. Step the *aggregate* model (population, resources, pollution, disease, overgrowth) at a coarse fixed timestep, for example one simulated minute per step, with a cap on total steps. Use closed-form solutions where the equations are linear or exponential. Either way it is cheap: thousands of coarse steps take milliseconds. Keep the aggregate model deterministic (seeded RNG, fixed-point or carefully ordered float maths) so a save plus elapsed time always gives the same result. That makes bugs reproducible.
- **Event generation** during catch-up. Log notable threshold crossings ("A plague swept the east village", "the moss overgrew the granary") to build a *"While you were away"* timeline. This turns offline time into the watching experience the game is about.
- **Short deterministic replay for continuity.** On resume, re-spawn the visual agents from the aggregate state. Optionally run 30–60 s of full simulation at accelerated speed behind a fade, so the agents appear mid-activity.
- **Degradation design:** cap how bad offline neglect can get (a "dormancy" floor) so returning after a week isn't a game over. This is a design choice, but it also limits the payoff from clock cheating.
- Design math references: Anthony Pecorella, *The Math of Idle Games*, parts [I](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-i), [II](https://www.gamedeveloper.com/disciplines/the-math-of-idle-games-part-ii), [III](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-iii) (Kongregate; growth vs cost curves, prestige).

**Clock-tampering protection** (single-player, so aim for "casual-proof", not perfect):
- Store `lastSaveUtc` together with a **monotonic counter**. Android `SystemClock.elapsedRealtime()` and iOS `ProcessInfo.systemUptime` (or `CLOCK_MONOTONIC_RAW`) cannot be changed by the user but reset on reboot. If the wall-clock delta is far larger than the uptime delta while no reboot happened, the clock was tampered with **(general technique; unverified source)**.
- When online, get **trusted time** from your own backend (for example the HTTP `Date` header, or a Unity Cloud Code function that returns server time) and use it to set or validate elapsed time. When offline, accept the local time but **cap offline credit** (for example 8–24 h) and treat negative deltas (clock moved back) as zero. The caps-plus-server-time pattern is described in [LootLocker's guide](https://lootlocker.com/guides/how-to-create-an-energy-system-that-can-t-easily-be-cheated) and the [Unity forum](https://discussions.unity.com/t/increase-a-variable-while-game-is-offline-666215/666215).
- If there is no IAP or leaderboard, cheating only hurts the cheater, so keep the checks lightweight.

### 2.3 Local notifications and OS limits
- Use the **Unity Mobile Notifications** package ([2.4 manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/index.html)). Schedule notifications **when the app goes to background**, using times *predicted* by the same aggregate model ("Pollution will hit critical in ~6 h", "Harvest festival ready"). Reschedule on every pause.
- **Android 13+:** `POST_NOTIFICATIONS` is a runtime permission, so ask for it in context, not at first launch. **Android 14+:** `SCHEDULE_EXACT_ALARM` is **denied by default** for new installs ([Android 14 change](https://developer.android.com/about/versions/14/changes/schedule-exact-alarms)). Use inexact notifications (within minutes), which are fine for an idle game.
- **iOS:** permission prompt via UNUserNotificationCenter. The commonly cited limit is **64 pending local notifications** per app **(unverified; check the [UserNotifications docs](https://developer.apple.com/documentation/usernotifications))**. Keep a small rolling set.
- Don't over-notify. Two to three meaningful "care needed" alerts per day suit the aquarium-maintenance fantasy without driving uninstalls.

### 2.4 Save system
- **Format:** a versioned binary or JSON blob of the *aggregate* state plus the RNG seed plus `lastSaveUtc` and the monotonic stamp. Visual agents are not saved; they are rebuilt from the aggregate state. Include a `schemaVersion` and migration functions from day one.
- **Atomicity:** write to a temp file, fsync, then rename. Keep one rolling backup. Save on `OnApplicationPause(true)` / `OnApplicationFocus(false)`, because mobile apps can be killed without a quit event. Save to `Application.persistentDataPath`.
- **Serialization:** `System.Text.Json`/Newtonsoft (`com.unity.nuget.newtonsoft-json`), or MemoryPack/MessagePack for compact binary.

### 2.5 Cloud saves on iOS and Android

| Option | Platforms | Notes |
|---|---|---|
| **Unity Cloud Save** + Unity Authentication (Sign in with Apple / Google Play Games / anonymous) | iOS, Android, cross-platform | Free tier per the pricing page: **5 GiB storage, 1M reads and 1M writes per month**. After that: $0.50/GiB, $0.025 per 10k writes, $0.009 per 10k reads. Unity announced (Nov 2025) that the free cloud tier would grow to 25 GB; **check which applies** ([UGS pricing](https://unity.com/products/gaming-services/pricing), [announcement](https://unity.com/products/pricing-updates)). Gives you Cloud Code for server time. [Cloud Save docs](https://docs.unity.com/ugs/manual/cloud-save/manual/overview) |
| **Google Play Games Services, Saved Games** | Android | Official [Unity plugin v2](https://developer.android.com/games/pgs/unity/overview) with a [Saved Games guide](https://developer.android.com/games/pgs/unity/saved-games) (automatic or manual conflict resolution). Saved Games must be enabled in the Play Console. |
| **Apple GameKit `GKSavedGame`** via Apple's official Unity plug-ins ([github.com/apple/unityplugins](https://github.com/apple/unityplugins)) | iOS/macOS | Needs the iCloud capability. Known issue: managed code stripping set to Low removes the `GKSavedGame` constructor, so set stripping to Minimal for that assembly or add `link.xml` ([Apple forum](https://developer.apple.com/forums/thread/768441)) |
| **iCloud Key-Value Store** | iOS | Small payloads only (Apple documents a 1 MB total limit **(verify)**). Open-source wrapper: [gilzoide iCloud KVS](https://openupm.com/packages/com.gilzoide.key-value-storage.icloud-kvs/), with a sibling [cloud-save](https://openupm.com/packages/com.gilzoide.cloud-save/) package covering Game Center and Play Games |
| Commercial wrappers | both | Voxel Busters Essential Kit ([cloud services](https://assetstore.essentialkit.voxelbusters.com/features/cloud-services/usage)) |

**Recommendation:** Unity Cloud Save with platform sign-in. It is one code path, works across devices and platforms, and provides server time through Cloud Code. Use the "last-writer-wins by simulated time" rule plus a user prompt if two saves diverge widely. Native PGS/GameKit is worth it if you want zero backend dependency.

### 2.6 Large numbers
This is probably **not needed**. A civilisation's population and resources stay well inside `double`/`long` unless you design exponential incremental currencies. If you do: [BreakInfinity.cs](https://github.com/Razenpok/BreakInfinity.cs) (C# port of [break_infinity.js](https://www.npmjs.com/package/break_infinity.js); last push 2022) or [break_eternity.js](https://github.com/Patashu/break_eternity.js) for absurd ranges.

### 2.7 Agent AI architecture
- **Utility AI** is the best fit for autonomous villagers who weigh needs (hunger, rest, fear of disease, work) and gives The Sims-like emergent behaviour. It is cheap to evaluate and easy to tune with response curves. References: Dave Mark & Kevin Dill, *Improving AI Decision Modeling Through Utility Theory*, GDC 2010 ([GDC Vault](https://gdcvault.com/play/1012410/Improving-AI-Decision-Modeling-Through)). Dave Mark & Mike Lewis, *Building a Better Centaur: AI at Massive Scale*, GDC 2015, introduced the Infinite Axis Utility System ([GDC Vault](https://www.gdcvault.com/play/1021848/Building-a-Better-Centaur-AI) **(URL unverified)**). Overview: [Wikipedia: Utility system](https://en.wikipedia.org/wiki/Utility_system).
- **GOAP** suits multi-step plans (gather wood, build, deliver), but it costs more and is overkill for most villagers. Reference: Jeff Orkin, *Three States and a Plan: The AI of F.E.A.R.* ([PDF](https://pages.cs.wisc.edu/~dyer/cs540/handouts/gdc2006_orkin_jeff_fear.pdf), [Orkin's GOAP page](https://alumni.media.mit.edu/~jorkin/goap.html)).
- **Behavior trees** suit authored routines for special characters. In Unity, the official **Behavior** package (`com.unity.behavior`) provides graph-authored BTs.
- **Suggested hybrid:** utility scoring picks a *job/need*, and a small FSM or scripted task sequence carries it out. Use **smart objects / affordances** (buildings advertise what they satisfy, as in The Sims) so new buildings don't require new AI code. Evaluate utility at low frequency (every 1–3 s, staggered).

### 2.8 Procedural generation of the terrarium
- **Irregular-grid WFC, as in Townscaper/Bad North:** Oskar Stålberg's technique (quad-relaxed irregular grid plus marching-cubes-style modular tiles). See [mxgmn/WaveFunctionCollapse](https://github.com/mxgmn/WaveFunctionCollapse) for the base algorithm. Stålberg's talks are on YouTube and GDC (search "Oskar Stålberg Townscaper talk"; **specific links not verified**).
- For a single bounded terrarium, simpler is enough: heightmap noise ([FastNoiseLite](https://github.com/Auburn/FastNoiseLite), C# single-file) plus rule-based placement and a **seed**, so worlds are reproducible and cheap to save.

### 2.9 Stylised diorama rendering in URP
- **Tilt-shift / miniature look:** URP has no built-in tilt-shift. Options:
  - **(a)** URP *Depth of Field* in **Gaussian** mode, which is cheaper than Bokeh and acceptable on mobile, with near and far ranges set around the terrarium.
  - **(b)** A custom **Full Screen Pass Renderer Feature** with a Shader Graph Fullscreen shader: a screen-space vertical gradient mask plus downsampled blur. This is the cheapest and fits the "miniature" look best ([Full Screen Pass docs](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/renderer-features/create-full-screen-pass.html)).
  - **(c)** Asset Store packs, for example SC Post Effects ([forum discussion](https://discussions.unity.com/t/how-to-achieve-tilt-shift-with-unity-s-post-processing-stack/759156)).
  - Add more saturation and contrast, a soft vignette and a slightly high camera FOV-to-distance ratio to sell the scale.
- **Glass / terrarium enclosure:** the cheapest convincing approach on mobile is a **transparent fresnel rim + reflection probe/cubemap + faint smudges/condensation texture, without refraction**. Real refraction needs URP's **Opaque Texture** (an extra full-screen copy per frame) with UV offset by normals ([Mirza Beig's PRISM URP Glass](https://mirzabeig.notion.site/PRISM-URP-Glass-22e8c1476e31805eb2e6da0b584fd583), [80.lv write-up](https://80.lv/articles/making-stunning-glass-refraction-effects-in-unity)). Use downsampled opaque texture. Condensation and dirt on the glass can show the **cleanliness state** directly, which supports the care mechanic.
- **Lighting:** bake or mostly use one real-time directional light plus SH/ambient. Use soft blob or low-res shadows. A day/night cycle by gradient-lerping light colour is cheap.
- **Many-character look:** a shared material/atlas, so everything stays SRP-Batcher and instancing compatible.

### 2.10 Desktop overlay (brief, possible future Steam port)
*Rusty's Retirement* is a Unity game ([Wikipedia](https://en.wikipedia.org/wiki/Rusty%27s_Retirement)). In Unity, a transparent, click-through, always-on-top window uses Win32/Cocoa calls (`DwmExtendFrameIntoClientArea`, `WS_EX_LAYERED`/`WS_EX_TRANSPARENT`). [kirurobo/UniWindowController](https://github.com/kirurobo/UniWindowController) wraps this for Windows and macOS, with alpha or raycast hit-testing. One report says transparency broke in 6.3 because of the DXGI swapchain setting ([forum](https://discussions.unity.com/t/how-to-make-a-unity-game-window-transparent-and-click-through/1704947)). Godot has built-in per-pixel transparency plus `window_set_mouse_passthrough`. Electron has `setIgnoreMouseEvents(true, {forward:true})`. A Unity mobile codebase can later ship a desktop overlay build with a different camera and UI layout.

---

## 3. Curated documentation (primary sources first)

**Unity (recommended stack)**
- Release and LTS status: https://unity.com/releases/release-overview. Pricing: https://unity.com/products/pricing-updates
- URP 6.3 manual: https://docs.unity3d.com/6000.3/Documentation/Manual/urp/urp-introduction.html. GPU Resident Drawer: https://docs.unity3d.com/6000.3/Documentation/Manual/urp/gpu-resident-drawer.html
- Jobs: https://docs.unity3d.com/Manual/job-system.html. Burst: https://docs.unity3d.com/Packages/com.unity.burst@latest
- Entities 1.4: https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/index.html. Entities Graphics requirements: https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.4/manual/requirements-and-compatibility.html. Samples: https://github.com/Unity-Technologies/EntityComponentSystemSamples. ECS status thread: https://discussions.unity.com/t/ecs-development-status-for-unity-6-6/1724480
- Adaptive Performance (6.6 manual): https://docs.unity.com/en-us/engine/6000.6/manual/analysis/runtime-performance-scaling/adaptive-performance.md
- OnDemandRendering: https://docs.unity3d.com/ScriptReference/Rendering.OnDemandRendering.html
- Mobile Notifications: https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/index.html
- UGS Cloud Save: https://docs.unity.com/ugs/manual/cloud-save/manual/overview. UGS pricing: https://unity.com/products/gaming-services/pricing
- Unity e-books: *Optimize your mobile game performance*, *Introduction to URP for advanced creators* at https://unity.com/resources (Unity 6 editions)

**Platform (mobile)**
- Play Games Services Unity plugin: https://developer.android.com/games/pgs/unity/overview. Saved Games: https://developer.android.com/games/pgs/unity/saved-games
- Android 14 exact alarms: https://developer.android.com/about/versions/14/changes/schedule-exact-alarms. Vulkan: https://developer.android.com/games/develop/vulkan/overview
- Apple Unity plug-ins (GameKit etc.): https://github.com/apple/unityplugins. BackgroundTasks: https://developer.apple.com/documentation/backgroundtasks. UserNotifications: https://developer.apple.com/documentation/usernotifications

**Design / AI / idle**
- Pecorella, *Math of Idle Games* I–III (links in §2.2)
- Mark & Dill, Utility Theory (GDC 2010): https://gdcvault.com/play/1012410/Improving-AI-Decision-Modeling-Through
- Orkin, F.E.A.R. GOAP: https://pages.cs.wisc.edu/~dyer/cs540/handouts/gdc2006_orkin_jeff_fear.pdf
- WFC: https://github.com/mxgmn/WaveFunctionCollapse

**Fallback engine**
- Godot docs: https://docs.godotengine.org/en/stable/. Blog / releases: https://godotengine.org/blog/. Web export limits: https://docs.godotengine.org/en/latest/tutorials/export/exporting_for_web.html

---

## 4. Preliminary recommendation

**Use Unity 6 (start on 6.3 LTS) + URP Forward+ + Jobs/Burst + GPU instancing**, with no full ECS at first. Use Unity Cloud Save + Authentication for cloud saves and server time, and Unity Mobile Notifications for care reminders. Model the civilisation as a **deterministic aggregate simulation** with a **visual agent layer** over it. Offline progress is computed analytically when the app resumes, presented as a "while you were away" event log, with offline credit capped and server time used when available.

Why: the developer already knows Unity, Unity has the most mature iOS/Android pipeline and store integrations, and Personal is free up to $200k with the runtime fee permanently gone. URP has every building block the diorama look needs. Godot is the only credible alternative, and for mobile 3D in C# it offers less while costing the ramp-up time.

**Open questions that would change this:**
1. **Agent count and fidelity.** If the design needs more than ~5k individually simulated, visible agents, adopt Entities + Entities Graphics early (Vulkan/Metal only), or rethink with LOD aggregation.
2. **Web or desktop-overlay as a second SKU.** A web build favours Unity (Godot C# cannot export to web). A desktop overlay works in both but is easier in Godot.
3. **Monetisation (IAP/ads, leaderboards).** If time-sensitive monetisation exists, you need server-authoritative time and the save on a backend (Cloud Code), not just caps.
4. **Licensing risk tolerance.** If Unity's past policy swings are unacceptable, Godot 4.7/4.8 (C#, Mobile renderer) is the hedge, at a cost of roughly 1–3 months of ramp-up and thinner mobile tooling.
5. **Art pipeline.** Hand-modelled modular kit vs procedural (Townscaper-style WFC). Procedural increases tech scope considerably for a solo developer.
6. **Minimum device spec.** Supporting GLES-only Android devices rules out GPU Resident Drawer and Entities Graphics on those devices; you would need a plain instancing fallback.
