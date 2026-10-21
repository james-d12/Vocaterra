using Vocaterra.Game;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Generation;
using Vocaterra.Simulation.Physics;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game.Tests;

/// <summary>Where the guide stands, when the player is near her, and the arrow's life in the session.</summary>
public sealed class GuideNpcAndSessionTests
{
    private static readonly WorldSize MediumSize = new(512, 256);

    [Fact]
    public void TheGuideStandsOnGrassWithHeadroomWithinWalkingDistanceOfTheSpawn()
    {
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var world = WorldGenerator.Generate(seed, MediumSize);
            var guide = GuideNpc.Place(world);

            var distance = Math.Abs(guide.Feet.X - world.Spawn.X);
            Assert.InRange(distance, GuideNpc.MinDistanceFromSpawn, GuideNpc.MaxDistanceFromSpawn);
            Assert.Equal(TileId.Grass, world.GetTile(guide.Feet.X, guide.Feet.Y + 1));
            for (var dy = 0; dy < 3; dy++)
            {
                Assert.Equal(TileId.Air, world.GetTile(guide.Feet.X, guide.Feet.Y - dy));
            }
        }
    }

    [Fact]
    public void TheGuideKeepsAwayFromTreesSoTheNearestTreeIsAWalkAway()
    {
        var clear = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var world = WorldGenerator.Generate(seed, MediumSize);
            var guide = GuideNpc.Place(world);
            if (world.Record.TreeBases.All(b => Math.Abs(b.X - guide.Feet.X) >= GuideNpc.TreeClearance))
            {
                clear++;
            }
        }

        Assert.True(clear >= 55, $"Only {clear} of 60 worlds put the guide clear of trees.");
    }

    [Fact]
    public void ThePlacementIsAPureFunctionOfTheWorld()
    {
        var a = GuideNpc.Place(WorldGenerator.Generate(42));
        var b = GuideNpc.Place(WorldGenerator.Generate(42));

        Assert.Equal(a, b);
        Assert.NotEqual(a, GuideNpc.Place(WorldGenerator.Generate(43)));
    }

    [Fact]
    public void TheGuideIsNearOnlyWithinFiveColumnsAndFourRows()
    {
        var world = WorldGenerator.Generate(42);
        var guide = GuideNpc.Place(world);

        PlayerState At(int dx, int dy) => new(
            ((guide.Feet.X + dx) * PhysicsConstants.UnitsPerTile) + (PhysicsConstants.UnitsPerTile / 2) - (PhysicsConstants.PlayerWidth / 2),
            ((guide.Feet.Y + 1 + dy) * PhysicsConstants.UnitsPerTile) - PhysicsConstants.PlayerHeight,
            0,
            0,
            true,
            1);

        Assert.True(guide.IsNear(At(0, 0)));
        Assert.True(guide.IsNear(At(GuideNpc.NearColumns, 0)));
        Assert.True(guide.IsNear(At(-GuideNpc.NearColumns, 0)));
        Assert.False(guide.IsNear(At(GuideNpc.NearColumns + 1, 0)));
        Assert.False(guide.IsNear(At(-(GuideNpc.NearColumns + 1), 0)));
        Assert.False(guide.IsNear(At(0, -12)));
    }

    [Fact]
    public void ThePlayerStartsAWalkAwayFromTheGuideAndGetsNearByWalking()
    {
        var session = TestGame.NewSession();
        Assert.False(session.IsNearGuide);

        var direction = session.PlayerTile.X < session.Guide.Feet.X ? PlayerCommand.MoveRight : PlayerCommand.MoveLeft;
        var ticks = 0;
        while (!session.IsNearGuide && ticks < 600)
        {
            session.Step([direction]);
            ticks++;
        }

        Assert.True(session.IsNearGuide, "Walking toward the guide should bring the player near.");
    }

    [Fact]
    public void TheArrowStaysUntilThePlayerReachesTheTargetAndThenGoes()
    {
        var session = TestGame.NewSession();
        var turn = session.Say("¿dónde está el árbol?");
        Assert.NotNull(session.Pointer);
        var target = turn.Pointer!.Target;

        session.Step([]);
        Assert.NotNull(session.Pointer); // standing still: still pointing

        var direction = target.X > session.PlayerTile.X ? PlayerCommand.MoveRight : PlayerCommand.MoveLeft;
        var ticks = 0;
        while (session.Pointer is not null && ticks < 900)
        {
            Assert.True(
                Math.Abs(session.PlayerTile.X - target.X) > GameSession.ReachTiles || Math.Abs(session.PlayerTile.Y - target.Y) > GameSession.ReachTiles,
                "The arrow should have gone as soon as the player was within reach.");
            session.Step([direction]);
            ticks++;
        }

        Assert.Null(session.Pointer);
        Assert.InRange(Math.Abs(session.PlayerTile.X - target.X), 0, GameSession.ReachTiles);
    }

    [Fact]
    public void TheArrowCanBeDismissedAndANewQuestionReplacesIt()
    {
        var session = TestGame.NewSession();
        session.Say("¿dónde está el árbol?");
        var tree = session.Pointer;
        Assert.NotNull(tree);

        session.DismissPointer();
        Assert.Null(session.Pointer);

        session.Say("dónde está el árbol");
        session.Say("dónde hay piedra");
        Assert.Equal("piedra", session.Pointer!.Place);
        Assert.NotEqual(tree.Target, session.Pointer.Target);
    }

    [Fact]
    public void ALineTheGuideDoesNotUnderstandLeavesTheArrowAlone()
    {
        var session = TestGame.NewSession();
        session.Say("¿dónde está el árbol?");
        var pointer = session.Pointer;

        session.Say("blah blah");
        session.Say("gracias");

        Assert.Equal(pointer, session.Pointer);
    }

    [Fact]
    public void TheSessionNeverEvaluatesInsideAStep()
    {
        var session = TestGame.NewSession();
        session.Say("¿dónde está el árbol?");
        var counter = session.Conversation.GuideState.InteractionCounter;

        for (var i = 0; i < 200; i++)
        {
            session.Step([PlayerCommand.MoveRight]);
        }

        Assert.Equal(counter, session.Conversation.GuideState.InteractionCounter);
    }

    [Fact]
    public void TheGuideStartsIdleAndASessionNeedsABundle()
    {
        var bundle = TestGame.Bundle;
        var guide = new GuideService(bundle, new WorldFacts(WorldGenerator.Generate(42, MediumSize)), 42, new Vocaterra.Nlp.Core.Determinism.StoredTimestamp(0));
        Assert.Equal("idle", guide.GuideState.StateId);
        Assert.Throws<ArgumentNullException>(() => new GameSession(null!, 42));
    }

    [Fact]
    public void WarmingUpChangesNothing()
    {
        var session = TestGame.NewSession();
        var before = session.Conversation.Context;

        session.Conversation.WarmUp();

        Assert.Equal(before, session.Conversation.Context);
    }
}
