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
    [InlineData(BlockId.Bed, 5.49f, 5.51f)] // a bed stands half a block high
    [InlineData(BlockId.Bedroll, 5.29f, 5.31f)] // a bedroll lies lower, 0.3
    [InlineData(BlockId.Loaves, 5.41f, 5.43f)] // the stall's goods keep 0.42
    public void Given_APileOfOneKind_When_Meshed_Then_ItsBoxStandsAtItsHeight(BlockId kind, float low, float high)
    {
        var pile = ChunkMesher.Build(new MapBlocks().Set(5, 5, 5, kind), 0, 0, 0).Opaque;
        Assert.Equal(6, pile.FaceCount);
        var top = pile.Vertices.ToArray().Max(static v => v.Y);
        Assert.InRange(top, low, high);
    }
}
