namespace Vocaterra.Game.Graphics.Sky;

/// <summary>
/// Where the sun and the moon are at an hour (E02). The sun rises in the east at 5, passes its highest point at 13 and sets in the west at 21 (sixteen hours above the horizon, which
/// is where the dawn's and the dusk's colours sit), then goes round under the world until 5. Its path is tilted towards the north, which is where today's face shading puts it
/// (<see cref="World.ChunkMesher.SunDirection"/> is on this path, at about twenty to four in the afternoon, and its mirror image in the morning at about twenty past ten). The moon
/// rises at 18, is highest at midnight and sets at 6, on a path tilted to the south. Plain numbers; no clock: the hour comes in.
/// </summary>
public static class SkyArc
{
    /// <summary>The hour the sun rises.</summary>
    public const double Sunrise = 5;

    /// <summary>The hour the sun sets.</summary>
    public const double Sunset = 21;

    /// <summary>The hour the moon rises.</summary>
    public const double Moonrise = 18;

    /// <summary>The hour the moon sets.</summary>
    public const double Moonset = 6;

    // The tilt of the sun's path towards the north (the sun passes 56 degrees up at its highest), and of the moon's towards the south.
    private const double SunTilt = 33.7 * Math.PI / 180;
    private const double MoonTilt = -22.0 * Math.PI / 180;

    /// <summary>The unit vector towards the sun at an hour of the 24-hour dial (x east, y up, z south).</summary>
    /// <param name="hour">The hour, 0 up to 24.</param>
    public static (float X, float Y, float Z) SunAt(double hour)
    {
        var h = hour < Sunrise ? hour + 24 : hour;
        var angle = h <= Sunset ? (h - Sunrise) / (Sunset - Sunrise) * Math.PI : Math.PI + ((h - Sunset) / (24 - Sunset + Sunrise) * Math.PI);
        return Along(angle, SunTilt);
    }

    /// <summary>The unit vector towards the moon at an hour of the 24-hour dial (x east, y up, z south).</summary>
    /// <param name="hour">The hour, 0 up to 24.</param>
    public static (float X, float Y, float Z) MoonAt(double hour)
    {
        var h = hour < Moonrise ? hour + 24 : hour;
        var up = 24 - Moonrise + Moonset;
        var angle = h <= Moonrise + up ? (h - Moonrise) / up * Math.PI : Math.PI + ((h - (Moonrise + up)) / (24 - up) * Math.PI);
        return Along(angle, MoonTilt);
    }

    // A point on the great circle through east and west, tilted about the east-west axis by the given angle (towards the north when positive).
    private static (float X, float Y, float Z) Along(double angle, double tilt)
    {
        var sin = Math.Sin(angle);
        return ((float)Math.Cos(angle), (float)(sin * Math.Cos(tilt)), (float)(-sin * Math.Sin(tilt)));
    }
}
