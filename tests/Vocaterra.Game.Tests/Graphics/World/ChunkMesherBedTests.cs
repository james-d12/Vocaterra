using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>Y03: the bed and the bedroll are piles, each at its own height.</summary>
public sealed class ChunkMesherBedTests
{
    [Fact]
    public void Given_ABedOrABedroll_When_ItsShapeIsRead_Then_ItIsAPile()
    {
        Assert.Equal(BlockShape.Pile, ChunkMesher.ShapeOf(BlockId.Bed));
        Assert.Equal(BlockShape.Pile, ChunkMesher.ShapeOf(BlockId.Bedroll));
    }

    [Theory]
    [InlineData(BlockId.Bedroll, 5.29f, 5.31f)] // a bedroll lies lower, 0.3
    [InlineData(BlockId.Loaves, 5.41f, 5.43f)] // the stall's goods keep 0.42
    public void Given_APileOfOneKind_When_Meshed_Then_ItsBoxStandsAtItsHeight(BlockId kind, float low, float high)
    {
        var pile = ChunkMesher.Build(new MapBlocks().Set(5, 5, 5, kind), 0, 0, 0).Opaque;
        Assert.Equal(6, pile.FaceCount);
        var top = pile.Vertices.ToArray().Max(static v => v.Y);
        Assert.InRange(top, low, high);
    }

    [Fact]
    public void Given_ABedInTheOpen_When_Meshed_Then_ItsMattressIsHalfABlockHighAndItsHeadboardStandsOverIt()
    {
        var bed = ChunkMesher.Build(new MapBlocks().Set(5, 5, 5, BlockId.Bed), 0, 0, 0).Opaque;
        Assert.Equal(12, bed.FaceCount); // the mattress and the headboard, six faces each
        var vertices = bed.Vertices.ToArray();
        Assert.InRange(vertices.Max(static v => v.Y), 5 + ChunkMesher.BedHeadboardHeight - 0.01f, 5 + ChunkMesher.BedHeadboardHeight + 0.01f);
        Assert.Contains(vertices, static v => Math.Abs(v.Y - 5.5f) < 0.01f); // the mattress's top
        Assert.True(vertices.Where(static v => v.Y > 5.6f).All(static v => v.Z < 5.25f), "the headboard is at the north edge when no wall is near");
    }

    [Theory]
    [InlineData(-1, 0, 0)] // a wall to the west: the head is at the west edge
    [InlineData(1, 0, 1)] // a wall to the east
    [InlineData(0, 1, 2)] // a wall to the south
    public void Given_ABedBesideAWall_When_Meshed_Then_ItsHeadboardStandsAgainstThatWall(int wallDx, int wallDz, int edge)
    {
        var blocks = new MapBlocks().Set(5, 5, 5, BlockId.Bed).Set(5 + wallDx, 5, 5 + wallDz, BlockId.Planks);
        var tall = ChunkMesher.Build(blocks, 0, 0, 0).Opaque.Vertices.ToArray().Where(static v => v.Y > 5.6f && v.X > 5.01f && v.X < 5.99f && v.Z > 5.01f && v.Z < 5.99f).ToArray();
        Assert.NotEmpty(tall);
        switch (edge)
        {
            case 0: Assert.All(tall, static v => Assert.True(v.X < 5.25f)); break;
            case 1: Assert.All(tall, static v => Assert.True(v.X > 5.75f)); break;
            default: Assert.All(tall, static v => Assert.True(v.Z > 5.75f)); break;
        }
    }
}
