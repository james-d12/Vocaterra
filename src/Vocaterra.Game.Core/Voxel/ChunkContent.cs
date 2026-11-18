namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// The blocks of one chunk, as a generator makes them and <see cref="VoxelGrid"/> installs them (never an edit). The grid takes the arrays without
/// copying them, so the maker hands them over and never touches them again.
/// </summary>
public sealed class ChunkContent
{
    /// <summary>Wraps the blocks (and, when any water cell holds a level other than source, the water levels) of one chunk.</summary>
    /// <param name="blocks">The 4 096 blocks in storage order (index = (localY * 16 + localZ) * 16 + localX).</param>
    /// <param name="waterLevels">The 4 096 encoded water levels in the same order (<see cref="WaterLevel.Encoded"/>; 0 for source and for cells that are not water), or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">An array has the wrong length, or a level lies on a cell that is not water.</exception>
    public ChunkContent(BlockId[] blocks, byte[]? waterLevels = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        if (blocks.Length != VoxelChunk.Volume || (waterLevels is not null && waterLevels.Length != VoxelChunk.Volume))
        {
            throw new ArgumentException($"A chunk holds {VoxelChunk.Volume} blocks.", nameof(blocks));
        }

        if (waterLevels is not null)
        {
            for (var i = 0; i < waterLevels.Length; i++)
            {
                if (waterLevels[i] != 0 && blocks[i] != BlockId.Water)
                {
                    throw new ArgumentException($"Cell {i} holds a water level but is not water.", nameof(waterLevels));
                }
            }
        }

        Blocks = blocks;
        WaterLevels = waterLevels;
    }

    /// <summary>The blocks in storage order.</summary>
    internal BlockId[] Blocks { get; }

    /// <summary>The encoded water levels in storage order, or <see langword="null"/>.</summary>
    internal byte[]? WaterLevels { get; }
}
