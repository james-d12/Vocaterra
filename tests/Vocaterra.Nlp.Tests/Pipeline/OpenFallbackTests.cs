using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// M80 (the owner, D-game-22, 2026-11-06, option 1 of the study of spoken off-prompt lines): with <see cref="PipelineOptions.OpenRecognitionFallback"/> a
/// constrained prompt whose first reading is not taken decodes the clip a second time, freely, and keeps that reading when it is taken. The recognizer is
/// scripted by mode here, so the two decodes can disagree as they do on a real clip.
/// </summary>
public class OpenFallbackTests
{
    private static readonly PipelineOptions On = new() { OpenRecognitionFallback = true };

    private static AsrResult Constrained(string transcript) => TestSupport.Asr(transcript, RecognitionMode.Constrained);

    // A free recognition with a confidence for every word (the scripted recognizers of other tests carry none); the default floor is 0.5, so 0.9 clears it.
    private static AsrResult Open(string transcript, params double[] confidences)
    {
        var tokens = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words = tokens.Select((text, i) => new AsrWord(text, i * 0.4, (i * 0.4) + 0.3, i < confidences.Length ? confidences[i] : 0.9)).ToList();
        return new AsrResult(transcript, [new AsrHypothesis(transcript, 1.0)], words, 1.0, "scripted-test", RecognitionMode.Open);
    }

    private static UtteranceAnalysis AnalyzeWith(AsrResult open, PipelineOptions? options)
    {
        var recognizer = new ScriptedWordRecognizer(Constrained(string.Empty), open);
        var services = TestSupport.Services(open.Transcript, RecognitionMode.Constrained);
        services = services.WithRecognizers(recognizer, services.PhonemeRecognizer);
        return new UtterancePipeline(services, options).Analyze(TestSupport.Clip(), TestSupport.Bundle.Prompts["smith-open"]);
    }

    private static (UtterancePipeline Pipeline, ScriptedWordRecognizer Recognizer) Build(string constrained, string open, PipelineOptions? options, bool phonemes = false)
    {
        var recognizer = new ScriptedWordRecognizer(Constrained(constrained), Open(open));
        var services = TestSupport.Services(open, RecognitionMode.Constrained, phonemes);
        services = services.WithRecognizers(recognizer, services.PhonemeRecognizer);
        return (new UtterancePipeline(services, options), recognizer);
    }

    private static UtteranceAnalysis Analyze(string promptId, string constrained, string open, PipelineOptions? options, out ScriptedWordRecognizer recognizer)
    {
        var (pipeline, scripted) = Build(constrained, open, options);
        recognizer = scripted;
        return pipeline.Analyze(TestSupport.Clip(), TestSupport.Bundle.Prompts[promptId]);
    }

    [Fact]
    public void TheOptionIsOffByDefault()
    {
        Assert.False(PipelineOptions.Default.OpenRecognitionFallback);
    }

    [Theory]
    [InlineData("smith-open", "hola")]
    [InlineData("stall-offer", "sí")]
    [InlineData("baker-open", "quiero dos panes por favor")]
    public void AFirstReadingThatIsTakenDecodesOnce(string promptId, string heard)
    {
        var analysis = Analyze(promptId, heard, "me gusta el río", On, out var recognizer);

        Assert.True(analysis.Verdict.Communicated);
        Assert.Equal(1, recognizer.CallCount);
        Assert.Equal(RecognitionMode.Constrained, analysis.Asr.Mode);
        Assert.Null(analysis.OtherPass);
    }

    [Fact]
    public void AnOffPromptFreeReadingIsTakenAsOffPromptAndMarkedOpen()
    {
        var analysis = Analyze("smith-open", string.Empty, "me gusta el río", On, out var recognizer);

        Assert.Equal(2, recognizer.CallCount);
        Assert.Equal([RecognitionMode.Constrained, RecognitionMode.Open], recognizer.Requests.Select(static r => r.Mode));
        Assert.Equal(RecognitionMode.Open, analysis.Asr.Mode);
        Assert.Equal("me gusta el río", analysis.Asr.Transcript);
        Assert.Equal(new IntentId("state_like"), analysis.Verdict.OffPromptIntent);
        Assert.False(analysis.Verdict.Communicated);
        Assert.Equal(RecognitionMode.Constrained, analysis.OtherPass!.Mode);
    }

