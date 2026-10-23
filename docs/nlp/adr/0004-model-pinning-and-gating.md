# ADR 0004: Pin models by hash and gate real-model code

> **Note (M19e, 2026-10-21):** game-side matters in this record, such as the engine choice and the game's scenes, are now decided in the Vocaterra game repository, not in this one. The text below is unchanged.

> **Note (M19a, 2026-10-20):** this record names the project `Slg.*` (and the `SLG_*` variables). Those names are now `Vocaterra.Nlp.*` and `VOCATERRA_*` (the old variable names are read for one release); see [ADR 0009](0009-core-and-game-boundary.md). The text below is unchanged.

- Status: accepted
- Date: 2026-10-20
- Deciders: repository owner and the implementation plan (`IMPLEMENTATION_PLAN.md` section 3, rules 7 and 9)

## Context

The system uses four kinds of neural model (D1 in the determinism model): a constrained recognizer (Vosk), an open recognizer
(Parakeet through sherpa-onnx), a phoneme recognizer (wav2vec2) with an embedding model (multilingual-e5-small), and a speech
synthesizer (Kokoro). They are large (about 50 MB to well over 1 GB each), they are not ours to commit, and a different file
or a different runtime can change a verdict. Replay logs and saves are only meaningful if they say exactly which models
produced them. Contributors and CI runners must also be able to build and test without downloading anything.

## Decision

1. **Models are never committed.** `.gitignore` excludes `models/*` except `models/manifest.json` and `models/README.md`.
   `scripts/fetch-models.sh` downloads archives, checks the archive SHA-256, extracts them to
   `${SLG_MODELS_DIR:-<repo>/models}/<id>/` and verifies every listed file.
2. **`models/manifest.json` is the pin.** Each entry has an `id` (also the directory name), a `role`, `name`, `version`,
   `license`, `source`, an optional `archive` (format, SHA-256, size) and the `files` with `path`, SHA-256 and size. A `null`
   hash means "not pinned yet": the fetch script warns, `ModelStore.Verify` reports `Unpinned`, and `ModelStore.ModelHash`
   refuses to produce a hash for that model.
3. **The model hash is derived, not declared.** `ModelStore.ModelHash(id)` is `DeterministicHash.Combine` of the
   ordinal-sorted file hashes. Every adapter exposes it as `ModelHash`, and `PipelineServices` copies the hashes into the
   `VersionStamp.ModelHashes` map (keys `asr`, `phoneme`, `embedding`, `tts`, sorted ordinally) next to the content hash. The
   `VersionStamp` goes into every verdict and every replay record.
4. **Adapters are deterministic by configuration.** One intra-op thread, CPU execution provider, greedy decoding, no
   sampling. Stock Vosk models leave Kaldi's dithering on and then give slightly different scores for the same audio, so
   `VoskWordRecognizer` decodes with a temporary copy of the model whose `conf/mfcc.conf` has `--dither=0.0` appended; the
   files in the models directory are never modified (finding recorded in `IMPLEMENTATION_PLAN.md` section 5b).
5. **Adapters are gated.** An adapter implements a `Slg.Core` interface, so everything else is tested with fakes
   (`ScriptedWordRecognizer`, `ReplayWordRecognizer`, `FakePhonemeRecognizer`, `FakeEmbeddingModel`). A test that needs the real
   model runs only when `SLG_MODELS_DIR` is set and `ModelStore.Verify` says the files match the manifest; otherwise xUnit
   reports it as skipped, never failed. The optional `models` job in `.github/workflows-pending/ci.yml` opts in with the
   repository variable `SLG_RUN_MODEL_TESTS` and fetches only the Vosk model.
6. **Replays do not need models.** The replay envelope stores the recognizers' outputs, so a log can be checked on a machine
   that has no models at all (`docs/REPLAY.md`).

## Consequences

- A fresh clone builds and tests green with no downloads. Real-model coverage exists only where a developer or the opt-in CI
  job has fetched the models.
- Today only `vosk-model-small-es-0.42` is fully pinned (archive and all 14 files, from a real download). Parakeet and Kokoro
  have the archive digest from the GitHub release but null file hashes until their first fetch; wav2vec2 and
  multilingual-e5-small need a manual ONNX export before they can be pinned (the upstream repositories ship PyTorch weights,
  or an ONNX file that has to be copied in). Until then the pronunciation scorer and the embedding rung run only with fakes
  in tests, and the CLI runs without them.
- Changing a model, or the runtime that executes it, changes the hash and therefore the `VersionStamp`. Old replay logs
  stay valid records of what happened under the old stamp; they are not expected to re-run with a new model.
- A model update is a reviewed change to `models/manifest.json` with new hashes, and should be accompanied by a run of the
  golden audio set once real learner recordings exist (`golden/README.md`).
- The licenses of the models are recorded in the manifest and repeated in `content/v1/ATTRIBUTION.md`. The Parakeet model is
  CC-BY-4.0 and needs attribution; the Kokoro phonemizer has an open licensing question (ADR 0007).
