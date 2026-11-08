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
using Vocaterra.Game.Graphics.Figures;
using Vocaterra.Game.Graphics.Sky;
using Vocaterra.Game.Graphics.Water;

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
/// Draws the voxel world with MonoGame's own effects (no custom shader: mgfxc needs Wine on Linux): <see cref="AlphaTestEffect"/> for the opaque faces and
/// the undergrowth (a texture times the vertex colour, the light and the ambient occlusion being in the mesh, and the cut-out pixels of the leaves and the
/// plants dropped by the alpha test), <see cref="BasicEffect"/> for the water and the small overlays. The sky comes first (<see cref="SkyRenderer"/>), then
/// with the depth buffer on: the opaque faces of every visible chunk, the undergrowth of the chunks near the eye without culling, the figures and the
/// highlight, then the translucent water chunk by chunk from the farthest to the nearest without writing depth, then the beacon. At the scale of the 384 x 384 x 128 world
/// (S13) only the chunks within the preset's view distance of the eye are meshed and drawn (<see cref="ChunkViewSet"/>): chunks of air and chunks
/// buried in solid blocks are skipped, the rest are built nearest first within the frame's time budget, and a chunk that goes out of range lets its
/// mesh go. The chunks that are drawn are culled against the camera's frustum, and the fog closes in at the view distance, in the sky colour. A chunk
/// is meshed again when its version, or a neighbour's, changed, or when a block kind it holds changed its grey state (<see cref="ChunkVersionTracker"/>).
/// </summary>
public sealed class VoxelRenderer : IDisposable
{
    /// <summary>The colour the target is cleared to and the colour of the fog: the haze at the horizon (<see cref="SkyPalette.Horizon"/>), so far land fades into the sky.</summary>
    public static readonly Color Sky = new(SkyPalette.Horizon.R, SkyPalette.Horizon.G, SkyPalette.Horizon.B);

    /// <summary>The colour of the evening haze (S15, <c>--dusk</c>).</summary>
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
    private const int MaxQuadsPerMesh = 16383;

    private readonly GraphicsDevice _device;
    private readonly BlockAtlas _atlas;
    private readonly WaterTextures _water;
    private readonly VoxelGrid _grid;
    private readonly BasicEffect _effect;
    private readonly AlphaTestEffect _solid;
    private readonly SkyRenderer _sky;
    private MeshOptions _meshOptions;
    private readonly GridBlockSource _source;
    private readonly ChunkVersionTracker _tracker;
    private ChunkViewSet _view;
    private readonly IndexBuffer _indices;
    private readonly ChunkGpu[] _chunks;
    private readonly short[] _scratchIndices = new short[4096 * 6];
    private VertexPositionColorTexture[] _scratchVertices = new VertexPositionColorTexture[4096];
    private readonly List<ChunkGpu> _visible = [];
    private IReadOnlyList<NpcFigure> _figures = [];
    private readonly MeshBuffer _figureBuffer = new();
    private bool _dusk;
    private readonly HashSet<int> _resident = [];
    private IReadOnlyList<int> _wanted = [];
    private long _checkedEdits = -1;
    private int _meshed;
    private double _totalMilliseconds;
    private double _longestMilliseconds;
    private int _drawnChunks;
    private int _drawnPlantQuads;
    private int _buriedSkipped;
    private int _released;
    private int _pending;

