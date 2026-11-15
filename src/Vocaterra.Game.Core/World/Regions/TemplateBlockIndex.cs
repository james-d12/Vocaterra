using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Regions;

/// <summary>
/// Where the blocks of the rarer kinds stood when a region was made (B06): the fire rings' stones, the roads' paths and bridge planks. It lets the simulation find them without reading the
/// world's chunks (a streamed world has no chunk list to read), and it never changes: a block the player later places or breaks is the world's edit, not the template's. A kind that fills
/// many chunks (stone, dirt, grass, water) is not indexed and is refused, so a caller cannot ask for the whole ground by mistake.
/// </summary>
public sealed class TemplateBlockIndex
{
    /// <summary>A kind present in more chunks than this is not indexed.</summary>
    public const int MostChunksPerKind = 1024;

    private static readonly BlockPos[] None = [];

    private readonly Dictionary<BlockId, BlockPos[]> _byKind;
    private readonly HashSet<BlockId> _refused;

    private TemplateBlockIndex(Dictionary<BlockId, BlockPos[]> byKind, HashSet<BlockId> refused)
    {
        _byKind = byKind;
        _refused = refused;
    }

    /// <summary>
    /// Indexes the kinds that fill few chunks of a bounded grid, in the order <see cref="VoxelGrid.BlocksOfKind"/> answers: chunk layer, then row, then column, and the blocks in storage order
    /// inside each chunk.
    /// </summary>
    /// <param name="grid">The grid as built, before anything edits it.</param>
    internal static TemplateBlockIndex Scan(VoxelGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var limits = grid.Limits;
        var chunks = new List<VoxelChunk>();
        var chunkCount = new int[64];
        for (var cy = limits.MinChunkY; cy < limits.EndChunkY; cy++)
        {
            for (var cz = limits.MinChunkZ; cz < limits.EndChunkZ; cz++)
            {
                for (var cx = limits.MinChunkX; cx < limits.EndChunkX; cx++)
                {
                    if (!grid.TryGetChunk(new VoxelChunkCoord(cx, cy, cz), out var chunk) || chunk.IsEmpty)
                    {
                        continue;
                    }

                    chunks.Add(chunk);
                    var mask = chunk.KindMask;
                    for (var kind = 1; kind < 64; kind++)
                    {
                        if ((mask & (1UL << kind)) != 0)
                        {
                            chunkCount[kind]++;
                        }
                    }
                }
            }
        }

        var wanted = 0UL;
        var refused = new HashSet<BlockId>();
        for (var kind = 1; kind < 64; kind++)
        {
            if (chunkCount[kind] > MostChunksPerKind)
            {
                _ = refused.Add((BlockId)kind);
            }
            else if (chunkCount[kind] > 0)
            {
                wanted |= 1UL << kind;
            }
        }

        var lists = new Dictionary<BlockId, List<BlockPos>>();
        foreach (var chunk in chunks)
        {
            if ((chunk.KindMask & wanted) == 0)
            {
                continue;
            }

            var origin = chunk.Coord.Origin;
            var blocks = chunk.Blocks;
            for (var i = 0; i < blocks.Length; i++)
            {
                var block = blocks[i];
                if (block == BlockId.Air || (wanted & (1UL << (int)block)) == 0)
                {
                    continue;
                }

                if (!lists.TryGetValue(block, out var list))
                {
                    lists[block] = list = [];
                }

                list.Add(new BlockPos(
                    origin.X + (i & (VoxelChunk.Size - 1)),
                    origin.Y + (i >> (2 * VoxelChunk.Shift)),
                    origin.Z + ((i >> VoxelChunk.Shift) & (VoxelChunk.Size - 1))));
            }
        }

        return new TemplateBlockIndex(lists.ToDictionary(static p => p.Key, static p => p.Value.ToArray()), refused);
    }

    /// <summary>The index of the same blocks moved by whole chunks (a region placed away from the origin).</summary>
    /// <param name="chunksX">The shift along x, in chunks.</param>
    /// <param name="chunksZ">The shift along z, in chunks.</param>
    /// <param name="chunksY">The shift up, in chunks (0 for the valley).</param>
    internal TemplateBlockIndex Shifted(int chunksX, int chunksZ, int chunksY = 0)
    {
        if (chunksX == 0 && chunksZ == 0 && chunksY == 0)
        {
            return this;
        }

        var (dx, dy, dz) = (chunksX << VoxelChunk.Shift, chunksY << VoxelChunk.Shift, chunksZ << VoxelChunk.Shift);
        return new TemplateBlockIndex(_byKind.ToDictionary(static p => p.Key, p => p.Value.Select(b => b with { X = b.X + dx, Y = b.Y + dy, Z = b.Z + dz }).ToArray()), _refused);
    }

    /// <summary>The positions of the blocks of a kind in the region as it was made, in a fixed order (none for a kind it did not hold).</summary>
    /// <param name="kind">The block kind.</param>
    /// <exception cref="ArgumentException">The kind is air, or fills too many chunks to be indexed.</exception>
    public IReadOnlyList<BlockPos> Of(BlockId kind)
    {
        if (kind == BlockId.Air || _refused.Contains(kind))
        {
            throw new ArgumentException($"{kind} is too common to be listed: the template indexes only the kinds that fill at most {MostChunksPerKind} chunks.", nameof(kind));
        }

        return _byKind.TryGetValue(kind, out var positions) ? positions : None;
    }
}
