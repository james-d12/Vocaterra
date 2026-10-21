namespace Vocaterra.Simulation.Generation;

/// <summary>
/// Integer value noise. Lattice values come from <see cref="WorldRng.Mix"/>, interpolation is a cubic smoothstep in fixed point, so the
/// results are identical on every platform and runtime (no floating point). Every sample is in [0, <see cref="Max"/>].
/// </summary>
public static class ValueNoise
{
    /// <summary>The largest sample value (16 bits).</summary>
    public const int Max = 65535;

    private const int FractionBits = 12;
    private const int FractionOne = 1 << FractionBits;

    /// <summary>One-dimensional noise at column <paramref name="x"/>, with one lattice point every <paramref name="wavelength"/> columns.</summary>
    public static int Sample1D(ulong seed, int x, int wavelength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(wavelength);
        var cell = FloorDiv(x, wavelength);
        var t = Smooth(((x - (cell * wavelength)) << FractionBits) / wavelength);
        return Lerp(Lattice(seed, cell, 0), Lattice(seed, cell + 1, 0), t);
    }

    /// <summary>Two-dimensional noise (bilinear over a smoothed fraction) with a square lattice of <paramref name="wavelength"/> tiles.</summary>
    public static int Sample2D(ulong seed, int x, int y, int wavelength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(wavelength);
        var cx = FloorDiv(x, wavelength);
        var cy = FloorDiv(y, wavelength);
        var tx = Smooth(((x - (cx * wavelength)) << FractionBits) / wavelength);
        var ty = Smooth(((y - (cy * wavelength)) << FractionBits) / wavelength);
        var top = Lerp(Lattice(seed, cx, cy), Lattice(seed, cx + 1, cy), tx);
        var bottom = Lerp(Lattice(seed, cx, cy + 1), Lattice(seed, cx + 1, cy + 1), tx);
        return Lerp(top, bottom, ty);
    }

    /// <summary>
    /// Fractal one-dimensional noise: <paramref name="octaves"/> layers, each half the wavelength and half the weight of the one before
    /// (the wavelength never drops below 1). The result is normalised to [0, <see cref="Max"/>].
    /// </summary>
    public static int Fractal1D(ulong seed, int x, int wavelength, int octaves)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(octaves);
        long sum = 0;
        long totalWeight = 0;
        for (var i = 0; i < octaves; i++)
        {
            var weight = 1L << (octaves - 1 - i);
            var octaveSeed = WorldRng.Mix(seed + (ulong)(i + 1));
            sum += weight * Sample1D(octaveSeed, x, Math.Max(1, wavelength >> i));
            totalWeight += weight;
        }

        return (int)(sum / totalWeight);
    }

    private static int Lattice(ulong seed, int x, int y) =>
        (int)(WorldRng.Mix(seed ^ (WorldRng.Mix((ulong)(uint)x | ((ulong)(uint)y << 32)) * 0x9E3779B97F4A7C15UL)) >> 48);

    /// <summary>Smoothstep 3t^2 - 2t^3 on a 12-bit fraction.</summary>
    private static int Smooth(int t) => (int)(((long)t * t * ((3L * FractionOne) - (2L * t))) >> (2 * FractionBits));

    private static int Lerp(int a, int b, int t) => a + (int)(((long)(b - a) * t) >> FractionBits);

    private static int FloorDiv(int a, int b)
    {
        var q = a / b;
        return (a % b != 0 && a < 0) ? q - 1 : q;
    }
}
