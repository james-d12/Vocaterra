using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>
/// A <c>do</c> step that sends the learner to a place of the scene (village-plan V02, <c>pointPlace</c>): the beacon goes there when the step begins, and the step
/// waiting for <c>beacon_reached</c> with that place is done on arrival, as session 4 walks the learner to the bakery and the smithy.
/// </summary>
public sealed class LessonPointPlaceTests
{
    private const string Walk = """
        { "schemaVersion": 1, "lessons": [ { "id": "walk", "npc": "villager", "level": "a1", "title": "A walk", "english": "Walk to the shop.", "autoStart": true, "steps": [
          { "id": "go", "type": "do", "pointPlace": "stall", "trigger": { "event": "beacon_reached", "detail": "stall" }, "english": "Walk to the shop.", "hud": "Walk to the shop" },
          { "id": "end", "type": "explain", "english": "You are there." } ] } ] }
        """;

    [Fact]
    public void ADoStepPointsAtAPlaceAndIsDoneWhenThePlayerArrives()
    {
        var session = TestGame.NewSession(lessons: TestGame.Lessons(Walk));
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager"); // the lesson starts by itself, and its first step points the beacon at the stall

        var stall = session.Scene.Point("stall");
        Assert.Equal(new PointerMarker("tienda", stall, Point: "stall"), session.Pointer);
        var set = session.Events.OfType<WorldEvent>().Last(e => e.Name == WorldEvents.BeaconSet);
        Assert.Equal("villager", set.Npc);
        Assert.Equal("go", session.Lessons.Current!.Id);
        session.EndTalk();

        // Far from the stall nothing happens; standing next to it, the arrow comes down, the arrival is logged with the place's name and the step is done.
        session.Step([]);
        Assert.DoesNotContain(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.BeaconReached);
        session.Simulation.TeleportTo(Beside(session, stall));
        session.Step([]);

        Assert.Null(session.Pointer);
        Assert.Equal("stall", Assert.Single(session.Events.OfType<WorldEvent>(), e => e.Name == WorldEvents.BeaconReached).Detail);
        Assert.Equal("end", session.Lessons.Current!.Id);
    }

    [Fact]
    public void TheGuidesReviewQuestionIsNoDestinationAndLogsNoArrival()
    {
        var session = TestGame.NewSession();
        var stall = session.Scene.Point("stall");
        session.RestorePointer(new PointerMarker("tienda", stall, Quiz: true));
        session.Simulation.TeleportTo(Beside(session, stall));

        session.Step([]);

        Assert.Null(session.Pointer);
        Assert.DoesNotContain(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.BeaconReached);
    }

    [Fact]
    public void ADoStepPointsAtANounOrAPlaceNotBoth()
    {
        Assert.Equal("stall", TestGame.Lessons(Walk).Find("walk")!.Steps[0].PointPlace);

        var both = Walk.Replace("\"pointPlace\": \"stall\"", "\"point\": \"árbol\", \"pointPlace\": \"stall\"", StringComparison.Ordinal);
        var error = Assert.Throws<InvalidDataException>(() => TestGame.Lessons(both));
        Assert.Contains("points at one of them", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APlaceALessonPointsAtThatTheSceneDoesNotHaveIsAProblem()
    {
        var lessons = TestGame.Lessons(Walk.Replace("\"pointPlace\": \"stall\"", "\"pointPlace\": \"mill\"", StringComparison.Ordinal));

        var problems = GameDataValidator.Validate(TestGame.Data.WithLessons(lessons));

        Assert.Contains("game/lessons/walk.json: the step 'go' points at the place 'mill', which is not a point of the scene.", problems);
        Assert.DoesNotContain(GameDataValidator.Validate(TestGame.Data.WithLessons(TestGame.Lessons(Walk))), static p => p.Contains("point of the scene", StringComparison.Ordinal));
    }

    // A free cell on the ground next to a place: its own column, one block up when the place is a solid block's top (the feet stand on it).
    private static BlockPos Beside(VoxelSession session, BlockPos place)
    {
        var grid = session.Scene.Grid;
        for (var radius = 1; radius <= 2; radius++)
        {
            foreach (var (dx, dz) in new[] { (radius, 0), (-radius, 0), (0, radius), (0, -radius) })
            {
                var x = place.X + dx;
                var z = place.Z + dz;
                for (var y = place.Y + 3; y >= place.Y - 1; y--)
                {
                    if (grid.IsSolid(x, y - 1, z) && !grid.IsSolid(x, y, z) && !grid.IsSolid(x, y + 1, z))
                    {
                        return new BlockPos(x, y, z);
                    }
                }
            }
        }

        throw new InvalidOperationException($"No free cell beside {place}.");
    }
}
