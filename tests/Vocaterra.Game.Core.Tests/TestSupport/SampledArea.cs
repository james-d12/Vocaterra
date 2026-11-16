using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// A rectangle of chunk columns of the procedural world made whole (B11): every layer of every column from a source, with the final ground height of each column as the shapers left it, so a test can
/// tell cave air (air at or under the ground) from the sky and read any block, in or out of the sample, without generating a chunk twice.
/// </summary>
internal sealed class SampledArea
{
    private readonly IReadOnlyList<ChunkData>[,] _columns;
    private readonly int[][] _heights;
    private readonly int _minChunkY;

    /// <summary>Generates the area.</summary>
    /// <param name="procedural">The procedural source: its shaped columns give the heights.</param>
    /// <param name="blocks">The source of the blocks: the procedural one, or the composite of a world with regions.</param>
    /// <param name="chunkX0">The first chunk column's x.</param>
    /// <param name="chunkZ0">The first chunk column's z.</param>
    /// <param name="width">The chunk columns along x.</param>
    /// <param name="depth">The chunk columns along z.</param>
    public SampledArea(ProceduralChunkSource procedural, Func<int, int, IReadOnlyList<ChunkData>> blocks, int chunkX0, int chunkZ0, int width, int depth)
    {
        ChunkX0 = chunkX0;
        ChunkZ0 = chunkZ0;
        Width = width;
        Depth = depth;
        _minChunkY = procedural.MinChunkY;
        _columns = new IReadOnlyList<ChunkData>[width, depth];
        _heights = new int[width * depth][];
        for (var j = 0; j < depth; j++)
        {
            for (var i = 0; i < width; i++)
            {
                _columns[i, j] = blocks(chunkX0 + i, chunkZ0 + j);
                var ctx = procedural.ShapedColumn(chunkX0 + i, chunkZ0 + j);
                var heights = new int[256];
                for (var lz = 0; lz < 16; lz++)
                {
                    for (var lx = 0; lx < 16; lx++)
                    {
                        heights[(lz * 16) + lx] = ctx.HeightAt(((chunkX0 + i) << 4) + lx, ((chunkZ0 + j) << 4) + lz);
                    }
                }

                _heights[(j * width) + i] = heights;
            }
        }
    }

    /// <summary>The first chunk column's x.</summary>
    public int ChunkX0 { get; }

    /// <summary>The first chunk column's z.</summary>
    public int ChunkZ0 { get; }

    /// <summary>The chunk columns along x.</summary>
    public int Width { get; }

    /// <summary>The chunk columns along z.</summary>
    public int Depth { get; }

    /// <summary>The first block's x.</summary>
    public int MinX => ChunkX0 << 4;

    /// <summary>The first block's z.</summary>
    public int MinZ => ChunkZ0 << 4;

    /// <summary>One past the last block's x.</summary>
    public int MaxX => (ChunkX0 + Width) << 4;

    /// <summary>One past the last block's z.</summary>
    public int MaxZ => (ChunkZ0 + Depth) << 4;

    /// <summary>Whether the column is in the area.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public bool Contains(int x, int z) => x >= MinX && x < MaxX && z >= MinZ && z < MaxZ;

    /// <summary>The block at a position of the area.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public BlockId Block(int x, int y, int z)
    {
        var chunk = _columns[(x >> 4) - ChunkX0, (z >> 4) - ChunkZ0][(y >> 4) - _minChunkY];
        return chunk.IsEmpty ? BlockId.Air : chunk.Blocks[ChunkData.Index(x & 15, y & 15, z & 15)];
    }

    /// <summary>The final ground height of a column of the area (a water column's bed).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public int Height(int x, int z) => _heights[(((z >> 4) - ChunkZ0) * Width) + ((x >> 4) - ChunkX0)][((z & 15) * 16) + (x & 15)];

    /// <summary>Whether the block is cave air or lava: air or lava at or under the ground of its column.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public bool IsCave(int x, int y, int z) => y <= Height(x, z) && Block(x, y, z) is BlockId.Air or BlockId.Lava;

    /// <summary>The lowest and highest y of water in a column, or (1, 0) when it has none.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public (int Low, int High) Water(int x, int z)
    {
        var (low, high) = (int.MaxValue, int.MinValue);
        for (var y = _minChunkY << 4; y < (_minChunkY << 4) + (_columns[0, 0].Count << 4); y++)
        {
            if (Block(x, y, z) == BlockId.Water)
            {
                low = Math.Min(low, y);
                high = Math.Max(high, y);
            }
        }

        return low == int.MaxValue ? (1, 0) : (low, high);
    }
}
