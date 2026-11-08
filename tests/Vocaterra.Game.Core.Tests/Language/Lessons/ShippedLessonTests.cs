using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>
/// The guide's first lesson, <c>first-words</c> (S17, D-game-15), through the real core: the opening that holds no quiz, the six units in order with typed
/// answers, the guide speaking for herself through the lesson (her ordinary replies are not shown), the repeat and the slow repeat that do what they say, "No sé"
/// as a lesson phrase, the English that thins, the fail-forward paths and the phrases the lesson leaves on the support ladder.
/// </summary>
public sealed class ShippedLessonTests(ITestOutputHelper output)
{
    private static VoxelSession AtGuide()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalkForClass("guide");
        return session;
    }

    // The introduction done, then Luna's first class taken (the player says Sí to her offer).
    private static VoxelSession AtFirstClass()
    {
        var session = AtGuide();
        _ = LessonDriver.CompleteAll(session);
        session.EndTalk();
        _ = session.OpenTalkForClass("guide");
        Assert.Equal("first-class", session.Lessons.Active?.Id);
        return session;
    }

    [Fact]
    public void ThereIsNoQuizAtSpawnAndTheLessonWaitsForTheGuide()
    {
        var session = TestGame.NewSessionWithShippedLessons();

        Assert.Null(session.Lessons.View);
        Assert.False(session.Lessons.Engaged);
        Assert.Null(session.PhraseReview);
        Assert.Null(session.Review);
        Assert.Equal("meet-guide", session.CurrentObjective?.Id);
        Assert.Empty(session.Events.OfType<PhraseUse>());
        Assert.Equal("start", session.Data.Tips[0].When); // the opening is the support-language tips (the first is "look")
    }

    [Fact]
    public void TheLessonStartsByItselfWhenThePlayerFirstTalksToTheGuideAndHerOpeningIsTheLessonNotAQuestion()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "guide");

        var opening = session.OpenTalkForClass("guide");

        Assert.Empty(opening.Lines); // no "¿En qué te ayudo?" before the first step
        Assert.True(session.Lessons.Engaged);
        Assert.Equal("u1-welcome", session.Lessons.Current!.Id);
        Assert.True(session.GuideMet);
        Assert.Equal("camp", session.CurrentObjective?.Id);
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.GuideMet);
        Assert.Contains("Luna", session.Lessons.View!.English, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWholeLessonRunsWithTypedAnswersInSixUnitsAndEveryStepEndsDone()
    {
        var session = AtGuide();

        var steps = LessonDriver.CompleteAll(session);

        foreach (var step in steps)
        {
            output.WriteLine($"{step.Id,-14} {step.Type,-8} {step.Sent,-26} {step.Outcome,-8} {string.Join(" / ", step.Shown)}");
        }

        Assert.Equal(25, steps.Count); // the introduction: u4 and u4v became Luna's first class (L01)
        Assert.All(steps, static s => Assert.Equal(StepOutcome.Done, s.Outcome));
        var units = steps.Select(static s => s.Id[..2]).Distinct().ToList();
        Assert.Equal(["u1", "u2", "u3", "u5", "u6"], units);
        Assert.Equal(["u1-welcome", "u1-hello", "u1-say-hola", "u1-name"], steps.Take(4).Select(static s => s.Id));
        Assert.Equal("u6-recap", steps[^1].Id);
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("first-words"));
        Assert.Null(session.Lessons.Active);
        Assert.Equal("first-class", session.Lessons.OfferFor("guide")?.LessonId); // Luna's first class is offered, never started by itself
        Assert.False(session.Lessons.Engaged);
        Assert.Null(session.Lessons.HudLine);
    }

    [Fact]
    public void TheGuideSpeaksForHerselfThroughTheLessonSoHerOrdinaryRepliesAreNotShown()
    {
        var session = AtGuide();

        var steps = LessonDriver.CompleteAll(session);

        // A turn the lesson took shows nothing of the guide's chatter ("¿Qué buscas?", "A ver qué es.", "¡Muy bien!"): the lesson's next step is what she says.
        var turns = steps.Where(static s => s.Turn is not null && s.Id is not ("u5-again" or "u5-slow")).ToList();
        Assert.True(turns.Count >= 7);
        Assert.All(turns, s =>
        {
            Assert.True(s.Turn!.LessonHandled, s.Id);
            Assert.Empty(s.Shown);
            Assert.NotEmpty(s.Turn.Replies); // she did reply for the core: the lesson just does not show it
        });
    }

    [Fact]
    public void OtraVezAndMasDespacioDoWhatTheySayTheGuideRepeatsHerLastLineTheSecondTimeSlowly()
    {
        var session = AtGuide();
        LessonDriver.Done again = null!;
        LessonDriver.Done slower = null!;
        while (session.Lessons.Current is { } step)
        {
            var done = LessonDriver.DoStep(session);
            if (step.Id == "u5-again")
            {
                again = done;
            }
            else if (step.Id == "u5-slow")
            {
                slower = done;
                break;
            }
        }

        const string Fast = "Mira. La madera es para la antorcha.";
        Assert.Equal("Otra vez, por favor", again.Sent);
        Assert.Equal("ask_repeat", again.Turn!.Intent);
        Assert.Equal([Fast], again.Shown);
        Assert.False(again.Turn.SlowFollowUp);
        Assert.Equal("Más despacio, por favor", slower.Sent);
        Assert.Equal("ask_slower", slower.Turn!.Intent);
        Assert.Equal([Fast], slower.Shown);
        Assert.True(slower.Turn.SlowFollowUp);
        Assert.Contains(GuideEvents.Repeat, again.Turn.HostEvents);
        Assert.Contains(GuideEvents.RepeatSlow, slower.Turn.HostEvents);
        Assert.Equal(StepOutcome.Done, again.Outcome);
        Assert.Equal(StepOutcome.Done, slower.Outcome);
    }

    [Fact]
    public void TheRepeatWorksAnywhereWithTheGuideNotOnlyInTheLessonAndRepeatsWhatSheSaidLast()
    {
        var session = TestGame.NewSession(); // no lesson: her ordinary conversation
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalkForClass("guide");
        var first = session.Say("¿dónde está el árbol?");
        var lastLine = first.GuideLines[^1];

        var again = session.Say("otra vez, por favor");
        var slower = session.Say("más despacio, por favor");

        Assert.Equal("ask_repeat", again.Intent);
        Assert.False(again.LessonHandled);
        Assert.Equal(lastLine, again.GuideLines[^1]); // her last line, said again after her "Claro. Otra vez."
        Assert.Equal(2, again.GuideLines.Count);
        Assert.Equal(lastLine, slower.GuideLines[^1]);
        Assert.True(slower.SlowFollowUp);
        Assert.Equal(lastLine, session.LastLineOf("guide")); // asking again does not make "Claro. Otra vez." the line to repeat
    }

    [Fact]
    public void NoSeIsTheAnswerOfItsStepAndNeverAHelpRequestThereAndSaysTheGuidesAnswerNext()
    {
        var session = AtGuide();
        LessonDriver.Done quiz = null!;
        while (session.Lessons.Current is { } step)
        {
            var done = LessonDriver.DoStep(session);
            if (step.Id == "u5-quiz")
            {
                quiz = done;
                break;
            }
        }

        Assert.Equal(("No sé", "dont_know", StepOutcome.Done), (quiz.Sent, quiz.Turn!.Intent, quiz.Outcome));
        Assert.Equal("u5-answer", session.Lessons.Current!.Id);
        Assert.Equal("Esto es una piedra.", session.Lessons.Current.Line!.Text); // "No sé" is never wrong: she tells the answer
        var use = session.Events.OfType<PhraseUse>().Last(static u => u.PhraseId == "no-se");
        Assert.Equal(PhraseUseKind.DontKnow, use.Kind);
    }

    [Fact]
    public void TheLessonLeavesItsPhrasesOnTheSupportLadderWithTheEnglishOffTheCardAfterTheFirstCorrectUse()
    {
        var session = AtFirstClass();

        _ = LessonDriver.CompleteAll(session, "first-class");

        foreach (var id in new[] { "hola", "si", "que-es-esto", "si-por-favor", "otra-vez", "mas-despacio" })
        {
            var record = session.PhraseProgress.RecordOf(id);
            Assert.Equal(SupportLevel.CardAndAudio, record.Level); // the English rung went with the first correct use (S16); the next rungs need the spacing gaps
            Assert.True(record.Supported > 0, $"{id} was used with its card up");
            Assert.True(record.CardShown >= 1, id);
        }

        foreach (var id in new[] { "no", "gracias" })
        {
            var record = session.PhraseProgress.RecordOf(id);
            Assert.True(record.Unaided > 0, $"{id} was an answer step, unaided"); // no card was up: the explain step before it named the word
        }

        Assert.True(session.PhraseProgress.RecordOf("que-es-esto").Unaided > 0); // the second and third asks were answer steps
    }

    [Fact]
    public void AGarbledOrWrongAnswerNeverBlocksTheLessonItMovesOnAfterTheAttemptsAndIsNotALanguageFailure()
    {
        var session = AtGuide();
        _ = session.Lessons.Start("first-words");
        var guard = 0;
        while (session.Lessons.StatusOf("first-words") == LessonStatus.InProgress && guard++ < 400)
        {
            var step = session.Lessons.Current!;
            switch (step.Type)
            {
                case LessonStepType.Repeat or LessonStepType.Answer:
                    _ = session.Say("blah blah"); // not understood, twice: the step moves on
                    _ = session.Say("blah blah");
                    break;
                case LessonStepType.Listen:
                    _ = session.Lessons.Pick((step.Correct + 1) % step.Options.Count);
                    _ = session.Lessons.Pick((step.Correct + 1) % step.Options.Count);
                    _ = session.Lessons.Continue();
                    break;
                case LessonStepType.Do:
                    Assert.True(session.Lessons.Skip()); // skipping is always possible
                    break;
                default:
                    _ = session.Lessons.Continue();
                    break;
            }
        }

        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("first-words"));
        var outcomes = session.Lessons.OutcomesOf("first-words");
        Assert.Contains(outcomes, static o => o.Outcome == StepOutcome.MovedOn);
        Assert.DoesNotContain(session.Events.OfType<PhraseUse>(), static u => !u.Correct); // not understood is never counted as a mistake in the phrase
    }

    [Fact]
    public void EveryStepCanBeSkippedFromTheFirstToTheLast()
    {
        var session = AtGuide();
        _ = session.Lessons.Start("first-words");

        for (var guard = 0; session.Lessons.StatusOf("first-words") == LessonStatus.InProgress && guard < 100; guard++)
        {
            Assert.True(session.Lessons.Skip());
        }

        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("first-words"));
        Assert.All(session.Lessons.OutcomesOf("first-words"), static o => Assert.Equal(StepOutcome.Skipped, o.Outcome));
    }

    [Fact]
    public void TheFruitStepWaitsForTheLearnerToPickAFruitAndPointsTheBeaconAtABush()
    {
        var session = AtFirstClass();
        while (session.Lessons.Current is { } step && step.Id != "u4-pick")
        {
            _ = LessonDriver.DoStep(session);
        }

        Assert.Equal("u4-pick", session.Lessons.Current!.Id);
        Assert.Equal("fruta", session.Pointer!.Place); // the beacon is on the nearest berry bush
        Assert.Contains("berry bush", session.Lessons.HudLine, StringComparison.Ordinal);
        session.EndTalk(); // the player closes the panel to walk to the bush: a class at a do step stays engaged until the pickup (L01), and holds back nothing
        Assert.True(session.Lessons.Engaged);
        Assert.True(session.CanSave);

        // A pickup of something else does not end the step; a fruit does.
        session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, WorldEvents.ItemPickedUp, null, "madera"));
        Assert.Equal("u4-pick", session.Lessons.Current!.Id);
        session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, WorldEvents.ItemPickedUp, null, "fruta"));
        Assert.Equal("u4-thanks", session.Lessons.Current!.Id);
        Assert.Null(session.Lessons.HudLine);
    }

    [Fact]
    public void TheRecapListsEveryPhraseTheLessonTaught()
    {
        var session = AtGuide();
        while (session.Lessons.Current is { Type: not LessonStepType.Recap })
        {
            _ = LessonDriver.DoStep(session);
        }

        var view = session.Lessons.View!;

        Assert.Equal(LessonStepType.Recap, view.Type);
        Assert.Equal(7, view.RecapLines.Count);
        Assert.Contains("Hola. = Hello.", view.RecapLines);
        Assert.Contains("Más despacio, por favor. = More slowly, please.", view.RecapLines);
        Assert.Contains("¿Qué es esto? = What is this?", view.RecapLines);
    }

    [Fact]
    public void TheGuideNeverAsksTheLearnerForTheirName()
    {
        var lesson = TestGame.NewSessionWithShippedLessons().Lessons.Book.Find("first-words")!;
        var sheet = RouteScene.DataOf(TestGame.Bundle).Phrases;

        // "Me llamo Luna." is heard, not said (core finding M29d): no step has the learner say a name.
        Assert.Contains(lesson.Steps, static s => s.Line?.Text == "Me llamo Luna.");
        Assert.DoesNotContain(lesson.Steps, s => s.Phrase is { } id && sheet.Find(id)!.Spanish.Contains("llamo", StringComparison.OrdinalIgnoreCase));
        Assert.False(sheet.Find("me-llamo")!.IsLearnerPhrase);
    }
}
