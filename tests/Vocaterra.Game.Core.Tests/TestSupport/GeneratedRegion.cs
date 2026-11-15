using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>A rectangle of chunk columns generated whole, read by world position: the tests that look across chunk borders read through it.</summary>
internal sealed class GeneratedRegion
{
    private readonly Dictionary<(int X, int Z), IReadOnlyList<ChunkData>> _columns = [];
    private readonly int _minChunkY;

    public GeneratedRegion(ProceduralChunkSource source, int x0, int z0, int width, int depth)
    {
        X0 = x0;
        Z0 = z0;
        Width = width;
        Depth = depth;
        _minChunkY = source.MinChunkY;
        for (var cz = z0; cz < z0 + depth; cz++)
        {
            for (var cx = x0; cx < x0 + width; cx++)
            {
                _columns[(cx, cz)] = source.GenerateColumn(cx, cz);
            }
        }
    }

    public int X0 { get; }

    public int Z0 { get; }

    public int Width { get; }

    public int Depth { get; }

    /// <summary>The first block x of the region.</summary>
    public int MinX => X0 * 16;

    /// <summary>The first block z of the region.</summary>
    public int MinZ => Z0 * 16;

    /// <summary>One past the last block x.</summary>
    public int MaxX => (X0 + Width) * 16;

    /// <summary>One past the last block z.</summary>
    public int MaxZ => (Z0 + Depth) * 16;

    /// <summary>The block at a world position inside the region, or air above and below the world.</summary>
    public BlockId BlockAt(int x, int y, int z)
    {
        var layer = (y >> 4) - _minChunkY;
        var column = _columns[(x >> 4, z >> 4)];
        return layer < 0 || layer >= column.Count ? BlockId.Air : column[layer][x & 15, y & 15, z & 15];
    }

    /// <summary>The chunks of the region, every layer of every column.</summary>
    public IEnumerable<ChunkData> Chunks => _columns.Values.SelectMany(static c => c);

    /// <summary>The y of the highest block of the column that is not air, or the world's floor when it is all air.</summary>
    public int HighestSolid(int x, int z)
    {
        for (var y = ((_columns[(x >> 4, z >> 4)].Count + _minChunkY) * 16) - 1; y >= _minChunkY * 16; y--)
        {
            if (BlockAt(x, y, z) != BlockId.Air)
            {
                return y;
            }
        }

        return _minChunkY * 16;
    }
}
