namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// One band of how the temperature feels (I06, <c>game/temperature.json</c>): warm, cool, cold, very cold are rows. A band begins at its lower bound and runs up to the next warmer
/// band's; what it does to the player is its own numbers, so the code names no band.
/// </summary>
/// <param name="Id">The band's id (<c>cold</c>), unique in the file.</param>
/// <param name="FromTenths">The lowest feeling in the band, in tenths of a degree. The coldest band takes every feeling below its bound too.</param>
/// <param name="WalkFactor">What the band multiplies the walking speed by: above 0 and at most 1 (1 leaves it alone).</param>
/// <param name="Chill">0 for a band with no cold in it, otherwise how cold, 1 and up: the cold icon shows while it is above 0, with that many notches, and the line is said as it first rises above 0.</param>
/// <param name="Line">The HUD line key said when the feeling first enters a chilled band (<c>temperature.cold</c>; a row of <c>hudLines</c> and of the locale), or <see langword="null"/> for none. Only on a band with a chill.</param>
public sealed record TemperatureBand(string Id, int FromTenths, double WalkFactor, int Chill, string? Line);
