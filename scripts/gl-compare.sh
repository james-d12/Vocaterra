#!/usr/bin/env bash
# B17a, B17b: the same view drawn with MonoGame's effects (--gl-shaders off) and with the game's own GLSL program (--gl-shaders on), compared pixel by pixel, with the frame times and the card's time of each
# run (--stream-stats). Needs a Release build (dotnet build src/Vocaterra.Game -c Release) and a display.
#
#   scripts/gl-compare.sh OUT_DIR [view...]
#
# Views (default: all): meadow (midday), wood-dusk (the valley wood at B22's dusk), cave (torches), shore (a pool and a lake's edge: water), under (under water: the render target), village (a street), grass (B18's grass layer).
# Environment: GL_RUNS=N (default 1) repeats each view N times, effect and GLSL interleaved (other agents load the machine: interleave before comparing times); GL_QUALITY (default high).
#
# Writes OUT_DIR/<view>-effect.png, <view>-gl.png, <view>-diff.png (red where the largest channel differs by more than 8 of 255) and the stdout files (<view>-<mode>-<run>.stdout.txt; the first run's pictures are
# the ones compared), and prints per view the pixels that differ by more than a tolerance (scripts/gl-compare-images.py: 0.1 percent of the pixels by more than 8 of 255) and the passes' times.
set -uo pipefail
OUT="$1"; shift
VIEWS=("$@"); [ "${#VIEWS[@]}" -gt 0 ] || VIEWS=(meadow wood-dusk cave shore under village grass)
RUNS="${GL_RUNS:-1}"
QUALITY="${GL_QUALITY:-high}"
REPO="$(cd "$(dirname "$0")/.." && pwd)"
BIN="${GAME_BIN:-$REPO/src/Vocaterra.Game/bin/Release/net10.0/Vocaterra.Game}"
export DISPLAY="${DISPLAY:-:0}"
export VOCATERRA_MODELS_DIR="${VOCATERRA_MODELS_DIR:-$HOME/.local/share/vocaterra/models}"
mkdir -p "$OUT"

# The options and the steps of a view: world, hour, extra options, steps.
view_options() {
  case "$1" in
    meadow) echo "--time 13 --grass 0 --teleport 380,217 --look 90,-6 --wait 3" ;;
    wood-dusk) echo "--world valley --time 21 --grass 0 --teleport 196,206 --look 100,-3 --wait 3" ;;
    cave) echo "--time 13 --grass 0 --teleport-to 468,62,153 --look 340,-4 --give antorcha --give antorcha --place antorcha@468,62,151 --place antorcha@469,62,152 --look 340,-14 --wait 3" ;;
    shore) echo "--world valley --time 13 --grass 0 --teleport 258,106 --look 180,-12 --wait 3" ;;
    under) echo "--world valley --time 13 --grass 0 --teleport-to 258,98,106 --look 180,-20 --wait 3" ;;
    village) echo "--world valley --time 13 --grass 0 --teleport 266,223 --look 80,2 --wait 3" ;;
    grass) echo "--time 13 --grass 60 --teleport 380,217 --look 90,-14 --wait 3" ;;
    *) echo "unknown view $1" >&2; return 1 ;;
  esac
}

for view in "${VIEWS[@]}"; do
  options="$(view_options "$view")" || exit 2
  for run in $(seq 1 "$RUNS"); do
    for mode in effect gl; do
      flag=(--gl-shaders off); [ "$mode" = gl ] && flag=(--gl-shaders on)
      picture="$OUT/$view-$mode-run$run.png"
      work="$(mktemp -d)"
      # shellcheck disable=SC2086
      "$REPO/scripts/limited.sh" "$BIN" --session-dir "$work/session" --seed 42 --mute --needs off --pass open --quality "$QUALITY" --size 1280x720 --stream-stats "${flag[@]}" $options --screenshot "$picture" --frames 240 > "$OUT/$view-$mode-run$run.stdout.txt" 2>&1
      echo "$view $mode run $run: exit $?"
      rm -rf "$work"
    done
  done
  cp "$OUT/$view-effect-run1.png" "$OUT/$view-effect.png"
  cp "$OUT/$view-gl-run1.png" "$OUT/$view-gl.png"
  python3 -I "$REPO/scripts/gl-compare-images.py" "$OUT/$view-effect.png" "$OUT/$view-gl.png" "$OUT/$view-diff.png" | sed "s/^/$view: /"
  grep -h "chunk passes\|gl: the last frame" "$OUT/$view"-*-run*.stdout.txt | sed "s/^/$view: /" | head -$((RUNS * 6))
done
