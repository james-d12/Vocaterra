using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Currents;
using Vocaterra.Game.Graphics.Water;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>The mesher's flowing water (water-plan 4.8): corner heights from the levels, both surface quads on the same corners, the picture turned to the current.</summary>
public sealed class WaterMeshTests
{
    private const float Sixteenth = 1f / 16f;

    private static ChunkMeshes Mesh(MapBlocks blocks) => ChunkMesher.Build(blocks, 0, 0, 0);

    // The quads of the translucent mesh, each as its four vertices.
    private static List<MeshVertex[]> Quads(ChunkMeshes meshes)
    {
        var v = meshes.Alpha.Vertices;
        var quads = new List<MeshVertex[]>();
        for (var q = 0; q + 3 < v.Length; q += 4)
        {
            quads.Add([v[q], v[q + 1], v[q + 2], v[q + 3]]);
        }

        return quads;
    }

    // The y of the normal of the quad's first triangle: positive for a face seen from above.
    private static float NormalY(MeshVertex[] quad) =>
        ((quad[1].Z - quad[0].Z) * (quad[2].X - quad[0].X)) - ((quad[1].X - quad[0].X) * (quad[2].Z - quad[0].Z));

    // The up-facing quad of the cell whose west-north corner is at (x, z): the quad whose four corners span x..x+1 and z..z+1 and face up.
    private static MeshVertex[] Up(ChunkMeshes meshes, int x, int z) =>
        Quads(meshes).Single(q => NormalY(q) > 0 && q.Min(static v => v.X) == x && q.Min(static v => v.Z) == z && q.Max(static v => v.X) == x + 1);

    private static MeshVertex[] Down(ChunkMeshes meshes, int x, int z) =>
        Quads(meshes).Single(q => NormalY(q) < 0 && q.Min(static v => v.X) == x && q.Min(static v => v.Z) == z && q.Max(static v => v.X) == x + 1 && q.Min(static v => v.Y) < q.Max(static v => v.Y) - 0.0001f);

    private static float HeightAt(MeshVertex[] quad, float x, float z) => quad.Single(v => v.X == x && v.Z == z).Y;

    private static MapBlocks Floor(int y = 4) => new MapBlocks().Fill(0, y, 0, 15, y, 15, BlockId.Stone);

    [Fact]
    public void Given_ASourceAmongSources_When_Meshed_Then_AllFourCornersAreFourteenSixteenths()
    {
        var blocks = Floor().Fill(4, 5, 4, 6, 5, 6, BlockId.Water);

        var top = Up(Mesh(blocks), 5, 5);

        Assert.All(top, v => Assert.Equal(5 + (14 * Sixteenth), v.Y, 4));
    }

    [Fact]
    public void Given_ALoneSourceOnOpenGround_When_Meshed_Then_ItsCornersSlopeDownToTheAir()
    {
        var blocks = Floor().Water(5, 5, 5);

        var top = Up(Mesh(blocks), 5, 5);

        Assert.All(top, v => Assert.InRange(v.Y, 5.5f, 5.8f));
    }

    [Fact]
    public void Given_AFlowingSlopeBetweenWalls_When_Meshed_Then_TheSurfaceFallsFromTheSourceToTheThinEnd()
    {
        // A channel: a source at x = 4, then flowing 1 to 7 at x = 5 to 11, with walls on both sides.
        var blocks = Floor().Fill(3, 5, 4, 12, 5, 4, BlockId.Stone).Fill(3, 5, 6, 12, 5, 6, BlockId.Stone).Water(4, 5, 5);
        for (var d = 1; d <= 7; d++)
        {
            blocks.Water(4 + d, 5, 5, WaterLevel.Flowing(d));
        }

        var meshes = Mesh(blocks);
        var last = float.MaxValue;
        for (var x = 5; x <= 11; x++) // (the source's own west edge, at x = 4, dips towards the open air beyond it)
        {
            var west = HeightAt(Up(meshes, x, 5), x, 5);
            Assert.True(west < last, $"the surface rises at x = {x}: {west} after {last}");
            last = west;
        }

        // The corner between the distance-3 and distance-4 cells (8/16 and 7/16, both light) is their mean.
        Assert.Equal(5 + (7.5f * Sixteenth), HeightAt(Up(meshes, 8, 5), 8, 5), 3);
        Assert.Equal(5 + (1 * Sixteenth), HeightAt(Up(meshes, 11, 5), 12, 5), 3); // the far end meets open air: half of the last cell's 2/16
    }

