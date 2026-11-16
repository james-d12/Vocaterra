using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World.Underground;

/// <summary>
/// One three-dimensional fractal noise field of the underground (B11): <see cref="GradientNoise"/> over <c>octaves</c> octaves whose cells and weights halve, keyed by the world seed and the
/// field's name, with its own cell across and up, -4 096 to 4 096. The caves and the rock blobs sample it on a coarse lattice and interpolate. Pure and immutable.
/// </summary>
internal sealed class NoiseField3
{
    private readonly GradientNoise _noise;
    private readonly int _cell;
    private readonly int _cellY;
    private readonly int _octaves;
    private readonly int _totalWeight;

    /// <summary>Creates the field.</summary>
    /// <param name="seed">The world seed.</param>
    /// <param name="name">The field's name: part of its stream, so two fields never share values.</param>
    /// <param name="cell">The first octave's cell across, in blocks.</param>
    /// <param name="cellY">The first octave's cell up, in blocks.</param>
    /// <param name="octaves">The octaves (at least 1).</param>
    public NoiseField3(ulong seed, string name, int cell, int cellY, int octaves)
    {
        _noise = new GradientNoise(seed, name);
        _cell = cell;
        _cellY = cellY;
        _octaves = octaves;
        for (var o = 0; o < octaves; o++)
        {
            _totalWeight += IntMath.One >> o;
        }
    }

    /// <summary>The field at a block.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public int At(int x, int y, int z)
    {
        long sum = 0;
        for (var o = 0; o < _octaves; o++)
        {
            sum += (long)_noise.Sample(o, x, y, z, Math.Max(1, _cell >> o), Math.Max(1, _cellY >> o)) * (IntMath.One >> o);
        }

        return (int)(sum / _totalWeight);
    }
}
