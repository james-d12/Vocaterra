using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// M84 (free talk's forced rows, V31): with <see cref="PipelineOptions.OpenRecognitionFallback"/> on, a constrained reading that was taken but looks forced also gets the
/// open decode: its answer match is no whole row (<see cref="PipelineOptions.OpenPassOnLooseMatch"/>), or the recognized words leave most of the clip's speech unused
/// (<see cref="PipelineOptions.OpenPassMinCoverage"/>). The free reading then wins only when it is taken, clears the word floor and has another intent. The test clip is
/// one second of a tone, voiced throughout, so words that span the first half second cover half of its speech.
/// </summary>
public class ForcedRowsTests
{
    private static readonly PipelineOptions On = new() { OpenRecognitionFallback = true };

    // Neither of the two signals: the M80 behaviour with the fallback on.
    private static readonly PipelineOptions OnlyUntaken = On with { OpenPassOnLooseMatch = false, OpenPassMinCoverage = 0.0 };

    // A recognition whose words share the first <paramref name="seconds"/> of the one-second clip evenly, each with the given confidence ([unk] is a word of its own).
    private static AsrResult Heard(string transcript, RecognitionMode mode, double seconds = 1.0, double confidence = 0.95)
    {
        var tokens = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var width = tokens.Length == 0 ? 0.0 : seconds / tokens.Length;
        var words = tokens.Select((text, i) => new AsrWord(text, i * width, (i + 1) * width, confidence)).ToList();
        return new AsrResult(transcript, [new AsrHypothesis(transcript, 1.0)], words, 1.0, "scripted-test", mode);
    }

    private static UtteranceAnalysis Analyze(string promptId, AsrResult constrained, AsrResult open, PipelineOptions? options, out ScriptedWordRecognizer recognizer, PcmClip? clip = null)
    {
        recognizer = new ScriptedWordRecognizer(constrained, open);
        var services = TestSupport.Services(open.Transcript, RecognitionMode.Constrained);
        services = services.WithRecognizers(recognizer, services.PhonemeRecognizer);
        return new UtterancePipeline(services, options).Analyze(clip ?? TestSupport.Clip(), TestSupport.Bundle.Prompts[promptId]);
    }

    private static string Intent(Verdict verdict) => verdict.Communicated ? verdict.Intent.Value : verdict.OffPromptIntent?.Value ?? "-";

    // ---- V31's two forced lines ----------------------------------------------------------------------------------------------------

    [Fact]
    public void ALineReadAsAPriceQuestionButSaidAsALikingIsReplacedByTheFreeReading()
    {
        // V31 at the shopkeeper: «me gusta el río» was heard «me cuesta el río», all of it, with confidence; the reading (ask_price) is taken, no answer row.
        var forced = Heard("me cuesta el río", RecognitionMode.Constrained);
        var plain = Analyze("stall-need", forced, Heard("me gusta el río", RecognitionMode.Open), null, out _);
        var analysis = Analyze("stall-need", forced, Heard("me gusta el río", RecognitionMode.Open), On, out var recognizer);

        Assert.Equal("ask_price", Intent(plain.Verdict));
        Assert.Equal(2, recognizer.CallCount);
        Assert.Equal([RecognitionMode.Constrained, RecognitionMode.Open], recognizer.Requests.Select(static r => r.Mode));
        Assert.Equal(RecognitionMode.Open, analysis.Asr.Mode);
        Assert.Equal("state_like", Intent(analysis.Verdict));
        Assert.Equal(forced, analysis.OtherPass);
    }

    [Fact]
    public void ALineHeardAsAnAnswerRowOverMostOfItsSpeechIsReplacedByTheFreeReading()
    {
        // V31 at the baker: «el río es grande» was heard «es gracias» (one short word of the clip's second); the row is taken, the words cover a quarter of the speech.
        var forced = Heard("no gracias", RecognitionMode.Constrained, seconds: 0.25);
        var analysis = Analyze("stall-offer", forced, Heard("el río es grande", RecognitionMode.Open), On, out var recognizer);

        Assert.Equal(2, recognizer.CallCount);
        Assert.Equal(RecognitionMode.Open, analysis.Asr.Mode);
        Assert.Equal("describe", Intent(analysis.Verdict));
        Assert.Equal(forced, analysis.OtherPass);
    }

