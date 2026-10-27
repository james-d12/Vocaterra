# Game and Game.Core: keep the two projects; finish R16 phase 2 and regroup the folders (report, 2026-10-27)

## Context

The owner asked whether `src/Vocaterra.Game` and `src/Vocaterra.Game.Core` should be merged into one project, and what the folders
should look like, in light of `docs/code_guidelines.md`.

What the investigation found:
- `Vocaterra.Game` has 172 files, and only 70 of them need MonoGame (54 name it, 16 more use those 54). The other ~100 have no engine
  type. Some are game logic that the guideline puts in Core, and some are layout or meshing code that the guideline keeps in Game.
- R16 phase 1 (on this branch since `e5779e9`) split `VoxelGame.cs` into components. Phase 2, which moves the engine-free logic
  to Core, is fully planned in `docs/game/R16_PHASE2.md` but has not started. It waits on four owner decisions (D-p2-1 to D-p2-4).
- Game's folders have grown into catch-alls. `Utilities/` holds 22 unrelated files. `UI/` holds 46 files with no subfolders, its
  `*Screen`/`*Panel`/`*Layer` files duplicate `Screens/`, and `UI/English/` groups files by the language of their text.
- About 115 test files sit at the roots of the two test projects. The guideline wants test folders to mirror the source folders.
- Several places still use the old project names `Vocaterra.Simulation` and `Vocaterra.MonoGame`: `CLAUDE.md`,
  `Directory.Build.props`, `docs/game/IMPLEMENTATION_PLAN.md` §2–3, the title of `src/Vocaterra.Game/README.md` and the comment in
  `Vocaterra.Host.csproj`.

## Recommendation: do not merge

1. **The guideline sets the split.** `docs/code_guidelines.md` names both projects: Core holds the logic, and Game holds the MonoGame,
   graphics and UI code.
2. **Only a project boundary is enforced.** Core references no packages, so it cannot see MonoGame at all.
   `tests/Vocaterra.Game.Core.Tests/ArchitectureRulesTests.cs` also bans the clock, `Random` and engine tokens in Core. After a
   merge, the same rules would depend on folder paths and discipline.
3. **The host would get heavier.** `Vocaterra.Host` (the console host) references only `Game.Core`. A merged project would bring
   MonoGame, FontStashSharp and the native Vosk and ONNX copy rules into the host and its tests.
4. **Core runs headless.** Core's tests run without a graphics device or native audio, which keeps them fast and reliable.

The pain that suggests a merge comes from logic sitting in the wrong project. Moving that logic to Core (R16 phase 2) fixes it.

## Owner decisions (2026-10-27)

- **D-p2-1, keys: (a).** Core gets an engine-free key list: a `GameKey` enum and an `IInputFrame` interface covering pressed, held,
  modifiers, clicks, wheel and pointer. Game's `InputSnapshot` implements `IInputFrame` through one `Keys`→`GameKey` table. The
  screens, `KeyBindings`, `ListenSession`, `TextEditKeys` and `BookItemUse` then move to Core. Their click hit-tests take a layout
  function from Game instead of a `SpriteFontBase`.
- **D-p2-2, wall clock:** not asked, so the recommended default applies. `SessionLog`, `CrashHandler` and `WallClock` stay in Game, and
  Core's rule stays as written.
- **D-p2-3, options:** not asked, so the default applies. `LaunchOptions` stays in Game. The script step types go to their own files
  in Core's `Scripted/`, and moved classes take small option records (for example `SoundOptions`) instead of `LaunchOptions`.
- **D-p2-4, tests:** test folders mirror source folders now. The project names and the current sentence-style test names stay; the
  `*.Unit.Tests`/`*.Component.Tests` split and Given_When_Then naming remain deferred.
- **Game layout:** keep top-level folders by area, and add feature subfolders inside them.

## Target layout: `src/Vocaterra.Game.Core` (folders by system; a namespace follows its folder)

Core's existing folders stay. Additions and changes are marked ＋. Where this list differs from R16_PHASE2 §5.1, it is more specific
and this list wins.

