using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World.Generation;

/// <summary>
/// The strategy that chooses the natural ground of a layout (B15, biomes plan section 3.6): the height of every column before the waterfall, the river, the lake, the paths and the sites are cut
/// into it. <see cref="HandDrawnGround"/> is the valley's (the layout's relief and the seed's noise, as it always was); <see cref="ProceduralGround"/> reads the procedural world's height at the
/// region's place, so a town stands on the land the world has there. Implementations are pure and immutable.
/// </summary>
internal interface IGroundShape
{
    /// <summary>The natural ground of the layout's column (x, z) in sixteenths of a block, in the layout's own coordinates (y 0 is the grid's floor).</summary>
    /// <param name="x">The layout column.</param>
    /// <param name="z">The layout row.</param>
    /// <param name="featureDistance16">How far the column is from the nearest river, path or level site in sixteenths of a block beyond the feature's own reach (<see cref="int.MaxValue"/> when none is near): a shape may calm its noise towards it.</param>
    int Height16(int x, int z, int featureDistance16);
}

/// <summary>
/// The hand-drawn relief (the valley's ground): a smooth surface through the layout's chunk-corner heights, looked up at a point moved by slow noise, with the seed's hills and crags added and the
/// cliffs stepped. The noise is damped near the river, the paths and the sites, so the seed changes the land round them and never the places themselves. Moved here from <see cref="TerrainGenerator"/>
/// unchanged, so the valley's ground is byte for byte what it was.
/// </summary>
internal sealed class HandDrawnGround : IGroundShape
{
    /// <summary>How far from a feature, in blocks, the hand-drawn noise is calmed.</summary>
    internal const int CalmReach = 24;

    private readonly TerrainSpec _spec;
    private readonly TerrainNoise _noise;
    private readonly int _w;
    private readonly int _d;

    public HandDrawnGround(TerrainSpec spec, TerrainNoise noise, int width, int depth)
    {
        _spec = spec;
        _noise = noise;
        _w = width;
        _d = depth;
    }

    /// <inheritdoc />
    public int Height16(int x, int z, int featureDistance16)
    {
        var valley16 = _spec.Valley * 16;
        var calm = Math.Max(300, IntMath.Ramp(featureDistance16 == int.MaxValue ? CalmReach * 16 : featureDistance16, 0, CalmReach * 16));

        // S14: the relief is looked up at a point moved by slow noise (a wide bend and a small wiggle), so the walls of the valley are not the straight
        // lines of the chunk grid. The move is damped near the sites, the paths and the river, like the other noise, so they keep their ground.
        var warpX = ((_noise.Value(20, x, z, 120) - 2048) * 64 / 2048) + ((_noise.Value(22, x, z, 44) - 2048) * 22 / 2048);
        var warpZ = ((_noise.Value(21, x, z, 120) - 2048) * 64 / 2048) + ((_noise.Value(23, x, z, 44) - 2048) * 22 / 2048);
        warpX = (int)((long)warpX * calm / IntMath.One);
        warpZ = (int)((long)warpZ * calm / IntMath.One);
        var basis = Relief16(Math.Clamp(x + warpX, 0, _w - 1), Math.Clamp(z + warpZ, 0, _d - 1));
        var above16 = Math.Max(0, basis - valley16);
        var amp16 = 24 + (int)(above16 * 16L / 100);
        var detail = (int)((long)_noise.Hills(x, z) * amp16 / IntMath.One * calm / IntMath.One);
        var crag = (int)((long)(_noise.Value(5, x, z, 5) - 2048) * 48 / 2048 * IntMath.Ramp(above16, 30 * 16, 60 * 16) / IntMath.One * calm / IntMath.One);
        var h = basis + detail + crag;
        return Terrace(h);
    }

    // The macro relief at a column in 1/16 blocks: a smooth surface through the chunk-corner heights (Catmull-Rom both ways).
    private int Relief16(int x, int z)
    {
        var cols = _spec.ReliefColumns;
        var rows = _spec.ReliefRows;
        var gx = x >> 4;
        var gz = z >> 4;
        var tx = (x & 15) << 8;
        var tz = (z & 15) << 8;
        Span<int> column = stackalloc int[4];
        for (var j = 0; j < 4; j++)
        {
            var row = Math.Clamp(gz + j - 1, 0, rows - 1);
            int P(int i) => _spec.Relief[(row * cols) + Math.Clamp(gx + i - 1, 0, cols - 1)] * 16;
            column[j] = IntMath.CatmullRom(P(0), P(1), P(2), P(3), tx);
        }

        return IntMath.CatmullRom(column[0], column[1], column[2], column[3], tz);
    }

    // Stepped faces: above the foothills the ground climbs in flat shelves of Terrace blocks joined by short risers (the shared Terracing helper).
    private int Terrace(int h16) => Terracing.Apply(h16, _spec.Terrace, 64 * 16, 90 * 16);
}

/// <summary>
/// The procedural ground (B15): the natural ground of a layout placed in the world is the height the procedural world has at that place, so a region built on it stands on the land as it is and
/// its border meets the land outside. The layout's column (x, z) is the world's column (originX + x, originZ + z), and the height is shifted down by the origin's y, since a layout's y 0 is its
/// grid's floor. It adds no noise of its own and calms nothing: the world's own relief is already the natural ground, and only the sites and the paths level it.
/// </summary>
internal sealed class ProceduralGround : IGroundShape
{
    private readonly IGroundHeights _heights;
    private readonly int _originX;
    private readonly int _originY;
    private readonly int _originZ;

    /// <summary>Creates the shape of a layout whose first column, first layer and first row are the world's (<paramref name="originX"/>, <paramref name="originY"/>, <paramref name="originZ"/>).</summary>
    /// <param name="heights">The procedural world's ground.</param>
    /// <param name="originX">The world x of the layout's column 0.</param>
    /// <param name="originY">The world y of the layout's layer 0.</param>
    /// <param name="originZ">The world z of the layout's row 0.</param>
    public ProceduralGround(IGroundHeights heights, int originX, int originY, int originZ)
    {
        _heights = heights;
        _originX = originX;
        _originY = originY;
        _originZ = originZ;
    }

    /// <inheritdoc />
    public int Height16(int x, int z, int featureDistance16) => (_heights.HeightAt(_originX + x, _originZ + z) - _originY) * 16;
}

/// <summary>The ground of the procedural world in world coordinates: the y of the topmost solid block of a column (<see cref="Procedural.ColumnShape.Height"/>). Pure and immutable.</summary>
internal interface IGroundHeights
{
    /// <summary>The ground of the world's column (x, z).</summary>
    /// <param name="x">The world x.</param>
    /// <param name="z">The world z.</param>
    int HeightAt(int x, int z);
}
