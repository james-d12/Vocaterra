using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World.Climate;

/// <summary>
/// The continentalness as continents (B02b redo, D-game-34 item 20): the plane is cut into jittered cells (a Voronoi diagram of one point a cell), a share of the cells (<see cref="ContinentSpec.SeaCells"/>) are open sea and
/// the rest are land, and a channel of <see cref="ContinentSpec.Channel"/> blocks each side of every cell border is sea. The value is the signed distance from the coast in blocks (positive inland, negative at sea),
/// roughened by a low noise that is smaller than the channel, so two land cells are never joined by land and the sea is one connected ocean whatever the seed; scaled so that
/// <see cref="ContinentSpec.ScaleBlocks"/> blocks are 1 000 thousandths. The land cells are the continents (thousands of blocks across), the shelf is the distance to the shore, and a sea cell is deepest in the middle. Pure and
/// integer only: the cell's point and kind are hashes of the seed and the cell.
/// </summary>
internal sealed class ContinentField
{
    private readonly ContinentSpec _spec;
    private readonly NoiseSpec _warp;
    private readonly NoiseStack _stack;
    private readonly NoiseStack _coast;
    private readonly NoiseStack _width;
    private readonly NoiseStack _rough;
    private readonly NoiseStack _coast2;
    private readonly NoiseStack _coast3;
    private readonly ulong _key;

    /// <summary>Creates the field of one world seed.</summary>
    /// <param name="seed">The world seed.</param>
    /// <param name="spec">The continents' numbers.</param>
    /// <param name="warp">The continentalness's domain warp, which bends the cell borders into coasts.</param>
    public ContinentField(ulong seed, ContinentSpec spec, NoiseSpec warp)
    {
        _spec = spec;
        _warp = warp;
        _stack = new NoiseStack(seed, "climate/continents");
        _coast = new NoiseStack(seed, "climate/continents/coast");
        _width = new NoiseStack(seed, "climate/continents/width");
        _rough = new NoiseStack(seed, "climate/continents/rough");
        _coast2 = new NoiseStack(seed, "climate/continents/coast2");
        _coast3 = new NoiseStack(seed, "climate/continents/coast3");
        _key = WorldRng.For(seed, "climate/continents/cells").NextUInt64() ^ Mix((ulong)(uint)spec.Salt);
    }

    /// <summary>The continentalness at a block, fixed point (4 096 is 1 000 thousandths).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public int At(int x, int z)
    {
        var (ux, uz) = _stack.Warp(x, z, _warp.WarpCell, _warp.WarpBlocks);
        var (vx, vz) = _coast.Warp(ux, uz, _spec.CoastCell, _spec.CoastBlocks);
        var (sx, sz) = _coast2.Warp(vx, vz, Math.Max(16, _spec.CoastCell / 2), _spec.CoastBlocks / 2); // a bend of half the size and reach (round 3: no border runs straight for long)
        var (tx, tz) = _width.Warp(sx, sz, Math.Max(16, _spec.CoastCell / 4), _spec.CoastBlocks / 3); // a finer bend on top: bays and capes
        var (wx, wz) = _coast3.Warp(tx, tz, Math.Max(16, _spec.CoastCell / 8), _spec.CoastBlocks / 6); // and a finer still
        var size = _spec.Cell;
        int cx = IntMath.DivDown(wx, size), cz = IntMath.DivDown(wz, size);
        Span<long> px = stackalloc long[9];
        Span<long> pz = stackalloc long[9];
        Span<ulong> hash = stackalloc ulong[9];
        var (best, bestD2) = (-1, long.MaxValue);
        for (var j = 0; j < 3; j++)
        {
            for (var i = 0; i < 3; i++)
            {
                var k = (j * 3) + i;
                var (ax, az) = (cx + i - 1, cz + j - 1);
                var h = Hash(ax, az);
                hash[k] = h;
                px[k] = ((long)ax * size) + (size / 2) + Jitter(h, 0);
                pz[k] = ((long)az * size) + (size / 2) + Jitter(h, 1);
                long dx = wx - px[k], dz = wz - pz[k];
                var d2 = (dx * dx) + (dz * dz);
                if (d2 < bestD2)
                {
                    (best, bestD2) = (k, d2);
                }
            }
        }

        // The distance to the cell's border: the least over the other cells of the distance to the bisector between the two points.
        var border = long.MaxValue;
        for (var k = 0; k < 9; k++)
        {
            if (k == best)
            {
                continue;
            }

            long dx = wx - px[k], dz = wz - pz[k];
            long gx = px[k] - px[best], gz = pz[k] - pz[best];
            var gap = Math.Max(1, IntMath.ISqrt((gx * gx) + (gz * gz)));
            border = Math.Min(border, (((dx * dx) + (dz * dz)) - bestD2) / (2 * gap));
        }

        var land = !IsSea(cx + (best % 3) - 1, cz + (best / 3) - 1);
        var wide = Math.Clamp((IntMath.One / 2) + ((long)_width.Fbm(wx, wz, _spec.WidthCell, 3) * 2), 0, IntMath.One); // 0 to One, spread over the noise's usual range
        var channel = _spec.ChannelMin + ((_spec.Channel - _spec.ChannelMin) * wide / IntMath.One);
        var value = land ? border - channel : -(border + channel);
        if (_spec.RoughBlocks > 0)
        {
            var limit = Math.Max(0, channel - (_spec.ChannelMin / 2)); // never so far that the channel closes: the land cells stay apart by at least half the least channel each side
            value += Math.Clamp((long)_rough.Fbm(wx, wz, _spec.RoughCell, _spec.RoughOctaves) * _spec.RoughBlocks / IntMath.One, -limit, limit); // the coast wanders at every scale from the first octave down
        }

        return (int)Math.Clamp(value * IntMath.One / _spec.ScaleBlocks, -IntMath.One, IntMath.One);
    }

    // Whether a cell is open sea: each block of SeaBlock x SeaBlock cells holds SeaPerBlock sea cells, picked by a partial shuffle of the block's cells keyed by the block.
    private bool IsSea(int cx, int cz)
    {
        var n = _spec.SeaBlock;
        int bx = IntMath.DivDown(cx, n), bz = IntMath.DivDown(cz, n);
        var mine = (IntMath.Mod(cz, n) * n) + IntMath.Mod(cx, n);
        Span<int> order = stackalloc int[16];
        var cells = n * n;
        for (var i = 0; i < cells; i++)
        {
            order[i] = i;
        }

        for (var t = 0; t < _spec.SeaPerBlock; t++)
        {
            var pick = t + (int)(Mix(_key ^ Mix(((ulong)(uint)bx << 32) | (uint)bz) ^ (0x1234UL + (ulong)t)) % (ulong)(cells - t));
            (order[t], order[pick]) = (order[pick], order[t]);
            if (order[t] == mine)
            {
                return true;
            }
        }

        return false;
    }

    private ulong Hash(int cx, int cz) => Mix(_key ^ Mix(((ulong)(uint)cx << 32) | (uint)cz));

    // The point's offset from the cell's centre along one axis: up to half the cell times the jitter.
    private long Jitter(ulong hash, int axis)
    {
        var unit = (long)((Mix(hash ^ (axis == 0 ? 0xA5A5A5A5UL : 0x5A5A5A5AUL)) >> 40) & 0xFFFFFF) - 0x800000; // -8 388 608 .. 8 388 607
        return unit * _spec.Cell / 0x1000000 * _spec.Jitter / 1000;
    }

    private static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
