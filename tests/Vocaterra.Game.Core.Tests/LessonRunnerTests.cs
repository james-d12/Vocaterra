using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Game.Core.Tests;

/// <summary>
/// The lesson runner of S16 through the real core: the sample lesson from the offer to the end, the seven step types in order, the skip and the help path of
/// every step, fail-forward (a failed or uncertain turn moves on, and is never a language failure), the phrase tracking of repeat and answer steps, the world
/// event a <c>do</c> step waits on, the save and the load, and a lesson that starts by itself against one the learner left.
/// </summary>
public sealed class LessonRunnerTests
{
    private static readonly PcmClip Clip = PcmClip.FromSamples(new short[1600]);

    private sealed class Scripted(string transcript, double confidence) : IWordRecognizer
    {
        public string ModelHash => "test-model";

        public AsrResult Recognize(PcmClip clip, RecognitionRequest request)
        {
            var heard = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var words = heard.Select((w, i) => new AsrWord(w, i * 0.3, (i * 0.3) + 0.25, confidence)).ToList();
            return new AsrResult(transcript, [new AsrHypothesis(transcript, 1.0)], words, 1.0, ModelHash, request.Mode);
        }
    }

    private static VoxelSession AtGuide(string? lessons = null)
    {
        var session = TestGame.NewSession(lessons: TestGame.Lessons(lessons ?? TestGame.SampleLesson)); // the retired S16 sample, kept here as the runner's plain two-step lesson
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        return session;
    }

    private static List<LessonEvent> Events(VoxelSession session) => session.Events.OfType<LessonEvent>().ToList();

    private static IEnumerable<(string Step, string Kind, StepOutcome? Outcome)> Trail(VoxelSession session) =>
        Events(session).Select(static e => (e.StepId, LessonEventNames.Of(e.Kind), e.Outcome));

    private static string LessonWith(string steps, string extra = "\"autoStart\": false") =>
        "{ \"schemaVersion\": 1, \"lessons\": [ { \"id\": \"t\", \"npc\": \"guide\", \"title\": \"T\", \"english\": \"E\", " + extra + ", \"steps\": [ " + steps + " ] } ] }";

    private const string Repeat = "{ \"id\": \"say\", \"type\": \"repeat\", \"phrase\": \"hola\", \"prompt\": \"guide-open\", \"english\": \"Say hello.\", \"help\": \"Say: Hola.\" }";
    private const string Answer = "{ \"id\": \"reply\", \"type\": \"answer\", \"phrase\": \"gracias\", \"prompt\": \"guide-open\", \"line\": \"guide_offer_yes.1\", \"translation\": \"Of course! Here you are.\", \"english\": \"Thank her.\" }";
    private const string Explain = "{ \"id\": \"read\", \"type\": \"explain\", \"english\": \"Read this.\" }";

    // ---- The sample, from the offer to the end.

    [Fact]
    public void TheSampleLessonIsOfferedAtTheGuideAndLeavesHerOrdinaryConversationAloneUntilItIsStarted()
    {
        var session = AtGuide();

        var offer = session.Lessons.OfferFor("guide");
        Assert.Equal(("sample", "A first hello", false), (offer!.LessonId, offer.Title, offer.Resume));
        Assert.Null(session.Lessons.OfferFor("shopkeeper"));
        Assert.False(session.Lessons.Engaged);
        Assert.Null(session.Lessons.ActivePrompt);
        Assert.Null(session.Lessons.View);

        var turn = session.Say("¿dónde está el río?");

        Assert.True(turn.Communicated);
        Assert.Equal("ask_directions", turn.Intent);
        Assert.Empty(Events(session)); // an ordinary question is not a lesson step
        Assert.Equal(LessonStatus.NotStarted, session.Lessons.StatusOf("sample"));
    }

