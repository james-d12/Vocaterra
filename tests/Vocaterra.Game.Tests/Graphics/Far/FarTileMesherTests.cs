using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Tests.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>The far view's tile meshes (B07c): the faces of three-dimensional sections, axis-aligned on whole voxels, closed and crack-free, overhangs kept, greedy-merged when flat and textured when near, with water and light kinds in lists of their own.</summary>
public sealed class FarTileMesherTests(ITestOutputHelper output)
{
    private static readonly FarViewSettings Settings = new(4096);

    // A world of stacks made by a function: one section of voxels at the level, the stone as high as the function says (in voxels), air over it.
    private sealed class Land(Func<int, int, int, LodSection> make) : ILodSectionSource
    {
        public LodStack Stack(int level, int sx, int sz) => new(0, [make(level, sx, sz)]);
    }

    private static LodSection Heights(int sx, int sz, Func<int, int, int> height)
    {
        var section = LodSection.Air();
        for (var z = 0; z < 32; z++)
        {
            for (var x = 0; x < 32; x++)
            {
                for (var y = 0; y < height((sx * 32) + x, (sz * 32) + z); y++)
                {
                    section.SetBlock(LodSection.Index(x, y, z), BlockId.Stone);
                }
            }
        }

        return section;
    }

    // The quads of a mesh: four vertices each, with the normal the winding gives and the area.
    private sealed record Quad(Vector3[] Corners, Vector3 Normal, float Area, int Bucket);

    private static List<Quad> Quads(FarTileMesh mesh)
    {
        var quads = new List<Quad>();
        var opaqueQuads = mesh.OpaqueIndices / 6;
        var litQuads = mesh.LitIndices / 6;
        for (var q = 0; q < mesh.Vertices.Count / 4; q++)
        {
            var v = mesh.Vertices.Skip(q * 4).Take(4).Select(static x => x.Position).ToArray();
            var cross = Vector3.Cross(v[1] - v[0], v[2] - v[0]);
            quads.Add(new Quad(v, Vector3.Normalize(cross), Vector3.Cross(v[1] - v[0], v[3] - v[0]).Length(), q < opaqueQuads ? 0 : q < opaqueQuads + litQuads ? 1 : 2));
        }

        return quads;
    }

    private static FarTileMesh Mesh(ILodSectionSource source, int level = 2, int tx = 3, int tz = -2, FarViewSettings? settings = null, UInt128 grey = default) =>
        FarTileMesher.Build(source, settings ?? Settings, new FarTileKey(level, tx, tz), grey);

    [Fact]
    public void Given_FlatLand_When_ATileIsBuilt_Then_TheTopIsOneQuadAndTheOnlySidesAreTheSkirtsOfTheEdge()
    {
        const int Level = 2;
        var mesh = Mesh(new Land((_, sx, sz) => Heights(sx, sz, (_, _) => 10)), Level);
        var quads = Quads(mesh);
        var cell = Settings.CellOf(Level);
        var skirt = ((Settings.SkirtOf(Level) + cell - 1) / cell) + 1;

        var tops = quads.Where(static q => q.Normal.Y > 0.99f).ToList();
        Assert.Single(tops);
        Assert.Equal((float)Settings.TileCells * cell * Settings.TileCells * cell, tops[0].Area, 1f);
        var sides = quads.Where(static q => Math.Abs(q.Normal.Y) < 0.01f).ToList();
        Assert.Equal(4, sides.Count);
        Assert.All(sides, q => Assert.Equal(skirt * cell, q.Corners.Max(static c => c.Y) - q.Corners.Min(static c => c.Y)));
        Assert.Equal(mesh.Triangles, quads.Count * 2);
        Assert.False(mesh.Textured);
        Assert.Equal(0, mesh.WaterIndices);
        Assert.Equal(0, mesh.LitIndices);
    }

