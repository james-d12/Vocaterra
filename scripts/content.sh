#!/usr/bin/env bash
# Runs the language core's content tool (vocaterra-content, the Vocaterra.Nlp.ContentBuilder project under tools/) on a game content
# bundle. `validate` checks every file's hash and the cross-references; `hash` rewrites manifest.json from the files on disk, which is the
# step after any edit to a bundle. `cefr-check`, `lint`, `pack` and `verify-pack` are passed through as well (docs/nlp/TOOLS.md).
#
# `grammar` runs the core's grammar check (docs/nlp/API.md, "Grammar check the strings"): the bundle's Spanish, or with
#   `--strings content/grammar/game-v1-strings.jsonl --strings-only` the game's whole list (written by the host's --grammar-strings).
#   It needs a local LanguageTool server (scripts/languagetool.sh start) unless `--offline` replays the committed cache.
#
# `refresh` is the whole loop after an edit of the game's content (CONTENT_PLAN 3.7, C01), with no step to forget and without the words the
#   sandbox refuses: it rewrites the manifest of content/game-v1 (keeping its bundle id and its supportLanguage), lists the files that have no
#   provenance item, validates, runs the gate's lint and the CEFR check at A1 and A2, runs the game's own checks between its files (the host's
#   --validate-content, CONTENT_PLAN C02; the ContentBuilder references only the core and cannot), writes the grammar strings again (the host's
#   --grammar-strings) and runs the grammar check offline from the committed cache. A cache miss means new Spanish: the message names the
#   LanguageTool steps. Run it through scripts/limited.sh (it runs several tools one after the other; it never calls limited.sh itself).
#
# Usage: scripts/content.sh validate|hash|cefr-check|lint|pack|verify-pack|grammar [bundleDir] [tool options]
#        scripts/content.sh refresh
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
  echo "       scripts/content.sh refresh" >&2
  exit 2
}
[ "$#" -ge 1 ] || usage
case "$1" in
  validate|hash|cefr-check|lint|pack|verify-pack|grammar|refresh) ;;
  *) echo "scripts/content.sh: unknown command '$1'" >&2; usage ;;
esac
PROJECT="$ROOT/tools/Vocaterra.Nlp.ContentBuilder"
HOST="$ROOT/src/Vocaterra.Host"
if [ "$1" != "refresh" ]; then
  BUNDLE="${2:-content/game-v1}"
  exec dotnet run --project "$PROJECT" -- "$1" "$BUNDLE" "${@:3}"
fi

# ---- refresh: every step after an edit, in order; the first failure stops it.
GAME=content/game-v1
STRINGS=content/grammar/game-v1-strings.jsonl
tool() { dotnet run --project "$PROJECT" -- "$@"; }
step() { printf '\n== refresh: %s\n' "$*"; }

step "manifest of $GAME"
tool hash "$GAME"

step "provenance"
python3 -I - "$GAME" <<'EOF'
import json, os, sys
bundle = sys.argv[1]
listed = {f["path"] for f in json.load(open(os.path.join(bundle, "manifest.json"), encoding="utf-8"))["files"]}
covered = {i["path"] for i in json.load(open(os.path.join(bundle, "provenance.json"), encoding="utf-8"))["items"]}
missing = sorted(listed - covered - {"provenance.json"})
stale = sorted(covered - listed)
for path in missing:
    print(f"no provenance item for {path}: add one to {bundle}/provenance.json")
for path in stale:
    print(f"a provenance item names {path}, which is not in the bundle")
if missing or stale:
    sys.exit(1)
print(f"every file of {bundle} has its provenance item ({len(covered)} items)")
EOF

step "validate"
tool validate "$GAME"
step "lint (the gate's arguments)"
tool lint "$GAME" --strict --level a1 --host-pools 'guide.*'
step "cefr-check a1 and a2"
tool cefr-check "$GAME" --level a1
tool cefr-check "$GAME" --level a2

step "the game's own checks (the host's --validate-content: every game file, and the checks between them)"
dotnet run --project "$HOST" -- --content "$GAME" --validate-content

step "grammar strings"
dotnet run --project "$HOST" -- --content "$GAME" --grammar-strings "$STRINGS"
step "grammar check (offline, from the committed cache)"
if ! tool grammar "$GAME" --strings "$STRINGS" --strings-only --offline --strict; then
  cat >&2 <<'EOF'

refresh: the grammar check failed. A cache miss means new Spanish text: start the local server (scripts/languagetool.sh start), run
  scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --prune
read every finding (fix the content or add a waiver with its reason to content/grammar/game-v1-waivers.json), stop the server
(scripts/languagetool.sh stop) and run the refresh again (docs/game/README.md, "Refreshing the cache").
EOF
  exit 1
fi
step "done: commit the manifest, the strings and the cache with the edit, then run the whole test suite"
