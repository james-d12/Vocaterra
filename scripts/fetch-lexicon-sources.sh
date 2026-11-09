#!/usr/bin/env bash
# Downloads the four data sets the lexicon imports read into ${VOCATERRA_LEXICON_SOURCES_DIR:-<repo>/lexicon-sources}/ (git-ignored), checks
# their SHA-256 and byte size against the values pinned below, and records them with the retrieval date in sources.json next to them.
#
#   kaikki-es          the kaikki.org Spanish extract of English Wiktionary (wiktextract JSONL), CC BY-SA 4.0
#   frequency-es-50k   hermitdave/FrequencyWords, content/2018/es/es_50k.txt (OpenSubtitles 2018), CC BY-SA 4.0
#   kaikki-de          the kaikki.org German extract of English Wiktionary (wiktextract JSONL), CC BY-SA 4.0 (M36)
#   frequency-de-50k   hermitdave/FrequencyWords, content/2018/de/de_50k.txt (OpenSubtitles 2018), CC BY-SA 4.0 (M36)
#
# Usage: scripts/fetch-lexicon-sources.sh [--check]
#   --check   verify the files that are present against the pins and sources.json; download nothing; exit 1 when one is missing or differs
#
# A file that is already present and matches its pin is kept, and its recorded retrieval date is kept too. A downloaded file whose hash
# differs from the pin is deleted and the script stops: the kaikki.org URL is not versioned (the site replaces the file with every new
# extraction), so a mismatch means upstream moved on. Re-pinning is a deliberate act described in docs/nlp/lexicon.md; nothing is substituted
# silently. Downloaded files are data: the import tool reads them, nothing in them is ever executed.
# Needs bash, python3, curl and sha256sum (or shasum).
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
dest="${VOCATERRA_LEXICON_SOURCES_DIR:-${SLG_LEXICON_SOURCES_DIR:-$repo_root/lexicon-sources}}"

# ---- Pins -------------------------------------------------------------------------------------------------------------------------------
# id|file name|url|sha256|bytes|licence
# The FrequencyWords URL names a commit, so it is immutable; the kaikki.org URL is not, which is why the hash is the pin.
# kaikki.org Spanish and German: last modified 2026-10-30 (extracted 2026-10-30 from the enwiktionary dump of 2026-10-01, wiktextract 19f9a30 and
# 6f96ddf, per the page footer), retrieved 2026-11-08. M90 re-pinned kaikki-es to this file (it was 1054565723 bytes, 7b23a5dc..., extracted from
# the dump of 2026-09-02); content/game-v1/lexicon/ was built from the earlier pin and is not regenerated (docs/nlp/lexicon.md). The German
# extract is marked deprecated on kaikki.org ("will be removed in the near future"); if it disappears, the pinned hash identifies what to keep.
pins=(
  "kaikki-es|kaikki.org-dictionary-Spanish.jsonl|https://kaikki.org/dictionary/Spanish/kaikki.org-dictionary-Spanish.jsonl|336903d1290968d77bd361c620d7d799ad7a7a9bc6ad353ec35954f2e15fbe6f|1119672965|CC-BY-SA-4.0 (Wiktionary text; older revisions CC-BY-SA-3.0) and GFDL"
  "frequency-es-50k|es_50k.txt|https://raw.githubusercontent.com/hermitdave/FrequencyWords/525f9b560de45753a5ea01069454e72e9aa541c6/content/2018/es/es_50k.txt|dcff3ad4316192f4dc4ff7d26e637c6ff314ef1ca0f3f720c5649018a71056c0|658626|CC-BY-SA-4.0 (content), MIT (code)"
  "kaikki-de|kaikki.org-dictionary-German.jsonl|https://kaikki.org/dictionary/German/kaikki.org-dictionary-German.jsonl|4fb15662a125085e1eb6b533a210b45edcdffa7b8e72e07634f46e1485e4e715|1095258648|CC-BY-SA-4.0 (Wiktionary text; older revisions CC-BY-SA-3.0) and GFDL"
  "frequency-de-50k|de_50k.txt|https://raw.githubusercontent.com/hermitdave/FrequencyWords/525f9b560de45753a5ea01069454e72e9aa541c6/content/2018/de/de_50k.txt|d9e50546fd7e8b6fe6542a2b33c51d1331092b2a3916ec09f80d97856068705b|662497|CC-BY-SA-4.0 (content), MIT (code)"
)

warn() { printf 'warning: %s\n' "$*" >&2; }
fail() { printf 'error: %s\n' "$*" >&2; exit 1; }

check_only=0
case "${1:-}" in
  "") ;;
  --check) check_only=1 ;;
  -h | --help)
    sed -n '2,15p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    exit 0
    ;;
  *) fail "unknown argument: $1 (usage: scripts/fetch-lexicon-sources.sh [--check])" ;;