    [Theory]
    [InlineData("smith-open", "mañana voy a comprar pan", "state_action")]
    [InlineData("baker-open", "tengo dos manzanas", "state_have")]
    public void AnAllowedIntentOutsideTheAnswerRowsBecomesUnderstood(string promptId, string spoken, string intent)
    {
        // The constrained decode drops the line as [unk]; without the fallback it is not understood, with it the line is read as the typed line is.
        var without = Analyze(promptId, "[unk]", spoken, null, out _);
        var with = Analyze(promptId, "[unk]", spoken, On, out _);

        Assert.False(without.Verdict.Communicated);
        Assert.True(with.Verdict.Communicated);
        Assert.Equal(new IntentId(intent), with.Verdict.Intent);
        Assert.Equal(RecognitionMode.Open, with.Asr.Mode);
        var typed = TestSupport.Pipeline(spoken, RecognitionMode.Open).Analyze(TestSupport.Clip(), TestSupport.Bundle.Prompts[promptId]);
        Assert.Equal(typed.Verdict.Intent, with.Verdict.Intent);
    }

    // The owner's answer 2: a spoken line is understood as the typed line is. The free pass reads the same text with the same prompt and stages, so whatever it
    // takes, the typed line takes alike (the intent, the slots, the errors and the off-prompt intent), and a line the typed reading does not take stays untaken.
    // (The CLI's JSON verdict shows the unknown intent of an off-prompt line; its «off-prompt:» line and the verdict's offPromptIntent carry what it means.)
    [Theory]
    [InlineData("el libro es muy interesante")]
    [InlineData("ese libro es muy interesante")]
    [InlineData("si usted hace hachas muy buenas")]
    [InlineData("me gusta el río")]
    [InlineData("mañana voy a comprar pan")]
    [InlineData("el gato duerme")]
    [InlineData("hoy llueve mucho")]
    public void TheFreePassReadsAHeardLineAsTheSameTextTypedIsRead(string text)
    {
        foreach (var promptId in new[] { "fisher-open", "baker-open", "smith-open", "farmer-open", "stall-need", "stall-offer", "guide-offer" })
        {
            var prompt = TestSupport.Bundle.Prompts[promptId];
            var typed = TestSupport.Pipeline(text, RecognitionMode.Open).Analyze(TestSupport.Clip(), prompt).Verdict;
            var spoken = Analyze(promptId, string.Empty, text, On, out _);
            var taken = typed.Communicated || typed.OffPromptIntent is not null;
            var heard = spoken.Verdict;

            Assert.Equal(taken, heard.Communicated || heard.OffPromptIntent is not null);
            Assert.Equal(taken, spoken.Asr.Mode == RecognitionMode.Open);
            if (taken)
            {
                Assert.Equal(typed.Intent, heard.Intent);
                Assert.Equal(typed.Slots, heard.Slots);
                Assert.Equal(typed.Communicated, heard.Communicated);
                Assert.Equal(typed.OffPromptIntent, heard.OffPromptIntent);
                Assert.Equal(typed.Errors, heard.Errors);
                Assert.Equal(typed.Correctness, heard.Correctness);
            }
        }
    }

    [Fact]
    public void AFreeReadingThatIsNotTakenKeepsTheFirstVerdict()
    {
        var kept = Analyze("smith-open", string.Empty, "el gato duerme", On, out var recognizer);
        var plain = Analyze("smith-open", string.Empty, "el gato duerme", null, out _);

        Assert.Equal(2, recognizer.CallCount);
        Assert.Null(kept.Verdict.OffPromptIntent);
        Assert.Equal(RecognitionMode.Constrained, kept.Asr.Mode);
        Assert.Equal(plain.Verdict, kept.Verdict);
        Assert.Equal(plain.Asr, kept.Asr);
        Assert.Equal(RecognitionMode.Open, kept.OtherPass!.Mode);
        Assert.Equal("el gato duerme", kept.OtherPass.Transcript);
    }

