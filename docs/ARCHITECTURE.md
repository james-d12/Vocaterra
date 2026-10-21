# Architecture: how the game consumes the language core

Vocaterra is the game. The language core, Vocaterra.NLP (`https://github.com/james-d12/Vocaterra.NLP`, Apache-2.0), is a separate repository.
This repository includes it as the git submodule `external/Vocaterra.NLP` and consumes its libraries as the packages `Vocaterra.Nlp.*`
version 0.1.0, from the submodule's local feed. This document says what the game takes from the core, what the game owns, and how the two
meet. The core's contract is [docs/API.md](https://github.com/james-d12/Vocaterra.NLP/blob/main/docs/API.md) there, and the boundary is that
repository's [ADR 0009](https://github.com/james-d12/Vocaterra.NLP/blob/main/docs/adr/0009-core-and-game-boundary.md). The game design is
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
| Content formats: the bundle loader, validator, hashing, provenance, the lexicon and CEFR lists | The game's content bundles (`content/`), built with the core's content tool |

The core knows nothing about prices, items, spells or quests. An effect's `kind` and an action's `hostEvent` are strings the game interprets.
A change to a public type in the core's API document is a change to this boundary, and the core updates its ADR when it makes one.

## What the game takes from the core

The host in `src/Vocaterra.Host` makes the calls of the core's API document, in order, against the packages only (no project references
to the core). Each step below names the call in the host.

1. **Load a bundle.** `ContentLoader.Load(directory)` verifies every file's hash and the cross-references, and returns the bundle with its
   `ContentHash`. The game's bundle is `content/game-v1`. A bundle is hashed and its manifest rewritten by the core's content tool, through
   `scripts/content.sh`.
2. **Build the services.** `PipelineServices` takes a word recognizer, an optional phoneme recognizer, an embedding model, a synthesizer and
   the bundle. The host uses the scripted recognizer (the typed line is the transcript), `FakeEmbeddingModel` and `SilentSynthesizer`, so no
   model file and no audio are involved yet. Real models come from the core's model store (`ModelStore.FromEnvironment()`, with
   `VOCATERRA_MODELS_DIR`) once the game needs them.
3. **Build a pipeline and evaluate.** `UtterancePipeline.Evaluate(clip, prompt, context)` returns a `PipelineResult`: the verdict, the
   decision (agent step, learner, rating, feedback), the agent's lines, the resolved effects and an optional repair step. `PromptSpec.Text`
   is the line the agent says before the player answers (null when there is none). The text loop shows it before each turn, under the
   prompt's header (`VerdictReport.WritePrompt`); a prompt without text shows the header alone. A `--transcript` run prints no prompt line,
   so it prints no prompt text either.
4. **Carry the conversation.** `ConversationContext` holds the world seed, the agent id and its state, the stored time, the learner model
   and the repeat counts per error tag. After each utterance the host calls the core's `ConversationContext.Next(result, now)`
   (`ConversationSession.Evaluate`), which builds the next context from this one and the result; it calls `RepeatCounts.Next(before,
   verdict)` inside it (core M19d, API document sections 4 and 5). The counts count each error tag, and `not_communicated` when the player
   was not understood, one more in a row, and an int stops at `int.MaxValue`. The host keeps the state of each agent it talks to in its own
   map, because `Next` carries only the agent of the utterance.
5. **Read the outcome.** The verdict's errors and correctness, the lines of the response plan and the effects are what the game maps to its
   own rules. The host prints them; the game does the mapping.

Speech, replay and the save of a log are the core's other calls (API document sections 6 and 7). The game does not use them yet.

## What the host owns

The core cannot know these, so the host supplies them (API document, "The host owns what the core cannot know"):

- **The world seed** (`ConversationSession.DefaultWorldSeed` until the game has saves). It seeds every random draw with the agent id and the
  interaction counter.
- **The stored time.** The text loop reads the clock; a one-shot `--transcript` run uses a fixed time so its output is repeatable.
- **The learner model.** It is persisted by the game and passed back in, as `LearnerModel.New()` for a new player.
- **The repeat counts** per error tag, counted in a row, which the feedback policy uses to vary its repair. The core provides the rule
  (`RepeatCounts.Next`, called by `ConversationContext.Next`); the host keeps the counts and passes them into the next utterance. A
  restored conversation passes the counts it saved to the `ConversationSession` constructor.
- **`HostStateHash`.** `ConversationContext.Create` computes it over the values above. A game with its own state digest uses the
  constructor instead, and recomputes it after `Next`.

## Versions, the submodule and the feed

- The packages are `Vocaterra.Nlp.*` 0.1.0, set in `Directory.Packages.props`. A version change is made there, with the code that depends on it.
- The core is the submodule `external/Vocaterra.NLP`, pinned by its commit in this repository's history (the gitlink). The pin moves only
  by the bump procedure in `.claude/agents/orchestrator.md`, which also clears the NuGet cache of the old packages.
- A fresh clone takes the core in two steps: `git submodule update --init` checks out the pinned commit, then
  `bash external/Vocaterra.NLP/scripts/pack.sh` fills the submodule's local feed `external/Vocaterra.NLP/artifacts/feed`. The pack takes
  minutes and writes about 3 GB, all git-ignored in the submodule.
- `nuget.config` reads `external/Vocaterra.NLP/artifacts/feed`, which the core's `scripts/pack.sh` fills. Nothing is published to a hosted
  feed (core D5, decided 2026-10-21).