esac
[ "$#" -le 1 ] || fail "too many arguments"

for tool in python3 curl; do
  command -v "$tool" >/dev/null 2>&1 || fail "required tool '$tool' not found"
done
if command -v sha256sum >/dev/null 2>&1; then
  sha256_cmd=(sha256sum)
elif command -v shasum >/dev/null 2>&1; then
  sha256_cmd=(shasum -a 256)
else
  fail "required tool 'sha256sum' (or 'shasum') not found"
fi

sha256_of() { "${sha256_cmd[@]}" "$1" | cut -d' ' -f1; }
size_of() { wc -c <"$1" | tr -d ' '; }

mkdir -p "$dest"
dest="$(cd "$dest" && pwd)"
sources_json="$dest/sources.json"
today="$(date -u +%Y-%m-%d)"

# Prints the retrieval date recorded for an id in sources.json, or nothing.
recorded_date() {
  [ -f "$sources_json" ] || return 0
  python3 -I - "$sources_json" "$1" <<'PY'
import json
import sys

try:
    data = json.load(open(sys.argv[1], encoding="utf-8"))
except (OSError, ValueError):
    sys.exit(0)
for source in data.get("sources", []):
    if source.get("id") == sys.argv[2]:
        print(source.get("retrievedOn", ""))
PY
}

# Verifies one file against its pin. Prints nothing; returns 0 (ok), 1 (missing), 2 (differs).
verify_file() {
  local file="$1" sha="$2" bytes="$3" size actual
  [ -f "$file" ] || return 1
  size="$(size_of "$file")"
  if [ "$size" != "$bytes" ]; then
    warn "$file has $size bytes, the pin says $bytes"
    return 2
  fi
  actual="$(sha256_of "$file")"
  if [ "$actual" != "$sha" ]; then
    warn "$file has sha256 $actual, the pin says $sha"
    return 2
  fi
  return 0
}

records=()
status=0
for pin in "${pins[@]}"; do
  IFS='|' read -r id name url sha bytes licence <<<"$pin"
  file="$dest/$name"
  retrieved="$(recorded_date "$id")"
  if [ -z "$sha" ]; then
    fail "the pin for $id has no sha256 yet; see docs/nlp/lexicon.md (re-pinning)"
  fi

  rc=0
  verify_file "$file" "$sha" "$bytes" || rc=$?
  if [ "$rc" -eq 0 ]; then
    echo "$id: present and verified ($bytes bytes, sha256 $sha)"
    [ -n "$retrieved" ] || retrieved="$today"
  elif [ "$rc" -eq 2 ]; then
    if [ "$check_only" -eq 1 ]; then
      status=1
      continue
    fi
    fail "$id: $file does not match the pin; delete it to download again, or re-pin on purpose (docs/nlp/lexicon.md)"
  else
    if [ "$check_only" -eq 1 ]; then
      echo "$id: missing ($file)"
      status=1
      continue
    fi
    echo "$id: downloading $url"
    part="$file.part"
    curl --fail --silent --show-error --location --retry 3 --continue-at - --output "$part" "$url"
    size="$(size_of "$part")"
    actual="$(sha256_of "$part")"
    if [ "$size" != "$bytes" ] || [ "$actual" != "$sha" ]; then
      rm -f "$part"
      fail "$id: the download has $size bytes and sha256 $actual; the pin says $bytes bytes and $sha. The upstream file changed (kaikki.org replaces it with every extraction); the partial download was deleted. See docs/nlp/lexicon.md (re-pinning)."
    fi
    mv "$part" "$file"
    retrieved="$today"
    echo "$id: downloaded and verified ($bytes bytes)"
  fi
  records+=("$id|$name|$url|$sha|$bytes|$retrieved|$licence")
done

if [ "$check_only" -eq 1 ]; then
  [ "$status" -eq 0 ] && echo "all lexicon sources verified" || echo "some lexicon sources are missing or differ" >&2
  exit "$status"
fi

# sources.json: the record the importer copies into the sidecar (url, retrieval date, sha256, bytes). Sorted keys, LF, trailing newline.
python3 -I - "$sources_json" "${records[@]}" <<'PY'
import json
import sys

sources = []
for record in sys.argv[2:]:
    id_, name, url, sha, size, retrieved, licence = record.split("|", 6)
    sources.append({"id": id_, "file": name, "url": url, "sha256": sha, "bytes": int(size), "retrievedOn": retrieved, "license": licence})
with open(sys.argv[1], "w", encoding="utf-8", newline="\n") as handle:
    json.dump({"schemaVersion": 1, "sources": sources}, handle, indent=2, sort_keys=True, ensure_ascii=False)
    handle.write("\n")
PY
echo "done; sources recorded in $sources_json"
