#!/usr/bin/env bash
# Downloads the models listed in models/manifest.json, extracts them below ${SLG_MODELS_DIR:-<repo>/models}/<id>/ and verifies the
# SHA-256 of the archive and of every listed file.
#
# Usage: scripts/fetch-models.sh [--list] [model-id ...]
#   --list      print the manifest entries and whether each is present, then exit
#   model-id    fetch only these models (default: every model with a downloadable archive)
#
# Entries without an archive (manual ONNX export needed) are skipped with a warning, and files whose sha256 is null in the manifest
# are reported as unpinned instead of verified. Needs bash, python3, curl, sha256sum (or shasum), and unzip or tar with bzip2.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
manifest="$repo_root/models/manifest.json"
dest="${SLG_MODELS_DIR:-$repo_root/models}"

warn() { printf 'warning: %s\n' "$*" >&2; }
fail() { printf 'error: %s\n' "$*" >&2; exit 1; }

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
[ -f "$manifest" ] || fail "manifest not found: $manifest"

staging=""
cleanup() { if [ -n "$staging" ]; then rm -rf "$staging"; fi; }
trap cleanup EXIT

# Prints either the ids to process (mode ids [id...]) or one model's details (mode model <id>): a MODEL line, then one FILE line per file.
manifest_query() {
  python3 -I - "$manifest" "$@" <<'PY'
import json
import re
import sys

manifest = json.load(open(sys.argv[1], encoding="utf-8"))
mode = sys.argv[2]
models = {m["id"]: m for m in manifest["models"]}
safe = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")
for model_id in models:
    if not safe.match(model_id):
        sys.exit("unsafe model id in manifest: " + model_id)


def dash(value):
    return "-" if value is None else str(value)


if mode == "ids":
    wanted = sys.argv[3:]
    for model_id in wanted:
        if model_id not in models:
            sys.exit("unknown model id: " + model_id)
    for model_id in (wanted if wanted else models):
        print(model_id)
elif mode == "model":
    model = models[sys.argv[3]]
    archive = model.get("archive") or {}
    print("\t".join(["MODEL", model["id"], model["role"], model["source"], dash(archive.get("format")),
                     dash(archive.get("sha256")), dash(archive.get("bytes")), dash(model.get("notes"))]))
    for f in model["files"]:
        print("\t".join(["FILE", f["path"], dash(f.get("sha256")), dash(f.get("bytes"))]))
PY
}

sha256_of() { "${sha256_cmd[@]}" "$1" | cut -d' ' -f1; }

# Verifies the files of one model against the manifest lines held in the global array 'lines'.
# Sets: n_ok n_missing n_corrupt n_unpinned
verify_files() {
  local id="$1" line kind path sha bytes full actual size
  n_ok=0; n_missing=0; n_corrupt=0; n_unpinned=0
  for line in "${lines[@]}"; do
    IFS=$'\t' read -r kind path sha bytes <<<"$line"
    [ "$kind" = FILE ] || continue
    full="$dest/$id/$path"
    if [ ! -f "$full" ]; then
      n_missing=$((n_missing + 1))
    elif [ "$sha" = "-" ]; then
      n_unpinned=$((n_unpinned + 1))
    else
      size="$(wc -c <"$full" | tr -d ' ')"
      actual="$(sha256_of "$full")"
      if [ "$actual" = "$sha" ] && { [ "$bytes" = "-" ] || [ "$size" = "$bytes" ]; }; then
        n_ok=$((n_ok + 1))
      else
        n_corrupt=$((n_corrupt + 1))
        warn "$id/$path does not match the manifest (expected $sha, got $actual)"
      fi
    fi
  done
}

list_models() {
  local ids id kind role source fmt asha abytes notes
  ids="$(manifest_query ids "$@")"
  while IFS= read -r id; do
    mapfile -t lines < <(manifest_query model "$id")
    IFS=$'\t' read -r kind id role source fmt asha abytes notes <<<"${lines[0]}"
    verify_files "$id"
    printf '%s\trole=%s\tok=%d missing=%d corrupt=%d unpinned=%d\tarchive=%s\n' "$id" "$role" "$n_ok" "$n_missing" "$n_corrupt" "$n_unpinned" "$fmt"
  done <<<"$ids"
}

fetch_model() {
  local id="$1" kind role source fmt asha abytes notes
  mapfile -t lines < <(manifest_query model "$id")
  [ "${#lines[@]}" -gt 0 ] || fail "could not read manifest entry $id"
  IFS=$'\t' read -r kind id role source fmt asha abytes notes <<<"${lines[0]}"

  if [ "$fmt" = "-" ]; then
    warn "$id has no downloadable archive and is skipped (manual export needed): ${notes}"
    return 0
  fi

  verify_files "$id"
  if [ "$n_missing" -eq 0 ] && [ "$n_corrupt" -eq 0 ]; then
    echo "$id: already present ($n_ok verified, $n_unpinned unpinned)"
    [ "$n_unpinned" -eq 0 ] || warn "$id: $n_unpinned file(s) have no sha256 in the manifest and were not verified"
    return 0
  fi

  mkdir -p "$dest/.downloads"
  local archive
  archive="$dest/.downloads/$(basename "$source")"
  echo "$id: downloading $source"
  curl --fail --silent --show-error --location --retry 3 --continue-at - --output "$archive" "$source"

  if [ "$asha" = "-" ]; then
    warn "$id: archive sha256 is not pinned in the manifest; archive not verified"
  else
    local actual
    actual="$(sha256_of "$archive")"
    if [ "$actual" != "$asha" ]; then
      rm -f "$archive"
      fail "$id: archive sha256 mismatch (expected $asha, got $actual); the download was deleted"
    fi
  fi

  staging="$(mktemp -d "$dest/.staging.XXXXXX")"
  case "$fmt" in
    zip)
      command -v unzip >/dev/null 2>&1 || fail "unzip not found"
      unzip -q "$archive" -d "$staging" ;;
    tar.bz2)
      tar -xjf "$archive" -C "$staging" ;;
    *)
      fail "$id: unsupported archive format '$fmt'" ;;
  esac
  [ -d "$staging/$id" ] || fail "$id: the archive does not contain a top-level directory named $id"
  rm -rf "${dest:?}/$id"
  mv "$staging/$id" "$dest/$id"
  rm -rf "$staging" "$archive"
  staging=""

  verify_files "$id"
  echo "$id: extracted ($n_ok verified, $n_missing missing, $n_corrupt mismatched, $n_unpinned unpinned)"
  [ "$n_unpinned" -eq 0 ] || warn "$id: $n_unpinned file(s) have no sha256 in the manifest and were not verified"
  if [ "$n_missing" -ne 0 ] || [ "$n_corrupt" -ne 0 ]; then
    fail "$id: verification failed"
  fi
}

if [ "${1:-}" = "--list" ]; then
  shift
  list_models "$@"
  exit 0
fi

ids="$(manifest_query ids "$@")"
mkdir -p "$dest"
dest="$(cd "$dest" && pwd)"
while IFS= read -r id; do
  fetch_model "$id"
done <<<"$ids"
echo "done; models directory: $dest"
