#!/usr/bin/env bash
# Editor health gate (docs/workflow.md §4). Run before taking .agent-editor.lock.
# Exit 0 = healthy, 1 = restart the Editor (Tools/restart-editor.sh), 2 = C: too full: tell the user.
LOG="${LOCALAPPDATA:-$HOME/AppData/Local}/Unity/Editor/Editor.log"
LIMIT_MS=${EDITOR_RELOAD_LIMIT_MS:-20000}
MIN_C_GB=${MIN_C_GB:-5}
status=0
last=$(grep -a "Domain Reload Profiling:" "$LOG" 2>/dev/null | tail -1 | sed -E 's/.*: *([0-9]+)ms.*/\1/')
count=$(grep -a -c "Domain Reload Profiling:" "$LOG" 2>/dev/null)
echo "last domain reload: ${last:-unknown} ms (limit $LIMIT_MS), reloads this Editor: ${count:-0}"
if [ -n "$last" ] && [ "$last" -gt "$LIMIT_MS" ]; then echo "RESTART: reloads are slow"; status=1; fi
free_gb=$(df -BG /c 2>/dev/null | tail -1 | tr -s ' ' | cut -d' ' -f4 | tr -d G)
echo "C: free: ${free_gb:-unknown} GB (minimum $MIN_C_GB)"
if [ -n "$free_gb" ] && [ "$free_gb" -lt "$MIN_C_GB" ]; then echo "STOP: C: is nearly full, tell the user"; status=2; fi
exit $status
