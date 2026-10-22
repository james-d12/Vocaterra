using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame;

/// <summary>What building the chunk meshes cost and produced, for the run's report.</summary>
/// <param name="Chunks">The chunks in the grid.</param>
/// <param name="Meshed">How many chunk meshes were built (a chunk is built again after an edit).</param>
/// <param name="Vertices">The vertices now on the graphics card.</param>
/// <param name="Quads">The quads (faces) now on the graphics card.</param>
/// <param name="TotalMilliseconds">The time spent meshing and uploading, all builds together.</param>
/// <param name="LongestMilliseconds">The slowest single chunk build.</param>
public readonly record struct MeshStats(int Chunks, int Meshed, int Vertices, int Quads, double TotalMilliseconds, double LongestMilliseconds)
{
    /// <summary>The average build time of a chunk in milliseconds.</summary>
    public double MeanMilliseconds => Meshed == 0 ? 0 : TotalMilliseconds / Meshed;

    /// <summary>A one-line summary.</summary>
    public string Summary() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Chunks} chunks, {Meshed} meshes built in {TotalMilliseconds:0.0} ms (mean {MeanMilliseconds:0.00} ms, longest {LongestMilliseconds:0.00} ms), {Quads} faces, {Vertices} vertices");
}

/// <summary>
/// Draws the voxel world with MonoGame's <see cref="BasicEffect"/> (a texture times the vertex colour, so the face shade is in the mesh), with the
/// depth buffer on: the opaque faces of every visible chunk first, then the figures and the highlight, then the translucent water chunk by chunk
/// from the farthest to the nearest without writing depth, then the beacon. There is no custom shader. Distance fog in the sky colour hides where
/// the 96-block world ends. A chunk is meshed again when its version, or a neighbour's, changed, or when a block kind it holds changed its grey state
/// (<see cref="ChunkVersionTracker"/>).
/// </summary>
public sealed class VoxelRenderer : IDisposable
{
    /// <summary>The sky colour behind the world and the colour of the fog.</summary>
    public static readonly Color Sky = new(135, 190, 235);

    /// <summary>Where the fog starts, in blocks from the eye.</summary>
    public const float FogStart = 48f;

    /// <summary>Where the fog is complete, in blocks from the eye.</summary>
    public const float FogEnd = 110f;

    // A chunk mesh is drawn with 16-bit indices, so it holds at most 65 532 vertices (16 383 quads). A full chunk of cubes tops out at 12 288 quads.
    private const int MaxQuadsPerMesh = 16383;

    private readonly GraphicsDevice _device;
    private readonly BlockAtlas _atlas;
    private readonly VoxelGrid _grid;
    private readonly BasicEffect _effect;
    private readonly GridBlockSource _source;
    private readonly ChunkVersionTracker _tracker;
    private readonly IndexBuffer _indices;
    private readonly ChunkGpu[] _chunks;
    private readonly short[] _scratchIndices = new short[4096 * 6];
    private VertexPositionColorTexture[] _scratchVertices = new VertexPositionColorTexture[4096];
    private readonly List<ChunkGpu> _visible = [];
    private readonly List<MeshBuffer> _figureMeshes = [];
    private int _meshed;
    private double _totalMilliseconds;
    private double _longestMilliseconds;
    private int _drawnChunks;

    /// <summary>Creates the renderer and meshes the whole grid once.</summary>
    public VoxelRenderer(GraphicsDevice device, BlockAtlas atlas, VoxelGrid grid)
    {
        _device = device;
        _atlas = atlas;
        _grid = grid;
        _source = new GridBlockSource(grid);
        _tracker = new ChunkVersionTracker(grid);
        _effect = new BasicEffect(device)
        {
            TextureEnabled = true,
            VertexColorEnabled = true,
            LightingEnabled = false,
            FogEnabled = true,
            FogColor = Sky.ToVector3(),
            FogStart = FogStart,
            FogEnd = FogEnd,
            Texture = atlas.Texture,
        };
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

        Remesh();
    }

    /// <summary>Sets the NPC figures that are drawn with the world; their meshes are built once here.</summary>
    public void SetFigures(IReadOnlyList<NpcFigure> figures)
    {
        ArgumentNullException.ThrowIfNull(figures);
        _figureMeshes.Clear();
        foreach (var figure in figures)
        {
            _figureMeshes.Add(NpcFigures.Build(figure));
        }
    }

    /// <summary>What the mesh builds have cost so far.</summary>
    public MeshStats Stats
    {
        get
        {
            var vertices = 0;
            var quads = 0;
            foreach (var chunk in _chunks)
            {
                vertices += chunk.OpaqueQuads * 4;
                vertices += chunk.AlphaQuads * 4;
                quads += chunk.OpaqueQuads + chunk.AlphaQuads;
            }

            return new MeshStats(_chunks.Length, _meshed, vertices, quads, _totalMilliseconds, _longestMilliseconds);
        }
    }

