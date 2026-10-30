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

- USS has no `:first-child`/`:nth-child`, `gap`, `line-height` or `text-transform` (rules are silently ignored). Space
  children with one-sided margins. [inherited]
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
