using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics;

namespace Vocaterra.Game.Tests;

public sealed class BlockAtlasTests
{
    private static readonly BlockFace[] Faces = Enum.GetValues<BlockFace>();

    [Fact]
    public void EveryBlockKindHasAPictureForEveryFace()
    {
        foreach (var definition in BlockDefinition.All.Where(static d => d.Id != BlockId.Air))
        {
            foreach (var face in Faces)
            {
                var cell = BlockAtlasLayout.CellFor(definition.Id, face);
                Assert.NotEqual(BlockCell.White, cell); // the white cell is for figures and overlays, never for a block of the world
                Assert.InRange((int)cell, 0, BlockAtlasLayout.CellCount - 1);
            }
        }
    }

    [Fact]
    public void TheAtlasHoldsEveryCellAndTheUvRectanglesAreInsideTheirCellsAndDistinct()
    {
        Assert.True(BlockAtlasLayout.CellCount <= BlockAtlasLayout.Columns * BlockAtlasLayout.Rows);
        var seen = new HashSet<(float, float)>();
        foreach (var cell in Enum.GetValues<BlockCell>())
        {
            var (x, y, w, h) = BlockAtlasLayout.PixelRectangle(cell);
            var (u0, v0, u1, v1) = BlockAtlasLayout.Uv(cell);
            Assert.True(u0 > x / (float)BlockAtlasLayout.Width && u1 < (x + w) / (float)BlockAtlasLayout.Width, $"{cell}: u is not inset");
            Assert.True(v0 > y / (float)BlockAtlasLayout.Height && v1 < (y + h) / (float)BlockAtlasLayout.Height, $"{cell}: v is not inset");
            Assert.InRange(u0, 0f, 1f);
            Assert.InRange(v1, 0f, 1f);
            Assert.True(seen.Add((u0, v0)), $"{cell} shares its rectangle with another cell");
        }
    }

    [Fact]
    public void TheLookupFollowsTheFaceOfTheBlock()
    {
        Assert.Equal(BlockCell.GrassTop, BlockAtlasLayout.CellFor(BlockId.Grass, BlockFace.Up));
        Assert.Equal(BlockCell.GrassSide, BlockAtlasLayout.CellFor(BlockId.Grass, BlockFace.North));
        Assert.Equal(BlockCell.GrassSide, BlockAtlasLayout.CellFor(BlockId.Grass, BlockFace.East));
        Assert.Equal(BlockCell.Dirt, BlockAtlasLayout.CellFor(BlockId.Grass, BlockFace.Down));
        Assert.Equal(BlockCell.TrunkEnd, BlockAtlasLayout.CellFor(BlockId.Trunk, BlockFace.Up));
        Assert.Equal(BlockCell.TrunkSide, BlockAtlasLayout.CellFor(BlockId.Trunk, BlockFace.South));
        Assert.Equal(BlockCell.Water, BlockAtlasLayout.CellFor(BlockId.Water, BlockFace.West));
        Assert.Equal(BlockCell.TableTop, BlockAtlasLayout.CellFor(BlockId.Table, BlockFace.Up));
        Assert.Equal(BlockCell.StallTop, BlockAtlasLayout.CellFor(BlockId.Stall, BlockFace.Up));
        Assert.Equal(BlockCell.RoofTop, BlockAtlasLayout.CellFor(BlockId.Roof, BlockFace.Up));
        Assert.Equal(BlockCell.BerryBush, BlockAtlasLayout.CellFor(BlockId.BerryBush, BlockFace.South));
        Assert.NotEqual(BlockAtlasLayout.CellFor(BlockId.Bush, BlockFace.Up), BlockAtlasLayout.CellFor(BlockId.BerryBush, BlockFace.Up));
        Assert.NotEqual(BlockAtlasLayout.CellFor(BlockId.Stone, BlockFace.Up), BlockAtlasLayout.CellFor(BlockId.FireRing, BlockFace.Up));
    }

    [Fact]
    public void TheUvOfACellIsItsPixelRectangleOverTheAtlasSize()
    {
        var (u0, v0, u1, v1) = BlockAtlasLayout.Uv(BlockCell.Stone);
        var (x, y, w, h) = BlockAtlasLayout.PixelRectangle(BlockCell.Stone);
        Assert.Equal((x + BlockAtlasLayout.Inset) / BlockAtlasLayout.Width, u0, 6);
        Assert.Equal((y + BlockAtlasLayout.Inset) / BlockAtlasLayout.Height, v0, 6);
        Assert.Equal((x + w - BlockAtlasLayout.Inset) / BlockAtlasLayout.Width, u1, 6);
        Assert.Equal((y + h - BlockAtlasLayout.Inset) / BlockAtlasLayout.Height, v1, 6);
        Assert.Equal(((int)BlockCell.Stone % BlockAtlasLayout.Columns) * BlockAtlasLayout.CellSize, x);
    }

