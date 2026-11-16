using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>One cell of a passage's path: where its centre line stands, how high its floor is, how wide and how high its tube is there.</summary>
/// <param name="X">The cell's x.</param>
/// <param name="Z">The cell's z.</param>
/// <param name="Floor">The y of the floor block (the top solid block) at the cell.</param>
/// <param name="HalfWidth16">The tube's half width, in sixteenths of a block.</param>
/// <param name="Headroom">How many blocks the roof lies over the floor.</param>
internal readonly record struct PathNode(int X, int Z, int Floor, int HalfWidth16, int Headroom);

/// <summary>
/// One passage from the surface into the caves (B11b): a winding path of cells, each 4-connected to the one before it and a block lower than it at most, with a tube of its own width and height round each, carved
/// by a noise so the walls and the roof bulge. It is a pure description: which cells it cuts is computed from it for any block (<see cref="Column"/> and <see cref="Carves"/>). The path's index 0 is the mouth,
/// <see cref="Length"/> the cell where it meets its cave, <see cref="Last"/> the end of its landing.
/// </summary>
internal sealed class EntranceRamp
{
    private const ulong GoldenX = 0x9E3779B97F4A7C15UL;
    private const ulong GoldenY = 0xC2B2AE3D27D4EB4FUL;
    private const ulong GoldenZ = 0x165667B19E3779F9UL;

    private readonly Dictionary<(int X, int Z), int> _forced = [];
    private readonly ulong _key;
    private readonly int _roughness;
    private readonly int _cell;
    private readonly int _ledges;

    /// <summary>Describes a passage.</summary>
    /// <param name="nodes">The path, mouth first.</param>
    /// <param name="length">The index of the cell where the passage meets its cave.</param>
    /// <param name="approachX">The x of the cell before the mouth, on the ground a body comes from.</param>
    /// <param name="approachZ">The z of that cell.</param>
    /// <param name="key">A key of the passage's own noise.</param>
    /// <param name="roughness">How far the noise moves the walls, in thousandths of a block.</param>
    /// <param name="cell">The noise's cell, in blocks.</param>
    /// <param name="ledges">The share of the side columns that stand a block higher, in thousandths.</param>
    public EntranceRamp(PathNode[] nodes, int length, int approachX, int approachZ, ulong key, int roughness, int cell, int ledges)
    {
        Nodes = nodes;
        Length = length;
        ApproachX = approachX;
        ApproachZ = approachZ;
        _key = key;
        _roughness = roughness;
        _cell = Math.Max(1, cell);
        _ledges = ledges;
        var widest = 0;
        foreach (var node in nodes)
        {
            widest = Math.Max(widest, node.HalfWidth16);
        }

        ReachBlocks = ((widest + (roughness * 16 / 1000) + 15) / 16) + 1;

        // The centre of every cell is always cut, and one cell beside it (the side the path's own hash picks), so the passage is two blocks wide at its narrowest and a body can always walk it.
        for (var i = 0; i < nodes.Length; i++)
        {
            _forced[(nodes[i].X, nodes[i].Z)] = nodes[i].Floor;
        }

        for (var i = 0; i < nodes.Length; i++)
        {
            var (px, pz) = Perpendicular(i);
            var (sx, sz) = (nodes[i].X + px, nodes[i].Z + pz);
            if (!_forced.TryAdd((sx, sz), nodes[i].Floor))
            {
                _forced.TryAdd((nodes[i].X - px, nodes[i].Z - pz), nodes[i].Floor); // the picked side is the path itself (a bend): the other side then
            }
        }
    }

    /// <summary>The path, mouth first.</summary>
    public PathNode[] Nodes { get; }

    /// <summary>The index of the cell where the passage meets its cave.</summary>
    public int Length { get; }

    /// <summary>The cells of the landing after it.</summary>
    public int Landing => Nodes.Length - 1 - Length;

    /// <summary>The last index of the path, the end of the landing.</summary>
    public int Last => Nodes.Length - 1;

    /// <summary>The mouth's x.</summary>
    public int AnchorX => Nodes[0].X;

