using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Needs;

/// <summary>
/// The fire rings of a world and whether each burns (N02, adaptation design 9.4): a ring is the eight stones round an empty middle cell (<see cref="BlockId.FireRing"/>), and it
/// is lit while a torch (<see cref="BlockId.Torch"/>) or a scenery torch on a post (<see cref="BlockId.PostTorch"/>) stands in its three-by-three square, on the layer of its stones
/// or on top of them. So the player's own ring burns once its torch is on the rim, the guide's camp ring likewise, and the forge, whose post torch stands in its middle, always
/// burns. Resting at a lit fire is standing still within the data's radius of the ring's middle. The rings are found from the stones, by shape, never by name: when the constructor runs and again on <see cref="Rescan"/> (a ring built while the game runs). One finder serves the whole session (the rest rule, the crafting stations, the sleep places and the free ground to wake on).
/// </summary>
public sealed class FireRings
{
    private readonly VoxelGrid _grid;
    private readonly HashSet<BlockPos> _stones;
    private IReadOnlyList<BlockPos> _middles = [];

    /// <summary>
    /// Finds the rings of a scene: from the stones the world holds now that the region template held or that sit in a changed chunk (<see cref="Scene.CurrentBlocksOfKind"/>), and from every stone
    /// the world has set or broken since (B06: a streamed world has no list of its chunks to scan, so the finder is told what changes, as a bounded world tells it too).
    /// </summary>
    /// <param name="scene">The scene (its grid is read live, so a torch put on or taken off a rim counts at once).</param>
    public FireRings(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        _grid = scene.Grid;
        _stones = [.. scene.CurrentBlocksOfKind(BlockId.FireRing)];
        _grid.AddListener(new StoneListener(_stones));
        Rescan();
    }

    /// <summary>Finds the rings again from the stones the world holds now: call it after a ring's blocks are set while the game runs.</summary>
    public void Rescan()
    {
        var grid = _grid;
        var middles = new HashSet<BlockPos>();
        foreach (var stone in _stones.Where(s => grid.InBounds(s.X, s.Y, s.Z) && grid.GetBlock(s.X, s.Y, s.Z) == BlockId.FireRing).ToList())
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dz = -1; dz <= 1; dz++)
                {
                    var middle = new BlockPos(stone.X + dx, stone.Y, stone.Z + dz);
                    if ((dx != 0 || dz != 0) && IsMiddle(grid, middle))
                    {
                        middles.Add(middle);
                    }
                }
            }
        }

        _middles = middles.OrderBy(static m => m.X).ThenBy(static m => m.Z).ThenBy(static m => m.Y).ToList();
    }

    /// <summary>The middle cell of every fire ring, in a fixed order.</summary>
    public IReadOnlyList<BlockPos> Middles => _middles;

    /// <summary>Whether the ring with this middle cell burns now.</summary>
    /// <param name="middle">One of <see cref="Middles"/>.</param>
    public bool IsLit(BlockPos middle)
    {
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dy = 0; dy <= 1; dy++)
                {
                    var (x, y, z) = (middle.X + dx, middle.Y + dy, middle.Z + dz);
                    if (_grid.InBounds(x, y, z) && _grid.GetBlock(x, y, z) is BlockId.Torch or BlockId.PostTorch)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Whether the stone at <paramref name="stone"/> belongs to a ring that burns now (N07: the fire as a crafting station).</summary>
    /// <param name="stone">A <see cref="BlockId.FireRing"/> cell.</param>
    public bool StoneBurns(BlockPos stone)
    {
        foreach (var middle in _middles)
        {
            if (middle.Y == stone.Y && Math.Abs(middle.X - stone.X) <= 1 && Math.Abs(middle.Z - stone.Z) <= 1 && IsLit(middle))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a lit ring's middle lies within <paramref name="blocks"/> across of the feet (and within six blocks in height): the player is at the fire.</summary>
    /// <param name="feet">The block the player's feet are in.</param>
    /// <param name="blocks">The radius in blocks.</param>
    public bool NearLit(BlockPos feet, int blocks)
    {
        foreach (var middle in _middles)
        {
            long dx = middle.X - feet.X;
            long dz = middle.Z - feet.Z;
            if ((dx * dx) + (dz * dz) <= (long)blocks * blocks && Math.Abs(middle.Y - feet.Y) <= 6 && IsLit(middle))
            {
                return true;
            }
        }

        return false;
    }

    // Keeps the set of cells that hold a fire-ring stone up to date: a cell that becomes one joins it, a cell that stops being one leaves it.
    private sealed class StoneListener(HashSet<BlockPos> stones) : IBlockChangeListener
    {
        public void OnBlockChanged(BlockChange change)
        {
            if (change.NewBlock == BlockId.FireRing)
            {
                _ = stones.Add(change.Position);
            }
            else if (change.OldBlock == BlockId.FireRing)
            {
                _ = stones.Remove(change.Position);
            }
        }
    }

    private static bool IsMiddle(IVoxelReader grid, BlockPos cell)
    {
        if (grid.InBounds(cell.X, cell.Y, cell.Z) && grid.GetBlock(cell.X, cell.Y, cell.Z) == BlockId.FireRing)
        {
            return false;
        }

        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                if ((dx != 0 || dz != 0) && (!grid.InBounds(cell.X + dx, cell.Y, cell.Z + dz) || grid.GetBlock(cell.X + dx, cell.Y, cell.Z + dz) != BlockId.FireRing))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
