#!/usr/bin/env bash
# Checks the relative Markdown links in the language core's documents (every *.md under nlp/, build output excluded).
# A link is relative when it has no URL scheme and is not a bare #anchor. Its target is resolved from the file that holds it,
# with any #fragment or ?query removed, and must exist as a file or a folder in the working tree. Links inside fenced code blocks
# and inline code spans are ignored. Nothing is fetched over the network.
#
# Usage: nlp/scripts/check-links.sh [--quiet]
#   Prints one line per broken link (file:line: target) and a summary. Exits 1 when any link is broken, 0 otherwise.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
NLP="$ROOT/nlp"
quiet=0
[ "${1:-}" = "--quiet" ] && quiet=1

PYTHONDONTWRITEBYTECODE=1 python3 -I - "$ROOT" "$NLP" "$quiet" <<'PY'
import os, re, sys

root, nlp, quiet = sys.argv[1], sys.argv[2], sys.argv[3] == "1"
link = re.compile(r'!?\[[^\]]*\]\(([^)\s]+)(?:\s+"[^"]*")?\)')
code_span = re.compile(r'`[^`]*`')
scheme = re.compile(r'^[a-zA-Z][a-zA-Z0-9+.-]*:')

checked = 0
broken = []
for dirpath, dirnames, filenames in os.walk(nlp):
    dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", "artifacts", ".git")]
    for name in filenames:
        if not name.endswith(".md"):
            continue
        path = os.path.join(dirpath, name)
        fence = False
        with open(path, encoding="utf-8") as handle:
            for number, line in enumerate(handle, 1):
                if line.lstrip().startswith("```"):
                    fence = not fence
                    continue
                if fence:
                    continue
                text = code_span.sub("", line)
                for match in link.finditer(text):
                    target = match.group(1).strip("<>")
                    if target.startswith("#") or scheme.match(target) or target.startswith("//"):
                        continue
                    target = target.split("#", 1)[0].split("?", 1)[0]
                    if not target:
                        continue
                    checked += 1
                    resolved = os.path.normpath(os.path.join(dirpath, target))
                    if not os.path.exists(resolved):
                        rel = os.path.relpath(path, root)
                        broken.append((rel, number, target))

for rel, number, target in broken:
    print(f"{rel}:{number}: {target}")
if not quiet or broken:
    print(f"links checked: {checked}, broken: {len(broken)}")
sys.exit(1 if broken else 0)
PY
