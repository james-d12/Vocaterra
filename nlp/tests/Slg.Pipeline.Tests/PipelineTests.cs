using Slg.Asr;
using Slg.Core.Audio;
using Slg.Core.Prompts;
using Slg.Core.Stages;
using Slg.Core.Taxonomy;
using Slg.Core.Verdicts;
using Slg.Decision.Feedback;
using Slg.Decision.Scheduling;
using Slg.Nlp.Intent;
using Slg.Pronunciation;

namespace Slg.Pipeline.Tests;

/// <summary>Tests for <see cref="UtterancePipeline"/>: the worked example, stage order, determinism, decisions, responses and repair.</summary>
public class PipelineTests
{
    private static PromptSpec Prompt(string id)
    {
        return TestSupport.Bundle.Prompts[id];
    }

    [Fact]
    public void WorkedExampleProducesTheHldVerdict()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample);

        var verdict = pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context()).Verdict;

        Assert.Equal("offer_item", verdict.Intent.Value);
        Assert.Equal("espada", verdict.Slots["item"]);
        Assert.Equal("2", verdict.Slots["qty"]);
        Assert.True(verdict.Communicated);
        Assert.Equal(ResolvedBy.Grammar, verdict.ResolvedBy);
        Assert.Equal([ErrorTags.ConjPerson, ErrorTags.NounNumber], verdict.Errors.Select(static error => error.Tag).ToArray());
        Assert.Equal(("tiene", "tengo"), (verdict.Errors[0].Actual, verdict.Errors[0].Expected));
        Assert.Equal(("espada", "espadas"), (verdict.Errors[1].Actual, verdict.Errors[1].Expected));
        Assert.Equal(0.5, verdict.Correctness, 6);
    }

    [Fact]
    public void WorkedExampleIsRecastFromThePlayersOwnSlots()
    {
        var result = TestSupport.Pipeline(TestSupport.WorkedExample)
            .Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context());

        var recast = Assert.Single(result.Response.Lines, static line => line.IsRecast);
        Assert.Contains("tienes dos espadas", recast.Text, StringComparison.Ordinal);
        Assert.True(result.Response.Lines[0].IsRecast, "A recast is spoken before the reply.");
        Assert.Equal(2, result.Response.Lines.Count);
        Assert.StartsWith("offer_item_reply.", result.Response.Lines[1].LineId, StringComparison.Ordinal);
        Assert.Equal(Rating.Hard, result.Decision.Rating);
        Assert.Equal("done", result.Decision.Npc!.Next.StateId);
        Assert.Equal(FeedbackType.Recast, result.Decision.Feedback.Items[0].Type);
    }

    [Fact]
    public void ReplayRecordCarriesTheClipPromptStateAndVerdict()
    {
        var clip = TestSupport.Clip();
        var context = TestSupport.Context();

        var result = TestSupport.Pipeline(TestSupport.WorkedExample).Evaluate(clip, Prompt(TestSupport.OfferPrompt), context);

        Assert.Equal(clip.ClipHash, result.Replay.ClipHash);
        Assert.Equal(TestSupport.OfferPrompt, result.Replay.PromptId);
        Assert.Equal(context.GameStateHash, result.Replay.GameStateHash);
        Assert.Equal(result.Verdict, result.Replay.Verdict);
        Assert.Equal(TestSupport.Bundle.ContentHash, result.Replay.Versions.ContentHash);
    }

    [Fact]
    public void SameInputsGiveByteIdenticalResults()
    {
        var clip = TestSupport.Clip();
        var context = TestSupport.Context(counter: 5);
        var prompt = Prompt(TestSupport.OfferPrompt);

        var first = TestSupport.Json(TestSupport.Pipeline(TestSupport.WorkedExample, phonemes: true).Evaluate(clip, prompt, context));
        var second = TestSupport.Json(TestSupport.Pipeline(TestSupport.WorkedExample, phonemes: true).Evaluate(clip, prompt, context));

        Assert.Equal(first, second);
    }

    [Fact]
    public void ReusingOnePipelineGivesTheSameResult()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample, phonemes: true);
        var clip = TestSupport.Clip();
        var context = TestSupport.Context();
        var prompt = Prompt(TestSupport.OfferPrompt);

        var first = pipeline.Evaluate(clip, prompt, context);
        var second = pipeline.Evaluate(clip, prompt, context);

        Assert.Equal(TestSupport.Json(first), TestSupport.Json(second));
        Assert.Equal(first, second);
    }

    [Fact]
    public void ConcurrentAndSequentialRecognitionAgree()
    {
        var clip = TestSupport.Clip();
        var context = TestSupport.Context();
        var prompt = Prompt(TestSupport.OfferPrompt);

        var concurrent = TestSupport.Pipeline(TestSupport.WorkedExample, phonemes: true, options: new PipelineOptions { RunRecognizersConcurrently = true });
        var sequential = TestSupport.Pipeline(TestSupport.WorkedExample, phonemes: true, options: new PipelineOptions { RunRecognizersConcurrently = false });

        Assert.Equal(TestSupport.Json(sequential.Evaluate(clip, prompt, context)), TestSupport.Json(concurrent.Evaluate(clip, prompt, context)));
    }

    [Fact]
    public void ManyThreadsEvaluatingIndependentPipelinesAgree()
    {
        var clip = TestSupport.Clip();
        var context = TestSupport.Context();
        var prompt = Prompt(TestSupport.OfferPrompt);
        var results = new string[8];

        Parallel.For(0, results.Length, i => results[i] = TestSupport.Json(TestSupport.Pipeline(TestSupport.WorkedExample, phonemes: true).Evaluate(clip, prompt, context)));

        Assert.All(results, result => Assert.Equal(results[0], result));
    }

    [Fact]
    public void OnePipelineCanBeSharedAcrossThreads()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample, phonemes: true);
        var clip = TestSupport.Clip();
        var context = TestSupport.Context();
        var prompt = Prompt(TestSupport.OfferPrompt);
        var results = new string[8];

        Parallel.For(0, results.Length, i => results[i] = TestSupport.Json(pipeline.Evaluate(clip, prompt, context)));

        Assert.All(results, result => Assert.Equal(results[0], result));
    }

    [Fact]
    public void ADifferentInteractionCounterCanChangeOnlyTheDrawnVariants()
    {
        var pipeline = TestSupport.Pipeline("quiero comprar una espada");
        var prompt = Prompt("buy-item-open");
        var lineIds = new HashSet<string>(StringComparer.Ordinal);

        for (var counter = 0; counter < 16; counter++)
        {
            var result = pipeline.Evaluate(TestSupport.Clip(), prompt, TestSupport.Context(counter: counter));

            Assert.True(result.Verdict.Communicated);
            Assert.Empty(result.Verdict.Errors);
            var line = Assert.Single(result.Response.Lines);
            var variant = Assert.Single(result.Decision.Npc!.Actions).VariantIndex;
            Assert.Equal($"buy_item_reply.{variant + 1}", line.LineId);
            lineIds.Add(line.LineId);
        }

        Assert.True(lineIds.Count > 1, "Sixteen interactions should draw more than one of the three variants.");
    }

    [Fact]
    public void OpenPromptsAskTheRecognizerForOpenRecognition()
    {
        var recognizer = new ScriptedWordRecognizer(TestSupport.Asr(TestSupport.WorkedExample));
        var pipeline = new UtterancePipeline(new PipelineServices(recognizer, null, null, null, TestSupport.Bundle));

        pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context());

        Assert.Equal(1, recognizer.CallCount);
        Assert.Equal(RecognitionMode.Open, recognizer.LastRequest!.Mode);
        Assert.Empty(recognizer.LastRequest.GrammarPhrases);
    }

    [Fact]
    public void ConstrainedPromptsBuildTheGrammarFromTheAnswerSet()
    {
        var recognizer = new ScriptedWordRecognizer(TestSupport.Asr("espada", RecognitionMode.Constrained));
        var pipeline = new UtterancePipeline(new PipelineServices(recognizer, null, null, null, TestSupport.Bundle));
        var prompt = Prompt("quiz-item-name");

        pipeline.Evaluate(TestSupport.Clip(), prompt, TestSupport.Context());

        var request = recognizer.LastRequest!;
        Assert.Equal(RecognitionMode.Constrained, request.Mode);
        var expected = GrammarBuilder.BuildList(TestSupport.Bundle.AnswerSets[prompt.AnswerSetId].GrammarPhrases());
        Assert.Equal(expected, request.GrammarPhrases);
        Assert.Equal(GrammarBuilder.UnknownToken, request.GrammarPhrases[^1]);
        Assert.Contains("espada", request.GrammarPhrases);
    }

    [Fact]
    public void RecognizersRunInTheStatedOrderWhenSequential()
    {
        var log = new List<string>();
        var words = new LoggingWordRecognizer(log, TestSupport.Asr(TestSupport.WorkedExample));
        var phonemes = new LoggingPhonemeRecognizer(log, TestSupport.HearsCorrectly(TestSupport.WorkedExample));
        var pipeline = new UtterancePipeline(
            new PipelineServices(words, phonemes, null, null, TestSupport.Bundle),
            new PipelineOptions { RunRecognizersConcurrently = false });

        pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context());

        Assert.Equal(["asr", "phoneme"], log);
    }

    [Fact]
    public void BothRecognizersRunExactlyOnceWhenConcurrent()
    {
        var log = new List<string>();
        var words = new LoggingWordRecognizer(log, TestSupport.Asr(TestSupport.WorkedExample));
        var phonemes = new LoggingPhonemeRecognizer(log, TestSupport.HearsCorrectly(TestSupport.WorkedExample));
        var pipeline = new UtterancePipeline(new PipelineServices(words, phonemes, null, null, TestSupport.Bundle));

        pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context());

        Assert.Equal(["asr", "phoneme"], log.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ARecognizerFailureSurfacesWithItsOwnExceptionType()
    {
        var pipeline = new UtterancePipeline(new PipelineServices(new ThrowingWordRecognizer(), TestSupport.HearsCorrectly("hola"), null, null, TestSupport.Bundle));

        Assert.Throws<NotSupportedException>(() => pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context()));
    }

    [Fact]
    public void PronunciationIsSkippedWithoutAPhonemeRecognizer()
    {
        var verdict = TestSupport.Pipeline(TestSupport.WorkedExample)
            .Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context()).Verdict;

        Assert.Empty(verdict.Pronunciation);
        Assert.DoesNotContain(verdict.Errors, static error => ErrorTags.IsPronunciation(error.Tag));
    }

    [Theory]
    [InlineData(ScoringMode.Gop)]
    [InlineData(ScoringMode.Greedy)]
    public void CorrectPhonesGivePerfectPronunciationScores(ScoringMode mode)
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample, phonemes: true, options: new PipelineOptions { PronunciationMode = mode });

        var verdict = pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context()).Verdict;

        Assert.NotEmpty(verdict.Pronunciation);
        Assert.All(verdict.Pronunciation, static score => Assert.Equal(1.0, score.Score, 6));
        Assert.DoesNotContain(verdict.Errors, static error => ErrorTags.IsPronunciation(error.Tag));
    }

    [Fact]
    public void ARolledToEnglishRSoundIsReportedAsPronR()
    {
        const string text = "quiero comprar un arco";
        var recognizer = TestSupport.HearsCorrectly(text, new Dictionary<string, string> { ["ɾ"] = "ɹ" });
        var services = TestSupport.Services(text, phonemeRecognizer: recognizer);

        var verdict = new UtterancePipeline(services).Evaluate(TestSupport.Clip(), Prompt("buy-item-open"), TestSupport.Context()).Verdict;

        Assert.Contains(verdict.Errors, static error => error.Tag == ErrorTags.PronR);
        Assert.True(verdict.Communicated);
    }

    [Fact]
    public void VersionStampListsEveryProvidedModel()
    {
        var services = TestSupport.Services(TestSupport.WorkedExample, phonemes: true, embeddings: true, synthesizer: true);

        Assert.Equal(TestSupport.Bundle.ContentHash, services.Versions.ContentHash);
        Assert.Equal(["asr", "embedding", "phoneme", "tts"], services.Versions.ModelHashes.Keys.ToArray());
        Assert.Equal("scripted-test", services.Versions.ModelHashes["asr"]);
        Assert.Equal("fake-trigram-v1", services.Versions.ModelHashes["embedding"]);
        Assert.Equal("fake-phoneme-recognizer", services.Versions.ModelHashes["phoneme"]);
        Assert.Equal("silent-synthesizer-v1", services.Versions.ModelHashes["tts"]);
    }

    [Fact]
    public void VersionStampOmitsModelsThatAreNotProvided()
    {
        var services = TestSupport.Services(TestSupport.WorkedExample);

        Assert.Equal(["asr"], services.Versions.ModelHashes.Keys.ToArray());
    }

    [Fact]
    public void TheVerdictCarriesTheServicesVersionStamp()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample, phonemes: true);

        var verdict = pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context()).Verdict;

        Assert.Equal(pipeline.Services.Versions, verdict.Versions);
    }

    [Fact]
    public void AnUnrelatedUtteranceIsNotCommunicatedAndTheNpcAsksAgain()
    {
        var pipeline = TestSupport.Pipeline("xyzzy plugh");

        var result = pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context());

        Assert.False(result.Verdict.Communicated);
        Assert.True(result.Verdict.Intent.IsUnknown);
        Assert.StartsWith("not_understood.", Assert.Single(result.Response.Lines).LineId, StringComparison.Ordinal);
        Assert.Equal(RepairKind.AskAgain, result.Repair!.Kind);
        Assert.Equal(Rating.Again, result.Decision.Rating);
    }

    [Theory]
    [InlineData(0, RepairKind.AskAgain)]
    [InlineData(1, RepairKind.AskAgain)]
    [InlineData(2, RepairKind.SlowRepeat)]
    [InlineData(3, RepairKind.OfferChoices)]
    [InlineData(4, RepairKind.GiveUp)]
    public void TheRepairStepFollowsTheNotCommunicatedRepeatCount(int repeats, RepairKind expected)
    {
        var pipeline = TestSupport.Pipeline("xyzzy plugh", RecognitionMode.Constrained);
        var counts = new Dictionary<string, int> { [FeedbackPolicy.NotCommunicatedTag] = repeats };

        var result = pipeline.Evaluate(TestSupport.Clip(), Prompt("quiz-item-name"), TestSupport.Context(repeats: counts));

        Assert.Equal(expected, result.Repair!.Kind);
        if (expected == RepairKind.OfferChoices)
        {
            Assert.Equal(RecognitionMode.Constrained, result.Repair.RetryRequest!.Mode);
            Assert.Equal(RepairStrategy.MaxChoices, result.Repair.RetryRequest.GrammarPhrases.Count);
        }
    }

    [Fact]
    public void ACommunicatedUtteranceHasNoRepairStep()
    {
        var result = TestSupport.Pipeline(TestSupport.WorkedExample).Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context());

        Assert.Null(result.Repair);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[unk]")]
    [InlineData("  ")]
    public void AnEmptyOrUnknownTranscriptIsHandledWithoutErrors(string transcript)
    {
        var pipeline = TestSupport.Pipeline(transcript, phonemes: true);

        var result = pipeline.Evaluate(PcmClip.FromSamples([]), Prompt(TestSupport.OfferPrompt), TestSupport.Context());

        Assert.False(result.Verdict.Communicated);
        Assert.Empty(result.Verdict.Errors);
        Assert.Empty(result.Verdict.Pronunciation);
        Assert.NotNull(result.Repair);
        Assert.Single(result.Response.Lines);
    }

    [Fact]
    public void UnknownWordsAreReportedAsVocabUnknown()
    {
        var result = TestSupport.Pipeline("yo tengo dos zorkmids").Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context());

        Assert.Contains(result.Verdict.Errors, static error => error.Tag == ErrorTags.VocabUnknown && error.Actual == "zorkmids");
    }

    [Fact]
    public void WithoutAnNpcNobodyRepliesAndAPerfectUtteranceHasNoLines()
    {
        var prompt = Prompt("spell-cast-open");
        var context = TestSupport.Context(state: "none", npcId: "player");

        var result = TestSupport.Pipeline("lanzo fuego").Evaluate(TestSupport.Clip(), prompt, context);

        Assert.Null(result.Decision.Npc);
        Assert.True(result.Verdict.Communicated);
        Assert.Empty(result.Verdict.Errors);
        Assert.Empty(result.Response.Lines);
        var effect = Assert.Single(result.Effects);
        Assert.Equal("spell_power", effect.Kind);
        Assert.Equal(1.0, effect.Value, 6);
    }

    [Fact]
    public void WithoutAnNpcOnlyFeedbackIsVoiced()
    {
        var prompt = Prompt("spell-cast-open");
        var context = TestSupport.Context(state: "none", npcId: "player");

        var result = TestSupport.Pipeline("lanzo el fuegos").Evaluate(TestSupport.Clip(), prompt, context);

        Assert.Null(result.Decision.Npc);
        Assert.NotEmpty(result.Verdict.Errors);
        var line = Assert.Single(result.Response.Lines);
        Assert.StartsWith("feedback.", line.LineId, StringComparison.Ordinal);
        Assert.DoesNotContain("__silent__", line.LineId, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePlaceholderAnswerSetIdIsNeverSpokenAsAnExpectedForm()
    {
        var counts = new Dictionary<string, int> { [ErrorTags.VocabWrong] = 4 };
        var prompt = Prompt("spell-cast-constrained");

        var result = TestSupport.Pipeline("lanzo fuego", RecognitionMode.Constrained)
            .Evaluate(TestSupport.Clip(), prompt, TestSupport.Context(state: "none", npcId: "player", repeats: counts));

        Assert.Contains(result.Verdict.Errors, static error => error.Tag == ErrorTags.VocabWrong);
        Assert.Equal(FeedbackType.ExplicitCorrection, result.Decision.Feedback.Items[0].Type);
        Assert.DoesNotContain(result.Response.Lines, line => line.Text.Contains(prompt.AnswerSetId, StringComparison.Ordinal));
    }

    [Fact]
    public void EffectsComeFromTheVerdictsCorrectness()
    {
        var result = TestSupport.Pipeline("quiero comprar una espada").Evaluate(TestSupport.Clip(), Prompt("buy-item-open"), TestSupport.Context());

        var effect = Assert.Single(result.Effects);
        Assert.Equal("trade_price", effect.Kind);
        Assert.Equal(1.0, effect.Value, 6);
    }

    [Fact]
    public void TheLearnerModelIsUpdatedFromTheErrorFamiliesAndTheIntent()
    {
        var context = TestSupport.Context();

        var result = TestSupport.Pipeline(TestSupport.WorkedExample).Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), context);

        var skills = result.Decision.Learner.Skills.Select(static skill => skill.SkillId).ToArray();
        Assert.Equal(["conj", "intent.offer_item", "noun"], skills);
        Assert.Equal(1, result.Decision.Learner.Find("conj")!.Errors);
        Assert.Equal(0, result.Decision.Learner.Find("intent.offer_item")!.Errors);
        Assert.Empty(context.Learner.Skills);
    }

    [Fact]
    public void RepeatedErrorsEscalateTheFeedbackFromRecastToHint()
    {
        var counts = new Dictionary<string, int> { [ErrorTags.ConjPerson] = 2 };

        var result = TestSupport.Pipeline(TestSupport.WorkedExample)
            .Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context(repeats: counts));

        Assert.Equal(FeedbackType.Hint, result.Decision.Feedback.Items[0].Type);
        Assert.DoesNotContain(result.Response.Lines, static line => line.IsRecast);
        Assert.StartsWith("feedback.hint.", result.Response.Lines[1].LineId, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePromptsOwnGrammarsAreOptional()
    {
        var constrained = Prompt("offer-items-constrained");

        var result = TestSupport.Pipeline("tengo dos espadas", RecognitionMode.Constrained)
            .Evaluate(TestSupport.Clip(), constrained, TestSupport.Context());

        Assert.Equal("offer_item", result.Verdict.Intent.Value);
        Assert.Equal("2", result.Verdict.Slots["qty"]);
    }

    [Fact]
    public void AnEmbeddingModelAddsItsHashAndKeepsTheResultDeterministic()
    {
        var clip = TestSupport.Clip();
        var prompt = Prompt("trade-free-conversation");
        var context = TestSupport.Context();
        var first = new UtterancePipeline(TestSupport.Services("hola", embeddings: true));
        var second = new UtterancePipeline(TestSupport.Services("hola", embeddings: true));

        var one = first.Evaluate(clip, prompt, context);
        var two = second.Evaluate(clip, prompt, context);

        Assert.Equal("fake-trigram-v1", one.Verdict.Versions.ModelHashes["embedding"]);
        Assert.Equal(TestSupport.Json(one), TestSupport.Json(two));
    }

    [Fact]
    public void ResultsRoundTripThroughCanonicalJson()
    {
        var result = TestSupport.Pipeline(TestSupport.WorkedExample, phonemes: true)
            .Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context());

        var json = TestSupport.Json(result);
        var back = Slg.Core.Determinism.CanonicalJson.Deserialize<PipelineResult>(json);

        Assert.Equal(json, TestSupport.Json(back));
        Assert.Equal(result, back);
    }

    [Fact]
    public void ASynthesizerSpeaksEveryResponseLine()
    {
        var pipeline = new UtterancePipeline(TestSupport.Services(TestSupport.WorkedExample, synthesizer: true));
        var result = pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context());

        var audio = pipeline.Synthesize(result.Response, "npc-1");

        Assert.Equal(result.Response.Lines.Count, audio.Count);
        Assert.All(audio, static buffer => Assert.NotEmpty(buffer.Samples));
    }

    [Fact]
    public void SynthesizingWithoutASynthesizerThrows()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample);
        var result = pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context());

        Assert.Throws<InvalidOperationException>(() => pipeline.Synthesize(result.Response, "npc-1"));
    }

    [Fact]
    public void APromptWithAnUnknownAnswerSetIsRejected()
    {
        var prompt = Prompt(TestSupport.OfferPrompt) with { AnswerSetId = "no-such-set" };

        var ex = Assert.Throws<ArgumentException>(() => TestSupport.Pipeline(TestSupport.WorkedExample).Evaluate(TestSupport.Clip(), prompt, TestSupport.Context()));

        Assert.Contains("no-such-set", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AStateOfAnotherNpcIsRejected()
    {
        var context = TestSupport.Context() with { NpcState = new Slg.Decision.Npc.NpcState("wizard", "idle", 0) };

        Assert.Throws<ArgumentException>(() =>
            TestSupport.Pipeline(TestSupport.WorkedExample).Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), context));
    }

    private sealed class LoggingWordRecognizer(List<string> log, AsrResult result) : IWordRecognizer
    {
        public string ModelHash => result.ModelHash;

        public AsrResult Recognize(PcmClip clip, RecognitionRequest request)
        {
            lock (log)
            {
                log.Add("asr");
            }

            return result;
        }
    }

    private sealed class LoggingPhonemeRecognizer(List<string> log, IPhonemeRecognizer inner) : IPhonemeRecognizer
    {
        public string ModelHash => inner.ModelHash;

        public PhonePosteriors Recognize(PcmClip clip)
        {
            lock (log)
            {
                log.Add("phoneme");
            }

            return inner.Recognize(clip);
        }
    }

    private sealed class ThrowingWordRecognizer : IWordRecognizer
    {
        public string ModelHash => "throwing";

        public AsrResult Recognize(PcmClip clip, RecognitionRequest request)
        {
            throw new NotSupportedException("no model");
        }
    }
}
