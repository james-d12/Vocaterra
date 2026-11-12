using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Land;

/// <summary>The session's land (Y09): marking a pending plot, owning it, and asking whether a column is owned. What Y10 and Y11 call.</summary>
public sealed class LandSessionTests
{
    private static readonly (int X, int Z) Plot = LandTestScene.NorthPlot;

    [Fact]
    public void Given_ANewGame_When_TheLandIsAsked_Then_NoPlotIsPendingOrOwned()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);

        Assert.Null(session.PendingPlot);
        Assert.Null(session.OwnedPlot);
        Assert.False(session.IsOwned(Plot.X + 1, Plot.Z + 1));
        Assert.Same(session.Land, session.Simulation.BuildRules);
    }

    [Fact]
    public void Given_AValidSpot_When_ThePendingPlotIsSet_Then_TheAnswerIsYesAndThePlotIsPending()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);

        var check = session.SetPendingPlot(Plot.X, Plot.Z);

        Assert.True(check.Ok);
        Assert.Equal(new Plot(Plot.X, Plot.Z, 10, 10), session.PendingPlot);
        Assert.Null(session.OwnedPlot);
        Assert.False(session.IsOwned(Plot.X + 1, Plot.Z + 1)); // marked, not bought
    }

    [Fact]
    public void Given_APendingPlot_When_ABadSpotIsSet_Then_TheReasonIsGivenAndThePendingPlotIsKept()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.SetPendingPlot(Plot.X, Plot.Z);

        var check = session.SetPendingPlot(285, 210);

        Assert.Equal(new PlotCheck(PlotFault.Protected, Protection.Village), check);
        Assert.Equal(new Plot(Plot.X, Plot.Z, 10, 10), session.PendingPlot);
    }

    [Fact]
    public void Given_APendingPlot_When_AnotherValidSpotIsSet_Then_ItReplacesTheFirstAtNoCost()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.SetPendingPlot(Plot.X, Plot.Z);

        Assert.True(session.SetPendingPlot(Plot.X + 2, Plot.Z + 2).Ok);

        Assert.Equal(new Plot(Plot.X + 2, Plot.Z + 2, 10, 10), session.PendingPlot);
    }

    [Fact]
    public void Given_APendingPlot_When_ItIsOwned_Then_ItsColumnsAreOwnedAndNothingIsPending()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.SetPendingPlot(Plot.X, Plot.Z);

        Assert.True(session.OwnPlot());

        Assert.Equal(new Plot(Plot.X, Plot.Z, 10, 10), session.OwnedPlot);
        Assert.Null(session.PendingPlot);
        Assert.True(session.IsOwned(Plot.X, Plot.Z));
        Assert.True(session.IsOwned(Plot.X + 9, Plot.Z + 9));
        Assert.False(session.IsOwned(Plot.X + 10, Plot.Z));
        Assert.False(session.IsOwned(Plot.X - 1, Plot.Z));
        Assert.False(session.IsOwned(Plot.X, Plot.Z + 10));
    }

    [Fact]
    public void Given_NoPendingPlot_When_ItIsOwned_Then_NothingChanges()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);

        Assert.False(session.OwnPlot());

        Assert.Null(session.OwnedPlot);
    }

    [Fact]
    public void Given_AnOwnedPlot_When_AnotherIsMarkedAndOwned_Then_TheFirstStaysTheOnlyOne()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.SetPendingPlot(Plot.X, Plot.Z);
        Assert.True(session.OwnPlot());

        var (otherX, otherZ) = LandTestScene.OtherPlot(session);
        Assert.True(session.SetPendingPlot(otherX, otherZ).Ok);
        Assert.False(session.OwnPlot());

        Assert.Equal(new Plot(Plot.X, Plot.Z, 10, 10), session.OwnedPlot);
        Assert.NotNull(session.PendingPlot);
    }

    [Fact]
    public void Given_APendingPlotThatTheWorldNowRefuses_When_ItIsOwned_Then_ItIsNotOwned()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.SetPendingPlot(Plot.X, Plot.Z);
        var top = LandRules.GroundOf(session.Scene.Grid, Plot.X + 2, Plot.Z + 2).Y;
        _ = session.Scene.Grid.SetBlock(Plot.X + 2, top + 1, Plot.Z + 2, Vocaterra.Game.Core.Voxel.BlockId.Planks);

        Assert.False(session.OwnPlot());

        Assert.Null(session.OwnedPlot);
        Assert.NotNull(session.PendingPlot);
    }

    [Fact]
    public void Given_APendingPlot_When_TheMarkIsCleared_Then_NoPlotIsPending()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.SetPendingPlot(Plot.X, Plot.Z);

        session.ClearPendingPlot();

        Assert.Null(session.PendingPlot);
    }
}
