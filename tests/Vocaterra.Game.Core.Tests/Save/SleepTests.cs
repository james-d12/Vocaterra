using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Component;
using Vocaterra.Game.Core.Tests.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>
/// The sleep at the player's own fire (V12c) is exactly a load: the save written after a sleep is the save written after a save and a load (<see cref="SaveGame.Load"/> with
/// the next session) of the same state, byte for byte, and what the player sees next is the same too (the objective, the arrow, the lesson, the warm-up, the review). No field
/// differs but the needs, which a night moves by the rule of PlayerNeeds.Sleep (N03). The stored time does not move, the session number goes up by one, and what a save does not keep is gone.
/// </summary>
public sealed class SleepTests
{
    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static string SaveOf(VoxelSession session) => SaveGame.Save(session, Pristine());

    // A load keeps the day (V25) and a sleep moves it: the load that a sleep equals is a load and then the next day.
    private static VoxelSession LoadOf(string text, VoxelSession like)
    {
        var loaded = SaveGame.Load(text, TestGame.Data, Pristine, credit: like.Credit);
        loaded.Day++;
        loaded.Needs.Sleep(); // N03: a night moves the needs, which a load does not; everything else is a load's
        loaded.SettleNeedStages();
        loaded.BeginPlaySession();
        return loaded;
    }

    private static void Sleep(VoxelSession session) => SaveGame.Sleep(session, SaveGame.Capture(session, Pristine()));

