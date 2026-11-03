using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Navigation;

/// <summary>
/// Where the beacon stands: the guide's camp for a new game, a requested point or none, and nothing once the player is there; the arrival at a beacon the director
/// placed is logged with its point's name (village-plan V02), the arrival at the session's own arrow by the session.
/// </summary>
public sealed class BeaconDirectorTests
{
    [Fact]
    public void ANewGameStartsWithTheBeaconOnTheGuidesCampAndALoadedOneWithout()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var lines = new List<string>();
        var fresh = new BeaconDirector(session, lines.Add, lines.Add);
        fresh.PlaceAtStart(session.Scene, requested: null, loaded: false);
        Assert.Equal(session.Scene.Point(fresh.StartPoint), fresh.Beacon.Target);
        Assert.StartsWith($"beacon: {fresh.StartPoint} at ", Assert.Single(lines));

        var loaded = new BeaconDirector(session, lines.Add, lines.Add);
        loaded.PlaceAtStart(session.Scene, requested: null, loaded: true);
        Assert.False(loaded.Beacon.IsSet);
    }

    [Fact]
    public void ARequestedPointIsUsedNoneMeansNoBeaconAndAnUnknownPointIsReported()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var lines = new List<string>();
        var errors = new List<string>();
        var river = new BeaconDirector(session, lines.Add, errors.Add);
        river.PlaceAtStart(session.Scene, "river", loaded: false);
        Assert.Equal(session.Scene.Point("river"), river.Beacon.Target);

        var none = new BeaconDirector(session, lines.Add, errors.Add);
        none.PlaceAtStart(session.Scene, "none", loaded: false);
        Assert.False(none.Beacon.IsSet);

        var unknown = new BeaconDirector(session, lines.Add, errors.Add);
        unknown.PlaceAtStart(session.Scene, "nowhere", loaded: false);
        Assert.False(unknown.Beacon.IsSet);
        Assert.StartsWith("beacon: the scene has no point 'nowhere'", Assert.Single(errors));
    }

    [Fact]
    public void TheBeaconComesDownWhenThePlayerStandsNearIt()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var lines = new List<string>();
        var director = new BeaconDirector(session, lines.Add, lines.Add);
        var body = session.Simulation.Player;
        var under = new BlockPos(body.CenterX / VoxelPhysics.UnitsPerBlock, body.FeetY / VoxelPhysics.UnitsPerBlock - 1, body.CenterZ / VoxelPhysics.UnitsPerBlock);
        director.Beacon.Set(new BlockPos(under.X + 20, under.Y, under.Z), "lejos");
        director.ClearIfReached(session.Simulation);
        Assert.True(director.Beacon.IsSet);
        Assert.Empty(lines);

        director.Beacon.Set(under, "aquí");
        director.ClearIfReached(session.Simulation);
        Assert.False(director.Beacon.IsSet);
        Assert.Equal($"beacon: reached aquí at tick {session.Simulation.Tick}", Assert.Single(lines));
        Assert.Equal("aquí", Assert.Single(Reached(session)).Detail); // a block with no point's name is logged with its word
    }

    [Fact]
    public void TheArrivalAtTheStartBeaconIsLoggedWithItsPointsName()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var director = new BeaconDirector(session, static _ => { }, static _ => { });
        director.PlaceAtStart(session.Scene, requested: null, loaded: false);
        Assert.Equal("camp.guide", director.Beacon.Name);

        var camp = session.Scene.Point("camp.guide");
        session.Simulation.TeleportTo(new BlockPos(camp.X, camp.Y + 1, camp.Z)); // the feet on the ground block of the camp's point
        director.ClearIfReached(session.Simulation);

        Assert.False(director.Beacon.IsSet);
        var reached = Assert.Single(Reached(session));
        Assert.Null(reached.Npc);
        Assert.Equal("camp.guide", reached.Detail);
    }

    [Fact]
    public void TheArrivalAtTheSessionsArrowIsLeftToTheSession()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var director = new BeaconDirector(session, static _ => { }, static _ => { });
        var feet = session.Simulation.Player.FeetBlock;
        session.RestorePointer(new PointerMarker("aquí", new GuideCell(feet.X, feet.Y - 1, feet.Z), Point: "here"));
        director.Sync();
        Assert.Equal("here", director.Beacon.Name);

        director.ClearIfReached(session.Simulation); // the beacon comes down, but the arrow is the session's: its Step logs the arrival
        Assert.Empty(Reached(session));

        session.Step([]);
        Assert.Equal("here", Assert.Single(Reached(session)).Detail);
    }

    private static IEnumerable<WorldEvent> Reached(VoxelSession session) => session.Events.OfType<WorldEvent>().Where(static e => e.Name == WorldEvents.BeaconReached);
}
