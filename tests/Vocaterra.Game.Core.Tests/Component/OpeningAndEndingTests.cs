using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Component;

/// <summary>
/// The opening and the ending of the session (S17, D-game-15): the player wakes alone at their own camp and is sent down the trail to the guide; the checklist
/// is the objective once she is met; the complete checklist sends the player back up the trail with a spare torch; a torch on the rim of the fire ring at their own
/// camp lights it and ends the session. The guide's camp ring keeps completing the checklist's torch step. Nothing here is gated behind language or voice.
/// </summary>
public sealed class OpeningAndEndingTests
{
    private static string Hud(VoxelSession session) => string.Concat(session.ObjectiveHud().Select(static p => p.Text));

    private static BlockPos PlayerRingRim(VoxelSession session)
    {
        var centre = session.Scene.Point("firering.player");
        var stone = session.Scene.Grid.BlocksOfKind(BlockId.FireRing).First(p => Math.Abs(p.X - centre.X) <= 1 && Math.Abs(p.Z - centre.Z) <= 1);
        return new BlockPos(stone.X, stone.Y + 1, stone.Z);
    }

    internal static void PlaceTorchOnPlayersRing(VoxelSession session)
    {
        var rim = PlayerRingRim(session);
        Assert.True(VoxelWalker.WalkTo(session, cell => Math.Abs(cell.X - rim.X) + Math.Abs(cell.Z - rim.Z) <= 3 && Math.Abs(cell.Y - rim.Y) <= 2), "no way up to the player's camp");
        Assert.True(VoxelWalker.Place(session, ItemId.Torch, rim));
        Assert.Equal(BlockId.Torch, session.Scene.Grid.GetBlock(rim.X, rim.Y, rim.Z));
    }

