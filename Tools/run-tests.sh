#!/usr/bin/env bash
# Batched Unity test runner for an open Editor (docs/tooling.md, 2026-11-04 entries).
#
#   bash Tools/run-tests.sh "mode|filter|filter_type|tag" ["mode|filter|filter_type|tag" ...]
#
#   mode        editmode | playmode
#   filter      partial test name, an assembly name, or a category (one per run: no "|" or "," lists)
#   filter_type empty (name), assembly, or category
#   tag         short name for the output files (defaults to the filter)
#
# Examples:
#   bash Tools/run-tests.sh "editmode|AquariumCiv.Game.Sim.Tests|assembly|sim"
#   bash Tools/run-tests.sh "playmode|TelemetryPlayTests||telemetry" "playmode|SmokePlay|category|smoke"
#
# Per run: waits for `unity status` = ready, starts `test run --async_tests true`, polls `test status` until it
# has seen `running` and then `completed` (or a status that changed from the one before the run, for a run that
# ends inside the first poll), and prints the summary plus every failed test. Every wait is bounded:
# READY_WAIT_S (default 300 s) for the Editor and RUN_LIMIT_S (default 2400 s) per run.
# EXPLICIT=1 also runs [Explicit] tests (shot classes). A filter that matches 0 tests gives up after 90 s.
# Stall guard (2026-11-12: a PlayMode run sat "running" for 76 min while the Editor died and restarted under it):
# while a run is "running", the Editor log must grow at least once every STALL_S (default 180 s), and the Editor's
# pid must not change. Either failure stops the WHOLE batch with exit 3 and a STALLED line in summary.txt, so a
# background run always wakes the caller within a few minutes of anything going wrong.
# Output: $OUT (default AquariumCiv/Temp/agent/test-runs)/<tag>.status.json, and a line per run in summary.txt.
set -u
export UNITY_NO_PAGER=1 UNITY_NO_BANNER=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="${PROJ:-F:/GameDev/AquariumCiv}"
OUT="${OUT:-$PROJ/Temp/agent/test-runs}"
READY_WAIT_S="${READY_WAIT_S:-300}"
RUN_LIMIT_S="${RUN_LIMIT_S:-2400}"
POLL_S="${POLL_S:-10}"
STALL_S="${STALL_S:-180}"
LOG="${LOCALAPPDATA:-}/Unity/Editor/Editor.log"
editor_pid() { timeout 60 unity status --project-path "$PROJ" --format json 2>/dev/null | grep -oE '"pid": *[0-9]+' | head -1 | grep -oE '[0-9]+'; }
log_size() { wc -c < "$LOG" 2>/dev/null || echo 0; }
stalled() { echo "== $1: STALLED ($2) - batch stopped" | tee -a "$OUT/summary.txt"; echo "$st" > "$OUT/$1.status.json"; exit 3; }
mkdir -p "$OUT"

wait_ready() {
  local waited=0
  while [ "$waited" -lt "$READY_WAIT_S" ]; do
    if timeout 60 unity status --project-path "$PROJ" --format json 2>/dev/null | grep -q '"ready"'; then
      sleep 5; return 0
    fi
    sleep 5; waited=$((waited + 5))
  done
  echo "!! Editor not ready after ${READY_WAIT_S}s (run Tools/unity-dialogs/dismiss-dialogs.ps1)"; return 1
}

state_of() { grep -oiE '"(state|status)": *"[a-z]+"' | head -1 | sed -E 's/.*: *"([a-z]+)"/\1/I'; }

run_one() {
  local mode=$1 filter=$2 ftype=$3 tag=$4
  local args=(--mode "$mode" --filter "$filter" --async_tests true)
  [ -n "$ftype" ] && args+=(--filter_type "$ftype")
  [ "${EXPLICIT:-0}" = 1 ] && args+=(--include_explicit true)
  wait_ready || { echo "== $tag: SKIPPED (Editor not ready)" | tee -a "$OUT/summary.txt"; return 1; }
  local before; before=$(timeout 60 unity command test status --format json 2>/dev/null | md5sum | cut -c1-12)
  local started=0
  for attempt in 1 2 3 4 5 6; do   # the Pipeline can be gone ~30 s after a recompile
    if timeout 60 unity command test run "${args[@]}" > "$OUT/$tag.start.txt" 2>&1; then started=1; break; fi
    sleep 10
  done
  [ $started = 1 ] || { echo "== $tag: could not start ($(tail -c 300 "$OUT/$tag.start.txt"))" | tee -a "$OUT/summary.txt"; return 1; }
  local seen=0 st="" s="" t=0
  local pid0; pid0=$(editor_pid)
  local lsz; lsz=$(log_size)
  local lmove=0
  while [ "$t" -lt "$RUN_LIMIT_S" ]; do
    sleep "$POLL_S"; t=$((t + POLL_S))
    st=$(timeout 60 unity command test status --format json 2>/dev/null)
    s=$(echo "$st" | state_of)
    [ "$s" = "running" ] && seen=1
    local pid; pid=$(editor_pid)
    if [ -n "$pid0" ] && [ -n "$pid" ] && [ "$pid" != "$pid0" ]; then stalled "$tag" "Editor restarted: pid $pid0 -> $pid after ${t}s"; fi
    local nsz; nsz=$(log_size)
    if [ "$nsz" != "$lsz" ]; then lsz=$nsz; lmove=$t; fi
    if [ "$s" = "running" ] && [ $((t - lmove)) -ge "$STALL_S" ]; then
      stalled "$tag" "Editor log silent for $((t - lmove))s while running; check dialogs: Tools/unity-dialogs/dismiss-dialogs.ps1"
    fi
    if echo "$s" | grep -qiE '^(completed|finished|done)$'; then
      [ $seen = 1 ] && break
      # ended inside the first poll: trust it only once the status differs from the one before the run
      [ "$(echo "$st" | md5sum | cut -c1-12)" != "$before" ] && [ "$t" -ge 20 ] && break
      # still the old "completed" a minute on: the filter most likely matched 0 tests (an [Explicit] class needs EXPLICIT=1)
      if [ "$t" -ge 90 ]; then echo "!! $tag: no run started in 90 s (0 tests matched? [Explicit] needs EXPLICIT=1)"; break; fi
    fi
  done
  echo "$st" > "$OUT/$tag.status.json"
  if [ "$t" -ge "$RUN_LIMIT_S" ]; then stalled "$tag" "still '$s' after ${RUN_LIMIT_S}s"; fi
  python -I - "$OUT/$tag.status.json" "$tag" <<'PY' | tee -a "$OUT/summary.txt"
import json, sys
path, tag = sys.argv[1], sys.argv[2]
try:
    r = json.load(open(path, encoding="utf-8"))["data"]["result"]
except Exception as e:
    print(f"== {tag}: unreadable status ({e})"); sys.exit()
s = r.get("summary", {})
print(f"== {tag}: {r.get('status')} {s}")
for t in r.get("results", []):
    if t.get("Status") == "Failed":
        msg = (t.get("Message") or "")[:300].replace("\n", " ")
        print(f"  FAIL {t.get('FullName')} :: {msg}")
PY
}

[ $# -gt 0 ] || { sed -n 2,20p "$0"; exit 2; }
for spec in "$@"; do
  IFS='|' read -r mode filter ftype tag <<< "$spec"
  run_one "$mode" "$filter" "${ftype:-}" "${tag:-$filter}"
done
echo ALL-DONE
