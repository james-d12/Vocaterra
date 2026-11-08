using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Reporting;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Screens;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Flow;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.Camera;
using Vocaterra.Game.Graphics.Figures;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Input;
using Vocaterra.Game.Scripted;
using Vocaterra.Game.Sound;
using Vocaterra.Game.Startup;
using Vocaterra.Game.UI.Checks;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Crafting;
using Vocaterra.Game.UI.Dialogue;
using Vocaterra.Game.UI.Hud;
using Vocaterra.Game.UI.Lesson;
using Vocaterra.Game.UI.Overlays;
using Vocaterra.Game.UI.Report;
using Vocaterra.Game.UI.Station;

namespace Vocaterra.Game;

/// <summary>
/// The engine host of the 3D first-person scene, the prototype's only world (S2 to S4), and the composition root of the front-end (R16): the
/// constructor makes the components and wires them, and MonoGame's loop calls them in the frame's order. <see cref="Update"/> turns the real clock
/// into whole ticks, reads the input (<see cref="InputDevices"/>: the report panel, the global keys, the top screen of the
/// <see cref="ScreenStack"/> or the world's keys, then <see cref="WorldInput"/>), applies the finished turns (<see cref="TurnPresenter"/>) and
/// steps the world (<see cref="WorldStepper"/> and its tick observers); <see cref="Draw"/> places the <see cref="PlayerCamera"/> and draws through
/// the <see cref="SceneDrawer"/>. The rules live in <c>Vocaterra.Game.Core</c> and in the components under <c>Screens/</c>, <c>Flow/</c>,
/// <c>Frame/</c>, <c>Input/</c>, <c>Persistence/</c>, <c>Reporting/</c> and <c>Scripted/</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What works in 3D.</b> Walking (WASD or the arrows, Space to jump), mouse look in relative mode (Esc lets the mouse go, a click takes it
/// again), gathering by holding the left button on a trunk or a berry bush, placing with the right button, the hotbar, the inventory (I, or E
/// when the crosshair is not on an NPC), the journal (J), the debug overlay (F3), toasts, the NPC figures with their names, the beacon column
/// and its edge arrow, and the NPCs: aim the crosshair at the guide, the shopkeeper, the villager or one of the village's four and press E (or T to type, V to talk) to open
/// the panel, which shows the NPC's name. The guide's answers point the beacon at the named noun's nearest block; "¿qué es esto?" names the block
/// under the crosshair; the shopkeeper hands over a bottle that the left button fills at the river; the villager points the beacon at the river.
/// The panel takes the keyboard and frees the mouse while it is open. The sign reads <c>Tienda / Río</c>.
/// </para>
/// <para>
/// <b>Script steps.</b> <c>--look</c>, <c>--walk-to</c>, <c>--interact</c>, <c>--place</c>, <c>--select</c>, <c>--craft</c>, <c>--talk</c> (walk to an NPC and
/// open the panel), <c>--say</c> (type and send a line; the scripted run waits for each reply), <c>--leave</c>, <c>--shot</c>, then <c>--beacon</c>,
/// <c>--journal</c>, <c>--inventory</c> and <c>--crafting</c> at the end (<see cref="ScenarioScript"/>).
/// </para>
/// </remarks>
public sealed class VoxelGame : Microsoft.Xna.Framework.Game
{
    private const int StartWidth = 1280;
    private const int StartHeight = 720;

    /// <summary>The save file F6 writes when the run has no <c>--save</c> file.</summary>
    private const string DefaultSavePath = "vocaterra.save.json";

    private readonly LaunchOptions _options;
    private readonly VoxelSession _session;
    private readonly Scene _scene;
    private readonly SaveService _saves;
    private readonly VoxelSimulation _sim;
    private readonly GraphicsDeviceManager _graphics;
    private readonly TickAccumulator _accumulator = new(VoxelSimulation.TicksPerSecond);
    private readonly FrameStats _stats = new();
    private readonly IFrameClock _clock;
    private readonly LookControl _look = new();
    private readonly ViewModeState _viewMode = new();
    private readonly Func<int, int, int, bool> _isSolid;
    private readonly UiCanvas _canvas;
    private readonly PlayerCamera _camera;
    private readonly FigureDirector _figures;
    private readonly ArmPresenter _arm;
    private readonly BeaconDirector _beacon;
    private readonly EventPrinter _events;
    private readonly LiveVoxelInput _live = new();
    private readonly ScenarioScript? _scenario;
    private readonly InputFocus _focus = new();
    private readonly DialogueModel _dialogue;
    private readonly TurnPump _pump;
    private readonly ListenSession _listen;
    private readonly SoundFrontend _sound;
    private readonly ISoundOutput _soundOutput;
    private readonly SoundLogWriter? _soundLog;
    private readonly SoundFrame _soundFrame;
    private readonly SpeechActionRunner _actions;
    private readonly SupportFrontend _support;
    private readonly SessionEnding _ending = new();
    private readonly GuideGreeting _greeting;
    private readonly SessionEndingFlow _endingFlow;
    private readonly LessonFrontend _lesson;
    private readonly ControlsHelp _controls;
    private readonly SettingsPanel _settings;
    private bool _aimedTipOffered;
    private readonly NpcVoice _voice;
    private readonly MicCheckModel? _micCheck;
    private readonly MicCheckScreen _micScreen;
    private readonly InputDevices _devices;
    private readonly TextEditKeys _textEdits = new();
    private readonly OpenKeySwallow _swallow = new();
    private readonly BookModel _book = new();
    private readonly BookRecordings _recordings;
    private readonly BookSpeechControls _bookControls;
    private readonly BookScreen _bookScreen;
    private readonly ToastQueue _toasts = new();
    private readonly InventoryScreen _inventory = new();
    private readonly FrameTimings _timings = new();
    private readonly WorldStepper _stepper;
    private readonly TurnPresenter _turns;
    private readonly VoxelDebugOverlay _debug = new();
    private readonly SceneDrawer _drawer;
    private bool _firstUpdateSeen;
    private NpcFigure? _aimed;
    private readonly WordCardQueue _cards = new();
    private readonly CraftingScreen _crafting = new();
    private readonly InventoryPanel _inventoryPanel;
    private readonly CraftingPanel _craftingPanel;
    private readonly StationScreen _station = new();
    private readonly StationPanel _stationPanel;
    private int _loadingFrames;
    private readonly ShotRequest _shots = new();
    private readonly ScriptedRunEnd _runEnd;
    private readonly CheckBattery _checks;
    private readonly SessionLog _log;

    // The report panel (S21, D-game-17): F8 pauses the world and the next frame is kept in memory (the picture the player saw); nothing is written
    // until Ctrl+Enter, and Esc keeps nothing. A scripted report (--report, --report-open) is typed and saved by the script's frame.
    private readonly ReportPanel _report = new();
    private readonly ReportScreen _reportScreen;
    private readonly DialogueScreen _dialogueScreen;
    private string _sessionDirectory = string.Empty;
    private bool _microphoneFound;

    // The playtest profile (S12): the post-check starts by itself or with F10, its save goes to the session folder, and the thank-you
    // screen waits for Esc. A screenshot waits for the wanted window size before it is taken (ScreenshotGate).
    private readonly bool _playtest;
    private readonly PlaytestFlow _playtestFlow;
    private readonly ScreenStack _screens;
    private readonly KeyBindings _globalKeys;
    private readonly KeyBindings _worldKeys;
    private readonly WorldInput _worldInput;

