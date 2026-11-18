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
    private readonly Grid<ColumnEntry> _columns = new(1024);
    private readonly Grid<ClimateSample> _corners = new(512);

    public FarScope(GenerationWorld world)
    {
        _world = world;
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
        ref var entry = ref _columns.Find(x, z).Value;
        if (entry.Level == 0)
        {
            entry.Climate = Compute(x, z);
            entry.Level = 1;
        }

        return entry.Climate;
    }

    /// <summary>The height model's shape of a column, from the column's climate.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public ColumnShape Shape(int x, int z)
    {
        ref var entry = ref _columns.Find(x, z).Value;
        if (entry.Level < 2)
        {
            if (entry.Level == 0)
            {
                entry.Climate = Compute(x, z);
            }

            entry.Shape = _world.Height.Shape(x, z, entry.Climate);
            entry.Level = 2;
        }

        return entry.Shape;
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
        ref var slot = ref _corners.Find(ix, iz);
        if (!slot.Filled)
        {
            slot.Value = _lattice!.AtLatticePoint(ix * _cell, iz * _cell);
            slot.Filled = true;
        }

        return slot.Value;
    }

    private static int Blend(int a, int b, int c, int d, int fx, int fz) => IntMath.Lerp(IntMath.Lerp(a, b, fx), IntMath.Lerp(c, d, fx), fz);

    private struct ColumnEntry
    {
        public int Level; // 0 nothing, 1 the climate, 2 the climate and the shape
        public ClimateSample Climate;
        public ColumnShape Shape;
    }

    private struct Slot<T>
    {
        public int X;
        public int Z;
        public bool Used;
        public bool Filled;
        public T Value;
    }

    // An open-addressing table from a pair of integers to a value, in one array: the references it hands out stay valid until the next Find that has to add a key (a caller finishes with one before it asks for another).
    private sealed class Grid<T>(int capacity)
        where T : struct
    {
        private Slot<T>[] _slots = new Slot<T>[capacity];
        private int _count;

        public ref Slot<T> Find(int x, int z)
        {
            while (true)
            {
                var mask = _slots.Length - 1;
                var h = ((uint)x * 0x9E3779B1u) ^ ((uint)z * 0x85EBCA6Bu);
                h ^= h >> 15;
                var i = (int)(h & (uint)mask);
                while (_slots[i].Used)
                {
                    if (_slots[i].X == x && _slots[i].Z == z)
                    {
                        return ref _slots[i];
                    }

                    i = (i + 1) & mask;
                }

                if ((_count + 1) * 2 > _slots.Length)
                {
                    Grow();
                    continue;
                }

                _slots[i].X = x;
                _slots[i].Z = z;
                _slots[i].Used = true;
                _count++;
                return ref _slots[i];
            }
        }

        private void Grow()
        {
            var old = _slots;
            _slots = new Slot<T>[old.Length * 2];
            var mask = _slots.Length - 1;
            foreach (var slot in old)
            {
                if (!slot.Used)
                {
                    continue;
                }

                var h = ((uint)slot.X * 0x9E3779B1u) ^ ((uint)slot.Z * 0x85EBCA6Bu);
                h ^= h >> 15;
                var i = (int)(h & (uint)mask);
                while (_slots[i].Used)
                {
                    i = (i + 1) & mask;
                }

                _slots[i] = slot;
            }
        }
    }
}
