using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Tests.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>The far view's tile meshes (B07c): boxes of whole blocks on the level's cells, closed and crack-free, greedy-merged, coloured from the atlas, with water and light kinds in lists of their own.</summary>
public sealed class FarTileMesherTests(ITestOutputHelper output)
{
    private static readonly FarViewSettings Settings = new(4096);

    private sealed class Source(Func<int, int, int, LodSegment[]> make) : ILodColumnSource
    {
        public void Fill(LodGrid grid)
        {
            for (var j = 0; j < grid.Depth; j++)
            {
                for (var i = 0; i < grid.Width; i++)
                {
                    grid.Set(i, j, make(grid.Level, grid.X0 + i, grid.Z0 + j));
                }
            }
        }
    }

    private static LodSegment Run(int top, int bottom, BlockId block, byte sky = 15, byte light = 0) => new(top, bottom, block, sky, light);

    private static LodSegment[] Ground(int height, BlockId top = BlockId.Grass) => [Run(height, height, top), Run(height - 1, height - 3, BlockId.Dirt), Run(height - 4, -64, BlockId.Stone)];

    // The quads of a mesh: four vertices each, with the normal the winding gives and the area.
    private sealed record Quad(Vector3[] Corners, Vector3 Normal, float Area, int Bucket);

    private static List<Quad> Quads(FarTileMesh mesh)
    {
        var quads = new List<Quad>();
        var opaqueQuads = mesh.OpaqueIndices / 6;
        var litQuads = mesh.LitIndices / 6;
        for (var q = 0; q < mesh.Vertices.Length / 4; q++)
        {
            var v = mesh.Vertices.Skip(q * 4).Take(4).Select(static x => x.Position).ToArray();
            var cross = Vector3.Cross(v[1] - v[0], v[2] - v[0]);
            var normal = Vector3.Normalize(cross);
            quads.Add(new Quad(v, normal, Vector3.Cross(v[1] - v[0], v[3] - v[0]).Length(), q < opaqueQuads ? 0 : q < opaqueQuads + litQuads ? 1 : 2));
        }

        return quads;
    }

    private static FarTileMesh Build(Func<int, int, int, LodSegment[]> make, int level = 2, FarTileKey? key = null) =>
        FarTileMesher.Build(new Source(make), Settings, key ?? new FarTileKey(level, 3, -2));

    [Fact]
    public void Given_FlatMeadow_When_ATileIsBuilt_Then_TheTopIsOneQuadAndTheOnlySidesAreTheSkirts()
    {
        var mesh = Build((_, _, _) => Ground(40));
        var quads = Quads(mesh);
        var cell = Settings.CellOf(2);
        var side = Settings.TileCells * cell;

        var tops = quads.Where(static q => q.Normal.Y > 0.99f).ToList();
        Assert.Single(tops);
        Assert.Equal((float)side * side, tops[0].Area, 1f);
        Assert.All(quads.Where(static q => Math.Abs(q.Normal.Y) < 0.01f), q => Assert.True(q.Corners.Min(static c => c.Y) >= 40 - Settings.SkirtOf(2) - 1));
        Assert.Equal(mesh.Triangles, quads.Count * 2);
        Assert.Equal(0, mesh.WaterIndices);
        Assert.Equal(0, mesh.LitIndices);
    }

    [Fact]
    public void Given_RuggedHeights_When_ATileIsBuilt_Then_TheFacesAreExactlyTheSurfaceOfTheBoxesWithNoCrackAndNoOverlap()
    {
        const int Level = 2;
        static int Height(int cx, int cz) => 30 + (int)(((uint)((cx * 73856093) ^ (cz * 19349663)) >> 7) % 9);
        var key = new FarTileKey(Level, 3, -2);
        var mesh = Build((l, cx, cz) => [Run(Height(cx, cz), -64, BlockId.Stone)], Level, key);
        var quads = Quads(mesh);
        var cell = Settings.CellOf(Level);
        var n = Settings.TileCells;
        var skirt = Settings.SkirtOf(Level);

        // The expected area: every top, and every wall down to the lower neighbour; on the tile's edge the wall is at least the skirt deep.
        double expectedSides = 0;
        for (var j = 0; j < n; j++)
        {
            for (var i = 0; i < n; i++)
            {
                var (cx, cz) = ((key.Tx * n) + i, (key.Tz * n) + j);
                var h = Height(cx, cz) + 1;
                foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var edge = i + di < 0 || i + di >= n || j + dj < 0 || j + dj >= n;
                    var drop = h - (Height(cx + di, cz + dj) + 1);
                    expectedSides += Math.Max(edge ? Math.Max(drop, skirt) : drop, 0) * (double)cell;
                }
            }
        }

