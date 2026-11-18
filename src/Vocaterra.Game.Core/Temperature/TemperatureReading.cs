namespace Vocaterra.Game.Core.Temperature;

/// <summary>How the temperature stands for the player at a tick (I06): where they stand, what they wear, how it feels and which band that is. Derived each tick, never saved.</summary>
/// <param name="PlaceTenths">The temperature the player feels the place to have, in tenths of a degree: the place's own value after the lag (<see cref="TemperatureLag"/>), or the held value of <c>--temperature</c>.</param>
/// <param name="WarmthTenths">The player's warmth (<see cref="IWarmthSource"/>), in tenths.</param>
/// <param name="Band">The band the feeling falls in.</param>
/// <param name="Held">Whether the place's temperature is held by <c>--temperature</c>.</param>
/// <param name="Terms">What each term gave at this tick, by name (I06c), for the debug overlay; they sum to <see cref="TargetTenths"/>, not to <paramref name="PlaceTenths"/> while the lag runs.</param>
/// <param name="TargetTenths">The temperature the place has now, in tenths: what <paramref name="PlaceTenths"/> is moving towards (equal to it when nothing lags or the value is held).</param>
public readonly record struct TemperatureReading(int PlaceTenths, int WarmthTenths, TemperatureBand Band, bool Held, IReadOnlyList<TemperatureContribution>? Terms = null, int? TargetTenths = null)
{
    /// <summary>The value of one term by name, 0 when there is none.</summary>
    /// <param name="name">The term's name.</param>
    public int TermTenths(string name) => Terms?.FirstOrDefault(c => string.Equals(c.Term, name, StringComparison.Ordinal)).Tenths ?? 0;

    /// <summary>The feeling: the place's temperature plus the player's warmth, in tenths of a degree.</summary>
    public int FeelingTenths => PlaceTenths + WarmthTenths;
}
