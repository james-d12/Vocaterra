#!/usr/bin/env bash
# Starts the playtest from the build folder (S12): the game, its content bundle and the Vosk model are in the folder this script is in.
# VOCATERRA_MODELS_DIR is pointed at the folder's own models, so the playtest never reads another copy of the models.
# Extra arguments are passed on to the game, for example: ./playtest.sh --greyscale (the A/B arm B) or --no-mic-check.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
export VOCATERRA_MODELS_DIR="$here/models"
if [ ! -f "$here/Vocaterra.MonoGame" ]; then
  echo "playtest.sh: the game (Vocaterra.MonoGame) is not in this folder ($here)." >&2
  echo "Build this folder with scripts/playtest-build.sh first (run it from the repository with a new, empty folder), then run ./playtest.sh from that folder." >&2
  exit 1
fi
cd "$here"
exec ./Vocaterra.MonoGame --playtest "$@"
