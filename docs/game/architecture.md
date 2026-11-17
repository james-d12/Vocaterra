# Architecture: how the game consumes the language core

Vocaterra is the game: the projects `Vocaterra.Game.Core`, `Vocaterra.Game`, `Vocaterra.Host` and `tools/Vocaterra.SpeechRender` of the repository. The language core, Vocaterra.NLP (the engine project `Vocaterra.Nlp` and its `Vocaterra.Nlp.*` tests and tools, Apache-2.0), is the other half of the same `src/`, `tests/` and `tools/` folders;
the two were separate repositories until 2026-10-21 (D-game-7) and sibling folders until the root layout of R01 (2026-10-23). The game references the core's library projects under `src` directly
(`Vocaterra.Game.Core` and `Vocaterra.Host` list them as `ProjectReference`s) and copies nothing of its source. This document says what the game
takes from the core, what the game owns, and how the two meet. The core's contract is [docs/nlp/api.md](../nlp/api.md), and the
boundary is [ADR 0009](../nlp/adr/0009-core-and-game-boundary.md). The game design is [specification.md](specification.md).

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

1. **Load a bundle.** `SpanishContent.Load(directory)` (the core's `ContentLoader.Load` with the Spanish pack registered) verifies every file's hash and the cross-references, and returns the bundle with its
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
  core's projects (`../Vocaterra.Nlp/Vocaterra.Nlp.csproj` and, since M53, the Spanish pack `../Vocaterra.Nlp.Es/Vocaterra.Nlp.Es.csproj`, which `GameBundle` and the host register), so a core change is visible to the game in the same build. There is no
  package version to bump, no feed and no pack step (M50 retired the packaging scripts and the sample host; core D5 is superseded); the root
  `nuget.config` lists nuget.org only.
- Third-party package versions for both halves are in the root `Directory.Packages.props`.
- A core change the game needs is a core milestone in `docs/nlp/implementation-plan.md`; a game milestone never edits the engine `Vocaterra.Nlp` or its `Vocaterra.Nlp.*` projects.
- The content tool is the core's `tools/Vocaterra.Nlp.ContentBuilder`, run from source by `scripts/content.sh`.
- The core is Apache-2.0 (core D3); the game is Apache-2.0 too (the whole repository since R01; D-game-2 in [decisions.md](decisions.md) was MIT).
- The core's sherpa-onnx dependency carries GPL-3.0 code in its native build (core D2). Nothing here ships the native libraries, and the
  game does not distribute a build that includes them until D2 is settled in the core.

**Native-asset trim (G7).** The speech packages and MonoGame ship native libraries for every platform, and a plain build copied all of them
(about 400 MB in the front-end's `bin` alone). `Directory.Build.props` therefore sets `RuntimeIdentifier=linux-x64` (with
`SelfContained=false` and `AppendRuntimeIdentifierToOutputPath=false`, so the output stays at `bin/Debug/net10.0/`) when the build host is
Linux x64. NuGet then copies only that runtime's natives, and the same `dotnet build`, `dotnet test` and `dotnet run` commands work. The
property `VocaterraAllRuntimes=true` turns the condition off, so a release build that must carry every platform restores the full copy; other
build hosts never get the trim. Restore again after changing the property, because the assets file records the runtimes restored.

## The engine

The engine is MonoGame for the prototype and the final engine is open (D-game-1 in [decisions.md](decisions.md)); a Godot .NET front-end is a
later spike. The console host stays as the cheapest way to exercise the core's calls and the content. A front-end references the same packages
(through `Vocaterra.Game.Core`), calls the same sequence and renders the outcome itself. Audio capture and playback belong to the front-end and stay
out of the core.

## The game layer (P3)

The game is one engine-free library and one front-end (`docs/process/code-guidelines.md`, R16), so that the engine can change without rewriting the
game: `Vocaterra.Game.Core` holds the systems and mechanics, `Vocaterra.Game` what needs MonoGame or draws. The old `Vocaterra.Simulation` and
the engine-free half of the old `Vocaterra.Game` are `Vocaterra.Game.Core`; R16 phase 2 (2026-10-28, `docs/archive/game/r16-phase2.md`,
`docs/archive/game/game-projects-layout.md`) moved the screens' models, the screens and their keys, the turn flow, the scripts and the tick there. A folder is a
namespace (`src/Vocaterra.Game.Core/Language/Book/` is `Vocaterra.Game.Core.Language.Book`), and a file holds one type.

**`src/Vocaterra.Game.Core`**, folders by system: no package, no engine type, no clock and no `System.Random` (its `ArchitectureRulesTests`).

```
Content/          GameData (every game file of the bundle, read once from its verified bytes), GameDataValidator (the checks between the files,
                  content-plan C02), LocaleBundle and LocaleText (the learner's support language from content/locale, C03)
Event/            the game's events (WorldEvents, GameEvent), EventPrinter (the console's event lines) and Trigger (what a tip, a lesson step
                  or an objective waits for, C06)
Needs/            PlayerNeeds (the five meters and the exhaustion timer, stages and effects worked out from them), NeedsData (game/needs.json), NeedsValidator,
                  the Drive, NeedMeter, NeedActivity and PlayPause kinds (N01), FireRings (the world's fire rings found by shape, with a `Rescan()`, and which burn; one finder shared by the session and the simulation, Y01), SleepPlaces (Session/: where the player can sleep, Y01) and NeedActionResult (N02)
Input/            the keys without the engine (GameKey, IInputFrame, D-p2-1a), the command pattern (IGameCommand, DelegateCommand,
                  GameCommands, KeyBinding, KeyBindings, ControlTable: every control of the game with its keys, in one table, which InputKeys, the bindings, the panels and the menu's controls page read; InputKeys: the controls' keys by name, which the locale's {key:…} spells),
                  InputFocus, KeyRepeater, OpenKeySwallow, TextEntry, AccentShortcuts, TextEdits, TextEditKeys, the push-to-talk
                  (PushToTalkSpeechCapture, ListenSession, ListenTarget), BookItemUse
Items/            Inventory, ItemDefinition, ScriptItemNames, InventoryScreen and its panel (InventoryPanel)
  Crafting/       Recipe, RecipeBook and their records, CraftingRules, CraftingScreen, CraftingRow, RecipePanelModel, CraftingPanel
Activities/       the procedures at a station (W05 to W07): StationBook, ProcedureBook, ActionTable, ProcedureRunner (the run), and the station overlay: StationScreen (state and
                  the view it makes), StationView and StationCell (plain values: the strip, the fixtures, the targets, the page of what can be held, the selection, the verb
                  choice), InstructionStrip (the strip as a model of its own, built from a ProcedureView; W30's recipe-book page shows the same one), StationPanel (the IScreen: the
                  keys of design 10.8, every act to GuideSession.ActInProcedure with how the row was presented, the audio through IInstructionAudio, the OnTalk hook; TeacherTalk (W08: the learner's text box, recording and the teacher's answer on the station's talk line) and ProcedureCue (what her answer asks of the station)),
                  StationProps (E at a cell of a station's prop: Scene.PropCells, the cells each named stamp placed)
Atmosphere/       the day and the weather as data and state (E01, E04): DayData (game/day.json), DayClock and DayPhase (the hour, the phase, the daylight), WeatherData, WeatherKind and WeatherChannels (game/weather.json: the kinds as rows of channels), WeatherSystem (the spells, drawn from the world seed), AtmosphereModel and AtmosphereSnapshot (the blended channels, one per tick, which every consumer reads)
Land/             where the player may build and where land is bought (Y09, D-game-32 (c)): LandData (game/land.json) and LandValidator, LandRules (a mask per column built once from the scene and the file: protected, eligible; `CheckPlot` reads the grid for water, trees, the ground on top and the slope and answers a PlotCheck: yes or a PlotFault), Plot, Protection (village, camp, road), LandRegister (the pending and the owned plot, and the IBuildRules hook the simulation asks: protected columns refuse any block but a torch, planks come back only on owned land) and LandSave (`systems.land`)
Dialogue/         DialogueModel and its records, the speech buttons, TextWrap, the turn flow (TurnPump, SpeechActionRunner, SupportFrontend,
                  TurnPresenter, GuideGreeting) and DialogueScreen (U06: the wheel scrolls the panel, `PanelScrollbar` computes the bar, `DialogueModel.Pages` keeps one page per NPC id)
                  (U02: the panel's rows, `DialogueModel.PanelRows`: the current exchange from the learner's last line, the one `CoachingLine`
                  in place of the correction, accent and English feedback stack, `Expanded` for the older lines; the lines themselves, and so
                  the book's history, are unchanged. The lesson heading reaches the panel's header as `FrameView.LessonHeading` (beside the NPC's name, which is the panel's title at 32 px, 16 px when compact: `DialogueLayout.Header`, U05); the strip has
                  no heading row, `LessonStripLayout.HeadingRow` = 0.)
Language/         Book/ (the book's model, pages, tables, recordings, speakers, geometry and BookScreen; U07: a page is `BookLine`s of a `BookLineKind` (Section, Entry, Meta, Gap, Tiles, Detail), the list drops the details, which are the right page's block, and the layer reports the right page's speaker buttons to `BookModel.RightButtons` for the mouse), Checks/ (the battery's bank, plan,
                  grader, CheckScreen, CheckBattery and ICheckLog), Learner/, Lessons/ (LessonRunner, LessonFrontend, LessonActions),
                  Phrases/ (phrase progress, the support ladder, PhraseJournalRows), Speech/ (the evaluators, MicCheck, MicCheckModel,
                  MicCheckScreen), Words/ (the noun catalogue, word progress, JournalRows, VerbReading, GreyscaleWorld)
Mathematics/      Angles, WorldRng, PresentationRng
Navigation/       Beacon, the walk's path search, BeaconDirector, PointLabels, ScenePlaces (the places a word names, village-plan V02)
Npc/              NpcService, WorldFacts, the aim at an NPC, the guide's turns and events, the NPCs' data (NpcRoster: since V01 also the prompt of
                  each state, the opening, the voice and the key words) and HostEventTable
Physics/          the player's body and its collision with the grid
Reporting/        the F8 report (ReportPanel, ReportEntry, ReportWrap, ReportStateBuilder, BuildCommit), FrameStats, BusyFrameStats, the crash
                  report and its source (ICrashSource, CrashGameState, CrashSource)
Save/             the save document, its migrations, SaveService and AutosaveController (its clock injected)
Screens/          IScreen, ScreenStack (the top open screen takes the keys)
Menu/             the menu (the menu issue): MenuScreen (pages, keys and mouse), MenuRow and MenuRows (the settings page, with its testing section), PlayerSettings (the one table of the
                  settings, their ranges and defaults, Resolve over a save and the command line, ToSave), SettingSpec, LaunchSettings, TestingHooks, ControlsListing (the controls
                  page, made from Input/ControlTable), MenuText and the IMenuMetrics the front-end answers
Scripted/         the scripts' steps (ScriptStep, VoxelStep and their kinds), VoxelScript, ScenarioScript, CheckScript, ScreenshotGate, ShotRequest
Session/          VoxelSession, GuideSession, TurnWorker, the objectives (ObjectiveBook reads game/session.json and game/objectives/), PlaytestEnd,
                  GuideApproach, SessionEnding, ObjectiveAnnouncer,
                  WordCardPresenter, PlaytestFlow
Simulation/       VoxelSimulation (the fixed 60 Hz tick: gathering, placing, crafting) and its commands, TickAccumulator, TurnWatchdog, the
                  observer pattern (ITickObserver, DelegateTickObserver), LiveVoxelInput, LookControl, WorldStepper
Sound/            the mixer, the cues and their mapping, the ambient beds, the footsteps, the NPC voice, SoundFrame, SoundTickObserver,
                  SoundSetup with its SoundOptions
Tip/              the tips (TipFeed, TipBook, TipSpec), the controls help's lines (ControlHelp), the objective line's pieces, ToastQueue
Voxel/            the blocks, the chunks and the grid, the block ray and the line of sight. The grid is sparse (B04a): chunks keyed by VoxelChunkCoord, none for
                  air, a read cache; a bounded grid (the valley, the tests) or a streamed one (WorldLimits, y may be negative, IChunkGenerator generates on
                  read, installs and evictions are not edits: ResidencyVersion, IChunkResidencyListener). Readers take IVoxelReader; the dense members
                  (Chunks, ChunksX/Y/Z, BlocksOfKind, ComputeDigest) are bounded-only and DenseGridMembersAllowListTests lists every use in src/
World/            the built Scene; Layout/ (the layout file), Generation/ (the terrain and the scene builder; Procedural/ the height model, B01), Noise/ (integer noise, splines, lattices, B01), Climate/ (the climate fields and the lapse with altitude, B01), Biomes/ (the biome table from game/biomes.json, B01), WorldGenData (game/world.json)
```

**`src/Vocaterra.Game`**, folders by area with feature subfolders: the window, the drawing, the devices and the layers, and the only engine
code. `VoxelGame` is the composition root (its constructor makes the components, MonoGame's `Update` and `Draw` call them in the frame's
order) and `Program` starts it.

```
Startup/          LaunchOptions (the command line, D-p2-3) and MicCheckMode, AssetPaths, SceneLocator, SoundBootstrap
Diagnostics/      SessionLog (it implements Core's ICheckLog), CrashHandler, CrashOutcome, WallClock, FrameClock, FrameTimings
Flow/             SessionStart, SessionEndingFlow (they read the wall clock, D-p2-2)
Input/            InputDevices, InputSnapshot (it implements IInputFrame), KeyMap (Keys to GameKey, one table), MouseLook, WorldInput
Sound/            SoundEffectOutput, DynamicSoundOutput, MicrophoneCapture
Scripted/         ScriptedRunEnd (it saves the pictures)
Streaming/        JobPool (the only worker threads: at most two; lanes: chunk meshes (B21), residency, tiles, ring), ChunkResidency (plans, makes, installs and drops the voxel chunks of a streamed world), ColumnBandCache, StreamedWorldHost (B07: the pool, the residency and the far view behind SceneDrawer.Streaming)
Graphics/         SceneDrawer, DrawHelpers, QualityPreset, ScreenshotWriter
  Camera/         FirstPersonCamera, PlayerCamera, CameraEase, ViewMath (CameraBasis, EdgeArrow), PlayerView
  Blocks/         BlockArt, BlockArtNature, BlockArtVillage, BlockAtlas, BlockAtlasLayout
  World/          VoxelRenderer, ChunkMesher, ChunkMeshPipeline and ChunkSnapshot (B21: meshing off the game thread), ChunkVersionTracker, ChunkViewSet, ChunkLightIndex, MeshVertex, OverlayMeshes, Greyscale (B04b: the renderer, tracker, view set, light index and
                  rain cover are keyed by VoxelChunkCoord, read by peeking (TryGetChunk, PeekBlock; nothing is generated for the picture) and listen to residency and block changes)
  Far/            FarViewSettings, FarTileKey, FarTileSelector (a quadtree of level-of-detail tiles), FarTileMesher (B07c: the faces of a footprint's stack of three-dimensional sections), FarNoise, FarPalette, FarView (tiles cached by key, built on the workers, made again when real chunks change their sections), FarTerrainRenderer (B07: the far land in a pass of its own, drawn after the sky and before the chunks, with its own depth range); Streaming/ holds FileLodPersistence, LodRing and LodEdits (B07c: the far world's sections on disk, the ring of real chunks, the player's edits); the store itself is Core's World/Lod
  Figures/        FigureMesher, FigureFacing, FigureDirector, NpcFigures, HeldArm, ArmPresenter
  Sky/            SkyModel, SkyRenderer, SkyLook (a pure function of a SkyLookInput: the dome, the clouds, the sun, the moon, the stars, the fog), SkyLookInput (`At` takes the snapshot), SkyKeyframe (the five keyframes: noon, golden, twilight, night, deep night), SkyBlend (their weights by the phase, the daylight and the sun's height), SkyArc, SkyWeather (the channels' effect on the look)
  Weather/        PrecipitationField (the drops in a box round the camera), PrecipitationRenderer, PrecipitationStyles (one row per precipitation kind), ColumnCover, PrecipitationState, Streak
UI/
  Common/         UiCanvas, UiTransform, UiPalette, UiScale, FontProvider, FrameView, LayerStack, CountBadge, NounIcons, PixelBox
  Hud/            HudLayers (ObjectiveLayer: the tracker, U01), HudLayout (TrackerRows, TipBlock, ObjectiveBox: the top of the HUD kept apart), HotbarLayer, ToastLayer, TipLayer, BeaconLabel, NeedsHudLayout, NeedsHudLayer, NeedIcons, ViewEffectsLayer, DeathScreenLayer (N05), VoxelLayers, VoxelDebugText
  Overlays/       OverlayLayouts, LoadingLayer, EndingLayer
  Menu/           MenuLayer (the menu's three pages), MenuLayout (their numbers), MenuMetrics (the pointer's answers, Core's IMenuMetrics), ControlsPageLines and ControlsPageCache (the controls table wrapped)
  Dialogue/       DialoguePanel, DialogueLayout, DialogueBubble, DialogueBubbleLayer, ConversationFrame, ConversationLayout, ConversationCards,
                  VoxelTalkLayers, SupportLayer, SupportLayout
  Book/           BookLayer (the page by line kind, the right page by detail role, U07), ConjugationLayout
  Checks/         CheckLayer, CheckCardLayout, MicCheckLayer, MicCheckLayout
  Crafting/       CraftingLayer, CraftingLayout, RecipePanelLayer
  Station/        StationLayer, StationLayout, InstructionStripLayer, InstructionStripLayout (the strip's boxes and text lines, for a box the caller gives)
  Inventory/      InventoryLayer, InventoryLayout
  Lesson/         LessonLayer, LessonStripLayout
  Report/         ReportLayer, ReportLayout, ReportScreen (it reads the frame's pixels)
```

How a screen meets the engine: the screens in Core read the frame as an `IInputFrame` and name keys as `GameKey`; Game's `InputSnapshot`
answers through `KeyMap`. Where a screen hit-tests a click against a layout that needs the font or the canvas, Game gives it the layout as a
delegate (the dialogue's lesson strip and speech buttons, the mic check's buttons, the crafting rows, the book's geometry). A Game type that
names Core's `Inventory` under `UI/` writes `Core.Items.Inventory`: the namespace `Vocaterra.Game.UI.Inventory` hides the type there.

The tests mirror the sources: `tests/Vocaterra.Game.Core.Tests/<folder>/` for Core's classes and `tests/Vocaterra.Game.Tests/<folder>/` for
Game's, with `Component/` in each for the tests that drive several classes together (the `*FrontendTests`, the route and session runs),
`TestSupport/` for the shared helpers (Core's `Repo.cs` and `RouteScene.cs` are compiled into Game's tests too) and `Architecture/` for the
layering rules.

**The NPCs as data** (content-plan 3.6, C08; schema 2 since village-plan V01). `NpcRoster` reads `game/npcs.json` and one `game/npcs/<id>.json` per NPC:
its label, name and figure key, the phrases its prompts practise, and what it does that was code until V01: the prompt it waits on in each state of
its agent (a state not listed waits on the initial state's; the guide's `review` prompt), the host event it opens a talk with and when (`always`,
`first`, `never`), its Kokoro voice (speaker id, name, speed), its key words and the prefix of its dialogue pools. `NpcService` takes the prompts and
the opening from it (`PromptAfter`, the constructor's check of every prompt an NPC file names); `SpeechSpeakers.OfPool` gives a pool to the NPC whose
prefix it carries (`<prefix>_*`, `guide.lesson.<prefix>.*`; every other pool is the guide's); the speech renderer (`tools/Vocaterra.SpeechRender`)
records each speaker with its file's voice and stops on a speaker with none (and, with `--trial <folder>`, renders a voice trial from the folder's `trial.json`,
village-plan V06; the V06 trial was deleted on 2026-11-03 and is in git history); `GuideSession` shows the talking NPC's key words; the phrase sheet answers
an NPC's prompt that no row names with the rows of another NPC's prompt that accepts the same answers (the guide's offer, the stall's offer rows); and
`--talk` accepts any NPC of the roster, checked once it has loaded. `GameDataValidator` reports an NPC with no prompts, a prompt the bundle lacks or
of another agent, a state its agent lacks, an opening event no state takes and a voice with no name. What the data cannot say stays code, by id: the
shopkeeper's fail-forward and later visits, and the guide's review, transfer offer and world lines. Adding an NPC is `content/README.md`'s "Adding
an NPC".

**What the NPCs' host events do** (C08; `game/host-events.json` schema 2 since village-plan V02). The core's agents raise opaque host events; `HostEventTable`
gives each (NPC, event) an action and `VoxelSession.ApplyHostEvents` carries it out on the game's thread, with the slots of the learner's line: `give_item` (a
count, or the number the learner said, `qty`, from 1 to 5; all of the items or none, the bottle once), `point_at_noun` (a noun's nearest block),
`point_at_place` (a named place of the scene, through `Beacon`'s points), `point_at_slot` (the place a slot names, resolved by `Navigation/ScenePlaces`: a
label of the scene, the nearest of the points that share it, the nearest block of a place made of its word's blocks such as the river (of the blocks joined to its point: the village well's water, V04, is not the
river), else a catalogue noun's nearest block), and since V03 `take_item` (all or none, through `VoxelSimulation.TakeItems`) and `sell` (the number asked for, at a price in coins: the coins
go and the items come, or nothing changes hands). What the world of a turn cannot do is decided on the worker, on the turn's own copy of the world and
inventory, because only there may the NPC's conversation move: `NpcService` drops the line of the action that raised the event and raises
`<npc>.unknown_place` (a place the scene does not have) or `<npc>.cant_pay` (too few coins) to the agent as a host turn, whose lines follow the turn's and
which is in the replay record (the shopkeeper's fail-forward is the model). The game's thread then does nothing in the world for it (the place is logged
`place_asked unknown`; the coins and the items stay where they were). The arrival at an arrow is the world event `beacon_reached`, with the place's name (`PointerMarker.Point`): the session logs it when its `Step`
takes the arrow down, and `BeaconDirector` for a beacon it placed itself (the start, `--beacon`); a save keeps the arrow's word and block, and the name is
worked out again from the scene on load. A lesson's `do` step with a `pointPlace` points the beacon there and may wait for that arrival.

**Objectives by session, and orders** (C07; village-plan V03). `ObjectiveBook` reads `game/session.json` and one `game/objectives/<id>.json` per objective. An
objective may name the play `session` it starts in: before it `VoxelSession` neither shows it (`CurrentObjective`, the HUD's line) nor counts world events for
it. An order names the NPC the goods are brought to (`deliverTo`) and the goods (`deliver`); the loader gives it one step, `delivered`, which waits for the
world event `order_delivered` at that NPC with the order's id, so the save keeps it as any step (`<order>.delivered`; no save field is new). The delivery
is checked when the panel opens, in `GuideSession.OpenNow`, never on a line (D-game-8, D-game-9: walking up with the goods is enough): when no turn is on the
worker and the inventory holds an open order's goods (`VoxelSession.OrderReadyAt`), `NpcService.Open` raises `<npc>.delivered` to the agent as the opening
instead of the NPC's own; the goods are taken, the host events of the thanks are applied (the reward is a `give_item`), and `order_delivered` completes the
order. An order with `handOver` (D-game-27, issue #42) is not taken at the opening: `GuideSession.BeginHandOver` raises `<npc>.order_ready` instead, the
agent asks for the goods and waits in its `receiving` state, whose prompt (`<npc>-receive`) takes `hand_over`; the session then judges the learner's lines there
(`HandOverAfter`: a line whose item, or any noun, is one of the order's `handOver` nouns delivers it; «no sé» or `HandOverMisses` missed lines deliver it anyway)
and raises `<npc>.delivered` as a host turn, with the same goods, reward and `order_delivered`. A delivery decided while another turn is on the worker waits for it
(`CompleteHandOver`) and its thanks come as an opening. A class at that NPC is held back at the opening (`OpenTalk`) and comes after the hand-over
(`ReleaseClass`), and while the NPC waits its prompt, card and help buttons take precedence over a lesson step's. Only an order the player has taken on is
delivered or asked for (issue #54, D-game-30, `GuideSession.OrderOffer.cs`): with none ready, `OpenNow` asks `VoxelSession.OrderToOfferAt` for an order of the NPC not
given and not done, and passes `<npc>.offer_order` to `NpcService.Open` as the opening offer (before the bake offer; any state that takes the event may open with it). The
agent asks and waits in `offering_order`, whose prompt (`<npc>-order-offer`) takes confirm and deny beside the open prompt's intents; `OrderOfferAfter` reads the turn's
host events: `<npc>.accept_order` gives the order (`GiveOrder`, the world event `order_accepted`), `<npc>.decline_order` puts it off until the night (`order_declined`).
A talk that opens on an agent left in `offering_order` first raises `<npc>.offer_gone`. A class at the NPC is held back until the answer, as for the hand-over. A lesson
step that tells an order gives it when it starts (`OnLessonStepStarted`, the objective's `lessonStep`), and while the teacher would offer that class
(`LessonRunner.ClassOfferedAt`) the order is not offered on its own. The validator checks every order NPC's offer (`GameDataValidator.CheckOrderOffer`). The session's ending (S17) waits only for the objectives with no session that are not orders, so an order never holds back or brings back the closing
moment. The three items of the economy (`ItemId.Coin`, `Apple`, `Axe`, V03) are ordinary items: the axe, held, makes a trunk take a quarter of its gather
ticks (`VoxelSimulation.GatherTicksOf`, which the HUD's bar reads too).

**The camp (Y12, D-game-32).** The camp kit (`camp-kit`, bought with the plot) sets up one piece on the owned plot: the scene layout's `camp` stamp (`game/scene.json`: a fire ring, a free row of ground, the tent's A-frame
and a two-cell bedroll in it, with the anchors `fire`, `bed` and `wake`), turned by `StampSpec.Turn`, the same turning the scene's own build uses (`SceneBuilder.BuildStamp` and the camp share it, so they put blocks down alike). Core's
`Land/CampPlan` names the cells and checks them against the grid without changing it (`Check`: every column owned, the ground at one height, every cell free or undergrowth, the ground plain; `HasWake`: a cell to wake on reached on foot from the bed
and a walk out of the footprint, read with the camp's blocks set and put back); `VoxelSession.UseCampKit` (`Session/VoxelSession.Camp.cs`) adds the player's own check (stand clear of the camp), then sets the blocks directly (never `TryPlace`,
which refuses all but planks and torches on protected ground), takes the kit, rescans the fire rings, adds the sleep places `camp.bed` and `camp.fire` (made in play, saved with their cells and the wake cell) and raises `camp_placed`.
A torch on the ring lights it through the same hook as Luna's (`torch_at_ring` and, the first time, `fire_lit`, both with the detail `camp.fire`); the fire cooks and rests as every ring does (`Fires`), and the fire bed of the sound follows
it (`AmbientBeds.At`'s `campFire`). Holding the gather button on any block of the camp (`IBuildRules.IsCampBlock`, `VoxelSimulation.StepPackCamp`, 45 ticks) raises `CampPackRequested` and the session packs the whole camp back
(`PackCamp`: the kit and the ring's torches into the bag, the places dropped, the last place slept falling back). A load keeps a camp whose blocks all stand and otherwise drops its places and gives the kit back once. Planting works on the plot
as well as at Luna's camp, each with its own count of bushes.

**Land rules (Y09, D-game-32 (c)).** Core's `Land/` holds the rules and the player's plots; the simulation only asks. `game/land.json` names the protected regions by scene site
(the village's disc, Luna's camp), the protected squares round the `path` and `bridge` blocks, the meadows and the plot's size, price and slope; `LandRules.Create` turns them and the scene into one byte per column
when the session starts (`VoxelSession.InitLand`), and `CheckPlot` reads the grid for the rest. `VoxelSimulation.BuildRules` (an `IBuildRules`, set by the session to its `LandRegister`; a host setting like `WalkPermille`, not state, not in the digest)
is asked by `TryPlace` (a protected column refuses every block but a torch: a `PlaceRefused` event, which the session turns into the hint `ui.landVillage`, `landCamp` or `landRoad`), by `PourWater` (the dev aid) and by the gather (a plank block is gatherable
only where the hook exists, for `PlankTakeBackTicks`, and only on owned land; elsewhere a `TakeBackRefused` event says `ui.landTakeBack` once per hold). Every other gather, trees, berries, stone, is as it was. `VoxelSession.SetPendingPlot`, `OwnPlot`,
`IsOwned` and the save entry `systems.land` are what the stake (Y10) and the mayor (Y11) call; a simulation with no hook behaves as before the rules.

**The stake and the plot's outline (Y10).** The item `stake` (`ItemId.Stake`, stack of one, places no block) is used with the right click on a block: `Input/HeldItemUse.Applies` with `ItemId.Stake` (Core, as `PlantingUse` is; the camp kit uses it too) says the frame is
a use (mouse captured, so no talk or panel is open; stake selected; a block aimed at), `WorldInput` then calls the session's `UseStake(BlockPos)`, which marks the pending plot with the aimed column as the plot's **middle**
(`LandRules.PlotAround`: the 10 by 10 plot spans five columns west and north of it and four east and south; the outline lands round the cell the player points at) and says the answer on the hint line: `ui.plotMarked`, or the
refusal's own line (`plotVillage`, `plotCamp`, `plotRoad`, `plotOutside`, `plotWater`, `plotTree`, `plotNotGround`, `plotSteep`), or `plotOwnedHere` / `plotOwnedAlready` once land is owned (the stake then marks nothing). A refusal keeps the
old pending plot; marking again moves it and costs nothing. `PlotHudLine()` is the HUD text (`ui.plotHud` with size and price from `land.json`, or `ui.plotHudOwned`), shown by `PlotHudLayer` under the crosshair while the player is within
24 blocks of a plot or holds the stake. The outline is Game's: `OverlayMeshes.PlotOutline(plot, surface, owned)` builds four corner posts and a low rail round the plot, each at the ground height of its own column (`LandRules.GroundOf`);
the pending plot has bare amber posts and a broken rail (every second column), the owned plot white posts with a green flag each and a solid rail, so the two differ by shape as well as colour. `SceneDrawer.DrawWorld` builds the meshes
for plots within 64 blocks of the eye and `VoxelRenderer.DrawWorld` draws them after the figures in the sun's light (the figures' diffuse colour, so they dim at night). The dev aid `--mark-plot x,z` is `MarkPlotWithHint`.

**Buying the plot (Y11).** The mayor's sale is one exchange that cannot half happen: `VoxelSession.SellPlot` (called by `ApplyHostEvents` for the kind `sell_plot`, the `NpcEffectKind.SellPlot` row of `game/host-events.json`) first asks the exchange's checks
(`NpcEffect.PlotMiss`: owned already, no plot marked or the marked one no longer passes `CheckPlot`, the coins short of `Land.Rules.PlotPrice`, no room for the camp kit once the coins are out: `Inventory.CanExchange`), then `OwnPlot()`, which checks the world once
more and may refuse, and only then takes the coins and the stake (the mayor keeps it, Y15; the room check counts its slot as freed, so a full bag that holds the stake still takes the kit; a stake in a chest stays there) and gives the kit, logs `plot_bought` and puts the arrow on the plot. A refusal at any step leaves coins, bag and land as they were, and the repeated «sí», a reload and a second talk meet
the land owned (`already_owned`). The same `PlotMiss` runs on the worker over the turn's copies (`GuideScene.Land`, a `PlotFacts` taken on the game thread at submit, and the bag's copy), so the mayor never says «El terreno es tuyo» for a sale the
world cannot make; the session asks again of the bag and the land themselves. His offers at the talk's opening are the session's (`GuideSession.LandOffer`: `LandOfferAt`, the stake while the player has none and no plot, the sale once a plot is marked, from the
play session the checklist `my-land` is shown from) and follow the order offer's shape (`offering_stake`, `offering_plot`, `offer_gone`).

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

**The day and the weather (E01 to E06, D-game-31).** Core's `Atmosphere/` holds the clock and the weather; the game draws and hears them. Core blends
the channels of the previous and the current weather kind with the time of day into one `AtmosphereSnapshot` per tick, and every consumer reads that
snapshot: `Graphics/Sky` (the dome, clouds, sun, moon, stars and fog, through `SkyLookInput` and `SkyLook`), the light of `Graphics/World` (`VoxelRenderer.SetAtmosphere`, the light bands),
`Graphics/Weather` (the drops, from the precipitation channel), the sound (`AmbientBeds.At`) and the NPCs' answer (the snapshot's `say` key). No consumer
switches on a weather kind's id. Two tests hold that rule: `NoWeatherKindIdsInSourceTests.NoSourceFileNamesAKindOfTheFirstDataOutsideTheDefaultData`
fails on any quoted kind id under `src/` outside `WeatherData.cs`, and `TheAllowancesCoverOnlyThePrecipitationIdInTheStyleTableAndTheBedNames` allows
exactly two exceptions, the precipitation's row in `PrecipitationStyles.cs` and the ambient bed's name for rain. A new weather that reuses the channels is
a row of `game/weather.json` and no code (`content/README.md`, "Adding a weather kind"). The clock's and the weather's state is in the save's
`systems.atmosphere` entry (`saves.md`).

## The streamed world: threads and render passes (B07)

The game starts in the streamed tall world (`--world valley` keeps the bounded valley). Two things change the picture of the layers.

- **Threads** (only in `Vocaterra.Game`, `Streaming/JobPool`): at most two worker threads in all (the bounded valley gets a pool of its own with the same two), shared by the chunk prefetcher, the far view's tile builds and, since B21, the chunk meshes, on top of the game loop's own thread and the turn worker that owns Vosk. A worker runs a pure computation (over `StreamedWorld`: the composite source, the surface bands, the far sampler; or, for a mesh, over a `ChunkSnapshot` the game loop took) and hands the result to a queue; only the game loop touches `VoxelGrid` (`GridResidency.Install` and `TryEvict`), the renderer and the simulation. `Game.Core` starts no thread. A jump bumps the residency's epoch: the jobs of the old place do nothing when a worker reaches them and their packages never free a request slot of the new place; a change of quality changes the same driver's radii (`ChunkResidency.Reconfigure`), so the jobs it has in flight are kept. Residency never changes a result: a test runs one walk and the same edits under two policies, with workers and without, and gets one state digest and one save.
- **Meshing off the game thread** (B21, `Graphics/World/ChunkMeshPipeline`; the review's P1, P2 and P5): `VoxelRenderer.Remesh` no longer builds a mesh. A chunk is listed to be built when it is wanted and has none (`SetWanted`, once when the wanted list changes), when a block changes (`IBlockChangeListener`: the chunk, each neighbour that reads the cell across a face, an edge or a corner, and the chunk above for the water's depth), when a light or the sky's reach near it changes, or when its mesh is in another light band; each frame walks a bounded window (512) of each list, in the frustum first and nearest first, so a frame with nothing to do looks at nothing and an edit costs a handful of lookups (`ChunkViewSet` is told of block changes too and re-sorts only when the eye changes chunk, the view changes or a chunk gains its first block or loses its last). For a chunk the game thread copies what the mesher reads, in a few slab copies of the resident chunks' arrays, into a `ChunkSnapshot` (the chunk and a layer round it, two layers under it, the blocks the lights near it stand on, the river's currents), the options (lights, sky, band, grey kinds) are read at the same moment, and a job of the mesh lane (`JobLane.Mesh`: before the residency's jobs, with one residency job after every four mesh jobs) builds the meshes from the snapshot and the grass layer likewise (the 16 biome ids of a chunk are read first, since the place query is the game thread's). The game thread takes the results back within the frame's budget (always one) and puts them on the card, the new buffers replacing the old in one step. Every chunk has a **stamp**, bumped by anything that could change its mesh; a job carries the stamp of its snapshot and a result whose chunk has another stamp (an edit came after the snapshot) is refused and the chunk built again, a worker skipping a job already stale when it starts; a change of the grey set or the quality's options moves an epoch that refuses every job in flight. So a mesh is never put on the card from older blocks than the grid's, and the old mesh stays on screen until the new one replaces it (an edit does not flicker). A chunk an edit touched within 40 blocks of the eye is meshed on the game thread in the frame of the edit (at most four a frame; the same function as a worker's, so the same vertices), so a block broken or placed shows at once; the others go to the workers. With no worker (`--prefetch 0`, the tests) the jobs run on the game thread inside the same budget. A scripted run's `Remesh` with no budget waits for the workers, so a picture is taken of a finished view. The far view's hand-off (`FarView.SetNearReady`) reads what the renderer has on the card, and is recomputed only when a chunk gets or loses a mesh or the wanted list changes.
- **Render passes** (`SceneDrawer.DrawWorld`): the sky; then the far pass (`FarTerrainRenderer`: level-of-detail land tiles with their own projection, a near plane of 12 blocks and the range as the far plane, the haze shared with the chunks) and a depth clear; then the voxel pass as before (the opaque chunks, the undergrowth, the glow, the figures, the water, the rain, the beacon, the arm), whose far plane now covers the world's height (`FarPlaneFor(view, 384)`). The residency and the far view are updated before `Remesh`, within a small frame budget (a scripted run settles instead).

## The settlement series (Y00 to Y13): where to look

Day one ends at the inn, day two offers land, and the land holds a camp (settlement-plan, D-game-32). Each piece is data in `content/game-v1/game/` plus a small amount of code; this is the map, the details are in the sections named.

| Piece | Data | Code |
| --- | --- | --- |
| **Sleep places** (Y01, Y02, Y08): Luna's fire `guide.fire`, the inn's bed `inn.bed`, and the camp's two places made in play; each has a kind (`fire` or `bed`), a reach, an access (`always` or `lodging`) and optionally a wake cell | `needs.json` `sleepPlaces` and `death.wakeAt` | `Session/SleepPlaces.cs` (the list, `Add` for places made in play), `SleepOffer.cs`, `SleepWalk.cs`, `VoxelSession.Sleep.cs`, `VoxelSession.Death.cs` (the wake) |
| **Lodging** (Y05, Y06, Y07): the innkeeper Rosa gives the room once; the inn's bed is usable only after it | `agents/innkeeper.json`, `game/host-events.json` row `innkeeper.room` with the action `grant_lodging`, `game/session.json` `innBeacon` | `VoxelSession.GrantLodging` (raises `lodging_granted`), `systems.sleep.lodgingGiven` in the save, the dev aid `--lodging` |
| **The inn and the meadows** (Y04): the `inn` house with the `inn_rooms` stamp (two-block beds), the sites `meadow.north` and `meadow.south` where land is sold | `scene.json` | `World/Generation/` (the scene build), the land rules' eligible regions |
| **Land rules** (Y09): where one may build and where a plot may lie | `land.json` | `Land/LandRules.cs`, `LandRegister.cs` (an `IBuildRules` the simulation asks), `LandValidator.cs`; the section "Land rules" above |
| **The stake and the outline** (Y10): the stake marks the pending plot, the outline is drawn round it | item `stake`, `ui.json` plot lines | `Input/HeldItemUse.cs`, `VoxelSession.Land.cs` (`UseStake`, `SetPendingPlot`), `OverlayMeshes.PlotOutline` in Game; the section "The stake" above |
| **The exchange** (Y11): the mayor gives the stake and sells the plot; coins, plot and camp kit change hands in one step that cannot half happen | `host-events.json` kinds `give_item` and `sell_plot` (`villager.give_stake`, `villager.sell_plot`), the villager's agent (`offering_stake`, `offering_plot`), `objectives/my-land.json` | `VoxelSession.SellPlot`, `NpcEffect.PlotMiss`, `GuideSession.LandOffer.cs`; the section "Buying the plot" above |
| **The camp** (Y12): the kit sets up a fire ring, a tent and a bedroll on the owned plot, and can be packed back | `scene.json` stamp `camp` with its anchors, item `camp-kit` | `Land/CampPlan.cs`, `VoxelSession.Camp.cs`, `World/Layout/StampSpec.cs` (the turning); the section "The camp" above |

The checklist `my-land` ("Mi terreno", from play session 2: `stake`, `buy`, `camp`, `fire`) never holds a session back; its steps are events (`item_picked_up`, `plot_bought`, `camp_placed`, `fire_lit` with the detail `camp.fire`), and `stake` counts the bag and one more once the plot is owned (`VoxelSession.HeldForSteps`: the mayor keeps the stake at the purchase, so the step stays done, in the run and after a load), and a load reads `buy`, `camp` and `fire` back from the save and the world (the restore hooks `plot-owned`, `camp-placed`, `camp-fire-lit`). The scripted runs that cover the series are r20 and r30 (the sleep), r40 to r43 (`scripted-runs.md`).

## Needs (N01)

The player's hunger, thirst, sleep, health and stamina (adaptation design section 9, D-game-24). Engine-free, in `Vocaterra.Game.Core`:

- **The data** is `content/game-v1/game/needs.json` (schema `content/schema/needs.schema.json`): per drive its seconds per point, its effort factors and its four
  stages (their effects, the key of their line); health's recovery and warnings; stamina's costs and refill; rest at a lit fire; a night's and a collapse's changes;
  the foods; the death rules. `GameData.Needs` holds it (`NeedsData`) and `GameDataValidator` runs `NeedsValidator` on it (every food an item, every rate positive,
  the stages in order). The stage lines' text is the HUD's (N05); the file holds their keys only.
- **The model** is `Needs/PlayerNeeds`: plain state for the five meters and two timers, with `Step(ticks, activity)`, `Eat`, `Drink`, `Sleep` (a night), `Collapse`, `Die`
  and `Wake`. `StageOf`, `HealthStage` and `Effects()` (stamina's maximum and refill, the walking factor, sprint allowed, health's change, the view effects) are worked out
  from the meters and the data on every call and never stored. No clock and no random numbers: time arrives as ticks.
- **The wiring** is the partial `Session/VoxelSession.Needs.cs`: the session owns the `PlayerNeeds` and steps it on its world tick, once a tick, only while play is active
  (`PlayPauses` is `PlayPause.None`). The game sets `PlayPauses` each frame from what it has open (`VoxelGame.PlayPausesNow`): the conversation and lesson panels, the
  book, the inventory, crafting, a station, the report (F8), a menu, the check battery, and speech being recorded or processed. The world's ticks go on under most of
  these and the stored time counts them all, so the needs count active ticks themselves. The activity of a tick is read from the simulation (a gather tick, the body
  moved, standing still within the rest radius of a lit fire). `need_stage` is raised when a stage changes. What the stages do to the player: N02 below; sleep, the collapse
  and death are N03 and N04's. Sleeping, the prompt, the closing card, the arrow to bed and the waking after a death ask `SleepPlaces` (Y01: `needs.json`'s `sleepPlaces`, Luna's fire `guide.fire` and the inn's bed since Y08, a fire usable while its ring is lit, a bed by its access; `death.wakeAt` names the place the player wakes at), not a scene point.
- **Eating, drinking, sprinting (N02)** are the partial `Session/VoxelSession.Eat.cs`. `EatSelected`/`EatItem` use a food of `needs.json` through `PlayerNeeds.Eat` (the bottle
  comes back empty; at full hunger or thirst the food is kept and the ui line `foodKept` or `drinkKept` is a hint), `DrinkByHand` drinks at the water under the crosshair
  (`AimedAtWater`), and they raise `meal_eaten` (the food's noun) and `water_drunk` (`botella`, or `pozo`, `lago`, the river's noun). The game binds the right click to
  `FoodItemUse` (`WorldInput`) and E at water to `GameCommands.DrinkByHand`, and passes the sprint key (`InputKeys` `sprint`, Left Shift) as `SprintHeld`. Before each tick
  `PrepareTick` sets the simulation's `WalkPermille` (a stage's walk factor, times the data's `sprintSpeedFactor` while the sprint counts: held, moving, allowed by the needs and
  stamina above 0) and `GatherPermille` (the data's `gatherSlowFactor` at zero stamina: gathering slows and never stops); neither is state or in the digest. The tick's activity
  is Sprinting, Gathering (a gathered block; filling the bottle is no labour), Walking, Resting (still within the radius of a lit ring of `FireRings`) or Idle. With the needs
  off the sprint is free and the speed factors are 1.
- **The save** (version 8, the oldest and only one read: the needs', the water's and the sleep places' fields together): the optional `systems` map, with `systems.needs` holding the five meters and the two timers (a save without it starts with full needs) and `systems.sleep` the lodging, the last place slept and the places made in play (Y02). Water adds the player's `air` and the `flow` of the scene diff; the underwater blur is an optional field of the settings. The needs
  are not in the world's digest; a load clamps a value out of range and refuses one that is not a number.
- **Drowning (water-plan H11):** `Breath` reports `OutOfAir` once a second at zero air; `DrowningDamage` takes 10 health through `IPlayerHealth`, which `NeedsHealth` joins to `PlayerNeeds.Hurt`. While the air is gone `PlayerNeeds.RecoveryHeld` stops health's recovery. The session runs the death flow after the damage (`DieIfDead`): the death names `player_drowned` (the `diedDrowned` / `faintedDrowned` line). With the needs off (or while play is paused) the adapter takes nothing, as hunger and thirst take nothing then. The underwater blur level (`Off`, `Low`, `Medium`, `High`) is a setting (row 4 of the F8 panel, `SaveSettings.UnderwaterBlur`, `--underwater-blur` for a run) and is forced off with the view effects. The bubble row (`BreathHudLayout.RaiseAbove`) lifts above the stage line or the needs block where they would overlap.
- **Test aids:** `--needs off` freezes the needs (every scripted run passes it), `--set-need <need> <value>` sets one at launch.

## Spoken input, the worker thread and the models folder (P5)

A turn is either typed (the line is the transcript, the core's scripted recognizer) or spoken (a 16 kHz clip decoded by Vosk). Both go through
the same `NpcService` turn, and each turn records how it arrived (`GuideTurn.Input`) so that word progress applies the credit of that kind
(`CreditRules`: typed input half, spoken full, D-game-6; `--typed-credit full` switches typed back to full).

- **One worker thread owns the recognizer.** `TurnWorker` (in `Vocaterra.Game.Core`, no engine type) is the only thread that loads the Vosk model
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
- **Free talk adds a second, open decode (V31, core M80).** `SpokenUtteranceEvaluator` holds two pipelines. The second has
  `OpenRecognitionFallback` on and is used only for a turn that is free talk (`GuideScene.PromptOverride` null and not `Review`) at one of
  `SpokenUtteranceEvaluator.FreeTalkPrompts` (baker-open, smith-open, farmer-open, fisher-open, stall-need, stall-offer, guide-offer; the villager's and the
  guide's open prompts decode once anyway). When the constrained decode's reading is not taken, the core decodes the clip again freely and keeps that
  reading if it is taken and its weakest word is at least the core's floor (`OpenPassWordFloor`, 0.5; the game adds no floor of its own). `LineReading` then
  reads `UtteranceAnalysis.OtherPass` the way it reads the first recognition (the vocative out, the greeting's rest) when the chosen reading is not taken, and
  holds a free recognition it re-reads to the same floor. A line so heard is understood and answered as the typed line is («Te entiendo…» off the prompt), is
  `GuideTurn.HeardOpen`, and is credited as typed (`GuideTurn.CreditKind`, half; the phrase use carries it) though `Input` stays `Spoken`. Lesson steps (a
  `PromptOverride`), reviews, the mic check and the check battery decode once, as before. `TimedRecognizer` sums the decodes of a turn
  (`SpeechEngine.BeginTurn`, `RecognizeCount`); `TurnTimings.Decodes` shows the count, the session log's turn record has `heard_open` and (above one)
  `decodes`, and a `VoiceEvent` of kind `HeardOpen` marks the line. A scripted (clockless) run keeps its timings at 0.0 ms.
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
- **Push-to-talk and the microphone** (the device in `src/Vocaterra.Game/Sound/`, the session in Core's `Input/ListenSession`). MonoGame's `Microphone` (OpenAL capture) records at the device's own rate
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
- `tests/Vocaterra.Game.Core.Tests` runs the guide's turns on a fixed seed and a fixed stored time with the scripted recognizer (the verdict, the
  guide's state, the host event, the target tile and the world line; an unaccented line; the repair path; determinism), `WorldFacts` on the
  route, the guide's content in the bundle, the layering rules of `Vocaterra.Game.Core` (no clock, no `System.Random`, no engine type, no package),
  the audio handling (`AudioClips`), the turn worker (order, fixed inputs, the snapshot), and the model tests: Kokoro speaks a line, Vosk
  recognizes it once, the guide answers it like the typed line. Those are skipped with a reason when `VOCATERRA_MODELS_DIR` is unset or a model
  is missing.
- `tests/Vocaterra.Game.Tests` runs what needs Game's types without a GPU: the meshers, the layouts, the device input, the session log and the
  `Component/` tests; its `ArchitectureRulesTests` keep `System.Random` and `Guid` out, the wall clock in `WallClock.cs` and the `Stopwatch` in
  `FrameClock.cs`.
- The core's own suites (fuzzing, goldens, replay) test the core; the game's tests cover only what the game does with it.
- Continuous integration is `.github/workflows/ci.yml` (`build-and-test` without the speech models, and `languagetool`; `docs/game/building-and-testing.md`, "Continuous integration").