    [Fact]
    public void Given_ASlopedCell_When_Meshed_Then_TheUpAndDownQuadsShareTheFourCorners()
    {
        var blocks = Floor().Fill(3, 5, 4, 8, 5, 4, BlockId.Stone).Fill(3, 5, 6, 8, 5, 6, BlockId.Stone).Water(4, 5, 5)
            .Water(5, 5, 5, WaterLevel.Flowing(1)).Water(6, 5, 5, WaterLevel.Flowing(5));
        var meshes = Mesh(blocks);

        var top = Quads(meshes).Where(q => q.Min(static v => v.X) == 5 && q.Max(static v => v.X) == 6 && q.Min(static v => v.Y) > 5.05f).ToList();

        Assert.Equal(2, top.Count);
        var up = top.Single(static q => NormalY(q) > 0);
        var down = top.Single(static q => NormalY(q) < 0);
        Assert.Equal(up.Select(static v => (v.X, v.Y, v.Z)).Order().ToList(), down.Select(static v => (v.X, v.Y, v.Z)).Order().ToList());
        Assert.NotEqual(up[0].Y, up[2].Y); // and the cell really is sloped
    }

    [Fact]
    public void Given_AColumnOfFallingWater_When_Meshed_Then_EachCellIsFullAndItsSidesReachTheCellAbove()
    {
        var blocks = Floor();
        for (var y = 5; y <= 8; y++)
        {
            blocks.Water(5, y, 5, WaterLevel.Flowing(1, falling: true));
        }

        var meshes = Mesh(blocks);
        var sides = Quads(meshes).Where(static q => Math.Abs(NormalY(q)) < 0.001f).ToList();

        // Four open sides on each of the four cells; the middle ones go from their floor to the floor of the cell above.
        Assert.Equal(16, sides.Count);
        foreach (var y in new[] { 5, 6, 7 })
        {
            var cell = sides.Where(q => q.Min(static v => v.Y) == y).ToList();
            Assert.Equal(4, cell.Count);
            Assert.All(cell, q => Assert.Equal(y + 1f, q.Max(static v => v.Y), 4));
        }

        // The top cell has air above, so it is the only one with a surface.
        Assert.Single(Quads(meshes), static q => NormalY(q) > 0);
    }

    [Fact]
    public void Given_ACellWithWaterAbove_When_Meshed_Then_ItsSidesAreFullHeightAndItHasNoSurface()
    {
        var blocks = Floor().Water(5, 5, 5).Water(5, 6, 5);
        var meshes = Mesh(blocks);

        var lowerSides = Quads(meshes).Where(static q => Math.Abs(NormalY(q)) < 0.001f && q.Min(static v => v.Y) == 5f).ToList();

        Assert.Equal(4, lowerSides.Count);
        Assert.All(lowerSides, q => Assert.Equal(6f, q.Max(static v => v.Y), 4));
        Assert.DoesNotContain(Quads(meshes), static q => NormalY(q) > 0 && q.Max(static v => v.Y) < 6f && q.Min(static v => v.Y) >= 5f);
    }

    [Fact]
    public void Given_ASurfaceBesideAColumnWithWaterAbove_When_Meshed_Then_TheSharedCornerIsFull()
    {
        // The cell at x = 5 stands beside the cell at x = 6, which has water above it: the edge between them rises to the top of the cell.
        var blocks = Floor().Water(5, 5, 5).Water(6, 5, 5).Water(6, 6, 5);
        var meshes = Mesh(blocks);

        var top = Up(meshes, 5, 5);

        Assert.Equal(6f, HeightAt(top, 6, 5), 4);
        Assert.Equal(6f, HeightAt(top, 6, 6), 4);
        Assert.True(HeightAt(top, 5, 5) < 6f);
    }

