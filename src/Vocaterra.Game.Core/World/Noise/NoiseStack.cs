using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Noise;

/// <summary>
/// The noise recipes the climate and the height model are made of, over <see cref="GradientNoise"/> (B01): fractal Brownian motion (<see cref="Fbm"/>), ridged noise
/// (<see cref="Ridged"/>) and a domain warp (<see cref="Warp"/>). Octave k has cell <c>baseCell / 2^k</c> and weight <c>1 / 2^k</c>; the weights are normalised, so
/// the result keeps the range of one octave. Integer only, pure, and safe from any thread.
/// </summary>
internal sealed class NoiseStack
{
    private readonly GradientNoise _noise;
    private readonly GradientNoise _warpX;
    private readonly GradientNoise _warpZ;

    /// <summary>Creates the stack of one field of one world seed.</summary>
    /// <param name="seed">The world seed.</param>
    /// <param name="field">The field's name: part of every stream it draws, so two fields never share values.</param>
    public NoiseStack(ulong seed, string field)
    {
        ArgumentNullException.ThrowIfNull(field);
        _noise = new GradientNoise(seed, field);
        _warpX = new GradientNoise(seed, field + "/warp-x");
        _warpZ = new GradientNoise(seed, field + "/warp-z");
    }

    /// <summary>Fractal noise: <paramref name="octaves"/> octaves of gradient noise added with halving cells and weights, -4 096 to 4 096.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="baseCell">The first octave's cell in blocks.</param>
    /// <param name="octaves">The number of octaves (at least 1).</param>
    public int Fbm(int x, int z, int baseCell, int octaves)
    {
        long sum = 0;
        long total = 0;
        for (var o = 0; o < octaves; o++)
        {
            var weight = IntMath.One >> o;
            sum += (long)_noise.Sample(o, x, z, Math.Max(1, baseCell >> o)) * weight;
            total += weight;
        }

        return (int)(sum / total);
    }

    /// <summary>
    /// Ridged noise: each octave is folded about zero (<c>1 - |n|</c>, squared) so the ridges lie along the noise's zero lines, 0 to 4 096: the ranges' crests and the
    /// rivers' courses.
    /// </summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="baseCell">The first octave's cell in blocks.</param>
    /// <param name="octaves">The number of octaves (at least 1).</param>
    /// <param name="round">The thousandths over which the crest of each ridge is rounded: the fold <c>|n|</c> becomes <c>sqrt(n^2 + r^2) - r</c>, smooth at the crest (0: sharp).</param>
    public int Ridged(int x, int z, int baseCell, int octaves, int round = 0)
    {
        var r = (long)round * IntMath.One / 1000;
        long sum = 0;
        long total = 0;
        for (var o = 0; o < octaves; o++)
        {
            var weight = IntMath.One >> o;
            var n = _noise.Sample(o, x, z, Math.Max(1, baseCell >> o));
            var ridge = IntMath.One - (r == 0 ? Math.Abs(n) : (int)(IntMath.ISqrt(((long)n * n) + (r * r)) - r));
            sum += ((long)ridge * ridge / IntMath.One) * weight;
            total += weight;
        }

        return (int)(sum / total);
    }

    /// <summary>
    /// A chain (B02b round 3): one octave of noise measured as the distance, in blocks, to its zero line (<c>|n| / |grad n|</c>, the gradient taken over a few blocks), turned into a straight slope that is 4 096 on the line and 0 at
    /// <paramref name="halfBlocks"/> blocks from it, so the band it makes has the same width everywhere. The crest is rounded over <paramref name="roundBlocks"/> blocks.
    /// </summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="cell">The noise's cell in blocks.</param>
    /// <param name="halfBlocks">The distance from the line at which the chain is gone.</param>
    /// <param name="roundBlocks">The blocks over which the crest is rounded.</param>
    public int Chain(int x, int z, int cell, int halfBlocks, int roundBlocks)
    {
        var e = Math.Clamp(cell / 16, 8, 512); // wide enough that the noise's whole-number steps do not show in the slope
        long n = _noise.Sample(0, x, z, cell);
        long gx = _noise.Sample(0, x + e, z, cell) - _noise.Sample(0, x - e, z, cell);
        long gz = _noise.Sample(0, x, z + e, cell) - _noise.Sample(0, x, z - e, cell);
        var grad = IntMath.ISqrt((gx * gx) + (gz * gz)); // the noise's change over 2e blocks
        var distance = grad == 0 ? halfBlocks : Math.Min(halfBlocks, Math.Abs(n) * 2 * e / grad);
        if (roundBlocks > 0)
        {
            distance = IntMath.ISqrt((distance * distance) + ((long)roundBlocks * roundBlocks)) - roundBlocks;
        }

        return (int)Math.Max(0, (halfBlocks - distance) * IntMath.One / halfBlocks);
    }

    /// <summary>The point a domain warp moves (x, z) to: each axis shifted by up to <paramref name="amplitudeBlocks"/> blocks by a smooth field of cell <paramref name="cell"/>.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="cell">The warp field's cell in blocks.</param>
    /// <param name="amplitudeBlocks">The farthest shift in blocks.</param>
    public (int X, int Z) Warp(int x, int z, int cell, int amplitudeBlocks)
    {
        if (amplitudeBlocks == 0)
        {
            return (x, z);
        }

        var dx = (int)((long)_warpX.Sample(0, x, z, cell) * amplitudeBlocks / IntMath.One);
        var dz = (int)((long)_warpZ.Sample(0, x, z, cell) * amplitudeBlocks / IntMath.One);
        return (x + dx, z + dz);
    }
}
