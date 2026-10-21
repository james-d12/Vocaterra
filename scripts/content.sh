#!/usr/bin/env bash
# Runs the language core's content tool (vocaterra-content, the Vocaterra.Nlp.ContentBuilder dotnet tool) on a game content bundle.
# `validate` checks every file's hash and the cross-references; `hash` rewrites manifest.json from the files on disk, which is the step
# after any edit to a bundle. `cefr-check`, `pack` and `verify-pack` are passed through as well (core docs/PACKAGING.md).
#
# Usage: scripts/content.sh validate|hash|cefr-check|pack|verify-pack [bundleDir] [tool options]
#   bundleDir defaults to content/game-v1 (relative to this repository). Options after it are passed on, for example
#   `hash content/game-v1 --bundle-id game-v1` (hash keeps the manifest's bundle id unless one is given) or
#   `cefr-check content/game-v1 --level a1`.
#
# Which tool runs:
#   default: vocaterra-content installed under .tools/ (git-ignored) from the core's local feed,
#            external/Vocaterra.NLP/artifacts/feed (run `bash external/Vocaterra.NLP/scripts/pack.sh` first). It is installed on first
#            use and reinstalled when TOOL_VERSION below changes. VOCATERRA_CONTENT_TOOL_DIR moves the install (worktrees can share one).
#   VOCATERRA_NLP_DIR=<core checkout>: runs that checkout's tools/Vocaterra.Nlp.ContentBuilder project instead, for a core commit the
#            feed does not hold yet.
set -euo pipefail
# A user-local SDK (~/.dotnet, as on the development machine) is preferred when present; CI uses the SDK that setup-dotnet installed.
if [ -d "$HOME/.dotnet" ]; then export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

usage() {
  echo "usage: scripts/content.sh validate|hash|cefr-check|pack|verify-pack [bundleDir] [options]" >&2
  exit 2
}
[ "$#" -ge 1 ] || usage
case "$1" in
  validate|hash|cefr-check|pack|verify-pack) ;;
  *) echo "scripts/content.sh: unknown command '$1'" >&2; usage ;;
esac
BUNDLE="${2:-content/game-v1}"

if [ -n "${VOCATERRA_NLP_DIR:-}" ]; then
  PROJECT="$VOCATERRA_NLP_DIR/tools/Vocaterra.Nlp.ContentBuilder"
  if [ ! -f "$PROJECT/Vocaterra.Nlp.ContentBuilder.csproj" ]; then
    echo "scripts/content.sh: no ContentBuilder at $PROJECT (VOCATERRA_NLP_DIR)" >&2
    exit 2
  fi
  exec dotnet run --project "$PROJECT" -- "$1" "$BUNDLE" "${@:3}"
fi

TOOL_VERSION=0.1.0
FEED="$ROOT/external/Vocaterra.NLP/artifacts/feed"
TOOL_DIR="${VOCATERRA_CONTENT_TOOL_DIR:-$ROOT/.tools}"
TOOL="$TOOL_DIR/vocaterra-content"
if [ ! -x "$TOOL" ] || [ "$(cat "$TOOL_DIR/.version" 2>/dev/null || true)" != "$TOOL_VERSION" ]; then
  if [ ! -f "$FEED/Vocaterra.Nlp.ContentBuilder.$TOOL_VERSION.nupkg" ]; then
    echo "scripts/content.sh: no Vocaterra.Nlp.ContentBuilder $TOOL_VERSION in $FEED; run: bash external/Vocaterra.NLP/scripts/pack.sh" >&2
    exit 2
  fi
  rm -rf "$TOOL_DIR"
  dotnet tool install --tool-path "$TOOL_DIR" --add-source "$FEED" Vocaterra.Nlp.ContentBuilder --version "$TOOL_VERSION" >&2
  echo "$TOOL_VERSION" > "$TOOL_DIR/.version"
fi
exec "$TOOL" "$1" "$BUNDLE" "${@:3}"
