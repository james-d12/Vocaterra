namespace Vocaterra.Game.Graphics.Weather;

/// <summary>What falls now, as the weather's channels say it (E06): the kind (empty for none), how hard it falls, 0 to 1, and the wind, 0 to 1.</summary>
/// <param name="Kind">The precipitation kind's id (a row of <see cref="PrecipitationStyles"/>), or empty.</param>
/// <param name="Intensity">How hard it falls, 0 to 1 (blended across a change of weather).</param>
/// <param name="Wind">The wind channel, 0 to 1.</param>
public readonly record struct PrecipitationState(string Kind, double Intensity, double Wind)
{
    /// <summary>Nothing falls.</summary>
    public static PrecipitationState None => new(string.Empty, 0, 0);
}
