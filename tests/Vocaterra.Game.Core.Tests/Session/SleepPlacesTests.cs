using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>The places the player can sleep at (Y01): the shipped mountain fire, a fire usable while a lit ring stands at it, a bed by its access, and the places made while the game runs.</summary>
public sealed class SleepPlacesTests
{
    private static BlockPos Ring(VoxelSession session) => session.Scene.Point(VoxelSession.CampFireRing);

    // A torch on the rim of the player's own ring, as the game puts it.
    private static void LightRing(VoxelSession session)
    {
        var ring = Ring(session);
        var middle = session.Fires.Middles.Single(m => m.X == ring.X && m.Z == ring.Z);
        session.Scene.Grid.SetBlock(middle.X + 1, middle.Y + 1, middle.Z, BlockId.Torch);
    }

    private static SleepPlace Bed(string id, string access, BlockPos cell) => new(id, null, cell, SleepPlaceSpec.KindBed, 2, 2, access);

    [Fact]
    public void GivenTheShippedBundle_WhenTheSessionStarts_ThenTheMountainFireIsTheFirstPlaceAndWakingNamesIt()
    {
        var session = TestGame.NewSession();

        Assert.Equal(["guide.fire", "inn.bed"], session.SleepPlaces.All.Select(static p => p.Id));
        var place = session.SleepPlaces.All[0];
        Assert.Equal(("guide.fire", VoxelSession.CampFireRing, SleepPlaceSpec.KindFire, 5, 6), (place.Id, place.Point, place.Kind, place.RadiusBlocks, place.HeightBlocks));
        Assert.Equal(Ring(session), place.Cell);
        Assert.Same(place, session.SleepPlaces.WakeAt);
    }

    [Fact]
    public void GivenAColdRing_When_ItsTorchGoesOnTheRim_Then_TheFireBecomesUsableAndItGoesOutWithTheTorch()
    {
        var session = TestGame.NewSession();
        var place = session.SleepPlaces.All[0];
        Assert.False(session.SleepPlaces.IsUsable(place));
        Assert.False(session.SleepPlaces.AnyUsable);
        Assert.False(session.CanSleep);

        var ring = Ring(session);
        var middle = session.Fires.Middles.Single(m => m.X == ring.X && m.Z == ring.Z);
        session.Scene.Grid.SetBlock(middle.X + 1, middle.Y + 1, middle.Z, BlockId.Torch);

        Assert.True(session.SleepPlaces.IsUsable(place));
        Assert.True(session.SleepPlaces.AnyUsable);
        Assert.True(session.CanSleep); // the --sleep aid, which checks no position

        session.Scene.Grid.SetBlock(middle.X + 1, middle.Y + 1, middle.Z, BlockId.Air);
        Assert.False(session.SleepPlaces.AnyUsable); // read from the world at the time of asking
    }

    [Fact]
    public void GivenALitFire_When_ThePlayerStandsNearAndFar_Then_OnlyNearIsAtASleepPlace()
    {
        var session = TestGame.NewSession();
        LightRing(session);
        var ring = Ring(session);

        session.Simulation.TeleportTo(new BlockPos(ring.X + 5, ring.Y, ring.Z));
        Assert.True(session.AtSleepPlace);
        Assert.Equal("guide.fire", session.CurrentSleepPlace?.Id);

        session.Simulation.TeleportTo(new BlockPos(ring.X + 6, ring.Y, ring.Z));
        Assert.False(session.AtSleepPlace); // the reach across is five blocks

        session.Simulation.TeleportTo(new BlockPos(ring.X, ring.Y + 7, ring.Z));
        Assert.False(session.AtSleepPlace); // and six in height
    }

    [Fact]
    public void GivenAColdFire_When_ThePlayerStandsAtIt_Then_ItIsNoSleepPlace()
    {
        var session = TestGame.NewSession();
        session.Simulation.TeleportTo(Ring(session));

        Assert.False(session.AtSleepPlace);
        Assert.Null(session.SleepPlaces.UsableAt(session.Simulation.Player.FeetBlock));
    }

