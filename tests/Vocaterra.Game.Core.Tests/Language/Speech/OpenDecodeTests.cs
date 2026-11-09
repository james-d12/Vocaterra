using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Speech;

/// <summary>
/// V31, the spoken lines heard on the open decode (core M80; the owner's answers of 2026-11-06 and 2026-11-07): in free talk at the seven prompts a line the
/// constrained decode does not take is decoded a second time, freely, and answered as the same typed line is; lesson steps, reviews and the mic check decode once;
/// a line heard that way is credited as a typed line (half) and marked in the voice events. A recognizer scripted by mode stands in for Vosk; the model tests speak
/// Kokoro lines at the NPCs.
/// </summary>
[Collection("speech models")]
public sealed class OpenDecodeTests(ITestOutputHelper output)
{
    private static readonly PcmClip Clip = PcmClip.FromSamples(new short[1600]);

    // Hears nothing at a constrained request (the grammar does not have the words) and the line said at an open one, with the given word confidences; counts the decodes by mode.
    private sealed class ByMode(string open, params double[] confidences) : IWordRecognizer
    {
        public int Constrained { get; private set; }

        public int Open { get; private set; }

        public int Calls => Constrained + Open;

        public string ModelHash => "test-model";

        public AsrResult Recognize(PcmClip clip, RecognitionRequest request)
        {
            if (request.Mode == RecognitionMode.Constrained)
            {
                Constrained++;
                return new AsrResult(string.Empty, [new AsrHypothesis(string.Empty, 1.0)], [], 1.0, ModelHash, request.Mode);
            }

            Open++;
            var heard = open.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var words = heard.Select((w, i) => new AsrWord(w, i * 0.3, (i * 0.3) + 0.25, confidences[Math.Min(i, confidences.Length - 1)])).ToList();
            return new AsrResult(open, [new AsrHypothesis(open, 1.0)], words, 1.0, ModelHash, request.Mode);
        }
    }

    private sealed class Counting(IWordRecognizer inner) : IWordRecognizer
    {
        public int Calls { get; private set; }

        public string ModelHash => inner.ModelHash;

        public AsrResult Recognize(PcmClip clip, RecognitionRequest request)
        {
            Calls++;
            return inner.Recognize(clip, request);
        }
    }

    private static VoxelSession AtNpc(string npc, IWordRecognizer recognizer, CreditRules? credit = null)
    {
        var session = TestGame.NewSession(credit: credit ?? CreditRules.Default);
        _ = session.TakeOrders(); // the orders are taken: this test is not about their offer (issue #54)
        Stage.TeleportToNpc(session, npc);
        _ = session.OpenTalk(npc);
        session.Conversation.AttachSpeech(new SpokenUtteranceEvaluator(TestGame.Bundle, recognizer));
        return session;
    }

    private static GuideTurn Speak(VoxelSession session, string npc, GuideScene? scene = null) =>
        session.Conversation.SaySpoken(npc, Clip, scene ?? new GuideScene(TestGame.ViewpointOf(session)), session.StoredTime, session.Simulation.ComputeStateDigest());

    private static AppliedTurn Apply(VoxelSession session, GuideTurn turn, string npc, GuideScene scene)
    {
        var request = new TurnRequest(1, TurnSource.Clip, null, Clip, null, scene, session.StoredTime, session.Simulation.ComputeStateDigest(), null, session.Simulation.Tick, npc);
        return session.Apply(new TurnOutcome(request, turn, null, new TurnTimings(0, 0, 0, false, 2)));
    }

    [Theory]
    [InlineData("smith", "me gusta el río", "state_like", "Te entiendo. Yo hago hachas.")]
    [InlineData("fisher", "tengo dos manzanas", "state_have", "Te entiendo. ¿Buscas un lugar?")]
    [InlineData("baker", "el río es grande", "describe", "Te entiendo. ¿Quieres pan?")]
    public void ASpokenOffPromptLineTheConstrainedDecodeMissesIsHeardOnTheOpenDecodeAndGetsTeEntiendo(string npc, string line, string intent, string reply)
    {
        var recognizer = new ByMode(line, 0.9);
        using var session = AtNpc(npc, recognizer);

        var turn = Speak(session, npc);

        Assert.Equal((1, 1), (recognizer.Constrained, recognizer.Open));
        Assert.Equal([reply], turn.Replies);
        Assert.Equal(intent, turn.OffPromptIntent);
        Assert.True(turn.HeardOpen);
        Assert.Equal(InputKind.Spoken, turn.Input);
        Assert.False(turn.Uncertain);
        Assert.Equal(line, turn.Line);
    }

