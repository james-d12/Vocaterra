using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation;
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

    /// <summary>Creates the game for a started session and options. The session already holds the generated world and the loaded bundle.</summary>
    public VocaterraGame(LaunchOptions options, GameSession session)
    {
        _options = options;
        _session = session;
        _camera = new Camera(session.Simulation.World.Width, session.Simulation.World.Height, TileArt.Size * Zoom);
        _previousPlayer = session.Simulation.Player;
        _dialogue = new DialogueModel(_focus, session.OpeningLine);
        if (options.IsScreenshotRun)
        {
            _clock = new FixedFrameClock(GameSimulation.TicksPerSecond);
            if (options.Says.Count > 0)
            {
                _script = new ScriptedDialogue(session, _dialogue, options.Says);
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
        };
        _layers.Add(new GuideLayer());
        _layers.Add(new PointerLayer());
        _layers.Add(new DebugOverlay());
        _layers.Add(new DialoguePanel());
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
            _session.Step(_commands);
        }

        // A typed line is evaluated here, between frames' ticks, never inside a step (P5 moves this to a worker).
        if (_dialogue.SubmitRequested)
        {
            SendTypedLine();
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
        if (_dialogue.IsOpen)
        {
            var alt = keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);
            var shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
            if (pressed(Keys.Escape))
            {
                _dialogue.Close();
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
            _dialogue.Open();
            _swallowTypedT = true;
        }
        else if (pressed(Keys.Escape) && _session.Pointer is not null)
        {
            _session.DismissPointer();
        }

        _previousKeys = keys;
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

        var timer = TurnTimer.Start();
        var turn = _dialogue.Submit(line => _session.Say(line, cursor), () => timer.ElapsedMilliseconds);
        if (turn is not null)
        {
            Console.WriteLine($"turn {_dialogue.TurnCount}: \"{turn.Line}\" -> {turn.Summary}, {_dialogue.LastTurnMilliseconds:0.0} ms, state {turn.AgentState}");
            foreach (var line in turn.GuideLines)
            {
                Console.WriteLine($"  guide: {line}");
            }
        }
    }
}