    [Fact]
    public void Given_AnEastCurrent_When_Meshed_Then_TheTextureRunsEastAcrossTheCell()
    {
        var blocks = Floor().Water(5, 5, 5, flow: new FlowVector(FlowVector.One, 0, 0));

        var top = Up(Mesh(blocks), 5, 5);

        var west = top.Where(static v => v.X == 5f).Select(static v => v.U).Distinct().Single();
        var east = top.Where(static v => v.X == 6f).Select(static v => v.U).Distinct().Single();
        Assert.True(east > west, "the picture's horizontal, along which the ripples slide, points east");
        Assert.Equal(top.Single(static v => v.X == 5f && v.Z == 5f).V, top.Single(static v => v.X == 6f && v.Z == 5f).V, 5); // nothing across the current changes along it
        Assert.All(top, static v => Assert.Equal(WaterTile.ShallowFlow, WaterTileLayout.TileAt(v.U, v.V).Tile)); // one block of water over the bed is shallow
    }

    [Fact]
    public void Given_ASouthCurrent_When_Meshed_Then_TheTextureRunsSouthAcrossTheCell()
    {
        var blocks = Floor().Water(5, 5, 5, flow: new FlowVector(0, 0, FlowVector.One));

        var top = Up(Mesh(blocks), 5, 5);

        var north = top.Where(static v => v.Z == 5f).Select(static v => v.U).Distinct().Single();
        var south = top.Where(static v => v.Z == 6f).Select(static v => v.U).Distinct().Single();
        Assert.True(south > north);
    }

    [Fact]
    public void Given_StillWater_When_Meshed_Then_TheStillPictureIsLaidUnturned()
    {
        var blocks = Floor().Water(5, 5, 5);

        var top = Up(Mesh(blocks), 5, 5);

        Assert.All(top, static v => Assert.Equal(WaterTile.ShallowStill, WaterTileLayout.TileAt(v.U, v.V).Tile));
        var northWest = top.Single(static v => v.X == 5f && v.Z == 5f);
        var southEast = top.Single(static v => v.X == 6f && v.Z == 6f);
        Assert.True(southEast.U > northWest.U);
        Assert.True(southEast.V > northWest.V);
    }

    [Fact]
    public void Given_ASlantedCurrent_When_Meshed_Then_EveryCornerStaysInsideItsTile()
    {
        var blocks = Floor().Fill(4, 5, 4, 6, 5, 6, BlockId.Water).Water(5, 5, 5, flow: new FlowVector(180, 0, 180));

        var top = Up(Mesh(blocks), 5, 5);

        var (u0, v0) = WaterTileLayout.Uv(WaterTile.ShallowFlow, false, 0f, 0f);
        var (u1, v1) = WaterTileLayout.Uv(WaterTile.ShallowFlow, false, 1f, 1f);
        Assert.All(top, v =>
        {
            Assert.InRange(v.U, u0 - 1e-5f, u1 + 1e-5f);
            Assert.InRange(v.V, v0 - 1e-5f, v1 + 1e-5f);
        });
    }

    [Fact]
    public void Given_AFallingCell_When_ItsSheetIsMeshed_Then_TheFallingPictureRunsDownTheFace()
    {
        var blocks = Floor();
        for (var y = 5; y <= 7; y++)
        {
            blocks.Water(5, y, 5, WaterLevel.Flowing(1, falling: true), new FlowVector(0, -FlowVector.One, 0));
        }

        var sheet = Quads(Mesh(blocks)).Where(static q => Math.Abs(NormalY(q)) < 0.001f && q.All(static v => WaterTileLayout.TileAt(v.U, v.V).Tile == WaterTile.Falling)).ToList();

        Assert.Equal(12, sheet.Count);
        foreach (var quad in sheet)
        {
            var top = quad.Where(static v => v.Y > 0).OrderByDescending(static v => v.Y).First();
            var bottom = quad.OrderBy(static v => v.Y).First();
            Assert.True(bottom.V > top.V, "the picture's down is the world's down");
        }

        Assert.All(sheet, static q => Assert.Equal((255u, 255u, 255u), (q[0].Color & 0xFF, (q[0].Color >> 8) & 0xFF, (q[0].Color >> 16) & 0xFF))); // unshaded
    }

