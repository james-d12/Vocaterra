using System.Text.Json;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Land;

/// <summary>The land in the save (Y09, <c>systems.land</c>): written only when a plot exists, read back on a load and on a sleep, with no change of the save version.</summary>
public sealed class LandSaveTests
{
    private static readonly (int X, int Z) Plot = LandTestScene.NorthPlot;

    private static Scene Pristine(ulong seed = LandTestScene.Seed) => TestGame.Route(seed);

    private static string SaveOf(VoxelSession session) => SaveGame.Save(session, Pristine());

    private static VoxelSession LoadOf(string text) => SaveGame.Load(text, TestGame.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: false);

    [Fact]
    public void Given_NoPlot_When_TheGameIsSaved_Then_NoLandEntryIsWritten()
    {
        var text = SaveOf(TestGame.NewSession(LandTestScene.Seed));

        Assert.DoesNotContain("\"land\"", text, StringComparison.Ordinal);
        Assert.Null(SaveGame.Parse(text).Systems!.Land);
    }

    [Fact]
    public void Given_APendingPlotOnly_When_TheGameIsSaved_Then_OnlyThePendingPartIsWrittenAndALoadRestoresIt()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.SetPendingPlot(Plot.X, Plot.Z);

        var text = SaveOf(session);
        var loaded = LoadOf(text);

        using (var json = JsonDocument.Parse(text))
        {
            var land = json.RootElement.GetProperty("systems").GetProperty("land");
            Assert.False(land.TryGetProperty("owned", out _));
            var pending = land.GetProperty("pending");
            Assert.Equal((Plot.X, Plot.Z, 10, 10), (pending.GetProperty("x").GetInt32(), pending.GetProperty("z").GetInt32(), pending.GetProperty("width").GetInt32(), pending.GetProperty("depth").GetInt32()));
        }

        Assert.Equal(new Plot(Plot.X, Plot.Z, 10, 10), loaded.PendingPlot);
        Assert.Null(loaded.OwnedPlot);
    }

    [Fact]
    public void Given_APendingAndAnOwnedPlot_When_TheGameIsSavedAndLoaded_Then_BothComeBackAndTheRulesStillApply()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.SetPendingPlot(Plot.X, Plot.Z);
        Assert.True(session.OwnPlot());
        var (otherX, otherZ) = LandTestScene.OtherPlot(session);
        Assert.True(session.SetPendingPlot(otherX, otherZ).Ok);

        var text = SaveOf(session);
        var loaded = LoadOf(text);

        Assert.Equal(new Plot(Plot.X, Plot.Z, 10, 10), loaded.OwnedPlot);
        Assert.Equal(new Plot(otherX, otherZ, 10, 10), loaded.PendingPlot);
        Assert.True(loaded.IsOwned(Plot.X + 4, Plot.Z + 4));
        Assert.True(loaded.Land.MayTakeBack(new Vocaterra.Game.Core.Voxel.BlockPos(Plot.X + 4, 40, Plot.Z + 4)));
        Assert.Equal(Protection.Village, loaded.Land.Rules.ProtectionAt(292, 218)); // the mask is rebuilt from the scene, not saved
        Assert.Equal(SaveGame.Parse(text).Systems!.Land, SaveGame.Parse(SaveOf(loaded)).Systems!.Land);
    }

    [Fact]
    public void Given_AnOwnedPlot_When_ThePlayerSleeps_Then_TheLandIsAsItWas()
    {
        var session = Save.SleepTests.EndedFirstSession();
        _ = session.SetPendingPlot(Plot.X, Plot.Z);
        Assert.True(session.OwnPlot());

        SaveGame.Sleep(session, SaveGame.Capture(session, Pristine()));

        Assert.Equal(new Plot(Plot.X, Plot.Z, 10, 10), session.OwnedPlot);
        Assert.Null(session.PendingPlot);
    }

    [Fact]
    public void Given_APlotOutsideTheWorld_When_TheSaveIsLoaded_Then_ItIsRefused()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.SetPendingPlot(Plot.X, Plot.Z);
        _ = session.OwnPlot();
        var document = SaveGame.Capture(session, Pristine());
        var broken = document with { Systems = document.Systems! with { Land = new LandSave(null, new PlotSave(-4, 10, 10, 10)) } };

        _ = Assert.Throws<SaveException>(() => SaveGame.Load(SaveGame.Serialize(broken), TestGame.Data, Pristine, credit: CreditRules.FullTyped));
    }

    [Fact]
    public void Given_TheSaveVersion_When_TheLandEntryExists_Then_ItIsNine()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.SetPendingPlot(Plot.X, Plot.Z);

        Assert.Equal(9, SaveGame.Parse(SaveOf(session)).Version);
        Assert.Equal(9, SaveGame.CurrentVersion);
    }
}