    // The autosave (X05, R04): the policy decides the frames; the file is the playtest's playtest.save, else autosave.save in the session folder.
    // A run with --no-autosave has neither.
    private readonly AutosaveController? _autosave;

    /// <summary>
    /// Creates the 3D game for a started session (its simulation is the scene's), the built scene, the NPCs' voices (S9) and the builder of a fresh
    /// scene of the session's seed (a save is taken against it, S10).
    /// </summary>
    /// <param name="options">The launch options.</param>
    /// <param name="session">The started session.</param>
    /// <param name="scene">The built scene.</param>
    /// <param name="voice">The NPCs voices.</param>
    /// <param name="pristine">Builds a fresh scene of the session seed.</param>
    /// <param name="savedVolumes">The sound volumes a loaded save holds (S24), or <see langword="null"/>; <c>--sfx-volume</c> and <c>--ambient-volume</c> override them.</param>
    /// <param name="layout">The scene's layout, whose places the ambient beds listen to (S25), or <see langword="null"/> (a run without one has no places).</param>
    /// <param name="savedTextScale">The interface text size in percent a loaded save holds (X08), or <see langword="null"/>; <c>--text-scale</c> overrides it.</param>
    public VoxelGame(LaunchOptions options, VoxelSession session, Scene scene, NpcVoice voice, Func<Scene> pristine, SoundVolumes? savedVolumes = null, SceneLayout? layout = null, int? savedTextScale = null, SaveSettings? savedSettings = null)
    {
        _options = options;
        _canvas = new UiCanvas(SaveSettings.SnapTextScale(options.TextScale ?? savedTextScale ?? 100), () => (GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
        _devices = new InputDevices(_canvas);
        _session = session;
        session.NeedsEnabled = !options.NeedsOff; // --needs off freezes the needs (N01, a test aid)
        session.RelaxedNeeds = options.RelaxedNeeds || savedSettings?.RelaxedNeeds == true; // half the rates, a faint for a death (N05, design decision 17)
        session.NeedsRate = options.NeedsRate ?? savedSettings?.EffectiveNeedsRate ?? SaveSettings.DefaultNeedsRate; // the drive rate (N09)
        _underwaterBlur = options.UnderwaterBlur ?? savedSettings?.EffectiveUnderwaterBlur ?? UnderwaterBlurSettings.Default; // H11: a launch option overrides the saved setting for a run
        _needsHud = new NeedsHud(session) { EffectsOn = options.ViewEffects ?? !(savedSettings?.ViewEffectsOff ?? false) };
        foreach (var (meter, value) in options.SetNeeds)
        {
            session.SetNeed(meter, value); // --set-need (N01, a test aid)
        }

        session.SleepOffered = !options.Playtest; // a playtest plays one session per run: its post-check follows the ending, and no sleep is offered (V12c)
        _scene = scene;
        _voice = voice;
        _sim = session.Simulation;
        (_sound, _soundOutput, _soundLog) = SoundBootstrap.Create(options, session, savedVolumes, layout);
        _isSolid = (x, y, z) => _sim.Grid.IsSolid(x, y, z);
        // The book's speaker buttons (S20b): a row plays the recording the speech folder has for it (the files are read once, here).
        _recordings = new BookRecordings(SpeechLines.Enumerate(session.Bundle, session.Nouns, session.Phrases, session.Data.Npcs).Select(static l => l.LineId).Where(voice.HasRecording), session.Phrases);
        _bookControls = new BookSpeechControls(_book, new BookSpeaker(voice, _sound.Mixer), session, _recordings);
        _dialogue = new DialogueModel(_focus, session.OpeningLine, session.Data, session.Phrases, session.Locale, showVerdictNotes: options.Dev);
        _soundFrame = new SoundFrame(_sound, voice, session, () => new PanelState(_inventory.IsOpen, _crafting.IsOpen, _book.IsOpen, (int)_book.Page, _dialogue.IsOpen));
        var microphone = options.UsesMicrophone ? MicrophoneCapture.TryOpen() : null;
        _listen = new ListenSession(new PushToTalkSpeechCapture(microphone) { Sounds = _sound.Mixer });
        _microphoneFound = microphone is not null;
        _pump = new TurnPump(session, _dialogue, CursorCell);
        _actions = new SpeechActionRunner(session, _dialogue, voice, CursorCell);
        _support = new SupportFrontend(session, _dialogue, _actions, Console.WriteLine);
        _events = new EventPrinter(session.Events, _support.LinesFor, Console.WriteLine);
        _beacon = new BeaconDirector(session, Console.WriteLine, Console.Error.WriteLine);
        _lesson = new LessonFrontend(session, _dialogue, Console.WriteLine) { OnNote = _toasts.Show };
        session.Deck.Enabled = !options.IsScreenshotRun && options.Script.Count == 0; // the guide offers the warm-up in play and the playtest; the scripted runs stay as recorded (X06)
        _ending.MarkSeen(session); // a game loaded after its ending does not play it again
        _micCheck = options.ShowsMicCheck ? new MicCheckModel(session.LogMicCheck, session.Locale, session.Data.Words.Greeting) : null;
        _playtest = options.Playtest;
        _debug.Visible = options.Dev; // U01: clean by default; --dev (or F3, not in a playtest) shows the overlay and the developer note
        _look.Set(scene.Spawn.Yaw, scene.Spawn.Pitch);
        _camera = new PlayerCamera(_sim, _look, _viewMode, _isSolid);
        _figures = new FigureDirector(scene, _sim, _look, _dialogue, session.Data.Npcs);
        _arm = new ArmPresenter(_sim, _isSolid, options.Dusk);
        _drawer = new SceneDrawer(options, session, _toasts, _figures.Figures, Console.WriteLine) { BlurLevel = () => UnderwaterBlurSettings.Effective(_underwaterBlur, _needsHud.EffectsOn) }; // the accessibility switch forces the blur off (H11)
        _reportScreen = new ReportScreen(
            _report,
            session,
            options.ReportKind,
            options.ReportDir,
            () => _sessionDirectory,
            open => ReportStateBuilder.Build(session, (_look.YawSteps, _look.PitchSteps), open, _drawer.Preset.Name, options.Greyscale, options.NoSpeech, _microphoneFound, options.UsesMicrophone),
            () => GraphicsDevice,
            _canvas,
            () => _drawer.View.Fonts.Get(FontProvider.NativeSize),
            _toasts,
            _sound,
            _textEdits,
            Console.WriteLine,
            Console.Error.WriteLine);
        _dialogueScreen = new DialogueScreen(
            _dialogue,
            session,
            voice,
            _actions,
            _lesson,
            _listen,
            _swallow,
            _textEdits,
            _sound,
            (x, y) => LessonLayer.LayoutFor(_drawer.View, _lesson)?.ActionAt(x, y),
            (x, y) => DialoguePanel.LayoutFor(_dialogue, _canvas.Width, _canvas.Height, _drawer.View.Fonts.Get(FontProvider.NativeSize), session).ActionAt(x, y),
            () => _aimed?.Name,
            options.IsScreenshotRun,
            Console.WriteLine);
        _bookScreen = new BookScreen(
            _book,
            _bookControls,
            _focus,
            session,
            options.BookStart,
            options.BookVerb,
            options.BookDetailLines,
            scripted: options.IsScreenshotRun && options.Script.Count > 0,
            _toasts,
            _sound,
            () => new BookGeometry(_canvas.Width, _canvas.Height),
            () =>
            {
                if (_dialogue.IsOpen)
                {
                    _dialogueScreen.Close();
                }
            },
            Console.WriteLine);
        _lesson.OnRecapDone = _bookScreen.ShowPhrases; // the lesson's recap ends on the journal's phrases page (S17)
        _controls = new ControlsHelp(() => ControlsLayer.MaxScrollFor(_drawer.View));
        _settings = new SettingsPanel(() => session.NeedsRate, rate => { session.NeedsRate = rate; Console.WriteLine($"needs rate: {SettingsPanel.RateText(session.NeedsRate)}"); }, () => _needsHud.EffectsOn, on => { _needsHud.EffectsOn = on; Console.WriteLine($"view effects: {(on ? "on" : "off")}"); }, () => session.RelaxedNeeds, on => { session.RelaxedNeeds = on; Console.WriteLine($"relaxed needs: {(on ? "on" : "off")}"); }, () => _underwaterBlur, level => { _underwaterBlur = level; Console.WriteLine($"underwater blur: {level.ToString().ToLowerInvariant()}"); }); // N09, H11
        _inventoryPanel = new InventoryPanel(_inventory, _focus, session, () => _book.IsOpen);
        _craftingPanel = new CraftingPanel(_crafting, session, _sound, (x, y) => new CraftingLayout(_canvas.Width, _canvas.Height, session.Recipes.Entries.Count).RowAt(x, y), () => _book.IsOpen || _inventory.IsOpen || _dialogue.IsOpen);
        _stationPanel = new StationPanel(
            _station,
            session,
            new NpcInstructionAudio(voice, new BookSpeaker(voice, _sound.Mixer), _recordings),
            StationHitAt,
            () => _book.IsOpen || _inventory.IsOpen || _crafting.IsOpen || _dialogue.IsOpen);
        _stationPanel.Talk = new TeacherTalk(session, _listen, CursorCell); // W08: V, Space and W at the station talk to the teacher at her teaching prompt
        _pump.Talk = _stationPanel.Talk; // her turns are the station's, not the dialogue panel's
        session.StationRequested += OpenStationFromOffer;
        if (options.IsScreenshotRun)
        {
            _clock = new FixedFrameClock(VoxelSimulation.TicksPerSecond);
            if (options.Script.Count > 0)
            {
                var hooks = new ScenarioHooks(
                    npc => _dialogueScreen.Open(npc, null),
                    _dialogueScreen.Close,
                    _shots.Request,
                    () => _shots.Pending,
                    _dialogueScreen.RunLessonStep,
                    (text, keep) => _reportScreen.RequestScripted(text, keep, OpenScreenName()),
                    () => _report.Paused,
                    _bookScreen.PlayTarget,
                    _bookScreen.PractiseFromScript,
                    () =>
                    {
                        ClosePanels();
                        SleepNow("--sleep");
                    },
                    item =>
                    {
                        if (session.Simulation.Inventory.Count(item) < 1)
                        {
                            session.Simulation.GrantItems(item, 1); // a test aid: --eat gives the food the bag lacks
                        }

                        var result = session.EatItem(item);
                        var healed = session.Data.Needs.FoodOf(ItemDefinition.Get(item).Name) is { Healing: > 0 } ? $", health {session.Needs.Value(NeedMeter.Health):0.#}" : string.Empty; // a food that heals shows the health too (N07)
                        Console.WriteLine($"eat: {result} ({item}), hunger {session.Needs.Value(NeedMeter.Hunger):0.#}, thirst {session.Needs.Value(NeedMeter.Thirst):0.#}{healed}");
                    },
                    () => Console.WriteLine($"drink: {session.DrinkByHand()}, thirst {session.Needs.Value(NeedMeter.Thirst):0.#}"),
                    () => Console.WriteLine($"take-belongings: {session.TakeBelongings()}, chest {(session.Chest is { } chest ? $"{chest.Total} items at {chest.Cell.X},{chest.Cell.Y},{chest.Cell.Z}" : "gone")}"),
                    text =>
                    {
                        var parts = text.Split(' ');
                        session.SetNeed(Enum.Parse<NeedMeter>(parts[0], ignoreCase: true), double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
                        Console.WriteLine($"need: {text}");
                    },
                    item =>
                    {
                        session.Simulation.GrantItems(item, 1); // a test aid (N07): --give puts one in the bag
                        Console.WriteLine($"give: {item}, bag {session.Simulation.Inventory.Count(item)}");
                    },
                    ground =>
                    {
                        var result = session.PlantOn(ground); // a test aid (N08): --plant x,y,z is a right click on that ground block with the berry held
                        Console.WriteLine($"plant: {result} at {ground.X},{ground.Y},{ground.Z}, berries {session.Simulation.Inventory.Count(ItemId.Berry)}, planted {session.PlantedAlive}");
                    },
                    OpenStationFromScript,
                    ActAtStationFromScript,
                    () =>
                    {
                        _stationPanel.Help(); // --proc-help: H at the station
                        Console.WriteLine(StationLine("proc-help"));
                    },
                    StationSayFromScript);
                _scenario = new ScenarioScript(session, _look, _dialogue, options.Script, name => ScriptItemNames.Resolve(session.Nouns, name), hooks, Console.WriteLine, holdsCheckAnswers: options.Playtest);
            }
        }
        else
        {
            _clock = new RealFrameClock();
        }

        _stepper = new WorldStepper(
            session,
            _look,
            _scenario,
            _live,
            () => _micCheck is { IsActive: true },
            () => _dialogue.IsOpen,
            _craftingPanel,
            options.DebugCrashAfter,
            [
                _arm, // a placement swings the arm once (S15)
                new SoundTickObserver(_sound, session),
                _figures,
                _camera,
                new ObjectiveAnnouncer(session, _toasts, Console.WriteLine),
                new DelegateTickObserver(_ => voice.Update()),
                new WordCardPresenter(session, _cards, _sound, voice),
                new DelegateTickObserver(_ =>
                {
                    foreach (var hint in session.TakeHints())
                    {
                        _toasts.Show(hint);
                    }
                }),
            ]);
        _turns = new TurnPresenter(_pump, _sound, voice, _dialogue, _micCheck, Console.WriteLine);

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = options.WindowWidth ?? StartWidth,
            PreferredBackBufferHeight = options.WindowHeight ?? StartHeight,
            PreferredDepthStencilFormat = DepthFormat.Depth24,
            SynchronizeWithVerticalRetrace = !options.IsScreenshotRun,
        };
        IsFixedTimeStep = false; // the accumulator owns the tick rate
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = _playtest ? $"Vocaterra playtest (seed {options.Seed})" : $"Vocaterra 3D (seed {options.Seed})";
        Window.TextInput += OnTextInput;
        _saves = new SaveService(session, pristine, () => new SaveSettings(_sound.Mixer.Volumes, _canvas.TextPercent, !_needsHud.EffectsOn, session.RelaxedNeeds, session.NeedsRate == SaveSettings.DefaultNeedsRate ? 0 : session.NeedsRate, _underwaterBlur == UnderwaterBlurSettings.Default ? null : _underwaterBlur), Console.WriteLine); // the sound volumes (S24), the text size (X08), the view effects and the relaxed needs (N05) ride in the save's settings
        _checks = new CheckBattery(
            session,
            () => _log,
            MonotonicClock.Milliseconds,
            _sound,
            voice,
            _listen,
            _swallow,
            options.Script,
            options.IsScreenshotRun,
            options.Playtest,
            new CheckBatteryHooks(_shots.Request, () => _shots.Pending, () => _micCheck is { IsActive: true }, index => _scenario?.SkipTo(index), Exit, EndPlaytest),
            Console.WriteLine);
        _micScreen = new MicCheckScreen(_micCheck, session, _listen, _sound, (check, x, y) => MicCheckLayer.LayoutFor(check, _canvas.Width, _canvas.Height, _drawer.View.Fonts.Get(FontProvider.NativeSize), session.Locale).ButtonAt(x, y), options.IsScreenshotRun, () => _scenario is { IsDone: false } || _checks.Screen is { IsDone: false }, Console.WriteLine);
        _playtestFlow = new PlaytestFlow(
            options.Playtest,
            options.IsPlaytestRecall,
            options.IsScreenshotRun,
            options.Script.Count,
            session,
            _checks,
            _saves,
            new PlaytestHooks(() => _scenario?.Index ?? 0, index => _scenario?.SkipTo(index), ClosePanels, DrainForSave),
            Console.WriteLine);
        _runEnd = new ScriptedRunEnd(options, session, _checks, _scenario, _dialogue, _micCheck, _shots, (_bookScreen.Open, _inventoryPanel.Open, _craftingPanel.Open), Exit, Console.WriteLine, (tick, message) => _log?.RunTruncated(tick, message), _settings.Open);
        _screens = new ScreenStack(
            [_micScreen, _checks, _dialogueScreen, _controls, _settings, _bookScreen, _inventoryPanel, _craftingPanel, _stationPanel],
            [_checks, _micScreen, _dialogueScreen, _bookScreen, _inventoryPanel, _craftingPanel, _stationPanel, _controls, _settings]);
        var openBook = new DelegateCommand(_bookScreen.Open); // J and the book item (S20)
        _worldInput = new WorldInput(_sim, _look, _live, openBook, new DelegateCommand(() => _session.EatSelected()), session.Data.Needs, cell => _session.PlantOn(cell));
        var commands = new GameCommands(
            ToggleView: new DelegateCommand(ToggleView),
            Save: new DelegateCommand(SaveNow),
            CycleTextSize: new DelegateCommand(CycleTextSize),
            ToggleViewEffects: new DelegateCommand(ToggleViewEffects),
            OpenSettings: new DelegateCommand(_settings.Open),
            EndPlaytest: new DelegateCommand(() => _playtestFlow.EndKey(WorldRunning)),
            ToggleDebug: new DelegateCommand(() => _dialogue.ShowVerdictNotes = _debug.Visible = !_debug.Visible),
            Listen: new DelegateCommand(() =>
            {
                _dialogueScreen.Open(_aimed!.Name, null);
                _dialogueScreen.BeginListening(GameKey.V);
            }),
            TypeTo: new DelegateCommand(() => _dialogueScreen.Open(_aimed!.Name, 't')),
            TalkTo: new DelegateCommand(() => _dialogueScreen.Open(_aimed!.Name, 'e')),
            OpenControls: new DelegateCommand(_controls.Open),
            NextTip: new DelegateCommand(() => _session.Tips.Dismiss()),
            OpenBook: openBook,
            OpenCrafting: new DelegateCommand(_craftingPanel.Open),
            OpenStation: new DelegateCommand(OpenStationAimed),
            OpenInventory: new DelegateCommand(_inventoryPanel.Open),
            ReleaseMouse: new DelegateCommand(() =>
            {
                _worldInput.ReleaseMouse();
                _ending.Dismiss(); // Esc takes the closing card down (S17)
            }),
            Sleep: new DelegateCommand(() => SleepNow("the player")),
            DrinkByHand: new DelegateCommand(() => _session.DrinkByHand()),
            TakeBelongings: new DelegateCommand(() => _session.TakeBelongings()));
        _globalKeys = KeyBindings.Global(commands, options.Playtest);
        _worldKeys = KeyBindings.World(commands, () => _aimed is not null, () => _session.Tips.Current is not null, AimedAtWorkbench, () => CardOffersSleep, () => FireOffersSleep || FireNotTired, () => _session.AimedAtWater, () => _session.AimedAtChest, AimedAtStation);
        // The session log (S11) is opened first, so the battery's start is in it; the battery's plan is the bundle's items in the seeded order of its kind.
        (_sessionDirectory, _log, _autosave) = SessionStart.Open(options, session, _micCheck is not null, _toasts, _playtestFlow, _saves, OnAppliedTurn, Console.WriteLine);
        if (options.Check is { } battery)
        {
            _checks.Begin(battery, options.IsScreenshotRun, 0);
        }

        _endingFlow = new SessionEndingFlow(_ending, session, voice, _log, Console.WriteLine);
        _greeting = new GuideGreeting(session, npc => _dialogueScreen.Open(npc, null), Console.WriteLine);

        CrashSource = new CrashSource(_sessionDirectory, options.ReportDir, session, _dialogue, _report, _autosave, _saves);
        _session.StartWorker(options.TurnClock, options.NeedsSpeech);
    }

    // Closes the dialogue, the book, the inventory and the crafting panel, whichever is open (the playtest's post-check takes the screen, S12).
    private void ClosePanels()
    {
        if (_dialogue.IsOpen)
        {
            _dialogueScreen.Close();
        }

        if (_book.IsOpen)
        {
            _bookScreen.Close();
        }

        if (_inventory.IsOpen)
        {
            _inventoryPanel.Close();
        }

        if (_crafting.IsOpen)
        {
            _craftingPanel.Close();
        }

        if (_station.IsOpen)
        {
            _stationPanel.Close();
        }
    }

    // The world is on screen: no check battery and no mic check (the playtest's triggers and its end key need that).
    private bool WorldRunning => _checks.Screen is null && _micCheck is not { IsActive: true };

    // Why play is not active this frame (N01, design 9.2): the panels, the report, the check battery, the voice. The world's ticks go on under most of them, so the session is told.
    private PlayPause PlayPausesNow()
    {
        var pauses = PlayPause.None;
        if (_dialogue.IsOpen)
        {
            pauses |= PlayPause.Conversation;
            if (_session.Lessons.Active is not null)
            {
                pauses |= PlayPause.Lesson;
            }
        }

        pauses |= _bookScreen.IsOpen ? PlayPause.Book : PlayPause.None;
        pauses |= _inventoryPanel.IsOpen ? PlayPause.Inventory : PlayPause.None;
        pauses |= _craftingPanel.IsOpen ? PlayPause.Crafting : PlayPause.None;
        pauses |= _stationPanel.IsOpen ? PlayPause.Station : PlayPause.None;
        pauses |= _report.Paused ? PlayPause.Report : PlayPause.None;
        pauses |= _controls.IsOpen || _settings.IsOpen || _micScreen.IsOpen ? PlayPause.Menu : PlayPause.None;
        pauses |= _checks.Screen is not null ? PlayPause.CheckBattery : PlayPause.None;
        pauses |= _listen.Listening ? PlayPause.SpeechRecording : PlayPause.None;
        pauses |= _session.PendingTurns > 0 ? PlayPause.SpeechProcessing : PlayPause.None;
        return pauses;
    }

    // The player is free for the sleep's offer (V12c): the world on screen, no screen, panel or report up, no voice recorded.
    private bool SleepFree => WorldRunning && _screens.Top is null && !_dialogue.IsOpen && !_listen.Listening && !_report.Paused;

    // The player's own lit fire offers the sleep: E sleeps there, after an NPC under the crosshair and the workbench (V12c).
    private bool FireOffersSleep => SleepOffer.AtFire(_session, SleepFree && _aimed is null && !AimedAtWorkbench());

    // L01: at the fire and not tired, E says the player's line «No tengo sueño.» (text only) and sleeps nothing.
    private bool FireNotTired => SleepOffer.AtFireNotTired(_session, SleepFree && _aimed is null && !AimedAtWorkbench());

    // The session's closing card offers the sleep: Enter sleeps while it is up (V12c).
    private bool CardOffersSleep => SleepOffer.OnCard(_session, _ending.Visible(_sim.Tick), SleepFree);

    /// <summary>The crash handler's view of the game: its state and the last-chance save (X05).</summary>
    public ICrashSource CrashSource { get; }

    /// <summary>The process exit code: 0 unless the screenshot could not be saved.</summary>
    public int ExitCode => _runEnd.ExitCode;

    /// <inheritdoc />
    protected override void LoadContent()
    {
        _devices.Start();
        if (_options.ThirdPerson)
        {
            _viewMode.Set(CameraMode.ThirdPerson);
        }

        _drawer.Load(GraphicsDevice, MakeView, LayerStack.Build(_debug, _lesson, _playtest));
        Console.WriteLine($"scene: {_scene.Name}, seed {_scene.Seed}, {_scene.TreeCount} trees, spawn {_scene.Spawn.Feet.X},{_scene.Spawn.Feet.Y},{_scene.Spawn.Feet.Z}, digest {_scene.Grid.ComputeDigestHex()[..16]}");
        _beacon.PlaceAtStart(_scene, _options.Beacon, loaded: _options.LoadPath is not null || _options.SkipToSession is not null); // a skipped game starts where a load would (V12c)
    }

    // The frame's view: what the layers draw from, the parts that stay for the run (the parts of each frame are set in Draw).
    private FrameView MakeView(Texture2D pixel, BlockAtlas atlas) => new()
    {
        Session = _session,
        Dialogue = _dialogue,
        Fonts = new FontProvider(AssetPaths.Font),
        Pixel = pixel,
        Seed = _options.Seed,
        Book = _book,
        Recordings = _recordings,
        Toasts = _toasts,
        Inventory = _inventory,
        Crafting = _crafting,
        Station = _stationPanel,
        Voxel = _sim,
        Camera3D = _camera.Camera,
        Beacon = _beacon.Beacon,
        Figures = _figures.Figures,
        VoxelIcons = atlas,
        Objective = _session.Objective,
        Cards = _cards,
        MicCheck = _micCheck,
        ShowTips = _options.ShowsTips,
        Playtest = _playtest,
        Greyscale = _options.Greyscale,
        Ending = _ending,
    };

    /// <inheritdoc />
    protected override void Update(GameTime gameTime)
    {
        // MonoGame calls Update once before the first Draw, outside the frame loop; skip it so that a frame is one Update plus one Draw.
        if (!_firstUpdateSeen)
        {
            _firstUpdateSeen = true;
            _ = _clock.NextFrameSeconds();
            base.Update(gameTime);
            return;
        }

        var seconds = _clock.NextFrameSeconds();
        _stats.Record(seconds);
        if (_options.IsScreenshotRun)
        {
            _book.FinishTurn(); // a scripted screenshot waits for no leaf
        }
        else
        {
            _book.Advance(seconds); // the page turn runs on the frame clock (S28)
        }

        if (!_options.IsScreenshotRun)
        {
            ReadInput();
        }

        // The report panel pauses the world (S21): no turn is applied and no tick passes while it is open; the turns wait on the worker.
        if (!_report.Paused)
        {
            _turns.Apply();
            _stationPanel.PumpCue(); // the instruction the teacher was asked to play again comes after her own line (W08)
        }

        _log?.Pump(_session); // the replay records and the events of this frame (S11)
        _ = _support.Pump(later => _voice.Say(later.Npc, later.Lines)); // the shopkeeper's question after the key words is spoken first
        _lesson.Pump(); // a lesson step's line goes on the panel (S16)
        _endingFlow.Update(); // the session's ending (S17): the guide's closing line, the card and the summary

        if (!_report.Paused)
        {
            _dialogueScreen.RunWatchdog(seconds); // a paused world does not time a turn out (S21)
        }

        _micScreen.DriveScripted();
        _reportScreen.DriveScripted(); // a scripted report is typed and saved here, while its panel is up (S21)
        _playtestFlow.StartIfDue(atStepBoundary: _scenario is null || _scenario.AtStepBoundary, WorldRunning, _dialogue.IsOpen);
        _playtestFlow.SaveIfDue();

        AutosaveDue(); // the autosave at its safe points (X05)

        _checks.Drive();
        _session.PlayPauses = PlayPausesNow(); // the needs move only in active play (N01)
        _session.SprintHeld = _worldInput.SprintHeld; // sprint, if the needs allow it (N02)
        var steps = _accumulator.Advance(seconds);
        if (_checks.Screen is not null)
        {
            steps = 0; // the world does not run while the check battery is on (its time is not the game's)
        }

        steps = _report.Gate(steps); // no tick passes while the report panel is open (S21)
        for (var i = 0; i < steps && !_report.Paused; i++)
        {
            _stepper.Step(); // a script step may open the report panel: the rest of the ticks wait for it
        }

        PumpTalk(seconds);
        if (!_report.Paused)
        {
            if (_dialogue.PendingSpeech is not null)
            {
                _sound.Scripted.Begin(); // a scripted spoken line stands for a push-to-talk: the start beep, a silent window, the stop beep (S24)
            }

            _pump.Submit();
        }

        DriveCollapse(seconds); // the collapse of an exhausted player (N03)
        DriveNeedsHud(); // the stage lines and their sound (N05)
        _beacon.Sync();
        _greeting.Update(free: _scenario is null && WorldRunning && !_screens.FreesMouse && _session.PendingTurns == 0); // the guide greets the player who comes up to her (S17)

        _aimed = _figures.Aimed(_camera.CurrentEye);
        if (_aimed is not null && !_aimedTipOffered)
        {
            _aimedTipOffered = true;
            _session.Tips.Offer("aimed_npc"); // the first time the crosshair is on someone (S16)
        }

        _events.Print();

        _runEnd.AfterUpdate(); // a scripted run counts its settled frames and opens the panels asked for at its end

        _sound.Mixer.SetUnderwater(_drawer.UnderwaterAmount); // the beds go quieter and duller under water (H10)
        _soundFrame.Update(seconds);

        _beacon.ClearIfReached(_sim);
        if (_options.IsScreenshotRun && _session.Worker is { } scriptedWorker)
        {
            // A scripted run takes each turn on the frame after it is submitted (T9): the worker is waited for here, so whether it finished before the
            // next frame's collect (a matter of milliseconds against a frame of about 30) cannot move the tick the script goes on at.
            _ = scriptedWorker.WaitForOutcomes(ScriptedTurnWaitMilliseconds);
        }

        base.Update(gameTime);
    }

    // A scripted run waits this long for the worker at the end of a frame at most (a stuck worker must not hang a run); a turn never takes this long.
    private const int ScriptedTurnWaitMilliseconds = 60_000;

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        var view = _drawer.View;
        var viewport = GraphicsDevice.PresentationParameters;
        var width = viewport.BackBufferWidth;
        var height = viewport.BackBufferHeight;
        var (canvasWidth, canvasHeight) = _canvas.For(width, height); // the 2D layers are laid out on this canvas and magnified to the window (X08)
        view.ViewportWidth = canvasWidth;
        view.ViewportHeight = canvasHeight;
        var drawStart = MonotonicClock.Milliseconds();

        var alpha = (float)_accumulator.Alpha;
        var farPlane = VoxelRenderer.FarPlaneFor(_drawer.Preset.ViewDistance);
        var eye = _camera.Place(alpha, width, height, farPlane, canvasWidth, canvasHeight);
        var shoulder = _camera.Shoulder;

        view.Eye = eye;
        view.Look = (_look.YawSteps, _look.PitchSteps);
        view.Fps = _stats.Fps;
        view.Aimed = _aimed;
        view.Check = _checks.Screen;
        view.SettingsOpen = _settings.IsOpen;
        view.SettingsSelected = _settings.Selected;
        view.SettingsRate = _session.NeedsRate;
        view.SettingsEffectsOn = _needsHud.EffectsOn;
        view.SettingsRelaxed = _session.RelaxedNeeds;
        view.SettingsBlur = _underwaterBlur;
        view.ControlsOpen = _controls.IsOpen;
        view.ControlsScroll = _controls.Scroll;
        view.Report = _report;
        view.Loading = _session.Worker!.IsReady ? null : LoadingLayer.Text(_session.Locale);
        _dialogue.SpeechUnavailable = _session.Worker.IsReady && !_session.Worker.CanHear;
        view.LoadingFrame = _loadingFrames++;
        var mouse = Mouse.GetState();
        view.Mouse = !_options.IsScreenshotRun && GraphicsDevice.Viewport.Bounds.Contains(mouse.Position) ? _canvas.ToCanvas(mouse.Position) : null;

        view.DebugOverlayBottom = _debug.PanelFor(view)?.Bottom ?? 0;
        view.LessonStripBox = LessonLayer.LayoutFor(view, _lesson) is { } lessonStrip ? lessonStrip.Box : null; // the cards keep clear of it (S22)
        var (headingStep, headingOffer) = _lesson.State();
        view.LessonHeading = headingStep is not null ? LessonStripLayout.HeadingParts(view.Session.Locale, headingStep) : headingOffer is not null ? LessonStripLayout.HeadingParts(view.Session.Locale, headingOffer) : null; // the panel's header (U02)
        view.SleepPrompt = FireOffersSleep && !_ending.Visible(_sim.Tick) ? SleepOffer.PromptText(_session) : null; // the fire's prompt; the card says it while it is up (V12c)
        view.EndingSleep = CardOffersSleep ? SleepOffer.CardText(_session) : null;
        view.FadeAlpha = (float)_collapseFade;
        view.Needs = _needsHud;
        view.DeathScreen = _deathHold > 0 ? new DeathScreenView(_session.Data.Words.HudLine("hud.camp") ?? string.Empty, _session.Locale.Text("hud.camp"), _session.DeathLine ?? string.Empty) : null;
        view.Breath = BreathReading.Of(_sim); // the bubble meter (H04)

        _drawer.DrawWorld(GraphicsDevice, _camera.Camera, _sim.Target?.Block, _beacon.Beacon.Target, _sim.Tick, shoulder ? _figures.Avatar() : null, shoulder ? null : _arm.Frame(eye, _drawer.Renderer.Lights));
        _drawer.DrawLayers(width, height, canvasWidth, canvasHeight);
        _reportScreen.TakePictureIfRequested(); // F8's frame: the picture as the player saw it, before the report panel is drawn (the panel opens after this frame)

        var drawEnd = MonotonicClock.Milliseconds();
        _timings.Record(drawStart, drawEnd, _sim.Tick);
        base.Draw(gameTime);

        _runEnd.AfterDraw(
            GraphicsDevice,
            width,
            height,
            new WindowSizes(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight, Window.ClientBounds.Width, Window.ClientBounds.Height),
            _timings,
            _drawer.Renderer,
            _drawer.Preset);
    }

    /// <inheritdoc />
    protected override void UnloadContent()
    {
        Window.TextInput -= OnTextInput;
        foreach (var line in _support.Summary())
        {
            Console.WriteLine(line);
        }

        foreach (var line in LearnerSummary.Lines(_session.Learner, _session.Lessons.Record, _session.Day, _session.StoredTime.UnixSeconds))
        {
            Console.WriteLine(line); // the learner's book at exit (S19): the nouns and verbs used, the lessons and their states
        }

        if (_options.SavePath is { } savePath)
        {
            DrainForSave();
            _ = _saves.Write(savePath, "at the end of the run");
        }

        _playtestFlow.SaveAtQuit();

        if (_log is { } log)
        {
            log.End(_session, _sim.Tick); // the session's last line: the objective, the phrases and the time (S11)
            log.Dispose();
        }

        _session.StopWorker(); // the worker disposes the recognizer on its own thread
        _sound.Mixer.WriteSummary();
        _soundLog?.Dispose();
        _soundOutput.Dispose();
        _drawer.Dispose();
        base.UnloadContent();
    }

    // The crosshair is on a crafting station: the workbench (the mesa), or a lit fire (N07: a stone of a burning ring, or the ground inside it). E opens the crafting panel there, as C does
    // anywhere, and comes before the sleep at the same fire (an unlit ring does nothing, as before).
    private bool AimedAtWorkbench() =>
        _sim.Target is { } hit && (hit.Id == BlockId.Table
            || (hit.Id == BlockId.FireRing && _session.Fires.StoneBurns(hit.Block))
            || (_session.Fires.Middles.Contains(hit.Adjacent) && _session.Fires.IsLit(hit.Adjacent)));

    // The crosshair is on a cell of a station's prop within reach (the oven), and the station has a procedure to do: E opens the station, after an NPC and before the workbench (W07, design 10.8).
    private bool AimedAtStation() => _sim.Target is { } hit && StationProps.At(_session.Scene, _session.Data.Stations, hit.Block) is { } station && _session.Data.Procedures.AtStation(station.Id).Count > 0;

    private void OpenStationAimed()
    {
        if (_sim.Target is { } hit && StationProps.At(_session.Scene, _session.Data.Stations, hit.Block) is { } station)
        {
            _ = _stationPanel.Open(station.Id, teacherPresent: true); // the teacher listens from the station (design 10.8, W08)
        }
    }

    // The accepted offer to bake (W08, start_procedure): the dialogue closes (the teacher's line is spoken after this, by the turn's presenter) and the station opens, with the teacher.
    private void OpenStationFromOffer(string stationId, string procedureId)
    {
        if (_dialogue.IsOpen)
        {
            _dialogueScreen.Close();
        }

        Console.WriteLine(_stationPanel.Open(stationId, teacherPresent: true) ? StationLine($"offer accepted, station {stationId} ({procedureId})") : $"offer accepted: station {stationId} did not open");
    }

    // --station-say <line>: a typed line to the teacher from the open station (W08).
    private bool StationSayFromScript(string line)
    {
        var sent = _station.IsOpen && _stationPanel.Talk is { } talk && talk.Say(line);
        Console.WriteLine($"station-say \"{line}\": {(sent ? "sent to the teacher" : "not sent")}");
        return sent;
    }

    // What the canvas's point is on the station overlay; the layout is the one the layer draws.
    private StationHit? StationHitAt(int x, int y)
    {
        var view = _stationPanel.View();
        return new StationLayout(_canvas.Width, _canvas.Height, view.Fixtures.Count, view.Targets.Count, view.Strip.Dots.Count, view.Verbs?.Verbs.Count ?? 0).HitAt(x, y, view.Held.Count, view.PageCount);
    }

    // --station <id>: opens the station and starts its first procedure, as E at the oven does (the teacher listens, W08).
    private bool OpenStationFromScript(string id)
    {
        var opened = _stationPanel.Open(id, teacherPresent: true);
        Console.WriteLine(opened ? StationLine("station " + id) : $"station {id}: not opened");
        return opened;
    }

    // --act use,on[,verb]: an act at the open station.
    private void ActAtStationFromScript(string value)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (!_station.IsOpen || parts.Length < 2)
        {
            Console.WriteLine($"act {value}: no station is open, or the act is not use,on[,verb]");
            return;
        }

        _stationPanel.Act(parts[0], parts[1], parts.Length > 2 ? parts[2] : string.Empty);
        Console.WriteLine(StationLine("act " + value));
    }

