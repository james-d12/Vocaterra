#!/usr/bin/env bash
# Re-hashes the game's content bundle after an edit (content/game-v1) and checks it, then prints the new bundle hash (B29c).
#
#   scripts/rehash-bundle.sh
#
# It runs `scripts/content.sh hash content/game-v1 --bundle-id game-v1` (rewrites content/game-v1/manifest.json from the files on disk; the
# bundle id is kept) and then `scripts/content.sh validate` (every file's hash and the cross-references), each through scripts/limited.sh, and
# prints the bundle hash that manifest.json now holds. No test pins that hash any more (B29c: the save fixtures read the current bundle's hash),
# so the edit touches the content and manifest.json only. For the content edit loop with the checks, the lint and the grammar, use
# scripts/content-loop.sh (its first step is `scripts/content.sh refresh`, which also hashes).
#
# A conflict in content/game-v1/manifest.json (a merge of two branches that both edited the content) is resolved by taking either side of the
# conflict and then running this script: the hash is recomputed from the files, so the side taken does not matter.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
BUNDLE=content/game-v1

scripts/limited.sh scripts/content.sh hash "$BUNDLE" --bundle-id game-v1
scripts/limited.sh scripts/content.sh validate "$BUNDLE"

python3 -I -c 'import json, sys; print("bundle hash:", json.load(open(sys.argv[1], encoding="utf-8"))["bundleHash"])' "$BUNDLE/manifest.json"
