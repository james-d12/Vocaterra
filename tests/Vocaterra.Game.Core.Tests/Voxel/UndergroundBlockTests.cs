using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>B10: the nine underground blocks (slate, granite, limestone, gravel, lava, bedrock and the three ores): their properties, bedrock that no edit removes, lava that stops a body and glows.</summary>
public sealed class UndergroundBlockTests
{
    private static readonly BlockId[] Rock = [BlockId.Slate, BlockId.Granite, BlockId.Limestone, BlockId.Gravel, BlockId.Bedrock, BlockId.IronOre, BlockId.TinOre, BlockId.CopperOre];
    private static readonly BlockId[] All = [.. Rock, BlockId.Lava];

    public static TheoryData<BlockId> Blocks() => [.. All];

    [Fact]
    public void TheNineBlocksAreAppendedAfterTheCounterInTheOrderOfTheBrief()
    {
        BlockId[] expected = [BlockId.Slate, BlockId.Granite, BlockId.Limestone, BlockId.Gravel, BlockId.Lava, BlockId.Bedrock, BlockId.IronOre, BlockId.TinOre, BlockId.CopperOre];
        Assert.Equal(57, (int)BlockId.Slate);
        Assert.Equal(expected.Select(static id => (int)id), Enumerable.Range(57, 9));
        Assert.Equal(expected.Select(static id => id.ToString()).Order(StringComparer.Ordinal), All.Select(static id => id.ToString()).Order(StringComparer.Ordinal));
        Assert.True(Enum.GetValues<BlockId>().Max(static id => (int)id) < BlockKinds.Capacity);
    }

    [Theory]
    [MemberData(nameof(Blocks))]
    public void NoneOfThemCanBeGatheredOrGivesAnItem(BlockId id)
    {
        var definition = BlockDefinition.Get(id);
        Assert.False(definition.Gatherable);
        Assert.Equal(0, definition.GatherTicks);
        Assert.Null(definition.Drop);
        Assert.Equal(0, definition.DropCount);
        Assert.False(definition.Decor);
        Assert.False(definition.Transparent);
        Assert.Null(BlockDefinition.FromName(definition.Name) is { } found && found.Id != id ? found : null);
        Assert.DoesNotContain(Enum.GetValues<ItemId>(), item => BlockDefinition.PlacedBy(item) == id);
        Assert.DoesNotContain(ItemDefinition.All, item => string.Equals(item.Name, definition.Name, StringComparison.Ordinal));
        Assert.Empty(TestGame.Nouns.NounsOfBlock(id));
    }

    [Theory]
    [InlineData(BlockId.Slate, "slate")]
    [InlineData(BlockId.Granite, "granite")]
    [InlineData(BlockId.Limestone, "limestone")]
    [InlineData(BlockId.Gravel, "gravel")]
    [InlineData(BlockId.Lava, "lava")]
    [InlineData(BlockId.Bedrock, "bedrock")]
    [InlineData(BlockId.IronOre, "iron-ore")]
    [InlineData(BlockId.TinOre, "tin-ore")]
    [InlineData(BlockId.CopperOre, "copper-ore")]
    public void EachHasItsName(BlockId id, string name) => Assert.Equal(name, BlockDefinition.Get(id).Name);

    [Theory]
    [MemberData(nameof(Blocks))]
    public void TheRockIsSolidAndLavaIsAFluidThatIsNot(BlockId id)
    {
        var definition = BlockDefinition.Get(id);
        var lava = id == BlockId.Lava;
        Assert.Equal(!lava, definition.Solid);
        Assert.Equal(!lava, BlockDefinition.IsSolid(id));
        Assert.Equal(lava, definition.Liquid);
        Assert.Equal(lava, definition.StopsBody);
        Assert.Equal(lava, definition.Glows);
        Assert.Equal(id == BlockId.Bedrock, definition.Indestructible);
        Assert.False(definition.Tall);
    }

    [Fact]
    public void OnlyTheTorchTheLanternThePostTorchAndLavaGlow()
    {
        // B12: the placed torch (BlockId.Torch) became a light kind, so a torch placed in a cave lights it.
        var lights = BlockDefinition.All.Where(static d => d.Glows).Select(static d => d.Id).ToHashSet();
        Assert.Equal(new HashSet<BlockId> { BlockId.Torch, BlockId.Lantern, BlockId.PostTorch, BlockId.Lava }, lights);
    }