    [Fact]
    public void ALineHeardOnlyAsUnknownsIsReadAgainFreely()
    {
        // Every word of the constrained decode is [unk], so nothing is left of it after cleaning; the free decode has the words.
        var analysis = Analyze("smith-open", "[unk] [unk]", "tengo un hacha", On, out var recognizer);

        Assert.Equal(2, recognizer.CallCount);
        Assert.True(analysis.Verdict.Communicated);
        Assert.Equal(new IntentId("state_have"), analysis.Verdict.Intent);
    }

    [Fact]
    public void AnOpenPromptNeverDecodesTwice()
    {
        var recognizer = new ScriptedWordRecognizer(Constrained("never"), Open("el gato duerme"));
        var services = TestSupport.Services("x", RecognitionMode.Open);
        services = services.WithRecognizers(recognizer, null);
        var pipeline = new UtterancePipeline(services, On);

        var analysis = pipeline.Analyze(TestSupport.Clip(), TestSupport.Bundle.Prompts[TestSupport.OpenPrompt]);

        Assert.Equal(1, recognizer.CallCount);
        Assert.Null(analysis.OtherPass);
    }

    [Theory]
    [InlineData("smith-open", "", "me gusta el río")]
    [InlineData("smith-open", "hola", "me gusta el río")]
    [InlineData("smith-open", "[unk]", "mañana voy a comprar pan")]
    public void WithTheOptionOffTheRecognizerIsCalledOnceAndTheFirstReadingStands(string promptId, string constrained, string open)
    {
        var analysis = Analyze(promptId, constrained, open, null, out var recognizer);
        var explicitOff = Analyze(promptId, constrained, open, new PipelineOptions { OpenRecognitionFallback = false }, out var again);

        Assert.Equal(1, recognizer.CallCount);
        Assert.Equal(1, again.CallCount);
        Assert.Equal(RecognitionMode.Constrained, recognizer.LastRequest!.Mode);
        Assert.Equal(constrained, analysis.Asr.Transcript);
        Assert.Null(analysis.OtherPass);
        Assert.Equal(analysis.Verdict, explicitOff.Verdict);
    }

    [Fact]
    public void ARecordedFreeRecognitionNeverTriggersASecondDecode()
    {
        // A replay serves the recorded (free) recognition whatever the request asks for; its mode is Open, so the trigger does not fire.
        var recognizer = new ScriptedWordRecognizer(Open("me gusta el río"));
        var services = TestSupport.Services("x", RecognitionMode.Open);
        services = services.WithRecognizers(recognizer, null);
        var pipeline = new UtterancePipeline(services, On);

        var analysis = pipeline.Analyze(TestSupport.Clip(), TestSupport.Bundle.Prompts["smith-open"]);

        Assert.Equal(1, recognizer.CallCount);
        Assert.Equal(new IntentId("state_like"), analysis.Verdict.OffPromptIntent);
    }

    [Fact]
    public void TheChosenFreeRecognitionIsRecordedAndReplaysWithoutTheModelThroughPipelinesWithTheOptionOnAndOff()
    {
        var (live, _) = Build(string.Empty, "tengo un hacha", On, phonemes: true);
        var context = TestSupport.Context(agentId: "smith");
        var clip = TestSupport.Clip();
        var trace = live.Trace(clip, TestSupport.Bundle.Prompts["smith-open"], context);
        var envelope = ReplayEnvelope.From(trace, context);
        using var writer = new StringWriter();
        new ReplayLog(writer).Append(envelope);
        var back = ReplayReader.ReadEnvelopes(new StringReader(writer.ToString())).Single();

        Assert.Equal(RecognitionMode.Open, back.Asr.Mode);
        Assert.True(trace.Result.Verdict.Communicated);
        foreach (var options in new PipelineOptions?[] { On, null })
        {
            var offline = new UtterancePipeline(TestSupport.Services("never used", RecognitionMode.Constrained, phonemes: true), options);

            Assert.Null(Replayer.Replay(back, offline, hash => new Vocaterra.Nlp.Core.Audio.PcmClip([], Vocaterra.Nlp.Core.Audio.PcmClip.DefaultSampleRate, hash)));
        }
    }

