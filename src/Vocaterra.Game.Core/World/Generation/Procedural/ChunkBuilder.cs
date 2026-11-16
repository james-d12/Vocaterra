using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The chunk a pass writes into (B03): 4 096 blocks under construction, addressed by world position. A write outside the chunk is ignored, so a pass that reaches into a neighbour (a crown) can
/// write everything it means and keep only what is inside. Procedural water is source except where a river's surface steps down (falling water and a cell of thin flow, see <see cref="WaterPass"/>), so the levels are kept only then. Not shared: one builder is made, filled and turned into a
/// <see cref="ChunkData"/> by one call.
/// </summary>
internal sealed class ChunkBuilder
{
    private readonly BlockId[] _blocks = new BlockId[ChunkData.Volume];
    private readonly byte[] _water = new byte[ChunkData.Volume]; // the levels of the water cells: all zero (every cell a source) for most chunks, which ChunkData then drops

    /// <summary>Starts a chunk of air.</summary>
    /// <param name="coord">The chunk's position.</param>
    public ChunkBuilder(VoxelChunkCoord coord)
    {
        Coord = coord;
        MinX = coord.X << VoxelChunk.Shift;
        MinY = coord.Y << VoxelChunk.Shift;
        MinZ = coord.Z << VoxelChunk.Shift;
    }

    /// <summary>Where the chunk sits in the chunk grid.</summary>
    public VoxelChunkCoord Coord { get; }

    /// <summary>The x of the chunk's first block.</summary>
    public int MinX { get; }

    /// <summary>The y of the chunk's lowest layer.</summary>
    public int MinY { get; }

    /// <summary>The z of the chunk's first block.</summary>
    public int MinZ { get; }

    /// <summary>The y of the chunk's highest layer.</summary>
    public int MaxY => MinY + VoxelChunk.Size - 1;

    /// <summary>Whether the world position lies in the chunk.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public bool Contains(int x, int y, int z) => (uint)(x - MinX) < VoxelChunk.Size && (uint)(y - MinY) < VoxelChunk.Size && (uint)(z - MinZ) < VoxelChunk.Size;

    /// <summary>The block at a world position inside the chunk.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public BlockId Get(int x, int y, int z) => _blocks[ChunkData.Index(x - MinX, y - MinY, z - MinZ)];

    /// <summary>Writes a block at a world position; ignored when the position is outside the chunk.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="block">The block.</param>
    public void Set(int x, int y, int z, BlockId block)
    {
        if (Contains(x, y, z))
        {
            var index = ChunkData.Index(x - MinX, y - MinY, z - MinZ);
            _blocks[index] = block;
            _water[index] = 0;
        }
    }

    /// <summary>Sets the level of the water block at a world position (the cell must hold water; a position outside the chunk is ignored).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="level">The level.</param>
    public void SetWater(int x, int y, int z, WaterLevel level)
    {
        if (Contains(x, y, z))
        {
            var index = ChunkData.Index(x - MinX, y - MinY, z - MinZ);
            _blocks[index] = BlockId.Water;
            _water[index] = level.Encoded;
        }
    }

    /// <summary>Fills the column (x, z), which must be inside the chunk's footprint, from y0 to y1 inclusive, clipped to the chunk.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="y0">The lowest y.</param>
    /// <param name="y1">The highest y.</param>
    /// <param name="block">The block.</param>
    public void FillColumn(int x, int z, int y0, int y1, BlockId block)
    {
        var lo = Math.Max(y0, MinY);
        var hi = Math.Min(y1, MaxY);
        for (var y = lo; y <= hi; y++)
        {
            var index = ChunkData.Index(x - MinX, y - MinY, z - MinZ);
            _blocks[index] = block;
            _water[index] = 0;
        }
    }

    /// <summary>Fills the whole chunk with one block.</summary>
    /// <param name="block">The block.</param>
    public void FillAll(BlockId block)
    {
        Array.Fill(_blocks, block);
        Array.Clear(_water);
    }

    /// <summary>The finished chunk. The builder hands over its blocks and must not be written afterwards.</summary>
    public ChunkData ToData() => new(Coord, _blocks, _water);
}