    // One line for the run's output: what the station shows after a scripted step.
    private string StationLine(string what)
    {
        var view = _stationPanel.View();
        return $"{what}: {view.State}, notice {view.Notice}, step {view.Strip.Dots.Count(static d => d.State == StripDotState.Done)}/{view.Strip.Dots.Count}, rung {view.Strip.Rung}, row {view.Strip.Row ?? "-"}, text {(view.Strip.ShowsText ? "shown" : "hidden")}, english {(view.Strip.ShowsEnglish ? "shown" : "hidden")}, highlight {string.Join('+', view.Held.Concat(view.Targets).Append(view.Hand).Where(static c => c.Highlight).Select(static c => c.Noun)) switch { "" => "none", var h => h }}";
    }

    // Keys and mouse, once per frame, for the interactive run. The device state becomes this frame's commands (LiveVoxelInput) and the look.
    private void ReadInput()
    {
        _swallow.Tick();
        var input = _devices.Read(); // the pointer is on the canvas the layers are laid out on (X08)
        var mouse = input.MouseState;

        // The report panel (S21) takes every key and click while it is open; nothing else in the game reads the input then. F8 opens it from anywhere.
        if (_report.IsOpen)
        {
            if (IsActive)
            {
                _reportScreen.HandleInput(input);
            }

            EndInput(input);
            return;
        }

        if (IsActive && input.Pressed(Keys.F8))
        {
            RequestReport();
            EndInput(input);
            return;
        }

        if (IsActive)
        {
            if (_playtestFlow.ShowsThankYou && input.Pressed(Keys.Escape))
            {
                Exit(); // the playtest's thank-you screen: Esc closes the game (Esc always works)
                _devices.CommitKeys(input);
                return;
            }

            _globalKeys.RunEvery(input); // F10, F5, F6, F9 and F3, wherever the player is
            if (!_screens.HandleInput(input))
            {
                _ = _worldKeys.RunFirst(input); // no screen is open: the keys are the world's
            }
        }

        var uiOpen = _screens.FreesMouse;
        if (IsActive && _book.IsOpen)
        {
            _bookScreen.HandleMouse(input); // the wheel scrolls the book, a click turns to a tab or presses a row's button (S20)
        }

        if (IsActive && _crafting.IsOpen && input.LeftClicked)
        {
            _craftingPanel.Click(input); // a row asks for one craft in the next tick
        }

        if (IsActive && _station.IsOpen && input.LeftClicked)
        {
            _stationPanel.Click(input); // a cell, a target, a button or a verb of the station
        }

        if (IsActive && input.LeftClicked)
        {
            ClickOnScreens(mouse.X, mouse.Y);
        }

        IsMouseVisible = !_worldInput.Read(input, IsActive, uiOpen, _book.IsOpen, Window); // the pointer is hidden while the mouse is captured
        _devices.Commit(input);
    }

