using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.Sky;
using Vocaterra.Game.Graphics.Water;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Startup;
using Vocaterra.Game.Tests.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics;

/// <summary>
/// The look (S14) without a GPU: the code-drawn atlas and its mips, the sun and the ambient occlusion in the vertex colours, the crossed-quad plants, the
/// water's shore and the waterfall, the sky and the clouds, the quality presets, and the greyscale switch with the new trees and plants.
/// </summary>
public sealed class LookTests
{
    private static ChunkMeshes Mesh(MapBlocks blocks, MeshOptions? options = null, ulong grey = 0) => ChunkMesher.Build(blocks, 0, 0, 0, grey, options);

    private static (int R, int G, int B, int A) Channels(uint colour) => ((int)(colour & 0xFF), (int)((colour >> 8) & 0xFF), (int)((colour >> 16) & 0xFF), (int)(colour >> 24));

    private static BlockCell CellOf(MeshVertex vertex)
    {
        var column = (int)(vertex.U * BlockAtlasLayout.Width / BlockAtlasLayout.CellSize);
        var row = (int)(vertex.V * BlockAtlasLayout.Height / BlockAtlasLayout.CellSize);
        return (BlockCell)((row * BlockAtlasLayout.Columns) + column);
    }

    // The water has its own strip of pictures, not the atlas (H09): which tile a water vertex's texture coordinate falls in.
    private static WaterTile WaterTileOf(MeshVertex vertex) => WaterTileLayout.TileAt(vertex.U, vertex.V).Tile;

    private static (double X, double Y, double Z) Normal(ReadOnlySpan<MeshVertex> v, int quad)
    {
        var a = v[quad * 4];
        var b = v[(quad * 4) + 1];
        var c = v[(quad * 4) + 2];
        double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z;
        double vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
        var n = ((uy * vz) - (uz * vy), (uz * vx) - (ux * vz), (ux * vy) - (uy * vx));
        var length = Math.Sqrt((n.Item1 * n.Item1) + (n.Item2 * n.Item2) + (n.Item3 * n.Item3));
        return (n.Item1 / length, n.Item2 / length, n.Item3 / length);
    }

    // ------------------------------------------------------------------ the atlas

    [Fact]
    public void TheAtlasIsDeterministicAndEveryNewCellIsDrawn()
    {
        Assert.Equal(BlockArt.AtlasPixels(), BlockArt.AtlasPixels());
        Assert.True(BlockAtlasLayout.CellCount <= BlockAtlasLayout.Columns * BlockAtlasLayout.ColourRows, "the colour rows hold every cell");
        foreach (var cell in new[]
        {
            BlockCell.GrassTop, BlockCell.GrassSide, BlockCell.Dirt, BlockCell.Stone, BlockCell.MossyStone, BlockCell.Snow, BlockCell.SnowSide, BlockCell.Sand,
            BlockCell.PathTop, BlockCell.Cobble, BlockCell.TrunkSide, BlockCell.TrunkEnd, BlockCell.SpruceBark, BlockCell.SpruceEnd, BlockCell.BirchBark, BlockCell.BirchEnd, BlockCell.Planks,
            BlockCell.Leaves, BlockCell.SpruceLeaves, BlockCell.BirchLeaves, BlockCell.TallGrass, BlockCell.Fern, BlockCell.Reeds, BlockCell.YellowFlower, BlockCell.WhiteFlower,
            BlockCell.Water, BlockCell.WaterShallow, BlockCell.Waterfall,
        })
        {
            Assert.True(BlockArtNature.Draws(cell), $"{cell} is drawn by the nature art");
            var pixels = BlockArt.Pixels(cell);
            Assert.Equal(16 * 16 * 4, pixels.Length);
            Assert.Equal(pixels, BlockArt.Pixels(cell)); // the same picture every time
            Assert.True(Enumerable.Range(0, 256).Count(i => pixels[(i * 4) + 3] > 0) > 20, $"{cell} draws almost nothing");
            var colours = new HashSet<(byte, byte, byte)>();
            for (var i = 0; i < 256; i++)
            {
                if (pixels[(i * 4) + 3] > 0)
                {
                    colours.Add((pixels[i * 4], pixels[(i * 4) + 1], pixels[(i * 4) + 2]));
                }
            }

            Assert.True(colours.Count >= 3, $"{cell} has {colours.Count} colours: a picture needs texture");
        }

        Assert.False(BlockArtNature.Draws(BlockCell.White));
        Assert.False(BlockArtNature.Draws(BlockCell.TorchFlame)); // the village's cells stay for S15
    }

    [Fact]
    public void TheCellsAreWrittenByTheirOwnBytesNotShared()
    {
        var one = BlockArt.Pixels(BlockCell.Stone);
        one[0] = 7;
        Assert.NotEqual((byte)7, BlockArt.Pixels(BlockCell.Stone)[0]);
    }

