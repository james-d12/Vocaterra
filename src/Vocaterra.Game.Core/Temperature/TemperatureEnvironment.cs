namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// What every term shares about the place (I06c): the steady ground temperature and how deep the ground takes over (<see cref="GroundTenths"/>, <see cref="ShallowBlocks"/>,
/// <see cref="FadeBlocks"/>) and the biomes' adjustments by biome id. The ids are data (the biome table's rows): no code names one.
/// </summary>
/// <param name="GroundTenths">The steady temperature below the surface, in tenths of a degree.</param>
/// <param name="ShallowBlocks">How far below the natural surface counts as still the surface (a ditch, a cellar's first steps): the sky reaches in full to here.</param>
/// <param name="FadeBlocks">Over how many blocks below that the sky and the day fade out and the ground takes over.</param>
/// <param name="Biomes">The adjustment of each biome that has a row, by biome id.</param>
public sealed record TemperatureEnvironment(int GroundTenths, int ShallowBlocks, int FadeBlocks, IReadOnlyDictionary<string, BiomeEffect> Biomes)
{
    /// <summary>An environment with no biome rows and a ground at 10 degrees (a place in no world, and tests).</summary>
    public static TemperatureEnvironment Plain { get; } = new(100, 4, 20, new Dictionary<string, BiomeEffect>());

    /// <summary>The adjustment of a biome: its row, or <see cref="BiomeEffect.Neutral"/>.</summary>
    /// <param name="biomeId">The biome's id.</param>
    public BiomeEffect EffectOf(string biomeId) => Biomes.TryGetValue(biomeId, out var effect) ? effect : BiomeEffect.Neutral;

    /// <summary>The exposure, in thousandths, of a place that deep below the natural surface (0 or less is on it).</summary>
    /// <param name="depthBlocks">Blocks below the natural surface.</param>
    public int ExposureAt(int depthBlocks)
    {
        var below = depthBlocks - ShallowBlocks;
        if (below <= 0)
        {
            return 1000;
        }

        return FadeBlocks <= 0 || below >= FadeBlocks ? 0 : 1000 - (int)((long)below * 1000 / FadeBlocks);
    }
}
