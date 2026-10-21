using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game;

/// <summary>
/// What the guide can say about the 3D scene (S3): the nearest block of a catalogue noun from where the player stands, the direction to it
/// relative to where the player faces, and its distance in blocks. Everything is integer arithmetic on the fixed-point units of the body
/// (1/4096 block) and the angle table (<see cref="Angles"/>, 65536 is 1.0): no <c>Math.Sin</c>, no <c>Math.Atan2</c>, no floating point.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nearest.</b> The block with the smallest squared horizontal distance (x and z) between the player's centre and the centre of the block;
/// the vertical distance is not counted, so a tree's canopy above the player is as near as its trunk. Ties go to the block first in reading
/// order: the smaller z (north first), then the smaller x (west first), then the smaller y (lower first). The search reads the grid in that order
/// and keeps the first block of the smallest distance, so the answer is the same on every machine.
/// </para>
/// <para>
/// <b>Direction.</b> Forward is the facing (<c>sin yaw</c> east, <c>-cos yaw</c> south: yaw 0 looks north) and lateral is to the right
/// (<c>cos yaw</c>, <c>sin yaw</c>). The larger of the two components leads, as in the 2D slice: <c>adelante</c>, <c>detrás</c>, <c>a la
/// izquierda</c>, <c>a la derecha</c>. The other one is added, forward first, when it is at least two blocks and at least half the leading one:
/// <c>adelante, a la derecha</c>. A block the player stands on gives an empty direction.
/// </para>
/// <para>
/// <b>Distance.</b> The horizontal distance in blocks, rounded to the nearest whole block (an integer square root of the squared distance in
/// units). Less than half a block is zero, which the guide says as "aquí".
/// </para>
/// <para>
/// The facts read a grid through a function of (x, y, z), so the same class serves the live scene (the front-end's thread, <see cref="Live"/>) and
/// a copy taken when a line is submitted (<see cref="Snapshot"/>, which the turn worker reads while the simulation keeps changing the world).
/// </para>
/// </remarks>
public sealed class WorldFacts3D : IGuideFacts
{
    /// <summary>The units of one block (the body's fixed-point unit is 1/4096 block).</summary>
    public const int UnitsPerBlock = VoxelPhysics.UnitsPerBlock;

    /// <summary>Half a block in units: the centre of a block is this far from its corner.</summary>
    public const int HalfBlock = UnitsPerBlock / 2;

    /// <summary>The Spanish direction phrases (forward and back, left and right).</summary>
    public const string Ahead = "adelante";

    /// <summary>The phrase for a block behind the player.</summary>
    public const string Behind = "detrás";

    /// <summary>The phrase for a block to the player's left.</summary>
    public const string Left = "a la izquierda";

    /// <summary>The phrase for a block to the player's right.</summary>
    public const string Right = "a la derecha";

    private readonly int _sizeX;
    private readonly int _sizeY;
    private readonly int _sizeZ;
    private readonly Func<int, int, int, BlockId> _blockAt;
    private readonly NounCatalog _nouns;

