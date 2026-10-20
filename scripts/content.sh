#!/usr/bin/env bash
# Runs the core's ContentBuilder on a game content bundle. `validate` checks every file's hash and the cross-references; `hash` rewrites
# manifest.json from the files on disk, which is the step after any edit to a bundle.
#
# Usage: scripts/content.sh validate|hash [bundleDir] [ContentBuilder options]
#   bundleDir defaults to content/game-v1 (relative to this repository). Options after it are passed on, for example
#   `hash content/game-v1 --bundle-id game-v1` (hash keeps the manifest's existing bundle id unless one is given).
#   VOCATERRA_NLP_DIR is the core checkout that holds tools/Vocaterra.Nlp.ContentBuilder. It defaults to ../SpanishLearningGame, the folder
#   the core is cloned into beside this repository. Packaging the ContentBuilder as a dotnet tool is a follow-up in the core.
set -euo pipefail
# A user-local SDK (~/.dotnet, as on the development machine) is preferred when present; CI uses the SDK that setup-dotnet installed.
if [ -d "$HOME/.dotnet" ]; then export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

if [ "$#" -lt 1 ] || [ "$#" -gt 2 ]; then
  echo "usage: scripts/content.sh validate|hash [bundleDir]" >&2
  exit 2
fi
case "$1" in
  validate|hash) ;;
  *) echo "scripts/content.sh: unknown command '$1' (use validate or hash)" >&2; exit 2 ;;
esac

BUNDLE="${2:-content/game-v1}"
NLP_DIR="${VOCATERRA_NLP_DIR:-$ROOT/../SpanishLearningGame}"
PROJECT="$NLP_DIR/tools/Vocaterra.Nlp.ContentBuilder"
if [ ! -f "$PROJECT/Vocaterra.Nlp.ContentBuilder.csproj" ]; then
  echo "scripts/content.sh: no ContentBuilder at $PROJECT; set VOCATERRA_NLP_DIR to the core checkout" >&2
  exit 2
fi
dotnet run --project "$PROJECT" -- "$1" "$BUNDLE" "${@:3}"