    /// <summary>Creates the renderer for a grid and a quality preset. Nothing is meshed yet: the first <see cref="Remesh"/> builds what the eye can see.</summary>
    public VoxelRenderer(GraphicsDevice device, BlockAtlas atlas, WaterTextures water, VoxelGrid grid, QualityPreset preset, bool dusk = false, RiverCurrents? currents = null)
    {
        ArgumentNullException.ThrowIfNull(preset);
        _device = device;
        _atlas = atlas;
        _water = water;
        _grid = grid;
        Preset = preset;
        _dusk = dusk;
        ClearColour = dusk ? DuskSky : Sky;
        var fogEnd0 = FogEndFor(preset.ViewDistance);
        DryFog = new FogParameters(ClearColour.R / 255f, ClearColour.G / 255f, ClearColour.B / 255f, fogEnd0 * FogStartFraction, fogEnd0);
        _source = new GridBlockSource(grid, currents);
        _tracker = new ChunkVersionTracker(grid);
        _view = new ChunkViewSet(grid, preset.ViewDistance);
        var fogEnd = FogEndFor(preset.ViewDistance);
        _effect = new BasicEffect(device)
        {
            TextureEnabled = true,
            VertexColorEnabled = true,
            LightingEnabled = false,
            FogEnabled = true,
            FogColor = ClearColour.ToVector3(),
            FogStart = fogEnd * FogStartFraction,
            FogEnd = fogEnd,
            Texture = atlas.Texture,
        };
        _solid = new AlphaTestEffect(device)
        {
            VertexColorEnabled = true,
            FogEnabled = true,
            FogColor = ClearColour.ToVector3(),
            FogStart = fogEnd * FogStartFraction,
            FogEnd = fogEnd,
            Texture = atlas.Texture,
            AlphaFunction = CompareFunction.Greater,
            ReferenceAlpha = 110,
        };
        _sky = new SkyRenderer(device, dusk);
        Lights = FindLights(grid);
        _meshOptions = new MeshOptions(preset.AmbientOcclusion, preset.PlantKeepPercent, Lights, dusk);
        _indices = BuildIndexBuffer(device, MaxQuadsPerMesh);
        _chunks = new ChunkGpu[grid.Chunks.Count];
        for (var i = 0; i < _chunks.Length; i++)
        {
            var c = grid.Chunks[i].Coord;
            var origin = new Vector3(c.X * VoxelChunk.Size, c.Y * VoxelChunk.Size, c.Z * VoxelChunk.Size);
            _chunks[i] = new ChunkGpu(new BoundingBox(origin, origin + new Vector3(VoxelChunk.Size)));
        }

        for (var i = 0; i < _scratchIndices.Length / 6; i++)
        {
            WriteQuadIndices(_scratchIndices, i);
        }
    }

    /// <summary>The lanterns and post torches of the world (S15): the places the glow comes from (found once, at the start; none is ever gathered or placed).</summary>
    public IReadOnlyList<BlockPos> Lights { get; }

    /// <summary>The positions of every lantern and post torch of a grid, in a fixed order (chunk by chunk): only the chunks that hold one are looked into.</summary>
    public static IReadOnlyList<BlockPos> FindLights(VoxelGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var found = new List<BlockPos>();
        var bit = ChunkMesher.KindBit(BlockId.Lantern) | ChunkMesher.KindBit(BlockId.PostTorch);
        foreach (var chunk in grid.Chunks)
        {
            if ((chunk.KindMask & bit) == 0)
            {
                continue;
            }

            var x0 = chunk.Coord.X * VoxelChunk.Size;
            var y0 = chunk.Coord.Y * VoxelChunk.Size;
            var z0 = chunk.Coord.Z * VoxelChunk.Size;
            for (var y = y0; y < y0 + VoxelChunk.Size; y++)
            {
                for (var z = z0; z < z0 + VoxelChunk.Size; z++)
                {
                    for (var x = x0; x < x0 + VoxelChunk.Size; x++)
                    {
                        if (grid.GetBlock(x, y, z) is BlockId.Lantern or BlockId.PostTorch)
                        {
                            found.Add(new BlockPos(x, y, z));
                        }
                    }
                }
            }
        }

        return found;
    }

    /// <summary>The colour the target is cleared to and the fog's colour: the haze of the day, or of the evening with <c>--dusk</c>; the water's while the view is under water.</summary>
    public Color ClearColour { get; private set; }

    /// <summary>The fog above water (the haze's colour, start and end for the preset): what <see cref="SetUnderwater"/> blends from.</summary>
    public FogParameters DryFog { get; private set; }

