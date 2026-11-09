using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>Save version 7 (N01; version 6 until the water series met the needs): the <c>systems.needs</c> entry, full needs for every older save, the needs kept out of the world's digest, and a night passing them through.</summary>
public sealed class NeedsSaveTests
{
    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static VoxelSession Played()
    {
        var session = TestGame.NewSession();
        session.SetNeed(NeedMeter.Thirst, 40);
        session.SetNeed(NeedMeter.Hunger, 61.5);
        for (var i = 0; i < 1800; i++)
        {
            session.Step([]);
        }

        return session;
    }

    [Fact]
    public void TheSaveIsVersion8AndCarriesTheNeedsUnderSystems()
    {
        var session = Played();

        var document = SaveGame.Capture(session, Pristine());

        Assert.Equal(8, SaveGame.CurrentVersion);
        Assert.Equal(8, document.Version);
        Assert.Equal(session.Needs.Capture(), document.Systems?.Needs);
        Assert.Contains("\"systems\":{\"needs\":{", SaveGame.Serialize(document), StringComparison.Ordinal);
    }

    [Fact]
    public void ASaveRoundTripsTheNeedsExactly()
    {
        var session = Played();
        var text = SaveGame.Save(session, Pristine());

        var loaded = SaveGame.Load(text, TestGame.Data, Pristine);

        Assert.Equal(session.Needs.Capture(), loaded.Needs.Capture());
        Assert.Equal(SaveGame.Parse(text).Systems, SaveGame.Parse(SaveGame.Serialize(SaveGame.Parse(text))).Systems);
    }

    [Fact]
    public void ALoadRaisesNoNeedStageEventForWhatWasSaved()
    {
        var session = Played();
        var loaded = SaveGame.Load(SaveGame.Save(session, Pristine()), TestGame.Data, Pristine);

        for (var i = 0; i < 60; i++)
        {
            loaded.Step([]);
        }

        Assert.DoesNotContain(loaded.Events.OfType<Vocaterra.Game.Core.Event.WorldEvent>(), static e => e.Name == "need_stage");
    }

    [Fact]
    public void ASaveWithoutSystemsLoadsWithFullNeeds()
    {
        var session = Played();
        var document = SaveGame.Capture(session, Pristine()) with { Systems = null };

        var loaded = SaveGame.Load(SaveGame.Serialize(document), TestGame.Data, Pristine);

        Assert.Equal(new PlayerNeeds(TestGame.Data.Needs).Capture(), loaded.Needs.Capture());
        Assert.Equal(SaveGame.CurrentVersion, SaveGame.Parse(SaveGame.Save(loaded, Pristine())).Version);
    }

    [Theory]
    [InlineData("save-v1.json")]
    [InlineData("save-v4.json")]
    public void TheOldFixturesLoadWithFullNeeds(string fixture)
    {
        var text = File.ReadAllText(Repo.Path("tests", "Vocaterra.Game.Core.Tests", "Fixtures", fixture));
        Assert.Null(SaveGame.Parse(text).Systems);

        var loaded = SaveGame.Load(text, TestGame.Data, Pristine, migrate: true);

        Assert.Equal(new PlayerNeeds(TestGame.Data.Needs).Capture(), loaded.Needs.Capture());
    }

    [Fact]
    public void TheNeedsAreNotInTheWorldsDigest()
    {
        var session = TestGame.NewSession();
        var before = session.Simulation.ComputeStateDigest();

        session.SetNeed(NeedMeter.Hunger, 3);
        session.Needs.Set(NeedMeter.Health, 12);

        Assert.Equal(before, session.Simulation.ComputeStateDigest());
        Assert.Equal(before, SaveGame.Capture(session, Pristine()).StateDigest);
    }

    [Fact]
    public void ALoadClampsAValueOutOfRangeAndRefusesANonNumber()
    {
        var session = TestGame.NewSession();
        var document = SaveGame.Capture(session, Pristine());

        var clamped = SaveGame.Restore(document with { Systems = new SystemsSave(new NeedsSave(150, -5, 50, 1000, 70)) }, TestGame.Data, Pristine());
        Assert.Equal(100, clamped.Needs.Value(NeedMeter.Hunger));
        Assert.Equal(0, clamped.Needs.Value(NeedMeter.Thirst));
        Assert.Equal(50, clamped.Needs.Value(NeedMeter.Stamina)); // held to its maximum: thirst at 0 takes 50 off it

        _ = Assert.Throws<SaveException>(() => SaveGame.Restore(document with { Systems = new SystemsSave(new NeedsSave(double.NaN, 1, 1, 1, 1)) }, TestGame.Data, Pristine()));
    }

    [Fact]
    public void ANightIsALoadInTheSameRunWithTheNightsChangesOnTheNeeds()
    {
        var session = SleepTests.EndedFirstSession();
        session.SetNeed(NeedMeter.Thirst, 33);
        session.SetNeed(NeedMeter.Stamina, 40);
        var before = session.Needs.Capture();

        SaveGame.Sleep(session, SaveGame.Capture(session, Pristine()));

        var rule = new PlayerNeeds(TestGame.Data.Needs); // N03: the needs come back as a load's do, then the night's rule is applied
        rule.Restore(before);
        rule.Sleep();
        Assert.Equal(rule.Capture(), session.Needs.Capture());
    }
}
