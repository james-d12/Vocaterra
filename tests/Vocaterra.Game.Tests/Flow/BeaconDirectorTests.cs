using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Tests.Flow;

/// <summary>Where the beacon stands: the guide's camp for a new game, a requested point or none, and nothing once the player is there.</summary>
public sealed class BeaconDirectorTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    [Fact]
    public void ANewGameStartsWithTheBeaconOnTheGuidesCampAndALoadedOneWithout()
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
        var lines = new List<string>();
        var fresh = new BeaconDirector(session, lines.Add, lines.Add);
        fresh.PlaceAtStart(session.Scene, requested: null, loaded: false);
        Assert.Equal(session.Scene.Point(BeaconDirector.StartPoint), fresh.Beacon.Target);
        Assert.StartsWith($"beacon: {BeaconDirector.StartPoint} at ", Assert.Single(lines));

        var loaded = new BeaconDirector(session, lines.Add, lines.Add);
        loaded.PlaceAtStart(session.Scene, requested: null, loaded: true);
        Assert.False(loaded.Beacon.IsSet);
    }

    [Fact]
    public void ARequestedPointIsUsedNoneMeansNoBeaconAndAnUnknownPointIsReported()
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
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
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
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
    }
}
