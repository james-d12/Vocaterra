using System.Text.Json.Nodes;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Tests for the two-phase evaluation (CR-017): <see cref="UtterancePipeline.Analyze"/> then <see cref="UtterancePipeline.Complete(UtteranceAnalysis, ConversationContext)"/>.</summary>
public sealed class TwoPhaseTests
{
    // Every example sentence of every intent against every prompt that allows the intent (plus the answer-only prompts with their own answers).
    [Fact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void EvaluateEqualsPhaseTwoOfPhaseOneForEveryExampleOnEveryAllowingPrompt()
    {
        var bundle = TestSupport.Bundle;
        var recognizer = new SwitchableRecognizer();
        var pipeline = new UtterancePipeline(new PipelineServices(recognizer, null, null, null, bundle));
        var context = TestSupport.Context();
        var clip = TestSupport.Clip();
        var compared = 0;

        foreach (var prompt in bundle.Prompts.Values.OrderBy(static p => p.PromptId, StringComparer.Ordinal))
        {
            var sentences = prompt.AllowedIntentIds.Count == 0
                ? bundle.AnswerSets[prompt.AnswerSetId].Answers.Select(static a => a.Canonical)
                : bundle.Intents.Intents.Where(i => prompt.AllowedIntentIds.Contains(i.Id.Value)).SelectMany(static i => i.Examples);
            foreach (var sentence in sentences.Distinct(StringComparer.Ordinal))
            {
                recognizer.Transcript = sentence;
                var direct = Outcome(() => pipeline.Evaluate(clip, prompt, context));
                var twoPhase = Outcome(() => pipeline.Complete(pipeline.Analyze(clip, prompt), context));

                Assert.Equal(direct.Error, twoPhase.Error);
                if (direct.Result is { } expected)
                {
                    var actual = twoPhase.Result!;
                    Assert.Equal(TestSupport.Json(expected), TestSupport.Json(actual));
                    Assert.Equal(expected, actual);
                    Assert.Equal(expected.Replay.HostStateHash, actual.Replay.HostStateHash);
                    compared++;
                }
            }
        }

        Assert.True(compared > 100, $"only {compared} comparisons ran");
    }

    [Fact]
    public void TheReadmeDemoGivesTheSameTwoErrorsInBothPaths()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample);
        var prompt = TestSupport.Bundle.Prompts[TestSupport.OpenPrompt];
        var context = TestSupport.Context();

        var analysis = pipeline.Analyze(TestSupport.Clip(), prompt);
        var result = pipeline.Complete(analysis, context);

