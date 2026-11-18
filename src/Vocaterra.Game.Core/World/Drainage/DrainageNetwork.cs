using System.Collections.Concurrent;
using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Drainage;

/// <summary>What the drainage network needs to know of the land at one node (B02c): the level its water would stand at, how far the lake field dents it, and whether it is sea or beyond the network's reach.</summary>
/// <param name="Level16">The surface the water would have at the node, in sixteenths of a block: the smooth ground less the hills, the clearance and the valley's depth (unquantised).</param>
/// <param name="Route16">The surface the water is routed over, in sixteenths of a block: the level moved by the relief noise and dented where the lake field is high (what the flood fills).</param>
/// <param name="Sea">Whether the node is at or under the sea's level: the end of every river.</param>
/// <param name="Excluded">Whether the node lies where no river and no lake may be (a region's margin and the clearance round it): the water runs round it.</param>
internal readonly record struct DrainageNode(int Level16, int Route16, bool Sea, bool Excluded);

/// <summary>The land under the drainage network: one node's level, as a pure function of its block (it is called from any thread).</summary>
internal interface IDrainageSurface
{
    /// <summary>The node at the block (x, z).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    DrainageNode NodeAt(int x, int z);
}

/// <summary>The river at a column (B02c): how deep in the channel the column is, the water's level there and how large the river is.</summary>
/// <param name="Core">The profile across the river, 0 (beyond its reach) to <see cref="IntMath.One"/> (on its centre line), before the squaring that narrows the water.</param>
/// <param name="Level16">The surface of the river's water at the nearest point of its line, in sixteenths of a block (never higher than the water upstream).</param>
/// <param name="Size">How large the river is, 0 (a stream at its source) to <see cref="IntMath.One"/> (the largest): the ground that drains into it.</param>
internal readonly record struct RiverHit(int Core, int Level16, int Size);

/// <summary>The lake at a column (B02c): how deep the water would stand there, and the lake's level.</summary>
/// <param name="Depth16">How far under the lake's surface the dented land lies at the column, in sixteenths of a block (0 beyond the shore): the depth the four nodes round the column have, blended.</param>
/// <param name="Level16">The surface of the lake, in sixteenths of a block: the level of the pass it spills over, one for the whole body (B02d).</param>
internal readonly record struct LakeHit(int Depth16, int Level16);

/// <summary>What the network holds for one node (for the tests and the preview tool).</summary>
/// <param name="Level16">The node's water level after the basins were filled, in sixteenths of a block: a lake body's one level, and no higher than the lake below the nodes that drain into one.</param>
/// <param name="Area">The nodes that drain through it (itself included), at most 65 535.</param>
/// <param name="Next">The direction (0 to 7, east first, clockwise) the water runs from the node, or -1 where it ends (the sea, or the edge of what was read).</param>
/// <param name="River">Whether a river runs through the node.</param>
/// <param name="Lake">Whether the node lies under a lake.</param>
/// <param name="Sea">Whether the node is the sea.</param>
/// <param name="Excluded">Whether the node is outside the network.</param>
internal readonly record struct DrainageNodeInfo(int Level16, int Area, int Next, bool River, bool Lake, bool Sea, bool Excluded);

/// <summary>
/// The drainage network of a world (B02c, D-game-34 item 33): rivers that only fall, from sources in the hills to the sea or a lake. A grid of nodes (<see cref="DrainageSpec.Spacing"/> blocks apart) has the level the water would
/// stand at (<see cref="IDrainageSurface"/>); a priority flood from the sea raises every basin to the level of its outlet and gives every other node a neighbour at its level or lower to run to, so the level never rises along the
/// water's way and a basin is a lake whose outlet is the pass it spills over. The ground that drains through a node is counted; where it passes <see cref="DrainageSpec.SourceArea"/> there is a river, whose width and depth grow with it,
/// and which joins the larger river it runs into.
/// The land is worked out one landmass at a time: when a node of a landmass (the land nodes connected to it, up to the sea that rings it) is first asked for, every node of the landmass is read and flooded together, so the water of a basin
/// that a ring of mountains encloses runs out over its true pass, however far away, and nothing depends on a window or a border. The result is kept in tiles of nodes and shared by every thread; priorities depend on the nodes' places and
/// never on the order they were found in, so the network is the same whichever column asked first. A landmass of more than <see cref="DrainageSpec.MaxNodes"/> nodes (a world with no sea round its land) is cut there and the water runs out at its edge.
/// </summary>
internal sealed class DrainageNetwork
{
    private const int One = IntMath.One;
    private const ulong NodeSalt = 0xA24BAED4963EE407UL;

