using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>Y03, Y08: the bed and the bedroll are two cells long, a head cell and a foot cell meshed as one piece; the goods of the stall are piles at their own height.</summary>
public sealed class ChunkMesherBedTests
{
    private static MeshVertex[] Mesh(MapBlocks blocks) => ChunkMesher.Build(blocks, 0, 0, 0).Opaque.Vertices.ToArray();

    private static MapBlocks Pair(BlockId head, BlockId foot, int footDx, int footDz) => new MapBlocks().Set(5, 5, 5, head).Set(5 + footDx, 5, 5 + footDz, foot);

    [Fact]
    public void Given_TheBedAndBedrollKinds_When_TheirShapesAreRead_Then_AllFourArePiles()
    {
        foreach (var kind in new[] { BlockId.Bed, BlockId.BedFoot, BlockId.Bedroll, BlockId.BedrollFoot })
        {
            Assert.Equal(BlockShape.Pile, ChunkMesher.ShapeOf(kind));
        }
    }

    [Fact]
    public void Given_APileOfTheStall_When_Meshed_Then_ItsBoxStandsAtItsHeight()
    {
        var pile = ChunkMesher.Build(new MapBlocks().Set(5, 5, 5, BlockId.Loaves), 0, 0, 0).Opaque;
        Assert.Equal(6, pile.FaceCount);
        Assert.InRange(pile.Vertices.ToArray().Max(static v => v.Y), 5.41f, 5.43f);
    }

    [Fact]
    public void Given_ATwoCellBedInTheOpen_When_Meshed_Then_OneMattressRunsOverBothCellsWithNoSeamAndTheHeadboardStandsAtTheHeadEnd()
    {
        var bed = ChunkMesher.Build(Pair(BlockId.Bed, BlockId.BedFoot, 0, 1), 0, 0, 0).Opaque;
        var vertices = bed.Vertices.ToArray();

        // head cell: mattress, frame, headboard, pillow (the mattress and the frame without their south face); foot cell: mattress and frame (no north face) and the footboard
        Assert.Equal(22 + 16, bed.FaceCount);
        var mattressTop = vertices.Where(static v => Math.Abs(v.Y - (5 + ChunkMesher.BedMattressTop)) < 0.005f).ToArray();
        Assert.Contains(mattressTop, static v => Math.Abs(v.Z - 6f) < 0.005f); // the two halves meet in one edge across the seam
        Assert.InRange(mattressTop.Min(static v => v.Z), 5.1f, 5.2f);
        Assert.InRange(mattressTop.Max(static v => v.Z), 6.85f, 6.95f);
        Assert.InRange(vertices.Max(static v => v.Y), 5 + ChunkMesher.BedHeadboardHeight - 0.01f, 5 + ChunkMesher.BedHeadboardHeight + 0.01f);
        Assert.True(vertices.Where(static v => v.Y > 5 + ChunkMesher.BedFootboardHeight + 0.01f && v.Y < 5 + ChunkMesher.BedMattressTop).All(static v => v.Z < 6f), "nothing between the footboard's height and the mattress's rises at the foot end");
        Assert.True(vertices.Where(static v => v.Y > 5.8f).All(static v => v.Z < 5.25f), "the headboard is at the north end, beside the head cell's wall edge");
        Assert.True(vertices.Where(static v => v.Z > 6.9f).Max(static v => v.Y) < 5 + ChunkMesher.BedFootboardHeight + 0.01f, "the footboard is lower than the headboard");
    }

    [Fact]
    public void Given_ATwoCellBed_When_Meshed_Then_ItStandsOffTheFloorAndIsLongerThanItIsWide()
    {
        var vertices = Mesh(Pair(BlockId.Bed, BlockId.BedFoot, 0, 1));
        var mattress = vertices.Where(static v => Math.Abs(v.Y - 5.55f) < 0.005f).ToArray();
        Assert.InRange(mattress.Max(static v => v.Z) - mattress.Min(static v => v.Z), 1.6f, 1.9f);
        Assert.InRange(mattress.Max(static v => v.X) - mattress.Min(static v => v.X), 0.8f, 0.9f);
        Assert.DoesNotContain(vertices, static v => v.Y > 5.01f && v.Y < 5.19f && v.X > 5.1f && v.X < 5.9f && v.Z > 5.2f && v.Z < 6.9f); // under the mattress there is air between the boards
    }

    [Theory]
    [InlineData(1, 0, 0)] // the foot lies to the east of the head: the head end is the west edge of the head cell
    [InlineData(-1, 0, 1)] // the foot lies to the west: the head end is the east edge
    [InlineData(0, -1, 2)] // the foot lies to the north: the head end is the south edge
    public void Given_ATwoCellBedWhoseFootLiesInAnotherDirection_When_Meshed_Then_TheHeadboardStandsAtTheOppositeEnd(int footDx, int footDz, int edge)
    {
        var vertices = Mesh(Pair(BlockId.Bed, BlockId.BedFoot, footDx, footDz));
        var tall = vertices.Where(static v => v.Y > 5.8f).ToArray();
        Assert.NotEmpty(tall);
        switch (edge)
        {
            case 0: Assert.All(tall, static v => Assert.True(v.X < 5.25f)); break;
            case 1: Assert.All(tall, static v => Assert.True(v.X > 5.75f)); break;
            default: Assert.All(tall, static v => Assert.True(v.Z > 5.75f)); break;
        }
    }