    [Fact]
    public void TheArtIsDeterministicTheRightSizeAndEveryWorldCellHasPixels()
    {
        var first = BlockArt.AtlasPixels();
        var second = BlockArt.AtlasPixels();
        Assert.Equal(BlockAtlasLayout.Width * BlockAtlasLayout.Height * 4, first.Length);
        Assert.Equal(first, second);
        foreach (var cell in Enum.GetValues<BlockCell>())
        {
            var pixels = BlockArt.Pixels(cell);
            var opaque = 0;
            for (var i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] > 0)
                {
                    opaque++;
                }
            }

            Assert.True(opaque > 8, $"{cell} draws almost nothing");
        }
    }

    [Fact]
    public void TheBlocksOfTheWorldAreFullyOpaqueExceptWaterAndTheCellsDrawnOnClear()
    {
        foreach (var cell in new[] { BlockCell.GrassTop, BlockCell.GrassSide, BlockCell.Dirt, BlockCell.Stone, BlockCell.MossyStone, BlockCell.Cobble, BlockCell.Sand, BlockCell.Snow, BlockCell.SnowSide, BlockCell.PathTop, BlockCell.TrunkSide, BlockCell.TrunkEnd, BlockCell.SpruceBark, BlockCell.BirchBark, BlockCell.Planks, BlockCell.White })
        {
            var pixels = BlockArt.Pixels(cell);
            for (var i = 3; i < pixels.Length; i += 4)
            {
                Assert.Equal(255, pixels[i]);
            }
        }

        foreach (var cell in new[] { BlockCell.Water, BlockCell.WaterShallow })
        {
            var water = BlockArt.Pixels(cell);
            for (var i = 3; i < water.Length; i += 4)
            {
                Assert.InRange(water[i], 100, 255); // translucent: the alpha pass blends it
            }
        }
    }

    [Fact]
    public void TheColoursAreBrightAndSaturatedAndNatural()
    {
        // The brief: greens for vegetation, clear blues for water, warm wood. Average each cell's colour and check the dominant channel.
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
        Assert.True(grass.G > grass.R * 1.4 && grass.G > grass.B * 2);
        var leaves = Mean(BlockCell.Leaves);
        Assert.True(leaves.G > leaves.R * 1.6 && leaves.G > leaves.B * 1.8);
        var water = Mean(BlockCell.Water);
        Assert.True(water.B > water.G && water.B > water.R * 2.5);
        var planks = Mean(BlockCell.Planks);
        Assert.True(planks.R > planks.G && planks.G > planks.B); // warm
        foreach (var cell in new[] { BlockCell.GrassTop, BlockCell.Sand, BlockCell.RoofTop, BlockCell.Planks })
        {
            var c = Mean(cell);
            Assert.True(Math.Max(c.R, Math.Max(c.G, c.B)) > 130, $"{cell} is dark");
        }
    }

    [Fact]
    public void ItemIconsExistForWhatTheVoxelWorldGivesThePlayer()
    {
        Assert.Equal(BlockCell.IconWood, BlockAtlasLayout.IconFor(ItemId.Wood));
        Assert.Equal(BlockCell.IconBerry, BlockAtlasLayout.IconFor(ItemId.Berry));
        Assert.Equal(BlockCell.IconTorch, BlockAtlasLayout.IconFor(ItemId.Torch)); // S15: a stick with its flame, not the flame's square
        Assert.Equal(BlockCell.IconBottle, BlockAtlasLayout.IconFor(ItemId.Bottle));
        Assert.Equal(BlockCell.IconWaterBottle, BlockAtlasLayout.IconFor(ItemId.WaterBottle));
        Assert.Equal(BlockCell.IconBread, BlockAtlasLayout.IconFor(ItemId.Bread));
        Assert.Null(BlockAtlasLayout.IconFor((ItemId)999));
    }

    [Fact]
    public void TheBottleOfWaterIsBluerThanTheEmptyBottleAndTheBreadIsWarm()
    {
        static (double R, double B) Mean(BlockCell cell)
        {
            var p = BlockArt.Pixels(cell);
            double r = 0, b = 0;
            var n = 0;
            for (var i = 0; i < p.Length; i += 4)
            {
                if (p[i + 3] > 0)
                {
                    r += p[i];
                    b += p[i + 2];
                    n++;
                }
            }

            return (r / n, b / n);
        }

        var empty = Mean(BlockCell.IconBottle);
        var full = Mean(BlockCell.IconWaterBottle);
        var bread = Mean(BlockCell.IconBread);
        Assert.True(full.B - full.R > empty.B - empty.R + 40, "the filled bottle shows water");
        Assert.True(bread.R > bread.B * 1.8, "bread is warm");
    }
}
