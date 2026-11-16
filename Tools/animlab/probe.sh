#!/usr/bin/env bash
# Tools/animlab/probe.sh (#692 p3) <request.json> <probe.txt> : enter play (AnimLabLive), run the request's setup steps, then each probe line (C#, one per line;
# a line "WAIT n" sleeps n s), then exit play.
set -u
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
export UNITY_NO_PAGER=1 UNITY_NO_BANNER=1 UNITY_PROJECT_PATH="${PROJ:-$(pwd)/AquariumCiv}"
PY=/c/Python311/python
ueval() {
  local raw; raw="$(timeout 75 unity command script eval --code "$1" --timeout 60 --format json 2>&1)"
  $PY -c '
import json, sys
raw = sys.stdin.read()
try: d = json.loads(raw[raw.index("{"):])
except Exception: print("ERROR: " + raw.strip()[-300:]); sys.exit(0)
r = ((d.get("data") or {}).get("result") or {})
if d.get("success") and r.get("success", True): print(r.get("result"))
else: print("ERROR: " + json.dumps(d.get("errors") or r.get("diagnostics") or d)[:600])
' <<<"$raw"
}
mkdir -p AquariumCiv/Temp/agent/animlab
echo "enter: $(ueval 'return AquariumCiv.EditorTools.AnimLabLive.EnterPlay();')"
for i in $(seq 1 30); do sleep 2; [ "$(ueval 'return UnityEditor.EditorApplication.isPlaying ? "yes" : "no";')" = yes ] && break; done
echo "load: $(ueval 'return AquariumCiv.EditorTools.AnimLabLive.Load(4242, 1);')"
for i in $(seq 1 45); do sleep 2; s="$(ueval 'return AquariumCiv.EditorTools.AnimLabLive.Status();')"; echo "$s" | grep -q '^ready' && break; done
echo "status: $s"
while IFS= read -r stepq; do
  step="$($PY -c 'import json,sys; print(json.loads(sys.argv[1]))' "$stepq")"
  echo "setup: $(ueval "$step" | cut -c1-400)"; sleep 2
done < <($PY Tools/animlab/prep.py steps "$1")
while IFS= read -r line; do
  [ -z "$line" ] && continue
  case "$line" in WAIT*) sleep ${line#WAIT }; continue;; esac
  echo "probe: $(ueval "$line")"
done < "$2"
echo "exit: $(ueval 'return AquariumCiv.EditorTools.AnimLabLive.ExitPlay();')"
for i in $(seq 1 30); do sleep 2; [ "$(ueval 'return UnityEditor.EditorApplication.isPlaying ? "yes" : "no";')" = no ] && break; done
echo "finish: $(ueval 'return AquariumCiv.EditorTools.AnimLabBuilder.Finish();')"
