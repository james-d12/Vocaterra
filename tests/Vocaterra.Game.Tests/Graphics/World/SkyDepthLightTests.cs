using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>B12: the ground darkens with depth under the open sky, down to a cave minimum within about six blocks; a face open to the sky is unchanged; lava and the lights are not dimmed by it.</summary>
public sealed class SkyDepthLightTests
{
    // A stone mass from y 0 to 9 with its surface at y 9 over a 16 x 16 chunk, and a flat chamber hollowed from y 1 to 3 (a cave under the surface).
    private static MapBlocks Mass() => new MapBlocks().Fill(0, 0, 0, 15, 9, 15, BlockId.Stone);

    private static SkySurface Surface(int y = 9) => new(0, 0, (_, _) => y);

    private static ChunkMeshes Build(MapBlocks blocks, SkySurface? sky) =>
        ChunkMesher.Build(blocks, 0, 0, 0, 0, new MeshOptions(true, 100, null, LightBand.Day, sky));

    private static double Brightness(uint colour) => (colour & 0xFF) + ((colour >> 8) & 0xFF) + ((colour >> 16) & 0xFF);

    [Fact]
    public void Given_ADepth_When_TheShareIsRead_Then_ItIsOneAtTheSurfaceFallsInALineAndStopsAtTheCaveMinimum()
    {
        Assert.Equal(1f, SkyDepthLight.Factor(-5));
        Assert.Equal(1f, SkyDepthLight.Factor(0));
        Assert.Equal(SkyDepthLight.CaveMinimum, SkyDepthLight.Factor(SkyDepthLight.FalloffBlocks));
        Assert.Equal(SkyDepthLight.CaveMinimum, SkyDepthLight.Factor(60));
        Assert.Equal(1f - ((1f - SkyDepthLight.CaveMinimum) / 2f), SkyDepthLight.Factor(SkyDepthLight.FalloffBlocks / 2), 5);
        for (var depth = 1; depth <= SkyDepthLight.FalloffBlocks; depth++)
        {
            Assert.True(SkyDepthLight.Factor(depth) < SkyDepthLight.Factor(depth - 1), $"depth {depth} is not darker than {depth - 1}");
        }

        Assert.InRange(SkyDepthLight.FalloffBlocks, 5, 8); // "within about 6 blocks of the open sky"
        Assert.InRange(SkyDepthLight.CaveMinimum, 0.05f, 0.25f);
    }

    [Fact]
    public void Given_ASurfaceTile_When_ACellIsRead_Then_ItsDepthUnderTheColumnsSurfaceSetsTheShare()
    {
        var sky = Surface(9);

        Assert.Equal(1f, sky.FactorAt(4, 10, 4)); // above the surface
        Assert.Equal(1f, sky.FactorAt(4, 9, 4)); // the surface block itself
        Assert.Equal(SkyDepthLight.Factor(3), sky.FactorAt(4, 6, 4));
        Assert.Equal(SkyDepthLight.CaveMinimum, sky.FactorAt(4, -20, 4));
        Assert.Equal(SkyDepthLight.Factor(2), sky.FactorAt(-1, 7, 16)); // the ring round the chunk is held too
    }

    [Fact]
    public void Given_FacesOpenToTheSky_When_TheyAreMeshedWithASurface_Then_TheyAreTheMeshWithNoSurface()
    {
        var blocks = Mass();
        var plain = Build(blocks, null);
        var withSky = Build(blocks, Surface(9));

        // The top of the mass is the open surface: exactly the plain mesh. (Its sides are the stone's own layers, under the surface by their columns' reckoning, and do darken.)
        var top = plain.Opaque.Vertices.ToArray().Zip(withSky.Opaque.Vertices.ToArray()).Where(static p => p.First.Y == 10f && p.First.X is > 0f and < 16f).ToArray();
        Assert.NotEmpty(top);
        Assert.All(top, static p => Assert.Equal(p.First.Color, p.Second.Color));

        var buried = Build(blocks, Surface(30)).Opaque.Vertices.ToArray().Where(static v => v.Y == 10f).ToArray(); // a surface far above: the same top is deep under it
        var topBrightness = Brightness(top[0].First.Color);
        Assert.All(buried, v => Assert.True(Brightness(v.Color) < topBrightness * 0.5));
    }

