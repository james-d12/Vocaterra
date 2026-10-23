using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Vocaterra.Game;
using Vocaterra.Game.Reports;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Crafting;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame;

/// <summary>
/// The engine host of the 3D first-person scene, the prototype's only world (S2 to S4). Each frame it turns the real clock into whole ticks, reads the
/// keyboard and the mouse (or the scripted run) into <see cref="VoxelCommand"/>s and steps the <see cref="VoxelSession"/>'s simulation; then it
/// places the <see cref="FirstPersonCamera"/> at the interpolated eye, draws the voxel world with <see cref="VoxelRenderer"/> and the layers
/// over it. All rules live in <c>Vocaterra.Simulation</c>, <c>Vocaterra.Game</c> and the plain classes under <c>Frontend/</c>.
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
    /// <summary>The point the beacon stands on at the start of a new game: the guide's camp, at the foot of the trail from the player's own camp (S13).</summary>
    public const string StartBeaconPoint = "camp.guide";

    private const int StartWidth = 1280;
    private const int StartHeight = 720;
    private const int SettleTicks = 30;
    private const int RunawayTicks = 60 * 60 * 5;

    /// <summary>The save file F6 writes when the run has no <c>--save</c> file.</summary>
    private const string DefaultSavePath = "vocaterra.save.json";

    /// <summary>The folder of the session folder that holds the reports (S21); <c>--report-dir</c> replaces it.</summary>
    private const string ReportsFolder = "reports";

    private static readonly (Keys Key, PanelKey Panel)[] PanelKeys =
    [
        (Keys.R, PanelKey.R), (Keys.S, PanelKey.S), (Keys.H, PanelKey.H), (Keys.N, PanelKey.N), (Keys.W, PanelKey.W), (Keys.Tab, PanelKey.Tab),
        (Keys.D1, PanelKey.D1), (Keys.D2, PanelKey.D2), (Keys.D3, PanelKey.D3), (Keys.NumPad1, PanelKey.D1), (Keys.NumPad2, PanelKey.D2), (Keys.NumPad3, PanelKey.D3),
    ];

    /// <summary>The frames a post- or recall-check's thank-you screen stays up before the run ends.</summary>
    private const int CheckEndFrames = 90;

    /// <summary>The thank-you note of the playtest's post-check (the screen's closing lines; the English is for the facilitator and the tester alike).</summary>
    private const string PlaytestThankYou = "Thank you for playing. Your answers are in the session log.";

    /// <summary>The thank-you note of the next-day recall of a playtest.</summary>
    private const string RecallThankYou = "Thank you. Your answers are in the session log.";

    private static readonly (Keys Key, LessonKey Lesson)[] LessonKeys =
    [
        (Keys.Enter, LessonKey.Enter), (Keys.K, LessonKey.K), (Keys.L, LessonKey.L),
        (Keys.D1, LessonKey.D1), (Keys.D2, LessonKey.D2), (Keys.D3, LessonKey.D3), (Keys.D4, LessonKey.D4),
        (Keys.NumPad1, LessonKey.D1), (Keys.NumPad2, LessonKey.D2), (Keys.NumPad3, LessonKey.D3), (Keys.NumPad4, LessonKey.D4),
    ];

    private static readonly (Keys Key, Keys Pad)[] CheckOptionKeys = [(Keys.D1, Keys.NumPad1), (Keys.D2, Keys.NumPad2), (Keys.D3, Keys.NumPad3)];

    private static readonly Keys[] SlotKeys = [Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0];

    private readonly LaunchOptions _options;
    private readonly VoxelSession _session;
    private readonly Scene _scene;
    private readonly Func<Scene> _pristine;
    private readonly VoxelSimulation _sim;
    private readonly GraphicsDeviceManager _graphics;
    private readonly TickAccumulator _accumulator = new(VoxelSimulation.TicksPerSecond);
    private readonly FrameStats _stats = new();
    private readonly IFrameClock _clock;
    private readonly LookControl _look = new();
    private readonly CameraEase _ease = new();
    private readonly FirstPersonCamera _camera = new();
    private readonly ViewModeState _viewMode = new();
    private readonly Func<int, int, int, bool> _isSolid;
    private long _lastPlaceTick = long.MinValue / 2;
    private double _walkDistance;
    private (double X, double Z)? _lastWalkPoint;
    private readonly MouseLook _mouseLook = new();
    private readonly LiveVoxelInput _live = new();
    private readonly ScenarioScript? _scenario;
    private readonly InputFocus _focus = new();
    private readonly DialogueModel _dialogue;
    private readonly TurnPump _pump;
    private readonly PushToTalk _talk;
    private readonly SoundFrontend _sound;
    private readonly ISoundOutput _soundOutput;
    private readonly SoundLogWriter? _soundLog;
    private readonly SpeechActionRunner _actions;
    private readonly SupportFrontend _support;
    private readonly SessionEnding _ending = new();
    private bool _greetArmed = true;
    private readonly LessonFrontend _lesson;
    private bool _controlsOpen;
    private bool _aimedTipOffered;
    private readonly NpcVoice _voice;
    private readonly TurnWatchdog _watchdog = new();
    private readonly MicCheckModel? _micCheck;
    private readonly KeyRepeater _backspace = new();
    private readonly KeyRepeater _delete = new();
    private readonly KeyRepeater _left = new();
    private readonly KeyRepeater _right = new();
    private readonly BookModel _book = new();
    private readonly BookRecordings _recordings;
    private readonly BookSpeechControls _bookControls;
    private readonly ToastQueue _toasts = new();
    private readonly InventoryScreen _inventory = new();
    private readonly BusyFrameStats _draws = new();
    private readonly BusyFrameStats _frames = new();
    private readonly List<VoxelCommand> _commands = [];
    private readonly List<IFrameLayer> _layers = [];
    private readonly VoxelDebugOverlay _debug = new();
    private IReadOnlyList<NpcFigure> _figures = [];
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private BlockAtlas? _blockAtlas;
    private VoxelRenderer? _renderer;
    private QualityPreset _preset = QualityPreset.For(QualityLevel.High);
    private GreyscaleWorld? _colours;
    private FrameView? _view;
    private KeyboardState _previousKeys;
    private int _previousScroll;
    private bool _previousLeftDown;
    private int _previousWheel;
    private bool _previousRightDown;
    private bool _captureWanted = true;
    private bool _firstUpdateSeen;
    private Vector3 _previousEye;
    private Vector3 _currentEye;
    private double _previousEase;
    private double _currentEase;
    private int _settled;
    private bool _shown;
    private double _lastDrawEnd = -1;
    private Keys _listenKey = Keys.None;
    private char? _swallowedOpenKey;
    private int _swallowFrames;
    private NpcFigure? _aimed;
    private PointerMarker? _shownPointer;
    private readonly WordCardQueue _cards = new();
    private readonly CraftingScreen _crafting = new();
    private readonly Queue<int> _pendingCrafts = new();
    private int _loadingFrames;
    private bool _micScriptedSent;
    private int _micShownTicks;
    private int _printedEvents;
    private string? _shotPath;
    private CheckScreen? _check;
    private CheckScript? _checkScript;
    private SessionLog? _log;
    private bool _checkDone;
    private bool _endAfterCheck;
    private int _checkFramesLeft;
    private int _checkPlayedAt = -1;

    // The report panel (S21, D-game-17): F8 pauses the world and the next frame is kept in memory (the picture the player saw); nothing is written
    // until Ctrl+Enter, and Esc keeps nothing. A scripted report (--report, --report-open) is typed and saved by the script's frame.
    private readonly ReportPanel _report = new();
    private (Microsoft.Xna.Framework.Color[] Pixels, int Width, int Height)? _reportShot;
    private long _reportTick;
    private int _reportSession;
    private (string Text, bool Keep)? _scriptReport;
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
    public VoxelGame(LaunchOptions options, VoxelSession session, Scene scene, NpcVoice voice, Func<Scene> pristine, SoundVolumes? savedVolumes = null, SceneLayout? layout = null)
    {
        _options = options;
        _session = session;
        _scene = scene;
        _voice = voice;
        _pristine = pristine;
        _sim = session.Simulation;
        (_sound, _soundOutput, _soundLog) = CreateSound(options, session, savedVolumes, layout);
        _isSolid = (x, y, z) => _sim.Grid.IsSolid(x, y, z);
        // The book's speaker buttons (S20b): a row plays the recording the speech folder has for it (the files are read once, here).
        _recordings = new BookRecordings(SpeechLines.Enumerate(session.Bundle, session.Nouns, session.Phrases).Select(static l => l.LineId).Where(voice.HasRecording), session.Phrases);
        _bookControls = new BookSpeechControls(_book, new BookSpeaker(voice, _sound.Mixer), session, _recordings);
        _dialogue = new DialogueModel(_focus, session.OpeningLine, session.Phrases, session.English, showVerdictNotes: !options.Playtest);
        var microphone = options.UsesMicrophone ? MicrophoneCapture.TryOpen() : null;
        _talk = new PushToTalk(microphone) { Sounds = _sound.Mixer };
        _microphoneFound = microphone is not null;
        _pump = new TurnPump(session, _dialogue, CursorCell);
        _actions = new SpeechActionRunner(session, _dialogue, voice, CursorCell);
        _support = new SupportFrontend(session, _dialogue, _actions, Console.WriteLine);
        _lesson = new LessonFrontend(session, _dialogue, Console.WriteLine);
        _lesson.OnRecapDone = ShowPhrasesBook; // the lesson's recap ends on the journal's phrases page (S17)
        _ending.MarkSeen(session); // a game loaded after its ending does not play it again
        _micCheck = options.ShowsMicCheck ? new MicCheckModel(session.LogMicCheck) : null;
        _playtest = options.Playtest;
        _debug.Visible = !_playtest; // the playtest has no debug overlay (F3 is off there too)
        _look.Set(scene.Spawn.Yaw, scene.Spawn.Pitch);
        _currentEye = _previousEye = EyeBlocks();
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
                    RequestScriptReport,
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
        StartLogAndCheck(options);
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
        _session.Applied += OnAppliedTurn;
        if (options.Playtest)
        {
            _playtestSavePath = PlaytestEnd.SaveIn(directory);
            Console.WriteLine(options.IsPlaytestRecall
                ? $"playtest: the next-day recall of the save from session {_session.PlaySession - 1}; the run ends after the check"
                : $"playtest: the pre-check, the run, then the post-check when the camp is done, the fire at the player's camp is lit and the transfer offered (or F10); the save is {_playtestSavePath}");
        }

        if (options.Check is { } battery)
        {
            BeginBattery(battery, options.IsScreenshotRun, 0);
        }
    }

    // Starts a check battery: its plan from the bundle and the seed, its log line, and for a scripted run the script's answers from a step.
    private void BeginBattery(CheckKind battery, bool scripted, int scriptIndex)
    {
        // A battery starts fresh: the pre-check's end (_checkDone) must not stand for the post-check (S12).
        _checkDone = false;
        _endAfterCheck = false;
        _checkFramesLeft = 0;
        _checkPlayedAt = -1;
        var plan = CheckPlan.For(CheckBank.Load(_session.Bundle), battery, _session.WorldSeed);
        _check = new CheckScreen(battery, plan, MonotonicClock.Milliseconds, result => _log!.CheckResult(result, CheckKinds.Of(battery), _session.PlaySession, _sim.Tick));
        _log!.CheckStart(CheckKinds.Of(battery), _session.PlaySession, plan.Count);
        Console.WriteLine($"check: {CheckKinds.Of(battery)}, {plan.Count} items");
        if (!scripted)
        {
            return;
        }

        // A scripted run prints the order and the right option of each item, so that its --pick steps can be written (a console line only).
        var order = plan.Select(static e => e.Item.Kind == CheckItemKind.Comprehension ? $"{e.Item.Id} (pick {e.ShownCorrect + 1})" : e.Item.Id);
        Console.WriteLine($"check order: {string.Join(", ", order)}");
        var hooks = new CheckScriptHooks(
            picked => PickCheck(picked - 1),
            SubmitTypedCheck,
            SubmitSpeakCheck,
            path => _shotPath = path,
            () => _shotPath is not null);
        _checkScript = new CheckScript(_options.Script, _check, hooks, Console.WriteLine, scriptIndex);
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
        BeginBattery(CheckKind.Post, _options.IsScreenshotRun, _scenario?.Index ?? 0);
    }

    // The world is on screen: no check battery and no mic check (the playtest's triggers and its end key need that).
    private bool WorldRunning => _check is null && _micCheck is not { IsActive: true };

    /// <summary>The process exit code: 0 unless the screenshot could not be saved.</summary>
    public int ExitCode { get; private set; }

    /// <summary>The beacon: set it to send the player somewhere (a column stands on the block and an arrow points to it from the screen edge).</summary>
    public VoxelBeacon Beacon { get; } = new();

    /// <summary>The 3D simulation (read it; commands go through the game's input).</summary>
    public VoxelSimulation Simulation => _sim;

    /// <summary>The 3D game's session: the guide, the words and the arrow.</summary>
    public VoxelSession Session => _session;

    /// <inheritdoc />
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _previousScroll = Mouse.GetState().ScrollWheelValue;
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _blockAtlas = new BlockAtlas(GraphicsDevice);
        _figures = NpcFigures.FromScene(_scene);
        var started = MonotonicClock.Milliseconds();
        _preset = _options.QualityPreset;
        _renderer = new VoxelRenderer(GraphicsDevice, _blockAtlas, _sim.Grid, _preset, _options.Dusk);
        if (_options.ThirdPerson)
        {
            _viewMode.Set(CameraMode.ThirdPerson);
        }

        _renderer.SetFigures(_figures);
        _colours = new GreyscaleWorld(_options.Greyscale, _session.Nouns, _session.Words, _toasts, _renderer.SetGreyKinds, _options.AssumeKnown);
        Console.WriteLine($"voxel: quality {_preset.Name}, view distance {_preset.ViewDistance} blocks, fog from {VoxelRenderer.FogEndFor(_preset.ViewDistance) * VoxelRenderer.FogStartFraction:0} to {VoxelRenderer.FogEndFor(_preset.ViewDistance):0}, mesh budget {_preset.MeshBudgetMilliseconds:0} ms a frame (renderer ready in {MonotonicClock.Milliseconds() - started:0} ms)");
        Console.WriteLine($"graphics: {GraphicsAdapter.DefaultAdapter.Description}, depth {GraphicsDevice.PresentationParameters.DepthStencilFormat}");
        if (_options.Greyscale)
        {
            Console.WriteLine("greyscale: blocks whose noun is not known are grey (the playtest A/B, D-game-6); figures, sky and UI in colour");
        }
        Console.WriteLine($"scene: {_scene.Name}, seed {_scene.Seed}, {_scene.TreeCount} trees, spawn {_scene.Spawn.Feet.X},{_scene.Spawn.Feet.Y},{_scene.Spawn.Feet.Z}, digest {_scene.Grid.ComputeDigestHex()[..16]}");
        if (_options.Beacon is { } point && !string.Equals(point, "none", StringComparison.Ordinal))
        {
            if (Beacon.SetPoint(_scene, point, PointLabels.For(point)))
            {
                Console.WriteLine($"beacon: {point} at {Beacon.Target!.Value.X},{Beacon.Target.Value.Y},{Beacon.Target.Value.Z}");
            }
            else
            {
                Console.Error.WriteLine($"beacon: the scene has no point '{point}' (it has {string.Join(", ", _scene.Points.Keys)})");
            }
        }

        if (_options.Beacon is null && _options.LoadPath is null && Beacon.SetPoint(_scene, StartBeaconPoint, PointLabels.For(StartBeaconPoint)))
        {
            // The new game starts with a walk down the trail: the beacon stands on the guide's camp (until the opening of S17 says it in words).
            Console.WriteLine($"beacon: {StartBeaconPoint} at {Beacon.Target!.Value.X},{Beacon.Target.Value.Y},{Beacon.Target.Value.Z} (the walk down the trail)");
        }

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
            Camera3D = _camera,
            Beacon = Beacon,
            Figures = _figures,
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
            Console.WriteLine($"session complete: {_ending.Spanish} ({_ending.English}); {_session.Objective.Summary}");
        }

        if (!_report.Paused)
        {
            RunWatchdog(seconds); // a paused world does not time a turn out (S21)
        }

        DriveScriptedMicCheck();
        DriveScriptedReport(); // a scripted report is typed and saved here, while its panel is up (S21)
        if (_playtest && (_scenario is null || _scenario.AtStepBoundary)
            && PlaytestEnd.StartsOnItsOwn(true, WorldRunning, _playtestStarted, _session.SessionComplete, PlaytestEnd.TransferOffered(_session.Events), _dialogue.IsOpen))
        {
            StartPlaytestPost("the camp is done, the player's fire is lit and the transfer offered");
        }

        if (_playtestSaveDue && _session.CanSave)
        {
            _playtestSaved = WriteSave(_playtestSavePath!, "at the end of the playtest");
            _playtestSaveDue = !_playtestSaved;
        }

        DriveCheck();
        var steps = _accumulator.Advance(seconds);
        if (_check is not null)
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

        SyncBeacon();
        var free = _scenario is null && WorldRunning && !_dialogue.IsOpen && !_book.IsOpen && !_inventory.IsOpen && !_crafting.IsOpen && !_controlsOpen && _session.PendingTurns == 0;
        if (GuideApproach.ShouldGreet(_session, ref _greetArmed, free))
        {
            Console.WriteLine("approach: the guide greets the player and her lesson begins");
            OpenDialogueOn(NpcService.GuideAgent, null); // the player came up to her (S17): the same call the E key makes
        }

        _aimed = AimedFigure();
        if (_aimed is not null && !_aimedTipOffered)
        {
            _aimedTipOffered = true;
            _session.Tips.Offer("aimed_npc"); // the first time the crosshair is on someone (S16)
        }

        PrintEvents();

        // Settled: the world's script is done (or there is none) with no battery on screen, a battery that ends the run has shown its end, or a
        // battery's script rests. A pre-check is not an end: the world's script after it still has steps to take (S12, a screenshot waits for them).
        if (_check is null && (_scenario is null || _scenario.IsDone) || _checkDone && _endAfterCheck || _checkScript is { Rests: true } && _check is { IsDone: false })
        {
            _settled++;
        }

        if (_check is null && (_scenario is null || _scenario.IsDone))
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

        var label = Beacon.Label;
        if (Beacon.ClearIfReached(_sim.Player.CenterX / (double)VoxelPhysics.UnitsPerBlock, _sim.Player.CenterZ / (double)VoxelPhysics.UnitsPerBlock))
        {
            Console.WriteLine($"beacon: reached {label} at tick {_sim.Tick}");
        }

        base.Update(gameTime);
    }

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        var view = _view!;
        var viewport = GraphicsDevice.PresentationParameters;
        var width = viewport.BackBufferWidth;
        var height = viewport.BackBufferHeight;
        view.ViewportWidth = width;
        view.ViewportHeight = height;
        var drawStart = MonotonicClock.Milliseconds();

        var alpha = (float)_accumulator.Alpha;
        var eye = Vector3.Lerp(_previousEye, _currentEye, alpha);
        eye.Y += (float)(_previousEase + ((_currentEase - _previousEase) * alpha));
        var farPlane = VoxelRenderer.FarPlaneFor(_preset.ViewDistance);
        _camera.Update(eye, _look.YawSteps, _look.PitchSteps, width, height, farPlane);
        var shoulder = _viewMode.IsThirdPerson;
        if (shoulder)
        {
            // Over the right shoulder, three blocks back, and in front of any wall behind the player (S15).
            var back = ThirdPersonRig.Eye(
                _isSolid,
                (eye.X, eye.Y, eye.Z),
                (_camera.Forward.X, _camera.Forward.Y, _camera.Forward.Z),
                (_camera.Right.X, _camera.Right.Y, _camera.Right.Z));
            _camera.Update(new Vector3(back.X, back.Y, back.Z), _look.YawSteps, _look.PitchSteps, width, height, farPlane);
        }

        view.Eye = eye;
        view.Look = (_look.YawSteps, _look.PitchSteps);
        view.Fps = _stats.Fps;
        view.Aimed = _aimed;
        view.Check = _check;
        view.ControlsOpen = _controlsOpen;
        view.Report = _report;
        view.Loading = _session.Worker!.IsReady ? null : LoadingLayer.Text;
        _dialogue.SpeechUnavailable = _session.Worker.IsReady && !_session.Worker.CanHear;
        view.LoadingFrame = _loadingFrames++;
        var mouse = Mouse.GetState();
        view.Mouse = !_options.IsScreenshotRun && GraphicsDevice.Viewport.Bounds.Contains(mouse.Position) ? mouse.Position : null;

        view.LessonStripBox = LessonLayer.LayoutFor(view, _lesson) is { } lessonStrip ? lessonStrip.Box : null; // the cards keep clear of it (S22)

        _colours!.Refresh();
        // A scripted run builds every mesh it needs before the picture; a live one spends its budget a frame (more while the world is still loading in).
        _renderer!.Remesh(_camera.Eye, _camera.Frustum, _options.IsScreenshotRun ? null : _session.Worker!.IsReady ? _preset.MeshBudgetMilliseconds : 30);
        GraphicsDevice.Clear(_renderer.ClearColour);
        _renderer.DrawWorld(_camera, _sim.Target?.Block, Beacon.Target, _sim.Tick, shoulder ? PlayerAvatarNow() : null, shoulder ? null : ArmFrameNow(eye));

        var spriteBatch = _spriteBatch!;
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        foreach (var layer in _layers)
        {
            layer.Draw(spriteBatch, view);
        }

        spriteBatch.End();
        if (_report.Requested)
        {
            // F8's frame: the picture as the player saw it, before the report panel is drawn (the panel opens after this frame).
            TakeReportPicture();
        }

        var drawEnd = MonotonicClock.Milliseconds();
        _draws.Record(drawEnd - drawStart);
        if (_lastDrawEnd >= 0 && _sim.Tick > 2)
        {
            _frames.Record(drawEnd - _lastDrawEnd);
        }

        _lastDrawEnd = drawEnd;
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
                || (CheckRestsForShot && _settled >= SettleTicks)))
        {
            ExitCode = ScreenshotWriter.TrySave(GraphicsDevice, path) ? 0 : 1;
            Console.WriteLine($"scene draw (voxel world, layers, flush): {_draws.Summary()}");
            Console.WriteLine($"frame time (end of one draw to the next, no vsync): {_frames.Summary()}");
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
            _ = WriteSave(savePath, "at the end of the run");
        }

        if (_playtest && !_options.IsPlaytestRecall && !_playtestSaved && _playtestSavePath is { } playtestSave)
        {
            DrainForSave();
            _playtestSaved = WriteSave(playtestSave, "at the quit (the playtest ended before its post-check)");
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

    private Vector3 EyeBlocks()
    {
        var body = _sim.Player;
        const float U = VoxelPhysics.UnitsPerBlock;
        return new Vector3(body.EyeX / U, body.EyeY / U, body.EyeZ / U);
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
        foreach (var command in _commands)
        {
            if (command.Kind == VoxelCommandKind.Place)
            {
                _lastPlaceTick = _sim.Tick; // the arm swings once (S15)
            }
        }

        // The sounds of the tick (S24): they read the tick's events and write nothing back.
        _sound.ObserveSimulation(_sim.Events, (x, y, z) => _sim.Grid.GetBlock(x, y, z));
        _sound.ObserveWorld(_sim.Tick, _sim.Events, _sim.Player, (x, y, z) => _sim.Grid.GetBlock(x, y, z), _session.FireLit); // the footsteps and the beds (S25)
        _sound.Scripted.Tick(); // the cues of the frame play together in UpdateSound, so an answer's chime and a step's chime fold into one

        UpdateFigures();
        _ease.Tick(_sim.Player.SteppedUp);
        _previousEye = _currentEye;
        _currentEye = EyeBlocks();
        _previousEase = _currentEase;
        _currentEase = _ease.Offset;
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
        if (_swallowFrames > 0 && --_swallowFrames == 0)
        {
            _swallowedOpenKey = null;
        }

        var keys = Keyboard.GetState();
        var mouse = Mouse.GetState();
        bool Pressed(Keys key) => keys.IsKeyDown(key) && !_previousKeys.IsKeyDown(key);

        // The report panel (S21) takes every key and click while it is open; nothing else in the game reads the input then. F8 opens it from anywhere.
        if (_report.IsOpen)
        {
            if (IsActive)
            {
                ReadReportInput(keys, mouse, Pressed);
            }

            EndInput(keys, mouse);
            return;
        }

        if (IsActive && Pressed(Keys.F8))
        {
            RequestReport();
            EndInput(keys, mouse);
            return;
        }

        if (IsActive)
        {
            if (_playtest && _endAfterCheck && _checkDone && Pressed(Keys.Escape))
            {
                Exit(); // the playtest's thank-you screen: Esc closes the game (Esc always works)
                _previousKeys = keys;
                return;
            }

            if (_playtest && Pressed(Keys.F10) && PlaytestEnd.EndKeyStarts(true, WorldRunning, _playtestStarted))
            {
                StartPlaytestPost("the end key");
            }

            if (Pressed(Keys.F5))
            {
                var mode = _viewMode.Toggle(); // the behind-the-shoulder view and back (S15)
                Console.WriteLine($"view: {(mode == CameraMode.ThirdPerson ? "behind the shoulder" : "first person")}");
            }

            if (Pressed(Keys.F6))
            {
                SaveNow();
            }

            if (_micCheck is { IsActive: true })
            {
                ReadMicCheckKeys(keys, Pressed);
            }
            else if (_check is { IsDone: false })
            {
                ReadCheckKeys(keys, Pressed);
            }
            else if (_dialogue.IsOpen)
            {
                ReadDialogueKeys(keys, Pressed);
            }
            else if (_controlsOpen)
            {
                if (Pressed(Keys.Escape) || Pressed(Keys.F1))
                {
                    _controlsOpen = false; // the controls help (F1, S16)
                }
            }
            else if (_book.IsOpen)
            {
                ReadBookKeys(keys, Pressed);
            }
            else if (_inventory.IsOpen)
            {
                if (Pressed(Keys.Escape) || Pressed(Keys.I) || Pressed(Keys.E))
                {
                    CloseInventory();
                }
            }
            else if (_crafting.IsOpen)
            {
                if (Pressed(Keys.Escape) || Pressed(Keys.C))
                {
                    CloseCrafting();
                }
            }
            else if (_aimed is not null && (Pressed(Keys.E) || Pressed(Keys.T) || Pressed(Keys.V)))
            {
                // The crosshair is on an NPC: E or T opens the panel; V opens it and starts listening.
                if (Pressed(Keys.V))
                {
                    OpenDialogueOn(_aimed.Name, null);
                    BeginListening(Keys.V);
                }
                else
                {
                    OpenDialogueOn(_aimed.Name, Pressed(Keys.T) ? 't' : 'e');
                }
            }
            else if (Pressed(Keys.F1))
            {
                _controlsOpen = true;
            }
            else if (Pressed(Keys.Enter) && _session.Tips.Current is not null)
            {
                _session.Tips.Dismiss(); // Enter: next tip (S16)
            }
            else if (Pressed(Keys.J))
            {
                OpenBook();
            }
            else if (Pressed(Keys.C) || (Pressed(Keys.E) && AimedAtWorkbench()))
            {
                OpenCrafting();
            }
            else if (Pressed(Keys.I) || Pressed(Keys.E))
            {
                OpenInventory();
            }
            else if (Pressed(Keys.Escape))
            {
                _captureWanted = false;
                _ending.Dismiss(); // Esc takes the closing card down (S17)
            }

            if (Pressed(Keys.F3) && !_playtest)
            {
                _debug.Visible = !_debug.Visible;
            }
        }

        var uiOpen = _book.IsOpen || _inventory.IsOpen || _dialogue.IsOpen || _crafting.IsOpen || _controlsOpen || _micCheck is { IsActive: true };
        var leftDown = mouse.LeftButton == ButtonState.Pressed;
        var rightDown = mouse.RightButton == ButtonState.Pressed;
        // The wheel scrolls the book (three lines a notch, up for a turn up); a click on a tab turns to that page (S20).
        var bookWheel = mouse.ScrollWheelValue;
        if (IsActive && _book.IsOpen && bookWheel != _previousWheel)
        {
            _book.Scroll(-(int)Math.Round((bookWheel - _previousWheel) / 40.0));
        }

        _previousWheel = bookWheel;
        if (IsActive && _book.IsOpen && leftDown && !_previousLeftDown)
        {
            if (new BookGeometry(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height).TabAt(mouse.X, mouse.Y) is { } tab)
            {
                _book.Show(tab);
                _sound.QueueClick(SoundCue.UiTab);
            }
            else if (_bookControls.Click(new BookGeometry(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), mouse.X, mouse.Y, ShiftDown(keys)))
            {
                _sound.QueueClick(SoundCue.UiClick);
            }
            else if (_bookControls.ClickVerb(new BookGeometry(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), mouse.X, mouse.Y))
            {
                _sound.QueueClick(SoundCue.UiClick);
            }
            else if (_bookControls.ClickPractise(new BookGeometry(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), mouse.X, mouse.Y) is { } practise)
            {
                _sound.QueueClick(SoundCue.UiClick);
                PractiseAgain(practise);
            }
        }

        if (IsActive && _crafting.IsOpen && leftDown && !_previousLeftDown)
        {
            // A click on a row of the panel asks for one craft in the next tick (the same command a script sends).
            var layout = new CraftingLayout(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, _session.Recipes.Entries.Count);
            if (layout.RowAt(mouse.X, mouse.Y) is { } row)
            {
                if (_sim.CanCraft(row) != CraftResult.Done)
                {
                    _sound.Queue(SoundCue.CraftRefused); // soft: the ingredients are not there yet (S24)
                }

                _pendingCrafts.Enqueue(row);
            }
        }

        if (IsActive && leftDown && !_previousLeftDown)
        {
            ClickOnScreens(mouse.X, mouse.Y);
        }

        var swallowClick = false;
        if (IsActive && !uiOpen && !_captureWanted && leftDown && !_previousLeftDown)
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
            strafe = (keys.IsKeyDown(Keys.D) || keys.IsKeyDown(Keys.Right) ? 1 : 0) - (keys.IsKeyDown(Keys.A) || keys.IsKeyDown(Keys.Left) ? 1 : 0);
            forward = (keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.Up) ? 1 : 0) - (keys.IsKeyDown(Keys.S) || keys.IsKeyDown(Keys.Down) ? 1 : 0);
            jump = keys.IsKeyDown(Keys.Space);
        }

        if (!uiOpen)
        {
            for (var i = 0; i < SlotKeys.Length; i++)
            {
                if (Pressed(SlotKeys[i]))
                {
                    slot = i;
                }
            }

            var wheel = mouse.ScrollWheelValue - _previousScroll;
            if (wheel != 0)
            {
                slot = (((_sim.SelectedSlot - Math.Sign(wheel)) % Inventory.HotbarSize) + Inventory.HotbarSize) % Inventory.HotbarSize;
            }
        }

        var target = _sim.Target;
        // Using the book item (selected, right-click) opens the book, as J does (S20). The item is not placeable, so the press places nothing.
        if (IsActive && captured && rightDown && !_previousRightDown && !_book.IsOpen && _sim.SelectedStack is { Item: ItemId.Book, Count: > 0 })
        {
            OpenBook();
        }

        _live.SetFrame(
            strafe,
            forward,
            jump,
            captured && leftDown && !swallowClick ? LiveVoxelInput.GatherFor(target) : null,
            captured && rightDown && !_previousRightDown ? LiveVoxelInput.PlacementFor(_sim.SelectedStack, target) : null,
            slot);
        _previousLeftDown = leftDown;
        _previousRightDown = rightDown;
        _previousScroll = mouse.ScrollWheelValue;
        _previousKeys = keys;
    }

    // ---- The bug and feature report (S21, D-game-17): local files only, nothing is sent. ----

    // F8: the game pauses. A recording in progress is dropped (the microphone is off), and the next frame is kept as the picture the player saw.
    private void RequestReport()
    {
        CancelListening();
        if (_check is { Listening: true })
        {
            CancelCheckListening();
        }

        if (_micCheck is { Phase: MicCheckPhase.Listening } micCheck)
        {
            micCheck.EndListening(); // the same as a clip too short to hear: the check asks for another try, and nothing is sent
            micCheck.HeardNothing();
        }

        _report.Request(OpenScreenName());
        Console.WriteLine($"report: F8 at tick {_sim.Tick} ({_report.OpenPanel})");
    }

    // Draw, after the frame of F8 has been drawn without the panel: the picture is kept in memory until the report is saved or cancelled.
    private void TakeReportPicture()
    {
        _reportShot = ScreenshotWriter.Capture(GraphicsDevice);
        _reportTick = _sim.Tick;
        _reportSession = _session.PlaySession;
        _report.Show(_options.ReportKind);
    }

    // A scripted report (--report, --report-open): the panel opens at the next frame and the text is typed there (DriveScriptedReport).
    private void RequestScriptReport(string text, bool keep)
    {
        _report.Request(OpenScreenName());
        _scriptReport = (text, keep);
        Console.WriteLine($"report: scripted, {(keep ? "the panel is left open" : "saved")} ({_report.OpenPanel})");
    }

    // The scripted report's text, typed once the panel is up; a report that is not left open is saved at once.
    private void DriveScriptedReport()
    {
        if (_scriptReport is not { } script || !_report.IsOpen)
        {
            return;
        }

        _scriptReport = null;
        _report.TypeText(script.Text);
        if (!script.Keep)
        {
            SaveReport();
        }
    }

    // The panel's keys and clicks while it is open: Ctrl+Enter saves, Esc cancels (nothing is kept), Tab switches Bug and Feature request, and the
    // keys edit the text. The repeats of the editing keys are the dialogue's (KeyRepeater).
    private void ReadReportInput(KeyboardState keys, MouseState mouse, Func<Keys, bool> pressed)
    {
        if (_backspace.Step(keys.IsKeyDown(Keys.Back)))
        {
            _ = _report.Press(ReportKey.Backspace);
        }

        if (_delete.Step(keys.IsKeyDown(Keys.Delete)))
        {
            _ = _report.Press(ReportKey.Delete);
        }

        if (_left.Step(keys.IsKeyDown(Keys.Left)))
        {
            _ = _report.Press(ReportKey.Left);
        }

        if (_right.Step(keys.IsKeyDown(Keys.Right)))
        {
            _ = _report.Press(ReportKey.Right);
        }

        if (pressed(Keys.Up))
        {
            _ = _report.Press(ReportKey.Up);
        }

        if (pressed(Keys.Down))
        {
            _ = _report.Press(ReportKey.Down);
        }

        if (pressed(Keys.Home))
        {
            _ = _report.Press(ReportKey.Home);
        }

        if (pressed(Keys.End))
        {
            _ = _report.Press(ReportKey.End);
        }

        var outcome = ReportOutcome.None;
        if (pressed(Keys.Escape))
        {
            outcome = _report.Press(ReportKey.Escape);
        }
        else if (pressed(Keys.Enter))
        {
            var ctrl = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
            outcome = _report.Press(ctrl ? ReportKey.Save : ReportKey.Enter);
        }
        else if (pressed(Keys.Tab))
        {
            _ = _report.Press(ReportKey.Tab);
        }

        var leftDown = mouse.LeftButton == ButtonState.Pressed;
        if (outcome == ReportOutcome.None && leftDown && !_previousLeftDown
            && new ReportLayout(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height).ButtonAt(mouse.X, mouse.Y) is { } button)
        {
            outcome = _report.Click(button);
        }

        switch (outcome)
        {
            case ReportOutcome.Save:
                SaveReport();
                break;
            case ReportOutcome.Cancel:
                CancelReport();
                break;
        }
    }

    // Esc: the report is dropped with its picture, and nothing is written.
    private void CancelReport()
    {
        _report.Hide();
        _reportShot = null;
        Console.WriteLine("report: cancelled; nothing was kept");
    }

    // Ctrl+Enter (or the Save button): the Markdown and the PNG are written to the reports folder, named by the session, the tick and the kind. A
    // write that fails keeps the panel open, with the reason on a toast.
    private void SaveReport()
    {
        if (_reportShot is not { } shot)
        {
            _report.Hide();
            return;
        }

        var kind = _report.Kind;
        var directory = _options.ReportDir ?? Path.Combine(_sessionDirectory, ReportsFolder);
        try
        {
            Directory.CreateDirectory(directory);
            var stem = ReportNaming.Choose(_reportSession, _reportTick, kind, name => File.Exists(Path.Combine(directory, name)));
            var picture = stem + ".png";
            ScreenshotWriter.SaveImage(GraphicsDevice, shot.Pixels, shot.Width, shot.Height, Path.Combine(directory, picture));
            var report = new ReportSnapshot(
                kind,
                _report.Text,
                _reportSession,
                _reportTick,
                WallClock.LocalNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                picture,
                BuildReportState(),
                _session.Learner.Recent(ReportBounds.RecentTurns),
                ReadSessionLogTail());
            var markdownPath = Path.GetFullPath(Path.Combine(directory, stem + ".md"));
            File.WriteAllText(markdownPath, ReportMarkdown.Build(report), new System.Text.UTF8Encoding(false));
            _report.Hide();
            _reportShot = null;
            _toasts.Show($"Report saved: {markdownPath}");
            _sound.Queue(SoundCue.ReportSaved);
            Console.WriteLine($"report saved: {markdownPath} and {picture} ({ReportKinds.Word(kind)}, panel: {report.State.OpenPanel})");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            _toasts.Show($"Report not saved: {ex.Message}");
            Console.Error.WriteLine($"report not saved: {ex.Message}");
        }
    }

    // The game's state for the report's table: the build, the bundle, the seed, where the player stands and looks, the objective and the lesson, what
    // was open, the settings and the speech.
    private ReportGameState BuildReportState()
    {
        var body = _sim.Player;
        var worker = _session.Worker;
        var speech = _options.NoSpeech
            ? "off (--no-speech)"
            : worker is null || !worker.IsReady
                ? "models still loading"
                : worker.CanHear
                    ? "models found, speech ready"
                    : worker.Load?.Problem is { } problem
                        ? $"models not loaded: {problem}"
                        : "models not loaded in this run";
        var microphone = _microphoneFound ? "found" : _options.UsesMicrophone ? "not found" : "not used in this run";
        var lesson = _session.Lessons.Active is { } active
            ? $"{active.Id}, step {_session.Lessons.StepNumber} of {active.Steps.Count}"
            : "none";
        return new ReportGameState(
            BuildCommit.Read(),
            _session.Bundle.Manifest.BundleId,
            _session.Bundle.ContentHash,
            _session.WorldSeed,
            body.CenterX / (double)VoxelPhysics.UnitsPerBlock,
            body.FeetY / (double)VoxelPhysics.UnitsPerBlock,
            body.CenterZ / (double)VoxelPhysics.UnitsPerBlock,
            _look.YawSteps * 360.0 / Angles.StepsPerTurn,
            _look.PitchSteps * 360.0 / Angles.StepsPerTurn,
            _session.Objective.Summary,
            _session.Objective.Steps.Select(static step => (step.Text, step.Done)).ToList(),
            lesson,
            _report.OpenPanel,
            _preset.Name,
            _options.Greyscale,
            speech,
            microphone);
    }

    // The newest lines of this folder's session log (the file is flushed a line at a time, so it is complete up to the last turn).
    private IReadOnlyList<string> ReadSessionLogTail()
    {
        var path = Path.Combine(_sessionDirectory, SessionLog.SessionFileName);
        return File.Exists(path) ? File.ReadLines(path).TakeLast(ReportBounds.LogLines).ToList() : Array.Empty<string>();
    }

    // What is open now, in the words the report's table uses.
    private string OpenScreenName()
    {
        if (_check is { IsDone: false })
        {
            return $"check ({CheckKinds.Of(_check.Kind)})";
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
    private void EndInput(KeyboardState keys, MouseState mouse)
    {
        _mouseLook.SetCaptured(false, Window);
        IsMouseVisible = true;
        _previousKeys = keys;
        _previousLeftDown = mouse.LeftButton == ButtonState.Pressed;
        _previousRightDown = mouse.RightButton == ButtonState.Pressed;
        _previousScroll = mouse.ScrollWheelValue;
        _previousWheel = mouse.ScrollWheelValue;
    }

    // Whether Alt is held: an accent is Alt plus a vowel (AccentShortcuts), in every text box.
    private static bool AltDown()
    {
        var keys = Keyboard.GetState();
        return keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);
    }

    // The panel's keys while it is open. In the speech-button mode R, S, H, N, W (or Tab), 1 to 3 press the buttons, V or Space held talks and Enter
    // presses the Escribir button when typing is on offer; in the typing mode Enter sends, Tab or Esc go back to the buttons and the editing keys repeat.
    // Esc undoes the newest thing first: a recording, a turn that is thinking, the typing mode, and last the panel itself.
    private void ReadDialogueKeys(KeyboardState keys, Func<Keys, bool> pressed)
    {
        if (_talk.Listening && !keys.IsKeyDown(_listenKey))
        {
            EndListening();
        }

        if (pressed(Keys.Escape))
        {
            if (_talk.Listening)
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
            if (pressed(Keys.Tab))
            {
                _dialogue.EndTyping();
                return;
            }

            if (pressed(Keys.Enter))
            {
                _dialogue.RequestSubmit();
            }

            var entry = _dialogue.Entry;
            if (_backspace.Step(keys.IsKeyDown(Keys.Back)))
            {
                _ = entry.Backspace();
            }

            if (_delete.Step(keys.IsKeyDown(Keys.Delete)))
            {
                _ = entry.Delete();
            }

            if (_left.Step(keys.IsKeyDown(Keys.Left)))
            {
                entry.MoveLeft();
            }

            if (_right.Step(keys.IsKeyDown(Keys.Right)))
            {
                entry.MoveRight();
            }

            return;
        }

        // The lesson strip's keys (S16): L starts the lesson on offer, Enter continues, K skips the step, 1 to 4 pick an answer of a listen step.
        foreach (var (key, lessonKey) in LessonKeys)
        {
            if (pressed(key) && _lesson.PressKey(lessonKey))
            {
                return;
            }
        }

        if (pressed(Keys.V) || pressed(Keys.Space))
        {
            BeginListening(pressed(Keys.V) ? Keys.V : Keys.Space);
            return;
        }

        if (pressed(Keys.Enter) && _dialogue.TypingOffered)
        {
            PerformAction(SpeechAction.Type, null);
            return;
        }

        foreach (var (key, panelKey) in PanelKeys)
        {
            if (pressed(key) && SpeechButtons.ForKey(panelKey) is { } action)
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
                _swallowedOpenKey = typed;
                _swallowFrames = 2;
            }

            _watchdog.Reset();
        }
    }

    // The mouse went down at a point: a button of the mic check, or a button or choice of the panel.
    private void ClickOnScreens(int x, int y)
    {
        var font = _view!.Fonts.Get(FontProvider.NativeSize);
        if (_micCheck is { IsActive: true } check)
        {
            var layout = MicCheckLayer.LayoutFor(check, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, font);
            if (layout.ButtonAt(x, y) is { } button)
            {
                PressMicButton(button);
            }

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

            var layout = DialoguePanel.LayoutFor(_dialogue, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, font, _session);
            if (layout.ActionAt(x, y) is { } action)
            {
                PerformAction(action, null);
            }
        }
    }

    // The mic check's keys: hold V or Space to talk, Enter or Space to go on after a pass, Esc to skip.
    private void ReadMicCheckKeys(KeyboardState keys, Func<Keys, bool> pressed)
    {
        var check = _micCheck!;
        if (_talk.Listening && !keys.IsKeyDown(_listenKey))
        {
            EndMicListening();
        }

        if (pressed(Keys.Escape))
        {
            if (_talk.Listening)
            {
                _ = _talk.End();
            }

            check.Skip();
            return;
        }

        switch (check.Phase)
        {
            case MicCheckPhase.Passed:
                if (pressed(Keys.Enter) || pressed(Keys.Space))
                {
                    check.Continue();
                }

                break;
            case MicCheckPhase.Ready:
            case MicCheckPhase.Failed:
                if (pressed(Keys.V) || pressed(Keys.Space))
                {
                    BeginMicListening(pressed(Keys.V) ? Keys.V : Keys.Space);
                }
                else if (pressed(Keys.Enter) && check.Phase == MicCheckPhase.Failed)
                {
                    check.Retry();
                }

                break;
            default:
                break;
        }
    }

    private void PressMicButton(MicCheckButton button)
    {
        _sound.QueueClick(SoundCue.UiClick);
        if (button == MicCheckButton.Skip && _talk.Listening)
        {
            _ = _talk.End();
        }

        _micCheck!.Press(button);
    }

    // The mic check: the talk key went down. The recognizer must be loaded and the machine must have a microphone; otherwise the screen says why and
    // the player can skip: nothing here ever stops the game.
    private void BeginMicListening(Keys key)
    {
        var check = _micCheck!;
        var worker = _session.Worker!;
        if (!worker.CanHear)
        {
            check.Notice = worker.IsReady ? worker.Load?.Problem ?? "Speech is not available." : "Loading speech…";
            return;
        }

        if (_talk.Begin())
        {
            _listenKey = key;
            check.Notice = null;
            check.BeginListening();
        }
        else
        {
            check.Notice = _talk.Problem;
        }
    }

    // The mic check: the key went up (or the recording hit its cap). The clip goes to the worker, which hears it and answers with what it heard.
    private void EndMicListening()
    {
        var check = _micCheck!;
        var clip = _talk.End();
        check.EndListening();
        if (clip is null)
        {
            check.Notice = _talk.Problem;
            check.HeardNothing();
            return;
        }

        _ = _session.SubmitHear(clip);
        Console.WriteLine($"mic check: {clip.DurationSeconds:0.00} s heard by the recognizer");
    }

    // A scripted mic check (no microphone): once the recognizer is loaded the worker synthesizes "hola" and hears it, as if it had been said.
    private void DriveScriptedMicCheck()
    {
        if (_micCheck is not { IsActive: true } check || !_options.IsScreenshotRun)
        {
            return;
        }

        var worker = _session.Worker!;
        if (!_micScriptedSent && worker.IsReady)
        {
            _micScriptedSent = true;
            if (worker.CanHear)
            {
                _sound.Scripted.Begin(); // the scripted check stands for a recording: beep, silent window, beep (S24)
                _ = _session.SubmitHearSpeak(MicCheck.Word);
                check.BeginScripted();
            }
            else
            {
                check.BeginScripted();
                check.Complete(new MicCheckResult(string.Empty, false, 0));
            }
        }

        // With steps to run after it (or a check battery waiting behind it, S12: the playtest's pre-check), the scripted check goes on by itself once
        // its result has been on screen for a moment; alone it stays up for the screenshot.
        if (check.Phase is MicCheckPhase.Passed or MicCheckPhase.Failed && (_scenario is { IsDone: false } || _check is { IsDone: false }) && ++_micShownTicks >= 60)
        {
            check.Press(check.Phase == MicCheckPhase.Passed ? MicCheckButton.Continue : MicCheckButton.Skip);
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
            // The report's text box (S21): every letter, with the accents of Alt; the control characters (Enter, Tab, Ctrl+Enter) are keys.
            if (!char.IsControl(args.Character))
            {
                _report.Type(args.Character, AltDown());
            }

            return;
        }

        if (_check is { IsDone: false, IsTyping: true } typing)
        {
            // The check's text box (S11): the key that opened it (W) is swallowed, the rest is typed.
            if (_swallowedOpenKey is { } pendingKey && char.ToLowerInvariant(args.Character) == pendingKey)
            {
                _swallowedOpenKey = null;
                return;
            }

            if (!char.IsControl(args.Character))
            {
                _ = typing.Entry.Insert(AccentShortcuts.ForTyped(args.Character, AltDown()));
            }

            return;
        }

        if (!_dialogue.IsTyping || char.IsControl(args.Character) || _talk.Listening)
        {
            if (_dialogue.IsOpen && _swallowedOpenKey is { } pending && char.ToLowerInvariant(args.Character) == pending)
            {
                _swallowedOpenKey = null;
            }

            return;
        }

        if (_swallowedOpenKey is { } opener && char.ToLowerInvariant(args.Character) == opener)
        {
            _swallowedOpenKey = null;
            return;
        }

        _ = _dialogue.Entry.Insert(AccentShortcuts.ForTyped(args.Character, AltDown())); // Alt plus a vowel is the accented letter (S21 wired the hints' keys)
    }

    // Opens the panel on an NPC: its opening is a core host turn when its state has one (the shopkeeper's question, the villager's hello, the
    // guide's transfer offer), otherwise its prompt's text, and the guide's review question when one is due. The key that opened the panel is
    // swallowed by the text event.
    private void OpenDialogueOn(string npc, char? openedBy)
    {
        var opening = _session.OpenTalk(npc, teachKeyWords: true);
        _dialogue.Open(opening, NpcFigures.LabelOf(npc), _session.ReviewLine);
        _voice.Say(npc, opening.Lines);
        if (openedBy == 't')
        {
            _dialogue.BeginTyping(); // T is "type": the text box has the keyboard from the start
        }

        _swallowedOpenKey = openedBy;
        _swallowFrames = 2; // the platform types the key once, in the next frame at the latest
        var how = opening.HostTurn ? "host turn" : "prompt";
        Console.WriteLine($"talk: {npc} ({how}, {opening.Prompt}): {(_session.KeyWordsPending ? $"[key words: {string.Join(", ", _session.KeyWordCards.Select(static c => c.Phrase))}]" : string.Join(" / ", opening.Lines))}{(_session.ReviewLine is { } review ? $" / {review}" : string.Empty)}");
    }

    // Once a tick (S15): the figures in a conversation face the player and gesture, the others stand as the scene put them, and the distance the player has walked
    // (the stride of the figure seen over the shoulder) grows.
    private void UpdateFigures()
    {
        var px = _sim.Player.CenterX / (double)VoxelPhysics.UnitsPerBlock;
        var pz = _sim.Player.CenterZ / (double)VoxelPhysics.UnitsPerBlock;
        foreach (var figure in _figures)
        {
            figure.Talking = _dialogue.IsOpen && string.Equals(_dialogue.Npc, figure.Name, StringComparison.Ordinal);
            FigureFacing.Update(figure, px, pz);
        }

        if (_lastWalkPoint is { } last)
        {
            _walkDistance += Math.Sqrt(((px - last.X) * (px - last.X)) + ((pz - last.Z) * (pz - last.Z)));
        }

        _lastWalkPoint = (px, pz);
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

    // The new game events since the last frame, one console line each (the phrase uses, the voice events, the world events).
    private void PrintEvents()
    {
        var events = _session.Events;
        for (; _printedEvents < events.Count; _printedEvents++)
        {
            Console.WriteLine($"  event: {events[_printedEvents].Text}");
            foreach (var line in _support.LinesFor(events[_printedEvents]))
            {
                Console.WriteLine(line);
            }
        }
    }

    // The tile for "¿qué es esto?": the block under the crosshair (the scripted run has the same crosshair).
    private GuideCell? CursorCell() => _session.AimedCell;

    // The player's own figure for the behind-the-shoulder view: at the feet of the body, facing the look (S15).
    private PlayerAvatar PlayerAvatarNow()
    {
        var body = _sim.Player;
        var moving = body.VelocityX != 0 || body.VelocityZ != 0;
        return new PlayerAvatar(
            (float)(body.CenterX / (double)VoxelPhysics.UnitsPerBlock),
            (float)(body.FeetY / (double)VoxelPhysics.UnitsPerBlock),
            (float)(body.CenterZ / (double)VoxelPhysics.UnitsPerBlock),
            (float)(_look.YawSteps * Math.Tau / Angles.StepsPerTurn),
            (float)(_look.PitchSteps * Math.Tau / Angles.StepsPerTurn),
            (float)(_walkDistance * 2.6),
            moving);
    }

    // The first-person arm: what the hand holds, how it moves and the light on it (S15).
    private ArmFrame ArmFrameNow(Vector3 eye)
    {
        var held = HeldItems.For(_sim.SelectedStack);
        var moving = _sim.Player.VelocityX != 0 || _sim.Player.VelocityZ != 0;
        var motion = ArmMotion.At(_sim.Tick, moving, _sim.GatherProgress, _sim.Tick - _lastPlaceTick);
        var light = ArmLight.At(_isSolid, (eye.X, eye.Y, eye.Z), _renderer!.Lights, held.Kind == HeldKind.Torch, _options.Dusk);
        return new ArmFrame(held, motion, light);
    }

    // The NPC figure under the crosshair, when it is within reach and no block is in the way: the one E talks to.
    private NpcFigure? AimedFigure()
    {
        if (_dialogue.IsOpen)
        {
            return null;
        }

        var eye = (x: (double)_currentEye.X, y: (double)_currentEye.Y, z: (double)_currentEye.Z);
        foreach (var figure in _figures)
        {
            var point = FigureAim.BodyOf(figure);
            if (FigureAim.Hits(eye, _look.Yaw, _look.Pitch, point)
                && !NpcLabelLayer.Occluded(_sim, _currentEye, ((float)point.X, (float)point.Y, (float)point.Z), FigureAim.Distance(eye, point)))
            {
                return figure;
            }
        }

        return null;
    }

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

        if (_talk.Begin())
        {
            _listenKey = key;
            _dialogue.Listening = true;
            _dialogue.SpeechNotice = null;
        }
        else
        {
            _dialogue.SpeechNotice = _talk.Problem;
        }
    }

    // The key went up (or the recording reached its cap): the clip goes to the worker as a spoken turn.
    private void EndListening()
    {
        _dialogue.Listening = false;
        var clip = _talk.End();
        if (clip is null)
        {
            _dialogue.SpeechNotice = _talk.Problem;
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
        if (_talk.Listening)
        {
            _ = _talk.End();
        }

        _dialogue.Listening = false;
    }

    // Once per frame while a key is held: collect the samples, and send the clip when the recording hits its length cap. The mic check's meter moves
    // with the level of what was collected.
    private void PumpTalk(double seconds)
    {
        if (_check is { Listening: true })
        {
            _talk.Pump();
            if (_talk.ReachedLimit && !_talk.Listening)
            {
                EndCheckListening();
            }

            return;
        }

        if (_micCheck is { IsActive: true } check)
        {
            if (_talk.Listening)
            {
                _talk.Pump();
                check.Feed(_talk.LastLevel, seconds);
                if (_talk.ReachedLimit && !_talk.Listening)
                {
                    EndMicListening();
                }
            }
            else
            {
                check.Feed(0, seconds);
            }

            return;
        }

        if (!_talk.Listening)
        {
            return;
        }

        _talk.Pump();
        if (_talk.ReachedLimit && !_talk.Listening)
        {
            EndListening();
        }
    }

    // Applies the turns the worker finished, in order: the panel, the console, the arrow.
    // ---- The check battery (S11): outside the world, its own screen, its own grades; nothing it does reaches the progress or the world. ----

    // A finished turn of the session: a check answer grades the battery's item; any other turn goes to the session log.
    private void OnAppliedTurn(AppliedTurn applied)
    {
        if (applied.Outcome.Request.Check is not null)
        {
            if (_check is null)
            {
                return;
            }

            if (applied.Outcome.Grade is { } grade)
            {
                _check.Graded(grade);
                _sound.ObserveTurn(null, null, isCheck: true);
                Console.WriteLine($"check: {applied.Outcome.Request.Check.Id} heard \"{grade.Transcript}\" -> {grade.Intent}, {(grade.Correct ? "correct" : grade.DontKnow ? "no sé" : "not correct")}{(grade.ErrorTags.Count > 0 ? $" ({string.Join(", ", grade.ErrorTags)})" : string.Empty)}");
            }
            else
            {
                _check.Failed(applied.Outcome.Problem ?? "The answer was not graded.");
            }

            return;
        }

        _log?.Turn(applied, _session);
    }

    // The learner picked an option of the comprehension item on the screen (1 to 3, as shown).
    private void PickCheck(int shown)
    {
        if (_check is not { } check)
        {
            Console.WriteLine("check: no battery is running");
            return;
        }

        if (check.Pick(shown))
        {
            _sound.ObserveTurn(null, null, isCheck: true);
            Console.WriteLine($"check: picked {shown + 1} ({(check.Results[^1].Correct ? "the right one" : "not the right one")}, {check.Results[^1].LatencyMs:0} ms)");
        }
    }

    // A typed answer of the production item (W, then Enter; or the scripted --say).
    private void SubmitTypedCheck(string text)
    {
        if (_check is not { } check || !check.Sent())
        {
            return;
        }

        var item = check.Plan[check.Index].Item;
        _ = _session.SubmitCheck(item, TurnSource.Typed, text, null, null);
        Console.WriteLine($"check: typed \"{text}\" for {item.Id}");
    }

    // A line to synthesize and answer as spoken (the scripted --speak).
    private void SubmitSpeakCheck(string text)
    {
        if (_check is not { } check || !check.Sent())
        {
            return;
        }

        var item = check.Plan[check.Index].Item;
        _ = _session.SubmitCheck(item, TurnSource.Speak, text, null, null);
        Console.WriteLine($"check: spoken \"{text}\" for {item.Id}");
    }

    // The "No sé" button of a production item: graded as dont_know, never a failure.
    private void SubmitButtonCheck()
    {
        if (_check is not { } check || !check.Sent())
        {
            return;
        }

        var item = check.Plan[check.Index].Item;
        _ = _session.SubmitCheck(item, TurnSource.Button, GuideSession.DontKnowLine, null, null);
        Console.WriteLine($"check: no sé for {item.Id}");
    }

    // The keys of the battery: the comprehension items (1 to 3, R to hear the line again), the production items (V held to speak, W to type,
    // Enter to send, N for no sé) and Esc (end the battery; the rest is skipped, and an answer in the air is waited for).
    private void ReadCheckKeys(KeyboardState keys, Func<Keys, bool> pressed)
    {
        var check = _check!;
        if (pressed(Keys.Escape))
        {
            if (check.IsTyping)
            {
                check.IsTyping = false;
            }
            else if (check.Listening)
            {
                CancelCheckListening();
            }
            else if (check.Phase != CheckPhase.Thinking)
            {
                SkipCheck();
            }

            return;
        }

        if (check.Phase != CheckPhase.Showing || check.Current is not { } entry)
        {
            return;
        }

        if (entry.Item.Kind == CheckItemKind.Comprehension)
        {
            for (var i = 0; i < CheckOptionKeys.Length; i++)
            {
                if (pressed(CheckOptionKeys[i].Key) || pressed(CheckOptionKeys[i].Pad))
                {
                    PickCheck(i);
                    return;
                }
            }

            if (pressed(Keys.R))
            {
                PlayCheckLine(entry);
            }

            return;
        }

        if (check.IsTyping)
        {
            if (pressed(Keys.Enter))
            {
                if (!check.Entry.IsBlank)
                {
                    SubmitTypedCheck(check.Entry.Text);
                }
            }
            else if (pressed(Keys.Back))
            {
                _ = check.Entry.Backspace();
            }

            return;
        }

        if (check.Listening)
        {
            if (!keys.IsKeyDown(Keys.V))
            {
                EndCheckListening();
            }

            return;
        }

        if (pressed(Keys.V))
        {
            BeginCheckListening();
        }
        else if (pressed(Keys.W) || pressed(Keys.Tab))
        {
            check.IsTyping = true;
            if (pressed(Keys.W))
            {
                _swallowedOpenKey = 'w'; // the platform types the key once more: the text event must not take it
                _swallowFrames = 2;
            }
        }
        else if (pressed(Keys.N))
        {
            SubmitButtonCheck();
        }
    }

    private void BeginCheckListening()
    {
        var check = _check!;
        if (check.Phase != CheckPhase.Showing || check.Current is not { Item.Kind: CheckItemKind.Production })
        {
            return;
        }

        if (!_session.Worker!.CanHear)
        {
            check.Notice = _session.Worker.Load?.Problem ?? "The voice is not available: type your answer (W).";
            return;
        }

        if (_talk.Begin())
        {
            check.Listening = true;
            check.Notice = null;
        }
        else
        {
            check.Notice = _talk.Problem;
        }
    }

    // The key went up (or the recording reached its cap): the clip is the answer, sent to the worker as a spoken check answer.
    private void EndCheckListening()
    {
        var check = _check!;
        check.Listening = false;
        var clip = _talk.End();
        if (clip is null)
        {
            check.Notice = _talk.Problem;
            return;
        }

        if (!check.Sent())
        {
            return;
        }

        _ = _session.SubmitCheck(check.Plan[check.Index].Item, TurnSource.Clip, null, clip, null);
        Console.WriteLine($"check: listened {clip.DurationSeconds:0.00} s for {check.Plan[check.Index].Item.Id}");
    }

    // Esc while the microphone records: the recording is dropped and nothing is sent.
    private void CancelCheckListening()
    {
        if (_talk.Listening)
        {
            _ = _talk.End();
        }

        if (_check is { } check)
        {
            check.Listening = false;
        }
    }

    private void SkipCheck()
    {
        CancelCheckListening();
        _check?.SkipRest();
        Console.WriteLine("check: ended early (Esc); the rest is skipped");
    }

    // The comprehension item's line plays once when the item comes up, and again on R.
    private void PlayCheckLine(CheckPlanEntry entry) => _voice.Play(CheckSpeaker(entry.Item.Line), entry.Item.Spanish, slow: false);

    // The speaker of a check line: a prompt's line is said by the prompt's agent; a phrase's model line by the guide.
    private string CheckSpeaker(string line)
    {
        const string PromptPrefix = "prompt.";
        if (line.StartsWith(PromptPrefix, StringComparison.Ordinal) && _session.Bundle.Prompts.TryGetValue(line[PromptPrefix.Length..], out var prompt))
        {
            return prompt.AgentId ?? SpeechSpeakers.Guide;
        }

        return SpeechSpeakers.Guide;
    }

    // Once per frame while the battery runs: the scripted answers (screenshot runs), the first sound of an item, and the end of the battery.
    private void DriveCheck()
    {
        if (_check is null)
        {
            return;
        }

        if (_checkDone)
        {
            if (_endAfterCheck && _checkFramesLeft > 0 && --_checkFramesLeft == 0 && !_options.IsScreenshotRun)
            {
                Exit(); // a post- or recall-check ends the run after its thank-you screen
            }

            return;
        }

        if (_check.IsDone)
        {
            FinishCheck();
            return;
        }

        if (_micCheck is { IsActive: true })
        {
            return; // the mic check comes first
        }

        _checkScript?.Fill();
        if (_check.Current is { Item.Kind: CheckItemKind.Comprehension } entry && _checkPlayedAt != _check.Index)
        {
            _checkPlayedAt = _check.Index;
            PlayCheckLine(entry);
        }
    }

    // The battery is done: the end is in the log, a pre-check hands over to the world (its script goes on from where the battery stopped), and a
    // post- or recall-check shows its thank-you screen and ends the run.
    private void FinishCheck()
    {
        var check = _check!;
        _checkDone = true;
        var answered = check.Results.Count(static r => r.Input != CheckInput.Skipped);
        var skipped = check.Results.Count - answered;
        var kind = CheckKinds.Of(check.Kind);
        _log?.CheckEnd(kind, answered, skipped);
        Console.WriteLine($"check {kind}: {answered} answered, {skipped} skipped");
        if (check.Kind == CheckKind.Pre)
        {
            if (_checkScript is { } script && _scenario is not null)
            {
                _scenario.SkipTo(script.Index);
            }

            _checkScript = null;
            _check = null;
            return;
        }

        _checkScript = null;
        _endAfterCheck = true;
        if (!_playtest)
        {
            _checkFramesLeft = CheckEndFrames;
            return;
        }

        // The playtest's end: the thank-you holds until Esc (no timer), the world script is done with its steps, and a post-check saves the run.
        _checkFramesLeft = -1;
        if (_scenario is not null)
        {
            _scenario.SkipTo(_options.Script.Count);
        }

        if (_options.IsPlaytestRecall)
        {
            check.Closing = RecallThankYou;
            return;
        }

        var folder = Path.GetFileName(Path.GetDirectoryName(_playtestSavePath!)) ?? string.Empty;
        check.Closing = $"{PlaytestThankYou}\nSaved: {PlaytestEnd.SaveFileName} in the session folder {folder}.";
        _playtestSaveDue = true;
    }

    // The battery is waiting for a learner's answer that the scripted run has not given, or it has ended and shows its thank-you screen.
    private bool CheckRestsForShot => _check is { IsDone: false, Phase: CheckPhase.Showing } && _checkScript is { Rests: true } || (_checkDone && _endAfterCheck);

    // The save file of F6 (S10): the --save file, in a playtest the session folder's playtest.save (S18), or vocaterra.save.json in the working folder.
    // A turn still thinking means "not now".
    private void SaveNow()
    {
        var path = PlaytestEnd.ManualSavePath(_options.SavePath, _playtest ? _playtestSavePath : null, DefaultSavePath);
        if (WriteSave(path, "on F6"))
        {
            _toasts.Show($"Game saved (session {_session.PlaySession})");
        }
        else
        {
            _toasts.Show("Wait a moment, then press F6 again.");
        }
    }

    // Writes the save of the session as it is now (the capture refuses a session that is not at rest, and that is said, not thrown).
    private bool WriteSave(string path, string why)
    {
        if (!_session.CanSave)
        {
            Console.WriteLine($"save: not written {why}: the guide is still thinking, or the key words are on show");
            return false;
        }

        var document = SaveGame.Capture(_session, _pristine(), new SaveSettings(_sound.Mixer.Volumes)); // the sound volumes ride in the save's settings (S24)
        var text = SaveGame.Serialize(document);
        SaveFile.Write(path, text);
        Console.WriteLine(
            $"save: {path} written {why} (session {document.Session}, tick {document.Tick}, stored time {document.StoredTime}, world digest {document.StateDigest[..16]}, {System.Text.Encoding.UTF8.GetByteCount(text)} bytes)");
        return true;
    }

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

    // The arrow of the session is the beacon: a new arrow stands the column on its block (a review question shows no name), and when the
    // beacon's own reach takes it down, the arrow goes too.
    private void SyncBeacon()
    {
        var pointer = _session.Pointer;
        if (!ReferenceEquals(pointer, _shownPointer))
        {
            _shownPointer = pointer;
            if (pointer is { } arrow)
            {
                Beacon.Set(arrow.Target.ToBlock(), arrow.Quiz ? "¿?" : arrow.Place);
            }
            else
            {
                Beacon.Clear();
            }
        }

        if (pointer is not null && !Beacon.IsSet)
        {
            _session.DismissPointer();
            _shownPointer = null;
        }
    }

    // The book's keys (S20, S20b): Escape or J closes it; Tab (Shift and Tab turns back), Left and Right turn the page; Page Up and Page Down scroll.
    // Up and Down move the selection among the rows with a speaker button (the selection is kept on screen); a page with none scrolls a line. Enter plays
    // the selected row, Shift+Enter its slow recording.
    private void ReadBookKeys(KeyboardState keys, Func<Keys, bool> pressed)
    {
        if (pressed(Keys.Escape) || pressed(Keys.J))
        {
            CloseBook();
            return;
        }

        if (pressed(Keys.Tab))
        {
            _book.Turn(forward: !(keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift)));
        }
        else if (pressed(Keys.Left))
        {
            _book.Turn(forward: false);
        }
        else if (pressed(Keys.Right))
        {
            _book.Turn(forward: true);
        }

        var lines = new BookGeometry(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height).LinesVisible;
        if (pressed(Keys.PageDown))
        {
            _book.Scroll(lines);
        }
        else if (pressed(Keys.PageUp))
        {
            _book.Scroll(-lines);
        }
        else if (pressed(Keys.Down) || pressed(Keys.Up))
        {
            _bookControls.Move(pressed(Keys.Down) ? 1 : -1, new BookGeometry(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height).LinesVisible);
        }
        else if (pressed(Keys.Enter))
        {
            _ = _bookControls.Enter(ShiftDown(keys));
        }
        else if (pressed(Keys.P) && _bookControls.PractiseFirst(new BookGeometry(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height)) is { } practise)
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

    private static bool ShiftDown(KeyboardState keys) => keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);

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