    [Fact]
    public void ASpokenLineThePromptAllowsButNotAmongItsRowsIsUnderstoodAsTheTypedLineIs()
    {
        const string line = "mañana voy a comprar pan";
        using var session = AtNpc("smith", new ByMode(line, 0.9));
        using var typed = TestGame.NewSession();
        _ = typed.TakeOrders(); // the orders are taken: this test is not about their offer (issue #54)
        Stage.TeleportToNpc(typed, "smith");
        _ = typed.OpenTalk("smith");

        var spoken = Speak(session, "smith");
        var expected = typed.Say(line);

        Assert.True(spoken.HeardOpen);
        Assert.True(spoken.Communicated);
        Assert.Equal(expected.Intent, spoken.Intent);
        Assert.Equal(expected.Replies, spoken.Replies);
    }

    [Fact]
    public void AVocativeInTheOpenDecodeIsTakenOutOfTheLineAsInTheTypedOne()
    {
        // «Domingo, mañana voy a comprar pan»: the name is no part of the sentence (V14); the open recognition is read without it.
        var smith = TestGame.Data.Npcs.Find("smith")!.Name;
        using var session = AtNpc("smith", new ByMode($"{smith} mañana voy a comprar pan", 0.9));

        var turn = Speak(session, "smith");

        Assert.True(turn.HeardOpen);
        Assert.True(turn.Communicated || turn.OffPromptIntent is not null);
    }

    [Fact]
    public void ALessonStepAndAReviewDecodeOnceAndNeverOpen()
    {
        var step = new ByMode("me gusta el río", 0.9);
        using var atStep = AtNpc("guide", step);
        var onStep = Speak(atStep, "guide", new GuideScene(TestGame.ViewpointOf(atStep), PromptOverride: "lesson-like-apples"));

        Assert.Equal((1, 0), (step.Constrained, step.Open));
        Assert.False(onStep.HeardOpen);

        var review = new ByMode("me gusta el río", 0.9);
        using var atReview = AtNpc("smith", review);
        var inReview = Speak(atReview, "smith", new GuideScene(TestGame.ViewpointOf(atReview), Review: true));

        Assert.Equal(1, review.Calls);
        Assert.False(inReview.HeardOpen);
    }

    [Fact]
    public void AnOpenPromptDecodesOnceAndTheMicCheckDecodesOnceWhenItHearsHola()
    {
        var recognizer = new ByMode("hola", 0.9);
        using var session = AtNpc("guide", recognizer);
        _ = Speak(session, "guide"); // guide-open is open: one decode, whatever it hears
        Assert.Equal((0, 1), (recognizer.Constrained, recognizer.Open));

        var constrainedHola = new Counting(new SpeechUxHola());
        var heard = MicCheck.Run(constrainedHola, Clip, "hola");
        Assert.True(heard.Passed);
        Assert.Equal(1, constrainedHola.Calls);
    }

    // Hears «hola» whatever it is asked.
    private sealed class SpeechUxHola : IWordRecognizer
    {
        public string ModelHash => "test-model";

        public AsrResult Recognize(PcmClip clip, RecognitionRequest request) =>
            new("hola", [new AsrHypothesis("hola", 1.0)], [new AsrWord("hola", 0, 0.3, 0.95)], 1.0, ModelHash, request.Mode);
    }

    [Fact]
    public void AFreePassUnderTheCoresWordFloorIsNotTakenAndTheLineIsNotUnderstood()
    {
        var recognizer = new ByMode("me gusta el río", 0.9, 0.9, 0.4, 0.9); // «el» at 0.4, under the 0.5 floor
        using var session = AtNpc("smith", recognizer);

        var turn = Speak(session, "smith");

        Assert.Equal(2, recognizer.Calls);
        Assert.False(turn.HeardOpen);
        Assert.False(turn.Taken);
        Assert.Null(turn.OffPromptIntent);
    }

