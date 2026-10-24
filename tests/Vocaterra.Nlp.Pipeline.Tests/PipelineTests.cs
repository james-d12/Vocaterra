using System.Text;
using System.Text.Json.Nodes;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.Feedback;
using Vocaterra.Nlp.Decision.Scheduling;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Pronunciation;

namespace Vocaterra.Nlp.Pipeline.Tests;

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

        var verdict = pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context()).Verdict;

        Assert.Equal("request_item", verdict.Intent.Value);
        Assert.Equal("espada", verdict.Slots["item"]);
        Assert.Equal("2", verdict.Slots["qty"]);
        Assert.True(verdict.Communicated);
        Assert.Equal(ResolvedBy.Grammar, verdict.ResolvedBy);
        Assert.Equal([ErrorTags.ConjPerson, ErrorTags.NounNumber], verdict.Errors.Select(static error => error.Tag).ToArray());
        Assert.Equal(("quiere", "quiero"), (verdict.Errors[0].Actual, verdict.Errors[0].Expected));
        Assert.Equal(("espada", "espadas"), (verdict.Errors[1].Actual, verdict.Errors[1].Expected));
        Assert.Equal(0.6, verdict.Correctness, 6);
    }

    [Fact]
    public void TheDesignDocumentsWorkedExampleGivesExactlyConjPersonAndNounNumber()
    {
        // "yo tiene dos espada", the sentence of the high-level design (and the invariant of the orchestrator's notes): the verb form and the missing plural
        // are the only two errors, and the item and the quantity are still understood. No prompt of the game answers it, so the prompt is built here.
        var verdict = TestSupport.Pipeline(TestSupport.HldWorkedExample)
            .Evaluate(TestSupport.Clip(), TestSupport.HldPrompt, TestSupport.Context()).Verdict;

        Assert.Equal("state_have", verdict.Intent.Value);
        Assert.Equal("espada", verdict.Slots["item"]);
        Assert.Equal("2", verdict.Slots["qty"]);
        Assert.True(verdict.Communicated);
        Assert.Equal([ErrorTags.ConjPerson, ErrorTags.NounNumber], verdict.Errors.Select(static error => error.Tag).ToArray());
        Assert.Equal(("tiene", "tengo"), (verdict.Errors[0].Actual, verdict.Errors[0].Expected));
        Assert.Equal(("espada", "espadas"), (verdict.Errors[1].Actual, verdict.Errors[1].Expected));
    }

    [Fact]
    public void WorkedExampleIsRecastFromThePlayersOwnSlots()
    {
        var result = TestSupport.Pipeline(TestSupport.WorkedExample)
            .Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context());

        var recast = Assert.Single(result.Response.Lines, static line => line.IsRecast);
        Assert.Contains("yo quiero dos espadas", recast.Text, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Response.Lines[0].IsRecast, "A recast is spoken before the reply.");
        Assert.Equal(2, result.Response.Lines.Count);
        Assert.StartsWith("villager_give.", result.Response.Lines[1].LineId, StringComparison.Ordinal);
        Assert.Equal("idle", result.Decision.Agent!.Next.StateId);
        Assert.Equal(FeedbackType.Recast, result.Decision.Feedback.Items[0].Type);
    }

    [Fact]
    public void ReplayRecordCarriesTheClipPromptStateAndVerdict()
    {
        var clip = TestSupport.Clip();
        var context = TestSupport.Context();

        var result = TestSupport.Pipeline(TestSupport.WorkedExample).Evaluate(clip, Prompt(TestSupport.OpenPrompt), context);

        Assert.Equal(clip.ClipHash, result.Replay.ClipHash);
        Assert.Equal(TestSupport.OpenPrompt, result.Replay.PromptId);
        Assert.Equal(context.HostStateHash, result.Replay.HostStateHash);
        Assert.Equal(result.Verdict, result.Replay.Verdict);
        Assert.Equal(TestSupport.Bundle.ContentHash, result.Replay.Versions.ContentHash);
    }

    [Fact]
    public void SameInputsGiveByteIdenticalResults()
    {
        var clip = TestSupport.Clip();
        var context = TestSupport.Context(counter: 5);
        var prompt = Prompt(TestSupport.OpenPrompt);

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
        var prompt = Prompt(TestSupport.OpenPrompt);

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
        var prompt = Prompt(TestSupport.OpenPrompt);

        var concurrent = TestSupport.Pipeline(TestSupport.WorkedExample, phonemes: true, options: new PipelineOptions { RunRecognizersConcurrently = true });
        var sequential = TestSupport.Pipeline(TestSupport.WorkedExample, phonemes: true, options: new PipelineOptions { RunRecognizersConcurrently = false });

        Assert.Equal(TestSupport.Json(sequential.Evaluate(clip, prompt, context)), TestSupport.Json(concurrent.Evaluate(clip, prompt, context)));
    }

    [Fact]
    public void ManyThreadsEvaluatingIndependentPipelinesAgree()
    {
        var clip = TestSupport.Clip();
        var context = TestSupport.Context();
        var prompt = Prompt(TestSupport.OpenPrompt);
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
        var prompt = Prompt(TestSupport.OpenPrompt);
        var results = new string[8];

        Parallel.For(0, results.Length, i => results[i] = TestSupport.Json(pipeline.Evaluate(clip, prompt, context)));

        Assert.All(results, result => Assert.Equal(results[0], result));
    }

    [Fact]
    public void ADifferentInteractionCounterCanChangeOnlyTheDrawnVariants()
    {
        var pipeline = TestSupport.Pipeline("quiero agua");
        var prompt = Prompt(TestSupport.OpenPrompt);
        var lineIds = new HashSet<string>(StringComparer.Ordinal);

        for (var counter = 0; counter < 16; counter++)
        {
            var result = pipeline.Evaluate(TestSupport.Clip(), prompt, TestSupport.Context(counter: counter));

            Assert.True(result.Verdict.Communicated);
            Assert.Empty(result.Verdict.Errors);
            var line = Assert.Single(result.Response.Lines);
            var variant = Assert.Single(result.Decision.Agent!.Actions).VariantIndex;
            Assert.Equal($"villager_give.{variant + 1}", line.LineId);
            lineIds.Add(line.LineId);
        }

        Assert.True(lineIds.Count > 1, "Sixteen interactions should draw more than one of the two variants.");
    }

    [Fact]
    public void OpenPromptsAskTheRecognizerForOpenRecognition()
    {
        var recognizer = new ScriptedWordRecognizer(TestSupport.Asr(TestSupport.WorkedExample));
        var pipeline = new UtterancePipeline(new PipelineServices(recognizer, null, null, null, TestSupport.Bundle));

        pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context());

        Assert.Equal(1, recognizer.CallCount);
        Assert.Equal(RecognitionMode.Open, recognizer.LastRequest!.Mode);
        Assert.Empty(recognizer.LastRequest.GrammarPhrases);
    }

    [Fact]
    public void ConstrainedPromptsBuildTheGrammarFromTheAnswerSet()
    {
        var recognizer = new ScriptedWordRecognizer(TestSupport.Asr("piedra", RecognitionMode.Constrained));
        var pipeline = new UtterancePipeline(new PipelineServices(recognizer, null, null, null, TestSupport.Bundle));
        var prompt = TestSupport.QuizPrompt;

        pipeline.Evaluate(TestSupport.Clip(), prompt, TestSupport.Context());

        var request = recognizer.LastRequest!;
        Assert.Equal(RecognitionMode.Constrained, request.Mode);
        var expected = GrammarBuilder.BuildList(TestSupport.Bundle.AnswerSets[prompt.AnswerSetId].GrammarPhrases());
        Assert.Equal(expected, request.GrammarPhrases);
        Assert.Equal(GrammarBuilder.UnknownToken, request.GrammarPhrases[^1]);
        Assert.Contains("piedra", request.GrammarPhrases);
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

        pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context());

        Assert.Equal(["asr", "phoneme"], log);
    }

    [Fact]
    public void BothRecognizersRunExactlyOnceWhenConcurrent()
    {
        var log = new List<string>();
        var words = new LoggingWordRecognizer(log, TestSupport.Asr(TestSupport.WorkedExample));
        var phonemes = new LoggingPhonemeRecognizer(log, TestSupport.HearsCorrectly(TestSupport.WorkedExample));
        var pipeline = new UtterancePipeline(new PipelineServices(words, phonemes, null, null, TestSupport.Bundle));

        pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context());

        Assert.Equal(["asr", "phoneme"], log.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ARecognizerFailureSurfacesWithItsOwnExceptionType()
    {
        var pipeline = new UtterancePipeline(new PipelineServices(new ThrowingWordRecognizer(), TestSupport.HearsCorrectly("hola"), null, null, TestSupport.Bundle));

        Assert.Throws<NotSupportedException>(() => pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context()));
    }

    [Fact]
    public void PronunciationIsSkippedWithoutAPhonemeRecognizer()
    {
        var verdict = TestSupport.Pipeline(TestSupport.WorkedExample)
            .Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context()).Verdict;

        Assert.Empty(verdict.Pronunciation);
        Assert.DoesNotContain(verdict.Errors, static error => ErrorTags.IsPronunciation(error.Tag));
    }

    [Theory]
    [InlineData(ScoringMode.Gop)]
    [InlineData(ScoringMode.Greedy)]
    public void CorrectPhonesGivePerfectPronunciationScores(ScoringMode mode)
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample, phonemes: true, options: new PipelineOptions { PronunciationMode = mode });

        var verdict = pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context()).Verdict;

        Assert.NotEmpty(verdict.Pronunciation);
        Assert.All(verdict.Pronunciation, static score => Assert.Equal(1.0, score.Score, 6));
        Assert.DoesNotContain(verdict.Errors, static error => ErrorTags.IsPronunciation(error.Tag));
    }

    [Fact]
    public void ARolledToEnglishRSoundIsReportedAsPronR()
    {
        const string text = "quiero un arco";
        var recognizer = TestSupport.HearsCorrectly(text, new Dictionary<string, string> { ["ɾ"] = "ɹ" });
        var services = TestSupport.Services(text, phonemeRecognizer: recognizer);

        var verdict = new UtterancePipeline(services).Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context()).Verdict;

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

        var verdict = pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context()).Verdict;

        Assert.Equal(pipeline.Services.Versions, verdict.Versions);
    }

    [Fact]
    public void AnUnrelatedUtteranceIsNotCommunicatedAndTheAgentAsksAgain()
    {
        var pipeline = TestSupport.Pipeline("xyzzy plugh");

        var result = pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context());

        Assert.False(result.Verdict.Communicated);
        Assert.True(result.Verdict.Intent.IsUnknown);
        Assert.StartsWith("not_understood.", Assert.Single(result.Response.Lines).LineId, StringComparison.Ordinal);
        Assert.Equal(RepairKind.AskAgain, result.Repair!.Kind);
        Assert.Equal(Rating.Again, result.Decision.Rating);
    }

    [Theory]
    [InlineData(0, RepairKind.AskAgain)]
    [InlineData(1, RepairKind.SlowRepeat)]
    [InlineData(2, RepairKind.OfferChoices)]
    [InlineData(3, RepairKind.GiveUp)]
    public void TheRepairStepFollowsTheNotCommunicatedRepeatCount(int repeats, RepairKind expected)
    {
        var pipeline = TestSupport.Pipeline("xyzzy plugh", RecognitionMode.Constrained);
        var counts = new Dictionary<string, int> { [FeedbackPolicy.NotCommunicatedTag] = repeats };

        var result = pipeline.Evaluate(TestSupport.Clip(), TestSupport.QuizPrompt, TestSupport.Context(repeats: counts));

        Assert.Equal(expected, result.Repair!.Kind);
        if (expected == RepairKind.OfferChoices)
        {
            Assert.Equal(RecognitionMode.Constrained, result.Repair.RetryRequest!.Mode);
            Assert.Equal(RepairStrategy.MaxChoices, result.Repair.RetryRequest.GrammarPhrases.Count);
        }
    }

    [Fact]
    public void TheOfferedChoicesAreOneCanonicalPerAnswerInDeclaredOrder()
    {
        // places-basic is declared tree, stone, river: the choices are their canonicals in that order, not the ordinal-sorted aliases («el río», «el árbol», «la piedra»).
        var pipeline = TestSupport.Pipeline("xyzzy plugh", RecognitionMode.Constrained);
        var counts = new Dictionary<string, int> { [FeedbackPolicy.NotCommunicatedTag] = 2 };

        var result = pipeline.Evaluate(TestSupport.Clip(), TestSupport.QuizPrompt, TestSupport.Context(repeats: counts));

        Assert.Equal(RepairKind.OfferChoices, result.Repair!.Kind);
        Assert.Equal(["árbol", "piedra", "río"], result.Repair.RetryRequest!.GrammarPhrases);
        Assert.Equal("¿Quieres decir «árbol», «piedra» o «río»?", result.Repair.AgentLine);
    }

    [Fact]
    public void TheRetryBeforeTheChoicesStillRecognizesTheAliases()
    {
        var pipeline = TestSupport.Pipeline("xyzzy plugh", RecognitionMode.Constrained);
        var counts = new Dictionary<string, int> { [FeedbackPolicy.NotCommunicatedTag] = 0 };

        var result = pipeline.Evaluate(TestSupport.Clip(), TestSupport.QuizPrompt, TestSupport.Context(repeats: counts));

        Assert.Equal(RepairKind.AskAgain, result.Repair!.Kind);
        Assert.Equal(RecognitionMode.Constrained, result.Repair.RetryRequest!.Mode);
        Assert.Contains("el río", result.Repair.RetryRequest.GrammarPhrases);
    }

    [Fact]
    public void ACommunicatedUtteranceHasNoRepairStep()
    {
        var result = TestSupport.Pipeline(TestSupport.WorkedExample).Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context());

        Assert.Null(result.Repair);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[unk]")]
    [InlineData("  ")]
    public void AnEmptyOrUnknownTranscriptIsHandledWithoutErrors(string transcript)
    {
        var pipeline = TestSupport.Pipeline(transcript, phonemes: true);

        var result = pipeline.Evaluate(PcmClip.FromSamples([]), Prompt(TestSupport.OpenPrompt), TestSupport.Context());

        Assert.False(result.Verdict.Communicated);
        Assert.Empty(result.Verdict.Errors);
        Assert.Empty(result.Verdict.Pronunciation);
        Assert.NotNull(result.Repair);
        Assert.Single(result.Response.Lines);
    }

    [Fact]
    public void UnknownWordsAreReportedAsVocabUnknown()
    {
        var result = TestSupport.Pipeline("yo tengo dos zorkmids").Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context());

        Assert.Contains(result.Verdict.Errors, static error => error.Tag == ErrorTags.VocabUnknown && error.Actual == "zorkmids");
    }

    [Fact]
    public void WithoutAnAgentAPerfectUtteranceIsAcknowledgedFromTheAnswerAcceptedPool()
    {
        var prompt = TestSupport.AgentlessOpenPrompt;
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline("quiero agua", bundle: TestSupport.EffectsBundle).Evaluate(TestSupport.Clip(), prompt, context);

        Assert.Null(result.Decision.Agent);
        Assert.True(result.Verdict.Communicated);
        Assert.Empty(result.Verdict.Errors);
        var line = Assert.Single(result.Response.Lines);
        Assert.StartsWith("answer_accepted.", line.LineId, StringComparison.Ordinal);
        Assert.False(line.IsRecast);
        var effect = Assert.Single(result.Effects);
        Assert.Equal("item_quality", effect.Kind);
        Assert.Equal(1.0, effect.Value, 6);
    }

    [Fact]
    public void WithoutAnAgentAPerfectUtteranceHasNoLinesWhenTheBundleHasNoAnswerAcceptedPool()
    {
        using var directory = new BundleWithoutAnswerAccepted();
        var services = new PipelineServices(new ScriptedWordRecognizer(TestSupport.Asr("quiero agua")), null, null, null, directory.Bundle);
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = new UtterancePipeline(services).Evaluate(TestSupport.Clip(), TestSupport.AgentlessOpenPrompt, context);

        Assert.True(result.Verdict.Communicated);
        Assert.Empty(result.Response.Lines);
    }

    [Fact]
    public void AnExactQuizAnswerIsCommunicatedWithoutAnIntentAndAcknowledged()
    {
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline("piedra", RecognitionMode.Constrained).Evaluate(TestSupport.Clip(), TestSupport.QuizPrompt, context);

        var verdict = result.Verdict;
        Assert.True(verdict.Communicated);
        Assert.True(verdict.Intent.IsUnknown);
        Assert.Empty(verdict.Slots);
        Assert.Equal(ResolvedBy.Exact, verdict.ResolvedBy);
        Assert.Equal(1.0, verdict.Correctness, 6);
        Assert.Equal(1.0, verdict.Confidence, 6);
        Assert.Empty(verdict.Errors);
        Assert.Null(result.Repair);
        Assert.Empty(result.Effects);
        var line = Assert.Single(result.Response.Lines);
        Assert.StartsWith("answer_accepted.", line.LineId, StringComparison.Ordinal);
        Assert.Contains(line.Text, TestSupport.Bundle.Dialogue.Pools["answer_accepted"].Variants.Select(static variant => variant.Text));
        Assert.Equal(Rating.Easy, result.Decision.Rating);
    }

    [Theory]
    [InlineData("la piedra", ResolvedBy.Alias)]
    [InlineData("un árbol", ResolvedBy.Alias)]
    [InlineData("río", ResolvedBy.Exact)]
    public void QuizPromptsResolveThroughTheAnswerMatchAlone(string transcript, ResolvedBy expected)
    {
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline(transcript, RecognitionMode.Constrained).Evaluate(TestSupport.Clip(), TestSupport.QuizPrompt, context);

        Assert.True(result.Verdict.Communicated);
        Assert.True(result.Verdict.Intent.IsUnknown);
        Assert.Empty(result.Verdict.Slots);
        Assert.Equal(expected, result.Verdict.ResolvedBy);
        Assert.Empty(result.Effects);
        Assert.StartsWith("answer_accepted.", Assert.Single(result.Response.Lines).LineId, StringComparison.Ordinal);
    }

    [Fact]
    public void AQuizAnswerIsAcknowledgedEvenWhenTheContextStillNamesAnAgent()
    {
        var result = TestSupport.Pipeline("piedra", RecognitionMode.Constrained).Evaluate(TestSupport.Clip(), TestSupport.QuizPrompt, TestSupport.Context());

        Assert.True(result.Verdict.Communicated);
        Assert.StartsWith("answer_accepted.", Assert.Single(result.Response.Lines).LineId, StringComparison.Ordinal);
    }

    [Fact]
    public void AQuizAnswerWithAGrammarErrorGetsFeedbackInsteadOfTheAcknowledgement()
    {
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline("una piedras", RecognitionMode.Constrained).Evaluate(TestSupport.Clip(), TestSupport.QuizPrompt, context);

        Assert.True(result.Verdict.Communicated);
        Assert.Equal(ResolvedBy.Fuzzy, result.Verdict.ResolvedBy);
        Assert.Contains(result.Verdict.Errors, static error => error.Tag == ErrorTags.NounNumber);
        Assert.DoesNotContain(result.Response.Lines, static line => line.LineId.StartsWith("answer_accepted.", StringComparison.Ordinal));
        Assert.NotEmpty(result.Response.Lines);
    }

    [Fact]
    public void AWrongQuizAnswerIsNotCommunicatedAndKeepsTheRepair()
    {
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline("perro", RecognitionMode.Constrained).Evaluate(TestSupport.Clip(), TestSupport.QuizPrompt, context);

        Assert.False(result.Verdict.Communicated);
        Assert.Equal(ResolvedBy.Unresolved, result.Verdict.ResolvedBy);
        Assert.Equal(RepairKind.AskAgain, result.Repair!.Kind);
        Assert.DoesNotContain(result.Response.Lines, static line => line.LineId.StartsWith("answer_accepted.", StringComparison.Ordinal));
    }

    [Fact]
    public void AFuzzyQuizAnswerAtTheBudgetsEdgeIsCommunicatedLikeEveryOtherNearMiss()
    {
        // piedro is one edit from piedra, the whole Beginner budget for a six-letter answer. M17c: an edge match is communicated (pieszra is too).
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline("piedro", RecognitionMode.Constrained).Evaluate(TestSupport.Clip(), TestSupport.QuizPrompt, context);

        Assert.True(result.Verdict.Communicated);
        Assert.Equal(ResolvedBy.Fuzzy, result.Verdict.ResolvedBy);
        Assert.Null(result.Repair);
        Assert.Contains("«piedra»", Assert.Single(result.Response.Lines, static line => line.IsRecast).Text, StringComparison.Ordinal);
    }

    public static TheoryData<string, string> SuppliesPhrases()
    {
        // Every phrase of the three item answers of the "supplies" set, with the item lemma the request grammar must bind from it.
        var lemmas = new Dictionary<string, string>(StringComparer.Ordinal) { ["water"] = "agua", ["bread"] = "pan", ["fruit"] = "fruta" };
        var data = new TheoryData<string, string>();
        foreach (var answer in TestSupport.Bundle.AnswerSets["supplies"].Answers.Where(answer => lemmas.ContainsKey(answer.AnswerId)))
        {
            foreach (var phrase in answer.Aliases.Prepend(answer.Canonical))
            {
                data.Add(phrase, lemmas[answer.AnswerId]);
            }
        }

        return data;
    }

    [Fact]
    public void ABareAnswerOnTheConstrainedPromptFillsTheItemSlotAtFullPower()
    {
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline("quiero agua por favor", RecognitionMode.Constrained, bundle: TestSupport.EffectsBundle)
            .Evaluate(TestSupport.Clip(), Prompt("stall-need"), context);

        Assert.Equal("request_item", result.Verdict.Intent.Value);
        Assert.Equal("agua", result.Verdict.Slots["item"]);
        Assert.True(result.Verdict.Communicated);
        Assert.Equal(1.0, result.Verdict.Correctness, 6);
        Assert.Empty(result.Verdict.Errors);
        var effect = Assert.Single(result.Effects);
        Assert.Equal("item_quality", effect.Kind);
        Assert.Equal(1.0, effect.Value, 6);
    }

    [Theory]
    [MemberData(nameof(SuppliesPhrases))]
    public void EveryPhraseOfTheSuppliesAnswerSetSatisfiesTheRequestGrammar(string transcript, string item)
    {
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline(transcript, RecognitionMode.Constrained, bundle: TestSupport.EffectsBundle)
            .Evaluate(TestSupport.Clip(), Prompt("stall-need"), context);

        Assert.Equal("request_item", result.Verdict.Intent.Value);
        Assert.Equal(item, result.Verdict.Slots["item"]);
        Assert.True(result.Verdict.Communicated);
        Assert.Empty(result.Verdict.Errors);
        Assert.Equal(1.0, result.Verdict.Correctness, 6);
        Assert.Equal(1.0, Assert.Single(result.Effects).Value, 6);
    }

    [Theory]
    [InlineData("quiero agua", "agua")]
    [InlineData("quiero el pan", "pan")]
    [InlineData("necesito fruta", "fruta")]
    [InlineData("agua por favor", "agua")]
    [InlineData("me da una fruta", "fruta")]
    public void TheOpenPromptAcceptsTheItemWithOrWithoutTheVerb(string transcript, string item)
    {
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline(transcript, bundle: TestSupport.EffectsBundle).Evaluate(TestSupport.Clip(), TestSupport.AgentlessOpenPrompt, context);

        Assert.Equal("request_item", result.Verdict.Intent.Value);
        Assert.Equal(item, result.Verdict.Slots["item"]);
        Assert.True(result.Verdict.Communicated);
        Assert.Empty(result.Verdict.Errors);
        Assert.Equal(1.0, Assert.Single(result.Effects).Value, 6);
    }

    [Theory]
    [InlineData("quiero agua", "villager_give.", "idle")]
    [InlineData("dónde está el río", "villager_point.", "pointing")]
    [InlineData("hola", "villager_greet.", "idle")]
    [InlineData("gracias", "villager_thank.", "idle")]
    [InlineData("adiós", "villager_bye.", "idle")]
    public void TheVillagerAnswersEachIntentFromItsInitialIdleState(string transcript, string replyPool, string nextState)
    {
        var machine = TestSupport.Bundle.Agents["villager"];
        var context = TestSupport.Context(state: machine.InitialState().StateId);

        var result = TestSupport.Pipeline(transcript).Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), context);

        Assert.Equal("idle", context.AgentState.StateId);
        Assert.True(result.Verdict.Communicated);
        Assert.Equal(nextState, result.Decision.Agent!.Next.StateId);
        Assert.DoesNotContain(result.Response.Lines, static line => line.LineId.StartsWith("not_understood.", StringComparison.Ordinal));
        Assert.Contains(result.Response.Lines, line => line.LineId.StartsWith(replyPool, StringComparison.Ordinal));
    }

    [Fact]
    public void WithoutAnAgentOnlyFeedbackIsVoiced()
    {
        var prompt = TestSupport.AgentlessOpenPrompt;
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline("yo quiere agua").Evaluate(TestSupport.Clip(), prompt, context);

        Assert.Null(result.Decision.Agent);
        Assert.NotEmpty(result.Verdict.Errors);
        var line = Assert.Single(result.Response.Lines);
        Assert.StartsWith("feedback.", line.LineId, StringComparison.Ordinal);
        Assert.DoesNotContain("__silent__", line.LineId, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePlaceholderAnswerSetIdIsNeverSpokenAsAnExpectedForm()
    {
        var counts = new Dictionary<string, int> { [ErrorTags.VocabWrong] = 4 };
        var prompt = Prompt("stall-need");

        var result = TestSupport.Pipeline("quiero madera", RecognitionMode.Constrained)
            .Evaluate(TestSupport.Clip(), prompt, TestSupport.Context(state: "none", agentId: "player", repeats: counts));

        Assert.Contains(result.Verdict.Errors, static error => error.Tag == ErrorTags.VocabWrong);
        Assert.Equal(FeedbackType.ExplicitCorrection, result.Decision.Feedback.Items[0].Type);
        Assert.DoesNotContain(result.Response.Lines, line => line.Text.Contains(prompt.AnswerSetId, StringComparison.Ordinal));
    }

    [Fact]
    public void EffectsComeFromTheVerdictsCorrectness()
    {
        var perfect = TestSupport.Pipeline("quiero agua", bundle: TestSupport.EffectsBundle).Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context());
        var flawed = TestSupport.Pipeline(TestSupport.WorkedExample, bundle: TestSupport.EffectsBundle).Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context());

        var effect = Assert.Single(perfect.Effects);
        Assert.Equal("item_quality", effect.Kind);
        Assert.Equal(1.0, effect.Value, 6);

        // The curve runs from 0.5 at correctness 0 to 1 at correctness 1, and the worked example's correctness is 0.6.
        Assert.Equal(0.5 + (0.5 * flawed.Verdict.Correctness), Assert.Single(flawed.Effects).Value, 6);
        Assert.Empty(TestSupport.Pipeline("quiero agua").Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context()).Effects);
    }

    [Fact]
    public void TheLearnerModelIsUpdatedFromTheErrorFamiliesAndTheIntent()
    {
        var context = TestSupport.Context();

        var result = TestSupport.Pipeline(TestSupport.WorkedExample).Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), context);

        var skills = result.Decision.Learner.Skills.Select(static skill => skill.SkillId).ToArray();
        Assert.Equal(["conj", "intent.request_item", "noun"], skills);
        Assert.Equal(1, result.Decision.Learner.Find("conj")!.Errors);
        Assert.Equal(0, result.Decision.Learner.Find("intent.request_item")!.Errors);
        Assert.Empty(context.Learner.Skills);
    }

    [Fact]
    public void RepeatedErrorsEscalateTheFeedbackFromRecastToHint()
    {
        // Seen once before: this utterance is the second offence, which is a hint.
        var counts = new Dictionary<string, int> { [ErrorTags.ConjPerson] = 1 };

        var result = TestSupport.Pipeline(TestSupport.WorkedExample)
            .Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context(repeats: counts));

        Assert.Equal(FeedbackType.Hint, result.Decision.Feedback.Items[0].Type);
        Assert.DoesNotContain(result.Response.Lines, static line => line.IsRecast);
        Assert.StartsWith("feedback.hint.", result.Response.Lines[1].LineId, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePromptsOwnGrammarsAreOptional()
    {
        // "necesito dos panes" is no phrase of the answer set of stall-need, but the intent grammars still read it (they are optional extras to the set).
        var constrained = Prompt("stall-need");

        var result = TestSupport.Pipeline("necesito dos panes", RecognitionMode.Constrained)
            .Evaluate(TestSupport.Clip(), constrained, TestSupport.Context());

        Assert.Equal("request_item", result.Verdict.Intent.Value);
        Assert.Equal("2", result.Verdict.Slots["qty"]);
    }

    [Fact]
    public void AnEmbeddingModelAddsItsHashAndKeepsTheResultDeterministic()
    {
        var clip = TestSupport.Clip();
        var prompt = Prompt(TestSupport.OpenPrompt);
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
            .Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context());

        var json = TestSupport.Json(result);
        var back = Vocaterra.Nlp.Core.Determinism.CanonicalJson.Deserialize<PipelineResult>(json);

        Assert.Equal(json, TestSupport.Json(back));
        Assert.Equal(result, back);
    }

    [Fact]
    public void ASynthesizerSpeaksEveryResponseLine()
    {
        var pipeline = new UtterancePipeline(TestSupport.Services(TestSupport.WorkedExample, synthesizer: true));
        var result = pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context());

        var audio = pipeline.Synthesize(result.Response, "agent-1");

        Assert.Equal(result.Response.Lines.Count, audio.Count);
        Assert.All(audio, static buffer => Assert.NotEmpty(buffer.Samples));
    }

    [Fact]
    public void SynthesizingWithoutASynthesizerThrows()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample);
        var result = pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context());

        Assert.Throws<InvalidOperationException>(() => pipeline.Synthesize(result.Response, "agent-1"));
    }

    [Fact]
    public void APromptWithAnUnknownAnswerSetIsRejected()
    {
        var prompt = Prompt(TestSupport.OpenPrompt) with { AnswerSetId = "no-such-set" };

        var ex = Assert.Throws<ArgumentException>(() => TestSupport.Pipeline(TestSupport.WorkedExample).Evaluate(TestSupport.Clip(), prompt, TestSupport.Context()));

        Assert.Contains("no-such-set", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AStateOfAnotherAgentIsRejected()
    {
        var context = TestSupport.Context() with { AgentState = new Vocaterra.Nlp.Decision.ConversationAgents.AgentState("wizard", "idle", 0) };

        Assert.Throws<ArgumentException>(() =>
            TestSupport.Pipeline(TestSupport.WorkedExample).Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), context));
    }

    /// <summary>
    /// A copy of the bundle whose dialogue has no <c>answer_accepted</c> pool; hashes are not verified because the copy was edited. The game's agents use that
    /// pool for their own acknowledgements, so in the copy their lines say <c>not_understood</c> instead, which keeps the copy valid.
    /// </summary>
    private sealed class BundleWithoutAnswerAccepted : IDisposable
    {
        private readonly TempBundle _copy = new();

        public BundleWithoutAnswerAccepted()
        {
            _copy.EditJson("dialogue/dialogue.json", root => Assert.True(root["pools"]!.AsObject().Remove(UtterancePipeline.AnswerAcceptedPool)));
            foreach (var agent in Directory.EnumerateFiles(_copy.PathOf("agents"), "*.json").Select(path => "agents/" + Path.GetFileName(path)))
            {
                _copy.EditJson(agent, root =>
                {
                    Rename(root);
                    var pools = root["variantPools"]!.AsArray();
                    foreach (var pool in pools.Where(pool => pool!["id"]!.GetValue<string>() == UtterancePipeline.AnswerAcceptedPool).ToList())
                    {
                        pools.Remove(pool);
                    }
                });
            }

            Bundle = _copy.Load();
        }

        private static void Rename(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var key in obj.Select(pair => pair.Key).ToList())
                    {
                        if ((key is "dialogueIntent" or "variantPoolId") && obj[key]!.GetValue<string>() == UtterancePipeline.AnswerAcceptedPool)
                        {
                            obj[key] = "not_understood";
                        }
                        else
                        {
                            Rename(obj[key]);
                        }
                    }

                    break;
                case JsonArray array:
                    foreach (var item in array)
                    {
                        Rename(item);
                    }

                    break;
            }
        }

        public ContentBundle Bundle { get; }

        public void Dispose()
        {
            _copy.Dispose();
        }
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
