using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.World.Regions;

/// <summary>
/// A region on the procedural ground (B15, biomes plan section 3.6): a layout's sites, props and paths built at a place of the world, on the land the world has there. Where the valley draws its
/// own relief (<see cref="AuthoredReliefRegion"/>), a town takes its natural ground from the procedural height at its position (<see cref="ProceduralGround"/>) and levels it only where its sites
/// and paths need it, so beyond them the region's ground is the world's own and its border meets the land outside with no step beyond the blend's bound. The size comes from the layout (a town of 96 x 96
/// blocks is 6 x 6 chunk columns), the place from a <see cref="RegionPlacement"/>, and nothing about either is a constant: another town is another layout and another placement. Every point of it knows its
/// origin: <see cref="Points"/> list each site, point and named prop in the layout's coordinates and in the world's.
/// <para>
/// It is built once by the scene builder, then frozen like the valley (immutable chunks, an edge profile, a template digest), so <see cref="RegionCatalog"/>, <see cref="RegionBlend"/> and
/// <see cref="CompositeChunkSource"/> take it as they take the valley. Its ground is the world's, so it goes on dry land (no column below the sea level) and its layout has no river, lake or
/// waterfall (lists of water in a layout are B16's). The layout's heights are its own: its y 0 is the floor of the placement's first layer, and its rock and snow lines and its ceiling are
/// read in those coordinates.
/// </para>
/// </summary>
public sealed class ProceduralReliefRegion : IRegion
{
    /// <summary>The stone kept under the lowest ground of a fitted region: <see cref="Fit"/> puts the region's floor at most this far below it (the terrain keeps 4 blocks of stone under any ground, and its soil is 3 thick).</summary>
    public const int Footing = 8;

    private readonly AuthoredReliefRegion _frozen;
    private readonly RegionPoint[] _points;

    private ProceduralReliefRegion(AuthoredReliefRegion frozen, SceneLayout layout, RegionPlacement placement, RegionPoint[] points, ulong seed)
    {
        Seed = seed;
        _frozen = frozen;
        Layout = layout;
        Placement = placement;
        _points = points;
    }

    /// <inheritdoc />
    public string Id => _frozen.Id;

    /// <inheritdoc />
    public RegionBounds Bounds => _frozen.Bounds;

    /// <summary>The world seed the region was built for: its ground is that seed's, so it belongs in a world of the same seed only.</summary>
    public ulong Seed { get; }

    /// <summary>The layout the region was built from.</summary>
    public SceneLayout Layout { get; }

    /// <summary>Where the layout stands in the world.</summary>
    public RegionPlacement Placement { get; }

    /// <summary>The digest of the template (<see cref="WorldStateHeader.TemplateDigest"/>), 32 bytes: the layout as built at this place on this ground.</summary>
    public byte[] TemplateDigest => _frozen.TemplateDigest;

    /// <summary>Where the rarer kinds of block stood when the region was made (the fire rings' stones, the roads), in world coordinates.</summary>
    public TemplateBlockIndex Blocks => _frozen.Blocks;

    /// <summary>Every site, point and named prop of the layout, ordered by name (ordinal), each with its block in the layout's coordinates and in the world's.</summary>
    public IReadOnlyList<RegionPoint> Points => _points;

    /// <summary>The world position of the layout's origin.</summary>
    public BlockPos Origin => Placement.Origin;

    /// <summary>The place called <paramref name="name"/>.</summary>
    /// <param name="name">A site, point or named prop of the layout.</param>
    /// <exception cref="KeyNotFoundException">The layout has no such place.</exception>
    public RegionPoint Point(string name)
    {
        foreach (var point in _points)
        {
            if (string.Equals(point.Name, name, StringComparison.Ordinal))
            {
                return point;
            }
        }

        throw new KeyNotFoundException($"The region '{Id}' has no place '{name}'.");
    }

