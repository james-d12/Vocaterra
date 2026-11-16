namespace Vocaterra.Game.Core.Temperature;

/// <summary>How the temperature stands for the player at a tick (I06): where they stand, what they wear, how it feels and which band that is. Derived each tick, never saved.</summary>
/// <param name="PlaceTenths">The temperature of the place, in tenths of a degree (or the held value of <c>--temperature</c>).</param>
/// <param name="WarmthTenths">The player's warmth (<see cref="IWarmthSource"/>), in tenths.</param>
/// <param name="Band">The band the feeling falls in.</param>
/// <param name="Held">Whether the place's temperature is held by <c>--temperature</c>.</param>
public readonly record struct TemperatureReading(int PlaceTenths, int WarmthTenths, TemperatureBand Band, bool Held)
{
    /// <summary>The feeling: the place's temperature plus the player's warmth, in tenths of a degree.</summary>
    public int FeelingTenths => PlaceTenths + WarmthTenths;
}
