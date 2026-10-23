# ADR 0007: Keep espeak-ng (GPL-3.0) out of the shipped game; replace the Kokoro phonemizer with the in-house G2P

> **Note (M19e, 2026-10-21):** game-side matters in this record, such as the engine choice and the game's scenes, are now decided in the Vocaterra game repository, not in this one. The text below is unchanged.

> **Note (M19a, 2026-10-20):** this record names the project `Slg.*` (and the `SLG_*` variables). Those names are now `Vocaterra.Nlp.*` and `VOCATERRA_*` (the old variable names are read for one release); see [ADR 0009](0009-core-and-game-boundary.md). The text below is unchanged.

- Status: accepted
- Date: 2026-10-20
- Deciders: repository owner; raised by the implementation plan (M11, `// TODO(ADR)` in `KokoroSynthesizer`)

This ADR records an engineering decision and the evidence behind it. It is not legal advice; the repository owner has to get the
licensing conclusion confirmed before release, as `docs/nlp/history/HIGH_LEVEL_DOCS.md` ("Licensing") already says.

## Context

The high-level design wants a permissive shipped stack and names two GPL components that must stay out of the binary:
espeak-ng (GPL-3.0) and the current Piper runtime (GPL-3.0 since 2025). Kokoro-82M itself is Apache-2.0 and is the planned
engine for dynamic NPC speech. Two facts complicate that:

1. **The Kokoro bundle for sherpa-onnx brings espeak-ng with it.** `kokoro-multi-lang-v1_0` contains an `espeak-ng-data/`
   directory (see `models/manifest.json`, the notes of that entry). `KokoroSynthesizer` refuses to start without it, and passes
   it to sherpa-onnx as the phonemizer's data directory, so Spanish text is phonemized by espeak-ng, not by our code. The
   `SynthesisRequest.Phones` that `Slg.Response` builds with `Slg.Phonology` (`SpeechRequests.Create`) are accepted and ignored.
2. **The sherpa-onnx native library appears to contain espeak-ng itself.** Observed on 2026-10-20 in the NuGet package
   `org.k2fsa.sherpa.onnx.runtime.linux-x64` 1.13.8: `libsherpa-onnx-c-api.so` has no dynamic dependency on any espeak library
   (`ldd` lists only system libraries and `libonnxruntime.so`), but the binary contains espeak-ng strings and function names
   such as `espeak_Initialize`, `espeak_TextToPhonemesWithTerminator` and "espeak-ng library has not been initialized". That is
   what a statically linked espeak-ng looks like. The package's own metadata says `Apache-2.0`, and
   `scripts/license-scan.sh` passes, because the scan reads package metadata and cannot see inside a native binary. The same
   library also provides the Parakeet recognizer, so the concern is not limited to speech synthesis. The other platform
   packages were not inspected.

## Decision

1. **Do not ship espeak-ng, its data, or any binary known to include it, in a release build.** espeak-ng may be used as a
   development-time test oracle (as the high-level design allows), never as a runtime dependency.
2. **Replace the phonemizer with the in-house G2P.** `Slg.Phonology` already produces the Latin American neutral phone
   sequence for any Spanish text (D0, no GPL code). The plan is to hand Kokoro phone tokens from `Slg.Phonology` directly, so
   no phonemizer runs inside the synthesizer. Concretely: map the phones of `SynthesisRequest.Phones` to the token ids in the
   model's `tokens.txt`, and run the Kokoro model with those tokens, the fixed voice style and a fixed speed. The model file,
   voices and token table are not GPL. Whether that is done through a sherpa-onnx build without espeak-ng or directly through
   ONNX Runtime (already a dependency) is decided by a spike, because it depends on what the Kokoro model's inputs and the voices
   file look like (this ADR does not claim to have verified them).
3. **Resolve the native-library question before release.** Establish whether the shipped sherpa-onnx native binaries contain
   espeak-ng, on every target platform, by building them ourselves with the text-to-speech and phonemizer components
   disabled (sherpa-onnx offers build switches for this; confirm the exact options), by confirming with the upstream
   maintainers, or by legal review of what the GPL requires for the game if the library stays. Dropping sherpa-onnx for Parakeet
   in favor of ONNX Runtime directly is the fallback.
4. **Gate it.** Extend the license check so this cannot regress unseen: the Kokoro manifest entry must not list
   `espeak-ng-data/` among the shipped files, and release packaging must fail if an `espeak-ng-data` directory or a binary
   containing espeak-ng symbols is present. The NuGet license scan stays as it is; it is necessary but does not see inside binaries.
5. **Until then**, `KokoroSynthesizer` and the `tts` model are development tools. They are gated behind `SLG_MODELS_DIR` and
   not part of any shipped path, and static lines can be pre-rendered at build time so a release may not need runtime synthesis
   at all (the high-level design already calls for pre-rendered static lines).

## Consequences

- Dynamic speech with a clean license depends on work that is not done: the phone-to-token mapping, a way to run Kokoro
  without espeak-ng, and a decision about the native binaries. Until it is done, shipping dynamic TTS is blocked and shipping
  anything built on the current sherpa-onnx native packages needs a legal answer first.
- Doing the replacement gives a bonus: the learner hears the same phonemes the grader expects, and the phoneme sequences come
  from our dialect-aware G2P (ADR 0005), including the golden set. The risk is audio quality: the in-house phones must match
  what Kokoro was trained on, and stress and syllable boundaries must survive the mapping. A sensible check is a comparison
  of the mapped tokens with espeak-ng output for a word list, run only on developer machines.
- The decision adds an item to the checklist in `CONTRIBUTING.md` for D1 adapters: record the license of every native binary,
  not only of the package that carries it.
- The code comments `TODO(ADR)` in `src/Slg.Response/Speech/KokoroSynthesizer.cs` refer to this record. They can be reworded to
  point here when that file is next touched.
