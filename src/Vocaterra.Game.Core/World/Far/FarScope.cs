using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.World.Far;

/// <summary>
/// The work one far call shares between its columns (B36, D-game-34 item 61): the climate and the height model's shape of each column asked for, kept in arrays for the call, and the climate's lattice corners, so that a column read twice (by
/// the cliff test, <c>TryRiver</c> and the water's levelling, which each look at the neighbours of a sample) is computed once and a lattice point four blocks wide is evaluated once and not for every column that leans on it. Every number is the
/// one the sampler computes without a scope (the same integer arithmetic over the same lattice corners; the digests of the far sections are pinned by a test). Owned by one call on one thread and dropped with it: nothing is static, and nothing is
/// shared between calls or threads.
/// </summary>
internal sealed class FarScope
{
    private readonly GenerationWorld _world;
    private readonly ClimateSampler? _lattice; // the free world's climate, whose lattice corners are shared; null when the climate is some other kind (read column by column)
    private readonly RegionBlend? _blend;
    private readonly int _cell;
    private readonly Keys _columnKeys;
    private readonly byte[] _level; // per column slot: 0 nothing, 1 the climate, 2 the climate and the shape
    private readonly ClimateSample[] _climate;
    private readonly ColumnShape[] _shape;
    private readonly Keys _cornerKeys;
    private readonly bool[] _cornerDone;
    private readonly ClimateSample[] _corner;

    /// <summary>Creates the scope of one call.</summary>
    /// <param name="world">The world.</param>
    /// <param name="samples">How many columns the call samples (the tables are sized from it; a call that reads more than they hold computes the surplus unshared, with the same result).</param>
    public FarScope(GenerationWorld world, int samples)
    {
        _world = world;
        var capacity = Keys.CapacityFor(samples);
        _columnKeys = new Keys(capacity);
        _level = new byte[capacity];
        _climate = new ClimateSample[capacity];
        _shape = new ColumnShape[capacity];
        _cornerKeys = new Keys(capacity);
        _cornerDone = new bool[capacity];
        _corner = new ClimateSample[capacity];
        _cell = world.Data.Climate.LatticeBlocks;
        switch (world.Climate)
        {
            case ClimateSampler plain:
                _lattice = plain;
                break;
            case BlendedClimateSampler { Inner: ClimateSampler inner }:
                _lattice = inner;
                _blend = world.Regions;
                break;
        }
    }

    /// <summary>The climate of a column, as <see cref="IClimateSampler.At"/> gives it.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public ClimateSample Climate(int x, int z)
    {
        var i = _columnKeys.Find(x, z);
        if (i < 0)
        {
            return Compute(x, z);
        }

        if (_level[i] == 0)
        {
            _climate[i] = Compute(x, z);
            _level[i] = 1;
        }

        return _climate[i];
    }

    /// <summary>The height model's shape of a column, from the column's climate.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public ColumnShape Shape(int x, int z)
    {
        var i = _columnKeys.Find(x, z);
        if (i < 0)
        {
            return _world.Height.Shape(x, z, Compute(x, z));
        }

        if (_level[i] < 2)
        {
            if (_level[i] == 0)
            {
                _climate[i] = Compute(x, z);
            }

            _shape[i] = _world.Height.Shape(x, z, _climate[i]);
            _level[i] = 2;
        }

        return _shape[i];
    }

    // The column's climate from the lattice corners round it: the same blend of the same four corners as the sampler's, and the regions' bias over it when there is a blend.
    private ClimateSample Compute(int x, int z)
    {
        if (_lattice is null)
        {
            return _world.Climate.At(x, z);
        }

        var ix = IntMath.DivDown(x, _cell);
        var iz = IntMath.DivDown(z, _cell);
        var a = Corner(ix, iz);
        var b = Corner(ix + 1, iz);
        var c = Corner(ix, iz + 1);
        var d = Corner(ix + 1, iz + 1);
        var fx = IntMath.Mod(x, _cell) * IntMath.One / _cell;
        var fz = IntMath.Mod(z, _cell) * IntMath.One / _cell;
        var sample = new ClimateSample(
            Blend(a.Temperature, b.Temperature, c.Temperature, d.Temperature, fx, fz),
            Blend(a.Humidity, b.Humidity, c.Humidity, d.Humidity, fx, fz),
            Blend(a.Continentalness, b.Continentalness, c.Continentalness, d.Continentalness, fx, fz),
            Blend(a.Erosion, b.Erosion, c.Erosion, d.Erosion, fx, fz),
            Blend(a.Ridges, b.Ridges, c.Ridges, d.Ridges, fx, fz),
            Blend(a.River, b.River, c.River, d.River, fx, fz));
        return _blend is null ? sample : _blend.Bias(x, z, sample);
    }

    private ClimateSample Corner(int ix, int iz)
    {
        var i = _cornerKeys.Find(ix, iz);
        if (i < 0)
        {
            return _lattice!.AtLatticePoint(ix * _cell, iz * _cell);
        }

        if (!_cornerDone[i])
        {
            _corner[i] = _lattice!.AtLatticePoint(ix * _cell, iz * _cell);
            _cornerDone[i] = true;
        }

        return _corner[i];
    }

    private static int Blend(int a, int b, int c, int d, int fx, int fz) => IntMath.Lerp(IntMath.Lerp(a, b, fx), IntMath.Lerp(c, d, fx), fz);

    // The slots of a table of integer pairs, open addressing in fixed arrays (fields stay readonly: the world's folders hold no mutable state): Find gives a key's slot, adding the key when it is new, or -1 when the table is half full (the caller then goes without).
    private sealed class Keys
    {
        private readonly int[] _x;
        private readonly int[] _z;
        private readonly bool[] _used;
        private readonly int[] _count = new int[1];

        public Keys(int capacity)
        {
            _x = new int[capacity];
            _z = new int[capacity];
            _used = new bool[capacity];
        }

        public static int CapacityFor(int samples) => (int)Math.Clamp(System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, samples) * 6u), 1024u, 1u << 17);

        public int Find(int x, int z)
        {
            var mask = _used.Length - 1;
            var h = ((uint)x * 0x9E3779B1u) ^ ((uint)z * 0x85EBCA6Bu);
            h ^= h >> 15;
            var i = (int)(h & (uint)mask);
            while (_used[i])
            {
                if (_x[i] == x && _z[i] == z)
                {
                    return i;
                }

                i = (i + 1) & mask;
            }

            if ((_count[0] + 1) * 2 > _used.Length)
            {
                return -1;
            }

            _x[i] = x;
            _z[i] = z;
            _used[i] = true;
            _count[0]++;
            return i;
        }
    }
}