    // The eight directions the water can run, east first and clockwise; the opposite of d is (d + 4) & 7.
    private static readonly int[] Dx = [1, 1, 0, -1, -1, -1, 0, 1];
    private static readonly int[] Dz = [0, 1, 1, 1, 0, -1, -1, -1];

    private const byte RiverFlag = 1;
    private const byte LakeFlag = 2;
    private const byte SeaFlag = 4;
    private const byte OutsideFlag = 8;
    private const byte BuiltFlag = 16;

    // A node is under a lake, and not just in a pit of the relief noise, where the flood stands this deep (sixteenths of a block) over the surface the water is routed over; and a lake is at least this many nodes
    // (a body of fewer is a puddle: it is left to the river that runs through it).
    private const int LakeMin = 40;
    private const int LakeNodesMin = 9;

    // The nodes are kept in square tiles of this many nodes a side.
    private const int Tile = 128;

    // The widest a river's carve can be, in sixteenths of a block (64 blocks), and how many nodes round a column's own the river lines are looked for in: a reach is at most about 68 blocks long (a node moves 8 blocks, a bow bends it), so
    // one that passes within 64 blocks of a column has an end within about 72 blocks of it; the nodes within `_look` of a column's own node are at least `_look` x spacing less the jitter less the column's place in its cell (spacing less 1) from it on every side (4 for 32-block spacing: 89 blocks).
    private const int MaxRamp16 = 64 * 16;

    private readonly DrainageSpec _spec;
    private readonly IDrainageSurface _surface;
    private readonly ulong _seed;
    private readonly int _sea16;
    private readonly int _spacing;
    private readonly ConcurrentDictionary<long, DrainageCell> _tiles = new();
    private readonly object _build = new(); // B40: only the publishing of a read landmass's nodes
    private readonly ConcurrentDictionary<long, object> _reading = new(); // B40: one lock for each node a landmass is read from, so two landmasses are read at once
    private readonly int _jitter16;
    private readonly int _look;
    private readonly int _minRamp16;
    private readonly int _maxRamp16;

    [ThreadStatic]
    private static CellCache? s_last;

    [ThreadStatic]
    private static int s_noRead;

