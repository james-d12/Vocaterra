using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Npc;

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
/// (<c>cos yaw</c>, <c>sin yaw</c>). The larger of the two components leads: <c>adelante</c>, <c>detrás</c>, <c>a la
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
public sealed class WorldFacts : IGuideFacts
{
    /// <summary>The units of one block (the body's fixed-point unit is 1/4096 block).</summary>
    public const int UnitsPerBlock = VoxelPhysics.UnitsPerBlock;

    /// <summary>Half a block in units: the centre of a block is this far from its corner.</summary>
    public const int HalfBlock = UnitsPerBlock / 2;

    private readonly WorldWindow _bounds;
    private readonly Func<GuideViewpoint, WorldWindow> _searchWindow;
    private const int ChunkShift = VoxelChunk.Shift;
    private const int ChunkSize = VoxelChunk.Size;

    // The most blocks the walk over a body of blocks visits (NearestOfNounJoinedTo, V23a): a river and its lake are tens of thousands of blocks, and the walk kept every
    // one in a set; past this it stops and answers with the nearest it has seen.
    private const int JoinedBlockLimit = 4096;

    // The six faces a body of blocks is joined through (NearestOfNounJoinedTo).
    private static readonly (int X, int Y, int Z)[] JoinedSteps = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)];

    private readonly Func<int, int, int, BlockId> _blockAt;
    private readonly Func<int, int, int, UInt128>? _chunkKinds;
    private readonly NounCatalog _nouns;
    private readonly DirectionWords _directions;

    /// <summary>
    /// Creates the facts over any grid of the given size, read through <paramref name="blockAt"/> (a function of x, y and z). A search skips the
    /// 16 x 16 x 16 chunks that cannot hold the noun when <paramref name="chunkKinds"/> says which block kinds a chunk holds (one bit per kind, as
    /// <see cref="VoxelChunk.KindMask"/>); without it every block is read.
    /// </summary>
    /// <param name="sizeX">The grid's width.</param>
    /// <param name="sizeY">The grid's height.</param>
    /// <param name="sizeZ">The grid's depth.</param>
    /// <param name="blockAt">The block at a cell.</param>
    /// <param name="nouns">The noun catalogue.</param>
    /// <param name="directions">The direction phrases of the answers (<c>game/words.json</c>, C05).</param>
    /// <param name="chunkKinds">The block kinds each chunk holds, or <see langword="null"/> to read every block.</param>
    public WorldFacts(int sizeX, int sizeY, int sizeZ, Func<int, int, int, BlockId> blockAt, NounCatalog nouns, DirectionWords directions, Func<int, int, int, UInt128>? chunkKinds = null)
    {
        _directions = directions ?? throw new ArgumentNullException(nameof(directions));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeY);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeZ);
        _bounds = new WorldWindow(0, 0, 0, sizeX, sizeY, sizeZ);
        _searchWindow = _ => _bounds;
        _blockAt = blockAt ?? throw new ArgumentNullException(nameof(blockAt));
        _chunkKinds = chunkKinds;
        _nouns = nouns ?? throw new ArgumentNullException(nameof(nouns));
    }

    /// <summary>
    /// Creates the facts over any grid, looking only at a window of it (B06): a block outside the <paramref name="bounds"/> is air, and a search reads only the chunks of the window
    /// <paramref name="searchWindow"/> gives for the viewpoint it is asked from, so a live window can follow the player.
    /// </summary>
    /// <param name="bounds">The box a point read may reach, in whole chunks (a streamed grid's limits).</param>
    /// <param name="searchWindow">The box a search from a viewpoint reads, in whole chunks (<see cref="WorldWindow.For"/>).</param>
    /// <param name="blockAt">The block at a cell.</param>
    /// <param name="nouns">The noun catalogue.</param>
    /// <param name="directions">The direction phrases of the answers.</param>
    /// <param name="chunkKinds">The block kinds each chunk holds, or <see langword="null"/> to read every block.</param>
    public WorldFacts(WorldWindow bounds, Func<GuideViewpoint, WorldWindow> searchWindow, Func<int, int, int, BlockId> blockAt, NounCatalog nouns, DirectionWords directions, Func<int, int, int, UInt128>? chunkKinds = null)
    {
        _bounds = bounds;
        _searchWindow = searchWindow ?? throw new ArgumentNullException(nameof(searchWindow));
        _directions = directions ?? throw new ArgumentNullException(nameof(directions));
        _blockAt = blockAt ?? throw new ArgumentNullException(nameof(blockAt));
        _chunkKinds = chunkKinds;
        _nouns = nouns ?? throw new ArgumentNullException(nameof(nouns));
    }

    /// <summary>
    /// The facts over the live grid: every call reads the grid as it is now (the simulation's own thread). A bounded grid is searched whole; a streamed one needs a <paramref name="window"/>
    /// (the box a search from a viewpoint reads), because the world is too large to search, and reads the chunks of the window as they are or as they would be generated.
    /// </summary>
    /// <exception cref="NotSupportedException">The grid is streamed and no window was given.</exception>
    public static WorldFacts Live(VoxelGrid grid, NounCatalog nouns, DirectionWords directions, Func<GuideViewpoint, WorldWindow>? window = null)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (grid.IsStreamed && window is null)
        {
            throw new NotSupportedException("A streamed world is too large to search: the live facts need a window (B06).");
        }

        var whole = WorldWindow.Of(grid.Limits);
        return new WorldFacts(
            whole,
            window ?? (_ => whole),
            grid.GetBlock,
            nouns,
            directions,
            (cx, cy, cz) =>
            {
                var coord = new VoxelChunkCoord(cx, cy, cz);
                return grid.IsStreamed ? grid.GetChunk(coord).KindMask : grid.TryGetChunk(coord, out var chunk) ? chunk.KindMask : UInt128.Zero;
            });
    }

    /// <summary>
    /// A copy of the blocks, as facts that no later edit can reach (the turn worker reads these). Only the chunks of the window that hold something are copied
    /// (a chunk of air costs nothing), keyed by chunk coordinate, with the block kinds each holds, so the copy is a few milliseconds and a search reads only the chunks that can
    /// hold the noun. Without a <paramref name="window"/> it copies the whole of a bounded grid (the valley); a streamed grid needs one (<see cref="WorldWindow.For"/>) and generates the chunks of it
    /// that are not resident, as a read would.
    /// </summary>
    /// <exception cref="NotSupportedException">The grid is streamed and no window was given.</exception>
    public static WorldFacts Snapshot(VoxelGrid grid, NounCatalog nouns, DirectionWords directions, WorldWindow? window = null)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (grid.IsStreamed && window is null)
        {
            throw new NotSupportedException("A streamed world is too large to copy: a snapshot needs a window around the player (B06).");
        }

        var box = window ?? WorldWindow.Of(grid.Limits);
        var copied = new Dictionary<VoxelChunkCoord, (BlockId[] Blocks, UInt128 Kinds)>();
        for (var cy = box.MinY >> ChunkShift; cy < (box.EndY + ChunkSize - 1) >> ChunkShift; cy++)
        {
            for (var cz = box.MinZ >> ChunkShift; cz < (box.EndZ + ChunkSize - 1) >> ChunkShift; cz++)
            {
                for (var cx = box.MinX >> ChunkShift; cx < (box.EndX + ChunkSize - 1) >> ChunkShift; cx++)
                {
                    var coord = new VoxelChunkCoord(cx, cy, cz);
                    VoxelChunk? chunk;
                    if (grid.IsStreamed)
                    {
                        chunk = grid.Limits.Contains(coord) ? grid.GetChunk(coord) : null;
                    }
                    else
                    {
                        _ = grid.TryGetChunk(coord, out chunk);
                    }

                    if (chunk is not null && !chunk.IsEmpty)
                    {
                        copied[coord] = (chunk.Blocks.ToArray(), chunk.KindMask);
                    }
                }
            }
        }

        return new WorldFacts(
            box,
            _ => box,
            (x, y, z) =>
            {
                if (!box.Contains(x, y, z))
                {
                    return BlockId.Air;
                }

                return copied.TryGetValue(VoxelChunkCoord.FromBlock(x, y, z), out var chunk)
                    ? chunk.Blocks[(((((y & (ChunkSize - 1)) << ChunkShift) + (z & (ChunkSize - 1))) << ChunkShift) + (x & (ChunkSize - 1)))]
                    : BlockId.Air;
            },
            nouns,
            directions,
            (cx, cy, cz) => copied.TryGetValue(new VoxelChunkCoord(cx, cy, cz), out var chunk) ? chunk.Kinds : 0UL);
    }

    /// <summary>The block at a cell; air outside the scene.</summary>
    public BlockId BlockAt(GuideCell cell) => _bounds.Contains(cell.X, cell.Y, cell.Z) ? _blockAt(cell.X, cell.Y, cell.Z) : BlockId.Air;

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

        UInt128 mask = 0;
        for (var kind = 0; kind < wanted.Length; kind++)
        {
            if (wanted[kind])
            {
                mask |= BlockKinds.Bit(kind);
            }
        }

        // The chunks that can hold the noun, nearest first: a chunk is read only while it could still beat the best block found.
        var window = _searchWindow(from);
        var (firstX, endX) = (window.MinX >> ChunkShift, (window.EndX + ChunkSize - 1) >> ChunkShift);
        var (firstY, endY) = (window.MinY >> ChunkShift, (window.EndY + ChunkSize - 1) >> ChunkShift);
        var (firstZ, endZ) = (window.MinZ >> ChunkShift, (window.EndZ + ChunkSize - 1) >> ChunkShift);
        var candidates = new List<(long Nearest, int Cx, int Cz)>();
        for (var cz = firstZ; cz < endZ; cz++)
        {
            var gapZ = GapToChunk(from.CentreZ, cz, window.MinZ, window.EndZ);
            for (var cx = firstX; cx < endX; cx++)
            {
                if (!ChunkColumnMayHold(cx, cz, firstY, endY, mask))
                {
                    continue;
                }

                var gapX = GapToChunk(from.CentreX, cx, window.MinX, window.EndX);
                candidates.Add(((gapX * gapX) + (gapZ * gapZ), cx, cz));
            }
        }

        candidates.Sort(static (a, b) => a.Nearest != b.Nearest ? a.Nearest.CompareTo(b.Nearest) : a.Cz != b.Cz ? a.Cz.CompareTo(b.Cz) : a.Cx.CompareTo(b.Cx));

        // The least (horizontal distance, z, x, y): the guide's tie rule, whatever order the chunks are read in.
        long bestDistance = long.MaxValue;
        int bestX = 0;
        int bestY = 0;
        int bestZ = 0;
        var found = false;
        foreach (var (nearest, cx, cz) in candidates)
        {
            if (nearest > bestDistance)
            {
                break;
            }

            for (var cy = firstY; cy < endY; cy++)
            {
                if (_chunkKinds is { } kindsOf && (kindsOf(cx, cy, cz) & mask) == UInt128.Zero)
                {
                    continue;
                }

                for (var z = Math.Max(window.MinZ, cz << ChunkShift); z < Math.Min(window.EndZ, (cz + 1) << ChunkShift); z++)
                {
                    var dz = ((long)z * UnitsPerBlock) + HalfBlock - from.CentreZ;
                    for (var x = Math.Max(window.MinX, cx << ChunkShift); x < Math.Min(window.EndX, (cx + 1) << ChunkShift); x++)
                    {
                        var dx = ((long)x * UnitsPerBlock) + HalfBlock - from.CentreX;
                        var horizontal = (dx * dx) + (dz * dz);
                        if (horizontal > bestDistance)
                        {
                            continue;
                        }

                        for (var y = Math.Max(window.MinY, cy << ChunkShift); y < Math.Min(window.EndY, (cy + 1) << ChunkShift); y++)
                        {
                            var block = _blockAt(x, y, z);
                            if (block == BlockId.Air || !wanted[(int)block])
                            {
                                continue;
                            }

                            if (horizontal < bestDistance || (z, x, y).CompareTo((bestZ, bestX, bestY)) < 0)
                            {
                                bestDistance = horizontal;
                                bestX = x;
                                bestY = y;
                                bestZ = z;
                                found = true;
                            }

                            break; // the lowest block of the column that is the noun
                        }
                    }
                }
            }
        }

        return found ? Answer(from, bestX, bestY, bestZ, bestDistance) : null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A walk over the blocks of the noun from <paramref name="body"/>, face to face (the river and the lake it runs into are one body of water, some tens of
    /// thousands of blocks; a well's water touches none of them). Of each column it keeps the lowest block of the body, as <see cref="NearestOfNoun"/> keeps the
    /// lowest of the noun, and the nearest column wins by the same rule: the smallest squared horizontal distance, then the smaller z, x and y. The walk visits at most
    /// <see cref="JoinedBlockLimit"/> blocks (V23a): a larger body is answered with the nearest block of those the walk reached from <paramref name="body"/>, which is the
    /// nearest of the whole body when the viewpoint is near the block it started from.
    /// </remarks>
    public GuideNearest? NearestOfNounJoinedTo(GuideViewpoint from, string lemma, GuideCell body)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        var wanted = new HashSet<BlockId>(_nouns.BlocksOf(lemma));
        if (!wanted.Contains(BlockAt(body)))
        {
            return null;
        }

        var seen = new HashSet<(int X, int Y, int Z)> { (body.X, body.Y, body.Z) };
        var open = new Queue<(int X, int Y, int Z)>();
        open.Enqueue((body.X, body.Y, body.Z));
        long bestDistance = long.MaxValue;
        (int X, int Y, int Z) best = default;
        while (open.TryDequeue(out var cell))
        {
            var dx = ((long)cell.X * UnitsPerBlock) + HalfBlock - from.CentreX;
            var dz = ((long)cell.Z * UnitsPerBlock) + HalfBlock - from.CentreZ;
            var horizontal = (dx * dx) + (dz * dz);
            if (horizontal < bestDistance || (horizontal == bestDistance && (cell.Z, cell.X, cell.Y).CompareTo((best.Z, best.X, best.Y)) < 0))
            {
                bestDistance = horizontal;
                best = cell;
            }

            foreach (var (sx, sy, sz) in JoinedSteps)
            {
                if (seen.Count >= JoinedBlockLimit)
                {
                    break;
                }

                var next = (X: cell.X + sx, Y: cell.Y + sy, Z: cell.Z + sz);
                if (wanted.Contains(BlockAt(new GuideCell(next.X, next.Y, next.Z))) && seen.Add(next))
                {
                    open.Enqueue(next);
                }
            }
        }

        return Answer(from, best.X, best.Y, best.Z, bestDistance);
    }

    /// <inheritdoc />
    public GuideNearest Toward(GuideViewpoint from, GuideCell cell)
    {
        var dx = ((long)cell.X * UnitsPerBlock) + HalfBlock - from.CentreX;
        var dz = ((long)cell.Z * UnitsPerBlock) + HalfBlock - from.CentreZ;
        return Answer(from, cell.X, cell.Y, cell.Z, (dx * dx) + (dz * dz));
    }

    // The answer for the block found: its distance in blocks and the direction to it from the viewpoint's facing.
    private GuideNearest Answer(GuideViewpoint from, int bestX, int bestY, int bestZ, long bestDistance)
    {
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
            DirectionPhrase(forward, lateral, _directions),
            bestDistance);
    }

    // The distance in units from a coordinate to the nearest block centre of the chunk row or column (0 when the coordinate is inside it).
    private static long GapToChunk(long centre, int chunk, int first, int end)
    {
        long low = ((long)Math.Max(first, chunk << ChunkShift) * UnitsPerBlock) + HalfBlock;
        long high = (((long)Math.Min(end, (chunk + 1) << ChunkShift) - 1) * UnitsPerBlock) + HalfBlock;
        return centre < low ? low - centre : centre > high ? centre - high : 0;
    }

    private bool ChunkColumnMayHold(int cx, int cz, int firstY, int endY, UInt128 mask)
    {
        if (_chunkKinds is not { } kindsOf)
        {
            return true;
        }

        for (var cy = firstY; cy < endY; cy++)
        {
            if ((kindsOf(cx, cy, cz) & mask) != UInt128.Zero)
            {
                return true;
            }
        }

        return false;
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
    /// <param name="forward">The forward component, in units.</param>
    /// <param name="lateral">The lateral component, in units.</param>
    /// <param name="words">The direction phrases (<c>game/words.json</c>).</param>
    public static string DirectionPhrase(long forward, long lateral, DirectionWords words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var absForward = Math.Abs(forward);
        var absLateral = Math.Abs(lateral);
        if (absForward == 0 && absLateral == 0)
        {
            return string.Empty;
        }

        var forwardLeads = absForward >= absLateral;
        var lead = forwardLeads ? absForward : absLateral;
        var other = forwardLeads ? absLateral : absForward;
        var leadPhrase = forwardLeads ? ForwardPhrase(forward, words) : LateralPhrase(lateral, words);
        if (other < 2 * UnitsPerBlock || other * 2 < lead)
        {
            return leadPhrase;
        }

        return forwardLeads
            ? leadPhrase + ", " + LateralPhrase(lateral, words)
            : ForwardPhrase(forward, words) + ", " + leadPhrase;
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

    private static string ForwardPhrase(long forward, DirectionWords words) => forward < 0 ? words.Behind : words.Ahead;

    private static string LateralPhrase(long lateral, DirectionWords words) => lateral < 0 ? words.Left : words.Right;
}


