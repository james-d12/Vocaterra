using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Physics;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame;

/// <summary>
/// The engine host. Each frame it reads the real clock (<see cref="IFrameClock"/>), lets the <see cref="TickAccumulator"/> turn it into whole
/// ticks (capped), polls the <see cref="IInputSource"/> once per tick, maps it to <c>PlayerCommand</c>s and steps the game session; then it
/// follows the player with the camera, draws the world, then each <see cref="IFrameLayer"/> in order. All rules live in
/// <c>Vocaterra.Simulation</c>, in <c>Vocaterra.Game</c> and in the plain classes under <c>Frontend/</c>; this class only wires them to MonoGame.
/// </summary>
/// <remarks>
/// <para>
/// <b>The dialogue.</b> T next to the guide opens the panel and gives it the keyboard (<see cref="InputFocus"/>), so the movement keys type
/// letters instead of walking. The window's text events feed the box; the editing keys, Enter and Esc are read from the keyboard state once
/// per frame. Enter only <i>asks</i> to send: the turn is evaluated at the end of <c>Update</c>, after the tick loop and never inside a step.
/// Evaluation is synchronous in P3 and its time is measured and shown; P5 moves it to a worker.
/// </para>
/// <para>
/// The base class is written out in full (<c>Microsoft.Xna.Framework.Game</c>) because the namespace <c>Vocaterra.Game</c> would otherwise
/// hide the type from a name lookup inside <c>Vocaterra.*</c>.
/// </para>
/// </remarks>
public sealed class VocaterraGame : Microsoft.Xna.Framework.Game
{
    private const int Zoom = 2;
    private const int StartWidth = 1280;
    private const int StartHeight = 720;

    /// <summary>The hint when a correct use came before its gap: it counted, but the colour did not move.</summary>
    public const string EarlyHint = "Bien. Repasa más tarde.";

    private readonly LaunchOptions _options;
    private readonly GraphicsDeviceManager _graphics;
    private readonly GameSession _session;
    private readonly Camera _camera;
    private readonly TickAccumulator _accumulator = new(GameSimulation.TicksPerSecond);
    private readonly FrameStats _stats = new();
    private readonly IFrameClock _clock;
    private readonly IInputSource _input;
    private readonly InputFocus _focus = new();
    private readonly DialogueModel _dialogue;
    private readonly ScriptedDialogue? _script;
    private readonly JournalModel _journal = new();
    private readonly ToastQueue _toasts = new();
    private readonly InventoryScreen _inventory = new();
    private readonly CraftingScreen _crafting = new();
    private readonly LiveWorldInput _live = new();
    private readonly ScriptedCursor? _cursor;
    private readonly KeyRepeater _backspace = new();
    private readonly KeyRepeater _delete = new();
    private readonly KeyRepeater _left = new();
    private readonly KeyRepeater _right = new();
    private readonly List<PlayerCommand> _commands = [];
    private readonly List<IFrameLayer> _layers = [];
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private TileAtlas? _atlas;
    private WorldRenderer? _worldRenderer;
    private FrameView? _view;
    private KeyboardState _previousKeys;
    private PlayerState _previousPlayer;
    private bool _firstUpdateSeen;
    private bool _swallowTypedT;
    private bool _journalShown;
    private int _previousScroll;
    private bool _previousRightDown;
    private bool _previousLeftDown;

    private static readonly Keys[] SlotKeys = [Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0];