    /// <summary>
    /// The placement whose first layer puts the region's floor under the lowest ground of its footprint (at most <see cref="Footing"/> blocks below it, and at least 8 above the floor), for a layout
    /// at the chunk column and row given: the way to place a town on the land without choosing a layer by hand.
    /// </summary>
    /// <param name="layout">The town's layout.</param>
    /// <param name="chunkX">The chunk column of the layout's column 0.</param>
    /// <param name="chunkZ">The chunk row of the layout's row 0.</param>
    /// <param name="data">The world's numbers.</param>
    /// <param name="seed">The world seed.</param>
    public static RegionPlacement Fit(SceneLayout layout, int chunkX, int chunkZ, WorldGenData data, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(data);
        var bounds = new RegionBounds(chunkX, 0, chunkZ, layout.SizeX >> VoxelChunk.Shift, layout.SizeY >> VoxelChunk.Shift, layout.SizeZ >> VoxelChunk.Shift);
        var lowest = FootprintGround.Over(data, seed, bounds).Lowest;
        return new RegionPlacement(chunkX, (lowest - Footing) >> VoxelChunk.Shift, chunkZ);
    }

    /// <summary>Builds the region of a layout at a place: the layout's scene on the procedural ground there, frozen.</summary>
    /// <param name="id">The region's name.</param>
    /// <param name="layout">The town's layout (no river, lake or waterfall; it need not have a relief).</param>
    /// <param name="placement">Where the layout stands.</param>
    /// <param name="data">The world's numbers.</param>
    /// <param name="seed">The world seed (the layout's trees and plants are drawn from it by place in the layout, so the same town is the same at any placement).</param>
    /// <exception cref="ArgumentException">The layout holds water, the ground at the place is under the sea or does not fit the layout's height, or a prop does not fit its ground.</exception>
    public static ProceduralReliefRegion Create(string id, SceneLayout layout, RegionPlacement placement, WorldGenData data, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(data);
        var bounds = new RegionBounds(placement.ChunkX, placement.ChunkY, placement.ChunkZ, layout.SizeX >> VoxelChunk.Shift, layout.SizeY >> VoxelChunk.Shift, layout.SizeZ >> VoxelChunk.Shift);
        return Create(id, layout, placement, FootprintGround.Over(data, seed, bounds), seed, data.Vertical.SeaLevel, data.Blend.Smoothing.BlurRadius);
    }

    /// <summary>Builds the region on the ground a test supplies.</summary>
    internal static ProceduralReliefRegion Create(string id, SceneLayout layout, RegionPlacement placement, IGroundHeights ground, ulong seed, int seaLevel, int edgeBlurRadius)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(ground);
        if (layout.River is not null || layout.Lake is not null || layout.Waterfall is not null)
        {
            throw new ArgumentException($"The layout '{layout.Name}' holds a river, a lake or a waterfall, which a region on procedural ground cannot have: its water is the world's.", nameof(layout));
        }

        if (layout.WaterLevel > 4)
        {
            // The terrain fills any ground under the layout's water level, and a town's ground is never under 4 (checked below): a water level above it would flood the town.
            throw new ArgumentException($"The layout '{layout.Name}' has a water level of {layout.WaterLevel}, which is not under its ground: set it to 1.", nameof(layout));
        }

        var spec = layout.Terrain;
        for (var z = 0; z < layout.SizeZ; z++)
        {
            for (var x = 0; x < layout.SizeX; x++)
            {
                var world = ground.HeightAt(placement.OriginX + x, placement.OriginZ + z);
                var local = world - placement.OriginY;
                if (world < seaLevel)
                {
                    throw new ArgumentException($"The ground at ({placement.OriginX + x}, {placement.OriginZ + z}) is {world}, under the sea level {seaLevel}: a town goes on dry land.", nameof(placement));
                }

                if (local < 4 || local > spec.Ceiling)
                {
                    throw new ArgumentException($"The ground at ({placement.OriginX + x}, {placement.OriginZ + z}) is {world}, which is {local} in the layout's own height: it must lie between 4 and the layout's ceiling {spec.Ceiling} (see Fit, or make the layout taller).", nameof(placement));
                }
            }
        }

