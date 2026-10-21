using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Physics;

namespace Vocaterra.MonoGame;

/// <summary>
/// The engine host. Each frame it reads the real clock (<see cref="IFrameClock"/>), lets the <see cref="TickAccumulator"/> turn it into whole
/// ticks (capped), polls the <see cref="IInputSource"/> once per tick, maps it to <c>PlayerCommand</c>s and steps the simulation; then it
/// follows the player with the camera, draws the world, then each <see cref="IFrameLayer"/> in order. All rules live in
/// <c>Vocaterra.Simulation</c> and in the plain classes under <c>Frontend/</c>; this class only wires them to MonoGame.
/// </summary>
public sealed class VocaterraGame : Game
{
    private const int Zoom = 2;
    private const int StartWidth = 1280;
    private const int StartHeight = 720;

    private readonly LaunchOptions _options;
    private readonly GraphicsDeviceManager _graphics;
    private readonly GameSimulation _simulation;
    private readonly Camera _camera;
    private readonly TickAccumulator _accumulator = new(GameSimulation.TicksPerSecond);
    private readonly FrameStats _stats = new();
    private readonly IFrameClock _clock;
    private readonly IInputSource _input;
    private readonly List<PlayerCommand> _commands = [];
    private readonly List<IFrameLayer> _layers = [];
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private TileAtlas? _atlas;
    private WorldRenderer? _worldRenderer;
    private FrameView? _view;
    private PlayerState _previousPlayer;
    private bool _firstUpdateSeen;

    /// <summary>Creates the game for a seed and options; the world is generated here.</summary>
    public VocaterraGame(LaunchOptions options)
    {
        _options = options;
        _simulation = GameSimulation.Create(options.Seed);
        _camera = new Camera(_simulation.World.Width, _simulation.World.Height, TileArt.Size * Zoom);
        _previousPlayer = _simulation.Player;
        if (options.IsScreenshotRun)
        {
            _clock = new FixedFrameClock(GameSimulation.TicksPerSecond);
            _input = new ScriptedInput(options.WalkRightTicks);
        }
        else
        {
            _clock = new RealFrameClock();
            _input = new KeyboardInput();
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
            Simulation = _simulation,
            Camera = _camera,
            Fonts = new FontProvider(AssetPaths.Font),
            Pixel = _pixel,
            Seed = _options.Seed,
        };
        _layers.Add(new DebugOverlay());
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
        var steps = _accumulator.Advance(seconds);
        for (var i = 0; i < steps; i++)
        {
            _previousPlayer = _simulation.Player;
            CommandMapper.Fill(_input.Poll(_simulation.Tick), _commands);
            _simulation.Step(_commands);
        }

        base.Update(gameTime);
    }

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        var view = _view!;
        var spriteBatch = _spriteBatch!;
        var viewport = GraphicsDevice.PresentationParameters;
        _camera.Resize(viewport.BackBufferWidth, viewport.BackBufferHeight);

        var player = _simulation.Player;
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
        if (_options.ScreenshotPath is { } path && _simulation.Tick >= _options.Frames)
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
}