    /// <summary>Creates the network of one world.</summary>
    /// <param name="spec">The network's numbers.</param>
    /// <param name="seaLevel">The sea level, in blocks.</param>
    /// <param name="seed">The world seed.</param>
    /// <param name="narrowest">The width of the narrowest river, in blocks (<see cref="RiverSpec.Width"/>).</param>
    /// <param name="surface">The land the water runs over.</param>
    public DrainageNetwork(DrainageSpec spec, int seaLevel, ulong seed, int narrowest, IDrainageSurface surface)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(surface);
        _spec = spec;
        _surface = surface;
        _seed = seed;
        _sea16 = seaLevel * 16;
        _spacing = spec.Spacing;
        _jitter16 = spec.Jitter * 16;
        var reach = (3 * spec.Spacing / 2) + (2 * spec.Jitter) + 2; // the longest reach, in blocks, with the bow
        var need = IntMath.ISqrt(((long)MaxRamp16 / 16 * (MaxRamp16 / 16)) + ((long)reach / 2 * (reach / 2))); // an end of a reach that passes within the widest carve of a column is this near it
        _look = (int)((need + spec.Jitter + spec.Spacing - 1 + spec.Spacing - 1) / spec.Spacing);
        _minRamp16 = narrowest * 16;
        _maxRamp16 = spec.WidthMax * 16;
    }

    /// <summary>
    /// Makes the calling thread's <see cref="River"/> and <see cref="Lake"/> find nothing, and read no landmass, until the scope is disposed: a thread that must never wait for a landmass (the game loop's) asks for a column dry and has the landmass
    /// read in the background (<see cref="IsRead"/>, <see cref="Warm"/>).
    /// </summary>
    public static NoReadScope WithoutReading()
    {
        s_noRead++;
        return default;
    }

    /// <summary>The end of a <see cref="WithoutReading"/> scope.</summary>
    public readonly struct NoReadScope : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => s_noRead--;
    }

    /// <summary>
    /// Whether everything a column's river and lake look-up reads is ready: the nodes round it are read, or are sea (or excluded) and so are known at once. When it is not, the column's first look-up would read a landmass (up to a second):
    /// the caller asks for the column <see cref="WithoutReading"/> and calls <see cref="Warm"/> on another thread.
    /// </summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public bool IsRead(int x, int z)
    {
        var (ni, nj) = (IntMath.DivDown(x, _spacing), IntMath.DivDown(z, _spacing));
        for (var dj = -_look; dj <= _look; dj += _look) // no array of the three offsets: the game loop asks this every frame and must allocate nothing
        {
            for (var di = -_look; di <= _look; di += _look)
            {
                if (!Ready(ni + di, nj + dj))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Reads the landmasses round a column (the ones <see cref="IsRead"/> waits for), waiting for the lock if another thread is reading.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public void Warm(int x, int z)
    {
        var (ni, nj) = (IntMath.DivDown(x, _spacing), IntMath.DivDown(z, _spacing));
        for (var dj = -_look; dj <= _look; dj += _look)
        {
            for (var di = -_look; di <= _look; di += _look)
            {
                _ = Locate(ni + di, nj + dj);
            }
        }
    }

    /// <summary>The distance in blocks between the rows (and the columns of a row) that <see cref="WarmRow"/> needs to leave no landmass unread.</summary>
    public int WarmStep => _look * _spacing;

    /// <summary>Reads the landmasses along a row of the world (B40): from x0 to x1 at one z, a step apart that the nodes <see cref="Warm"/> reads round a column cover without a gap. A job for a worker (the far view warms the whole far range with rows of these ahead of the tiles).</summary>
    /// <param name="z">The row's z, in blocks.</param>
    /// <param name="x0">The first x.</param>
    /// <param name="x1">The last x.</param>
    public void WarmRow(int z, int x0, int x1)
    {
        var step = _look * _spacing;
        for (var x = x0; x <= x1; x += step)
        {
            Warm(x, z);
        }
    }

    // A node that is read, or that is sea or excluded (which is settled here, without the lock); land that is not read is not ready.
    private bool Ready(int i, int j)
    {
        var (ti, tj) = (IntMath.DivDown(i, Tile), IntMath.DivDown(j, Tile));
        var cell = _tiles.GetOrAdd(Key(ti, tj), static _ => new DrainageCell(Tile));
        var k = ((j - (tj * Tile)) * Tile) + (i - (ti * Tile));
        if ((Volatile.Read(ref cell.Flags[k]) & BuiltFlag) != 0)
        {
            return true;
        }

        var node = _surface.NodeAt(i * _spacing, j * _spacing);
        if (!node.Sea && !node.Excluded)
        {
            return false;
        }

        cell.Level16[k] = _sea16;
        cell.Next[k] = -1;
        Volatile.Write(ref cell.Flags[k], (byte)((node.Sea ? SeaFlag : OutsideFlag) | BuiltFlag));
        return true;
    }

    /// <summary>The distance between two nodes, in blocks.</summary>
    public int Spacing => _spacing;

    /// <summary>The tiles of nodes read so far.</summary>
    public int TilesRead => _tiles.Count;

    /// <summary>The network of a node (for the tests and the preview tool).</summary>
    /// <param name="i">The node's column: its block x is about i x spacing.</param>
    /// <param name="j">The node's row.</param>
    public DrainageNodeInfo Info(int i, int j)
    {
        var (cell, k) = Locate(i, j);
        var flags = cell.Flags[k];
        return new DrainageNodeInfo(cell.Level16[k], cell.Area[k], cell.Next[k], (flags & RiverFlag) != 0, (flags & LakeFlag) != 0, (flags & SeaFlag) != 0, (flags & OutsideFlag) != 0);
    }

    /// <summary>Where the node stands, in blocks: its grid point moved by its jitter.</summary>
    /// <param name="i">The node's column.</param>
    /// <param name="j">The node's row.</param>
    public (int X, int Z) Position(int i, int j)
    {
        var (px, pz) = Position16(i, j);
        return (IntMath.DivDown(px + 8, 16), IntMath.DivDown(pz + 8, 16));
    }

    /// <summary>The node nearest a block (by its grid point).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public (int I, int J) NodeNear(int x, int z) => (IntMath.DivDown(x + (_spacing / 2), _spacing), IntMath.DivDown(z + (_spacing / 2), _spacing));

    /// <summary>The river at a column: the nearest river line, how far the column is across it and the water's level there.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="extra16">How much wider the valley makes the river here, in sixteenths of a block.</param>
    /// <param name="share">The share of the river's width the column keeps (<see cref="IntMath.One"/>: all; near a region, less).</param>
    /// <param name="hit">The river, when there is one.</param>
    public bool River(int x, int z, int extra16, int share, out RiverHit hit)
    {
        hit = default;
        if (share <= 0 || s_noRead > 0)
        {
            return false;
        }

        var ni = IntMath.DivDown(x, _spacing);
        var nj = IntMath.DivDown(z, _spacing);
        var cx = x * 16L;
        var cz = z * 16L;
        var reach = (long)((Math.Min(_maxRamp16 + extra16, MaxRamp16) * (long)share / One) + 1);
        var bestCore = 0;
        var bestLevel = 0;
        var bestSize = 0;
        foreach (ref readonly var seg in Segments(ni, nj).AsSpan())
        {
            var (dx, dz) = (seg.Dx, seg.Dz);
            var (ux, uz) = (seg.Ux, seg.Uz);
            if (ChordDistanceSquared(cx - ux, cz - uz, dx, dz) > (reach + seg.Bow) * (reach + seg.Bow))
            {
                continue;
            }

            var t = Nearest(cx, cz, ux, uz, dx, dz, seg.Curve, out var d2);
            if (d2 >= reach * reach)
            {
                continue;
            }

            var area = Lerp(seg.AreaU, seg.AreaV, t);
            var size = SizeOf(area);
            var taper = IntMath.Ramp(area, _spec.SourceArea, _spec.SourceArea + (_spec.SourceArea / 2));
            var wide = (int)((long)Math.Min(Lerp(_minRamp16, _maxRamp16, size) + extra16, MaxRamp16) * share / One);
            if (wide <= 0)
            {
                continue;
            }

            var d = (int)IntMath.ISqrt(d2);
            var core = (One - IntMath.Ramp(d, 0, wide)) * taper / One; // a stream is as wide as the narrowest river from its first node, and as deep only as the ground that drains into it allows
            if (core > bestCore)
            {
                bestCore = core;
                bestLevel = Lerp(seg.LevelU, seg.LevelV, t);
                bestSize = size;
            }
        }

        if (bestCore == 0)
        {
            return false;
        }

        hit = new RiverHit(bestCore, bestLevel, bestSize);
        return true;
    }

    // The river reaches that can touch the columns of a node's cell: every river reach (not under a lake) that starts within `_look` nodes of it, worked out once per node from the nodes round it and kept in its tile (the
    // columns of a cell, up to 1,024, share it). Racing threads work out the same array.
    private RiverSegment[] Segments(int ni, int nj)
    {
        var (cell, k) = Locate(ni, nj);
        var known = Volatile.Read(ref cell.Near[k]);
        if (known is not null)
        {
            return known;
        }

        var list = new List<RiverSegment>();
        for (var j = nj - _look; j <= nj + _look; j++)
        {
            for (var i = ni - _look; i <= ni + _look; i++)
            {
                var (c, n) = Locate(i, j);
                if ((c.Flags[n] & (RiverFlag | LakeFlag)) != RiverFlag)
                {
                    continue;
                }

                var dir = c.Next[n];
                if (dir < 0)
                {
                    continue;
                }

                var (vi, vj) = (i + Dx[dir], j + Dz[dir]);
                var (vcell, vk) = Locate(vi, vj);
                var (ux, uz) = Position16(i, j);
                var (vx, vz) = Position16(vi, vj);
                var (dx, dz) = ((long)vx - ux, (long)vz - uz);
                var bow = (Math.Abs(dx) + Math.Abs(dz)) * _spec.Bow / 1000;
                var sign = (Hash(i, j) & (1UL << 48)) == 0 ? 1 : -1;
                list.Add(new RiverSegment(ux, uz, dx, dz, bow, sign * _spec.Bow, c.Area[n], vcell.Area[vk], c.Level16[n], vcell.Level16[vk]));
            }
        }

        var made = list.Count == 0 ? [] : list.ToArray();
        Volatile.Write(ref cell.Near[k], made);
        return made;
    }

    /// <summary>The lake at a column: how deep the filled basin is there (the depth of the four nodes round it, blended) and the lake's level.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="hit">The lake, when a node round the column is under one.</param>
    public bool Lake(int x, int z, out LakeHit hit)
    {
        hit = default;
        if (s_noRead > 0)
        {
            return false;
        }

        var ni = IntMath.DivDown(x, _spacing);
        var nj = IntMath.DivDown(z, _spacing);
        var fx = IntMath.Mod(x, _spacing) * One / _spacing;
        var fz = IntMath.Mod(z, _spacing) * One / _spacing;
        var level = int.MinValue;
        var depth = 0L;
        for (var dj = 0; dj <= 1; dj++)
        {
            for (var di = 0; di <= 1; di++)
            {
                var (cell, k) = Locate(ni + di, nj + dj);
                var wx = di == 0 ? One - fx : fx;
                var wz = dj == 0 ? One - fz : fz;
                depth += (long)cell.Depth[k] * wx / One * wz / One;
                if ((cell.Flags[k] & LakeFlag) != 0)
                {
                    level = Math.Max(level, cell.Level16[k]);
                }
            }
        }

        if (level == int.MinValue || depth <= 0)
        {
            return false;
        }

        hit = new LakeHit((int)depth, level);
        return true;
    }

    private int SizeOf(int area)
    {
        var s = (long)Math.Clamp(area - _spec.SourceArea, 0, _spec.FullArea - _spec.SourceArea) * One / (_spec.FullArea - _spec.SourceArea);
        return (int)IntMath.ISqrt(s * One); // width follows the square root of the ground that drains into the river
    }

    private static int Lerp(int a, int b, int t) => a + (int)(((long)(b - a) * t) / One);

    // The squared distance from a point (relative to the segment's start) to the chord of a segment, in sixteenths squared.
    private static long ChordDistanceSquared(long px, long pz, long dx, long dz)
    {
        var len2 = (dx * dx) + (dz * dz);
        if (len2 == 0)
        {
            return (px * px) + (pz * pz);
        }

        var t = Math.Clamp(((px * dx) + (pz * dz)) * One / len2, 0, One);
        var (qx, qz) = (px - (dx * t / One), pz - (dz * t / One));
        return (qx * qx) + (qz * qz);
    }

    // The nearest point of the river's line between two nodes (a bowed arc of four straight pieces): the share of the way along it (0 to One) and the squared distance, in sixteenths squared.
    private static int Nearest(long cx, long cz, long ux, long uz, long dx, long dz, long bow, out long distanceSquared)
    {
        // The arc leaves the chord by bow thousandths of its length, sideways, 0.7 / 1 / 0.7 of it a quarter, half and three quarters of the way.
        ReadOnlySpan<int> shape = [0, 707, 1000, 707, 0];
        var best = long.MaxValue;
        var bestT = 0;
        long ax = 0, az = 0;
        for (var p = 0; p <= 4; p++)
        {
            var bx = ux + (dx * p / 4) - (dz * bow * shape[p] / 1_000_000);
            var bz = uz + (dz * p / 4) + (dx * bow * shape[p] / 1_000_000);
            if (p > 0)
            {
                var (sx, sz) = (bx - ax, bz - az);
                var (px, pz) = (cx - ax, cz - az);
                var len2 = (sx * sx) + (sz * sz);
                var t = len2 == 0 ? 0 : (int)Math.Clamp(((px * sx) + (pz * sz)) * One / len2, 0, One);
                var (qx, qz) = (px - (sx * t / One), pz - (sz * t / One));
                var d2 = (qx * qx) + (qz * qz);
                if (d2 < best)
                {
                    best = d2;
                    bestT = (int)(((long)(p - 1) * One) + t) / 4;
                }
            }

            (ax, az) = (bx, bz);
        }

        distanceSquared = best;
        return bestT;
    }

    private ulong Hash(int i, int j) => WorldRng.Mix(_seed ^ NodeSalt ^ ((ulong)(uint)i << 32) ^ (uint)j);

    // A node's place in sixteenths of a block: its grid point moved by up to the jitter each way.
    private (int X, int Z) Position16(int i, int j)
    {
        var h = Hash(i, j);
        var jx = (int)(((long)((int)(h & 0xFFFF) - 32768) * _jitter16) >> 15);
        var jz = (int)(((long)((int)((h >> 16) & 0xFFFF) - 32768) * _jitter16) >> 15);
        return ((i * _spacing * 16) + jx, (j * _spacing * 16) + jz);
    }

    // The tile and index of a node, read from the network first if no one has asked for it yet.
    private (DrainageCell Cell, int Index) Locate(int i, int j)
    {
        var ti = IntMath.DivDown(i, Tile);
        var tj = IntMath.DivDown(j, Tile);
        var last = s_last;
        if (last is null || !ReferenceEquals(last.Owner, this) || last.Ci != ti || last.Cj != tj)
        {
            var cell = _tiles.GetOrAdd(Key(ti, tj), static _ => new DrainageCell(Tile));
            last = new CellCache(this, ti, tj, cell);
            s_last = last;
        }

        var k = ((j - (tj * Tile)) * Tile) + (i - (ti * Tile));
        if ((Volatile.Read(ref last.Cell.Flags[k]) & BuiltFlag) == 0)
        {
            Resolve(last.Cell, i, j, k);
        }

        return (last.Cell, k);
    }

    private static long Key(int ci, int cj) => ((long)ci << 32) ^ (uint)cj;

    // A node no one has read: the sea and the excluded places are known at once; land is the start of a landmass, which is read whole.
    private void Resolve(DrainageCell cell, int i, int j, int k)
    {
        var node = _surface.NodeAt(i * _spacing, j * _spacing);
        if (node.Sea || node.Excluded)
        {
            cell.Level16[k] = _sea16;
            cell.Next[k] = -1;
            Volatile.Write(ref cell.Flags[k], (byte)((node.Sea ? SeaFlag : OutsideFlag) | BuiltFlag));
            return;
        }

        // B40: a landmass is read on the lock of the node it is read from, not on one lock for the whole network, so landmasses far apart are read at once by different threads. Two threads that start from different nodes of one landmass
        // both read it (the reading is a pure function of the land); the first to publish wins and the other finds its own node built and drops its result.
        lock (_reading.GetOrAdd(NodeKey(i, j), static _ => new object()))
        {
            if ((Volatile.Read(ref cell.Flags[k]) & BuiltFlag) == 0)
            {
                ReadLandmass(i, j, node, cell, k);
            }
        }
    }

    private static long NodeKey(int i, int j) => ((long)j << 32) ^ (uint)i;

    // The landmass that holds a node, read whole: the land nodes connected to it (eight neighbours, the excluded places are walls) and the sea nodes and excluded places beside them, flooded from the sea, with the ground that drains through
    // every node counted. The order the nodes are found in does not reach the result: the flood takes its nodes by (level, place).
    private void ReadLandmass(int startI, int startJ, DrainageNode start, DrainageCell startCell, int startIndex)
    {
        var index = new Dictionary<long, int>();
        var ni = new List<int>();
        var nj = new List<int>();
        var kind = new List<byte>(); // 0 land, SeaFlag, OutsideFlag, or 32 for land the cap cut off (water runs out there)
        var level = new List<int>(); // the surface the water is routed over
        var water = new List<int>(); // the level the water would stand at
        var near = new List<int>(); // eight neighbours of each node, or -1

        int Add(int i, int j, DrainageNode node, byte k)
        {
            var at = ni.Count;
            index[NodeKey(i, j)] = at;
            ni.Add(i);
            nj.Add(j);
            kind.Add(k);
            level.Add(k == SeaFlag ? _sea16 : node.Route16);
            water.Add(k == SeaFlag ? _sea16 : node.Level16);
            for (var d = 0; d < 8; d++)
            {
                near.Add(-1);
            }

            return at;
        }

        const byte Cut = 32;
        var queue = new Queue<int>();
        queue.Enqueue(Add(startI, startJ, start, 0));
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            for (var d = 0; d < 8; d++)
            {
                var (i, j) = (ni[n] + Dx[d], nj[n] + Dz[d]);
                if (!index.TryGetValue(NodeKey(i, j), out var m))
                {
                    var node = _surface.NodeAt(i * _spacing, j * _spacing);
                    if (node.Sea)
                    {
                        m = Add(i, j, node, SeaFlag);
                    }
                    else if (node.Excluded)
                    {
                        m = Add(i, j, node, OutsideFlag);
                    }
                    else if (ni.Count >= _spec.MaxNodes)
                    {
                        m = Add(i, j, node, Cut);
                    }
                    else
                    {
                        m = Add(i, j, node, 0);
                        queue.Enqueue(m);
                    }
                }

                near[(n * 8) + d] = m;
                near[(m * 8) + ((d + 4) & 7)] = n;
            }
        }

        var count = ni.Count;
        var filled = new int[count];
        var parent = new sbyte[count];
        Array.Fill(parent, (sbyte)-1);
        var seen = new bool[count];
        var order = new int[count];
        var heap = new PriorityQueue<int, (int Level, long Place)>();
        for (var n = 0; n < count; n++)
        {
            if (kind[n] is SeaFlag or Cut)
            {
                filled[n] = level[n];
                seen[n] = true;
                heap.Enqueue(n, (level[n], NodeKey(ni[n], nj[n])));
            }
        }

        var visited = 0;
        while (heap.TryDequeue(out var current, out _))
        {
            order[visited++] = current;
            for (var d = 0; d < 8; d++)
            {
                var m = near[(current * 8) + d];
                if (m < 0 || seen[m] || kind[m] == OutsideFlag)
                {
                    continue;
                }

                seen[m] = true;
                filled[m] = Math.Max(level[m], filled[current]);
                parent[m] = (sbyte)((d + 4) & 7);
                heap.Enqueue(m, (filled[m], NodeKey(ni[m], nj[m])));
            }
        }

        var area = new int[count];
        for (var o = 0; o < visited; o++)
        {
            area[order[o]] = 1;
        }

        // The nodes under a lake: flooded deep enough, in a body of at least LakeNodesMin nodes.
        var lake = new bool[count];
        for (var n = 0; n < count; n++)
        {
            lake[n] = kind[n] == 0 && seen[n] && filled[n] - level[n] >= LakeMin;
        }

        // One level to a body (B02d): the flood gives all the nodes of a body (eight neighbours) one level, the pass it spills over (the lowest among them is taken all the same), and the water stands there, whatever the ground or the water
        // level of the banks beside it. A node that level covers by less than LakeMin is a bank, and so is a spur: a node with no lake node at its side (north, east, south or west) holds, by the bilinear blend of its depth, a lone pocket
        // of water the body's other nodes do not reach (a puddle); the body is looked at again without them (it may fall in two, or under LakeNodesMin nodes and go). The nodes are found in any order and the result is the same.
        var lakeLevel = new int[count];
        var body = new List<int>();
        var drop = new List<int>();
        var taken = new bool[count];
        bool changed;
        do
        {
            changed = false;
            Array.Clear(taken);
            drop.Clear();
            for (var first = 0; first < count; first++)
            {
                if (!lake[first] || taken[first])
                {
                    continue;
                }

                body.Clear();
                body.Add(first);
                taken[first] = true;
                var low = filled[first];
                for (var b = 0; b < body.Count; b++)
                {
                    low = Math.Min(low, filled[body[b]]);
                    for (var d = 0; d < 8; d++)
                    {
                        var m = near[(body[b] * 8) + d];
                        if (m >= 0 && lake[m] && !taken[m])
                        {
                            taken[m] = true;
                            body.Add(m);
                        }
                    }
                }

                foreach (var m in body)
                {
                    var joined = false;
                    for (var d = 0; d < 8; d += 2)
                    {
                        var side = near[(m * 8) + d];
                        joined |= side >= 0 && lake[side];
                    }

                    if (body.Count < LakeNodesMin || low - level[m] < LakeMin || !joined)
                    {
                        drop.Add(m);
                    }
                    else
                    {
                        lakeLevel[m] = low;
                    }
                }
            }

            foreach (var m in drop)
            {
                lake[m] = false;
                changed = true;
            }
        }
        while (changed);

        // The water's level: a lake body's is its own, any other node's its own, and no node's is lower than the node it drains into (the nodes are taken parents first), so along a river the level only falls. A lake spills into the
        // node its outflow runs to, so the water downstream of a lake is never higher than the lake: the nodes below a lake are held to its level, however high the ground beside them stands.
        var final = new int[count];
        for (var o = 0; o < visited; o++)
        {
            var n = order[o];
            final[n] = lake[n] ? lakeLevel[n] : parent[n] >= 0 ? Math.Max(water[n], final[near[(n * 8) + parent[n]]]) : water[n];
        }

        var limit = new int[count]; // the lowest level of a lake at or above a node, up the water's way
        Array.Fill(limit, int.MaxValue);
        for (var o = visited - 1; o >= 0; o--)
        {
            var n = order[o];
            if (lake[n])
            {
                limit[n] = Math.Min(limit[n], lakeLevel[n]);
            }

            if (parent[n] >= 0)
            {
                var up = near[(n * 8) + parent[n]];
                limit[up] = Math.Min(limit[up], limit[n]);
            }
        }

        for (var o = 0; o < visited; o++)
        {
            var n = order[o];
            if (!lake[n])
            {
                final[n] = Math.Min(final[n], limit[n]);
            }
        }

        for (var o = visited - 1; o >= 0; o--)
        {
            var n = order[o];
            if (parent[n] >= 0)
            {
                area[near[(n * 8) + parent[n]]] += area[n];
            }
        }

        // How deep each node is under a lake, and how far over its surface a node beside a lake stands.
        var full = _spec.LakeBasin * 8; // the depth, in sixteenths of a block, where the lake's carve is full (the ramp of HeightModel's lake weight)
        short Depth(int n)
        {
            if (kind[n] == OutsideFlag || kind[n] == SeaFlag || !seen[n])
            {
                return 0;
            }

            if (lake[n])
            {
                return (short)Math.Min(Math.Max(final[n] - level[n], full), short.MaxValue); // a lake node is deep or it is not a lake node: the shallow rim of the flood is as deep as the carve's full weight (B02d)
            }

            var surface = int.MinValue;
            for (var d = 0; d < 8; d++)
            {
                var m = near[(n * 8) + d];
                if (m >= 0 && lake[m])
                {
                    surface = Math.Max(surface, final[m]);
                }
            }

            return surface == int.MinValue ? (short)0 : (short)Math.Max(Math.Min(surface - level[n], 0), short.MinValue);
        }

        lock (_build)
        {
            if ((Volatile.Read(ref startCell.Flags[startIndex]) & BuiltFlag) != 0)
            {
                return; // another thread published this landmass from another of its nodes while this one was read
            }

            Publish(count, ni, nj, kind, seen, area, lake, final, parent, Depth);
        }
    }

    private void Publish(int count, List<int> ni, List<int> nj, List<byte> kind, bool[] seen, int[] area, bool[] lake, int[] final, sbyte[] parent, Func<int, short> depth)
    {
        for (var n = 0; n < count; n++)
        {
            var (i, j) = (ni[n], nj[n]);
            var ti = IntMath.DivDown(i, Tile);
            var tj = IntMath.DivDown(j, Tile);
            var cell = _tiles.GetOrAdd(Key(ti, tj), static _ => new DrainageCell(Tile));
            var k = ((j - (tj * Tile)) * Tile) + (i - (ti * Tile));
            byte flags;
            if (kind[n] == OutsideFlag || !seen[n])
            {
                flags = OutsideFlag;
            }
            else if (kind[n] == SeaFlag)
            {
                flags = SeaFlag;
            }
            else
            {
                flags = 0;
                if (area[n] >= _spec.SourceArea)
                {
                    flags |= RiverFlag;
                }

                if (lake[n])
                {
                    flags |= LakeFlag;
                }
            }

            cell.Level16[k] = final[n];
            cell.Area[k] = Math.Min(area[n], ushort.MaxValue);
            cell.Depth[k] = depth(n);
            cell.Next[k] = parent[n];
            Volatile.Write(ref cell.Flags[k], (byte)(flags | BuiltFlag));
        }
    }

    private sealed class CellCache(DrainageNetwork owner, int ci, int cj, DrainageCell cell)
    {
        public DrainageNetwork Owner { get; } = owner;

        public int Ci { get; } = ci;

        public int Cj { get; } = cj;

        public DrainageCell Cell { get; } = cell;
    }
}

/// <summary>One reach of a river, with what a column's look-up needs of it (B27): the upstream node's place and the reach's chord in sixteenths of a block, the chord's bow, the bow's signed curve, and the area and level at each end.</summary>
internal readonly record struct RiverSegment(long Ux, long Uz, long Dx, long Dz, long Bow, long Curve, int AreaU, int AreaV, int LevelU, int LevelV);

/// <summary>What the drainage network keeps of the nodes of one cell, row by row.</summary>
internal sealed class DrainageCell
{
    public DrainageCell(int side)
    {
        Level16 = new int[side * side];
        Area = new int[side * side];
        Depth = new short[side * side];
        Next = new sbyte[side * side];
        Flags = new byte[side * side];
        Near = new RiverSegment[]?[side * side];
    }

    /// <summary>The river reaches near each node's cell (B27), worked out the first time a column of the node asks; null until then.</summary>
    public RiverSegment[]?[] Near { get; }

    /// <summary>The level of each node after the basins were filled, in sixteenths of a block.</summary>
    public int[] Level16 { get; }

    /// <summary>The nodes that drain through each node.</summary>
    public int[] Area { get; }

    /// <summary>How far under its lake's surface each node's dented land lies, in sixteenths of a block, at least the depth where the lake's carve is full (a lake node is deep or it is not one; B02d); for a node beside a lake that is not under it, how far over the surface it lies, negative (so the shore is where the blended depth passes zero, between the nodes, and not on the grid); 0 elsewhere.</summary>
    public short[] Depth { get; }

    /// <summary>The direction the water runs from each node, or -1.</summary>
    public sbyte[] Next { get; }

    /// <summary>River, lake, sea and outside flags of each node.</summary>
    public byte[] Flags { get; }
}