    /// <summary>Creates the game for a started session and options. The session already holds the generated world and the loaded bundle.</summary>
    public VocaterraGame(LaunchOptions options, GameSession session)
    {
        _options = options;
        _session = session;
        _camera = new Camera(session.Simulation.World.Width, session.Simulation.World.Height, TileArt.Size * Zoom);
        _previousPlayer = session.Simulation.Player;
        _dialogue = new DialogueModel(_focus, session.OpeningLine);
        _cursor = options.IsScreenshotRun && (options.Point is not null || options.PointNearest is not null)
            ? new ScriptedCursor(options.Point, options.PointNearest)
            : null;
        if (options.IsScreenshotRun)
        {
            _clock = new FixedFrameClock(GameSimulation.TicksPerSecond);
            if (options.Steps.Count > 0)
            {
                _script = new ScriptedDialogue(session, _dialogue, options.Steps);
                _input = new FocusGatedInput(_script, _focus);
            }
            else
            {
                _input = new FocusGatedInput(new ScriptedInput(options.WalkRightTicks), _focus);
            }
        }
        else
        {
            _clock = new RealFrameClock();
            _input = new FocusGatedInput(new KeyboardInput(), _focus);
            Window.TextInput += OnTextInput;
        }

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = StartWidth,
            PreferredBackBufferHeight = StartHeight,
            SynchronizeWithVerticalRetrace = !options.IsScreenshotRun,
        };
        IsFixedTimeStep = false; // the accumulator owns the tick rate, not MonoGame's timer
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = $"Vocaterra (seed {options.Seed})";
    }

    /// <summary>The process exit code: 0 unless the screenshot could not be saved.</summary>
    public int ExitCode { get; private set; }

    /// <inheritdoc />
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _previousScroll = Mouse.GetState().ScrollWheelValue; // the wheel moves the hotbar by what it turns after the window opens
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _atlas = new TileAtlas(GraphicsDevice);
        _worldRenderer = new WorldRenderer(_atlas);
        _view = new FrameView
        {
            Simulation = _session.Simulation,
            Session = _session,
            Dialogue = _dialogue,
            Camera = _camera,
            Fonts = new FontProvider(AssetPaths.Font),
            Pixel = _pixel,
            Seed = _options.Seed,
            Atlas = _atlas,
            Journal = _journal,
            Toasts = _toasts,
            Inventory = _inventory,
            Crafting = _crafting,
        };
        _layers.Add(new GuideLayer());
        _layers.Add(new PointerLayer());
        _layers.Add(new DebugOverlay());
        _layers.Add(new HotbarLayer());
        _layers.Add(new DialoguePanel());
        _layers.Add(new ToastLayer());
        _layers.Add(new JournalLayer());
        _layers.Add(new InventoryLayer());
        _layers.Add(new CraftingLayer());
    }

    /// <inheritdoc />
    protected override void Update(GameTime gameTime)
    {
        // MonoGame calls Update once before the first Draw, outside the frame loop; skip it so that a frame is one Update plus one Draw.
        if (!_firstUpdateSeen)
        {
            _firstUpdateSeen = true;
            _ = _clock.NextFrameSeconds(); // discard the start-up time (window and GL creation) so it is not counted as a slow frame
            base.Update(gameTime);
            return;
        }

        var seconds = _clock.NextFrameSeconds();
        _stats.Record(seconds);
        if (!_options.IsScreenshotRun && IsActive)
        {
            ReadKeyboard();
        }

        var steps = _accumulator.Advance(seconds);
        for (var i = 0; i < steps; i++)
        {
            _previousPlayer = _session.Simulation.Player;
            CommandMapper.Fill(_input.Poll(_session.Simulation.Tick), _commands);
            if (_script is not null)
            {
                // The scripted run's world steps (mine, place, select) and its inventory requests, read after its poll.
                if (_script.TakeInventoryRequest() is { } wanted)
                {
                    SetInventory(wanted);
                }

                if (_script.TakeCraftingRequest() is { } craftingWanted)
                {
                    SetCrafting(craftingWanted);
                }

                _commands.AddRange(_script.Commands);
            }
            else
            {
                _live.Fill(_commands);
            }

            _session.Step(_commands);
        }

        // A typed line is evaluated here, between frames' ticks, never inside a step (P5 moves this to a worker).
        if (_dialogue.SubmitRequested)
        {
            SendTypedLine();
        }

        // --journal: open the journal once the scripted lines are sent (at the start when there are none).
        if (_options.Journal && !_journalShown && (_script is null || _script.IsDone))
        {
            _journalShown = true;
            OpenJournal();
        }

        _swallowTypedT = false;
        base.Update(gameTime);
    }

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        var view = _view!;
        var spriteBatch = _spriteBatch!;
        var viewport = GraphicsDevice.PresentationParameters;
        _camera.Resize(viewport.BackBufferWidth, viewport.BackBufferHeight);

        var player = _session.Simulation.Player;
        var alpha = _accumulator.Alpha;
        var x = Lerp(_previousPlayer.X, player.X, alpha);
        var y = Lerp(_previousPlayer.Y, player.Y, alpha);
        view.PlayerUnits = new Rectangle(x, y, PhysicsConstants.PlayerWidth, PhysicsConstants.PlayerHeight);
        view.Facing = player.Facing;
        view.Fps = _stats.Fps;
        view.DroppedTicks = _accumulator.DroppedTicks;
        var mouse = Mouse.GetState();
        view.Mouse = !_options.IsScreenshotRun && GraphicsDevice.Viewport.Bounds.Contains(mouse.Position) ? mouse.Position : null;
        _camera.Follow(x + (PhysicsConstants.PlayerWidth / 2), y + (PhysicsConstants.PlayerHeight / 2));

        GraphicsDevice.Clear(WorldRenderer.Sky);
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _worldRenderer!.Draw(spriteBatch, view);
        foreach (var layer in _layers)
        {
            layer.Draw(spriteBatch, view);
        }

        spriteBatch.End();
        base.Draw(gameTime);

        // --frames n: run until n ticks have been simulated (one per frame on the fixed clock), then save this frame.
        if (_options.ScreenshotPath is { } path && _session.Simulation.Tick >= _options.Frames)
        {
            ExitCode = ScreenshotWriter.TrySave(GraphicsDevice, path) ? 0 : 1;
            Exit();
        }
    }

    /// <inheritdoc />
    protected override void UnloadContent()
    {
        _atlas?.Dispose();
        _pixel?.Dispose();
        _spriteBatch?.Dispose();
        base.UnloadContent();
    }

    private static int Lerp(int from, int to, double alpha) => from + (int)Math.Round((to - from) * alpha);

    private static char? KeyCharacter(Keys key) => key switch
    {
        >= Keys.A and <= Keys.Z => (char)('a' + (key - Keys.A)),
        Keys.D1 => '1',
        Keys.OemQuestion => '/',
        _ => null,
    };

    // The window's text event: a character the layout, a compose key or the platform typed. Control characters belong to the key handling
    // below, and Alt combinations to the accent shortcuts, so neither is typed twice.
    private void OnTextInput(object? sender, TextInputEventArgs args)
    {
        if (!_dialogue.IsOpen || char.IsControl(args.Character))
        {
            return;
        }

        var keys = Keyboard.GetState();
        if (keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt))
        {
            return;
        }

        if (_swallowTypedT && char.ToLowerInvariant(args.Character) == 't')
        {
            _swallowTypedT = false; // the T that opened the panel
            return;
        }

        _dialogue.Entry.Insert(args.Character);
    }

    // Keys read once per frame from the keyboard state (edges against the previous frame, auto-repeat counted in frames).
    private void ReadKeyboard()
    {
        var keys = Keyboard.GetState();
        var pressed = (Keys key) => keys.IsKeyDown(key) && !_previousKeys.IsKeyDown(key);
        if (_journal.IsOpen)
        {
            if (pressed(Keys.Escape) || pressed(Keys.J))
            {
                CloseJournal();
            }
        }
        else if (_inventory.IsOpen)
        {
            if (pressed(Keys.Escape) || pressed(Keys.I) || pressed(Keys.E))
            {
                CloseInventory();
            }
        }
        else if (_crafting.IsOpen)
        {
            if (pressed(Keys.Escape) || pressed(Keys.C))
            {
                CloseCrafting();
            }
        }
        else if (_dialogue.IsOpen)
        {
            var alt = keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);
            var shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
            if (pressed(Keys.Escape))
            {
                _dialogue.Close();
                _session.EndReview();
            }
            else if (pressed(Keys.Enter))
            {
                _dialogue.RequestSubmit();
            }

            var entry = _dialogue.Entry;
            if (_backspace.Step(keys.IsKeyDown(Keys.Back)))
            {
                entry.Backspace();
            }

            if (_delete.Step(keys.IsKeyDown(Keys.Delete)))
            {
                entry.Delete();
            }

            if (_left.Step(keys.IsKeyDown(Keys.Left)))
            {
                entry.MoveLeft();
            }

            if (_right.Step(keys.IsKeyDown(Keys.Right)))
            {
                entry.MoveRight();
            }

            if (pressed(Keys.Home))
            {
                entry.Home();
            }

            if (pressed(Keys.End))
            {
                entry.End();
            }

            if (alt)
            {
                foreach (var key in keys.GetPressedKeys())
                {
                    if (pressed(key) && KeyCharacter(key) is { } typed && AccentShortcuts.Map(typed, shift) is { } accented)
                    {
                        entry.Insert(accented);
                    }
                }
            }
        }
        else if (pressed(Keys.T) && _session.IsNearGuide)
        {
            _dialogue.Open(_session.BeginTalk()?.Question);
            _swallowTypedT = true;
        }
        else if (pressed(Keys.J))
        {
            OpenJournal();
        }
        else if (pressed(Keys.I) || pressed(Keys.E))
        {
            OpenInventory();
        }
        else if (pressed(Keys.C))
        {
            OpenCrafting();
        }
        else if (pressed(Keys.Escape) && _session.Pointer is not null)
        {
            _session.DismissPointer();
        }

        ReadWorldInput(keys);
        _previousKeys = keys;
    }

    // The number keys and the wheel pick the hotbar slot; the mouse mines (held) and places (pressed) on the tile under it. Nothing acts
    // while the dialogue or the journal has the screen, and the mouse does nothing while the inventory is shown.
    private void ReadWorldInput(KeyboardState keys)
    {
        var mouse = Mouse.GetState();
        var inside = GraphicsDevice.Viewport.Bounds.Contains(mouse.Position);
        var textOrJournal = _dialogue.IsOpen || _journal.IsOpen;
        int? slot = null;
        if (!textOrJournal)
        {
            for (var i = 0; i < SlotKeys.Length; i++)
            {
                if (keys.IsKeyDown(SlotKeys[i]) && !_previousKeys.IsKeyDown(SlotKeys[i]))
                {
                    slot = i;
                }
            }

            var wheel = mouse.ScrollWheelValue - _previousScroll;
            if (wheel != 0)
            {
                slot = ((_session.Simulation.SelectedSlot - Math.Sign(wheel)) % Inventory.HotbarSize + Inventory.HotbarSize) % Inventory.HotbarSize;
            }
        }

        var rightDown = mouse.RightButton == ButtonState.Pressed;
        var worldOpen = textOrJournal || _inventory.IsOpen || _crafting.IsOpen;
        var hovered = inside ? _camera.ScreenToTile(mouse.X, mouse.Y) : (TilePoint?)null;
        _live.SetFrame(
            hovered,
            !worldOpen && inside && mouse.LeftButton == ButtonState.Pressed,
            !worldOpen && inside && rightDown && !_previousRightDown,
            slot);
        var leftDown = mouse.LeftButton == ButtonState.Pressed;
        if (_crafting.IsOpen && inside && leftDown && !_previousLeftDown)
        {
            var layout = new CraftingLayout(GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight, _session.Recipes.Entries.Count);
            if (layout.RowAt(mouse.X, mouse.Y) is { } row)
            {
                _live.QueueCraft(row);
            }
        }

        _previousLeftDown = leftDown;
        _previousRightDown = rightDown;
        _previousScroll = mouse.ScrollWheelValue;
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

        var lemmas = _session.Simulation.Inventory.Slots
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

    private void OpenCrafting()
    {
        if (_journal.IsOpen || _inventory.IsOpen)
        {
            return;
        }

        _crafting.Open(_session.Words, CraftingScreen.LemmasShown(_session.Recipes));
        _focus.CraftingOpen = true;
    }

    private void CloseCrafting()
    {
        _crafting.Close();
        _focus.CraftingOpen = false;
    }

    // The scripted run asks for the panel: open it (as the C key would) or close it.
    private void SetCrafting(bool open)
    {
        if (open)
        {
            if (!_crafting.IsOpen)
            {
                OpenCrafting();
            }
        }
        else if (_crafting.IsOpen)
        {
            CloseCrafting();
        }
    }

    // The scripted run asks for the screen: open it (as a key would) or close it.
    private void SetInventory(bool open)
    {
        if (open)
        {
            if (!_inventory.IsOpen)
            {
                OpenInventory();
            }
        }
        else if (_inventory.IsOpen)
        {
            CloseInventory();
        }
    }

    private void SendTypedLine()
    {
        TilePoint? cursor = null;
        if (!_options.IsScreenshotRun)
        {
            var mouse = Mouse.GetState();
            if (GraphicsDevice.Viewport.Bounds.Contains(mouse.Position))
            {
                cursor = _camera.ScreenToTile(mouse.X, mouse.Y);
            }
        }
        else
        {
            cursor = _cursor?.Resolve(_session);
        }

        var timer = TurnTimer.Start();
        var turn = _dialogue.Submit(line => _session.Say(line, cursor), () => timer.ElapsedMilliseconds);
        if (turn is not null)
        {
            Console.WriteLine($"turn {_dialogue.TurnCount}: \"{turn.Line}\" -> {turn.Summary}, {_dialogue.LastTurnMilliseconds:0.0} ms, state {turn.AgentState}");
            foreach (var line in turn.GuideLines)
            {
                Console.WriteLine($"  guide: {line}");
            }

            if (turn.Pointer is { } arrow)
            {
                Console.WriteLine($"  arrow: {arrow.Place} at {arrow.Target.X},{arrow.Target.Y} ({arrow.Blocks} blocks)");
            }

            ReportWords();
        }
    }

    // What the turn did to the learner's words: each credited noun with its uses so far, and each newly known one as a toast.
    private void ReportWords()
    {
        var result = _session.LastWordTurn;
        if (result is null)
        {
            return;
        }

        foreach (var early in result.TooEarly)
        {
            var label = _session.Nouns.ForLemma(early.Lemma)?.WithDefinite ?? early.Lemma;
            Console.WriteLine($"  early: {label}, no colour yet, {JournalRows.DueText(early.SecondsRemaining)}");
        }

        foreach (var none in result.Outcomes.Where(static o => o.Outcome is CreditOutcome.NoTarget or CreditOutcome.NamedWrong))
        {
            var label = _session.Nouns.ForLemma(none.Lemma)?.WithDefinite ?? none.Lemma;
            Console.WriteLine($"  no colour: {label}, {(none.Outcome == CreditOutcome.NoTarget ? "nothing to check it against" : "not the name of that tile")}");
        }

        if (result.TooEarly.Count > 0)
        {
            _toasts.Show(EarlyHint);
            _dialogue.AddHint(EarlyHint);
        }

        foreach (var lemma in result.Credited)
        {
            var noun = _session.Nouns.ForLemma(lemma);
            var uses = Math.Min(WordProgress.UsesToKnow, _session.Words.RecordOf(lemma).CreditUnits / WordProgress.UnitsPerUse);
            Console.WriteLine($"  word: {noun?.WithDefinite ?? lemma}, {uses} de {WordProgress.UsesToKnow}");
        }

        foreach (var lemma in result.NewlyKnown)
        {
            var noun = _session.Nouns.ForLemma(lemma);
            var label = noun?.WithDefinite ?? lemma;
            _toasts.Show($"¡Nueva palabra: {label}!");
            Console.WriteLine($"  known: {label} at tick {_session.Simulation.Tick}");
        }
    }
}
