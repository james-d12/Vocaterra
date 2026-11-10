namespace Vocaterra.Game.Graphics.Sky;

/// <summary>
/// How the weather's channels change the sky's look (E05): plain functions of the cloud cover, the sunlight and the fog, all continuous in them, so the blend between two kinds
/// shows no step. The fair-weather values (<see cref="FairCover"/>, a sunlight of 1, no fog) change nothing: every function returns its input exactly, so the first kind's look is the
/// one the day cycle always drew. No function here knows a kind's id; they read the channels.
/// </summary>
public static class SkyWeather
{
    /// <summary>The cloud cover of fair weather, 0 to 1: the share of cloud the sky has always had (<see cref="CloudField.Cover"/> percent). Above it the sky closes; the look never gets clearer than this.</summary>
    public const double FairCover = CloudField.Cover / 100.0;

    private const double CoverWeight = 0.5;
    private const double DimWeight = 0.7;

    /// <summary>How far the cover is past fair weather, 0 (fair or clearer) to 1 (a full cover).</summary>
    /// <param name="cover">The cloud cover channel, 0 to 1.</param>
    public static float Overcast(double cover) => (float)Math.Clamp((cover - FairCover) / (1 - FairCover), 0, 1);

    /// <summary>How heavy the sky is, 0 (fair) to 1: the cloud cover past fair weather and the sunlight lost, joined. It is how far the sky's colours go to a dark grey.</summary>
    /// <param name="cover">The cloud cover channel, 0 to 1.</param>
    /// <param name="sunlight">The sunlight channel, 0 to 1.</param>
    public static float Gloom(double cover, double sunlight)
    {
        var dim = Math.Clamp(1 - sunlight, 0, 1);
        return (float)Math.Clamp((CoverWeight * Overcast(cover)) + (DimWeight * dim), 0, 1);
    }

    /// <summary>How much of the sun (and the moon and the stars) shows through the cover: 1 at fair weather, falling smoothly to 0 at a full cover.</summary>
    /// <param name="cover">The cloud cover channel, 0 to 1.</param>
    public static float BodyVisibility(double cover)
    {
        var t = Overcast(cover);
        return 1f - (t * t * (3f - (2f * t)));
    }

    /// <summary>
    /// A colour taken <paramref name="amount"/> of the way to the grey of its own brightness, darkened by <paramref name="darken"/> (1 keeps the brightness, 0.5 halves it). An
    /// <paramref name="amount"/> of 0 returns the colour itself.
    /// </summary>
    /// <param name="colour">The colour.</param>
    /// <param name="amount">How far to the grey, 0 to 1.</param>
    /// <param name="darken">The brightness the grey keeps, 0 to 1.</param>
    public static (byte R, byte G, byte B) Grey((byte R, byte G, byte B) colour, float amount, float darken)
    {
        if (amount <= 0f)
        {
            return colour;
        }

        var a = Math.Clamp(amount, 0f, 1f);
        var grey = ((0.3f * colour.R) + (0.59f * colour.G) + (0.11f * colour.B)) * darken;
        return (Channel(colour.R, grey, a), Channel(colour.G, grey, a), Channel(colour.B, grey, a));
    }

    /// <summary>How near the fog's end is, as a multiple of the clear-weather distance: 1 with no fog, 1/3 at full fog, straight in between.</summary>
    /// <param name="fog">The fog channel, 0 to 1.</param>
    public static float FogReach(double fog) => 1f - ((float)Math.Clamp(fog, 0, 1) * (2f / 3f));

    private static byte Channel(byte from, float to, float t) => (byte)Math.Clamp(MathF.Round(from + ((to - from) * t)), 0f, 255f);
}
