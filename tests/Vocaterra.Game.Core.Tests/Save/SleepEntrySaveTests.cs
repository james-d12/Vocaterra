using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>Save version 9 (8 added the sleep entry, Y02): the lodging given, the place last slept at and the places made in play survive a save, a load and a sleep; an older version is refused.</summary>
public sealed class SleepEntrySaveTests
{
    private const string OtherBundle = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static BlockPos Ring(VoxelSession session) => session.Scene.Point(VoxelSession.CampFireRing);

    private static void LightRing(VoxelSession session)
    {
        var ring = Ring(session);
        var middle = session.Fires.Middles.Single(m => m.X == ring.X && m.Z == ring.Z);
        session.Scene.Grid.SetBlock(middle.X + 1, middle.Y + 1, middle.Z, BlockId.Torch);
    }

    private static SleepPlace Bed(string id, BlockPos cell, string access = SleepPlaceSpec.AccessAlways) => new(id, null, cell, SleepPlaceSpec.KindBed, 2, 2, access);

    // A cell far from the ring, on the scene's points.
    private static BlockPos FarCell(VoxelSession session)
    {
        var ring = Ring(session);
        return session.Scene.Points.Values.OrderByDescending(p => (((long)p.X - ring.X) * ((long)p.X - ring.X)) + (((long)p.Z - ring.Z) * ((long)p.Z - ring.Z))).First();
    }

    private static VoxelSession Reload(VoxelSession session, bool migrate = false, string? bundle = null)
    {
        var document = SaveGame.Capture(session);
        if (bundle is not null)
        {
            document = document with { BundleHash = bundle };
        }

        return SaveGame.Load(SaveGame.Serialize(document), TestGame.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: false, migrate: migrate);
    }

    private static void Sleep(VoxelSession session, bool collapse = false) => SaveGame.Sleep(session, SaveGame.Capture(session), collapse);

