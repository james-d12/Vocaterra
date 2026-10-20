Model files (ASR, phoneme recognizer, embedding and TTS models) are not committed to this repository; they are fetched by a script and verified against the SHA-256 hashes listed in `manifest.json`, which is the only model metadata under version control. Set `VOCATERRA_MODELS_DIR` to a directory containing the fetched files to run the real-model tests; without it those tests are skipped.

## Fetching

```
scripts/fetch-models.sh --list                      # what is present, missing or mismatching
VOCATERRA_MODELS_DIR=$PWD/models scripts/fetch-models.sh vosk-model-small-es-0.42
```

The script (bash, python3, curl, sha256sum, unzip or tar) downloads each requested model into `${VOCATERRA_MODELS_DIR:-<repo>/models}/<id>/`, checks the archive hash, extracts it, and verifies every listed file. Without ids it fetches every model that has an archive; entries without one (the ONNX models that still need a manual export) are skipped with a warning. The top-level directory of each archive must be named like the model id.

## Manifest

`manifest.json` is `{ "schemaVersion": 1, "models": [...] }`. Each model has `id` (also the directory name), `role` (`asr-constrained`, `asr-open`, `phoneme`, `embedding`, `tts`), `name`, `version`, `license`, `source` (URL), an optional `archive` (`format` `zip` or `tar.bz2`, `sha256`, `bytes`), `files` (`path` relative to the model directory, `sha256`, `bytes`) and optional `notes`.

A `null` hash means "not pinned yet": the fetch script warns instead of verifying, `ModelStore.Verify` reports `Unpinned`, and `ModelStore.ModelHash` refuses to produce a model hash. The model hash is `DeterministicHash.Combine` of the ordinal-sorted file hashes.

| id | role | state |
| --- | --- | --- |
| `vosk-model-small-es-0.42` | `asr-constrained` | archive and all 14 files pinned from a real download |
| `sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` | `asr-open` | archive hash is the GitHub release digest; file hashes pending the first fetch |
| `wav2vec2-xlsr-53-espeak-cv-ft` | `phoneme` | needs an ONNX export; hashes pending |
| `multilingual-e5-small` | `embedding` | needs an ONNX export; hashes pending |
| `kokoro-multi-lang-v1_0` | `tts` | archive hash is the GitHub release digest; file hashes pending the first fetch |

## Determinism note for Vosk

Stock Vosk models leave Kaldi's default dithering on, so decoding the same audio twice gives slightly different scores. `VoskWordRecognizer` therefore loads a runtime copy of the model (links to the original files, with `--dither=0.0` appended to `conf/mfcc.conf`) kept under the temp directory in `slg-vosk-runtime/`; the files in this directory are never modified.
