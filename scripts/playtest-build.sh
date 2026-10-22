#!/usr/bin/env bash
# Builds the private linux-x64 playtest folder (S12): the MonoGame game, self-contained so that a tester needs no .NET install, the content
# bundle with its 226 speech WAVs, the Vosk model (the only speech model the playtest loads) with the manifest that pins it, the tester's README
# and the launcher. Then it checks the folder: no sherpa-onnx or ONNX Runtime native library, no espeak-ng library and no sherpa model file
# ships (core D2 and CR-021); the playtest loads Vosk only (the NPCs' voices are the pre-rendered WAVs, S9).
#
# Usage: scripts/playtest-build.sh <destination folder>
#   The destination must be a new or empty folder outside the repository. It is private: the speech WAVs are model output and the game's
#   native code is not GPL-free (core D2: private builds only until the GPL-free sherpa-onnx pin). The Vosk model is copied from
#   VOCATERRA_MODELS_DIR (default ~/.local/share/vocaterra/models; fetch it with nlp/scripts/fetch-models.sh) after its SHA-256 sums and sizes
#   are checked against nlp/models/manifest.json. Exit status is 0 only when the build and every check passed.
set -euo pipefail
if [ -d "$HOME/.dotnet" ]; then export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

if [ "$#" -ne 1 ]; then
  echo "usage: scripts/playtest-build.sh <destination folder outside the repository>" >&2
  exit 2
