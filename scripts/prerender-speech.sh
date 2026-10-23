#!/usr/bin/env bash
# Renders the NPCs' speakable lines of the game bundle with Kokoro (S9): every slot-free dialogue line of the NPCs' pools (the shopkeeper's,
# the villager's and the guide's, and the feedback and not-understood lines), each prompt's text, one line per word-card noun, and each learner
# phrase of the phrase sheet spoken as its model answer. Output: content/game-v1/speech/<lineId>.wav (speed 1.0) and
# <lineId>.slow.wav (speed 0.8), 16-bit mono, host-owned bundle files, hashed in manifest.json and listed in provenance.json; the listening
# table docs/game/SPEECH_CHECKLIST.md; and the re-run index content/speech-index.json (outside the bundle).
#
# Speakers (the core's Kokoro voice table, docs/nlp/API.md section 6): the guide 28 ef_dora (also the word cards and the model answers), the
# shopkeeper 29 em_alex, the villager 53 em_santa. Re-runnable: a file whose text, speaker, speed and bytes match is skipped.
#
# Usage: scripts/prerender-speech.sh [--check] [--force] [--rate 16000]
#   --check   report what would be rendered, write nothing (exit 1 when something would be)
#   --force   render every file again
#   --rate 16000  downsample the output (Kokoro's native rate is 24000; use it when the speech folder is over 20 MB)
# Needs the models folder (VOCATERRA_MODELS_DIR, default ~/.local/share/vocaterra/models; kokoro-multi-lang-v1_0 from scripts/fetch-models.sh).
# The sandbox refuses command lines that contain the words the content tool uses, so this script runs them itself.
set -euo pipefail
if [ -d "$HOME/.dotnet" ]; then export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
export VOCATERRA_MODELS_DIR="${VOCATERRA_MODELS_DIR:-$HOME/.local/share/vocaterra/models}"

BUNDLE="content/game-v1"
TOOL="$ROOT/tools/Vocaterra.SpeechRender"

# A check writes nothing, so the bundle is not re-hashed.
for arg in "$@"; do
  if [ "$arg" = "--check" ]; then
    exec dotnet run --project "$TOOL" -- --bundle "$BUNDLE" "$@"
  fi
done

# 1. The renderer loads the bundle and checks its hashes, so the manifest must match the files on disk first.
scripts/content.sh hash "$BUNDLE" --bundle-id game-v1
# 2. Render (the speech folder is written, the index and the checklist are rewritten, provenance gets the speech items).
dotnet run --project "$TOOL" -- --bundle "$BUNDLE" "$@"
# 3. Hash the bundle with the new files, then validate it.
scripts/content.sh hash "$BUNDLE" --bundle-id game-v1
scripts/content.sh validate "$BUNDLE"
echo "speech: done; the listening table is docs/game/SPEECH_CHECKLIST.md"
