# Architecture: how the game consumes the language core

Vocaterra is the game. The language core, Vocaterra.NLP, is a separate repository
(`https://github.com/james-d12/Vocaterra.NLP`) whose libraries are published to a package feed as `Vocaterra.Nlp.*` version 0.1.0. This
document says what the game takes from the core, what the game owns, and how the two meet. The core's contract is
[docs/API.md](https://github.com/james-d12/Vocaterra.NLP/blob/main/docs/API.md) there, and the boundary is that repository's
[ADR 0009](https://github.com/james-d12/Vocaterra.NLP/blob/main/docs/adr/0009-core-and-game-boundary.md). The game design is
[SPECIFICATION.md](SPECIFICATION.md).

## The boundary

The core takes a learner's utterance, from audio or a transcript, to a deterministic verdict, feedback, a conversation step and the agent's
response text. The game takes that outcome and decides what it means in the world.

| The core provides (`Vocaterra.Nlp.*`) | The game owns (this repository and its engine) |
| --- | --- |
| The verdict contract: intent, communicated, correctness, tagged errors | Scenes, characters, dialogue pools and the agents' stories |
| Speech recognition, phonology, pronunciation, NLP and evaluation (behind interfaces) | Items, spells, quests, prices and rewards, and the meaning of each effect kind |
| The learner model (BKT), scheduling (FSRS) and the feedback policy | The save file, the world seed, the stored time and the host state digest |
| The conversation state-machine engine (`ConversationAgents`), with opaque host event names | Mapping host events and effect kinds to game state |
| Response templates, recasts, speech synthesis, and the effect resolver with opaque kinds | Rendering, device audio capture, playback, and the engine itself |
| Content formats: the bundle loader, validator, hashing, provenance, the lexicon and CEFR lists | The game's content bundles (`content/`), built with the core's ContentBuilder |

The core knows nothing about prices, items, spells or quests. An effect's `kind` and an action's `hostEvent` are strings the game interprets.
A change to a public type in the core's API document is a change to this boundary, and the core updates its ADR when it makes one.

## What the game takes from the core

The host in `src/Vocaterra.Host` makes the calls of the core's API document, in order, against the packages only (no project references
to the core). Each step below names the call in the host.

1. **Load a bundle.** `ContentLoader.Load(directory)` verifies every file's hash and the cross-references, and returns the bundle with its
   `ContentHash`. The game's bundle is `content/game-v1`. A bundle is hashed and its manifest rewritten by the core's ContentBuilder, through
   `scripts/content.sh`.
2. **Build the services.** `PipelineServices` takes a word recognizer, an optional phoneme recognizer, an embedding model, a synthesizer and
   the bundle. The host uses the scripted recognizer (the typed line is the transcript), `FakeEmbeddingModel` and `SilentSynthesizer`, so no
   model file and no audio are involved yet. Real models come from the core's model store (`ModelStore.FromEnvironment()`, with
   `VOCATERRA_MODELS_DIR`) once the game needs them.
3. **Build a pipeline and evaluate.** `UtterancePipeline.Evaluate(clip, prompt, context)` returns a `PipelineResult`: the verdict, the
   decision (agent step, learner, rating, feedback), the agent's lines, the resolved effects and an optional repair step.
4. **Carry the conversation.** `ConversationContext` holds the world seed, the agent id and its state, the stored time, the learner model
   and the repeat counts per error tag. The host builds one per utterance with `ConversationContext.Create`, and takes the next agent state
   from `Decision.Agent.Next` and the learner from `Decision.Learner`. `ConversationSession` in the host does this for a text loop.
5. **Read the outcome.** The verdict's errors and correctness, the lines of the response plan and the effects are what the game maps to its
   own rules. The host prints them; the game does the mapping.

Speech, replay and the save of a log are the core's other calls (API document sections 6 and 7). The game does not use them yet.

## What the host owns

The core cannot know these, so the host supplies them (API document, "The host owns what the core cannot know"):

- **The world seed** (`ConversationSession.DefaultWorldSeed` until the game has saves). It seeds every random draw with the agent id and the
  interaction counter.
- **The stored time.** The text loop reads the clock; a one-shot `--transcript` run uses a fixed time so its output is repeatable.
- **The learner model.** It is persisted by the game and passed back in, as `LearnerModel.New()` for a new player.
- **The repeat counts** per error tag, counted in a row (`ConversationSession.NextRepeatCounts`), which the feedback policy uses to vary its
  repair. The core has no helper for them; this is the one piece of the context the host computes itself.
- **`HostStateHash`.** `ConversationContext.Create` computes it over the values above. A game with its own state digest uses the
  constructor instead.

## Versions and the feed

- The packages are `Vocaterra.Nlp.*` 0.1.0, set in `Directory.Packages.props`. A version change is made there, with the code that depends on it.
- The packages come from the core's local feed (`nuget.config` points at `../SpanishLearningGame/artifacts/feed`, which the core's
  `scripts/pack.sh` fills). Nothing is published, and the feed is undecided (the core's DECISIONS.md D5); the GitHub Packages source
  replaces the local one when it is chosen.
- The core's licence is not chosen (D3) and neither is this repository's. Until both are, the packages grant no permission to use them.
- The core's sherpa-onnx dependency carries GPL code in its native build (D2 in the core). Nothing here ships the native libraries, and the
  game does not distribute a build that includes them until D2 is settled.

## The engine

The engine is not chosen (D12 in the core's DECISIONS.md). The host is a console program so that the core's calls can be exercised without
one. A game engine (Godot .NET is the research report's suggestion) would reference the same packages, call the same sequence and render
the outcome itself. Audio capture and playback belong to the engine and stay out of the core.

## Where the rules are checked

- `tests/Vocaterra.Host.Tests` runs the two demo utterances through the host's evaluation path on `content/game-v1`, checks the bundle hash in
  the manifest against the loader's, and checks that the conversation carries its counts and learner from one utterance to the next.
- The core's own suites (fuzzing, goldens, replay) test the packages; this repository tests only what the game does with them.
- The continuous integration job is parked in `.github/workflows-pending/ci.yml` until the workflow scope and the feed are settled.
