# Tooling notes and lessons log

Read on demand (CLAUDE.md links here). **Append a dated entry the moment you solve a tooling problem**, so nothing
is solved twice. Keep entries short: the symptom, the cause, the fix.

Tags: **[verified]** = checked on this project or in our Pipeline docs. **[inherited]** = learned on another Unity 6
project (shared workflow doc, 2026-10-28); likely true here but unconfirmed. Upgrade it to [verified] or delete it
when you hit it.

Pipeline command reference: `AquariumCiv/Library/PackageCache/com.unity.pipeline@*/Documentation~/commands/*.md`.

---

## 1. When a Unity call stalls: check for a modal dialog first

A native dialog in the Editor blocks the main thread, so every `unity command` times out and a test run stays
"running" forever. [inherited, matches Pipeline batch.md "modal-dialog hazard"]

- Run `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/unity-dialogs/dismiss-dialogs.ps1` (report only).
  Add `-Click` to answer the safe ones, and `-Click -Loop -Seconds 600` in the background during long test runs. [verified]
- Safe answers it gives: "...quit?" -> **No**; "Scene(s) Have Been Modified" -> **Cancel**; native popup menu -> Esc.
- Everything else is printed as UNKNOWN. Decide by hand:
  - "Modified externally (Reload / Ignore)" after a `git checkout`/`pull` that touched the open scene: the files on
    disk are what git holds, so **Reload** is usually right. If unsure, ask.
  - "Recovering Scene Backups" after a crash: ask the user. Don't guess.
  - Safe Mode (compile errors on open): fix the compile errors. CLAUDE.md says to report this.
- Common triggers: entering play mode or running PlayMode tests with a dirty scene (save it first), switching branches
  with a scene open, `editor invoke_menu` on items that open dialogs.

## 2. Pipeline CLI facts (differ from older Unity MCP/CLI tools)

- **Test filter** [verified, build-and-compilation.md]: `test run --filter` is a *case-insensitive partial match* on
  the test name; `--filter_type assembly|category` selects by assembly or `[Category]`. Prefer
  `--filter_type assembly --filter AquariumCiv.Prototypes.Shared.Tests` to run one test assembly. A filter that matches
  nothing silently runs 0 tests, so always check the count in the result.
- **`test run` is synchronous by default with a 600 s timeout.** For anything long, pass `--async_tests true`, then
  poll `test status`. Only trust `completed` after you've seen this run as running: `test status` returns the previous
  run's results until a new one starts. [inherited, plausible from the docs]
- **`unity test` (top-level CLI)** writes `test-results.xml` to the current directory by default. Pass
  `--output F:/GameDev/AquariumCiv/Temp/agent/test-results.xml`. `--affected` runs only the tests a change can reach,
  which is good for targeted runs. [verified from --help; how it behaves while the Editor is open is unconfirmed]
- **Use `job wait` instead of client-side polling loops** [verified, wait.md]: a server-side condition checked every
  frame. A synchronous wait blocks every other command, so use `--async true` + `job wait_status` for long waits or
  when the condition depends on a command you haven't sent yet.
- **`script eval` / `eval_file`**: default timeout 30 s. On timeout the code **keeps running** detached, so its
  effects may still land. Check the result on disk before re-running anything that isn't idempotent. [verified,
  scripts.md]
- **Prefer `script run`** (compiles a project `.cs` file and runs a named entry point) over long escaped C# strings
  through `eval`. [verified, scripts.md]
- **Screenshots**: `capture screenshot --view game --output <abs path> --width W --height H` writes anywhere (use
  `Temp/agent/`). `capture game_view --save_path` is project-relative: keep it out of `Assets/`. `source=camera` never
  sees overlay canvases, so use the default `screen` for HUD checks. [verified, capture.md]
- **`runtime simulate_pointer` / `simulate_key`** are tagged `runtime`: they drive a running **Player** (dev build with
  the runtime Pipeline), not Editor play mode. To simulate input in Editor play mode, use a PlayMode test with the
  Input System's `InputTestFixture` (touch via `TouchSimulation` / `Touchscreen`). [verified from docs; not yet tried]
- **`console get`** returns `groundTruth.compilationFailed` / `compiling`. `console status` is cheap to poll during a
  compile. Echo the returned `session` as `since_session` when following with a cursor. [verified, runtime.md]

## 3. Compile and domain reload

- `unity recompile --project-path F:\GameDev\AquariumCiv --timeout 60` waits for the compile and reports errors.
  `--strict` fails on warnings.
- Right after a recompile, a status or test call can still see the **previous** assemblies (a fix "doesn't work",
  new tests missing). Wait for `compiling=false` (`console status` or `unity status --format json`), then a few
  seconds more, and sanity-check that the test count includes your new tests. [inherited]
- Don't edit scripts while a PlayMode run is going: the domain reload kills the run. [inherited]
- If domain reload on entering play mode is disabled (Enter Play Mode Options), static fields keep their values
  between plays. Avoid mutable statics, or reset them in `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`.
  [inherited; check our project setting before relying on it either way]

## 4. Assets and ScriptableObjects

- An already-loaded ScriptableObject keeps its serialized values across script reloads, so changing a field
  initializer does nothing for existing assets. Change values through `SerializedObject` + `SetDirty` +
  `AssetDatabase.SaveAssetIfDirty`. [inherited]
- Don't hand-edit a `.asset` the Editor has loaded: the next save writes the in-memory copy back over your edit (also
  a CLAUDE.md rule). [inherited]
- Fields lazily cached on a ScriptableObject need `[System.NonSerialized]`. [inherited]
- Replacing a mesh with `EditorUtility.CopySerialized` can leave the old mesh on the GPU, so screenshots stay
  pixel-identical. Rebuild through the Mesh API (`Clear()`, then set data). [inherited]
- `EditorApplication.delayCall` doesn't fire while the Editor is in the background. [inherited]

## 5. Tests

- Tests that pause the game (`timeScale = 0`) hang on `WaitForSeconds`: use `WaitForSecondsRealtime`. [inherited]
- Real UI Toolkit clicks from tests need `Application.isFocused`, and virtual keyboard/touch input needs
  `InputSystem.settings.backgroundBehavior = IgnoreFocus` and
  `editorInputBehaviorInPlayMode = AllDeviceInputAlwaysGoesToGameView` while the Test Runner has focus. Restore both
  afterwards. [inherited]
- With two pointer devices (the real mouse plus a test mouse/touch), position actions set to `Value` can pick the
  stale device. Make pointer position/delta actions **PassThrough**. [inherited]
- A killed PlayMode run can leave `Assets/InitTestScene*.unity`: delete it before committing. [inherited]
- Never run two test runs or poll loops at once: back-to-back runs can wedge the runner. Fix: `test cancel`,
  `console clear`, rerun; if it wedges again, restart the Editor. [inherited]

## 6. Editor memory and hangs

- The Editor grows with every PlayMode run (several GB after a few dozen). If RAM gets low or background commands get
  killed: save, restart the Editor, re-run what was killed. Keep the Profiler closed or not recording (its frame
  buffer alone reached 2.4 GB). [inherited]
- Idle Asset Import Workers can hold ~2 GB each: `EditorUserSettings.standbyImportWorkerCount = 0`. [inherited]
- **Frozen Editor** (every call times out, Editor.log stops moving, no dialog found): ask the user before killing
  `Unity.exe`, unless they have said otherwise. Files on disk are safe. [inherited + our "report if stuck" rule]

## 7. Shell and files (Windows)

- Run `unity` from Bash, not PowerShell (CLAUDE.md). Git Bash rewrites arguments that start with `/` into Windows
  paths; `export MSYS_NO_PATHCONV=1` when passing hierarchy paths like `/Root/Child`. [inherited]
- Big heredocs with C#/Python bodies (apostrophes, backticks) can fail with "unexpected EOF": write the script with the
  Write tool and run the file. [inherited]
- Line endings: Python text-mode writes and `sed -i` silently flip CRLF/LF. Read and write with `newline=''`. Never
  `sed -i` C# with `\n` in the replacement. [inherited]
- PowerShell 5.1 `$x = SomeFunction` captures everything the function writes to output, not just its `return` value
  (this bit `dismiss-dialogs.ps1`). [verified 2026-10-28]

## 8. UI Toolkit (for when we leave IMGUI prototypes)

