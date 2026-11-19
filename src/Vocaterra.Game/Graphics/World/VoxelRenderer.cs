using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;
using Vocaterra.Game.Core.Water.Currents;
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
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Graphics.World;

/// <summary>What building the chunk meshes cost and produced, for the run's report.</summary>
/// <param name="Chunks">The chunks in the grid.</param>
/// <param name="Meshed">How many chunk meshes were built (a chunk is built again after an edit, or when it comes back into view).</param>
/// <param name="Vertices">The vertices now on the graphics card.</param>
/// <param name="Quads">The quads (faces) now on the graphics card.</param>
/// <param name="TotalMilliseconds">The time spent meshing and uploading, all builds together.</param>
/// <param name="LongestMilliseconds">The slowest single chunk build.</param>
/// <param name="InView">The chunks within the view distance that hold something (the ones that are meshed and drawn).</param>
/// <param name="Resident">The chunks that have a mesh on the card now.</param>
/// <param name="Pending">The chunks in view still waiting for their mesh.</param>
/// <param name="EmptySkipped">The chunks never meshed because they hold nothing but air (counted over the whole grid).</param>
/// <param name="BuriedSkipped">The chunks passed by because they are full of opaque blocks all round, so they have no face to draw.</param>
/// <param name="Released">The meshes let go because the chunk went out of view.</param>
public readonly record struct MeshStats(
    int Chunks,
    int Meshed,
    int Vertices,
    int Quads,
    double TotalMilliseconds,
    double LongestMilliseconds,
    int InView = 0,
    int Resident = 0,
    int Pending = 0,
    int EmptySkipped = 0,
    int BuriedSkipped = 0,
    int Released = 0)
{
    /// <summary>The average build time of a chunk in milliseconds.</summary>
    public double MeanMilliseconds => Meshed == 0 ? 0 : TotalMilliseconds / Meshed;

    /// <summary>A one-line summary.</summary>
    public string Summary() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Chunks} chunks ({EmptySkipped} empty, {InView} in view, {Resident} meshed now, {Pending} pending, {BuriedSkipped} buried skipped), {Meshed} meshes built in {TotalMilliseconds:0.0} ms (mean {MeanMilliseconds:0.00} ms, longest {LongestMilliseconds:0.00} ms), {Quads} faces, {Vertices} vertices");
}

/// <summary>
/// Draws the voxel world through the game's own GLSL program when the context can run it (B17a to B17c: the light is data in the meshes and the shader combines it with the hour, the weather and the glow, with fog by height, the water shader and the far view in
/// the same light), and otherwise, or with the Shaders setting off, with MonoGame's own effects (mgfxc needs Wine on Linux; the light is then baked into the vertex colours, by bands, as it always was): <see cref="AlphaTestEffect"/> for the opaque faces and
/// the undergrowth (a texture times the vertex colour, the light and the ambient occlusion being in the mesh, and the cut-out pixels of the leaves and the
/// plants dropped by the alpha test), <see cref="BasicEffect"/> for the water and the small overlays. The sky comes first (<see cref="SkyRenderer"/>), then
/// with the depth buffer on: the opaque faces of every visible chunk, the undergrowth of the chunks near the eye without culling, the figures and the
/// highlight, then the translucent water chunk by chunk from the farthest to the nearest without writing depth, then the beacon. At the scale of the 384 x 384 x 128 world
/// (S13) only the chunks within the preset's view distance of the eye are meshed and drawn (<see cref="ChunkViewSet"/>): chunks of air and chunks
/// buried in solid blocks are skipped, the rest are built nearest first within the frame's time budget, and a chunk that goes out of range lets its
/// mesh go. The chunks that are drawn are culled against the camera's frustum, and the fog closes in at the view distance, in the sky colour. A chunk
/// is meshed again when its version, or a neighbour's, changed, or when a block kind it holds changed its grey state (<see cref="ChunkVersionTracker"/>).
/// The light of the day cycle (E02) is a <see cref="SkyLook"/> given each frame (<see cref="SetAtmosphere"/>): the clear colour and the fog follow it, and the world's light follows
/// it without a remesh: every mesh bakes the sun's light of one of three bands (<see cref="LightBand"/>) and is drawn with a tint that brings that baked light to the light of the
/// moment, so the chunks of two bands show the same light while a change of band is built again in the background.
/// </summary>
public sealed class VoxelRenderer : IDisposable, IChunkResidencyListener, IChunkMeshSink
{
    /// <summary>The colour of the day's haze at the horizon (<see cref="SkyPalette.Horizon"/>), which is the clear colour and the fog's at noon, so far land fades into the sky.</summary>
    public static readonly Color Sky = new(SkyPalette.Horizon.R, SkyPalette.Horizon.G, SkyPalette.Horizon.B);

    /// <summary>The colour of the evening haze (S15), which is the clear colour and the fog's at the middle of a dusk.</summary>
    public static readonly Color DuskSky = new(SkyPalette.DuskHorizon.R, SkyPalette.DuskHorizon.G, SkyPalette.DuskHorizon.B);

    /// <summary>Where the fog starts, as a fraction of where it is complete (<see cref="FogEndFor"/>): the near land is clear, and from a third of the way out the distance turns to haze.</summary>
    public const float FogStartFraction = 0.8f;

    /// <summary>
    /// The sampler of the block atlas: the picture is sampled by the nearest texel (crisp pixels up close, no blur between them) and blended between the two
    /// nearest mip levels with distance, each level made cell by cell (<see cref="BlockArt.MipLevel"/>), so a far face is smooth and in its own colours instead of a shimmer.
    /// </summary>
    public static readonly SamplerState AtlasSampler = new()
    {
        Filter = TextureFilter.PointMipLinear,
        AddressU = TextureAddressMode.Clamp,
        AddressV = TextureAddressMode.Clamp,
    };

    // A chunk mesh is drawn with 16-bit indices, so it holds at most 65 532 vertices (16 383 quads). A full chunk of cubes tops out at 12 288 quads.
    private const int MaxQuadsPerMesh = MeshPart.MaxQuads;

    // The longest a scripted run's Remesh with no budget waits for the workers, in milliseconds of the wall clock.
    private const double RemeshWaitLimitMilliseconds = 120_000;

    /// <summary>The most quads a chunk mesh holds (the size of the shared quad index buffers).</summary>
    public const int MaxQuadsPerChunkMesh = MaxQuadsPerMesh;

    private readonly GraphicsDevice _device;
    private readonly BlockAtlas _atlas;
    private readonly WaterTextures _water;
    private readonly VoxelGrid _grid;
    private readonly BasicEffect _effect;
    private readonly AlphaTestEffect _solid;
    private GlChunkPass? _glPass;
    private GlCardTimer? _card;
    private readonly SkyRenderer _sky;
    private MeshOptions _meshOptions;
    private readonly Func<int, int, int, (int Temperature, int Humidity)>? _ground;
    private readonly GridBlockSource _source;
    private readonly ChunkVersionTracker _tracker;
    private ChunkViewSet _view;
    private readonly IndexBuffer _indices;
    private readonly Dictionary<VoxelChunkCoord, ChunkGpu> _chunks = [];
    private readonly ChunkLightIndex _lights;
    private readonly short[] _scratchIndices = new short[4096 * 6];
    private VertexPositionColorTexture[] _scratchVertices = new VertexPositionColorTexture[4096];
    private readonly List<ChunkGpu> _visible = [];
    private IReadOnlyList<NpcFigure> _figures = [];
    private readonly MeshBuffer _figureBuffer = new();
    private SkyLook _look;
    private readonly SkyExposure? _skyExposure;
    private float _eyeSky = 1f;
    private PrecipitationState _precipitation = PrecipitationState.None;
    private readonly PrecipitationRenderer _rain;
    private LightBand _band;
    private GraphicsEffects _effects = GraphicsEffects.All;
    private SkyLook _rawLook;
    private bool _farGlFailed;
    private Vector3 _sunLight = Vector3.One;
    private readonly Vector3[] _tints = [Vector3.One, Vector3.One, Vector3.One, Vector3.One]; // the tint of each band (LightBands.Tint), indexed by LightBand
    private readonly int[] _bandChunks = new int[LightBands.Count]; // how many visible chunks each band has this frame
    private readonly HashSet<VoxelChunkCoord> _resident = [];
    private IReadOnlyList<VoxelChunkCoord> _wanted = [];
    private readonly ChunkMeshPipeline _pipeline;
    private PipelineCounters? _counters;
    private readonly List<VoxelChunkCoord> _greyScratch = [];
    private readonly GrassOrder _grassOrder;
    private long _meshVersion;
    private long _readinessVersion = -1;
    private int _meshed;
    private double _totalMilliseconds;
    private double _longestMilliseconds;
    private int _drawnChunks;
    private int _drawnPlantQuads;
    private int _buriedSkipped;
    private int _released;
    private int _pending;
    private readonly Dictionary<(int X, int Z), bool> _columnReady = [];
    private FarView? _far;
    private FarTerrainRenderer? _farRenderer;
    private float _farRange;
    private readonly GrassBiomes _grassBiomes = GrassBiomes.Shipped;
    private Func<int, int, string?>? _grassBiomeAt;
    private int _grassPercent;
    private bool _grassDirty;
    private bool _grassScanned;
    private Vector3 _grassScanEye;
    private int _drawnGrassQuads;
    private int _grassBuilt;
    private double _grassMilliseconds;
    private readonly List<VoxelChunkCoord> _grassScratch = [];

