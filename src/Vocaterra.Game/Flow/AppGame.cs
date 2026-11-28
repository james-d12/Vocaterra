using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Title;
using Vocaterra.Game.Core.Worlds;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Input;
using Vocaterra.Game.Startup;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Menu;
using Vocaterra.Game.UI.Title;

namespace Vocaterra.Game.Flow;

/// <summary>What starting a world from the title screen came to: the world, or why not (the title opens again on the Load page and says it), or an exit code (the run ends).</summary>
/// <param name="World">The world that was made, or <see langword="null"/>.</param>
/// <param name="Failure">Why the world could not be started, or <see langword="null"/>.</param>
/// <param name="ExitCode">The exit code when the run ends instead.</param>
internal sealed record WorldAttempt(StartedWorld? World, string? Failure = null, int ExitCode = 0);

/// <summary>
/// The engine host of a run (Q01): the one MonoGame window, which the title screen and then every world are played in, one after the other, so a world is not a window of its own (a second MonoGame window made in
/// the same process never starts its loop). A run that opens on the title starts there: <see cref="TitleScreen"/> until the player chooses, a frame of "making the world" while the program builds it, then the world
/// (<see cref="VoxelGame"/>) until it ends; its Main menu returns to the title, and a world chosen there starts in the same window, after the first world's streaming, far store, sound, session log and every other
/// thing it held have been closed. A run with a start flag, or a scripted one, starts in its world as it always did. A scripted <c>--title</c> run draws a few frames of the title on the fixed clock, saves the picture and ends.
/// </summary>
internal sealed class AppGame : Microsoft.Xna.Framework.Game, IGameHost
{
    private const int StartWidth = 1280;
    private const int StartHeight = 720;
    private const int ScriptedFrames = 4;

    private readonly LaunchOptions _options;
    private readonly LocaleText _ui;
    private readonly WorldStore _store;
    private readonly string? _settingsPath;
    private readonly Func<SaveSettings?> _readSettings;
    private readonly Func<ulong> _randomSeed;
    private readonly Func<TitleChoice, WorldAttempt>? _startWorld;
    private readonly Action<VoxelGame> _worldStarted;
    private readonly Action<VoxelGame> _worldEnded;
    private readonly GraphicsDeviceManager _graphics;
    private StartedWorld? _initial;
    private UiCanvas _canvas;
    private InputDevices _devices;
    private TitleScreen _title;
    private SettingsPersistence? _persistence;
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private FontProvider? _fonts;
    private VoxelGame? _world;
    private TitleChoice? _pending;
    private Phase _phase;
    private int _frames;

    private enum Phase
    {
        Title,
        Making,
        World,
        Done,
    }