    private float _underwaterAmount;

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
        var fog = UnderwaterFog.Blend(DryFog, amount);
        var colour = new Vector3(fog.Red, fog.Green, fog.Blue);
        ClearColour = amount <= 0f ? (_dusk ? DuskSky : Sky) : new Color(colour);
        ApplyFog(_effect, colour, fog);
        ApplyFog(_solid, colour, fog);
        _sky.SetHaze(ClearColour, amount);
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
    /// Changes the quality preset while the game runs (the menu): the view distance, the fog, the undergrowth and the corner shading follow. Every mesh is let go, so the
    /// next <see cref="Remesh"/> builds the view again with the new options (nearest first); the sky takes the cloud setting at its next draw.
    /// </summary>
    /// <param name="preset">The new preset.</param>
    public void SetPreset(QualityPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (preset == Preset)
        {
            return;
        }

        Preset = preset;
        var fogEnd = FogEndFor(preset.ViewDistance);
        DryFog = new FogParameters(DryFog.Red, DryFog.Green, DryFog.Blue, fogEnd * FogStartFraction, fogEnd);
        _view = new ChunkViewSet(_grid, preset.ViewDistance);
        _meshOptions = new MeshOptions(preset.AmbientOcclusion, preset.PlantKeepPercent, Lights, _dusk);
        foreach (var index in _resident.ToList())
        {
            _chunks[index].Release();
        }

        _resident.Clear();
        foreach (var chunk in _grid.Chunks)
        {
            _tracker.Discard(chunk.Coord);
        }

        _wanted = [];
        _checkedEdits = -1;
        _underwaterAmount = float.NaN; // the next SetUnderwater applies the fog of the new distance
    }

    /// <summary>Where the fog is complete for a view distance: a little inside it, so the last chunks fade out before they are cut off.</summary>
    public static float FogEndFor(int viewDistance) => viewDistance * 0.94f;

    /// <summary>The far plane of the camera for a view distance: past the fog's end and the last chunk's corner.</summary>
    public static float FarPlaneFor(int viewDistance) => viewDistance + (2f * VoxelChunk.Size);

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
            foreach (var index in _resident)
            {
                var chunk = _chunks[index];
                vertices += (chunk.OpaqueQuads + chunk.AlphaQuads + chunk.PlantQuads) * 4;
                quads += chunk.OpaqueQuads + chunk.AlphaQuads + chunk.PlantQuads;
            }