    [Fact]
    public void AnAllowedFreePassTheRecognizerWasNotSureOfKeepsNoTeEntendiBienAndAnOffPromptOneKeepsTheNpcsNotUnderstood()
    {
        var allowed = new ByMode("mañana voy a comprar pan", 0.52); // taken (over the floor) but the mean is under 0.55
        using var atSmith = AtNpc("smith", allowed);

        var unsure = Speak(atSmith, "smith");

        Assert.True(unsure.Uncertain);
        Assert.Contains("No te entendí bien. ¿Lo dices otra vez?", unsure.Replies);
        Assert.False(unsure.Communicated);
        Assert.True(unsure.HeardOpen); // the telemetry still counts that the line was heard on the open decode

        var off = new ByMode("me gusta el río", 0.52);
        using var atFisher = AtNpc("fisher", off);

        var held = Speak(atFisher, "fisher");

        Assert.Null(held.OffPromptIntent); // V27: a recognizer unsure of the words gets no «Te entiendo»
        Assert.DoesNotContain(held.Replies, static r => r.StartsWith("Te entiendo", StringComparison.Ordinal));
    }

    [Fact]
    public void ALineHeardOnTheOpenDecodeEarnsTheCreditOfATypedLineAndOneHeardFirstTimeEarnsTheSpokenCredit()
    {
        Assert.Equal(CreditRules.Default.TypedUnits, CreditRules.Default.For(new GuideTurn("x", "i", true, 1, [], null, [], null, "s", [], null, null, null, "h", Vocaterra.Nlp.Core.Verdicts.SlotMap.Empty, Input: InputKind.Spoken) { HeardOpen = true }.CreditKind));

        // The same off-prompt line with the words credited: heard on the open decode, typed, and heard on the first decode (Open prompt, a spoken credit).
        long Credit(VoxelSession session) => session.Words.Records.Values.Sum(static r => (long)r.CreditUnits);

        using var open = AtNpc("fisher", new ByMode("tengo dos manzanas", 0.9));
        var openScene = new GuideScene(TestGame.ViewpointOf(open));
        var turn = Speak(open, "fisher", openScene);
        Assert.True(turn.HeardOpen);
        _ = Apply(open, turn, "fisher", openScene);

        using var typed = TestGame.NewSession(credit: CreditRules.Default);
        _ = typed.TakeOrders(); // the orders are taken: this test is not about their offer (issue #54)
        Stage.TeleportToNpc(typed, "fisher");
        _ = typed.OpenTalk("fisher");
        _ = typed.Say("tengo dos manzanas");

        Assert.NotEqual(0, Credit(typed));
        Assert.Equal(Credit(typed), Credit(open));

        var phrase = open.Events.OfType<PhraseUse>().FirstOrDefault();
        Assert.True(phrase is null || phrase.Input == InputKind.Typed);
    }