- A git worktree has an empty submodule and does not pack the core again (about 3 GB each). It restores from the main checkout's feed:
  `dotnet restore Vocaterra.sln -p:RestoreSources="<main checkout>/external/Vocaterra.NLP/artifacts/feed;https://api.nuget.org/v3/index.json"`,
  then builds and tests with `--no-restore`.
- The content tool, `vocaterra-content`, comes from the same feed and is installed under `.tools/` by `scripts/content.sh`.
- The core is Apache-2.0 (core D3). This repository's own licence is not chosen (D-game-2 in [DECISIONS.md](DECISIONS.md)). Until it is,
  the code grants no permission to use it.
- The core's sherpa-onnx dependency carries GPL-3.0 code in its native build (core D2). Nothing here ships the native libraries, and the
  game does not distribute a build that includes them until D2 is settled in the core.

**Native-asset trim (G7).** The speech packages and MonoGame ship native libraries for every platform, and a plain build copied all of them
(about 400 MB in `src/Vocaterra.MonoGame/bin` alone). `Directory.Build.props` therefore sets `RuntimeIdentifier=linux-x64` (with
`SelfContained=false` and `AppendRuntimeIdentifierToOutputPath=false`, so the output stays at `bin/Debug/net10.0/`) when the build host is
Linux x64. NuGet then copies only that runtime's natives, and the same `dotnet build`, `dotnet test` and `dotnet run` commands work. The
property `VocaterraAllRuntimes=true` turns the condition off, so a release build that must carry every platform restores the full copy; other
build hosts never get the trim. Restore again after changing the property, because the assets file records the runtimes restored.

## The engine

The engine is MonoGame for the prototype and the final engine is open (D-game-1 in [DECISIONS.md](DECISIONS.md)); a Godot .NET front-end is a
later spike. The console host stays as the cheapest way to exercise the core's calls and the content. A front-end references the same packages
(through `Vocaterra.Game`), calls the same sequence and renders the outcome itself. Audio capture and playback belong to the front-end and stay
out of the core.

## The game layer (P3)

The prototype is three libraries and a front-end, so that the engine can change without rewriting the game:

- `src/Vocaterra.Simulation`: the world and the player body, generation and a fixed 60 Hz tick. No packages, no engine.
- `src/Vocaterra.Game`: `GameSession` (the simulation, the bundle, the world seed, the stored time, the guide and the conversation),
  `GuideService` (one typed turn over the packages), `WorldFacts` (the nearest tile, the direction and the distance in Spanish) and
  `TypedUtteranceEvaluator`, the call sequence of the steps above, which the console host shares. No engine type.
- `src/Vocaterra.MonoGame`: the only engine code: window, drawing, input, the dialogue panel. It names no `Vocaterra.Nlp.*` package.

How the guide's turn meets the host's duties (see `src/Vocaterra.Game/README.md` for the steps):

- **The stored time** is a fixed start plus `Tick / 60` seconds of game time (`GameSession.StoredTime`); nothing reads the clock, so a scripted
  run gives the same turns everywhere. The save file will own the start time.
- **`HostStateHash`** is the simulation's state digest (`GameSimulation.ComputeStateDigest()`), given to the context's constructor instead of the
  core's default digest and set again before each turn, because `ConversationContext.Next` recomputes the default.
- **Repeat counts.** The pipeline adds the utterance's own repeats to the counts in the context and `Next` carries them to the next utterance
  (CR-012, core M26a). The guide and the console host both evaluate once with the carried counts, so the recast, hint and correction ladder and
  the repair ladder move one step per repeat: a repeated mistake is recast once, then hinted.
- **Host facts in replies** (CR-003 in part, core M26d). The facts of the guide's line go in `hostSlots` of the second evaluation, so the replay
  record holds them. The line itself ("El árbol está a la derecha, a doce bloques.") is still rendered by the game from the bundle's dialogue
  pools with the bundle's template engine, because the core cannot pick one of three templates from a host fact (CR-016, open). The agent's
  reply and the correction come from the core.
- **Words and learner skills** (CR-002, CR-005, core M26c and M26d). The guide's evaluation reads the catalogue nouns of a turn from the core's
  resolved words, and gives each one its `word.<lemma>` skill in the same learner update, through `extraSkills`.
- **Host events.** The guide's state machine raises `guide.point`, `guide.identify` and `guide.help` (opaque strings to the core); the game maps
  them to the nearest tile, the cursor tile and a hint, and the front-end draws the arrow.
- **Evaluation** is synchronous in P3, between ticks and never inside `GameSession.Step`; P5 moves it to a worker thread.

## Where the rules are checked

- `tests/Vocaterra.Host.Tests` runs the two demo utterances through the host's evaluation path on `content/game-v1`, checks the bundle hash in
  the manifest against the loader's, and checks that the conversation carries its counts and learner from one utterance to the next. It
  checks that a count at `int.MaxValue` stays there, and that the prompt's line is written under its header (`PromptReportTests`).
- `tests/Vocaterra.Game.Tests` runs the guide's turns on a fixed seed and a fixed stored time with the scripted recognizer (the verdict, the
  guide's state, the host event, the target tile and the world line; an unaccented line; the repair path; determinism), `WorldFacts` on small
  grids, the guide's content in the bundle, and the layering rules of `Vocaterra.Game` (no clock, no `System.Random`, no engine type, packages only).
- The core's own suites (fuzzing, goldens, replay) test the packages; this repository tests only what the game does with them.
- The continuous integration job is parked in `.github/workflows-pending/ci.yml` (D-game-3, deferred by the owner).