    /// <summary>Creates the renderer for a grid, a quality preset and the sky's look at the start. Nothing is meshed yet: the first <see cref="Remesh"/> builds what the eye can see.</summary>
    /// <param name="log">Where the rain says it has no style for a precipitation kind (once), or <see langword="null"/>.</param>
    /// <param name="groundClimate">The ground temperature and humidity at a block (<see cref="GroundClimate.Of"/>), which the grass and the oak leaves take their colour from (B22); <see langword="null"/> for the colour map's middle everywhere.</param>
    /// <param name="naturalSurface">The natural surface y of a block column (<c>IPlaceInfo.NaturalSurfaceAt</c>), from which the ground darkens with depth (B12, <see cref="SkyDepthLight"/>); <see langword="null"/> for no darkening.</param>
    /// <param name="biomeAt">The id of the biome of a block column (<c>IPlaceInfo.BiomeAt</c>), which sets how lush the decorative grass is there (B18); <see langword="null"/> where the world has no place query (the valley).</param>
    public VoxelRenderer(GraphicsDevice device, BlockAtlas atlas, WaterTextures water, VoxelGrid grid, QualityPreset preset, in SkyLook look, RiverCurrents? currents = null, Action<string>? log = null, Func<int, int, int>? naturalSurface = null, Func<int, int, string?>? biomeAt = null, Func<int, int, int, (int Temperature, int Humidity)>? groundClimate = null)
    {
        ArgumentNullException.ThrowIfNull(preset);
        _device = device;
        _atlas = atlas;
        _water = water;
        _grid = grid;
        Preset = preset;
        _rawLook = look;
        _look = look;
        _band = look.Band;
        ClearColour = new Color(look.FogColour.R, look.FogColour.G, look.FogColour.B);
        var fogEnd0 = FogEndNow(preset.ViewDistance, look.Fog);
        DryFog = new FogParameters(ClearColour.R / 255f, ClearColour.G / 255f, ClearColour.B / 255f, FogStartOf(fogEnd0), fogEnd0);
        _source = new GridBlockSource(grid, currents);
        _tracker = new ChunkVersionTracker(grid);
        _view = new ChunkViewSet(grid, preset.ViewDistance, MeshCap(preset)) { ResidencyRebuildCalls = 8 };
        var fogEnd = fogEnd0;
        _effect = new BasicEffect(device)
        {
            TextureEnabled = true,
            VertexColorEnabled = true,
            LightingEnabled = false,
            FogEnabled = true,
            FogColor = ClearColour.ToVector3(),
            FogStart = FogStartOf(fogEnd),
            FogEnd = fogEnd,
            Texture = atlas.Texture,
        };
        _solid = new AlphaTestEffect(device)
        {
            VertexColorEnabled = true,
            FogEnabled = true,
            FogColor = ClearColour.ToVector3(),
            FogStart = FogStartOf(fogEnd),
            FogEnd = fogEnd,
            Texture = atlas.Texture,
            AlphaFunction = CompareFunction.Greater,
            ReferenceAlpha = 110,
        };
        _sky = new SkyRenderer(device, look);
        _rain = new PrecipitationRenderer(device, grid, log);
        _lights = new ChunkLightIndex(grid);
        _grassBiomeAt = biomeAt;
        _skyExposure = naturalSurface is null ? null : new SkyExposure(grid, naturalSurface);
        _ground = groundClimate;
        _meshOptions = BaseOptions(preset); // the lights near a chunk are put in when it is meshed (BuildChunk)
        grid.AddResidencyListener(_tracker);
        grid.AddResidencyListener(this);
        _pipeline = new ChunkMeshPipeline(grid, _tracker, OptionsFor, currents) { Band = _band };
        _grassOrder = new GrassOrder(this);
        SetLightTints(look);
        _indices = BuildIndexBuffer(device, MaxQuadsPerMesh);
        for (var i = 0; i < _scratchIndices.Length / 6; i++)
        {
            WriteQuadIndices(_scratchIndices, i);
        }
    }

    /// <summary>The lanterns and post torches of the world (S15): the places the glow comes from, kept current by the light index (<see cref="ChunkLightIndex"/>) as lights are placed, removed, installed or evicted.</summary>
    public IReadOnlyList<BlockPos> Lights => _lights.All;

    /// <summary>
    /// Gives the renderer the worker threads to build chunk meshes and grass layers on (B21): the streamed world's pool, or one of the bounded valley's own; <see langword="null"/> (the start) builds them on the game thread within the frame's
    /// budget, as before. The workers build from a snapshot of the blocks taken on the game thread (<see cref="ChunkSnapshot"/>) and the game thread puts the result on the card within the budget.
    /// </summary>
    /// <param name="pool">The pool, or <see langword="null"/>.</param>
    public void SetMeshPool(JobPool? pool) => _pipeline.Pool = pool;

    /// <summary>Whether chunk meshes are built on worker threads.</summary>
    public bool MeshesOnWorkers => _pipeline.IsAsync;

