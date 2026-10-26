using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Event;
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
using Vocaterra.Nlp.Core.Audio;

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

    private static readonly (Keys Key, PanelKey Panel)[] PanelKeys =
    [
        (Keys.R, PanelKey.R), (Keys.S, PanelKey.S), (Keys.H, PanelKey.H), (Keys.N, PanelKey.N), (Keys.W, PanelKey.W), (Keys.Tab, PanelKey.Tab),
        (Keys.D1, PanelKey.D1), (Keys.D2, PanelKey.D2), (Keys.D3, PanelKey.D3), (Keys.NumPad1, PanelKey.D1), (Keys.NumPad2, PanelKey.D2), (Keys.NumPad3, PanelKey.D3),
    ];

    /// <summary>The thank-you note of the playtest's post-check (the screen's closing lines; the English is for the facilitator and the tester alike).</summary>
    private const string PlaytestThankYou = "Thank you for playing. Your answers are in the session log.";

    /// <summary>The thank-you note of the next-day recall of a playtest.</summary>
    private const string RecallThankYou = "Thank you. Your answers are in the session log.";

    private static readonly (Keys Key, LessonKey Lesson)[] LessonKeys =
    [
        (Keys.Enter, LessonKey.Enter), (Keys.K, LessonKey.K), (Keys.L, LessonKey.L), (Keys.M, LessonKey.M),
        (Keys.D1, LessonKey.D1), (Keys.D2, LessonKey.D2), (Keys.D3, LessonKey.D3), (Keys.D4, LessonKey.D4),
        (Keys.NumPad1, LessonKey.D1), (Keys.NumPad2, LessonKey.D2), (Keys.NumPad3, LessonKey.D3), (Keys.NumPad4, LessonKey.D4),
    ];

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
    private bool _controlsOpen;
    private int _controlsScroll;
    private bool _aimedTipOffered;
    private readonly NpcVoice _voice;
    private readonly TurnWatchdog _watchdog = new();
    private readonly MicCheckModel? _micCheck;
    private readonly MicCheckScreen _micScreen;
    private readonly InputDevices _devices;
    private readonly TextEditKeys _textEdits = new();
    private readonly OpenKeySwallow _swallow = new();
    private readonly BookModel _book = new();
    private readonly BookRecordings _recordings;
    private readonly BookSpeechControls _bookControls;
    private readonly ToastQueue _toasts = new();
    private readonly InventoryScreen _inventory = new();
    private readonly FrameTimings _timings = new();
    private readonly List<VoxelCommand> _commands = [];
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
    private readonly Queue<int> _pendingCrafts = new();
    private int _loadingFrames;
    private string? _shotPath;
    private readonly CheckBattery _checks;
    private SessionLog? _log;

    // The report panel (S21, D-game-17): F8 pauses the world and the next frame is kept in memory (the picture the player saw); nothing is written
    // until Ctrl+Enter, and Esc keeps nothing. A scripted report (--report, --report-open) is typed and saved by the script's frame.
    private readonly ReportPanel _report = new();
    private readonly ReportScreen _reportScreen;
    private string _sessionDirectory = string.Empty;
    private bool _microphoneFound;

    // The playtest profile (S12): the post-check starts by itself or with F10, its save goes to the session folder, and the thank-you
    // screen waits for Esc. A screenshot waits for the wanted window size before it is taken (ScreenshotGate).
    private readonly bool _playtest;
    private string? _playtestSavePath;
    private bool _playtestStarted;
    private bool _playtestSaveDue;
    private bool _playtestSaved;
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
        _lesson.OnRecapDone = ShowPhrasesBook; // the lesson's recap ends on the journal's phrases page (S17)
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
        if (options.IsScreenshotRun)
        {
            _clock = new FixedFrameClock(VoxelSimulation.TicksPerSecond);
            if (options.Script.Count > 0)
            {
                var hooks = new ScenarioHooks(
                    npc => OpenDialogueOn(npc, null),
                    CloseDialogue,
                    path => _shotPath = path,
                    () => _shotPath is not null,
                    RunLessonStep,
                    (text, keep) => _reportScreen.RequestScripted(text, keep, OpenScreenName()),
                    () => _report.Paused,
                    PlayBookTarget,
                    PractiseFromScript);
                _scenario = new ScenarioScript(session, _look, _dialogue, options.Script, ResolveItem, hooks, Console.WriteLine, holdsCheckAnswers: options.Playtest);
            }
        }
        else
        {
            _clock = new RealFrameClock();
        }

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
        if (options.Playtest)
        {
            _playtestSavePath = PlaytestEnd.SaveIn(directory);
            Console.WriteLine(options.IsPlaytestRecall
                ? $"playtest: the next-day recall of the save from session {_session.PlaySession - 1}; the run ends after the check"
                : $"playtest: the pre-check, the run, then the post-check when the camp is done, the fire at the player's camp is lit and the transfer offered (or F10); the save is {_playtestSavePath}");
        }

        if (!options.NoAutosave)
        {
            var autosavePath = options.Playtest && _playtestSavePath is not null ? _playtestSavePath : Path.Combine(directory, AutosaveFiles.FileName);
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

    // The post-check of a playtest (S12): it starts when the camp is done and the transfer offered (Update), or at once with F10. The world stops
    // while it runs; a scripted run answers it from the script's next step, so the world script is done with its steps when it ends.
    private void StartPlaytestPost(string why)
    {
        _playtestStarted = true;
        if (_dialogue.IsOpen)
        {
            CloseDialogue();
        }

        if (_book.IsOpen)
        {
            CloseBook();
        }

        if (_inventory.IsOpen)
        {
            CloseInventory();
        }

        if (_crafting.IsOpen)
        {
            CloseCrafting();
        }

        Console.WriteLine($"playtest: the post-check starts ({why}) at tick {_sim.Tick}; {_session.Objective.Summary}");
        _checks.Begin(CheckKind.Post, _options.IsScreenshotRun, _scenario?.Index ?? 0);
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
            ApplyFinishedTurns();
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
            RunWatchdog(seconds); // a paused world does not time a turn out (S21)
        }

        _micScreen.DriveScripted();
        _reportScreen.DriveScripted(); // a scripted report is typed and saved here, while its panel is up (S21)
        if (_playtest && (_scenario is null || _scenario.AtStepBoundary)
            && PlaytestEnd.StartsOnItsOwn(true, WorldRunning, _playtestStarted, _session.SessionComplete, PlaytestEnd.TransferOffered(_session.Events), _dialogue.IsOpen))
        {
            StartPlaytestPost("the camp is done, the player's fire is lit and the transfer offered");
        }

        if (_playtestSaveDue && _session.CanSave)
        {
            _playtestSaved = _saves.Write(_playtestSavePath!, "at the end of the playtest");
            _playtestSaveDue = !_playtestSaved;
        }

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
            StepOnce(); // a script step may open the report panel: the rest of the ticks wait for it
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
        var free = _scenario is null && WorldRunning && !_dialogue.IsOpen && !_book.IsOpen && !_inventory.IsOpen && !_crafting.IsOpen && !_controlsOpen && _session.PendingTurns == 0;
        if (GuideApproach.ShouldGreet(_session, ref _greetArmed, free))
        {
            Console.WriteLine("approach: the guide greets the player and her lesson begins");
            OpenDialogueOn(NpcService.GuideAgent, null); // the player came up to her (S17): the same call the E key makes
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
                    OpenBook();
                }

                if (_options.ShowInventory)
                {
                    OpenInventory();
                }

                if (_options.ShowCrafting)
                {
                    OpenCrafting();
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
        view.ControlsOpen = _controlsOpen;
        view.ControlsScroll = _controlsScroll;
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

        if (_playtest && !_options.IsPlaytestRecall && !_playtestSaved && _playtestSavePath is { } playtestSave)
        {
            DrainForSave();
            _playtestSaved = _saves.Write(playtestSave, "at the quit (the playtest ended before its post-check)");
        }

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

    private void StepOnce()
    {
        _commands.Clear();
        if (_scenario is { IsDone: false } && _micCheck is not { IsActive: true })
        {
            _scenario.Fill(_commands);
        }
        else if (!_dialogue.IsOpen && _micCheck is not { IsActive: true })
        {
            _live.Fill(_commands);
        }

        // A craft clicked or typed this frame runs in this tick, after the gathering and the placing (the simulation's order).
        while (_pendingCrafts.Count > 0)
        {
            _commands.Add(VoxelCommand.Craft(_pendingCrafts.Dequeue()));
        }

        // The look goes first: it is absolute and every other command of the tick uses the facing it sets.
        _commands.Insert(0, VoxelCommand.Look(_look.Yaw, _look.Pitch));
        _session.Step(_commands);
        if (_options.DebugCrashAfter is { } crashAt && _sim.Tick >= crashAt)
        {
            // The test hook of the crash handler (X05, --debug-crash-after): the exception comes out of the game loop, as a real bug would.
            throw new InvalidOperationException($"debug crash: --debug-crash-after {crashAt} asked for it (a test hook, not a bug)");
        }
        _arm.AfterTick(_commands); // a placement swings the arm once (S15)

        // The sounds of the tick (S24): they read the tick's events and write nothing back.
        _sound.ObserveSimulation(_sim.Events, (x, y, z) => _sim.Grid.GetBlock(x, y, z));
        _sound.ObserveWorld(_sim.Tick, _sim.Events, _sim.Player, (x, y, z) => _sim.Grid.GetBlock(x, y, z), _session.FireLit); // the footsteps and the beds (S25)
        _sound.Scripted.Tick(); // the cues of the frame play together in UpdateSound, so an answer's chime and a step's chime fold into one

        _figures.AfterTick();
        _camera.AfterTick();
        // Pickups show in the hotbar and, the first time, as a word card; they do not toast (a toast per pickup would queue behind the
        // checklist's completions for minutes).
        AnnounceObjective(_session.Completed);
        _voice.Update();
        foreach (var card in _session.TakeWordCards())
        {
            _cards.Add(card);
            _sound.Queue(SoundCue.WordLearnt); // a new word: the sparkle (S24)
            _voice.Say(SpeechSpeakers.Guide, [card.Phrase]); // a word card says its noun (S9)
        }

        foreach (var hint in _session.TakeHints())
        {
            _toasts.Show(hint);
        }
    }

    // A step of the camp checklist that was just done: a toast with the step, and one for the whole checklist when it is complete.
    private void AnnounceObjective(IReadOnlyList<ObjectiveStep> completed)
    {
        foreach (var step in completed)
        {
            _toasts.Show($"Done: {step.Text}");
            Console.WriteLine($"objective: Done: {step.Text} -> {_session.Objective.Summary}");
        }

        if (completed.Count > 0 && _session.Objective.Complete)
        {
            _toasts.Show("Objective complete");
            Console.WriteLine("objective: complete");
        }
    }

    private void OpenCrafting()
    {
        if (_book.IsOpen || _inventory.IsOpen || _dialogue.IsOpen)
        {
            return;
        }

        _crafting.Open(_session.Words, CraftingScreen.LemmasShown(_session.Recipes));
    }

    private void CloseCrafting() => _crafting.Close();

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
            if (_playtest && _checks.EndsRun && _checks.Done && input.Pressed(Keys.Escape))
            {
                Exit(); // the playtest's thank-you screen: Esc closes the game (Esc always works)
                _devices.CommitKeys(input);
                return;
            }

            if (_playtest && input.Pressed(Keys.F10) && PlaytestEnd.EndKeyStarts(true, WorldRunning, _playtestStarted))
            {
                StartPlaytestPost("the end key");
            }

            if (input.Pressed(Keys.F5))
            {
                var mode = _viewMode.Toggle(); // the behind-the-shoulder view and back (S15)
                Console.WriteLine($"view: {(mode == CameraMode.ThirdPerson ? "behind the shoulder" : "first person")}");
            }

            if (input.Pressed(Keys.F6))
            {
                SaveNow();
            }

            if (input.Pressed(Keys.F9))
            {
                _ = _canvas.Next(); // the interface text size: 100, 125, 150 percent (X08); no other F9 binding, and a text box never reads it
                Console.WriteLine($"text scale: {_canvas.TextPercent} percent (canvas {_canvas.Width} x {_canvas.Height})");
            }

            if (_micCheck is { IsActive: true })
            {
                _micScreen.HandleInput(input);
            }
            else if (_checks.Screen is { IsDone: false })
            {
                _checks.HandleInput(input);
            }
            else if (_dialogue.IsOpen)
            {
                ReadDialogueKeys(input);
            }
            else if (_controlsOpen)
            {
                if (input.Pressed(Keys.Escape) || input.Pressed(Keys.F1))
                {
                    _controlsOpen = false; // the controls help (F1, S16)
                    _controlsScroll = 0;
                }
                else
                {
                    // When the controls do not all fit the canvas (a large text size), Up and Down and the wheel scroll the list (X08).
                    var maxScroll = ControlsLayer.MaxScrollFor(_view!);
                    var step = (input.Pressed(Keys.Down) ? 1 : 0) - (input.Pressed(Keys.Up) ? 1 : 0) + (input.Pressed(Keys.PageDown) ? 5 : 0) - (input.Pressed(Keys.PageUp) ? 5 : 0);
                    var wheelNotches = -(int)Math.Round(input.Wheel / 120.0);
                    _controlsScroll = Math.Clamp(_controlsScroll + step + wheelNotches, 0, maxScroll);
                }
            }
            else if (_book.IsOpen)
            {
                ReadBookKeys(input);
            }
            else if (_inventory.IsOpen)
            {
                if (input.Pressed(Keys.Escape) || input.Pressed(Keys.I) || input.Pressed(Keys.E))
                {
                    CloseInventory();
                }
            }
            else if (_crafting.IsOpen)
            {
                if (input.Pressed(Keys.Escape) || input.Pressed(Keys.C))
                {
                    CloseCrafting();
                }
            }
            else if (_aimed is not null && (input.Pressed(Keys.E) || input.Pressed(Keys.T) || input.Pressed(Keys.V)))
            {
                // The crosshair is on an NPC: E or T opens the panel; V opens it and starts listening.
                if (input.Pressed(Keys.V))
                {
                    OpenDialogueOn(_aimed.Name, null);
                    BeginListening(Keys.V);
                }
                else
                {
                    OpenDialogueOn(_aimed.Name, input.Pressed(Keys.T) ? 't' : 'e');
                }
            }
            else if (input.Pressed(Keys.F1))
            {
                _controlsOpen = true;
            }
            else if (input.Pressed(Keys.Enter) && _session.Tips.Current is not null)
            {
                _session.Tips.Dismiss(); // Enter: next tip (S16)
            }
            else if (input.Pressed(Keys.J))
            {
                OpenBook();
            }
            else if (input.Pressed(Keys.C) || (input.Pressed(Keys.E) && AimedAtWorkbench()))
            {
                OpenCrafting();
            }
            else if (input.Pressed(Keys.I) || input.Pressed(Keys.E))
            {
                OpenInventory();
            }
            else if (input.Pressed(Keys.Escape))
            {
                _captureWanted = false;
                _ending.Dismiss(); // Esc takes the closing card down (S17)
            }

            if (input.Pressed(Keys.F3) && !_playtest)
            {
                _debug.Visible = !_debug.Visible;
            }
        }

        var uiOpen = _book.IsOpen || _inventory.IsOpen || _dialogue.IsOpen || _crafting.IsOpen || _controlsOpen || _micCheck is { IsActive: true };
        // The wheel scrolls the book (three lines a notch, up for a turn up); a click on a tab turns to that page (S20).
        if (IsActive && _book.IsOpen && input.Wheel != 0)
        {
            // Over the right page the wheel scrolls that page (X03); over the list it scrolls the list.
            var notchLines = -(int)Math.Round(input.Wheel / 40.0);
            if (new BookGeometry(_canvas.Width, _canvas.Height).RightPage.Contains(mouse.X, mouse.Y))
            {
                _book.ScrollDetail(notchLines * BookGeometry.LineHeight);
            }
            else
            {
                _book.Scroll(notchLines);
            }
        }

        if (IsActive && _book.IsOpen && input.LeftClicked)
        {
            if (new BookGeometry(_canvas.Width, _canvas.Height).TabAt(mouse.X, mouse.Y) is { } tab)
            {
                _book.Show(tab);
                _sound.QueueClick(SoundCue.UiTab);
            }
            else if (_bookControls.Click(new BookGeometry(_canvas.Width, _canvas.Height), mouse.X, mouse.Y, input.Shift))
            {
                _sound.QueueClick(SoundCue.UiClick);
            }
            else if (_bookControls.ClickVerb(new BookGeometry(_canvas.Width, _canvas.Height), mouse.X, mouse.Y))
            {
                _sound.QueueClick(SoundCue.UiClick);
            }
            else if (_bookControls.ClickPractise(new BookGeometry(_canvas.Width, _canvas.Height), mouse.X, mouse.Y) is { } practise)
            {
                _sound.QueueClick(SoundCue.UiClick);
                PractiseAgain(practise);
            }
        }

        if (IsActive && _crafting.IsOpen && input.LeftClicked)
        {
            // A click on a row of the panel asks for one craft in the next tick (the same command a script sends).
            var layout = new CraftingLayout(_canvas.Width, _canvas.Height, _session.Recipes.Entries.Count);
            if (layout.RowAt(mouse.X, mouse.Y) is { } row)
            {
                if (_sim.CanCraft(row) != CraftResult.Done)
                {
                    _sound.Queue(SoundCue.CraftRefused); // soft: the ingredients are not there yet (S24)
                }

                _pendingCrafts.Enqueue(row);
            }
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
        // Using the book item (selected, right-click) opens the book, as J does (S20). The item is not placeable, so the press places nothing.
        if (IsActive && captured && input.RightClicked && !_book.IsOpen && _sim.SelectedStack is { Item: ItemId.Book, Count: > 0 })
        {
            OpenBook();
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

    // ---- The bug and feature report (S21, D-game-17): local files only, nothing is sent. ----

    // F8: the game pauses. A recording in progress is dropped (the microphone is off), and the next frame is kept as the picture the player saw.
    private void RequestReport()
    {
        CancelListening();
        if (_checks.Screen is { Listening: true })
        {
            _checks.CancelListening();
        }

        _micScreen.InterruptForReport(); // the same as a clip too short to hear: the check asks for another try, and nothing is sent

        _reportScreen.Request(OpenScreenName());
    }

    // What is open now, in the words the report's table uses.
    private string OpenScreenName()
    {
        if (_checks.Screen is { IsDone: false } check)
        {
            return $"check ({CheckKinds.Of(check.Kind)})";
        }

        if (_micCheck is { IsActive: true })
        {
            return "mic check";
        }

        if (_dialogue.IsOpen)
        {
            return $"dialogue with {_dialogue.NpcName}";
        }

        if (_book.IsOpen)
        {
            return "book";
        }

        if (_inventory.IsOpen)
        {
            return "inventory";
        }

        if (_crafting.IsOpen)
        {
            return "crafting";
        }

        return _controlsOpen ? "controls (F1)" : "none (the world)";
    }

    // The input of a frame the panel or F8 took: the mouse is free, and the states are kept for the next frame's presses and clicks.
    private void EndInput(InputSnapshot input)
    {
        _mouseLook.SetCaptured(false, Window);
        IsMouseVisible = true;
        _devices.Commit(input);
    }

    // The panel's keys while it is open. In the speech-button mode R, S, H, N, W (or Tab), 1 to 3 press the buttons, V or Space held talks and Enter
    // presses the Escribir button when typing is on offer; in the typing mode Enter sends, Tab or Esc go back to the buttons and the editing keys repeat.
    // Esc undoes the newest thing first: a recording, a turn that is thinking, the typing mode, and last the panel itself.
    private void ReadDialogueKeys(InputSnapshot input)
    {
        _listen.ReleaseIfUp(input);
        if (input.Pressed(Keys.Escape))
        {
            if (_listen.Listening)
            {
                CancelRecording();
            }
            else if (_actions.CancelThinking())
            {
                _watchdog.Reset();
            }
            else if (_dialogue.IsTyping)
            {
                _dialogue.EndTyping();
            }
            else
            {
                CloseDialogue();
            }

            return;
        }

        if (_dialogue.IsTyping)
        {
            if (input.Pressed(Keys.Tab))
            {
                _dialogue.EndTyping();
                return;
            }

            if (input.Pressed(Keys.Enter))
            {
                _dialogue.RequestSubmit();
            }

            var entry = _dialogue.Entry;
            var edits = _textEdits.Step(input.KeyboardState);
            if (edits.Backspace)
            {
                _ = entry.Backspace();
            }

            if (edits.Delete)
            {
                _ = entry.Delete();
            }

            if (edits.Left)
            {
                entry.MoveLeft();
            }

            if (edits.Right)
            {
                entry.MoveRight();
            }

            return;
        }

        // The lesson strip's keys (S16): L starts the lesson on offer, Enter continues, K skips the step, 1 to 4 pick an answer of a listen step.
        foreach (var (key, lessonKey) in LessonKeys)
        {
            if (input.Pressed(key) && _lesson.PressKey(lessonKey))
            {
                return;
            }
        }

        if (input.Pressed(Keys.V) || input.Pressed(Keys.Space))
        {
            BeginListening(input.Pressed(Keys.V) ? Keys.V : Keys.Space);
            return;
        }

        if (input.Pressed(Keys.Enter) && _dialogue.TypingOffered)
        {
            PerformAction(SpeechAction.Type, null);
            return;
        }

        foreach (var (key, panelKey) in PanelKeys)
        {
            if (input.Pressed(key) && SpeechButtons.ForKey(panelKey) is { } action)
            {
                PerformAction(action, panelKey == PanelKey.W ? 'w' : null);
                return;
            }
        }
    }

    // A button or a choice of the panel was pressed (key or mouse). Opening the text box with W would also type the W the platform delivers: swallow it.
    private void PerformAction(SpeechAction action, char? typedByKey)
    {
        if (_actions.Perform(action))
        {
            _sound.QueueClick(SoundCue.UiClick);
            if (action == SpeechAction.Type && typedByKey is { } typed)
            {
                _swallow.Arm(typed);
            }

            _watchdog.Reset();
        }
    }

    // The mouse went down at a point: a button of the mic check, or a button or choice of the panel.
    private void ClickOnScreens(int x, int y)
    {
        var font = _view!.Fonts.Get(FontProvider.NativeSize);
        if (_micScreen.Active)
        {
            _micScreen.Click(x, y);
            return;
        }

        if (_dialogue.IsOpen)
        {
            if (LessonLayer.LayoutFor(_view!, _lesson) is { } strip && strip.ActionAt(x, y) is { } lessonAction)
            {
                if (_lesson.Perform(lessonAction)) // a button of the lesson strip (S16)
                {
                    _sound.QueueClick(SoundCue.UiClick);
                }

                return;
            }

            var layout = DialoguePanel.LayoutFor(_dialogue, _canvas.Width, _canvas.Height, font, _session);
            if (layout.ActionAt(x, y) is { } action)
            {
                PerformAction(action, null);
            }
        }
    }

    // The front-end's watchdog (S7): a turn that has been thinking for five seconds of the frame clock logs a timeout and offers typing. A scripted run
    // has no real time, so it has no watchdog.
    private void RunWatchdog(double seconds)
    {
        if (_options.IsScreenshotRun)
        {
            return;
        }

        if (_watchdog.Update(seconds, _dialogue.IsOpen && _dialogue.IsThinking))
        {
            _session.LogVoice(VoiceEventKind.Timeout, $"{TurnWatchdog.LimitSeconds:0} s");
            _dialogue.OfferTyping();
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

        if (!_dialogue.IsTyping || char.IsControl(args.Character) || _listen.Listening)
        {
            if (_dialogue.IsOpen)
            {
                _ = _swallow.Take(args.Character);
            }

            return;
        }

        if (_swallow.Take(args.Character))
        {
            return;
        }

        _ = _dialogue.Entry.Insert(AccentShortcuts.ForTyped(args.Character, InputDevices.AltHeld())); // Alt plus a vowel is the accented letter (S21 wired the hints' keys)
    }

    // Opens the panel on an NPC: its opening is a core host turn when its state has one (the shopkeeper's question, the villager's hello, the
    // guide's transfer offer), otherwise its prompt's text, and the guide's review question when one is due. The key that opened the panel is
    // swallowed by the text event.
    private void OpenDialogueOn(string npc, char? openedBy)
    {
        var opening = _session.OpenTalk(npc, teachKeyWords: true);
        _dialogue.Open(opening, NpcNames.LabelOf(npc), _session.ReviewLine, _session.RetryNote);
        _voice.Say(npc, opening.Lines);
        if (openedBy == 't')
        {
            _dialogue.BeginTyping(); // T is "type": the text box has the keyboard from the start
        }

        _swallow.Arm(openedBy); // the platform types the key once, in the next frame at the latest
        var how = opening.HostTurn ? "host turn" : "prompt";
        Console.WriteLine($"talk: {npc} ({how}, {opening.Prompt}): {(_session.KeyWordsPending ? $"[key words: {string.Join(", ", _session.KeyWordCards.Select(static c => c.Phrase))}]" : string.Join(" / ", opening.Lines))}{(_session.ReviewLine is { } review ? $" / {review}" : string.Empty)}");
    }

    // A lesson step of the scripted run (S16): the same presses the strip's buttons make.
    private void RunLessonStep(ScriptStepKind kind, string value)
    {
        switch (kind)
        {
            case ScriptStepKind.LessonStart:
                if (!_session.Lessons.Start(value))
                {
                    Console.WriteLine($"lesson: cannot start '{value}' (unknown, or another lesson is active)");
                    return;
                }

                _lesson.Pump();
                break;
            case ScriptStepKind.LessonContinue:
                _ = _lesson.Perform(LessonAction.Continue);
                break;
            case ScriptStepKind.LessonSkip:
                _ = _lesson.Perform(LessonAction.Skip);
                break;
            case ScriptStepKind.LessonHelp:
                _ = _lesson.Perform(LessonAction.Help);
                break;
            default: // LessonPick
                _ = _lesson.Perform(LessonAction.Option1 + (int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) - 1));
                break;
        }
    }

    // The panel closes (Esc, or the script walks on): a recording in progress is dropped and the talk ends.
    private void CloseDialogue()
    {
        CancelListening();
        _watchdog.Reset();
        _dialogue.Close();
        _voice.Stop();
        _session.EndTalk();
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

    // The microphone: the key went down. The worker must have the recognizer, the machine a microphone; otherwise the panel says why.
    private void BeginListening(Keys key)
    {
        var worker = _session.Worker!;
        if (!_dialogue.IsOpen)
        {
            OpenDialogueOn(_aimed?.Name ?? NpcService.GuideAgent, null);
        }

        if (!worker.CanHear)
        {
            _dialogue.SpeechNotice = worker.Load?.Problem ?? "Speech is not available: type your answer.";
            return;
        }

        if (_listen.Begin(new ListenTarget(HeardSpeech), key))
        {
            _dialogue.Listening = true;
            _dialogue.SpeechNotice = null;
        }
        else
        {
            _dialogue.SpeechNotice = _listen.Problem;
        }
    }

    // The key went up (or the recording reached its cap): the clip goes to the worker as a spoken turn.
    private void HeardSpeech(PcmClip? clip)
    {
        _dialogue.Listening = false;
        if (clip is null)
        {
            _dialogue.SpeechNotice = _listen.Problem;
            return;
        }

        _ = _session.SubmitClip(clip, CursorCell());
        _dialogue.BeginSpoken();
        Console.WriteLine($"listened: {clip.DurationSeconds:0.00} s of speech sent to {_session.CurrentNpc}");
    }

    // Esc while the microphone records: the recording is dropped and nothing is sent (logged as a cancel).
    private void CancelRecording()
    {
        CancelListening();
        _session.LogVoice(VoiceEventKind.Cancel, "recording");
    }

    // The panel closed while recording: the clip is dropped.
    private void CancelListening()
    {
        _listen.Cancel();
        _dialogue.Listening = false;
    }

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

    // The playtest's end (S12): its post- or recall-check is over, the world script is done with its steps, and a post-check saves the run.
    private void EndPlaytest(CheckScreen check)
    {
        _scenario?.SkipTo(_options.Script.Count);
        if (_options.IsPlaytestRecall)
        {
            check.Closing = RecallThankYou;
            return;
        }

        var folder = Path.GetFileName(Path.GetDirectoryName(_playtestSavePath!)) ?? string.Empty;
        check.Closing = $"{PlaytestThankYou}\nSaved: {PlaytestEnd.SaveFileName} in the session folder {folder}.";
        _playtestSaveDue = true;
    }

    // The save file of F6 (S10): the --save file, in a playtest the session folder's playtest.save (S18), or vocaterra.save.json in the working folder.
    // A turn still thinking means "not now".
    private void SaveNow()
    {
        var path = PlaytestEnd.ManualSavePath(_options.SavePath, _playtest ? _playtestSavePath : null, DefaultSavePath);
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
            ApplyFinishedTurns();
            System.Threading.Thread.Sleep(10);
        }

        ApplyFinishedTurns();
        if (_session.KeyWordsPending)
        {
            _session.FinishKeyWords();
        }

        _ = _session.TakeOpening(); // an opening that waits is only what the panel says: the save holds the NPCs' states, not the lines
    }

    private void ApplyFinishedTurns()
    {
        foreach (var applied in _pump.Apply())
        {
            var turn = applied.Turn;
            if (applied.Outcome.Request.Check is not null)
            {
                continue; // a check answer: the check screen took it when it was applied (OnAppliedTurn)
            }

            if (applied.Outcome.Cancelled)
            {
                if (!applied.Outcome.Request.IsMicCheck)
                {
                    Console.WriteLine("turn cancelled: the player pressed Esc while it was thinking; nothing of it was applied");
                }

                continue;
            }

            if (applied.Outcome.Request.IsMicCheck)
            {
                // The sound for a mic check's result comes with its voice event (passed or not), in the session's events (S24).
                var result = applied.Outcome.MicCheck ?? new MicCheckResult(string.Empty, false, 0);
                Console.WriteLine($"mic check: heard \"{result.Heard}\", level {result.Level:0.00}, {(result.Passed ? "passed" : "not passed")}{(applied.Outcome.Problem is null ? string.Empty : $" ({applied.Outcome.Problem})")}");
                _micCheck?.Complete(result);
                continue;
            }

            if (turn is null)
            {
                Console.WriteLine($"turn failed: {applied.Outcome.Problem}");
                continue;
            }

            _sound.ObserveTurn(turn, applied.Words); // the answer's cue: understood, or the neutral one, and a new known word (S24)
            var timings = applied.Outcome.Timings;
            var detail = turn.Input == InputKind.Spoken
                ? $"heard \"{turn.Line}\" (confidence {(int)Math.Round((turn.Confidence ?? 0) * 100)} %){(turn.Uncertain ? ", UNCERTAIN" : string.Empty)} -> {turn.Summary}, {timings.TotalMilliseconds:0} ms"
                : $"\"{turn.Line}\" -> {turn.Summary}, {timings.TotalMilliseconds:0.0} ms";
            Console.WriteLine($"turn {_dialogue.TurnCount} ({turn.Npc}, {turn.Prompt}): {(turn.Input == InputKind.Spoken ? "spoken " : string.Empty)}{detail}, state {turn.AgentState}{(turn.HostEvents.Count > 0 ? $", host events {string.Join(" ", turn.HostEvents)}" : string.Empty)}{(turn.FailForward ? ", FAIL-FORWARD" : string.Empty)}");
            foreach (var line in turn.GuideLines)
            {
                Console.WriteLine($"  {turn.Npc}: {line}");
            }

            if (turn.SlowFollowUp && turn.FollowUp is { Count: > 0 } again)
            {
                // "Más despacio, por favor" (S17): her reply, then her last line again from the slow recordings.
                _voice.Say(turn.Npc, turn.GuideLines.Take(turn.GuideLines.Count - again.Count));
                foreach (var line in again)
                {
                    _voice.Append(turn.Npc, line, slow: true);
                }
            }
            else
            {
                _voice.Say(turn.Npc, turn.GuideLines); // the NPC's lines, in the order the panel shows them (S9)
            }

            if (_dialogue.Heard is { } heard && turn.Input == InputKind.Spoken)
            {
                Console.WriteLine($"  panel: {heard.Text}");
            }

            if (turn.Choices is { Count: > 0 })
            {
                Console.WriteLine($"  choices: {string.Join(" | ", _dialogue.Choices.Select(static c => c.Label))}");
            }

            if (turn.Pointer is { } arrow)
            {
                Console.WriteLine($"  arrow: {arrow.Place} at {arrow.Target.X},{arrow.Target.Y},{arrow.Target.Z} ({arrow.Blocks} blocks, {arrow.Direction})");
            }
        }
    }

    // The book's keys (S20, S20b): Escape or J closes it; Tab (Shift and Tab turns back), Left and Right turn the page; Page Up and Page Down scroll.
    // Up and Down move the selection among the rows with a speaker button (the selection is kept on screen); a page with none scrolls a line. Enter plays
    // the selected row, Shift+Enter its slow recording.
    private void ReadBookKeys(InputSnapshot input)
    {
        if (input.Pressed(Keys.Escape) || input.Pressed(Keys.J))
        {
            CloseBook();
            return;
        }

        if (input.Pressed(Keys.Tab))
        {
            _book.Turn(forward: !(input.Shift));
        }
        else if (input.Pressed(Keys.Left))
        {
            _book.Turn(forward: false);
        }
        else if (input.Pressed(Keys.Right))
        {
            _book.Turn(forward: true);
        }

        var geometry = new BookGeometry(_canvas.Width, _canvas.Height);
        var lines = geometry.LinesVisible;
        var shift = input.Shift;
        if (shift && (input.Pressed(Keys.Down) || input.Pressed(Keys.Up)))
        {
            // Shift with Up or Down scrolls the right page a line (X03); Shift with Page Up or Page Down, a page of it.
            _book.ScrollDetail(input.Pressed(Keys.Down) ? BookGeometry.LineHeight : -BookGeometry.LineHeight);
        }
        else if (shift && (input.Pressed(Keys.PageDown) || input.Pressed(Keys.PageUp)))
        {
            _book.ScrollDetail(input.Pressed(Keys.PageDown) ? geometry.DetailHeight : -geometry.DetailHeight);
        }
        else if (input.Pressed(Keys.PageDown))
        {
            _book.Scroll(lines);
        }
        else if (input.Pressed(Keys.PageUp))
        {
            _book.Scroll(-lines);
        }
        else if (input.Pressed(Keys.Down) || input.Pressed(Keys.Up))
        {
            _bookControls.Move(input.Pressed(Keys.Down) ? 1 : -1, new BookGeometry(_canvas.Width, _canvas.Height).LinesVisible);
        }
        else if (input.Pressed(Keys.Enter))
        {
            _ = _bookControls.Enter(input.Shift);
        }
        else if (input.Pressed(Keys.P) && _bookControls.PractiseFirst(new BookGeometry(_canvas.Width, _canvas.Height)) is { } practise)
        {
            PractiseAgain(practise);
        }
    }

    // "Practise again" in the book (S26): the lesson starts when the learner talks to its NPC. The book closes, the objective area says where to go and the beacon points there.
    private void PractiseAgain(string lessonId)
    {
        CloseBook();
        var line = _session.Lessons.ReplayHud ?? lessonId;
        _toasts.Show(line);
        Console.WriteLine($"book: practise again {lessonId}; {line}");
    }

    // The scripted run's book step (S26, --book-practise): opens the book on its Lessons page, then presses the lesson's button.
    private void PractiseFromScript(string lessonId)
    {
        if (!_book.IsOpen)
        {
            OpenBook();
        }

        _book.Show(BookPage.Lessons);
        if (_bookControls.Practise(lessonId) is { } asked)
        {
            PractiseAgain(asked);
        }
        else
        {
            Console.WriteLine($"book: '{lessonId}' cannot be practised again (it is not a completed lesson, or another lesson is under way)");
            CloseBook();
        }
    }

    // The scripted run's book step (S20b, --book-play and --book-play-slow): opens the book, then plays the target's row as Enter does.
    private void PlayBookTarget(string target, bool slow)
    {
        if (!_book.IsOpen)
        {
            OpenBook();
        }

        Console.WriteLine(_bookControls.PlayTarget(target, slow));
    }

    private void OpenBook()
    {
        _book.Open(_session.Words);
        if (_options.BookStart is { } start)
        {
            _book.Show(start, animate: false);
        }

        if (_options.BookVerb is { } verb)
        {
            Console.WriteLine(_bookControls.SelectVerb(verb) ? $"book: the table of {verb}" : $"book: no verb '{verb}' in the book");
        }

        if (_options.BookDetailLines > 0)
        {
            _book.ScrollDetail(_options.BookDetailLines * BookGeometry.LineHeight); // a scripted run's right page, scrolled down (X03)
        }

        _focus.BookOpen = true;
    }

    // The end of the lesson's recap (S17): the panel closes and the journal opens on its phrases page, where each phrase the lesson taught is listed with the help it has now.
    private void ShowPhrasesBook()
    {
        if (_scenario is not null)
        {
            return; // a scripted run goes on with its steps and asks for the journal itself (--journal --journal-phrases); an overlay would cover its screenshots
        }

        if (_dialogue.IsOpen)
        {
            CloseDialogue();
        }

        _book.Open(_session.Words);
        _book.Show(BookPage.Phrases);
        _focus.BookOpen = true;
    }

    private void CloseBook()
    {
        _book.Close();
        _focus.BookOpen = false;
    }

    private void OpenInventory()
    {
        if (_book.IsOpen)
        {
            return;
        }

        var lemmas = _sim.Inventory.Slots
            .Where(static s => !s.IsEmpty)
            .Select(s => _session.Nouns.ForItem(s.Item)?.Lemma)
            .OfType<string>()
            .ToList();
        _inventory.Open(_session.Words, lemmas);
        _focus.InventoryOpen = true;
    }

    private void CloseInventory()
    {
        _inventory.Close();
        _focus.InventoryOpen = false;
    }
}
