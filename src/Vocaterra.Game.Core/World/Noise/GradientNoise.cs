using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Noise;

/// <summary>
/// Integer gradient (Perlin) noise in two and three dimensions with the quintic fade, keyed by the world seed and a pass name like <see cref="TerrainNoise"/>. Value noise
/// shows square-ish contours once its cells are hundreds of blocks wide; gradient noise does not, so the climate and the height model use this. Every value is a function of
/// the key, the octave and the coordinates only: negative coordinates and coordinates near plus and minus 29 000 behave like any other, and no sample reads state. The result
/// runs from -4 096 to 4 096 (a fixed-point fraction of <see cref="IntMath.One"/>) and is zero on the lattice points.
/// </summary>
internal sealed class GradientNoise
{
    private const ulong Golden = 0x9E3779B97F4A7C15UL;
    private const ulong Second = 0xC2B2AE3D27D4EB4FUL;
    private const ulong Third = 0x165667B19E3779F9UL;
    private const ulong Fourth = 0x27D4EB2F165667C5UL;
    private const int Unit16 = 65536;

    // Sixteen unit directions of the plane, as fixed point (cos and sin of k * 22.5 degrees times 4 096), so no direction of the lattice is preferred.
    private static readonly int[] Gx = [4096, 3784, 2896, 1567, 0, -1567, -2896, -3784, -4096, -3784, -2896, -1567, 0, 1567, 2896, 3784];
    private static readonly int[] Gz = [0, 1567, 2896, 3784, 4096, 3784, 2896, 1567, 0, -1567, -2896, -3784, -4096, -3784, -2896, -1567];

    // The twelve edge directions of the cube, then four of them again to make sixteen (Perlin's improved noise).
    private static readonly int[] Cx = [1, -1, 1, -1, 1, -1, 1, -1, 0, 0, 0, 0, 1, -1, 0, 0];
    private static readonly int[] Cy = [1, 1, -1, -1, 0, 0, 0, 0, 1, -1, 1, -1, 1, 1, -1, -1];
    private static readonly int[] Cz = [0, 0, 0, 0, 1, 1, -1, -1, 1, 1, -1, -1, 0, 0, 1, 1];

    private readonly ulong _key;

    /// <summary>Creates the noise of one pass of one world seed.</summary>
    /// <param name="seed">The world seed.</param>
    /// <param name="pass">The pass (or field) name: part of the stream, so two names never share values.</param>
    public GradientNoise(ulong seed, string pass) => _key = WorldRng.For(seed, pass).NextUInt64();

    /// <summary>The noise of one octave at a block in a plane, smooth over <paramref name="cell"/> blocks: -4 096 to 4 096.</summary>
    /// <param name="octave">The octave number (a salt: another octave is another lattice).</param>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="cell">The lattice spacing in blocks (positive).</param>
    public int Sample(int octave, int x, int z, int cell)
    {
        var ix = IntMath.DivDown(x, cell);
        var iz = IntMath.DivDown(z, cell);
        var fx = (int)((long)IntMath.Mod(x, cell) * Unit16 / cell);
        var fz = (int)((long)IntMath.Mod(z, cell) * Unit16 / cell);
        var u = Fade(fx) >> 4;
        var v = Fade(fz) >> 4;
        var a = Dot2(octave, ix, iz, fx, fz);
        var b = Dot2(octave, ix + 1, iz, fx - Unit16, fz);
        var c = Dot2(octave, ix, iz + 1, fx, fz - Unit16);
        var d = Dot2(octave, ix + 1, iz + 1, fx - Unit16, fz - Unit16);
        var value = IntMath.Lerp(IntMath.Lerp(a, b, u), IntMath.Lerp(c, d, u), v);
        return Math.Clamp((value * 5793) >> 12, -IntMath.One, IntMath.One); // unit gradients peak at 0.7071; 5793 / 4096 is the square root of two
    }

    /// <summary>The noise of one octave at a block in space: -4 096 to 4 096, smooth over <paramref name="cellXz"/> blocks across and <paramref name="cellY"/> blocks up.</summary>
    /// <param name="octave">The octave number (a salt).</param>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="cellXz">The lattice spacing across, in blocks.</param>
    /// <param name="cellY">The lattice spacing up, in blocks.</param>
    public int Sample(int octave, int x, int y, int z, int cellXz, int cellY)
    {
        var ix = IntMath.DivDown(x, cellXz);
        var iy = IntMath.DivDown(y, cellY);
        var iz = IntMath.DivDown(z, cellXz);
        var fx = (int)((long)IntMath.Mod(x, cellXz) * Unit16 / cellXz);
        var fy = (int)((long)IntMath.Mod(y, cellY) * Unit16 / cellY);
        var fz = (int)((long)IntMath.Mod(z, cellXz) * Unit16 / cellXz);
        var u = Fade(fx) >> 4;
        var v = Fade(fy) >> 4;
        var w = Fade(fz) >> 4;
        var x00 = IntMath.Lerp(Dot3(octave, ix, iy, iz, fx, fy, fz), Dot3(octave, ix + 1, iy, iz, fx - Unit16, fy, fz), u);
        var x10 = IntMath.Lerp(Dot3(octave, ix, iy + 1, iz, fx, fy - Unit16, fz), Dot3(octave, ix + 1, iy + 1, iz, fx - Unit16, fy - Unit16, fz), u);
        var x01 = IntMath.Lerp(Dot3(octave, ix, iy, iz + 1, fx, fy, fz - Unit16), Dot3(octave, ix + 1, iy, iz + 1, fx - Unit16, fy, fz - Unit16), u);
        var x11 = IntMath.Lerp(Dot3(octave, ix, iy + 1, iz + 1, fx, fy - Unit16, fz - Unit16), Dot3(octave, ix + 1, iy + 1, iz + 1, fx - Unit16, fy - Unit16, fz - Unit16), u);
        var value = IntMath.Lerp(IntMath.Lerp(x00, x10, v), IntMath.Lerp(x01, x11, v), w);
        return Math.Clamp(value, -IntMath.One, IntMath.One);
    }

    // The quintic 6t^5 - 15t^4 + 10t^3 on 0..65 536: no slope and no curvature at the lattice points, so no creases show.
    private static int Fade(int t)
    {
        long t1 = t;
        var t2 = t1 * t1 >> 16;
        var t3 = t2 * t1 >> 16;
        var inner = ((t1 * ((6 * t1) - (15L * Unit16))) >> 16) + (10L * Unit16);
        return (int)(t3 * inner >> 16);
    }

    // The gradient at a lattice point dotted with the offset (16-bit fixed point) from it: 4 096 is a dot product of 1.
    private int Dot2(int octave, int ix, int iz, int dx, int dz)
    {
        var g = (int)(Hash(octave, ix, 0, iz) >> 60);
        return (int)((((long)Gx[g] * dx) + ((long)Gz[g] * dz)) >> 16);
    }

    private int Dot3(int octave, int ix, int iy, int iz, int dx, int dy, int dz)
    {
        var g = (int)(Hash(octave, ix, iy, iz) >> 60);
        return ((Cx[g] * dx) + (Cy[g] * dy) + (Cz[g] * dz)) >> 4;
    }

    private ulong Hash(int octave, int ix, int iy, int iz) =>
        WorldRng.Mix(_key + ((ulong)(uint)ix * Golden) + ((ulong)(uint)iz * Second) + ((ulong)(uint)octave * Third) + ((ulong)(uint)iy * Fourth));
}
