# R16 phase 2: moving the engine-free game logic to `Vocaterra.Game.Core`

A handover for the agent that takes R16 on (written 2026-10-26, at the end of phase 1, branch `feature/organise`, commit `e5779e9`). Read it
with `docs/code_guidelines.md` (the owner's rules), `CLAUDE.md` (the machine and repository rules), `docs/game/ARCHITECTURE.md` (the two
projects and their folders) and the R16 entry of `docs/game/IMPLEMENTATION_PLAN.md` section 5 (what phase 1 did and found).

## 1. The goal

`docs/code_guidelines.md` puts the game's systems and mechanics in `Vocaterra.Game.Core` (anything not tied to MonoGame: algorithms, inventory,
physics, NPC logic, language processing, decisions) and keeps in `Vocaterra.Game` only what needs MonoGame: graphics, rendering, input management,
UI elements. Phase 1 split the 2,957-line `VoxelGame.cs` into components inside `Vocaterra.Game` (it is now about 850 lines: the composition
root and MonoGame's loop). Phase 2 moves the components and models that do not need MonoGame into Core, so that they are tested there and
cannot grow engine types, and leaves in `Vocaterra.Game` the drawing, the devices, the layers and the host.

No behaviour changes: every step is a move, a split of a file, or a small seam (an injected clock, a constant moved). The scripted runs and the
tests are the check (section 7).

## 2. The state at handover

- **Branch:** `feature/organise`, not pushed by the agent; the owner merges into `main` by pull request (never push to `main`, CLAUDE.md).
- **Phase 1's commits:** `f7d1f6d` (the scripted-runs doc) and `5350c7d` (step 1) to `e5779e9` (steps 11e and 12), one per step; `fbc8ed6`
  in between is the owner's own clean-up.
- **The namespace moves that were tried and reverted.** `225c76a` moved `GameBundle` and `Objective` to `Vocaterra.Game.Core.Content` and
  `SoundSetup` to `Vocaterra.Game.Core.Sound` without updating their users, and broke the build; the next commit reverted them. If they are
  wanted, they belong in batch 3 (`SoundSetup`) or a batch of their own, done with their users. Commit with explicit pathspecs
  (`git commit -- <paths>`) so that nothing staged by someone else goes into a commit.
- **The owner works in the same tree** (Rider) while an agent works: a file can appear or change under you (an unused-using clean-up landed
  mid-session). Check `git status` and `git log` before each commit; Rider's builds and test runs do not go through `scripts/limited.sh`, so a
  heavy command may wait for them, and the machine may be busy (section 8).
- **The new folders of `src/Vocaterra.Game`:** `Diagnostics/`, `Flow/`, `Frame/`, `Input/`, `Persistence/`, `Reporting/`, `Screens/`,
  `Scripted/`, with `Graphics/`, `Sound/`, `UI/` and `Utilities/` from before (ARCHITECTURE.md lists what each holds).

## 3. The rules every step follows

- **Machine (CLAUDE.md):** one agent at a time; every build, test, format and game run through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`).
  The .NET SDK is user-local: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1` (limited.sh sets it).
- **Verification cadence (the owner, 2026-10-26):** after each small step only build the solution with `-warnaserror`, check
  `dotnet format Vocaterra.sln --verify-no-changes` and run `tests/Vocaterra.Game.Tests` (about 25 s). Run the full set (the three game test
  projects and the scripted runs, section 7) at the end of each batch of section 6, not after every move.
- **One commit per step**, its message the step's subject and a body that says what moved where; the attribution line the session asks for.
- **A namespace follows its folder** (`src/Vocaterra.Game.Core/Sound/X.cs` is `namespace Vocaterra.Game.Core.Sound;`). Use `git mv` so the
  history follows a file; split a file of several types first (one type per file, the guideline's single responsibility).
- **Tests move with their classes**, into `tests/Vocaterra.Game.Core.Tests/<the source folder>/`, with the current sentence-style names. The
  owner decided (2026-10-26) to keep the current test projects and naming for now (not `*.Unit.Tests` / `*.Component.Tests`, not
  Given_When_Then). A test that needs both a moved class and a Game type stays in `Vocaterra.Game.Tests`.
- **Never weaken a test or an architecture rule to make a move pass.** Where a rule blocks a move, the move waits for the owner's decision
  (section 5).

## 4. What Core allows

`tests/Vocaterra.Game.Core.Tests/ArchitectureRulesTests.cs` fails the build's tests when a source file of Core (comments stripped, string
literals not) contains any of `System.Random`, `new Random`, `Random.Shared`, `Guid.NewGuid`, `DateTime`, `DateTimeOffset`, `Stopwatch`,
`Environment.TickCount`, `TimeProvider`, `Thread.Sleep`, `Microsoft.Xna`, `FontStashSharp`, `using Godot`; and when Core's project has a
`<PackageReference>` or a project reference other than `../Vocaterra.Nlp.*`. Note: the word `DateTime` in a string or an XML doc's prose that is
not behind `//` counts too (`///` lines are stripped, as they start with `//`). `Vocaterra.Game.Core.csproj` gives `InternalsVisibleTo` to both
test projects. `tests/Vocaterra.Game.Tests/ArchitectureRulesTests.cs` has the Game's own rules (no `System.Random`, no `Guid`, no
`DateTime.Now`/`UtcNow` outside `WallClock.cs`, no `Stopwatch` outside `FrameClock.cs`).

