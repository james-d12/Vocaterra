namespace Vocaterra.Game.Core.Mathematics;

/// <summary>
/// Integer angles and their sine and cosine, with no floating point at all. An angle is a whole number of 1/4096 turns
/// (<see cref="StepsPerTurn"/> steps: 0.0879 degrees each, finer than a mouse count). Sine and cosine are fixed-point numbers with
/// <see cref="One"/> (65 536) standing for 1.0.
/// <para>
/// <b>Yaw convention:</b> yaw 0 faces north (-Z), 1 024 faces east (+X), 2 048 south (+Z), 3 072 west (-X): a compass bearing, increasing
/// clockwise seen from above. <b>Pitch:</b> 0 is level, positive looks up (+Y); pitch is clamped to +-<see cref="MaxPitch"/> (about 89.9 degrees).
/// </para>
/// <para>
/// <b>Why a table built at start-up:</b> the library sine is not guaranteed to give the same last bit on every platform and runtime, and a
/// replay has to be bit-identical. So the first quarter turn (1 025 entries) is computed once, in type initialisation, with a Taylor series
/// in 64-bit integers (Q30 fixed point, x up to pi/2, terms up to x^15, error below 2^-16), and the other three quarters follow by symmetry.
/// Every operation is an integer multiply, shift or divide, which every platform does identically. A test compares the table with the
/// library sine to within one unit in the last place (the comparison lives in the tests, never in the simulation).
/// </para>
/// </summary>
public static class Angles
{
    /// <summary>Angle steps in a full turn (4 096).</summary>
    public const int StepsPerTurn = 4096;

    /// <summary>Angle steps in a quarter turn (1 024).</summary>
    public const int QuarterTurn = StepsPerTurn / 4;

    /// <summary>The fixed-point value of 1.0 (65 536).</summary>
    public const int One = 65536;

    /// <summary>The largest pitch magnitude, one step short of straight up or down (1 023 steps, 89.91 degrees).</summary>
    public const int MaxPitch = QuarterTurn - 1;

    private const int FractionBits = 30;
    private const long PiOverTwoQ30 = 1686629713L; // round(pi / 2 * 2^30)

    private static readonly int[] QuarterSine = BuildQuarterSine();

    /// <summary>The angle <paramref name="degrees"/> degrees (a whole number), rounded to the nearest step and wrapped into [0, 4096).</summary>
    public static int FromDegrees(int degrees) => Wrap(((degrees * StepsPerTurn) + (degrees >= 0 ? 180 : -180)) / 360);

    /// <summary>Wraps an angle into [0, 4096).</summary>
    public static int Wrap(int angle) => angle & (StepsPerTurn - 1);

    /// <summary>Clamps a pitch into [-<see cref="MaxPitch"/>, <see cref="MaxPitch"/>].</summary>
    public static int ClampPitch(int pitch) => Math.Clamp(pitch, -MaxPitch, MaxPitch);

    /// <summary>The sine of the angle in units of 1/65 536 (so -65 536 to 65 536).</summary>
    public static int Sin(int angle)
    {
        var a = Wrap(angle);
        var quadrant = a >> 10;
        var offset = a & (QuarterTurn - 1);
        return quadrant switch
        {
            0 => QuarterSine[offset],
            1 => QuarterSine[QuarterTurn - offset],
            2 => -QuarterSine[offset],
            _ => -QuarterSine[QuarterTurn - offset],
        };
    }

    /// <summary>The cosine of the angle in units of 1/65 536.</summary>
    public static int Cos(int angle) => Sin(angle + QuarterTurn);

    /// <summary>
    /// The unit vector (fixed point, <see cref="One"/> = 1.0) a viewer looks along for a yaw and a pitch: east component
    /// cos(pitch) * sin(yaw), up component sin(pitch), south component -cos(pitch) * cos(yaw). Yaw 0, pitch 0 is (0, 0, -1): north.
    /// </summary>
    public static (int X, int Y, int Z) Direction(int yaw, int pitch)
    {
        var cosPitch = Cos(pitch);
        return (Multiply(cosPitch, Sin(yaw)), Sin(pitch), Multiply(cosPitch, -Cos(yaw)));
    }

    /// <summary>The product of two fixed-point numbers (<see cref="One"/> = 1.0), rounded half up.</summary>
    public static int Multiply(int a, int b) => (int)((((long)a * b) + (One / 2)) >> 16);

    private static int[] BuildQuarterSine()
    {
        var table = new int[QuarterTurn + 1];
        for (var step = 1; step < QuarterTurn; step++)
        {
            var x = ((step * PiOverTwoQ30) + (QuarterTurn / 2)) / QuarterTurn; // the angle in radians, Q30
            var term = x;
            var sum = x;
            for (var k = 1; k <= 7; k++)
            {
                term = -(((term * x) >> FractionBits) * x >> FractionBits) / ((2 * k) * ((2 * k) + 1));
                sum += term;
            }

            var value = (int)((sum + (1L << (FractionBits - 17))) >> (FractionBits - 16));
            table[step] = Math.Clamp(value, 0, One);
        }

        table[0] = 0;
        table[QuarterTurn] = One;
        return table;
    }
}


