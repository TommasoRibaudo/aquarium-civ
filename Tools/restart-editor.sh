#!/usr/bin/env bash
# Restart the Unity Editor between stints (docs/workflow.md §4). Only while you hold .agent-editor.lock,
# out of play mode. Saves open scenes, quits, reopens the project, waits for `ready`. Never kill Unity by hand.
# Safe to run when the Editor is already closed: it just opens it.
set -u
P="${PROJ:-F:/GameDev/AquariumCiv}"
# A second Editor on a worktree: PROJ=F:/GameDev-wt/<dir>/AquariumCiv EDITOR_LOG=<its own log file>.
PMATCH=$(echo "$P" | sed 's#[/\]#.#g')
EXE="/c/Program Files/Unity/Hub/Editor/6000.3.27f1/Editor/Unity.exe"
export UNITY_NO_PAGER=1 UNITY_NO_BANNER=1

# Only the real Editor for this project counts: the Hub keeps its own "unity.exe serve" running all the time.
editor_up() {
  powershell -NoProfile -Command "@(Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Where-Object { \$_.CommandLine -match 'Hub.Editor' -and \$_.CommandLine -notmatch 'batchMode' -and \$_.CommandLine -match '$PMATCH' }).Count" | tr -d '\r' | grep -qv '^0$'
}

if editor_up; then
  # Save only scenes that have a file: SaveOpenScenes() on an untitled scene (AnimLabLive's empty play scene, left open when a live
  # run is stopped mid-way) opens a Save-As dialog that waits for the user (2026-11-15: saved as "Assets/temp file.unity").
  CODE='for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) { var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i); if (sc.isDirty && !string.IsNullOrEmpty(sc.path)) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc); } UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.EditorApplication.Exit(0); return "quitting";'
  unity command script eval --project-path "$P" --timeout 30 --code "$CODE" || echo "(an error reply is normal: the Editor quits mid-call)"
  for i in $(seq 1 30); do editor_up || break; sleep 5; done
  if editor_up; then echo "Editor did not quit in 150 s: stop and report"; exit 1; fi
fi

if [ -n "${EDITOR_LOG:-}" ]; then "$EXE" -projectpath "$P" -logFile "$(cygpath -w "$EDITOR_LOG" 2>/dev/null || echo "$EDITOR_LOG")" >/dev/null 2>&1 &
else "$EXE" -projectpath "$P" >/dev/null 2>&1 &
fi
for i in $(seq 1 60); do
  if unity status --project-path "$P" --format json 2>/dev/null | grep -q '"state": "ready"'; then echo "Editor ready"; exit 0; fi
  sleep 10
done
echo "Editor not ready after 10 min: check dismiss-dialogs.ps1 and Editor.log, stop and report"
exit 1
