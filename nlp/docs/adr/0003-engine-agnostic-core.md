# ADR 0003: Keep the core engine-agnostic

> **Note (M19a, 2026-10-20):** this record names the project `Slg.*` (and the `SLG_*` variables). Those names are now `Vocaterra.Nlp.*` and `VOCATERRA_*` (the old variable names are read for one release); see [ADR 0009](0009-core-and-game-boundary.md). The text below is unchanged.

- Status: accepted
- Date: 2026-10-20
- Deciders: repository owner and the implementation plan (`IMPLEMENTATION_PLAN.md` section 1, "Engine")

## Context

The game engine has not been chosen. The high-level design lists Godot .NET and MonoGame as the C# candidates and Bevy as the
Rust one, and its open questions still ask for a final engine. The voice recognition, decision and feedback system is the
hard part of the project and does not depend on rendering, input or scene management. Tying it to an engine now would make
the engine choice irreversible and would make the system untestable outside the editor.

## Decision

- Everything under `src/` is a plain `net10.0` class library with no reference to an engine, a UI toolkit or an audio device.
- The libraries are layered as in `IMPLEMENTATION_PLAN.md` section 2. `Slg.Core` holds the contracts (the verdict, the stage
  records, the determinism primitives, the error taxonomy) and depends on nothing. The stage libraries (`Slg.Audio`,
  `Slg.Phonology`, `Slg.Nlp`, `Slg.Pronunciation`, `Slg.Asr`, `Slg.Evaluation`, `Slg.Decision`, `Slg.Response`) depend only on
  `Slg.Core`, except `Slg.Pronunciation` (also `Slg.Phonology`) and `Slg.Response` (also `Slg.Nlp` for morphology and
  `Slg.Phonology` for phones). `Slg.Content` loads and hashes the data bundle. `Slg.Pipeline` wires everything together.
  There are no cycles.
- The outside world enters through interfaces: `ICaptureSource` for audio input, `IWordRecognizer`, `IPhonemeRecognizer`,
  `IEmbeddingModel` and `ISpeechSynthesizer` for models. Models and capture devices are adapters behind them; tests and the
  replayer use deterministic fakes.
- The host is a thin console program. `tools/Slg.Cli` is the demo host (`eval`, `replay`), `tools/Slg.Bench` runs the ASR
  benchmark and `tools/Slg.ContentBuilder` is the build-time content tool. An engine integration will be a fourth host with
  the same shape: build a `PipelineServices`, build a `GameContext` from the save, call `UtterancePipeline.Evaluate`, apply
  the `PipelineResult`.
- The pipeline owns no game state. The caller supplies a `GameContext` (world seed, NPC id and state, stored time, learner
  model, repeat counts) and receives the updated learner model and the NPC's next state inside the `PipelineResult`; the caller
  decides when and how to save them.
- v1 has no operating-system microphone adapter. `WavFileCaptureSource` reads files and `PushToTalkSession` assembles a clip
  from appended buffers, which is what an engine's capture callback will feed.

## Consequences

- The whole system builds, runs and is tested on any machine with the SDK, including CI, without an engine installed.
- The engine integration is deferred work, and its size is not yet known. At minimum it has to provide: push-to-talk capture
  that feeds `PushToTalkSession`; playback of the `AudioBuffer`s the synthesizer returns; a mapping from save data to
  `GameContext` and back; handling of the `GameEffect` values and NPC game events (such as `trade.offer`) the pipeline
  returns; worker threads for the recognizers (the pipeline runs ASR and phoneme recognition on two tasks and combines them in
  a fixed order, but the engine decides on which thread `Evaluate` runs).
- The native packages (Vosk, sherpa-onnx, ONNX Runtime) have to be carried by the engine's export step for every target
  platform. This has not been tried.
- Because nothing in the libraries knows about frames or ticks, latency budgets from the high-level design have to be met by
  the host (lazy model loading, worker threads), not by the libraries.
- If the engine turns out to be Rust-based, the contracts, content bundle, replay format and tests described in this
  repository are still the specification (see ADR 0001).
