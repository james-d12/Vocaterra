using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Land;

/// <summary>The stake (Y10): used on the ground it marks the pending plot round the aimed column, or says on the hint line why it cannot; the HUD line says the plot's size and price.</summary>
public sealed class StakeTests
{
    private static readonly (int X, int Z) Corner = LandTestScene.NorthPlot;

    // The middle column of the plot at the corner (the one the stake is used on).
    private static (int X, int Z) Middle => (Corner.X + 5, Corner.Z + 5);

    private static VoxelSession WithStake()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.Simulation.GrantItems(ItemId.Stake, 1);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Stake));
        return session;
    }

    private static BlockPos GroundAt(VoxelSession session, int x, int z) => new(x, LandRules.GroundOf(session.Scene.Grid, x, z).Y, z);

    [Fact]
    public void Given_AStakeAndTheMeadow_When_ItIsUsedOnTheGround_Then_ThePendingPlotIsSetRoundThatColumnAndSaved()
    {
        var session = WithStake();

        var result = session.UseStake(GroundAt(session, Middle.X, Middle.Z));

        Assert.Equal(VoxelSession.StakeResult.Marked, result);
        Assert.Equal(new Plot(Corner.X, Corner.Z, 10, 10), session.PendingPlot);
        Assert.Equal(Middle, session.PendingPlot!.Value.Centre); // the aimed column is the plot's middle
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Stake)); // the stake stays in the bag
        var text = SaveGame.Save(session, TestGame.Route(LandTestScene.Seed));
        Assert.Equal(new PlotSave(Corner.X, Corner.Z, 10, 10), SaveGame.Parse(text).Systems!.Land!.Pending);
        Assert.Contains(session.Locale.Ui("plotMarked", ("width", "10"), ("depth", "10"), ("price", "10")), session.TakeHints());
    }

    [Fact]
    public void Given_APendingPlot_When_TheStakeIsUsedInTheVillage_Then_ItIsRefusedWithTheReasonAndThePlotIsKept()
    {
        var session = WithStake();
        _ = session.UseStake(GroundAt(session, Middle.X, Middle.Z));
        _ = session.TakeHints();

        var result = session.UseStake(GroundAt(session, 285, 206));

        Assert.Equal(VoxelSession.StakeResult.Refused, result);
        Assert.Equal(new Plot(Corner.X, Corner.Z, 10, 10), session.PendingPlot);
        Assert.Equal(["You cannot mark land in the village."], session.TakeHints());
    }

    [Fact]
    public void Given_NoPlot_When_TheStakeIsUsedInTheVillage_Then_NoPlotIsPending()
    {
        var session = WithStake();

        Assert.Equal(VoxelSession.StakeResult.Refused, session.UseStake(GroundAt(session, 285, 206)));

        Assert.Null(session.PendingPlot);
        Assert.Null(SaveGame.Parse(SaveGame.Save(session, TestGame.Route(LandTestScene.Seed))).Systems!.Land);
    }

    [Fact]
    public void Given_APendingPlot_When_TheStakeIsUsedAgainElsewhereInTheMeadow_Then_ThePlotMovesAtNoCost()
    {
        var session = WithStake();
        _ = session.UseStake(GroundAt(session, Middle.X, Middle.Z));
        var (otherX, otherZ) = LandTestScene.OtherPlot(session);

        var result = session.UseStake(GroundAt(session, otherX + 5, otherZ + 5));

        Assert.Equal(VoxelSession.StakeResult.Marked, result);
        Assert.Equal(new Plot(otherX, otherZ, 10, 10), session.PendingPlot);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Stake));
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Coin));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    public void Given_NoStakeInTheBag_When_TheStakeIsUsed_Then_NothingHappens(int dx, int dz)
    {
        var session = TestGame.NewSession(LandTestScene.Seed);

        Assert.Equal(VoxelSession.StakeResult.NotApplied, session.UseStake(GroundAt(session, Middle.X + dx, Middle.Z + dz)));

        Assert.Null(session.PendingPlot);
        Assert.Empty(session.TakeHints());
    }

    [Fact]
    public void Given_AnOwnedPlot_When_TheStakeIsUsedInsideOrOutside_Then_NothingIsMarkedAndTheHintSaysWhy()
    {
        var session = WithStake();
        _ = session.SetPendingPlot(Corner.X, Corner.Z);
        Assert.True(session.OwnPlot());
        _ = session.TakeHints();

        Assert.Equal(VoxelSession.StakeResult.AlreadyOwned, session.UseStake(GroundAt(session, Middle.X, Middle.Z)));
        Assert.Equal(["This land is already yours."], session.TakeHints());
        var (otherX, otherZ) = LandTestScene.OtherPlot(session);
        Assert.Equal(VoxelSession.StakeResult.AlreadyOwned, session.UseStake(GroundAt(session, otherX + 5, otherZ + 5)));
        Assert.Equal(["You already have your land. The stake has nothing more to mark."], session.TakeHints());

        Assert.Null(session.PendingPlot);
        Assert.Equal(new Plot(Corner.X, Corner.Z, 10, 10), session.OwnedPlot);
    }

    [Fact]
    public void Given_EachReasonOfARefusal_When_TheStakeIsUsed_Then_EachHasItsOwnEnglishLine()
    {
        var session = WithStake();
        var lines = new List<string>();
        foreach (var check in new[]
        {
            new PlotCheck(PlotFault.Protected, Protection.Village), new PlotCheck(PlotFault.Protected, Protection.Camp), new PlotCheck(PlotFault.Protected, Protection.Road),
            new PlotCheck(PlotFault.OutsideMeadows), new PlotCheck(PlotFault.Water), new PlotCheck(PlotFault.Tree), new PlotCheck(PlotFault.NotGround), new PlotCheck(PlotFault.TooSteep),
        })
        {
            lines.Add(session.PlotRefusalLine(check));
        }

        Assert.Equal(lines.Count, lines.Distinct().Count());
        Assert.All(lines, line => Assert.EndsWith(".", line, StringComparison.Ordinal));
        Assert.Contains("only in the meadows", lines[3], StringComparison.Ordinal);
    }

    [Fact]
    public void Given_APendingPlotAndAnOwnedOne_When_TheHudLineIsAsked_Then_ItGivesSizeAndPriceOrYourLandNearItOrWithTheStake()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        Assert.Null(session.PlotHudLine());
        _ = session.SetPendingPlot(Corner.X, Corner.Z);
        Assert.Null(session.PlotHudLine()); // far from it, nothing in hand

        LandTestScene.StandBeside(session, Corner.X + 5, Corner.Z + 12);
        Assert.Equal("Plot 10 × 10 · price 10 coins", session.PlotHudLine());

        Assert.True(session.OwnPlot());
        Assert.Equal("Your land", session.PlotHudLine());
    }

    [Fact]
    public void Given_TheStakeInHand_When_TheHudLineIsAskedFarAway_Then_TheMarkedPlotIsStillDescribed()
    {
        var session = WithStake();
        _ = session.SetPendingPlot(Corner.X, Corner.Z);
        session.Simulation.TeleportTo(new BlockPos(285, LandRules.GroundOf(session.Scene.Grid, 285, 206).Y + 1, 206)); // the village, far from the meadow
        Assert.Null(session.PlotHudLine()); // the stake is in the bag, not in the hand

        var slot = session.Simulation.Inventory.Slots.ToList().FindIndex(static stack => !stack.IsEmpty && stack.Item == ItemId.Stake);
        Assert.InRange(slot, 0, Inventory.HotbarSize - 1);
        session.Step([VoxelCommand.SelectSlot(slot)]);

        Assert.Equal("Plot 10 × 10 · price 10 coins", session.PlotHudLine());
    }

    [Fact]
    public void TheRightClickWithTheStakeSelectedUsesItOnABlockAndNotInTheAir()
    {
        var click = new TestInputFrame { RightDown = true };
        var stake = new ItemStack(ItemId.Stake, 1);
        var grass = new BlockRayHit(new BlockPos(0, 0, 0), BlockId.Grass, BlockFace.Up, new BlockPos(0, 1, 0), 10);

        Assert.True(StakeUse.Applies(click, true, stake, grass));
        Assert.False(StakeUse.Applies(click, true, stake, null)); // in the air
        Assert.False(StakeUse.Applies(click, false, stake, grass)); // a talk or a panel holds the input
        Assert.False(StakeUse.Applies(new TestInputFrame(), true, stake, grass)); // no click
        Assert.False(StakeUse.Applies(click, true, new ItemStack(ItemId.Wood, 5), grass));
        Assert.False(StakeUse.Applies(click, true, ItemStack.Empty, grass));
    }

    [Fact]
    public void TheStakeIsAnItemOfOneThatPlacesNoBlock()
    {
        var definition = ItemDefinition.Get(ItemId.Stake);

        Assert.Equal("stake", definition.Name);
        Assert.Equal(1, definition.MaxStack);
        Assert.Null(BlockDefinition.PlacedBy(ItemId.Stake));
    }
}