    [Fact]
    public void Given_RuggedHeights_When_ATileIsBuilt_Then_TheFacesAreExactlyTheSurfaceOfTheVoxelsWithNoCrackAndNoOverlap()
    {
        const int Level = 2;
        static int Height(int cx, int cz) => 3 + (int)(((uint)((cx * 73856093) ^ (cz * 19349663)) >> 7) % 9);
        var key = new FarTileKey(Level, 3, -2);
        var n = Settings.TileCells;
        var cell = Settings.CellOf(Level);
        var skirt = ((Settings.SkirtOf(Level) + cell - 1) / cell) + 1;
        var mesh = Mesh(new Land((_, sx, sz) => Heights(sx, sz, Height)), Level, key.Tx, key.Tz);
        var quads = Quads(mesh);

        // The expected area: every top, every wall down to the lower neighbour, and along the tile's edge the voxels within the skirt of the surface.
        double expectedSides = 0;
        for (var j = 0; j < n; j++)
        {
            for (var i = 0; i < n; i++)
            {
                var (cx, cz) = ((key.Tx * n) + i, (key.Tz * n) + j);
                var h = Height(cx, cz);
                foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var edge = i + di < 0 || i + di >= n || j + dj < 0 || j + dj >= n;
                    // Over a tile's edge a face is culled by the neighbour's voxels like one inside, and drawn down to a skirt under the neighbour's ground.
                    expectedSides += (edge ? Math.Max(0, h - Math.Max(0, Height(cx + di, cz + dj) - skirt)) : Math.Max(0, h - Height(cx + di, cz + dj))) * (double)cell * cell;
                }
            }
        }