    /// <summary>How many chunks the last <see cref="DrawWorld"/> drew (the rest were outside the view or empty).</summary>
    public int DrawnChunks => _drawnChunks;

    /// <summary>
    /// Sets the block kinds drawn grey (the greyscale A/B, <see cref="GreyscaleWorld"/>): the chunks that hold a kind whose grey state changed are
    /// meshed again by the next <see cref="Remesh"/>; the other chunks keep their meshes.
    /// </summary>
    /// <param name="greyKinds">One bit per block kind (<see cref="ChunkMesher.KindBit"/>); 0 draws the whole world in colour.</param>
    public void SetGreyKinds(ulong greyKinds) => _tracker.SetGreyKinds(greyKinds);

    /// <summary>Meshes the chunks whose blocks (or neighbours') changed since they were last meshed. Returns how many it rebuilt.</summary>
    public int Remesh()
    {
        var due = _tracker.Due();
        foreach (var coord in due)
        {
            var started = MonotonicClock.Milliseconds();
            var meshes = ChunkMesher.Build(_source, coord.X, coord.Y, coord.Z, _tracker.GreyKinds);
            var chunk = _chunks[_tracker.IndexOf(coord)];
            chunk.Opaque?.Dispose();
            chunk.Alpha?.Dispose();
            (chunk.Opaque, chunk.OpaqueQuads) = Upload(meshes.Opaque);
            (chunk.Alpha, chunk.AlphaQuads) = Upload(meshes.Alpha);
            _tracker.MarkMeshed(coord, meshes.Kinds);
            var spent = MonotonicClock.Milliseconds() - started;
            _meshed++;
            _totalMilliseconds += spent;
            _longestMilliseconds = Math.Max(_longestMilliseconds, spent);
        }

        return due.Count;
    }

    /// <summary>
    /// Draws the world from the camera: opaque chunks, the NPC figures and the block highlight, then the water and the beacon column. The caller
    /// clears the target first and draws the layers afterwards.
    /// </summary>
    /// <param name="camera">The camera for this frame.</param>
    /// <param name="highlight">The block to frame, or <see langword="null"/>.</param>
    /// <param name="beacon">The block the beacon column stands on, or <see langword="null"/>.</param>
    /// <param name="tick">The simulation tick (animates the beacon).</param>
    public void DrawWorld(FirstPersonCamera camera, BlockPos? highlight, BlockPos? beacon, long tick)
    {
        _device.DepthStencilState = DepthStencilState.Default;
        _device.RasterizerState = RasterizerState.CullClockwise; // triangles run counter-clockwise seen from outside
        _device.BlendState = BlendState.Opaque;
        _device.SamplerStates[0] = SamplerState.PointClamp;
        _effect.World = Matrix.Identity;
        _effect.View = camera.View;
        _effect.Projection = camera.Projection;
        _effect.Texture = _atlas.Texture;

        _visible.Clear();
        foreach (var chunk in _chunks)
        {
            if (chunk.Visible(camera.Frustum))
            {
                _visible.Add(chunk);
            }
        }

        _drawnChunks = _visible.Count;
        _device.Indices = _indices;
        _effect.CurrentTechnique.Passes[0].Apply();
        foreach (var chunk in _visible)
        {
            DrawChunk(chunk.Opaque, chunk.OpaqueQuads);
        }

        foreach (var figure in _figureMeshes)
        {
            DrawDynamic(figure);
        }

        if (highlight is { } block)
        {
            DrawDynamic(OverlayMeshes.Highlight(block));
        }

        // The translucent pass: the water chunks from the farthest to the nearest (a sort by chunk, as the files say), reading depth but not writing it.
        _device.BlendState = BlendState.NonPremultiplied;
        _device.DepthStencilState = DepthStencilState.DepthRead;
        _effect.CurrentTechnique.Passes[0].Apply();
        var eye = camera.Eye;
        _visible.Sort((a, b) => Vector3.DistanceSquared(b.Centre, eye).CompareTo(Vector3.DistanceSquared(a.Centre, eye)));
        _device.Indices = _indices;
        foreach (var chunk in _visible)
        {
            DrawChunk(chunk.Alpha, chunk.AlphaQuads);
        }

        if (beacon is { } target)
        {
            DrawDynamic(OverlayMeshes.Beacon(target, tick));
        }

        _device.DepthStencilState = DepthStencilState.None;
        _device.BlendState = BlendState.Opaque;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var chunk in _chunks)
        {
            chunk.Opaque?.Dispose();
            chunk.Alpha?.Dispose();
        }

        _indices.Dispose();
        _effect.Dispose();
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
        public Vector3 Centre { get; } = (box.Min + box.Max) / 2f;

        public bool Visible(BoundingFrustum frustum) => (Opaque is not null || Alpha is not null) && frustum.Intersects(box);
    }
}
