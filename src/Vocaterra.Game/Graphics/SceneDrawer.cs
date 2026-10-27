using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.UI;
using Vocaterra.Game.Utilities;

namespace Vocaterra.Game.Graphics;

/// <summary>
/// The picture of a frame: the voxel world through the player's camera (<see cref="VoxelRenderer"/>), then the 2D layers over it on the interface
/// canvas, magnified to the window (X08). It owns the device's resources (the sprite batch, the pixel, the block atlas, the renderer) and the
/// greyscale colours of the world (D-game-6).
/// </summary>
public sealed class SceneDrawer : IDisposable
{
    private readonly LaunchOptions _options;
    private readonly VoxelSession _session;
    private readonly ToastQueue _toasts;
    private readonly IReadOnlyList<NpcFigure> _figures;
    private readonly Action<string> _console;
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private BlockAtlas? _blockAtlas;
    private VoxelRenderer? _renderer;
    private GreyscaleWorld? _colours;
    private IReadOnlyList<IFrameLayer> _layers = [];
    private FrameView? _view;

    /// <summary>Creates the drawer of a run (nothing is made until <see cref="Load"/>).</summary>
    /// <param name="options">The run's options: the quality, the dusk, the greyscale and the nouns assumed known.</param>
    /// <param name="session">The session whose world is drawn.</param>
    /// <param name="toasts">Where a noun that became known is toasted in greyscale.</param>
    /// <param name="figures">The NPCs' figures.</param>
    /// <param name="console">Where the drawer's start-up lines go.</param>
    public SceneDrawer(LaunchOptions options, VoxelSession session, ToastQueue toasts, IReadOnlyList<NpcFigure> figures, Action<string> console)
    {
        _options = options;
        _session = session;
        _toasts = toasts;
        _figures = figures;
        _console = console;
    }

    /// <summary>The quality preset (high until <see cref="Load"/> takes the run's).</summary>
    public QualityPreset Preset { get; private set; } = QualityPreset.For(QualityLevel.High);

    /// <summary>The renderer, after <see cref="Load"/>.</summary>
    public VoxelRenderer Renderer => _renderer ?? throw new InvalidOperationException("The scene is not loaded.");

    /// <summary>The frame's view the layers draw from, after <see cref="Load"/>.</summary>
    public FrameView View => _view ?? throw new InvalidOperationException("The scene is not loaded.");

    /// <summary>
    /// Makes the device's resources and the renderer, says the quality, the device and the greyscale, and makes the frame's view (with the pixel and
    /// the block icons it needs) and the layers.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="makeView">Makes the frame's view from the one-pixel texture and the block atlas.</param>
    /// <param name="layers">The 2D layers, bottom to top.</param>
    public void Load(GraphicsDevice device, Func<Texture2D, BlockAtlas, FrameView> makeView, IReadOnlyList<IFrameLayer> layers)
    {
        _spriteBatch = new SpriteBatch(device);
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData([Color.White]);
        _blockAtlas = new BlockAtlas(device);
        var started = MonotonicClock.Milliseconds();
        Preset = _options.QualityPreset;
        _renderer = new VoxelRenderer(device, _blockAtlas, _session.Simulation.Grid, Preset, _options.Dusk);
        _renderer.SetFigures(_figures);
        _colours = new GreyscaleWorld(_options.Greyscale, _session.Nouns, _session.Words, _toasts, _renderer.SetGreyKinds, _options.AssumeKnown);
        _console($"voxel: quality {Preset.Name}, view distance {Preset.ViewDistance} blocks, fog from {VoxelRenderer.FogEndFor(Preset.ViewDistance) * VoxelRenderer.FogStartFraction:0} to {VoxelRenderer.FogEndFor(Preset.ViewDistance):0}, mesh budget {Preset.MeshBudgetMilliseconds:0} ms a frame (renderer ready in {MonotonicClock.Milliseconds() - started:0} ms)");
        _console($"graphics: {GraphicsAdapter.DefaultAdapter.Description}, depth {device.PresentationParameters.DepthStencilFormat}");
        if (_options.Greyscale)
        {
            _console("greyscale: blocks whose noun is not known are grey (the playtest A/B, D-game-6); figures, sky and UI in colour");
        }

        _view = makeView(_pixel, _blockAtlas);
        _layers = layers;
    }

    /// <summary>
    /// Draws the world: the grey set follows the learner's words, the chunks are meshed (a scripted run builds every mesh it needs before its
    /// picture; a live one spends its budget a frame, more while the world is still loading in), and the world, the target block, the beacon and
    /// the player's figure or arm are drawn.
    /// </summary>
    public void DrawWorld(GraphicsDevice device, FirstPersonCamera camera, BlockPos? target, BlockPos? beacon, long tick, PlayerAvatar? avatar, ArmFrame? arm)
    {
        var renderer = Renderer;
        _colours!.Refresh();
        renderer.Remesh(camera.Eye, camera.Frustum, _options.IsScreenshotRun ? null : _session.Worker!.IsReady ? Preset.MeshBudgetMilliseconds : 30);
        device.Clear(renderer.ClearColour);
        renderer.DrawWorld(camera, target, beacon, tick, avatar, arm);
    }

    /// <summary>Draws the 2D layers, laid out on the canvas of <paramref name="canvasWidth"/> by <paramref name="canvasHeight"/> and magnified to the window.</summary>
    public void DrawLayers(int width, int height, int canvasWidth, int canvasHeight)
    {
        var spriteBatch = _spriteBatch!;
        UiTransform.Set(width, height, canvasWidth, canvasHeight);
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: UiTransform.Matrix);
        foreach (var layer in _layers)
        {
            layer.Draw(spriteBatch, View);
        }

        spriteBatch.End();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _renderer?.Dispose();
        _blockAtlas?.Dispose();
        _pixel?.Dispose();
        _spriteBatch?.Dispose();
    }
}
