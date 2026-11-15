using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using static Vocaterra.Game.Core.Tests.Voxel.StreamedGridSupport;

namespace Vocaterra.Game.Core.Tests.World;

/// <summary>B05: the world's state digest hashes a header and the changed chunks, so it does not depend on what is resident and it costs by the edits.</summary>
public sealed class WorldStateDigestTests
{
    private static readonly WorldStateHeader Header = WorldStateHeader.Unkeyed(42);

    // Floors of stone, dirt on the odd columns, in the layer y 0 only; everything else is air.
    private static CountingGenerator Terrain() => new(static coord => coord.Y == 0 ? Floor((coord.X & 1) == 0 ? BlockId.Stone : BlockId.Dirt) : null);

    private static VoxelGrid Streamed() => new(WorldLimits.Default, Terrain());

    [Fact]
    public void Given_AValleyNobodyEdited_When_TheDigestIsTaken_Then_NoChunkIsListed()
    {
        var grid = TestGame.Route().Grid;

        Assert.Empty(grid.ChangedChunks());
        Assert.Equal(WorldStateDigest.ComputeHex(TestGame.Route().Grid, Header), WorldStateDigest.ComputeHex(grid, Header));
    }

    [Fact]
    public void Given_AnEdit_When_TheDigestIsTaken_Then_ItMovesAndTheChunkIsListed()
    {
        var grid = TestGame.Route().Grid;
        var before = WorldStateDigest.ComputeHex(grid, Header);

        grid.SetBlock(50, 5, 2, grid.GetBlock(50, 5, 2) == BlockId.Torch ? BlockId.Air : BlockId.Torch);

        Assert.NotEqual(before, WorldStateDigest.ComputeHex(grid, Header));
        Assert.Equal([new VoxelChunkCoord(3, 0, 0)], grid.ChangedChunks().Select(static c => c.Coord));
    }

    [Fact]
    public void Given_AnEditThatIsUndone_When_TheDigestIsTaken_Then_ItIsTheDigestOfTheWorldBeforeTheEdit()
    {
        var grid = TestGame.Route().Grid;
        var before = WorldStateDigest.ComputeHex(grid, Header);
        var first = grid.GetBlock(50, 5, 2);
        var second = grid.GetBlock(19, 5, 19);

        grid.SetBlock(50, 5, 2, BlockId.Torch);
        grid.SetBlock(19, 5, 19, BlockId.Stone);
        Assert.NotEqual(before, WorldStateDigest.ComputeHex(grid, Header));
        grid.SetBlock(19, 5, 19, BlockId.Dirt);
        grid.SetBlock(50, 5, 2, first);
        grid.SetBlock(19, 5, 19, second);

        Assert.Empty(grid.ChangedChunks());
        Assert.Equal(before, WorldStateDigest.ComputeHex(grid, Header));
    }

    [Fact]
    public void Given_TheSameEditsOnGridsWithDifferentResidency_When_TheDigestIsTaken_Then_TheDigestIsTheSame()
    {
        var few = Streamed();
        var many = Streamed();
        foreach (var x in Enumerable.Range(-5, 11))
        {
            for (var z = -5; z <= 5; z++)
            {
                _ = many.GetChunk(new VoxelChunkCoord(x, 0, z)); // generated and resident, but unchanged
            }
        }

        foreach (var grid in new[] { few, many })
        {
            grid.SetBlock(40, 3, -20, BlockId.Torch);
            grid.SetBlock(-100, -20, 7, BlockId.Stone); // a negative layer: air generated there
        }

        Assert.True(many.ResidentChunkCount > few.ResidentChunkCount + 100);
        Assert.Equal(WorldStateDigest.ComputeHex(few, Header), WorldStateDigest.ComputeHex(many, Header));
        Assert.Equal(2, many.ChangedChunks().Count);
    }

    [Fact]
    public void Given_AnUnchangedChunkThatIsEvicted_When_TheDigestIsTaken_Then_ItDoesNotMove()
    {
        var grid = Streamed();
        grid.SetBlock(1, 3, 1, BlockId.Torch);
        _ = grid.GetChunk(new VoxelChunkCoord(4, 0, 4));
        var before = WorldStateDigest.ComputeHex(grid, Header);

        Assert.True(grid.TryEvictChunk(new VoxelChunkCoord(4, 0, 4)));
        Assert.False(grid.TryEvictChunk(new VoxelChunkCoord(0, 0, 0))); // changed chunks are state and stay

        Assert.Equal(before, WorldStateDigest.ComputeHex(grid, Header));
    }

    [Fact]
    public void Given_ChangedChunksOnAGrid_When_TheyAreListed_Then_TheyAreSortedByLayerRowAndColumn()
    {
        var grid = Streamed();
        grid.SetBlock(40, 3, 40, BlockId.Torch);
        grid.SetBlock(-100, -20, 7, BlockId.Stone);
        grid.SetBlock(1, 3, 1, BlockId.Torch);

        Assert.Equal([new VoxelChunkCoord(-7, -2, 0), new VoxelChunkCoord(0, 0, 0), new VoxelChunkCoord(2, 0, 2)], grid.ChangedChunks().Select(static c => c.Coord));
    }

    [Fact]
    public void Given_AHeaderField_When_ItChanges_Then_TheDigestMoves()
    {
        var grid = Streamed();
        var baseline = WorldStateDigest.ComputeHex(grid, Header);

        Assert.NotEqual(baseline, WorldStateDigest.ComputeHex(grid, Header with { Seed = 43 }));
        Assert.NotEqual(baseline, WorldStateDigest.ComputeHex(grid, Header with { GeneratorVersion = Header.GeneratorVersion + 1 }));
        Assert.NotEqual(baseline, WorldStateDigest.ComputeHex(grid, Header with { TemplateDigest = Enumerable.Repeat((byte)1, WorldStateHeader.DigestLength).ToArray() }));
        Assert.NotEqual(baseline, WorldStateDigest.ComputeHex(grid, Header with { GenerationDataHash = Enumerable.Repeat((byte)1, WorldStateHeader.DigestLength).ToArray() }));
    }

    [Fact]
    public void Given_TheBundle_When_TheHeaderIsMade_Then_ItHashesTheWorldGenerationFilesOnly()
    {
        var header = WorldStateHeader.For(TestGame.Bundle, 42);

        Assert.Equal(["game/world.json", "game/biomes.json", "game/underground.json"], WorldStateHeader.GenerationFiles);
        Assert.NotEqual(new byte[WorldStateHeader.DigestLength], header.GenerationDataHash);
        Assert.Equal(header.GenerationDataHash, WorldStateHeader.For(TestGame.Bundle, 7).GenerationDataHash); // the seed is its own field
    }
}