        Assert.Equal(["conj.person", "noun.number"], result.Verdict.Errors.Select(static e => e.Tag).ToArray());
        Assert.Equal(TestSupport.Json(pipeline.Evaluate(TestSupport.Clip(), prompt, context)), TestSupport.Json(result));
    }

    [Fact]
    public void PhaseOneExposesTheVerdictWordsAndAnswerThatThePhaseTwoResultCarries()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample);
        var analysis = pipeline.Analyze(TestSupport.Clip(), TestSupport.Bundle.Prompts[TestSupport.OpenPrompt]);

        var result = pipeline.Complete(analysis, TestSupport.Context());

        Assert.Equal(result.Verdict, analysis.Verdict);
        Assert.Equal(result.Words, analysis.Words);
        Assert.Equal(result.Answer, analysis.Answer);
        Assert.Contains(analysis.Words!, static word => word.Token.Text == "espada");
        Assert.Equal(TestSupport.WorkedExample, analysis.Asr.Transcript);
    }

    [Fact]
    public void HostSlotsComputedFromPhaseOneWordsRenderInThePhaseTwoReply()
    {
        var bundle = EditedBundle(pools => SetVariants(pools, "villager_give", "Qué {noun} tan buena, {mood}.", "Qué {noun} tan buena, {mood}. ¡Toma!"));
        var recognizer = new SwitchableRecognizer { Transcript = TestSupport.WorkedExample };
        var pipeline = new UtterancePipeline(new PipelineServices(recognizer, null, null, null, bundle));
        var prompt = bundle.Prompts[TestSupport.OpenPrompt];

        var analysis = pipeline.Analyze(TestSupport.Clip(), prompt);
        var noun = analysis.Words!.First(static w => w.Reading?.Pos == "noun").Token.Text;
        var host = SlotMap.From([new KeyValuePair<string, string>("noun", noun), new KeyValuePair<string, string>("mood", "bien")]);
        var result = pipeline.Complete(analysis, TestSupport.Context(), host, ["word.espada", "intent.request_item"]);

        Assert.Equal("espada", noun);
        var reply = result.Response.Lines.Single(static line => line.LineId.StartsWith("villager_give", StringComparison.Ordinal));
        Assert.Contains("espada tan buena, bien.", reply.Text, StringComparison.Ordinal);
        Assert.NotNull(result.Decision.Learner.Find("word.espada"));
        Assert.Equal(1, recognizer.Calls);

        // The overload is the same as building the context with the facts and completing it.
        var viaContext = pipeline.Complete(analysis, TestSupport.Context().WithHost(host, ["word.espada", "intent.request_item"]));
        Assert.Equal(result, viaContext);
        Assert.Equal(viaContext.Replay.HostStateHash, result.Replay.HostStateHash);
    }

    [Fact]
    public void ThePhaseOneVerdictSlotWinsOverAHostSlotOfTheSameName()
    {
        var bundle = EditedBundle(pools => SetVariants(pools, "villager_give", "Una {item}.", "Una {item}."));
        var recognizer = new SwitchableRecognizer { Transcript = TestSupport.WorkedExample };
        var pipeline = new UtterancePipeline(new PipelineServices(recognizer, null, null, null, bundle));
        var analysis = pipeline.Analyze(TestSupport.Clip(), bundle.Prompts[TestSupport.OpenPrompt]);

        var result = pipeline.Complete(analysis, TestSupport.Context(), SlotMap.From([new KeyValuePair<string, string>("item", "martillo")]));

        Assert.Contains(result.Response.Lines, static line => line.Text == "Una espada.");
    }

    [Fact]
    public void TheRecognizerRunsOnceForTheWholeTwoPhaseEvaluationEvenWhenCompletedTwice()
    {
        var recognizer = new SwitchableRecognizer { Transcript = TestSupport.WorkedExample };
        var pipeline = new UtterancePipeline(new PipelineServices(recognizer, null, null, null, TestSupport.Bundle));

        var analysis = pipeline.Analyze(TestSupport.Clip(), TestSupport.Bundle.Prompts[TestSupport.OpenPrompt]);
        var first = pipeline.Complete(analysis, TestSupport.Context());
        var second = pipeline.Complete(analysis, TestSupport.Context(counter: 5));

        Assert.Equal(1, recognizer.Calls);
        Assert.Equal(first.Verdict, second.Verdict);
    }

    [Fact]
    public void ReplayOfATwoPhaseEvaluationReproducesTheResult()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample);
        var prompt = TestSupport.Bundle.Prompts[TestSupport.OpenPrompt];
        var context = TestSupport.Context().WithHost(SlotMap.From([new KeyValuePair<string, string>("noun", "espada")]), ["word.espada"]);

        var analysis = pipeline.Analyze(TestSupport.Clip(), prompt);
        var trace = pipeline.CompleteTrace(analysis, context);
        using var writer = new StringWriter();
        new ReplayLog(writer).Append(ReplayEnvelope.From(trace, context));
        var envelope = ReplayReader.ReadEnvelopes(new StringReader(writer.ToString())).Single();

        Assert.Null(Replayer.Replay(envelope, pipeline, static hash => new PcmClip([], PcmClip.DefaultSampleRate, hash)));
        Assert.Equal(pipeline.Trace(TestSupport.Clip(), prompt, context).Result, trace.Result);
        Assert.Equal(analysis.Asr, trace.Asr);
    }

    [Fact]
    public void AnAnalysisFromAPipelineOverOtherModelsIsRejected()
    {
        var other = new UtterancePipeline(new PipelineServices(new SwitchableRecognizer { Transcript = "hola", Hash = "other" }, null, null, null, TestSupport.Bundle));
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample);
        var analysis = other.Analyze(TestSupport.Clip(), TestSupport.Bundle.Prompts[TestSupport.OpenPrompt]);

        Assert.Throws<ArgumentException>(() => pipeline.Complete(analysis, TestSupport.Context()));
    }

    [Fact]
    public async Task AnAnalysisCanBeCompletedFromAnotherThread()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample);
        var analysis = pipeline.Analyze(TestSupport.Clip(), TestSupport.Bundle.Prompts[TestSupport.OpenPrompt]);
        var expected = pipeline.Complete(analysis, TestSupport.Context());

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => pipeline.Complete(analysis, TestSupport.Context()))));

        Assert.All(results, result => Assert.Equal(expected, result));
    }

    [Fact]
    public void NullArgumentsAreRejected()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample);
        var analysis = pipeline.Analyze(TestSupport.Clip(), TestSupport.Bundle.Prompts[TestSupport.OpenPrompt]);

        Assert.Throws<ArgumentNullException>(() => pipeline.Analyze(null!, TestSupport.Bundle.Prompts[TestSupport.OpenPrompt]));
        Assert.Throws<ArgumentNullException>(() => pipeline.Complete(null!, TestSupport.Context()));
        Assert.Throws<ArgumentNullException>(() => pipeline.Complete(analysis, null!));
    }

    private static (PipelineResult? Result, string? Error) Outcome(Func<PipelineResult> run)
    {
        try
        {
            return (run(), null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return (null, $"{ex.GetType().FullName}: {ex.Message}");
        }
    }

    internal static void SetVariants(JsonObject pools, string poolId, params string[] texts)
    {
        var variants = new JsonArray();
        for (var i = 0; i < texts.Length; i++)
        {
            variants.Add(new JsonObject { ["lineId"] = $"{poolId}.{i + 1}", ["text"] = texts[i] });
        }

        pools[poolId] = new JsonObject { ["poolId"] = poolId, ["variants"] = variants };
    }

    // A copy of the bundle with edited dialogue pools; hashes are not verified because the copy was edited.
    private static ContentBundle EditedBundle(Action<JsonObject> editPools)
    {
        return FixtureBundle.Create(editPools);
    }

    private sealed class SwitchableRecognizer : IWordRecognizer
    {
        private int _calls;

        public string Transcript { get; set; } = string.Empty;

        public string Hash { get; init; } = "scripted-test";

        public int Calls => Volatile.Read(ref _calls);

        public string ModelHash => Hash;

        public AsrResult Recognize(PcmClip clip, RecognitionRequest request)
        {
            Interlocked.Increment(ref _calls);
            return TestSupport.Asr(Transcript, modelHash: Hash);
        }
    }
}

/// <summary>
/// A copy of the bundle (<c>content/game-v1</c>) with edited dialogue pools and/or one agent file, for tests that need data the game does not have. The copy
/// keeps the repository bundle's manifest, so its bundle hash is the repository's, and it is loaded without hash verification because it was edited.
/// </summary>
internal static class FixtureBundle
{
    public static ContentBundle Create(Action<JsonObject>? editPools, string? agentId = null, Action<JsonObject>? editAgent = null)
    {
        using var copy = new TempBundle(rehash: false);
        if (editPools is not null)
        {
            copy.EditJson("dialogue/dialogue.json", root => editPools(root["pools"]!.AsObject()));
        }

        if (editAgent is not null)
        {
            copy.EditJson($"agents/{agentId}.json", root => editAgent(root.AsObject()));
        }

        return copy.Load();
    }
}