```
Event/            + EventPrinter
Input/            ISpeechCapture, PushToTalkSpeechCapture
                  + GameKey, IInputFrame (D-p2-1a), IGameCommand, DelegateCommand, GameCommands, InputFocus, KeyRepeater,
                    OpenKeySwallow, KeyBindings, TextEditKeys, ListenSession, BookItemUse
Items/            Inventory, ItemDefinition + InventoryScreen, InventoryPanel, ScriptItemNames
Items/Crafting/ + Recipe, RecipeBook (from Items/), CraftingScreen, CraftingRow, RecipePanelModel, RecipeIngredientCount, CraftingPanel
Dialogue/       + DialogueModel, SpeechAction, PanelKey, SpeechButton(s), TurnPump, SpeechActionRunner, SupportFrontend,
                  TurnPresenter, GuideGreeting, DialogueScreen
Language/Book/  + BookModel and its state types, BookPages, ConjugationTable, BookVerbLabels, BookRecordings, BookPractise,
                  BookSpeechControls, BookSpeaker, BookScreen
Language/Checks/  + CheckScreen, CheckBattery
Language/Lessons/ + LessonAction, LessonKey, LessonFrontend (after LessonStrip is split)
Language/Phrases/ + PhraseJournal
Language/Speech/  + MicCheckModel (with MicCheckButton, MicCheckPhase), MicCheckScreen
Language/Words/   + VerbReading, Journal, GreyscaleWorld
Mathematics/      + WorldRng (from the root), PresentationRng (from Utilities/; Utilities/ is deleted)
Navigation/       + BeaconDirector, PointLabels
Reporting/        + ReportPanel, ReportEntry, ReportWrap, ReportStateBuilder, BuildCommit, FrameStats, BusyFrameStats, ICrashSource,
                    CrashGameState, CrashSource
Save/             + SaveService, AutosaveController (its clock injected as a Func<double>)
Screens/        + IScreen, ScreenStack, ControlsHelp
Scripted/       + ScriptStep, ScriptStepKind, VoxelStep (out of LaunchOptions.cs), VoxelScript, ScenarioScript, CheckScript,
                  ScreenshotGate, ShotRequest
Session/          + PlaytestEnd, GuideApproach, SessionEnding, ObjectiveAnnouncer, WordCardPresenter, PlaytestFlow
Simulation/       + TickAccumulator, TurnWatchdog, ITickObserver, DelegateTickObserver, LiveVoxelInput, LookControl,
                    WorldStepper
Sound/            + SoundFrame, SoundTickObserver, SoundSetup (with a SoundOptions record)
Tip/              + ToastQueue
UI state:         TextEntry, AccentShortcuts → Input/ (they are text-input state)
```

## Target layout: `src/Vocaterra.Game` (folders by area, subfolders by feature)

```
Program.cs, VoxelGame.cs   composition root and the MonoGame loop
Startup/        LaunchOptions, AssetPaths, SceneLocator, SoundBootstrap
Diagnostics/    SessionLog, CrashHandler, WallClock, FrameClock, FrameTimings
Flow/           SessionStart, SessionEndingFlow (they stay because they use the wall clock, D-p2-2)
Input/          InputDevices, InputSnapshot (implements IInputFrame), KeyMap (Keys→GameKey), MouseLook, WorldInput
Sound/          SoundEffectOutput, DynamicSoundOutput, MicrophoneCapture
Scripted/       ScriptedRunEnd
Graphics/       SceneDrawer, DrawHelpers, QualityPreset, ScreenshotWriter
  Camera/       FirstPersonCamera, PlayerCamera, CameraEase, ViewMath, PlayerView
  Blocks/       BlockArt, BlockArtNature, BlockArtVillage, BlockAtlas, BlockAtlasLayout
  World/        VoxelRenderer, ChunkMesher, ChunkVersionTracker, ChunkViewSet, MeshVertex, OverlayMeshes, Greyscale
  Figures/      FigureMesher, FigureFacing, FigureDirector, NpcFigures, HeldArm, ArmPresenter
  Sky/          SkyModel, SkyRenderer
UI/
  Common/       UiCanvas, UiTransform (from Graphics/), UiPalette, UiScale, FontProvider, FrameView, LayerStack, CountBadge, NounIcons
  Hud/          HudLayers, HudLayout, HotbarLayer, ToastLayer, TipLayer, BeaconLabel, VoxelLayers, VoxelDebugText
  Overlays/     OverlayLayouts, LoadingLayer, ControlsLayer, EndingLayer
  Dialogue/     DialoguePanel, DialogueLayout, PixelBox, DialogueBubble, DialogueBubbleLayer, ConversationFrame, ConversationLayout,
                ConversationCards, VoxelTalkLayers, SupportLayer, SupportLayout
  Book/         BookLayer, BookGeometry, ConjugationLayout
  Checks/       CheckLayer, CheckCardLayout, MicCheckLayer
  Crafting/     CraftingLayer, CraftingLayout, RecipePanelLayer
  Inventory/    InventoryLayer, InventoryLayout
  Lesson/       LessonLayer, LessonStripLayout
  Report/       ReportLayer, ReportLayout, ReportScreen (from Reporting/)
```

After the moves, the folders `Utilities/`, `Screens/`, `Frame/`, `Persistence/`, `Reporting/` and `UI/English/` are empty and are
deleted. The trees above are the target. If an agent finds that a file's content fits another folder better, the agent may put it
there and records why in the commit body.

## Target layout: tests

- Each test file goes into the folder that matches the source folder of the class it tests, in the project that holds that class.
  For example, `SaveGameTests` goes to `tests/Vocaterra.Game.Core.Tests/Save/`, and `ChunkMesherTests` goes to
  `tests/Vocaterra.Game.Tests/Graphics/World/`. A test namespace follows its folder.
