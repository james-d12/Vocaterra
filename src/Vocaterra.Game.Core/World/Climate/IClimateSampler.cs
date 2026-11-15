namespace Vocaterra.Game.Core.World.Climate;

/// <summary>
/// The climate of the world as a pure function of the seed, the world data and a column (B01; section 3.2 of the biomes plan). Every implementation is immutable, integer only and
/// safe to call from any thread, so a chunk's climate is the same whenever and wherever it is asked for.
/// </summary>
public interface IClimateSampler
{
    /// <summary>The climate of the column (x, z).</summary>
    /// <param name="x">The block's x (negative is fine).</param>
    /// <param name="z">The block's z.</param>
    ClimateSample At(int x, int z);

    /// <summary>
    /// The climate temperature at a point: the column's sea-level temperature lowered by the data's lapse rate for every block above the sea. The same value decides where
    /// snow lies and where trees stop, and the future temperature system reads it (biomes plan, section 3.3).
    /// </summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The height.</param>
    /// <param name="z">The block's z.</param>
    int TemperatureAt(int x, int y, int z);

    /// <summary>The climate of a rectangle of columns, row by row (x fastest): the same samples <see cref="At"/> gives, with the lattice evaluated once.</summary>
    /// <param name="x0">The first column's x.</param>
    /// <param name="z0">The first column's z.</param>
    /// <param name="width">The columns along x.</param>
    /// <param name="depth">The columns along z.</param>
    /// <param name="into">The samples, at least width times depth of them.</param>
    void Fill(int x0, int z0, int width, int depth, Span<ClimateSample> into);
}
