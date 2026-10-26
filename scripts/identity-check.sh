#!/usr/bin/env bash
# The Phase A gate rules of the language core (research 2026-10-22-multilingual.md section 4.9), as a check that runs without building.
#
#   scripts/identity-check.sh <base-ref>
#
# (a) content/game-v1/manifest.json's bundleHash equals the one at <base-ref>.
# (b) No file under tests/*/Data/, tests/golden/ or content/ differs from <base-ref>, in the working tree or in the index, and no new
#     untracked file sits there. The one exception is a pure rename (git status R100, 100 % similarity), because M52 moves Data folders with git mv.
# (c) It does not run any build, test or content tool.
#
# Exit status: 0 when both rules hold; 1 with a message naming each failing rule and file; 2 for a usage or base-ref error.
set -uo pipefail

if [ "$#" -ne 1 ] || [ -z "$1" ]; then
    echo "usage: scripts/identity-check.sh <base-ref>" >&2
    exit 2
fi

BASE="$1"
ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "identity-check: not inside a git repository" >&2; exit 2; }
cd "$ROOT" || exit 2

if ! git rev-parse --verify --quiet "$BASE^{commit}" >/dev/null; then
    echo "identity-check: base ref '$BASE' does not resolve to a commit" >&2
    exit 2
fi

MANIFEST="content/game-v1/manifest.json"
failures=0

# Reads bundleHash from a manifest text on stdin.
bundle_hash() {
    python3 -c 'import json, sys; print(json.load(sys.stdin)["bundleHash"])'
}

# (a) the bundle hash is unchanged.
base_hash="$(git show "$BASE:$MANIFEST" 2>/dev/null | bundle_hash)"
if [ -z "$base_hash" ]; then
    echo "identity-check: FAIL (a): '$MANIFEST' has no bundleHash at $BASE" >&2
    failures=$((failures + 1))
else
    now_hash="$(bundle_hash < "$MANIFEST" 2>/dev/null)"
    if [ "$base_hash" != "$now_hash" ]; then
        echo "identity-check: FAIL (a): bundleHash changed: $BASE has $base_hash, the working tree has ${now_hash:-(unreadable)}" >&2
        failures=$((failures + 1))
    else
        echo "identity-check: ok (a): bundleHash unchanged ($now_hash)"
    fi
fi

# Is a path in a protected area? tests/*/Data/, tests/golden/ and content/ (bash pattern matching, so * also crosses folders).
protected() {
    case "$1" in
        tests/*/Data/*|tests/golden/*|content/*) return 0 ;;
        *) return 1 ;;
    esac
}

# (b) no protected file differs from the base, except a pure rename. --name-status with -M100% lists renames as R100 only when 100 % similar.
changed=0
while IFS=$'\t' read -r status first second; do
    [ -n "$status" ] || continue
    if [ "$status" = "R100" ]; then
        continue
    fi
    case "$status" in
        R*|C*)
            # A rename that is not 100 % similar: both paths count.
            for path in "$first" "$second"; do
                if protected "$path"; then
                    echo "identity-check: FAIL (b): $status $path (differs from $BASE)" >&2
                    changed=$((changed + 1))
                fi
            done
            ;;
        *)
            if protected "$first"; then
                echo "identity-check: FAIL (b): $status $first (differs from $BASE)" >&2
                changed=$((changed + 1))
            fi
            ;;
    esac
done < <(git diff --name-status -M100% "$BASE" --)

# Untracked files in the protected areas are differences too.
while IFS= read -r path; do
    [ -n "$path" ] || continue
    if protected "$path"; then
        echo "identity-check: FAIL (b): A $path (untracked, not in $BASE)" >&2
        changed=$((changed + 1))
    fi
done < <(git ls-files --others --exclude-standard --)

if [ "$changed" -gt 0 ]; then
    echo "identity-check: FAIL (b): $changed file(s) under tests/*/Data/, tests/golden/ or content/ differ from $BASE (pure renames excepted)." >&2
    failures=$((failures + 1))
else
    echo "identity-check: ok (b): no file under tests/*/Data/, tests/golden/ or content/ differs from $BASE"
fi

if [ "$failures" -gt 0 ]; then
    echo "identity-check: FAILED ($failures rule(s)); see the messages above." >&2
    exit 1
fi

echo "identity-check: passed against $BASE"
exit 0
