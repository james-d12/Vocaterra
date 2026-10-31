using System.Globalization;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Nlp.Es;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>
/// The lesson "Ayer y mañana" of session 3 (S27, D-game-19): the near future with <c>ir a</c> and an infinitive, a few preterite forms, the time words, the present of <c>ir</c>;
/// its walk from the villager to the shopkeeper, the tense check of its answer steps (next to S26's person check), the forms it declares (the near future as a periphrastic form),
/// and the fix that a lesson miss is not credited as a use of the phrase.
/// </summary>
public sealed class TenseLessonTests(ITestOutputHelper output)
{
    private const string LessonId = "ayer-y-manana";

    private static LessonBook Book => TestGame.NewSessionWithShippedLessons().Lessons.Book;

    private static VoxelSession SessionThreeAtVillager()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.PlaySession = 3;
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");
        return session;
    }

    // Personas done, so that the lesson of session 3 is the villager's offer (the lessons of earlier sessions come first).
    private static void CompletePersonasSilently(VoxelSession session)
    {
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");
        Assert.Equal("personas", session.Lessons.Current is null ? null : session.Lessons.Active!.Id);
        while (session.Lessons.StatusOf("personas") == LessonStatus.InProgress)
        {
            if (session.Lessons.Current is { Id: "p2-walk" })
            {
                session.EndTalk();
                Stage.TeleportToNpc(session, "shopkeeper");
                _ = session.OpenTalk("shopkeeper");
                continue;
            }

            _ = LessonDriver.DoStep(session);
        }

        session.EndTalk();
    }

    private static VoxelSession AtStep(string stepId)
    {
        var session = SessionThreeAtVillager();
        while (session.Lessons.Current is { } step && step.Id != stepId)
        {
            if (step.Id == "f3-walk")
            {
                session.EndTalk();
                Stage.TeleportToNpc(session, "shopkeeper");
                _ = session.OpenTalk("shopkeeper");
                continue;
            }

            _ = LessonDriver.DoStep(session);
        }

        return session;
    }

    private static IEnumerable<PhraseUse> Uses(VoxelSession session, string phrase) =>
        session.Events.OfType<PhraseUse>().Where(e => e.PhraseId == phrase);

    // ---- The lesson

    [Fact]
    public void AyerYMananaIsTheVillagersLessonOfSessionThreeWithTheShopkeeperAsItsSecondNpc()
    {
        var lesson = Book.Find(LessonId)!;

        Assert.Equal(("villager", 3, true), (lesson.Npc, lesson.Session, lesson.AutoStart));
        Assert.Contains("Ayer y mañana", lesson.Objective, StringComparison.Ordinal); // the objective names it
        Assert.StartsWith("Session 3", lesson.Objective, StringComparison.Ordinal);
        Assert.Equal(["villager", "shopkeeper"], lesson.Steps.Select(lesson.NpcOf).Distinct());
        Assert.Equal(["voy-a-comer-pan", "vamos-a-comer-fruta", "ayer-comi-fruta", "ayer-fui-al-rio", "ayer-bebi-agua", "voy-a-ir-al-rio"], lesson.TaughtPhrases);
        var walk = Assert.Single(lesson.Steps, static s => s.Type == LessonStepType.Do);
        Assert.Equal(("talk_opened", "shopkeeper", "tienda"), (walk.Event, walk.At, walk.Point)); // the walk from the villager to the stall
        Assert.Equal(LessonStepType.Recap, lesson.Steps[^1].Type);
        Assert.InRange(lesson.Steps.Count, 15, 24); // the lesson stays within ten minutes (the S26 lessons have 20 and 37 steps)
        foreach (var step in lesson.Steps.Where(static s => s.Line is not null))
        {
            Assert.Equal(lesson.NpcOf(step), step.Line!.Speaker);
        }

        var all = string.Join(" ", lesson.Steps.Select(static s => s.English + " " + s.Line?.Text));
        Assert.DoesNotContain("vosotros", all, StringComparison.OrdinalIgnoreCase);
        foreach (var time in new[] { "ayer", "hoy", "mañana" })
        {
            Assert.Contains(time, all, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void EveryLearnerStepOfTheLessonDeclaresItsPersonAndItsTense()
    {
        var expected = new Dictionary<string, (string Person, string Tense)>
        {
            ["voy-a-comer-pan"] = ("1sg", "ir-a"),
            ["vamos-a-comer-fruta"] = ("1pl", "ir-a"),
            ["ayer-comi-fruta"] = ("1sg", "pret"),
            ["ayer-fui-al-rio"] = ("1sg", "pret"),
            ["ayer-bebi-agua"] = ("1sg", "pret"),
            ["voy-a-ir-al-rio"] = ("1sg", "ir-a"),
        };

        var turns = Book.Find(LessonId)!.Steps.Where(static s => s.Type is LessonStepType.Repeat or LessonStepType.Answer).ToList(); // a review step asks session 2 again, judging no form (X06)

        Assert.Equal(expected.Keys, turns.Select(static s => s.Phrase!));
        foreach (var step in turns)
        {
            Assert.Equal(expected[step.Phrase!], (step.Person!, step.Tense!));
            Assert.False(string.IsNullOrWhiteSpace(step.PersonRetry), step.Id);
            Assert.False(string.IsNullOrWhiteSpace(step.TenseRetry), step.Id);
        }
    }

    [Fact]
    public void SessionThreeUnlocksTheLessonFirstAndTheObjectiveNamesItWhileSessionTwosLessonStaysOnOffer()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.PlaySession = 3;

        Assert.Equal(LessonId, session.Lessons.OfferFor("villager")!.LessonId); // the newest session's lesson first, even when session 2's is not done
        Assert.Equal(LessonId, session.Lessons.NewLesson!.Id);
        Assert.Equal(LessonId, session.Lessons.NewLesson!.Id);
        var piece = Assert.Single(session.ObjectiveHud());
        Assert.Equal("Session 3: go to the village and talk to the villager to start the lesson “Ayer y mañana”", piece.Text);
        Assert.Equal(ObjectivePieceKind.Support, piece.Kind);
        session.BeginPlaySession();
        var villager = session.Scene.Npc("villager").Feet;
        Assert.Equal(new GuideCell(villager.X, villager.Y, villager.Z), session.Pointer!.Target);

        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");
        Assert.Equal("f0-intro", session.Lessons.Current!.Id);
        var walked = false;
        while (session.Lessons.StatusOf(LessonId) == LessonStatus.InProgress)
        {
            if (session.Lessons.Current is { Id: "f3-walk" } && !walked)
            {
                walked = true;
                session.EndTalk();
                Stage.TeleportToNpc(session, "shopkeeper");
                _ = session.OpenTalk("shopkeeper");
                continue;
            }

            _ = LessonDriver.DoStep(session);
        }

        session.EndTalk();
        Assert.Equal("personas", session.Lessons.OfferFor("villager")!.LessonId); // and then session 2's lesson, still not done
        Assert.Equal("personas", session.Lessons.NewLesson!.Id);
    }

    [Fact]
    public void TheLessonIsNotOfferedBeforeSessionThree()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.PlaySession = 2;
        CompletePersonasSilently(session);

        Assert.Null(session.Lessons.OfferFor("villager"));
        Assert.Null(session.Lessons.NewLesson);
        Assert.Equal(LessonStatus.NotStarted, session.Lessons.StatusOf(LessonId));
    }

    [Fact]
    public void TheLessonRunsFromTheVillagerToTheShopkeeperWithTypedAnswersAndEveryStepEndsDone()
    {
        var session = SessionThreeAtVillager();
        Assert.True(session.Lessons.Engaged);
        Assert.Equal("f0-intro", session.Lessons.Current!.Id);
        var done = new List<LessonDriver.Done>();

        while (session.Lessons.Current is { } step && step.Id != "f3-walk")
        {
            done.Add(LessonDriver.DoStep(session));
        }

        Assert.Equal("Walk to the stall and talk to the shopkeeper", session.Lessons.HudLine);
        session.EndTalk();
        Stage.TeleportToNpc(session, "shopkeeper");
        var opening = session.OpenTalk("shopkeeper");
        Assert.Empty(opening.Lines); // the shopkeeper's lesson speaks first
        Assert.Equal("y0-intro", session.Lessons.Current!.Id);

        while (session.Lessons.StatusOf(LessonId) == LessonStatus.InProgress)
        {
            done.Add(LessonDriver.DoStep(session));
        }

        foreach (var d in done)
        {
            output.WriteLine($"{d.Id,-14} {d.Type,-8} {d.Sent,-26} {d.Turn?.Intent,-13} {d.Outcome}");
        }

        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf(LessonId));
        Assert.Equal(3, session.Lessons.Record.Single(static r => r.LessonId == LessonId).CompletedSession);
        Assert.All(done, static d => Assert.Equal(StepOutcome.Done, d.Outcome));
        var turns = done.Where(static d => d.Turn is not null).ToList();
        Assert.Equal(["Voy a comer pan", "Vamos a comer fruta", "Ayer comí fruta", "Ayer fui al río", "Ayer bebí agua", "Mañana voy a ir al río"], turns.Select(static d => d.Sent));
        Assert.All(turns, static d =>
        {
            Assert.Equal("state_action", d.Turn!.Intent);
            Assert.True(d.Turn.Communicated, d.Id);
            Assert.Empty(d.Turn.Errors);
            Assert.Empty(d.Turn.HostEvents); // a statement about an action hands nothing over
            Assert.True(d.Turn.LessonHandled, d.Id);
        });
        Assert.Equal(["villager", "villager", "shopkeeper", "shopkeeper", "shopkeeper", "shopkeeper"], turns.Select(static d => d.Turn!.Npc));
        Assert.Equal("personas", session.Lessons.OfferFor("villager")!.LessonId); // session 2's lesson, not done, is the only one left
        Assert.Equal(6, session.Events.OfType<PhraseUse>().Count(static u => u.Kind != PhraseUseKind.DontKnow && u.PhraseId is "voy-a-comer-pan" or "vamos-a-comer-fruta" or "ayer-comi-fruta" or "ayer-fui-al-rio" or "ayer-bebi-agua" or "voy-a-ir-al-rio"));
    }

    [Fact]
    public void ThePhraseCardsStayDownExceptAtTheRepeatStep()
    {
        var session = AtStep("y2-went");

        Assert.Equal(["ayer-fui-al-rio"], session.Support.Cards.Select(static c => c.PhraseId)); // a repeat step has its card up
        _ = LessonDriver.DoStep(session);
        Assert.Empty(session.Support.Cards); // an answer step has none
    }

    // ---- The tense check

    private static void AssertMiss(VoxelSession session, string said, string stepId, string reasonStart, string hintContains)
    {
        var attempts = session.Lessons.Attempts;
        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.True(turn.LessonHandled, said); // the lesson speaks: the retry line, not the NPC's reply...
        Assert.Null(turn.Feedback); // ...and not a recast that praises the learner's own wrong words ("¡Eso es! «Como pan»")
        Assert.DoesNotContain(turn.Errors, static e => e.Tag.StartsWith("conj.", StringComparison.Ordinal)); // the core flags nothing: no pronoun, no time word
        Assert.Equal(stepId, session.Lessons.Current!.Id); // a miss: the step stays
        Assert.Equal(attempts + 1, session.Lessons.Attempts);
        Assert.Contains(hintContains, session.Lessons.View!.Hint, StringComparison.Ordinal); // the retry names the form
        Assert.Contains(session.Events.OfType<LessonEvent>(), e => e.Kind == LessonEventKind.Retry && e.StepId == stepId && e.Detail!.StartsWith(reasonStart, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("como pan")]
    [InlineData("comí pan")]
    [InlineData("quiero comer pan")]
    public void ThePresentOrThePastAtTheNearFutureQuestionIsAMissWithATenseRetry(string said)
    {
        var session = AtStep("f2-ask");

        AssertMiss(session, said, "f2-ask", "wrong tense", "then “a”, then the infinitive");
        _ = session.Say("Voy a comer pan");

        Assert.Equal("f2-we", session.Lessons.Current!.Id);
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf(LessonId)[^1].Outcome);
    }

    [Fact]
    public void TheNosotrosFormAtTheFirstPersonQuestionIsAPersonMissAndPersonComesBeforeTense()
    {
        var session = AtStep("f2-ask");

        AssertMiss(session, "vamos a comer pan", "f2-ask", "wrong person", "like “voy”");
    }

    [Theory]
    [InlineData("como fruta", "wrong tense")]
    [InlineData("voy a comer fruta", "wrong tense")]
    [InlineData("comiste fruta", "wrong person")]
    public void TheWrongTenseOrPersonAtYesterdaysQuestionIsAMiss(string said, string reason)
    {
        var session = AtStep("y1-ate");

        AssertMiss(session, said, "y1-ate", reason, reason == "wrong tense" ? "“comí”" : "-í");
        _ = session.Say("Ayer comí fruta");

        Assert.Equal("y2-ask", session.Lessons.Current!.Id);
    }

    [Fact]
    public void FuiAndBebiAndVoyAIrAreEachChecked()
    {
        var went = AtStep("y2-went");
        AssertMiss(went, "voy al río", "y2-went", "wrong tense", "“fui”");
        AssertMiss(AtStep("y2-went"), "fuiste al río", "y2-went", "wrong person", "“fui”");

        var had = AtStep("y3-had");
        AssertMiss(had, "bebo agua", "y3-had", "wrong tense", "“bebí”");

        var tomorrow = AtStep("y6-tomorrow");
        AssertMiss(tomorrow, "fui al río", "y6-tomorrow", "wrong tense", "voy a");
        _ = tomorrow.Say("voy a ir al río");
        Assert.Equal("y7-table", tomorrow.Lessons.Current!.Id);
    }

    [Theory]
    [InlineData("mañana comí pan", "f2-ask", "f2-we")]
    [InlineData("voy a como pan", "f2-ask", "f2-we")]
    [InlineData("ayer como fruta", "y1-ate", "y2-ask")]
    [InlineData("ayer voy al río", "y2-went", "y4-you")]
    [InlineData("ayer bebo agua", "y3-had", "y5-contrast")]
    [InlineData("mañana fui al río", "y6-tomorrow", "y7-table")]
    public void ATenseNextToATimeWordIsTheCoresSlipAndTheStepStillEnds(string said, string stepId, string next)
    {
        var session = AtStep(stepId);

        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.Contains(turn.Errors, static e => e.Tag == "conj.tense"); // M49: the time word and the form disagree, or ir a is followed by a conjugated verb
        Assert.Equal(next, session.Lessons.Current!.Id); // a slip is the phrase tracking's business: the lesson goes on
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf(LessonId)[^1].Outcome);
    }

    [Fact]
    public void ThePersonSlipWithAPronounIsTheCoresToo()
    {
        var session = AtStep("y1-ate");

        var turn = session.Say("yo comiste fruta");

        Assert.Contains(turn.Errors, static e => e.Tag == "conj.person");
        Assert.Equal("y2-ask", session.Lessons.Current!.Id);
    }

    [Fact]
    public void AMissIsStillFailForwardAfterTheStepsAttempts()
    {
        var session = AtStep("f2-ask");

        _ = session.Say("como pan");
        _ = session.Say("comí pan");

        Assert.Equal("f2-we", session.Lessons.Current!.Id);
        Assert.Equal(StepOutcome.MovedOn, session.Lessons.OutcomesOf(LessonId)[^1].Outcome);
    }

    [Fact]
    public void AStepWithNoDeclaredTenseJudgesNothingOfIt()
    {
        const string Plain = """
            { "schemaVersion": 1, "lessons": [ { "id": "t", "npc": "guide", "level": "a1", "title": "T", "english": "E", "steps": [
              { "id": "say", "type": "repeat", "phrase": "voy-a-comer-pan", "prompt": "lesson-plan", "english": "Say it." },
              { "id": "end", "type": "explain", "english": "Done." } ] } ] }
            """;
        var session = TestGame.NewSession(lessons: TestGame.Lessons(Plain));
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        Assert.True(session.Lessons.Start("t"));

        _ = session.Say("como pan"); // no tense declared: the intent is all that counts

        Assert.Equal("end", session.Lessons.Current!.Id);
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf("t")[^1].Outcome);
    }

    // ---- Correct Spanish is never a miss (S27 decision): a plan said in the present is credited, with a note

    [Theory]
    [InlineData("f2-ask", "mañana como pan", "voy-a-comer-pan", "f2-we", "That is right too. Here we practise “voy a comer”: Voy a comer pan.")]
    [InlineData("f2-we", "mañana comemos fruta", "vamos-a-comer-fruta", "f2-they", "That is right too. Here we practise “vamos a comer”: Vamos a comer fruta.")]
    [InlineData("y6-tomorrow", "mañana voy al río", "voy-a-ir-al-rio", "y7-table", "That is right too. Here we practise “voy a ir”: Mañana voy a ir al río.")]
    public void ThePresentSaidForAPlanIsCreditedAndTheNextStepOpensWithAGentleNote(string stepId, string said, string phrase, string next, string note)
    {
        var session = AtStep(stepId);

        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.DoesNotContain(turn.Errors, static e => e.Tag.StartsWith("conj.", StringComparison.Ordinal));
        Assert.Equal(next, session.Lessons.Current!.Id); // the step ends: not a retry
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf(LessonId)[^1].Outcome);
        Assert.Equal(note, session.Lessons.View!.Hint);
        var use = Assert.Single(Uses(session, phrase)); // credited as a use of the phrase
        Assert.Equal(said, use.Line);
        Assert.DoesNotContain(session.Events.OfType<LessonEvent>(), e => e.Kind == LessonEventKind.Retry && e.StepId == stepId);
        Assert.Contains(session.Events.OfType<LessonEvent>(), e => e.Kind == LessonEventKind.StepDone && e.StepId == stepId && e.Detail == "present for a plan, credited"); // a distinct outcome in the log
    }

    [Fact]
    public void ThePresentWithNoFutureWordIsStillAMissAndThePastAfterTomorrowIsStillTheCoresSlip()
    {
        var session = AtStep("y6-tomorrow");

        AssertMiss(session, "voy al río", "y6-tomorrow", "wrong tense", "voy a");
        Assert.Empty(Uses(session, "voy-a-ir-al-rio"));
    }

    [Fact]
    public void YesterdaysYouFormComesRightAfterTheLearnerSaysFui()
    {
        var steps = Book.Find(LessonId)!.Steps.Select(static s => s.Id).ToList();

        Assert.Equal(["y2-ask", "y2-went", "y4-you", "y3-had", "y5-contrast"], steps.Skip(steps.IndexOf("y2-ask")).Take(5));
        Assert.DoesNotContain("f1-here", steps); // estamos and están left the lesson
    }

    // ---- A miss is not credited as a use of the phrase (S26 leftover)

    [Fact]
    public void AWrongPersonMissIsNotCreditedAsAUseOfThePhrase()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        while (session.Lessons.Current is { } step && step.Id != "u4v-say-want")
        {
            _ = LessonDriver.DoStep(session);
        }

        var before = Uses(session, "quiero-madera").Count();
        _ = session.Say("quieres madera");

        Assert.Equal("u4v-say-want", session.Lessons.Current!.Id); // a miss
        Assert.Equal(before, Uses(session, "quiero-madera").Count()); // no phrase use
        Assert.DoesNotContain(session.Events.OfType<PhraseUse>(), static e => e.PhraseId == "quiero-madera" && e.Line == "quieres madera");

        _ = session.Say("quiero madera");

        var uses = Uses(session, "quiero-madera").ToList();
        Assert.Equal(before + 1, uses.Count);
        Assert.Equal("quiero madera", uses[^1].Line);
        Assert.True(uses[^1].Correct);
    }

    [Fact]
    public void AWrongTenseMissIsNotCreditedAsAUseOfThePhraseAndTheRightAnswerIs()
    {
        var session = AtStep("y1-ate");
        var before = Uses(session, "ayer-comi-fruta").Count();
        var progressBefore = session.PhraseProgress.RecordOf("ayer-comi-fruta");

        _ = session.Say("como fruta");

        Assert.Equal(before, Uses(session, "ayer-comi-fruta").Count());
        Assert.Equal(progressBefore, session.PhraseProgress.RecordOf("ayer-comi-fruta")); // the phrase's progress did not move

        _ = session.Say("Ayer comí fruta");

        Assert.Equal(before + 1, Uses(session, "ayer-comi-fruta").Count());
    }

    [Fact]
    public void ASlipTheCoreFlagsIsStillAUseOfThePhraseButNotACorrectOne()
    {
        var session = AtStep("y1-ate");

        _ = session.Say("ayer como fruta");

        var use = Assert.Single(Uses(session, "ayer-comi-fruta"));
        Assert.False(use.Correct);
    }

    [Fact]
    public void AnAnswerOutsideAnyLessonStepIsNeverHeldBack()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");

        _ = session.Say("quiero agua, por favor");

        Assert.Single(Uses(session, "pedir-agua"));
    }

    // ---- The declared forms

    [Fact]
    public void TheLessonDeclaresTheIrPresentTheEstarFormsTheNearFutureAndThePreteriteAndNoneTwice()
    {
        var forms = Book.Find(LessonId)!.TaughtVerbs;

        Assert.Equal(
            [
                "ir 1 sg pres", "ir 2 sg pres", "ir 3 sg pres", "ir 3 pl pres",
                "comer 1 sg ir-a", "comer 2 sg ir-a", "comer 1 pl ir-a", "comer 3 pl ir-a", "ir 1 sg ir-a",
                "comer 1 sg pret", "comer 2 sg pret", "ir 1 sg pret", "ir 2 sg pret", "beber 1 sg pret", "beber 2 sg pret",
            ],
            forms.Select(Show));
        Assert.DoesNotContain(forms, static f => f is { Person: "2", Number: "pl" }); // no vosotros
        Assert.DoesNotContain(forms, static f => f.Tense == "fut"); // the simple future waits for A2
    }

    [Fact]
    public void TheNearFutureIsAPeriphrasticFormBuiltFromIrAndTheInfinitive()
    {
        var morphology = TestGame.Bundle.SpanishMorphology();

        Assert.Equal("voy a comer", new TaughtVerbForm("comer", "1", "sg", "ir-a").Text(morphology, TestGame.Data.Words.NearFuture));
        Assert.Equal("vamos a comer", new TaughtVerbForm("comer", "1", "pl", "ir-a").Text(morphology, TestGame.Data.Words.NearFuture));
        Assert.Equal("van a comer", new TaughtVerbForm("comer", "3", "pl", "ir-a").Text(morphology, TestGame.Data.Words.NearFuture));
        Assert.Equal("vas a hacer", new TaughtVerbForm("hacer", "2", "sg", "ir-a").Text(morphology, TestGame.Data.Words.NearFuture));
        Assert.Equal("voy a ir", new TaughtVerbForm("ir", "1", "sg", "ir-a").Text(morphology, TestGame.Data.Words.NearFuture));
        Assert.Equal("comí", new TaughtVerbForm("comer", "1", "sg", "pret").Text(morphology, TestGame.Data.Words.NearFuture));
        Assert.Equal("fuiste", new TaughtVerbForm("ir", "2", "sg", "pret").Text(morphology, TestGame.Data.Words.NearFuture));
        Assert.Equal("bebí", new TaughtVerbForm("beber", "1", "sg", "pret").Text(morphology, TestGame.Data.Words.NearFuture));
        Assert.Equal("voy", new TaughtVerbForm("ir", "1", "sg", "pres").Text(morphology, TestGame.Data.Words.NearFuture));
    }

    [Fact]
    public void TheTenseOfAStepAndOfAFormMustBeOneTheGameKnows()
    {
        string Lesson(string extra, string step) =>
            "{ \"schemaVersion\": 1, \"lessons\": [ { \"id\": \"t\", \"npc\": \"guide\", \"level\": \"a1\", \"title\": \"T\", \"english\": \"E\", " + extra + " \"steps\": [ " + step + " ] } ] }";
        const string Say = "{ \"id\": \"a\", \"type\": \"repeat\", \"phrase\": \"voy-a-comer-pan\", \"prompt\": \"lesson-plan\", \"english\": \"E\", ";

        Assert.Equal("ir-a", Assert.Single(TestGame.Lessons(Lesson("", Say + "\"tense\": \"ir-a\" }")).Lessons).Steps[0].Tense);
        Assert.Contains("tense", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("", Say + "\"tense\": \"future\" }"))).Message, StringComparison.Ordinal);
        Assert.Contains("tense", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("", "{ \"id\": \"a\", \"type\": \"explain\", \"english\": \"E\", \"tense\": \"pres\" }"))).Message, StringComparison.Ordinal);
        Assert.Single(TestGame.Lessons(Lesson("\"verbs\": [ { \"lemma\": \"comer\", \"person\": \"1\", \"number\": \"sg\", \"tense\": \"ir-a\" } ],", Say + "\"tense\": \"pret\" }")).Lessons);
        Assert.Contains("tense", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("\"verbs\": [ { \"lemma\": \"comer\", \"person\": \"1\", \"number\": \"sg\", \"tense\": \"past\" } ],", Say + "\"attempts\": 2 }"))).Message, StringComparison.Ordinal);
    }

    // ---- Replay and the save

    [Fact]
    public void TheLessonCanBePractisedAgainAtTheVillager()
    {
        var session = AtStep("f3-walk");
        session.EndTalk();
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");
        while (session.Lessons.StatusOf(LessonId) == LessonStatus.InProgress)
        {
            _ = LessonDriver.DoStep(session);
        }

        session.EndTalk();
        Assert.True(session.Lessons.RequestReplay(LessonId));
        Assert.Equal("Practise again: talk to the villager to start “Ayer y mañana: yesterday and tomorrow”", session.Lessons.ReplayHud);
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");

        Assert.Equal("f0-intro", session.Lessons.Current!.Id);
        Assert.Equal("villager", session.Lessons.CurrentNpc);
        Assert.True(session.Lessons.IsDone(LessonId));
    }

    [Fact]
    public void ALoadIntoSessionThreeUnlocksTheLessonAndTheSaveKeepsItsProgress()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.PlaySession = 2;
        CompletePersonasSilently(session);
        var text = SaveGame.Save(session, TestGame.Route());

        var loaded = SaveGame.Load(text, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped);

        Assert.Equal(3, loaded.PlaySession);
        Assert.Equal(LessonId, loaded.Lessons.NewLesson!.Id);
        Assert.NotNull(loaded.Pointer); // the beacon points at the villager

        Stage.TeleportToNpc(loaded, "villager");
        _ = loaded.OpenTalk("villager");
        for (var i = 0; i < 6; i++)
        {
            _ = LessonDriver.DoStep(loaded);
        }

        loaded.EndTalk();
        var half = SaveGame.Save(loaded, TestGame.Route());
        var again = SaveGame.Load(half, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.Equal(LessonStatus.InProgress, again.Lessons.StatusOf(LessonId));
        Assert.Equal(6, again.Lessons.OutcomesOf(LessonId).Count);
        Assert.Equal(half, SaveGame.Save(again, TestGame.Route()));
    }

    // ---- The content

    [Fact]
    public void TheNewRowsAreInTheSheetAndTheNewLinesAreInTheSpeechList()
    {
        var sheet = RouteScene.DataOf(TestGame.Bundle).Phrases;
        var added = new[]
        {
            "que-vas-a-comer", "voy-a-comer-pan", "que-vamos-a-comer", "vamos-a-comer-fruta", "que-comiste-ayer", "ayer-comi-fruta",
            "fuiste-al-rio-ayer", "ayer-fui-al-rio", "bebiste-agua-ayer", "ayer-bebi-agua", "que-vas-a-hacer", "voy-a-ir-al-rio",
        };

        Assert.All(added, id => Assert.NotNull(sheet.Find(id)));
        var speech = TestGame.Speech.ToDictionary(static l => l.LineId, StringComparer.Ordinal);
        Assert.Equal("villager", speech["guide.lesson.villager.plan.1"].Speaker);
        Assert.Equal("shopkeeper", speech["guide.lesson.shop.ate.1"].Speaker);
        Assert.Equal("shopkeeper", speech["prompt.lesson-tomorrow"].Speaker);
        Assert.Equal("villager", speech["prompt.lesson-plan-we"].Speaker);
        Assert.All(PendingSpeech.Ids, id => Assert.True(speech.ContainsKey(id), id));
    }

    [Fact]
    public void ThePromptsOfTheLessonAllowTheStatementAndNeverARequest()
    {
        var prompts = TestGame.Bundle.Prompts;

        foreach (var id in new[] { "lesson-plan", "lesson-plan-we", "lesson-ate", "lesson-went", "lesson-had", "lesson-tomorrow" })
        {
            Assert.Contains("state_action", prompts[id].AllowedIntentIds);
            Assert.DoesNotContain("request_item", prompts[id].AllowedIntentIds); // nothing is handed over
            Assert.DoesNotContain("state_have", prompts[id].AllowedIntentIds);
        }

        Assert.Equal("villager", prompts["lesson-plan"].AgentId);
        Assert.Equal("shopkeeper", prompts["lesson-ate"].AgentId);
    }

    [Fact]
    public void TheLessonsEnglishNeverHandsOverVosotrosAndNamesTheFormsItDeclares()
    {
        var lesson = Book.Find(LessonId)!;
        var text = string.Join(" ", lesson.Steps.Select(static s => s.English));

        foreach (var form in new[] { "voy", "vas", "va", "vamos", "van", "comí", "comiste", "bebí", "bebiste", "fui", "fuiste" })
        {
            Assert.Contains(form, text, StringComparison.Ordinal);
        }
    }

    private static string Show(TaughtVerbForm form) => string.Create(CultureInfo.InvariantCulture, $"{form.Lemma} {form.Person} {form.Number} {form.Tense}");
}
