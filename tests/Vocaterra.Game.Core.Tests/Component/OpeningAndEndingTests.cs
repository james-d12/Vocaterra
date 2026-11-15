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
/// is the objective once she is met; the complete checklist points the beacon at the inn and Luna sends the player there (Y07); a room from the innkeeper completes the
/// session, and its ending is the innkeeper's, after the talk that gave the room has closed. The guide's camp ring keeps completing the checklist's torch step. Nothing here is gated behind language or voice.
/// </summary>
public sealed class OpeningAndEndingTests
{
    private static string Hud(VoxelSession session) => string.Concat(session.ObjectiveHud().Select(static p => p.Text));

    /// <summary>The player takes the room at the inn (Y07): walks up to Rosa, opens the talk, says «sí» and closes the talk.</summary>
    internal static void TakeTheRoom(VoxelSession session)
    {
        Stage.TeleportToNpc(session, "innkeeper");
        _ = session.OpenTalk("innkeeper");
        _ = session.Say("sí");
        session.EndTalk();
        session.Step([]);
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
    public void ThePlayerArrivesAloneOnTheBareLedgeAndTheObjectiveSendsThemDownTheTrailToTheGuide()
    {
        var session = TestGame.NewSessionWithShippedLessons();

        Assert.Equal("meet-guide", session.CurrentObjective?.Id);
        Assert.Equal("Go down the trail and find the guide", Hud(session));
        Assert.Equal(ObjectivePieceKind.Support, Assert.Single(session.ObjectiveHud()).Kind); // English only, in its own colour
        Assert.False(session.GuideMet);
        Assert.InRange(session.Simulation.Player.FeetBlock.X, session.Scene.Point("camp.player").X - 6, session.Scene.Point("camp.player").X + 6); // the spawn is the mountain ledge, alone: no NPC within a long walk
        Assert.All(TestGame.Data.Npcs.Ids, npc =>
        {
            var feet = session.Scene.Npc(npc).Feet;
            var me = session.Simulation.Player.FeetBlock;
            Assert.True(Math.Abs(feet.X - me.X) + Math.Abs(feet.Z - me.Z) > 40, npc);
        });
        Assert.Empty(session.Events.OfType<PhraseUse>()); // no quiz
        Assert.Null(session.Lessons.View);
        // Y08: the ledge is bare: no tent, chest, crate or fire ring within sight of the spawn (the player's own camp comes later, on land they buy)
        var spawn = session.Scene.Point("camp.player");
        foreach (var kind in new[] { BlockId.FireRing, BlockId.Canvas, BlockId.Chest, BlockId.Crate })
        {
            Assert.DoesNotContain(session.Scene.Grid.BlocksOfKind(kind), p => Math.Abs(p.X - spawn.X) <= 30 && Math.Abs(p.Z - spawn.Z) <= 30);
        }
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
        _ = gathered.Simulation.GrantItems(ItemId.Wood, 1);
        gathered.Step([]); // the first step of the checklist without ever meeting the guide: nothing is gated, and the line moves on
        Assert.Equal("camp", gathered.CurrentObjective?.Id);
    }

    [Fact]
    public void ADoStepOfTheLessonPutsItsShortLineInTheObjectiveAreaWhileItWaits()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        _ = LessonDriver.CompleteAll(session); // the introduction, then Luna's first class (L01), whose fruit step this is
        session.EndTalk();
        _ = session.OpenTalkForClass("guide");
        while (session.Lessons.Current is { Id: not "u4-pick" })
        {
            _ = LessonDriver.DoStep(session);
        }

        Assert.Equal("Pick a fruit from a berry bush, then go back to Luna", Hud(session));
    }

