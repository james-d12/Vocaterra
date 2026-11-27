namespace Vocaterra.Game.Core.Voxel;

/// <summary>The write side of a voxel world, internal to the simulation and the generation passes: every call that changes something is an edit.</summary>
internal interface IVoxelWriter : IVoxelReader
{
    /// <summary>Writes a block with state 0; returns whether the world changed.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The position lies outside the limits.</exception>
    bool SetBlock(int x, int y, int z, BlockId block);

    /// <summary>Writes a block and its state together (K01); returns whether the world changed (a different block, or the same block with a different state). Air always holds state 0.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The position lies outside the limits.</exception>
    bool SetBlock(int x, int y, int z, BlockId block, byte state);

    /// <summary>Writes the level of a water cell; returns whether it changed.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The position lies outside the limits.</exception>
    /// <exception cref="InvalidOperationException">The block at the position is not water.</exception>
    bool SetWater(int x, int y, int z, WaterLevel level);

    /// <summary>Fills the column (<paramref name="x"/>, <paramref name="z"/>) from <paramref name="y0"/> to <paramref name="y1"/> inclusive (clipped to the limits) with a block.</summary>
    void FillColumn(int x, int z, int y0, int y1, BlockId block);
}
