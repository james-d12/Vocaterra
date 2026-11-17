using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Graphics.Sky;

/// <summary>
/// The sun of the world's light as the shader takes it (B17c), a pure function of the sky's look: the direction the faces are shaded by and the colour of the light. B22 gave each of four baked light bands its own sun; the shader has one sun
/// that follows the hour continuously, so nothing is baked and nothing is built again when the hour moves. With the setting off (<c>SunFollowsHour</c>) the sun stays where the Day band had it and the dusk's light is the lighter one B22 replaced.
/// </summary>
public static class SunModel
{
    /// <summary>The sun's elevation, in degrees, below which the direction is the real sun's and from which it is the fixed afternoon one of <see cref="ChunkMesher.SunDirection"/> (the sky's own golden hour and noon, <see cref="SkyBlend"/>).</summary>
    public const float NoonFrom = SkyBlend.NoonAbove;

    /// <summary>The elevation below which the shading uses the real sun (B22's dusk and dawn suns stood at 7.5 degrees: <see cref="LightBands.DuskSunHour"/>).</summary>
    public const float LowSunUntil = SkyBlend.GoldenBelow;

    /// <summary>The lowest the shading sun is held above the horizon while it is up, in degrees: B22's dusk band stood the sun at about seven degrees, so the ground keeps a little of its light until the sun is down.</summary>
    public const float LowestShadingElevation = 7.5f;

    /// <summary>The direction the faces are shaded by at night: the moon at midnight, high in the south, as B22's Night band baked it.</summary>
    public static (float X, float Y, float Z) NightDirection => LightBands.SunDirectionOf(LightBand.Night);

    /// <summary>The older dusk's light (before B22 deepened it): lighter and less red.</summary>
    public static readonly (float R, float G, float B) LegacyDuskLight = (0.84f, 0.66f, 0.60f);

    /// <summary>The sun the faces are shaded by at a moment.</summary>
    /// <param name="look">The look at the moment.</param>
    /// <param name="followsHour">Whether the sun follows the hour (the setting); off, it is <see cref="ChunkMesher.SunDirection"/> at every hour.</param>
    public static (float X, float Y, float Z) ShadingDirection(in SkyLook look, bool followsHour)
    {
        if (!followsHour)
        {
            return ChunkMesher.SunDirection;
        }

        var sun = look.SunDirection;
        var elevation = (float)(Math.Asin(Math.Clamp(sun.Y, -1f, 1f)) * 180 / Math.PI);

        // By day: the real sun low down, the fixed afternoon sun from high up, a smoothstep between. A low sun is held a few degrees above the horizon, so the ground never goes fully into the shade before the night's light takes over.
        var high = Smooth(LowSunUntil, NoonFrom, elevation);
        var low = HoldAbove(sun, LowestShadingElevation);
        var day = high >= 1f ? ChunkMesher.SunDirection : Slerp(low, ChunkMesher.SunDirection, high);

        // By night: the moon's direction at midnight (B22 shaded the Night band by it: high, in the south), fixed, because the moon's own path puts it opposite the sun at dusk and at dawn, and a turn between opposite directions has no steady pace. The turn is a slow one
        // through the dusk's whole twilight (the sun from 12 degrees under the horizon to 2 over it) along the great circle between the sun's and the moon's, so the shade turns at a steady pace.
        var night = Smooth(-12f, 2f, elevation);
        if (night >= 1f)
        {
            return day;
        }

        return Slerp(NightDirection, day, night);
    }

    /// <summary>The colour of the sun's light on the world at a moment: the look's, or with the older dusk when the sun does not follow the hour.</summary>
    /// <param name="look">The look at the moment.</param>
    /// <param name="followsHour">Whether the sun follows the hour (B22's deeper dusk is in the look's light).</param>
    public static (float R, float G, float B) Colour(in SkyLook look, bool followsHour)
    {
        if (followsHour || look.TwilightLight <= 0f)
        {
            return look.SunLight;
        }

        var dusk = ChunkMesher.DuskLight;
        var t = look.TwilightLight;
        return (look.SunLight.R + (t * (LegacyDuskLight.R - dusk.R)), look.SunLight.G + (t * (LegacyDuskLight.G - dusk.G)), look.SunLight.B + (t * (LegacyDuskLight.B - dusk.B)));
    }

    /// <summary>The look with its sun light as <see cref="Colour"/> gives it (the world, the figures and the rain take the light from the look).</summary>
    /// <param name="look">The look.</param>
    /// <param name="followsHour">Whether the sun follows the hour.</param>
    public static SkyLook Adjusted(in SkyLook look, bool followsHour) => followsHour ? look : look with { SunLight = Colour(look, false) };

    private static (float X, float Y, float Z) HoldAbove((float X, float Y, float Z) direction, float elevationDegrees)
    {
        var floor = MathF.Sin(elevationDegrees * MathF.PI / 180f);
        return direction.Y >= floor ? direction : Normalise((direction.X, 0f, direction.Z), floor);
    }

    // A direction with the same bearing as (x, z) at a height of y: the horizontal part scaled so the whole is a unit vector.
    private static (float X, float Y, float Z) Normalise((float X, float Y, float Z) bearing, float y)
    {
        var horizontal = MathF.Sqrt((bearing.X * bearing.X) + (bearing.Z * bearing.Z));
        if (horizontal < 1e-5f)
        {
            return (0f, 1f, 0f);
        }

        var scale = MathF.Sqrt(Math.Max(0f, 1f - (y * y))) / horizontal;
        return (bearing.X * scale, y, bearing.Z * scale);
    }

    private static (float X, float Y, float Z) Normalise((float X, float Y, float Z) v)
    {
        var length = MathF.Sqrt((v.X * v.X) + (v.Y * v.Y) + (v.Z * v.Z));
        return length < 1e-5f ? (0f, 1f, 0f) : (v.X / length, v.Y / length, v.Z / length);
    }

    // The point t of the way along the great circle from a to b (unit vectors).
    private static (float X, float Y, float Z) Slerp((float X, float Y, float Z) a, (float X, float Y, float Z) b, float t)
    {
        var dot = Math.Clamp((a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z), -1f, 1f);
        var omega = MathF.Acos(dot);
        var sin = MathF.Sin(omega);
        if (sin < 1e-4f)
        {
            return Normalise((a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t), a.Z + ((b.Z - a.Z) * t)));
        }

        var from = MathF.Sin((1f - t) * omega) / sin;
        var to = MathF.Sin(t * omega) / sin;
        return Normalise((a.X * from + b.X * to, a.Y * from + b.Y * to, a.Z * from + b.Z * to));
    }

    private static float Smooth(float from, float to, float x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0f, 1f);
        return t * t * (3f - (2f * t));
    }
}