## 5. The inventory: what moves, what stays, what waits

`python3 -I scripts/core-move-scan.py .` classifies every file of `src/Vocaterra.Game` (MOVABLE: no banned token and no package, and every Game
file it needs is MOVABLE too; BLOCKED-self: it uses one, listed; BLOCKED-deps: it needs blocked Game files, listed). At handover: 99 MOVABLE, 58
BLOCKED-self, 15 BLOCKED-deps. MOVABLE means engine-free, not "belongs in Core": the guideline keeps graphics and UI in Game even without
MonoGame types. The judgement below is the plan.

### 5.1 Move as they are (engine-free game logic and state)

| From `src/Vocaterra.Game/` | To `src/Vocaterra.Game.Core/` | Notes |
| --- | --- | --- |
| `Utilities/TickAccumulator.cs`, `FrameStats.cs`, `BusyFrameStats.cs`, `TurnWatchdog.cs` | `Simulation/` (the tick), `Reporting/` (the stats) | plain numbers |
| `Utilities/ScreenshotGate.cs`, `Scripted/ShotRequest.cs` | `Scripted/` | the scripted run's rules |
| `Scripted/ScriptItemNames.cs` | `Items/` | |
| `Utilities/PlaytestEnd.cs`, `Utilities/GuideApproach.cs` | `Session/` | the playtest's and the greeting's rules |
| `Utilities/VerbReading.cs` | `Language/Words/` | shared by the book's labels and tables |
| `UI/Toasts.cs` (`ToastQueue`), `UI/TextEntry.cs` (`TextEntry`, `AccentShortcuts`) | `Session/` or a new `Presentation/` | split `AccentShortcuts` into its own file |
| `Input/InputFocus.cs` (`InputFocus`, `KeyRepeater`), `Input/OpenKeySwallow.cs` | `Input/` | split `KeyRepeater` into its own file |
| `Input/IGameCommand.cs` (`IGameCommand`, `DelegateCommand`), `Input/GameCommands.cs` | `Input/` | the command pattern |
| `Frame/ITickObserver.cs` (`ITickObserver`, `DelegateTickObserver`) | `Simulation/` | the observer pattern |
| `Diagnostics/EventPrinter.cs` | `Event/` | |
| `UI/SessionEnding.cs`, `UI/InventoryScreen.cs` | `Session/`, `Items/` | screen state, no drawing |
| `UI/CheckScreen.cs` | `Language/Checks/` | the battery's state machine |
| `UI/DialogueModel.cs`, `UI/SpeechPanel.cs` | `Npc/` or `Session/` | split `SpeechPanel.cs` first: `SpeechAction`, `PanelKey`, `SpeechButton`, `SpeechButtons` move; `PixelBox` and `DialogueLayout` are layout and stay |
| `UI/ReportPanel.cs`, `UI/ReportEntry.cs` (`ReportEntry`, `ReportWrap`) | `Reporting/` | split `ReportWrap` out |
| `Sound/MicCheckModel.cs` | `Language/Speech/` (beside `MicCheck`) | `MicCheckButton`, `MicCheckPhase` with it |
| `UI/CraftingScreen.cs` | `Items/` | split it: `CraftingScreen` and `CraftingRow` move, `CraftingLayout` stays |
| `UI/RecipePanel.cs` (`RecipePanelModel`, `RecipeIngredientCount`), `UI/Journal.cs`, `UI/PhraseJournal.cs` | `Items/`, `Language/Words/`, `Language/Phrases/` | rows of state, no drawing |
| `UI/Book/Book.cs`, `BookPages.cs`, `ConjugationTable.cs`, `BookVerbLabels.cs`, `BookRecordings.cs`, `BookPractise.cs`, `BookSpeechControls.cs`, `BookSpeaker.cs` | `Language/Book/` (new) | check each file's types first: `Book.cs` holds `BookModel` with `BookPage`, `BookTabs`, `BookRect`, `BookScroll` and `DetailScroll` (a rect is layout: leave it, or move it with the model if the model needs it). `BookSpeechControls` takes a `BookGeometry` (layout) in its click methods: move `BookGeometry` with it, or keep the click methods in Game |
| `Utilities/TurnPump.cs`, `SpeechActionRunner.cs`, `SupportFrontend.cs` | `Session/` | |
| `Flow/TurnPresenter.cs`, `WordCardPresenter.cs`, `ObjectiveAnnouncer.cs`, `GuideGreeting.cs` | `Flow/` (new) or `Session/` | after their dependencies (TurnPump, DialogueModel, MicCheckModel, ToastQueue) |
| `Flow/BeaconDirector.cs` with `UI/PointLabels.cs` | `Navigation/` (beside `Beacon`) | `PointLabels` names the scene's points in Spanish: game words |
| `Persistence/SaveService.cs` | `Save/` | |
| `Reporting/ReportStateBuilder.cs` with `Utilities/BuildCommit.cs` | `Reporting/` | BuildCommit reads the assembly's metadata, no clock |
| `Sound/SoundFrame.cs`, `Sound/SoundTickObserver.cs` | `Sound/` | |
| `Graphics/GreyscaleWorld.cs` | `Language/Words/` (beside `WordColours`) | it decides which kinds are grey and toasts a newly known noun; the renderer is a callback |
| `Utilities/VoxelScript.cs`, `ScenarioScript.cs`, `CheckScript.cs` | `Scripted/` | after the script's step types leave `LaunchOptions.cs` (5.3) |
| `Input/LiveVoxelInput.cs` | `Simulation/` | the frame's input as commands |
| `Input/LookControl.cs` | `Simulation/` or keep | it uses `CameraBasis.RadiansPerStep` from `Graphics/ViewMath.cs`: move that constant into `LookControl`, or keep `LookControl` in Game. It uses `double` (Core's `Angles` is integer-only: do not put it there) |

### 5.2 Stay in `Vocaterra.Game` (engine-free, but drawing or UI)

They are presentation by the guideline; being engine-free, they stay unit-tested in `Vocaterra.Game.Tests`: `Graphics/BlockArt*.cs`,
`BlockAtlasLayout.cs`, `ChunkMesher.cs`, `ChunkVersionTracker.cs`, `ChunkViewSet.cs`, `FigureMesher.cs`, `FigureFacing.cs`, `CameraEase.cs`,
`HeldArm.cs`, `MeshVertex.cs`, `NpcFigures.cs`, `OverlayMeshes.cs`, `PlayerView.cs`, `QualityPreset.cs`, `SkyModel.cs`, `ViewMath.cs`,
`Greyscale.cs`, `FrameTimings.cs`; and the layouts and visuals of `UI/`: `BeaconLabel.cs`, `OverlayLayouts.cs`, `ReportLayout.cs`,
`HudLayout.cs`, `Book/BookGeometry.cs`, `Book/ConjugationLayout.cs`, `CheckCardLayout.cs`, `Dialogue/ConversationLayout.cs`,
`Dialogue/ConversationCards.cs`, `DialogueBubble.cs`, `InventoryLayout.cs`, `SupportLayout.cs`, `CountBadge.cs`, `UiPalette.cs`, `UiScale.cs`,
`NounIcons.cs`, `VoxelDebugText.cs`; and the host's file locations, `Utilities/AssetPaths.cs` and `SceneLocator.cs`. The engine code stays too:
`VoxelGame.cs`, `Program.cs`, the renderers, `SceneDrawer`, `ScreenshotWriter`, `UiTransform`, the layers, `FontProvider`, `FrameView`,
`DrawHelpers`, `Input/InputDevices.cs`, `MouseLook.cs`, `WorldInput.cs`, `Sound/SoundEffectOutput.cs`, `DynamicSoundOutput.cs`,
`MicrophoneCapture.cs`, `Scripted/ScriptedRunEnd.cs` (it saves pictures), `Reporting/ReportScreen.cs` (it reads the frame's pixels),
`Graphics/PlayerCamera.cs`, `FigureDirector.cs`, `ArmPresenter.cs` (MonoGame's `Vector3`), `UI/UiCanvas.cs` (`Point`, `MouseState`).

### 5.3 Blocked: they wait for a seam or a decision

| What | Why it is blocked | The way through |
| --- | --- | --- |
| The screens: `Screens/IScreen.cs`, `ScreenStack.cs`, `CheckBattery.cs`, `MicCheckScreen.cs`, `DialogueScreen.cs`, `BookScreen.cs`, `InventoryPanel.cs`, `CraftingPanel.cs`, `ControlsHelp.cs`; `Input/KeyBindings.cs`, `ListenSession.cs`, `TextEditKeys.cs`, `BookItemUse.cs`; then `Frame/WorldStepper.cs` (needs `CraftingPanel`) and `Flow/PlaytestFlow.cs` (needs `CheckBattery`) | They read MonoGame's `Keys` and the frame's `InputSnapshot` (the scan counts 18 `Keys` in `CheckBattery`, 36 in `DialogueScreen`, and so on). `DialogueScreen`, `MicCheckScreen` and `ReportScreen` also take a `SpriteFontBase` to hit-test clicks through their layouts | Decision D-p2-1 (below) |
| `Utilities/SessionLog.cs`, `CrashHandler.cs`, `WallClock.cs`; then `Flow/SessionStart.cs`, `SessionEndingFlow.cs`, `Persistence/CrashSource.cs` | They name `DateTime`/`DateTimeOffset` (the wall clock of the log's `t` fields and the crash report), a banned token even when injected | Decision D-p2-2. `CrashSource` itself needs only `ICrashSource` and `CrashGameState`, which live in `CrashHandler.cs`: split them into their own file and they can move without the handler |
| `Persistence/AutosaveController.cs` | It calls `MonotonicClock.Milliseconds()` (a `Stopwatch`) to time a save for its log line | Inject a `Func<double>` (the elapsed milliseconds) from Game, then move it to `Save/` |
| `Utilities/LaunchOptions.cs` (1,015 lines) and `Sound/SoundSetup.cs`, `Sound/SoundBootstrap.cs` | Engine-free, but the whole command line; `ScriptStep`, `ScriptStepKind` and `VoxelStep` (the scripts' steps) are declared in it, so every script needs it | D-p2-3: keep `LaunchOptions` in Game; move the step types to their own files in Core's `Scripted/`; give the moved classes small option records (a `SoundOptions` for `SoundSetup`) rather than `LaunchOptions` |
| `UI/LessonStrip.cs`, `UI/LessonFrontend.cs` | `LessonStrip` uses `DialoguePanel.EnglishPrefix` (a layer's constant) for its wrapped lines, and holds the strip's layout beside `LessonAction` and `LessonKey` | Move the constant to the model side, split the strip's layout (stays) from `LessonAction`/`LessonKey`/`LessonFrontend` (move) |
| `UI/Dialogue/ConversationFrame.cs`, `UI/LayerStack.cs` | Drawing | Stay |

### 5.4 Decisions for the owner (answered 2026-10-27; see `GAME_PROJECTS_LAYOUT.md`)

**The owner's answers (2026-10-27):** D-p2-1 **(a)**: `GameKey` and `IInputFrame` in Core, `KeyMap` (`Keys` to `GameKey`) in Game,
`InputSnapshot` implements `IInputFrame`, and the click hit-tests take a layout delegate from Game instead of a `SpriteFontBase`.
D-p2-2: the default (`SessionLog`, `CrashHandler`, `WallClock` stay in Game; Core's rule unchanged). D-p2-3: the default (`LaunchOptions`
stays in Game; the step types go to Core's `Scripted/`; small option records such as `SoundOptions`). D-p2-4: test folders mirror source
folders now; project names and sentence-style test names stay. Game keeps top-level folders by area with feature subfolders.
`GAME_PROJECTS_LAYOUT.md` holds the target trees and wins where it differs from 5.1. Work lands on `feature/game_refactor`.

The questions as they were put:

- **D-p2-1, the keys in Core.** (a) Core gets an engine-free key vocabulary: a `GameKey` enum and an input-frame interface (pressed, held,
  Shift, Ctrl, the clicks, the wheel, the pointer on the canvas); Game's `InputSnapshot` implements it with one `Keys` to `GameKey` table. The
  screens, `KeyBindings`, `ListenSession` and `TextEditKeys` then move almost unchanged, with their tests; the click hit-tests take a layout
  function from Game instead of a font. (b) Key handling stays in Game: each screen splits into a Core flow (its rules: pick, skip, begin and end
  listening, open, close) and a Game key map. (a) moves more for less code; (b) follows the guideline's "input management in Game" to the
  letter. The device reading (`InputDevices`, `MouseLook`, `WorldInput`) stays in Game either way. The agent's recommendation was (a).
- **D-p2-2, the wall clock.** Keep `SessionLog`, `CrashHandler` and `WallClock` in Game as the host's file output and clock (recommended), or
  relax Core's rule to allow an injected `Func<DateTimeOffset>`. The first keeps the rule as written.
- **D-p2-3, the options.** As in 5.3 (keep `LaunchOptions` in Game, small records for Core). Grouping its flags by area was also in R16's
  backlog item.
- **D-p2-4, the test projects.** Renaming to `*.Unit.Tests` / `*.Component.Tests` and Given_When_Then names (the guideline) was deferred by
  the owner on 2026-10-26; ask whether phase 2 should do it at its end.

## 6. The order of work

Each batch is a few commits; each commit builds, formats and passes `Vocaterra.Game.Tests`; each batch ends with the full check (section 7).

0. **Before anything:** check `git status` and `git log` (section 2); build; take your own baseline:
   `scripts/scripted-runs.sh /tmp/r16p2-base` (see `docs/game/SCRIPTED_RUNS.md`). Run `scripts/core-move-scan.py` to see the starting point.
1. **Leaves with no Game dependency:** TickAccumulator, FrameStats, BusyFrameStats, TurnWatchdog, ScreenshotGate, ShotRequest,
   ScriptItemNames, PlaytestEnd, GuideApproach, VerbReading, ToastQueue, TextEntry and AccentShortcuts, InputFocus and KeyRepeater,
   OpenKeySwallow, IGameCommand, GameCommands, ITickObserver, EventPrinter, SessionEnding, InventoryScreen.
2. **The screens' models:** DialogueModel with the split SpeechPanel, CheckScreen, ReportPanel and ReportEntry, MicCheckModel, CraftingScreen
   (split), RecipePanelModel, Journal, PhraseJournal, the book's models (split their geometry), LessonStrip (split) and LessonFrontend.
3. **The flows:** TurnPump, SpeechActionRunner, SupportFrontend, TurnPresenter, WordCardPresenter, ObjectiveAnnouncer, GuideGreeting,
   BeaconDirector with PointLabels, SaveService, ReportStateBuilder with BuildCommit, SoundFrame, SoundTickObserver, SoundSetup (with an option
   record), GreyscaleWorld, AutosaveController (inject its clock), CrashSource (after `ICrashSource` leaves `CrashHandler.cs`).
4. **The scripts:** the step types out of `LaunchOptions.cs`, then VoxelScript, ScenarioScript, CheckScript.
5. **After D-p2-1:** the screens, IScreen and ScreenStack, KeyBindings, ListenSession, TextEditKeys, BookItemUse, then WorldStepper,
   PlaytestFlow, CheckBattery.
6. **Docs:** ARCHITECTURE.md (the two projects' folders), the R16 entry of the plan, this file's status; run the scan again and record the
   numbers.

How a move goes: `git mv` the file (split it first if it holds several types); change its namespace to its new folder; build, and add the
`using`s the errors ask for across `src/`, `tests/` and `tools/` (most files that used it need `using Vocaterra.Game.Core.<Folder>;`); remove
`using`s that became unused (`python3 -I scripts/audit-usings.py . <files>` lists a `Vocaterra.*` using with no type of it named in the file;
it misses names used only through members, so let the build confirm a removal); `dotnet format` fixes the order of the usings
(`dotnet format style Vocaterra.sln --diagnostics IMPORTS --include <files>`); move the class's tests; build, format, test.

## 7. How to check

- **Each step:** `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`;
  `scripts/limited.sh dotnet test tests/Vocaterra.Game.Tests --no-build -m:2`.
- **Each batch:** also `tests/Vocaterra.Game.Core.Tests` (its `ArchitectureRulesTests` are the gate of every move) and
  `tests/Vocaterra.Host.Tests`, and the scripted runs: `scripts/scripted-runs.sh /tmp/r16p2-<batch>` then
  `python3 -I scripts/compare-runs.py /tmp/r16p2-base /tmp/r16p2-<batch>` (exit 0: every run the same; seven exactly, nine by the order of what
  they print, as `SCRIPTED_RUNS.md` explains). The counts at handover: Game.Core.Tests 851 passed and 12 skipped, Game.Tests 870 and 2,
  Host.Tests 36. A moved test moves its count from one project to the other; the total stays.
- **A live run by hand** at the end (`SCRIPTED_RUNS.md`, "By hand"): the scripted runs never read the keyboard or the mouse, and batch 5 moves
  the keys.

## 8. Pitfalls met in phase 1

- **Constructor lambdas and nullable fields.** In `VoxelGame`'s constructor a lambda that dereferences a field assigned later fails the build
  (`CS8602` as an error). Make the object first, or route the call through a one-line method (`EndPlaytest`), or share a small object made
  as a field initializer (`ShotRequest`).
- **A method group binds its target when it is made:** `_screen.Close` passed before `_screen` is assigned throws later; a lambda does not.
- **Same type names in two namespaces.** `VoxelGame.cs` starts with `using SoundEventMapper = SoundEventMapper;` and tests alias
  `using SoundEventMapper = Vocaterra.Game.Core.Sound.SoundEventMapper;`: Game and Core have both had a `SoundEventMapper`. Check a moved type's
  name in its new namespace before the move. Core already has `Input/`, `Sound/`, `Simulation/` and `Utilities/` namespaces; a namespace called
  `Simulation` sits beside properties called `Simulation`, which is legal but read carefully.
- **Scripted runs:** nine of the sixteen land their turns a few ticks apart from one capture to the next, with the same binary (the worker's
  speed); `--report-open` never ends a run (by S21's design), so it is not in the set; a run that seems stuck may be waiting for `limited.sh`'s
  lock behind another heavy command (it says so on its first line).
- **The owner's parallel edits** (section 2): commit with explicit pathspecs; check `git status` and `git log` first, as a commit can land under you.
- **Shell:** quote heredocs (`<<'EOF'`) that carry Markdown: an unquoted one runs the backticked text as commands. The sandbox refuses command
  lines with the words `eval` or `hash` (CLAUDE.md).
- **`dotnet format`** reported no IDE0005 (unused usings are not enforced), which is why `scripts/audit-usings.py` exists.

## 9. Left for later (not phase 2)

- `SceneBuilder` (1,389 lines: one builder per prop kind, a tree planter, an undergrowth planter) and `VoxelSimulation` (crafting, gathering,
  filling and placing as components): gated by the seeded scene digests and the save tests.
- The report names "check (pre)" while a playtest's mic check takes the keys (`ScreenStack`'s name order, kept on purpose).
- Scripted runs that are not tick-deterministic (the plan's section 5): a scripted run does not freeze the world exactly while a turn is out.
- `CLAUDE.md` still names the old projects (`Vocaterra.Simulation`, `Vocaterra.MonoGame`, an engine-free `Vocaterra.Game`).