    [Fact]
    public void Given_AChamberUnderTheSurface_When_ItsFacesAreMeshed_Then_TheyDarkenWithDepthDownToTheCaveMinimum()
    {
        // A tunnel under the surface at y 9, air from y 1 to 7: its floor is the face at y 1 (the cell in front of it is 8 under the surface), its ceiling the face at y 8 (the cell in front is 2 under it).
        var blocks = Mass();
        for (var x = 2; x <= 13; x++)
        {
            for (var y = 1; y <= 7; y++)
            {
                blocks.Set(x, y, 8, BlockId.Air);
            }
        }

        var plain = Build(blocks, null).Opaque.Vertices.ToArray();
        var dark = Build(blocks, Surface(9)).Opaque.Vertices.ToArray();
        Assert.Equal(plain.Length, dark.Length); // the darkening changes colours, never the geometry

        double Mean(MeshVertex[] vertices, float y) => vertices.Where(v => v.Y == y && v.X is > 3f and < 12f && v.Z is >= 8f and <= 9f).Average(v => Brightness(v.Color));

        var floorRatio = Mean(dark, 1f) / Mean(plain, 1f);
        // the tunnel's floor, the deepest (8 blocks, past the falloff)
        Assert.InRange(floorRatio, SkyDepthLight.CaveMinimum - 0.03, SkyDepthLight.CaveMinimum + 0.03);

        var ceilingRatio = Mean(dark, 8f) / Mean(plain, 8f); // the ceiling, two blocks under the surface
        Assert.InRange(ceilingRatio, SkyDepthLight.Factor(2) - 0.1, SkyDepthLight.Factor(2) + 0.1);
        Assert.True(ceilingRatio > floorRatio, "the nearer the sky, the brighter");
    }

    [Fact]
    public void Given_ACaveBelowThePureSurface_When_ItsRoofIsOpen_Then_ItsFacesAreNotDarkened()
    {
        // The surface said 9 but the roof is open (a shaft): SkyExposure walks the sky down to the first opaque block, so the tile holds the real top. Here the tile itself is the claim.
        var grid = new VoxelGrid(32, 32, 32);
        for (var x = 0; x < 32; x++)
        {
            for (var y = 0; y <= 9; y++)
            {
                for (var z = 0; z < 32; z++)
                {
                    grid.SetBlock(x, y, z, BlockId.Stone);
                }
            }
        }

        for (var y = 4; y <= 9; y++)
        {
            foreach (var (x, z) in new[] { (8, 8), (9, 8), (8, 9), (9, 9) })
            {
                grid.SetBlock(x, y, z, BlockId.Air); // a shaft from the surface down to y 4
            }
        }

        var exposure = new SkyExposure(grid, static (_, _) => 9);

        Assert.Equal(9, exposure.SurfaceOf(3, 3)); // solid to the natural surface: it stands
        Assert.Equal(3, exposure.SurfaceOf(8, 8)); // the shaft: the sky reaches the stone at its floor, y 3
        Assert.Equal(1f, exposure.FactorAt(8.5f, 4.5f, 8.5f));
        Assert.Equal(SkyDepthLight.Factor(9 - 4), exposure.FactorAt(3.5f, 4.5f, 3.5f));
        Assert.Same(exposure.Tile(0, 0), exposure.Tile(0, 0));
    }

    [Fact]
    public void Given_LavaUnderTheGround_When_ItIsMeshed_Then_ItsOwnFacesAreNotDarkened()
    {
        var blocks = Mass();
        blocks.Set(8, 2, 8, BlockId.Air).Set(8, 1, 8, BlockId.Lava);
        blocks.Set(9, 1, 8, BlockId.Air).Set(7, 1, 8, BlockId.Air).Set(8, 1, 7, BlockId.Air).Set(8, 1, 9, BlockId.Air);
        var plain = Build(blocks, null).Opaque.Vertices.ToArray();
        var dark = Build(blocks, Surface(9)).Opaque.Vertices.ToArray();

        var lava = new List<(MeshVertex Plain, MeshVertex Dark)>();
        for (var q = 0; q < plain.Length; q += 4)
        {
            var top = Enumerable.Range(q, 4).All(i => plain[i].Y == 2f && plain[i].X is >= 8f and <= 9f && plain[i].Z is >= 8f and <= 9f); // the lava's own top face, at y 2
            if (top)
            {
                lava.AddRange(Enumerable.Range(q, 4).Select(i => (plain[i], dark[i])));
            }
        }

        Assert.Equal(4, lava.Count);
        Assert.All(lava, static p => Assert.Equal(p.Plain.Color, p.Dark.Color));
    }

    [Fact]
    public void Given_AChunkWhollyAboveTheSurface_When_ItIsMeshedWithASurface_Then_NothingIsDarkened()
    {
        var blocks = new MapBlocks().Fill(0, 40, 0, 15, 41, 15, BlockId.Stone).Set(5, 42, 5, BlockId.Planks);
        var sky = Surface(10);
        var plain = ChunkMesher.Build(blocks, 0, 2, 0, 0, new MeshOptions(true, 100, null, LightBand.Day));
        var withSky = ChunkMesher.Build(blocks, 0, 2, 0, 0, new MeshOptions(true, 100, null, LightBand.Day, sky));

        Assert.Equal(plain.Opaque.Vertices.ToArray(), withSky.Opaque.Vertices.ToArray());
    }
}
