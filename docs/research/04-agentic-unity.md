# 04: Agentic Unity workflow (Claude Code ↔ Unity 6.3 LTS Editor)

*Researched 2026-10-28 against live sources. Project: "aquarium civilization" (Unity 6.3 LTS, URP, iOS/Android, solo dev, Windows 11).*

> **TL;DR.** The landscape changed in July–Oct 2026. Unity **deprecated the MCP server inside its AI Assistant package** and replaced it with the free **Unity CLI (`unity`) + `com.unity.pipeline` package**, which drives a running Editor from the shell (`unity command …`, eval, capture, play mode, tests, builds) and also has an MCP mode (`unity mcp`). Unity's own docs say that a shell-capable agent like Claude Code should use the direct commands, because they're faster and use fewer tokens than MCP. **Recommended:** Unity CLI + Pipeline + Unity's official Claude Code plugin as the primary path. Keep **CoplayDev "MCP for Unity"** as the mature open-source MCP fallback. Add plain `Unity.exe -batchmode` scripts for deterministic tests and builds. Both Unity pieces are still **beta/experimental** (CLI 1.0.0-beta.14, Pipeline 0.9.1-exp.1), so pin versions and keep git as your undo button.

---

## 1. Editor bridges: what exists (Oct 2026)

### 1.1 Unity's official offerings (there are three, and it's easy to confuse them)

