using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// The surface y of the 18 x 18 block columns that cover one chunk column and the ring round it (B12): what <see cref="ChunkMesher"/> reads to find how far under the open sky a cell is
/// (<see cref="SkyDepthLight"/>). Plain values, so a mesh stays a function of its inputs and the rules are tested without a device.
/// </summary>
public sealed class SkySurface
{
    /// <summary>The columns along one side: the chunk's 16 and one on each side.</summary>
    public const int Side = VoxelChunk.Size + 2;

    private readonly int[] _surface;

    /// <summary>Makes the surface of a chunk column from a function of a block column.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    /// <param name="surfaceOf">The surface y of the block column (x, z).</param>
    public SkySurface(int chunkX, int chunkZ, Func<int, int, int> surfaceOf)
    {
        ArgumentNullException.ThrowIfNull(surfaceOf);
        OriginX = (chunkX << VoxelChunk.Shift) - 1;
        OriginZ = (chunkZ << VoxelChunk.Shift) - 1;
        _surface = new int[Side * Side];
        var max = int.MinValue;
        for (var dz = 0; dz < Side; dz++)
        {
            for (var dx = 0; dx < Side; dx++)
            {
                var y = surfaceOf(OriginX + dx, OriginZ + dz);
                _surface[(dz * Side) + dx] = y;
                max = Math.Max(max, y);
            }
        }

        Highest = max;
    }

    /// <summary>The x of the first block column held.</summary>
    public int OriginX { get; }

    /// <summary>The z of the first block column held.</summary>
    public int OriginZ { get; }

    /// <summary>The highest surface among the columns: a cell above it is open to the sky whatever column it is in.</summary>
    public int Highest { get; }

    /// <summary>The surface y of the block column (x, z), which lies within one block of the chunk column.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public int At(int x, int z) => _surface[((z - OriginZ) * Side) + (x - OriginX)];

    /// <summary>The share of the sun's light the cell (x, y, z) gets (<see cref="SkyDepthLight.Factor"/> of its depth under its column's surface).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public float FactorAt(int x, int y, int z) => y > Highest ? 1f : SkyDepthLight.Factor(At(x, z) - y);
}