    [Fact]
    public void GivenAGameWithNoLodgingNoLastPlaceAndNoPlaceMade_When_ItIsCaptured_Then_NoSleepEntryIsWritten()
    {
        var session = TestGame.NewSession();

        var document = SaveGame.Capture(session);

        Assert.Null(document.Systems!.Sleep);
        Assert.DoesNotContain("\"sleep\":{", SaveGame.Serialize(document), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenASaveWithoutTheEntry_When_ItLoads_Then_ThereIsNoLodgingNoLastPlaceAndNoPlaceMade()
    {
        var session = TestGame.NewSession();
        session.LodgingGiven = true;
        session.LastSleepPlace = new LastSleepSave("guide.fire");
        session.SleepPlaces.Add(Bed("camp.1", FarCell(session)));
        var document = SaveGame.Capture(session);

        var loaded = SaveGame.Load(SaveGame.Serialize(document with { Systems = document.Systems! with { Sleep = null } }), TestGame.Data, Pristine, bumpSession: false);

        Assert.False(loaded.LodgingGiven);
        Assert.Null(loaded.LastSleepPlace);
        Assert.Empty(loaded.SleepPlaces.MadeInPlay);
        Assert.Equal(["guide.fire", "inn.bed"], loaded.SleepPlaces.All.Select(static p => p.Id)); // the shipped places (Y04: the inn's bed too)
    }

    [Fact]
    public void GivenTheLodgingAlone_When_ItIsSavedAndLoaded_Then_ItComesBack()
    {
        var session = TestGame.NewSession();
        session.LodgingGiven = true;

        var loaded = Reload(session);

        Assert.Equal(new SleepSave(LodgingGiven: true), SaveGame.Capture(session).Systems!.Sleep);
        Assert.True(loaded.LodgingGiven);
        Assert.Null(loaded.LastSleepPlace);
        Assert.Empty(loaded.SleepPlaces.MadeInPlay);
    }

    [Fact]
    public void GivenALastAuthoredPlaceAlone_When_ItIsSavedAndLoaded_Then_ItsIdComesBackWithNoCell()
    {
        var session = TestGame.NewSession();
        session.LastSleepPlace = new LastSleepSave("guide.fire");

        var loaded = Reload(session);

        Assert.Equal(new LastSleepSave("guide.fire"), loaded.LastSleepPlace);
        Assert.False(loaded.LodgingGiven);
    }

    [Fact]
    public void GivenAPlaceMadeInPlayAlone_When_ItIsSavedAndLoaded_Then_ItIsInSleepPlacesAgainWithEverythingAddNeeds()
    {
        var session = TestGame.NewSession();
        var cell = FarCell(session);
        var made = new SleepPlace("camp.1", null, cell, SleepPlaceSpec.KindFire, 3, 4, SleepPlaceSpec.AccessLodging);
        session.SleepPlaces.Add(made);

        var loaded = Reload(session);

        Assert.Equal(made, Assert.Single(loaded.SleepPlaces.MadeInPlay));
        Assert.Equal(made, loaded.SleepPlaces.Find("camp.1"));
        Assert.Equal(3, loaded.SleepPlaces.All.Count); // the fire, the inn's bed and the place made
        Assert.Null(loaded.LastSleepPlace);
    }

    [Fact]
    public void GivenEveryPart_When_ItIsSavedAndLoadedTwice_Then_TheSaveIsTheSameAndThePartsComeBack()
    {
        var session = TestGame.NewSession();
        var cell = FarCell(session);
        session.LodgingGiven = true;
        session.SleepPlaces.Add(Bed("camp.1", cell));
        session.LastSleepPlace = new LastSleepSave("camp.1", cell.X, cell.Y, cell.Z);
        var first = System.Text.Json.JsonSerializer.Serialize(SaveGame.Capture(session).Systems!.Sleep);

        var loaded = Reload(session);

        Assert.Equal(first, System.Text.Json.JsonSerializer.Serialize(SaveGame.Capture(loaded).Systems!.Sleep));
        Assert.True(loaded.LodgingGiven);
        Assert.Equal(new LastSleepSave("camp.1", cell.X, cell.Y, cell.Z), loaded.LastSleepPlace);
        Assert.Equal("camp.1", Assert.Single(loaded.SleepPlaces.MadeInPlay).Id);
    }

    [Fact]
    public void GivenAPlayerAtTheLitFire_When_TheySleep_Then_TheFireIsTheLastPlaceAndTheNextSaveHoldsIt()
    {
        var session = TestGame.NewSession();
        LightRing(session);
        session.Simulation.TeleportTo(Ring(session));

        Sleep(session);

        Assert.Equal(new LastSleepSave("guide.fire"), session.LastSleepPlace);
        Assert.Equal(new LastSleepSave("guide.fire"), SaveGame.Capture(session).Systems!.Sleep!.Last);
    }

    [Fact]
    public void GivenTheFixtureWithThePlayerAtTheLitFire_When_ItLoadsAndTheyAreWokenBySleeping_Then_TheLastPlaceIsTheFire()
    {
        var text = SaveFixture.Read("save-v11.json");
        Assert.Null(SaveGame.Parse(text).Systems!.Sleep); // recorded before any sleep, with no lodging
        var loaded = SaveGame.Load(text, TestGame.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: false);
        Assert.True(loaded.AtSleepPlace);
        Assert.Null(loaded.LastSleepPlace);

        Sleep(loaded);

        Assert.Equal(new LastSleepSave("guide.fire"), loaded.LastSleepPlace);
    }

    [Fact]
    public void GivenTheLodgingAndAPlaceMade_When_ThePlayerSleeps_Then_BothAreStillThereAfterTheSleep()
    {
        var session = TestGame.NewSession();
        var cell = FarCell(session);
        session.LodgingGiven = true;
        session.SleepPlaces.Add(Bed("camp.1", cell));
        session.Simulation.TeleportTo(cell);

        Sleep(session);

        Assert.True(session.LodgingGiven);
        Assert.Equal(new LastSleepSave("camp.1", cell.X, cell.Y, cell.Z), session.LastSleepPlace);
        Assert.Equal("camp.1", Assert.Single(session.SleepPlaces.MadeInPlay).Id);
    }

    [Fact]
    public void GivenALitFireAndAPlayerAwayFromIt_When_TheAidSleeps_Then_TheLastPlaceIsAsItWas()
    {
        var session = TestGame.NewSession();
        LightRing(session);
        var ring = Ring(session);
        session.Simulation.TeleportTo(ring);
        Sleep(session);
        session.Simulation.TeleportTo(new BlockPos(ring.X + 40, ring.Y, ring.Z));
        Assert.False(session.AtSleepPlace);

        Sleep(session); // the --sleep aid: a usable place exists, the player is at none

        Assert.Equal(new LastSleepSave("guide.fire"), session.LastSleepPlace);
    }

    [Fact]
    public void GivenAPlayerAwayFromAnyPlaceWhoSleptNowhere_When_TheAidSleeps_Then_ThereIsStillNoLastPlace()
    {
        var session = TestGame.NewSession();
        LightRing(session);
        var ring = Ring(session);
        session.Simulation.TeleportTo(new BlockPos(ring.X + 40, ring.Y, ring.Z));

        Sleep(session);

        Assert.Null(session.LastSleepPlace);
    }

    [Fact]
    public void GivenAPlayerAtTheFire_When_TheyCollapse_Then_TheLastPlaceIsAsItWas()
    {
        var session = TestGame.NewSession();
        LightRing(session);
        session.Simulation.TeleportTo(Ring(session));

        Sleep(session, collapse: true);
        Assert.Null(session.LastSleepPlace);

        session.LastSleepPlace = new LastSleepSave("somewhere.else");
        Sleep(session, collapse: true);
        Assert.Equal(new LastSleepSave("somewhere.else"), session.LastSleepPlace);
    }

    [Fact]
    public void GivenASecondPlaceSleptAt_When_ThePlayerDies_Then_TheyWakeThereAndNotAtTheFire()
    {
        var session = TestGame.NewSession();
        var cell = FarCell(session);
        session.SleepPlaces.Add(Bed("camp.1", cell));
        session.Simulation.TeleportTo(cell);
        Sleep(session);

        Kill(session);

        Assert.Equal(1, session.DeathCount);
        var feet = session.Simulation.Player.FeetBlock;
        Assert.True(SleepPlaces.IsWithinReach(session.SleepPlaces.Find("camp.1")!, new BlockPos(feet.X, cell.Y, feet.Z)) || AcrossFrom(cell, feet) <= 5.01);
        Assert.True(AcrossFrom(cell, feet) < AcrossFrom(Ring(session), feet));
    }

    [Fact]
    public void GivenTheLastPlaceIsGone_When_ThePlayerDies_Then_TheyWakeAtTheAuthoredWakePlace()
    {
        var session = TestGame.NewSession();
        var cell = FarCell(session);
        session.SleepPlaces.Add(Bed("camp.1", cell));
        session.Simulation.TeleportTo(cell);
        Sleep(session);
        _ = session.SleepPlaces.Remove("camp.1");

        Kill(session);

        var feet = session.Simulation.Player.FeetBlock;
        Assert.InRange(AcrossFrom(Ring(session), feet), 2, 5);
    }

    [Fact]
    public void GivenNoSleepAtAll_When_ThePlayerDies_Then_TheyWakeAtTheAuthoredWakePlace()
    {
        var session = TestGame.NewSession();
        session.SleepPlaces.Add(Bed("camp.1", FarCell(session)));

        Kill(session);

        Assert.InRange(AcrossFrom(Ring(session), session.Simulation.Player.FeetBlock), 2, 5);
    }

    [Fact]
    public void GivenAVersion7Save_When_ItIsParsed_Then_ItIsRefusedWithItsVersionAndTheOldestThatLoads()
    {
        var error = Assert.Throws<SaveException>(() => SaveGame.Parse("{\"format\":\"vocaterra-save\",\"version\":7}"));

        Assert.Contains("version 7", error.Message, StringComparison.Ordinal);
        Assert.Contains("loads is 12", error.Message, StringComparison.Ordinal);
        Assert.Equal(12, SaveGame.OldestReadableVersion);
        Assert.Equal(12, SaveGame.CurrentVersion);
    }

    [Fact]
    public void GivenAVersion8Save_When_ItIsParsed_Then_ItIsRefusedWithItsVersionAndTheOldestThatLoads()
    {
        var error = Assert.Throws<SaveException>(() => SaveGame.Parse("{\"format\":\"vocaterra-save\",\"version\":8}"));

        Assert.Contains("version 8", error.Message, StringComparison.Ordinal);
        Assert.Contains("loads is 12", error.Message, StringComparison.Ordinal);
        Assert.Contains("not migrated", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenASaveOfTheCurrentVersionMadeIntoVersion8_When_ItLoads_Then_ItIsRefused()
    {
        var document = SaveGame.Capture(TestGame.NewSession()) with { Version = 8 };

        _ = Assert.Throws<SaveException>(() => SaveGame.Load(SaveGame.Serialize(document), TestGame.Data, Pristine));
    }

    [Fact]
    public void GivenASleepEntryOfAnotherBundle_When_ItIsMigrated_Then_TheLodgingAndPlacesMadeStayAndAGonePlaceIsLetGo()
    {
        var session = TestGame.NewSession();
        var cell = FarCell(session);
        session.LodgingGiven = true;
        session.SleepPlaces.Add(Bed("camp.1", cell));
        session.LastSleepPlace = new LastSleepSave("camp.1", cell.X, cell.Y, cell.Z);

        var kept = Reload(session, migrate: true, bundle: OtherBundle);
        Assert.True(kept.LodgingGiven);
        Assert.Equal("camp.1", kept.LastSleepPlace?.Id);
        Assert.Equal("camp.1", Assert.Single(kept.SleepPlaces.MadeInPlay).Id);

        session.SleepPlaces.Remove("camp.1");
        session.LastSleepPlace = new LastSleepSave("an-old-inn");
        session.SleepPlaces.Add(Bed("far.away", new BlockPos(100_000, 0, 0)));
        var dropped = Reload(session, migrate: true, bundle: OtherBundle);
        Assert.True(dropped.LodgingGiven);
        Assert.Null(dropped.LastSleepPlace);
        Assert.Empty(dropped.SleepPlaces.MadeInPlay);
        Assert.Contains("an-old-inn", dropped.Migration!.Find("place last slept at")!.Dropped);
    }

    [Fact]
    public void GivenAnAuthoredLastPlaceOfAnotherBundle_When_ItIsMigrated_Then_ItStaysWhileTheNewBundleHasIt()
    {
        var session = TestGame.NewSession();
        session.LastSleepPlace = new LastSleepSave("guide.fire");

        var loaded = Reload(session, migrate: true, bundle: OtherBundle);

        Assert.Equal(new LastSleepSave("guide.fire"), loaded.LastSleepPlace);
    }

    // The player dies of thirst, as DeathTests does it.
    private static void Kill(VoxelSession session)
    {
        session.SetNeed(NeedMeter.Thirst, 0);
        session.SetNeed(NeedMeter.Health, 0.25);
        for (var i = 0; i < 60; i++)
        {
            session.Step([]);
        }
    }

    private static double AcrossFrom(BlockPos a, BlockPos b) => Math.Sqrt((((long)a.X - b.X) * ((long)a.X - b.X)) + (((long)a.Z - b.Z) * ((long)a.Z - b.Z)));
}
