using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Streaming;

/// <summary>
/// How far round the player the world is kept loaded (B06): a radius to load to and a radius to keep to, in chunks, across (the columns, as a circle) and up and down (the layers).
/// </summary>
/// <param name="LoadRadius">The chunk columns round the player's own that are wanted: a column is wanted when its distance from the player's, in columns, is at most this.</param>
/// <param name="KeepRadius">The distance beyond which an unchanged chunk may be dropped, in columns: at least <see cref="LoadRadius"/>.</param>
/// <param name="LayersUp">The chunk layers wanted above the player's layer.</param>
/// <param name="LayersDown">The chunk layers wanted below the player's layer.</param>
public readonly record struct StreamPolicy(int LoadRadius, int KeepRadius, int LayersUp, int LayersDown)
{
    /// <summary>Whether the radii and distances make sense.</summary>
    internal bool IsValid => LoadRadius >= 0 && KeepRadius >= LoadRadius && LayersUp >= 0 && LayersDown >= 0;
}

/// <summary>
/// The residency policy of a streamed world and nothing else (B06, biomes plan section 3.7): from the player's position and a <see cref="StreamPolicy"/> it says which chunks are wanted, nearest
/// first with a fixed tie-break, and which resident chunks may be dropped: those beyond the keep distance plus one chunk of slack, so a chunk that is meshed never loses a neighbour (the
/// renderer needs all 26). It reads no grid and holds no state; the caller installs what is wanted and evicts what may go (a changed chunk is state, and the grid refuses to evict it).
/// Because chunks are a pure function of their coordinates, the policy changes performance, never results.
/// </summary>
public sealed class ChunkStreamer
{
    private readonly StreamPolicy _policy;
    private readonly WorldLimits _limits;

    /// <summary>Creates the policy for a world.</summary>
    /// <param name="policy">The radii.</param>
    /// <param name="limits">The world's limits: nothing outside them is wanted.</param>
    /// <exception cref="ArgumentException">The keep radius is below the load radius, or a distance is negative.</exception>
    public ChunkStreamer(StreamPolicy policy, WorldLimits limits)
    {
        if (!policy.IsValid)
        {
            throw new ArgumentException("A streaming policy needs a keep radius of at least its load radius and distances of 0 or more.", nameof(policy));
        }

        _policy = policy;
        _limits = limits;
    }

    /// <summary>The radii.</summary>
    public StreamPolicy Policy => _policy;

    /// <summary>The chunk that holds a block position (the player's eye or feet).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public static VoxelChunkCoord ChunkOf(int x, int y, int z) => VoxelChunkCoord.FromBlock(x, y, z);

    /// <summary>
    /// The chunks wanted round the player's chunk, nearest first: the squared distance from the player's chunk (across and up), then the layer, the row and the column, so the order is the same on every
    /// run and every machine. Chunks outside the world's limits are left out.
    /// </summary>
    /// <param name="player">The chunk the player is in.</param>
    public IReadOnlyList<VoxelChunkCoord> Wanted(VoxelChunkCoord player)
    {
        var wanted = new List<(long Distance, VoxelChunkCoord Chunk)>();
        var radius = _policy.LoadRadius;
        var limit = (long)radius * radius;
        for (var dz = -radius; dz <= radius; dz++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var across = ((long)dx * dx) + ((long)dz * dz);
                if (across > limit)
                {
                    continue;
                }

                for (var dy = -_policy.LayersDown; dy <= _policy.LayersUp; dy++)
                {
                    var chunk = new VoxelChunkCoord(player.X + dx, player.Y + dy, player.Z + dz);
                    if (_limits.Contains(chunk))
                    {
                        wanted.Add((across + ((long)dy * dy), chunk));
                    }
                }
            }
        }

        wanted.Sort(static (a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : Compare(a.Chunk, b.Chunk));
        return [.. wanted.Select(static w => w.Chunk)];
    }

    /// <summary>Whether a chunk may be dropped from memory when the player is in the given chunk: it lies beyond the keep distance plus one chunk of slack, across or up and down.</summary>
    /// <param name="chunk">A resident chunk.</param>
    /// <param name="player">The chunk the player is in.</param>
    public bool MayEvict(VoxelChunkCoord chunk, VoxelChunkCoord player)
    {
        var keep = _policy.KeepRadius + 1;
        long dx = chunk.X - player.X;
        long dz = chunk.Z - player.Z;
        var dy = chunk.Y - player.Y;
        return (dx * dx) + (dz * dz) > (long)keep * keep || dy > _policy.LayersUp + 1 || -dy > _policy.LayersDown + 1;
    }

    /// <summary>The resident chunks that may be dropped, in a fixed order (layer, row, column).</summary>
    /// <param name="resident">The resident chunks (<c>VoxelGrid.ResidentCoordinates</c>).</param>
    /// <param name="player">The chunk the player is in.</param>
    public IReadOnlyList<VoxelChunkCoord> Evictable(IEnumerable<VoxelChunkCoord> resident, VoxelChunkCoord player)
    {
        ArgumentNullException.ThrowIfNull(resident);
        var list = resident.Where(chunk => MayEvict(chunk, player)).ToList();
        list.Sort(Compare);
        return list;
    }

    private static int Compare(VoxelChunkCoord a, VoxelChunkCoord b) =>
        a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X);
}
