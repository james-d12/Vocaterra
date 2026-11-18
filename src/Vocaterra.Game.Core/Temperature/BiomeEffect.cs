namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// What a biome does to the temperature (I06c, <c>game/temperature.json</c> <c>biomes</c>): an offset added to the climate's baseline and the shares, in thousandths, of the day's swing and of
/// the weather's cooling that reach the player there (1 000 leaves them alone: open ground swings more, a forest or the water less, a ridge takes the weather harder).
/// </summary>
/// <param name="OffsetTenths">Tenths of a degree added to the baseline.</param>
/// <param name="DaySwingPermille">The share of the day's swing, in thousandths.</param>
/// <param name="WeatherPermille">The share of the weather's cooling, in thousandths.</param>
public readonly record struct BiomeEffect(int OffsetTenths, int DaySwingPermille, int WeatherPermille)
{
    /// <summary>A biome with no row: no offset, the day and the weather in full.</summary>
    public static BiomeEffect Neutral { get; } = new(0, 1000, 1000);
}