    // ---- The signals, one by one -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("stall-need", "me cuesta el río", true)]      // taken through the intent catalog: no answer row
    [InlineData("stall-need", "río es grande", true)]         // a near miss of the row «es grande»: Fuzzy
    [InlineData("baker-open", "quiero dos panes por favor", false)] // the row, heard whole
    [InlineData("smith-open", "hola", false)]
    [InlineData("stall-offer", "no gracias", false)]
    public void ALooseAnswerMatchFiresTheTriggerAndAnExactOneDoesNot(string promptId, string heard, bool fires)
    {
        var analysis = Analyze(promptId, Heard(heard, RecognitionMode.Constrained), Heard("el gato duerme", RecognitionMode.Open), On with { OpenPassMinCoverage = 0.0 }, out var recognizer);

        Assert.Equal(fires ? 2 : 1, recognizer.CallCount);
        Assert.Equal(fires, analysis.OtherPass is not null);
    }

    [Fact]
    public void TheLooseMatchTriggerCanBeSwitchedOff()
    {
        var forced = Heard("me cuesta el río", RecognitionMode.Constrained);
        var analysis = Analyze("stall-need", forced, Heard("me gusta el río", RecognitionMode.Open), On with { OpenPassOnLooseMatch = false, OpenPassMinCoverage = 0.0 }, out var recognizer);

        Assert.Equal(1, recognizer.CallCount);
        Assert.Equal("ask_price", Intent(analysis.Verdict));
        Assert.Null(analysis.OtherPass);
    }

    [Theory]
    [InlineData(0.25, true)]
    [InlineData(0.5, true)]
    [InlineData(0.9, false)]
    [InlineData(1.0, false)]
    public void WordsThatLeaveMostOfTheSpeechUnusedFireTheTriggerOnAnExactRow(double seconds, bool fires)
    {
        var heard = Heard("no gracias", RecognitionMode.Constrained, seconds);
        var analysis = Analyze("stall-offer", heard, Heard("el río es grande", RecognitionMode.Open), On with { OpenPassOnLooseMatch = false }, out var recognizer);

        Assert.Equal(fires ? 2 : 1, recognizer.CallCount);
        Assert.Equal(fires, analysis.Asr.Mode == RecognitionMode.Open);
    }

    [Fact]
    public void TheCoverageThresholdIsTheOptionAndTheShareExactlyAtItIsTrusted()
    {
        var heard = Heard("no gracias", RecognitionMode.Constrained, 0.5);
        var share = ForcedReading.Coverage(TestSupport.Clip(), heard)!.Value;
        var open = Heard("el río es grande", RecognitionMode.Open);
        var only = On with { OpenPassOnLooseMatch = false };

        Assert.InRange(share, 0.45, 0.55);
        Assert.Equal(1, CallsFor(heard, open, only with { OpenPassMinCoverage = share }));
        Assert.Equal(2, CallsFor(heard, open, only with { OpenPassMinCoverage = share + 0.01 }));
        Assert.Equal(1, CallsFor(heard, open, only with { OpenPassMinCoverage = 0.0 }));
    }

    private static int CallsFor(AsrResult heard, AsrResult open, PipelineOptions options)
    {
        Analyze("stall-offer", heard, open, options, out var recognizer);
        return recognizer.CallCount;
    }

