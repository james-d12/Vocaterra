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
    public int Ridged(int x, int z, int baseCell, int octaves)
    {
        long sum = 0;
        long total = 0;
        for (var o = 0; o < octaves; o++)
        {
            var weight = IntMath.One >> o;
            var n = _noise.Sample(o, x, z, Math.Max(1, baseCell >> o));
            var ridge = IntMath.One - Math.Abs(n);
            sum += ((long)ridge * ridge / IntMath.One) * weight;
            total += weight;
        }

        return (int)(sum / total);
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