        var tops = quads.Where(static q => q.Normal.Y > 0.99f).Sum(static q => (double)q.Area);
        var sides = quads.Where(static q => Math.Abs(q.Normal.Y) < 0.01f).Sum(static q => (double)q.Area);
        Assert.Equal((double)n * n * cell * cell, tops, 1);
        Assert.Equal(expectedSides, sides, 1);
        Assert.DoesNotContain(quads, static q => q.Normal.Y < -0.99f);
        // Greedy: fewer quads than cells.
        Assert.True(quads.Count(static q => q.Normal.Y > 0.99f) < n * n);
    }

    [Fact]
    public void Given_TilesOfEveryLevelOverRealLand_When_TheyAreBuilt_Then_EveryFaceIsAxisAlignedAndEveryVertexIsOnAWholeBlockAndTheLevelsCells()
    {
        var lod = StreamedWorlds.Composition.World.Far.Lod(4);
        var total = 0;
        foreach (var level in new[] { 0, 1, 2, 3, 4, 5, 6, 7 })
        {
            var settings = new FarViewSettings(16384);
            var key = new FarTileKey(level, level == 0 ? 15 : 7 >> level, -9 >> Math.Min(level, 3));
            var mesh = FarTileMesher.Build(lod, settings, key);
            var cell = settings.CellOf(level);
            output.WriteLine($"level {level}: {mesh.Vertices.Length / 4} quads, {mesh.Bytes / 1024} KB, {mesh.BuildMilliseconds:0} ms (Debug)");
            foreach (var quad in Quads(mesh))
            {
                var n = quad.Normal;
                Assert.Equal(1, new[] { n.X, n.Y, n.Z }.Count(static c => Math.Abs(c) > 0.999f));
                foreach (var corner in quad.Corners)
                {
                    Assert.Equal(0f, corner.Y - MathF.Round(corner.Y));
                    Assert.Equal(0, ((int)corner.X % cell + cell) % cell);
                    Assert.Equal(0, ((int)corner.Z % cell + cell) % cell);
                    Assert.Equal(corner.X, MathF.Round(corner.X));
                    Assert.Equal(corner.Z, MathF.Round(corner.Z));
                }

                total++;
            }
        }

        Assert.True(total > 500);
    }

    [Fact]
    public void Given_ATileWhoseEdgeStepsDownToItsNeighbour_When_ItIsBuilt_Then_TheWallReachesDownToTheNeighbourOrTheSkirtWhicheverIsDeeper()
    {
        const int Level = 3;
        var key = new FarTileKey(Level, 0, 0);
        var n = Settings.TileCells;
        // The land is 40 inside the tile and 5 in the border: a 35-block cliff round it.
        var mesh = Build((_, cx, cz) => cx < 0 || cz < 0 || cx >= n || cz >= n ? Ground(5) : Ground(40), Level, key);

        var lowest = Quads(mesh).Where(static q => Math.Abs(q.Normal.Y) < 0.01f).Min(static q => q.Corners.Min(static c => c.Y));
        Assert.Equal(6f, lowest); // down to the neighbour's top at 6
        Assert.True(Settings.SkirtOf(Level) < 35);
    }

    [Fact]
    public void Given_AColumnWithACrownOverAGap_When_ATileIsBuilt_Then_TheCrownHasAnUndersideAndTheGroundUnderItIsDarker()
    {
        var open = Build((_, _, _) => Ground(40));
        LodSegment[] Tree() => [Run(52, 48, BlockId.Leaves, 15), .. Ground(40).Select(static s => s with { Sky = 9 })];
        var covered = Build((_, _, _) => Tree());

        Assert.DoesNotContain(Quads(open), static q => q.Normal.Y < -0.99f);
        Assert.Contains(Quads(covered), static q => q.Normal.Y < -0.99f);
        var openGround = open.Vertices.First(static v => v.Position.Y == 41).Color;
        var shaded = covered.Vertices.First(static v => v.Position.Y == 41).Color;
        Assert.True(shaded.G < openGround.G);
    }

    [Fact]
    public void Given_WaterAndLava_When_ATileIsBuilt_Then_TheyAreInTheirOwnListsAndTheWaterDeepensWithItsDepth()
    {
        var mesh = Build((_, cx, cz) => cx < 48 ? [Run(32, 28, BlockId.Water), Run(27, 27, BlockId.Sand, 12), Run(26, -64, BlockId.Stone, 8)] : [Run(33, 20, BlockId.Lava, 15, 15), Run(19, -64, BlockId.Stone, 0)], 0, new FarTileKey(0, 1, 1));

        var quads = Quads(mesh);
        Assert.Contains(quads, static q => q.Bucket == 2);
        Assert.Contains(quads, static q => q.Bucket == 1);
        Assert.True(mesh.WaterIndices > 0 && mesh.LitIndices > 0 && mesh.OpaqueIndices > 0);
        Assert.All(quads.Where(static q => q.Bucket == 2), q => Assert.True(q.Normal.Y > 0.99f || Math.Abs(q.Normal.Y) < 0.01f));

        // Deeper water is more opaque and darker.
        var shallow = Build((_, _, _) => [Run(32, 31, BlockId.Water), Run(30, 30, BlockId.Sand), Run(29, -64, BlockId.Stone)]);
        var deep = Build((_, _, _) => [Run(32, 12, BlockId.Water), Run(11, 11, BlockId.Sand), Run(10, -64, BlockId.Stone)]);
        var shallowColour = shallow.Vertices.Last().Color;
        var deepColour = deep.Vertices.Last().Color;
        Assert.True(deepColour.A > shallowColour.A);
        Assert.True(deepColour.B <= shallowColour.B);
        // What lies under deep water is not drawn.
        Assert.True(deep.Triangles < shallow.Triangles + 40);
    }

    [Fact]
    public void Given_BlocksAndALight_When_TheyAreColoured_Then_TheColourIsTheAverageOfTheAtlasTileLitAsAFace()
    {
        var pixels = BlockArt.Pixels(BlockAtlasLayout.CellFor(BlockId.Grass, BlockFace.Up));
        long r = 0, g = 0, b = 0, count = 0;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            if (pixels[i + 3] >= 128)
            {
                r += pixels[i];
                g += pixels[i + 1];
                b += pixels[i + 2];
                count++;
            }
        }

        var palette = FarPalette.Of(BlockId.Grass);
        Assert.Equal((byte)(r / count), palette.R);
        Assert.Equal((byte)(g / count), palette.G);
        Assert.Equal((byte)(b / count), palette.B);
        var sun = ChunkMesher.SunTintFor(0, 1, 0);
        var colour = FarTileMesher.TopColour(BlockId.Grass, sun);
        Assert.Equal((byte)Math.Clamp(palette.G * sun.G * FarTileMesher.NoiseGain, 0, 255), colour.G);
        Assert.True(colour.G > colour.R && colour.G > colour.B);
        Assert.NotEqual(FarPalette.Of(BlockId.Grass), FarPalette.SideOf(BlockId.Grass));
        foreach (var block in new[] { BlockId.Stone, BlockId.Sand, BlockId.Snow, BlockId.Water, BlockId.Leaves, BlockId.SpruceLeaves, BlockId.BirchLeaves, BlockId.Dirt, BlockId.Trunk })
        {
            var (pr, pg, pb) = FarPalette.Of(block);
            Assert.True(pr + pg + pb > 0, $"{block}");
        }
    }

    [Fact]
    public void Given_ABlockDrawnGreyInTheChunks_When_ItsLandIsColoured_Then_TheFarLandIsGreyToo()
    {
        var sun = (1f, 1f, 1f);

        var colour = FarTileMesher.TopColour(BlockId.Grass, sun);
        var grey = FarTileMesher.TopColour(BlockId.Grass, sun, 15, ChunkMesher.KindBit(BlockId.Grass));
        var other = FarTileMesher.TopColour(BlockId.Grass, sun, 15, ChunkMesher.KindBit(BlockId.Sand));

        Assert.True(grey.R == grey.G && grey.G == grey.B);
        Assert.Equal(colour, other);
        var mesh = Build((_, _, _) => Ground(40));
        var greyMesh = FarTileMesher.Build(new Source((_, _, _) => Ground(40)), Settings, new FarTileKey(2, 3, -2), ~UInt128.Zero);
        Assert.Equal(mesh.Vertices.Select(static v => v.Position), greyMesh.Vertices.Select(static v => v.Position));
        Assert.NotEqual(mesh.Vertices.Select(static v => v.Color), greyMesh.Vertices.Select(static v => v.Color));
    }

    [Fact]
    public void Given_ATileOverRealLand_When_ItIsBuiltTwiceAndFromManyThreads_Then_TheMeshIsIdentical()
    {
        var lod = StreamedWorlds.Composition.World.Far.Lod(4);
        var key = new FarTileKey(1, 14, -9);
        var first = FarTileMesher.Build(lod, Settings, key);
        var results = new FarTileMesh[4];

        Parallel.For(0, 4, new ParallelOptions { MaxDegreeOfParallelism = 3 }, t => results[t] = FarTileMesher.Build(lod, Settings, key));

        Assert.All(results, m =>
        {
            Assert.True(first.Vertices.SequenceEqual(m.Vertices));
            Assert.Equal(first.Indices16, m.Indices16);
            Assert.Equal(first.Box, m.Box);
        });
        output.WriteLine($"one level-1 tile over land outside the valley: {first.Vertices.Length} vertices, {first.Triangles} triangles, {first.Bytes / 1024.0:0} KB, {first.BuildMilliseconds:0} ms (Debug)");
    }

    [Fact]
    public void Given_ATileOfTheNearRing_When_ItIsBuilt_Then_ItsBoxHoldsEveryVertexAndTheTreesStandAboveTheLand()
    {
        var sampler = StreamedWorlds.Composition.World.Far;
        var lod = sampler.Lod(4);
        // Find a tile with trees: a forest column with a tree in it.
        FarTileMesh? found = null;
        for (var tz = -40; tz < 40 && found is null; tz++)
        {
            for (var tx = 20; tx < 120 && found is null; tx++)
            {
                if (sampler.TreesIn(tx * 32, tz * 32, 32, 32).Count > 25)
                {
                    found = FarTileMesher.Build(lod, Settings, new FarTileKey(0, tx, tz));
                }
            }
        }

        Assert.NotNull(found);
        Assert.All(found.Vertices, v => Assert.True(found.Box.Contains(v.Position) != ContainmentType.Disjoint));
        var ground = Quads(found).Where(static q => q.Normal.Y > 0.99f).Min(static q => q.Corners[0].Y);
        var highest = found.Vertices.Max(static v => v.Position.Y);
        Assert.True(highest >= ground + 6, $"the trees rise {highest - ground} blocks over the ground");
    }

    [Fact]
    public void Given_TheNoiseTexture_When_ItIsMade_Then_ItsMeanIsTheStatedMeanAndItsMipsAverageIt()
    {
        var pixels = FarNoise.Pixels(0);
        var mean = pixels.Where(static (_, i) => i % 4 == 0).Select(static b => b / 255.0).Average();
        var top = FarNoise.Pixels(FarNoise.Levels - 1);

        Assert.InRange(mean, FarNoise.Mean - 0.02, FarNoise.Mean + 0.02);
        Assert.Equal(4, top.Length);
        Assert.InRange(top[0] / 255.0, FarNoise.Mean - 0.02, FarNoise.Mean + 0.02);
        Assert.InRange(1f / FarNoise.Mean, FarTileMesher.NoiseGain - 0.02f, FarTileMesher.NoiseGain + 0.02f);
        Assert.True(pixels.Where(static (_, i) => i % 4 == 0).Distinct().Count() > 20);
    }
}