    [Fact]
    public void ACoverageNeedsVoicedAudioAndTimedWords()
    {
        var silence = PcmClip.FromSamples(new short[PcmClip.DefaultSampleRate]);
        var noWords = TestSupport.Asr("no gracias", RecognitionMode.Constrained);
        var unknownOnly = new AsrResult("[unk]", [new AsrHypothesis("[unk]", 1.0)], [new AsrWord("[unk]", 0.0, 1.0, 0.9)], 1.0, "scripted-test", RecognitionMode.Constrained);
        var open = Heard("el río es grande", RecognitionMode.Open);

        Assert.Null(ForcedReading.Coverage(silence, Heard("no gracias", RecognitionMode.Constrained)));
        Assert.Null(ForcedReading.Coverage(TestSupport.Clip(), noWords));
        Assert.Null(ForcedReading.Coverage(TestSupport.Clip(), unknownOnly));
        Assert.Null(ForcedReading.Coverage(new PcmClip([], PcmClip.DefaultSampleRate, "empty"), Heard("no gracias", RecognitionMode.Constrained)));
        // None of these can fire the coverage trigger: the row is trusted as it was.
        Assert.Equal(1, CallsFor(Heard("no gracias", RecognitionMode.Constrained, 0.1), open, On with { OpenPassOnLooseMatch = false }, silence));
        Assert.Equal(1, CallsFor(noWords, open, On with { OpenPassOnLooseMatch = false }));
    }

    private static int CallsFor(AsrResult heard, AsrResult open, PipelineOptions options, PcmClip clip)
    {
        Analyze("stall-offer", heard, open, options, out var recognizer, clip);
        return recognizer.CallCount;
    }

    [Fact]
    public void AnUnknownTokenIsNotCoveredSpeech()
    {
        // «[unk] no gracias»: the unknown token takes the first half of the second, the row's words the rest. Only the row's words count.
        var heard = Heard("[unk] no gracias", RecognitionMode.Constrained);
        var share = ForcedReading.Coverage(TestSupport.Clip(), heard)!.Value;

        Assert.InRange(share, 0.6, 0.7);
    }

    // ---- Which reading wins ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void AFreeReadingWithTheSameIntentLeavesTheFirstReading()
    {
        // «dónde está el río» is the row, said over the clip's first quarter; the free decode hears a place question as well: the same intent, so the first stands.
        var first = Heard("dónde está el río", RecognitionMode.Constrained, 0.25);
        var open = Heard("dónde está la panadería", RecognitionMode.Open);
        var analysis = Analyze("fisher-open", first, open, On, out var recognizer);

        Assert.Equal(2, recognizer.CallCount);
        Assert.Equal(RecognitionMode.Constrained, analysis.Asr.Mode);
        Assert.Equal(first, analysis.Asr);
        Assert.Equal(open, analysis.OtherPass);
        Assert.Equal("ask_directions", Intent(analysis.Verdict));
    }

    [Fact]
    public void AFreeReadingThatIsNotTakenLeavesTheFirstReading()
    {
        var forced = Heard("me cuesta el río", RecognitionMode.Constrained);
        var gato = Heard("el gato duerme", RecognitionMode.Open);
        var analysis = Analyze("stall-need", forced, gato, On, out var recognizer);

        Assert.Equal(2, recognizer.CallCount);
        Assert.Equal("ask_price", Intent(analysis.Verdict));
        Assert.Equal(forced, analysis.Asr);
        Assert.Equal(gato, analysis.OtherPass);
    }

    [Fact]
    public void AFreeReadingWithAWordUnderTheFloorLeavesTheFirstReadingAndTheFloorIsTheOptions()
    {
        var forced = Heard("me cuesta el río", RecognitionMode.Constrained);
        var weak = new AsrResult("me gusta el río", [new AsrHypothesis("me gusta el río", 1.0)], Heard("me gusta el río", RecognitionMode.Open).Words.Select(static (w, i) => i == 2 ? w with { Confidence = 0.4 } : w).ToList(), 1.0, "scripted-test", RecognitionMode.Open);

        Assert.Equal("ask_price", Intent(Analyze("stall-need", forced, weak, On, out _).Verdict));
        Assert.Equal("state_like", Intent(Analyze("stall-need", forced, weak, On with { OpenPassWordFloor = 0.4 }, out _).Verdict));
        Assert.Equal("state_like", Intent(Analyze("stall-need", forced, weak, On with { OpenPassWordFloor = 0.0 }, out _).Verdict));
    }

