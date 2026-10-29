# Aquarium Civ — agent working rules

Mobile (iOS/Android) idle civilization builder: a tiny 3D civ in a glass terrarium.
Design and research docs: `docs/` (start at `docs/README.md`; decisions in `docs/decisions.md`).

## Layout
- `F:\GameDev` = git root (private GitHub repo `aquarium-civ`, Git LFS for binaries)
- `AquariumCiv/` = Unity project, **Unity 6000.3.27f1 (6.3 LTS)**, URP, `com.unity.pipeline` installed
- `docs/` = research, decisions, design

## Driving the Editor (Unity CLI + Pipeline)
- Before any scene/prefab/asset edit: `unity status --project-path F:\GameDev\AquariumCiv` → state must be `ready`.
- **Never hand-edit `.unity` / `.prefab` / `.asset` YAML while an Editor is reachable.** Use `unity command ...` or `eval`.
- Discover commands with `unity command` / `unity command --tag <tag>`; don't guess names.
- Full Pipeline command reference: `AquariumCiv/Library/PackageCache/com.unity.pipeline@*/Documentation~/commands/*.md`.
- Unity's Claude Code plugin (`unity@claude-plugins-official`) is installed user-wide: use its `unity:*` skills
  (unity-cli, urp-postprocessing, ui, build-live-game, ...). Don't install a second local copy of `unity-cli`.
- Run `unity` from **Bash** (not PowerShell): PowerShell splits quoted C# into multiple args.
  Set `UNITY_NO_PAGER=1 UNITY_NO_BANNER=1`.
- `eval` C#: use `UnityEngine.Object` (plain `Object` is ambiguous).
- Screenshots: `unity command screenshot --view scene|game --output F:/GameDev/AquariumCiv/Temp/agent/<name>.png`.
  (`capture scene_view/game_view --save_path` writes under `Assets/` — avoid.)
- Console: `unity command console get --tail N --format json`.
- If commands can't connect: check Safe Mode (compile errors) with `unity pipeline list`. Don't loop retries or
  fall back to editing YAML — report to the user.
- Tooling facts and past fixes: `docs/tooling.md`. Read the relevant section before fighting a tool. **Append a dated
  entry the moment you solve a tooling problem.**

## Never let a Unity call hang silently (user rule — super important)
Every time something runs in Unity (open, recompile, eval, tests, builds, imports, installs):
- Short calls: pass `--timeout 60` (or less) so they can't block for more than a minute.
- Anything that may take longer: run it in the background, then **check in after 1 minute**:
  `unity status --project-path F:\GameDev\AquariumCiv --format json` (state, compiling/importing),
  `unity command console get --tail 10`, and the Editor log (`unity logs` / `%LOCALAPPDATA%\Unity\Editor\Editor.log`).
- **A stalled call is usually a modal dialog.** Check first:
  `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/unity-dialogs/dismiss-dialogs.ps1` (add `-Click` to answer
  the safe ones; unknown dialogs → see `docs/tooling.md` §1, or ask).
- Keep checking every minute until it finishes. If it looks stuck (no progress, state not changing, modal dialog,
  Editor gone), stop and tell the user what is stuck — don't wait silently or retry blindly.

## Loop after every C# change
1. `unity recompile --project-path F:\GameDev\AquariumCiv` → no compile errors.
2. `unity command console get` → no new errors/warnings.
3. Visual change? Take a screenshot and look at it. The user is the final judge of look & feel.
4. Logic change? Add or run tests (`unity test`, exit 8 = real failures). Run only the affected assembly/class;
   check the test count isn't 0.
5. Visual, UI, interaction or gameplay change? Run the **`tester`** sub-agent (`.claude/agents/tester.md`) with a
   brief: what changed, what to play, what to watch for, a pass list, which tests. Don't touch the Editor while it
   runs. Fix its **[real]** findings before calling the work done; list **[polish]** ones for the user to decide.
   Always run it before a prototype goes to human testers.

## Conventions
- Commit before large scene/prefab changes.
- **Debug panel stays current:** whenever a prototype adds a resource, mess type, event, variant or other testable
  state, add a cheat for it to the debug panel (give, spawn, skip to, toggle) in the same change, so you, the tester
  and the user can reach it without waiting.
- Judge player-facing work against `docs/game-design-practices.md` (the design docs still win).
- Editor helper tools live in `Assets/Editor/Agent/`.
- Free assets only for now (KayKit CC0, Kenney CC0). Record source + license of every imported pack in `docs/assets-log.md`.
- Unity CLI is beta: don't run `unity self-update`, `unity bug`, or plugin installs without asking.