    /// <summary>The camp played headless from the spawn (the guide's camp torch placed), then the water: the checklist is complete.</summary>
    private static VoxelSession CompletedCamp()
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp();
        _ = session.HostEvent(WorldEvents.BottleFilled);
        Assert.True(session.Objective.Complete);
        return session;
    }

    [Fact]
    public void ThePlayerWakesAloneAtTheirCampAndTheObjectiveSendsThemDownTheTrailToTheGuide()
    {
        var session = TestGame.NewSessionWithShippedLessons();

        Assert.Equal("meet-guide", session.CurrentObjective?.Id);
        Assert.Equal("Go down the trail and find the guide", Hud(session));
        Assert.Equal(ObjectivePieceKind.Support, Assert.Single(session.ObjectiveHud()).Kind); // English only, in its own colour
        Assert.False(session.GuideMet);
        Assert.InRange(session.Simulation.Player.FeetBlock.X, session.Scene.Point("camp.player").X - 6, session.Scene.Point("camp.player").X + 6); // the spawn is the player's camp, alone: no NPC within a long walk
        Assert.All(TestGame.Data.Npcs.Ids, npc =>
        {
            var feet = session.Scene.Npc(npc).Feet;
            var me = session.Simulation.Player.FeetBlock;
            Assert.True(Math.Abs(feet.X - me.X) + Math.Abs(feet.Z - me.Z) > 40, npc);
        });
        Assert.Empty(session.Events.OfType<PhraseUse>()); // no quiz
        Assert.Null(session.Lessons.View);
        Assert.Contains(session.Scene.Grid.BlocksOfKind(BlockId.FireRing), p => Math.Abs(p.X - session.Scene.Point("firering.player").X) <= 1); // the cold ring waits at the camp
    }

    [Fact]
    public void ThePointerOfTheBeaconStartsOnTheGuidesCampSoTheTrailLeadsThere()
    {
        var session = TestGame.NewSessionWithShippedLessons();

        // The front-end stands the beacon on camp.guide at the start (S13); the guide's camp is the nearest NPC by the trail.
        var guide = session.Scene.Point("camp.guide");
        Assert.True(Math.Abs(guide.X - session.Simulation.Player.FeetBlock.X) + Math.Abs(guide.Z - session.Simulation.Player.FeetBlock.Z) > 40);
    }

    [Fact]
    public void TheObjectiveAreaShowsTheChecklistOnceTheGuideIsMetOrTheFirstStepIsDone()
    {
        var talked = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(talked, "guide");
        _ = talked.OpenTalk("guide");
        Assert.Equal("camp", talked.CurrentObjective?.Id);
        Assert.StartsWith("Campamento (camp): madera (wood) 0/5", Hud(talked), StringComparison.Ordinal);

        var gathered = TestGame.NewSessionWithShippedLessons();
        _ = gathered.Objective.Record(WorldEvents.ItemPickedUp, "madera", 1);
        gathered.Step([]); // the first step of the checklist without ever meeting the guide: nothing is gated, and the line moves on
        Assert.Equal("camp", gathered.CurrentObjective?.Id);
    }

    [Fact]
    public void ADoStepOfTheLessonPutsItsShortLineInTheObjectiveAreaWhileItWaits()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        while (session.Lessons.Current is { Id: not "u4-pick" })
        {
            _ = LessonDriver.DoStep(session);
        }

        Assert.Equal("Pick a fruit from a berry bush, then go back to Luna", Hud(session));
    }

    [Fact]
    public void ACompleteChecklistSendsThePlayerBackUpTheTrailWithASpareTorchAndASayingInEnglish()
    {
        var session = CompletedCamp();

        Assert.Equal("light-fire", session.CurrentObjective?.Id);
        Assert.Equal("Go back up the trail and light your own fire", Hud(session));
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Torch)); // the guide's camp took the first torch (the checklist's); this one is for the player's own fire
        var world = session.Events.OfType<WorldEvent>().Select(static e => e.Name).ToList();
        Assert.Contains(WorldEvents.ObjectiveComplete, world);
        Assert.Contains(WorldEvents.SpareTorch, world);
        Assert.Equal(["A spare torch is in your bag. Take it up the trail and light your own fire."], session.TakeHints());
        Assert.False(session.FireLit);
        Assert.False(session.SessionComplete);
        Assert.Equal("fuego", session.Pointer!.Place); // the beacon points up the trail to the cold ring
        Assert.Equal(session.Scene.Point("firering.player").X, session.Pointer.Target.X);
    }

    [Fact]
    public void ATorchOnTheRimOfTheRingAtThePlayersOwnCampLightsTheFireAndEndsTheSession()
    {
        var session = CompletedCamp();

        PlaceTorchOnPlayersRing(session);
        session.Step([]);

        Assert.True(session.FireLit);
        Assert.True(session.SessionComplete);
        Assert.Null(session.CurrentObjective);
        Assert.Equal("Session complete: sleep at your fire to start session 2", Hud(session)); // V12c: the next session is a sleep at this fire
        Assert.Null(session.Pointer); // the player stands at the fire: no arrow to it
        session.SleepOffered = false; // a playtest offers no sleep: the line of S17
        Assert.Equal("Session complete: your fire is lit", Hud(session));
        session.SleepOffered = true;
        var names = session.Events.OfType<WorldEvent>().Select(static e => e.Name).ToList();
        Assert.Equal(1, names.Count(static n => n == WorldEvents.FireLit));
        Assert.Equal(1, names.Count(static n => n == WorldEvents.SessionComplete));
        Assert.True(names.IndexOf(WorldEvents.FireLit) < names.IndexOf(WorldEvents.SessionComplete));
        Assert.Equal("¡Muy bien! Ya tienes fuego.", session.ClosingLine);
        Assert.Equal("Very good! Now you have a fire.", session.Locale.Line(session.ClosingLineId!));
    }

    [Fact]
    public void TheClosingLineIsASpeakableLineOfTheGuideWithItsEnglish()
    {
        var line = TestGame.Speech.Single(static l => l.Text == "¡Muy bien! Ya tienes fuego.");

        Assert.Equal(("guide.lesson.end.1", SpeechSpeakers.Guide), (line.LineId, line.Speaker));
        Assert.Equal(line.Text, TestGame.NewSession().ClosingLine);
    }

    [Fact]
    public void TheGuidesCampFireRingStillCompletesTheChecklistsTorchStepAndIsNotThePlayersFire()
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp();

        Assert.Equal(1, session.Objective.Step("torch")!.Count);
        Assert.False(session.FireLit); // the torch at the guide's ring is the camp's, not the player's own fire
        Assert.False(session.SessionComplete);
    }

    [Fact]
    public void ATorchAtThePlayersCampBeforeTheChecklistIsDoneLightsTheFireAndTheSessionEndsWhenTheChecklistDoes()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Assert.Equal(0, session.Simulation.GrantItems(ItemId.Torch, 1));

        PlaceTorchOnPlayersRing(session);
        session.Step([]);

        Assert.True(session.FireLit);
        Assert.False(session.SessionComplete); // the checklist is not done
        Assert.Equal("meet-guide", session.CurrentObjective?.Id);

        _ = session.Objective.Record(WorldEvents.ItemPickedUp, "madera", 5);
        _ = session.Objective.Record(WorldEvents.ItemPickedUp, "fruta", 3);
        _ = session.Objective.Record(WorldEvents.TorchAtRing, "firering");
        _ = session.HostEvent(WorldEvents.BottleFilled);

        Assert.True(session.SessionComplete);
        Assert.Null(session.CurrentObjective);
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Torch)); // the fire is lit already: no spare torch
        Assert.DoesNotContain(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.SpareTorch);
    }

    [Fact]
    public void AFullBagKeepsTheSpareTorchOutOfTheBagAndTheLearnerCanStillMakeOne()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        for (var i = 0; i < 100; i++)
        {
            _ = session.Simulation.GrantItems(ItemId.Wood, 99); // until the bag is full
        }

        Assert.True(session.Simulation.GrantItems(ItemId.Torch, 1) > 0, "the bag is full");

        _ = session.Objective.Record(WorldEvents.ItemPickedUp, "madera", 5);
        _ = session.Objective.Record(WorldEvents.ItemPickedUp, "fruta", 3);
        _ = session.Objective.Record(WorldEvents.TorchAtRing, "firering");
        _ = session.HostEvent(WorldEvents.BottleFilled);

        Assert.Equal("light-fire", session.CurrentObjective?.Id); // the objective line still sends them up; a torch is made from two wood at the workbench
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e is { Name: WorldEvents.SpareTorch, Detail: "bag full" });
        Assert.Empty(session.TakeHints()); // nothing was said about a torch that is not there
    }

    [Fact]
    public void TheEndingSurvivesASaveAndALoadAndTheStageIsWorkedOutAgain()
    {
        var session = CompletedCamp();
        PlaceTorchOnPlayersRing(session);
        session.Step([]);
        var saved = SaveGame.Save(session, TestGame.Route());

        var loaded = SaveGame.Load(saved, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.True(loaded.FireLit);
        Assert.True(loaded.SessionComplete);
        Assert.Null(loaded.CurrentObjective);
        Assert.Equal(saved, SaveGame.Save(loaded, TestGame.Route()));
    }

    [Fact]
    public void ASaveBeforeTheFireIsLitLoadsAtTheStageTheChecklistHasReached()
    {
        var session = CompletedCamp();
        var saved = SaveGame.Save(session, TestGame.Route());

        var loaded = SaveGame.Load(saved, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.Equal("light-fire", loaded.CurrentObjective?.Id);
        Assert.False(loaded.FireLit);
    }
}
