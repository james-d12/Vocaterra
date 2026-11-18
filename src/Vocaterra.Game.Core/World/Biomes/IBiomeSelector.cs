using Vocaterra.Game.Core.World.Climate;

namespace Vocaterra.Game.Core.World.Biomes;

/// <summary>The biome of a column as a pure function of the seed, the biome data, the column's climate and its ground (B01). Implementations are immutable and safe to call from any thread.</summary>
public interface IBiomeSelector
{
    /// <summary>The biome row of the column (x, z): a data row, never an enum.</summary>
    /// <param name="c">The column's climate.</param>
    /// <param name="height">The column's surface height in blocks (the bed, under water).</param>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    BiomeRow Select(in ClimateSample c, int height, int x, int z);
}