    /// <summary>A new game with session 1's ending reached without the walk (the skip's ending, V12c): the guide's lesson counted, the camp done, the fire lit.</summary>
    internal static VoxelSession EndedFirstSession()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.Lessons.CountAsCompleted("first-words", 1);
        session.SkipToEnding();
        Assert.True(session.SessionComplete);
        Assert.True(session.FireLit);
        return session;
    }

    // What the player meets next, beyond the save: the session, the time, the objective line, the arrow, the lesson, the warm-up, the review question and the second tries.
    private static void SameAsALoad(VoxelSession loaded, VoxelSession slept)
    {
        Assert.Equal(SaveOf(loaded), SaveOf(slept));
        Assert.Equal(loaded.Day, slept.Day);
        Assert.Equal(loaded.StoredTime, slept.StoredTime);
        Assert.Equal(loaded.Simulation.ComputeStateDigest(), slept.Simulation.ComputeStateDigest());
        Assert.Equal(loaded.ObjectiveHud(), slept.ObjectiveHud());
        Assert.Equal(loaded.Pointer, slept.Pointer);
        Assert.Equal(loaded.SessionComplete, slept.SessionComplete);
        Assert.Equal(loaded.FireLit, slept.FireLit);
        Assert.Equal(loaded.GuideMet, slept.GuideMet);
        Assert.Equal(loaded.Lessons.Engaged, slept.Lessons.Engaged);
        Assert.Equal(loaded.Lessons.Active?.Id, slept.Lessons.Active?.Id);
        Assert.Equal(loaded.Lessons.StepNumber, slept.Lessons.StepNumber);
        Assert.Equal(loaded.Lessons.NewLesson?.Id, slept.Lessons.NewLesson?.Id);
        Assert.Equal(loaded.Deck.Status, slept.Deck.Status);
        Assert.Equal(loaded.Deck.OfferedThisSession, slept.Deck.OfferedThisSession);
        Assert.Equal(loaded.Review, slept.Review);
        Assert.Equal(loaded.PhraseReview, slept.PhraseReview);
        Assert.Equal(loaded.RetryNote, slept.RetryNote);
        Assert.Equal(loaded.Retries.Pending, slept.Retries.Pending);
        Assert.Equal(loaded.Retries.Asked, slept.Retries.Asked);
        Assert.Equal(loaded.Tips.Current, slept.Tips.Current);
        Assert.Equal(loaded.CurrentNpc, slept.CurrentNpc);
        Assert.Equal(loaded.KeyWordCards, slept.KeyWordCards);
        Assert.Equal(loaded.TakeWordCards(), slept.TakeWordCards());
        Assert.Equal(loaded.TakeHints(), slept.TakeHints());
        Assert.Equal(loaded.Migration, slept.Migration);
        Assert.Equal(loaded.SkippedTo, slept.SkippedTo);
        Assert.Equal(loaded.RestDue, slept.RestDue);
        foreach (var button in SpeechButtonKeys.All)
        {
            Assert.Equal(loaded.ButtonEnglishUp(button), slept.ButtonEnglishUp(button));
        }

        // The new session's events: a load starts with none, a sleep with the sleep itself; the rest (the arrow of the new lesson) is the same.
        Assert.Equal(WorldEvents.Slept, Assert.IsType<WorldEvent>(slept.SessionEvents[0]).Name);
        Assert.Equal(loaded.SessionEvents, slept.SessionEvents.Skip(1));
    }

    [Fact]
    public void ASleepAfterThePlayedFirstSessionGivesTheSaveAndTheGameASaveAndALoadGive()
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp();
        _ = session.HostEvent(WorldEvents.BottleFilled);
        OpeningAndEndingTests.PlaceTorchOnPlayersRing(session);
        session.Step([]);
        Assert.True(session.SessionComplete);
        session.OpenTalk("villager"); // a talk that leaves its traces: the NPC's last line, the panel's support
        session.Say("no sé");
        session.EndTalk();
        session.Step([]);

        var loaded = LoadOf(SaveOf(session), session);
        Sleep(session);

        loaded.RenewAtNight(); // the night's one change a load does not make (N06): the three bushes the camp picked grow back
        SameAsALoad(loaded, session);
        Assert.Equal(2, session.Day);
        Assert.Equal("personas", session.Lessons.NewLesson?.Id);
        Assert.Equal("villager", session.Pointer?.Point); // the new session's lesson is pointed at (S26)
    }

    [Fact]
    public void ASleepWithAReviewAWarmUpAndASecondTryOpenClearsThemAsALoadDoes()
    {
        var session = EndedFirstSession();
        session.Day = 2;
        session.Sitting = 2;
        session.Deck.Enabled = true;
        session.OpenTalk("shopkeeper"); // the shopkeeper has no lesson: his own conversation
        session.Say("no sé"); // a second try waits for him
        session.EndTalk();
        Assert.NotEmpty(session.Retries.Pending);
        _ = session.Deck.Request(); // "Review now" from the book
        session.Step([]);

        var loaded = LoadOf(SaveOf(session), session);
        Sleep(session);

        SameAsALoad(loaded, session);
        Assert.Empty(session.Retries.Pending);
        Assert.Equal(DeckStatus.Idle, session.Deck.Status);
        Assert.False(session.Deck.OfferedThisSession);
        Assert.Null(session.PhraseReview);
    }

    [Fact]
    public void ALessonUnderWayStaysAtItsStepAndWaitsForItsNpcAsAfterALoad()
    {
        var session = EndedFirstSession();
        session.Day = 2;
        session.Sitting = 2;
        session.OpenTalk("villager"); // Personas starts by itself
        Assert.True(session.Lessons.Engaged);
        Assert.Equal("personas", session.Lessons.Active?.Id);
        var step = session.Lessons.StepNumber;
        session.EndTalk();
        Assert.True(session.Lessons.Engaged); // a lesson that starts by itself stays engaged when its panel closes: its step waits
        Assert.False(session.CanSleep); // so the fire offers no sleep yet

        var loaded = LoadOf(SaveOf(session), session);
        Sleep(session); // the game would not offer it; the sleep itself is still a load

        SameAsALoad(loaded, session);
        Assert.Equal(LessonStatus.InProgress, session.Lessons.StatusOf("personas"));
        Assert.Equal(step, session.Lessons.StepNumber);
        Assert.False(session.Lessons.Engaged); // it takes up again when the learner talks to the villager
        Assert.True(session.CanSleep);
        Assert.Equal("The lesson “Personas” is under way: talk to the villager to go on", string.Concat(session.ObjectiveHud().Select(static p => p.Text)));
    }

    [Fact]
    public void ASleepOfASkippedGameGivesTheSaveOfALoadAndStaysMarked()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(session, 5);

        var loaded = LoadOf(SaveOf(session), session);
        Sleep(session);

        SameAsALoad(loaded, session);
        Assert.Equal(6, session.Day);
        Assert.Equal(5, session.SkippedTo);
    }

    [Fact]
    public void ASleepMovesNoStoredTimeAndLiftsTheSpacingGaps()
    {
        var session = EndedFirstSession();
        session.OpenTalk("villager");
        session.Say("¿dónde está el río?");
        session.EndTalk();
        var phrase = session.PhraseProgress.Records.First(static r => r.CreditedUses > 0);
        var time = session.StoredTime;
        var credit = session.Credit;
        Assert.DoesNotContain(session.PhraseProgress.DuePhrases(time.UnixSeconds, credit), d => d.PhraseId == phrase.Id); // inside its ten-minute gap

        Sleep(session);

        Assert.Equal(time, session.StoredTime); // a load adds nothing to the stored time (README, "Time rule")
        Assert.Equal(2, session.Words.Session);
        Assert.Equal(2, session.PhraseProgress.Session);
        Assert.Contains(session.PhraseProgress.DuePhrases(time.UnixSeconds, credit), d => d.PhraseId == phrase.Id); // a new session lifts the gap (D-game-6)
    }

    [Fact]
    public void ASleepIsRefusedWithASaveThatIsNotTheSessionsOwnTakenNow()
    {
        var session = EndedFirstSession();
        var document = SaveGame.Capture(session, Pristine());
        session.Step([]);

        Assert.Throws<ArgumentException>(() => SaveGame.Sleep(session, document)); // a tick later: not the session as it is now
        Assert.Throws<ArgumentException>(() => SaveGame.Sleep(session, SaveGame.Capture(session, Pristine()) with { Session = 3 }));
        Assert.Equal(1, session.Day);
    }

    [Fact]
    public void TwoSleepsGiveTheSaveOfTwoLoads()
    {
        var session = EndedFirstSession();
        var twice = LoadOf(SaveOf(LoadOf(SaveOf(session), session)), session);

        Sleep(session);
        Sleep(session);

        Assert.Equal(3, session.Day);
        Assert.Equal(SaveOf(twice), SaveOf(session));
    }
}