    /// <summary>What the meshing pipeline has done (jobs, results refused as stale, edits meshed in their own frame), for the stream stats line.</summary>
    public string MeshingSummary => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"meshing: {(_pipeline.IsAsync ? "on workers" : "on the game thread")}, {_pipeline.Submitted} jobs, {_pipeline.Installed} installed, {_pipeline.Refused} refused as stale, {_pipeline.InlineEdits} edits meshed in their frame, {_pipeline.Pending} pending, worker time {_pipeline.BuildMilliseconds.Total:0} ms (longest {_pipeline.BuildMilliseconds.Longest:0.0})");

    /// <summary>How many chunks of the streamed grid a read on the game thread generated and installed (the residency driver makes them on the workers; this is what slipped past it).</summary>
    public long ChunksGeneratedByReads => _grid.ChunksGeneratedByReads;

    /// <summary>The pipeline that decides which chunks to mesh (tests read its counters).</summary>
    internal ChunkMeshPipeline Pipeline => _pipeline;

    // The mesh options of a chunk as they are now: the quality's, the band's, the lights near it and the sky over it (read on the game thread).
    private MeshOptions OptionsFor(VoxelChunkCoord coord) => _meshOptions with { Lights = _lights.Near(coord), Sky = _skyExposure?.Tile(coord.X, coord.Z) };

    /// <summary>The colour the target is cleared to and the fog's colour: the look's horizon colour (<see cref="SkyLook.FogColour"/>); the water's while the view is under water.</summary>
    public Color ClearColour { get; private set; }

    /// <summary>The fog above water (the look's haze colour, start and end for the preset): what <see cref="SetUnderwater"/> blends from.</summary>
    public FogParameters DryFog { get; private set; }

    /// <summary>How many vertices the rain sent in the last <see cref="DrawWorld"/> (E06; four to a drop, 0 when it does not rain or the eye is under water).</summary>
    public int DrawnRainVertices => _rain.DrawnVertices;

    /// <summary>
    /// Takes what falls this frame (E06), from the weather's channels: the precipitation pass of <see cref="DrawWorld"/> draws it after the water, with the quality's drop budget.
    /// </summary>
    /// <param name="state">The kind, the intensity and the wind.</param>
    public void SetPrecipitation(in PrecipitationState state) => _precipitation = state;

    /// <summary>The look the renderer draws in now (<see cref="SetAtmosphere"/>).</summary>
    public SkyLook Look => _look;

    /// <summary>The light band new meshes are built in (the look's).</summary>
    public LightBand Band => _band;

    private float _underwaterAmount;

    /// <summary>The share of the sky's light at the eye, 0 to 1 (B12, <see cref="SkyDepthLight"/>): 1 in the open, the cave minimum deep under the ground. 1 when the renderer has no height model.</summary>
    public float EyeSky => _eyeSky;

    /// <summary>How strongly the lights glow now (B12): the look's strength in the open, and at the most underground, where no daylight outshines a lantern, a torch or lava.</summary>
    public float GlowStrength => _look.LanternStrength + ((1f - _look.LanternStrength) * (1f - _eyeSky));

    /// <summary>
    /// How much of the water's own colour the light of the moment leaves, 0 to 1 (E02): the luminance of the look's sun light, 1 at noon, about 0.7 at dusk and 0.36 at night. The water's
    /// colour and fog still win under water, as before, but they are dimmed by this, so the water is dark at night and not a bright blue under a dark world.
    /// </summary>
    public float WaterLight { get; private set; } = 1f;

    /// <summary>
    /// Sets how far under water the view is (H05): the fog, the clear colour and the sky blend from the dry ones to the water's by <paramref name="amount"/>
    /// (<see cref="UnderwaterFog.Blend"/>). At 0 everything is exactly as above water. The renderer only applies the numbers; the rule is in Core.
    /// </summary>
    /// <param name="amount">How far under water, 0 to 1.</param>
    public void SetUnderwater(float amount)
    {
        if (amount == _underwaterAmount)
        {
            return;
        }

        _underwaterAmount = amount;
        ApplyFogState();
    }

    /// <summary>
    /// Takes the sky's look of this frame (E02), in place of the old dusk switch: the clear colour and the fog follow the look's horizon colour, the sky is drawn in it, and the
    /// world's light follows its sun light by the tints each band is drawn with. A change of the look's light band makes the meshes of the other bands stale: <see cref="Remesh"/>
    /// builds them again in the background within its budget, the old ones staying on screen (tinted to the same light) until each is replaced. It composes with
    /// <see cref="SetUnderwater"/>: under water the water's colour and fog win, as before.
    /// </summary>
    /// <param name="look">The look at this moment of the cycle.</param>
    public void SetAtmosphere(in SkyLook look)
    {
        _rawLook = look;
        _look = SunModel.Adjusted(look, _effects.SunFollowsHour); // with the sun not following the hour the dusk's light is the older, lighter one (B17c)
        var band = EffectiveBand;
        if (band != _band)
        {
            _band = band;
            _meshOptions = _meshOptions with { Band = _band };
            _pipeline.Band = _band;
            _pipeline.BandChanged();
        }

        SetLightTints(_look);
        var fogEnd = FogEndNow(Preset.ViewDistance, _look.Fog); // the weather's fog brings the distances in (E05); the view distance and the preset set the clear-weather ones
        DryFog = new FogParameters(_look.FogColour.R / 255f, _look.FogColour.G / 255f, _look.FogColour.B / 255f, FogStartOf(fogEnd), fogEnd);
        _sky.SetLook(_look);
        ApplyFogState();
    }

    // The share of the sky's light at the eye (B12): the fog, the haze and the clear colour darken with it, so the bright horizon does not show through a cave's far end as a sky, and the lights glow at full.
    private void UpdateEyeSky(Vector3 eye)
    {
        if (_skyExposure is null)
        {
            return;
        }

        var share = _skyExposure.FactorAt(eye.X, eye.Y, eye.Z);
        if (MathF.Abs(share - _eyeSky) < 0.002f)
        {
            return;
        }

        _eyeSky = share;
        ApplyFogState();
    }

    // The sun's light of the look and each band's tint: the colour a chunk of the band is drawn with so that its baked light shows as the look's.
    private void SetLightTints(in SkyLook look)
    {
        _sunLight = new Vector3(look.SunLight.R, look.SunLight.G, look.SunLight.B);
        WaterLight = Math.Clamp((0.3f * look.SunLight.R) + (0.59f * look.SunLight.G) + (0.11f * look.SunLight.B), 0f, 1f);
        for (var b = 0; b < LightBands.Count; b++)
        {
            var (r, g, bl) = LightBands.Tint(look.SunLight, (LightBand)b);
            _tints[b] = new Vector3(r, g, bl);
        }
    }

    // The dry fog blended with the water's by the amount under water, applied to both effects, the clear colour and the sky's haze.
    private void ApplyFogState()
    {
        var fog = UnderwaterFog.Blend(DryFog, _underwaterAmount);
        var t = Math.Clamp(_underwaterAmount, 0f, 1f);
        var dim = (WaterLight - 1f) * t; // the water's share of the blend, dimmed by the light: dry * (1 - t) + water * t becomes dry * (1 - t) + water * light * t
        var colour = new Vector3(fog.Red + (dim * UnderwaterFog.WaterRed), fog.Green + (dim * UnderwaterFog.WaterGreen), fog.Blue + (dim * UnderwaterFog.WaterBlue)) * _eyeSky;
        ClearColour = _underwaterAmount <= 0f ? new Color(new Vector3(_look.FogColour.R, _look.FogColour.G, _look.FogColour.B) / 255f * _eyeSky) : new Color(colour);
        ApplyFog(_effect, colour, fog);
        ApplyFog(_solid, colour, fog);
        _sky.SetHaze(ClearColour, _underwaterAmount);
    }

    private static void ApplyFog(IEffectFog effect, Vector3 colour, FogParameters fog)
    {
        effect.FogColor = colour;
        effect.FogStart = fog.Start;
        effect.FogEnd = fog.End;
    }

    /// <summary>The quality preset in force: the one the renderer was made with, or the last <see cref="SetPreset"/> gave.</summary>
    public QualityPreset Preset { get; private set; }

    /// <summary>
    /// Changes the quality preset while the game runs (the menu): the view distance, the fog, the undergrowth and the corner shading follow. When the undergrowth or the corner
    /// shading change, every mesh is let go, so the next <see cref="Remesh"/> builds the view again with the new options (nearest first); a new distance alone keeps the meshes
    /// (the chunks past the new distance go in the next <see cref="Remesh"/>, the new ones come in nearest first). The sky takes the cloud setting at its next draw.
    /// </summary>
    /// <param name="preset">The new preset.</param>
    public void SetPreset(QualityPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (preset == Preset)
        {
            return;
        }

        var remesh = preset.AmbientOcclusion != Preset.AmbientOcclusion || preset.PlantKeepPercent != Preset.PlantKeepPercent;
        var regrow = preset.GrassCapPercent != Preset.GrassCapPercent;
        Preset = preset;
        if (regrow)
        {
            ReleaseAllGrass(); // the layer holds the tufts up to the quality's cap: built again, near the eye, by the next Remesh
        }

        var fogEnd = FogEndNow(preset.ViewDistance, _look.Fog);
        DryFog = new FogParameters(DryFog.Red, DryFog.Green, DryFog.Blue, FogStartOf(fogEnd), fogEnd);
        _view.Detach();
        _view = new ChunkViewSet(_grid, preset.ViewDistance, MeshCap(preset)) { ResidencyRebuildCalls = 8 };
        _meshOptions = BaseOptions(preset);
        if (remesh)
        {
            ReleaseAllMeshes(); // the corners and the undergrowth are in the meshes: build them again
        }

        _wanted = [];
        _pipeline.SetWanted(_wanted);
        ApplyFogState(); // the fog of the new distance
    }

    /// <summary>
    /// The decorative grass's density, 0 to 100 percent (B18, the settings page's Grass slider). Moving it between two values above 0 builds nothing again: each chunk's grass layer holds the tufts up to the quality's cap
    /// in the order of their density rank, and the slider only chooses how many tiers of it are drawn. From 0 the layers are built near the eye by the next <see cref="Remesh"/>; to 0 they are let go and nothing extra is drawn.
    /// </summary>
    /// <param name="percent">The density in percent.</param>
    public void SetGrass(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        if (percent == _grassPercent)
        {
            return;
        }

        var wasOff = _grassPercent == 0;
        _grassPercent = percent;
        if (percent == 0)
        {
            ReleaseAllGrass();
        }
        else if (wasOff)
        {
            _grassDirty = true;
        }
    }

    /// <summary>
    /// Draws every chunk pass through the game's own GLSL program from now on (B17a, B17b: the opaque faces, the lit pieces, the cut-outs, the grass, the glow and the water), or through MonoGame's effects again with
    /// <see langword="null"/>. Every mesh already built is in the other kind of buffers, so the renderer lets them all go and builds the view again (nearest first, on the workers), which is why a switch while the game runs
    /// shows the world filling in for a moment.
    /// </summary>
    /// <param name="pass">The pass, or <see langword="null"/>. The renderer disposes it with itself.</param>
    public void SetGlPass(GlChunkPass? pass)
    {
        if (ReferenceEquals(pass, _glPass))
        {
            return;
        }

        ReleaseAllMeshes();
        ReleaseAllGrass();
        _glPass?.Dispose();
        _glPass = pass;
        _farGlFailed = false;
        _band = EffectiveBand;
        _meshOptions = BaseOptions(Preset); // the meshes carry their light as data for the program, and baked for the effects
        _pipeline.Band = _band;
    }

    // The mesh options of a quality level as the renderer builds them now (B17c): the light as data when the program draws, the climate colours and the water's tint as the settings say.
    private MeshOptions BaseOptions(QualityPreset preset) =>
        new(preset.AmbientOcclusion, preset.PlantKeepPercent, null, _band, null, _effects.ClimateColours ? _ground : null, _glPass is not null, _effects.WaterShader);

    // The band new meshes are baked in: the look's for the effects (the sun's multiplier and direction by band, built again when the band changes), and Day for the program, which has the light as data and builds nothing again when the hour moves
    // (also Day for the effects with the sun not following the hour: the one direction and the tint of the light alone).
    private LightBand EffectiveBand => LightBands.MeshBand(_look.Band, _glPass is not null, _effects.SunFollowsHour);

    /// <summary>The graphics effects now in force (B17c).</summary>
    public GraphicsEffects Effects => _effects;

    /// <summary>
    /// Takes the graphics effects of the settings page (B17c), each on or off (<see cref="GraphicsEffects"/>). The sun, the water shader and the height fog are uniforms of the program (no mesh is built again; with the effects drawing, the sun's and the water's are baked, so the
    /// view is built again); the climate colours are in the meshes' materials, so the view is built again; the far view's light is the way the far view is drawn.
    /// </summary>
    /// <param name="effects">The effects.</param>
    public void SetEffects(GraphicsEffects effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (effects == _effects)
        {
            return;
        }

        var was = _effects;
        _effects = effects;
        _meshOptions = BaseOptions(Preset);
        var remesh = effects.ClimateColours != was.ClimateColours || (_glPass is null && effects.WaterShader != was.WaterShader);
        if (effects.SunFollowsHour != was.SunFollowsHour)
        {
            SetAtmosphere(_rawLook); // the sun's light (the deeper dusk) and, for the effects, the band the meshes are baked in
        }

        if (remesh)
        {
            ReleaseAllMeshes();
            ReleaseAllGrass();
        }
    }

    /// <summary>Gives the renderer a timer for the card's time round the chunk passes (B17b, <c>--stream-stats</c>); <see langword="null"/> for none. The renderer disposes it with itself.</summary>
    /// <param name="timer">The timer, or <see langword="null"/>.</param>
    public void SetCardTimer(GlCardTimer? timer)
    {
        _card?.Dispose();
        _card = timer;
    }

    /// <summary>The processor time each frame spent submitting the chunk passes (the draw calls, through the effects or through the GLSL program, the figures not counted; the card's own time is <see cref="CardTimes"/>), for the comparison of the two paths (B17a, B17b).</summary>
    public Vocaterra.Game.Core.Reporting.BusyFrameStats ChunkPassTimes { get; } = new();

    /// <summary>The card's time each frame spent on the chunk passes, from a GL timer query (B17b), or <see langword="null"/> when there is no timer.</summary>
    public Vocaterra.Game.Core.Reporting.BusyFrameStats? CardTimes => _card?.Times;

    /// <summary>The game's own GLSL pass, or <see langword="null"/> when the chunks are drawn with MonoGame's effects.</summary>
    public GlChunkPass? GlPass => _glPass;

    /// <summary>The decorative grass's density in percent (0 draws nothing extra).</summary>
    public int GrassPercent => _grassPercent;

    /// <summary>How many quads of decorative grass the last <see cref="DrawWorld"/> drew.</summary>
    public int DrawnGrassQuads => _drawnGrassQuads;

    /// <summary>How many chunks' grass layers were built so far, and the time they took in milliseconds (a build is apart from the chunk's own mesh).</summary>
    public (int Built, double Milliseconds) GrassBuilds => (_grassBuilt, _grassMilliseconds);

    /// <summary>How many quads of grass the chunks near the eye hold right now (what is built, drawn or not).</summary>
    public int GrassQuadsBuilt
    {
        get
        {
            var quads = 0;
            foreach (var chunk in _chunks.Values)
            {
                quads += chunk.GrassQuads;
            }

            return quads;
        }
    }

    // Lets every chunk's grass layer go (the setting went to 0, or the quality's cap changed); the next scan builds them again if they are wanted.
    private void ReleaseAllGrass()
    {
        foreach (var chunk in _chunks.Values)
        {
            chunk.ReleaseGrass();
        }

        _grassDirty = true;
    }

    // Builds the grass layers of the chunks within reach of the eye that have none yet, nearest first, and lets go of the layers of the chunks well beyond it. Chunks come and go with the eye, so the scan runs when the
    // eye has moved a few blocks, a chunk was meshed again or a layer is still due; the budget bounds a frame's work like the chunk meshes' (always one build, so the view fills).
    private void UpdateGrass(Vector3 eye, double? budgetMilliseconds, double started)
    {
        if (_grassPercent == 0)
        {
            return;
        }

        if (!_grassDirty && _grassScanned && Vector3.DistanceSquared(eye, _grassScanEye) < 16f)
        {
            return;
        }

        _grassScanned = true;
        _grassScanEye = eye;
        var reach = Preset.GrassDistance + VoxelChunk.Size + (VoxelChunk.Size / 2);
        var reachSquared = (float)reach * reach;
        var keepSquared = (float)(reach + (2 * VoxelChunk.Size)) * (reach + (2 * VoxelChunk.Size));
        var due = _grassScratch;
        due.Clear();
        foreach (var coord in _resident)
        {
            var gpu = _chunks[coord];
            var distance = Vector3.DistanceSquared(gpu.Centre, eye);
            if (gpu.GrassBuilt)
            {
                if (distance > keepSquared)
                {
                    gpu.ReleaseGrass();
                }
            }
            else if (distance <= reachSquared)
            {
                due.Add(coord);
            }
            else if (gpu.Grass is not null && distance > keepSquared)
            {
                gpu.ReleaseGrass(); // the old layer a remesh left on screen until its new one came, and the chunk is out of reach: let it go
            }
        }

        _grassOrder.Eye = eye;
        due.Sort(_grassOrder);
        var submitted = 0;
        var waiting = false;
        foreach (var coord in due)
        {
            if (budgetMilliseconds is { } budget && submitted > 0 && MonotonicClock.Milliseconds() - started >= budget)
            {
                waiting = true;
                break;
            }

            if (_pipeline.IsGrassFlying(coord))
            {
                continue; // its layer is on its way
            }

            if (!SubmitGrass(coord, _chunks[coord]))
            {
                waiting = true; // the workers have all they can take: the next frame goes on
                break;
            }

            submitted++;
        }

        _grassDirty = waiting; // chunks are still waiting: the next frame goes on
        due.Clear();
    }

    // Hands a chunk's grass layer to the workers (the biomes of its sixteen 4 x 4 blocks and the sky's tile are read here, on the game thread); a chunk with no block that carries grass has none to build. Returns false when the job was not taken.
    private bool SubmitGrass(VoxelChunkCoord coord, ChunkGpu gpu)
    {
        if (!_grid.TryGetChunk(coord, out var chunk) || (chunk.KindMask & _grassBiomes.TopMask) == UInt128.Zero)
        {
            gpu.ReleaseGrass();
            gpu.GrassBuilt = true; // not resident, or no block that carries grass
            return true;
        }

        Func<int, int, string?>? biomeAt = null;
        if (_grassBiomeAt is { } lookup)
        {
            var (x0, z0) = (coord.X * VoxelChunk.Size, coord.Z * VoxelChunk.Size);
            var ids = new string?[16];
            for (var cell = 0; cell < ids.Length; cell++)
            {
                ids[cell] = lookup(x0 + ((cell & 3) << 2) + 2, z0 + ((cell >> 2) << 2) + 2);
            }

            biomeAt = (x, z) => ids[((((z - z0) >> 2) << 2) + ((x - x0) >> 2))];
        }

        var options = new GrassLayerOptions(Preset.GrassCapPercent / 100f, _grassBiomes, biomeAt, _skyExposure?.Tile(coord.X, coord.Z), gpu.Band, ChunkMesher.IsGrey(BlockId.Grass, _tracker.GreyKinds), _effects.ClimateColours ? _ground : null, _glPass is not null);
        return _pipeline.SubmitGrass(coord, options, this);
    }

    // The grass of a chunk, drawn quadrant by quadrant: the slider's share of the density rank tiers, thinned by the distance from the eye of the quadrant's middle, so the layer thins out over its outer half with no
    // edge to see: a tuft at a time goes, a quadrant at a time (8 blocks), never a whole chunk. No allocation.
    private void DrawGrass(ChunkGpu chunk, Vector3 eye, float reach, float slider)
    {
        var buffer = chunk.Grass;
        var glBuffer = chunk.GlGrass;
        if ((buffer is null && glBuffer is null) || chunk.GrassStart is not { } start || chunk.GrassCumulative is not { } cumulative)
        {
            return;
        }

        var origin = chunk.Box.Min;
        var dy = eye.Y - chunk.Centre.Y;
        for (var q = 0; q < GrassMesher.Quadrants; q++)
        {
            var dx = eye.X - (origin.X + ((q & 1) == 0 ? VoxelChunk.Size / 4f : 3f * VoxelChunk.Size / 4f));
            var dz = eye.Z - (origin.Z + ((q & 2) == 0 ? VoxelChunk.Size / 4f : 3f * VoxelChunk.Size / 4f));
            var distance = MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            var share = Math.Clamp((reach - distance) / (reach * 0.5f), 0f, 1f) * slider;
            var tiers = (int)((share * GrassMesher.Tiers) + 0.5f);
            if (tiers <= 0)
            {
                continue;
            }

            var quads = cumulative[(q * (GrassMesher.Tiers + 1)) + Math.Min(tiers, GrassMesher.Tiers)];
            if (quads == 0)
            {
                continue;
            }

            if (glBuffer is not null)
            {
                _glPass!.Draw(glBuffer, start[q], quads); // the quadrant's quads, from its first one (a base vertex)
            }
            else
            {
                _device.SetVertexBuffer(buffer);
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, start[q] * 4, 0, quads * 2);
            }

            _drawnGrassQuads += quads;
        }
    }

    // Lets every mesh go and forgets the versions the tracker holds, so the next Remesh builds the view again, nearest first.
    private void ReleaseAllMeshes()
    {
        foreach (var coord in _resident.ToList())
        {
            _chunks[coord].Release();
        }

        _resident.Clear();
        _tracker.DiscardAll();
        _pipeline.Reset();
        _meshVersion++;
    }

    /// <summary>
    /// Where the fog is complete for a view distance: a little inside it, so the last chunks fade out before they are cut off. The weather's <paramref name="fog"/> (0 to 1, E05) brings it
    /// nearer, straight in the channel (<see cref="SkyWeather.FogReach"/>): at 0 it is the clear-weather distance, at 1 about a third of the view distance. The fog's start keeps its
    /// proportion to the end (<see cref="FogStartFraction"/>).
    /// </summary>
    /// <param name="viewDistance">The view distance in blocks.</param>
    /// <param name="fog">The weather's fog channel.</param>
    public static float FogEndFor(int viewDistance, float fog = 0f) => viewDistance * 0.94f * SkyWeather.FogReach(fog);

    /// <summary>The far plane of the camera for a view distance: past the fog's end and the last chunk's corner.</summary>
    public static float FarPlaneFor(int viewDistance) => viewDistance + (2f * VoxelChunk.Size);

    /// <summary>
    /// The far plane of the camera over a tall streamed world (B07): the chunks are drawn out to the view distance across whatever their height, so the farthest corner of a peak at the world's top
    /// stands a view distance away across and the world's height up or down, and the plane must be past it (a peak at 300 is not cut off while its foot is in view).
    /// </summary>
    /// <param name="viewDistance">The view distance in blocks, across.</param>
    /// <param name="worldHeight">The world's height in blocks (384 by default).</param>
    public static float FarPlaneFor(int viewDistance, int worldHeight)
    {
        var across = viewDistance + (2f * VoxelChunk.Size);
        return MathF.Sqrt((across * across) + ((float)worldHeight * worldHeight)) + VoxelChunk.Size;
    }

    /// <summary>The share of the fog's end where the haze starts when the far view is on (B07): the voxel land stays clear (at the high preset the haze starts at a kilometre, past the chunks) and the far land fades over almost all of its range, 30 percent at 5 km and 60 percent at 10 km of 16, to the sky's own colour at the end, so the far edge is no line.</summary>
    public const float FarFogStartFraction = 0.06f;

    /// <summary>
    /// Where the haze is complete when the far view reaches <paramref name="range"/> blocks (B07): the range itself in clear weather, so the far land fades into the sky colour as it ends, and the
    /// weather's fog brings it in cubically towards the voxel view's own fog end (a thick fog still hides the far view first).
    /// </summary>
    /// <param name="viewDistance">The voxel view distance.</param>
    /// <param name="range">The far view's range.</param>
    /// <param name="fog">The weather's fog channel.</param>
    public static float FarFogEnd(int viewDistance, int range, float fog = 0f)
    {
        var reach = SkyWeather.FogReach(fog);
        return FogEndFor(viewDistance, fog) + (Math.Max(0f, range - (viewDistance * 0.94f)) * reach * reach * reach);
    }

    private float FogEndNow(int viewDistance, float weather) => _farRange > 0 ? FarFogEnd(viewDistance, (int)_farRange, weather) : FogEndFor(viewDistance, weather);

    private float FogStartOf(float end) => end * (_farRange > 0 ? FarFogStartFraction : FogStartFraction);

    private int MeshCap(QualityPreset preset) => _grid.IsStreamed ? preset.MaxMeshedChunks : int.MaxValue;

    /// <summary>Gives the renderer the voxel zone's stage counters (B43): it adds the chunks it meshes, uploads and draws for the first time, or nothing with <see langword="null"/>.</summary>
    /// <param name="counters">The counters, or <see langword="null"/>.</param>
    public void SetPipelineCounters(PipelineCounters? counters) => _counters = counters;

    /// <summary>Gives the sky's exposure the natural surface heights the residency's workers made (B44), so a mesh's sky tile needs no evaluation of the height model on the game thread.</summary>
    /// <param name="heights">The residency's store, or <see langword="null"/>.</param>
    public void SetNaturalHeights(ColumnNaturalHeights? heights) => _skyExposure?.SetNaturalHeights(heights);

    /// <summary>The sky tiles built from the workers' heights and the ones that evaluated the height model on the game thread (B44).</summary>
    internal (long FromHeights, long Computed) SkyTileSources => _skyExposure?.TileSources ?? (0, 0);

    /// <summary>
    /// Gives the renderer the far view to draw beyond the chunks (B07), or <see langword="null"/> for none (the bounded valley, or the far view off): the haze and the far plane follow its range. Call after
    /// <see cref="SetPreset"/> when the range changes.
    /// </summary>
    /// <param name="far">The far view, or <see langword="null"/>.</param>
    public void SetFarView(FarView? far)
    {
        _far = far;
        far?.SetNearReady((cx, cz) => _columnReady.TryGetValue((cx, cz), out var ready) && ready);
        far?.SetGreyKinds(_tracker.GreyKinds, FarTerrainRenderer.Release);
        var range = far is { Settings.Enabled: true } ? far.Settings.Range : 0;
        _farRenderer ??= far is null ? null : new FarTerrainRenderer(_device, far, _atlas.Texture);
        if (range != _farRange)
        {
            _farRange = range;
            SetAtmosphere(_look);
        }
    }

    // Which chunk columns the near zone has wholly drawn (every chunk the view wants in the column has its mesh, and there is one): the far view covers the others (B07e), so no column of the zone is sky or a floating slab while its chunks are made.
    private void RefreshColumnReadiness()
    {
        if (_readinessVersion == _meshVersion)
        {
            return; // no chunk got or lost a mesh and the wanted chunks are the same since the last frame
        }

        _readinessVersion = _meshVersion;
        _columnReady.Clear();
        foreach (var coord in _wanted)
        {
            var key = (coord.X, coord.Z);
            _columnReady[key] = (!_columnReady.TryGetValue(key, out var before) || before) && _tracker.HasMesh(coord);
        }
    }

    /// <summary>Whether the renderer draws a streamed world (its counts of chunks depend on how it was loaded).</summary>
    public bool IsStreamed => _grid.IsStreamed;

    /// <summary>The far view's range in blocks now (0: none).</summary>
    public int FarRange => (int)_farRange;

    /// <summary>How many far tiles the last <see cref="DrawWorld"/> drew.</summary>
    public int DrawnFarTiles => _farRenderer?.DrawnTiles ?? 0;

    /// <summary>Frees a far tile's buffer (the far view drops it).</summary>
    /// <param name="tile">The tile.</param>
    public static void ReleaseFarTile(FarTile tile) => FarTerrainRenderer.Release(tile);

    /// <summary>
    /// Sets the NPC figures that are drawn with the world. Their meshes are built each frame into one reused buffer (the figures idle, talk and turn: S15), so a frame
    /// allocates nothing for them.
    /// </summary>
    public void SetFigures(IReadOnlyList<NpcFigure> figures)
    {
        ArgumentNullException.ThrowIfNull(figures);
        _figures = figures;
    }

    /// <summary>How many quads the figures drew last frame (the NPCs and, in the shoulder view, the player).</summary>
    public int DrawnFigureQuads { get; private set; }

    /// <summary>What the mesh builds have cost so far.</summary>
    public MeshStats Stats
    {
        get
        {
            var vertices = 0;
            var quads = 0;
            foreach (var coord in _resident)
            {
                var chunk = _chunks[coord];
                vertices += (chunk.OpaqueQuads + chunk.AlphaQuads + chunk.PlantQuads + chunk.LitQuads + chunk.GlowQuads) * 4;
                quads += chunk.OpaqueQuads + chunk.AlphaQuads + chunk.PlantQuads + chunk.LitQuads + chunk.GlowQuads;
            }

            return new MeshStats(
                _grid.IsStreamed ? _grid.ResidentChunkCount : BoxChunks(_grid.Limits),
                _meshed,
                vertices,
                quads,
                _totalMilliseconds,
                _longestMilliseconds,
                _wanted.Count,
                _resident.Count,
                _pending,
                _view.EmptyChunks,
                _buriedSkipped,
                _released);
        }
    }

    /// <summary>How many undergrowth quads the last <see cref="DrawWorld"/> drew (the plants of the chunks near the eye).</summary>
    public int DrawnPlantQuads => _drawnPlantQuads;

    /// <summary>How many chunks the last <see cref="DrawWorld"/> drew (the rest were outside the view, outside the frustum or empty).</summary>
    public int DrawnChunks => _drawnChunks;

    /// <summary>How many chunks in view are still waiting for their mesh (zero when the picture is complete).</summary>
    public int PendingChunks => _pending;

    /// <summary>Whether every chunk in view within a few chunks of the eye (across, not up) has its mesh, and there is at least one: the near view is complete (B07b's probe of a move).</summary>
    /// <param name="eye">The eye in blocks.</param>
    /// <param name="chunks">The radius in chunks.</param>
    public bool IsMeshedAround(Vector3 eye, int chunks)
    {
        var centre = VoxelChunkCoord.FromBlock((int)MathF.Floor(eye.X), (int)MathF.Floor(eye.Y), (int)MathF.Floor(eye.Z));
        var any = false;
        foreach (var coord in _wanted)
        {
            if (Math.Abs(coord.X - centre.X) > chunks || Math.Abs(coord.Z - centre.Z) > chunks)
            {
                continue;
            }

            if (!_tracker.HasMesh(coord))
            {
                return false;
            }

            any = true;
        }

        return any;
    }

    /// <summary>
    /// Sets the block kinds drawn grey (the greyscale A/B, <see cref="GreyscaleWorld"/>): the chunks that hold a kind whose grey state changed are
    /// meshed again by the next <see cref="Remesh"/>; the other chunks keep their meshes.
    /// </summary>
    /// <param name="greyKinds">One bit per block kind (<see cref="ChunkMesher.KindBit"/>); 0 draws the whole world in colour.</param>
    public void SetGreyKinds(UInt128 greyKinds)
    {
        _greyScratch.Clear();
        if (_tracker.ChangeGreyKinds(greyKinds, _greyScratch))
        {
            // The meshes in flight were built with the old set: refused when they come back. The chunks that hold a kind whose state changed are built again in the background, the old meshes on screen until then.
            _pipeline.BumpEpoch();
            foreach (var coord in _greyScratch)
            {
                _pipeline.Invalidate(coord);
            }
        }

        _far?.SetGreyKinds(greyKinds, FarTerrainRenderer.Release); // the far land has the chunks' colours (B07)
    }

    /// <summary>
    /// Builds the meshes the eye needs: the chunks in view whose mesh is missing or out of date, in-frustum ones first and nearest first, and lets go of the meshes of the chunks that went out of range. Returns how many meshes it put on
    /// the card. The blocks each chunk reads are copied on this thread and the meshes are built on the workers (<see cref="SetMeshPool"/>); this call takes back what they finished within the time budget (always at least one, so the
    /// view fills even on a slow frame), meshes a chunk that an edit just changed near the eye in the same call, so a block broken or placed shows in the frame, and hands the workers the next chunks (B21). With no workers the
    /// chunks are built here, as they were before. A chunk whose blocks changed after its snapshot is built again, never shown from the old blocks; the old mesh stays on screen until the new one replaces it. A mesh built in another light band than the
    /// look's (E02) is stale, not due: it stays on screen, drawn with its band's tint, until its replacement is built; the due chunks come first, then the stale ones.
    /// </summary>
    /// <param name="eye">The eye in blocks.</param>
    /// <param name="frustum">The camera's frustum, to build what is on screen first, or <see langword="null"/>.</param>
    /// <param name="budgetMilliseconds">The most time to spend on this thread, or <see langword="null"/> to build everything that is due and wait for it (a scripted run before its screenshot).</param>
    public int Remesh(Vector3 eye, BoundingFrustum? frustum = null, double? budgetMilliseconds = null)
    {
        var built = RemeshOnce(eye, frustum, budgetMilliseconds);
        if (budgetMilliseconds is not null)
        {
            return built;
        }

        // No budget: the view is made before the call returns. The workers take their time; this thread waits for them and takes what they finish.
        var waitStarted = MonotonicClock.Milliseconds();
        while ((_pipeline.Busy || (_grassPercent > 0 && _grassDirty)) && MonotonicClock.Milliseconds() - waitStarted < RemeshWaitLimitMilliseconds)
        {
            if (_pipeline.IsAsync)
            {
                Thread.Sleep(1);
            }

            built += RemeshOnce(eye, frustum, null);
        }

        return built;
    }

    private int RemeshOnce(Vector3 eye, BoundingFrustum? frustum, double? budgetMilliseconds)
    {
        UpdateEyeSky(eye);
        var wanted = _view.Update(eye.X, eye.Y, eye.Z);
        var started = MonotonicClock.Milliseconds();
        if (!ReferenceEquals(wanted, _wanted))
        {
            _wanted = wanted;
            ReleaseOutOfRange(eye);
            _pipeline.SetWanted(wanted);
            _meshVersion++;
        }

        // A light placed or removed (or a chunk that holds one coming or going) changes the glow of the chunks within its reach, whose blocks did not change: they are meshed again.
        var lights = _lights.TakeChanged();
        for (var i = 0; i < lights.Count; i++)
        {
            foreach (var reached in ChunkLightIndex.ChunksReachedBy(lights[i]))
            {
                _pipeline.Invalidate(reached);
            }
        }

        // A block placed or removed where it changes how far down the open sky reaches (B12) changes the darkening of the chunks under it: they are meshed again.
        if (_skyExposure is not null)
        {
            var dirty = _skyExposure.TakeDirty();
            for (var i = 0; i < dirty.Count; i++)
            {
                _pipeline.Invalidate(dirty[i]);
            }
        }

        var built = _pipeline.Frame(eye, frustum, budgetMilliseconds, started, this);
        _pending = _pipeline.Pending;
        UpdateGrass(eye, budgetMilliseconds, started);
        return built;
    }

    /// <summary>
    /// Draws the world from the camera: the opaque chunks in view and in the frustum, the NPC figures and the block highlight, then the water and the
    /// beacon column. The caller clears the target first and draws the layers afterwards.
    /// </summary>
    /// <param name="camera">The camera for this frame.</param>
    /// <param name="highlight">The block to frame, or <see langword="null"/>.</param>
    /// <param name="beacon">The block the beacon column stands on, or <see langword="null"/>.</param>
    /// <param name="tick">The simulation tick (animates the beacon and the figures).</param>
    /// <param name="avatar">The player's own figure, drawn in the behind-the-shoulder view, or <see langword="null"/> in first person.</param>
    /// <param name="arm">The first-person arm and what it holds, drawn last over a cleared depth buffer, or <see langword="null"/> in the shoulder view.</param>
    /// <param name="plotOutlines">The outlines of the plots of land near the eye (<see cref="OverlayMeshes.PlotOutline"/>, Y10), drawn in the sun's light after the figures, or <see langword="null"/>.</param>
    /// <param name="overlay">Drawn after the figures, the water and the beacon and before the arm clears the depth buffer, so it can test against the scene (the NPCs' name plates, V22), or <see langword="null"/>.</param>
    public void DrawWorld(FirstPersonCamera camera, BlockPos? highlight, BlockPos? beacon, long tick, PlayerAvatar? avatar = null, ArmFrame? arm = null, Action? overlay = null, IReadOnlyList<MeshBuffer>? plotOutlines = null)
    {
        _sky.Draw(camera, tick, Preset.Clouds);

        if (_far is { Settings.Enabled: true } && _farRenderer is not null)
        {
            RefreshColumnReadiness();

            // The far view (B07): beyond the chunks, in a pass of its own with its own depth range and the same haze, then the depth is cleared and the chunks are drawn over it. Through the program (B17c, the far view's light: the same sun, glow's colour,
            // fog and fog by height as the chunks) when the context runs it and the setting is on; otherwise through MonoGame's effects, in the day's baked light.
            FarGlDraw? farGl = null;
            if (_glPass is { } through && _effects.FarLight && !_farGlFailed)
            {
                if (!through.HasFarTextures)
                {
                    _farGlFailed = !_farRenderer.PrepareGl(through);
                }

                if (!_farGlFailed)
                {
                    farGl = new FarGlDraw(through, LightFrame(camera.Eye, tick), _device.Viewport, _device.PresentationParameters.BackBufferHeight, _device.RenderTargetCount > 0);
                }
            }

            _farRenderer.Draw(camera, new FarDrawState(_effect.FogColor, _effect.FogStart, _effect.FogEnd, _tints[(int)LightBand.Day]), farGl);
        }

        _device.DepthStencilState = DepthStencilState.Default;
        _device.RasterizerState = RasterizerState.CullClockwise; // triangles run counter-clockwise seen from outside
        _device.BlendState = BlendState.Opaque;
        _device.SamplerStates[0] = AtlasSampler;
        _effect.World = Matrix.Identity;
        _effect.View = camera.View;
        _effect.Projection = camera.Projection;
        _effect.Texture = _atlas.Texture;
        _effect.FogEnabled = true;
        _solid.World = Matrix.Identity;
        _solid.View = camera.View;
        _solid.Projection = camera.Projection;
        _solid.Texture = _atlas.Texture;

        _visible.Clear();
        Array.Clear(_bandChunks);
        var firstSeen = 0;
        foreach (var coord in _wanted)
        {
            var chunk = GpuOf(coord);
            if (chunk.Visible(camera.Frustum))
            {
                _visible.Add(chunk);
                _bandChunks[(int)chunk.Band]++;
                if (!chunk.SeenCounted)
                {
                    chunk.SeenCounted = true;
                    firstSeen++;
                }
            }
        }

        _counters?.Add(PipelineStage.Visible, firstSeen, MonotonicClock.Milliseconds());

        _drawnChunks = _visible.Count;
        _device.Indices = _indices;

        _card?.NextFrame(); // the results of the card's earlier frames are collected here
        var spent = _glPass is not null ? DrawChunksGl(camera, tick, highlight, avatar, plotOutlines) : DrawChunksEffects(camera, tick, highlight, avatar, plotOutlines);
        ChunkPassTimes.Record(spent);

        if (_glPass is not null)
        {
            // What the water leaves for the rain after it, in MonoGame's own state (the scope above never touched the device's): the water's blend and depth states, its sampler and its frame.
            _device.BlendState = BlendState.NonPremultiplied;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.SamplerStates[0] = WaterTextures.Sampler;
            _effect.Texture = _water.FrameAt(tick);
            _device.Indices = _indices;
        }

        // The rain (E06): after the water, in the water's blend and depth states (alpha, depth read only), fogged like the world, and not under water.
        _rain.Draw(camera, tick, _precipitation, Preset.RainDrops, _sunLight, _effect.FogColor, _effect.FogStart, _effect.FogEnd, _underwaterAmount);
        _device.RasterizerState = RasterizerState.CullClockwise;
        _device.Indices = _indices;

        _device.SamplerStates[0] = AtlasSampler;
        _effect.Texture = _atlas.Texture;
        _effect.DiffuseColor = Vector3.One;

        if (beacon is { } target)
        {
            // The beacon is a signal: it is not lost in the haze, however far the place is.
            _effect.FogEnabled = false;
            _effect.CurrentTechnique.Passes[0].Apply();
            DrawDynamic(OverlayMeshes.Beacon(target, tick));
            _effect.FogEnabled = true;
        }

        overlay?.Invoke();

        if (arm is { } held)
        {
            DrawArm(camera, held);
        }

        _device.DepthStencilState = DepthStencilState.None;
        _device.BlendState = BlendState.Opaque;
    }

    // The chunk passes through MonoGame's effects, as they have always been drawn (the built-in path, and the fallback of the GLSL one): the opaque faces by band, the lit pieces, the undergrowth, the grass, the glow, then the figures and
    // the highlight, then the translucent water chunk by chunk from the farthest to the nearest. Returns the processor time spent submitting the chunk draws (the figures not counted); the card's timer, when there is one, brackets the same.
    private double DrawChunksEffects(FirstPersonCamera camera, long tick, BlockPos? highlight, PlayerAvatar? avatar, IReadOnlyList<MeshBuffer>? plotOutlines)
    {
        var started = MonotonicClock.Milliseconds();
        _card?.Begin();
        // The chunks are drawn grouped by the light band their mesh was built in (E02), each group tinted so that its baked light shows as the look's light: normally one group,
        // two while a change of band is being built again.
        for (var band = 0; band < LightBands.Count; band++)
        {
            if (_bandChunks[band] == 0)
            {
                continue;
            }

            _solid.DiffuseColor = _tints[band];
            _solid.CurrentTechnique.Passes[0].Apply();
            foreach (var chunk in _visible)
            {
                if ((int)chunk.Band == band)
                {
                    DrawChunk(chunk.Opaque, chunk.OpaqueQuads);
                }
            }
        }

        // The self-lit pieces (a lantern's glass, a torch's flame): in their own light, so no band's tint touches them.
        _solid.DiffuseColor = Vector3.One;
        _solid.CurrentTechnique.Passes[0].Apply();
        foreach (var chunk in _visible)
        {
            DrawChunk(chunk.Lit, chunk.LitQuads);
        }

        // The undergrowth: crossed quads with both sides showing, only in the chunks near the eye.
        var eye = camera.Eye;
        var plantReach = Preset.PlantDistance + VoxelChunk.Size;
        var plantReachSquared = (float)plantReach * plantReach;
        _device.RasterizerState = RasterizerState.CullNone;
        _drawnPlantQuads = 0;
        for (var band = 0; band < LightBands.Count; band++)
        {
            if (_bandChunks[band] == 0)
            {
                continue;
            }

            _solid.DiffuseColor = _tints[band];
            _solid.CurrentTechnique.Passes[0].Apply();
            foreach (var chunk in _visible)
            {
                if ((int)chunk.Band == band && chunk.PlantQuads > 0 && Vector3.DistanceSquared(chunk.Centre, eye) <= plantReachSquared)
                {
                    DrawChunk(chunk.Plants, chunk.PlantQuads);
                    _drawnPlantQuads += chunk.PlantQuads;
                }
            }
        }

        // The decorative grass (B18): the tufts on the grass blocks' tops, in the same pass and tint as the undergrowth, thinning out towards the edge of their distance.
        _drawnGrassQuads = 0;
        if (_grassPercent > 0)
        {
            var grassReach = (float)Preset.GrassDistance;
            var grassChunkReach = (grassReach + VoxelChunk.Size) * (grassReach + VoxelChunk.Size);
            var slider = _grassPercent / 100f;
            for (var band = 0; band < LightBands.Count; band++)
            {
                if (_bandChunks[band] == 0)
                {
                    continue;
                }

                _solid.DiffuseColor = _tints[band];
                _solid.CurrentTechnique.Passes[0].Apply();
                foreach (var chunk in _visible)
                {
                    if ((int)chunk.Band == band && chunk.Grass is not null && Vector3.DistanceSquared(chunk.Centre, eye) <= grassChunkReach)
                    {
                        DrawGrass(chunk, eye, grassReach, slider);
                    }
                }
            }
        }

        _solid.DiffuseColor = Vector3.One;
        _device.RasterizerState = RasterizerState.CullClockwise;
        DrawGlow();
        var spent = MonotonicClock.Milliseconds() - started;
        _card?.End();

        DrawFiguresAndOverlays(tick, highlight, avatar, plotOutlines);

        var waterStarted = MonotonicClock.Milliseconds();
        _card?.Begin();
        // The translucent pass: the water chunks from the farthest to the nearest (a sort by chunk, as the files say), reading depth but not writing it.
        // The water has its own animated textures (H09): the frame for this tick, bilinear, in place of the block atlas for this pass only.
        _device.BlendState = BlendState.NonPremultiplied;
        _device.DepthStencilState = DepthStencilState.DepthRead;
        _device.SamplerStates[0] = WaterTextures.Sampler;
        _effect.Texture = _water.FrameAt(tick);
        _visible.Sort((a, b) => Vector3.DistanceSquared(b.Centre, eye).CompareTo(Vector3.DistanceSquared(a.Centre, eye)));
        _device.Indices = _indices;
        var waterBand = -1; // the water takes its band's tint like the rest of the chunk; the effect is applied again only where the band changes along the sorted chunks
        foreach (var chunk in _visible)
        {
            if (chunk.AlphaQuads == 0)
            {
                continue;
            }

            if ((int)chunk.Band != waterBand)
            {
                waterBand = (int)chunk.Band;
                _effect.DiffuseColor = _tints[waterBand];
                _effect.CurrentTechnique.Passes[0].Apply();
            }

            DrawChunk(chunk.Alpha, chunk.AlphaQuads);
        }
        _card?.End();
        return spent + (MonotonicClock.Milliseconds() - waterStarted);
    }

    // The figures and the player's avatar, the plots' outlines and the block's frame: MonoGame's, in the sun's light, after the chunks' opaque passes in the effect path and before the GLSL scope in the other (no MonoGame call may stand
    // inside the scope; these are opaque and test depth, so where they are drawn only changes which of two surfaces at exactly the same depth wins).
    private void DrawFiguresAndOverlays(long tick, BlockPos? highlight, PlayerAvatar? avatar, IReadOnlyList<MeshBuffer>? plotOutlines)
    {
        // The figures and the player's avatar are lit by the look's sun light directly (their colours are not baked in a band) and, as before, warmed near a lantern.
        _effect.DiffuseColor = _sunLight;
        _effect.CurrentTechnique.Passes[0].Apply();
        DrawnFigureQuads = 0;
        foreach (var figure in _figures)
        {
            var (fx, fy, fz) = figure.FeetPosition;
            _figureBuffer.Clear();
            var warm = ArmLight.Warmth(Lights, fx, fy + 1f, fz, GlowStrength);
            FigureMesher.Append(_figureBuffer, NpcFigures.PartsOf(figure.Look), FigureAnimation.For(figure, tick), fx, fy, fz, figure.FacingRadians, FigureLight(warm));
            DrawnFigureQuads += _figureBuffer.FaceCount;
            DrawDynamic(_figureBuffer);
        }

        if (avatar is { } player)
        {
            _figureBuffer.Clear();
            var warm = ArmLight.Warmth(Lights, player.X, player.Y + 1f, player.Z, GlowStrength);
            var pose = player.Moving ? FigureAnimation.Walking(player.WalkPhase) : FigureAnimation.Idle(tick, 3);
            pose = pose with { HeadPitch = Math.Clamp(-player.PitchRadians, -0.6f, 0.6f) };
            FigureMesher.Append(_figureBuffer, NpcFigures.PartsOf(NpcFigures.PlayerName), pose, player.X, player.Y, player.Z, player.YawRadians, FigureLight(warm));
            DrawnFigureQuads += _figureBuffer.FaceCount;
            DrawDynamic(_figureBuffer);
        }

        if (plotOutlines is not null)
        {
            foreach (var outline in plotOutlines)
            {
                DrawDynamic(outline); // the sun's light is still the diffuse colour here, as for the figures
            }
        }

        _effect.DiffuseColor = Vector3.One; // the block's frame is not dimmed by the night
        _effect.CurrentTechnique.Passes[0].Apply();
        if (highlight is { } block)
        {
            DrawDynamic(OverlayMeshes.Highlight(block));
        }
    }

    // The frame's light for the program (B17c), from the look of the moment and the effects in force: where the sun stands for the faces' shade and its colour, what the block light adds, the height fog and the water's movement. A few uniforms a frame:
    // the hour, the weather and the glow change with no mesh built again.
    private GlLightFrame LightFrame(Vector3 eye, long tick)
    {
        var direction = SunModel.ShadingDirection(_look, _effects.SunFollowsHour);
        var (glowR, glowG, glowB) = LanternGlow.PassColour(GlowStrength);
        var haze = _effects.HeightFog && _underwaterAmount <= 0f ? HeightFogDensity * (1f + (2f * _look.Fog)) : 0f; // thicker in fog and rain; none under water, where the water's own fog is
        var day = ChunkMesher.SunDirection;
        return new GlLightFrame(
            eye,
            new Vector3(direction.X, direction.Y, direction.Z),
            _sunLight,
            new Vector3(day.X, day.Y, day.Z),
            new Vector3(glowR, glowG, glowB),
            haze,
            HeightFogBase,
            1f / HeightFogScale,
            _effects.WaterShader,
            tick / (float)Vocaterra.Game.Core.Atmosphere.DayClock.TicksPerSecond,
            WaterWave);
    }

    /// <summary>The fog by height's density at its base height, per block (B17c): about 8 percent haze for a kilometre of the low land in the ray's way (half a percent at the near view's 60 blocks, 3 percent at its edge), more with distance: the far view lies in it.</summary>
    public const float HeightFogDensity = 0.00008f;

    /// <summary>The height, in blocks, at which the fog by height has its full density (the sea level and the valley floors; a ridge above it stands out of the haze).</summary>
    public const float HeightFogBase = 64f;

    /// <summary>How many blocks up the fog by height falls to a third of its density.</summary>
    public const float HeightFogScale = 64f;

    /// <summary>How far the water shader moves the surface, in blocks (B17c): a gentle swell.</summary>
    public const float WaterWave = 0.035f;

    // The chunk passes through the game's own GLSL program (B17a to B17c), in one state scope of the frame: the opaque faces and the lit pieces, the undergrowth and the grass (both sides showing), then the water from the farthest chunk to the nearest
    // (translucent, depth read only, its own frame of the animated art). The meshes carry their light as data and the program combines it with the frame's sun, glow and haze, so there are no bands, no tints and no glow pass: the glow is the block light
    // in the opaque faces' own vertices. The figures, the plots' outlines and the block's frame are drawn before it (MonoGame's, opaque, depth tested): no MonoGame call may stand inside the scope. Returns the processor time spent submitting.
    private double DrawChunksGl(FirstPersonCamera camera, long tick, BlockPos? highlight, PlayerAvatar? avatar, IReadOnlyList<MeshBuffer>? plotOutlines)
    {
        DrawFiguresAndOverlays(tick, highlight, avatar, plotOutlines);
        var pass = _glPass!;
        var eye = camera.Eye;
        var viewport = _device.Viewport;
        var backBufferHeight = _device.PresentationParameters.BackBufferHeight;
        var renderTarget = _device.RenderTargetCount > 0;
        var started = MonotonicClock.Milliseconds();
        _card?.Begin();
        pass.Begin(camera.View, camera.Projection, _solid.FogColor, _solid.FogStart, _solid.FogEnd, viewport, backBufferHeight, renderTarget, LightFrame(eye, tick));

        foreach (var chunk in _visible)
        {
            if (chunk.GlOpaque is { } buffer)
            {
                pass.Draw(buffer);
            }
        }

        // The self-lit pieces (a lantern's glass, a torch's flame): their words are fixed, so no hour touches them.
        foreach (var chunk in _visible)
        {
            if (chunk.GlLit is { } buffer)
            {
                pass.Draw(buffer);
            }
        }

        // The undergrowth: crossed quads with both sides showing, only in the chunks near the eye.
        var plantReach = Preset.PlantDistance + VoxelChunk.Size;
        var plantReachSquared = (float)plantReach * plantReach;
        pass.Use(GlPassKind.Cutout);
        _drawnPlantQuads = 0;
        foreach (var chunk in _visible)
        {
            if (chunk.PlantQuads > 0 && chunk.GlPlants is { } buffer && Vector3.DistanceSquared(chunk.Centre, eye) <= plantReachSquared)
            {
                pass.Draw(buffer);
                _drawnPlantQuads += chunk.PlantQuads;
            }
        }

        // The decorative grass (B18): the tufts on the grass blocks' tops, in the same pass as the undergrowth, thinning out towards the edge of their distance.
        _drawnGrassQuads = 0;
        if (_grassPercent > 0)
        {
            var grassReach = (float)Preset.GrassDistance;
            var grassChunkReach = (grassReach + VoxelChunk.Size) * (grassReach + VoxelChunk.Size);
            var slider = _grassPercent / 100f;
            foreach (var chunk in _visible)
            {
                if (chunk.GlGrass is not null && Vector3.DistanceSquared(chunk.Centre, eye) <= grassChunkReach)
                {
                    DrawGrass(chunk, eye, grassReach, slider);
                }
            }
        }

        // The translucent pass: the water chunks from the farthest to the nearest (a sort by chunk, as the files say), reading depth but not writing it, in the water's own animated frame for this tick (H09).
        pass.Use(GlPassKind.Water);
        pass.SetWaterFrame(WaterFrames.IndexAt(tick));
        _visible.Sort((a, b) => Vector3.DistanceSquared(b.Centre, eye).CompareTo(Vector3.DistanceSquared(a.Centre, eye)));
        foreach (var chunk in _visible)
        {
            if (chunk.GlAlpha is { } buffer)
            {
                pass.Draw(buffer);
            }
        }

        pass.End(); // the context's state is back as MonoGame left it
        _card?.End();
        return MonotonicClock.Milliseconds() - started;
    }

    // The lanterns' glow (E02a): the quads of the faces a lantern reaches, drawn again over the opaque pass and added to it, with the depth tested and not written. They are the very
    // quads of the opaque mesh (the same positions, in the same order, through the same transform and the same effect), so every pixel they cover is at the very depth the face wrote and
    // the "less or equal" test lets it through: no fighting. The strength is the look's (continuous over the day, untouched by the weather), and the fog fades the glow out to nothing
    // (the fog colour is black for this pass: the colour added at a distance is none) instead of adding haze.
    private void DrawGlow()
    {
        var any = false;
        foreach (var chunk in _visible)
        {
            if (chunk.GlowQuads > 0)
            {
                any = true;
                break;
            }
        }

        if (!any)
        {
            return;
        }

        var fogColour = _solid.FogColor;
        _device.BlendState = GlowBlend;
        _device.DepthStencilState = DepthStencilState.DepthRead;
        var (r, g, b) = LanternGlow.PassColour(GlowStrength);
        _solid.DiffuseColor = new Vector3(r, g, b);
        _solid.FogColor = Vector3.Zero;
        _solid.CurrentTechnique.Passes[0].Apply();
        foreach (var chunk in _visible)
        {
            DrawChunk(chunk.Glow, chunk.GlowQuads);
        }

        _solid.FogColor = fogColour;
        _solid.DiffuseColor = Vector3.One;
        _device.BlendState = BlendState.Opaque;
        _device.DepthStencilState = DepthStencilState.Default;
    }

    /// <summary>The blend of the glow pass: the colour is added to what is there (the picture times the glow times the strength), and the alpha is left as it is.</summary>
    private static readonly BlendState GlowBlend = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
    };

    // The colour a lantern's warmth gives a figure on top of the sun: amber added to the three channels.
    private static (float R, float G, float B) FigureLight(float warm) =>
        (1f + (ChunkMesher.GlowColour.R * warm * 0.45f), 1f + (ChunkMesher.GlowColour.G * warm * 0.45f), 1f + (ChunkMesher.GlowColour.B * warm * 0.45f));

    // Where the arm's elbow end is in the view space (blocks) and how it is turned: at the bottom right, the hand forward and up towards the middle of the view.
    private const float ArmX = 0.42f;
    private const float ArmY = -0.56f;
    private const float ArmZ = -0.46f;
    private const float ArmPitch = 0.42f;
    private const float ArmYaw = 0.34f;

    // The first-person arm: the depth buffer is cleared so the arm never sinks into a wall, then the arm and the item in its hand are drawn in the view space with
    // the projection of the camera, tinted by the light of the scene (ArmLight), and the flame of a torch at full brightness.
    private void DrawArm(FirstPersonCamera camera, in ArmFrame frame)
    {
        _device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1f, 0);
        _device.DepthStencilState = DepthStencilState.Default;
        _device.RasterizerState = RasterizerState.CullClockwise;
        _device.BlendState = BlendState.Opaque;
        var m = frame.Motion;
        _solid.World = Matrix.CreateRotationX(ArmPitch + m.Pitch) * Matrix.CreateRotationY(ArmYaw) * Matrix.CreateTranslation(ArmX + m.SwayX, ArmY + m.SwayY, ArmZ - m.Forward);
        _solid.View = Matrix.Identity;
        _solid.Projection = camera.Projection;
        _solid.FogEnabled = false;
        _solid.DiffuseColor = new Vector3(frame.Light.R, frame.Light.G, frame.Light.B);
        _device.Indices = _indices;
        _solid.CurrentTechnique.Passes[0].Apply();
        DrawDynamic(ArmMeshes.Arm());
        var (body, glow) = ArmMeshes.Item(frame.Held);
        DrawDynamic(body);
        if (glow.VertexCount > 0)
        {
            _solid.DiffuseColor = Vector3.One;
            _solid.CurrentTechnique.Passes[0].Apply();
            DrawDynamic(glow);
        }

        _solid.DiffuseColor = Vector3.One;
        _solid.FogEnabled = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _grid.RemoveResidencyListener(_tracker);
        _grid.RemoveResidencyListener(this);
        _view.Detach();
        _pipeline.Dispose();
        _far?.Clear(FarTerrainRenderer.Release); // the far tiles' GPU buffers are freed with the device still alive, not left to finalizers that run after it is gone (a native crash at exit)
        _farRenderer?.Dispose();
        _lights.Detach();
        foreach (var chunk in _chunks.Values)
        {
            chunk.Release();
        }

        _card?.Dispose();
        _glPass?.Dispose();
        _indices.Dispose();
        _effect.Dispose();
        _solid.Dispose();
        _sky.Dispose();
        _rain.Dispose();
    }

    private static int BoxChunks(WorldLimits limits) => (limits.EndChunkX - limits.MinChunkX) * (limits.EndChunkY - limits.MinChunkY) * (limits.EndChunkZ - limits.MinChunkZ);

    // The mesh drawing state of a chunk, made when first asked for.
    private ChunkGpu GpuOf(VoxelChunkCoord coord)
    {
        if (!_chunks.TryGetValue(coord, out var gpu))
        {
            var origin = new Vector3(coord.X * VoxelChunk.Size, coord.Y * VoxelChunk.Size, coord.Z * VoxelChunk.Size);
            _chunks[coord] = gpu = new ChunkGpu(new BoundingBox(origin, origin + new Vector3(VoxelChunk.Size)));
        }

        return gpu;
    }

    /// <summary>A chunk was installed: nothing to do here, the view set sees it (the grid's residency version moved) and it is meshed when it is in view.</summary>
    /// <param name="coord">The chunk.</param>
    public void OnChunkInstalled(VoxelChunkCoord coord)
    {
    }

    /// <summary>A chunk was evicted: its mesh is let go with it.</summary>
    /// <param name="coord">The chunk.</param>
    public void OnChunkEvicted(VoxelChunkCoord coord)
    {
        if (_chunks.Remove(coord, out var gpu))
        {
            gpu.Release();
        }

        _resident.Remove(coord);
        _meshVersion++;
    }

    /// <inheritdoc />
    void IChunkMeshSink.Install(ChunkMeshResult result)
    {
        var coord = result.Coord;
        var gpu = GpuOf(coord);
        _meshVersion++;
        if (!gpu.MeshCounted)
        {
            gpu.MeshCounted = true;
            _counters?.Add(PipelineStage.Meshed, 1, result.FinishedAt);
        }

        if (result.Buried)
        {
            gpu.Release();
            _buriedSkipped++;
            _tracker.MarkMeshed(coord, result.Kinds, result.Band);
            _resident.Remove(coord);
            return;
        }

        // The new buffers go in place of the old in one step, so the chunk is never drawn without its mesh between two frames (an edit does not flicker).
        if (_glPass is { } glPass)
        {
            // Every part goes to the game's own buffers, and MonoGame gets none of them (B17a, B17b): on the game thread, as the pipeline hands them over.
            var glOpaque = UploadGl(glPass, result.Opaque);
            var glAlpha = UploadGl(glPass, result.Alpha);
            var glPlants = UploadGl(glPass, result.Plants);
            var glLit = UploadGl(glPass, result.Lit);
            gpu.ReleaseMeshes();
            (gpu.GlOpaque, gpu.OpaqueQuads) = (glOpaque, result.Opaque.Quads);
            (gpu.GlAlpha, gpu.AlphaQuads) = (glAlpha, result.Alpha.Quads);
            (gpu.GlPlants, gpu.PlantQuads) = (glPlants, result.Plants.Quads);
            (gpu.GlLit, gpu.LitQuads) = (glLit, result.Lit.Quads);
        }
        else
        {
            var (opaque, opaqueQuads) = Upload(result.Opaque);
            var (alpha, alphaQuads) = Upload(result.Alpha);
            var (plants, plantQuads) = Upload(result.Plants);
            var (lit, litQuads) = Upload(result.Lit);
            var (glow, glowQuads) = Upload(result.Glow);
            gpu.ReleaseMeshes();
            (gpu.Opaque, gpu.OpaqueQuads) = (opaque, opaqueQuads);
            (gpu.Alpha, gpu.AlphaQuads) = (alpha, alphaQuads);
            (gpu.Plants, gpu.PlantQuads) = (plants, plantQuads);
            (gpu.Lit, gpu.LitQuads) = (lit, litQuads);
            (gpu.Glow, gpu.GlowQuads) = (glow, glowQuads);
        }

        _tracker.MarkMeshed(coord, result.Kinds, result.Band);
        gpu.Band = result.Band;
        gpu.GrassBuilt = false; // the grass layer was made for the old blocks: built again, and the old one drawn until the new one comes
        if (gpu.HasMesh)
        {
            _resident.Add(coord);
            if (!gpu.UploadCounted)
            {
                gpu.UploadCounted = true;
                _counters?.Add(PipelineStage.GpuReady, 1, MonotonicClock.Milliseconds());
            }
        }
        else
        {
            _resident.Remove(coord);
        }

        _grassDirty = true;
        _meshed++;
        _totalMilliseconds += result.Milliseconds;
        _longestMilliseconds = Math.Max(_longestMilliseconds, result.Milliseconds);
    }

    /// <inheritdoc />
    void IChunkMeshSink.InstallGrass(ChunkMeshResult result)
    {
        if (_grassPercent == 0)
        {
            return; // the slider went to 0 while the layer was being made
        }

        if (!_chunks.TryGetValue(result.Coord, out var gpu) || result.Band != gpu.Band)
        {
            _grassDirty = true; // the chunk's mesh was built again in another band meanwhile: the layer is made for that one
            return;
        }

        gpu.ReleaseGrass();
        gpu.GrassBuilt = true;
        if (result.GrassStart is { } start && result.GrassCumulative is { } cumulative)
        {
            if (_glPass is { } glPass)
            {
                gpu.GlGrass = UploadGl(glPass, result.GrassMesh);
                gpu.GrassQuads = gpu.GlGrass is null ? 0 : result.GrassMesh.Quads;
            }
            else
            {
                var (buffer, quads) = Upload(result.GrassMesh);
                gpu.Grass = buffer;
                gpu.GrassQuads = quads;
            }

            gpu.GrassStart = start;
            gpu.GrassCumulative = cumulative;
        }

        _grassBuilt++;
        _grassMilliseconds += result.Milliseconds;
    }

    /// <inheritdoc />
    void IChunkMeshSink.GrassRefused(VoxelChunkCoord coord) => _grassDirty = true;

    /// <inheritdoc />
    void IChunkMeshSink.Release(VoxelChunkCoord coord)
    {
        if (_chunks.TryGetValue(coord, out var gpu))
        {
            gpu.Release();
        }

        _tracker.Discard(coord);
        _resident.Remove(coord);
        _meshVersion++;
    }

    // A chunk that has a mesh but is beyond the keep distance of the eye lets it go (and is due again if the eye comes back).
    private void ReleaseOutOfRange(Vector3 eye)
    {
        var keep = (long)_view.KeepDistance * _view.KeepDistance;
        List<VoxelChunkCoord>? let = null;
        foreach (var c in _resident)
        {
            // A streamed world keeps what is within the view across, whatever the height (the surface band decides the layers); a bounded one measures in three dimensions as it always did.
            var distance = _grid.IsStreamed ? ChunkViewSet.AcrossDistanceSquared(eye.X, eye.Z, c.X, c.Z) : ChunkViewSet.DistanceSquared(eye.X, eye.Y, eye.Z, c.X, c.Y, c.Z);
            if (distance > keep)
            {
                (let ??= []).Add(c);
            }
        }

        if (let is null)
        {
            return;
        }

        foreach (var c in let)
        {
            _chunks[c].Release();
            _tracker.Discard(c);
            _pipeline.Forget(c);
            _resident.Remove(c);
            _released++;
        }
    }

    private static IndexBuffer BuildIndexBuffer(GraphicsDevice device, int quads)
    {
        var indices = new ushort[quads * 6];
        for (var q = 0; q < quads; q++)
        {
            var v = (ushort)(q * 4);
            var i = q * 6;
            indices[i] = v;
            indices[i + 1] = (ushort)(v + 1);
            indices[i + 2] = (ushort)(v + 2);
            indices[i + 3] = v;
            indices[i + 4] = (ushort)(v + 2);
            indices[i + 5] = (ushort)(v + 3);
        }

        var buffer = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        buffer.SetData(indices);
        return buffer;
    }

    private static void WriteQuadIndices(short[] indices, int quad)
    {
        var v = (short)(quad * 4);
        var i = quad * 6;
        indices[i] = v;
        indices[i + 1] = (short)(v + 1);
        indices[i + 2] = (short)(v + 2);
        indices[i + 3] = v;
        indices[i + 4] = (short)(v + 2);
        indices[i + 5] = (short)(v + 3);
    }

    private (VertexBuffer? Buffer, int Quads) Upload(MeshPart part)
    {
        if (part.Quads == 0 || part.Vertices is null)
        {
            return (null, 0);
        }

        var buffer = new VertexBuffer(_device, VertexPositionColorTexture.VertexDeclaration, part.Quads * 4, BufferUsage.WriteOnly);
        buffer.SetData(part.Vertices, 0, part.Quads * 4);
        return (buffer, part.Quads);
    }

    private static GlChunkBuffer? UploadGl(GlChunkPass pass, MeshPart part) => part.Quads == 0 || part.Vertices is null ? null : pass.Upload(part.Vertices, part.Lights, part.Quads);

    private void DrawChunk(VertexBuffer? buffer, int quads)
    {
        if (buffer is null)
        {
            return;
        }

        _device.SetVertexBuffer(buffer);
        _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, quads * 2);
    }

    // A small mesh built this frame (a figure, the highlight, the beacon), drawn from user arrays.
    private void DrawDynamic(MeshBuffer mesh)
    {
        var count = mesh.VertexCount;
        if (count == 0 || count > 4096)
        {
            return;
        }

        if (_scratchVertices.Length < count)
        {
            _scratchVertices = new VertexPositionColorTexture[count];
        }

        var source = mesh.Vertices;
        for (var i = 0; i < count; i++)
        {
            var v = source[i];
            _scratchVertices[i] = new VertexPositionColorTexture(new Vector3(v.X, v.Y, v.Z), new Color(v.Color), new Vector2(v.U, v.V));
        }

        _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _scratchVertices, 0, count, _scratchIndices, 0, count / 2);
        _device.Indices = _indices; // the user-array draw leaves no index buffer bound; the chunk draws set it again
    }

    // Nearest to the eye first (the grass scan sorts with this one object, so a scan makes no closure).
    private sealed class GrassOrder(VoxelRenderer owner) : IComparer<VoxelChunkCoord>
    {
        public Vector3 Eye { get; set; }

        public int Compare(VoxelChunkCoord a, VoxelChunkCoord b)
        {
            var da = Vector3.DistanceSquared(owner._chunks[a].Centre, Eye);
            var db = Vector3.DistanceSquared(owner._chunks[b].Centre, Eye);
            return da != db ? da.CompareTo(db) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X);
        }
    }

    private sealed class ChunkGpu(BoundingBox box)
    {
        public VertexBuffer? Opaque;
        public GlChunkBuffer? GlOpaque;
        public GlChunkBuffer? GlAlpha;
        public GlChunkBuffer? GlPlants;
        public GlChunkBuffer? GlLit;
        public GlChunkBuffer? GlGrass;
        public bool HasOpaque => Opaque is not null || GlOpaque is not null;
        public bool HasMesh => HasOpaque || Alpha is not null || Plants is not null || Lit is not null || GlAlpha is not null || GlPlants is not null || GlLit is not null;
        public int OpaqueQuads;
        public VertexBuffer? Alpha;
        public int AlphaQuads;
        public VertexBuffer? Plants;
        public int PlantQuads;
        public VertexBuffer? Lit;
        public int LitQuads;
        public VertexBuffer? Glow;
        public int GlowQuads;
        public VertexBuffer? Grass;
        public int GrassQuads;
        public int[]? GrassStart;
        public int[]? GrassCumulative;
        public bool GrassBuilt;
        public bool MeshCounted; // the B43 stage counters have counted this chunk's first mesh, upload and draw
        public bool UploadCounted;
        public bool SeenCounted;
        public LightBand Band;
        public BoundingBox Box { get; } = box;
        public Vector3 Centre { get; } = (box.Min + box.Max) / 2f;

        public bool Visible(BoundingFrustum frustum) => HasMesh && frustum.Intersects(Box);

        public void ReleaseGrass()
        {
            Grass?.Dispose();
            Grass = null;
            GlGrass?.Dispose();
            GlGrass = null;
            GrassQuads = 0;
            GrassStart = null;
            GrassCumulative = null;
            GrassBuilt = false;
        }

        public void Release()
        {
            ReleaseGrass();
            ReleaseMeshes();
        }

        public void ReleaseMeshes()
        {
            GlOpaque?.Dispose();
            GlAlpha?.Dispose();
            GlPlants?.Dispose();
            GlLit?.Dispose();
            GlOpaque = GlAlpha = GlPlants = GlLit = null;
            Opaque?.Dispose();
            Alpha?.Dispose();
            Plants?.Dispose();
            Lit?.Dispose();
            Glow?.Dispose();
            Opaque = null;
            Alpha = null;
            Plants = null;
            Lit = null;
            Glow = null;
            OpaqueQuads = 0;
            AlphaQuads = 0;
            PlantQuads = 0;
            LitQuads = 0;
            GlowQuads = 0;
        }
    }
}

/// <summary>Where the player's own figure stands, for the behind-the-shoulder view (S15).</summary>
/// <param name="X">The middle of the feet, east, in blocks.</param>
/// <param name="Y">The feet, up.</param>
/// <param name="Z">The middle of the feet, south.</param>
/// <param name="YawRadians">The facing (0 north, a quarter turn east).</param>
/// <param name="PitchRadians">The look up (positive) or down.</param>
/// <param name="WalkPhase">The stride, which grows with the distance walked.</param>
/// <param name="Moving">Whether the player is walking.</param>
public readonly record struct PlayerAvatar(float X, float Y, float Z, float YawRadians, float PitchRadians, float WalkPhase, bool Moving);

/// <summary>What the first-person arm is doing and holding this frame (S15).</summary>
/// <param name="Held">The model of the selected item.</param>
/// <param name="Motion">The sway and the swing.</param>
/// <param name="Light">The scene's light on the arm (<see cref="ArmLight"/>).</param>
public readonly record struct ArmFrame(HeldChoice Held, ArmMotion Motion, (float R, float G, float B) Light);