    [Fact]
    public void GivenABedWithEachAccess_When_LodgingIsGiven_Then_OnlyTheLodgingBedWaitsForIt()
    {
        var session = TestGame.NewSession();
        var cell = new BlockPos(10, 10, 10);
        session.SleepPlaces.Add(Bed("bed.free", SleepPlaceSpec.AccessAlways, cell));
        session.SleepPlaces.Add(Bed("bed.inn", SleepPlaceSpec.AccessLodging, new BlockPos(30, 10, 30)));

        Assert.True(session.SleepPlaces.IsUsable(session.SleepPlaces.Find("bed.free")!));
        Assert.False(session.SleepPlaces.IsUsable(session.SleepPlaces.Find("bed.inn")!));
        Assert.False(session.LodgingGiven);

        session.LodgingGiven = true;
        Assert.True(session.SleepPlaces.IsUsable(session.SleepPlaces.Find("bed.inn")!));
    }

    [Fact]
    public void GivenAPlaceMadeInPlay_When_ItIsAddedAndRemoved_Then_TheQuestionsFollow()
    {
        var session = TestGame.NewSession();
        var cell = new BlockPos(10, 10, 10);
        Assert.False(session.SleepPlaces.AnyUsable);

        session.SleepPlaces.Add(Bed("camp.made", SleepPlaceSpec.AccessAlways, cell));
        Assert.True(session.CanSleep);
        Assert.Null(session.SleepPlaces.UsableAt(new BlockPos(20, 10, 10)));
        Assert.Equal("camp.made", session.SleepPlaces.UsableAt(new BlockPos(11, 10, 11))?.Id);
        Assert.Equal("camp.made", session.SleepPlaces.NearestUsable(new BlockPos(90, 10, 90))?.Id);

        Assert.True(session.SleepPlaces.Remove("camp.made"));
        Assert.False(session.SleepPlaces.Remove("camp.made"));
        Assert.False(session.CanSleep);
    }

    [Fact]
    public void GivenSeveralUsablePlaces_When_TheArrowAsks_Then_TheNearestIsChosen()
    {
        var session = TestGame.NewSession();
        session.SleepPlaces.Add(Bed("far", SleepPlaceSpec.AccessAlways, new BlockPos(100, 10, 100)));
        session.SleepPlaces.Add(Bed("near", SleepPlaceSpec.AccessAlways, new BlockPos(12, 10, 10)));

        Assert.Equal("near", session.SleepPlaces.NearestUsable(new BlockPos(10, 10, 10))?.Id);
        Assert.Equal("far", session.SleepPlaces.NearestUsable(new BlockPos(98, 10, 100))?.Id);
    }

    [Fact]
    public void GivenAPlaceWhosePointTheSceneLacks_When_PlacesAreResolved_Then_ItIsLeftOut()
    {
        var scene = TestGame.Route();
        var specs = new[]
        {
            new SleepPlaceSpec("guide.fire", VoxelSession.CampFireRing, SleepPlaceSpec.KindFire, 5, 6, SleepPlaceSpec.AccessAlways),
            new SleepPlaceSpec("ghost.bed", "nowhere.at.all", SleepPlaceSpec.KindBed, 2, 2, SleepPlaceSpec.AccessAlways),
        };

        var places = new SleepPlaces(specs, "ghost.bed", scene, new FireRings(scene.Grid), () => true);

        Assert.Equal(["guide.fire"], places.All.Select(static p => p.Id).ToArray());
        Assert.Null(places.WakeAt);
    }

    [Fact]
    public void GivenALitFire_When_ThePlayerIsAwayFromIt_Then_TheCardOffersNoSleepButTheAidStillSleeps()
    {
        var session = TestGame.NewSession();
        LightRing(session);
        var ring = Ring(session);
        session.Simulation.TeleportTo(new BlockPos(ring.X + 30, ring.Y, ring.Z));

        Assert.True(session.CanSleep);
        Assert.False(session.AtSleepPlace);
        Assert.False(SleepOffer.AtFire(session, free: true));
    }
}
