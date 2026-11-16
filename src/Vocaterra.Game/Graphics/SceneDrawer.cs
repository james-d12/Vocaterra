using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.Camera;
using Vocaterra.Game.Graphics.Figures;
using Vocaterra.Game.Graphics.Sky;
using Vocaterra.Game.Graphics.Water;
using Vocaterra.Game.Graphics.Weather;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Startup;
using Vocaterra.Game.Streaming;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Hud;

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
    private WaterTextures? _waterTextures;
    private VoxelRenderer? _renderer;
    private GreyscaleWorld? _colours;
    private IReadOnlyList<IFrameLayer> _layers = [];
    private FrameView? _view;
    private readonly UnderwaterView _underwater = new();
    private readonly UnderwaterView _molten = new(); // the same easing for the eye in lava (B10b)
    private const float LavaTintStrength = 0.6f;
    private UnderwaterPass? _underwaterPass;
    private double _lastDrawMilliseconds = double.NaN;
    private StreamedWorldHost? _streaming;
    private readonly Vocaterra.Game.Diagnostics.MoveProbe _moves = new();

    // The most a frame spends meshing the near zone it has just made (B07b); the rest of the view fills in over the next frames.
    private const double NearZoneMeshMilliseconds = 400;

    // The near view the probe waits for: chunks within this many chunks of the eye across the ground (64 blocks).
    private const int NearViewChunks = 4;
    private readonly Vocaterra.Game.Core.Reporting.BusyFrameStats _streamTimes = new();
    private readonly Vocaterra.Game.Core.Reporting.BusyFrameStats _remeshTimes = new();

    /// <summary>Creates the drawer of a run (nothing is made until <see cref="Load"/>).</summary>
    /// <param name="options">The run's options: the quality, the greyscale and the nouns assumed known.</param>
    /// <param name="session">The session whose world is drawn.</param>
    /// <param name="toasts">Where a noun that became known is toasted in greyscale.</param>
    /// <param name="figures">The NPCs' figures.</param>
    /// <param name="console">Where the drawer's start-up lines go.</param>
    public SceneDrawer(LaunchOptions options, VoxelSession session, ToastQueue toasts, IReadOnlyList<NpcFigure> figures, Action<string> console)
    {
        _options = options;
        BlurLevel = () => options.UnderwaterBlur ?? UnderwaterBlurSettings.Default;
        _session = session;
        _toasts = toasts;
        _figures = figures;
        _console = console;
    }

    /// <summary>The quality preset in force (high until <see cref="ApplyQuality"/> gives another).</summary>
    public QualityPreset Preset { get; private set; } = QualityPreset.For(QualityLevel.High);

    /// <summary>
    /// Takes a graphics quality and a draw distance (the run's start, then the menu's settings): the preset of the level, drawn as far as <paramref name="viewDistance"/> says when the
    /// player chose one (Auto, <see langword="null"/>, is the level's own distance). Before <see cref="Load"/> it only chooses the preset the renderer starts with; after it the
    /// renderer follows at once (its meshes are built again), and nothing is rebuilt when the preset is the one in force.
    /// </summary>
    /// <param name="level">The level.</param>
    /// <param name="viewDistance">The draw distance in blocks, or <see langword="null"/> for the level's own.</param>
    public void ApplyQuality(QualityLevel level, int? viewDistance = null)
    {
        Preset = viewDistance is { } blocks ? QualityPreset.For(level).WithViewDistance(blocks) : QualityPreset.For(level);
        if (_options.FarViewRange is { } farRange)
        {
            Preset = Preset.WithFarRange(farRange); // --far-view
        }

        _renderer?.SetPreset(Preset);
        if (_streaming is not null && _renderer is not null)
        {
            _streaming.ApplyPreset(Preset, VoxelRenderer.ReleaseFarTile);
            _renderer.SetFarView(_streaming.Far);
        }
    }

    /// <summary>The time each frame spent streaming the world (the residency and the far view) and meshing chunks, for the stream stats line.</summary>
    public (Vocaterra.Game.Core.Reporting.BusyFrameStats Streaming, Vocaterra.Game.Core.Reporting.BusyFrameStats Remesh) PhaseTimes => (_streamTimes, _remeshTimes);

    /// <summary>The streamed world's host (the worker threads, the chunks' residency and the far view), or <see langword="null"/> in the bounded valley (B07).</summary>
    public StreamedWorldHost? Streaming => _streaming;

    /// <summary>The colours of the world (the greyscale view and what counts as known in it), after <see cref="Load"/>; <see langword="null"/> before.</summary>
    public GreyscaleWorld? Colours => _colours;

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
        _waterTextures = new WaterTextures(device);
        _renderer = new VoxelRenderer(device, _blockAtlas, _waterTextures, _session.Simulation.Grid, Preset, SkyLook.At(SkyLookInput.At(_session.Simulation.Atmosphere)), _session.Scene.Currents, _console);
        _renderer.SetFigures(_figures);
        _streaming = StreamedWorldHost.For(_session.Scene, Preset, _options.Prefetch ?? JobPool.MaxWorkers);
        _renderer.SetFarView(_streaming?.Far);
        _colours = new GreyscaleWorld(_options.Greyscale, _session.Nouns, _session.Words, _toasts, _renderer.SetGreyKinds, _session.Locale, _options.AssumeKnown);
        var fogEnd = _renderer.FarRange > 0 ? VoxelRenderer.FarFogEnd(Preset.ViewDistance, _renderer.FarRange) : VoxelRenderer.FogEndFor(Preset.ViewDistance);
        var fogStart = fogEnd * (_renderer.FarRange > 0 ? VoxelRenderer.FarFogStartFraction : VoxelRenderer.FogStartFraction);
        _console($"voxel: quality {Preset.Name}, view distance {Preset.ViewDistance} blocks, fog from {fogStart:0} to {fogEnd:0}, mesh budget {Preset.MeshBudgetMilliseconds:0} ms a frame (renderer ready in {MonotonicClock.Milliseconds() - started:0} ms)");
        if (_streaming is not null)
        {
            _console($"world: streamed, {_streaming.Pool.Workers} worker thread(s), far view {(_renderer.FarRange > 0 ? $"to {_renderer.FarRange} blocks" : "off")}"); // the bounded valley prints nothing: its runs compare byte for byte
        }

        _console($"graphics: {GraphicsAdapter.DefaultAdapter.Description}, depth {device.PresentationParameters.DepthStencilFormat}, {(device.PresentationParameters.IsFullScreen ? "full screen" : "window")} {device.PresentationParameters.BackBufferWidth}x{device.PresentationParameters.BackBufferHeight}");
        if (_options.Greyscale)
        {
            _console("greyscale: blocks whose noun is not known are grey (the playtest A/B, D-game-6); figures, sky and UI in colour");
        }

        _underwaterPass = new UnderwaterPass(device, _spriteBatch, _pixel);
        _view = makeView(_pixel, _blockAtlas);
        _layers = layers;
    }

    /// <summary>The blur level in force now (the setting, or off with the view effects off); the launch option's level until the game says otherwise.</summary>
    public Func<UnderwaterBlur> BlurLevel { get; set; }

    /// <summary>How far under water the view is, 0 to 1 (<c>UnderwaterView.Amount</c>): the sound side muffles the beds by it (H10).</summary>
    public double UnderwaterAmount => _underwater.Amount;

    /// <summary>
    /// Draws the world: the grey set follows the learner's words, the chunks are meshed (a scripted run builds every mesh it needs before its
    /// picture; a live one spends its budget a frame, more while the world is still loading in), and the world, the target block, the beacon and
    /// the player's figure or arm are drawn. The sky, the fog and the world's light follow the look of the moment (E02).
    /// </summary>
    public void DrawWorld(GraphicsDevice device, FirstPersonCamera camera, BlockPos? target, BlockPos? beacon, long tick, PlayerAvatar? avatar, ArmFrame? arm, in SkyLook look, Vector3? streamEye = null)
    {
        var renderer = Renderer;
        var nearZoneMade = false;
        var frameBegan = MonotonicClock.Milliseconds();
        renderer.SetAtmosphere(look); // before the meshing: a change of light band is noticed by this frame's Remesh
        var channels = _session.Simulation.Atmosphere.Channels;
        renderer.SetPrecipitation(new PrecipitationState(channels.PrecipitationKind, channels.PrecipitationIntensity, channels.Wind)); // the rain's pass reads the channels, never a kind's id (E06)
        _colours!.Refresh();
        if (_streaming is { } streaming)
        {
            // The chunks and the far land round the eye (B07): a live frame installs a few within its budget, a scripted one has everything in place before its picture.
            var eye = streamEye ?? camera.Eye; // the body's own eye, not the camera's between two ticks: the world is made for where the body is, which matters while the simulation waits for it
            var phaseStart = MonotonicClock.Milliseconds();
            nearZoneMade = false;
            if (_options.IsScreenshotRun && !_options.NoSettle)
            {
                _ = streaming.Settle(eye.X, eye.Y, eye.Z, VoxelRenderer.ReleaseFarTile);
            }
            else
            {
                streaming.Update(eye.X, eye.Y, eye.Z, VoxelRenderer.ReleaseFarTile);
                nearZoneMade = streaming.MakeNearZone(eye.X, eye.Y, eye.Z); // a start, a jump, a respawn, a load: the near zone is made now and meshed below, as a start's always was (B07b)
            }

            _streamTimes.Record(MonotonicClock.Milliseconds() - phaseStart);
        }

        var remeshStart = MonotonicClock.Milliseconds();
        renderer.Remesh(camera.Eye, camera.Frustum, nearZoneMade ? NearZoneMeshMilliseconds : _streaming is { IsCatchingUp: true } ? StreamedWorldHost.CatchUpMeshBudgetMilliseconds : _options.NoSettle ? Preset.MeshBudgetMilliseconds : _options.IsScreenshotRun ? null : _session.Worker!.IsReady ? Preset.MeshBudgetMilliseconds : 30);
        var remeshEnd = MonotonicClock.Milliseconds();
        _remeshTimes.Record(remeshEnd - remeshStart);
        if (_streaming is { } probed && _options.StreamStats)
        {
            var at = streamEye ?? camera.Eye;
            if (_moves.Observe(remeshEnd, frameBegan, at.X, at.Z, probed.IsNearReady(at.X, at.Y, at.Z), renderer.IsMeshedAround(at, NearViewChunks)) is { } line)
            {
                _console(line); // --stream-stats: how long a start or a move took to show the world round the body (B07b)
            }
        }
        AdvanceUnderwater(camera);
        var amount = (float)_underwater.Amount;
        renderer.SetUnderwater(amount);
        _underwaterPass!.Begin(amount, BlurLevel());
        device.Clear(renderer.ClearColour);
        renderer.DrawWorld(camera, target, beacon, tick, avatar, arm, () => NpcLabelLayer.Draw(device, _spriteBatch!, View, camera), PlotOutlines(camera));
        _underwaterPass.End(amount, _underwater.TintOpacity, renderer.WaterLight);
        _underwaterPass.DrawLavaTint(LavaTintStrength * (float)_molten.Amount);
    }

    // The outlines of the pending and the owned plot, when the eye is near them (Y10): posts and an edge line on the ground's own height, built for the frame (a few dozen quads).
    private List<MeshBuffer>? PlotOutlines(FirstPersonCamera camera)
    {
        List<MeshBuffer>? outlines = null;
        var grid = _session.Simulation.Grid;
        float Surface(int x, int z) => LandRules.GroundOf(grid, x, z).Y + 1f;
        foreach (var (plot, owned) in new[] { (_session.PendingPlot, false), (_session.OwnedPlot, true) })
        {
            if (plot is { } p && OverlayMeshes.PlotOutlineVisible(p, camera.Eye.X, camera.Eye.Z))
            {
                (outlines ??= []).Add(OverlayMeshes.PlotOutline(p, Surface, owned));
            }
        }

        return outlines;
    }

    // The view under water eases by the frame's time (presentation, not simulation); a scripted run counts one tick of time a frame, as its clock does.
    private void AdvanceUnderwater(FirstPersonCamera camera)
    {
        var now = MonotonicClock.Milliseconds();
        var seconds = _options.IsScreenshotRun || double.IsNaN(_lastDrawMilliseconds)
            ? 1.0 / VoxelSimulation.TicksPerSecond
            : Math.Min((now - _lastDrawMilliseconds) / 1000.0, 0.25);
        _lastDrawMilliseconds = now;
        var (x, y, z) = UnderwaterGeometry.EyeUnits(camera.Eye);
        _underwater.Advance(WaterVolume.Contains(_session.Simulation.Grid, x, y, z), seconds);
        _molten.Advance(LavaVolume.Contains(_session.Simulation.Grid, x, y, z), seconds);
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
        _underwaterPass?.Dispose();
        _streaming?.Dispose();
        _renderer?.Dispose();
        _waterTextures?.Dispose();
        _blockAtlas?.Dispose();
        _pixel?.Dispose();
        _spriteBatch?.Dispose();
    }
}
