using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Component;

/// <summary>The guide in the 3D scene: a typed line on the bundle's route gets the nearest block of the noun, its direction and distance, and the identification of the block under the cursor.</summary>
public sealed class VoxelSessionTests
{
    private const int Unit = VoxelPhysics.UnitsPerBlock;

    private static Scene RouteScene() =>
        TestGame.Route();

    // The body at the spawn, as the simulation reports it: the middle of the feet block and the spawn's facing.
    private static GuideViewpoint AtSpawn(Scene scene) => new(
        scene.Spawn.Feet,
        (scene.Spawn.Feet.X * (long)Unit) + (Unit / 2),
        (scene.Spawn.Feet.Z * (long)Unit) + (Unit / 2),
        scene.Spawn.Yaw);

    [Fact]
    public void TheGuidePointsAtTheNearestRiverBlockFromWhereThePlayerFaces()
    {
        var scene = RouteScene();
        var session = new VoxelSession(TestGame.Bundle, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Nouns, scene);
        var turn = session.Say("¿dónde está el río?");

        Assert.True(turn.Communicated);
        Assert.Equal("ask_directions", turn.Intent);
        Assert.NotNull(turn.Pointer);
        Assert.Equal("río", turn.Pointer.Place);
        Assert.Equal(BlockId.Water, scene.Grid.GetBlock(turn.Pointer.Target.X, turn.Pointer.Target.Y, turn.Pointer.Target.Z));

        // The same answer as the facts give from the spawn: the nearest water block, its distance and its direction.
        var expected = WorldFacts.Live(scene.Grid, TestGame.Nouns).NearestOfNoun(AtSpawn(scene), "río");
        Assert.NotNull(expected);
        Assert.Equal(expected.Cell, turn.Pointer.Target);
        Assert.Equal(expected.Blocks, turn.Pointer.Blocks);
        Assert.Equal(expected.Direction, turn.Pointer.Direction);
        Assert.Contains(expected.Direction, turn.WorldLine, StringComparison.Ordinal);
        Assert.Contains(expected.BlocksInWords, turn.WorldLine, StringComparison.Ordinal);

        // The arrow is out, and it is the beacon's block.
        Assert.Equal(new PointerMarker("río", expected.Cell), session.Pointer);
    }

    [Fact]
    public void ThePlayerWhoAsksWhatTheCursorIsOnIsToldTheNounOfTheBlock()
    {
        var scene = RouteScene();
        var session = new VoxelSession(TestGame.Bundle, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Nouns, scene);
        var water = scene.Point("river");
        var turn = session.Say("¿qué es esto?", water);

        Assert.Equal("guide.identify", turn.HostEvents.Single(static e => e.StartsWith("guide.", StringComparison.Ordinal)));
        Assert.Equal(TestGame.Nouns.NounsOfBlock(scene.Grid.GetBlock(water.X, water.Y, water.Z)).First().Lemma, turn.IdentifiedNoun);
    }

    [Fact]
    public void ANamedRiverBlockIsAguaAndRioAndANameOfEitherIsRight()
    {
        var scene = RouteScene();
        var session = new VoxelSession(TestGame.Bundle, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Nouns, scene);
        var water = scene.Point("river");
        Assert.Equal(BlockId.Water, scene.Grid.GetBlock(water.X, water.Y, water.Z));

        var turn = session.Say("es un río", water);
        Assert.Equal(NameCheck.Right, turn.Naming);
        Assert.Equal("agua", turn.NameTarget);
    }
}
