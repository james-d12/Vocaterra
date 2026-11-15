using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Land;

/// <summary>
/// Where the player may build and where a plot may lie (Y09, D-game-32 (c)): a mask per column built once from the scene and <c>game/land.json</c> (protected, eligible), and
/// <see cref="CheckPlot"/>, which reads the grid as it is now (water, trees, the ground on top, the slope). Pure: no clock, no random numbers, nothing changes after construction.
/// </summary>
public sealed class LandRules
{
    private const byte ProtectionBits = 0b11;
    private const byte EligibleBit = 0x80;

    /// <summary>The blocks above the ground in which a trunk or leaves make a column a tree's (settlement-plan 9.2).</summary>
    public const int TreeProbeHeight = 24;

    private readonly byte[] _mask;
    private readonly int _sizeX;
    private readonly int _sizeZ;

    private LandRules(LandData data, byte[] mask, int sizeX, int sizeZ)
    {
        Data = data;
        _mask = mask;
        _sizeX = sizeX;
        _sizeZ = sizeZ;
    }

    /// <summary>The rules' numbers.</summary>
    public LandData Data { get; }

    /// <summary>The plot's east-west size.</summary>
    public int PlotWidth => Data.PlotWidth;

    /// <summary>The plot's north-south size.</summary>
    public int PlotDepth => Data.PlotDepth;

    /// <summary>The plot's price in coins.</summary>
    public int PlotPrice => Data.PlotPrice;

    /// <summary>
    /// Builds the mask for a scene: the discs round the sites (the position is the scene's own point of that name, the radius and blend are the layout's; a site the scene has no point for
    /// protects and admits nothing), then the squares round the named blocks as they stand in the grid now.
    /// </summary>
    /// <param name="data">The rules.</param>
    /// <param name="layout">The scene's layout (the sites' radius and blend).</param>
    /// <param name="scene">The built scene (the sites' positions and the grid).</param>
    public static LandRules Create(LandData data, SceneLayout layout, Scene scene)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(scene);
        var grid = scene.Grid;
        var mask = new byte[grid.SizeX * grid.SizeZ];
        var sites = layout.Sites.ToDictionary(static s => s.Name, StringComparer.Ordinal);
        foreach (var protectedSite in data.ProtectedSites)
        {
            if (sites.TryGetValue(protectedSite.Site, out var site) && scene.Points.TryGetValue(protectedSite.Site, out var point))
            {
                var radius = site.Radius + (protectedSite.IncludeBlend ? site.Blend : 0) + protectedSite.Margin;
                ForDisc(grid.SizeX, grid.SizeZ, point.X, point.Z, radius, (index, _) => SetProtection(mask, index, protectedSite.Protection));
            }
        }

        foreach (var protectedBlocks in data.ProtectedBlocks)
        {
            foreach (var name in protectedBlocks.Blocks)
            {
                if (BlockDefinition.FromName(name) is not { } definition)
                {
                    continue;
                }

                foreach (var cell in grid.BlocksOfKind(definition.Id))
                {
                    var m = protectedBlocks.Margin;
                    for (var z = Math.Max(0, cell.Z - m); z <= Math.Min(grid.SizeZ - 1, cell.Z + m); z++)
                    {
                        for (var x = Math.Max(0, cell.X - m); x <= Math.Min(grid.SizeX - 1, cell.X + m); x++)
                        {
                            SetProtection(mask, (z * grid.SizeX) + x, protectedBlocks.Protection);
                        }
                    }
                }
            }
        }

        foreach (var meadow in data.Eligible)
        {
            if (scene.Points.TryGetValue(meadow.Site, out var point))
            {
                ForDisc(grid.SizeX, grid.SizeZ, point.X, point.Z, meadow.Radius, (index, _) => mask[index] |= EligibleBit);
            }
        }