    // F5: the behind-the-shoulder view and back (S15).
    private void ToggleView()
    {
        var mode = _viewMode.Toggle();
        Console.WriteLine($"view: {(mode == CameraMode.ThirdPerson ? "behind the shoulder" : "first person")}");
    }

    // F9: the interface text size, 100, 125 and 150 percent (X08); no other F9 binding, and a text box never reads it.
    private void CycleTextSize()
    {
        _ = _canvas.Next();
        Console.WriteLine($"text scale: {_canvas.TextPercent} percent (canvas {_canvas.Width} x {_canvas.Height})");
    }

    // ---- The bug and feature report (S21, D-game-17): local files only, nothing is sent. ----

    // F8: the game pauses. A recording in progress is dropped (the microphone is off), and the next frame is kept as the picture the player saw.
    private void RequestReport()
    {
        _dialogueScreen.CancelListening();
        if (_checks.Screen is { Listening: true })
        {
            _checks.CancelListening();
        }

        _micScreen.InterruptForReport(); // the same as a clip too short to hear: the check asks for another try, and nothing is sent

        _reportScreen.Request(OpenScreenName());
    }

    // What is open now, in the words the report's table uses.
    private string OpenScreenName() => _screens.OpenScreenName();

    // The input of a frame the panel or F8 took: the mouse is free, and the states are kept for the next frame's presses and clicks.
    private void EndInput(InputSnapshot input)
    {
        _worldInput.FreeMouse(Window);
        IsMouseVisible = true;
        _devices.Commit(input);
    }

