using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Tests.Land;

/// <summary>The camp kit's use and the camp's blocks under the interact button (Y12): the right click, the gather target and the script step that sets the camp up.</summary>
public sealed class CampInputTests
{
    [Fact]
    public void TheRightClickWithTheCampKitSelectedUsesItOnABlockAndNotInTheAir()
    {
        var click = new TestInputFrame { RightDown = true };
        var kit = new ItemStack(ItemId.CampKit, 1);
        var grass = new BlockRayHit(new BlockPos(0, 0, 0), BlockId.Grass, BlockFace.Up, new BlockPos(0, 1, 0), 10);

        Assert.True(HeldItemUse.Applies(click, true, kit, grass, ItemId.CampKit));
        Assert.False(HeldItemUse.Applies(click, true, kit, null, ItemId.CampKit));
        Assert.False(HeldItemUse.Applies(click, false, kit, grass, ItemId.CampKit));
        Assert.False(HeldItemUse.Applies(new TestInputFrame(), true, kit, grass, ItemId.CampKit));
        Assert.False(HeldItemUse.Applies(click, true, new ItemStack(ItemId.Stake, 1), grass, ItemId.CampKit));
        Assert.False(HeldItemUse.Applies(click, true, ItemStack.Empty, grass, ItemId.CampKit));
    }

    [Fact]
    public void TheCampKitIsAnItemOfOneThatPlacesNoBlock()
    {
        Assert.Equal(1, TestItems.Catalogue.Get(ItemId.CampKit).MaxStack);
        Assert.Null(BlockDefinition.PlacedBy(ItemId.CampKit));
    }

    [Fact]
    public void Given_APlacedCamp_When_TheInteractTargetIsAFireRingStoneOrCanvas_Then_TheCampsRulesMakeItAGatherTargetAndNothingElseDoes()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        var stone = camp.RingStones[0];
        var ring = new BlockRayHit(stone, BlockId.FireRing, BlockFace.Up, new BlockPos(stone.X, stone.Y + 1, stone.Z), 3);
        var scene = new BlockRayHit(new BlockPos(0, 0, 0), BlockId.FireRing, BlockFace.Up, new BlockPos(0, 1, 0), 3);

        Assert.Null(LiveVoxelInput.GatherFor(ring)); // without the rules a ring stone is scenery
        Assert.Equal(stone, LiveVoxelInput.GatherFor(ring, session.Land));
        Assert.Null(LiveVoxelInput.GatherFor(scene, session.Land)); // Luna's ring and every other block outside the camp stay as they were
    }

    [Fact]
    public void Given_APlaceCampStep_When_ItIsParsed_Then_ItTakesAColumnAndASideAndTheScriptCallsTheHookOnce()
    {
        Assert.True(ScenarioScript.TryParseCamp("266,163,north", out var x, out var z, out var facing));
        Assert.Equal((266, 163, Direction.North), (x, z, facing));
        Assert.True(ScenarioScript.TryParseCamp("1, 2, West", out _, out _, out var west));
        Assert.Equal(Direction.West, west);
        Assert.False(ScenarioScript.TryParseCamp("266,163", out _, out _, out _));
        Assert.False(ScenarioScript.TryParseCamp("266,163,up", out _, out _, out _));
        Assert.False(ScenarioScript.TryParseCamp("266,163,3", out _, out _, out _)); // a number is not a side
        Assert.False(ScenarioScript.TryParseCamp("a,b,north", out _, out _, out _));
    }
}