        return new LandRules(data, mask, grid.SizeX, grid.SizeZ);
    }

    /// <summary>The protection of a column, or <see cref="Protection.None"/> (also for a column outside the world).</summary>
    /// <param name="x">The column's x.</param>
    /// <param name="z">The column's z.</param>
    public Protection ProtectionAt(int x, int z) => InWorld(x, z) ? (Protection)(_mask[(z * _sizeX) + x] & ProtectionBits) : Protection.None;

    /// <summary>Whether a column is protected.</summary>
    /// <param name="x">The column's x.</param>
    /// <param name="z">The column's z.</param>
    public bool IsProtected(int x, int z) => ProtectionAt(x, z) != Protection.None;

    /// <summary>Whether a column lies in one of the eligible regions (a column outside the world does not).</summary>
    /// <param name="x">The column's x.</param>
    /// <param name="z">The column's z.</param>
    public bool IsEligible(int x, int z) => InWorld(x, z) && (_mask[(z * _sizeX) + x] & EligibleBit) != 0;

    /// <summary>The plot of the rules' size whose middle column is the given one (the stake's column, Y10).</summary>
    /// <param name="x">The middle column's x.</param>
    /// <param name="z">The middle column's z.</param>
    public Plot PlotAround(int x, int z) => new(x - (PlotWidth / 2), z - (PlotDepth / 2), PlotWidth, PlotDepth);

    /// <summary>The plot of the rules' size with its west-north corner at the given column.</summary>
    /// <param name="x">The west column.</param>
    /// <param name="z">The north row.</param>
    public Plot PlotAt(int x, int z) => new(x, z, PlotWidth, PlotDepth);

    /// <summary>
    /// Whether a plot may lie where it is, or why not. In order: a protected column, a column outside the meadows, then, reading the grid, water on top of a column, a tree (a trunk or leaves
    /// in the <see cref="TreeProbeHeight"/> blocks above the ground), a top block that is not grass or dirt, and a slope (the highest and lowest ground differ by more than <see cref="LandData.MaxSlope"/>).
    /// The ground of a column is its highest block that is not air, undergrowth, a trunk or leaves.
    /// </summary>
    /// <param name="grid">The world as it is now.</param>
    /// <param name="plot">The plot to check (the rules' size is not enforced here: the caller makes it with <see cref="PlotAt"/> or <see cref="PlotAround"/>).</param>
    public PlotCheck CheckPlot(IVoxelReader grid, Plot plot)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var outside = false;
        for (var z = plot.Z; z < plot.Z + plot.Depth; z++)
        {
            for (var x = plot.X; x < plot.X + plot.Width; x++)
            {
                var protection = ProtectionAt(x, z);
                if (protection != Protection.None)
                {
                    return new PlotCheck(PlotFault.Protected, protection);
                }

                outside |= !IsEligible(x, z);
            }
        }

        if (outside)
        {
            return new PlotCheck(PlotFault.OutsideMeadows);
        }

        bool water = false, tree = false, notGround = false;
        int low = int.MaxValue, high = int.MinValue;
        for (var z = plot.Z; z < plot.Z + plot.Depth; z++)
        {
            for (var x = plot.X; x < plot.X + plot.Width; x++)
            {
                var (y, block, treeAbove) = GroundOf(grid, x, z);
                water |= block == BlockId.Water;
                tree |= treeAbove;
                notGround |= block is not (BlockId.Grass or BlockId.Dirt);
                low = Math.Min(low, y);
                high = Math.Max(high, y);
            }
        }

        if (water)
        {
            return new PlotCheck(PlotFault.Water);
        }

        if (tree)
        {
            return new PlotCheck(PlotFault.Tree);
        }

        if (notGround)
        {
            return new PlotCheck(PlotFault.NotGround);
        }

        return high - low > Data.MaxSlope ? new PlotCheck(PlotFault.TooSteep) : PlotCheck.Yes;
    }

    /// <summary>
    /// The ground of a column: the y and kind of its highest block that is not air, undergrowth, a trunk or leaves (y -1 and air for an empty column), and whether a trunk or leaves
    /// hang within <see cref="TreeProbeHeight"/> blocks above it.
    /// </summary>
    /// <param name="grid">The world.</param>
    /// <param name="x">The column's x.</param>
    /// <param name="z">The column's z.</param>
    public static (int Y, BlockId Block, bool TreeAbove) GroundOf(IVoxelReader grid, int x, int z)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var lowestTree = int.MaxValue;
        for (var y = Math.Min(grid.HighestBlock(x, z), grid.Limits.EndY - 1); y >= grid.Limits.MinY; y--)
        {
            var block = grid.GetBlock(x, y, z);
            if (block == BlockId.Air || BlockDefinition.IsDecor(block))
            {
                continue;
            }

            if (BlockDefinition.IsTrunk(block) || BlockDefinition.IsLeaves(block))
            {
                lowestTree = y;
                continue;
            }

            return (y, block, lowestTree != int.MaxValue && lowestTree <= y + TreeProbeHeight);
        }

        return (grid.Limits.MinY - 1, BlockId.Air, false);
    }

    private bool InWorld(int x, int z) => (uint)x < (uint)_sizeX && (uint)z < (uint)_sizeZ;

    private static void SetProtection(byte[] mask, int index, Protection protection)
    {
        if ((mask[index] & ProtectionBits) == 0)
        {
            mask[index] = (byte)((mask[index] & ~ProtectionBits) | (byte)protection);
        }
    }

    private static void ForDisc(int sizeX, int sizeZ, int centreX, int centreZ, int radius, Action<int, int> visit)
    {
        long limit = (long)radius * radius;
        for (var z = Math.Max(0, centreZ - radius); z <= Math.Min(sizeZ - 1, centreZ + radius); z++)
        {
            for (var x = Math.Max(0, centreX - radius); x <= Math.Min(sizeX - 1, centreX + radius); x++)
            {
                long dx = x - centreX;
                long dz = z - centreZ;
                if ((dx * dx) + (dz * dz) <= limit)
                {
                    visit((z * sizeX) + x, x);
                }
            }
        }
    }
}