    // The mouse went down at a point: a button of the mic check, or a button or choice of the panel.
    private void ClickOnScreens(int x, int y)
    {
        if (_micScreen.Active)
        {
            _micScreen.Click(x, y);
        }
        else if (_dialogue.IsOpen)
        {
            _dialogueScreen.Click(x, y);
        }
    }

    // The window's text event: the letters of the panel's text box. The key that opened the panel (T or E) is typed by the platform too, once.
    private void OnTextInput(object? sender, TextInputEventArgs args)
    {
        if (_report.IsOpen)
        {
            _reportScreen.Type(args.Character, InputDevices.AltHeld()); // the report's text box (S21): the control characters (Enter, Tab, Ctrl+Enter) are keys
            return;
        }

        if (_checks.TakesText)
        {
            _checks.Type(args.Character, InputDevices.AltHeld()); // the check's text box (S11)
            return;
        }

        if (_station.IsOpen && _stationPanel.Talk is { IsTyping: true } talk)
        {
            talk.Type(args.Character, InputDevices.AltHeld()); // the station's text box (W08)
            return;
        }

        _dialogueScreen.Type(args.Character, InputDevices.AltHeld());
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _voice.Dispose();
        }

        base.Dispose(disposing);
    }

    // The tile for "¿qué es esto?": the block under the crosshair (the scripted run has the same crosshair).
    private GuideCell? CursorCell() => _session.AimedCell;

    // Once per frame: the recording collects its samples and ends at its length cap (ListenSession); the mic check's meter falls back while
    // nothing is recorded.
    private void PumpTalk(double seconds)
    {
        if (_checks.Screen is not { Listening: true })
        {
            _micScreen.FeedWhileIdle(seconds);
        }

        _listen.Pump(seconds);
    }

    // A finished turn of the session: a check answer grades the battery's item (CheckBattery); any other turn goes to the session log.
    private void OnAppliedTurn(AppliedTurn applied)
    {
        if (!_checks.Graded(applied))
        {
            _log?.Turn(applied, _session);
        }
    }

    // The battery's hook for the playtest's end (S12): the playtest is made after the battery, so the hook reaches it through this method.
    private void EndPlaytest(CheckScreen check) => _playtestFlow.Ended(check);

    // The save file of F6 (S10): the --save file, in a playtest the session folder's playtest.save (S18), or vocaterra.save.json in the working folder.
    // A turn still thinking means "not now".
    private void SaveNow()
    {
        var path = PlaytestEnd.ManualSavePath(_options.SavePath, _playtestFlow.ManualSaveFile, DefaultSavePath);
        if (_saves.Write(path, "on F6"))
        {
            _toasts.Show(_session.Locale.Ui("gameSaved", ("session", _session.Day.ToString(CultureInfo.InvariantCulture))));
        }
        else
        {
            _toasts.Show(_session.Locale.Ui("saveLater"));
        }
    }

    // The autosave (X05) at its safe points: only at rest, with no dialogue open, no voice being recorded, no check battery, mic check or report panel.
    private void AutosaveDue() =>
        _autosave?.Run(_sim.Tick, _session.Events, atRest: _session.CanSave && !_dialogue.IsOpen && !_listen.Listening && WorldRunning && !_report.Paused);

    // The sleep at the player's own fire (V12c), from E there, Enter on the closing card or the script's --sleep: the session's own save is restored into it as the next
    // play session (SaveGame.Sleep: the state a load of that save gives), so the session log ends the old session and starts the next as a quit and a load into the same
    // folder write them, the autosave writes the ending session (a load of it starts the session the sleep starts) and starts again as a load starts it, and the
    // front-end's own state goes back to a new run's: the closing card, the word cards not yet shown, the toasts waiting, the beacon of the start.
    private void SleepNow(string how)
    {
        if (!_session.CanSleep || _dialogue.IsOpen)
        {
            Console.WriteLine($"sleep: not now ({how}): {SleepRefusal()}");
            return;
        }

        // L01: the player sleeps when the sleep drive is below 75 (day 1: at its ending); the scripts' aids (--sleep, the scripted sleep step) sleep regardless of the drive.
        if (!_session.SleepWanted && how == "the player")
        {
            var line = SleepOffer.NotSleepyText(_session);
            _toasts.Show(line);
            Console.WriteLine($"sleep: not tired: {line}");
            return;
        }

        PassNight(how, collapse: false);
    }

    // The collapse (N03, design 9.5): 60 s of active play at the sleep drive's last stage, and the screen fades out (CollapseFadeSeconds), the player sleeps where they stand
    // (the night of SaveGame.Sleep, with the collapse's values) and the screen fades back in on the morning.
    private const double CollapseFadeSeconds = 1.5;
    private double _collapseFade; // 0 clear, 1 black
    private bool _collapsing;

    private int _deathsSeen;

    // The death screen (N05, design 9.7): the black holds for this long with «Estás en tu campamento.», its English and the cause line, then the world fades back in.
    private const double DeathHoldSeconds = 4;
    private double _deathHold;
    private readonly NeedsHud _needsHud;
    private UnderwaterBlur _underwaterBlur;

    // The stage lines of the needs (N05): a worse stage shows its line above the hotbar once and plays its soft sound once.
    private void DriveNeedsHud()
    {
        _needsHud.Update(_sim.Tick);
        for (var sounds = _needsHud.TakeSounds(); sounds > 0; sounds--)
        {
            _ = _sound.Mixer.Play(SoundCue.NeedStage);
        }
    }

    // F7 (N05): the needs' effects on the view on or off; the icons and the lines stay. Kept in the save's settings.
    private void ToggleViewEffects()
    {
        _needsHud.EffectsOn = !_needsHud.EffectsOn;
        _toasts.Show(_session.Locale.Ui(_needsHud.EffectsOn ? "effectsOn" : "effectsOff"));
        Console.WriteLine($"view effects: {(_needsHud.EffectsOn ? "on" : "off")}");
    }

    private void DriveCollapse(double seconds)
    {
        if (_session.DeathCount != _deathsSeen)
        {
            _deathsSeen = _session.DeathCount;
            _collapseFade = 1; // a death (N04): the screen is black where the player fell and fades in at their fire
            _deathHold = DeathHoldSeconds; // N05: the black holds the short screen «Estás en tu campamento.» for a few seconds before the fade-in
            var feet = _sim.Player.FeetBlock;
            Console.WriteLine($"death: {_deathsSeen}, woke at {feet.X},{feet.Y},{feet.Z} (health {_session.Needs.Value(NeedMeter.Health):0.#}, hunger {_session.Needs.Value(NeedMeter.Hunger):0.#}, thirst {_session.Needs.Value(NeedMeter.Thirst):0.#}, sleep {_session.Needs.Value(NeedMeter.Sleep):0.#}, stamina {_session.Needs.Value(NeedMeter.Stamina):0.#}), chest {(_session.Chest is { } chest ? $"{chest.Total} items at {chest.Cell.X},{chest.Cell.Y},{chest.Cell.Z}" : "none")}");
        }

        if (_collapsing)
        {
            _collapseFade = Math.Min(1, _collapseFade + (seconds / CollapseFadeSeconds));
            if (_collapseFade >= 1 && _session.CanSave && !_dialogue.IsOpen)
            {
                _collapsing = false;
                PassNight("collapse", collapse: true);
            }

            return;
        }

        if (_deathHold > 0)
        {
            _deathHold = Math.Max(0, _deathHold - seconds); // the screen stays black while the death screen is up
        }
        else if (_collapseFade > 0)
        {
            _collapseFade = Math.Max(0, _collapseFade - (seconds / (CollapseFadeSeconds * 2)));
        }

        if (_session.SleepOffered && _session.CollapseDue && SleepFree)
        {
            _collapsing = true;
        }
    }

    private void PassNight(string how, bool collapse)
    {
        var before = _session.Day;
        _autosave?.WriteNow(_sim.Tick, _session.Events.Count, "sleep");
        var document = _saves.Capture();
        _log.End(_session, _sim.Tick); // the session that ends
        SaveGame.Sleep(_session, document, collapse);
        SessionStart.Begin(_log, _options, _session, micCheck: false, check: false); // the session that begins
        _autosave?.Restart(_sim.Tick, _session.Events.Count);
        _ending.Dismiss();
        _cards.Clear();
        _beacon.Restart();
        _toasts.Clear(); // the old session's toasts are not the new one's: it opens with the morning
        _toasts.Show(SleepOffer.MorningText(_session));
        Console.WriteLine($"sleep: {how}: session {before} ends {(collapse ? "where the player fell" : "at the fire")}, session {_session.Day} begins (tick {_sim.Tick}, stored time {_session.StoredTime.UnixSeconds}, world digest {document.StateDigest[..16]})");
    }

    // Why the player may not sleep now, for the console.
    private string SleepRefusal() =>
        !_session.SleepOffered ? "a playtest plays one session per run"
        : !_session.FireLit ? "the player's own fire is not lit"
        : _dialogue.IsOpen ? "a dialogue is open"
        : "the game is not at rest";

    // At the quit, the turns still on the worker are applied first (up to five seconds), so that the save holds what the player said.
    private void DrainForSave()
    {
        var until = MonotonicClock.Milliseconds() + 5000;
        while (_session.PendingTurns > 0 && MonotonicClock.Milliseconds() < until)
        {
            _turns.Apply();
            System.Threading.Thread.Sleep(10);
        }

        _turns.Apply();
        if (_session.KeyWordsPending)
        {
            _session.FinishKeyWords();
        }

        _ = _session.TakeOpening(); // an opening that waits is only what the panel says: the save holds the NPCs' states, not the lines
    }

}
