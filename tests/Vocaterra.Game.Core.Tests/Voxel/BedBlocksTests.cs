using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>Y03: the bed and the bedroll are blocks the crosshair finds and a body walks through, and nothing is placed into them.</summary>
public sealed class BedBlocksTests
{
    public static TheoryData<BlockId> BedKinds => [BlockId.Bed, BlockId.Bedroll, BlockId.BedFoot, BlockId.BedrollFoot];

    [Theory]
    [MemberData(nameof(BedKinds))]
    public void Given_ABedOrABedroll_When_ItIsRead_Then_ItIsNotSolidNotDecorNotLiquidAndGivesNothing(BlockId kind)
    {
        var definition = BlockDefinition.Get(kind);
        Assert.False(definition.Solid);
        Assert.False(definition.Decor); // the ray stops on it, and a block cannot be placed into its cell
        Assert.False(definition.Liquid);
        Assert.Equal(0, definition.GatherTicks);
        Assert.Null(definition.Drop);
        Assert.False(BlockDefinition.IsDecor(kind));
        Assert.False(BlockDefinition.IsSolid(kind));
    }

    [Theory]
    [MemberData(nameof(BedKinds))]
    public void Given_ABedOrABedrollInFrontOfThePlayer_When_TheBlockRayIsCast_Then_ItStopsOnIt(BlockId kind)
    {
        // The flat scene's eye is at layer 11; yaw 0 looks north, so the bed at (32, 11, 30) is entered through its south face.
        var sim = FlatSim(g => g.SetBlock(32, 11, 30, kind));
        sim.Step([VoxelCommand.Look(0, 0)]);
        var hit = sim.Target;
        Assert.NotNull(hit);
        Assert.Equal(new BlockPos(32, 11, 30), hit.Value.Block);
        Assert.Equal(kind, hit.Value.Id);
        Assert.Equal(BlockFace.South, hit.Value.Face);
    }

    [Theory]
    [MemberData(nameof(BedKinds))]
    public void Given_ABedOrABedrollOnTheFloor_When_ThePlayerWalksIntoIt_Then_ThePlayerPassesThroughIt(BlockId kind)
    {
        var sim = FlatSim(g =>
        {
            for (var z = 28; z <= 36; z++)
            {
                g.SetBlock(40, GroundTop + 1, z, kind);
            }
        });
        Run(sim, 600, VoxelCommand.Move(1, 0));
        Assert.True(sim.Player.CenterX > 41 * Units, "the body is beyond the cell it walked into");
        Assert.Equal(GroundTop + 1, sim.Player.FeetBlock.Y);
    }

    [Fact]
    public void Given_TheInnsCounterBlock_When_ABodyWalksIntoItAndJumps_Then_ItNeitherStepsNorJumpsOntoItWhileACrateIsStoodOn()
    {
        // Y08: the counter is one block high and a body collides with it as with a fence (two high); a crate beside it is a step.
        Assert.True(BlockDefinition.IsSolid(BlockId.Counter));
        Assert.True(BlockDefinition.IsTall(BlockId.Counter));
        Assert.NotEqual(BlockId.Table, BlockId.Counter); // not a workbench: no crafting panel opens beside it
        var withCounter = FlatSim(g =>
        {
            for (var z = 28; z <= 36; z++)
            {
                g.SetBlock(40, GroundTop + 1, z, BlockId.Counter);
            }
        });
        Run(withCounter, 300, VoxelCommand.Move(1, 0), VoxelCommand.Jump);
        Assert.True(withCounter.Player.CenterX < 40 * Units, "the body stays west of the counter");

        var withCrate = FlatSim(g =>
        {
            for (var z = 28; z <= 36; z++)
            {
                g.SetBlock(40, GroundTop + 1, z, BlockId.Crate);
            }
        });
        Run(withCrate, 300, VoxelCommand.Move(1, 0), VoxelCommand.Jump);
        Assert.True(withCrate.Player.CenterX > 40 * Units, "a crate is climbed onto");
    }
}
