# Architecture: how the game consumes the language core

Vocaterra is the game: the projects `Vocaterra.Simulation`, `Vocaterra.Game`, `Vocaterra.MonoGame`, `Vocaterra.Host` and `Vocaterra.SpeechRender` of the repository. The language core, Vocaterra.NLP (the engine project `Vocaterra.Nlp` and its `Vocaterra.Nlp.*` tests and tools, Apache-2.0), is the other half of the same `src/`, `tests/` and `tools/` folders;
the two were separate repositories until 2026-10-21 (D-game-7) and sibling folders until the root layout of R01 (2026-10-23). The game references the core's library projects under `src` directly
(`Vocaterra.Game` and `Vocaterra.Host` list them as `ProjectReference`s) and copies nothing of its source. This document says what the game
takes from the core, what the game owns, and how the two meet. The core's contract is [docs/nlp/API.md](../nlp/API.md), and the
boundary is [ADR 0009](../nlp/adr/0009-core-and-game-boundary.md). The game design is [SPECIFICATION.md](SPECIFICATION.md).

## The boundary

The core takes a learner's utterance, from audio or a transcript, to a deterministic verdict, feedback, a conversation step and the agent's
response text. The game takes that outcome and decides what it means in the world.

| The core provides (`Vocaterra.Nlp`) | The game owns (this repository and its engine) |
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

Speech, replay and the save of a log are the core's other calls (API document sections 6 and 7). Since P5 the game recognizes speech (Vosk) and, for the scripted `--speak` lines, synthesizes it (Kokoro behind `CachedSynthesizer`); it does not use replay logs yet, and the guide does not speak until P8.

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

## Versions and the build

- The game and the core are built from the same commit by the root `Vocaterra.sln`: `Vocaterra.Game` and `Vocaterra.Host` reference the
  core's project (`../Vocaterra.Nlp/Vocaterra.Nlp.csproj`), so a core change is visible to the game in the same build. There is no
  package version to bump, no feed and no pack step (M50 retired the packaging scripts and the sample host; core D5 is superseded); the root
  `nuget.config` lists nuget.org only.
- Third-party package versions for both halves are in the root `Directory.Packages.props`.
- A core change the game needs is a core milestone in `docs/nlp/IMPLEMENTATION_PLAN.md`; a game milestone never edits the engine `Vocaterra.Nlp` or its `Vocaterra.Nlp.*` projects.
- The content tool is the core's `tools/Vocaterra.Nlp.ContentBuilder`, run from source by `scripts/content.sh`.
- The core is Apache-2.0 (core D3); the game is Apache-2.0 too (the whole repository since R01; D-game-2 in [DECISIONS.md](DECISIONS.md) was MIT).
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

The prototype is one engine-free library and one front-end, so that the engine can change without rewriting the game (the old
`Vocaterra.Simulation` and the engine-free half of the old `Vocaterra.Game` are now `Vocaterra.Game.Core`):

- `src/Vocaterra.Game.Core`: the game's rules and mechanics, with no engine type, no clock and no `System.Random` (`ArchitectureRulesTests`). By
  folder:
  - `Voxel/`: the blocks, the chunks and the grid, the block ray and the line of sight.
  - `Mathematics/`: the integer angles and the fixed-point helpers. `Physics/`: the player's body and its collision with the grid.
  - `Simulation/`: the fixed 60 Hz tick (`VoxelSimulation`: gathering, placing, crafting), its commands and its saved state.
  - `World/`: the built `Scene`; `World/Layout/`: the layout file and its parser; `World/Generation/`: the terrain and the scene builder.
  - `Navigation/`: the beacon and the scripted walk's path search.
  - `Session/`: `VoxelSession` (the simulation, the bundle, the world seed, the stored time, the guide, the conversation, the camp objective and the
    word cards).
  - `Npc/`: `NpcService` (the three NPCs' conversations over the packages: a context per agent, host turns, two-phase turns, S6), `WorldFacts`
    (the nearest block, the direction and the distance in Spanish), the aim at an NPC and the NPCs' shown names.
  - `Language/`, `Items/`, `Save/`, `Sound/`, `Event/`, `Reporting/`, `Tip/` and `Input/` for the rest; `Language/Speech/TypedUtteranceEvaluator`
    is the call sequence of the steps above, which the console host shares.
- `src/Vocaterra.Game`: the MonoGame front-end and the only engine code: window, drawing, sound output, input, the panels. `VoxelGame` is the
  host and the composition root (R16): its constructor makes the components, and MonoGame's `Update` and `Draw` call them in the frame's order.
  By folder:
  - `Input/`: the frame's `InputSnapshot`, the `KeyBindings` of the `GameCommands` (the command pattern), `WorldInput` (look, walk, hotbar,
    gather, place), `ListenSession` (push-to-talk), the text boxes' editing keys and the swallowed opening key.
  - `Screens/`: the screens over the world, each an `IScreen` in the `ScreenStack` that gives the keys to the top one: the mic check, the
    check battery, the dialogue, the controls help, the book, the inventory and the crafting panel.
  - `Frame/`: `WorldStepper`, one tick of the world and its `ITickObserver`s (the observer pattern): the arm, the sounds, the figures, the
    camera, the objective, the voice, the word cards and the hints.
  - `Flow/`: the game's flows that span screens: the session's start and ending, the playtest, the guide's greeting, the beacon, the turns
    applied, the objective's and the word cards' announcements.
  - `Persistence/` (the saves, the autosave, the crash source), `Reporting/` (the F8 report), `Scripted/` (the end of a scripted run),
    `Diagnostics/` (the console's event lines), `Graphics/` (the camera, the figures, the arm, the `SceneDrawer`), `Sound/` and `UI/` (the
    models and the layers).