    [Fact]
    public void TheLeavesHaveCutOutHolesAndTheUndergrowthIsMostlyTransparent()
    {
        static double Transparent(BlockCell cell)
        {
            var p = BlockArt.Pixels(cell);
            return Enumerable.Range(0, 256).Count(i => p[(i * 4) + 3] == 0) / 256.0;
        }

        foreach (var leaves in new[] { BlockCell.Leaves, BlockCell.SpruceLeaves, BlockCell.BirchLeaves, BlockCell.Bush, BlockCell.BerryBush })
        {
            var holes = Transparent(leaves);
            Assert.InRange(holes, 0.04, 0.35); // holes you can see through, but a canopy with a body
        }

        foreach (var plant in new[] { BlockCell.TallGrass, BlockCell.Fern, BlockCell.Reeds, BlockCell.YellowFlower, BlockCell.WhiteFlower })
        {
            Assert.InRange(Transparent(plant), 0.4, 0.95);
        }

        // Everything else that makes a cube is solid.
        foreach (var solid in new[] { BlockCell.GrassTop, BlockCell.GrassSide, BlockCell.Dirt, BlockCell.Stone, BlockCell.MossyStone, BlockCell.Cobble, BlockCell.Snow, BlockCell.SnowSide, BlockCell.Sand, BlockCell.PathTop, BlockCell.SpruceBark, BlockCell.BirchEnd })
        {
            Assert.Equal(0.0, Transparent(solid));
        }
    }

    [Fact]
    public void TheColoursFollowTheBriefsPalette()
    {
        static (double R, double G, double B) Mean(BlockCell cell)
        {
            var p = BlockArt.Pixels(cell);
            double r = 0, g = 0, b = 0;
            var n = 0;
            for (var i = 0; i < p.Length; i += 4)
            {
                if (p[i + 3] == 0)
                {
                    continue;
                }

                r += p[i];
                g += p[i + 1];
                b += p[i + 2];
                n++;
            }

            return (r / n, g / n, b / n);
        }

        var grass = Mean(BlockCell.GrassTop);
        Assert.True(grass.G > grass.R * 1.5 && grass.G > grass.B * 2.5 && grass.G > 150, "bright saturated grass");
        foreach (var leaves in new[] { BlockCell.Leaves, BlockCell.SpruceLeaves, BlockCell.BirchLeaves })
        {
            var c = Mean(leaves);
            Assert.True(c.G > c.R && c.G > c.B * 1.3, $"{leaves} is green");
        }

        var spruce = Mean(BlockCell.SpruceLeaves);
        var oak = Mean(BlockCell.Leaves);
        var birch = Mean(BlockCell.BirchLeaves);
        Assert.True(spruce.B > oak.B, "spruce needles are the bluest green");
        Assert.True(birch.R > oak.R, "birch leaves are the yellowest green");
        var snow = Mean(BlockCell.Snow);
        Assert.True(snow.R > 220 && snow.G > 230 && snow.B > 240, "snow is white");
        var bark = Mean(BlockCell.BirchBark);
        Assert.True(bark.R > 165 && bark.G > 160, "birch bark is pale");
        var sand = Mean(BlockCell.Sand);
        Assert.True(sand.R > 200 && sand.B < sand.R * 0.7, "sand is warm");
        foreach (var water in new[] { BlockCell.Water, BlockCell.WaterShallow, BlockCell.Waterfall })
        {
            var c = Mean(water);
            Assert.True(c.B > c.R * 1.3 && c.B > 200, $"{water} is a clear blue");
        }

        var shallow = Mean(BlockCell.WaterShallow);
        var deep = Mean(BlockCell.Water);
        Assert.True(shallow.R + shallow.G > deep.R + deep.G, "shallow water is lighter");
        var yellow = Mean(BlockCell.YellowFlower);
        Assert.True(yellow.G > 120);
    }

    [Fact]
    public void TheMipChainHasOneLevelPerHalvingAndEveryLevelIsItsOwnSize()
    {
        var levels = BlockArt.MipLevelCount;
        Assert.Equal(9, levels); // 128 x 384 down to 1 x 1
        var level0 = BlockArt.AtlasPixels();
        Assert.Same(level0, BlockArt.MipLevel(level0, 0));
        for (var level = 1; level < levels; level++)
        {
            var (w, h) = BlockArt.MipSize(level);
            Assert.Equal(w * h * 4, BlockArt.MipLevel(level0, level).Length);
        }

        Assert.Equal((1, 1), BlockArt.MipSize(levels - 1));
    }

