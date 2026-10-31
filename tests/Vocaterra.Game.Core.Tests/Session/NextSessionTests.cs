using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>
/// What comes next once a play session's work is done (V12c): the objective line sends the player to sleep at their fire (and the arrow goes there when they are away), or
/// says every lesson is done when no later session has one, or names the lesson to go on with; the fire offers the sleep when the player stands at it, the fire is lit, the
/// game is at rest and no lesson step waits; a playtest offers none.
/// </summary>
public sealed class NextSessionTests
{
    private static string Hud(VoxelSession session) => string.Concat(session.ObjectiveHud().Select(static p => p.Text));

    private static void Sleep(VoxelSession session) => SaveGame.Sleep(session, SaveGame.Capture(session, TestGame.Route()));

    // Stands the player on the ground three blocks south of their fire ring (where the scripted r20-sleep stands), as --teleport does.
    private static void TeleportToFire(VoxelSession session)
    {
        var ring = session.Scene.Point(VoxelSession.PlayerFireRing);
        var grid = session.Scene.Grid;
        var (x, z) = (ring.X, ring.Z + 3);
        var y = Enumerable.Range(1, grid.SizeY - 3).Reverse().First(y => grid.IsSolid(x, y, z) && !BlockDefinition.IsLeaves(grid.GetBlock(x, y, z)) && !grid.IsSolid(x, y + 1, z));
        session.Simulation.TeleportTo(new BlockPos(x, y + 1, z));
    }

    [Fact]
    public void AfterTheFirstSessionsEndingTheObjectiveSaysToSleepAtTheFireAndAPlaytestKeepsItsLine()
    {
        var session = SleepTests.EndedFirstSession();
        TeleportToFire(session);

        Assert.Null(session.CurrentObjective);
        Assert.True(session.SessionWorkDone);
        Assert.Equal(2, session.NextLessonSession);
        Assert.True(session.RestDue);
        var line = Assert.Single(session.ObjectiveHud());
        Assert.Equal("Session complete: sleep at your fire to start session 2", line.Text);
        Assert.Equal(ObjectivePieceKind.Support, line.Kind);
        Assert.True(line.Done);
        Assert.True(session.AtOwnFire);
        Assert.True(session.CanSleep);
        Assert.True(SleepOffer.AtFire(session, free: true));
        Assert.False(SleepOffer.AtFire(session, free: false)); // a panel, a screen or an NPC under the crosshair comes first
        Assert.Equal("E: sleep until morning (session 2)", SleepOffer.PromptText(session));
        Assert.Equal("Enter: sleep until morning and start session 2", SleepOffer.CardText(session));
        Assert.True(SleepOffer.OnCard(session, cardUp: true, free: true));
        Assert.False(SleepOffer.OnCard(session, cardUp: false, free: true));

        session.SleepOffered = false; // a playtest: one session per run
        Assert.Equal("Session complete: your fire is lit", Hud(session));
        Assert.False(session.CanSleep);
        Assert.False(SleepOffer.AtFire(session, free: true));
        Assert.False(SleepOffer.OnCard(session, cardUp: true, free: true));
    }

    [Fact]
    public void AwayFromTheFireThePromptIsNotShownAndTheSleepOfTheNewSessionNamesItsLesson()
    {
        var session = SleepTests.EndedFirstSession();
        Stage.TeleportToNpc(session, "guide");

        Assert.False(session.AtOwnFire);
        Assert.False(SleepOffer.AtFire(session, free: true));
        Assert.True(SleepOffer.OnCard(session, cardUp: true, free: true)); // the card offers it wherever the player stands

        Sleep(session);

        Assert.Equal(2, session.PlaySession);
        Assert.Equal("Session 2: go to the village and talk to the villager to start the lesson “Personas”", Hud(session));
        Assert.Equal("villager", session.Pointer?.Point);
        Assert.Equal("Good morning: session 2 begins", SleepOffer.MorningText(session));
        Assert.Equal("E: sleep until morning (session 3)", SleepOffer.PromptText(session));
    }

    [Fact]
    public void OnWakingTheFireOffersNoSleepWhileTheNewSessionsLessonIsLeftAndOffersItOnceTheLessonIsDone()
    {
        var session = SleepTests.EndedFirstSession();
        TeleportToFire(session);
        Sleep(session); // session 2 begins at the fire

        Assert.True(session.AtOwnFire);
        Assert.True(session.CanSleep); // --sleep would still sleep (a script's step)
        Assert.False(session.SessionWorkDone); // Personas is left: the objective names it
        Assert.False(SleepOffer.AtFire(session, free: true)); // no prompt that would skip it

        session.Lessons.CountAsCompleted("personas", 2);
        session.Step([]);

        Assert.True(session.SessionWorkDone);
        Assert.Equal("Session complete: sleep at your fire to start session 3", Hud(session));
        Assert.True(SleepOffer.AtFire(session, free: true));
    }