    [Fact]
    public void Given_TheCampChecklistIsComplete_When_TheLastStepIsDone_Then_TheBeaconPointsAtTheInnAndLunaSendsThePlayerThere()
    {
        var session = CompletedCamp();

        Assert.Equal("inn-room", session.CurrentObjective?.Id);
        Assert.Equal("Go to the inn (la posada) and ask for a room", Hud(session));
        Assert.Equal("posada", session.Pointer!.Place);
        Assert.Equal("inn", session.Pointer.Point);
        Assert.Equal(session.Scene.Point("inn"), session.Pointer.Target.ToBlock());
        var spoken = Assert.Single(session.TakeSpoken());
        Assert.Equal((SpeechSpeakers.Guide, "¡Muy bien! Ahora ve a la posada.", "Very good! Now go to the inn."), (spoken.Speaker, spoken.Spanish, spoken.English));
        Assert.Equal(["“¡Muy bien! Ahora ve a la posada.” (Very good! Now go to the inn.)"], session.TakeHints());
        Assert.Empty(session.TakeSpoken());
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Torch)); // no spare torch any more: the player's own fire is lit at the campsite (Y12)
        Assert.False(session.SessionComplete);
        Assert.DoesNotContain(session.Events.OfType<WorldEvent>(), static e => e.Name == "spare_torch");
    }

    [Fact]
    public void Given_TheRoomWasTakenFirst_When_TheCampChecklistIsComplete_Then_NoOneSendsThePlayerToTheInn()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        TakeTheRoom(session);

        _ = session.Simulation.GrantItems(ItemId.Wood, 5);
        _ = session.Simulation.GrantItems(ItemId.Berry, 3);
        _ = session.Objective.Record(WorldEvents.TorchAtRing, "firering");
        _ = session.HostEvent(WorldEvents.BottleFilled);

        Assert.Empty(session.TakeSpoken());
        Assert.True(session.SessionComplete);
    }

    [Fact]
    public void Given_TheCampIsDone_When_TheInnkeeperGivesTheRoom_Then_InnRoomIsDoneAndTheSessionCompletesWithNoSpecialCase()
    {
        var session = CompletedCamp();
        Assert.False(session.SessionComplete);

        Stage.TeleportToNpc(session, "innkeeper");
        _ = session.OpenTalk("innkeeper");
        _ = session.Say("sí");
        session.Step([]);

        Assert.True(session.LodgingGiven);
        Assert.True(session.Objectives.Single(static o => o.Id == "inn-room").Complete);
        Assert.True(session.SessionComplete);
        Assert.Null(session.CurrentObjective);
        var names = session.Events.OfType<WorldEvent>().Select(static e => e.Name).ToList();
        Assert.Equal(1, names.Count(static n => n == WorldEvents.LodgingGranted));
        Assert.Equal(1, names.Count(static n => n == WorldEvents.SessionComplete));
        Assert.True(names.IndexOf(WorldEvents.LodgingGranted) < names.IndexOf(WorldEvents.SessionComplete));
        Assert.True(session.FireLit); // the torch at Luna's ring (the camp's step) lit it; no objective waits for that
        Assert.Equal("Session complete: go to bed to start session 2", Hud(session));
        session.SleepOffered = false; // a playtest offers no sleep
        Assert.Equal("Session complete: you have a room at the inn", Hud(session));
    }

    [Fact]
    public void Given_TheRoomWasGivenInATalk_When_ItIsStillOpen_Then_TheEndingWaitsForItToCloseAndIsTheInnkeepersLine()
    {
        var session = CompletedCamp();
        var ending = new SessionEnding();
        Stage.TeleportToNpc(session, "innkeeper");
        _ = session.OpenTalk("innkeeper");
        _ = session.Say("sí");
        session.Step([]);

        Assert.True(session.SessionComplete);
        Assert.True(session.TalkOpen);
        Assert.False(ending.Update(session, 100)); // her talk panel is up: the card does not cover it
        Assert.False(ending.Begun);

        session.EndTalk();
        Assert.True(ending.Update(session, 200));

        Assert.Equal("innkeeper", ending.Speaker);
        Assert.Equal("Buenas noches. Tu cama está allí.", ending.Spanish);
        Assert.Equal("Good night. Your bed is over there.", ending.English);
        Assert.False(ending.Update(session, 201)); // once
    }

    [Fact]
    public void TheClosingLineIsASpeakableLineOfTheInnkeeperWithItsEnglish()
    {
        var session = TestGame.NewSession();
        var line = TestGame.Speech.Single(l => l.LineId == session.ClosingLineId);

        Assert.Equal(("innkeeper_night.1", "innkeeper", "Buenas noches. Tu cama está allí."), (line.LineId, line.Speaker, line.Text));
        Assert.Equal(line.Text, session.ClosingLine);
        Assert.Equal("innkeeper", session.ClosingSpeaker);
    }

    [Fact]
    public void TheTorchAtTheGuidesCampFireRingCompletesTheChecklistsTorchStepAndLightsTheFireButDoesNotEndTheSession()
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp();
        _ = session.HostEvent(WorldEvents.BottleFilled);

        Assert.Equal(1, session.Objective.Step("torch")!.Count);
        Assert.True(session.FireLit); // Y08: the latch the sound beds read is the torch at Luna's ring, the only camp ring there is
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.FireLit && e.Detail == "firering");
        Assert.False(session.SessionComplete);
        Assert.Equal("inn-room", session.CurrentObjective?.Id);
        Assert.Equal("posada", session.Pointer!.Place);
    }

    [Fact]
    public void Given_TheRoomWasGiven_When_TheGameIsSavedAndLoaded_Then_InnRoomIsDoneWithoutTheEventAgain()
    {
        var session = CompletedCamp();
        TakeTheRoom(session);
        var saved = SaveGame.Save(session);
        Assert.Contains("\"lodgingGiven\":true", saved.Replace(" ", string.Empty, StringComparison.Ordinal));

        var loaded = SaveGame.Load(saved, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.True(loaded.LodgingGiven);
        Assert.True(loaded.Objectives.Single(static o => o.Id == "inn-room").Complete);
        Assert.True(loaded.SessionComplete);
        Assert.Null(loaded.CurrentObjective);
        Assert.DoesNotContain(loaded.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.LodgingGranted); // restored, not raised
        Assert.Empty(loaded.TakeSpoken());
        Assert.Equal(saved, SaveGame.Save(loaded));
    }

    [Fact]
    public void Given_ASaveBeforeTheRoom_When_ItLoads_Then_TheObjectiveLineIsTheInn()
    {
        var session = CompletedCamp();
        var saved = SaveGame.Save(session);

        var loaded = SaveGame.Load(saved, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.Equal("inn-room", loaded.CurrentObjective?.Id);
        Assert.False(loaded.LodgingGiven);
        Assert.False(loaded.SessionComplete);
    }
}
