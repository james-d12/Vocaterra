namespace Vocaterra.Game.Core.World.Climate;

/// <summary>The fall of the temperature with height, in one place: the climate sampler and the biome table both use it, so the cold the player feels and the snow they see agree.</summary>
public static class ClimateLapse
{
    /// <summary>The temperature at a height: the sea-level temperature, less the lapse for every block above the sea (none below it).</summary>
    /// <param name="temperature">The sea-level temperature, fixed point.</param>
    /// <param name="y">The height in blocks.</param>
    /// <param name="seaLevel">The sea level in blocks.</param>
    /// <param name="lapsePer100Blocks">The fall, in thousandths of the range, for every 100 blocks above the sea.</param>
    public static int Apply(int temperature, int y, int seaLevel, int lapsePer100Blocks)
    {
        var above = Math.Max(0, y - seaLevel);
        return temperature - (int)((long)above * lapsePer100Blocks * Mathematics.IntMath.One / 100_000);
    }
}