        var shape = new ProceduralGround(ground, placement.OriginX, placement.OriginY, placement.OriginZ);
        var scene = SceneBuilder.Build(layout, seed, shape);
        var frozen = AuthoredReliefRegion.Freeze(id, scene, placement.ChunkX, placement.ChunkZ, keepChunks: true, edgeBlurRadius: edgeBlurRadius, originChunkY: placement.ChunkY);
        var points = scene.Points.Select(p => new RegionPoint(p.Key, p.Value, placement.ToWorld(p.Value))).OrderBy(static p => p.Name, StringComparer.Ordinal).ToArray();
        return new ProceduralReliefRegion(frozen, layout, placement, points, seed);
    }

    /// <inheritdoc />
    public bool Covers(VoxelChunkCoord chunk) => _frozen.Covers(chunk);

    /// <inheritdoc />
    public ChunkData Copy(VoxelChunkCoord chunk) => _frozen.Copy(chunk);

    /// <inheritdoc />
    public int EdgeHeight(int x, int z) => _frozen.EdgeHeight(x, z);

    /// <inheritdoc />
    public int SmoothedEdgeHeight(int x, int z) => _frozen.SmoothedEdgeHeight(x, z);

    /// <summary>
    /// The procedural world's ground over a footprint, read once: the height model over the climate with the land pulled towards it, which is what the world's own ground is inside a region (the
    /// region's own nearest-region distance there is 0, so no other region's bounds change it). The blend's edge easing is not part of it: that is the land outside, eased to this.
    /// </summary>
    private sealed class FootprintGround : IGroundHeights
    {
        private readonly int _minX;
        private readonly int _minZ;
        private readonly int _sizeX;
        private readonly int[] _heights;

        private FootprintGround(int minX, int minZ, int sizeX, int[] heights, int lowest)
        {
            _minX = minX;
            _minZ = minZ;
            _sizeX = sizeX;
            _heights = heights;
            Lowest = lowest;
        }

        /// <summary>The lowest ground of the footprint.</summary>
        public int Lowest { get; }

        public static FootprintGround Over(WorldGenData data, ulong seed, RegionBounds bounds)
        {
            var blend = new RegionBlend(new RegionCatalog([new FootprintOnly(bounds)]), data.Blend, data.Vertical.SeaLevel);
            var climate = new BlendedClimateSampler(new ClimateSampler(data, seed), blend);
            var height = new HeightModel(data, seed, climate, blend.Excluder(data.Height.DrainageData?.RegionClearance ?? 0));
            var sizeX = bounds.MaxX - bounds.MinX + 1;
            var sizeZ = bounds.MaxZ - bounds.MinZ + 1;
            var heights = new int[sizeX * sizeZ];
            var samples = new ClimateSample[sizeX * sizeZ];
            climate.Fill(bounds.MinX, bounds.MinZ, sizeX, sizeZ, samples);
            var lowest = int.MaxValue;
            for (var z = 0; z < sizeZ; z++)
            {
                for (var x = 0; x < sizeX; x++)
                {
                    var i = (z * sizeX) + x;
                    var h = height.Shape(bounds.MinX + x, bounds.MinZ + z, samples[i]).Height;
                    heights[i] = h;
                    lowest = Math.Min(lowest, h);
                }
            }

            return new FootprintGround(bounds.MinX, bounds.MinZ, sizeX, heights, lowest);
        }

        public int HeightAt(int x, int z) => _heights[((z - _minZ) * _sizeX) + (x - _minX)];
    }

    // A region that is only a box: the blend asks it for nothing but its bounds when it pulls the climate towards land.
    private sealed class FootprintOnly(RegionBounds bounds) : IRegion
    {
        public string Id => "footprint";

        public RegionBounds Bounds { get; } = bounds;

        public bool Covers(VoxelChunkCoord chunk) => Bounds.Covers(chunk);

        public ChunkData Copy(VoxelChunkCoord chunk) => throw new NotSupportedException("A footprint holds no chunks.");

        public int EdgeHeight(int x, int z) => throw new NotSupportedException("A footprint has no ground.");

        public int SmoothedEdgeHeight(int x, int z) => throw new NotSupportedException("A footprint has no ground.");
    }
}