    /// <summary>Creates the run's window.</summary>
    /// <param name="options">The launch options (the window, the text size and the scripted picture's path).</param>
    /// <param name="ui">The locale whose words the title uses.</param>
    /// <param name="store">The worlds on disk.</param>
    /// <param name="settingsPath">The settings file the settings pages keep their changes in, or <see langword="null"/> for none (a scripted run).</param>
    /// <param name="readSettings">Reads the settings file now (the title's settings and each world start from what it holds).</param>
    /// <param name="randomSeed">Draws a random seed (the New game page's Random button).</param>
    /// <param name="initial">The world the run opens in (a run with a start flag, or a scripted one), or <see langword="null"/> to open on the title.</param>
    /// <param name="startWorld">Makes the world of a title choice (the program's: the load or the new game, as the flags do), or <see langword="null"/> for a run that has no title.</param>
    /// <param name="worldStarted">Told when a world has started (the crash handler is given it).</param>
    /// <param name="worldEnded">Told when a world has ended, after it was closed (the program touches the world's last-played time).</param>
    /// <param name="notice">A line the title shows on its first page, or <see langword="null"/>.</param>
    public AppGame(LaunchOptions options, LocaleText ui, WorldStore store, string? settingsPath, Func<SaveSettings?> readSettings, Func<ulong> randomSeed, StartedWorld? initial, Func<TitleChoice, WorldAttempt>? startWorld, Action<VoxelGame> worldStarted, Action<VoxelGame> worldEnded, TitleNotice? notice)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(readSettings);
        ArgumentNullException.ThrowIfNull(randomSeed);
        _options = options;
        _ui = ui;
        _store = store;
        _settingsPath = settingsPath;
        _readSettings = readSettings;
        _randomSeed = randomSeed;
        _initial = initial;
        _startWorld = startWorld;
        _worldStarted = worldStarted;
        _worldEnded = worldEnded;
        var settings = PlayerSettings.Resolve(readSettings(), options.ToLaunchSettings());
        var (width, height) = options.StartsFullScreen ? DisplaySize() : (options.WindowWidth ?? StartWidth, options.WindowHeight ?? StartHeight);
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = width,
            PreferredBackBufferHeight = height,
            PreferredDepthStencilFormat = DepthFormat.Depth24,
            SynchronizeWithVerticalRetrace = !options.IsScreenshotRun,
            HardwareModeSwitch = false, // full screen is borderless at the desktop's mode, never a display mode change
            IsFullScreen = options.StartsFullScreen,
            PreferMultiSampling = settings.Msaa > 0, // B22: antialiased edges, the count the settings say when the window opens (the Antialiasing row takes effect at the next start)
        };
        var multiSamples = settings.Msaa;
        _graphics.PreparingDeviceSettings += (_, e) => e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = multiSamples;
        IsFixedTimeStep = false; // the world's accumulator owns the tick rate
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Vocaterra";
        Window.TextInput += OnTextInput;
        _phase = initial is null ? Phase.Title : Phase.World;
        _pendingNotice = notice;
        (_title, _canvas, _devices) = MakeTitle(settings);
    }

    private TitleNotice? _pendingNotice;

    /// <summary>The graphics device of the run's one window.</summary>
    GraphicsDevice IGameHost.GraphicsDevice => GraphicsDevice;

    /// <summary>The window.</summary>
    GameWindow IGameHost.Window => Window;

    /// <summary>The display's settings.</summary>
    GraphicsDeviceManager IGameHost.Graphics => _graphics;

    /// <summary>Whether the window has the focus.</summary>
    bool IGameHost.IsActive => IsActive;

    /// <summary>Whether the pointer is drawn over the window.</summary>
    bool IGameHost.IsMouseVisible
    {
        get => IsMouseVisible;
        set => IsMouseVisible = value;
    }

    /// <summary>The process exit code: the last world's, or 0 (1 when a scripted picture could not be saved).</summary>
    public int ExitCode { get; private set; }

    /// <inheritdoc />
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _fonts = new FontProvider(AssetPaths.Font);
        _devices.Start();
        if (_initial is { } initial)
        {
            _initial = null;
            StartWorld(initial);
        }
        else
        {
            ShowTitle(_options.TitlePage ?? TitlePage.Main, _pendingNotice);
            if (_options.TitleName is not null || _options.TitleSeed is not null)
            {
                _title.Prefill(_options.TitleName, _options.TitleSeed);
            }
        }

        base.LoadContent();
    }

    /// <inheritdoc />
    protected override void Update(GameTime gameTime)
    {
        _frames++;
        if (_world is { IsFinished: true })
        {
            EndWorld();
        }

        switch (_phase)
        {
            case Phase.Title:
                UpdateTitle();
                break;
            case Phase.Making:
                Make();
                break;
            case Phase.World:
                _world!.Update(gameTime);
                if (_world.IsFinished)
                {
                    EndWorld();
                }

                break;
            default:
                break;
        }

        base.Update(gameTime);
    }

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        if (_phase == Phase.World && _world is not null)
        {
            _world.Draw(gameTime);
            base.Draw(gameTime);
            return;
        }

        GraphicsDevice.Clear(Color.Black);
        var viewport = GraphicsDevice.PresentationParameters;
        var (canvasWidth, canvasHeight) = _canvas.For(viewport.BackBufferWidth, viewport.BackBufferHeight);
        UiTransform.Set(viewport.BackBufferWidth, viewport.BackBufferHeight, canvasWidth, canvasHeight);
        var caretOn = _options.IsScreenshotRun || ((long)MonotonicClock.Milliseconds() / 500) % 2 == 0;
        _spriteBatch!.Begin(samplerState: SamplerState.PointClamp, transformMatrix: UiTransform.Matrix);
        if (_phase == Phase.Making)
        {
            TitleDrawer.DrawMaking(_spriteBatch, _pixel!, _fonts!, _ui, canvasWidth, canvasHeight);
        }
        else if (_phase == Phase.Title)
        {
            TitleDrawer.Draw(_spriteBatch, _pixel!, _fonts!, _ui, _title, canvasWidth, canvasHeight, caretOn);
        }

        _spriteBatch.End();
        base.Draw(gameTime);
        if (_options.IsScreenshotRun && _phase == Phase.Title && _frames >= ScriptedFrames)
        {
            ExitCode = ScreenshotWriter.TrySave(GraphicsDevice, _options.ScreenshotPath!) ? 0 : 1;
            _phase = Phase.Done;
            Exit();
        }
    }

    /// <inheritdoc />
    protected override void UnloadContent()
    {
        Window.TextInput -= OnTextInput;
        if (_world is not null)
        {
            CloseWorld(); // the window was closed with a world open: it is saved and closed as an Exit closes it
        }

        _persistence?.Close();
        base.UnloadContent();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _spriteBatch?.Dispose();
            _pixel?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static (int Width, int Height) DisplaySize()
    {
        var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        return (mode.Width, mode.Height);
    }

    // The title screen over the player's settings as the settings file holds them now (a world may have changed them since the last title).
    private (TitleScreen Title, UiCanvas Canvas, InputDevices Devices) MakeTitle(PlayerSettings settings)
    {
        _persistence?.Close();
        var canvas = new UiCanvas(settings.TextPercent, () => (GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
        var devices = new InputDevices(canvas);
        var metrics = new TitleMetrics(() => (canvas.Width, canvas.Height));
        var rows = MenuRows.Settings(settings, null); // the testing section needs a world
        var menu = new MenuScreen(rows, new MenuMetrics(() => (canvas.Width, canvas.Height), () => rows, static _ => []), static () => { });
        var title = new TitleScreen(_store, metrics, menu, _randomSeed, number => _ui.Ui("titleDefaultName", ("n", number.ToString(CultureInfo.InvariantCulture))));
        settings.Changed += id => ApplyTitleSetting(settings, canvas, id);
        _persistence = _settingsPath is null ? null : new SettingsPersistence(_settingsPath, settings, static () => (long)MonotonicClock.Milliseconds(), Console.WriteLine);
        return (title, canvas, devices);
    }

    // The settings the title shows take effect here as in the menu: the text size now, and a quality level sets the graphics toggles (each can be changed alone after it); the rest is read when a world starts.
    private static void ApplyTitleSetting(PlayerSettings settings, UiCanvas canvas, SettingId id)
    {
        if (id == SettingId.TextSize)
        {
            canvas.Set(settings.TextPercent);
        }
        else if (id == SettingId.Quality)
        {
            var preset = QualityPreset.For(settings.Quality);
            settings.Set(SettingId.Shaders, preset.Shaders ? 1 : 0);
            GraphicsEffects.Of(preset).WriteTo(settings);
        }
    }

    private void ShowTitle(TitlePage page, TitleNotice? notice)
    {
        _phase = Phase.Title;
        Window.Title = "Vocaterra";
        IsMouseVisible = true;
        _title.Start(page, notice);
    }

    private void UpdateTitle()
    {
        var input = _devices.Read();
        _title.HandleInput(input);
        _devices.Commit(input);
        _persistence?.Pump();
        if (_title.Choice is not { } choice)
        {
            return;
        }

        if (choice.Kind == TitleChoiceKind.Quit || _startWorld is null)
        {
            _phase = Phase.Done;
            Exit();
            return;
        }

        _pending = choice;
        _phase = Phase.Making; // one frame says the world is being made; the next update makes it
    }

    private void Make()
    {
        var choice = _pending!;
        _pending = null;
        var attempt = _startWorld!(choice);
        if (attempt.World is { } started)
        {
            StartWorld(started);
        }
        else if (attempt.Failure is not null)
        {
            // The world cannot be started (its save is of another seed or an older version, or unreadable): the title opens again on the Load page and says why.
            (_title, _canvas, _devices) = MakeTitle(PlayerSettings.Resolve(_readSettings(), _options.ToLaunchSettings()));
            _devices.Start();
            ShowTitle(TitlePage.Load, new TitleNotice("titleLoadFailed", choice.World?.Name ?? string.Empty, attempt.Failure));
        }
        else
        {
            ExitCode = attempt.ExitCode;
            _phase = Phase.Done;
            Exit();
        }
    }

    private void StartWorld(StartedWorld started)
    {
        _persistence?.Close(); // the world keeps the settings file from here on
        _persistence = null;
        _world = new VoxelGame(this, started.Options, started.Session, started.Scene, started.Voice, savedSettings: _readSettings(), savedDialogue: started.SavedDialogue, settingsPath: _settingsPath);
        _world.Load();
        _phase = Phase.World;
        _worldStarted(_world);
    }

    // The world is over: it is closed (everything it held), then the title again for the Main menu, else the run ends.
    private void EndWorld()
    {
        var back = _world!.ReturnToTitle;
        CloseWorld();
        if (back)
        {
            Console.WriteLine("menu: back to the title screen");
            (_title, _canvas, _devices) = MakeTitle(PlayerSettings.Resolve(_readSettings(), _options.WithoutStartFlags().ToLaunchSettings()));
            _devices.Start();
            ShowTitle(TitlePage.Main, null);
        }
        else
        {
            _phase = Phase.Done;
            Exit();
        }
    }

    private void CloseWorld()
    {
        var world = _world!;
        _world = null;
        world.Unload();
        ExitCode = world.ExitCode;
        world.Dispose();
        _worldEnded(world);
    }

    // The window's text event: the letters of the name and seed fields on the title (the key that opened a page is a control character, which the screen ignores); a world reads its own.
    private void OnTextInput(object? sender, TextInputEventArgs args)
    {
        if (_phase == Phase.Title)
        {
            _title.Type(args.Character);
        }
    }
}
