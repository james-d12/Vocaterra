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
    /// <param name="states">The 4 096 state bytes in the same order (K01; 0 for a cell with none), or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">An array has the wrong length, a level lies on a cell that is not water, or a state lies on air.</exception>
    public ChunkContent(BlockId[] blocks, byte[]? waterLevels = null, byte[]? states = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        if (blocks.Length != VoxelChunk.Volume || (waterLevels is not null && waterLevels.Length != VoxelChunk.Volume) || (states is not null && states.Length != VoxelChunk.Volume))
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

        if (states is not null)
        {
            for (var i = 0; i < states.Length; i++)
            {
                if (states[i] != 0 && blocks[i] == BlockId.Air)
                {
                    throw new ArgumentException($"Cell {i} holds a state but is air.", nameof(states));
                }
            }
        }

        Blocks = blocks;
        WaterLevels = waterLevels;
        States = states;
    }

    /// <summary>The blocks in storage order.</summary>
    internal BlockId[] Blocks { get; }

    /// <summary>The encoded water levels in storage order, or <see langword="null"/>.</summary>
    internal byte[]? WaterLevels { get; }

    /// <summary>The state bytes in storage order, or <see langword="null"/>.</summary>
    internal byte[]? States { get; }
}
