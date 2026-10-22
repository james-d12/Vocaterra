#!/usr/bin/env bash
# Installs the development tools of the grammar check (nlp/tools/manifest.json): the Temurin JRE and LanguageTool, both pinned by URL and SHA-256,
# below ${VOCATERRA_TOOLS_DIR:-$HOME/.local/share/vocaterra/tools}. They run as separate local processes (nlp/docs/DECISIONS.md D10): no LanguageTool
# code is linked or shipped, and no n-gram data is installed.
#
# Usage: scripts/fetch-tools.sh [--list] [--clean]
#   --list   print the manifest entries and whether each one is installed, then exit
#   --clean  delete the downloaded archives (they are kept in <tools>/dl by default, so a reinstall needs no network)
#
# Idempotent: a tool whose stamp (<tools>/.<dir>.sha256, the archive's hash) matches the manifest and whose check file exists does nothing. An archive
# already in <tools>/dl with the right hash is extracted without downloading. Needs bash, python3, curl, sha256sum, tar.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
manifest="$repo_root/nlp/tools/manifest.json"
tools="${VOCATERRA_TOOLS_DIR:-$HOME/.local/share/vocaterra/tools}"
archives="$tools/dl"

warn() { printf 'warning: %s\n' "$*" >&2; }
fail() { printf 'error: %s\n' "$*" >&2; exit 1; }

for tool in python3 curl sha256sum tar; do
  command -v "$tool" >/dev/null 2>&1 || fail "required tool '$tool' not found"
done
[ -f "$manifest" ] || fail "manifest not found: $manifest"

mode="install"
clean=0
for arg in "$@"; do
  case "$arg" in
    --list) mode="list" ;;
    --clean) clean=1 ;;
    *) fail "unknown option: $arg (usage: scripts/fetch-tools.sh [--list] [--clean])" ;;
  esac
done

# Prints one line per tool: id, archive file, format, sha256, bytes, source, install dir, check path, ngrams.
entries() {
  python3 -I - "$manifest" <<'PY'
import json
import sys

manifest = json.load(open(sys.argv[1], encoding="utf-8"))
for tool in manifest["tools"]:
    archive = tool["archive"]
    install = tool["install"]
    fields = [tool["id"], archive["file"], archive["format"], archive["sha256"], str(archive["bytes"]), tool["source"],
              install["dir"], install["check"], str(tool.get("ngrams", "not-listed"))]
    if any("\t" in f or "\n" in f for f in fields):
        sys.exit("manifest value with a tab or newline: " + tool["id"])
    print("\t".join(fields))
PY
}

sha256_of() {
  sha256sum "$1" | cut -d' ' -f1
}

# Installs one tool; sets the global status to "current" or "installed".
install_tool() {
  local id="$1" file="$2" format="$3" sha="$4" bytes="$5" source="$6" dir="$7" check="$8"
  local target="$tools/$dir" stamp="$tools/.$dir.sha256" archive="$archives/$file"

  if [ -f "$stamp" ] && [ "$(cat "$stamp")" = "$sha" ] && [ -e "$target/$check" ]; then
    status="current"
    return 0
  fi

  mkdir -p "$archives"
  if [ -f "$archive" ] && [ "$(sha256_of "$archive")" = "$sha" ]; then
    printf 'fetch-tools: %s: using the archive in %s\n' "$id" "$archives"
  else
    printf 'fetch-tools: %s: downloading %s (%s bytes)\n' "$id" "$source" "$bytes"
    local partial="$archive.partial"
    if ! curl -fL --retry 3 --silent --show-error -o "$partial" "$source"; then
      rm -f "$partial"
      fail "download failed: $source"
    fi
    if [ "$(sha256_of "$partial")" != "$sha" ]; then
      rm -f "$partial"
      fail "$id: the download does not match the pinned sha256 in nlp/tools/manifest.json (not installed)"
    fi
    mv "$partial" "$archive"
  fi

  rm -rf "$target"
  mkdir -p "$target"
  case "$format" in
    tar.gz) tar -xzf "$archive" -C "$target" ;;
    zip) python3 -I -m zipfile -e "$archive" "$target" ;;
    *) fail "$id: unknown archive format '$format'" ;;
  esac
  [ -e "$target/$check" ] || fail "$id: $target/$check is missing after extraction"
  printf '%s\n' "$sha" > "$stamp"
  status="installed"
}

if [ "$mode" = "list" ]; then
  while IFS=$'\t' read -r id file format sha bytes source dir check ngrams; do
    state="missing"
    if [ -e "$tools/$dir/$check" ]; then state="present"; fi
    printf '%s\t%s\t%s\tn-grams: %s\n' "$id" "$state" "$tools/$dir" "$ngrams"
  done < <(entries)
  exit 0
fi

mkdir -p "$tools"
while IFS=$'\t' read -r id file format sha bytes source dir check ngrams; do
  status=""
  install_tool "$id" "$file" "$format" "$sha" "$bytes" "$source" "$dir" "$check"
  printf 'fetch-tools: %s: %s (%s)\n' "$id" "$status" "$tools/$dir"
done < <(entries)

if [ "$clean" -eq 1 ]; then
  rm -rf "$archives"
  printf 'fetch-tools: removed the downloaded archives in %s\n' "$archives"
fi