| Offering | What it is | Status | Cost | Works with Claude Code? |
|---|---|---|---|---|
| **Unity MCP in `com.unity.ai.assistant`** (bridge + `relay_win.exe`) | In-Editor MCP server. Uses a named-pipe bridge on Windows and a stdio relay at `%USERPROFILE%\.unity\relay\relay_win.exe --mcp`. Clients need one-time approval under *Project Settings > AI > Unity MCP* ([get started](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.0/manual/unity-mcp-get-started.html), [overview](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.0/manual/unity-mcp-overview.html)) | **Deprecated**, replaced by Unity CLI ([Unity docs](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli)) | Was tied to Unity AI plans (Personal: 14-day trial, then a subscription; Pro: 3 connections) ([pricing](https://unity.com/products), [changelog 2.8](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.11/changelog/CHANGELOG.html)) | Yes, but don't start new work on it |
| **Unity AI Gateway** | Runs third-party agents (Claude Code, Codex, Gemini, Cursor) *inside* the Assistant window with your own API keys ([docs](https://docs.unity3d.com/Packages/com.unity.ai.assistant@1.7/manual/ai-gateway-landing.html)) | Beta | Requires a Unity AI subscription per the [plans page](https://unity.com/products) | It's the opposite direction (Claude runs inside Unity), so it isn't useful for terminal-first work |
| **Unity CLI + Pipeline package** (`unity command`, `unity mcp`) + **Unity plugin for Claude Code** | Standalone `unity` binary. Installs Editors and modules, builds, tests, recompiles, and drives a **running Editor** over a localhost HTTP API from `com.unity.pipeline`. The plugin ships skills that call the CLI ([CLI docs](https://docs.unity.com/en-us/unity-cli/use-unity-cli.md), [Pipeline](https://docs.unity.com/en-us/unity-cli/unity-pipeline/unity-pipeline-package.md), [plugin](https://docs.unity.com/en-us/ai/unity-plugin/claude-code.md)) | CLI announced 15 Jul 2026 and labelled experimental. Latest is **1.0.0-beta.14 (21 Oct 2026)** ([release notes](https://docs.unity.com/unity-cli/release-notes)). Pipeline **0.9.1-exp.1** ([docs](https://docs.unity3d.com/Packages/com.unity.pipeline@0.9/manual/index.html)). Plugin launched 9 Sep 2026, ~577★ ([repo](https://github.com/Unity-Technologies/unity-agent-plugin)) | **Free**, "doesn't require a Unity AI subscription" ([Unity docs](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli)). Needs Unity 6.0+ to drive an Editor | **Yes, first-class.** Unity reports that Claude Code is the #1 agent on the CLI, and that 98% of CLI runs come from agents or automation ([Unity blog, 22 Oct 2026](https://unity.com/blog/unity-cli-usage-data-ai-coding-agents)) |

**Pipeline built-in command surface** (`unity command <group> <name>`), from the [Pipeline manual](https://docs.unity3d.com/Packages/com.unity.pipeline@0.9/manual/index.html):
- **Capture:** `capture game_view`, `scene_view`, `screenshot`. `editor_element` and `runtime_element` need Unity 6000.7+.
- **Play mode:** `editor.playmode start|stop|pause|set_run_in_background`.
- **Console:** `console get|status|clear|write`.
- **Scene:** `scene create|open|save|get_hierarchy|add_to_build…`.
- **GameObject/component:** `gameobject create|create_many|find|set_transform|set_parent…` and `component add|remove|get_properties|set_properties|set_field`.
- **Prefab:** `prefab create|instantiate|create_variant|apply_overrides|revert_overrides|unpack`.
- **Material:** `material get_properties|set_properties|list_shaders`.
- **Tests:** `test list|run|status|cancel`.
- **Build:** `build run|set_profile…`, `build.target switch`.
- **Code:** `script eval|eval_file` (Roslyn), `script run` (compiles a .cs file in memory, no domain reload), `recompile`, and `[MethodReload]` hot-reload.
- **Custom commands** use `[CliGroupCommand]` and `[CliArg]`. Mutations are gated by `confirm`/`dry_run` and a path sandbox.
- **Connectivity** ([connectivity](https://docs.unity3d.com/Packages/com.unity.pipeline@0.9/manual/connectivity.html)): Editor ports are 7800–7849, the descriptor lives at `Library/Pipeline/.unity-pipeline-port`, and requests use a bearer token. **The token persists through domain reloads.** Detached jobs do *not* survive a reload. Clients should retry `503 retryable` responses, and should use `127.0.0.1` rather than `localhost`. An unfocused Editor with Run In Background off stops its player loop, but `capture game_view` still returns a frame.
- **`unity mcp`** exposes ~171 tools (~27.7k tokens of tool schema, as measured by a vendor ([StraySpark](https://www.strayspark.studio/blog/unity-mcp-vs-unity-cli-2026))). Since beta.14 you can trim the list with `unity mcp --tools "eval,capture_*"` / `--exclude-tools` ([release notes](https://docs.unity.com/unity-cli/release-notes)).

### 1.2 Community MCP servers

| | **CoplayDev / MCP for Unity** (formerly justinpbarnett/unity-mcp; that URL now resolves to CoplayDev) | **IvanMurzak / Unity-MCP** ("AI Game Developer") | **CoderGamester / mcp-unity** | **Unity CLI `unity mcp`** (official) |
|---|---|---|---|---|
| Repo | [github](https://github.com/CoplayDev/unity-mcp) | [github](https://github.com/IvanMurzak/Unity-MCP) | [github](https://github.com/CoderGamester/mcp-unity) | [docs](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli) |
| Stars / activity | **15.5k★**, 1.6k forks. **v10.3.0 on 2026-10-04.** 54 open issues, 68 open PRs | 4.5k★. **1.5.0 on 2026-10-24.** 46 open issues | 2.0k★. 1.5.0 (3 Sep). 4 open issues | Unity-maintained, beta |
| Unity versions | 2021.3 LTS – 6.x ([install](https://coplaydev.github.io/unity-mcp/getting-started/install)) | CI runs on 2022.3, 6000.0, **6000.3**, 6000.6 ([releases](https://github.com/IvanMurzak/Unity-MCP/releases)) | Unity 6+ | 6.0+ |
| Runtime deps | Python 3.10+ and `uv` | .NET server binary, auto-placed in `Library/mcp-server/` | Node 18+ | `unity` binary |
| Transport | **HTTP default `http://localhost:8080/mcp`**, or stdio (`uvx --from mcpforunityserver mcp-for-unity --transport stdio`) ([transports](https://coplaydev.github.io/unity-mcp/architecture/transports)) | stdio or streamable HTTP (port 8080). Optional token/OAuth | Node stdio server that talks to a Unity WebSocket on port 8090 (Basic auth token in `Library/McpUnity/bridge-token`) | stdio (`unity mcp`) |
| Scene / GO / components | `manage_scene`, `manage_gameobject`, `find_gameobjects`, `manage_components` | `scene-*`, `gameobject-*`, `gameobject-component-*` | `get/update_gameobject`, `update_component`, `create/load/save_scene`, transform ops | yes |
| Prefab / asset / material | `manage_prefabs`, `manage_asset`, `manage_material`, `manage_shader`, `manage_texture`, `manage_scriptable_object` | `assets-prefab-*`, `assets-material-create`, `assets-*`, `package-*` | `create_prefab`, `create/assign/modify_material`, `add_asset_to_scene` | yes |
| Console | `read_console` | `console-get-logs` | `get_console_logs`, `unity://logs` | yes |
| Play mode | via `manage_editor` (play/stop). Issue #1379 was a false "success" | `editor-application-set-state` | `get/set_play_mode_status` | yes |
| Screenshots | `manage_scene` screenshot (`include_image`, `camera`, `batch="surround"`, `look_at`) ([tools ref](https://glama.ai/mcp/servers/@CoplayDev/unity-mcp/blob/08b8eacab13da57ed13859167fa6033c6441f58d/unity-mcp-skill/references/tools-reference.md)) | `screenshot-game-view`, `-scene-view`, `-camera`, `-isolated` | **none** | `capture_*` |
| Tests | `run_tests` + `get_test_job` (async) | `tests-run` (Edit/Play) | `run_tests` | yes |
| Menu items | `execute_menu_item` | (via reflection-method-call) | `execute_menu_item` | – |
| Arbitrary C# | `execute_code` (optional group) | `script-execute` (Roslyn), `reflection-method-call` | – | `script eval` |
| Builds | `manage_build` | – | – | `build run` |
| Extras | 50 tools in 10 groups that you toggle with `manage_tools`. Profiler, URP/graphics, ProBuilder, Blender bridge, AI asset generation, multi-instance routing, CLI ([tools](https://coplaydev.github.io/unity-mcp/reference/tools)) | 70+ tools, a `unity-mcp-cli` for setup and skills generation, **runtime (in-game) MCP** | Smallest and simplest; batch-mode bridge is off by default | Same surface as `unity command` |
| Claude Code setup | Window → MCP for Unity → *Configure All Detected Clients*, or `claude mcp add --transport http unityMCP http://localhost:8080/mcp` | Window → AI Game Developer → *Auto-generate skills*, or `claude mcp add ai-game-developer -- "<proj>\Library\mcp-server\win-x64\gamedev-mcp-server.exe" port=8080 client-transport=stdio` ([README](https://github.com/IvanMurzak/Unity-MCP)) | Tools → MCP Unity → Server Window → *Configure Claude Code (Project)*, which writes `.mcp.json` pointing at `Library/PackageCache/...` | `unity mcp configure claude` |
| Known gotchas | Domain-reload churn and unfocused-Editor stalls on Windows (see §1.3) | **No spaces in project path.** Tool calls are never re-sent. Server exits after 600 s idle | Play Mode tests fail with "Connection failed" across a domain reload (workaround: disable Reload Domain). 10 s default timeout. Package-cache hash changes on update, so you must regenerate the config. No spaces in path | Experimental. Detached jobs lost on reload. Large tool list (trim it) |

Other options seen: **Coplay MCP** (the commercial sibling of CoplayDev: `claude mcp add --scope user --transport stdio coplay-mcp --env MCP_TOOL_TIMEOUT=720000 -- uvx --python ">=3.11" coplay-mcp-server@latest` ([guide](https://docs.coplay.dev/coplay-mcp/claude-code-guide.md))), **UnityCtl** (CLI: play mode, screenshot, logs, C# exec, .NET 10 ([repo](https://github.com/DirtybitGames/unityctl))), **Bluepuff71/UnityMCP** (native-plugin HTTP server that survives reloads ([glama](https://glama.ai/mcp/servers/@Bluepuff71/UnityMCP/blob/109ddc5bcc8f55d8a1246930f09245325cbeb690/Package/README.md))), UCP and Gerty (mentioned on [Unity Discussions](https://discussions.unity.com/t/cli-mcp-ai-ide-chatgpt-codex-cursor-antigravity-claude-code-windsurf-next-steps-for-unity-workflow-automation/1705679?page=2)), and **StraySpark** (paid, $69+, Windows-only, verified only on 6.5. Its blog posts are vendor content).

### 1.3 Cross-cutting limitations (all bridges)

- **Domain reload kills in-Editor sockets.** It happens on every script compile, on entering Play Mode (unless Enter Play Mode Options disables reload), and on package changes. The symptoms are "transport closed", timeouts, or the agent hanging ([StraySpark explainer](https://www.strayspark.studio/blog/unity-mcp-keeps-disconnecting-domain-reload), [Claude Code issue](https://claudeissues.com/issue/605-feature-request-claude-should-be-able-to-reconnect-reset-mcp-server)). CoplayDev's open issues as of Oct 2026 include #1394 (run_tests hangs or false-fails across a reload on Windows), #1454 (EditMode tests auto-fail at 15 s when the Editor is unfocused), #1491 (stdio commands stall 30 s+ when unfocused), #1437 (statics wiped on entering play), and #1164 (orphaned Python server) ([issues](https://github.com/CoplayDev/unity-mcp/issues?q=is%3Aissue+domain+reload)). Many similar issues were fixed in v10.0.2–v10.2.0 ([releases](https://github.com/CoplayDev/unity-mcp/releases)). The Pipeline keeps its token through reloads, but its jobs don't survive one.
- **Compile waits.** After writing C#, the agent must trigger or await a recompile and then read errors: `unity recompile [--strict]` (exit 7 if no Editor is reachable), `refresh_unity` (CoplayDev), or `recompile_scripts` (CoderGamester). The Unity MCP troubleshooting page says timeouts usually mean Unity is busy importing or compiling ([docs](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.0/manual/unity-mcp-troubleshooting.html)).
- **Unfocused Editor (Windows).** Editor throttling and a stopped player loop can stall commands and tests. Set *Preferences > General > Interaction Mode = No Throttling* ([manual](https://docs.unity3d.com/2022.3/Documentation/Manual/Preferences.html)) and turn on Run In Background (`editor.playmode set_run_in_background`).
- **Modal dialogs block everything.** Since beta.14 the CLI fails fast and names the dialog.
- **Large scenes and token cost.** Hierarchy dumps and tool schemas are big. Claude Code's MCP output defaults to a 25k-token limit (`MAX_MCP_OUTPUT_TOKENS`), and results over 50k characters are spilled to a file ([Claude Code MCP docs](https://code.claude.com/docs/en/mcp)). Query by path or name instead of dumping the whole hierarchy, and trim tool groups.
- **Batchmode vs the open Editor.** You *cannot* open the same project in batchmode while the Editor has it open ([CLI args](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/editor)). Use the live Editor (Pipeline/MCP) for interactive loops, and a **second checkout or git worktree** for batch builds and tests.
- **Semantic weaknesses** (all tools): serialized values, prefab overrides, spatial layout, lighting mood, and "game feel" stay weak ([StraySpark, 21 Oct 2026](https://www.strayspark.studio/blog/claude-code-unity-what-works-2026), [Classmethod test](https://dev.classmethod.jp/en/articles/unity-mcp-tps-game-claude-code-modification/)).

---

## 2. CLI / headless Unity

### 2.1 Installing Editors and modules

- **Unity CLI (new, preferred).** Install with `winget install Unity.CLI` or `irm https://unity.com/install.ps1 | iex` ([docs](https://docs.unity.com/en-us/unity-cli/use-unity-cli.md)). Then run `unity install 6.3 -m android ios --cm`. The minor-stream alias picks the newest 6.3, and `--cm` pulls the Android SDK/NDK and OpenJDK child modules. Use `unity install-modules -e <ver> -m android --cm` for an existing install and `unity install-modules -e <ver> -l` to list modules. `--format json` gives machine-readable output, `UNITY_NON_INTERACTIVE=1` disables prompts, and `UNITY_NO_ELEVATE` skips the UAC helper ([reference](https://docs.unity.com/en-us/unity-cli/unity-cli-reference.md)).
- **Hub CLI (legacy, deprecated since Hub 3.18.0).** `"C:\Program Files\Unity Hub\Unity Hub.exe" -- --headless install -v 6000.3.<x>f1 -c <changeset> -m android android-sdk-ndk-tools android-open-jdk ios` and `... -- --headless install-modules -v <ver> -m ios --cm`. Module IDs: `android`, `android-sdk-ndk-tools`, `android-open-jdk`, `ios`, `windows-il2cpp`, `webgl` ([Hub CLI ref](https://docs.unity.com/en-us/hub/hub-cli-reference.md)). Modules can only be added to Hub- or CLI-installed Editors.
- iOS Build Support on Windows only *exports an Xcode project*. Signing and archiving need a Mac or cloud macOS runner (see `05-github-cicd.md`).

### 2.2 Unity CLI project commands

`unity build` (batch mode, Android signing flags, `--list-targets`), `unity test` (Edit and Play Mode, writes an NUnit/JUnit report, `--affected`, `--rerun-failed`, `--shard`), `unity recompile [--strict]`, `unity run --command`, `unity watch test`, `unity status`, `unity logs`, and `unity doctor`. **Exit codes:** 0 ok, 1 error, 2 usage, 3 auth, 4 config, **6 = run didn't finish (retryable: compile error, license, crash, timeout)**, 7 = service or Editor unreachable (retryable), **8 = tests ran and failed (don't retry)** ([reference](https://docs.unity.com/en-us/unity-cli/unity-cli-reference.md)). This is much more agent-friendly than raw Unity.exe.

### 2.3 Raw `Unity.exe` batchmode (stable fallback, no beta dependency)

Key flags ([CLI args 6000.3](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/editor), [build from CLI](https://docs.unity.com/en-us/engine/6000.3/manual/building-and-publishing/build-command-line), [test CLI](https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/test-framework-introduction/reference-command-line)):
- `-batchmode` suppresses dialogs. Any exception exits with code 1.
- `-nographics` skips GPU init. **Don't use it for PlayMode tests or screenshots that need rendering.** Logs are off unless you pass `-logFile`.
- `-quit` must **not** be combined with `-runTests`.
- `-executeMethod NS.Class.Method` runs a static method from an `Editor` folder. Signal failure with an exception or `EditorApplication.Exit(n)`.
- `-logFile -` sends the log to stdout, or `-logFile path`.
- `-buildTarget Android|iOS`, or `-activeBuildProfile <asset>`. Pass one of them every time; you can build only one target per process. `SwitchActiveBuildTarget*` doesn't work mid-script in batchmode.
- `-accept-apiupdate`.
- Tests: `-runTests -testPlatform EditMode|PlayMode|<BuildTarget> -testResults <xml> -testFilter "A;!B" -testCategory -assemblyNames -runSynchronously` (EditMode only). Results are **NUnit XML**. Exit codes from test runs aren't formally specified, so parse the XML.

```powershell
# tools/run-tests.ps1  (run against a SEPARATE worktree if the Editor is open)
$U="C:\Program Files\Unity\Hub\Editor\6000.3.<x>f1\Editor\Unity.exe"; $P="F:\GameDev\aquarium-ci"
& $U -batchmode -projectPath $P -runTests -testPlatform EditMode -testResults "$P\Logs\editmode.xml" -logFile "$P\Logs\editmode.log" | Out-Null
"exit=$LASTEXITCODE"; Select-Xml -Path "$P\Logs\editmode.xml" -XPath "/test-run" | % { $_.Node.result, $_.Node.passed, $_.Node.failed }
```

```csharp
// Assets/Editor/Agent/BuildScript.cs
using System; using System.Linq; using UnityEngine; using UnityEditor; using UnityEditor.Build.Reporting;
public static class BuildScript {
  static string[] Scenes => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
  static string Arg(string n, string d) { var a = Environment.GetCommandLineArgs(); var i = Array.IndexOf(a, n); return i >= 0 && i + 1 < a.Length ? a[i + 1] : d; }
  public static void Android() => Run(BuildTarget.Android, Arg("-out", "Builds/Android/aquarium.apk"));
  public static void IOS()     => Run(BuildTarget.iOS,     Arg("-out", "Builds/iOS"));   // Xcode project
  static void Run(BuildTarget t, string path) {
    var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, locationPathName = path, target = t, options = BuildOptions.None });
    Debug.Log($"[AGENT] build {r.summary.result} size={r.summary.totalSize} errors={r.summary.totalErrors}");
    if (Application.isBatchMode) EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
  }
}
```
Run with `Unity.exe -batchmode -quit -projectPath <p> -buildTarget Android -executeMethod BuildScript.Android -logFile Logs\build.log`. (The snippet is illustrative and has not been compiled.)

### 2.4 Licensing in batchmode (Personal)

Unity says the command-line activation procedures (`-serial`/`-username`/`-password`, `-returnlicense`) **don't apply to Personal**. You activate Personal by **logging in to Unity Hub** ([licensing](https://docs.unity.com/en-us/engine/6000.3/manual/get-started/install-and-upgrade/licenses-and-activation/managing-your-unity-license)). In practice, local batchmode reuses the license already activated on the machine via Hub, so just keep Hub signed in. The Unity CLI also has `unity auth login` and `unity license` (list/activate/return) and supports service-account env vars for CI. I did not verify whether `unity license activate` works for Personal on headless CI; treat Personal-on-CI as a separate item for `05-github-cicd.md`. Personal requires under $200K revenue/funding over the last 12 months ([plans](https://unity.com/products)).

---

## 3. How the agent "sees" the game

| Channel | How | Notes |
|---|---|---|
| Editor screenshots | `unity command capture game_view` / `scene_view` / `screenshot` (Pipeline). CoplayDev `manage_scene` screenshot with `include_image=true` and `max_resolution`. Ivan `screenshot-game-view`/`-camera` | Ask for small resolutions (512–768 px) to save tokens. Multi-angle capture (CoplayDev `batch="surround"`) helps with dioramas. Claude Code can `Read` a PNG directly, so a file path works as well as inline base64 |
| Play-mode capture in code | `ScreenCapture.CaptureScreenshot("Logs/shot.png", superSize)` captures the final composed frame. Relative paths resolve to the project root in the Editor and to `persistentDataPath` on mobile ([API](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/screencapture/capturescreenshot)). `CaptureScreenshotAsTexture` and `...IntoRenderTexture` exist for in-memory comparisons | Call it at end of frame (coroutine `WaitForEndOfFrame`). Build a menu item or Pipeline command that enters play, waits N seconds or frames, captures, and exits |
| Logs | Editor log at `%LOCALAPPDATA%\Unity\Editor\Editor.log`. Player log at `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\Player.log`. Android uses `adb logcat` ([log files](https://docs.unity.com/en-us/engine/6000.3/manual/get-started/project-configuration/log-files)). Live console via `console get`, `read_console`, or `get_console_logs` | Prefix agent-relevant logs with `[AGENT]` so the agent can grep for them. Batch logs go to `-logFile` |
| Tests | Unity Test Framework EditMode (pure sim logic: economy, agent AI, save/load) and PlayMode (spawn N agents, step time, assert invariants, capture a screenshot) | This is the most reliable verification signal. Bridges' screenshot self-assessment diverged from human judgment in the [Classmethod test](https://dev.classmethod.jp/en/articles/unity-mcp-tps-game-claude-code-modification/) |
| Runtime state | Pipeline Runtime server (`Runtime Pipeline Manager` component, dev builds only) lets the agent read and alter a running build. Ivan's Unity-MCP has runtime MCP | Strip it from release builds |

---

## 4. Recommended setup (Windows 11, Unity 6.3 LTS, Claude Code)

**Stack:**
1. **Unity CLI + `com.unity.pipeline` + Unity's Claude Code plugin** for live Editor control through the shell. Unity says this is faster and uses fewer tokens than MCP.
2. **CoplayDev MCP for Unity** (HTTP, pinned tag) as the MCP fallback and for its extras (screenshots with `include_image`, URP/graphics, profiler). Enable it only when needed to save context.
3. **Raw batchmode PowerShell scripts** on a second worktree for deterministic tests and builds that don't depend on a beta.
4. **Project conventions**: CLAUDE.md, an editor utility folder, and git.

**Steps:**
1. Prerequisites: git (+LFS), Unity Hub (signed in, Personal activated), and Claude Code. Install the CLI with `winget install Unity.CLI`, then run `unity --version`, `unity auth login`, and `unity doctor`.
2. Install the Editor with `unity install 6.3 -m android ios --cm`. Create the project with `unity projects create Aquarium --no-cloud` (this adds Pipeline by default since beta.14), or run `unity pipeline install` in an existing project. Verify with `unity pipeline list` and `unity status`. **Use a project path without spaces** (e.g. `F:\GameDev\aquarium`).
3. Install the plugin inside `claude`: `/plugin marketplace add Unity-Technologies/unity-agent-plugin`, then `/plugin install unity@unity-agent-plugin`, then `unity skill install claude` ([docs](https://docs.unity.com/en-us/ai/unity-plugin/claude-code.md)). Optionally add MCP mode with a trimmed tool list: `unity mcp configure claude` (and edit the config to add `--tools`).
4. Optional CoplayDev: in Package Manager, add `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.3.0` (requires Python 3.10+ and `uv`: `winget install astral-sh.uv`). Use Window → MCP for Unity → Start (HTTP). Then run `claude mcp add --transport http --scope project unityMCP http://localhost:8080/mcp` and confirm with `claude mcp list`. Use `/mcp` → Reconnect after a crash.
5. Editor settings: Preferences → Interaction Mode = **No Throttling**. Player → Run In Background = on (dev). *Enter Play Mode Options*: keep domain reload **on** at first (correctness over speed). Only disable it once statics are reset via `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`.
6. Add `Assets/Editor/Agent/`: `BuildScript.cs`; `AgentCapture.cs` (a menu item or `[CliGroupCommand]` that enters play, waits, captures `Logs/agent/*.png`, and exits); `SceneValidator.cs` (missing refs, default materials, agent counts, draw calls); and a `TestUtilities` asmdef. Add `tools/*.ps1` for test/build/log-tail.
7. **CLAUDE.md / AGENTS.md** should include:
   - Unity version and the CLI and Pipeline versions.
   - "Prefer `unity command` over MCP."
   - "After any C# edit: `unity recompile --strict`, then `unity command console get`. Don't proceed with errors."
   - "Verify visuals with `capture game_view` at ≤768 px."
   - "Never edit `.unity`/`.prefab` YAML by hand when a command exists."
   - "Never touch `ProjectSettings/` or `Packages/manifest.json` without asking."
   - "Commit before large scene ops."
   - "Tests: `unity test` (exit 8 = real failure)."
   - Folder map, naming rules, and the asmdef layout.
8. Git hygiene: `unity vcs doctor --fix`, `unity vcs merge-setup` (UnityYAMLMerge), and a Unity `.gitignore`. Make small commits per agent task.

**Risks:**
- The CLI and Pipeline are **beta/experimental**. Flags change (beta.14 broke `--timeout 30s`), so pin versions with `unity self-update --target`. Note that Hub and MSIX auto-update the CLI, and you can't disable that ([docs](https://docs.unity.com/en-us/unity-cli/use-unity-cli.md)).
- `eval` and `execute_code` amount to local remote code execution. Keep them localhost-only and keep the descriptor token private.
- Skills edit assets directly and may not be undoable from the Editor, so use git ([plugin repo](https://github.com/Unity-Technologies/unity-agent-plugin)).
- Running two bridges (Pipeline and CoplayDev) adds domain-reload time (CoplayDev #1336 measured ~9 s, since fixed). Disable whichever you're not using.
- Token cost: the full `unity mcp` schema is ~28k tokens.
- Agents' visual judgment is unreliable, so keep a human review pass for look and feel.

---

## 5. Real-world reports

- **Classmethod (Mar 2026)**: Unity 6000.3.10f1, the deprecated official MCP 2.0.0-pre.1, Claude Code with Opus 4.6, on Win11. Layout, lighting, and a custom `[McpTool]` worked. Problems: default materials, blocked doorways, wrong scale, Static flags off, and stacked darkening. The agent misjudged its own capture as "black", and it never moved the camera inside the room. Conclusion: human-reviewed, not autonomous ([article](https://dev.classmethod.jp/en/articles/unity-mcp-tps-game-claude-code-modification/)).
- **Void Balls (solo, 10 days, Mar 2026)**: Claude Code plus Unity MCP inspected scenes, component values, and play mode, which ended copy-pasting logs. The game shipped with 173 scripts (~29k LOC) and 88 test files, after writing a GDD first ([devlog](https://loopedingames.itch.io/void-balls/devlog/1464676/void-balls-is-live-how-one-dev-ai-built-a-roguelite-in-10-days)).
- **Unity Discussions thread (Mar–Jul 2026)**: Coplay MCP has "a decent amount of friction". One user said "the CLI approach is much faster" than MCP, including for screenshot analysis. Users found AGENTS.md, a project `Tools/` folder, and a `Docs/wiki` memory valuable. Too many skills sometimes made results worse. Claude/Opus credit burn was a complaint ([thread](https://discussions.unity.com/t/cli-mcp-ai-ide-chatgpt-codex-cursor-antigravity-claude-code-windsurf-next-steps-for-unity-workflow-automation/1705679?page=2)).
- **StraySpark (vendor, 21 Oct 2026)**: C# authoring works well. Serialized values, prefab instances, Play Mode, and spatial work are weak. A Reddit and r/unity complaint (14 Oct) says the CLI lacked scene editing and compile checks; that's secondhand, and Pipeline now has `scene`/`gameobject`/`recompile` ([post](https://www.strayspark.studio/blog/claude-code-unity-what-works-2026)).
- **Unity CLI telemetry (22 Oct 2026)**: 226k users, 200M+ events, and `unity command` ~124M runs. Claude Code is the #1 agent ([blog](https://unity.com/blog/unity-cli-usage-data-ai-coding-agents)).
- **GitHub issues** show that the main breakages in practice are domain reload, unfocused Editor, and test-runner locks (CoplayDev, listed in §1.3).

*Unverified or uncertain:* the exact capture output format of Pipeline commands (the capture reference page returned 404); whether `unity license` can activate Personal headlessly; the Unity AI subscription price after the trial (a $10/mo figure was removed from the pricing page in July 2026, per [pricingsaas](https://pricingsaas.com/companies/unity3d/diffs/2026W31)).
