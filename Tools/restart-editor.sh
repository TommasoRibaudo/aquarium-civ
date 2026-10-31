#!/usr/bin/env bash
# Restart the Unity Editor between stints (docs/workflow.md §4). Only while you hold .agent-editor.lock,
# out of play mode. Saves open scenes, quits, reopens the project, waits for `ready`. Never kill Unity by hand.
# Safe to run when the Editor is already closed: it just opens it.
set -u
P='F:\GameDev\AquariumCiv'
EXE="/c/Program Files/Unity/Hub/Editor/6000.3.27f1/Editor/Unity.exe"
export UNITY_NO_PAGER=1 UNITY_NO_BANNER=1

# Only the real Editor for this project counts: the Hub keeps its own "unity.exe serve" running all the time.
editor_up() {
  powershell -NoProfile -Command "@(Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Where-Object { \$_.CommandLine -match 'Hub.Editor' -and \$_.CommandLine -notmatch 'batchMode' }).Count" | tr -d '\r' | grep -qv '^0$'
}

if editor_up; then
  CODE='UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes(); UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.EditorApplication.Exit(0); return "quitting";'
  unity command script eval --timeout 30 --code "$CODE" || echo "(an error reply is normal: the Editor quits mid-call)"
  for i in $(seq 1 30); do editor_up || break; sleep 5; done
  if editor_up; then echo "Editor did not quit in 150 s: stop and report"; exit 1; fi
fi

"$EXE" -projectpath "$P" >/dev/null 2>&1 &
for i in $(seq 1 60); do
  if unity status --project-path "$P" --format json 2>/dev/null | grep -q '"state": "ready"'; then echo "Editor ready"; exit 0; fi
  sleep 10
done
echo "Editor not ready after 10 min: check dismiss-dialogs.ps1 and Editor.log, stop and report"
exit 1
