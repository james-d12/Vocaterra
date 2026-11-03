# Known gaps and next steps

What the core does not do yet, and what it does only partly. Milestones and their status are in [implementation-plan.md](implementation-plan.md) §5.

- **Engine integration.** This folder is the core, not a host. A host still has to capture the microphone (v1 has no
  operating-system capture adapter, only WAV files and `PushToTalkSession`), play the synthesized audio, map its saves to
  `ConversationContext` and back, apply the resolved effects and the agent's host events, and ship the native speech libraries. The
  engine is the host's own choice ([ADR 0003](adr/0003-engine-agnostic-core.md)); the boundary is in [ADR 0009](adr/0009-core-and-game-boundary.md).
- **Real golden learner audio.** `tests/golden/manifest.jsonl` has three synthetic entries and `tests/golden/audio/` is empty. Until
  recordings of real non-native speakers exist, word error rate, learner-error preservation, the pronunciation thresholds
  (initial, uncalibrated guesses) and the latency budget of the design document are unmeasured.
- **ONNX exports for wav2vec2 and e5.** The upstream wav2vec2 phoneme model ships PyTorch weights only and multilingual-e5-small
  needs its ONNX file copied in; neither is pinned in `models/manifest.json`. Until they are, pronunciation scoring and the
  embedding rung of intent resolution run only with fakes in tests. The embedding tokenizer is unverified against the real
  e5 files. The Parakeet and Kokoro file hashes are pinned (M26e, 2026-10-21).
- **Kokoro phonemizer licensing.** Kokoro through sherpa-onnx phonemizes with espeak-ng (GPL-3.0), and the sherpa-onnx native
  library appears to link it statically. The plan is to feed Kokoro phones from the in-house G2P and to settle the native
  binaries before release ([ADR 0007](adr/0007-kokoro-phonemizer-licensing.md)). Dynamic TTS must not ship until then.
- **App-local ICU.** Shipped builds should carry a pinned ICU instead of using the machine's ([ADR 0006](adr/0006-globalization-icu.md)).
- **Cross-platform CI.** `.github/workflows/ci.yml` (active since 2026-10-31) runs on Linux x64 only, so the cross-platform determinism check
  (ARM64, Windows, macOS) has not run anywhere but Linux x64.
- **Content.** The vocabulary is large (3 372 lemmas) and the repository's one bundle is the game's: seven agents, a few dozen prompts, the lessons' intents. The
  core's tests run on it, so a lesson that breaks the core fails a core test. Its effect table is empty, and no intent binds a free name (`[WORD]`); the tests
  build small fixtures for both. `pron.stress` is recognized by the taxonomy but is not scored in v1.
- **Open decisions.** The sherpa-onnx native build (D2) and the terms of the frequency list (D4) decide what may be released publicly; the
  rest are in [docs/nlp/decisions.md](decisions.md).
- **Build size.** A full build writes about 3 GB per checkout; trimming the runtime packs is a follow-up.
- **Part of speech.** Since M18 the analyzer resolves each word to one part of speech before it checks anything, from the neighbouring words, the
  slot grammar that matched and, last, the reading with the fewest errors ([how](pipeline.md#4-linguistic-analysis-d0)), so correct sentences
  with `libro`, `lanza`, `vino` or `ayudas` analyze clean and the mistakes in sentences with them are still found. Two verbs that share a form (`fue` is `ir`
  and `ser`) are not told apart, and a sentence that is a mistake under one reading and correct under another, with nothing around the word to
  prefer either, is read the charitable way ([fuzz suite findings](../../tests/Vocaterra.Nlp.Fuzzing.Tests/README.md)).
- **Pronunciation cost.** Scoring grows with the cube of the phone count, because `PredictedErrorMatcher.Match` aligns against the reference once
  per predicted-error variant. Spoken utterances are far below the sizes where this matters ([fuzz suite findings](../../tests/Vocaterra.Nlp.Fuzzing.Tests/README.md)).
- **Content defaults.** The content loader silently defaults missing number, flag and enumeration fields to 0, false or the first member. The
  places where this is accepted are listed in [`KnownTolerances.cs`](../../tests/Vocaterra.Nlp.Fuzzing.Tests/Oracles/KnownTolerances.cs) (`AcceptedContentField`).
- **WAV sample rate.** `WavFile.Read` does not validate the sample-rate range, and the resampler rebuilds its kernel bank on every call, so a
  damaged header can inflate one clip's output or make it slow ([fuzz suite findings](../../tests/Vocaterra.Nlp.Fuzzing.Tests/README.md)).
- **Replay envelope copies.** The replayer does not cross-check the envelope's duplicate `result/verdict` and `result/replay` copies against the
  core record, so an edited log can replay clean ([fuzz suite findings](../../tests/Vocaterra.Nlp.Fuzzing.Tests/README.md)).