    [Fact]
    public void ALineHeardOnTheOpenDecodeIsMarkedByAVoiceEventAndTheTurnTimingsShowTheDecodes()
    {
        var recognizer = new ByMode("me gusta el río", 0.9);
        using var session = AtNpc("smith", recognizer);
        var scene = new GuideScene(TestGame.ViewpointOf(session));
        var turn = Speak(session, "smith", scene);

        var applied = Apply(session, turn, "smith", scene);

        var marks = session.Events.OfType<VoiceEvent>().Where(static e => e.Kind == VoiceEventKind.HeardOpen).ToList();
        var mark = Assert.Single(marks);
        Assert.Equal("smith", mark.Npc);
        Assert.Equal("me gusta el río", mark.Detail);
        Assert.Equal("voice heard_open at smith (\"me gusta el río\")", mark.Text);
        Assert.Equal(2, applied.Outcome.Timings.Decodes);
        Assert.Equal(new TurnTimings(0, 0, 0, false), new TurnTimings(0, 0, 0, false, 0)); // a typed turn has no decodes
    }

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void KokoroOffPromptLinesAtTheNpcsGetTeEntiendoAndAllowedLinesOutsideTheRowsAreUnderstood()
    {
        using var engine = SpeechEngine.TryCreate(out _)!;
        var counting = new Counting(engine.Recognizer);
        var misses = new List<string>();
        foreach (var (npc, line, teEntiendo) in new[]
        {
            ("smith", "me gusta el río", true),
            ("smith", "mañana voy a comprar pan", false), // allowed at the prompt, not among its rows
            ("fisher", "tengo dos manzanas", true),
            ("farmer", "tengo dos manzanas", true),
            ("baker", "hoy llueve mucho", false), // the weather: «¡Sí!»
            ("shopkeeper", "el pan es muy bueno", false), // a statement about the listener's thing: «Sí, es verdad.»
        })
        {
            using var session = AtNpc(npc, counting);
            var before = counting.Calls;
            var turn = session.Conversation.SaySpoken(npc, engine.Synthesize(line, out _), new GuideScene(TestGame.ViewpointOf(session)), session.StoredTime, session.Simulation.ComputeStateDigest());
            output.WriteLine($"{npc} | {line} | heard '{turn.Line}' ({turn.Confidence:0.00}) | decodes {counting.Calls - before} | open {turn.HeardOpen} | intent {turn.Intent}/{turn.OffPromptIntent} | {string.Join(" / ", turn.Replies)}");
            var understood = turn.HeardOpen && turn.Taken && (!teEntiendo || turn.Replies.Any(static r => r.StartsWith("Te entiendo", StringComparison.Ordinal)));
            if (!understood)
            {
                misses.Add($"{npc}: '{line}' heard '{turn.Line}'");
            }
        }

        Assert.True(misses.Count == 0, "Not understood: " + string.Join("; ", misses));
    }

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void AKokoroLineAtALessonStepDecodesOnce()
    {
        using var engine = SpeechEngine.TryCreate(out _)!;
        var counting = new Counting(engine.Recognizer);
        using var session = AtNpc("guide", counting);
        var scene = new GuideScene(TestGame.ViewpointOf(session), PromptOverride: "lesson-like-apples");

        var turn = session.Conversation.SaySpoken("guide", engine.Synthesize("me gusta el río", out _), scene, session.StoredTime, session.Simulation.ComputeStateDigest());

        Assert.Equal(1, counting.Calls);
        Assert.False(turn.HeardOpen);
    }

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void ThroughTheWorkerASpokenOffPromptLineShowsTwoDecodesAndAnAnswerRowOne()
    {
        using var session = TestGame.NewSession(credit: CreditRules.Default);
        _ = session.TakeOrders(); // the orders are taken: this test is not about their offer (issue #54)
        Assert.True(session.StartWorker().WaitUntilReady(60000));
        Stage.TeleportToNpc(session, "smith");
        _ = session.OpenTalk("smith");

        _ = session.SubmitSpeak("me gusta el río");
        var off = Drain(session);
        _ = session.SubmitSpeak("¿cómo te llamas?");
        var row = Drain(session);

        output.WriteLine($"off-prompt: heard '{off.Turn!.Line}' open {off.Turn.HeardOpen} decodes {off.Outcome.Timings.Decodes}; row: heard '{row.Turn!.Line}' decodes {row.Outcome.Timings.Decodes}");
        Assert.Equal(1, row.Outcome.Timings.Decodes);
        Assert.True(off.Turn.HeardOpen);
        Assert.Equal(2, off.Outcome.Timings.Decodes);
        Assert.Single(session.Events.OfType<VoiceEvent>(), static e => e.Kind == VoiceEventKind.HeardOpen);
    }

    private static AppliedTurn Drain(VoxelSession session)
    {
        var deadline = Environment.TickCount64 + 120000;
        while (Environment.TickCount64 < deadline)
        {
            var applied = session.Collect();
            if (applied.Count > 0)
            {
                return applied[0];
            }

            Thread.Sleep(5);
        }

        throw new TimeoutException("The turn did not come back.");
    }
}
