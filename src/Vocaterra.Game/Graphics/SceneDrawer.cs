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
using Vocaterra.Game.Graphics.Colour;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Graphics.Figures;
using Vocaterra.Game.Graphics.Gl;
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
    private JobPool? _meshPool;
    private readonly double _createdAt = MonotonicClock.Milliseconds();
    private long _phaseAllocated;
    private long _phaseFrames;

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
        _level = level;
        _viewDistance = viewDistance;
        Rebuild();
    }

    private QualityLevel _level = QualityLevel.High;
    private int? _viewDistance;
    private (int Distance, int Detail, int Trees, int Textures, int Exact) _far = (0, 100, 0, 2, 0);

    /// <summary>
    /// Takes the far view's settings (B07d; the run's start, then the settings page's rows): the distance in km (0 is the quality's own), the detail in percent, the single trees, the textured levels and the real-chunk radius, each as the setting counts it
    /// (<see cref="FarChoices"/>). The preset in force changes and the far view follows at once, as for a quality change.
    /// </summary>
    /// <param name="distanceKm">The distance, 0 for the quality's own.</param>
    /// <param name="detailPercent">The detail in percent.</param>
    /// <param name="trees">The trees setting.</param>
    /// <param name="textures">The textures setting.</param>
    /// <param name="exact">The real-chunk setting.</param>
    public void ApplyFar(int distanceKm, int detailPercent, int trees, int textures, int exact)
    {
        _far = (distanceKm, detailPercent, trees, textures, exact);
        Rebuild();
    }

    // The preset of the quality, the draw distance and the far settings; the renderer and the streaming host follow it.
    private void Rebuild()
    {
        var preset = _viewDistance is { } blocks ? QualityPreset.For(_level).WithViewDistance(blocks) : QualityPreset.For(_level);
        Preset = FarChoices.Apply(preset, _far.Distance, _far.Detail, _far.Trees, _far.Textures, _far.Exact);
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

    private int _workersSetting;

    /// <summary>
    /// Takes the Worker threads setting (B07d; 0 is Auto): before <see cref="Load"/> it chooses the count the pool starts with, after it the pool is resized at once (new threads start, surplus ones end after the job each is on: no queued job is lost), unless the run is held
    /// to a count (a scripted run, <c>--prefetch</c>).
    /// </summary>
    /// <param name="setting">The setting's value: 0 for Auto, else 1 to 8.</param>
    public void ApplyWorkers(int setting)
    {
        _workersSetting = setting;
        if (WorkerPlan.Change(_options.Prefetch, _options.IsScreenshotRun, setting, Environment.ProcessorCount) is { } count)
        {
            _ = _streaming?.Pool.Resize(count);
            _ = _meshPool?.Resize(count);
        }
    }

    private int WorkersAtStart() => WorkerPlan.For(_options.Prefetch, _options.Workers, _options.IsScreenshotRun, _workersSetting, Environment.ProcessorCount);

    private int _grassPercent;
    private GraphicsEffects _effects = GraphicsEffects.All;
    private GraphicsDevice? _device;
    private bool _shaders = true;
    private bool _shadersUnavailable;

    /// <summary>
    /// Takes the decorative grass's density in percent (B18; the run's start, then the menu's Grass setting): before <see cref="Load"/> it is the value the renderer starts with, after it the renderer follows at once
    /// (moving it between two values above 0 builds nothing again; 0 draws nothing extra).
    /// </summary>
    /// <param name="percent">The density, 0 to 100.</param>
    public void ApplyGrass(int percent)
    {
        _grassPercent = percent;
        _renderer?.SetGrass(percent);
    }

    /// <summary>
    /// Takes the graphics effects of the settings page (B17c; the run's start, then each row's change): the renderer follows at once (<see cref="VoxelRenderer.SetEffects"/>), and the far view's climate colours are made again when that one changes
    /// (the far tiles carry them baked in: they are built again, nearest first, on the workers).
    /// </summary>
    /// <param name="effects">The effects.</param>
    public void ApplyEffects(GraphicsEffects effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        _effects = effects;
        _renderer?.SetEffects(effects);
        _streaming?.SetClimateColours(effects.ClimateColours, VoxelRenderer.ReleaseFarTile);
    }

    /// <summary>
    /// Takes the Shaders setting (B17b; the run's start, then the menu's row and <c>--gl-shaders</c>): on, the chunks are drawn through the game's own GLSL program when the context can run it (the log says why not, and the effects
    /// draw as always); off, through MonoGame's effects. Before <see cref="Load"/> it is the choice the scene starts with; after it the renderer follows at once, with no restart: the meshes are built again in the other kind of buffers
    /// (nearest first, on the workers), so the world fills in for a moment.
    /// </summary>
    /// <param name="on">Whether the game's own shaders draw the chunks.</param>
    public void ApplyShaders(bool on)
    {
        _shaders = on;
        if (_renderer is not null)
        {
            SwitchShaders();
        }
    }

    /// <summary>Whether the chunks are drawn through the game's own GLSL program now (false while the effects draw: the setting is off, or the context could not run the program).</summary>
    public bool ShadersInUse => _renderer?.GlPass is not null;

    private void SwitchShaders()
    {
        var renderer = _renderer!;
        if (!_shaders)
        {
            if (renderer.GlPass is not null)
            {
                renderer.SetGlPass(null);
                _console("gl: shaders off, the chunks are drawn with the effects");
            }

            return;
        }

        if (renderer.GlPass is not null || _shadersUnavailable)
        {
            return;
        }

        var pass = GlChunkPass.TryCreate(_device!, _blockAtlas!.Texture, _waterTextures!.Frames, VoxelRenderer.MaxQuadsPerChunkMesh, _console, out var glError);
        if (pass is null)
        {
            _shadersUnavailable = true; // the same context fails the same way: said once, and the effects draw as always
            _console($"gl: shaders not used, the effects draw as always: {glError}");
            return;
        }

        renderer.SetGlPass(pass);
        _console("gl: every chunk pass is drawn through the game's own GLSL program (chunk.vert.glsl, chunk.frag.glsl)");
    }

    /// <summary>The time each frame spent streaming the world (the residency and the far view) and meshing chunks, for the stream stats line.</summary>
    public (Vocaterra.Game.Core.Reporting.BusyFrameStats Streaming, Vocaterra.Game.Core.Reporting.BusyFrameStats Remesh) PhaseTimes => (_streamTimes, _remeshTimes);

    /// <summary>The bytes the game thread allocated in the streaming and meshing phases of the frames so far, how many frames that is (B21, P5: the steady state should allocate next to nothing) and the whole process's allocation rate since the drawer was made, in megabytes a second.</summary>
    public (long Bytes, long Frames, double ProcessMegabytesPerSecond) PhaseAllocations =>
        (_phaseAllocated, _phaseFrames, GC.GetTotalAllocatedBytes(false) / (1024.0 * 1024.0) / Math.Max((MonotonicClock.Milliseconds() - _createdAt) / 1000.0, 0.001));

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
        _renderer = new VoxelRenderer(device, _blockAtlas, _waterTextures, _session.Simulation.Grid, Preset, SkyLook.At(SkyLookInput.At(_session.Simulation.Atmosphere, _session.Simulation.Weather.Data.CloudAltitude)), _session.Scene.Currents, _console, _session.Scene.Places is { } places ? places.NaturalSurfaceAt : null, _session.Scene.Places is { } biomes ? (x, z) => biomes.BiomeAt(x, z).Id : null, _session.Scene.Places is { } ground ? GroundClimate.Of(ground) : null);
        _renderer.SetGrass(_grassPercent);
        _renderer.SetEffects(_effects);
        _device = device;
        if (_options.StreamStats)
        {
            _renderer.SetCardTimer(GlCardTimer.TryCreate(_console)); // the card's time for the chunk passes, with either path (B17b)
        }

        SwitchShaders();
        _renderer.SetFigures(_figures);
        _streaming = StreamedWorldHost.For(_session.Scene, Preset, WorkersAtStart(), StreamedWorldHost.LodWorldName(_session.Simulation.StateHeader));
        _streaming?.SetClimateColours(_effects.ClimateColours, VoxelRenderer.ReleaseFarTile);
        _renderer.SetFarView(_streaming?.Far);
        _renderer.SetPipelineCounters(_streaming?.Pipeline);
        _renderer.SetNaturalHeights(_streaming?.NaturalHeights);
        _meshPool = _streaming is null ? new JobPool(WorkersAtStart()) : null; // the bounded valley has no streaming host: its meshes get a pool of their own
        _renderer.SetMeshPool(_streaming?.Pool ?? _meshPool);
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
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        renderer.SetAtmosphere(look); // before the meshing: a change of light band is noticed by this frame's Remesh
        var channels = _session.Simulation.Atmosphere.Channels;
        renderer.SetPrecipitation(new PrecipitationState(channels.PrecipitationKind, channels.PrecipitationIntensity, channels.Wind)); // the rain's pass reads the channels, never a kind's id (E06)
        _colours!.Refresh();
        if (_streaming is { } streaming)
        {
            // The chunks and the far land round the eye (B07): a live frame installs a few within its budget, a scripted one has everything in place before its picture.
            var eye = streamEye ?? camera.Eye; // the body's own eye, not the camera's between two ticks: the world is made for where the body is, which matters while the simulation waits for it
            var phaseStart = MonotonicClock.Milliseconds();
            if (streaming.Pipeline.Track(eye.X, eye.Z, phaseStart)) // the start, or a jump: the stage counters' times count from this frame (B43)
            {
                streaming.NearGate.Arm(phaseStart); // B45: far work waits until the near view is made
                streaming.Far.BeginCoverage();
            }

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
        if (_streaming is { } gated && gated.NearGate.IsClosed)
        {
            var eyeNow = streamEye ?? camera.Eye;
            _ = gated.NearGate.Observe(remeshEnd, () => gated.Residency.IsResidentAround(eyeNow.X, eyeNow.Y, eyeNow.Z, NearFirstGate.NearViewChunks) && renderer.IsMeshedAround(eyeNow, NearFirstGate.NearViewChunks)); // B45: the near view, resident and meshed, opens the gate for the next frame
        }

        _phaseAllocated += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        _phaseFrames++;
        if (_streaming is { } probed && _options.StreamStats)
        {
            var at = streamEye ?? camera.Eye;
            if (_moves.Observe(remeshEnd, frameBegan, at.X, at.Z, probed.IsNearReady(at.X, at.Y, at.Z), renderer.IsMeshedAround(at, NearViewChunks), probed.Residency.IsSettled && renderer.PendingChunks == 0) is { } line)
            {
                _console(line); // --stream-stats: how long a start or a move took to show the world round the body (B07b)
                if (_moves.ViewCompleted)
                {
                    _console(probed.Pipeline.Line(remeshEnd)); // and how fast each stage of the chunks' way to the screen went (B43)
                }
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
        _meshPool?.Dispose();
        _renderer?.Dispose();
        _waterTextures?.Dispose();
        _blockAtlas?.Dispose();
        _pixel?.Dispose();
        _spriteBatch?.Dispose();
    }
}