    [Fact]
    public void AKeptConstrainedRecognitionReplaysTheSameThroughPipelinesWithTheOptionOnAndOff()
    {
        var (live, _) = Build(string.Empty, "el gato duerme", On);
        var context = TestSupport.Context(agentId: "smith");
        var trace = live.Trace(TestSupport.Clip(), TestSupport.Bundle.Prompts["smith-open"], context);
        var envelope = ReplayEnvelope.From(trace, context);

        Assert.Equal(RecognitionMode.Constrained, envelope.Asr.Mode);
        foreach (var options in new PipelineOptions?[] { On, null })
        {
            var offline = new UtterancePipeline(TestSupport.Services("never used", RecognitionMode.Constrained), options);

            Assert.Null(Replayer.Replay(envelope, offline, hash => new Vocaterra.Nlp.Core.Audio.PcmClip([], Vocaterra.Nlp.Core.Audio.PcmClip.DefaultSampleRate, hash)));
        }
    }

    [Fact]
    public void TheFirstReadingOfAKeptPassIsTheOptionOffResultByteForByte()
    {
        var (on, _) = Build(string.Empty, "el gato duerme", On);
        var (off, _) = Build(string.Empty, "el gato duerme", null);
        var context = TestSupport.Context(agentId: "smith");
        var prompt = TestSupport.Bundle.Prompts["smith-open"];

        Assert.Equal(CanonicalJson.Serialize(off.Trace(TestSupport.Clip(), prompt, context)), CanonicalJson.Serialize(on.Trace(TestSupport.Clip(), prompt, context)));
    }

    [Fact]
    public void TheFloorDefaultsToHalfAndIsValidated()
    {
        Assert.Equal(0.5, PipelineOptions.Default.OpenPassWordFloor);
        Assert.Equal(PipelineOptions.DefaultOpenPassWordFloor, new PipelineOptions().OpenPassWordFloor);
        foreach (var floor in new[] { -0.01, 1.01, double.NaN, double.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Build("x", "y", new PipelineOptions { OpenPassWordFloor = floor }));
        }

        foreach (var floor in new[] { 0.0, 0.5, 1.0 })
        {
            Assert.NotNull(Build("x", "y", new PipelineOptions { OpenPassWordFloor = floor }).Pipeline);
        }
    }

    [Fact]
    public void AFreeReadingWithAWordUnderTheFloorIsNotTaken()
    {
        var weak = Open("me gusta el río", 0.9, 0.49, 0.9, 0.9);
        var kept = AnalyzeWith(weak, On);

        Assert.Equal(RecognitionMode.Constrained, kept.Asr.Mode);
        Assert.Null(kept.Verdict.OffPromptIntent);
        Assert.False(kept.Verdict.Communicated);
        Assert.Equal(weak, kept.OtherPass);
        Assert.Equal(AnalyzeWith(weak, null).Verdict, kept.Verdict);
    }

    [Fact]
    public void AFreeReadingWhoseWeakestWordIsExactlyTheFloorIsTaken()
    {
        var analysis = AnalyzeWith(Open("me gusta el río", 0.9, 0.5, 0.9, 0.9), On);

        Assert.Equal(RecognitionMode.Open, analysis.Asr.Mode);
        Assert.Equal(new IntentId("state_like"), analysis.Verdict.OffPromptIntent);
    }

