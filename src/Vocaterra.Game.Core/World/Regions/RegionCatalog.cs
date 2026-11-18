using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Regions;

/// <summary>
/// The authored regions of a world (B06): one today, the valley; a data-declared list from B16. It answers which region owns a chunk or a column, which one is nearest, and whether the set is
/// sound. Immutable and safe to read from any thread.
/// </summary>
public sealed class RegionCatalog
{
    private readonly IRegion[] _regions;

    /// <summary>Creates a catalog of regions, in the order given (the order breaks no tie: regions may not overlap).</summary>
    /// <param name="regions">The regions.</param>
    public RegionCatalog(IEnumerable<IRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);
        _regions = [.. regions];
    }

    /// <summary>A world with no region.</summary>
    public static RegionCatalog Empty { get; } = new([]);

    /// <summary>The regions.</summary>
    public IReadOnlyList<IRegion> Regions => _regions;

    /// <summary>The region whose chunks include <paramref name="chunk"/>, or <see langword="null"/>.</summary>
    /// <param name="chunk">The chunk.</param>
    public IRegion? Covering(VoxelChunkCoord chunk)
    {
        foreach (var region in _regions)
        {
            if (region.Covers(chunk))
            {
                return region;
            }
        }

        return null;
    }

    /// <summary>The region whose footprint holds the block column, or <see langword="null"/>.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public IRegion? RegionAt(int x, int z)
    {
        foreach (var region in _regions)
        {
            if (region.Bounds.ContainsColumn(x, z))
            {
                return region;
            }
        }

        return null;
    }

    /// <summary>The region whose footprint holds the chunk column (any layer), or <see langword="null"/>.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    public IRegion? ColumnOwner(int chunkX, int chunkZ)
    {
        foreach (var region in _regions)
        {
            if (region.Bounds.CoversColumn(chunkX, chunkZ))
            {
                return region;
            }
        }

        return null;
    }

    /// <summary>The nearest region to a block column and the distance to it (0 inside; the first column beside the edge is 1), or <see langword="null"/> with no region.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public (IRegion? Region, int Distance) Nearest(int x, int z)
    {
        IRegion? best = null;
        var bestDistance = int.MaxValue;
        foreach (var region in _regions)
        {
            var distance = region.Bounds.DistanceTo(x, z);
            if (distance < bestDistance)
            {
                best = region;
                bestDistance = distance;
            }
        }

        return (best, best is null ? 0 : bestDistance);
    }

    /// <summary>
    /// Every problem of the set, in a fixed order (empty when it is sound): a name that repeats, a region that overlaps another (their blend zones may meet, their chunks may not), a region that lies
    /// outside the world's limits or its layers, and two regions closer than the blend's margin on both sides, which would leave no land between them to ease.
    /// </summary>
    /// <param name="limits">The streamed world's limits.</param>
    /// <param name="margin">The blend's margin in blocks.</param>
    public IReadOnlyList<string> Validate(WorldLimits limits, int margin)
    {
        var problems = new List<string>();
        for (var i = 0; i < _regions.Length; i++)
        {
            var a = _regions[i];
            var b = a.Bounds;
            if (b.ChunksX < 1 || b.ChunksY < 1 || b.ChunksZ < 1)
            {
                problems.Add($"Region '{a.Id}' has no chunks.");
            }

            if (!limits.Contains(new VoxelChunkCoord(b.MinChunkX, b.MinChunkY, b.MinChunkZ)) || !limits.Contains(new VoxelChunkCoord(b.MinChunkX + b.ChunksX - 1, b.EndChunkY - 1, b.MinChunkZ + b.ChunksZ - 1)))
            {
                problems.Add($"Region '{a.Id}' reaches outside the world's limits.");
            }

            for (var j = i + 1; j < _regions.Length; j++)
            {
                var other = _regions[j];
                if (string.Equals(a.Id, other.Id, StringComparison.Ordinal))
                {
                    problems.Add($"Two regions are called '{a.Id}'.");
                }

                var o = other.Bounds;
                if (b.MinX <= o.MaxX && o.MinX <= b.MaxX && b.MinZ <= o.MaxZ && o.MinZ <= b.MaxZ)
                {
                    problems.Add($"Regions '{a.Id}' and '{other.Id}' overlap.");
                }
                else
                {
                    var gapX = Math.Max(o.MinX - b.MaxX, b.MinX - o.MaxX);
                    var gapZ = Math.Max(o.MinZ - b.MaxZ, b.MinZ - o.MaxZ);
                    if (Math.Max(gapX, gapZ) < (2 * margin) + 1)
                    {
                        problems.Add($"Regions '{a.Id}' and '{other.Id}' are closer than two blend margins ({2 * margin} blocks) apart.");
                    }
                }
            }
        }

        return problems;
    }
}
