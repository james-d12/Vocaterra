using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Land;

/// <summary>Why the camp cannot be set up where the player aimed (Y12), in the order <see cref="CampPlan.Check"/> finds them.</summary>
public enum CampFault
{
    /// <summary>No fault: the camp may stand there.</summary>
    None = 0,

    /// <summary>A column of the footprint lies outside the plot the player owns.</summary>
    NotOwned = 1,

    /// <summary>The ground under the footprint is not at one height.</summary>
    NotLevel = 2,

    /// <summary>A cell of the camp is taken (a block, a bush, a plank), the ground under a column is not plain ground, or a column is outside the world.</summary>
    Blocked = 3,

    /// <summary>No cell to wake on that is free and reached on foot from the bed, and from which the player walks out of the camp.</summary>
    NoWake = 4,
}

/// <summary>
/// The camp the camp kit sets up (Y12, D-game-32): the scene layout's <c>camp</c> stamp turned to face a side and laid on a corner at a ground height, as one rigid piece. Pure: it names the
/// cells and the blocks and checks them against a grid, and changes nothing. The session sets the blocks, keeps the plan (saved as <see cref="CampSave"/>) and makes the sleep places at
/// its <see cref="Bed"/> and <see cref="Fire"/> cells. The stamp's turning is <see cref="StampSpec.Turn"/>, the scene build's own.
/// </summary>
public sealed class CampPlan
{
    /// <summary>The name of the stamp the camp is drawn as, in <c>game/scene.json</c>'s stamps.</summary>
    public const string StampName = "camp";

    /// <summary>The anchor of the fire ring's middle, the cell the player aims at.</summary>
    public const string FireAnchor = "fire";

    /// <summary>The anchor of the bedroll's head cell.</summary>
    public const string BedAnchor = "bed";

    /// <summary>The anchor of the cell the player wakes on after a death.</summary>
    public const string WakeAnchor = "wake";

    /// <summary>The anchors the camp stamp must have.</summary>
    public static IReadOnlyList<string> RequiredAnchors { get; } = [FireAnchor, BedAnchor, WakeAnchor];

    private readonly StampSpec _shape;
    private readonly List<(BlockPos Cell, BlockId Block)> _blocks = [];

    /// <summary>Lays the camp stamp on a corner.</summary>
    /// <param name="shape">The camp stamp (it must name the three anchors).</param>
    /// <param name="facing">The side the camp's front, with the fire, faces.</param>
    /// <param name="cornerX">The west column of the turned footprint.</param>
    /// <param name="cornerZ">The north row of the turned footprint.</param>
    /// <param name="groundY">The height of the ground the camp stands on: the first layer lies one above it.</param>
    public CampPlan(StampSpec shape, Direction facing, int cornerX, int cornerZ, int groundY)
    {
        ArgumentNullException.ThrowIfNull(shape);
        _shape = shape;
        Facing = facing;
        CornerX = cornerX;
        CornerZ = cornerZ;
        GroundY = groundY;
        Width = shape.WidthFacing(facing);
        Depth = shape.DepthFacing(facing);
        Height = shape.Layers.Count;
        Fire = Anchor(FireAnchor);
        Bed = Anchor(BedAnchor);
        Wake = Anchor(WakeAnchor);
        foreach (var (dx, dz, layer, block) in shape.Cells(facing))
        {
            _blocks.Add((new BlockPos(cornerX + dx, groundY + 1 + layer, cornerZ + dz), block));
        }
    }

