using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Streaming;

/// <summary>
/// How far round the player a tall world is kept loaded when only its surface band is (B07, biomes plan section 3.7), in chunk columns.
/// </summary>
/// <param name="LoadRadius">The chunk columns round the player's own that are wanted (a disc): a column is wanted when its distance from the player's, in columns, is at most this.</param>
/// <param name="KeepRadius">The distance beyond which an unchanged chunk may be dropped, in columns: at least <see cref="LoadRadius"/>; the plan holds one more column on top as slack.</param>
/// <param name="NearRadius">The columns round the player in which every layer is wanted, so the ground under the feet and the caves in it are there (the plan's "near zone").</param>
/// <param name="UndergroundNearRadius">The same while the player is underground: at least <see cref="NearRadius"/>.</param>
public readonly record struct BandStreamPolicy(int LoadRadius, int KeepRadius, int NearRadius, int UndergroundNearRadius)
{
    /// <summary>Whether the radii make sense.</summary>
    internal bool IsValid => LoadRadius >= 1 && KeepRadius >= LoadRadius && NearRadius >= 0 && UndergroundNearRadius >= NearRadius;
}

/// <summary>
/// What a <see cref="BandStreamer"/> decided for one place of the player: the chunks to hold resident, nearest first, and which resident chunks may go. Immutable once made.
/// </summary>
public sealed class BandPlan
{
    private readonly int _originX;
    private readonly int _originZ;
    private readonly int _width;
    private readonly int[] _min;
    private readonly int[] _max;
    private readonly long _holdRadiusSquared;
    private readonly int _playerX;
    private readonly int _playerZ;

    internal BandPlan(VoxelChunkCoord player, bool underground, int originX, int originZ, int width, int[] min, int[] max, long holdRadiusSquared, IReadOnlyList<VoxelChunkCoord> wanted, IReadOnlyList<WantedColumn> columns)
    {
        Player = player;
        Underground = underground;
        _originX = originX;
        _originZ = originZ;
        _width = width;
        _min = min;
        _max = max;
        _holdRadiusSquared = holdRadiusSquared;
        _playerX = player.X;
        _playerZ = player.Z;
        Wanted = wanted;
        Columns = columns;
    }

    /// <summary>The chunk the plan is for.</summary>
    public VoxelChunkCoord Player { get; }

    /// <summary>Whether the player was underground (the near zone is wider then).</summary>
    public bool Underground { get; }

    /// <summary>The chunks to hold resident: the surface band of every wanted column and of its neighbours, with the one layer of padding that lets every band chunk see all its neighbours, nearest first (the column's distance, then the layer nearest the player's, then row and column).</summary>
    public IReadOnlyList<VoxelChunkCoord> Wanted { get; }

    /// <summary>The wanted columns with the layers each must hold, nearest first.</summary>
    public IReadOnlyList<WantedColumn> Columns { get; }

    /// <summary>The layers a column of the plan must hold (inclusive), or <see langword="false"/> when the column is outside the plan or needs none.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    /// <param name="min">The lowest layer.</param>
    /// <param name="max">The highest layer.</param>
    public bool TryGetLayers(int chunkX, int chunkZ, out int min, out int max)
    {
        min = int.MaxValue;
        max = int.MinValue;
        var i = IndexOf(chunkX, chunkZ);
        if (i < 0 || _min[i] > _max[i])
        {
            return false;
        }

        (min, max) = (_min[i], _max[i]);
        return true;
    }

    /// <summary>
    /// Whether a chunk is held by the plan: inside the keep radius (and its one column of slack) and within one layer of what its column must hold. A chunk the plan does not hold may be dropped
    /// when it is unchanged (the grid refuses a changed one).
    /// </summary>
    /// <param name="chunk">A resident chunk.</param>
    public bool Holds(VoxelChunkCoord chunk)
    {
        var i = IndexOf(chunk.X, chunk.Z);
        if (i < 0)
        {
            return false;
        }

        var dx = (long)chunk.X - _playerX;
        var dz = (long)chunk.Z - _playerZ;
        return dx * dx + dz * dz <= _holdRadiusSquared && _min[i] <= _max[i] && chunk.Y >= _min[i] - 1 && chunk.Y <= _max[i] + 1;
    }

