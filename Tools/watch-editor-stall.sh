#!/usr/bin/env bash
# Watches a delegated Editor pass from the orchestrator. Run it in the background whenever an agent holds
# .agent-editor.lock: it exits, which wakes the orchestrator, as soon as the work ends or stops moving.
# Usage: bash Tools/watch-editor-stall.sh [quiet_seconds=600] [max_seconds=14400]
# Exit: 0 lock released, 3 stalled (lock held, nothing moving), 4 hit max_seconds.
quiet=${1:-600}
max=${2:-14400}
cd "$(dirname "$0")/.." || exit 1
log="$LOCALAPPDATA/Unity/Editor/Editor.log"
start=$(date +%s)
last=$start
prev=""
while :; do
  now=$(date +%s)
  if [ ! -f .agent-editor.lock ]; then
    echo "DONE: lock released after $(( (now - start) / 60 )) min"
    exit 0
  fi
  status=$(UNITY_NO_PAGER=1 UNITY_NO_BANNER=1 unity command test status --project-path 'F:\GameDev\AquariumCiv' --timeout 30 2>&1 | tail -1 | cut -c1-120)
  print="$status"
  # The log only proves progress while a run is going: an idle Editor still writes to it now and then.
  # A 504 means a blocking EditMode run has the Editor busy, so it counts as a run going, without its text.
  case "$status" in
    *running*) print="running $(wc -c < "$log" 2>/dev/null)" ;;
    *completed*) ;;
    *) print="busy $(wc -c < "$log" 2>/dev/null)" ;;
  esac
  if [ "$print" != "$prev" ]; then
    prev="$print"
    last=$now
  fi
  if [ $(( now - last )) -ge "$quiet" ]; then
    echo "STALL: lock held, nothing moved for $(( (now - last) / 60 )) min. Last status: $status"
    exit 3
  fi
  if [ $(( now - start )) -ge "$max" ]; then
    echo "TIMEOUT: still going after $(( max / 60 )) min. Last status: $status"
    exit 4
  fi
  sleep 60
done