    /// <summary>Lays the camp so that the fire ring's middle is on the aimed ground column.</summary>
    /// <param name="shape">The camp stamp.</param>
    /// <param name="aimX">The column's x.</param>
    /// <param name="aimZ">The column's z.</param>
    /// <param name="groundY">The ground's height there.</param>
    /// <param name="facing">The side the camp's front faces.</param>
    public static CampPlan Around(StampSpec shape, int aimX, int aimZ, int groundY, Direction facing)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var (dx, dz) = shape.AnchorOffset(FireAnchor, facing) ?? throw new ArgumentException("The camp stamp has no fire anchor.", nameof(shape));
        return new CampPlan(shape, facing, aimX - dx, aimZ - dz, groundY);
    }

    /// <summary>The side the camp's front faces.</summary>
    public Direction Facing { get; }

    /// <summary>The west column of the footprint.</summary>
    public int CornerX { get; }

    /// <summary>The north row of the footprint.</summary>
    public int CornerZ { get; }

    /// <summary>The height of the ground under the camp.</summary>
    public int GroundY { get; }

    /// <summary>The footprint's east-west size.</summary>
    public int Width { get; }

    /// <summary>The footprint's north-south size.</summary>
    public int Depth { get; }

    /// <summary>The camp's height in layers.</summary>
    public int Height { get; }

    /// <summary>The fire ring's middle cell (an empty cell inside the ring's stones, on the first layer).</summary>
    public BlockPos Fire { get; }

    /// <summary>The bedroll's head cell.</summary>
    public BlockPos Bed { get; }

    /// <summary>The cell the player wakes on after a death.</summary>
    public BlockPos Wake { get; }

    /// <summary>Every block the camp puts down, in the order the scene builds a stamp's cells.</summary>
    public IReadOnlyList<(BlockPos Cell, BlockId Block)> Blocks => _blocks;

    /// <summary>The cells of the fire ring's stones.</summary>
    public IReadOnlyList<BlockPos> RingStones => _blocks.Where(static b => b.Block == BlockId.FireRing).Select(static b => b.Cell).ToList();

    /// <summary>Whether the cell is one of the camp's block cells.</summary>
    /// <param name="cell">The cell.</param>
    public bool IsBlock(BlockPos cell) => _blocks.Any(b => b.Cell == cell);

    /// <summary>Whether the column lies in the camp's footprint.</summary>
    /// <param name="x">The column's x.</param>
    /// <param name="z">The column's z.</param>
    public bool Covers(int x, int z) => x >= CornerX && x < CornerX + Width && z >= CornerZ && z < CornerZ + Depth;

    /// <summary>Whether every block of the camp stands in the world as it was set: the camp is whole.</summary>
    /// <param name="grid">The world.</param>
    public bool Stands(VoxelGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return _blocks.All(b => grid.InBounds(b.Cell.X, b.Cell.Y, b.Cell.Z) && grid.GetBlock(b.Cell.X, b.Cell.Y, b.Cell.Z) == b.Block);
    }

    /// <summary>
    /// Whether the camp may be set where it lies, or why not, reading the grid and changing nothing. In order: a column outside the owned land, the ground not at one height,
    /// a cell taken (only air and undergrowth are cleared), a column whose ground is not plain grass, dirt, sand or a path, or outside the world.
    /// </summary>
    /// <param name="grid">The world as it is now.</param>
    /// <param name="owned">Whether a column is the player's land.</param>
    public CampFault Check(VoxelGrid grid, Func<int, int, bool> owned)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(owned);
        for (var z = CornerZ; z < CornerZ + Depth; z++)
        {
            for (var x = CornerX; x < CornerX + Width; x++)
            {
                if (!owned(x, z))
                {
                    return CampFault.NotOwned;
                }
            }
        }

        if (!grid.InBounds(CornerX, 0, CornerZ) || !grid.InBounds(CornerX + Width - 1, GroundY + Height, CornerZ + Depth - 1))
        {
            return CampFault.Blocked;
        }

        for (var z = CornerZ; z < CornerZ + Depth; z++)
        {
            for (var x = CornerX; x < CornerX + Width; x++)
            {
                if (GroundBelow(grid, x, z, GroundY) != GroundY)
                {
                    return CampFault.NotLevel;
                }
            }
        }

        for (var z = CornerZ; z < CornerZ + Depth; z++)
        {
            for (var x = CornerX; x < CornerX + Width; x++)
            {
                if (grid.GetBlock(x, GroundY, z) is not (BlockId.Grass or BlockId.Dirt or BlockId.Sand or BlockId.Path))
                {
                    return CampFault.Blocked;
                }

                for (var layer = 0; layer < Height; layer++)
                {
                    var id = grid.GetBlock(x, GroundY + 1 + layer, z);
                    if (id != BlockId.Air && !BlockDefinition.IsUndergrowth(id))
                    {
                        return CampFault.Blocked;
                    }
                }
            }
        }

        return CampFault.None;
    }

    /// <summary>
    /// The height of the ground of a column at or below a height: the highest solid block (or water) from there down, so what stands higher (a plank, a stone) is left to the check of the free cells, and a hole is not level. -1 for none.
    /// </summary>
    /// <param name="grid">The world.</param>
    /// <param name="x">The column's x.</param>
    /// <param name="z">The column's z.</param>
    /// <param name="from">The height to look from.</param>
    public static int GroundBelow(VoxelGrid grid, int x, int z, int from)
    {
        ArgumentNullException.ThrowIfNull(grid);
        for (var y = Math.Min(from, grid.SizeY - 1); y >= 0; y--)
        {
            var id = grid.GetBlock(x, y, z);
            if (BlockDefinition.IsSolid(id) || id == BlockId.Water)
            {
                return y;
            }
        }

        return -1;
    }

    /// <summary>
    /// The height of a column's ground: its highest solid block (or water), so a bush, a torch or any other thing that is not solid does not count as ground. -1 for an empty column.
    /// </summary>
    /// <param name="grid">The world.</param>
    /// <param name="x">The column's x.</param>
    /// <param name="z">The column's z.</param>
    public static int GroundOf(VoxelGrid grid, int x, int z)
    {
        ArgumentNullException.ThrowIfNull(grid);
        for (var y = Math.Min(grid.HighestBlock(x, z), grid.SizeY - 1); y >= 0; y--)
        {
            var id = grid.GetBlock(x, y, z);
            if (BlockDefinition.IsSolid(id) || id == BlockId.Water)
            {
                return y;
            }
        }

        return -1;
    }

    /// <summary>Sets the camp's blocks (undergrowth in the camp's way goes), without any check: <see cref="Check"/> first.</summary>
    /// <param name="grid">The world.</param>
    public void Place(VoxelGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        foreach (var cell in Cleared(grid))
        {
            grid.SetBlock(cell.X, cell.Y, cell.Z, BlockId.Air);
        }

        foreach (var (cell, block) in _blocks)
        {
            grid.SetBlock(cell.X, cell.Y, cell.Z, block);
        }
    }

    // The cells of the footprint's layers that something stands in now, which setting the camp clears before its blocks go in.
    private List<BlockPos> Cleared(VoxelGrid grid)
    {
        var cleared = new List<BlockPos>();
        for (var z = CornerZ; z < CornerZ + Depth; z++)
        {
            for (var x = CornerX; x < CornerX + Width; x++)
            {
                for (var layer = 0; layer < Height; layer++)
                {
                    var y = GroundY + 1 + layer;
                    if (grid.GetBlock(x, y, z) != BlockId.Air)
                    {
                        cleared.Add(new BlockPos(x, y, z));
                    }
                }
            }
        }

        return cleared;
    }

    // The world as setting the camp would leave it, as changed cells over the grid: the cleared cells, then the camp's blocks.
    private Dictionary<BlockPos, BlockId> Cells(VoxelGrid grid)
    {
        var cells = Cleared(grid).ToDictionary(static cell => cell, static _ => BlockId.Air);
        foreach (var (cell, block) in _blocks)
        {
            cells[cell] = block;
        }

        return cells;
    }

    /// <summary>The most steps on foot from the bed to the wake cell, and the most from the wake cell to the open ground, the walk of <see cref="HasWake"/>.</summary>
    public const int WakeWalkSteps = 14;

    /// <summary>
    /// Whether the camp, once set, leaves a cell to wake on: the wake cell can be stood in, it is reached on foot from the bed (the walk of the sleep places, Y05a) and from it a body walks out of the
    /// footprint. The world is read as it would be with the camp's blocks in it, so a plank wall round the camp says no; the grid itself is only read (no edit, no block change event).
    /// </summary>
    /// <param name="grid">The world as it is now, which <see cref="Check"/> found fit.</param>
    public bool HasWake(VoxelGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var world = new Session.SleepWalk.WalkView(grid, Cells(grid));
        if (Session.SleepWalk.StartFor(world, Wake) != Wake || Session.SleepWalk.StartFor(world, Bed) is not { } bedStart || !Session.SleepWalk.Reaches(world, bedStart, Wake, WakeWalkSteps))
        {
            return false;
        }

        return Session.SleepWalk.ReachFrom(world, Wake, WakeWalkSteps).Keys.Any(cell => !Covers(cell.X, cell.Z));
    }

    /// <summary>The entry a save keeps.</summary>
    public CampSave ToSave() => new(CornerX, CornerZ, GroundY, Facing.ToString().ToLowerInvariant());

    private BlockPos Anchor(string name)
    {
        var (dx, dz) = _shape.AnchorOffset(name, Facing) ?? throw new ArgumentException($"The camp stamp has no '{name}' anchor.");
        return new BlockPos(CornerX + dx, GroundY + 1, CornerZ + dz);
    }
}

/// <summary>
/// The camp the player set up, as a save keeps it (<c>systems.sleep.camp</c>, Y12): where its footprint lies and which way it faces; its cells and blocks come from the camp stamp, so a camp
/// is never rediscovered from the world. The sleep places it made are saved beside it with their cells.
/// </summary>
/// <param name="X">The west column of the footprint.</param>
/// <param name="Z">The north row of the footprint.</param>
/// <param name="Ground">The height of the ground under the camp.</param>
/// <param name="Facing">The side the camp's front faces: <c>north</c>, <c>east</c>, <c>south</c> or <c>west</c>.</param>
public sealed record CampSave(int X, int Z, int Ground, string Facing)
{
    /// <summary>The side the camp faces, or <see langword="null"/> when the text is not a side.</summary>
    internal Direction? ToDirection() => Enum.TryParse<Direction>(Facing, ignoreCase: true, out var facing) && Enum.IsDefined(facing) ? facing : null;
}
