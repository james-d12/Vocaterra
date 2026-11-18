using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.World.Regions;

/// <summary>
/// A region drawn by hand (B06, biomes plan section 3.6): the valley, built once by the unchanged <see cref="Generation.SceneBuilder"/> and then frozen. Its chunks are copied into immutable
/// arrays (<see cref="ChunkData"/>), so a worker thread reads them with no lazy cache to race on; its ground heights are kept as an array, which is the edge profile the land outside is eased
/// to; and its template digest, which the world's state digest starts with, is worked out from the chunks as built. Its box comes from the layout's size (a valley of 384 x 128 x 384 blocks is
/// 24 x 8 x 24 chunks), and it may stand anywhere on the chunk grid. The valley's trunks keep two blocks inside its edge, so no crown crosses the border.
/// </summary>
public sealed class AuthoredReliefRegion : IRegion
{
    private readonly ChunkData?[]? _chunks;
    private readonly int[] _ground;
    private readonly int[] _smoothed;
    private readonly int _sizeX;
    private readonly int _sizeZ;

    private AuthoredReliefRegion(string id, RegionBounds bounds, ChunkData?[]? chunks, int[] ground, int[] smoothed, byte[] templateDigest, TemplateBlockIndex blocks)
    {
        Id = id;
        Bounds = bounds;
        _chunks = chunks;
        _ground = ground;
        _smoothed = smoothed;
        _sizeX = bounds.ChunksX << VoxelChunk.Shift;
        _sizeZ = bounds.ChunksZ << VoxelChunk.Shift;
        TemplateDigest = templateDigest;
        Blocks = blocks;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public RegionBounds Bounds { get; }

    /// <summary>The digest of the template (<see cref="WorldStateHeader.TemplateDigest"/>), 32 bytes.</summary>
    public byte[] TemplateDigest { get; }

    /// <summary>Where the rarer kinds of block stood when the region was made (the fire rings' stones, the roads), in world coordinates.</summary>
    public TemplateBlockIndex Blocks { get; }

    /// <summary>Whether the region holds its chunks (a region made with <c>keepChunks: false</c> only describes its edge, its heights, its digest and its blocks).</summary>
    public bool HasChunks => _chunks is not null;

    /// <summary>
    /// Freezes a built scene into a region whose first chunk is (<paramref name="originChunkX"/>, 0, <paramref name="originChunkZ"/>): the scene's grid must be bounded and unedited.
    /// </summary>
    /// <param name="id">The region's name.</param>
    /// <param name="scene">The scene as the builder made it.</param>
    /// <param name="originChunkX">The first chunk column of the region in the world (0 for the valley).</param>
    /// <param name="originChunkZ">The first chunk row of the region in the world (0 for the valley).</param>
    /// <param name="originChunkY">The first chunk layer of the region in the world (0 for the valley; a town on procedural ground stands on whatever layer its ground needs, B15).</param>
    /// <param name="keepChunks">Whether to copy the chunks into immutable arrays; false keeps only what a bounded game needs (the digest, the edge heights and the block index).</param>
    /// <param name="edgeBlurRadius">The radius, in columns, of the blur that <see cref="SmoothedEdgeHeight"/> answers with (<see cref="EdgeSmoothingSpec.BlurRadius"/>).</param>
    /// <exception cref="ArgumentException">The scene did not come from the scene builder (it has no ground heights), or its grid is streamed.</exception>
    internal static AuthoredReliefRegion Freeze(string id, Scene scene, int originChunkX = 0, int originChunkZ = 0, bool keepChunks = true, int edgeBlurRadius = 16, int originChunkY = 0)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var grid = scene.Grid;
        if (grid.IsStreamed || scene.GroundHeights is not { } ground || scene.Template is not { } template)
        {
            throw new ArgumentException("A region is frozen from a scene the scene builder made (a bounded grid with its ground heights).", nameof(scene));
        }

        var limits = grid.Limits;
        var bounds = new RegionBounds(originChunkX, originChunkY, originChunkZ, limits.EndChunkX, limits.EndChunkY, limits.EndChunkZ);
        ChunkData?[]? chunks = null;
        if (keepChunks)
        {
            chunks = new ChunkData?[bounds.ChunksX * bounds.ChunksY * bounds.ChunksZ];
            for (var cy = 0; cy < bounds.ChunksY; cy++)
            {
                for (var cz = 0; cz < bounds.ChunksZ; cz++)
                {
                    for (var cx = 0; cx < bounds.ChunksX; cx++)
                    {
                        var coord = new VoxelChunkCoord(originChunkX + cx, originChunkY + cy, originChunkZ + cz);
                        if (grid.TryGetChunk(new VoxelChunkCoord(cx, cy, cz), out var chunk) && !chunk.IsEmpty)
                        {
                            chunks[(((cy * bounds.ChunksZ) + cz) * bounds.ChunksX) + cx] = ChunkData.From(coord, chunk.Blocks, chunk.WaterLevels);
                        }
                    }
                }
            }
        }

        // The ground is kept in the world's y: the scene's heights are the grid's own, and the grid's floor is the region's first layer.
        var lift = originChunkY << VoxelChunk.Shift;
        var heights = lift == 0 ? (int[])ground.Clone() : [.. ground.Select(h => h + lift)];
        return new AuthoredReliefRegion(id, bounds, chunks, heights, Blurred(heights, bounds.ChunksX << VoxelChunk.Shift, bounds.ChunksZ << VoxelChunk.Shift, edgeBlurRadius), RegionTemplateDigest.Of(grid, originChunkX, originChunkZ, originChunkY), template.Shifted(originChunkX, originChunkZ, originChunkY));
    }