    [Fact]
    public void Given_ASheetOfStackedCells_When_ItIsMeshed_Then_ThePictureJoinsFromOneCellToTheNext()
    {
        var blocks = Floor();
        for (var y = 5; y <= 7; y++)
        {
            blocks.Water(5, y, 5, WaterLevel.Flowing(1, falling: true));
        }

        // The two lower cells have water above, so both are full height (the top one dips towards the air above it).
        var east = Quads(Mesh(blocks)).Where(static q => Math.Abs(NormalY(q)) < 0.001f && q.All(static v => v.X == 6f)).OrderBy(static q => q.Min(static v => v.Y)).Take(2).ToList();

        Assert.Equal(2, east.Count);
        // Each cell shows exactly one repeat of the picture from its top edge to its bottom edge, so the bottom of the upper one meets the top of the lower one.
        foreach (var cell in east)
        {
            Assert.Equal((float)WaterTileLayout.Cell / WaterTileLayout.Height, cell.Max(static v => v.V) - cell.Min(static v => v.V), 5);
        }

        Assert.Equal(east[0].Min(static v => v.V), east[1].Min(static v => v.V), 5);
    }

    [Fact]
    public void Given_AGreySet_When_TheSurfaceIsMeshed_Then_ItTakesTheGreyRowOfTheStrip()
    {
        var blocks = Floor().Water(5, 5, 5);
        var grey = ChunkMesher.Build(blocks, 0, 0, 0, ChunkMesher.KindBit(BlockId.Water));

        Assert.All(Up(grey, 5, 5), static v => Assert.True(WaterTileLayout.TileAt(v.U, v.V).Grey));
    }

    [Fact]
    public void Given_ASlopeInARealGrid_When_ItsSurfaceIsMeshed_Then_TheTextureRunsDownhill()
    {
        var grid = new VoxelGrid(32, 32, 32);
        grid.SetBlock(5, 5, 5, BlockId.Water);
        grid.SetBlock(6, 5, 5, BlockId.Water);
        grid.SetBlock(7, 5, 5, BlockId.Water);
        grid.SetWater(5, 5, 5, WaterLevel.Flowing(2));
        grid.SetWater(6, 5, 5, WaterLevel.Flowing(3));
        grid.SetWater(7, 5, 5, WaterLevel.Flowing(4));

        var top = Up(ChunkMesher.Build(new GridBlockSource(grid), 0, 0, 0), 6, 5);

        Assert.True(top.Single(static v => v.X == 7f && v.Z == 5f).U > top.Single(static v => v.X == 6f && v.Z == 5f).U);
        Assert.All(top, static v => Assert.Equal(WaterTile.ShallowFlow, WaterTileLayout.TileAt(v.U, v.V).Tile));
    }

    [Fact]
    public void Given_ASourceInARiver_When_ItsSurfaceIsMeshedWithTheCurrents_Then_TheTextureFollowsTheRiver()
    {
        var grid = new VoxelGrid(64, 32, 64);
        for (var z = 8; z <= 52; z++)
        {
            for (var x = 30; x <= 34; x++)
            {
                grid.SetBlock(x, 5, z, BlockId.Water);
            }
        }

        var currents = RiverCurrents.Build(new Vocaterra.Game.Core.World.Layout.RiverSpec(2, 2, 2, [(32, 10), (32, 50)]), grid);
        var dry = Up(ChunkMesher.Build(new GridBlockSource(grid), 2, 0, 1), 32, 20);
        var wet = Up(ChunkMesher.Build(new GridBlockSource(grid, currents), 2, 0, 1), 32, 20);

        Assert.Equal(WaterTile.ShallowStill, WaterTileLayout.TileAt(dry[0].U, dry[0].V).Tile);
        Assert.Equal(WaterTile.ShallowFlow, WaterTileLayout.TileAt(wet[0].U, wet[0].V).Tile);
        Assert.True(wet.Single(static v => v.Z == 21f && v.X == 32f).U > wet.Single(static v => v.Z == 20f && v.X == 32f).U, "the river runs south, so does its texture");
    }
}