    [Fact]
    public void LavaIsNotSolidForSight()
    {
        var sim = FlatSim(g => g.SetBlock(40, GroundTop + 1, 32, BlockId.Lava));
        Assert.False(sim.Grid.IsSolid(40, GroundTop + 1, 32));
        Assert.False(BlockDefinition.Get(BlockId.Lava).Solid); // LineOfSight reads Solid: a wall of lava hides nothing
        Assert.True(sim.Grid.BlocksBody(40, GroundTop + 1, 32));
    }

    [Fact]
    public void BedrockIsRefusedByTheGridWhateverIsWrittenOverIt()
    {
        var sim = FlatSim(g =>
        {
            g.SetBlock(5, 3, 5, BlockId.Bedrock);
            g.FillColumn(6, 6, 0, 3, BlockId.Bedrock);
        });
        var grid = sim.Grid;
        foreach (var replacement in new[] { BlockId.Air, BlockId.Stone, BlockId.Water, BlockId.Lava, BlockId.Planks })
        {
            Assert.False(((IVoxelWriter)grid).SetBlock(5, 3, 5, replacement), $"{replacement} over bedrock");
            Assert.Equal(BlockId.Bedrock, grid.GetBlock(5, 3, 5));
        }

        var edits = grid.EditCount;
        ((IVoxelWriter)grid).FillColumn(6, 6, 0, 3, BlockId.Air);
        ((IVoxelWriter)grid).FillColumn(6, 6, 2, 9, BlockId.Dirt);
        Assert.Equal(BlockId.Bedrock, grid.GetBlock(6, 0, 6));
        Assert.Equal(BlockId.Bedrock, grid.GetBlock(6, 3, 6));
        Assert.Equal(BlockId.Dirt, grid.GetBlock(6, 4, 6)); // the cells above it were filled
        Assert.True(grid.EditCount > edits);
        Assert.False(((IVoxelWriter)grid).SetBlock(5, 3, 5, BlockId.Bedrock)); // the same kind changes nothing
    }

    [Fact]
    public void BedrockIsNotGatheredPlacedOverOrCountedAsAChange()
    {
        var changes = new List<BlockChange>();
        var sim = FlatSim(g => g.SetBlock(32, GroundTop + 1, 30, BlockId.Bedrock));
        sim.Grid.AddListener(new Listener(changes));
        Assert.False(BlockDefinition.Get(BlockId.Bedrock).Gatherable);
        Run(sim, 200, VoxelCommand.Interact(32, GroundTop + 1, 30));
        Assert.Equal(BlockId.Bedrock, sim.Grid.GetBlock(32, GroundTop + 1, 30));
        Assert.Empty(changes);
    }

    [Fact]
    public void AStreamedChunkOfBedrockStillInstallsAndIsGuardedAfterwards()
    {
        var blocks = new BlockId[VoxelChunk.Volume];
        Array.Fill(blocks, BlockId.Bedrock);
        var grid = new VoxelGrid(new WorldLimits(0, 0, 0, 64, 64, 64), new FixedGenerator(blocks));
        Assert.Equal(BlockId.Bedrock, grid.GetBlock(1, 1, 1));
        Assert.False(((IVoxelWriter)grid).SetBlock(1, 1, 1, BlockId.Air));
        Assert.Equal(BlockId.Bedrock, grid.GetBlock(1, 1, 1));
    }

    [Fact]
    public void ALavaCellIsNotStandableGroundForANpcButItBlocksTheNpcsStep()
    {
        var sim = FlatSim(g => g.SetBlock(33, GroundTop + 1, 32, BlockId.Lava));
        Assert.False(sim.Grid.CanStandOn(33, GroundTop + 1, 32));
        Assert.True(sim.Grid.BlocksBody(33, GroundTop + 1, 32));
        Assert.False(sim.Grid.BlocksBody(34, GroundTop + 1, 32));
    }

    private sealed class Listener(List<BlockChange> changes) : IBlockChangeListener
    {
        public void OnBlockChanged(BlockChange change) => changes.Add(change);
    }

    private sealed class FixedGenerator(BlockId[] blocks) : IChunkGenerator
    {
        public ChunkContent? Generate(VoxelChunkCoord coord) => new(blocks);
    }
}
