#!/usr/bin/env bash
# Rebuild the Adaptation Design Document from its generator (build_rev3.py) into a fresh folder.
#
# The generator rebuilds word/document.xml from revision 2's (git commit 4b312545); the other parts of the package (styles, images,
# package metadata) and the zip's entry order come from the current document. metadata.py sets the export's descriptive fields.
# An unchanged generator reproduces the current document byte for byte.
#
# Usage: tools/adaptation-design/rebuild.sh [out-dir]          (default: a new folder under ${TMPDIR:-/tmp})
# Then validate out.docx with the docx skill's scripts/office/validate.py (it needs defusedxml), render it with the skill's
# scripts/office/soffice.py, look at the pages you changed, and copy it over the document.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
doc="$repo/docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx"
out="${1:-$(mktemp -d "${TMPDIR:-/tmp}/adaptation-design.XXXXXX")}"
mkdir -p "$out/base" "$out/tree"
git -C "$repo" show 4b312545:docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx > "$out/rev2.docx"
unzip -q -o "$out/rev2.docx" word/document.xml -d "$out/base"
unzip -q -o "$doc" -d "$out/tree"
python3 -I -B "$here/build_rev3.py" "$out/base/word/document.xml" "$out/tree/word/document.xml"
python3 -I -B "$here/metadata.py" "$out/tree"
python3 -I -B "$here/pack.py" "$out/tree" "$doc" "$out/out.docx"
echo "built $out/out.docx"