fi
DEST="$(realpath -m "$1")"
case "$DEST" in
  "$ROOT"|"$ROOT"/*) echo "scripts/playtest-build.sh: the destination is inside the repository: $DEST" >&2; exit 2 ;;
esac
if [ -d "$DEST" ] && [ -n "$(ls -A "$DEST")" ]; then
  echo "scripts/playtest-build.sh: $DEST is not empty; choose a new folder (the build does not overwrite)." >&2
  exit 2
fi
mkdir -p "$DEST"

MODELS="${VOCATERRA_MODELS_DIR:-$HOME/.local/share/vocaterra/models}"
MANIFEST="$ROOT/nlp/models/manifest.json"
BUNDLE="$ROOT/game/content/game-v1"
VOSK_ID="vosk-model-small-es-0.42"

[ -d "$MODELS/$VOSK_ID" ] || { echo "scripts/playtest-build.sh: the Vosk model is not in $MODELS (run nlp/scripts/fetch-models.sh with VOCATERRA_MODELS_DIR set)." >&2; exit 1; }

echo "== 1. the content bundle (validate before it is copied)"
"$ROOT/scripts/content.sh" validate "$BUNDLE"

echo "== 2. publish the game for linux-x64 (self-contained; playtest: no sherpa-onnx or ONNX Runtime native library)"
dotnet publish "$ROOT/game/src/Vocaterra.MonoGame/Vocaterra.MonoGame.csproj" -c Release -r linux-x64 --self-contained true \
  -p:VocaterraPlaytest=true -o "$DEST" --nologo

echo "== 3. the bundle with its speech, the models, the tester's README and the launcher"
mkdir -p "$DEST/content" "$DEST/models"
cp -r "$BUNDLE" "$DEST/content/game-v1"
cp "$MANIFEST" "$DEST/models/manifest.json"
cp -r "$MODELS/$VOSK_ID" "$DEST/models/$VOSK_ID"
cp "$ROOT/game/playtest/README-tester.md" "$DEST/README.md"
cp "$ROOT/game/playtest/playtest.sh" "$DEST/playtest.sh"
chmod +x "$DEST/playtest.sh" "$DEST/Vocaterra.MonoGame"

echo "== 4. check the folder"
python3 -I - "$DEST" "$MANIFEST" "$VOSK_ID" <<'PY'
import hashlib
import json
import os
import re
import sys

dest, manifest_path, vosk_id = sys.argv[1], sys.argv[2], sys.argv[3]
failures = []


def report(ok, label, detail=""):
    print(f"check: {label}: {'PASS' if ok else 'FAIL'}{(' (' + detail + ')') if detail else ''}")
    if not ok:
        failures.append(label)


files = []
for folder, _, names in os.walk(dest):
    for name in names:
        files.append(os.path.relpath(os.path.join(folder, name), dest))
files.sort()
basenames = [os.path.basename(path) for path in files]


def is_elf(path):
    with open(os.path.join(dest, path), "rb") as handle:
        return handle.read(4) == b"\x7fELF"


# The forbidden names: sherpa-onnx's native libraries (libsherpa-onnx*, its Windows sherpa-onnx-c-api*), ONNX Runtime's (libonnxruntime*,
# onnxruntime*.dll/.so/.dylib), espeak-ng (GPL code, core D2) and any ONNX model file.
forbidden = [
    (r"^libsherpa", "sherpa-onnx native library"),
    (r"sherpa-onnx-c-api", "sherpa-onnx native library"),
    (r"^libonnxruntime", "ONNX Runtime native library (CR-021)"),
    (r"^onnxruntime.*\.(so|dll|dylib)$", "ONNX Runtime native library (CR-021)"),
    (r"espeak", "espeak-ng (GPL, core D2)"),
    (r"\.onnx$", "ONNX model file"),
]
hits = []
for path, name in zip(files, basenames):
    for pattern, what in forbidden:
        if re.search(pattern, name, re.IGNORECASE):
            hits.append(f"{path} ({what})")
report(not hits, "no sherpa-onnx, ONNX Runtime, espeak-ng or ONNX model file in the folder", "; ".join(hits[:5]) or f"{len(files)} files scanned")

# The managed sherpa-onnx assembly (Apache-2.0 wrapper) may stay; it must be a managed assembly named in deps.json's runtime section, not a native
# library, and no native entry of deps.json may name sherpa or ONNX Runtime.
with open(os.path.join(dest, "Vocaterra.MonoGame.deps.json"), encoding="utf-8") as handle:
    deps = json.load(handle)
managed, native_entries = set(), []
for library in deps.get("targets", {}).values():
    for entry in library.values():
        for asset in entry.get("runtime", {}):
            managed.add(os.path.basename(asset))
        for asset in entry.get("native", {}):
            native_entries.append(asset)
sherpa_dlls = [name for name in basenames if re.match(r"^sherpa-onnx.*\.dll$", name, re.IGNORECASE)]
managed_ok = all(name in managed and not is_elf(files[basenames.index(name)]) for name in sherpa_dlls)
report(managed_ok, "sherpa-onnx.dll is the managed assembly only", ", ".join(sherpa_dlls) or "not present")
native_bad = [asset for asset in native_entries if re.search(r"sherpa|onnxruntime|espeak", asset, re.IGNORECASE)]
report(not native_bad, "deps.json names no sherpa or ONNX Runtime native asset", "; ".join(native_bad[:3]) or f"{len(native_entries)} native assets listed")

# The playtest's native set: Vosk's library must be there (the recognizer loads at run time).
vosk_native = [path for path in files if os.path.basename(path) == "libvosk.so"]
report(len(vosk_native) == 1 and is_elf(vosk_native[0]), "libvosk.so (Vosk, Apache-2.0) is present", vosk_native[0] if vosk_native else "missing")

# The models folder holds the manifest and the Vosk model only; every Vosk file matches the pinned sizes and SHA-256 sums.
top_models = sorted(os.listdir(os.path.join(dest, "models")))
report(top_models == ["manifest.json", vosk_id] and not any("kokoro" in d.lower() or "sherpa" in d.lower() for d in top_models),
       "models folder holds the Vosk model and its manifest only", ", ".join(top_models))
with open(manifest_path, encoding="utf-8") as handle:
    pinned = next(m for m in json.load(handle)["models"] if m["id"] == vosk_id)
mismatch = []
for entry in pinned["files"]:
    path = os.path.join(dest, "models", vosk_id, entry["path"])
    if not os.path.isfile(path) or os.path.getsize(path) != entry["bytes"]:
        mismatch.append(entry["path"])
        continue
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    if digest.hexdigest() != entry["sha256"]:
        mismatch.append(entry["path"])
report(not mismatch, f"Vosk model files match the manifest ({len(pinned['files'])} files)", ", ".join(mismatch[:5]))

# The bundle: its manifest and the speech WAVs (the S9 files, 226 in game-v1 at this commit).
bundle = os.path.join(dest, "content", "game-v1")
with open(os.path.join(bundle, "manifest.json"), encoding="utf-8") as handle:
    bundle_manifest = json.load(handle)
wavs = sorted(os.listdir(os.path.join(bundle, "speech"))) if os.path.isdir(os.path.join(bundle, "speech")) else []
report(len(wavs) == 226 and all(name.endswith(".wav") for name in wavs), "speech WAVs in the bundle", f"{len(wavs)} files")
size = sum(os.path.getsize(os.path.join(dest, path)) for path in files)
print(f"bundle: {bundle_manifest['bundleId']}, bundle hash {bundle_manifest['bundleHash']}")
print(f"folder: {len(files)} files, {size / (1 << 20):.1f} MiB")
if failures:
    print("FAILED: " + "; ".join(failures))
    sys.exit(1)
print("all checks passed")
PY

echo "== built: $DEST (run it with ./playtest.sh from that folder)"
