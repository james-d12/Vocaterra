using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
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
        var session = new VoxelSession(TestGame.Data, TestGame.Seed, GuideSession.DefaultStartedAt, scene);
        var turn = session.Say("¿dónde está el río?");

        Assert.True(turn.Communicated);
        Assert.Equal("ask_directions", turn.Intent);
        Assert.NotNull(turn.Pointer);
        Assert.Equal("río", turn.Pointer.Place);
        Assert.Equal(BlockId.Water, scene.Grid.GetBlock(turn.Pointer.Target.X, turn.Pointer.Target.Y, turn.Pointer.Target.Z));

        // The same answer as the facts give from the spawn: the nearest water block, its distance and its direction.
        var expected = WorldFacts.Live(scene.Grid, TestGame.Nouns, TestGame.Data.Words.Directions).NearestOfNoun(AtSpawn(scene), "río");
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
    public void TheS3DemoLineStillHolds()
    {
        // implementation-plan section 6, item 4: the game at seed 42 with --look 120,-5 and --say "¿dónde está el río?" prints "ask_directions, understood" and the
        // guide's line, and the beacon goes out. As the scenario script runs it: the look, then a line with no panel open walks the player to the guide's
        // stand-off cell (ScenarioScript.StartTalk), faces her chest, settles, opens the panel on her and types the line; every tick sends the look control's angles
        // first (WorldStepper). The plan's line, "a la izquierda, a sesenta bloques", is the S3 world's; since S13 the game prints the line below (checked with the
        // game itself on 2026-10-30, village-plan V02, which does not touch the guide's answer).
        var session = TestGame.NewSessionWithShippedLessons(seed: 42);
        var look = new LookControl();
        Run(session, look, [VoxelStep.Look(120, -5)]);
        var guide = session.Scene.Npc("guide").Feet;
        var stand = NpcStandOff.Choose(session.Scene.Grid, guide, session.Simulation.Player.FeetBlock)!.Value;
        Run(session, look, [VoxelStep.WalkTo(stand.X, stand.Z)]);
        var body = session.Simulation.Player;
        look.LookAt(body.EyeX / (double)Unit, body.EyeY / (double)Unit, body.EyeZ / (double)Unit, guide.X + 0.5, guide.Y + 1.2, guide.Z + 0.5);
        for (var tick = 0; tick < ScenarioScript.SettleTicks; tick++)
        {
            session.Step([VoxelCommand.Look(look.Yaw, look.Pitch)]);
        }

        _ = session.OpenTalk("guide");

        var turn = session.Say("¿dónde está el río?");

        Assert.Equal(("ask_directions", true), (turn.Intent, turn.Communicated));
        Assert.Equal("El río está allí, adelante, a la izquierda, a cincuenta y cuatro bloques.", turn.WorldLine);
        Assert.Equal(new PointerMarker("río", turn.Pointer!.Target), session.Pointer);
        Assert.Equal(new GuideCell(230, 34, 201), turn.Pointer.Target); // the game's "arrow: río at 230,34,201 (54 blocks, adelante, a la izquierda)"
    }

    private static void Run(VoxelSession session, LookControl look, IReadOnlyList<VoxelStep> steps)
    {
        var script = new VoxelScript(session.Simulation, look, steps, static _ => null, static _ => { });
        var commands = new List<VoxelCommand>();
        for (var tick = 0; tick < 6000 && !script.IsDone; tick++)
        {
            commands.Clear();
            script.Fill(commands);
            commands.Insert(0, VoxelCommand.Look(look.Yaw, look.Pitch));
            session.Step(commands);
        }

        Assert.True(script.IsDone, "the scripted steps did not finish");
    }

    [Fact]
    public void ThePlayerWhoAsksWhatTheCursorIsOnIsToldTheNounOfTheBlock()
    {
        var scene = RouteScene();
        var session = new VoxelSession(TestGame.Data, TestGame.Seed, GuideSession.DefaultStartedAt, scene);
        var water = scene.Point("river");
        var turn = session.Say("¿qué es esto?", water);

        Assert.Equal("guide.identify", turn.HostEvents.Single(static e => e.StartsWith("guide.", StringComparison.Ordinal)));
        Assert.Equal(TestGame.Nouns.NounsOfBlock(scene.Grid.GetBlock(water.X, water.Y, water.Z)).First().Lemma, turn.IdentifiedNoun);
    }

    [Fact]
    public void ANamedRiverBlockIsAguaAndRioAndANameOfEitherIsRight()
    {
        var scene = RouteScene();
        var session = new VoxelSession(TestGame.Data, TestGame.Seed, GuideSession.DefaultStartedAt, scene);
        var water = scene.Point("river");
        Assert.Equal(BlockId.Water, scene.Grid.GetBlock(water.X, water.Y, water.Z));

        var turn = session.Say("es un río", water);
        Assert.Equal(NameCheck.Right, turn.Naming);
        Assert.Equal("agua", turn.NameTarget);
    }
}
