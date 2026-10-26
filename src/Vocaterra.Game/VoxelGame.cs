using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Utilities;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Flow;
using Vocaterra.Game.Frame;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Input;
using Vocaterra.Game.Persistence;
using Vocaterra.Game.Reporting;
using Vocaterra.Game.Screens;
using Vocaterra.Game.Sound;
using Vocaterra.Game.UI;
using Vocaterra.Game.UI.Book;
using Vocaterra.Game.UI.Dialogue;
using Vocaterra.Game.UI.English;
using Vocaterra.Game.Utilities;

namespace Vocaterra.Game;

using SoundEventMapper = SoundEventMapper;

/// <summary>
/// The engine host of the 3D first-person scene, the prototype's only world (S2 to S4). Each frame it turns the real clock into whole ticks, reads the
/// keyboard and the mouse (or the scripted run) into <see cref="VoxelCommand"/>s and steps the <see cref="VoxelSession"/>'s simulation; then it
/// places the <see cref="FirstPersonCamera"/> at the interpolated eye, draws the voxel world with <see cref="VoxelRenderer"/> and the layers
/// over it. All rules live in <c>Vocaterra.Simulation</c>, <c>Vocaterra.Game.Core</c> and the plain classes under <c>Frontend/</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What works in 3D.</b> Walking (WASD or the arrows, Space to jump), mouse look in relative mode (Esc lets the mouse go, a click takes it
/// again), gathering by holding the left button on a trunk or a berry bush, placing with the right button, the hotbar, the inventory (I, or E
/// when the crosshair is not on an NPC), the journal (J), the debug overlay (F3), toasts, the NPC figures with their names, the beacon column
/// and its edge arrow, and the NPCs: aim the crosshair at the guide, the shopkeeper or the villager and press E (or T to type, V to talk) to open
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
    private const int SettleTicks = 30;
    private const int RunawayTicks = 60 * 60 * 5;

    /// <summary>The save file F6 writes when the run has no <c>--save</c> file.</summary>
    private const string DefaultSavePath = "vocaterra.save.json";

    private static readonly Keys[] SlotKeys = [Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0];

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
    private readonly MouseLook _mouseLook = new();
    private readonly LiveVoxelInput _live = new();
    private readonly ScenarioScript? _scenario;
    private readonly InputFocus _focus = new();
    private readonly DialogueModel _dialogue;
    private readonly TurnPump _pump;
    private readonly ListenSession _listen;
    private readonly SoundFrontend _sound;
    private readonly ISoundOutput _soundOutput;
    private readonly SoundLogWriter? _soundLog;
    private readonly SpeechActionRunner _actions;
    private readonly SupportFrontend _support;
    private readonly SessionEnding _ending = new();
    private bool _greetArmed = true;
    private readonly LessonFrontend _lesson;
    private readonly ControlsHelp _controls;
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
    private readonly List<IFrameLayer> _layers = [];
    private readonly VoxelDebugOverlay _debug = new();
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private BlockAtlas? _blockAtlas;
    private VoxelRenderer? _renderer;
    private QualityPreset _preset = QualityPreset.For(QualityLevel.High);
    private GreyscaleWorld? _colours;
    private FrameView? _view;
    private bool _captureWanted = true;
    private bool _firstUpdateSeen;
    private int _settled;
    private bool _shown;
    private NpcFigure? _aimed;
    private readonly WordCardQueue _cards = new();
    private readonly CraftingScreen _crafting = new();
    private readonly InventoryPanel _inventoryPanel;
    private readonly CraftingPanel _craftingPanel;
    private int _loadingFrames;
    private string? _shotPath;
    private readonly CheckBattery _checks;
    private SessionLog? _log;

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
    private readonly IGameCommand _openBook;
    private int _sizeFrames;
    private bool _sizeWarned;

    // The autosave (X05, R04): the policy decides the frames; the file is the playtest's playtest.save, else autosave.save in the session folder.
    // A run with --no-autosave has neither.
    private AutosaveController? _autosave;

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
    public VoxelGame(LaunchOptions options, VoxelSession session, Scene scene, NpcVoice voice, Func<Scene> pristine, SoundVolumes? savedVolumes = null, SceneLayout? layout = null, int? savedTextScale = null)
    {
        _options = options;
        _canvas = new UiCanvas(SaveSettings.SnapTextScale(options.TextScale ?? savedTextScale ?? 100), () => (GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
        _devices = new InputDevices(_canvas);
        _session = session;
        _scene = scene;
        _voice = voice;
        _sim = session.Simulation;
        (_sound, _soundOutput, _soundLog) = CreateSound(options, session, savedVolumes, layout);
        _isSolid = (x, y, z) => _sim.Grid.IsSolid(x, y, z);
        // The book's speaker buttons (S20b): a row plays the recording the speech folder has for it (the files are read once, here).
        _recordings = new BookRecordings(SpeechLines.Enumerate(session.Bundle, session.Nouns, session.Phrases).Select(static l => l.LineId).Where(voice.HasRecording), session.Phrases);
        _bookControls = new BookSpeechControls(_book, new BookSpeaker(voice, _sound.Mixer), session, _recordings);
        _dialogue = new DialogueModel(_focus, session.OpeningLine, session.Phrases, session.English, showVerdictNotes: !options.Playtest);
        var microphone = options.UsesMicrophone ? MicrophoneCapture.TryOpen() : null;
        _listen = new ListenSession(new PushToTalkSpeechCapture(microphone) { Sounds = _sound.Mixer });
        _microphoneFound = microphone is not null;
        _pump = new TurnPump(session, _dialogue, CursorCell);
        _actions = new SpeechActionRunner(session, _dialogue, voice, CursorCell);
        _support = new SupportFrontend(session, _dialogue, _actions, Console.WriteLine);
        _events = new EventPrinter(session.Events, _support.LinesFor, Console.WriteLine);
        _beacon = new BeaconDirector(session, Console.WriteLine, Console.Error.WriteLine);
        _lesson = new LessonFrontend(session, _dialogue, Console.WriteLine);
        _ending.MarkSeen(session); // a game loaded after its ending does not play it again
        _micCheck = options.ShowsMicCheck ? new MicCheckModel(session.LogMicCheck) : null;
        _playtest = options.Playtest;
        _debug.Visible = !_playtest; // the playtest has no debug overlay (F3 is off there too)
        _look.Set(scene.Spawn.Yaw, scene.Spawn.Pitch);
        _camera = new PlayerCamera(_sim, _look, _viewMode, _isSolid);
        _figures = new FigureDirector(scene, _sim, _look, _dialogue);
        _arm = new ArmPresenter(_sim, _isSolid, options.Dusk);
        _reportScreen = new ReportScreen(
            _report,
            session,
            options.ReportKind,
            options.ReportDir,
            () => _sessionDirectory,
            open => ReportStateBuilder.Build(session, (_look.YawSteps, _look.PitchSteps), open, _preset.Name, options.Greyscale, options.NoSpeech, _microphoneFound, options.UsesMicrophone),
            () => GraphicsDevice,
            _canvas,
            () => _view!.Fonts.Get(FontProvider.NativeSize),
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
            _canvas,
            () => _view!.Fonts.Get(FontProvider.NativeSize),
            () => _view!,
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
            _canvas,
            () =>
            {
                if (_dialogue.IsOpen)
                {
                    _dialogueScreen.Close();
                }
            },
            Console.WriteLine);
        _lesson.OnRecapDone = _bookScreen.ShowPhrases; // the lesson's recap ends on the journal's phrases page (S17)
        _controls = new ControlsHelp(() => ControlsLayer.MaxScrollFor(_view!));
        _inventoryPanel = new InventoryPanel(_inventory, _focus, session, () => _book.IsOpen);
        _craftingPanel = new CraftingPanel(_crafting, session, _sound, _canvas, () => _book.IsOpen || _inventory.IsOpen || _dialogue.IsOpen);
        if (options.IsScreenshotRun)
        {
            _clock = new FixedFrameClock(VoxelSimulation.TicksPerSecond);
            if (options.Script.Count > 0)
            {
                var hooks = new ScenarioHooks(
                    npc => _dialogueScreen.Open(npc, null),
                    _dialogueScreen.Close,
                    path => _shotPath = path,
                    () => _shotPath is not null,
                    _dialogueScreen.RunLessonStep,
                    (text, keep) => _reportScreen.RequestScripted(text, keep, OpenScreenName()),
                    () => _report.Paused,
                    _bookScreen.PlayTarget,
                    _bookScreen.PractiseFromScript);
                _scenario = new ScenarioScript(session, _look, _dialogue, options.Script, ResolveItem, hooks, Console.WriteLine, holdsCheckAnswers: options.Playtest);
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
        _saves = new SaveService(session, pristine, () => new SaveSettings(_sound.Mixer.Volumes, _canvas.TextPercent), Console.WriteLine); // the sound volumes (S24) and the text size (X08) ride in the save's settings
        _checks = new CheckBattery(
            session,
            () => _log,
            _sound,
            voice,
            _listen,
            _swallow,
            options.Script,
            options.IsScreenshotRun,
            options.Playtest,
            new CheckBatteryHooks(path => _shotPath = path, () => _shotPath is not null, () => _micCheck is { IsActive: true }, index => _scenario?.SkipTo(index), Exit, EndPlaytest),
            Console.WriteLine);
        _micScreen = new MicCheckScreen(_micCheck, session, _listen, _sound, _canvas, () => _view!.Fonts.Get(FontProvider.NativeSize), options.IsScreenshotRun, () => _scenario is { IsDone: false } || _checks.Screen is { IsDone: false }, Console.WriteLine);
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
        _screens = new ScreenStack(
            [_micScreen, _checks, _dialogueScreen, _controls, _bookScreen, _inventoryPanel, _craftingPanel],
            [_checks, _micScreen, _dialogueScreen, _bookScreen, _inventoryPanel, _craftingPanel, _controls]);
        var commands = new GameCommands(
            ToggleView: new DelegateCommand(ToggleView),
            Save: new DelegateCommand(SaveNow),
            CycleTextSize: new DelegateCommand(CycleTextSize),
            EndPlaytest: new DelegateCommand(() => _playtestFlow.EndKey(WorldRunning)),
            ToggleDebug: new DelegateCommand(() => _debug.Visible = !_debug.Visible),
            Listen: new DelegateCommand(() =>
            {
                _dialogueScreen.Open(_aimed!.Name, null);
                _dialogueScreen.BeginListening(Keys.V);
            }),
            TypeTo: new DelegateCommand(() => _dialogueScreen.Open(_aimed!.Name, 't')),
            TalkTo: new DelegateCommand(() => _dialogueScreen.Open(_aimed!.Name, 'e')),
            OpenControls: new DelegateCommand(_controls.Open),
            NextTip: new DelegateCommand(() => _session.Tips.Dismiss()),
            OpenBook: new DelegateCommand(_bookScreen.Open),
            OpenCrafting: new DelegateCommand(_craftingPanel.Open),
            OpenInventory: new DelegateCommand(_inventoryPanel.Open),
            ReleaseMouse: new DelegateCommand(() =>
            {
                _captureWanted = false;
                _ending.Dismiss(); // Esc takes the closing card down (S17)
            }));
        _openBook = commands.OpenBook;
        _globalKeys = KeyBindings.Global(commands, options.Playtest);
        _worldKeys = KeyBindings.World(commands, () => _aimed is not null, () => _session.Tips.Current is not null, AimedAtWorkbench);
        StartLogAndCheck(options);
        CrashSource = new CrashSource(_sessionDirectory, options.ReportDir, session, _dialogue, _report, _autosave, _saves);
        _session.StartWorker(MonotonicClock.Milliseconds, options.NeedsSpeech);
    }

    // The sound side (S24): scripted runs and --no-sounds play nothing aloud (the null output, no audio device needed); the sound log still records the cues of a
    // scripted run. The volumes are the save's, then the command line's. The presentation generator is seeded from the world seed and is the sound's alone.
    private static (SoundFrontend Sound, ISoundOutput Output, SoundLogWriter? Log) CreateSound(LaunchOptions options, VoxelSession session, SoundVolumes? saved, SceneLayout? layout)
    {
        var volumes = SoundSetup.VolumesFor(options, saved);
        ISoundOutput output = SoundSetup.UsesNullOutput(options) ? new NullSoundOutput() : new SoundEffectOutput(AssetPaths.Audio, Console.WriteLine);
        var log = options.SoundLogPath is { } path ? SoundLogWriter.Open(path) : null;
        var mixer = new SoundMixer(output, volumes, new PresentationRng(session.WorldSeed), log, options.NoSounds);
        if (log is not null)
        {
            var clamped = mixer.Volumes;
            log.Write($"# vocaterra sound log, format 1: tick, category, cue, take, gain, pitch (marks: voice and recording windows); seed {session.WorldSeed}");
            log.Write(string.Create(CultureInfo.InvariantCulture, $"# volumes: voice {clamped.Voice:0.00} sfx {clamped.Sfx:0.00} ui {clamped.Ui:0.00} ambient {clamped.Ambient:0.00}; sounds {(options.NoSounds ? "off (--no-sounds)" : "on")}; output {(output is NullSoundOutput ? "null (silent)" : "device")}"));
        }

        // The ambient beds (S25) listen to the scene's places; a run without the layout (a test) has none of them, only the height and the day.
        var places = layout is null ? new AmbientPlaces(null, null, null, [], null, [], []) : AmbientPlaces.From(layout);
        var ambient = new AmbientSound(mixer, places, options.Dusk);
        var sound = new SoundFrontend(mixer, new SoundEventMapper(session.Lessons.Book), ambient);
        sound.SkipEvents(session.Events.Count);
        return (sound, output, log);
    }

    // Once per frame (S24): the voice's state ducks the other sounds, the game events, the panels and the frame's cues are played, and the duck and the beds move on.
    private void UpdateSound(double seconds)
    {
        var mixer = _sound.Mixer;
        mixer.SetTick(_sim.Tick);
        mixer.SetVoice(_voice.Busy);
        _voice.SetVolume(mixer.Gain(SoundCategory.Voice));
        _sound.ObserveGameEvents(_session.Events);
        _sound.ObservePanels(new PanelState(_inventory.IsOpen, _crafting.IsOpen, _book.IsOpen, (int)_book.Page, _dialogue.IsOpen));
        _sound.Flush(_sim.Tick);
        mixer.Advance(seconds);
    }

    // The session log (S11) and the check battery (S11): the log is opened first, so the battery's start is in it; the scripted answers of the
    // battery are read here too. The battery's plan is the bundle's items in the seeded order of the check kind.
    private void StartLogAndCheck(LaunchOptions options)
    {
        var directory = options.SessionDir ?? SessionLog.DefaultDirectory(WallClock.LocalNow, _session.PlaySession);
        _sessionDirectory = directory;
        _log = SessionLog.Open(directory, static () => WallClock.Now);
        var profile = new SessionProfile(
            MicCheck: _micCheck is not null,
            Muted: options.Muted,
            NoSpeech: options.NoSpeech,
            Greyscale: options.Greyscale,
            TypedCredit: options.Credit.TypedUnits < WordProgress.UnitsPerUse ? "half" : "full",
            Check: options.Check is { } kind ? CheckKinds.Of(kind) : null,
            Scripted: options.IsScreenshotRun,
            Playtest: options.Playtest);
        _log.SessionStart(_session.PlaySession, _session.WorldSeed, _session.Bundle.ContentHash, _session.StoredTime.UnixSeconds, profile);
        Console.WriteLine($"session log: {directory} ({SessionLog.SessionFileName}, {SessionLog.ReplayFileName})");
        if (_session.Migration is { } migration)
        {
            // A save of an older bundle was carried into this one (X01): the log says what was dropped, and the player is told the game was updated.
            _log.SaveMigrated(migration);
            _toasts.Show(migration.Toast);
        }

        _session.Applied += OnAppliedTurn;
        _playtestFlow.Prepare(directory);

        if (!options.NoAutosave)
        {
            var autosavePath = _playtestFlow.SavePath ?? Path.Combine(directory, AutosaveFiles.FileName);
            _autosave = new AutosaveController(new AutosavePolicy(_sim.Tick, _session.Events.Count), autosavePath, _saves, Console.WriteLine, _toasts.Show);
            Console.WriteLine($"autosave: {autosavePath} (written on a lesson step, an objective step, a lesson's end, the session's end, a closed dialogue, and every {AutosavePolicy.IntervalSeconds / 60} minutes of play)");
        }
        else
        {
            Console.WriteLine("autosave: off (--no-autosave)");
        }

        if (options.Check is { } battery)
        {
            _checks.Begin(battery, options.IsScreenshotRun, 0);
        }
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
    }

    // The world is on screen: no check battery and no mic check (the playtest's triggers and its end key need that).
    private bool WorldRunning => _checks.Screen is null && _micCheck is not { IsActive: true };

    /// <summary>The crash handler's view of the game: its state and the last-chance save (X05).</summary>
    public ICrashSource CrashSource { get; }

    /// <summary>The process exit code: 0 unless the screenshot could not be saved.</summary>
    public int ExitCode { get; private set; }

    /// <inheritdoc />
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _devices.Start();
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _blockAtlas = new BlockAtlas(GraphicsDevice);
        var started = MonotonicClock.Milliseconds();
        _preset = _options.QualityPreset;
        _renderer = new VoxelRenderer(GraphicsDevice, _blockAtlas, _sim.Grid, _preset, _options.Dusk);
        if (_options.ThirdPerson)
        {
            _viewMode.Set(CameraMode.ThirdPerson);
        }

        _renderer.SetFigures(_figures.Figures);
        _colours = new GreyscaleWorld(_options.Greyscale, _session.Nouns, _session.Words, _toasts, _renderer.SetGreyKinds, _options.AssumeKnown);
        Console.WriteLine($"voxel: quality {_preset.Name}, view distance {_preset.ViewDistance} blocks, fog from {VoxelRenderer.FogEndFor(_preset.ViewDistance) * VoxelRenderer.FogStartFraction:0} to {VoxelRenderer.FogEndFor(_preset.ViewDistance):0}, mesh budget {_preset.MeshBudgetMilliseconds:0} ms a frame (renderer ready in {MonotonicClock.Milliseconds() - started:0} ms)");
        Console.WriteLine($"graphics: {GraphicsAdapter.DefaultAdapter.Description}, depth {GraphicsDevice.PresentationParameters.DepthStencilFormat}");
        if (_options.Greyscale)
        {
            Console.WriteLine("greyscale: blocks whose noun is not known are grey (the playtest A/B, D-game-6); figures, sky and UI in colour");
        }
        Console.WriteLine($"scene: {_scene.Name}, seed {_scene.Seed}, {_scene.TreeCount} trees, spawn {_scene.Spawn.Feet.X},{_scene.Spawn.Feet.Y},{_scene.Spawn.Feet.Z}, digest {_scene.Grid.ComputeDigestHex()[..16]}");
        _beacon.PlaceAtStart(_scene, _options.Beacon, loaded: _options.LoadPath is not null);

        _view = new FrameView
        {
            Session = _session,
            Dialogue = _dialogue,
            Fonts = new FontProvider(AssetPaths.Font),
            Pixel = _pixel,
            Seed = _options.Seed,
            Book = _book,
            Recordings = _recordings,
            Toasts = _toasts,
            Inventory = _inventory,
            Crafting = _crafting,
            Voxel = _sim,
            Camera3D = _camera.Camera,
            Beacon = _beacon.Beacon,
            Figures = _figures.Figures,
            VoxelIcons = _blockAtlas,
            Objective = _session.Objective,
            Cards = _cards,
            MicCheck = _micCheck,
            ShowTips = _options.ShowsTips,
            Playtest = _playtest,
            Greyscale = _options.Greyscale,
            Ending = _ending,
        };
        _layers.Add(new CrosshairLayer());
        _layers.Add(new NpcLabelLayer());
        _layers.Add(new BeaconArrowLayer());
        _layers.Add(new TalkHintLayer());
        _layers.Add(_debug);
        _layers.Add(new HotbarLayer());
        _layers.Add(new RecipePanelLayer());
        _layers.Add(new ObjectiveLayer());
        _layers.Add(new TipLayer());
        _layers.Add(new SupportLayer());
        _layers.Add(new LessonLayer(_lesson));
        _layers.Add(new WordCardLayer());
        _layers.Add(new DialogueBubbleLayer());
        _layers.Add(new DialoguePanel());
        _layers.Add(new ToastLayer());
        _layers.Add(new EndingLayer());
        _layers.Add(new BookLayer());
        _layers.Add(new InventoryLayer());
        _layers.Add(new CraftingLayer());
        _layers.Add(new ControlsLayer());
        _layers.Add(new CheckLayer());
        _layers.Add(new MicCheckLayer());
        _layers.Add(new ReportLayer()); // the report panel is the top of the screen (S21)
        if (_playtest)
        {
            _layers.Add(new PlaytestHintLayer());
        }

        _layers.Add(new LoadingLayer());
    }

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
        }

        _log?.Pump(_session); // the replay records and the events of this frame (S11)
        _ = _support.Pump(later => _voice.Say(later.Npc, later.Lines)); // the shopkeeper's question after the key words is spoken first
        _lesson.Pump(); // a lesson step's line goes on the panel (S16)
        if (_ending.Update(_session, _sim.Tick))
        {
            // The session's ending (S17): the guide says her closing line, the card says "Session complete" in English and Spanish.
            _voice.Say(SpeechSpeakers.Guide, [_ending.Spanish]);
            _log?.SessionSummary(_ending.Summary, _sim.Tick); // the session's summary card, in the log (X04, R01)
            Console.WriteLine($"session complete: {_ending.Spanish} ({_ending.English}); {_session.Objective.Summary}");
            foreach (var line in _ending.Summary)
            {
                Console.WriteLine($"  {line}");
            }
        }

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

        _beacon.Sync();
        var free = _scenario is null && WorldRunning && !_screens.FreesMouse && _session.PendingTurns == 0;
        if (GuideApproach.ShouldGreet(_session, ref _greetArmed, free))
        {
            Console.WriteLine("approach: the guide greets the player and her lesson begins");
            _dialogueScreen.Open(NpcService.GuideAgent, null); // the player came up to her (S17): the same call the E key makes
        }

        _aimed = _figures.Aimed(_camera.CurrentEye);
        if (_aimed is not null && !_aimedTipOffered)
        {
            _aimedTipOffered = true;
            _session.Tips.Offer("aimed_npc"); // the first time the crosshair is on someone (S16)
        }

        _events.Print();

        // Settled: the world's script is done (or there is none) with no battery on screen, a battery that ends the run has shown its end, or a
        // battery's script rests. A pre-check is not an end: the world's script after it still has steps to take (S12, a screenshot waits for them).
        if (_checks.Screen is null && (_scenario is null || _scenario.IsDone) || _checks.Done && _checks.EndsRun || _checks.ScriptRests && _checks.Screen is { IsDone: false })
        {
            _settled++;
        }

        if (_checks.Screen is null && (_scenario is null || _scenario.IsDone))
        {
            if (!_shown)
            {
                _shown = true;
                if (_options.Journal)
                {
                    _bookScreen.Open();
                }

                if (_options.ShowInventory)
                {
                    _inventoryPanel.Open();
                }

                if (_options.ShowCrafting)
                {
                    _craftingPanel.Open();
                }
            }
        }

        UpdateSound(seconds);

        _beacon.ClearIfReached(_sim);

        base.Update(gameTime);
    }

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        var view = _view!;
        var viewport = GraphicsDevice.PresentationParameters;
        var width = viewport.BackBufferWidth;
        var height = viewport.BackBufferHeight;
        var (canvasWidth, canvasHeight) = _canvas.For(width, height); // the 2D layers are laid out on this canvas and magnified to the window (X08)
        view.ViewportWidth = canvasWidth;
        view.ViewportHeight = canvasHeight;
        var drawStart = MonotonicClock.Milliseconds();

        var alpha = (float)_accumulator.Alpha;
        var farPlane = VoxelRenderer.FarPlaneFor(_preset.ViewDistance);
        var eye = _camera.Place(alpha, width, height, farPlane, canvasWidth, canvasHeight);
        var shoulder = _camera.Shoulder;

        view.Eye = eye;
        view.Look = (_look.YawSteps, _look.PitchSteps);
        view.Fps = _stats.Fps;
        view.Aimed = _aimed;
        view.Check = _checks.Screen;
        view.ControlsOpen = _controls.IsOpen;
        view.ControlsScroll = _controls.Scroll;
        view.Report = _report;
        view.Loading = _session.Worker!.IsReady ? null : LoadingLayer.Text;
        _dialogue.SpeechUnavailable = _session.Worker.IsReady && !_session.Worker.CanHear;
        view.LoadingFrame = _loadingFrames++;
        var mouse = Mouse.GetState();
        view.Mouse = !_options.IsScreenshotRun && GraphicsDevice.Viewport.Bounds.Contains(mouse.Position) ? _canvas.ToCanvas(mouse.Position) : null;

        view.LessonStripBox = LessonLayer.LayoutFor(view, _lesson) is { } lessonStrip ? lessonStrip.Box : null; // the cards keep clear of it (S22)

        _colours!.Refresh();
        // A scripted run builds every mesh it needs before the picture; a live one spends its budget a frame (more while the world is still loading in).
        _renderer!.Remesh(_camera.Camera.Eye, _camera.Camera.Frustum, _options.IsScreenshotRun ? null : _session.Worker!.IsReady ? _preset.MeshBudgetMilliseconds : 30);
        GraphicsDevice.Clear(_renderer.ClearColour);
        _renderer.DrawWorld(_camera.Camera, _sim.Target?.Block, _beacon.Beacon.Target, _sim.Tick, shoulder ? _figures.Avatar() : null, shoulder ? null : _arm.Frame(eye, _renderer.Lights));

        var spriteBatch = _spriteBatch!;
        UiTransform.Set(width, height, canvasWidth, canvasHeight);
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: UiTransform.Matrix);
        foreach (var layer in _layers)
        {
            layer.Draw(spriteBatch, view);
        }

        spriteBatch.End();
        _reportScreen.TakePictureIfRequested(); // F8's frame: the picture as the player saw it, before the report panel is drawn (the panel opens after this frame)

        var drawEnd = MonotonicClock.Milliseconds();
        _timings.Record(drawStart, drawEnd, _sim.Tick);
        base.Draw(gameTime);

        // A screenshot is taken at the size the game asked for (ScreenshotGate): a window's first frames can be drawn before its resize.
        var sized = !_options.IsScreenshotRun || ScreenshotGate.IsFinalSize(width, height, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight, Window.ClientBounds.Width, Window.ClientBounds.Height);
        if (!sized)
        {
            _sizeFrames++;
            if (!_sizeWarned && ScreenshotGate.MayShoot(false, _sizeFrames))
            {
                _sizeWarned = true;
                Console.WriteLine($"screenshot: the window stayed {width} x {height} (wanted {_graphics.PreferredBackBufferWidth} x {_graphics.PreferredBackBufferHeight}) for {_sizeFrames} frames; the picture is taken at this size");
            }
        }

        var mayShoot = ScreenshotGate.MayShoot(sized, _sizeFrames);
        if (_shotPath is { } shot && mayShoot)
        {
            // A --shot step: the screen as it is at this point of the script, then the script goes on.
            var saved = ScreenshotWriter.TrySave(GraphicsDevice, shot);
            Console.WriteLine($"shot: {shot} ({(saved ? "saved" : "FAILED")}) at tick {_sim.Tick}, {_session.Objective.Summary}");
            ExitCode = saved ? ExitCode : 1;
            _shotPath = null;
        }

        if (mayShoot && _options.ScreenshotPath is { } path
            && ((_sim.Tick >= _options.Frames && _settled >= SettleTicks && _session.Worker!.IsReady && _session.PendingTurns == 0 && !_dialogue.SubmitRequested && _dialogue.PendingSpeech is null
                && _micCheck is not { Phase: MicCheckPhase.Ready or MicCheckPhase.Listening or MicCheckPhase.Thinking })
                || _sim.Tick > RunawayTicks
                || (_checks.RestsForShot && _settled >= SettleTicks)))
        {
            ExitCode = ScreenshotWriter.TrySave(GraphicsDevice, path) ? 0 : 1;
            Console.WriteLine($"scene draw (voxel world, layers, flush): {_timings.DrawSummary()}");
            Console.WriteLine($"frame time (end of one draw to the next, no vsync): {_timings.FrameSummary()}");
            Console.WriteLine($"voxel: {_renderer.Stats.Summary()}; {_renderer.DrawnChunks} chunks drawn this frame, {_renderer.DrawnFigureQuads} figure quads, {_renderer.DrawnPlantQuads} plant quads (within {_preset.PlantDistance} blocks, {_preset.PlantKeepPercent}% built), occlusion {(_preset.AmbientOcclusion ? "on" : "off")}, clouds {(_preset.Clouds ? "on" : "off")}");
            var body = _sim.Player;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"player: ({body.CenterX / (double)VoxelPhysics.UnitsPerBlock:0.00}, {body.FeetY / (double)VoxelPhysics.UnitsPerBlock:0.00}, {body.CenterZ / (double)VoxelPhysics.UnitsPerBlock:0.00}), tick {_sim.Tick}, wood {_sim.Inventory.Count(ItemId.Wood)}, berries {_sim.Inventory.Count(ItemId.Berry)}, bottle {_sim.Inventory.Count(ItemId.Bottle)}, water {_sim.Inventory.Count(ItemId.WaterBottle)}, bread {_sim.Inventory.Count(ItemId.Bread)}"));
            Console.WriteLine($"objective: {_session.Objective.Summary}");
            Exit();
        }
    }

    /// <inheritdoc />
    protected override void UnloadContent()
    {
        Window.TextInput -= OnTextInput;
        foreach (var line in _support.Summary())
        {
            Console.WriteLine(line);
        }

        foreach (var line in LearnerSummary.Lines(_session.Learner, _session.Lessons.Record, _session.PlaySession, _session.StoredTime.UnixSeconds))
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
        _renderer?.Dispose();
        _blockAtlas?.Dispose();
        _pixel?.Dispose();
        _spriteBatch?.Dispose();
        base.UnloadContent();
    }

    // The scripted run names an item as a noun (madera) or as the item's key (wood).
    private ItemId? ResolveItem(string name)
    {
        var noun = _session.Nouns.ForLemma(name);
        if (noun is not null && noun.Items.Count > 0)
        {
            return noun.Items[0];
        }

        foreach (var definition in ItemDefinition.All)
        {
            if (string.Equals(definition.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return definition.Id;
            }
        }

        return null;
    }

    // The crosshair is on the workbench (the mesa): E opens the crafting panel there, as C does anywhere.
    private bool AimedAtWorkbench() => _sim.Target is { } hit && hit.Id == BlockId.Table;

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

        if (IsActive && input.LeftClicked)
        {
            ClickOnScreens(mouse.X, mouse.Y);
        }

        var swallowClick = false;
        if (IsActive && !uiOpen && !_captureWanted && input.LeftClicked)
        {
            _captureWanted = true; // a click takes the mouse back; it does not gather
            swallowClick = true;
        }

        var captured = IsActive && !uiOpen && _captureWanted;
        _mouseLook.SetCaptured(captured, Window);
        IsMouseVisible = !captured;
        if (captured)
        {
            var (dx, dy) = _mouseLook.Read(Window);
            _look.Turn(dx, dy);
        }

        var strafe = 0;
        var forward = 0;
        var jump = false;
        int? slot = null;
        if (captured)
        {
            strafe = (input.Down(Keys.D) || input.Down(Keys.Right) ? 1 : 0) - (input.Down(Keys.A) || input.Down(Keys.Left) ? 1 : 0);
            forward = (input.Down(Keys.W) || input.Down(Keys.Up) ? 1 : 0) - (input.Down(Keys.S) || input.Down(Keys.Down) ? 1 : 0);
            jump = input.Down(Keys.Space);
        }

        if (!uiOpen)
        {
            for (var i = 0; i < SlotKeys.Length; i++)
            {
                if (input.Pressed(SlotKeys[i]))
                {
                    slot = i;
                }
            }

            var wheel = input.Wheel;
            if (wheel != 0)
            {
                slot = (((_sim.SelectedSlot - Math.Sign(wheel)) % Inventory.HotbarSize) + Inventory.HotbarSize) % Inventory.HotbarSize;
            }
        }

        var target = _sim.Target;
        if (IsActive && BookItemUse.Applies(input, captured, _book.IsOpen, _sim.SelectedStack))
        {
            _openBook.Execute(); // the book item opens the book, as J does (S20)
        }

        _live.SetFrame(
            strafe,
            forward,
            jump,
            captured && input.LeftDown && !swallowClick ? LiveVoxelInput.GatherFor(target) : null,
            captured && input.RightClicked ? LiveVoxelInput.PlacementFor(_sim.SelectedStack, target) : null,
            slot);
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
        _mouseLook.SetCaptured(false, Window);
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
            _toasts.Show($"Game saved (session {_session.PlaySession})");
        }
        else
        {
            _toasts.Show("Wait a moment, then press F6 again.");
        }
    }

    // The autosave (X05) at its safe points: only at rest, with no dialogue open, no voice being recorded, no check battery, mic check or report panel.
    private void AutosaveDue() =>
        _autosave?.Run(_sim.Tick, _session.Events, atRest: _session.CanSave && !_dialogue.IsOpen && !_listen.Listening && WorldRunning && !_report.Paused);

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