How the guide's turn meets the host's duties (see `src/Vocaterra.Game/README.md` for the steps):

- **The stored time** is a fixed start plus `Tick / 60` seconds of game time (`GuideSession.StoredTime`); nothing reads the clock, so a scripted
  run gives the same turns everywhere. The save file will own the start time.
- **`HostStateHash`** is the simulation's state digest (`VoxelSimulation.ComputeStateDigest()`), given to the context's constructor instead of the
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
  them to the nearest block, the cursor block and a hint, and the front-end draws the beacon.
- **Evaluation** never runs inside `VoxelSession.Step`. Since P5 it runs on the turn worker (next section); `GuideSession.Say` is the synchronous
  path that the tests and any headless caller still use.

## Spoken input, the worker thread and the models folder (P5)

A turn is either typed (the line is the transcript, the core's scripted recognizer) or spoken (a 16 kHz clip decoded by Vosk). Both go through
the same `NpcService` turn, and each turn records how it arrived (`GuideTurn.Input`) so that word progress applies the credit of that kind
(`CreditRules`: typed input half, spoken full, D-game-6; `--typed-credit full` switches typed back to full).

- **One worker thread owns the recognizer.** `TurnWorker` (in `Vocaterra.Game`, no engine type) is the only thread that loads the Vosk model
  (`VoskWordRecognizer`, one instance), decodes with it, runs the guide's evaluations and disposes it, as the core's thread rules require
  (API document section 8: Vosk is not safe for concurrent calls and must not be disposed while a call may run). The pipeline is built with
  `RunRecognizersConcurrently = false`, so Vosk is never called from a pool thread. The game thread never calls the recognizer or the guide.
- **Submit, then apply.** The frame calls `GuideSession.SubmitTyped / SubmitClip / SubmitWav / SubmitSpeak` between ticks and goes on. Every
  input of the evaluation is fixed at that moment: the stored time, the simulation's state digest, a copy of the inventory and a copy of the
  blocks around the player (`WorldFacts.Snapshot`), so the worker never reads a world that keeps changing and a turn does not depend on when
  the worker gets to it. `GuideSession.Collect` (called at the start of each frame through `TurnPump`) takes the finished turns in submission
  order and applies them on the game thread: the arrow, the review question, the word counts and the panel. The panel shows "Thinking…"
  while a turn is out and "Listening…" while the key is held. A scripted (`--screenshot`) run freezes the world while a turn is out, so its
  ticks and output do not depend on how fast the worker is.
- **The double evaluation recognizes once** (CR-017). The guide evaluates a line twice (the first finds the verdict, the words and the host
  events; the facts and skills they give go into the second, whose result is the one kept). For a spoken line the first evaluation is
  `UtterancePipeline.Trace` with Vosk, whose `PipelineTrace.Asr` is kept; the second is `TypedUtteranceEvaluator.EvaluateRecognized`, the core's
  scripted recognizer over that `AsrResult` with the real clip (so the replay record names the audio), so both verdicts see the same transcript,
  hypotheses and confidence and the clip is decoded once.
- **Vosk runs single-best.** With the pipeline's default of five n-best hypotheses `AsrResult.Confidence` is a softmax over near-identical
  hypotheses and measured 0.34 to 0.66 for clean, correct speech, under the verdict's communicated threshold of 0.6 (CR-022). The spoken
  pipeline asks for one hypothesis (`SpokenUtteranceEvaluator.SingleBest`), the result's confidence is then 1.0, and the turn line shows the mean
  of Vosk's per-word confidences instead (`GuideTurn.Confidence`). The intent stage decides whether the words were understood.
- **Loading screen.** The bundle loads before the window opens (about 0.6 s). The worker then warms up the guide with a typed evaluation, loads
  Vosk (about 0.25 s) and runs one spoken evaluation of half a second of silence, while the window shows "Cargando la voz…" and the world does
  not step and reads no key. Kokoro is not loaded at start: it loads an engine for every line it speaks (about 1.5 to 2.5 s), so it is only
  used for the scripted `--speak` lines, through the core's `CachedSynthesizer` over a directory cache (`~/.cache/vocaterra/tts`).
- **The models folder.** `VOCATERRA_MODELS_DIR` names a folder with `vosk-model-small-es-0.42` and `kokoro-multi-lang-v1_0` (fetched outside the
  repository with the core's `scripts/fetch-models.sh`, never committed). The core's `ModelStore` reads `manifest.json` from that folder, and
  the fetch script does not copy it there (CR-019), so `SpeechEngine` falls back to `VOCATERRA_MODELS_MANIFEST` and then to the core's
  `models/manifest.json` found above the executable. With no folder, no model or no manifest the game says so in English ("Speech off...") and typing
  works as before; the model tests are skipped with the reason.
- **Native libraries.** `libvosk.so` is copied by Vosk's `build/Vosk.targets`, which NuGet imports only into a project that references `Vosk`
  directly, so the executable and the test projects that run the recognizer name `Vosk` 0.3.38 (the core's own version, Apache-2.0) in
  `Directory.Packages.props`. The same projects name `Microsoft.ML.OnnxRuntime` 1.31.0 with `ExcludeAssets="native"` because its
  `libonnxruntime.so` would overwrite sherpa-onnx's in the same folder and Kokoro would fail to load (CR-021); the game uses the core's fake
  embeddings, so it never needs that copy.
- **Push-to-talk and the microphone** (`src/Vocaterra.MonoGame` only). MonoGame's `Microphone` (OpenAL capture) records at the device's own rate
  (44.1 kHz mono on the development machine) and `AudioClips.Prepare` converts to 16 kHz, trims the silence at both ends and cuts at 8 seconds.
  Hold **V** or **Space** while the dialogue is open (S7: **Tab** now opens the text box), or hold **V** with the crosshair on an NPC to open it and listen; letting go sends the clip. No PortAudio was
  needed. MonoGame 3.8.6 only fills the buffer `GetData` reads while something subscribes to `BufferReady`, so `MicrophoneCapture` subscribes an
  empty handler. With no microphone the panel says "No microphone: type your answer." and the keyboard keeps working.
- **Speech UX (S7).** The uncertainty rule (`UncertaintyRule`, D-game-9) works on the per-word confidences the core already returns in
  `AsrResult.Words` (the result's own confidence is 1.0 for the single-best decode, CR-022), leaving out `[unk]`; a line the core understood on words
  under the threshold is held back by `NpcService` before phase 2, so nothing moves in the conversation, the learner model or the world. A turn the player
  cancels is rolled back on the worker (`NpcService.Checkpoint` and `Rollback`) and its outcome dropped by `GuideSession.Collect`. The NPC prompts at the stall
  and the guide's offer are Constrained in the bundle: `UtterancePipeline.Analyze` builds the Vosk grammar from the prompt's answer set, so the game only
  evaluates on the prompt. The expected phrase of the panel's heard-versus-expected line and the labels of the offered choices come from `game/phrases.json`
  (`PhraseSheet`), the machine-readable copy of `content/PHRASES.md` that a test holds row by row.
- **Scripted speech for demos and tests.** `--wav <file>` feeds a 16-bit PCM WAV (any rate), `--speak "<text>"` synthesizes the line with
  Kokoro speaker 28 (`ef_dora`), converts 24 kHz to 16 kHz and recognizes it, and `--listen <seconds>` records the real microphone; all are
  ordered script steps like `--say`, and the turn line prints the transcript, the confidence and the stage timings.

## Where the rules are checked

- `tests/Vocaterra.Host.Tests` runs the two demo utterances through the host's evaluation path on `content/game-v1`, checks the bundle hash in
  the manifest against the loader's, and checks that the conversation carries its counts and learner from one utterance to the next. It
  checks that a count at `int.MaxValue` stays there, and that the prompt's line is written under its header (`PromptReportTests`).
- `tests/Vocaterra.Game.Tests` runs the guide's turns on a fixed seed and a fixed stored time with the scripted recognizer (the verdict, the
  guide's state, the host event, the target tile and the world line; an unaccented line; the repair path; determinism), `WorldFacts` on the
  route, the guide's content in the bundle, the layering rules of `Vocaterra.Game` (no clock, no `System.Random`, no engine type, packages only),
  the audio handling (`AudioClips`), the turn worker (order, fixed inputs, the snapshot), and the model tests: Kokoro speaks a line, Vosk
  recognizes it once, the guide answers it like the typed line. Those are skipped with a reason when `VOCATERRA_MODELS_DIR` is unset or a model
  is missing.
- The core's own suites (fuzzing, goldens, replay) test the core; the game's tests cover only what the game does with it.
- The continuous integration job is parked in `.github/workflows-pending/ci.yml` (D-game-3, deferred by the owner).
