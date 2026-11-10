namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>
/// What the weather keeps (E04): the kind now and the kind before it, the ticks the spell began and ends at, how many spells and mornings were drawn (the counters the
/// seeded draws are keyed by) and whether a test holds the kind. It is all that is digested and saved, and only once it differs from a new game's.
/// </summary>
/// <param name="Current">The id of the kind now.</param>
/// <param name="Previous">The id of the kind the transition leaves.</param>
/// <param name="SpellStart">The tick the spell began.</param>
/// <param name="SpellEnd">The tick the spell ends (the largest tick while held).</param>
/// <param name="Spells">How many spells were drawn since the start.</param>
/// <param name="Mornings">How many mornings were rolled.</param>
/// <param name="Held">Whether a test or a screenshot holds the kind.</param>
public sealed record WeatherState(string Current, string Previous, long SpellStart, long SpellEnd, int Spells, int Mornings, bool Held);