    /// <summary>Creates the facts over any grid of the given size, read through <paramref name="blockAt"/> (a function of x, y and z).</summary>
    public WorldFacts3D(int sizeX, int sizeY, int sizeZ, Func<int, int, int, BlockId> blockAt, NounCatalog nouns)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeY);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeZ);
        _sizeX = sizeX;
        _sizeY = sizeY;
        _sizeZ = sizeZ;
        _blockAt = blockAt ?? throw new ArgumentNullException(nameof(blockAt));
        _nouns = nouns ?? throw new ArgumentNullException(nameof(nouns));
    }

    /// <summary>The facts over the live grid: every call reads the grid as it is now (the simulation's own thread).</summary>
    public static WorldFacts3D Live(VoxelGrid grid, NounCatalog nouns)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return new WorldFacts3D(grid.SizeX, grid.SizeY, grid.SizeZ, grid.GetBlock, nouns);
    }

    /// <summary>A copy of the blocks, as facts that no later edit can reach (the turn worker reads these).</summary>
    public static WorldFacts3D Snapshot(VoxelGrid grid, NounCatalog nouns)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var copy = new BlockId[grid.SizeX * grid.SizeY * grid.SizeZ];
        var index = 0;
        for (var z = 0; z < grid.SizeZ; z++)
        {
            for (var y = 0; y < grid.SizeY; y++)
            {
                for (var x = 0; x < grid.SizeX; x++)
                {
                    copy[index++] = grid.GetBlock(x, y, z);
                }
            }
        }

        var sizeX = grid.SizeX;
        var sizeY = grid.SizeY;
        var sizeZ = grid.SizeZ;
        return new WorldFacts3D(sizeX, sizeY, sizeZ, (x, y, z) => x >= 0 && y >= 0 && z >= 0 && x < sizeX && y < sizeY && z < sizeZ
            ? copy[(((z * sizeY) + y) * sizeX) + x]
            : BlockId.Air, nouns);
    }

    /// <summary>The block at a cell; air outside the scene.</summary>
    public BlockId BlockAt(GuideCell cell) => cell.X >= 0 && cell.Y >= 0 && cell.Z >= 0 && cell.X < _sizeX && cell.Y < _sizeY && cell.Z < _sizeZ
        ? _blockAt(cell.X, cell.Y, cell.Z)
        : BlockId.Air;

    /// <inheritdoc />
    public GuideNearest? NearestOfNoun(GuideViewpoint from, string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        var wanted = new bool[Enum.GetValues<BlockId>().Length];
        var any = false;
        foreach (var block in _nouns.BlocksOf(lemma))
        {
            wanted[(int)block] = true;
            any = true;
        }

        if (!any)
        {
            return null;
        }

        long bestDistance = long.MaxValue;
        int bestX = 0;
        int bestY = 0;
        int bestZ = 0;
        var found = false;
        for (var z = 0; z < _sizeZ; z++)
        {
            var dz = ((long)z * UnitsPerBlock) + HalfBlock - from.CentreZ;
            for (var x = 0; x < _sizeX; x++)
            {
                var dx = ((long)x * UnitsPerBlock) + HalfBlock - from.CentreX;
                var horizontal = (dx * dx) + (dz * dz);
                if (horizontal >= bestDistance)
                {
                    continue; // the same as the reading order's first block of the least distance: a later block must be strictly nearer
                }

                for (var y = 0; y < _sizeY; y++)
                {
                    var block = _blockAt(x, y, z);
                    if (block != BlockId.Air && wanted[(int)block])
                    {
                        bestDistance = horizontal;
                        bestX = x;
                        bestY = y;
                        bestZ = z;
                        found = true;
                        break;
                    }
                }
            }
        }

        if (!found)
        {
            return null;
        }

        var offsetX = ((long)bestX * UnitsPerBlock) + HalfBlock - from.CentreX;
        var offsetZ = ((long)bestZ * UnitsPerBlock) + HalfBlock - from.CentreZ;
        var yaw = Angles.Wrap(from.Yaw);
        var sin = Angles.Sin(yaw);
        var cos = Angles.Cos(yaw);
        var forward = ((offsetX * sin) - (offsetZ * cos)) / Angles.One;
        var lateral = ((offsetX * cos) + (offsetZ * sin)) / Angles.One;
        return new GuideNearest(
            new GuideCell(bestX, bestY, bestZ),
            BlocksOf(bestDistance),
            DirectionPhrase(forward, lateral),
            bestDistance);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> NounsAt(GuideCell cell)
    {
        var block = BlockAt(cell);
        if (block == BlockId.Air)
        {
            return [];
        }

        var nouns = _nouns.NounsOfBlock(block);
        var lemmas = new string[nouns.Count];
        for (var i = 0; i < lemmas.Length; i++)
        {
            lemmas[i] = nouns[i].Lemma;
        }

        return lemmas;
    }

    /// <summary>
    /// The direction from the player to a block, from its components in units: <paramref name="forward"/> positive ahead, and
    /// <paramref name="lateral"/> positive to the right. The leading component is always said; the other one is added (forward first) when
    /// it is at least two blocks and at least half the leading one. Both zero gives an empty string.
    /// </summary>
    public static string DirectionPhrase(long forward, long lateral)
    {
        var absForward = Math.Abs(forward);
        var absLateral = Math.Abs(lateral);
        if (absForward == 0 && absLateral == 0)
        {
            return string.Empty;
        }

        var forwardLeads = absForward >= absLateral;
        var lead = forwardLeads ? absForward : absLateral;
        var other = forwardLeads ? absLateral : absForward;
        var leadPhrase = forwardLeads ? ForwardPhrase(forward) : LateralPhrase(lateral);
        if (other < 2 * UnitsPerBlock || other * 2 < lead)
        {
            return leadPhrase;
        }

        return forwardLeads
            ? leadPhrase + ", " + LateralPhrase(lateral)
            : ForwardPhrase(forward) + ", " + leadPhrase;
    }

    /// <summary>The distance in blocks: the squared distance in units rounded to the nearest whole block.</summary>
    public static int BlocksOf(long squaredUnits)
    {
        var units = IntegerSquareRoot(squaredUnits);
        return (int)((units + HalfBlock) / UnitsPerBlock);
    }

    /// <summary>The floor of the square root of a non-negative number (Newton's steps on integers; no floating point).</summary>
    public static long IntegerSquareRoot(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (value < 2)
        {
            return value;
        }

        var root = value;
        var next = (root + 1) / 2;
        while (next < root)
        {
            root = next;
            next = (root + (value / root)) / 2;
        }

        return root;
    }

    private static string ForwardPhrase(long forward) => forward < 0 ? Behind : Ahead;

    private static string LateralPhrase(long lateral) => lateral < 0 ? Left : Right;
}