    /// <summary>The mouth's z.</summary>
    public int AnchorZ => Nodes[0].Z;

    /// <summary>The x of the cell before the mouth.</summary>
    public int ApproachX { get; }

    /// <summary>The z of the cell before the mouth.</summary>
    public int ApproachZ { get; }

    /// <summary>The y of the top solid block at the mouth.</summary>
    public int MouthY => Nodes[0].Floor;

    /// <summary>How many blocks from a cell of the path the tube can reach.</summary>
    public int ReachBlocks { get; }

    /// <summary>The y of the floor block at an index along the path (clamped to its ends).</summary>
    /// <param name="along">The index.</param>
    public int FloorAt(int along) => Nodes[Math.Clamp(along, 0, Last)].Floor;

    /// <summary>The lowest floor: the landing's.</summary>
    public int LowestFloor => Nodes[Last].Floor;

    /// <summary>The cell of the path's centre line at an index (clamped to its ends).</summary>
    /// <param name="along">The index.</param>
    public (int X, int Z) CentreAt(int along)
    {
        var node = Nodes[Math.Clamp(along, 0, Last)];
        return (node.X, node.Z);
    }

    /// <summary>A box of columns that holds the path and its tube (a little more than they need), padded by <paramref name="pad"/>.</summary>
    /// <param name="pad">The padding, in blocks.</param>
    public (int MinX, int MinZ, int MaxX, int MaxZ) Bounds(int pad)
    {
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
        foreach (var node in Nodes)
        {
            minX = Math.Min(minX, node.X);
            minZ = Math.Min(minZ, node.Z);
            maxX = Math.Max(maxX, node.X);
            maxZ = Math.Max(maxZ, node.Z);
        }

        var reach = ReachBlocks + 2 + pad;
        return (minX - reach, minZ - reach, maxX + reach, maxZ + reach);
    }

