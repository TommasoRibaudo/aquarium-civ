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