        var tops = quads.Where(static q => q.Normal.Y > 0.99f).Sum(static q => (double)q.Area);
        var sides = quads.Where(static q => Math.Abs(q.Normal.Y) < 0.01f).Sum(static q => (double)q.Area);
        Assert.Equal((double)n * n * cell * cell, tops, 1);
        Assert.Equal(expectedSides, sides, 1);
        Assert.DoesNotContain(quads, static q => q.Normal.Y < -0.99f);
        Assert.True(quads.Count(static q => q.Normal.Y > 0.99f) < n * n, "flat runs are merged");
    }

    [Fact]
    public void Given_ABridgeOverAGapAndACaveMouth_When_ATileIsBuilt_Then_TheUndersideAndTheTunnelWallsAreDrawn()
    {
        // The three-dimensional part: stone up to 8, a bridge of stone at 14 and 15 over a trench cut 4 deep, none of which a height field has.
        var land = new Land((_, sx, sz) =>
        {
            var section = Heights(sx, sz, (_, _) => 8);
            for (var z = 8; z < 12; z++)
            {
                for (var x = 4; x < 28; x++)
                {
                    for (var y = 4; y < 8; y++)
                    {
                        section.SetBlock(LodSection.Index(x, y, z), BlockId.Air);
                    }

                    section.SetBlock(LodSection.Index(x, 14, z), BlockId.Planks);
                    section.SetBlock(LodSection.Index(x, 15, z), BlockId.Planks);
                }
            }

            return section;
        });

        var quads = Quads(Mesh(land, 1, 5, 5));

        var under = quads.Where(static q => q.Normal.Y < -0.99f).ToList();
        Assert.NotEmpty(under);
        Assert.All(under, q => Assert.Equal(14f * 2, q.Corners[0].Y));
        Assert.Contains(quads, static q => q.Normal.Y > 0.99f && q.Corners[0].Y == 16f * 2);
        Assert.Contains(quads, static q => q.Normal.Y > 0.99f && q.Corners[0].Y == 4f * 2); // the trench's floor
    }

    [Fact]
    public void Given_TilesOfEveryLevelOverRealLand_When_TheyAreBuilt_Then_EveryFaceIsAxisAlignedAndEveryVertexIsOnWholeCells()
    {
        var world = StreamedWorlds.Composition.World.Far.FarWorld(new LodStore(), 4);
        var total = 0;
        foreach (var level in new[] { 0, 1, 2, 3, 4, 5, 6, 7 })
        {
            var settings = new FarViewSettings(16384);
            var key = new FarTileKey(level, 3000 >> (5 + level), 1200 >> (5 + level));
            var mesh = FarTileMesher.Build(world, settings, key);
            var cell = settings.CellOf(level);
            output.WriteLine($"level {level}: {mesh.Vertices.Count / 4} quads, {mesh.Bytes / 1024} KB, {mesh.BuildMilliseconds:0} ms (Debug), textured {mesh.Textured}");
            foreach (var quad in Quads(mesh))
            {
                var n = quad.Normal;
                Assert.Equal(1, new[] { n.X, n.Y, n.Z }.Count(static c => Math.Abs(c) > 0.999f));
                foreach (var corner in quad.Corners)
                {
                    Assert.Equal(0, (((int)corner.X % cell) + cell) % cell);
                    Assert.Equal(0, (((int)corner.Y % cell) + cell) % cell);
                    Assert.Equal(0, (((int)corner.Z % cell) + cell) % cell);
                    Assert.Equal(corner, new Vector3(MathF.Round(corner.X), MathF.Round(corner.Y), MathF.Round(corner.Z)));
                }

                total++;
            }
        }

        Assert.True(total > 500);
    }

    [Fact]
    public void Given_TheNearLevelsAndTheFarOnes_When_TilesAreBuilt_Then_TheNearOnesCarryAtlasTexturesOneQuadAFaceAndTheFarOnesMergeFlatColours()
    {
        var land = new Land((_, sx, sz) => Heights(sx, sz, (_, _) => 10));
        var near = Mesh(land, 0, 1, 1);
        var far = Mesh(land, 2, 1, 1);

        Assert.True(near.Textured);
        Assert.False(far.Textured);
        var nearTops = Quads(near).Where(static q => q.Normal.Y > 0.99f).ToList();
        Assert.Equal(32 * 32, nearTops.Count);
        var (u0, v0, u1, v1) = BlockAtlasLayout.Uv(BlockAtlasLayout.CellFor(BlockId.Stone, BlockFace.Up));
        var topVertices = near.Vertices.Take(4 * 32 * 32).Where(static v => true).ToList();
        Assert.All(near.Vertices.Take(4), v =>
        {
            Assert.InRange(v.TextureCoordinate.X, u0 - 0.0001f, u1 + 0.0001f);
            Assert.InRange(v.TextureCoordinate.Y, v0 - 0.0001f, v1 + 0.0001f);
        });
        Assert.NotEmpty(topVertices);
        Assert.Single(Quads(far), static q => q.Normal.Y > 0.99f);
    }

    [Fact]
    public void Given_LandUnderACrownAndInTheOpen_When_ATileIsBuilt_Then_TheSkyLightOfTheDataShadesTheLandUnderTheCrown()
    {
        var land = new Land((_, sx, sz) =>
        {
            var section = Heights(sx, sz, (_, _) => 10);
            for (var z = 0; z < 32; z++)
            {
                for (var x = 16; x < 32; x++)
                {
                    section.SetLight(LodSection.Index(x, 10, z), (byte)(5 << 4));
                }
            }

            return section;
        });

        var tops = Quads(Mesh(land, 2, 1, 1)).Where(static q => q.Normal.Y > 0.99f).ToList();

        Assert.Equal(2, tops.Count);
        var mesh = Mesh(land, 2, 1, 1);
        var all = Quads(mesh);
        var colours = Enumerable.Range(0, all.Count).Where(q => all[q].Normal.Y > 0.99f).Select(q => mesh.Vertices[q * 4].Color).Distinct().ToList();
        Assert.Equal(2, colours.Count);
        Assert.True(colours.Min(static c => c.G) < colours.Max(static c => c.G));
    }

    [Fact]
    public void Given_WaterAndLava_When_ATileIsBuilt_Then_TheyAreInTheirOwnListsAndTheWaterDeepensWithItsDepth()
    {
        var land = new Land((_, sx, sz) =>
        {
            var section = Heights(sx, sz, (_, _) => 5);
            for (var z = 0; z < 32; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    for (var y = 5; y < 10; y++)
                    {
                        section.SetBlock(LodSection.Index(x, y, z), BlockId.Water);
                    }
                }

                section.SetBlock(LodSection.Index(24, 5, z), BlockId.Lava);
                section.SetLight(LodSection.Index(24, 5, z), 0xFF);
            }

            return section;
        });

        var mesh = Mesh(land, 0, 1, 1);

        var quads = Quads(mesh);
        Assert.Contains(quads, static q => q.Bucket == 2);
        Assert.Contains(quads, static q => q.Bucket == 1);
        Assert.True(mesh.WaterIndices > 0 && mesh.LitIndices > 0 && mesh.OpaqueIndices > 0);
        // The tile (1, 1) of level 0 is the blocks from 32 to 64: no wall of water on its outer planes.
        Assert.All(quads.Where(static q => q.Bucket == 2), static q => Assert.False((Math.Abs(q.Normal.X) > 0.99f && q.Corners[0].X is 32f or 64f) || (Math.Abs(q.Normal.Z) > 0.99f && q.Corners[0].Z is 32f or 64f)));

        var shallow = Mesh(new Land((_, sx, sz) => WaterOver(sx, sz, 1)), 2, 1, 1);
        var deep = Mesh(new Land((_, sx, sz) => WaterOver(sx, sz, 8)), 2, 1, 1);
        var shallowColour = shallow.Vertices.Last().Color;
        var deepColour = deep.Vertices.Last().Color;
        Assert.True(deepColour.A > shallowColour.A);
        Assert.True(deepColour.B <= shallowColour.B);
        // What lies under deep water is not drawn.
        Assert.True(Quads(deep).Count(static q => q.Bucket == 0 && q.Normal.Y > 0.99f) == 0);
        Assert.True(Quads(shallow).Count(static q => q.Bucket == 0 && q.Normal.Y > 0.99f) > 0);
    }

    private static LodSection WaterOver(int sx, int sz, int depth)
    {
        var section = Heights(sx, sz, (_, _) => 5);
        for (var z = 0; z < 32; z++)
        {
            for (var x = 0; x < 32; x++)
            {
                for (var y = 5; y < 5 + depth; y++)
                {
                    section.SetBlock(LodSection.Index(x, y, z), BlockId.Water);
                }
            }
        }

        return section;
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
        var land = new Land((_, sx, sz) => Heights(sx, sz, (_, _) => 10));
        var mesh = Mesh(land);
        var greyMesh = Mesh(land, grey: ~UInt128.Zero);
        Assert.Equal(mesh.Vertices.Select(static v => v.Position), greyMesh.Vertices.Select(static v => v.Position));
        Assert.NotEqual(mesh.Vertices.Select(static v => v.Color), greyMesh.Vertices.Select(static v => v.Color));
    }

    [Fact]
    public void Given_ATileOverRealLand_When_ItIsBuiltTwiceAndFromManyThreads_Then_TheMeshIsIdentical()
    {
        var lod = StreamedWorlds.Composition.World.Far.FarWorld(new LodStore(), 4);
        var key = new FarTileKey(1, 14, 9);
        var first = FarTileMesher.Build(lod, Settings, key);
        var results = new FarTileMesh[4];

        Parallel.For(0, 4, new ParallelOptions { MaxDegreeOfParallelism = 3 }, t => results[t] = FarTileMesher.Build(lod, Settings, key));

        Assert.All(results, m =>
        {
            Assert.True(first.Vertices.SequenceEqual(m.Vertices));
            Assert.Equal(first.Indices16, m.Indices16);
            Assert.Equal(first.Textured, m.Textured);
            Assert.Equal(first.Box, m.Box);
        });
        output.WriteLine($"one level-1 tile over land outside the valley: {first.Vertices.Count} vertices, {first.Triangles} triangles, {first.Bytes / 1024.0:0} KB, {first.BuildMilliseconds:0} ms (Debug)");
    }

    [Fact]
    public void Given_ATileOfTheNearRing_When_ItIsBuilt_Then_ItsBoxHoldsEveryVertexAndTheTreesStandAboveTheLand()
    {
        var sampler = StreamedWorlds.Composition.World.Far;
        var lod = sampler.FarWorld(new LodStore(), 4);
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

    [Fact]
    public void Given_ASnowCappedCliff_When_AFlatTileIsMeshed_Then_ItsSidesAreWhiteNotGrey()
    {
        // Columns of stone 20 voxels high with one voxel of snow on top, beside columns half as high: every side face of the high ones is a cliff.
        var land = new Land((_, sx, sz) =>
        {
            var section = LodSection.Air();
            for (var z = 0; z < 32; z++)
            {
                for (var x = 0; x < 32; x++)
                {
                    var high = (x / 4) % 2 == 0;
                    var top = high ? 20 : 10;
                    for (var y = 0; y < top; y++)
                    {
                        section.SetBlock(LodSection.Index(x, y, z), y == top - 1 && high ? BlockId.Snow : BlockId.Stone);
                    }
                }
            }

            return new LodSection[] { section }[0];
        });

        var mesh = Mesh(land, level: 4);

        var bright = mesh.Vertices.Count(static v => v.Color.R > 150 && v.Color.G > 150 && v.Color.B > 150);
        var grey = mesh.Vertices.Count(static v => v.Color.R is > 60 and < 125 && Math.Abs(v.Color.R - v.Color.B) < 20);
        output.WriteLine($"{mesh.Vertices.Count} vertices, {bright} bright, {grey} stone grey");
        Assert.True(bright > 0 && bright > grey, $"{bright} bright against {grey} grey");
    }

    [Fact]
    public void Given_ACliffOnATileBorderTallerThanTheSkirt_When_ATileIsBuilt_Then_ItsEdgeWallReachesDownToTheNeighboursGroundWithNoSlot()
    {
        const int Level = 2;
        // The tile is 30 voxels high; the tile on its +x side is 6: a wall of 24 voxels over the border, far more than the skirt's 4 (the review's H3).
        var land = new Land((_, sx, sz) => Heights(sx, sz, (cx, _) => cx < (Settings.TileCells * 3) + Settings.TileCells ? 30 : 6));
        var cell = Settings.CellOf(Level);
        var skirt = (Settings.SkirtOf(Level) + cell - 1) / cell;
        var mesh = Mesh(land, Level, 3, -2);

        var east = Quads(mesh).Where(q => q.Normal.X > 0.99f).Sum(static q => (double)q.Area);

        // From a skirt under the neighbour's ground (its top voxel is 5) to the top, the whole border row long.
        var expected = (30 - (6 - 1 - skirt)) * (double)cell * Settings.TileCells * cell;
        Assert.Equal(expected, east, 1);
    }

    [Fact]
    public void Given_ATileWithNoNeighbourStacks_When_ItIsBuilt_Then_ItsEdgesGetASkirtFromItsOwnGroundNotFromItsCrowns()
    {
        const int Level = 2;
        var section = LodSection.Air();
        for (var z = 0; z < 32; z++)
        {
            for (var x = 0; x < 32; x++)
            {
                for (var y = 0; y < 10; y++)
                {
                    section.SetBlock(LodSection.Index(x, y, z), BlockId.Stone);
                }

                if (x == 31)
                {
                    for (var y = 10; y < 20; y++)
                    {
                        section.SetBlock(LodSection.Index(x, y, z), BlockId.Leaves); // a hedge of crowns along the +x edge
                    }
                }
            }
        }

        var key = new FarTileKey(Level, 3, -2);
        var mesh = FarTileMesher.Mesh(new LodStack(0, [section]), Settings, key);

        var cell = Settings.CellOf(Level);
        var skirt = (Settings.SkirtOf(Level) + cell - 1) / cell;
        var east = Quads(mesh).Where(q => q.Normal.X > 0.99f).Sum(static q => (double)q.Area);
        // The stone's wall from a skirt under its ground (10) to 10, and the crowns' 10 voxels: measured from the crown tops (20) the skirt would have cut the stone wall off.
        Assert.Equal(((10 - (10 - 1 - skirt)) + 10) * (double)cell * 32 * cell, east, 1);
    }
}