    private int IndexOf(int chunkX, int chunkZ)
    {
        var gx = chunkX - _originX;
        var gz = chunkZ - _originZ;
        return (uint)gx < (uint)_width && (uint)gz < (uint)_width ? (gz * _width) + gx : -1;
    }
}

/// <summary>One chunk column a plan wants and the layers it must hold.</summary>
/// <param name="ChunkX">The chunk column.</param>
/// <param name="ChunkZ">The chunk row.</param>
/// <param name="MinLayer">The lowest layer (inclusive).</param>
/// <param name="MaxLayer">The highest layer (inclusive).</param>
public readonly record struct WantedColumn(int ChunkX, int ChunkZ, int MinLayer, int MaxLayer);

/// <summary>
/// The residency policy of the tall streamed world (B07, biomes plan section 3.7), the band version of <see cref="ChunkStreamer"/>: at a distance only the surface band of each column is
/// wanted (the chunks that meet the ground, decided by <see cref="SurfaceBands"/>), a chunk wholly underground only within a near radius of the player or when the player is underground, and
/// every chunk of the band is padded by one layer and one column on every side, because the renderer meshes a chunk only when all 26 of its neighbours are resident. It reads no grid and
/// holds no state; the caller installs what is wanted and drops what the plan no longer holds. Chunks are a pure function of their coordinates, so the policy changes performance, never results.
/// </summary>
public sealed class BandStreamer
{
    private readonly BandStreamPolicy _policy;
    private readonly WorldLimits _limits;
    private readonly int _minLayer;
    private readonly int _maxLayer;
    private readonly (int Dx, int Dz)[] _disc;

    /// <summary>Creates the policy for a world.</summary>
    /// <param name="policy">The radii.</param>
    /// <param name="limits">The world's limits: nothing outside them is wanted.</param>
    /// <exception cref="ArgumentException">The radii do not make sense.</exception>
    public BandStreamer(BandStreamPolicy policy, WorldLimits limits)
    {
        if (!policy.IsValid)
        {
            throw new ArgumentException("A band policy needs a load radius of at least 1, a keep radius of at least the load radius and an underground near radius of at least the near radius.", nameof(policy));
        }

        _policy = policy;
        _limits = limits;
        _minLayer = limits.MinChunkY;
        _maxLayer = limits.EndChunkY - 1;
        _disc = Disc(policy.LoadRadius);
    }

    /// <summary>The radii.</summary>
    public BandStreamPolicy Policy => _policy;

    /// <summary>The farthest column, in columns, whose band the plan reads: the keep radius, its one column of slack and one more for the neighbours' hull.</summary>
    public int BandReach => _policy.KeepRadius + 2;

