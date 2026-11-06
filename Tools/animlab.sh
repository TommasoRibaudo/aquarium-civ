#!/usr/bin/env bash
# #353 AnimLab: render queued animation clips on the REAL dolls in the Unity Editor (edit mode, URP post), side by side.
#
#   bash Tools/animlab.sh queue <clip.json> [--issue N]   copy a clip into the queue, stamped with branch/commit/issue
#   bash Tools/animlab.sh render [--cells N] [--chunk S]  render the queue (the Editor holder only): N clips per batch (default 6),
#                                                         S seconds per Editor call (default 40, always < 60); --calls C caps the
#                                                         Editor calls (default 80): an unfinished batch resumes on the next render
#   bash Tools/animlab.sh live                            render the LIVE requests (a "live" section: the real game in play
#                                                         mode, a cheat sets the scene up), all in one play-mode session
#   bash Tools/animlab.sh status                          the queue, the batch in progress, the last outputs
#
# Queue: AquariumCiv/Temp/agent/animlab/requests/*.json, one AnimBench clip per file (Tools/AnimBench/README.md), with an optional
# header  "animlab": {"issue": "#356", "branch": "vs/356-...", "commit": "abc1234"}  (AnimBench ignores it). Done requests move to
# requests/done/. Output: AquariumCiv/Temp/agent/animlab/out/<clip>/ (sheet.png, clip.mp4, clip-0.25x.mp4, clip.gif,
# clip-0.25x.gif, check.txt, bench/ = AnimBench's own run) and out/_batch/<id>/ (grid.mp4, grid-0.25x.mp4, grid.gif, grid.png).
#
# How: AnimBench simulates each clip (same code, same frame times), Tools/animlab/prep.py flattens its track to lab.json, then
# `unity command script eval` calls AnimLabBuilder.RenderChunk again and again (each call --timeout 60, state in batch.json, so a
# timeout or a domain reload resumes where it stopped), AnimLabBuilder.Finish() reopens the scene that was open, and
# Tools/animlab/post.py makes the videos and sheets. Every Unity call is bounded; the loop has a hard cap of calls.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJ="$ROOT/AquariumCiv"
WPROJ="$(cygpath -w "$PROJ" 2>/dev/null || echo 'F:\GameDev\AquariumCiv')"
LAB="$PROJ/Temp/agent/animlab"
REQ="$LAB/requests"
OUT="$LAB/out"
PY="${ANIMBENCH_PYTHON:-/c/Python311/python}"
export UNITY_NO_PAGER=1 UNITY_NO_BANNER=1
mkdir -p "$REQ/done" "$OUT"

die() { echo "animlab: $*" >&2; exit 2; }

ueval() { # $1 = C# code; prints the result string (or the error), bounded by --timeout 60 and a shell timeout
  local code="$1" raw
  raw="$(timeout 75 unity command script eval --code "$code" --timeout 60 --format json 2>&1)"
  "$PY" -c '
import json, sys
raw = sys.stdin.read()
try:
    d = json.loads(raw[raw.index("{"):])
except Exception:
    print("ERROR: no JSON from unity: " + raw.strip()[-300:]); sys.exit(0)
r = ((d.get("data") or {}).get("result") or {})
if d.get("success") and r.get("success", True): print(r.get("result"))
else: print("ERROR: " + json.dumps(d.get("errors") or r.get("diagnostics") or d)[:400])
' <<<"$raw"
}