    [Fact]
    public void ARaisedOrLoweredFloorMovesTheLine()
    {
        var heard = Open("me gusta el río", 0.9, 0.7, 0.9, 0.9);

        Assert.Equal(RecognitionMode.Open, AnalyzeWith(heard, On with { OpenPassWordFloor = 0.7 }).Asr.Mode);
        Assert.Equal(RecognitionMode.Constrained, AnalyzeWith(heard, On with { OpenPassWordFloor = 0.71 }).Asr.Mode);
        Assert.Equal(RecognitionMode.Open, AnalyzeWith(Open("me gusta el río", 0.9, 0.1, 0.9, 0.9), On with { OpenPassWordFloor = 0.1 }).Asr.Mode);
    }

    [Fact]
    public void AFloorOfZeroTakesTheFreeReadingAsBeforeTheFloorEvenWithoutWordConfidences()
    {
        var noWords = TestSupport.Asr("me gusta el río", RecognitionMode.Open);
        var off = On with { OpenPassWordFloor = 0.0 };

        Assert.Equal(RecognitionMode.Open, AnalyzeWith(Open("me gusta el río", 0.0, 0.0, 0.0, 0.0), off).Asr.Mode);
        Assert.Equal(RecognitionMode.Open, AnalyzeWith(noWords, off).Asr.Mode);
    }

    [Fact]
    public void AFreeRecognitionWithoutWordConfidencesIsNotTakenUnderTheDefaultFloor()
    {
        var kept = AnalyzeWith(TestSupport.Asr("me gusta el río", RecognitionMode.Open), On);

        Assert.Equal(RecognitionMode.Constrained, kept.Asr.Mode);
        Assert.Null(kept.Verdict.OffPromptIntent);
    }

    [Fact]
    public void TheFloorNeverTouchesTheFirstPass()
    {
        // A constrained reading that is taken stands whatever its word confidences, with the floor at its highest; the recognizer is asked once.
        var weakFirst = new AsrResult("hola", [new AsrHypothesis("hola", 1.0)], [new AsrWord("hola", 0.0, 0.3, 0.05)], 1.0, "scripted-test", RecognitionMode.Constrained);
        var recognizer = new ScriptedWordRecognizer(weakFirst, Open("me gusta el río"));
        var services = TestSupport.Services("hola", RecognitionMode.Constrained);
        services = services.WithRecognizers(recognizer, services.PhonemeRecognizer);
        var strict = new PipelineOptions { OpenRecognitionFallback = true, OpenPassWordFloor = 1.0 };

        var analysis = new UtterancePipeline(services, strict).Analyze(TestSupport.Clip(), TestSupport.Bundle.Prompts["smith-open"]);

        Assert.True(analysis.Verdict.Communicated);
        Assert.Equal(1, recognizer.CallCount);
        Assert.Equal(RecognitionMode.Constrained, analysis.Asr.Mode);
    }

    [Fact]
    public void TheFloorDoesNothingWithTheFallbackOff()
    {
        var weak = Open("me gusta el río", 0.9, 0.1, 0.9, 0.9);

        Assert.Equal(CanonicalJson.Serialize(AnalyzeWith(weak, null).Verdict), CanonicalJson.Serialize(AnalyzeWith(weak, new PipelineOptions { OpenPassWordFloor = 1.0 }).Verdict));
        Assert.Null(AnalyzeWith(weak, new PipelineOptions { OpenPassWordFloor = 1.0 }).OtherPass);
    }

    [Fact]
    public void ScriptedByModeServesEachModeItsOwnResultAndRecordsTheRequests()
    {
        var recognizer = new ScriptedWordRecognizer(Constrained("a"), Open("b"));
        var clip = TestSupport.Clip();

        Assert.Equal("a", recognizer.Recognize(clip, RecognitionRequest.Constrained(["a"])).Transcript);
        Assert.Equal("b", recognizer.Recognize(clip, RecognitionRequest.Open()).Transcript);
        Assert.Equal(2, recognizer.CallCount);
        Assert.Equal(2, recognizer.Requests.Count);
        Assert.Throws<InvalidOperationException>(() => new ScriptedWordRecognizer(Constrained("a"), (AsrResult?)null).Recognize(clip, RecognitionRequest.Open()));
    }
}