- Some tests in `Vocaterra.Game.Tests` exercise Core classes and use no Game type: `SoundMixerTests`, `NpcVoiceTests`,
  `FootstepTests`, `AmbientBedsTests` and `AmbientSynthTests`, among others. They move to `Vocaterra.Game.Core.Tests/Sound/`.
- `Component/` holds tests that drive several classes together: `RouteRunTests`, `VoxelSessionTests`, `OpeningAndEndingTests`, the
  `*FrontendTests` and similar. Grouping them now makes the later `*.Component.Tests` split a plain move.
- `TestSupport/` holds the shared helpers: `TestGame`, `Stage`, `VoxelWalker`, `LessonDriver`, `PendingSpeech`,
  `ModelFactAttribute`, `Repo`, `RouteScene` and `TrueTypeCmap`.
- `Architecture/` holds both `ArchitectureRulesTests` files. `Fixtures/` stays where it is.

## Execution (orchestrator, one Haiku 5.5 agent at a time; Sonnet 5.5 for batch 5's key design)

Integration branch `feature/game_refactor` from `main` at `5cb321e` (owner, 2026-10-27; `feature/language-packs` is merged into `main`).
Each batch is an agent branch `R16-p2-b<n>` gated into it. Commit prefix `R16:`, one commit per step. Push to the feature
branch; the owner opens the pull request into `main`. The method for each move is in R16_PHASE2.md §3 and §6, and the checks are in
§7: `git mv`, then the namespace follows the folder, then fix the `using`s (`scripts/audit-usings.py`), then `dotnet format`, then
move the tests. Use explicit pathspecs, because the owner edits the same tree.

0. **Baseline.**
   - Run `scripts/scripted-runs.sh /tmp/r16p2-base` and `python3 -I scripts/core-move-scan.py .`.
   - Record the test counts: Game.Core.Tests 851/12, Game.Tests 870/2, Host.Tests 36.
   - Write the four decisions above into R16_PHASE2.md §5.4 (done, 2026-10-27).
1. **Batches 1–4 of R16_PHASE2 §6.**
   - Batch 1: the leaves. Batch 2: the screens' models. Batch 3: the flows. Batch 4: the scripts.
   - Every destination is taken from the Core tree above.
   - Before a file holding several types moves, split it into one file per type.
2. **Batch 5 (D-p2-1a).**
   - Add `GameKey` and `IInputFrame` to Core, and `KeyMap` to Game. `InputSnapshot` implements `IInputFrame`.
   - Then move the screens, `KeyBindings`, `ListenSession`, `TextEditKeys` and `BookItemUse`, then `WorldStepper`, `PlaytestFlow`
     and `CheckBattery`.
   - Replace each `SpriteFontBase` hit-test parameter with a layout delegate supplied by Game.
   - Finish with a live run by hand, because no scripted run reads the keyboard.
3. **Batch 6: regroup Game into the target tree.**
   - Move files and namespaces only, with no code changes.
   - `tests/Vocaterra.Game.Tests/ArchitectureRulesTests.cs` exempts `WallClock.cs` and `FrameClock.cs` from its clock rules. Check
     whether it matches them by path; if so, update the paths.
4. **Batch 7: mirror the tests** as in the tests section above. Total test counts stay the same.
5. **Batch 8: docs.**
   - Update `docs/game/ARCHITECTURE.md` with both trees.
   - Update the R16 entry and the §2 layout in `docs/game/IMPLEMENTATION_PLAN.md`, and mark R16_PHASE2.md done.
   - Fix the stale project names in `CLAUDE.md`, the `Directory.Build.props` comment, the title of `src/Vocaterra.Game/README.md`,
     the comments in `Vocaterra.Host.csproj` and `Vocaterra.Game.Core.csproj`, and the `Vocaterra.Simulation` path in Core's
     `ArchitectureRulesTests`.
   - Rerun the scan and record its numbers.

## Verification

- **After each step:**
  - `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`
  - `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`
  - `scripts/limited.sh dotnet test tests/Vocaterra.Game.Tests --no-build -m:2`
- **After each batch:**
  - Run `Vocaterra.Game.Core.Tests`; its `ArchitectureRulesTests` gate every move.
  - Run `Vocaterra.Host.Tests`.
  - Run `scripts/scripted-runs.sh /tmp/r16p2-<n>`, then `python3 -I scripts/compare-runs.py /tmp/r16p2-base /tmp/r16p2-<n>` (exit 0).
  - The total test count stays the same; tests only move between projects.
- **At the end:**
  - `GATE_TARGET=feature/game_refactor scripts/merge-gate.sh "R16-b<n>:R16-p2-b<n>"` for each batch.
  - `core-move-scan.py` shows no MOVABLE game-logic file left in Game.
  - Game no longer has a `Utilities/` folder, and Core no longer has `Utilities/` or a root-level `.cs` file.
  - A live run by hand following `docs/game/SCRIPTED_RUNS.md` "By hand".