cmd="${1:-status}"; shift || true
case "$cmd" in
  queue)
    clip="${1:-}"; [ -f "$clip" ] || die "queue: no clip file '$clip'"; shift
    issue=""
    while [ $# -gt 0 ]; do case "$1" in --issue) issue="$2"; shift ;; esac; shift; done
    dir="$(cd "$(dirname "$clip")" && pwd)"
    branch="$(git -C "$dir" rev-parse --abbrev-ref HEAD 2>/dev/null || echo "")"
    commit="$(git -C "$dir" rev-parse --short HEAD 2>/dev/null || echo "")"
    name="$(basename "$clip")"
    "$PY" -c '
import json, sys
src, dst, issue, branch, commit = sys.argv[1:6]
c = json.load(open(src, encoding="utf-8"))
h = c.get("animlab", {}) or {}
h.setdefault("issue", issue); h.setdefault("branch", branch); h.setdefault("commit", commit)
c["animlab"] = h
json.dump(c, open(dst, "w", encoding="utf-8"), indent=1)
' "$clip" "$REQ/$name" "$issue" "$branch" "$commit" || die "queue: bad JSON in $clip"
    echo "queued $REQ/$name ($branch $commit $issue)"
    ;;

  status)
    echo "queue ($REQ):"; ls -1 "$REQ"/*.json 2>/dev/null | sed 's/^/  /' || true
    [ -n "$(ls "$REQ"/*.json 2>/dev/null)" ] || echo "  (empty)"
    echo "done: $(ls -1 "$REQ/done" 2>/dev/null | wc -l) requests"
    if [ -f "$LAB/batch.json" ]; then
      "$PY" -c '
import json, sys
b = json.load(open(sys.argv[1]))
fps = b["rendered"] / b["renderSeconds"] if b.get("renderSeconds") else 0
print("batch %s: frame %d/%d, %d clips, %d calls, %.2f frames/s, done=%s" % (b["id"], b["next"], b["total"], len(b["labs"]), b["calls"], fps, b["done"]))
' "$LAB/batch.json"
    else echo "no batch in progress"; fi
    echo "outputs:"; ls -1dt "$OUT"/*/ 2>/dev/null | head -8 | sed 's/^/  /' || true
    ;;

  render)
    cells=6; chunk=40; calls=80
    while [ $# -gt 0 ]; do case "$1" in --cells) cells="$2"; shift ;; --chunk) chunk="$2"; shift ;; --calls) calls="$2"; shift ;; esac; shift; done
    [ "$chunk" -lt 55 ] 2>/dev/null || chunk=40
    st="$(timeout 30 unity status --project-path "$WPROJ" --format json 2>/dev/null | tr -d '\n')"
    echo "$st" | grep -q '"ready"' || die "the Editor is not ready (unity status: ${st:0:200})"
    t0=$(date +%s)
    if [ -f "$LAB/batch.json" ] && ! grep -q '"done": *true' "$LAB/batch.json"; then
      echo "resuming the batch in progress"
    else
      reqs=( $(grep -L '"live"' "$REQ"/*.json 2>/dev/null | head -n "$cells") )   # live requests go through 'live'
      [ ${#reqs[@]} -gt 0 ] || { echo "queue empty"; exit 0; }
      labs=()
      for r in "${reqs[@]}"; do
        name="$(basename "$r" .json)"
        o="$OUT/$name"
        rm -rf "$o/frames" "$o/lab-check.tsv"
        mkdir -p "$o"
        echo "AnimBench $name ..."
        if ! timeout 180 bash "$ROOT/Tools/AnimBench/run.sh" "$r" --no-video --out "$o/bench" > "$o/bench.log" 2>&1; then
          echo "  AnimBench failed (see $o/bench.log): skipped"; tail -3 "$o/bench.log"; continue
        fi
        "$PY" "$ROOT/Tools/animlab/prep.py" lab "$r" "$o/bench" "$o/lab.json" || { echo "  prep failed: skipped"; continue; }
        labs+=( "$o/lab.json" )
      done
      [ ${#labs[@]} -gt 0 ] || die "nothing to render"
      "$PY" "$ROOT/Tools/animlab/prep.py" batch "$LAB/batch.json" "${labs[@]}" || die "batch failed"
      printf '%s\n' "${reqs[@]}" > "$LAB/batch.requests"
    fi
    # Editor chunks: each call renders ~$chunk s and saves its place. Hard cap: --calls (default 80).
    status="MORE"
    for i in $(seq 1 "$calls"); do
      res="$(ueval "return AquariumCiv.EditorTools.AnimLabBuilder.RenderChunk(${chunk}f);")"
      echo "  [$(( $(date +%s) - t0 )) s] $res"
      case "$res" in
        DONE*) status="DONE"; break ;;
        ERROR*no\ JSON*|ERROR*timed*|ERROR*TIMEOUT*) echo "  (unity call failed; checking dialogs, then retrying)";
          powershell -NoProfile -ExecutionPolicy Bypass -File "$ROOT/Tools/unity-dialogs/dismiss-dialogs.ps1" 2>/dev/null | tail -3
          timeout 90 unity status --project-path "$WPROJ" --until-ready --timeout 60 >/dev/null 2>&1 ;;
        ERROR*) status="ERROR"; break ;;
      esac
    done
    echo "  finish: $(ueval 'return AquariumCiv.EditorTools.AnimLabBuilder.Finish();')"
    [ "$status" = "DONE" ] || die "render stopped ($status); run 'render' again to resume"
    "$PY" "$ROOT/Tools/animlab/post.py" "$LAB/batch.json" || echo "  post had errors (above)"
    id="$("$PY" -c 'import json,sys; print(json.load(open(sys.argv[1]))["id"])' "$LAB/batch.json")"
    mkdir -p "$OUT/_batch/$id"
    mv "$LAB/batch.json" "$OUT/_batch/$id/batch.json"
    if [ -f "$LAB/batch.requests" ]; then
      while read -r r; do [ -f "$r" ] && mv "$r" "$REQ/done/"; done < "$LAB/batch.requests"
      rm -f "$LAB/batch.requests"
    fi
    "$PY" -c '
import json, sys
b = json.load(open(sys.argv[1]))
print("rendered %d frame steps x %d clips x 2 cameras in %.1f s of Editor time (%.2f steps/s, %d calls)" % (b["rendered"], len(b["labs"]), b["renderSeconds"], b["rendered"] / max(0.01, b["renderSeconds"]), b["calls"]))
' "$OUT/_batch/$id/batch.json"
    echo "total $(( $(date +%s) - t0 )) s. Look at: $OUT/_batch/$id/grid.png, grid.mp4 and each $OUT/<clip>/sheet.png"
    ;;
  live)
    # Live requests (a "live" section): one play-mode session for all of them. Every wait is a bounded loop of bounded calls.
    mapfile -t reqs < <(grep -l '"live"' "$REQ"/*.json 2>/dev/null)
    [ ${#reqs[@]} -gt 0 ] || { echo "no live requests"; exit 0; }
    st="$(timeout 30 unity status --project-path "$WPROJ" --format json 2>/dev/null | tr -d '\n')"
    echo "$st" | grep -q '"ready"' || die "the Editor is not ready (unity status: ${st:0:200})"
    t0=$(date +%s)
    playing() { ueval 'return UnityEditor.EditorApplication.isPlaying ? "yes" : "no";'; }
    wait_for() { # $1 = C# expression returning a string, $2 = regex to wait for, $3 = tries (2 s apart)
      local r=""
      for i in $(seq 1 "$3"); do
        r="$(ueval "return $1;")"
        if echo "$r" | grep -Eq "$2"; then echo "$r"; return 0; fi
        sleep 2
      done
      echo "TIMEOUT waiting for '$2' (last: $r)"; return 1
    }
    seed="$("$PY" "$ROOT/Tools/animlab/prep.py" field "${reqs[0]}" live.seed 4242)"
    eco="$("$PY" "$ROOT/Tools/animlab/prep.py" field "${reqs[0]}" live.ecology 1)"
    echo "  enter: $(ueval 'return AquariumCiv.EditorTools.AnimLabLive.EnterPlay();')"
    p=""; for i in $(seq 1 30); do sleep 2; p="$(playing)"; [ "$p" = "yes" ] && break; done
    [ "$p" = "yes" ] || { echo "  play mode did not start ($p)"; powershell -NoProfile -ExecutionPolicy Bypass -File "$ROOT/Tools/unity-dialogs/dismiss-dialogs.ps1" 2>/dev/null | tail -3; die "live: no play mode"; }
    echo "  load: $(ueval "return AquariumCiv.EditorTools.AnimLabLive.Load($seed, $eco);")"
    if ! wait_for 'AquariumCiv.EditorTools.AnimLabLive.Status()' '^ready' 45 >/dev/null; then
      echo "  $(ueval 'return AquariumCiv.EditorTools.AnimLabLive.Status();')"; ueval 'return AquariumCiv.EditorTools.AnimLabLive.ExitPlay();'; die "live: the scene never came up"
    fi
    echo "  [$(( $(date +%s) - t0 )) s] scene ready"
    labs=(); done_reqs=()
    for r in "${reqs[@]}"; do
      name="$(basename "$r" .json)"; o="$OUT/$name"
      rm -rf "$o/frames"; mkdir -p "$o"
      spec="$("$PY" "$ROOT/Tools/animlab/prep.py" live "$r" "$o")" || { echo "  $name: bad request"; continue; }
      while IFS= read -r stepq; do
        step="$("$PY" -c 'import json,sys; print(json.loads(sys.argv[1]))' "$stepq")"
        echo "  $name setup: $(ueval "$step")"
        sleep 2   # the game runs between steps (rabbits come out, a plan settles)
      done < <("$PY" "$ROOT/Tools/animlab/prep.py" steps "$r")
      esc="$("$PY" -c 'import json,sys; print(json.dumps(sys.argv[1]))' "$spec")"
      echo "  $name: $(ueval "return AquariumCiv.EditorTools.AnimLabLive.Begin($esc);")"
      if res="$(wait_for 'AquariumCiv.EditorTools.AnimLabLive.Status()' '^(done|ERROR)' 150)" && [ "${res#done}" != "$res" ]; then
        echo "  [$(( $(date +%s) - t0 )) s] $name recorded"; labs+=( "$o/lab.json" ); done_reqs+=( "$r" )
      else echo "  $name: $res"; fi
    done
    echo "  exit: $(ueval 'return AquariumCiv.EditorTools.AnimLabLive.ExitPlay();')"
    for i in $(seq 1 30); do sleep 2; [ "$(playing)" = "no" ] && break; done
    echo "  finish: $(ueval 'return AquariumCiv.EditorTools.AnimLabBuilder.Finish();')"
    [ ${#labs[@]} -gt 0 ] || die "live: nothing recorded"
    "$PY" "$ROOT/Tools/animlab/prep.py" batch "$LAB/live-batch.json" "${labs[@]}" >/dev/null
    "$PY" "$ROOT/Tools/animlab/post.py" "$LAB/live-batch.json" || echo "  post had errors (above)"
    id="$("$PY" -c 'import json,sys; print(json.load(open(sys.argv[1]))["id"])' "$LAB/live-batch.json")"
    mkdir -p "$OUT/_batch/$id"; mv "$LAB/live-batch.json" "$OUT/_batch/$id/batch.json"
    for r in "${done_reqs[@]}"; do mv "$r" "$REQ/done/"; done
    echo "total $(( $(date +%s) - t0 )) s. Look at: $OUT/_batch/$id/grid.png and each $OUT/<clip>/sheet.png"
    ;;
  *) sed -n '2,12p' "$0"; exit 2 ;;
esac