- USS has no `:first-child`/`:last-child`, `gap`, `line-height`, `text-transform`, `box-shadow`, `backdrop-filter`, gradients,
  `calc()`, `vw`, `rem`, `grid` or `animation`: the importer warns and ignores them. Space children with one-sided margins.
  Line height: rich tag `<line-height=150%>` works. [verified, #164, Unity 6000.3]
- **`:nth-child()` and `:not()` are import errors that leave the whole .uss empty**, with only one line in the console.
  No styles at all after an edit? Look for those. [verified, #164]
- 6000.3 has `filter` (blur, grayscale, contrast, hue-rotate, invert, opacity, sepia, tint, custom), `-unity-material`,
  `aspect-ratio`, `-unity-text-auto-size`, `text-shadow`, tiled 9-slice. Filters blur the element's own subtree only, spread
  inwards too, and re-render every frame. See docs/design/ui/limits.md. [verified, #164]
- Runtime panels never show `tooltip`: draw your own. Don't add a child to a `Label` that should size to its text. A
  `GeometryChangedEvent` handler that moves its own element loops ("Layout update is struggling"): ignore sub-pixel
  changes. [inherited]

---

## Log

- 2026-10-28: Started this file from a shared Unity 6 workflow doc; added `Tools/unity-dialogs/dismiss-dialogs.ps1`
  (tested against a dummy MessageBox: detects it and clicks the safe answer).

- **2026-10-29 · `unity test` refuses while the Editor is open** ("project is already open in a running Editor").
  Use the Pipeline instead: `unity command test run --mode editor --filter <Assembly> --filter_type assembly`
  (synchronous, prints the summary). [verified]
- **2026-10-29 · Game-view screenshots don't show IMGUI (`OnGUI`) panels.** In play mode, run
  `UnityEngine.ScreenCapture.CaptureScreenshot("F:/GameDev/AquariumCiv/Temp/agent/x.png")` through `script eval`,
  then check the file exists with a second eval (it is written at the end of the frame). [verified]
- **2026-10-29 · Long C# for `script eval`:** write it to `AquariumCiv/Temp/agent/<name>.cs` and pass
  `"$(cat file)"` from Bash. The result is under `.data.result.result` in `--format json`. [verified]
- **2026-10-29 · `OnApplicationPause(false)` fires at startup** (Editor too), not only on resume. Track a
  "was paused" flag before treating it as a resume. [verified]
- **2026-10-29 · CI Unity license:** this machine has no `C:\ProgramData\Unity\Unity_lic.ulf` (only the Hub's
  entitlement XML). Generate one via Hub → Licenses → Add → free personal license. See docs/testflight.md. [verified]
- **2026-10-29 · Tester: portrait Game view sizes + touch playtests in Editor play mode.** [verified]
  - Game view size: add/select a fixed resolution through reflection (`UnityEditor.GameViewSizes` singleton →
    `GetGroup(Standalone)` → `AddCustomSize(new GameViewSize(FixedResolution, w, h, name))`, then set
    `GameView.selectedSizeIndex`) from `script eval` before entering play mode. `Screen.width/height` and IMGUI then
    follow it (checked 1080x1920, 1080x2400, 1640x2360). Custom "Tester WxH" entries stay in the size list.
  - Touch input: a throwaway PlayMode test deriving `InputTestFixture` with `InputSystem.AddDevice<Touchscreen>()`.
    The asmdef must be **any platform** (an Editor-only asmdef's `[UnityTest]`s are not found in PlayMode: 0 tests).
    Set `InputSystem.settings.updateMode = ProcessEventsManually` and pump `InputSystem.Update()` from a
    `[DefaultExecutionOrder(-10000)]` MonoBehaviour; queue `BeginTouch/MoveTouch/EndTouch(..., queueEventOnly: true)`
    one step per frame so EnhancedTouch phases (Began/Moved/Ended) reach OrbitCamera exactly once.
  - Run **one** such test per `test run`: a second test in the same run inherits the first test's scene and its
    OrbitCamera throws "EnhancedTouch API is not enabled" after the fixture teardown.
  - IMGUI (OnGUI) buttons can't be clicked from automation in this project (Input System only): neither
    `GameView.SendEvent` (with `gameMouseOffset/gameMouseScale` mapping) nor `EditorGUIUtility.QueueGameViewInputEvent`
    reach the player's OnGUI, in play mode or under the test fixture. Survey/debug buttons stay NOT CHECKED via input.
  - `ScreenCapture.CaptureScreenshot` from a test coroutine sometimes leaves a cyan half-frame (capture artifact).
- **2026-10-29 · CI: "all actions must be pinned to a full-length commit SHA"** (job fails in 3 s at Set up job).
  The repo's Actions setting now requires SHA pins. Resolve a tag with `gh api repos/<owner>/<action>/commits/<tag> --jq .sha`
  and write `uses: owner/action@<sha> # vN`. When bumping an action, re-resolve the SHA. [verified]
- **2026-10-29 · Tester touch playtest: Input System "Cached unprocessed value unexpectedly became outdated" error fails
  the test** (logged from `InputSystem.Update()` in the manual pump). It is harness noise from the test fixture, not
  game code. Set `LogAssert.ignoreFailingMessages = true` at test start and collect real warnings/errors yourself via
  `Application.logMessageReceived` (filter that message). `console clear` afterwards so the main session doesn't
  mistake them for game errors. Also back up `%USERPROFILE%/AppData/LocalLow/Tommaso Ribaudo/Aquarium Civ/proto/P1.json`
  and the `logs/*.jsonl` before a playtest and restore them after: the test runs the real save and event log. [verified]
- **2026-10-29 · `--project-path F:\GameDev\AquariumCiv` from Bash mangles the path** (backslashes eaten:
  "F:\GameDev\GameDevAquariumCiv"; `unity status` then reports no instances). Use forward slashes:
  `--project-path F:/GameDev/AquariumCiv`, or run from the project dir without the flag. [verified]
- **2026-10-29 · Domain reload right after entering play mode nulls every `Instance`** (NREs in SimClock, GlassGrime,
  Wanderer; banners never show). Unity saw "changes in script compilation related files" and recompiled during play.
  Fix: `EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1)` (Recompile After Finished Playing). Set on this machine.
  Also `capture game_view --save_path` always lands under `Assets/` (even `Temp/agent/x.png` → `Assets/Temp/agent/`),
  but it's the only capture that includes IMGUI. Copy the file out and delete `Assets/Temp` **after** leaving play mode. [verified]
- **2026-10-29 · Tester touch fixture compile gotchas.** `EndTouch(id, pos, default, queueEventOnly: true, ...)` is
  ambiguous (two overloads); pass `delta: Vector2.zero`. `TestMode` clashes with `UnityEngine.TestTools.TestMode`:
  alias `using TestMode = AquariumCiv.Prototypes.TestMode;`. Load the scene from the test with
  `EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single))` under
  `#if UNITY_EDITOR`. Private HUD state (banner, toasts) can be read by reflection for evidence. [verified]
- **2026-10-29 · Tester P2 re-verify notes.** [verified]
  - A wipe stroke across the middle of the screen goes through the tank's **open top**: it starts a drag (world hit)
    and may flick junk, but never touches the glass. Aim strokes at the front panes: project
    `GlassGrime.Instance.Panes[i].renderer.bounds.center` with `Camera.main.WorldToScreenPoint` and take the two with
    the lowest screen y.
  - Economy A/B checks don't need play mode: build `new SimCore(cfg, SimCore.NewWorld(seed, cfg))` from a JSON clone
    of `SimClock.config` in `script eval` and `Advance(h)`; it reproduces the scene's away-path numbers exactly.
  - Hold gestures (`Pointer.current.press`) work from the touch fixture: `BeginTouch` once, yield frames, `EndTouch`.
  - After the first PlayMode run, `Assets/Art/Materials/Glass.mat` showed `_SrcBlend: 5 -> 1` in git (not from test
    code; probably URP re-deriving blend state). Report it, don't revert.
- **2026-10-29 · Tester run 3 notes.** [verified]
  - The first `unity recompile` right after a PlayMode `test run` often fails with "Pipeline server returned 400 Bad
    Request" (or "No Pipeline instance found" during the reload). Just run it again; the second one compiles.
  - Touch-fixture strokes that start **below the tank base** (no glass, no world hit) are camera turns by design and
    change the pitch for every later stroke in that test. Save `OrbitCamera` `yaw`/`pitch` (private fields) at the
    start and restore them after each stroke when measuring something else.
  - Junk usually sits inside the tank, seen through the **open top**, so `GlassGrime.Raycast` at a junk point often
    hits the *back* pane. Don't read "start on glass" as "start on the front glass".
  - To see which way a drag went, wrap `OrbitCamera.DragTool` in a spy `IDragTool` that forwards to `WipeCleaner`
    and logs what `BeginDrag` returns. The input path stays real.
- **2026-10-28 · `script eval_file` takes `--file <abs .cs path>`** (not `--path`); there is no top-level
  `unity eval`, use `unity command script eval --code '...'`. Junk spawned by `MessView.MakeMessy()` isn't in
  `TapToClean.Active` until a later frame, so make it messy in one eval and inspect it in the next. [verified]
- **2026-10-28 · Linting workflows locally (Docker from Git Bash).** `export MSYS_NO_PATHCONV=1`, mount with a Windows
  path: `docker run --rm -v "F:/GameDev:/repo" -w /repo rhysd/actionlint:1.7.12` and
  `docker run --rm -v "F:/GameDev:/repo" -w /repo -e GH_TOKEN=$(gh auth token) ghcr.io/zizmorcore/zizmor:latest .`.
  Without the env var, `-w /repo` becomes `C:/Program Files/Git/repo`. actionlint caught `${{ runner.temp }}` in a
  job-level `env:` (not allowed there; set it from a step via `$GITHUB_ENV`). [verified]
- **2026-10-29 · Tester P3 run notes.** [verified]
  - Several Game view sizes in **one** PlayMode test: call the `GameViewSizes` reflection (as in `AgentGameView.SetSize`)
    from the test under `#if UNITY_EDITOR`, then wait until `Screen.width/height` match (a few frames). IMGUI and
    `ProtoGui.Scale` follow. One run can then screenshot 1080x1920, 1080x2400 and 1640x2360 of the same state.
  - Wipe strokes: take the front pane (`GlassGrime.Panes` with the lowest projected `bounds.center.y`) and stroke about
    ±90 px around its centre. ±140 px runs off the pane, and a stroke that *starts* above the tank rim is a camera turn
    by design (it looked like "wipe rotates the camera" in the first run).
  - A tap within `WipeCleaner.pickRadius` of any junk goes to the junk (`TapClaim`) before the world tap. When a tap
    test on a person fails in a messy tank, check the nearest `TapToClean.Active` distance before blaming the harness.
  - A PlayMode `test run` can write pending in-memory ProjectSettings to disk (`EditorBuildSettings.asset` showed up
    modified right after one). Report it, don't revert it.
- **2026-10-29 · Tester touch fixture: taps only land with `queueEventOnly: true`.** With the default (`false`),
  `BeginTouch`/`EndTouch` update the Input System outside the player loop and `ETouch.activeTouches` never shows the
  touch in `OrbitCamera.Update` (every tap silently does nothing: no `tap` log line, no toast). Pass
  `queueEventOnly: true` to `BeginTouch`/`MoveTouch`/`EndTouch` and yield frames between them. Add the `Touchscreen`
  *after* the scene has loaded. The fixture logs "Cached unprocessed value unexpectedly became outdated" errors from
  `EnhancedTouchSupport.Enable` (fixture noise, not game code): set `LogAssert.ignoreFailingMessages = true` and
  collect console errors yourself. Run one `[UnityTest]` per `test run`: in a two-test run the second fails at once
  ("EnhancedTouch API is not enabled" from the first test's still-loaded `OrbitCamera`). [verified]
- **2026-10-29 · Tester Test Hub run notes.** [verified]
  - Scene-flow checks don't need a test fixture: `unity command editor.playmode set_run_in_background` once, then
    `editor.playmode start` (a cold launch: statics reset), drive with `script eval` (`LabLaunch.Open`,
    `HubLink.GoToHub`, `TestHub.OpenDiary` by reflection for the diary buttons) and `AgentGameView.Shot` for IMGUI.
    The Editor in the background needs ~20 s before the first scene's `Start` (frame count stays 1 until then).
  - `BeginTouch/MoveTouch/EndTouch` are `InputTestFixture` methods, not `Touchscreen` methods (`ts.BeginTouch` = CS1061).
  - Compile errors from a deleted `_AgentPlaytest` file stay in `console get` until `console clear`.
- **2026-10-29 · Tester P4 run notes.** [verified]
  - A touch-fixture test can open any lab scene the way the hub does: `EditorSceneManager.LoadSceneAsyncInPlayMode` the
    Hub scene (with `hub_autoopen`=0), wait ~30 frames, `LabLaunch.Open(id, variant, TestMode.Lab)`, then poll the
    protected `PrototypeBase.started` (ready in under 1 s). Add the `Touchscreen` after that.
  - Input-System-driven panels that map `Pointer.position` to IMGUI canvas (P4 timeline, book swipe) take fixture
    touches fine: convert canvas → screen with `(x * ProtoGui.Scale, Screen.height - y * ProtoGui.Scale)` and read
    the panel's private rect (e.g. `TimelapseView.barCanvas`) after one OnGUI frame. Keep touch start points on screen:
    a start at negative x misses `GuiBlocker` and turns the camera.
- **2026-10-29 · Tester P5 run notes.** [verified]
  - Banners that get replaced within a frame never show in screenshots. Poll `HudToast`'s private static `instance`
    and its `banner`/`bannerUntil`/`toasts` fields every frame from the touch-fixture test and log each change.
  - A ~4-minute lab watch fits in one `[UnityTest]` with `[Timeout(1200000)]` (the default timeout is too short).
  - `Renderer` bounds `Aggregate` throws on a prop with no renderers (the P5 rain nudge root has none): guard it, or
    the test fails mid-run.
- **2026-10-29 · Tester P6 run notes.** [verified]
  - Changing the Game view size mid-test resets `OrbitCamera`'s distance through `FitAspect`, so any `SetWide` pull-back
    is lost (P6 annex/shelf framing). To judge a wide view at several sizes, set the size **before** loading the scene and
    reach the state again (one `[UnityTest]` can load Hub → `LabLaunch.Open` several times in a loop).
  - Float interpolation in harness strings follows the machine locale (`1,5` not `1.5`): screenshot names and log
    numbers come out with commas. Use `CultureInfo.InvariantCulture` when it matters.
  - Overlapping IMGUI buttons: the one drawn **first** gets the MouseDown (and the one drawn last shows on top). A
    screenshot where panel X covers panel Y means Y's buttons take the taps.
- **2026-10-29 · Overnight P3–P6 notes (main session).** [verified]
  - Python/bash heredocs with apostrophes in C# strings ("Keeper's") fail with "unexpected EOF": write the edit script
    with the Write tool and run the file (already in §7; it bit three times).
  - The Editor's "Hold on... Importing assets" window is a progress bar, not a question: `dismiss-dialogs.ps1` lists it as
    UNKNOWN. Poll `unity status` until `ready` (an until-loop, not chained sleeps).
  - `unity recompile` right after a large edit often returns "No Pipeline instance" or 400 during the domain reload;
    wait for `ready` and run it once more. An eval sent during the reload is silently lost (check its result).
  - Another session can share this working tree and Editor: check `git status` for files you didn't write before every
    commit, stage paths explicitly (not `git add -A`), and agree on Editor time by message.
  - `AquariumCiv.EditorTools.AgentGameView.SetSize(w,h)` / `.Shot(name)` (Assets/Editor/Agent) set a portrait Game view
    and grab a play-mode screenshot including IMGUI; `Temp/agent/ready.cs` prints `PrototypeBase.started`.

- **2026-10-29 · Tester sound checks (no ears).** [verified]
  - A live `OnAudioFilterRead` synth can be verified by reflection: its smoothed fields only move on the audio thread,
    so a value rising during a fixture drag proves the filter runs in Editor play mode. One-shots played with
    `PlayOneShot` show up in `AudioSource.isPlaying`; to count plays, read the pool's round-robin index before/after.
  - `Application.logMessageReceivedThreaded` (not `logMessageReceived`) also catches audio-thread exceptions.
  - P2 lab mode writes the **diary** save `proto/P2.json` (P2 has no separate lab save): back it up before a P2 playtest.
  - A raster of wipe strokes inset 12% from the front pane's projected rect leaves its mean at ~0.05, just above the
    `PaneCleared` threshold (< 0.05). To test the clear chime, set a dirty central patch on a clean pane and wipe that.
- **2026-10-29 · `test run --mode play` silently runs 0 tests** ("0/0 passed (0s)", exit 0). The Pipeline's modes are
  `editor`, `playmode`, `all`; anything else matches nothing. Use `--mode playmode` (and `--async_tests true` + `test
  status` for anything long). `test list --mode play` still lists the tests, so a listed test is no proof it ran. [verified] Same for `--mode edit` (use `editor`).
- **2026-10-29 · Blender 4.5 LTS and RenderDoc 1.46 installed** (user-approved; `winget install --id
  BlenderFoundation.Blender.LTS.4.5` / `BaldurKarlsson.RenderDoc`, each needs one UAC click). Blender runs headless:
  `"/c/Program Files/Blender Foundation/Blender 4.5/blender.exe" --background --factory-startup --python script.py -- <args>`
  (script args after `--`, read via `sys.argv`); FBX export via `bpy.ops.export_scene.fbx` verified. RenderDoc:
  `C:\Program Files\RenderDoc\qrenderdoc.exe` (UI) and `renderdoccmd.exe` (CLI capture) for Windows frame captures;
  there is no Android device and no Mac, so iPhone GPU captures still need the in-app FrameTimingManager log. [verified]
- **2026-10-29 · `unity status --timeout 30` fails** with `STATUS_TIMEOUT_REQUIRES_UNTIL_READY`: `--timeout` only goes
  with `--until-ready`. Plain `unity status --format json` is instant. After `unity recompile`, poll it until `"state":
  "ready"` (it reads `unreachable`/`starting` for ~10–20 s). [verified]
- **2026-10-29 · The first `script eval` after a recompile can return 504 (30 s timeout) yet still run.** A scene-builder
  eval (`NewScene` + `SaveScene` + `AssetDatabase.Refresh`) timed out but the scene was written. Check the effect (file
  on disk, active scene) before re-running; give builder evals `--timeout 60`. [verified]
- **2026-10-29 · Slice play-mode checks:** the `GameClock` summary line is in `Editor.log` (`grep -a "\[GameClock\]"`)
  even when `console get` right after `editor.playmode start` shows 0 entries. The slice saves to
  `LocalLow/Tommaso Ribaudo/Aquarium Civ/game/` and never touches `proto/` or `logs/`. [verified]
- **2026-10-29 · Fast headless loop for pure-C# sim code (no Editor, no domain reload).** [verified] Unity ships a
  Roslyn compiler and a .NET 6 runtime: compile `Assets/Game/Sim/*.cs` + spike logic + a `Main.cs` with
  `"<Editor>/Data/NetCoreRuntime/dotnet.exe" "<Editor>/Data/DotNetSdkRoslyn/csc.dll" -noconfig @build.rsp`, referencing
  every `NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21/System*.dll` (+ `netstandard.dll`, `mscorlib.dll`; skip
  `*Native*`, keep `System.Private.CoreLib.dll`), and write a `<out>.runtimeconfig.json` for net6.0. The paths have spaces:
  put every `-r:"..."` and source path in the `.rsp` file, quoted (on the command line they split). Set
  `CultureInfo.InvariantCulture` in `Main` (this machine prints `0,5`). Gotcha: CoreCLR and the Editor's Mono differ in
  float results and in speed (Mono was 3–4× slower on Dijkstra-heavy code), so tune in the harness but measure the
  budget in the Editor; same-runtime runs are bit-identical.
- **2026-10-29 · Python edits of C# files: always `encoding='utf-8'`.** Python's default on this machine is cp1252: a
  write with a non-cp1252 character (→, ≥) raises *after* `open(..., 'w')` truncated the file, leaving it empty.
  Write to a temp file and `os.replace` it. [verified]
- **2026-10-29 · Edit-mode renders to JPG with URP post-processing:** `cam.targetTexture = RenderTexture.GetTemporary(w,
  h, 24, ARGB32, sRGB)`, `cam.Render()`, `ReadPixels`, `EncodeToJPG` (`GrowthLabBuilder.Render`). Tonemapping, grade and
  SMAA are applied; no play mode needed. Long frame exports: chunk them into `script eval` calls under 60 s
  (`GrowthLabBuilder.Frames(from, to)`), about 1 s per frame with the AO bake. [verified]
- **2026-10-29 · The Game view size lands a few frames into play mode.** After `AgentGameView.SetSize(1179, 2556)` and
  `editor.playmode start`, `Start()` still saw the old 1198x702 Game view, so a camera fitted once in `Start` was framed
  for landscape. Refit when `cam.aspect` changes (PerfCheck does). Also, ProfilerRecorder render counters
  ("Batches Count", ...) read double in the Editor with the Scene view open, and 0 on some frames. Trust the device numbers. [verified]
- **2026-10-29 · Unity (Mono) and Tools/SimTests (.NET 8) don't give bit-identical floats for the sim.** The slice
  fingerprint (ints, rng states, ages to 2 decimals) matches in both, but hashes over raw float fields (food, wood, mood…)
  differ, with "R" text or with raw bits. So: never pin a float-derived hash in a test that runs in both; pin under
  `#if !UNITY_2017_1_OR_NEWER` (dotnet only) and compare same-runtime runs in Unity. Unity stays the authority. [verified]
- **2026-10-30 · "The referenced script (Unknown) on this Behaviour is missing!" on a component the builder just
  added.** The MonoBehaviour (`NightLampDriver`) lived in a file named after another class (`NightLamps.cs`), so Unity
  gave it no script asset and the scene saved `m_Script: {fileID: N}` with no guid. Every MonoBehaviour needs its own
  file named like the class. Check after a scene build: `grep -n "m_Script: {fileID: [0-9]*}$" <scene>.unity` must be
  empty. [verified]
- **2026-10-30 · Phone-size renders of the slice:** `AquariumCiv.EditorTools.SliceSceneBuilder.Shot(path, w, h, zoom,
  target, yaw, pitch)` renders the main camera (URP post included) to PNG in edit or play mode, with the `BoxCamera`
  refitted to that aspect, and restores the rig. [verified]
- **2026-10-29 · Tester: Slice look checks (#31).** [verified]
  - `SliceSceneBuilder.Shot(path, w, h, zoom, target, yaw, pitch)` renders any size/angle in edit mode without dirtying
    the scene; villagers are `HideFlags.DontSave`, so find them with `Resources.FindObjectsOfTypeAll<PegDollView>()`
    (`FindObjectsByType` returns 0) and aim close-ups at their positions.
  - `BoxCamera` reads `Touchscreen.current`/`Pointer.current` directly, so an `InputTestFixture` with the default update
    mode and `queueEventOnly: true` touches drives it (no manual pump or EnhancedTouch needed).
  - Lamp flicker is per-frame shader noise: sample pixel luminance from `ScreenCapture.CaptureScreenshotAsTexture()`
    after `WaitForEndOfFrame` across frames at fixed `NightLamps.DebugSetLevel` values instead of diffing whole
    screenshots (wind sway changes ~100k pixels per frame).
- **2026-10-29 · Bash tool: a long `python - <<'EOF'` heredoc full of apostrophes failed to parse** ("unexpected EOF
  while looking for matching `''") although the delimiter was quoted. Workaround: write the edit spec or script with the
  Write tool to the scratchpad and run it (`python script.py spec`). [workaround]
- **2026-10-30 · The Editor compiles scripts in Debug mode (`CompilationPipeline.codeOptimization` = Debug), so managed
  per-vertex loops are several times slower than in a player.** Measure Editor budgets with that in mind. What made the
  slice box's chunk rebuild fit (#33): AO with `RaycastCommand.ScheduleBatch` plus plain (non-Burst) `IJobParallelFor`
  jobs that build the commands and sum the hits (the rays were only ~1/4 of the cost; the managed loops around them were
  the rest), packed `long` dictionary keys instead of tuple keys, `List.ToArray()` before hot loops, distance fields on
  a 0.025 u grid instead of walking polylines per triangle, and `Parallel.For` over terrain row bands.
  `Color.linear` is a native call: `MeshBuilder.Linear` does the same in managed code so builders run on worker threads;
  `Quaternion.FromToRotation` worked inside jobs. [verified]
- **2026-10-30 · Profiling a private method from `script eval`:** call it by reflection
  (`typeof(T).GetMethod("Name", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args)`), with a `Stopwatch`
  around each stage; out params come back in the `args` array. Build the result string with `+`, not `$"...\n"`, when
  the script is passed through `"$(cat file)"` (an escaped `\n` inside an interpolated string got mangled). [verified]
- **2026-10-30 · `VolumeProfile` built in code must add its components as sub-assets (#63).** `Post_Slice.asset` was
  committed with `components: [{fileID: 0} x5]`: `p.Add<T>(true)` creates the overrides in memory only, and they survive a
  domain reload but not an Editor restart or a player build, so a fresh checkout ran the slice with no tonemap, grade,
  vignette or bloom. `AssetDatabase.AddObjectToAsset(c, profile)` for each component before `SaveAssets` fixes it
  (`SliceSceneBuilder.Profile`). Check with `grep -c "" Post_Slice.asset` (about 190 lines, not 20). [verified]
- **2026-10-30 · After a mesh-builder or shader change, `BoxView.Rebuild()` before shots.** `SimLayoutSource.DebugPreview`
  reports "no chunk changed" when the layout hash is the same, so the old piece meshes stay. Call
  `UnityEngine.Object.FindAnyObjectByType<AquariumCiv.View.BoxView>().Rebuild()` first. [verified]
- **2026-10-30 · `script eval` takes no `using` lines** (top-level `using X;` is parsed as a using-statement and fails with
  "Identifier expected"): write fully qualified names. A sed pass over a script file does it:
  `sed -E 's/(^|[^A-Za-z0-9_.])(Vector3|Color)\b/\1UnityEngine.\2/g'`. This machine has no `python` (the Store stub) but
  has `perl`, which handles multi-line edits (`perl -0pi -e ...`) when a heredoc has no apostrophes. [verified]
- **2026-10-30 · `unity command test run` prints only the totals; failures are in `TestResults.xml`.** Read
  `%USERPROFILE%/AppData/LocalLow/Tommaso Ribaudo/Aquarium Civ/TestResults.xml` and grep `result="Failed"` with `-A12`
  (use `grep -a`). [verified]
- **2026-10-29 · Tester #63 camera-by-touch notes.** [verified]
  - `BoxCamera` takes `InputTestFixture` touches with the default update mode: `BeginTouch/MoveTouch/EndTouch(...,
    queueEventOnly: true)`, one step per frame, two ids for pinch/pan. The fixture leaves no `Mouse`, so `Touchscreen.current`
    is the only pointer. Several Game view sizes in one test: call `AgentGameView.SetSize` by reflection
    (`AppDomain.GetAssemblies()` → `AquariumCiv.EditorTools.AgentGameView`) and wait ~1 s; the yaw/pitch deltas then match.
  - Measure "ground under the fingers" with `BoxCameraMath.GroundPoint(cam.ScreenPointToRay(p), rig.target.y, ...)`.
  - The slice play mode rewrites `LocalLow/.../game/box.json`: copy it out before and back after.
  - Fire halo edge check: scan rows of a night close-up for a strong horizontal luminance step (PIL); the eye misses it in day shots.
- **2026-10-29 · Tester sim playtest without a UI (#24-#26 jobs/counts).** [verified]
  - Drive the live Slice box with `unity command script run --file Temp/agent/tester/X.cs --entry Type.Method --args '[...]'`
    (static methods using `GameClock.Instance.Sim`, `SimLayoutSource.DebugSkipYears/DebugNextEra`, `GameClock.DebugAway`); add
    `--result-only` and parse `.result` (use `python`, not `python3`, in Git Bash here). Offline audits: build
    `new SimCore(JsonUtility clone of GameClock.Instance.config, SimCore.NewWorld(seed, cfg))` for many seeds in one call.
  - IMGUI (the "dbg" panel) only shows in `UnityEngine.ScreenCapture.CaptureScreenshot`; `capture screenshot --view game`
    sometimes returns a tiny (299x648) image right after a Game view size change: shoot again.
  - A synchronous `test run --mode editor` can hang the Pipeline (every other command 504s "waiting for another command")
    when the Editor is unexpectedly in play mode ("This cannot be used during play mode" in RestoreSceneSetupTask, result
    0/0). Wait for it to end, make sure play mode is off, rerun with `--async_tests true` (234 tests, ~2.5 min).
  - Back up `%USERPROFILE%/AppData/LocalLow/Tommaso Ribaudo/Aquarium Civ/game/box.json(.bak)` before a live Slice run; delete
    them for a fresh camp, restore afterwards.

- **2026-10-29 · UI Toolkit panels are missing from Game-view screenshots** (like IMGUI). Capture with
  `UnityEngine.ScreenCapture.CaptureScreenshot(path)` from `unity command script run --file Temp/agent/x.cs --entry T.Run`
  (file path is relative to `AquariumCiv/`, then wait a second and read the PNG). The feedback sheet has to hide its own HUD for a frame before it
  captures, for the same reason. [verified]

- **2026-10-29 · UI Toolkit in `script eval` / HUD checks (#36).** [verified]
  - `script eval` doesn't pick up the `UQueryExtensions` extension methods: `root.Q("name")` fails to compile with
    "'VisualElement' does not contain a definition for 'Q'". Call it statically:
    `UnityEngine.UIElements.UQueryExtensions.Q(root, "name")`.
  - Open/close HUD sheets from eval with `GameHud.Instance.Open("settings"|"history"|"shaping")` / `CloseSheet()`, then
    `AgentGameView.Shot(name)`: the shot shows UI Toolkit (Game-view `capture screenshot` doesn't).
  - USS edits hot-reload in play mode after `AssetDatabase.Refresh()`; check with `resolvedStyle` before trusting a shot.
  - The Editor's Game view has no notch: `Screen.safeArea` is the full screen there, so safe-area handling is covered by
    `SafeInsetsTests` (EditMode) and needs a device or the Device Simulator to see.
- **2026-10-29 · Compile-check UnityEngine code (View, its tests) without the Editor.** [verified] Same csc as the sim
  loop above, but `-target:library` against `Editor/Data/NetStandard/ref/2.1.0/netstandard.dll`, every
  `Editor/Data/Managed/UnityEngine/*.dll`, and the project's own and package assemblies from
  `AquariumCiv/Library/ScriptAssemblies/` (`AquariumCiv.Sim/Game/Life`, `Unity.InputSystem`, `Unity.RenderPipelines.*.Runtime`,
  `Unity.Collections`, `Unity.Mathematics`; built by whichever branch the Editor last compiled, so only for assemblies you
  didn't change). Define `UNITY_EDITOR` for `#if` blocks. Tests also need `UnityEngine.TestRunner.dll`, the nunit from
  `Library/PackageCache/com.unity.ext.nunit*/net40/unity-custom/`, and `NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll`
  (else every `[Test]` fails with CS0012 "Attribute ... mscorlib"). Paths with spaces go quoted in an `.rsp` file
  (Git Bash `/c/...` paths don't work inside it: use `C:/...`). Catches compile errors in seconds while another agent
  holds the Editor; it can't run the tests (no engine). Unity's compile is still the authority.
- **2026-10-29 · "Scene(s) modified externally" after a merge/checkout.** `dismiss-dialogs.ps1 -Click` leaves it as KNOWN but
  doesn't answer it. Copy the script, add `@{ Match = 'modified externally'; Button = 'Reload' }` to `$safe`, run the copy
  with `-Click`. After the reload an "Importing assets" progress window follows: poll `unity status` until ready. [verified]
- **2026-10-30 · Tester slice batch (#30/#38/#40) notes.** [verified]
  - A PlayMode fixture test that loads `Slice.unity` with `LoadSceneAsyncInPlayMode` gets no `GameHud` (its
    `AfterSceneLoad` bootstrap has already run): call `GameHud.Create()` in the test after the scene is up.
  - `script run --entry Drive.X --result-only` returns JSON: pipe it through `jq -r .result` (jq is installed via WinGet).
    `script eval --format json` puts the value at `.data.result.result`.
  - Doll motion audit without screenshots: a throwaway MonoBehaviour reading `PeopleView.dolls` (private dict, `Doll.pos`
    / `view` / `act`) by reflection every `LateUpdate` finds teleports (> 0.12 u in one frame), walk speeds and heaps.
- **2026-10-30 · Panel-fix notes (lane B).** [verified]
  - `unity command script run --args` wants a JSON array (`'["a1","x"]'`); string/int params are converted. A shell helper that builds it with `jq -R . | jq -sc .` works.
  - IMGUI debug panels now share `DebugDock` (View): one "dbg" toggle top left, tabs dbg/sim, rects in `UiInput.DebugBlocks` (camera ignores presses there, `GameHud` puts picking blockers `hud-debug-block0/1` over them), hidden while a HUD/Feedback sheet is open. Check blocking with `root.panel.Pick(...)` in `script eval` (hit name `hud-debug-block1`).
  - Scenes had no AudioListener (music silent, "no audio listeners" spam); added to every game/prototype scene's Main Camera and to `SliceSceneBuilder`.
- **2026-10-30 · Care/wipe play tests (#37).** [verified] `unity command test run --mode playmode --filter <Assembly>
  --filter_type assembly --async_tests true`, then `test status` (the `name` filter ran 0 tests). The start-up
  `RuntimeInitializeOnLoadMethod` bootstraps (HUD, CareView) only run for the first scene, so a play test that loads the
  Slice with `EditorSceneManager.LoadSceneAsyncInPlayMode` adds them by hand. A press over the open top of the box is not
  on glass: aim touch strokes with `CareView.GlassAt(screenPos)`. The first `unity recompile` often answers 400; retry.
  The play test backs up and restores `LocalLow/.../game/box.json` (and `.bak`, which the game falls back to).
- **2026-10-30 · Live town shots in play mode overwrite the dev save (#34).** `SimLayoutSource.DebugSkipYears` saves, so
  copy `%USERPROFILE%/AppData/LocalLow/Tommaso Ribaudo/Aquarium Civ/game/box.json` aside before entering play mode and
  copy it back after `editor.playmode stop`. After a long skip the glass is full of grime: `Sim.CleanAll()` before shots.
  There is no night lighting yet: `PeopleView.DebugSetHour(22)` leaves `NightLamps.Level` at 0, so night shots are
  `NightLamps.DebugSetLevel(1)` over the day light. Kit sheets of every town piece in edit mode:
  `AquariumCiv.EditorTools.TownKitShots.Sheet()` then `SliceSceneBuilder.Shot(...)`, `TownKitShots.Restore()`. [verified]
- **2026-10-30 · One PlayMode test class alone (#88).** Put `[Category("X")]` on the class and run
  `unity command test run --mode playmode --filter X --filter_type category --async_tests true`, then poll `test status
  --format json` until `"status": "completed"`. Runs only that class (the assembly filter would also run the other
  InputTestFixture classes). `scene save` before the run writes a noise-only `lastBuildInfo` change into Slice.unity:
  leave it out of the commit. [verified]
- **2026-10-30 · `dismiss-dialogs.ps1 -Click` now answers "The open scene(s) have been modified externally" with Reload
  (#89).** Before, the "have been modified" rule matched it too (case-insensitive) and looked for a Cancel button that
  this dialog doesn't have, so it stayed KNOWN and blocked recompiles. After a checkout/merge, disk holds what git holds.
  [verified]
- **2026-10-30 · Downloading Kenney packs from the shell (#96).** The site's Download button calls
  `https://kenney.nl/download-link/<hex of "assets/<slug>">` (JSON `{"url": ...}`, a signed short-lived link). From bash:
  `id=$(printf "assets/interface-sounds" | xxd -p | tr -d '\n')`; GET the asset page once with a cookie jar
  (`-c ck.txt`, a browser User-Agent, `-e <page>` referer); GET the download-link with the same cookies; strip the `\/`
  escapes (`tr -d '\\'`); GET the url → the zip. Extract into a fresh temp dir outside the repo, copy only the clips used.
  OpenGameArt files are plain links (`/sites/default/files/...`); check the page's `license-name` says CC0. ffmpeg (winget
  Gyan.FFmpeg, on PATH) converts to mono .ogg and makes crossfade loops. [verified]

- **2026-10-30 · Shelf views pull the camera past the far plane; PlayMode filters; no tests run from `unity test` with the Editor open.** [verified, #93]
  - `BoxCamera` sits `FitDistance() * zoom` away (about 50 units at zoom 1). The Slice camera's far plane is 120, so a
    zoom above about 2.3 shows only the backdrop vignette. `BoxCompleteController.Start` raises the far plane to 420.
  - `unity command test run --mode PlayMode --filter BoxComplete` ran 0 tests; the fully qualified class name
    (`--filter AquariumCiv.View.PlayTests.BoxCompletePlayTests --async_tests true`) ran them. Check `test list --mode PlayMode`.
  - A new `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` component is not created when a test loads the scene with
    `LoadSceneAsyncInPlayMode` (the bootstrap ran earlier): tests call `X.Create()` when `Instance` is null.
  - The camera looks from -z (front of the box = -z). Anything meant to face the player (plate, plinths) goes at -z.
  - A "finished" state that stays set on a pure-C# director (`Phase.Finished`) re-fires a per-frame `Finish()`: guard with a flag.
- **2026-10-30 · Tester batch 2 notes (slice moments, box complete).** [verified]
  - One throwaway `InputTestFixture` class per `[Category]` (one test each) runs alone with `test run --mode playmode
    --filter <Cat> --filter_type category --async_tests true`; four classes can share an abstract harness base class.
  - Kill-like interruption in play mode: invoke every MonoBehaviour's `OnApplicationPause(true)` by reflection (GameClock
    saves), then `LoadSceneAsyncInPlayMode` the Slice again and re-add the bootstraps (GameHud, MomentPlayer,
    BoxCompleteController, CareView, GlassVoice, TimeLapseView.Ensure). The relaunch reads the saved box.json.
  - A UI audit that walks every `UIDocument` root (shown Buttons/Labels → screen rects, overlaps) logged per screenshot
    catches stuck bubbles and overlapping banners that the eye misses on a contact sheet.
  - Contact sheets: `ffmpeg -i a.png -i b.png ... -filter_complex "[0]scale=440:-1[s0];...;[s0][s1]...hstack=inputs=N"`.
  - The debug "belief stage" cheat can't go below the stage the found relics give (`DebugSetBelief(0..1)` reads back 2
    after two relics): test early-stage knocks on a fresh box.
  - Restoring the dev save: `rm -rf "$G"/*` is blocked by the safety check; copy the backup back with literal paths and
    delete only the stills your run added.
- **2026-10-30 · Tester batch 2 fixes (lane B).** [verified]
  - `unity command test run --filter "A;B" --filter_type assembly` ran **0 tests**: one assembly per run (a small loop
    that waits for `ready`, starts `--async_tests true`, polls `test status` until `completed`).
  - `unity status --project-path F:\GameDev\AquariumCiv` from Git Bash said "no instances" (the backslashes); run it
    from `F:/GameDev/AquariumCiv` without `--project-path` instead.
  - Prove a regression test catches the bug: disable the fix with a marked `if (false && ...)` line, recompile, run the
    test by name (`--filter <TestName>`, no filter_type), expect red, restore. Caught that an override-only fix passed
    a test whose real trigger was the view day turning at midnight.
  - `PeopleView.DebugSetHour` calls `Tick(0)`, and `dt <= 0` snaps every doll to its plan: never use it from game code
    mid-play (moments used it for their light). `PeopleView.SetHourGently` moves the clock and lets the dolls walk.
- **2026-10-30 · Waiting on async test runs from Bash (#39).** A foreground `sleep N; check` is blocked by the harness
  and a long poll loop gets moved to the background after 180 s. Run the poll loop with `run_in_background` (or wait
  for it with `until grep -q "== Done" <output file>; do sleep 5; done` in a background Bash) and read the output file
  when the notification lands. UI Toolkit tip from the same work: a header row inside a `max-height` sheet shrinks and
  overlaps unless it has `flexShrink = 0`, and a label column next to buttons only wraps with `flexBasis = 0` +
  `minWidth = 0`. [verified]
- **2026-10-30 · `unity test` (top-level) refuses while the Editor is open; use `unity command test run --mode playmode --filter <name> --async_tests true`, then poll `unity command test status`.** A bare `unity command test run` (no filter) ran all 811 EditMode tests and blocked every other command for minutes (the client timeout does not cancel it; `test cancel` itself waits). Always pass `--filter`. `ShapingView.GroundPoint` marched only 30 units: the default camera is ~50 away, so no ground tap worked at the default framing until the step was doubled (#94). [verified]
- **2026-10-30 · ShapingPlay flaked on "the people's answer is staged in the world"; pin a play test's world with
  `GameClock.DebugNextSeed`.** [verified] About 1 run in 3 failed (`e.moment` null, a different tool each run, before and
  after #96). `LifePlanner.StartShapingAnswer` gave null when nobody could walk to the gift in half the come-over beat: in
  a 3-year camp the 3-5 people are often all in the woods or at the fire, 7-13 s of walking from a gift on open ground
  (about 1 gift in 15 by day, worse near midday because the beats are spread evenly in view hours and the slow midday hour
  squeezes the come-over). Fixed in Life: by day the answer stretches (50-90 s, the late way) until someone comes over at
  the walking pace (`LifeShapingAnswerTests.ByDaySomeoneAlwaysComesToAFarGift`). After ~20:00 nobody is up and nothing is
  staged (by design for now). Found with a dotnet probe in `Tools/SimTests` (a probe `.cs` in a `<Compile Include>`,
  40 seeds × view hours, private planner state by reflection; deleted after): seconds instead of 5-minute play runs.
  `GameClock.DebugNextSeed = <seed>` before loading the Slice makes the next new world use that seed once (reset it in the
  test's Close); ShapingPlay pins 1234. Don't move the view clock back mid-test with `PeopleView.SetHourGently`: dolls glide
  up to 0.26 u in a frame and trip the doll-jump audit. MomentPlay/BoxComplete passed every run here.
- **2026-10-30 · Play tests' taps on UI buttons fail at random when the Editor isn't the foreground window.** [verified]
  "a tap on Watch starts the moment", "a tap on Finish", "the Clean button switches clean mode on", CameraMode's pill:
  all passed with Unity in front and failed while another app (a browser) was in front. UI Toolkit's
  `DefaultEventSystem` drops pointer events on Windows/macOS while `!Application.isFocused` unless
  `DefaultEventSystem.IsEditorRemoteConnected()` says Unity Remote is attached (read from its IL by reflection in
  `script eval`). The Input System side was already handled (`backgroundBehavior = IgnoreFocus`). Fix: every game play
  test calls `GameUiPanel.AcceptInputUnfocused(true)` in its Open (sets that internal `Func<bool>` to true, Editor only;
  play mode's domain reload resets it). Also: an edit made from a script was not picked up by `unity recompile` once
  (the dll in `Library/ScriptAssemblies` kept the old test); check with `script eval` reflection on the type and
  `ImportAsset(path, ForceUpdate)` + recompile when it's stale.
- **2026-10-30 · Checking sound without ears (#96).** In the Editor `AudioDirector.LogPlays` is on: every cue played logs
  `[Audio] <cue>: <clip> vol .. pitch ..` and every buzz `[Haptic] <kind>`. `AudioDirector.Status()` (script eval, or
  the Sound section of the dbg panel) shows the settings, bus gains, duck and the five ambience levels;
  `AudioDirector.Instance.Recent` / `.MomentLog` list what played. `DebugCheckClips()` lists CueLibrary clips that don't
  load. Play tests that load the Slice by hand call `AudioDirector.Ensure()` (the bootstrap ran for the first scene
  only). Long clips (ambience, the flute tag) are set to Compressed In Memory / Vorbis 0.6 / mono in their .meta. [verified]
- **2026-10-30 · Time-lapse hitches were GC pauses; `test status` lies after a timed-out `test run`.** [verified]
  - A chunk build took 12 ms, but about 1 step in 80 took 45-75 ms: `BoxView.lastRefreshInfo` now ends in `gc N`, and every slow
    step had `gc 1`. Splitting steps across frames alone gave 56 ms and pacing the sweep per frame didn't help (worker-thread
    allocation isn't sliced by the incremental GC). The fix was to stop making the garbage: `MeshBuilder.Rent` / `Free` /
    `AppendFree` keep list capacity (seeds and draw order unchanged, so the looks are the same). Sweep max 75 -> 23 ms.
  - `unity command test run --timeout 60` can answer 504 while the import before play mode runs (1-2 min here). `test status`
    then returns the **previous** run as `completed`, with its own counts. Pass `--timeout 170` and trust `completed` only
    after seeing `running`. JSON sits at `.data.result` (`status`, `summary`, `results[]` with `FullName`, `Status`, `Message`).
  - `unity recompile` can answer "timed out after 30000ms" or "no instances" during a reload: wait for `ready`, run it again.
  - A sed insert (`/pat/{n;r file}`) that looks silent may have worked: grep for the new text before inserting again.
- **2026-10-30 · Agent shell on Windows: `python3` is a Store stub, and a long Bash call dies.** [verified]
  - `python3` only prints "Python was not found"; use `py -3`. A Bash command of a few tens of KB fails with `ENAMETOOLONG: name too long, uv_spawn` and nothing in it runs: write big files with the Write tool, then run a short command.
- **2026-10-30 · Tester batch 3 notes (one continuous session).** [verified]
  - A play test that loads the Slice by hand must add **every** bootstrap, `FirstRunView` and `BoxCompleteController` too. ShapingPlay leaves them out, which hid a real bug (`FirstRunView.Update` runs `EndRun` every frame once a run is over and clears the shaping tool).
  - `test run --async_tests true` can show `running` for about 5 minutes before the first log line (import, then play mode); check `Run started` and `Loaded scene` in Editor.log before calling it stuck. "Run started: 50 test(s)" is the assembly's total; the category filter still runs 1.
  - After `editor.playmode start`, `script eval` can answer 504 for a minute (reload ~50 s) and then see `frameCount` 1. Retry; don't trust the first read.
  - To see buildings up close, pitch 60 or more: at pitch 28 close-ups show only the front pane's haze.
  - A `Safe(tag, coroutine)` wrapper (a stack of enumerators, `try` around `MoveNext`) lets one long `[UnityTest]` log a failed step and go on; a per-screenshot UI audit (every shown text rect, overlaps) finds covered toasts the eye misses.
- **2026-10-30 · The "glass haze" was leaked boxes, Editor only (tester batch 3).** [verified] `BoxView` builds its `Generated` root with `HideFlags.DontSave` and `[ExecuteAlways]`. DontSave objects outlive a scene unload and a domain reload: at the play-mode switch a deferred `Destroy` never ran, and the root stayed parentless and sceneless, still rendering. Its land meshes die (they show as null) but the panes use the built-in Cube, so each leak added four more 5 % panes. 81 roots = 1 - 0.95^81 = 98 % opaque: land and grime hidden, wipes changing 3 levels of RGB, in every Editor screenshot and play session. A device build never had it. Find them with `Resources.FindObjectsOfTypeAll<Transform>()` (`FindObjectsByType` doesn't list them): parent null, `scene.IsValid()` false. `BoxView.OnEnable` now sweeps them (`OrphanRoots`), edit-built roots go with `DestroyImmediate`, and play-mode roots carry no flag. Lesson: when a look can't be explained by the shader's numbers, disable the renderer and see if the look goes; and judge look from a shot with one pane count, not from the material.
  - The same Editor had 4780 meshes and 180 `Code-based tests runner` objects; a domain reload took ~110 s and entering play mode ~4 min. Restart the Editor if it gets that slow.
  - `Temp/agent` shots from before this fix show the wrong glass: don't use them as a reference for the look.
- **2026-10-30 · Tester batch 3 fixes (lane B).** [verified]
  - A Git Bash `nohup x &` keeps running after the tool call ends, while `pgrep` and `ps` here don't show it: a monitor said "queue gone" and a second queue ran next to it (both started the same class, and a real-looking failure came out of that). `all.log` turning "binary" (NUL gap from a second writer) was the giveaway. Find them with `Get-CimInstance Win32_Process -Filter "Name='bash.exe'"` and match `CommandLine`. Start queues with the tool's `run_in_background` only.
  - `FirstRunPlayTests` has no `[Category]`: `--filter FirstRunPlay --filter_type category` runs **0 tests** and reports completed. Check `total` isn't 0, and use the full class name.
  - `dismiss-dialogs.ps1` reports "UNKNOWN ... 'Hold on... (busy for 01:40)'" and "'Running managed callbacks'" during a reload: those are progress bars, not dialogs.
  - Per-item commits from one working tree: `git diff -U0 -- file`, keep the hunks matching a keyword, `git apply --cached --unidiff-zero`. Check no commit leaves a name defined in a later one.
  - Editing a heredoc with an apostrophe in a multi-line `python - <<'EOF'` failed in the tool's shell ("unexpected EOF"): write the script with the Write tool instead.
  - ShowMePlay and PersonCardPlay built a random world each run and failed a different test each time ("and it showed", "the card's Pin pins them", "doll N didn't jump"). They pin seed 1234 now, like ShapingPlay and CameraModePlay; 2+ clean runs since. The "didn't jump" audit compares two samples about a second apart, so a walk during a hitch can trip it: read it as a coarse check.
  - The "pitch 60 or more" tip in the tester batch 3 notes came from the leaked panes: close-ups read at any pitch now.
- **2026-10-30 · PlayTestBase, the smoke test, leak checks.** New play tests derive from `PlayTestBase` (`Assets/Game/View/PlayTests/`): `OpenSlice(w, h, seed, freshSave, skipFirstRun)` / `CloseSlice()`, unfocused input, a sandboxed save (2026-10-31), touch helpers, `errors`, and `Bootstraps.All()` (every `AfterSceneLoad` bootstrap in the `AquariumCiv.*` assemblies, by reflection, plus the time-lapse). Its teardown runs `LeakCheck.AssertClean()`. Whole-slice check on every Editor ticket: `unity command test run --mode playmode --filter SmokePlay --filter_type category --async_tests true` (about 100 s; fails on any console error). Audio, Telemetry and the other older classes keep their own `Open` for now (special save handling). [verified]
- **2026-10-30 · Leaked dolls look like props with nobody holding them.** A DontSave root that outlives its scene loses every non-DontSave mesh (doll body and arms) but keeps `PegDoll.PropMesh` (DontSave, shared): tester batch 3's "red discs and axe-like sticks" (K_b2) were the baskets, hoes and axes of the standing villagers in the 81 leaked `Generated` roots. Proved by planting a doll under a DontSave root and reopening the scene: `Body:NONE ArmL:NONE ArmR:NONE Prop_Hoe:mesh`. Only ever in the Editor. To see sceneless objects: `Resources.FindObjectsOfTypeAll<Transform>()`; `FindObjectsByType` skips hidden/DontSave ones. In play mode a DontSave object survives a Single scene load with a valid scene, so a sceneless orphan can only be planted in edit mode. [verified]
- **2026-10-30 · `test status` right after `test run --async_tests true` can still show the previous run as `completed`.** Wait ~25 s before the first read, and check the total matches what you started. Worker bash heredocs with apostrophes in C# failed to parse here (`unexpected EOF`): write such files with the Write tool, or use a quoted delimiter that isn't `EOF`. [verified]
- **2026-10-30 · `SliceSceneBuilder.Shot` in play mode paints the glass magenta (#65).** [verified] Cause not chased: the shader and materials report fine and the Game view is clean. In play mode take shots from the Game view: set the rig (`BoxCamera.target/zoom`, `Apply()`) in one eval, then `unity command screenshot --view game --output <abs path>` (two calls per shot, about 4 s). `FireLooks.Active` and other debug statics survive leaving play mode here (no domain reload), so tests must not assert a debug static's live value.
- **2026-10-30 · A restart empties `Temp/agent`; a run started during a reload never starts.** [verified]
  - `mkdir -p AquariumCiv/Temp/agent` before redirecting into it. After `unity recompile`, a `test run` sent at once can fail with `PIPELINE_UNREACHABLE`: `test status` then shows the **previous** run as `completed`. Check the start call's `"success"` and the test count.

- **2026-10-31 · `test run` right after an edit + `unity recompile` can run the OLD code once** (a debug `Debug.Log` added to a
  test did not show on the first run, only on the second). When a play test result does not match what you just changed, run
  it a second time before debugging. `--filter_type class` matches nothing: use `category` (the `[Category]` of the fixture).
  A play-test tap right after something changes the panel's layout (a caption card closing) can miss the button: wait 3-4
  frames before touching. [verified, #79]
- **2026-10-31 · Editor quit via `EditorApplication.delayCall += Exit` never happened** (restart-editor.sh timed out after 150 s, Editor
  responsive and idle). Calling `EditorApplication.Exit(0)` directly inside the eval works: the Editor quits and the CLI returns
  "Invalid response format from Pipeline server", which is expected. restart-editor.sh now does this and ignores that error.
  A restart plus recompile of new files takes about 5+ min on this machine. [verified, #98]
- **2026-10-31 · Local mobile builds from the Pipeline (#98).** [verified] `script eval --code "return AquariumCiv.EditorTools.SliceBuild.Android();" --detach`
  (also `.Ios()`); the call returns a job id at once and the Editor builds on (status stays `ready`; watch `Editor.log`, the
  `[n/1610]` IL2CPP lines). Android arm64 about 7 min, iOS Xcode project about 6 min on this machine. Afterwards the active
  target is Android/iOS: switch back with `SwitchActiveBuildTarget(Standalone, StandaloneWindows64)` and `git checkout` the
  URP assets under `Assets/Settings` and `ProjectSettings.asset`. `unity job` has no `get`/`list` here. Foreground `sleep`
  followed by another command is blocked; `ping -n N 127.0.0.1 >/dev/null` is a wait that works.

- **2026-10-31 · An Editor restart empties `AquariumCiv/Temp/`, including `Temp/agent/`.** Tester run 4's screenshots vanished when the next agent ran `restart-editor.sh`. Copy anything worth keeping out of `Temp/` before a restart. [verified]
- **2026-10-31 · Tester run 4 notes.** [verified]
  - Since the leak fix the Editor runs at about 100 fps, so a play test that waits a number of frames for something timed in seconds now fails: `CarePlay.FullCleanByTouch` waits 120 frames for the sun (about 2.6 s to recover), and `CameraModeTests.PhotoHides...` reaches its last tap 0.08 s after entering camera mode, inside the HUD's 0.15 s guard (a 0.3 s gap passes). Wait in seconds.
  - A queue script (wait for `ready`, `test run --async_tests true --timeout 170`, poll `test status`, stop when `results[0].FullName` holds the filter) with `run_in_background` ran 11 classes hands-free. `ping -n N 127.0.0.1` is the foreground wait.
  - PlayTestBase's restore did not hold: after a run `box.json` was the last test's box (seed 4243, written at the end of the test). Fixed the same day, see the entry below.
  - UI audit: walk every `UIDocument`, log each shown TextElement's rect and flag overlaps and `MeasureTextSize` overflow. An overlap in the audit can be an element hidden *under* a sheet: check it against the shot (the HUD toast sits behind sheets).
  - A URP asset upgrade rewrites `Assets/Settings/Mobile_RPAsset.asset` during test runs (version 12 to 13): noise, `git checkout` it.
- **2026-10-31 · Active Input Handling is Both (`activeInputHandler: 2`); the debug long-press reads `Pointer.current`.** [verified in the Editor; not on a device]
  - With Input System only, `OnGUI` gets no input events in a player (Input System doc `use-imgui-alongside-input-system.md`), so the IMGUI debug panels would draw but no button would work, and a long-press read from `Event.current` would never fire. The Editor hides this: IMGUI gets input there either way.
  - Fix: Both, set through `Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings")` + `SerializedObject` (a one-line diff), then `Tools/restart-editor.sh`. The long-press moved to `DebugDock.WatchCorner()`, called from `SliceDebugPanel.Update`. Cost: Unity processes input twice (small), and every Editor start logs "This project uses Input Manager, which is marked for deprecation": expected, ignore it. If the IMGUI panels ever go, return to Input System only.
  - UI Toolkit stays on the Input System path under Both: touch, wipe and camera suites pass. `DebugDockLockTests.TheDebugPanelsHaveLegacyInputInAPlayer` fails if someone switches back while the panels are IMGUI.
- **2026-10-31 · Play tests have their own save folder.** The backup/restore in PlayTestBase could not hold: the GameClock outlives `CloseSlice`, so its autosave, focus and quit saves (the last one as play mode ends) wrote the test box over the restored save, and the `.bak` rotated with it; a dead run also left the next run copying a test box over its own backup. Now `SaveSandbox` (`[SetUpFixture]` in `AquariumCiv.View.PlayTests`) sets `GameClock.SaveFolderOverride` to `Temp/agent/playtest-save` for the run, a clock reads the path once at Awake, and telemetry follows the save folder. Tests use `GameClock.DefaultSavePath`, never `persistentDataPath`. `game/stills` still gets test files (`EraStills.Folder`), and `TimeLapseSnapshot.Folder` has the same hard-coded path. [verified]
- **2026-10-31 · `test run --mode playmode` without `--async_tests true` returns at once** with `success: true`, total 0 and an `error` inside the result ("cannot run synchronously over HTTP"). The exit code is 0: read the summary. [verified]
- 2026-10-31 · **`[Explicit]` tests through the Pipeline.** `test run` skips them with no error, and reports `0/0 passed`, unless you pass `--include_explicit true`. Even with it, a synchronous run still gave 0/0 for category filters; `--async_tests true` ran them (`UiCaptureTour`, 15/15 by category `UiCapture`, about 5 minutes). Cause of the synchronous 0/0 unknown. `test list` shows Explicit tests either way. `Temp/agent` doesn't exist after an Editor restart: `mkdir -p` before writing there. [verified]
- 2026-10-31 · **#164 UI spike notes.** [verified]
  - A `test cancel` during a failed compile left the Editor in play mode; the next run died with "This cannot be used during play mode" and stayed `running` forever. Check `EditorApplication.isPlaying`, set it false, delete `Assets/InitTestScene*` before the rerun.
  - The bash tool failed on long heredocs ("unexpected EOF while looking for matching '"). Write the script with Write, then run it.
  - A play test can import its own throwaway assets: `File.WriteAllText` + `AssetDatabase.ImportAsset(..., ForceSynchronousImport)` in `[OneTimeSetUp]`, `DeleteAsset` in `[OneTimeTearDown]`. Import errors need `LogAssert.ignoreFailingMessages = true` there. A cancelled run leaves the folder behind.
  - A new shader or UI Shader Graph shows **cyan** until it compiles: wait for `!ShaderUtil.anythingCompiling` before a screenshot.
  - `.row` and other class names in `Game.uss` reach any panel on GamePanel through the theme: prefix spike classes.
  - Custom UITK filter: include `UnityUIEFilter.cginc`, take input from `FilterVertexInput`. `_MainTex_TexelSize` gave no usable offset, `abs(ddx(uv)) + abs(ddy(uv))` did. A `.shadergraph` is plain JSON objects split by blank lines, and can be built from `UITKBasicTemplate.shadergraph`. **Element Layout UV came out 0** in a hand-built graph; the plain UV node gave 0..1 across the element.
  - A `[SetUpFixture]` stops at its assembly: a play test in its own asmdef needs its own `GameClock.SaveFolderOverride`, or `OpenSlice` deletes the real save.
- 2026-10-31 · **#178 frost test notes.** [verified]
  - Play mode stays on `PC_RPAsset`. `QualitySettings.SetQualityLevel(0, true)` reports level 0 and even `QualitySettings.renderPipeline = Mobile` leaves `GraphicsSettings.currentRenderPipeline` on PC, in a play test, set before or after the scene load, and setting level 0 in edit mode first doesn't carry into the run. Edit mode swaps fine. Cause not chased. A test can't cover Mobile_Renderer this way: pin `UniversalRenderPipeline.asset.renderScale = 0.8f` for the setting that matters and restore it. Loading the Mobile asset dirties `Mobile_RPAsset.asset`, and a quality change dirties `QualitySettings.asset` (a serialization upgrade): `git checkout` both.
  - `unity recompile --project-path F:\GameDev\AquariumCiv` in Bash lost its backslashes ("F:GameDevAquariumCiv isn't a Unity project"). Use `F:/GameDev/AquariumCiv`. `unity status --timeout` needs `--until-ready`.
  - Running `restart-editor.sh` with output into `Temp/agent/` fails: the Editor start empties `Temp/`, log included.
  - A `filter` on an element running off the screen edge left a light band as wide as the overrun along one side. Keep filtered elements on screen. A blur also fades to transparent at the copy's edge, so what's under the sheet shows through there: give the sheet an opaque colour under the blur.
  - Label `white-space` from `Game.uss` didn't reach a code-built label in an absolute parent: set `WhiteSpace.Normal` in code.
  - No game code sets `Application.targetFrameRate`, so phones run at the default 30.
- **2026-10-31 · UI Toolkit over the tank, what CSS mocks hide (#107 hero).** [verified, Editor] The project is Linear, so the panel blends in linear light. Dark translucent over light comes out paler than CSS (40 % dots read as 31 %, a 20 % shadow almost vanishes), text reads 7 to 16 % thinner, and frost over black is 210, not 175. `PanelSettings.forceGammaRendering` does nothing on an overlay panel. A 0.3 px text outline in the text's own colour restores the weight. `<line-height=135%>` is a percent of the font's line, not its size: use a px tag. A blur fades at its element's edge: a sharp copy of the same texture under it removes the light band, with no extra pass. A TTF works directly in `-unity-font-definition`, and through `var()`. To compare with a mock, render its CSS in headless Chrome (`--force-device-scale-factor=3`, `--virtual-time-budget=3000`) over a frame the game saved, with the same fonts; an element lifted out of the board loses the board's `font-family`. Right after `unity recompile`, "No Pipeline instance" for a few seconds is the domain reload, not a failure. Details: docs/design/ui/hero/README.md.
- **2026-10-31 · #108 Keeper's Glass notes.** [verified, Editor]
  - An element with children doesn't measure its own text: a `Button` with any child (even absolute) collapses to its min-width and the text spills out. `GlassButton.Mark` adds an invisible `Label` twin to hold the width; change its words with `Say`.
  - Children paint over a `TextElement`'s own text. `GlassToggle` keeps `text` for tests, makes it transparent and draws a child label on top.
  - `RemoveFromClassList(Button.ussClassName)` in a control's constructor drops the theme's hover and border rules, so one USS class set owns every state.
  - `SVGImporter` defaults to `SVGType.VectorImage` here; the enum has no `UIToolkit`. Icon SVGs need an explicit white stroke, drawn as `stroke="#FFFFFF"`.
  - `FontAsset.CreateFontAsset` then `AddObjectToAsset` for material and atlas makes a small asset (`Tools`: `AquariumCiv.EditorTools.GlassAssets.Run()`). Dynamic, `clearDynamicDataOnBuild` on.
  - `AssetDatabase.MoveAsset` needs the target folder imported: run `AssetDatabase.Refresh()` first, or it fails with "Could not find parent directory GUID".
  - A dynamic font asset grows to 1024² at runtime and the Editor saves that as 2 MB of hex in the `.asset`. `GlassFontGuard` skips those saves during play and empties the assets outside it, so they stay out of git.
  - Atlas padding 9 at sampling 90 with `-unity-text-outline-width: 0.3px` drew thin dashes over `n` and `m` in bold text (a neighbouring glyph in the atlas). Padding 18 cleared them. Look at zoomed crops: at phone size it reads as dirt on the type.
  - Bash heredocs passed through the tool lost backslashes and broke on apostrophes. Write a script with the Write tool and run that.
  - `Write` overwrites silently: `GlassPlayTests.cs` was already the tank's tests. Check a new file's name with Glob first. A `Write` result saying "updated" instead of "created" is the sign.
  - `test run --filter_type class` matches nothing (0/0): the types are `testName`, `assembly`, `category`.
  - Tests set `GlassMotion.Instant` through `GameUiPanel.AcceptInputUnfocused(true)`: a tap goes to where a control is, and a sheet on its way up would take it at the wrong spot. A test of real motion sets it false.
  - A hit area larger than the drawn piece takes two things: `Panel.Pick` tests the element's bounding box first (so a clear absolute child past the edges has to widen it, picking Ignore is fine), and `Clickable` fires only if the finger lifts inside `ContainsPoint` (so override that too). `worldBoundingBox` is internal. `GlassReach` does both. `TextElement.text` is virtual: `GlassButton` overrides it to keep the text's measuring twin in step.
  - `hud-top--away` used to follow the first run on the half-second text refresh, so for up to 0.5 s after `DebugSkipAll` the top row was `display: none` and a test's tap at its `ScreenCenter` landed nowhere. Anything that gates a control follows state every frame, not on a text timer.
- **2026-10-31 · Background runner scripts outlive `TaskStop` on Windows (#108 tester).** `TaskStop` ended the task but left the Git Bash child running, so an old `runcat.sh` kept starting `test run`s, and the next run died with "Playmode tests were aborted because the player was stopped" with `test status` stuck on `running`. Find them with `Get-CimInstance Win32_Process | ? CommandLine -match <script>` and `Stop-Process` them before rerunning. [verified]
- **2026-10-31 · `test status` can stay `running` after a run is over (bundle 142-150-152-148-97-166).** [verified] Back-to-back EditMode runs: `TestResults.xml` held the finished run and the Editor was idle (`isPlaying` and `isCompiling` false), but the Pipeline's `TestResultCollector` never logged `Run finished` for it (an earlier log line reads "Ignoring duplicate RunFinished"), so `test status` said `running` until `test cancel`. Before calling a run stuck, compare `Saving results` in Editor.log with the results file; then `test cancel`, `console clear`, next run. A `test run` right after `unity recompile` can also fail with "domain reload is in progress": read the result of the call, it does not retry.
- **2026-10-31 · `gh issue view <n> --comments` prints only the comments when piped; read the body with `--json title,body`. `unity recompile` returns before the domain reload ends, so a `test run` right after it can fail to connect: check `unity status` shows `ready` first.** Bash heredocs with apostrophes in a long compound command can also fail to parse: put a long edit script in a file. [verified]
- **2026-11-01 · #170: three small things.** [verified] A `TextMesh`'s colour is a vertex colour: in a Linear project it isn't converted, so a dark brown reads as grey (use `.linear`). A centred `Label` in an `align-items: center` column measures its own width, so a line that only just fits wraps its last word while the parent stays one line tall: `align-self: stretch` on the label. Editing many C# files from Bash: a Python heredoc with apostrophes fails to parse in the shell, so write the script with the Write tool and run it.
- **2026-11-01 · #95, #97: notifications package, sliders in a sheet, mobile compile check.** [verified] `unity command package add --identifier com.unity.mobile.notifications@2.5.0 --confirm true` works (async: the Pipeline drops for about a minute during the reload, then `package status` says `completed`). The package asks for iOS permission at launch by default: `NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch = false` is set in `ProjectSettings/NotificationsSettings.asset`, keep it off, the ask belongs to the first era moment. `PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings { target = BuildTarget.iOS, group = BuildTargetGroup.iOS }, "Temp/agent/x")` through `script eval_file` compiles the `#if UNITY_IOS` code without switching the target (35 s for iOS and Android together): use it to check platform code. A `ScrollView` takes touch moves from a slider inside it, so `GlassScroll` hands a press on a `GlassSlider` over before its view sees it. An inline `style.display` beats any USS class, so a row that a class must fold can't also be shown with `style.display = Flex`.

- **2026-11-01 · `test run --mode edit` reports `0/0 passed` with no error.** The Pipeline's editor mode is `--mode editor` (and `playmode`, though `play` works); `test list` says so, `test run` doesn't. A synchronous `test run` also leaves `test status` on the previous run: read the failures from `test run --format json` (`Summary`, `Results`). `/tmp` in Git Bash isn't Python's `/tmp` on this machine: write JSON to the scratchpad. [verified, #77/#80]
- **2026-11-01 · A captured pointer's events go only to the capturer: a `PointerUpEvent` trickle-down on an ancestor never sees the lift of a tap on a `Button`.** `Clickable` captures on press, and from there the lift is dispatched to the button alone. To learn where a tap landed from a parent, read the `PointerDownEvent` (trickle-down), which happens before the capture (`GlassSheet`, tester run 5's double tap). `Panel.Pick` on a clear full-panel child hits it, but `GameUiPanel.Over(root, p)` then says false for every layer, so anything that eats taps also has to tell `UiInput` or the world still takes them (`GlassTapGuard`). [verified]
- **2026-11-01 · People bundle notes.** [verified] `AquariumCiv.View.Tests` must run with `--async_tests true`: a synchronous `test run` dies with "socket hang up" part way (a test in it reloads the domain), and the second call answers `PIPELINE_UNREACHABLE`. `AquariumCiv.Life.Tests` takes 6.5 min of Editor time with no line in Editor.log, and `dismiss-dialogs.ps1` lists its "Hold on" bar as UNKNOWN: compare `TestResults.xml`'s time before calling it stuck. `PeopleView.Activities` refreshes every 0.2 s (`replanSeconds`): after a planner change wait that long, not a few frames, before reading what someone is doing. `consoleErrors` in `console get`'s `groundTruth` keeps counting a fixed compile error until `console clear`.
- **2026-11-01 · Tester run 6 notes.** [verified] `ShowMeUI` is DontSave: find its `UIDocument` with `Resources.FindObjectsOfTypeAll<UIDocument>()`; `FindObjectsByType` and `GetComponentInChildren` both missed it, so every bubble read as "none". A town has no fire or stash piece (`PeopleView.solids` is empty from the Village on, seed 1234): a walk audit there passes without testing anything, so log the solid count. A throwaway audit that waits between samples must clear its last positions, or each wait reads as a teleport. In a test asmdef that references `AquariumCiv.View.PlayTests`, deriving from `PlayTestBase` works; it needs its own `[SetUpFixture]` for `SaveFolderOverride`. `cat > file` with no input hangs a Bash call until its timeout.
- **2026-11-01 · A namespace called `AquariumCiv.Light` hides `UnityEngine.Light` from all code under `AquariumCiv.*`.** [verified] The logic branch's new assembly compiled in dotnet, but the Editor scripts (`Light` in `SliceSceneBuilder` and four others) failed with CS0118 and the Editor opened on "Enter Safe Mode?". The assembly keeps its name, the namespace is `AquariumCiv.Lighting`. dotnet tests can't catch this: they don't build `Assets/Editor`. A method called `Awake(PeopleView)` on a MonoBehaviour logs "Script error: Awake() can not take parameters" on every reload: same for any Unity message name. `dismiss-dialogs.ps1 -Click` answers Safe Mode with Ignore, and a recompile then picks up the fix.
- **2026-11-01 · `unity recompile` then `test run` again.** The Pipeline is gone for up to 15 s after the recompile returns ("No Pipeline instance found"), and a wait on `TestResults.xml` then runs out its timeout for nothing. Wait for `"state": "ready"` first. `test run --filter` takes one partial name: to run a handful across classes in one go, tag them with one `[Category]`. Single PlayMode tests take 10 to 20 s after a fresh restart, so "test that fails first" costs under two minutes.
- **2026-11-01 · A white disc blooming over the screen was one tiny mesh face.** [verified] `Vector3.Normalize()` returns zero under a length of 1e-5, `MeshBuilder.Tri` only dropped faces under 1e-6, so a face with sides of about 0.002 got a zero normal, the shader's `normalize` a NaN, and bloom spread that pixel into a disc that hid the Keeper Doll. `MeshBuilder` divides by the magnitude now. A NaN pixel looks like a bright soft ball centred on the part: suspect it before the material.
- **2026-11-01 · Bundle 2 notes.** [verified] `PeopleView.Now.day` can lag the sim's month after `DebugSkipYears`: `DebugSetHour` lands on the month, so read a day's plan after a jump, or `RunInBeatsOn` lists another day. A test that drags with `step` 0 loops until its timeout. `FindTarget`-style picks at 10:00 miss: a doll's pick radius covers most of a small village at default zoom, so test roof taps at night with everyone in. The first test after a recompile can take over 70 s (`rt.sh`'s limit caught it twice): give it 100 s. `Texture.EncodeToJPG` output from a `RenderTexture.GetTemporary` with the default read/write mode comes out pale in this Linear project: pass `RenderTextureReadWrite.sRGB`.
- **2026-11-01 · Tester run 7 notes.** [verified] A person's card flies the camera: a sweep of roof taps needs `Cam.GoHome` and fresh screen points between taps, or the later taps land on stale spots. `WishPayoffView.Played`/`LastPlan` persist across tests in a class run, so `Until(view.Played > 0)` returns at once with the last test's plan when the view outlives the scene (`AWishLineWaitsForTheFirstRunsWord` fails in category `Tester6`, passes alone). Wait for `Played > before`. `rt.sh`-style `head -40` on `test status` cuts the failed test off: keep the whole result. A file written straight into `Temp/agent/tester/` survives test runs; an Editor restart empties it.
- **2026-11-01 · Bundle 3 notes.** [verified] `ScreenCapture.CaptureScreenshotAsTexture` reads as linear and a loaded JPG as sRGB in this Linear project, so no one blit mode suits both: `ScreenShot.Scaled` now averages on the CPU (screen 0.695, JPG 0.694). A pressed icon's fill is on the button itself (`.kg-btn--icon:active`), not its `.hud-disc`, and it outlasts the click by a frame. `Toasts.Current` stays null while a `GlassBigPrompt` holds the lane: a toast "lost" in a grown village is waiting, check `Waiting`. `Stop-Process` matched by a command line containing `rt\.sh` also kills the Bash that ran it (the pattern is in its own command line). A `test run` right after `test cancel` following a failed compile can sit at nothing until its limit: `test status` says `running`, cancel and start it again. `DebugSkipYears(1f / 12f)` moves `State.month` by 2, so it can step past every rest day (`day % 6 == 5`): set `State.month` directly in an audit. `ShapingFx` is the shared see-through material (vertex colour, no depth write): reuse it through `ShapingView.LoadFx()` instead of a new shader. In seed 1234's town the stalls sit behind roofs and the frame at the home view, so stall goods can't be judged there.

- **2026-11-01 · Tester run 8 notes.** [verified] `test status` answers `PIPELINE_UNREACHABLE` for a moment during the play-mode domain reload: a wait script must test for `"status": "completed"`, not for "not running", or it stops 9 s in. `BoxCamera.EaseTo` clamps yaw to `minYaw`..`maxYaw`, so a yaw sweep past the limits lands on the same pose: read the projected positions before trusting a sweep, and pass a raised pitch and a small zoom to look into a town. `FindObjectsByType<PegDollView>` finds none; use `Resources.FindObjectsOfTypeAll` with `activeInHierarchy`. To learn if a doll can really be seen, freeze `pv.timeScale = 0`, grab the screen, `SetActive(false)` the doll, grab again, count changed pixels round it. An annotated tap map (a TSV of each tap's screen point and result drawn over the home shot with PIL) judges a tap sweep faster than a pass rate. `Toasts.Current` is null behind a `GlassBigPrompt`, and the village's "Watch them become a town" holds the lane in most grown-village setups.

- **2026-11-01 · Bundle 4 notes.** [verified] A Bash heredoc with an apostrophe anywhere in it fails with "unexpected EOF" before anything runs, even with a quoted delimiter: write patch scripts with the Write tool. `python3` isn't on this machine: `/c/Python311/python`. `test run --filter` with a class takes `--filter_type testName`, not `name`. `Application.isPlaying` throws on a worker thread, and `BoxView` chunks build on `Parallel.For`: read such things in `OnEnable`. `Object.FindObjectsByType` skips `DontSave` objects: go through the parent's transform. `GlassBubble.TooLong` is 48 characters: a test line longer than that is a caption, so a bubble test reports "nobody on screen". `ShowMeDirector.BubblesOn = false` takes a line down the next frame, so a bubble test has to set it true. To show fails-first for a fix that changes an API a test needs, keep the test's hooks and swap only the logic files back with `git show HEAD:<path>`, in a script with a `trap` that restores them: 21 s of test time beat reasoning about it. `PeopleView.HandDriven` lets a test step `Tick` at a fixed 1/30 s, hundreds of frames in one real frame. Per-frame jitter isn't p95 turn rate (rounding a corner raises that): count acceleration reversing from frame to frame. **`TaskStop` on a background Bash stops the wrapper shell, not a loop script it started:** on Windows the script lives on, and two sweeps then take turns on one Editor. The signs: a class reporting another class's totals, a run that logs `Run started` and never finishes, `test status` stuck on `running`. Cost about 40 minutes here. List them with `Get-CimInstance Win32_Process`, match the script in the command line, and stop them before starting again. A helper that waits on `TestResults.xml` should print the class names from the file, so a stale file can't pass for this run. A view check that picks "any doll" can pick one hidden by a roof or a tree: use `BoxView.Blocked` for a doll the camera sees.
- **2026-11-01 · Tester run 9 notes.** [verified] `test status` says `running` between tests of a category too: `test run` sent then kills the run ("This cannot be used during play mode"); wait for `"status": "completed"` before the next start, then `test cancel`, `console clear` to recover. A throwaway check right after `yield return null` runs before `LateUpdate`, so `WindView`'s sails read as missing for one frame: check after `WaitForEndOfFrame`. Dragging the time-lapse to year 0 snaps to a pin, the caption takes the play row and `tl-play` has a zero rect: close the caption before tapping play. A knock counts `TapGap` (0.6 s) after the tap, so wait longer than that to read `Knocks`. Shot names built with `{h:0.0}` get a comma on this machine's locale. A subclass of `FirstRunPlayTests` in a throwaway asmdef can `yield return` the base test and then watch the tail in the same scene.
- **2026-11-01 · Bundle 5 notes.** [verified] Before blaming the crowd for a walker's speed, trace three speeds side by side (drawn, the plan's point, the doll's planned point): most of the Town's "crowd brakes" were the catch-up from a door slot to the route (twice the walk, then dropping to it), and one fix try (aiming ahead of the plan's point with `PositionAt` at a later time) made the planned point stop dead and was worse than nothing. `PeopleView.HandDriven` runs 2,000+ walker-seconds in a few real seconds, so `WalkCrowd` takes 7 s: measure often. A Bash `cat > file` with no heredoc waits on stdin for ever and ends up in the background after 120 s. In the Bash tool `--project-path F:\GameDev\AquariumCiv` unquoted says "no instances"; quote it with single backslashes or run from the project folder. `test run --filter_type testName` needs the full name (`AquariumCiv.View.PlayTests.Class.Test`). `dotnet test Tools/SimTests` takes over 3 minutes: a 170 s `timeout` kills the host and reads as "Test Run Aborted" with a passing count. A bounded wait script (start with `--async_tests`, loop on `test status` to a hard limit, print failures) in a background Bash gets a notification at the end, and catches a stale `completed` by requiring `running` first. Keep the fail-first log of a run (`test status --format json` to a file) before patching.

- **2026-11-01 · Light and camera pass (#113, #175, #146, #162, #193).** [verified] The Mobile quality level has `excludedTargetPlatforms: Standalone`, so in play mode on a Standalone Editor it doesn't exist: `SetQualityLevel(0)` lands on PC and `GetRenderPipelineAssetAt(0)` returns `PC_RPAsset`, with no error. Every "phone" look or frame time so far was the PC asset (2048, 4 cascades, soft). To see the phone's asset, load `Mobile_RPAsset` with `AssetDatabase` and assign `QualitySettings.renderPipeline` after `OpenSlice`, and put it back at the end (`LightShots.Box`). Loading the asset that way also reserializes it and `QualitySettings.asset` (engine version only): keep the first with the #193 change, don't commit the second.
  - The camera is 41 units from the box at zoom 1 (22 degree lens): a shadow distance of 12 to 14 would remove every shadow. What jagged the shadows was the range 0.3 to 50 stretched over the map. `BoxCamera.FitDepth` sets the near plane to `d - 7` and `shadowDistance` to `d + 8` every frame, so the map covers 15 units of depth at every zoom. It writes the asset's value in memory only and restores it in `OnDisable`; the file keeps 50.
  - `RenderSettings.ambientSkyColor` etc. in Trilight do update the ambient probe at runtime on 6000.3 (SH L0 logged at each hour), no `DynamicGI.UpdateEnvironment` and no `_AC_AmbientTint`.
  - `Volume.profile` makes a copy; `sharedProfile` is `Post_Slice.asset`. A material's `SetFloat` on the solid material writes an asset in the Editor too, so per-hour shader values are globals added to the property (`_AC_AmbientBoost`), which are also 0 in edit mode. A global multiplier would blacken everything until something sets it: make globals default to "no effect at 0".
  - URP's prefilter fields are recomputed at build (`UpdateShaderPrefilteringDataBeforeBuild`). `m_PrefilterSoftShadows: 0` means `_SHADOWS_SOFT` is kept. To ask what a build would keep, call `ShaderBuildPreprocessor.GetSupportedShaderFeaturesFromAssets` by reflection with the asset (Editor eval). That says `SoftShadows` for Mobile_RPAsset now; it isn't a variant list from a real build.
  - `UnityStats` works in a play test (`UnityEditor.UnityStats.batches`, `drawCalls`, `setPassCalls`). Shadow casters follow how many dolls are out: 81 at noon, 21 at 22 h, so compare looks at one hour.
  - `git add a b` with one bad path adds nothing and says only `fatal: pathspec`; a `; git commit` after it then fails too, quietly under `2>/dev/null`. Read the log line after a commit.
  - A flat quad on the ground ends in a hard line where a slope comes through it. A 5x5 grid laid on `land.OnGround` fixes it for one more mesh vertex count, no draw.
- **2026-11-01 · Settings and grime pass (#115, #121, #206, #207).** [verified]
  - `editor-health.sh` right after `restart-editor.sh` can say RESTART: the last reload in the log is the cold first one (105 s here). Don't restart again: recompile once and read it again (6 s here).
  - `BoxView.DebugFingerprint()` hashes the box's meshes per part (positions to 1/2048 u, rgb, AO alpha apart). It proves a look is unchanged; a game view shot can't, because the wind sways the trees between two shots (1,600 pixels over 16 levels on an unchanged box).
  - `Land.Height` and `Terrain.Height` are the same sum but differ by 1 ULP (1.2e-7) at 51 of 6,887 points under Mono, so an exact vertex hash changes when one replaces the other. Compare old and new formula on the same code before calling it a real change.
  - A setting's water hangs on the pond's centre: `Land.Shoreline` shoots rays from it, so a centre above the water gives a zero-size pond and no water mesh, with the sim still counting water cells. `SettingViewTests.EveryPresetHasWaterToDraw` holds it.
  - Fog is off in Slice (`m_Fog: 0`): `RenderSettings.fogColor` writes show nothing. The room is two things: the wall in the base mesh, lit by the sun, and the camera clear colour, scaled by the driver.
  - `--include_explicit true` with `--filter_type assembly` runs every Explicit test of the assembly.
  - `NoWalkRushes_HeavySweep` gives different numbers on one commit: .NET 2,138,174 walks, 5 over 2x, 2.19x; Unity 2,111,532 walks, 2 over 2x, 2.03x. Quote the runtime with the number.
  - Unity's test runner gives a test 180 s unless it has `[Timeout]`. A synchronous EditMode test can't be cut off: it runs to its end and is then marked failed (`TheSamePlanWhateverIsAskedFirst(1,200)`: 460 s, "Timeout value of 180000 ms was exceeded"). A long EditMode run blocks the main thread, so `unity status` says `unreachable` for all of it: read the mtime of `%USERPROFILE%/AppData/LocalLow/Tommaso Ribaudo/Aquarium Civ/TestResults.xml` and the dialog script instead.
  - `test status` stayed `running` again after a 12 s EditMode run (see 2026-10-31): `TestResults.xml` had the result. A bounded wait on that file's mtime is the safer end signal for EditMode.
  - `test run` sent while `unity recompile` reports a compile error runs the old assemblies; and a `test cancel` mid PlayMode leaves `Assets/InitTestScene*.unity` as the open scene: reopen Slice and delete it with `AssetDatabase.DeleteAsset`.
  - Shader globals outlive a scene in the Editor: a test that ended mid fade left `_AC_FrameFade` on every later box (the frames looked faded in shots of other tests). `BoxView` now sends it once on enable.

- **2026-11-01 · Why `AquariumCiv.View.Tests` stalled in the Editor (final checks).** [verified] "Run started: 1285 test(s)" is the whole
  tree's count, not a missing filter: the assembly filter worked (119 ran in 13 s). The stall was `BoxLeakTests.AfterADomainReload_OneBoxAndNoOrphans`,
  which calls `EditorUtility.RequestScriptReload()`; the reload drops the Pipeline's `TestResultCollector`, so `RunFinished` never arrives, `test status`
  stays `running` for ever and a synchronous call dies with "socket hang up" (results are still written to `TestResults.xml`). Fix: the test skips itself
  (Assume, shows as 1 inconclusive, 118/119 passed) unless `Application.isBatchMode` (CI) or `AQ_RELOAD_TEST=1`. Recover a stuck run with `test cancel`.
  Any EditMode test that reloads the domain will do the same.
- **2026-11-01 · `test run --filter` takes one pattern.** `--filter` twice is refused; `A;B` runs 0 tests. One test or class per run.
  `[Explicit]` shots need `--include_explicit true`, with `--async_tests true`. `unity recompile` returns before the Editor is `ready`: run
  `unity status --until-ready` before the next `test run`, or the run uses the old assembly. [verified]
- **2026-11-01 · Static test state that leaks between PlayMode classes.** `ShowMeDirector.BubblesOn` (many fixtures set it false and never restore it;
  `ShowMePlayTests.Open` now sets it true) and `GameUiPanel.Toasts` holds (`OpenSlice` now calls `ClearHolds()`; `Toasts.Holders` names them).
  After a swipe the peek only has to give up its own "peek" hold: the era's Watch prompt or the Finish prompt may hold the lane too. [verified]
- **2026-11-01 · `Tools/watch-editor-stall.sh`.** Run it from the orchestrator, in the background, while an agent holds `.agent-editor.lock`:
  `bash Tools/watch-editor-stall.sh [quiet_seconds=600] [max_seconds=14400]`. It exits (waking the orchestrator) when the lock is released (0), when nothing has
  moved for 10 minutes (3: test status and Editor.log size unchanged) or at the max (4). It is read-only against the Editor. [verified from the script]
- **2026-11-01 · `HudBlocksTheCameraOnlyWhereItIs` (hud-title 141.64 < 143.5) did not reproduce** alone, in `HudTapTests` as a class, or with the Game view at
  750x1334, 1536x2048 and 2556x1179. The assert message now carries the layout, panel and screen size and the resolved min-height for the next time. [verified]
- **2026-11-01 · Tester run 10 notes.** [verified] UI Toolkit snaps layout to physical pixels, so at a tiny Game view a 144 panel-unit element reads 141.64 (305 px wide, 40 px tall: 40 x 1080/305) or 141.75 (320 px wide): the `hud-title` height failure is a Game view left small, not a layout fault; a size assert in panel units should allow one physical pixel. `test status` keeps the last result until a new run starts: a run sent while the Editor reloads returns the old result at once, so wait for `unity status --until-ready` plus a few seconds. Bash foreground calls over 180 s move to the background: keep a throwaway run's script limit under 175 s and follow its log. `Card close` by world tap and `Glide` need a real tap, not `Cards.Close()`: the code path differs (a touch cancels the glide). A contact sheet (PIL, 8 columns of 240 px) reads 30 shots at a glance and catches what counters can't: a passing era moment that renders blank.

- **2026-11-01 · Tester run 10 fixes.** [verified] `RenderPipelineManager.beginCameraRendering` fires for a normal frame, but a `RenderPipeline.SubmitRenderRequest` goes through `RenderSingleCameraInternal` and raises no such event: anything fitted in that hook has to be called by hand before a request (`EraStills.FitDepth`). Setting `nearClipPlane` and the URP asset's `shadowDistance` in that hook takes effect the same frame (culling and `maxShadowDistance` are read after it). Scene far is 120; `BoxCompleteController` raises it to 420 later in start-up, so a value read in `Awake` is the wrong baseline: only ever raise it. `RenderPipelineManager.endCameraRendering` with a `DepthWatch` (`PlayTestBase.cs`) sees every frame whoever drives the camera, so one test covers every moment beat. A bug counters can't see needs that, or a shot. `cam.aspect = x` works for a test of other aspects (`FitDistance` and `ScreenPointToRay` follow it); `ResetAspect` after. A helper script started with no arguments would send a bare `test run`: make a runner refuse an empty filter. `test run --filter_type testName` with a full class name runs the class.

- **2026-11-01 · Grime measured on the real shader (fix 11).** [verified] A pane's true opacity at any hour: render the pane alone (`cullingMask = 1 << CareView.GlassLayer`, other panes off, post off) over a black and a white `SolidColor` clear into a Linear RT, then alpha = 1 - (white - black). `GrimeMatrixPlayTests.Opacity` does it; drop pixels under 0.03, because a slanted pane in its bounding box otherwise counts the empty corners as clear glass (0.20 read as 0.34). Colour numbers (moved, luminance, contrast) could not separate the cow hide from a good look; this could. `GlassNight.FrostOpacity` is the UI frost, not the panes: the panes' alpha never changed with the hour. A mask level normalised over texels that can never hold grime is a hidden scale factor: 1.5 here, and two sim levels drew the same. Don't check a stored mean alone, count how many texels sit pinned at the clamp. A straight-alpha `lerp(cleanRgb, grimeRgb, cover)` next to `lerp(cleanA, dirtyA, cover)` paints a thin layer in the thin colour: a milky wash that only shows once cover stops being near 0 or 1. Mix by the amount of each. Numbers under `{x:0.0}` and `{h:00.0}` come out with a comma on this machine, in logs and in file names: format names with `InvariantCulture`. `unity recompile` right after a test run can answer `400 Bad Request` and still recompile: wait for `ready` and check the Editor log rather than resend. A heredoc with an apostrophe in a Bash call still fails with "unexpected EOF": patch scripts go through the Write tool. A Glass.mat value can be overridden in `CareView.BindPanes` when the asset can't be committed.

- **2026-11-01 · `test status` after a switch from PlayMode to EditMode.** [verified] An EditMode `test run --filter <class> --filter_type testName` right after a PlayMode run left `test status` on the PlayMode totals: five classes all reported 8 of 8, the last PlayMode class, and looked green. Read `TestResults.xml` by its mtime and print the class names from it. A class filter with the wrong namespace matches nothing and writes no file: the same check catches that as a timeout rather than a pass.
- **2026-11-02 · #224 choice scene notes.** [verified] Unity's NUnit has no `Is.AnyOf` (CS0117; dotnet's NUnit has it): write `Assert.IsTrue(a == x || a == y)` in tests that run in both. Removing a live staged scene from `LifePlanner.live` mid-way and adding the next one makes the planner start the new block from the person's *day-plan* spot, so the doll walks off and back: cut the old scene's segments at the hand-over time instead (`LifePlanner.CutChoice`), and the next pieces join the same block. A pin inside the fire's solid (`CrowdAvoid.FireRadius` 0.2) is pushed to its rim: keep staged spots more than 0.2 from the fire. A play test's root count over a cycle can grow by others' lazy one-offs (`SpeakerRing`, `ChatterMarks`): compare root names, not counts.
- **2026-11-02 · `restart-editor.sh` can find the Pipeline unreachable and wait 150 s for nothing (#225/#231 stint).** [verified] Its quit eval failed with "Pipeline server isn't reachable" (Editor up, server not answering yet), so the Editor never got the quit and the script ended "did not quit in 150 s". `unity pipeline list` a minute later showed `serverReachable true`; running the script again restarted cleanly. If it says "did not quit", check `unity status` is `ready` and run it once more before reporting.
- **2026-11-02 · Dilemma cheats and apart camps (#225, #231).** [verified] `DilemmaCheats` (View) holds every P7 cheat as a static returning a line (`Raise`, `AnswerOnCard`, `StageInBox`, `AnswerInBox`, `Lapse`, `FireCallback`, `List`, `Report`, `Toggle`, `Lab`), usable from `script eval`. `CardStackView.DebugOpenEntry(entryId)` opens the stack on one chronicle entry's card. A play test that reads the telemetry log must filter by `GameClock.Tel.SessionId`: the sandbox telemetry folder keeps every earlier run's lines (same box id `b0`). `GameClock.config` is the one `SimConfig` every box shares: a test that turns dilemmas/lab on must turn them off in `TearDown`. `ApartGroupsView` kills its dolls' own body and arm meshes; `PeopleView` doesn't (its removed dolls' meshes wait for an unload).