    /// <summary>
    /// Decides what to hold for a player in a chunk. <paramref name="bandOf"/> says which layers of a column meet the ground (<see cref="SurfaceBands.Of"/>, or a cache of it); it is asked for
    /// every column within <see cref="BandReach"/> that lies inside the world.
    /// </summary>
    /// <param name="player">The chunk the player is in.</param>
    /// <param name="underground">Whether the player is underground (the near zone is wider).</param>
    /// <param name="bandOf">The band of a column.</param>
    public BandPlan Plan(VoxelChunkCoord player, bool underground, Func<int, int, ColumnBand> bandOf)
    {
        ArgumentNullException.ThrowIfNull(bandOf);
        var reach = BandReach;
        var width = (2 * reach) + 1;
        var originX = player.X - reach;
        var originZ = player.Z - reach;
        var reachSquared = (long)reach * reach;
        var near = underground ? _policy.UndergroundNearRadius : _policy.NearRadius;
        var nearSquared = (long)near * near;

        // The core of each column: the band, and everything under it in the near zone.
        var coreMin = new int[width * width];
        var coreMax = new int[width * width];
        for (var gz = 0; gz < width; gz++)
        {
            for (var gx = 0; gx < width; gx++)
            {
                var i = (gz * width) + gx;
                (coreMin[i], coreMax[i]) = (int.MaxValue, int.MinValue);
                var dx = gx - reach;
                var dz = gz - reach;
                var distance = ((long)dx * dx) + ((long)dz * dz);
                var cx = originX + gx;
                var cz = originZ + gz;
                if (distance > reachSquared || cx < _limits.MinChunkX || cx >= _limits.EndChunkX || cz < _limits.MinChunkZ || cz >= _limits.EndChunkZ)
                {
                    continue;
                }

                var band = bandOf(cx, cz);
                if (band.IsEmpty)
                {
                    continue;
                }

                coreMin[i] = distance <= nearSquared ? _minLayer : Math.Max(band.MinLayer, _minLayer);
                coreMax[i] = Math.Min(band.MaxLayer, _maxLayer);
            }
        }

        // What each column must hold: the hull of the cores of its 3 x 3 columns, one layer wider each way.
        var min = new int[width * width];
        var max = new int[width * width];
        Array.Fill(min, int.MaxValue);
        Array.Fill(max, int.MinValue);
        var holdRadius = _policy.KeepRadius + 1;
        for (var gz = 1; gz < width - 1; gz++)
        {
            for (var gx = 1; gx < width - 1; gx++)
            {
                var lo = int.MaxValue;
                var hi = int.MinValue;
                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var j = ((gz + dz) * width) + gx + dx;
                        lo = Math.Min(lo, coreMin[j]);
                        hi = Math.Max(hi, coreMax[j]);
                    }
                }

                var i = (gz * width) + gx;
                if (lo > hi)
                {
                    (min[i], max[i]) = (int.MaxValue, int.MinValue);
                }
                else
                {
                    (min[i], max[i]) = (Math.Max(lo - 1, _minLayer), Math.Min(hi + 1, _maxLayer));
                }
            }
        }

        var wanted = new List<VoxelChunkCoord>();
        var columns = new List<WantedColumn>();
        foreach (var (dx, dz) in _disc)
        {
            var cx = player.X + dx;
            var cz = player.Z + dz;
            var i = ((dz + reach) * width) + dx + reach;
            if (cx < _limits.MinChunkX || cx >= _limits.EndChunkX || cz < _limits.MinChunkZ || cz >= _limits.EndChunkZ || min[i] > max[i])
            {
                continue;
            }

            columns.Add(new WantedColumn(cx, cz, min[i], max[i]));
            for (var step = 0; step <= Math.Max(max[i] - player.Y, player.Y - min[i]); step++)
            {
                if (player.Y - step >= min[i] && player.Y - step <= max[i])
                {
                    wanted.Add(new VoxelChunkCoord(cx, player.Y - step, cz));
                }

                if (step > 0 && player.Y + step >= min[i] && player.Y + step <= max[i])
                {
                    wanted.Add(new VoxelChunkCoord(cx, player.Y + step, cz));
                }
            }
        }

        return new BandPlan(player, underground, originX, originZ, width, min, max, (long)holdRadius * holdRadius, wanted, columns);
    }

    /// <summary>The resident chunks the plan does not hold, in a fixed order (layer, row, column).</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="resident">The resident chunks.</param>
    public static IReadOnlyList<VoxelChunkCoord> Evictable(BandPlan plan, IEnumerable<VoxelChunkCoord> resident)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(resident);
        var list = resident.Where(chunk => !plan.Holds(chunk)).ToList();
        list.Sort(static (a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X));
        return list;
    }

    // The columns of a disc round (0, 0), nearest first, ties by row then column.
    private static (int Dx, int Dz)[] Disc(int radius)
    {
        var list = new List<(long Distance, int Dx, int Dz)>();
        var limit = (long)radius * radius;
        for (var dz = -radius; dz <= radius; dz++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var distance = ((long)dx * dx) + ((long)dz * dz);
                if (distance <= limit)
                {
                    list.Add((distance, dx, dz));
                }
            }
        }

        list.Sort(static (a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Dz != b.Dz ? a.Dz.CompareTo(b.Dz) : a.Dx.CompareTo(b.Dx));
        return [.. list.Select(static t => (t.Dx, t.Dz))];
    }
}
