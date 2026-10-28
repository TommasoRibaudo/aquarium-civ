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
- Run `unity` from **Bash** (not PowerShell): PowerShell splits quoted C# into multiple args.
  Set `UNITY_NO_PAGER=1 UNITY_NO_BANNER=1`.
- `eval` C#: use `UnityEngine.Object` (plain `Object` is ambiguous).
- Screenshots: `unity command screenshot --view scene|game --output F:/GameDev/AquariumCiv/Temp/agent/<name>.png`.
  (`capture scene_view/game_view --save_path` writes under `Assets/` — avoid.)
- Console: `unity command console get --tail N --format json`.
- If commands can't connect: check Safe Mode (compile errors) with `unity pipeline list`. Don't loop retries or
  fall back to editing YAML — report to the user.

## Loop after every C# change
1. `unity recompile --project-path F:\GameDev\AquariumCiv` → no compile errors.
2. `unity command console get` → no new errors/warnings.
3. Visual change? Take a screenshot and look at it. The user is the final judge of look & feel.
4. Logic change? Add or run tests (`unity test`, exit 8 = real failures).

## Conventions
- Commit before large scene/prefab changes.
- Editor helper tools live in `Assets/Editor/Agent/`.
- Free assets only for now (KayKit CC0, Kenney CC0). Record source + license of every imported pack in `docs/assets-log.md`.
- Unity CLI is beta: don't run `unity self-update`, `unity bug`, or plugin installs without asking.
