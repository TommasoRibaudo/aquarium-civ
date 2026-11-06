#!/usr/bin/env bash
# #352 AnimBench: build (only when a source changed), simulate, render. No Unity Editor involved.
#
#   bash Tools/AnimBench/run.sh <clip.json | clip name in Tools/AnimBench/clips> [--no-video] [--strict] [--out DIR]
#
# Output: Tools/AnimBench/out/<clip file name>/ (track.json, principles.txt, sheet.png, curves.png, clip.mp4/.gif, clip-0.25x.mp4/.gif).
# Everything it writes is inside this worktree (bin/, obj/, out/), so two worktrees can run it at the same time.
# Env: ANIMBENCH_PYTHON (default /c/Python311/python), ANIMBENCH_FFMPEG (else PATH, else the winget Gyan.FFmpeg install),
#      ANIMBENCH_JOBS (frame-render processes; default cores - 1, at most 8).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GAME="$HERE/../../AquariumCiv/Assets/Game"
DLL="$HERE/bin/Release/net8.0/AnimBench.dll"
start=$(date +%s.%N)

clip=""; outdir=""; video=1; strict=""
while [ $# -gt 0 ]; do
  case "$1" in
    --no-video) video=0 ;;
    --strict) strict="--strict" ;;
    --out) outdir="$2"; shift ;;
    -h|--help) sed -n '2,10p' "$0"; exit 0 ;;
    *) clip="$1" ;;
  esac
  shift
done
if [ -z "$clip" ]; then
  echo "usage: bash Tools/AnimBench/run.sh <clip.json | clip name> [--no-video] [--strict] [--out DIR]" >&2
  echo "clips: $(cd "$HERE/clips" && ls *.json 2>/dev/null | sed 's/\.json$//' | tr '\n' ' ')" >&2
  exit 2
fi
if [ ! -f "$clip" ]; then
  if [ -f "$HERE/clips/$clip.json" ]; then clip="$HERE/clips/$clip.json"
  elif [ -f "$HERE/clips/$clip" ]; then clip="$HERE/clips/$clip"
  else echo "AnimBench: no clip '$clip' (a path, or one of: $(cd "$HERE/clips" && ls *.json | sed 's/\.json$//' | tr '\n' ' '))" >&2; exit 2
  fi
fi
name="$(basename "$clip" .json)"
[ -n "$outdir" ] || outdir="$HERE/out/$name"
mkdir -p "$outdir"

PY="${ANIMBENCH_PYTHON:-/c/Python311/python}"
command -v "$PY" >/dev/null 2>&1 || PY="$(command -v python3 || command -v python || true)"
[ -n "$PY" ] || { echo "AnimBench: no Python (set ANIMBENCH_PYTHON)" >&2; exit 2; }

# ---- build, only when something it compiles is newer than the dll. The lock is this worktree's own (obj/), never global.
needs_build() {
  [ -f "$DLL" ] || return 0
  [ -n "$(find "$HERE/src" "$HERE/Shims" "$HERE/../SimTests/Shims" "$HERE/AnimBench.csproj" "$GAME/Sim" "$GAME/Life" \
          "$GAME/View/DollMotion.cs" "$GAME/View/PegDollTypes.cs" -name '*.cs*' -newer "$DLL" -print -quit 2>/dev/null)" ] && return 0
  [ -n "$(find "$GAME/View" -maxdepth 1 \( -name 'Beat*.cs' -o -name 'Arc*.cs' \) -newer "$DLL" -print -quit 2>/dev/null)" ] && return 0
  [ -d "$GAME/View/Anim/Core" ] && [ -n "$(find "$GAME/View/Anim/Core" -name '*.cs' -newer "$DLL" -print -quit)" ] && return 0
  return 1
}
if needs_build; then
  mkdir -p "$HERE/obj"
  lock="$HERE/obj/.animbench-build.lock"
  waited=0
  until mkdir "$lock" 2>/dev/null; do   # another run in THIS worktree is building: wait for it, at most 120 s
    [ $waited -ge 240 ] && { echo "AnimBench: build lock $lock held for 120 s; remove it if no build is running" >&2; exit 4; }
    sleep 0.5; waited=$((waited + 1))
  done
  trap 'rmdir "$lock" 2>/dev/null || true' EXIT
  if needs_build; then
    echo "AnimBench: building..."
    if ! timeout 300 dotnet build "$HERE/AnimBench.csproj" -c Release -nologo -v q -clp:ErrorsOnly -p:UseAppHost=false; then
      echo "AnimBench: build failed (a game file the bench compiles needs a shim? see Tools/AnimBench/README.md)" >&2; exit 4
    fi
  fi
  rmdir "$lock" 2>/dev/null || true
  trap - EXIT
fi

# ---- simulate (track.json, render.json, principles.txt)
code=0
timeout 120 dotnet "$DLL" "$clip" --out "$outdir" $strict || code=$?
if [ $code -ne 0 ] && [ $code -ne 3 ]; then exit $code; fi

# ---- render (sheet.png, curves.png, videos)
args=("$HERE/render.py" "$outdir")
[ $video -eq 1 ] || args+=("--no-video")
timeout 300 "$PY" "${args[@]}"

winpath() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else echo "$1"; fi; }
outdir="$(winpath "$outdir")"
end=$(date +%s.%N)
printf 'AnimBench: %s done in %.1f s -> %s\n' "$name" "$(echo "$end - $start" | bc 2>/dev/null || awk "BEGIN{print $end-$start}")" "$outdir"
printf '  look at: %s/sheet.png  %s/curves.png  (videos: clip.mp4, clip-0.25x.mp4, clip.gif, clip-0.25x.gif)\n' "$outdir" "$outdir"
exit $code