    /// <inheritdoc />
    public bool Covers(VoxelChunkCoord chunk) => Bounds.Covers(chunk);

    /// <inheritdoc />
    public ChunkData Copy(VoxelChunkCoord chunk)
    {
        if (!Bounds.Covers(chunk))
        {
            throw new ArgumentOutOfRangeException(nameof(chunk), chunk, $"The chunk is not one of the region '{Id}'.");
        }

        if (_chunks is null)
        {
            throw new InvalidOperationException($"The region '{Id}' was made without its chunks.");
        }

        var index = (((chunk.Y - Bounds.MinChunkY) * Bounds.ChunksZ) + (chunk.Z - Bounds.MinChunkZ)) * Bounds.ChunksX + (chunk.X - Bounds.MinChunkX);
        return _chunks[index] ?? ChunkData.Empty(chunk);
    }

    /// <inheritdoc />
    public int EdgeHeight(int x, int z)
    {
        var (cx, cz) = Bounds.Clamp(x, z);
        return _ground[((cz - Bounds.MinZ) * _sizeX) + (cx - Bounds.MinX)];
    }

    /// <inheritdoc />
    public int SmoothedEdgeHeight(int x, int z)
    {
        var (cx, cz) = Bounds.Clamp(x, z);
        return _smoothed[((cz - Bounds.MinZ) * _sizeX) + (cx - Bounds.MinX)];
    }

    // The ground averaged over a square of 2 x radius + 1 columns (fewer at the footprint's edge: only the columns inside count), as two passes of a running sum, rounded to the nearest block.
    private static int[] Blurred(int[] ground, int sizeX, int sizeZ, int radius)
    {
        if (radius <= 0)
        {
            return (int[])ground.Clone();
        }

        var across = new int[ground.Length];
        for (var z = 0; z < sizeZ; z++)
        {
            for (var x = 0; x < sizeX; x++)
            {
                var (from, to) = (Math.Max(0, x - radius), Math.Min(sizeX - 1, x + radius));
                var sum = 0L;
                for (var i = from; i <= to; i++)
                {
                    sum += ground[(z * sizeX) + i];
                }

                across[(z * sizeX) + x] = (int)((sum * 16) / (to - from + 1)); // sixteenths, so the second pass does not round twice
            }
        }

        var blurred = new int[ground.Length];
        for (var x = 0; x < sizeX; x++)
        {
            for (var z = 0; z < sizeZ; z++)
            {
                var (from, to) = (Math.Max(0, z - radius), Math.Min(sizeZ - 1, z + radius));
                var sum = 0L;
                for (var j = from; j <= to; j++)
                {
                    sum += across[(j * sizeX) + x];
                }

                blurred[(z * sizeX) + x] = (int)((((sum / (to - from + 1)) + 8) >> 4));
            }
        }

        return blurred;
    }
}
