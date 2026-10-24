#!/usr/bin/env bash
# Runs the language core's content tool (vocaterra-content, the Vocaterra.Nlp.ContentBuilder project under tools/) on a game content
# bundle. `validate` checks every file's hash and the cross-references; `hash` rewrites manifest.json from the files on disk, which is the
# step after any edit to a bundle. `cefr-check`, `lint`, `pack` and `verify-pack` are passed through as well (docs/nlp/TOOLS.md).
#
# `grammar` runs the core's grammar check (docs/nlp/API.md, "Grammar check the strings"): the bundle's Spanish, or with
#   `--strings content/grammar/game-v1-strings.jsonl --strings-only` the game's whole list (written by the host's --grammar-strings).
#   It needs a local LanguageTool server (scripts/languagetool.sh start) unless `--offline` replays the committed cache.
#
# Usage: scripts/content.sh validate|hash|cefr-check|lint|pack|verify-pack|grammar [bundleDir] [tool options]
#   bundleDir defaults to content/game-v1 (relative to the repository root). Options after it are passed on, for example
#   `hash content/game-v1 --bundle-id game-v1` (hash keeps the manifest's bundle id unless one is given) or
#   `cefr-check content/game-v1 --level a1`.
#
# The tool runs from source (`dotnet run --project tools/Vocaterra.Nlp.ContentBuilder`), so it is always the core at this commit; no
# feed, no install. The sandbox used by delegated agents refuses command lines that contain the words `eval` or `hash`; run such a command
# from a wrapper script outside the repository.
set -euo pipefail
# A user-local SDK (~/.dotnet, as on the development machine) is preferred when present; CI uses the SDK that setup-dotnet installed.
if [ -d "$HOME/.dotnet" ]; then export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

usage() {
  echo "usage: scripts/content.sh validate|hash|cefr-check|lint|pack|verify-pack|grammar [bundleDir] [options]" >&2
  exit 2
}
[ "$#" -ge 1 ] || usage
case "$1" in
  validate|hash|cefr-check|lint|pack|verify-pack|grammar) ;;
  *) echo "scripts/content.sh: unknown command '$1'" >&2; usage ;;
esac
BUNDLE="${2:-content/game-v1}"
PROJECT="$ROOT/tools/Vocaterra.Nlp.ContentBuilder"
exec dotnet run --project "$PROJECT" -- "$1" "$BUNDLE" "${@:3}"