    [Fact]
    public void ACorrectRowThatFiresTheTriggerKeepsItsReadingWhateverTheFreeDecodeHears()
    {
        // A fuzzy match of the right row (the learner's slip) fires the trigger; the free decode hears the same words, so the intent is the same and the row stands.
        var heard = Heard("quiero dos pan por favor", RecognitionMode.Constrained);
        var open = Heard("quiero dos panes por favor", RecognitionMode.Open);
        var analysis = Analyze("baker-open", heard, open, On, out var recognizer);
        var plain = Analyze("baker-open", heard, open, null, out _);

        Assert.Equal(2, recognizer.CallCount);
        Assert.Equal(RecognitionMode.Constrained, analysis.Asr.Mode);
        Assert.Equal(CanonicalJson.Serialize(plain.Verdict), CanonicalJson.Serialize(analysis.Verdict));
    }

    [Theory]
    [InlineData("baker-open", "quiero dos panes por favor")]
    [InlineData("smith-open", "hola")]
    [InlineData("stall-offer", "no gracias")]
    [InlineData("farmer-open", "cuánto cuesta")]
    public void AnAnswerRowHeardWholeDecodesOnceAndIsTheOptionOffResultByteForByte(string promptId, string row)
    {
        var heard = Heard(row, RecognitionMode.Constrained);
        var open = Heard("me gusta el río", RecognitionMode.Open);
        var on = Analyze(promptId, heard, open, On, out var recognizer);
        var off = Analyze(promptId, heard, open, null, out _);

        Assert.Equal(1, recognizer.CallCount);
        Assert.Null(on.OtherPass);
        Assert.Equal(CanonicalJson.Serialize(off.Verdict), CanonicalJson.Serialize(on.Verdict));
        Assert.Equal(off.Asr, on.Asr);
    }

    // ---- The options --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheOptionsDefaultToTheLooseMatchOnAndACoverageOfThreeFifths()
    {
        Assert.True(PipelineOptions.Default.OpenPassOnLooseMatch);
        Assert.Equal(0.6, PipelineOptions.Default.OpenPassMinCoverage);
        Assert.Equal(PipelineOptions.DefaultOpenPassMinCoverage, new PipelineOptions().OpenPassMinCoverage);
        Assert.False(PipelineOptions.Default.OpenRecognitionFallback);
    }

    [Fact]
    public void TheCoverageIsValidatedByThePipeline()
    {
        var services = TestSupport.Services("x", RecognitionMode.Constrained);
        foreach (var value in new[] { -0.01, 1.01, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UtterancePipeline(services, new PipelineOptions { OpenPassMinCoverage = value }));
        }

        foreach (var value in new[] { 0.0, 0.6, 1.0 })
        {
            Assert.NotNull(new UtterancePipeline(services, new PipelineOptions { OpenPassMinCoverage = value }));
        }
    }

    [Fact]
    public void WithBothSignalsOffTheFallbackIsTheM80OneAndAUntakenFirstReadingStillGetsTheFreePass()
    {
        var forced = Heard("me cuesta el río", RecognitionMode.Constrained);
        var untaken = Heard(string.Empty, RecognitionMode.Constrained);
        var open = Heard("me gusta el río", RecognitionMode.Open);

        Assert.Equal(1, CallsOf("stall-need", forced, open, OnlyUntaken));
        Assert.Equal(2, CallsOf("stall-need", untaken, open, OnlyUntaken));
    }

    private static int CallsOf(string promptId, AsrResult heard, AsrResult open, PipelineOptions options)
    {
        Analyze(promptId, heard, open, options, out var recognizer);
        return recognizer.CallCount;
    }

