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
using Vocaterra.Nlp.Nlp.Intent;
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
        Assert.Equal("done", result.Decision.Agent!.Next.StateId);
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
        Assert.Equal(context.HostStateHash, result.Replay.HostStateHash);
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
            var variant = Assert.Single(result.Decision.Agent!.Actions).VariantIndex;
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
    public void AnUnrelatedUtteranceIsNotCommunicatedAndTheAgentAsksAgain()
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
    public void WithoutAnAgentAPerfectUtteranceIsAcknowledgedFromTheAnswerAcceptedPool()
    {
        var prompt = Prompt("spell-cast-open");
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline("lanzo fuego").Evaluate(TestSupport.Clip(), prompt, context);

        Assert.Null(result.Decision.Agent);
        Assert.True(result.Verdict.Communicated);
        Assert.Empty(result.Verdict.Errors);
        var line = Assert.Single(result.Response.Lines);
        Assert.StartsWith("answer_accepted.", line.LineId, StringComparison.Ordinal);
        Assert.False(line.IsRecast);
        var effect = Assert.Single(result.Effects);
        Assert.Equal("spell_power", effect.Kind);
        Assert.Equal(1.0, effect.Value, 6);
    }

    [Fact]
    public void WithoutAnAgentAPerfectUtteranceHasNoLinesWhenTheBundleHasNoAnswerAcceptedPool()
    {
        using var directory = new BundleWithoutAnswerAccepted();
        var services = new PipelineServices(new ScriptedWordRecognizer(TestSupport.Asr("lanzo fuego")), null, null, null, directory.Bundle);
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = new UtterancePipeline(services).Evaluate(TestSupport.Clip(), directory.Bundle.Prompts["spell-cast-open"], context);

        Assert.True(result.Verdict.Communicated);
        Assert.Empty(result.Response.Lines);
    }

    [Fact]
    public void AnExactQuizAnswerIsCommunicatedWithoutAnIntentAndAcknowledged()
    {
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline("espada", RecognitionMode.Constrained).Evaluate(TestSupport.Clip(), Prompt("quiz-item-name"), context);

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
        Assert.Contains(line.Text, new[] { "¡Correcto!", "¡Muy bien!", "¡Exacto!" });
        Assert.Equal(Rating.Easy, result.Decision.Rating);
    }

    [Theory]
    [InlineData("quiz-item-name", "la espada", ResolvedBy.Alias)]
    [InlineData("quiz-spell-name", "fuego", ResolvedBy.Exact)]
    [InlineData("quiz-greeting-name", "hola", ResolvedBy.Exact)]
    public void QuizPromptsResolveThroughTheAnswerMatchAlone(string promptId, string transcript, ResolvedBy expected)
    {
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline(transcript, RecognitionMode.Constrained).Evaluate(TestSupport.Clip(), Prompt(promptId), context);

        Assert.True(result.Verdict.Communicated);
        Assert.True(result.Verdict.Intent.IsUnknown);
        Assert.Empty(result.Verdict.Slots);
        Assert.Equal(expected, result.Verdict.ResolvedBy);
        Assert.Empty(result.Effects);
        Assert.StartsWith("answer_accepted.", Assert.Single(result.Response.Lines).LineId, StringComparison.Ordinal);
    }

    [Fact]
    public void AQuizAnswerIsAcknowledgedEvenWhenTheContextStillNamesTheSampleAgent()
    {
        var result = TestSupport.Pipeline("espada", RecognitionMode.Constrained).Evaluate(TestSupport.Clip(), Prompt("quiz-item-name"), TestSupport.Context());

        Assert.True(result.Verdict.Communicated);
        Assert.StartsWith("answer_accepted.", Assert.Single(result.Response.Lines).LineId, StringComparison.Ordinal);
    }

    [Fact]
    public void AQuizAnswerWithAGrammarErrorGetsFeedbackInsteadOfTheAcknowledgement()
    {
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline("una espadas", RecognitionMode.Constrained).Evaluate(TestSupport.Clip(), Prompt("quiz-item-name"), context);

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

        var result = TestSupport.Pipeline("perro", RecognitionMode.Constrained).Evaluate(TestSupport.Clip(), Prompt("quiz-item-name"), context);

        Assert.False(result.Verdict.Communicated);
        Assert.Equal(ResolvedBy.Unresolved, result.Verdict.ResolvedBy);
        Assert.Equal(RepairKind.AskAgain, result.Repair!.Kind);
        Assert.DoesNotContain(result.Response.Lines, static line => line.LineId.StartsWith("answer_accepted.", StringComparison.Ordinal));
    }

    [Fact]
    public void AFuzzyQuizAnswerAtTheBudgetsEdgeIsCommunicatedLikeEveryOtherNearMiss()
    {
        // espado is one edit from espada, the whole Beginner budget for a six-letter answer. M17c: an edge match is communicated (ezpada is too).
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline("espado", RecognitionMode.Constrained).Evaluate(TestSupport.Clip(), Prompt("quiz-item-name"), context);

        Assert.True(result.Verdict.Communicated);
        Assert.Equal(ResolvedBy.Fuzzy, result.Verdict.ResolvedBy);
        Assert.Null(result.Repair);
        Assert.Contains("Ah, espada.", Assert.Single(result.Response.Lines, static line => line.IsRecast).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ABareSampleNameOnTheConstrainedSamplePromptFillsTheSlot()
    {
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline("fuego", RecognitionMode.Constrained).Evaluate(TestSupport.Clip(), Prompt("spell-cast-constrained"), context);

        Assert.Equal("cast_spell", result.Verdict.Intent.Value);
        Assert.Equal("fuego", result.Verdict.Slots["spell"]);
        Assert.True(result.Verdict.Communicated);
        Assert.Equal(1.0, result.Verdict.Correctness, 6);
        Assert.Empty(result.Verdict.Errors);
        var effect = Assert.Single(result.Effects);
        Assert.Equal("spell_power", effect.Kind);
        Assert.Equal(1.0, effect.Value, 6);
    }

    [Theory]
    [InlineData("fuego", "fuego")]
    [InlineData("el fuego", "fuego")]
    [InlineData("bola de fuego", "fuego")]
    [InlineData("una bola de fuego", "fuego")]
    [InlineData("hielo", "hielo")]
    [InlineData("el hielo", "hielo")]
    [InlineData("lanza de hielo", "hielo")]
    [InlineData("una lanza de hielo", "hielo")]
    [InlineData("rayo", "rayo")]
    [InlineData("el rayo", "rayo")]
    [InlineData("un rayo", "rayo")]
    [InlineData("relámpago", "relámpago")]
    [InlineData("un relámpago", "relámpago")]
    [InlineData("escudo mágico", "escudo")]
    [InlineData("el escudo mágico", "escudo")]
    [InlineData("un escudo mágico", "escudo")]
    [InlineData("escudo de magia", "escudo")]
    public void EveryPhraseOfTheSampleAnswerSetSatisfiesTheCastGrammar(string transcript, string spell)
    {
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline(transcript, RecognitionMode.Constrained).Evaluate(TestSupport.Clip(), Prompt("spell-cast-constrained"), context);

        Assert.Equal("cast_spell", result.Verdict.Intent.Value);
        Assert.Equal(spell, result.Verdict.Slots["spell"]);
        Assert.True(result.Verdict.Communicated);
        Assert.Empty(result.Verdict.Errors);
        Assert.Equal(1.0, result.Verdict.Correctness, 6);
        Assert.Equal(1.0, Assert.Single(result.Effects).Value, 6);
    }

    [Theory]
    [InlineData("lanzo fuego", "fuego")]
    [InlineData("lanzo el fuego", "fuego")]
    [InlineData("quiero lanzar hielo", "hielo")]
    [InlineData("fuego", "fuego")]
    [InlineData("el escudo mágico", "escudo")]
    public void TheOpenSamplePromptAcceptsTheSampleNameWithOrWithoutTheVerb(string transcript, string spell)
    {
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline(transcript).Evaluate(TestSupport.Clip(), Prompt("spell-cast-open"), context);

        Assert.Equal("cast_spell", result.Verdict.Intent.Value);
        Assert.Equal(spell, result.Verdict.Slots["spell"]);
        Assert.True(result.Verdict.Communicated);
        Assert.Empty(result.Verdict.Errors);
        Assert.Equal(1.0, Assert.Single(result.Effects).Value, 6);
    }

    [Fact]
    public void TheSampleAnswerSetPhrasesAreCoveredByTheTheory()
    {
        var phrases = TestSupport.Bundle.AnswerSets["spells-basic"].GrammarPhrases();

        Assert.Equal(
            ["bola de fuego", "el escudo mágico", "el fuego", "el hielo", "el rayo", "escudo de magia", "escudo mágico", "fuego", "hielo", "lanza de hielo", "rayo", "relámpago", "un escudo mágico", "un rayo", "un relámpago", "una bola de fuego", "una lanza de hielo"],
            phrases.Order(StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [InlineData("yo tiene dos espada", "offer-items-open", "offer_item_reply.")]
    [InlineData("quiero vender una espada", "sell-item-open", "sell_item_reply.")]
    [InlineData("quiero comprar una espada", "buy-item-open", "buy_item_reply.")]
    [InlineData("cuánto cuesta la espada", "ask-price-open", "ask_price_reply.")]
    public void TheSampleAgentAnswersExchangeIntentsFromItsInitialIdleState(string transcript, string promptId, string replyPool)
    {
        var machine = TestSupport.Bundle.Agents["blacksmith"];
        var context = TestSupport.Context(state: machine.InitialState().StateId);

        var result = TestSupport.Pipeline(transcript).Evaluate(TestSupport.Clip(), Prompt(promptId), context);

        Assert.Equal("idle", context.AgentState.StateId);
        Assert.True(result.Verdict.Communicated);
        Assert.Equal("trading", result.Decision.Agent!.Next.StateId);
        Assert.DoesNotContain(result.Response.Lines, static line => line.LineId.StartsWith("not_understood.", StringComparison.Ordinal));
        Assert.Contains(result.Response.Lines, line => line.LineId.StartsWith(replyPool, StringComparison.Ordinal));
    }

    [Fact]
    public void WithoutAnAgentOnlyFeedbackIsVoiced()
    {
        var prompt = Prompt("spell-cast-open");
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = TestSupport.Pipeline("lanzo el fuegos").Evaluate(TestSupport.Clip(), prompt, context);

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
        var prompt = Prompt("spell-cast-constrained");

        var result = TestSupport.Pipeline("lanzo fuego", RecognitionMode.Constrained)
            .Evaluate(TestSupport.Clip(), prompt, TestSupport.Context(state: "none", agentId: "player", repeats: counts));

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
        var back = Vocaterra.Nlp.Core.Determinism.CanonicalJson.Deserialize<PipelineResult>(json);

        Assert.Equal(json, TestSupport.Json(back));
        Assert.Equal(result, back);
    }

    [Fact]
    public void ASynthesizerSpeaksEveryResponseLine()
    {
        var pipeline = new UtterancePipeline(TestSupport.Services(TestSupport.WorkedExample, synthesizer: true));
        var result = pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context());

        var audio = pipeline.Synthesize(result.Response, "agent-1");

        Assert.Equal(result.Response.Lines.Count, audio.Count);
        Assert.All(audio, static buffer => Assert.NotEmpty(buffer.Samples));
    }

    [Fact]
    public void SynthesizingWithoutASynthesizerThrows()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample);
        var result = pipeline.Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), TestSupport.Context());

        Assert.Throws<InvalidOperationException>(() => pipeline.Synthesize(result.Response, "agent-1"));
    }

    [Fact]
    public void APromptWithAnUnknownAnswerSetIsRejected()
    {
        var prompt = Prompt(TestSupport.OfferPrompt) with { AnswerSetId = "no-such-set" };

        var ex = Assert.Throws<ArgumentException>(() => TestSupport.Pipeline(TestSupport.WorkedExample).Evaluate(TestSupport.Clip(), prompt, TestSupport.Context()));

        Assert.Contains("no-such-set", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AStateOfAnotherAgentIsRejected()
    {
        var context = TestSupport.Context() with { AgentState = new Vocaterra.Nlp.Decision.ConversationAgents.AgentState("wizard", "idle", 0) };

        Assert.Throws<ArgumentException>(() =>
            TestSupport.Pipeline(TestSupport.WorkedExample).Evaluate(TestSupport.Clip(), Prompt(TestSupport.OfferPrompt), context));
    }

    /// <summary>A copy of the committed bundle whose dialogue has no <c>answer_accepted</c> pool; hashes are not verified because the copy was edited.</summary>
    private sealed class BundleWithoutAnswerAccepted : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("vocaterra-bundle-");

        public BundleWithoutAnswerAccepted()
        {
            foreach (var source in Directory.EnumerateFiles(TestSupport.BundleDir, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(_directory.FullName, Path.GetRelativePath(TestSupport.BundleDir, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
            }

            var dialoguePath = Path.Combine(_directory.FullName, "dialogue", "dialogue.json");
            var root = JsonNode.Parse(File.ReadAllText(dialoguePath, Encoding.UTF8))!.AsObject();
            Assert.True(root["pools"]!.AsObject().Remove(UtterancePipeline.AnswerAcceptedPool));
            File.WriteAllText(dialoguePath, root.ToJsonString(), new UTF8Encoding(false));
            Bundle = ContentLoader.Load(_directory.FullName, new ContentLoadOptions { VerifyHashes = false });
        }

        public ContentBundle Bundle { get; }

        public void Dispose()
        {
            _directory.Delete(recursive: true);
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
