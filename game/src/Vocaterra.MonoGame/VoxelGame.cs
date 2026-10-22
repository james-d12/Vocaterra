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
    private const int StartWidth = 1280;
    private const int StartHeight = 720;
    private const int SettleTicks = 30;
    private const int RunawayTicks = 60 * 60 * 5;

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
    private readonly ScenarioScript? _scenario;
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
    private readonly WordCardQueue _cards = new();
    private readonly CraftingScreen _crafting = new();
    private readonly Queue<int> _pendingCrafts = new();
    private int _loadingFrames;
    private int _printedEvents;
    private string? _shotPath;

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
            if (options.Script.Count > 0)
            {
                var hooks = new ScenarioHooks(npc => OpenDialogueOn(npc, null), CloseDialogue, path => _shotPath = path, () => _shotPath is not null);
                _scenario = new ScenarioScript(session, _look, _dialogue, options.Script, ResolveItem, hooks, Console.WriteLine);
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
        _blockAtlas = new BlockAtlas(GraphicsDevice);
        _figures = NpcFigures.FromScene(_scene);
        var started = MonotonicClock.Milliseconds();
        _renderer = new VoxelRenderer(GraphicsDevice, _blockAtlas, _sim.Grid);
        _renderer.SetFigures(_figures);
        Console.WriteLine($"voxel: {_renderer.Stats.Summary()} (renderer ready in {MonotonicClock.Milliseconds() - started:0} ms)");
        Console.WriteLine($"graphics: {GraphicsAdapter.DefaultAdapter.Description}, depth {GraphicsDevice.PresentationParameters.DepthStencilFormat}");
        if (_options.Greyscale)
        {
            Console.WriteLine("greyscale: the 3D world is drawn in full colour (D-game-6 as amended); the grey A/B is not in the 3D world yet (stub)");
        }
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
            Fonts = new FontProvider(AssetPaths.Font),
            Pixel = _pixel,
            Seed = _options.Seed,
            Journal = _journal,
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
        };
        _layers.Add(new HeldItemLayer());
        _layers.Add(new CrosshairLayer());
        _layers.Add(new NpcLabelLayer());
        _layers.Add(new WorldLabelLayer([new WorldLabel("Tienda / Río", _scene.Point("signpost").X + 0.5f, _scene.Point("signpost").Y + 4.2f, _scene.Point("signpost").Z + 0.5f)]));
        _layers.Add(new BeaconArrowLayer());
        _layers.Add(new TalkHintLayer());
        _layers.Add(_debug);
        _layers.Add(new HotbarLayer());
        _layers.Add(new ObjectiveLayer());
        _layers.Add(new WordCardLayer());
        _layers.Add(new DialoguePanel());
        _layers.Add(new ToastLayer());
        _layers.Add(new JournalLayer());
        _layers.Add(new InventoryLayer());
        _layers.Add(new CraftingLayer());
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
        PrintEvents();

        if (_scenario is null || _scenario.IsDone)
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

                if (_options.ShowCrafting)
                {
                    OpenCrafting();
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
        view.ViewportWidth = width;
        view.ViewportHeight = height;
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
        view.LoadingFrame = _loadingFrames++;
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

        if (_shotPath is { } shot)
        {
            // A --shot step: the screen as it is at this point of the script, then the script goes on.
            var saved = ScreenshotWriter.TrySave(GraphicsDevice, shot);
            Console.WriteLine($"shot: {shot} ({(saved ? "saved" : "FAILED")}) at tick {_sim.Tick}, {_session.Objective.Summary}");
            ExitCode = saved ? ExitCode : 1;
            _shotPath = null;
        }

        if (_options.ScreenshotPath is { } path
            && ((_sim.Tick >= _options.Frames && _settled >= SettleTicks && _session.Worker!.IsReady && _session.PendingTurns == 0 && !_dialogue.SubmitRequested)
                || _sim.Tick > RunawayTicks))
        {
            ExitCode = ScreenshotWriter.TrySave(GraphicsDevice, path) ? 0 : 1;
            Console.WriteLine($"scene draw (voxel world, layers, flush): {_draws.Summary()}");
            Console.WriteLine($"frame time (end of one draw to the next, no vsync): {_frames.Summary()}");
            Console.WriteLine($"voxel: {_renderer.Stats.Summary()}; {_renderer.DrawnChunks} chunks drawn this frame");
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
        _session.StopWorker(); // the worker disposes the recognizer on its own thread
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
        if (_scenario is { IsDone: false })
        {
            _scenario.Fill(_commands);
        }
        else if (!_dialogue.IsOpen)
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
        _ease.Tick(_sim.Player.SteppedUp);
        _previousEye = _currentEye;
        _currentEye = EyeBlocks();
        _previousEase = _currentEase;
        _currentEase = _ease.Offset;
        // Pickups show in the hotbar and, the first time, as a word card; they do not toast (a toast per pickup would queue behind the
        // checklist's completions for minutes).
        AnnounceObjective(_session.Completed);
        foreach (var card in _session.TakeWordCards())
        {
            _cards.Add(card);
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
            _toasts.Show($"Hecho: {step.Text}");
            Console.WriteLine($"objective: Hecho: {step.Text} -> {_session.Objective.Summary}");
        }

        if (completed.Count > 0 && _session.Objective.Complete)
        {
            _toasts.Show($"{Objective.Title} completo");
            Console.WriteLine($"objective: {Objective.Title} completo");
        }
    }

    private void OpenCrafting()
    {
        if (_journal.IsOpen || _inventory.IsOpen || _dialogue.IsOpen)
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
            else if (Pressed(Keys.J))
            {
                OpenJournal();
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
            }

            if (Pressed(Keys.F3))
            {
                _debug.Visible = !_debug.Visible;
            }
        }

        var uiOpen = _journal.IsOpen || _inventory.IsOpen || _dialogue.IsOpen || _crafting.IsOpen;
        var leftDown = mouse.LeftButton == ButtonState.Pressed;
        var rightDown = mouse.RightButton == ButtonState.Pressed;
        if (IsActive && _crafting.IsOpen && leftDown && !_previousLeftDown)
        {
            // A click on a row of the panel asks for one craft in the next tick (the same command a script sends).
            var layout = new CraftingLayout(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, _session.Recipes.Entries.Count);
            if (layout.RowAt(mouse.X, mouse.Y) is { } row)
            {
                _pendingCrafts.Enqueue(row);
            }
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

    // The panel's keys while it is open: Enter sends, Esc closes, Tab holds push-to-talk, the editing keys repeat as the panel doeso.
    private void ReadDialogueKeys(KeyboardState keys, Func<Keys, bool> pressed)
    {
        if (pressed(Keys.Escape))
        {
            CloseDialogue();
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

    // Opens the panel on an NPC: its opening is a core host turn when its state has one (the shopkeeper's question, the villager's hello, the
    // guide's transfer offer), otherwise its prompt's text, and the guide's review question when one is due. The key that opened the panel is
    // swallowed by the text event.
    private void OpenDialogueOn(string npc, char? openedBy)
    {
        var opening = _session.OpenTalk(npc);
        _dialogue.Open(opening, NpcFigures.LabelOf(npc), _session.Review?.Question);
        _swallowedOpenKey = openedBy;
        _swallowFrames = 2; // the platform types the key once, in the next frame at the latest
        var how = opening.HostTurn ? "host turn" : "prompt";
        Console.WriteLine($"talk: {npc} ({how}, {opening.Prompt}): {string.Join(" / ", opening.Lines)}{(_session.Review is { } review ? $" / {review.Question}" : string.Empty)}");
    }

    // The panel closes (Esc, or the script walks on): a recording in progress is dropped and the talk ends.
    private void CloseDialogue()
    {
        CancelListening();
        _dialogue.Close();
        _session.EndTalk();
    }

    // The new game events since the last frame, one console line each (the phrase uses, the voice events, the world events).
    private void PrintEvents()
    {
        var events = _session.Events;
        for (; _printedEvents < events.Count; _printedEvents++)
        {
            Console.WriteLine($"  event: {events[_printedEvents].Text}");
        }
    }

    // The tile for "¿qué es esto?": the block under the crosshair (the scripted run has the same crosshair).
    private GuideCell? CursorCell() => _session.AimedCell;

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
        Console.WriteLine($"listened: {clip.DurationSeconds:0.00} s of speech sent to {_session.CurrentNpc}");
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
            Console.WriteLine($"turn {_dialogue.TurnCount} ({turn.Npc}, {turn.Prompt}): {(turn.Input == InputKind.Spoken ? "spoken " : string.Empty)}{detail}, state {turn.AgentState}{(turn.HostEvents.Count > 0 ? $", host events {string.Join(" ", turn.HostEvents)}" : string.Empty)}{(turn.FailForward ? ", FAIL-FORWARD" : string.Empty)}");
            foreach (var line in turn.GuideLines)
            {
                Console.WriteLine($"  {turn.Npc}: {line}");
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
