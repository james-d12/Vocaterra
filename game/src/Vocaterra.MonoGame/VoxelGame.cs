using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame;

/// <summary>
/// The engine host of the 3D first-person scene (<c>--voxel</c>, S2 and S3). Each frame it turns the real clock into whole ticks, reads the
/// keyboard and the mouse (or the scripted run) into <see cref="VoxelCommand"/>s and steps the <see cref="VoxelSession"/>'s simulation; then it
/// places the <see cref="FirstPersonCamera"/> at the interpolated eye, draws the voxel world with <see cref="VoxelRenderer"/> and the 2D layers
/// over it. All rules live in <c>Vocaterra.Simulation</c>, <c>Vocaterra.Game</c> and the plain classes under <c>Frontend/</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What works in 3D.</b> Walking (WASD or the arrows, Space to jump), mouse look in relative mode (Esc lets the mouse go, a click takes it
/// again), gathering by holding the left button on a trunk or a berry bush, placing with the right button, the hotbar, the inventory (I, or E
/// when the crosshair is not on the guide), the journal (J), the debug overlay (F3), toasts, the NPC figures with their names, the beacon column
/// and its edge arrow, and the guide: aim the crosshair at her figure and press E (or T to type, V to talk) to open the panel. Her answers
/// point the beacon at the named noun's nearest block; "¿qué es esto?" names the block under the crosshair. The panel takes the keyboard and
/// frees the mouse while it is open. The sign reads <c>Tienda / Río</c>.
/// </para>
/// <para>
/// <b>Script steps.</b> <c>--look</c>, <c>--walk-to</c>, <c>--interact</c>, <c>--place</c>, <c>--select</c>, <c>--beacon</c>, <c>--journal</c>,
/// <c>--inventory</c>, and <c>--say</c> (walk to the guide, open the panel, type and send each line; the scripted run waits for each reply).
/// </para>
/// </remarks>
public sealed class VoxelGame : Microsoft.Xna.Framework.Game
{
    private const int StartWidth = 1280;
    private const int StartHeight = 720;
    private const int SettleTicks = 30;
    private const int RunawayTicks = 60 * 60 * 5;
    private const string GuideName = "guide";

    private static readonly Keys[] SlotKeys = [Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0];

    private readonly LaunchOptions _options;
    private readonly VoxelSession _session;
    private readonly Scene _scene;
    private readonly VoxelSimulation _sim;
    private readonly GraphicsDeviceManager _graphics;
    private readonly TickAccumulator _accumulator = new(VoxelSimulation.TicksPerSecond);
    private readonly FrameStats _stats = new();
    private readonly IFrameClock _clock;
    private readonly LookControl _look = new();
    private readonly CameraEase _ease = new();
    private readonly FirstPersonCamera _camera = new();
    private readonly MouseLook _mouseLook = new();
    private readonly LiveVoxelInput _live = new();
    private readonly VoxelScript? _script;
    private readonly VoxelSayScript? _say;
    private readonly InputFocus _focus = new();
    private readonly DialogueModel _dialogue;
    private readonly TurnPump _pump;
    private readonly PushToTalk _talk;
    private readonly KeyRepeater _backspace = new();
    private readonly KeyRepeater _delete = new();
    private readonly KeyRepeater _left = new();
    private readonly KeyRepeater _right = new();
    private readonly JournalModel _journal = new();
    private readonly ToastQueue _toasts = new();
    private readonly InventoryScreen _inventory = new();
    private readonly Camera _screenCamera = new(1, 1, 1);
    private readonly BusyFrameStats _draws = new();
    private readonly BusyFrameStats _frames = new();
    private readonly List<VoxelCommand> _commands = [];
    private readonly List<VoxelCommand> _scripted = [];
    private readonly List<IFrameLayer> _layers = [];
    private readonly VoxelDebugOverlay _debug = new();
    private IReadOnlyList<NpcFigure> _figures = [];
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private TileAtlas? _tileAtlas;
    private BlockAtlas? _blockAtlas;
    private VoxelRenderer? _renderer;
    private FrameView? _view;
    private KeyboardState _previousKeys;
    private int _previousScroll;
    private bool _previousLeftDown;
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