    [Fact]
    public void Given_ABedWithItsFootToTheEast_When_TheFootCellIsMeshed_Then_ItsFootboardIsAtTheFarEdgeAndNothingTallStandsInIt()
    {
        var vertices = Mesh(Pair(BlockId.Bed, BlockId.BedFoot, 1, 0));
        var footCell = vertices.Where(static v => v.X > 6.01f).ToArray();
        Assert.InRange(footCell.Max(static v => v.X), 6.9f, 7f);
        Assert.True(footCell.Max(static v => v.Y) < 5 + ChunkMesher.BedFootboardHeight + 0.01f);
    }

    [Fact]
    public void Given_ALoneHeadWithNoFoot_When_Meshed_Then_ItIsAShortBedWithBothBoardsInsideItsCell()
    {
        var bed = ChunkMesher.Build(new MapBlocks().Set(5, 5, 5, BlockId.Bed), 0, 0, 0).Opaque;
        var vertices = bed.Vertices.ToArray();
        Assert.Equal(30, bed.FaceCount); // mattress, frame, headboard, pillow and footboard, six faces each
        Assert.All(vertices, static v =>
        {
            Assert.InRange(v.X, 5f, 6f);
            Assert.InRange(v.Z, 5f, 6f);
            Assert.InRange(v.Y, 5f, 5 + ChunkMesher.BedHeadboardHeight + 0.01f);
        });
        Assert.True(vertices.Where(static v => v.Y > 5.8f).All(static v => v.Z < 5.25f), "the head is to the north when no wall is near");
    }

    [Fact]
    public void Given_ALoneHeadBesideAWall_When_Meshed_Then_ItsHeadboardStandsAgainstTheWall()
    {
        var blocks = new MapBlocks().Set(5, 5, 5, BlockId.Bed).Set(4, 5, 5, BlockId.Planks);
        var tall = Mesh(blocks).Where(static v => v.Y > 5.8f && v.X > 5.01f).ToArray();
        Assert.NotEmpty(tall);
        Assert.All(tall, static v => Assert.True(v.X < 5.25f));
    }

    [Fact]
    public void Given_ALoneFootWithNoHead_When_Meshed_Then_ItIsAHalfBedInsideItsCellWithNoHeadboard()
    {
        var bed = ChunkMesher.Build(new MapBlocks().Set(5, 5, 5, BlockId.BedFoot), 0, 0, 0).Opaque;
        Assert.Equal(18, bed.FaceCount); // mattress, frame and footboard
        Assert.All(bed.Vertices.ToArray(), static v =>
        {
            Assert.InRange(v.X, 5f, 6f);
            Assert.InRange(v.Z, 5f, 6f);
            Assert.True(v.Y <= 5 + ChunkMesher.BedFootboardHeight + 0.01f);
        });
    }

    [Fact]
    public void Given_ATwoCellBedroll_When_Meshed_Then_ItIsAMatLowerThanABedWithARolledPillowAndNoBoard()
    {
        var roll = ChunkMesher.Build(Pair(BlockId.Bedroll, BlockId.BedrollFoot, 0, 1), 0, 0, 0).Opaque;
        var vertices = roll.Vertices.ToArray();
        Assert.Equal(11 + 5, roll.FaceCount); // the head half's mat (no south face) and the rolled pillow, the foot half's mat (no north face)
        Assert.InRange(vertices.Max(static v => v.Y), 5 + ChunkMesher.BedrollPillowTop - 0.01f, 5 + ChunkMesher.BedrollPillowTop + 0.01f);
        Assert.True(ChunkMesher.BedrollPillowTop < ChunkMesher.BedMattressTop, "a bedroll lies lower than a bed's mattress");
        var mat = vertices.Where(static v => Math.Abs(v.Y - (5 + ChunkMesher.BedrollMatTop)) < 0.005f).ToArray();
        Assert.InRange(mat.Max(static v => v.Z) - mat.Min(static v => v.Z), 1.7f, 1.95f); // the same length as a bed's
    }

    [Fact]
    public void Given_ALoneBedrollHalf_When_Meshed_Then_ItIsASaneMatInsideItsCell()
    {
        foreach (var kind in new[] { BlockId.Bedroll, BlockId.BedrollFoot })
        {
            var vertices = Mesh(new MapBlocks().Set(5, 5, 5, kind));
            Assert.NotEmpty(vertices);
            Assert.All(vertices, static v =>
            {
                Assert.InRange(v.X, 5f, 6f);
                Assert.InRange(v.Z, 5f, 6f);
                Assert.InRange(v.Y, 5f, 5 + ChunkMesher.BedrollPillowTop + 0.01f);
            });
        }
    }
}