    /// <summary>Whether no tree or plant may stand on the column: the mouth and the path's first cells, and a few blocks round them.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public bool Clears(int x, int z)
    {
        var reach = ReachBlocks + 3;
        for (var i = 0; i < Math.Min(Nodes.Length, 14); i++)
        {
            var (dx, dz) = (x - Nodes[i].X, z - Nodes[i].Z);
            if ((dx * dx) + (dz * dz) <= reach * reach)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// What a column of the passage is: its floor (the floor of the path cell the column belongs to, a block higher on a ledge), that cell and the column's squared distance from its centre. A column belongs to the
    /// nearest cell of the path (the earlier on a tie); it is part of the passage only within the tube's reach, or where it is a forced centre or side cell.
    /// </summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="floor">The floor's y.</param>
    /// <param name="node">The index of the cell, or -1 when the column is only forced.</param>
    /// <param name="forced">Whether the column is a centre or side cell the path always cuts to a height of three.</param>
    /// <param name="distance2">The squared distance from the cell, in blocks.</param>
    /// <returns>Whether the column is part of the passage at all.</returns>
    public bool Column(int x, int z, out int floor, out int node, out bool forced, out int distance2)
    {
        floor = 0;
        node = -1;
        distance2 = int.MaxValue;
        forced = _forced.TryGetValue((x, z), out var forcedFloor);
        if (forced)
        {
            floor = forcedFloor;
        }

        var reach2 = ReachBlocks * ReachBlocks;
        for (var i = 0; i < Nodes.Length; i++)
        {
            var (dx, dz) = (x - Nodes[i].X, z - Nodes[i].Z);
            var d2 = (dx * dx) + (dz * dz);
            if (d2 <= reach2 && d2 < distance2)
            {
                (node, distance2) = (i, d2);
            }
        }

        if (node >= 0 && !forced)
        {
            floor = Nodes[node].Floor + (Ledge(x, z, node) ? 1 : 0);
        }

        return forced || node >= 0;
    }

    /// <summary>Whether the cell of a passage's column is air: above the floor and, in a forced column, within its first three blocks, or inside the tube the noise carves round the column's path cell.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="floor">The column's floor, from <see cref="Column"/>.</param>
    /// <param name="node">The path cell the column belongs to.</param>
    /// <param name="forced">Whether the column is forced.</param>
    /// <param name="distance2">The column's squared distance from the cell.</param>
    public bool Carves(int x, int y, int z, int floor, int node, bool forced, int distance2)
    {
        if (y <= floor)
        {
            return false;
        }

        if (forced && y <= floor + 3)
        {
            return true;
        }

        if (node < 0)
        {
            return false;
        }

        var n = Nodes[node];
        var height = n.Headroom + (Noise(x, 0, z, 3) / 600);
        var up = y - (n.Floor + 1);
        if (up >= height)
        {
            return false;
        }

        var t = up * 1000 / Math.Max(1, height);
        var shrink = 300 * t * t / 1_000_000;
        var radius16 = (n.HalfWidth16 * (1000 - shrink) / 1000) + (int)((long)Noise(x, y, z, _cell) * _roughness * 16 / 1_000_000);
        return radius16 > 0 && distance2 * 256 <= radius16 * radius16;
    }

    // Whether a side column stands a block higher than its cell's floor: where a noise of the column says so, and only where the roof stays three blocks over the raised floor.
    private bool Ledge(int x, int z, int node) => _ledges > 0 && Nodes[node].Headroom >= 4 && ((Noise(x, 0, z, _cell + 2) + 1000) / 2) < _ledges;

    // The cell beside a path cell on the side its hash picks, at right angles to the path there.
    private (int X, int Z) Perpendicular(int index)
    {
        var from = Nodes[Math.Max(0, index - 1)];
        var to = Nodes[Math.Min(Last, index + 1)];
        var (dx, dz) = (Math.Sign(to.X - from.X), Math.Sign(to.Z - from.Z));
        var flip = (WorldRng.Mix(_key ^ (ulong)(index + 1)) & 1) == 0 ? 1 : -1;
        return dx != 0 ? (0, flip) : (flip, 0);
    }

    /// <summary>The smooth noise of a block, in thousandths from -1 000 to 1 000: two octaves of a value noise on lattices of <paramref name="cell"/> and half that.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="cell">The first octave's cell, in blocks.</param>
    public int Noise(int x, int y, int z, int cell)
    {
        cell = Math.Max(1, cell);
        return ((Octave(x, y, z, cell, 1) * 2) + Octave(x, y, z, Math.Max(1, cell / 2), 7)) / 3;
    }

    private int Octave(int x, int y, int z, int cell, ulong salt)
    {
        int ix = IntMath.DivDown(x, cell), iy = IntMath.DivDown(y, cell), iz = IntMath.DivDown(z, cell);
        int fx = IntMath.Mod(x, cell) * 1024 / cell, fy = IntMath.Mod(y, cell) * 1024 / cell, fz = IntMath.Mod(z, cell) * 1024 / cell;
        var floor = Plane(ix, iy, iz, salt, fx, fz);
        var ceiling = Plane(ix, iy + 1, iz, salt, fx, fz);
        return Lerp(floor, ceiling, fy);
    }

    private int Plane(int ix, int iy, int iz, ulong salt, int fx, int fz)
    {
        var near = Lerp(Corner(ix, iy, iz, salt), Corner(ix + 1, iy, iz, salt), fx);
        var far = Lerp(Corner(ix, iy, iz + 1, salt), Corner(ix + 1, iy, iz + 1, salt), fx);
        return Lerp(near, far, fz);
    }

    private int Corner(int ix, int iy, int iz, ulong salt)
    {
        var hash = WorldRng.Mix(_key ^ (salt * GoldenX) ^ ((ulong)(uint)ix * GoldenY) ^ ((ulong)(uint)iy * GoldenZ) ^ ((ulong)(uint)iz * GoldenX * 3));
        return (int)(hash % 2001UL) - 1000;
    }

    private static int Lerp(int a, int b, int f1024)
    {
        var smooth = ((long)f1024 * f1024 * (3072 - (2 * f1024))) >> 20; // smoothstep, 0 to 1024
        return a + (int)(((long)(b - a) * smooth) >> 10);
    }
}
