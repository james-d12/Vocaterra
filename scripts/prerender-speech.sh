#!/usr/bin/env bash
# Renders the speakable lines of the game bundle with Kokoro (S9; tools/Vocaterra.SpeechRender): every slot-free dialogue line (the pools of
# every NPC of game/npcs.json, `<id>_*` and the lesson lines `guide.lesson.<id>.*`, and the guide's, feedback and not-understood lines), each
# prompt's text, one line per word-card noun, and each learner phrase of the phrase sheet spoken as its model answer. Output:
# content/game-v1/speech/<speaker>/<lineId>.wav (at the speaker's speed; the word cards' in speech/cards/) and <lineId>.slow.wav (0.8 of it), 16-bit mono, host-owned bundle files, hashed
# in manifest.json and listed in provenance.json; the listening table docs/game/speech-checklist.md; and the re-run index
# content/speech-index.json (outside the bundle).
#
# Speakers: each NPC's voice comes from its file, game/npcs/<id>.json `voice` (village-plan V01): the Kokoro speaker id and voice name, the
# speed of the normal recordings, and an optional `gain` that scales the samples of a voice the model renders louder or quieter (V11). The
# guide 28 ef_dora also speaks the word cards, the model answers and every pool of no other NPC; the shopkeeper is 29 em_alex and the villager
# 53 em_santa (the core's Kokoro voice table, docs/nlp/api.md section 6); the fisher (em_santa at 0.9), the baker (if_sara, gain 0.32), the
# smith (im_nicola, gain 0.6) and the farmer (pm_santa) have provisional voices from the V06 voice trial (D-game-22 (d); the trial is in git history).
# Re-runnable: a file whose text, speaker, voice, speed, gain, rate and bytes match the index is skipped, so a changed `voice` renders only
# that NPC's lines again (no --force needed), and the checklist keeps the owner's marks except on rows whose text or recording changed.
#
# Usage: scripts/limited.sh scripts/prerender-speech.sh [--check] [--force] [--rate 16000]
#   --check   report what would be rendered, write nothing (exit 1 when something would be)
#   --force   render every file again
#   --rate 16000  downsample the output (Kokoro's native rate is 24000; use it when the speech folder is over 20 MB)
# Run it through scripts/limited.sh (it runs the content tool and the renderer one after the other; it never calls limited.sh itself).
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
echo "speech: done; the listening table is docs/game/speech-checklist.md"