    [Theory]
    [InlineData("stall-need", "me cuesta el río")]
    [InlineData("stall-offer", "no gracias")]
    public void WithTheFallbackOffNothingChangesWhateverTheSignalsSay(string promptId, string heard)
    {
        var forced = Heard(heard, RecognitionMode.Constrained, 0.1);
        var open = Heard("me gusta el río", RecognitionMode.Open);
        var defaults = Analyze(promptId, forced, open, null, out var recognizer);
        var explicitOff = Analyze(promptId, forced, open, new PipelineOptions { OpenRecognitionFallback = false, OpenPassOnLooseMatch = true, OpenPassMinCoverage = 1.0 }, out var again);

        Assert.Equal(1, recognizer.CallCount);
        Assert.Equal(1, again.CallCount);
        Assert.Null(defaults.OtherPass);
        Assert.Equal(CanonicalJson.Serialize(defaults.Verdict), CanonicalJson.Serialize(explicitOff.Verdict));
        Assert.Equal(RecognitionMode.Constrained, explicitOff.Asr.Mode);
    }

    [Fact]
    public void AnOpenPromptNeverDecodesTwiceWhateverTheSignalsSay()
    {
        var recognizer = new ScriptedWordRecognizer(Heard("no gracias", RecognitionMode.Constrained, 0.1), Heard("el gato duerme", RecognitionMode.Open));
        var services = TestSupport.Services("x", RecognitionMode.Open).WithRecognizers(recognizer, null);

        new UtterancePipeline(services, On with { OpenPassMinCoverage = 1.0 }).Analyze(TestSupport.Clip(), TestSupport.Bundle.Prompts[TestSupport.OpenPrompt]);

        Assert.Equal(1, recognizer.CallCount);
    }

    // ---- Replay -----------------------------------------------------------------------------------------------------------------------------

    private static PcmClip Empty(string hash) => new([], PcmClip.DefaultSampleRate, hash);

    [Fact]
    public void AForcedRowReplacedByTheFreeReadingIsRecordedAsFreeAndReplaysThroughPipelinesWithTheOptionOnAndOff()
    {
        var recognizer = new ScriptedWordRecognizer(Heard("me cuesta el río", RecognitionMode.Constrained), Heard("me gusta el río", RecognitionMode.Open));
        var services = TestSupport.Services("x", RecognitionMode.Constrained, phonemes: true);
        var live = new UtterancePipeline(services.WithRecognizers(recognizer, services.PhonemeRecognizer), On);
        var context = TestSupport.Context(agentId: "stall");
        var trace = live.Trace(TestSupport.Clip(), TestSupport.Bundle.Prompts["stall-need"], context);
        var envelope = ReplayEnvelope.From(trace, context);

        Assert.Equal(RecognitionMode.Open, envelope.Asr.Mode);
        Assert.Equal("state_like", Intent(trace.Result.Verdict));
        foreach (var options in new PipelineOptions?[] { On, null })
        {
            var offline = new UtterancePipeline(TestSupport.Services("never used", RecognitionMode.Constrained, phonemes: true), options);

            Assert.Null(Replayer.Replay(envelope, offline, Empty));
        }
    }

    [Fact]
    public void AKeptForcedLookingRowReplaysTheSameThroughPipelinesWithTheOptionOnAndOff()
    {
        var recognizer = new ScriptedWordRecognizer(Heard("me cuesta el río", RecognitionMode.Constrained), Heard("el gato duerme", RecognitionMode.Open));
        var services = TestSupport.Services("x", RecognitionMode.Constrained);
        var live = new UtterancePipeline(services.WithRecognizers(recognizer, null), On);
        var context = TestSupport.Context(agentId: "stall");
        var envelope = ReplayEnvelope.From(live.Trace(TestSupport.Clip(), TestSupport.Bundle.Prompts["stall-need"], context), context);

        Assert.Equal(RecognitionMode.Constrained, envelope.Asr.Mode);
        foreach (var options in new PipelineOptions?[] { On, null })
        {
            var offline = new UtterancePipeline(TestSupport.Services("never used", RecognitionMode.Constrained), options);

            Assert.Null(Replayer.Replay(envelope, offline, Empty));
        }
    }
}
