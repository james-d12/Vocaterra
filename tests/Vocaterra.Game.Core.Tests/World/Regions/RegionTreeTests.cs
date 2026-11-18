using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>The trees at the valley's border (B06): none inside the region's chunks, none within the clearance of the border so no crown crosses it, and thinning across the margin.</summary>
public sealed class RegionTreeTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static readonly int Clearance = TestGame.Data.WorldGen.Blend.TreeClearance;
    private static readonly int Margin = TestGame.Data.WorldGen.Blend.Margin;

    // The base of every trunk (a trunk block with none under it) in the chunk columns round the valley, up to 112 blocks out, as (distance from the footprint, x, y, z).
    private static List<(int Distance, int X, int Y, int Z)> Trunks(WorldComposition world, RegionBounds b)
    {
        var found = new List<(int, int, int, int)>();
        for (var cz = b.MinChunkZ - 7; cz < b.MinChunkZ + b.ChunksZ + 7; cz++)
        {
            for (var cx = b.MinChunkX - 7; cx < b.MinChunkX + b.ChunksX + 7; cx++)
            {
                if (b.CoversColumn(cx, cz))
                {
                    continue;
                }

                var column = world.Source.GenerateColumn(cx, cz);
                for (var i = 0; i < column.Count; i++)
                {
                    var chunk = column[i];
                    if (chunk.IsEmpty)
                    {
                        continue;
                    }

                    for (var ly = 0; ly < 16; ly++)
                    {
                        for (var lz = 0; lz < 16; lz++)
                        {
                            for (var lx = 0; lx < 16; lx++)
                            {
                                if (!BlockDefinition.IsTrunk(chunk[lx, ly, lz]))
                                {
                                    continue;
                                }

                                var below = ly > 0 ? chunk[lx, ly - 1, lz] : i > 0 ? column[i - 1][lx, 15, lz] : BlockId.Stone;
                                if (!BlockDefinition.IsTrunk(below))
                                {
                                    var (x, z) = ((cx << 4) + lx, (cz << 4) + lz);
                                    found.Add((b.DistanceTo(x, z), x, (chunk.Coord.Y << 4) + ly, z));
                                }
                            }
                        }
                    }
                }
            }
        }

        return found;
    }

    private static readonly Lazy<List<(int Distance, int X, int Y, int Z)>> ValleyTrunks = new(() => Trunks(RegionTestWorld.Composition(), RegionTestWorld.Bounds));

    [Fact]
    public void Given_TheChunksRoundTheValley_When_TheirTrunksAreFound_Then_NoneStandsWithinTheClearanceOfTheBorder()
    {
        var trunks = ValleyTrunks.Value;

        Assert.True(trunks.Count > 200, $"{trunks.Count} trunks");
        Assert.All(trunks, t => Assert.True(t.Distance > Clearance, $"a trunk {t.Distance} columns out at {t.X},{t.Y},{t.Z}"));
        output.WriteLine($"{trunks.Count} trunks within 112 blocks of the valley; the nearest {trunks.Min(static t => t.Distance)} columns out");
    }

    [Fact]
    public void Given_TheTreesRoundTheValley_When_TheyAreCountedInBandsOfTheMargin_Then_TheyThinOutTowardsTheBorder()
    {
        var trunks = ValleyTrunks.Value;
        var third = Margin / 3;
        int Count(int from, int to) => trunks.Count(t => t.Distance >= from && t.Distance < to);

        var near = Count(Clearance + 1, third);
        var middle = Count(third, 2 * third);
        var far = Count(2 * third, Margin);
        var free = Count(Margin, Margin + (Margin / 2));
        output.WriteLine($"trunks by distance: {Clearance + 1}-{third - 1}: {near}; {third}-{(2 * third) - 1}: {middle}; {2 * third}-{Margin - 1}: {far}; {Margin}-{Margin + (Margin / 2) - 1}: {free}");

        Assert.True(near < middle, $"{near} near the border, {middle} in the middle band");
        Assert.True(middle < far, $"{middle} in the middle band, {far} in the outer band");
        Assert.True(far < free * 2, $"{far} in the outer band against {free} beyond the margin (a wider ring)");
        Assert.True(near * 2 < free, $"{near} near the border against {free} in the free land");
    }

    [Fact]
    public void Given_TheProceduralSourceOverTheValleysFootprint_When_ItsChunksAreGenerated_Then_NoTrunkOrLeafIsInThemSoNoCrownCrossesTheBorder()
    {
        var world = RegionTestWorld.Composition();
        var b = RegionTestWorld.Bounds;
        var treeBlocks = 0;
        for (var cz = b.MinChunkZ; cz < b.MinChunkZ + b.ChunksZ; cz += 3)
        {
            for (var cx = b.MinChunkX; cx < b.MinChunkX + b.ChunksX; cx += 3)
            {
                for (var cy = 0; cy < 8; cy++)
                {
                    var chunk = world.Procedural.Generate(new VoxelChunkCoord(cx, cy, cz));
                    treeBlocks += chunk.Blocks.ToArray().Count(static id => BlockDefinition.IsTrunk(id) || BlockDefinition.IsLeaves(id));
                }
            }
        }

        // The border chunks themselves, every layer: this is where a crown from outside would come in.
        foreach (var coord in new[] { new VoxelChunkCoord(23, 3, 5), new VoxelChunkCoord(23, 4, 13), new VoxelChunkCoord(0, 3, 10), new VoxelChunkCoord(10, 3, 0), new VoxelChunkCoord(10, 4, 23), new VoxelChunkCoord(23, 3, 23) })
        {
            treeBlocks += world.Procedural.Generate(coord).Blocks.ToArray().Count(static id => BlockDefinition.IsTrunk(id) || BlockDefinition.IsLeaves(id));
        }

        Assert.Equal(0, treeBlocks);
    }
}