    [Fact]
    public void MipsNeverMixOneCellWithTheNext()
    {
        // At level 4 a texel is a whole 16 x 16 cell: it is the alpha-weighted mean of that cell and of nothing else, so a far leaf is green and a far snow is white.
        var level0 = BlockArt.AtlasPixels();
        var level4 = BlockArt.MipLevel(level0, 4);
        var (width, _) = BlockArt.MipSize(4);
        foreach (var cell in new[] { BlockCell.GrassTop, BlockCell.Snow, BlockCell.Leaves, BlockCell.Water, BlockCell.Stone, BlockCell.TallGrass })
        {
            var index = (int)cell;
            var tx = index % BlockAtlasLayout.Columns;
            var ty = index / BlockAtlasLayout.Columns;
            var pixels = BlockArt.Pixels(cell);
            long r = 0, g = 0, b = 0, a = 0;
            for (var i = 0; i < 256; i++)
            {
                var alpha = pixels[(i * 4) + 3];
                r += pixels[i * 4] * alpha;
                g += pixels[(i * 4) + 1] * alpha;
                b += pixels[(i * 4) + 2] * alpha;
                a += alpha;
            }

            var o = ((ty * width) + tx) * 4;
            Assert.InRange(level4[o], (byte)((r / a) - 1), (byte)((r / a) + 1));
            Assert.InRange(level4[o + 1], (byte)((g / a) - 1), (byte)((g / a) + 1));
            Assert.InRange(level4[o + 2], (byte)((b / a) - 1), (byte)((b / a) + 1));
        }

        // A far cut-out leaf keeps its green (no dark fringe from the holes).
        var leaf = ((int)BlockCell.Leaves / BlockAtlasLayout.Columns * width) + ((int)BlockCell.Leaves % BlockAtlasLayout.Columns);
        Assert.True(level4[leaf * 4 + 1] > level4[leaf * 4] * 1.5);
        Assert.True(level4[(leaf * 4) + 3] > 180);
    }

    [Fact]
    public void EveryNewBlockKindHasAPictureOnEveryFace()
    {
        foreach (var kind in new[]
        {
            BlockId.SpruceTrunk, BlockId.BirchTrunk, BlockId.SpruceLeaves, BlockId.BirchLeaves, BlockId.TallGrass, BlockId.Fern, BlockId.Reeds, BlockId.YellowFlower,
            BlockId.WhiteFlower, BlockId.MossyStone, BlockId.Cobble, BlockId.Snow,
        })
        {
            foreach (var face in Enum.GetValues<BlockFace>())
            {
                var cell = BlockAtlasLayout.CellFor(kind, face);
                Assert.NotEqual(BlockCell.White, cell);
                Assert.True(BlockArtNature.Draws(cell), $"{kind} {face} -> {cell}");
            }
        }

        Assert.Equal(BlockCell.SpruceEnd, BlockAtlasLayout.CellFor(BlockId.SpruceTrunk, BlockFace.Up));
        Assert.Equal(BlockCell.BirchBark, BlockAtlasLayout.CellFor(BlockId.BirchTrunk, BlockFace.East));
        Assert.Equal(BlockCell.Snow, BlockAtlasLayout.CellFor(BlockId.Snow, BlockFace.Up));
        Assert.Equal(BlockCell.SnowSide, BlockAtlasLayout.CellFor(BlockId.Snow, BlockFace.North));
        Assert.Equal(BlockCell.Stone, BlockAtlasLayout.CellFor(BlockId.Snow, BlockFace.Down));
    }

    // ------------------------------------------------------------------ light