    [Fact]
    public void WhenTheSessionsLessonIsDoneTheObjectiveSendsThePlayerBackToSleepAndTheArrowGoesToTheFire()
    {
        var session = SleepTests.EndedFirstSession();
        Sleep(session);
        Stage.TeleportToNpc(session, "shopkeeper");
        session.Step([]);
        Assert.False(session.SessionWorkDone); // Personas is unlocked and not done
        Assert.Equal("villager", session.Pointer?.Point);

        session.Lessons.CountAsCompleted("personas", 2); // the lesson ends at the stall, far from the fire
        session.Step([]);

        Assert.True(session.SessionWorkDone);
        Assert.Equal("Session complete: sleep at your fire to start session 3", Hud(session));
        Assert.Equal(VoxelSession.PlayerFireRing, session.Pointer?.Point);
        Assert.Contains(session.SessionEvents.OfType<WorldEvent>(), e => e.Name == WorldEvents.BeaconSet && e.Detail!.StartsWith("fuego", StringComparison.Ordinal));
        session.Step([]);
        Assert.Equal(1, session.SessionEvents.OfType<WorldEvent>().Count(e => e.Name == WorldEvents.BeaconSet && e.Detail!.StartsWith("fuego", StringComparison.Ordinal))); // once, when it begins
    }

    [Fact]
    public void ALessonUnderWayHoldsTheSleepBackAndTheObjectiveNamesItsNpc()
    {
        var session = SleepTests.EndedFirstSession();
        Sleep(session);
        session.OpenTalk("villager"); // Personas starts by itself
        session.EndTalk();

        Assert.True(session.Lessons.Engaged);
        Assert.False(session.SessionWorkDone);
        Assert.False(session.CanSleep); // a lesson step waits: it comes first
        Assert.False(session.RestDue);
        Assert.Equal("Next: talk to the villager for the lesson “Personas”", Hud(session));
    }

    [Fact]
    public void WhenNoLaterSessionHasALessonTheObjectiveSaysEveryLessonIsDoneAndTheFireStillSleeps()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(session, 7);
        session.Lessons.CountAsCompleted("como-es", 7);
        TeleportToFire(session);
        session.Step([]);
        Assert.Equal("water-for-plants", session.CurrentObjective?.Id);
        Assert.Equal(session.Locale.ObjectiveHudLine("water-for-plants"), Hud(session)); // an open order keeps its own line: there is still something to do

        session.SkipDeliver(session.Objectives.Single(static o => o.Id == "water-for-plants"));
        session.Step([]);

        Assert.Null(session.CurrentObjective);
        Assert.Null(session.NextLessonSession);
        Assert.True(session.SessionWorkDone);
        Assert.False(session.RestDue); // nothing to sleep for: no arrow to the fire
        Assert.Equal("Every lesson so far is done: practise any of them again in the book (J), or talk to the people of the village", Hud(session));
        Assert.True(session.CanSleep); // a sleep still lifts the spacing gaps
        Assert.True(SleepOffer.AtFire(session, free: true));

        Sleep(session);
        Assert.Equal(8, session.PlaySession);
        Assert.StartsWith("Every lesson so far is done", Hud(session), StringComparison.Ordinal);
    }

    [Fact]
    public void ANewSessionWhoseWorkIsAlreadyDoneSendsThePlayerToTheFire()
    {
        var session = SleepTests.EndedFirstSession();
        Sleep(session);
        session.Lessons.CountAsCompleted("personas", 2);
        session.Lessons.CountAsCompleted("ayer-y-manana", 2); // run early (--lesson-run runs a lesson whatever came before)
        Stage.TeleportToNpc(session, "shopkeeper");

        Sleep(session); // session 3 opens no new lesson: its work is done already

        Assert.Equal(3, session.PlaySession);
        Assert.Null(session.Lessons.NewLesson);
        Assert.Equal(4, session.NextLessonSession); // El pueblo opens in session 4 (after Ayer y mañana, done)
        Assert.Equal("Session complete: sleep at your fire to start session 4", Hud(session));
        Assert.Equal(VoxelSession.PlayerFireRing, session.Pointer?.Point);
    }
}