    [Fact]
    public void TheSampleRunsRepeatThenAnswerAndEachStepAndItsOutcomeIsInTheEvents()
    {
        var session = AtGuide();
        Assert.True(session.Lessons.Start("sample"));

        var view = session.Lessons.View!;
        Assert.Equal((1, 2, LessonStepType.Repeat, "Say it", "Hola."), (view.StepNumber, view.StepCount, view.Type, view.Heading, view.Spanish));
        Assert.Equal("Say hello to the guide. “Hola” is how you greet anyone.", view.English);
        Assert.Equal("guide-open", session.Lessons.ActivePrompt);
        var card = Assert.Single(session.Support.Cards);
        Assert.Equal("hola", card.PhraseId);
        Assert.True(card.EnglishShown); // the first rung: the English under the Spanish
        Assert.Contains(session.Support.TakeCues(), static c => c is { Reason: "card", Text: "Hola." });

        var hello = session.Say("hola");

        Assert.True(hello.Communicated);
        Assert.Equal("guide-open", hello.Prompt);
        view = session.Lessons.View!;
        Assert.Equal((2, LessonStepType.Answer, "¡Claro! Aquí tienes.", "Of course! Here you are."), (view.StepNumber, view.Type, view.Spanish, view.Translation));
        Assert.Empty(session.Support.Cards); // an answer step shows no card: it is the test
        Assert.Contains(session.Support.TakeCues(), static c => c is { Reason: "lesson", Text: "¡Claro! Aquí tienes." }); // the NPC's line plays

        var thanks = session.Say("gracias");

        Assert.True(thanks.Communicated);
        Assert.Null(session.Lessons.View);
        Assert.Null(session.Lessons.Active);
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("sample"));
        Assert.Equal([("say-hola", StepOutcome.Done), ("say-gracias", StepOutcome.Done)], session.Lessons.OutcomesOf("sample"));
        Assert.Null(session.Lessons.OfferFor("guide")); // nothing left to offer
        Assert.Equal(
            [
                ("", "lesson_started", null),
                ("say-hola", "step_started", null),
                ("say-hola", "step_done", StepOutcome.Done),
                ("say-gracias", "step_started", null),
                ("say-gracias", "step_done", StepOutcome.Done),
                ("", "lesson_completed", null),
            ],
            Trail(session));
        Assert.Equal("lesson sample step 1 repeat (say-hola) done", Events(session)[2].Text);
        Assert.Equal("lesson sample completed (done 2, skipped 0, moved on 0)", Events(session)[^1].Text);
    }

    [Fact]
    public void TheSampleCountsTheRepeatAsASupportedUseAndTheAnswerAsAnUnaidedOne()
    {
        var session = AtGuide();
        _ = session.Lessons.Start("sample");

        _ = session.Say("hola");
        _ = session.Say("gracias");

        var uses = session.Events.OfType<PhraseUse>().ToList();
        Assert.Equal([("hola", PhraseUseKind.Supported), ("gracias", PhraseUseKind.Unaided)], uses.Select(static u => (u.PhraseId, u.Kind)));
        Assert.All(uses, static u => Assert.Equal("guide", u.Npc));
        var hola = session.PhraseProgress.RecordOf("hola");
        var gracias = session.PhraseProgress.RecordOf("gracias");
        Assert.Equal((6, 0, 1), (hola.Supported, hola.Unaided, hola.CardShown));
        Assert.Equal((0, 6), (gracias.Supported, gracias.Unaided));
        Assert.Equal(SupportLevel.CardAndAudio, hola.Level); // the English rung went with the first correct use
        Assert.Equal(SupportLevel.CardAndAudio, gracias.Level); // and the answer was unaided, so it fades the same way
    }

    [Fact]
    public void HelpBeforeTheAnswerMakesItASupportedUse()
    {
        var session = AtGuide();
        _ = session.Lessons.Start("sample");
        _ = session.Say("hola");

        Assert.True(session.Lessons.Help());
        _ = session.Say("gracias");

        Assert.Equal(PhraseUseKind.Supported, session.Events.OfType<PhraseUse>().Last().Kind);
        Assert.Equal([("say-hola", StepOutcome.Done), ("say-gracias", StepOutcome.Done)], session.Lessons.OutcomesOf("sample"));
    }

    // ---- The seven step types, in order.

    [Fact]
    public void ALessonWithEveryStepTypeRunsTheStepsInOrder()
    {
        var session = AtGuide(LessonBookTests.Rich);
        Assert.True(session.Lessons.Start("rich"));
        var seen = new List<(LessonStepType Type, bool CanContinue)>();

        void Note() => seen.Add((session.Lessons.View!.Type, session.Lessons.View.CanContinue));

        Note();
        Assert.True(session.Lessons.Continue()); // explain
        Note();
        Assert.Contains(session.Support.TakeCues(), static c => c is { Reason: "lesson", Text: "Hola.", LineId: "phrase.hola" }); // the model plays its WAV
        Assert.Equal(("Hola.", "Hello."), (session.Lessons.View!.Spanish, session.Lessons.View.Translation));
        Assert.True(session.Lessons.Continue()); // model
        Note();
        Assert.False(session.Lessons.Continue()); // a listen step waits for a pick
        Assert.True(session.Lessons.Pick(0));
        Assert.True(session.Lessons.View!.CanContinue);
        Assert.True(session.Lessons.Continue()); // listen
        Note();
        _ = session.Say("hola"); // repeat
        Note();
        _ = session.Say("gracias"); // answer
        Note();
        Assert.False(session.Lessons.Continue()); // a do step waits for the world
        session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, WorldEvents.ItemPickedUp, null, "madera")); // not the fruit
        Assert.Equal(LessonStepType.Do, session.Lessons.View!.Type);
        session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, WorldEvents.ItemPickedUp, null, "fruta"));
        Note();
        Assert.Equal("Well done.", session.Lessons.View!.English);
        Assert.True(session.Lessons.Continue()); // recap

        Assert.Equal(
            [
                (LessonStepType.Explain, true), (LessonStepType.Model, true), (LessonStepType.Listen, false), (LessonStepType.Repeat, false),
                (LessonStepType.Answer, false), (LessonStepType.Do, false), (LessonStepType.Recap, true),
            ],
            seen);
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("rich"));
        Assert.All(session.Lessons.OutcomesOf("rich"), static o => Assert.Equal(StepOutcome.Done, o.Outcome));
        Assert.Equal(7, session.Lessons.OutcomesOf("rich").Count);
        Assert.Equal(
            ["read", "hear", "pick", "say", "reply", "get", "sum"],
            Events(session).Where(static e => e.Kind == LessonEventKind.StepDone).Select(static e => e.StepId));
    }

    [Fact]
    public void TheRecapListsThePhrasesThePhraseStepsTeach()
    {
        var session = AtGuide(LessonBookTests.Rich);
        _ = session.Lessons.Start("rich");
        for (var i = 0; i < 6; i++)
        {
            _ = session.Lessons.Skip();
        }

        var view = session.Lessons.View!;
        Assert.Equal(LessonStepType.Recap, view.Type);
        Assert.Equal(["Hola. = Hello.", "Gracias. = Thank you."], view.RecapLines);
    }

    [Fact]
    public void AListenStepMovesOnWithTheRightMeaningShownAfterItsAttemptsRunOut()
    {
        var session = AtGuide(LessonBookTests.Rich);
        _ = session.Lessons.Start("rich");
        _ = session.Lessons.Skip();
        _ = session.Lessons.Skip(); // the listen step now

        Assert.True(session.Lessons.Pick(1));
        Assert.False(session.Lessons.View!.CanContinue);
        Assert.Equal("Not quite. Try once more, or press H for help.", session.Lessons.View.Hint);
        Assert.True(session.Lessons.Pick(2));

        var view = session.Lessons.View!;
        Assert.True(view.CanContinue);
        Assert.Equal("Hello. (Hello)", view.Hint); // the line's meaning and the right option
        Assert.False(session.Lessons.Pick(0)); // resolved
        Assert.True(session.Lessons.Continue());
        Assert.Equal(StepOutcome.MovedOn, session.Lessons.OutcomesOf("rich")[^1].Outcome);
        Assert.Equal(LessonStepType.Repeat, session.Lessons.View!.Type);
    }

    // ---- Skip and help.

    [Fact]
    public void EveryStepOfEveryTypeCanBeSkipped()
    {
        var session = AtGuide(LessonBookTests.Rich);
        _ = session.Lessons.Start("rich");

        for (var i = 0; i < 7; i++)
        {
            Assert.True(session.Lessons.Skip(), $"step {i + 1}");
        }

        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("rich"));
        Assert.All(session.Lessons.OutcomesOf("rich"), static o => Assert.Equal(StepOutcome.Skipped, o.Outcome));
        Assert.Equal("lesson rich completed (done 0, skipped 7, moved on 0)", Events(session)[^1].Text);
        Assert.Empty(session.Events.OfType<PhraseUse>()); // a skip is not a use of the phrase, and not a failure of any kind
        Assert.False(session.Lessons.Skip()); // nothing is engaged any more
    }

    [Fact]
    public void EveryStepHasAHelpPathThatShowsTheEnglishHintIsNeverAnAttemptAndNeverEndsTheStep()
    {
        var session = AtGuide(LessonBookTests.Rich);
        _ = session.Lessons.Start("rich");

        for (var i = 0; i < 7; i++)
        {
            var step = session.Lessons.Current!;
            Assert.True(session.Lessons.Help(), step.Id);

            var view = session.Lessons.View!;
            Assert.False(string.IsNullOrWhiteSpace(view.Hint), step.Id);
            Assert.Equal(step.Help ?? "Listen to the guide again, then say it as shown on the card. You can press K to skip this step.", view.Hint);
            Assert.Equal(0, session.Lessons.Attempts);
            Assert.True(session.Lessons.Helped);
            Assert.Equal(step.Id, session.Lessons.Current!.Id); // still on the same step
            _ = session.Lessons.Skip();
        }

        Assert.Equal(7, Events(session).Count(static e => e.Kind == LessonEventKind.Help));
    }

    [Fact]
    public void TheHelpPathOfARepeatStepShowsTheCardAndTheEnglishAtAnyRungAndPlaysTheModelAgain()
    {
        var session = AtGuide(LessonWith(Repeat));
        _ = session.Lessons.Start("t");
        _ = session.Say("hola"); // the lesson ends; start it again and the rung is the card
        Assert.Equal(SupportLevel.CardAndAudio, session.PhraseProgress.LevelOf("hola"));
        Assert.True(session.Lessons.Start("t")); // a completed lesson starts again
        var shown = Assert.Single(session.Support.Cards);
        Assert.False(shown.EnglishShown); // rung 2: the English is on the help key
        _ = session.Support.TakeCues();

        Assert.True(session.Lessons.Help());

        Assert.True(Assert.Single(session.Support.Cards).EnglishShown);
        Assert.Equal("Say: Hola.", session.Lessons.View!.Hint);
    }

    [Fact]
    public void AskingForHelpOrSayingNoSeInTheLessonIsNeverAnAttemptAndNeverShamed()
    {
        var session = AtGuide(LessonWith(Repeat + "," + Answer));
        _ = session.Lessons.Start("t");

        var help = session.Say("ayuda");
        var unknown = session.Say("no sé");

        Assert.True(help.Communicated && unknown.Communicated);
        Assert.Equal(0, session.Lessons.Attempts);
        Assert.Equal("say", session.Lessons.Current!.Id);
        Assert.Equal("Say: Hola.", session.Lessons.View!.Hint);
        Assert.Equal([PhraseUseKind.DontKnow, PhraseUseKind.DontKnow], session.Events.OfType<PhraseUse>().Select(static u => u.Kind));
        Assert.DoesNotContain(Events(session), static e => e.Kind == LessonEventKind.Retry);
        Assert.Equal(2, Events(session).Count(static e => e.Kind == LessonEventKind.Help));
    }

    [Fact]
    public void TheNoSeButtonInALessonIsAHelpPathAndATrackedNoSe()
    {
        var session = AtGuide(LessonWith(Repeat));
        _ = session.Lessons.Start("t");

        Assert.True(session.Lessons.HandlesHelp);
        Assert.True(session.Lessons.DontKnow());

        var use = Assert.Single(session.Events.OfType<PhraseUse>());
        Assert.Equal((PhraseIds.DontKnow, PhraseUseKind.DontKnow, InputKind.Button), (use.PhraseId, use.Kind, use.Input));
        Assert.Equal(["hola"], use.InPlay);
        Assert.Equal(1, session.PhraseProgress.RecordOf("hola").DontKnow);
        Assert.Contains(session.Events.OfType<VoiceEvent>(), static e => e.Kind == VoiceEventKind.NoSe);
        Assert.Equal("say", session.Lessons.Current!.Id); // and the step stays: the learner can still say it, or skip
    }

    // ---- Fail-forward (D-game-9).

    [Fact]
    public void ATurnThePhraseCouldNotBeHeardInIsAnAttemptAndAfterTheAttemptsTheStepMovesOn()
    {
        var session = AtGuide(LessonWith(Repeat + "," + Answer));
        _ = session.Lessons.Start("t");

        _ = session.Say("zzz qqq");

        Assert.Equal(("say", 1), (session.Lessons.Current!.Id, session.Lessons.Attempts));
        Assert.Equal("Not quite. Try once more, or press H for help.", session.Lessons.View!.Hint);
        Assert.Contains(Events(session), static e => e.Kind == LessonEventKind.Retry);

        _ = session.Say("zzz qqq");

        Assert.Equal("reply", session.Lessons.Current!.Id); // it moved on
        Assert.Equal([("say", StepOutcome.MovedOn)], session.Lessons.OutcomesOf("t"));
        Assert.Equal("lesson t step 1 repeat (say) moved_on (not understood)", Events(session).Single(static e => e.Kind == LessonEventKind.StepDone).Text);
        Assert.Empty(session.Events.OfType<PhraseUse>()); // no phrase use, no error, no failure
        Assert.Equal(0, session.PhraseProgress.RecordOf("hola").Slips);
        Assert.Equal(SupportLevel.EnglishAndCard, session.PhraseProgress.LevelOf("hola")); // and the ladder did not move
    }

    [Fact]
    public void AStepWithOneAttemptMovesOnAtTheFirstFailure()
    {
        var session = AtGuide(LessonWith(Repeat.Replace("\"english\"", "\"attempts\": 1, \"english\"", StringComparison.Ordinal) + "," + Answer));
        _ = session.Lessons.Start("t");

        _ = session.Say("zzz qqq");

        Assert.Equal("reply", session.Lessons.Current!.Id);
        Assert.Equal([("say", StepOutcome.MovedOn)], session.Lessons.OutcomesOf("t"));
    }

    [Fact]
    public void AnotherPhraseThanTheOneAskedForIsAnAttemptToo()
    {
        var session = AtGuide(LessonWith(Answer + "," + Explain));

        _ = session.Lessons.Start("t");
        var wrong = session.Say("adiós");

        Assert.True(wrong.Communicated); // understood, but not the phrase this step asks for
        Assert.Equal(("reply", 1), (session.Lessons.Current!.Id, session.Lessons.Attempts));
        _ = session.Say("gracias");
        Assert.Equal("read", session.Lessons.Current!.Id);
        Assert.Equal([("reply", StepOutcome.Done)], session.Lessons.OutcomesOf("t"));
    }

    [Fact]
    public void ARepeatOrAnswerStepIsEvaluatedAtTheBundlePromptItNamesNotTheNpcsOwn()
    {
        var villagers = Repeat.Replace("\"prompt\": \"guide-open\"", "\"prompt\": \"villager-open\"", StringComparison.Ordinal);
        var session = AtGuide(LessonWith(villagers + "," + Explain));
        _ = session.Lessons.Start("t");

        Assert.Equal("villager-open", session.Lessons.ActivePrompt);
        var turn = session.Say("hola");

        Assert.Equal(("guide", "villager-open"), (turn.Npc, turn.Prompt)); // the guide's turn, at the prompt the lesson step names
        Assert.Equal("read", session.Lessons.Current!.Id);
        Assert.Null(session.Lessons.ActivePrompt); // an explain step waits on no turn
    }

    [Fact]
    public void ASlipInThePhraseIsTheTrackersBusinessAndTheStepStillEnds()
    {
        var session = AtGuide(LessonWith("{ \"id\": \"ask\", \"type\": \"repeat\", \"phrase\": \"pedir-agua\", \"prompt\": \"stall-need\", \"english\": \"Ask for water.\" }," + Explain));
        _ = session.Lessons.Start("t");

        var turn = session.Say("yo quieres agua, por favor");

        Assert.NotEmpty(turn.Errors);
        Assert.Equal("read", session.Lessons.Current!.Id);
        Assert.Equal("with a slip", Events(session).Single(static e => e.Kind == LessonEventKind.StepDone).Detail);
        Assert.Equal(1, session.PhraseProgress.RecordOf("pedir-agua").Slips); // logged as a slip, never as a success
        Assert.Equal(0, session.PhraseProgress.RecordOf("pedir-agua").Supported);
    }

    [Fact]
    public void AnUncertainRecognitionIsAVoiceEventAndAnAttemptNeverALanguageFailure()
    {
        var session = AtGuide(LessonWith(Repeat + "," + Answer));
        _ = session.Lessons.Start("t");
        session.Conversation.AttachSpeech(new SpokenUtteranceEvaluator(TestGame.Bundle, new Scripted("hola", 0.2)));

        var unsure = Spoken(session);

        Assert.True(unsure.Turn!.Unsure);
        Assert.Equal(("say", 1), (session.Lessons.Current!.Id, session.Lessons.Attempts));
        Assert.Single(session.Events.OfType<VoiceEvent>(), static e => e.Kind == VoiceEventKind.Uncertain);
        Assert.Empty(session.Events.OfType<PhraseUse>());
        Assert.Equal(0, session.Words.RecordOf("hola").CreditUnits);

        session.Conversation.AttachSpeech(new SpokenUtteranceEvaluator(TestGame.Bundle, new Scripted("hola", 0.95)));
        var sure = Spoken(session);

        Assert.True(sure.Turn!.Communicated);
        Assert.Equal("reply", session.Lessons.Current!.Id);
        Assert.Equal(InputKind.Spoken, session.Events.OfType<PhraseUse>().Single().Input);
        Assert.Equal([("say", StepOutcome.Done)], session.Lessons.OutcomesOf("t"));
    }

    [Fact]
    public void APickAmongTheOfferedChoicesMovesTheLessonOnAsRecognitionOnly()
    {
        var session = AtGuide(LessonWith(Repeat + "," + Answer));
        _ = session.Lessons.Start("t");
        var scene = new GuideScene(TestGame.ViewpointOf(session), PromptOverride: session.Lessons.ActivePrompt);
        var turn = session.Conversation.Say("guide", "hola", scene, session.StoredTime, session.Simulation.ComputeStateDigest());
        var request = new TurnRequest(1, TurnSource.Typed, "hola", null, null, scene, session.StoredTime, session.Simulation.ComputeStateDigest(), null, session.Simulation.Tick, "guide");

        _ = session.Apply(new TurnOutcome(request, turn with { Choice = true, Input = InputKind.Button }, null, new TurnTimings(0, 0, 0, false)));

        Assert.Equal([("say", StepOutcome.MovedOn)], session.Lessons.OutcomesOf("t"));
        Assert.Equal(PhraseUseKind.Choice, session.Events.OfType<PhraseUse>().Single().Kind);
        Assert.Equal(0, session.PhraseProgress.RecordOf("hola").Supported);
    }

    // ---- The world.

    [Fact]
    public void ADoStepWaitsOnTheWorldEventTheGameRaisesAndEndsWithIt()
    {
        var session = TestGame.NewSession(lessons: TestGame.Lessons(LessonWith("{ \"id\": \"get\", \"type\": \"do\", \"event\": \"item_picked_up\", \"detail\": \"madera\", \"english\": \"Collect some wood.\" }," + Explain)));
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        _ = session.Lessons.Start("t");
        Assert.True(session.Lessons.View!.WaitsOnWorld);

        var (played, _, _) = CampObjectiveTests.PlayCamp();
        Assert.Contains(played.Events.OfType<WorldEvent>(), static e => e is { Name: WorldEvents.ItemPickedUp, Detail: "madera" }); // the game raises it on a real pickup
        Assert.Contains(played.Events.OfType<WorldEvent>(), static e => e is { Name: WorldEvents.ItemPickedUp, Detail: "fruta" });

        session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, WorldEvents.BottleFilled, null, "agua"));
        Assert.Equal("get", session.Lessons.Current!.Id);
        session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, WorldEvents.ItemPickedUp, null, "madera"));
        Assert.Equal("read", session.Lessons.Current!.Id);
        Assert.Equal("madera", Events(session).Single(static e => e is { Kind: LessonEventKind.StepDone, StepId: "get" }).Detail);
    }

    // ---- Engaged or not.

    [Fact]
    public void ALessonTheLearnerLeftIsPausedAndDoesNotTakeOverTheGuidesConversationButIsOfferedToResume()
    {
        var session = AtGuide();
        _ = session.Lessons.Start("sample");
        _ = session.Say("hola");
        session.EndTalk();

        Assert.False(session.Lessons.Engaged);
        Assert.Equal(LessonStatus.InProgress, session.Lessons.StatusOf("sample"));
        _ = session.OpenTalk("guide");
        Assert.False(session.Lessons.Engaged);
        Assert.Null(session.Lessons.ActivePrompt);
        Assert.True(session.Lessons.OfferFor("guide")!.Resume);
        var ordinary = session.Say("¿dónde está el río?");
        Assert.Equal(("ask_directions", "guide-open"), (ordinary.Intent, ordinary.Prompt));
        Assert.Equal("say-gracias", session.Lessons.Current!.Id); // untouched

        Assert.True(session.Lessons.Start("sample")); // resumes where it stopped
        Assert.Equal(2, session.Lessons.View!.StepNumber);
        _ = session.Say("gracias");
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("sample"));
    }

    [Fact]
    public void ALessonThatStartsByItselfStartsAtTheFirstTalkAndTakesUpAgainAtTheNext()
    {
        var auto = LessonWith(Repeat + "," + Answer, "\"autoStart\": true");
        var session = TestGame.NewSession(lessons: TestGame.Lessons(auto));
        Stage.TeleportToNpc(session, "guide");

        _ = session.OpenTalk("guide");

        Assert.True(session.Lessons.Engaged);
        Assert.Equal(1, session.Lessons.StepNumber);
        _ = session.Say("hola");
        session.EndTalk();
        Assert.True(session.Lessons.Active is not null);
        _ = session.OpenTalk("guide");
        Assert.True(session.Lessons.Engaged);
        Assert.Equal(2, session.Lessons.StepNumber);
        Assert.Equal("guide-open", session.Lessons.ActivePrompt);
    }

    [Fact]
    public void AnAutoStartLessonHasTheGuidesConversationSoNoReviewQuestionCompetesWithItAndAnOfferedOneLeavesTheReviewToAsk()
    {
        static VoxelSession WithDuePhrase(LessonBook? lessons)
        {
            var session = TestGame.NewSession(lessons: lessons);
            Stage.TeleportToNpc(session, "shopkeeper");
            _ = session.OpenTalk("shopkeeper");
            _ = session.Say("quiero agua, por favor");
            session.EndTalk();
            session.SkipTime(11 * 60); // the phrase is due for the guide's review
            Stage.TeleportToNpc(session, "guide");
            return session;
        }

        var auto = WithDuePhrase(TestGame.Lessons(LessonWith(Repeat + "," + Answer, "\"autoStart\": true")));
        _ = auto.OpenTalk("guide");
        Assert.Null(auto.PhraseReview);
        Assert.True(auto.Lessons.Engaged);

        var offered = WithDuePhrase(TestGame.Lessons(TestGame.SampleLesson));
        _ = offered.OpenTalk("guide");
        Assert.NotNull(offered.PhraseReview); // the sample is only offered: the review asks as before
        Assert.False(offered.Lessons.Engaged);
        Assert.NotNull(offered.Lessons.OfferFor("guide"));
    }

    [Fact]
    public void ALessonAfterAnotherIsOfferedOnlyOnceTheFirstIsCompleted()
    {
        var json = "{ \"schemaVersion\": 1, \"lessons\": ["
            + "{ \"id\": \"one\", \"npc\": \"guide\", \"title\": \"One\", \"english\": \"E\", \"steps\": [ " + Explain + " ] },"
            + "{ \"id\": \"two\", \"npc\": \"guide\", \"title\": \"Two\", \"english\": \"E\", \"after\": \"one\", \"steps\": [ " + Explain + " ] } ] }";
        var session = AtGuide(json);

        Assert.Equal("one", session.Lessons.OfferFor("guide")!.LessonId);
        _ = session.Lessons.Start("one");
        Assert.False(session.Lessons.Start("two")); // one lesson at a time
        _ = session.Lessons.Continue();

        Assert.Equal("two", session.Lessons.OfferFor("guide")!.LessonId);
        Assert.False(session.Lessons.Start("nope"));
    }

    // ---- The save.

    [Fact]
    public void TheLessonProgressIsSavedAndLoadedAndAByteForByteSecondSaveIsTheSame()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        Assert.True(session.Lessons.Engaged); // the shipped lesson starts by itself
        _ = LessonDriver.DoStep(session); // u1-welcome
        _ = LessonDriver.DoStep(session); // u1-hello
        _ = LessonDriver.DoStep(session); // u1-say-hola: a typed "Hola"
        var saved = SaveGame.Save(session, TestGame.Route());

        var loaded = SaveGame.Load(saved, TestGame.Bundle, TestGame.Route, TestGame.Nouns, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.Equal(saved, SaveGame.Save(loaded, TestGame.Route()));
        Assert.Equal(LessonStatus.InProgress, loaded.Lessons.StatusOf("first-words"));
        Assert.Equal(
            [("u1-welcome", StepOutcome.Done), ("u1-hello", StepOutcome.Done), ("u1-say-hola", StepOutcome.Done)],
            loaded.Lessons.OutcomesOf("first-words"));
        Assert.Equal("u1-name", loaded.Lessons.Current!.Id);
        Assert.Equal(4, loaded.Lessons.StepNumber);
        Assert.False(loaded.Lessons.Engaged); // a load does not take the conversation over
        Assert.Equal(session.PhraseProgress.Records, loaded.PhraseProgress.Records);
        Assert.True(loaded.GuideMet); // worked out again from the progress that came back (S17)
        Assert.Equal(SessionStage.Camp, loaded.Stage);

        Stage.TeleportToNpc(loaded, "guide");
        _ = loaded.OpenTalk("guide");
        Assert.True(loaded.Lessons.Engaged); // it starts by itself again where it stopped
        Assert.Equal(4, loaded.Lessons.StepNumber);
        var rest = LessonDriver.CompleteAll(loaded);
        Assert.Equal("u1-name", rest[0].Id);
        Assert.Equal(LessonStatus.Completed, loaded.Lessons.StatusOf("first-words"));
    }

    [Fact]
    public void AFinishedLessonStaysFinishedAfterASaveAndALoad()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        _ = LessonDriver.CompleteAll(session);
        var loaded = SaveGame.Load(SaveGame.Save(session, TestGame.Route()), TestGame.Bundle, TestGame.Route, TestGame.Nouns, credit: CreditRules.FullTyped);

        Assert.Equal(LessonStatus.Completed, loaded.Lessons.StatusOf("first-words"));
        Assert.Null(loaded.Lessons.Active);
        Stage.TeleportToNpc(loaded, "guide");
        _ = loaded.OpenTalk("guide");
        Assert.Null(loaded.Lessons.OfferFor("guide"));
        Assert.False(loaded.Lessons.Engaged);
    }

    [Fact]
    public void ASaveWithAnUnknownLessonOrAStepItDoesNotHaveIsRefused()
    {
        var session = AtGuide();
        var doc = SaveGame.Parse(SaveGame.Save(session, TestGame.Route()));

        var unknown = doc with { Lessons = new LessonsSave([new LessonProgressSave("nope", LessonStatus.NotStarted, 0, [], 0, false)], null) };
        var beyond = doc with { Lessons = new LessonsSave([new LessonProgressSave("sample", LessonStatus.InProgress, 5, [], 0, false)], "sample") };

        Assert.Throws<SaveException>(() => SaveGame.Restore(unknown, TestGame.Bundle, TestGame.Route(), TestGame.Nouns));
        Assert.Throws<SaveException>(() => SaveGame.Restore(beyond, TestGame.Bundle, TestGame.Route(), TestGame.Nouns));
    }

    // ---- Determinism.

    [Fact]
    public void TheSameLessonRunGivesTheSameEvents()
    {
        static IReadOnlyList<string> Run()
        {
            var session = AtGuide();
            _ = session.Lessons.Start("sample");
            _ = session.Say("zzz qqq");
            _ = session.Say("hola");
            _ = session.Lessons.Help();
            _ = session.Say("gracias");
            return session.Events.Select(static e => $"{e.Tick}:{e.Time}:{e.Text}").ToList();
        }

        Assert.Equal(Run(), Run());
    }

    // The turn of a scripted spoken line, applied the way the worker's outcome is: at the prompt the lesson step names.
    private static AppliedTurn Spoken(VoxelSession session)
    {
        var scene = new GuideScene(TestGame.ViewpointOf(session), PromptOverride: session.Lessons.ActivePrompt);
        var turn = session.Conversation.SaySpoken("guide", Clip, scene, session.StoredTime, session.Simulation.ComputeStateDigest());
        var request = new TurnRequest(1, TurnSource.Clip, null, Clip, null, scene, session.StoredTime, session.Simulation.ComputeStateDigest(), null, session.Simulation.Tick, "guide");
        return session.Apply(new TurnOutcome(request, turn, null, new TurnTimings(0, 0, 0, false)));
    }
}