    [Fact]
    public void SunlitFacesAreWarmAndBrightAndFacesAwayFromTheSunAreDarkerAndBluer()
    {
        var vertices = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Dirt)).Opaque.Vertices;
        var byDirection = new Dictionary<string, (int R, int G, int B, int A)>();
        for (var quad = 0; quad < 6; quad++)
        {
            var n = Normal(vertices, quad);
            var key = Math.Abs(n.Y) > 0.5 ? (n.Y > 0 ? "up" : "down") : Math.Abs(n.X) > 0.5 ? (n.X > 0 ? "east" : "west") : (n.Z > 0 ? "south" : "north");
            byDirection[key] = Channels(vertices[quad * 4].Color);
            for (var k = 1; k < 4; k++)
            {
                Assert.Equal(byDirection[key], Channels(vertices[(quad * 4) + k].Color)); // an open block: no occlusion, one colour on the face
            }
        }

        Assert.Equal((255, 249, 234, 255), byDirection["up"]); // full sun, warm
        Assert.True(byDirection["west"].R > byDirection["east"].R && byDirection["north"].R > byDirection["south"].R);
        foreach (var shaded in new[] { "east", "south", "down" })
        {
            var c = byDirection[shaded];
            Assert.True(c.B > c.R, $"{shaded} is bluer than it is red");
            Assert.True(c.R < byDirection["up"].R * 0.7, $"{shaded} is dark");
        }

        Assert.True(byDirection["down"].R < byDirection["east"].R);
        Assert.True(ChunkMesher.SunShare(BlockFace.Up) == 1f && ChunkMesher.SunShare(BlockFace.East) == 0f && ChunkMesher.SunShare(BlockFace.Down) == 0f);
        Assert.InRange(ChunkMesher.SunShare(BlockFace.West), 0.5f, 0.9f);
        var sun = ChunkMesher.SunDirection;
        Assert.InRange((sun.X * sun.X) + (sun.Y * sun.Y) + (sun.Z * sun.Z), 0.98f, 1.02f);
        Assert.True(sun.X < 0 && sun.Z < 0 && sun.Y > 0.5f, "the sun is in the north-west and high");
    }

    [Fact]
    public void RockIsTintedByHeightWarmAtTheFootCoolOnThePeaksAndNothingElseIs()
    {
        var foot = ChunkMesher.RockTint(BlockId.Stone, 40);
        var middle = ChunkMesher.RockTint(BlockId.Stone, 84);
        var peak = ChunkMesher.RockTint(BlockId.Stone, 120);
        Assert.True(foot.R > foot.B + 0.12f, "the low crags are warm");
        Assert.True(Math.Abs(middle.R - middle.B) < 0.1f, "the middle ones are grey");
        Assert.True(peak.B >= peak.R, "the high ones are cool");
        Assert.All(new[] { foot, middle, peak }, t => Assert.InRange(Math.Min(t.R, Math.Min(t.G, t.B)), 0.7f, 1.0f));
        foreach (var other in new[] { BlockId.Grass, BlockId.MossyStone, BlockId.Snow, BlockId.Dirt, BlockId.Leaves })
        {
            Assert.Equal((1f, 1f, 1f), ChunkMesher.RockTint(other, 40));
        }

        // In the mesh: a stone at the foot of the mountains has a redder top than the same stone on a peak.
        var low = Channels(Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Stone)).Opaque.Vertices[0].Color);
        var high = Channels(ChunkMesher.Build(new MapBlocks().Set(5, 120, 5, BlockId.Stone), 0, 7, 0).Opaque.Vertices[0].Color);
        Assert.True(low.R - low.B > high.R - high.B);
    }

    [Fact]
    public void AWallBesideAFloorDarkensTheFloorsCornersThatTouchIt()
    {
        // A floor of stone at y = 4 and a pillar standing on it at (6, 5, 6): the floor's top faces beside the pillar are shaded at the corners that touch it.
        var blocks = new MapBlocks().Fill(2, 4, 2, 10, 4, 10, BlockId.Stone).Set(6, 5, 6, BlockId.Stone);
        var vertices = Mesh(blocks).Opaque.Vertices;
        var darkest = 255;
        var brightest = 0;
        for (var quad = 0; quad < vertices.Length / 4; quad++)
        {
            var n = Normal(vertices, quad);
            if (n.Y < 0.5 || Math.Abs(vertices[quad * 4].Y - 5.0) > 0.01)
            {
                continue; // the floor's top faces only
            }

            for (var k = 0; k < 4; k++)
            {
                var v = vertices[(quad * 4) + k];
                var red = Channels(v.Color).R;
                darkest = Math.Min(darkest, red);
                brightest = Math.Max(brightest, red);
                var touchesPillar = v.X is >= 5.99f and <= 7.01f && v.Z is >= 5.99f and <= 7.01f;
                if (!touchesPillar && v.X is > 3.1f and < 8.9f && v.Z is > 3.1f and < 8.9f && (v.X < 5.9f || v.X > 7.1f || v.Z < 5.9f || v.Z > 7.1f))
                {
                    Assert.Equal(255, red); // an open corner in the middle of the floor is fully lit
                }
            }
        }

        Assert.Equal(255, brightest);
        Assert.True(darkest < 255 * 0.9, $"the corners at the pillar are darkened (darkest red {darkest})");
        Assert.True(darkest > 255 * 0.4, "but not black");
    }

    [Fact]
    public void WithOcclusionOffEveryFaceIsFlatLit()
    {
        var blocks = new MapBlocks().Fill(2, 4, 2, 10, 4, 10, BlockId.Stone).Set(6, 5, 6, BlockId.Stone);
        var off = Mesh(blocks, new MeshOptions(AmbientOcclusion: false)).Opaque.Vertices;
        for (var quad = 0; quad < off.Length / 4; quad++)
        {
            var first = off[quad * 4].Color;
            for (var k = 1; k < 4; k++)
            {
                Assert.Equal(first, off[(quad * 4) + k].Color);
            }
        }

        // The same faces as with occlusion: only the colours and the diagonals differ.
        Assert.Equal(Mesh(blocks).Opaque.FaceCount, Mesh(blocks, new MeshOptions(AmbientOcclusion: false)).Opaque.FaceCount);
    }

    [Fact]
    public void TheDiagonalOfAShadedQuadFollowsTheDarkerCorners()
    {
        // A pillar on a floor makes the floor's corner next to it darker: that quad then starts from another corner (the diagonal turned), and stays counter-clockwise.
        var floor = Mesh(new MapBlocks().Fill(2, 4, 2, 10, 4, 10, BlockId.Stone).Set(6, 5, 6, BlockId.Stone)).Opaque.Vertices;
        var turned = 0;
        var tops = 0;
        for (var quad = 0; quad < floor.Length / 4; quad++)
        {
            var n = Normal(floor, quad);
            if (n.Y < 0.5)
            {
                continue;
            }

            tops++;
            Assert.True(n.Y > 0.99);
            if (floor[quad * 4].U >= floor[(quad * 4) + 1].U)
            {
                turned++; // the first corner is not the bottom left
            }
        }

        Assert.True(tops > 50);
        Assert.True(turned > 0, "some quad runs from another corner");
        Assert.True(turned < tops, "and the open ones keep the plain order");
    }

    // ------------------------------------------------------------------ plants

    [Theory]
    [InlineData(BlockId.TallGrass, BlockCell.TallGrass)]
    [InlineData(BlockId.Fern, BlockCell.Fern)]
    [InlineData(BlockId.Reeds, BlockCell.Reeds)]
    [InlineData(BlockId.YellowFlower, BlockCell.YellowFlower)]
    [InlineData(BlockId.WhiteFlower, BlockCell.WhiteFlower)]
    public void APlantIsTwoCrossedQuadsInThePlantMeshAndNeverTheOpaqueOne(BlockId plant, BlockCell cell)
    {
        var meshes = Mesh(new MapBlocks().Set(5, 5, 5, plant));
        Assert.Equal(0, meshes.Opaque.FaceCount);
        Assert.Equal(0, meshes.Alpha.FaceCount);
        Assert.Equal(2, meshes.PlantMesh.FaceCount);
        Assert.Equal(BlockShape.Cross, ChunkMesher.ShapeOf(plant));
        Assert.Equal(ChunkMesher.KindBit(plant), meshes.Kinds);

        var v = meshes.PlantMesh.Vertices;
        for (var i = 0; i < 8; i++)
        {
            Assert.Equal(cell, CellOf(v[i]));
            Assert.InRange(v[i].X, 4.7f, 6.3f); // inside the cell, give or take the slight shift (a fern's fronds spread a little wider)
            Assert.InRange(v[i].Z, 4.7f, 6.3f);
            Assert.InRange(v[i].Y, 5f, 6.01f);
        }

        // The two quads are in different vertical planes (they cross): their normals are at right angles.
        var n1 = Normal(v, 0);
        var n2 = Normal(v, 1);
        Assert.Equal(0.0, (n1.X * n2.X) + (n1.Y * n2.Y) + (n1.Z * n2.Z), 3);
        // They share a centre, so they do cross.
        var c1 = (X: (v[0].X + v[2].X) / 2, Z: (v[0].Z + v[2].Z) / 2);
        var c2 = (X: (v[4].X + v[6].X) / 2, Z: (v[4].Z + v[6].Z) / 2);
        Assert.Equal(c1.X, c2.X, 3);
        Assert.Equal(c1.Z, c2.Z, 3);
        // The tip is lighter than the root (a shade of occlusion at the foot).
        Assert.True(Channels(v[2].Color).R > Channels(v[0].Color).R);
    }

    [Fact]
    public void FlowersStayLowSoAPlayerOnALedgeLooksOverThem()
    {
        var flower = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.YellowFlower)).PlantMesh.Vertices.ToArray().Max(static v => v.Y) - 5f;
        var grass = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.TallGrass)).PlantMesh.Vertices.ToArray().Max(static v => v.Y) - 5f;
        Assert.InRange(flower, 0.3f, 0.6f);
        Assert.InRange(grass, 0.69f, 1.01f);
        Assert.True(Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Reeds)).PlantMesh.Vertices.ToArray().Max(static v => v.Y) - 5f >= 0.99f);
    }

    [Fact]
    public void ThePlantKeepPercentThinsThePlantsByPositionAndKeepsTheSameOnesEveryTime()
    {
        var field = new MapBlocks();
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                field.Set(x, 3, z, BlockId.TallGrass);
            }
        }

        Assert.Equal(256 * 2, Mesh(field).PlantMesh.FaceCount);
        Assert.Equal(256 * 2, Mesh(field, new MeshOptions(PlantKeepPercent: 100)).PlantMesh.FaceCount);
        var half = Mesh(field, new MeshOptions(PlantKeepPercent: 50)).PlantMesh.FaceCount / 2;
        Assert.InRange(half, 90, 166);
        Assert.Equal(half, Mesh(field, new MeshOptions(PlantKeepPercent: 50)).PlantMesh.FaceCount / 2);
        Assert.Equal(0, Mesh(field, new MeshOptions(PlantKeepPercent: 0)).PlantMesh.FaceCount);
        var low = Mesh(field, new MeshOptions(PlantKeepPercent: 25)).PlantMesh.FaceCount / 2;
        Assert.True(low < half && low > 0);
    }

    [Fact]
    public void PlantsDoNotHideTheFacesBehindThemAndDoNotCountAsOpaque()
    {
        var meshes = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Stone).Set(6, 5, 5, BlockId.TallGrass));
        Assert.Equal(6, meshes.Opaque.FaceCount); // the stone shows its east face to the grass too
        Assert.False(ChunkMesher.IsOpaqueCube(BlockId.TallGrass));
    }

    [Fact]
    public void LeavesOfDifferentTreesAreCulledAgainstEachOtherLikeLeavesOfOneTree()
    {
        var mixed = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Leaves).Set(6, 5, 5, BlockId.SpruceLeaves));
        Assert.Equal(10, mixed.Opaque.FaceCount);
        Assert.False(ChunkMesher.FaceVisible(BlockId.BirchLeaves, BlockId.SpruceLeaves));
        Assert.True(ChunkMesher.FaceVisible(BlockId.Leaves, BlockId.Air));
        Assert.True(ChunkMesher.FaceVisible(BlockId.Leaves, BlockId.Water));
        Assert.False(ChunkMesher.FaceVisible(BlockId.Leaves, BlockId.Trunk)); // a trunk is an opaque cube
        Assert.True(ChunkMesher.FaceVisible(BlockId.Stone, BlockId.BirchLeaves));
    }

    // ------------------------------------------------------------------ water

    [Fact]
    public void TheWaterIsClearerAtTheShoreAndLighterWhereItIsShallow()
    {
        // A pool 5 x 5 and two deep, in a bank of sand: the corners at the sand are more transparent than the corners in the middle.
        var blocks = new MapBlocks().Fill(0, 1, 0, 8, 1, 8, BlockId.Sand).Fill(0, 2, 0, 8, 3, 8, BlockId.Sand);
        for (var x = 2; x <= 6; x++)
        {
            for (var z = 2; z <= 6; z++)
            {
                blocks.Set(x, 3, z, BlockId.Water);
                blocks.Set(x, 2, z, BlockId.Water);
            }
        }

        var alpha = Mesh(blocks).Alpha.Vertices;
        var edge = 255;
        var middle = 0;
        for (var i = 0; i < alpha.Length; i++)
        {
            var v = alpha[i];
            if (Math.Abs(v.Y - (3 + (14f / 16f))) > 0.01f)
            {
                continue; // the surface only
            }

            var a = Channels(v.Color).A;
            if (v.X is 2f or 7f && v.Z is 2f or 7f)
            {
                edge = Math.Min(edge, a);
            }

            if (v.X is > 3.5f and < 5.5f && v.Z is > 3.5f and < 5.5f)
            {
                middle = Math.Max(middle, a);
            }
        }

        Assert.True(edge < middle, $"the shore corner ({edge}) is clearer than the middle ({middle})");
        Assert.InRange(middle, 200, 255);
    }

    [Fact]
    public void ShallowWaterUsesTheLighterPictureAndDeepWaterTheDarkerOne()
    {
        var shallow = new MapBlocks().Fill(0, 1, 0, 4, 1, 4, BlockId.Sand).Set(2, 2, 2, BlockId.Water);
        var deep = new MapBlocks().Fill(0, 0, 0, 4, 0, 4, BlockId.Sand).Fill(2, 1, 2, 2, 3, 2, BlockId.Water);
        Assert.Equal(WaterTile.ShallowStill, WaterTileOf(Mesh(shallow).Alpha.Vertices.ToArray().OrderByDescending(static v => v.Y).First()));
        Assert.Equal(WaterTile.DeepStill, WaterTileOf(Mesh(deep).Alpha.Vertices.ToArray().OrderByDescending(static v => v.Y).First()));
    }

    [Fact]
    public void AColumnOfFallingWaterShowsTheWaterfallPictureOnItsOpenSides()
    {
        var blocks = new MapBlocks();
        for (var y = 4; y <= 12; y++)
        {
            blocks.Set(5, y, 5, BlockId.Water);
        }

        blocks.Fill(3, 3, 3, 7, 3, 7, BlockId.Stone);
        var alpha = Mesh(blocks).Alpha;
        var sides = 0;
        var waterfall = 0;
        for (var quad = 0; quad < alpha.FaceCount; quad++)
        {
            var n = Normal(alpha.Vertices, quad);
            if (Math.Abs(n.Y) > 0.5)
            {
                continue;
            }

            sides++;
            if (WaterTileOf(alpha.Vertices[quad * 4]) == WaterTile.Falling)
            {
                waterfall++;
            }
        }

        Assert.True(sides >= 4 * 8);
        Assert.True(waterfall >= 4 * 8, $"{waterfall} of {sides} side faces show the falling water");

        // The falling water takes no shading: its foam stays white on every side, in the shade of the cliff too.
        for (var quad = 0; quad < alpha.FaceCount; quad++)
        {
            if (WaterTileOf(alpha.Vertices[quad * 4]) == WaterTile.Falling)
            {
                Assert.Equal((255, 255, 255, 255), Channels(alpha.Vertices[quad * 4].Color));
            }
        }
    }

    [Fact]
    public void TheRealWorldsWaterfallHasWaterfallFaces()
    {
        var scene = RouteScene.Build(42);
        var source = new GridBlockSource(scene.Grid);
        var fall = scene.Point("waterfall");
        var found = 0;
        for (var y = fall.Y - 30; y <= fall.Y; y += 16)
        {
            var coord = VoxelChunkCoord.FromBlock(fall.X, y, fall.Z);
            var meshes = ChunkMesher.Build(source, coord.X, coord.Y, coord.Z);
            for (var i = 0; i < meshes.Alpha.VertexCount; i++)
            {
                if (WaterTileOf(meshes.Alpha.Vertices[i]) == WaterTile.Falling)
                {
                    found++;
                }
            }
        }

        Assert.True(found > 40, $"{found} vertices of the waterfall picture along the falls");
    }

    // ------------------------------------------------------------------ the sky

    [Fact]
    public void TheSkyIsDeepBlueOverheadPaleAtTheHorizonAndTheFogIsTheHorizonsColour()
    {
        var horizon = SkyPalette.At(0);
        var low = SkyPalette.At(15);
        var high = SkyPalette.At(50);
        var zenith = SkyPalette.At(90);
        Assert.Equal(SkyPalette.Horizon, horizon);
        Assert.Equal(SkyPalette.Horizon, SkyPalette.At(-30));
        Assert.Equal(SkyPalette.Zenith, zenith);
        Assert.True(horizon.R > low.R && low.R > high.R && high.R > zenith.R, "the red drains out going up: blue deepens");
        Assert.True(zenith.B > zenith.R * 3 && zenith.B > 180, "a deep saturated blue overhead");
        Assert.True(horizon.R > 150 && horizon.G > 190 && horizon.B > 230, "pale at the horizon");
        // The fog the renderer uses (and the colour it clears to) is the horizon's, so far land fades into the sky.
        Assert.Equal((SkyPalette.Horizon.R, SkyPalette.Horizon.G, SkyPalette.Horizon.B), (VoxelRenderer.Sky.R, VoxelRenderer.Sky.G, VoxelRenderer.Sky.B));
        Assert.InRange(VoxelRenderer.FogStartFraction, 0.3f, 0.95f);
        Assert.Equal((100, 100, 100), SkyPalette.Blend((0, 0, 0), (200, 200, 200), 0.5f));
    }

    [Fact]
    public void TheCloudsAreFlatSlabsHighOverThePeaksPickedByAHashAndDriftingEast()
    {
        var a = new List<CloudBox>();
        var b = new List<CloudBox>();
        CloudField.Generate(200f, 200f, 0, a);
        CloudField.Generate(200f, 200f, 0, b);
        Assert.Equal(a, b); // no random generator, no clock
        Assert.InRange(a.Count, 50, 400);
        Assert.All(a, box =>
        {
            Assert.True(box.Y >= CloudField.Altitude - 1f && box.Y <= CloudField.Altitude + 16f, "above the peaks (124)");
            Assert.True(box.Width > box.Thickness * 2 && box.Depth > box.Thickness * 1.5f, "flat slabs");
        });
        Assert.All(a, box => Assert.InRange(box.X, 200f - (CloudField.CellSize * (CloudField.Reach + 2)), 200f + (CloudField.CellSize * (CloudField.Reach + 2))));

        // Later, every cloud has moved east by the same amount and not along the other axes.
        var later = new List<CloudBox>();
        CloudField.Generate(200f, 200f, 5000, later);
        var drift = 5000 * CloudField.DriftPerTick;
        foreach (var box in a)
        {
            var moved = later.FirstOrDefault(c => Math.Abs(c.Z - box.Z) < 0.01f && Math.Abs(c.Y - box.Y) < 0.01f && Math.Abs(c.X - (box.X + drift)) < 0.05f && Math.Abs(c.Width - box.Width) < 0.01f);
            if (moved == default && box.X + drift < 200f + (CloudField.CellSize * CloudField.Reach) && box.X > 200f - (CloudField.CellSize * (CloudField.Reach - 1)))
            {
                Assert.Fail($"the cloud at {box} did not drift by {drift}");
            }
        }

        // Another place has another sky, and standing a whole cell further on shifts the field by the cell.
        var elsewhere = new List<CloudBox>();
        CloudField.Generate(200f + CloudField.CellSize, 200f, 0, elsewhere);
        Assert.Contains(a.First(box => box.X > 200f - CloudField.CellSize && box.X < 200f + CloudField.CellSize), elsewhere);
    }

    // ------------------------------------------------------------------ quality

    [Fact]
    public void TheQualityPresetsChooseTheDrawnPlantsTheOcclusionAndTheClouds()
    {
        var low = QualityPreset.For(QualityLevel.Low);
        var medium = QualityPreset.For(QualityLevel.Medium);
        var high = QualityPreset.For(QualityLevel.High);
        Assert.True(low.PlantDistance < medium.PlantDistance && medium.PlantDistance < high.PlantDistance);
        Assert.True(low.PlantKeepPercent <= medium.PlantKeepPercent && medium.PlantKeepPercent <= high.PlantKeepPercent);
        Assert.Equal(100, high.PlantKeepPercent);
        Assert.True(high.AmbientOcclusion && high.Clouds, "high is the default for a real graphics card and draws everything");
        Assert.Null(new LaunchOptions().Quality); // not given: the saved setting, else high
        foreach (var preset in new[] { low, medium, high })
        {
            Assert.InRange(preset.PlantKeepPercent, 1, 100);
            Assert.True(preset.PlantDistance >= VoxelChunk.Size * 2);
            Assert.True(preset.PlantDistance < preset.ViewDistance);
            Assert.Equal(preset.WithViewDistance(500).PlantDistance, preset.PlantDistance); // --view-distance changes only how far the world is drawn
        }
    }

    // ------------------------------------------------------------------ greyscale (D-game-6)

    [Fact]
    public void WithGreyscaleEveryTreeKindIsGreyUntilArbolIsKnownAndPlantsAndSnowStayInColour()
    {
        var words = new WordProgress(TestGame.Nouns);
        var grey = WordColours.GreyKinds(TestGame.Nouns, words);
        foreach (var tree in new[] { BlockId.Trunk, BlockId.Leaves, BlockId.SpruceTrunk, BlockId.SpruceLeaves, BlockId.BirchTrunk, BlockId.BirchLeaves })
        {
            Assert.True(ChunkMesher.IsGrey(tree, grey), $"{tree} is grey while árbol is not known");
        }

        foreach (var rock in new[] { BlockId.Stone, BlockId.MossyStone, BlockId.Cobble })
        {
            Assert.True(ChunkMesher.IsGrey(rock, grey), $"{rock} is piedra");
        }

        Assert.True(ChunkMesher.IsGrey(BlockId.Grass, grey)); // hierba is not known
        Assert.True(ChunkMesher.IsGrey(BlockId.Water, grey));

        // Blocks no taught noun names stay in colour, as the bush does: the undergrowth, the snow and the bushes.
        foreach (var kept in new[] { BlockId.TallGrass, BlockId.Fern, BlockId.Reeds, BlockId.YellowFlower, BlockId.WhiteFlower, BlockId.Snow, BlockId.Bush, BlockId.BerryBush, BlockId.Air })
        {
            if (kept == BlockId.BerryBush)
            {
                Assert.True(ChunkMesher.IsGrey(kept, grey)); // fruta names the berry bush: grey until it is known
                continue;
            }

            Assert.False(ChunkMesher.IsGrey(kept, grey), $"{kept} stays in colour");
        }

        // Once árbol is known all the trees regain colour together.
        words.Restore([new WordRecord("árbol", CreditUnits: WordProgress.UnitsToKnow, CorrectTurns: WordProgress.UsesToKnow)]);
        var after = WordColours.GreyKinds(TestGame.Nouns, words);
        foreach (var tree in new[] { BlockId.Trunk, BlockId.Leaves, BlockId.SpruceTrunk, BlockId.SpruceLeaves, BlockId.BirchTrunk, BlockId.BirchLeaves })
        {
            Assert.False(ChunkMesher.IsGrey(tree, after));
        }

        Assert.True(ChunkMesher.IsGrey(BlockId.Stone, after)); // the rest are still grey
    }

    [Fact]
    public void TheMesherDrawsPlantsAndLeavesFromTheGreyCopyOnlyWhenTheirKindIsGrey()
    {
        var greyFrom = BlockAtlasLayout.ColourRows / (float)BlockAtlasLayout.Rows;
        var blocks = new MapBlocks().Set(3, 3, 3, BlockId.SpruceLeaves).Set(9, 3, 9, BlockId.TallGrass);
        var greyLeaves = Mesh(blocks, null, ChunkMesher.KindBit(BlockId.SpruceLeaves));
        Assert.All(greyLeaves.Opaque.Vertices.ToArray(), v => Assert.True(v.V >= greyFrom));
        Assert.All(greyLeaves.PlantMesh.Vertices.ToArray(), v => Assert.True(v.V < greyFrom)); // the grass is in colour
        var greyGrass = Mesh(blocks, null, ChunkMesher.KindBit(BlockId.TallGrass));
        Assert.All(greyGrass.PlantMesh.Vertices.ToArray(), v => Assert.True(v.V >= greyFrom)); // were it grey, it would use the grey copy
        Assert.All(greyGrass.Opaque.Vertices.ToArray(), v => Assert.True(v.V < greyFrom));
    }

    [Fact]
    public void TheGreyCopyKeepsTheCutOutHolesOfTheLeaves()
    {
        var colour = BlockArt.Pixels(BlockCell.SpruceLeaves);
        var grey = BlockArt.GreyPixels(BlockCell.SpruceLeaves);
        var holes = Enumerable.Range(0, 256).Count(i => colour[(i * 4) + 3] == 0);
        Assert.True(holes > 8);
        Assert.Equal(holes, Enumerable.Range(0, 256).Count(i => grey[(i * 4) + 3] == 0));
    }
}
