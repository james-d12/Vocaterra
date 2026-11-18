namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// How slowly what the player feels follows the place's temperature (I06c, <c>game/temperature.json</c> <c>lag</c>): it closes a share of the gap each tick, so after <see cref="SecondsToSettle"/>
/// seconds of play about two thirds of a step is felt. 0 turns the lag off. Whole-number arithmetic on the tick, so it is the same on every machine.
/// </summary>
/// <param name="SecondsToSettle">The time constant, in seconds of play (the world ticks <see cref="Needs.NeedsData.TicksPerSecond"/> times a second).</param>
public readonly record struct TemperatureLag(int SecondsToSettle)
{
    /// <summary>The share of the gap closed each tick, in millionths (1 000 000 when there is no lag).</summary>
    public int StepPerMillion => SecondsToSettle <= 0 ? 1_000_000 : Math.Max(1, 1_000_000 / (SecondsToSettle * Needs.NeedsData.TicksPerSecond));
}
