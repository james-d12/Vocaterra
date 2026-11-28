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

/// <summary>
/// The engine host of the title screen (Q01): a window of its own that opens before any world is built, shows <see cref="TitleScreen"/> and ends when the player has chosen (<see cref="Choice"/>: a world to start,
/// or Quit, which closing the window also is). It keeps the player's settings in the settings file as the Esc menu does. A scripted run (<c>--screenshot</c> with <c>--title</c>) draws a few frames on the fixed
/// clock, saves the picture and ends. The game proper is <see cref="VoxelGame"/>, made after this window has closed.
/// </summary>
public sealed class TitleGame : Microsoft.Xna.Framework.Game
{
    private const int StartWidth = 1280;
    private const int StartHeight = 720;
    private const int ScriptedFrames = 4;

    private readonly LaunchOptions _options;
    private readonly LocaleText _ui;
    private readonly PlayerSettings _settings;
    private readonly SettingsPersistence? _persistence;
    private readonly GraphicsDeviceManager _graphics;
    private readonly UiCanvas _canvas;
    private readonly InputDevices _devices;
    private readonly TitleScreen _title;
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private FontProvider? _fonts;
    private readonly TitleNotice? _notice;
    private int _frames;

    /// <summary>Creates the title screen's window.</summary>
    /// <param name="options">The launch options (the window, the text size and the scripted picture's path).</param>
    /// <param name="ui">The locale whose words the title uses.</param>
    /// <param name="store">The worlds on disk.</param>
    /// <param name="saved">The settings the settings file holds, or <see langword="null"/> for the defaults.</param>
    /// <param name="settingsPath">The settings file the settings page keeps its changes in, or <see langword="null"/> for none (a scripted run).</param>
    /// <param name="randomSeed">Draws a random seed (the New game page's Random button).</param>
    /// <param name="notice">A line to show on the page it opens on (a world that did not load), or <see langword="null"/>.</param>
    public TitleGame(LaunchOptions options, LocaleText ui, WorldStore store, SaveSettings? saved, string? settingsPath, Func<ulong> randomSeed, TitleNotice? notice)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(store);
        _options = options;
        _ui = ui;
        _settings = PlayerSettings.Resolve(saved, options.ToLaunchSettings());
        _canvas = new UiCanvas(_settings.TextPercent, () => (GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
        _devices = new InputDevices(_canvas);
        var (width, height) = options.StartsFullScreen ? DisplaySize() : (options.WindowWidth ?? StartWidth, options.WindowHeight ?? StartHeight);
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = width,
            PreferredBackBufferHeight = height,
            SynchronizeWithVerticalRetrace = !options.IsScreenshotRun,
            HardwareModeSwitch = false, // borderless full screen at the desktop's mode, as the game's
            IsFullScreen = options.StartsFullScreen,
        };
        IsFixedTimeStep = false;
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Vocaterra";
        Window.TextInput += OnTextInput;
        var metrics = new TitleMetrics(() => (_canvas.Width, _canvas.Height));
        var rows = MenuRows.Settings(_settings, null); // the testing section needs a world
        var menu = new MenuScreen(rows, new MenuMetrics(() => (_canvas.Width, _canvas.Height), () => rows, static _ => []), static () => { });
        _title = new TitleScreen(store, metrics, menu, randomSeed, number => ui.Ui("titleDefaultName", ("n", number.ToString(CultureInfo.InvariantCulture))));
        _settings.Changed += ApplySetting;
        if (settingsPath is not null)
        {
            _persistence = new SettingsPersistence(settingsPath, _settings, static () => (long)MonotonicClock.Milliseconds(), Console.WriteLine);
        }

        _notice = notice;
    }

    /// <summary>What the player chose: a world to start or Quit; <see langword="null"/> when the window was closed (the same as Quit) or the scripted picture was taken.</summary>
    public TitleChoice? Choice => _title.Choice;

    /// <summary>The process exit code: 0 unless the scripted picture could not be saved.</summary>
    public int ExitCode { get; private set; }

    /// <inheritdoc />
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _fonts = new FontProvider(AssetPaths.Font);
        _devices.Start();
        _title.Start(_options.TitlePage ?? TitlePage.Main, _notice); // the pages ask the canvas for its size, which the window gives once it is open
        if (_options.TitleName is not null || _options.TitleSeed is not null)
        {
            _title.Prefill(_options.TitleName, _options.TitleSeed);
        }

        base.LoadContent();
    }

    /// <inheritdoc />
    protected override void Update(GameTime gameTime)
    {
        _frames++;
        var input = _devices.Read();
        _title.HandleInput(input);
        _devices.Commit(input);
        _persistence?.Pump();
        if (_title.Choice is not null)
        {
            Exit();
        }

        base.Update(gameTime);
    }

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        var viewport = GraphicsDevice.PresentationParameters;
        var (canvasWidth, canvasHeight) = _canvas.For(viewport.BackBufferWidth, viewport.BackBufferHeight);
        UiTransform.Set(viewport.BackBufferWidth, viewport.BackBufferHeight, canvasWidth, canvasHeight);
        var caretOn = _options.IsScreenshotRun || ((long)MonotonicClock.Milliseconds() / 500) % 2 == 0;
        _spriteBatch!.Begin(samplerState: SamplerState.PointClamp, transformMatrix: UiTransform.Matrix);
        TitleDrawer.Draw(_spriteBatch, _pixel!, _fonts!, _ui, _title, canvasWidth, canvasHeight, caretOn);
        _spriteBatch.End();
        base.Draw(gameTime);
        if (_options.IsScreenshotRun && _frames >= ScriptedFrames)
        {
            ExitCode = ScreenshotWriter.TrySave(GraphicsDevice, _options.ScreenshotPath!) ? 0 : 1;
            Exit();
        }
    }

    /// <inheritdoc />
    protected override void UnloadContent()
    {
        Window.TextInput -= OnTextInput;
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

    // The window's text event: the letters of the name and seed fields (the key that opened the page is a control character, which the screen ignores).
    private void OnTextInput(object? sender, TextInputEventArgs args) => _title.Type(args.Character);

    // The settings the title shows take effect here as in the menu: the text size now, and a quality level sets the graphics toggles (each can be changed alone after it); the rest is read when a world starts.
    private void ApplySetting(SettingId id)
    {
        if (id == SettingId.TextSize)
        {
            _canvas.Set(_settings.TextPercent);
        }
        else if (id == SettingId.Quality)
        {
            var preset = QualityPreset.For(_settings.Quality);
            _settings.Set(SettingId.Shaders, preset.Shaders ? 1 : 0);
            GraphicsEffects.Of(preset).WriteTo(_settings);
        }
    }

    private static (int Width, int Height) DisplaySize()
    {
        var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        return (mode.Width, mode.Height);
    }
}