            return new MeshStats(
                _chunks.Length,
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

    /// <summary>
    /// Sets the block kinds drawn grey (the greyscale A/B, <see cref="GreyscaleWorld"/>): the chunks that hold a kind whose grey state changed are
    /// meshed again by the next <see cref="Remesh"/>; the other chunks keep their meshes.
    /// </summary>
    /// <param name="greyKinds">One bit per block kind (<see cref="ChunkMesher.KindBit"/>); 0 draws the whole world in colour.</param>
    public void SetGreyKinds(ulong greyKinds)
    {
        if (_tracker.SetGreyKinds(greyKinds) > 0)
        {
            _checkedEdits = -1;
        }
    }

    /// <summary>
    /// Builds the meshes the eye needs: the chunks in view whose mesh is missing or out of date, in-frustum ones first and nearest first, until the
    /// time budget is spent (always at least one, so the view fills even on a slow frame); and lets go of the meshes of the chunks that went out of
    /// range. Returns how many meshes it built.
    /// </summary>
    /// <param name="eye">The eye in blocks.</param>
    /// <param name="frustum">The camera's frustum, to build what is on screen first, or <see langword="null"/>.</param>
    /// <param name="budgetMilliseconds">The most time to spend, or <see langword="null"/> to build everything that is due (a scripted run before its screenshot).</param>
    public int Remesh(Vector3 eye, BoundingFrustum? frustum = null, double? budgetMilliseconds = null)
    {
        var wanted = _view.Update(eye.X, eye.Y, eye.Z);
        var started = MonotonicClock.Milliseconds();
        if (!ReferenceEquals(wanted, _wanted) || _checkedEdits != _grid.EditCount)
        {
            _wanted = wanted;
            ReleaseOutOfRange(eye);
        }

        // Which chunks in view are due: never meshed, or edited (any edit since the last full check makes every meshed chunk's key worth comparing).
        var recheck = _checkedEdits != _grid.EditCount;
        var due = new List<int>();
        var offscreen = new List<int>();
        foreach (var index in wanted)
        {
            var chunk = _grid.Chunks[index];
            var coord = chunk.Coord;
            var isDue = !_tracker.HasMesh(coord) || (recheck && _tracker.IsDue(coord));
            if (!isDue)
            {
                continue;
            }

            if (frustum is null || frustum.Intersects(_chunks[index].Box))
            {
                due.Add(index);
            }
            else
            {
                offscreen.Add(index);
            }
        }

        _checkedEdits = _grid.EditCount;
        due.AddRange(offscreen);
        var built = 0;
        foreach (var index in due)
        {
            if (budgetMilliseconds is { } budget && built > 0 && MonotonicClock.Milliseconds() - started >= budget)
            {
                break;
            }

            BuildChunk(index);
            built++;
        }

        _pending = due.Count - built;
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
    /// <param name="overlay">Drawn after the figures, the water and the beacon and before the arm clears the depth buffer, so it can test against the scene (the NPCs' name plates, V22), or <see langword="null"/>.</param>
    public void DrawWorld(FirstPersonCamera camera, BlockPos? highlight, BlockPos? beacon, long tick, PlayerAvatar? avatar = null, ArmFrame? arm = null, Action? overlay = null)
    {
        _sky.Draw(camera, tick, Preset.Clouds);

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
        foreach (var index in _wanted)
        {
            var chunk = _chunks[index];
            if (chunk.Visible(camera.Frustum))
            {
                _visible.Add(chunk);
            }
        }

        _drawnChunks = _visible.Count;
        _device.Indices = _indices;
        _solid.CurrentTechnique.Passes[0].Apply();
        foreach (var chunk in _visible)
        {
            DrawChunk(chunk.Opaque, chunk.OpaqueQuads);
        }

        // The undergrowth: crossed quads with both sides showing, only in the chunks near the eye.
        var eye = camera.Eye;
        var plantReach = Preset.PlantDistance + VoxelChunk.Size;
        var plantReachSquared = (float)plantReach * plantReach;
        _device.RasterizerState = RasterizerState.CullNone;
        _drawnPlantQuads = 0;
        foreach (var chunk in _visible)
        {
            if (chunk.PlantQuads > 0 && Vector3.DistanceSquared(chunk.Centre, eye) <= plantReachSquared)
            {
                DrawChunk(chunk.Plants, chunk.PlantQuads);
                _drawnPlantQuads += chunk.PlantQuads;
            }
        }

        _device.RasterizerState = RasterizerState.CullClockwise;
        _effect.CurrentTechnique.Passes[0].Apply();
        DrawnFigureQuads = 0;
        foreach (var figure in _figures)
        {
            var (fx, fy, fz) = figure.FeetPosition;
            _figureBuffer.Clear();
            var warm = ArmLight.Warmth(Lights, fx, fy + 1f, fz, _dusk);
            FigureMesher.Append(_figureBuffer, NpcFigures.PartsOf(figure.Look), FigureAnimation.For(figure, tick), fx, fy, fz, figure.FacingRadians, FigureLight(warm));
            DrawnFigureQuads += _figureBuffer.FaceCount;
            DrawDynamic(_figureBuffer);
        }

        if (avatar is { } player)
        {
            _figureBuffer.Clear();
            var warm = ArmLight.Warmth(Lights, player.X, player.Y + 1f, player.Z, _dusk);
            var pose = player.Moving ? FigureAnimation.Walking(player.WalkPhase) : FigureAnimation.Idle(tick, 3);
            pose = pose with { HeadPitch = Math.Clamp(-player.PitchRadians, -0.6f, 0.6f) };
            FigureMesher.Append(_figureBuffer, NpcFigures.PartsOf(NpcFigures.PlayerName), pose, player.X, player.Y, player.Z, player.YawRadians, FigureLight(warm));
            DrawnFigureQuads += _figureBuffer.FaceCount;
            DrawDynamic(_figureBuffer);
        }

        if (highlight is { } block)
        {
            DrawDynamic(OverlayMeshes.Highlight(block));
        }

        // The translucent pass: the water chunks from the farthest to the nearest (a sort by chunk, as the files say), reading depth but not writing it.
        // The water has its own animated textures (H09): the frame for this tick, bilinear, in place of the block atlas for this pass only.
        _device.BlendState = BlendState.NonPremultiplied;
        _device.DepthStencilState = DepthStencilState.DepthRead;
        _device.SamplerStates[0] = WaterTextures.Sampler;
        _effect.Texture = _water.FrameAt(tick);
        _effect.CurrentTechnique.Passes[0].Apply();
        _visible.Sort((a, b) => Vector3.DistanceSquared(b.Centre, eye).CompareTo(Vector3.DistanceSquared(a.Centre, eye)));
        _device.Indices = _indices;
        foreach (var chunk in _visible)
        {
            DrawChunk(chunk.Alpha, chunk.AlphaQuads);
        }

        _device.SamplerStates[0] = AtlasSampler;
        _effect.Texture = _atlas.Texture;

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
        foreach (var chunk in _chunks)
        {
            chunk.Opaque?.Dispose();
            chunk.Alpha?.Dispose();
            chunk.Plants?.Dispose();
        }

        _indices.Dispose();
        _effect.Dispose();
        _solid.Dispose();
        _sky.Dispose();
    }

    private void BuildChunk(int index)
    {
        var started = MonotonicClock.Milliseconds();
        var chunk = _grid.Chunks[index];
        var coord = chunk.Coord;
        var gpu = _chunks[index];
        gpu.Release();
        if (ChunkMesher.IsBuried(_source, chunk))
        {
            _buriedSkipped++;
            _tracker.MarkMeshed(coord, chunk.KindMask);
            _resident.Remove(index);
            return;
        }

        var meshes = ChunkMesher.Build(_source, coord.X, coord.Y, coord.Z, _tracker.GreyKinds, _meshOptions);
        (gpu.Opaque, gpu.OpaqueQuads) = Upload(meshes.Opaque);
        (gpu.Alpha, gpu.AlphaQuads) = Upload(meshes.Alpha);
        (gpu.Plants, gpu.PlantQuads) = Upload(meshes.PlantMesh);
        _tracker.MarkMeshed(coord, meshes.Kinds);
        if (gpu.Opaque is not null || gpu.Alpha is not null || gpu.Plants is not null)
        {
            _resident.Add(index);
        }
        else
        {
            _resident.Remove(index);
        }

        var spent = MonotonicClock.Milliseconds() - started;
        _meshed++;
        _totalMilliseconds += spent;
        _longestMilliseconds = Math.Max(_longestMilliseconds, spent);
    }

    // A chunk that has a mesh but is beyond the keep distance of the eye lets it go (and is due again if the eye comes back).
    private void ReleaseOutOfRange(Vector3 eye)
    {
        var keep = (long)_view.KeepDistance * _view.KeepDistance;
        List<int>? let = null;
        foreach (var index in _resident)
        {
            var c = _grid.Chunks[index].Coord;
            if (ChunkViewSet.DistanceSquared(eye.X, eye.Y, eye.Z, c.X, c.Y, c.Z) > keep)
            {
                (let ??= []).Add(index);
            }
        }

        if (let is null)
        {
            return;
        }

        foreach (var index in let)
        {
            var gpu = _chunks[index];
            gpu.Release();
            _tracker.Discard(_grid.Chunks[index].Coord);
            _resident.Remove(index);
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

    private (VertexBuffer? Buffer, int Quads) Upload(MeshBuffer mesh)
    {
        var quads = Math.Min(mesh.FaceCount, MaxQuadsPerMesh);
        if (quads == 0)
        {
            return (null, 0);
        }

        var buffer = new VertexBuffer(_device, VertexPositionColorTexture.VertexDeclaration, quads * 4, BufferUsage.WriteOnly);
        buffer.SetData(mesh.ToArray(), 0, quads * 4);
        return (buffer, quads);
    }

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

    private sealed class ChunkGpu(BoundingBox box)
    {
        public VertexBuffer? Opaque;
        public int OpaqueQuads;
        public VertexBuffer? Alpha;
        public int AlphaQuads;
        public VertexBuffer? Plants;
        public int PlantQuads;
        public BoundingBox Box { get; } = box;
        public Vector3 Centre { get; } = (box.Min + box.Max) / 2f;

        public bool Visible(BoundingFrustum frustum) => (Opaque is not null || Alpha is not null || Plants is not null) && frustum.Intersects(Box);

        public void Release()
        {
            Opaque?.Dispose();
            Alpha?.Dispose();
            Plants?.Dispose();
            Opaque = null;
            Alpha = null;
            Plants = null;
            OpaqueQuads = 0;
            AlphaQuads = 0;
            PlantQuads = 0;
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
