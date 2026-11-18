namespace Vocaterra.Game.Core.Light;

/// <summary>
/// The light round the player at one moment (I05b), in plain values for the renderer: how far it reaches, how strongly it is added to what it falls on, and its colour. It is a function of the worn items' <c>light</c> stat and the daylight at the
/// player (<see cref="Of"/>), never of the simulation: nothing here is saved, digested or stepped.
/// </summary>
/// <param name="Radius">The reach in blocks; 0 when the light is off.</param>
/// <param name="Strength">How strongly it is added next to the player, 0 to 1; 0 when the light is off.</param>
/// <param name="Colour">The light's colour, each channel 0 to 1.</param>
public readonly record struct PlayerLight(float Radius, float Strength, (float R, float G, float B) Colour)
{
    /// <summary>The most any sum of worn items counts for: the world's own top block light level (a placed torch).</summary>
    public const int MaxLight = 16;

    /// <summary>No light: nothing worn gives any, or it is day.</summary>
    public static PlayerLight Off => new(0f, 0f, (0f, 0f, 0f));

    /// <summary>Whether the light is on (something to draw).</summary>
    public bool IsOn => Radius > 0f && Strength > 0f;

    /// <summary>
    /// The light for a sum of worn light and the daylight at the player. The stat is clamped to 0 to <see cref="MaxLight"/>; the radius is the base plus a share per point and the strength a share per point up to the cap; both are then
    /// faded by how dark it is: full at or under <see cref="PlayerLightData.DarkAtDaylight"/>, none at or over <see cref="PlayerLightData.OffAtDaylight"/>, a straight line between. Nothing worn (the stat at or under 0) is <see cref="Off"/>.
    /// </summary>
    /// <param name="data">The light's numbers (<c>game/light.json</c>).</param>
    /// <param name="lightStat">The stat sheet's sum of <c>light</c>.</param>
    /// <param name="daylight">The share of daylight at the player, 0 (dark) to 1 (full day in the open).</param>
    public static PlayerLight Of(PlayerLightData data, double lightStat, double daylight)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!double.IsFinite(lightStat) || lightStat <= 0)
        {
            return Off;
        }

        var dark = Darkness(data, daylight);
        if (dark <= 0)
        {
            return Off;
        }

        var points = Math.Min(lightStat, MaxLight);
        var radius = data.RadiusBase + (data.RadiusPerPoint * points);
        var strength = Math.Min(data.MaxStrength, data.StrengthPerPoint * points);
        return new PlayerLight((float)radius, (float)(strength * dark), ((float)data.Colour.R, (float)data.Colour.G, (float)data.Colour.B));
    }

    /// <summary>How dark it is, 0 (the light is off) to 1 (the light is at its full strength), from the daylight at the player.</summary>
    /// <param name="data">The light's numbers.</param>
    /// <param name="daylight">The share of daylight, 0 to 1.</param>
    public static double Darkness(PlayerLightData data, double daylight)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!double.IsFinite(daylight))
        {
            return 0;
        }

        return Math.Clamp((data.OffAtDaylight - daylight) / (data.OffAtDaylight - data.DarkAtDaylight), 0, 1);
    }
}
