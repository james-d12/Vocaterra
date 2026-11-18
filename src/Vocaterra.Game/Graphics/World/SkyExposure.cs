using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// The sky's reach into the ground (B12): where the open sky ends in each block column, from the pure height model (<c>IPlaceInfo.NaturalSurfaceAt</c>: the ground as the world made it, with no
/// tree, roof or edit in it) and the column's blocks as they are now, and the tiles (<see cref="SkySurface"/>) the mesher darkens a chunk's faces from. Where the cell at the natural surface is
/// open (a cave's mouth, a shaft, the cut bank beside a river, an entrance that breaks the surface) the sky reaches down to the first opaque block below it, so the face of a cave that
/// opens to the sky is not darkened; where the chunk at the natural surface is not resident the natural surface stands. Tiles are kept by chunk column and dropped when a chunk of the column
/// is evicted. Used on the loop thread, with the meshing.
/// </summary>
public sealed class SkyExposure : IChunkResidencyListener, IBlockChangeListener
{
    private const int MaxTiles = 8192;
    private const int MaxShaft = 48;

    private readonly VoxelGrid _grid;
    private readonly Func<int, int, int> _natural;
    private readonly Dictionary<(int X, int Z), SkySurface> _tiles = [];

    /// <summary>Creates the exposure over a grid and the pure height model, and listens for evictions.</summary>
    /// <param name="grid">The world.</param>
    /// <param name="naturalSurface">The natural surface y of a block column (<c>IPlaceInfo.NaturalSurfaceAt</c>).</param>
    public SkyExposure(VoxelGrid grid, Func<int, int, int> naturalSurface)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(naturalSurface);
        _grid = grid;
        _natural = naturalSurface;
        grid.AddResidencyListener(this);
        grid.AddListener(this);
    }

    private readonly List<VoxelChunkCoord> _dirty = [];

    /// <summary>The chunks whose darkening an edit may have changed (the ones within the sky's reach below a block placed or removed in a column's open surface), taken once: the renderer meshes them again.</summary>
    public IReadOnlyList<VoxelChunkCoord> TakeDirty()
    {
        if (_dirty.Count == 0)
        {
            return [];
        }

        var taken = _dirty.ToArray();
        _dirty.Clear();
        return taken;
    }

    /// <summary>How many chunk columns have a tile now.</summary>
    public int TileCount => _tiles.Count;

    /// <summary>Stops listening to the grid.</summary>
    public void Detach()
    {
        _grid.RemoveResidencyListener(this);
        _grid.RemoveListener(this);
    }

    /// <summary>The tile of a chunk column, made when it is first asked for.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    public SkySurface Tile(int chunkX, int chunkZ)
    {
        if (_tiles.TryGetValue((chunkX, chunkZ), out var held))
        {
            return held;
        }

        if (_tiles.Count >= MaxTiles)
        {
            _tiles.Clear();
        }

        return _tiles[(chunkX, chunkZ)] = new SkySurface(chunkX, chunkZ, SurfaceOf);
    }

    /// <summary>The surface y of the block column (x, z): the natural surface, or where an opening in it lets the sky down to an opaque block.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public int SurfaceOf(int x, int z)
    {
        var natural = _natural(x, z);
        var y = natural;
        VoxelChunk? chunk = null;
        var chunkY = int.MinValue;
        for (var steps = 0; steps <= MaxShaft; steps++, y--)
        {
            var cy = y >> VoxelChunk.Shift;
            if (cy != chunkY)
            {
                chunkY = cy;
                if (!_grid.TryGetChunk(new VoxelChunkCoord(x >> VoxelChunk.Shift, cy, z >> VoxelChunk.Shift), out chunk))
                {
                    return y == natural ? natural : y + 1;
                }
            }

            var block = chunk![x & (VoxelChunk.Size - 1), y & (VoxelChunk.Size - 1), z & (VoxelChunk.Size - 1)];
            if (BlockDefinition.IsSolid(block) && !BlockDefinition.IsLeaves(block))
            {
                return y;
            }
        }

        return natural;
    }

    /// <summary>The share of the sun's light at a point (<see cref="SkyDepthLight.Factor"/> of its depth), for the eye.</summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    /// <param name="z">The point's z.</param>
    public float FactorAt(float x, float y, float z)
    {
        var bx = (int)MathF.Floor(x);
        var bz = (int)MathF.Floor(z);
        return SkyDepthLight.Factor(SurfaceOf(bx, bz) - (int)MathF.Floor(y));
    }

    /// <inheritdoc />
    public void OnBlockChanged(BlockChange change)
    {
        // Only a change of what stops the sky, at or under the column's natural surface and inside the shaft reach, can move the surface the cells below read.
        var was = BlockDefinition.IsSolid(change.OldBlock) && !BlockDefinition.IsLeaves(change.OldBlock);
        var now = BlockDefinition.IsSolid(change.NewBlock) && !BlockDefinition.IsLeaves(change.NewBlock);
        var p = change.Position;
        if (was == now || p.Y > _natural(p.X, p.Z))
        {
            return;
        }

        var bottom = (p.Y - MaxShaft - SkyDepthLight.FalloffBlocks) >> VoxelChunk.Shift;
        var top = (p.Y + 1) >> VoxelChunk.Shift;
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var cx = (p.X + dx) >> VoxelChunk.Shift;
                var cz = (p.Z + dz) >> VoxelChunk.Shift;
                _tiles.Remove((cx, cz));
                for (var cy = bottom; cy <= top; cy++)
                {
                    var coord = new VoxelChunkCoord(cx, cy, cz);
                    if (!_dirty.Contains(coord))
                    {
                        _dirty.Add(coord);
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public void OnChunkInstalled(VoxelChunkCoord coord)
    {
    }

    /// <inheritdoc />
    public void OnChunkEvicted(VoxelChunkCoord coord) => _tiles.Remove((coord.X, coord.Z));
}

