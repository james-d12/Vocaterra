#!/usr/bin/env bash
# B17a: the same view drawn with the built-in effect and with the game's own GLSL program (--gl-shaders), compared pixel by pixel, with the frame times of each
# run (--stream-stats). Needs a Release build (dotnet build src/Vocaterra.Game -c Release) and a display.
#
#   scripts/gl-compare.sh OUT_DIR [view...]      views: meadow, cave (default: both)
#
# Writes OUT_DIR/<view>-effect.png, <view>-gl.png, <view>-diff.png (red where the largest channel differs by more than 8 of 255) and the two stdout files, and prints per view the pixels that differ by more than
# a tolerance (scripts/gl-compare-images.py). The runs are scripted (a fixed clock), midday, High, seed 42, 1280x720.
set -uo pipefail
OUT="$1"; shift
VIEWS=("$@"); [ "${#VIEWS[@]}" -gt 0 ] || VIEWS=(meadow cave)
REPO="$(cd "$(dirname "$0")/.." && pwd)"
BIN="${GAME_BIN:-$REPO/src/Vocaterra.Game/bin/Release/net10.0/Vocaterra.Game}"
export DISPLAY="${DISPLAY:-:0}"
export VOCATERRA_MODELS_DIR="${VOCATERRA_MODELS_DIR:-$HOME/.local/share/vocaterra/models}"
mkdir -p "$OUT"

steps_for() {
  case "$1" in
    meadow) echo "--teleport 380,217 --look 90,-6 --wait 3" ;;
    cave) echo "--teleport-to 468,62,153 --look 340,-4 --give antorcha --give antorcha --place antorcha@468,62,151 --place antorcha@469,62,152 --look 340,-14 --wait 3" ;;
    *) echo "unknown view $1" >&2; return 1 ;;
  esac
}

for view in "${VIEWS[@]}"; do
  steps="$(steps_for "$view")" || exit 2
  for mode in effect gl; do
    flag=(); [ "$mode" = gl ] && flag=(--gl-shaders)
    work="$(mktemp -d)"
    # shellcheck disable=SC2086
    "$REPO/scripts/limited.sh" "$BIN" --session-dir "$work/session" --seed 42 --mute --needs off --pass open --time 13 --quality high --size 1280x720 --stream-stats "${flag[@]}" $steps --screenshot "$OUT/$view-$mode.png" --frames 240 > "$OUT/$view-$mode.stdout.txt" 2>&1
    echo "$view $mode: exit $?"
    rm -rf "$work"
  done
  python3 -I "$REPO/scripts/gl-compare-images.py" "$OUT/$view-effect.png" "$OUT/$view-gl.png" "$OUT/$view-diff.png" | sed "s/^/$view: /"
done
