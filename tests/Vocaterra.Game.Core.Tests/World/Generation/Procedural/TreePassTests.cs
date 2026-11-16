using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>The trees of the procedural world (B03): they stand on solid ground, apart, below the tree line, and a crown that hangs over a chunk's border is whole on both sides.</summary>
public sealed class TreePassTests
{
    private static readonly BlockId[] Trunks = [BlockId.Trunk, BlockId.SpruceTrunk, BlockId.BirchTrunk];

    private static (BlockId Leaves, int Offset) CrownOf(BlockId trunk) => trunk switch
    {
        BlockId.SpruceTrunk => (BlockId.SpruceLeaves, 1),
        BlockId.BirchTrunk => (BlockId.BirchLeaves, 3),
        _ => (BlockId.Leaves, 3),
    };

    // The trunks of a region as (x, y of the lowest trunk block, z, block): a trunk block with no trunk block under it.
    private static List<(int X, int Y, int Z, BlockId Block)> TrunkBases(GeneratedRegion region)
    {
        var bases = new List<(int, int, int, BlockId)>();
        for (var z = region.MinZ; z < region.MaxZ; z++)
        {
            for (var x = region.MinX; x < region.MaxX; x++)
            {
                var highest = region.HighestSolid(x, z);
                for (var y = highest; y > Math.Max(0, highest - 24); y--)
                {
                    var block = region.BlockAt(x, y, z);
                    if (Trunks.Contains(block) && region.BlockAt(x, y - 1, z) != block)
                    {
                        bases.Add((x, y, z, block));
                    }
                }
            }
        }

        return bases;
    }

    private static GeneratedRegion Woodland(ulong seed)
    {
        var (cx, cz) = ProceduralTestWorld.Find(seed, Feature.Forest);
        return new GeneratedRegion(ProceduralTestWorld.Source(seed), cx - 1, cz - 1, 4, 4);
    }

    [Fact]
    public void Given_AWoodland_When_ItIsGenerated_Then_ManyTreesStandOnSolidGroundWithTheirTrunkInTheOpen()
    {
        var region = Woodland(42);
        var bases = TrunkBases(region);

        Assert.True(bases.Count > 25, $"{bases.Count} trees in a woodland of 4 x 4 chunk columns");
        foreach (var (x, y, z, block) in bases)
        {
            Assert.True(BlockDefinition.IsSolid(region.BlockAt(x, y - 1, z)), $"the tree at {x}, {z} stands on {region.BlockAt(x, y - 1, z)}");
            Assert.NotEqual(BlockId.Water, region.BlockAt(x, y - 1, z));
            Assert.Contains(block, Trunks);
        }
    }

    [Fact]
    public void Given_AWoodland_When_TheTrunksAreCompared_Then_NoTwoStandCloserThanThreeBlocksAcrossChunkBordersToo()
    {
        var bases = TrunkBases(Woodland(7));

        Assert.True(bases.Count > 25);
        for (var i = 0; i < bases.Count; i++)
        {
            for (var j = i + 1; j < bases.Count; j++)
            {
                var (dx, dz) = (bases[i].X - bases[j].X, bases[i].Z - bases[j].Z);
                Assert.True((dx * dx) + (dz * dz) >= 9, $"trunks at ({bases[i].X}, {bases[i].Z}) and ({bases[j].X}, {bases[j].Z})");
            }
        }
    }

    [Fact]
    public void Given_EveryTreeOfAWoodland_When_ItsTopIsRead_Then_ItsCrownStartsAboveItInTheOwnLeavesAndNothingGrowsBeyondTheTreeLine()
    {
        var seed = 2026UL;
        var region = Woodland(seed);
        var data = TestGame.Data;
        var climate = new ClimateSampler(data.WorldGen, seed);
        var model = new HeightModel(data.WorldGen, seed);
        var table = new BiomeTable(data.Biomes, data.WorldGen, seed);

        var bases = TrunkBases(region);

        Assert.True(bases.Count > 25);
        foreach (var (x, y, z, block) in bases)
        {
            var top = y;
            while (region.BlockAt(x, top + 1, z) == block)
            {
                top++;
            }

            var (leaves, offset) = CrownOf(block);
            Assert.Equal(leaves, region.BlockAt(x, top + offset, z));
            var c = climate.At(x, z);
            var shape = model.Shape(x, z, c);
            var column = table.Classify(c, shape.Height, x, z, shape.Water);
            Assert.True(column.TreesGrow, $"the tree at {x}, {z} stands above the tree line");
            Assert.True(column.Row.Trees.Density > 0, $"the tree at {x}, {z} stands in '{column.Row.Id}', which grows none");
        }
    }

    [Fact]
    public void Given_TreesBesideAChunkBorder_When_TheirCrownsAreCounted_Then_LeavesHangOverTheBorderIntoTheNeighbour()
    {
        var region = Woodland(42);
        var checkedTrees = 0;
        foreach (var (x, y, z, block) in TrunkBases(region))
        {
            var (leaves, _) = CrownOf(block);
            var directions = new List<(int X, int Z)>();
            if ((x & 15) == 15 || (x & 15) == 0)
            {
                directions.Add(((x & 15) == 15 ? 1 : -1, 0));
            }

            if ((z & 15) == 15 || (z & 15) == 0)
            {
                directions.Add((0, (z & 15) == 15 ? 1 : -1));
            }

            foreach (var (nx, nz) in directions)
            {
                if (x + (4 * nx) < region.MinX || x + (4 * nx) >= region.MaxX || z + (4 * nz) < region.MinZ || z + (4 * nz) >= region.MaxZ)
                {
                    continue;
                }

                var over = 0;
                for (var step = 1; step <= 3; step++)
                {
                    for (var up = 3; up <= 12; up++)
                    {
                        over += region.BlockAt(x + (step * nx), y + up, z + (step * nz)) == leaves ? 1 : 0;
                    }
                }

                checkedTrees++;
                Assert.True(over > 0, $"the tree at {x}, {z} has no leaves over the border");
            }
        }

        Assert.True(checkedTrees >= 3, $"{checkedTrees} trees beside a border were checked");
    }

    [Fact]
    public void Given_TheHighestRangeAndAPlainWithoutTrees_When_TheyAreGenerated_Then_NoTrunkStandsOnTheBareRock()
    {
        var (cx, cz) = ProceduralTestWorld.Find(42, Feature.Mountain);
        var region = new GeneratedRegion(ProceduralTestWorld.Source(42), cx - 1, cz - 1, 3, 3);

        foreach (var (x, y, z, _) in TrunkBases(region))
        {
            Assert.True(y < 190, $"a tree stands at {x}, {y}, {z} among the peaks");
        }
    }
}