    /// <summary>Creates the 3D game for a started session (its simulation is the scene's) and the built scene.</summary>
    public VoxelGame(LaunchOptions options, VoxelSession session, Scene scene)
    {
        _options = options;
        _session = session;
        _scene = scene;
        _sim = session.Simulation;
        _dialogue = new DialogueModel(_focus, session.OpeningLine);
        var microphone = options.UsesMicrophone ? MicrophoneCapture.TryOpen() : null;
        _talk = new PushToTalk(microphone);
        _pump = new TurnPump(session, _dialogue, CursorCell);
        _look.Set(scene.Spawn.Yaw, 0);
        _currentEye = _previousEye = EyeBlocks();
        if (options.IsScreenshotRun)
        {
            _clock = new FixedFrameClock(VoxelSimulation.TicksPerSecond);
            if (options.VoxelSteps.Count > 0)
            {
                _script = new VoxelScript(_sim, _look, options.VoxelSteps, ResolveItem, Console.WriteLine);
            }

            if (options.Says.Count > 0)
            {
                _say = new VoxelSayScript(session, _look, _dialogue, options.Says, Console.WriteLine);
            }
        }
        else
        {
            _clock = new RealFrameClock();
        }

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = StartWidth,
            PreferredBackBufferHeight = StartHeight,
            PreferredDepthStencilFormat = DepthFormat.Depth24,
            SynchronizeWithVerticalRetrace = !options.IsScreenshotRun,
        };
        IsFixedTimeStep = false; // the accumulator owns the tick rate
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = $"Vocaterra 3D (seed {options.Seed})";
        Window.TextInput += OnTextInput;
        _session.StartWorker(MonotonicClock.Milliseconds, options.NeedsSpeech);
    }

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
        _tileAtlas = new TileAtlas(GraphicsDevice);
        _blockAtlas = new BlockAtlas(GraphicsDevice);
        _figures = NpcFigures.FromScene(_scene);
        var started = MonotonicClock.Milliseconds();
        _renderer = new VoxelRenderer(GraphicsDevice, _blockAtlas, _sim.Grid);
        _renderer.SetFigures(_figures);
        Console.WriteLine($"voxel: {_renderer.Stats.Summary()} (renderer ready in {MonotonicClock.Milliseconds() - started:0} ms)");
        Console.WriteLine($"graphics: {GraphicsAdapter.DefaultAdapter.Description}, depth {GraphicsDevice.PresentationParameters.DepthStencilFormat}");
        Console.WriteLine($"scene: {_scene.Name}, seed {_scene.Seed}, {_scene.TreeCount} trees, spawn {_scene.Spawn.Feet.X},{_scene.Spawn.Feet.Y},{_scene.Spawn.Feet.Z}, digest {_scene.Grid.ComputeDigestHex()[..16]}");
        if (_options.Beacon is { } point)
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

        _view = new FrameView
        {
            Session = _session,
            Dialogue = _dialogue,
            Camera = _screenCamera,
            Fonts = new FontProvider(AssetPaths.Font),
            Pixel = _pixel,
            Seed = _options.Seed,
            Atlas = _tileAtlas,
            Journal = _journal,
            Toasts = _toasts,
            Inventory = _inventory,
            Crafting = new CraftingScreen(),
            Voxel = _sim,
            Camera3D = _camera,
            Beacon = Beacon,
            Figures = _figures,
            VoxelIcons = _blockAtlas,
        };
        _layers.Add(new HeldItemLayer());
        _layers.Add(new CrosshairLayer());
        _layers.Add(new NpcLabelLayer());
        _layers.Add(new WorldLabelLayer([new WorldLabel("Tienda / Río", _scene.Point("signpost").X + 0.5f, _scene.Point("signpost").Y + 4.2f, _scene.Point("signpost").Z + 0.5f)]));
        _layers.Add(new BeaconArrowLayer());
        _layers.Add(new TalkHintLayer());
        _layers.Add(_debug);
        _layers.Add(new HotbarLayer());
        _layers.Add(new DialoguePanel());
        _layers.Add(new ToastLayer());
        _layers.Add(new JournalLayer());
        _layers.Add(new InventoryLayer());
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
        if (!_options.IsScreenshotRun)
        {
            ReadInput();
        }

        ApplyFinishedTurns();
        var steps = _accumulator.Advance(seconds);
        for (var i = 0; i < steps; i++)
        {
            StepOnce();
        }

        PumpTalk();
        _pump.Submit();
        SyncBeacon();
        _aimed = AimedFigure();

        if ((_script is null || _script.IsDone) && (_say is null || _say.IsDone))
        {
            _settled++;
            if (!_shown)
            {
                _shown = true;
                if (_options.Journal)
                {
                    OpenJournal();
                }

                if (_options.ShowInventory)
                {
                    OpenInventory();
                }
            }
        }

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
        _screenCamera.Resize(width, height);
        var drawStart = MonotonicClock.Milliseconds();

        var alpha = (float)_accumulator.Alpha;
        var eye = Vector3.Lerp(_previousEye, _currentEye, alpha);
        eye.Y += (float)(_previousEase + ((_currentEase - _previousEase) * alpha));
        _camera.Update(eye, _look.YawSteps, _look.PitchSteps, width, height);
        view.Eye = eye;
        view.Look = (_look.YawSteps, _look.PitchSteps);
        view.Fps = _stats.Fps;
        view.Aimed = _aimed;
        view.Loading = _session.Worker!.IsReady ? null : LoadingLayer.Text;
        var mouse = Mouse.GetState();
        view.Mouse = !_options.IsScreenshotRun && GraphicsDevice.Viewport.Bounds.Contains(mouse.Position) ? mouse.Position : null;

        _renderer!.Remesh();
        GraphicsDevice.Clear(VoxelRenderer.Sky);
        _renderer.DrawWorld(_camera, _sim.Target?.Block, Beacon.Target, _sim.Tick);

        var spriteBatch = _spriteBatch!;
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        foreach (var layer in _layers)
        {
            layer.Draw(spriteBatch, view);
        }

        spriteBatch.End();
        var drawEnd = MonotonicClock.Milliseconds();
        _draws.Record(drawEnd - drawStart);
        if (_lastDrawEnd >= 0 && _sim.Tick > 2)
        {
            _frames.Record(drawEnd - _lastDrawEnd);
        }

        _lastDrawEnd = drawEnd;
        base.Draw(gameTime);

        if (_options.ScreenshotPath is { } path
            && ((_sim.Tick >= _options.Frames && _settled >= SettleTicks && _session.Worker!.IsReady && _session.PendingTurns == 0 && !_dialogue.SubmitRequested)
                || _sim.Tick > RunawayTicks))
        {
            ExitCode = ScreenshotWriter.TrySave(GraphicsDevice, path) ? 0 : 1;
            Console.WriteLine($"scene draw (voxel world, layers, flush): {_draws.Summary()}");
            Console.WriteLine($"frame time (end of one draw to the next, no vsync): {_frames.Summary()}");
            Console.WriteLine($"voxel: {_renderer.Stats.Summary()}; {_renderer.DrawnChunks} chunks drawn this frame");
            var body = _sim.Player;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"player: ({body.CenterX / (double)VoxelPhysics.UnitsPerBlock:0.00}, {body.FeetY / (double)VoxelPhysics.UnitsPerBlock:0.00}, {body.CenterZ / (double)VoxelPhysics.UnitsPerBlock:0.00}), tick {_sim.Tick}, wood {_sim.Inventory.Count(ItemId.Wood)}, berries {_sim.Inventory.Count(ItemId.Berry)}"));
            Exit();
        }
    }

    /// <inheritdoc />
    protected override void UnloadContent()
    {
        Window.TextInput -= OnTextInput;
        _session.StopWorker(); // the worker disposes the recognizer on its own thread
        _renderer?.Dispose();
        _blockAtlas?.Dispose();
        _tileAtlas?.Dispose();
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

        foreach (var definition in ItemDefinition.All.Concat(ItemDefinition.VoxelOnly))
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
        if (_script is { IsDone: false })
        {
            _script.Fill(_scripted);
            _commands.AddRange(_scripted);
        }
        else if (_say is { IsDone: false })
        {
            _say.Fill(_commands);
        }
        else if (!_dialogue.IsOpen)
        {
            _live.Fill(_commands);
        }

        // The look goes first: it is absolute and every other command of the tick uses the facing it sets.
        _commands.Insert(0, VoxelCommand.Look(_look.Yaw, _look.Pitch));
        _session.Step(_commands);
        _ease.Tick(_sim.Player.SteppedUp);
        _previousEye = _currentEye;
        _currentEye = EyeBlocks();
        _previousEase = _currentEase;
        _currentEase = _ease.Offset;
        foreach (var e in _sim.Events)
        {
            if (e.Kind == SimulationEventKind.ItemPickedUp)
            {
                var item = (ItemId)e.Detail;
                var label = _session.Nouns.ForItem(item)?.Lemma ?? ItemDefinition.Get(item).Name;
                _toasts.Show($"+{e.Amount} {label}");
            }
        }
    }

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
        if (IsActive)
        {
            if (_dialogue.IsOpen)
            {
                ReadDialogueKeys(keys, Pressed);
            }
            else if (_journal.IsOpen)
            {
                if (Pressed(Keys.Escape) || Pressed(Keys.J))
                {
                    CloseJournal();
                }
            }
            else if (_inventory.IsOpen)
            {
                if (Pressed(Keys.Escape) || Pressed(Keys.I) || Pressed(Keys.E))
                {
                    CloseInventory();
                }
            }
            else if (_aimed is not null && (Pressed(Keys.E) || Pressed(Keys.T) || Pressed(Keys.V)))
            {
                // The crosshair is on the guide: E or T opens the panel; V opens it and starts listening (as in the 2D slice).
                if (Pressed(Keys.V))
                {
                    OpenDialogue(null);
                    BeginListening(Keys.V);
                }
                else
                {
                    OpenDialogue(Pressed(Keys.T) ? 't' : 'e');
                }
            }
            else if (Pressed(Keys.J))
            {
                OpenJournal();
            }
            else if (Pressed(Keys.I) || Pressed(Keys.E))
            {
                OpenInventory();
            }
            else if (Pressed(Keys.Escape))
            {
                _captureWanted = false;
            }

            if (Pressed(Keys.F3))
            {
                _debug.Visible = !_debug.Visible;
            }
        }

        var uiOpen = _journal.IsOpen || _inventory.IsOpen || _dialogue.IsOpen;
        var leftDown = mouse.LeftButton == ButtonState.Pressed;
        var rightDown = mouse.RightButton == ButtonState.Pressed;
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

    // The panel's keys while it is open: Enter sends, Esc closes, Tab holds push-to-talk, the editing keys repeat as the 2D panel's do.
    private void ReadDialogueKeys(KeyboardState keys, Func<Keys, bool> pressed)
    {
        if (pressed(Keys.Escape))
        {
            CancelListening();
            _dialogue.Close();
            _session.EndReview();
            return;
        }

        if (pressed(Keys.Enter))
        {
            _dialogue.RequestSubmit();
        }

        if (pressed(Keys.Tab))
        {
            BeginListening(Keys.Tab);
        }

        if (_talk.Listening && !keys.IsKeyDown(_listenKey))
        {
            EndListening();
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
    }

    // The window's text event: the letters of the panel's text box. The key that opened the panel (T or E) is typed by the platform too, once.
    private void OnTextInput(object? sender, TextInputEventArgs args)
    {
        if (!_dialogue.IsOpen || char.IsControl(args.Character) || _talk.Listening)
        {
            return;
        }

        if (_swallowedOpenKey is { } opener && char.ToLowerInvariant(args.Character) == opener)
        {
            _swallowedOpenKey = null;
            return;
        }

        _ = _dialogue.Entry.Insert(args.Character);
    }

    // Opens the panel with the guide's review question when one is due; the key that opened it is swallowed by the text event.
    private void OpenDialogue(char? openedBy)
    {
        _dialogue.Open(_session.BeginTalk()?.Question);
        _swallowedOpenKey = openedBy;
        _swallowFrames = 2; // the platform types the key once, in the next frame at the latest
    }

    // The tile for "¿qué es esto?": the block under the crosshair (the scripted run has the same crosshair).
    private GuideCell? CursorCell() => _session.AimedCell;

    // The guide's figure under the crosshair, when it is within reach and no block is in the way; the other figures cannot be talked to yet (S6).
    private NpcFigure? AimedFigure()
    {
        if (_dialogue.IsOpen)
        {
            return null;
        }

        var eye = (x: (double)_currentEye.X, y: (double)_currentEye.Y, z: (double)_currentEye.Z);
        foreach (var figure in _figures)
        {
            if (figure.Name != GuideName)
            {
                continue;
            }

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
            _dialogue.Open(_session.BeginTalk()?.Question);
        }

        if (!worker.CanHear)
        {
            _dialogue.SpeechNotice = worker.Load?.Problem ?? "La voz no está disponible: escribe tu frase.";
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
        Console.WriteLine($"listened: {clip.DurationSeconds:0.00} s of speech sent to the guide");
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

    // Once per frame while a key is held: collect the samples, and send the clip when the recording hits its length cap.
    private void PumpTalk()
    {
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
    private void ApplyFinishedTurns()
    {
        foreach (var applied in _pump.Apply())
        {
            var turn = applied.Turn;
            if (turn is null)
            {
                Console.WriteLine($"turn failed: {applied.Outcome.Problem}");
                continue;
            }

            var timings = applied.Outcome.Timings;
            var detail = turn.Input == InputKind.Spoken
                ? $"heard \"{turn.Line}\" (confidence {(int)Math.Round((turn.Confidence ?? 0) * 100)} %) -> {turn.Summary}, {timings.TotalMilliseconds:0} ms"
                : $"\"{turn.Line}\" -> {turn.Summary}, {timings.TotalMilliseconds:0.0} ms";
            Console.WriteLine($"turn {_dialogue.TurnCount}: {(turn.Input == InputKind.Spoken ? "spoken " : string.Empty)}{detail}, state {turn.AgentState}");
            foreach (var line in turn.GuideLines)
            {
                Console.WriteLine($"  guide: {line}");
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

    private void OpenJournal()
    {
        _journal.Open(_session.Words);
        _focus.JournalOpen = true;
    }

    private void CloseJournal()
    {
        _journal.Close();
        _focus.JournalOpen = false;
    }

    private void OpenInventory()
    {
        if (_journal.IsOpen)
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
