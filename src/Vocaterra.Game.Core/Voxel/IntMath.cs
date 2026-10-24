namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// Integer helpers for the terrain generator. Everything is exact integer arithmetic (no floating point), so the same seed gives the same
/// terrain on every machine. Fractions are fixed point: a weight or a blend factor runs 0 to <see cref="One"/> (4 096).
/// </summary>
internal static class IntMath
{
    /// <summary>1.0 in the fixed-point weights.</summary>
    public const int One = 4096;

    /// <summary>The floor division of a by a positive b (rounds towards negative infinity).</summary>
    public static int DivDown(int a, int b)
    {
        var q = a / b;
        return (a % b != 0 && a < 0) ? q - 1 : q;
    }

    /// <summary>The remainder of a by a positive b, never negative.</summary>
    public static int Mod(int a, int b)
    {
        var m = a % b;
        return m < 0 ? m + b : m;
    }

    /// <summary>The floor of the square root of a non-negative number (Newton's steps on integers).</summary>
    public static long ISqrt(long value)
    {
        if (value < 2)
        {
            return value < 0 ? 0 : value;
        }

        var root = value;
        var next = (root + 1) / 2;
        while (next < root)
        {
            root = next;
            next = (root + (value / root)) / 2;
        }

        return root;
    }

    /// <summary>The smoothstep of a weight: 0 to <see cref="One"/> in, 0 to <see cref="One"/> out, with a zero slope at both ends.</summary>
    public static int Smooth(int t)
    {
        var c = Math.Clamp(t, 0, One);
        return (int)(((long)c * c * ((3 * One) - (2 * c))) >> 24);
    }

    /// <summary>The weight of <paramref name="value"/> on the way from <paramref name="from"/> (weight 0) to <paramref name="to"/> (weight <see cref="One"/>), smoothed; <paramref name="from"/> may be the larger.</summary>
    public static int Ramp(int value, int from, int to)
    {
        if (from == to)
        {
            return value >= to ? One : 0;
        }

        var t = (int)(((long)(value - from) * One) / (to - from));
        return Smooth(t);
    }

    /// <summary>The linear blend of a and b by a weight (0 gives a, <see cref="One"/> gives b).</summary>
    public static int Lerp(int a, int b, int weight) => a + (int)(((long)(b - a) * weight) / One);

    /// <summary>
    /// One Catmull-Rom step between p1 and p2 (p0 and p3 are the points either side) at a weight, clamped to the range of p1 and p2 so the curve
    /// never overshoots the corners it passes between.
    /// </summary>
    public static int CatmullRom(int p0, int p1, int p2, int p3, int t)
    {
        long a = 2L * p1;
        long b = p2 - p0;
        long c = (2L * p0) - (5L * p1) + (4L * p2) - p3;
        long d = -p0 + (3L * p1) - (3L * p2) + p3;
        long t2 = (long)t * t;
        long t3 = t2 * t;
        const long O = One;
        var value = ((a * O * O * O) + (b * t * O * O) + (c * t2 * O) + (d * t3)) / (2 * O * O * O);
        var lo = Math.Min(p1, p2);
        var hi = Math.Max(p1, p2);
        return (int)Math.Clamp(value, lo, hi);
    }
}
