using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Evaluation.Tests;

/// <summary>Tests for <see cref="VerdictAssembler"/>.</summary>
public class VerdictAssemblerTests
{
    [Fact]
    public void WorkedExampleProducesTheHldVerdict()
    {
        var verdict = VerdictAssembler.Assemble(Fixtures.WorkedExample());

        Assert.True(verdict.Communicated);
        Assert.Equal(new IntentId("offer_item"), verdict.Intent);
        Assert.Equal(SlotMap.From([new("item", "espada"), new("qty", "2")]), verdict.Slots);
        Assert.Equal(
            [
                new ErrorTag(ErrorTags.ConjPerson, new TextSpan(3, 5), "tengo", "tiene"),
                new ErrorTag(ErrorTags.NounNumber, new TextSpan(13, 6), "espadas", "espada"),
            ],
            verdict.Errors);
        Assert.Equal(0.6, verdict.Correctness);
        Assert.Equal(ResolvedBy.Grammar, verdict.ResolvedBy);
        Assert.Equal(0.9, verdict.Confidence);
        Assert.Empty(verdict.Pronunciation);
        Assert.Equal(Fixtures.Versions(), verdict.Versions);
    }

    [Fact]
    public void WorkedExampleSerializesToCanonicalJson()
    {
        var verdict = VerdictAssembler.Assemble(Fixtures.WorkedExample());
        var json = CanonicalJson.Serialize(verdict);

        Assert.Equal(verdict, CanonicalJson.Deserialize<Verdict>(json));
        Assert.Equal(json, CanonicalJson.Serialize(CanonicalJson.Deserialize<Verdict>(json)));
    }

    [Theory]
    [InlineData(Difficulty.Beginner, 0.6)]
    [InlineData(Difficulty.Intermediate, 0.5)]
    [InlineData(Difficulty.Advanced, 0.4)]
    public void CorrectnessUsesTheDifficultyWeights(Difficulty difficulty, double expected)
    {
        var inputs = Fixtures.WorkedExample();
        var verdict = VerdictAssembler.Assemble(inputs with { Prompt = Fixtures.Prompt(difficulty: difficulty) });

        Assert.Equal(expected, verdict.Correctness);
    }

    [Fact]
    public void MissingRequiredSlotIsNotCommunicated()
    {
        var inputs = Fixtures.WorkedExample();
        var withoutQty = inputs with
        {
            Analysis = inputs.Analysis with { Slots = [new SlotValue("item", "espada", new TextSpan(13, 6))] },
        };

        var verdict = VerdictAssembler.Assemble(withoutQty);

        Assert.False(verdict.Communicated);
        Assert.Equal(SlotMap.From([new("item", "espada")]), verdict.Slots);
        Assert.Equal(new IntentId("offer_item"), verdict.Intent);
        Assert.Equal(ResolvedBy.Grammar, verdict.ResolvedBy);
    }

    [Fact]
    public void EmptyRequiredSlotValueCountsAsMissing()
    {
        var inputs = Fixtures.WorkedExample();
        var verdict = VerdictAssembler.Assemble(inputs with
        {
            Analysis = inputs.Analysis with { Slots = [new SlotValue("item", string.Empty, new TextSpan(13, 6)), new SlotValue("qty", "2", new TextSpan(9, 3))] },
        });

        Assert.False(verdict.Communicated);
    }

    [Fact]
    public void NoRequiredSlotsNeedsOnlyAKnownIntent()
    {
        var inputs = Fixtures.WorkedExample() with { RequiredSlots = [] };
        inputs = inputs with { Analysis = inputs.Analysis with { Slots = [] } };

        Assert.True(VerdictAssembler.Assemble(inputs).Communicated);
    }

    [Fact]
    public void UnknownIntentIsNotCommunicatedAndUnresolved()
    {
        var inputs = Fixtures.WorkedExample() with { Intent = Fixtures.UnknownIntent(), RequiredSlots = [] };

        var verdict = VerdictAssembler.Assemble(inputs);

        Assert.False(verdict.Communicated);
        Assert.True(verdict.Intent.IsUnknown);
        Assert.Equal(ResolvedBy.Unresolved, verdict.ResolvedBy);
    }

    [Fact]
    public void UnknownIntentWithAContradictoryRungIsStillUnresolved()
    {
        var contradictory = new IntentResolution(IntentId.Unknown, SlotMap.Empty, IntentRung.Rules, 0.0, 0.9);

        var verdict = VerdictAssembler.Assemble(Fixtures.WorkedExample() with { Intent = contradictory });

        Assert.Equal(ResolvedBy.Unresolved, verdict.ResolvedBy);
    }

    [Theory]
    [InlineData(MatchType.Exact, ResolvedBy.Exact)]
    [InlineData(MatchType.Alias, ResolvedBy.Alias)]
    [InlineData(MatchType.Fuzzy, ResolvedBy.Fuzzy)]
    public void AnAnswerMatchWithAnUnknownIntentIsCommunicated(MatchType matchType, ResolvedBy expected)
    {
        var verdict = VerdictAssembler.Assemble(QuizInputs(Fixtures.Matched(matchType)));

        Assert.True(verdict.Communicated);
        Assert.True(verdict.Intent.IsUnknown);
        Assert.Empty(verdict.Slots);
        Assert.Equal(expected, verdict.ResolvedBy);
        Assert.Equal(1.0, verdict.Correctness);
        Assert.Empty(verdict.Errors);
    }

    [Fact]
    public void AnAnswerMatchIsNotHeldBackByTheUnresolvedIntentsZeroConfidence()
    {
        var verdict = VerdictAssembler.Assemble(QuizInputs(Fixtures.Matched(MatchType.Exact, 0.8)));

        Assert.Equal(0.8, verdict.Confidence);
        Assert.Equal(OutcomeBandKind.Correct, OutcomeBand.Classify(verdict, EvaluationConfig.Default.Beginner).Kind);
    }

    [Fact]
    public void NoAnswerMatchWithAnUnknownIntentIsNotCommunicated()
    {
        var verdict = VerdictAssembler.Assemble(QuizInputs(Fixtures.NoMatch()));

        Assert.False(verdict.Communicated);
        Assert.True(verdict.Intent.IsUnknown);
        Assert.Equal(ResolvedBy.Unresolved, verdict.ResolvedBy);
        Assert.Equal(0.0, verdict.Confidence);
    }

    [Fact]
    public void AnAnswerMatchFollowsTheCommunicatedMarginOnTheMatchConfidence()
    {
        var settings = EvaluationConfig.Default.Beginner;
        var threshold = settings.CommunicatedConfidenceThreshold;
        var epsilon = settings.Epsilon;
        var margin = settings.CommunicatedMargin();

        var accept = threshold + epsilon;
        var uncertain = threshold;
        var reject = threshold - epsilon;
        Assert.Equal(MarginDecision.Accept, margin.Evaluate(accept));
        Assert.Equal(MarginDecision.Uncertain, margin.Evaluate(uncertain));
        Assert.Equal(MarginDecision.Reject, margin.Evaluate(reject));

        Assert.True(VerdictAssembler.Assemble(QuizInputs(Fixtures.Matched(MatchType.Fuzzy, accept))).Communicated);
        Assert.True(VerdictAssembler.Assemble(QuizInputs(Fixtures.Matched(MatchType.Fuzzy, uncertain))).Communicated);
        Assert.False(VerdictAssembler.Assemble(QuizInputs(Fixtures.Matched(MatchType.Fuzzy, reject))).Communicated);
        Assert.False(VerdictAssembler.Assemble(QuizInputs(Fixtures.Matched(MatchType.Fuzzy, 0.0))).Communicated);
    }

    [Fact]
    public void AnAnswerMatchWithAKnownIntentStillReportsThatIntent()
    {
        var inputs = Fixtures.WorkedExample() with { Match = Fixtures.Matched(MatchType.Exact) };

        var verdict = VerdictAssembler.Assemble(inputs);

        Assert.True(verdict.Communicated);
        Assert.Equal(new IntentId("offer_item"), verdict.Intent);
        Assert.Equal(ResolvedBy.Exact, verdict.ResolvedBy);
    }

    [Theory]
    [InlineData(MatchType.Exact, IntentRung.Grammar, ResolvedBy.Exact)]
    [InlineData(MatchType.Alias, IntentRung.Rules, ResolvedBy.Alias)]
    [InlineData(MatchType.Fuzzy, IntentRung.Embedding, ResolvedBy.Fuzzy)]
    [InlineData(MatchType.None, IntentRung.Grammar, ResolvedBy.Grammar)]
    [InlineData(MatchType.None, IntentRung.Rules, ResolvedBy.Rules)]
    [InlineData(MatchType.None, IntentRung.Embedding, ResolvedBy.Embedding)]
    [InlineData(MatchType.None, IntentRung.Unresolved, ResolvedBy.Unresolved)]
    public void ResolvedByPrefersTheMatchThenTheIntentRung(MatchType matchType, IntentRung rung, ResolvedBy expected)
    {
        var match = matchType == MatchType.None ? Fixtures.NoMatch() : Fixtures.Matched(matchType);
        var inputs = Fixtures.WorkedExample() with { Match = match, Intent = Fixtures.Intent(rung: rung) };

        Assert.Equal(expected, VerdictAssembler.Assemble(inputs).ResolvedBy);
    }

    [Fact]
    public void IntentSlotsWinOverAnalysisSlotsOnAClash()
    {
        var inputs = Fixtures.WorkedExample();
        var verdict = VerdictAssembler.Assemble(inputs with
        {
            Intent = Fixtures.Intent(slots: SlotMap.From([new("item", "escudo"), new("agent", "aldeano")])),
        });

        Assert.Equal(SlotMap.From([new("item", "escudo"), new("agent", "aldeano"), new("qty", "2")]), verdict.Slots);
        Assert.Equal(["agent", "item", "qty"], verdict.Slots.Keys);
    }

    [Fact]
    public void DuplicateAnalysisSlotsResolveToTheEarliestSpanRegardlessOfOrder()
    {
        var first = new SlotValue("item", "espada", new TextSpan(13, 6));
        var second = new SlotValue("item", "escudo", new TextSpan(20, 6));
        var inputs = Fixtures.WorkedExample();

        var forward = VerdictAssembler.Assemble(inputs with { Analysis = inputs.Analysis with { Slots = [first, second, new SlotValue("qty", "2", new TextSpan(9, 3))] } });
        var backward = VerdictAssembler.Assemble(inputs with { Analysis = inputs.Analysis with { Slots = [new SlotValue("qty", "2", new TextSpan(9, 3)), second, first] } });

        Assert.Equal("espada", forward.Slots["item"]);
        Assert.Equal(forward, backward);
    }

    [Fact]
    public void ConfidenceIsTheMinimumOfTheStageConfidences()
    {
        var inputs = Fixtures.WorkedExample() with { Pronunciation = Fixtures.Pronunciation(confidence: 0.55) };

        Assert.Equal(0.55, VerdictAssembler.Assemble(inputs).Confidence);
        Assert.Equal(0.3, VerdictAssembler.Assemble(WithConfidences(asr: 0.3)).Confidence);
        Assert.Equal(0.31, VerdictAssembler.Assemble(WithConfidences(analysis: 0.31)).Confidence);
        Assert.Equal(0.32, VerdictAssembler.Assemble(WithConfidences(intent: 0.32)).Confidence);
        Assert.Equal(0.33, VerdictAssembler.Assemble(WithConfidences(pronunciation: 0.33)).Confidence);
        Assert.Equal(0.34, VerdictAssembler.Assemble(WithConfidences(match: 0.34, matchType: MatchType.Fuzzy)).Confidence);
    }

    [Fact]
    public void MatchConfidenceIsIgnoredWhenNothingMatched()
    {
        var verdict = VerdictAssembler.Assemble(WithConfidences(match: 0.01, matchType: MatchType.None));

        Assert.Equal(1.0, verdict.Confidence);
    }

    [Fact]
    public void ConfidenceIsClampedAndNanCountsAsZero()
    {
        Assert.Equal(1.0, VerdictAssembler.Assemble(WithConfidences(asr: 7.0)).Confidence);
        Assert.Equal(0.0, VerdictAssembler.Assemble(WithConfidences(asr: -1.0)).Confidence);

        var nan = VerdictAssembler.Assemble(WithConfidences(asr: double.NaN));
        Assert.Equal(0.0, nan.Confidence);
        Assert.False(nan.Communicated);
        Assert.NotEmpty(CanonicalJson.Serialize(nan));
    }

    [Fact]
    public void CommunicatedFollowsTheMarginAndUncertainCountsAsCommunicated()
    {
        var settings = EvaluationConfig.Default.Beginner;
        var threshold = settings.CommunicatedConfidenceThreshold;
        var epsilon = settings.Epsilon;
        var margin = settings.CommunicatedMargin();

        var accept = threshold + epsilon;
        var reject = threshold - epsilon;
        var insideBelow = threshold - (epsilon / 2);
        var insideAbove = threshold + (epsilon / 2);

        Assert.Equal(MarginDecision.Accept, margin.Evaluate(accept));
        Assert.Equal(MarginDecision.Reject, margin.Evaluate(reject));
        Assert.Equal(MarginDecision.Uncertain, margin.Evaluate(threshold));
        Assert.Equal(MarginDecision.Uncertain, margin.Evaluate(insideBelow));
        Assert.Equal(MarginDecision.Uncertain, margin.Evaluate(insideAbove));

        Assert.True(VerdictAssembler.Assemble(WithConfidences(asr: accept)).Communicated);
        Assert.True(VerdictAssembler.Assemble(WithConfidences(asr: insideAbove)).Communicated);
        Assert.True(VerdictAssembler.Assemble(WithConfidences(asr: threshold)).Communicated);
        Assert.True(VerdictAssembler.Assemble(WithConfidences(asr: insideBelow)).Communicated);
        Assert.False(VerdictAssembler.Assemble(WithConfidences(asr: reject)).Communicated);
        Assert.False(VerdictAssembler.Assemble(WithConfidences(asr: 0.0)).Communicated);
    }

    [Fact]
    public void ErrorsAreOrderedBySpanStartThenTagOrderThenExpectedRegardlessOfInputOrder()
    {
        ErrorTag[] errors =
        [
            new(ErrorTags.NounNumber, new TextSpan(10, 4), "b", "x"),
            new("custom.tag", new TextSpan(10, 4), "a", "x"),
            new(ErrorTags.PronR, new TextSpan(10, 4), "a", "x"),
            new(ErrorTags.VocabWrong, new TextSpan(10, 4), "z", "x"),
            new(ErrorTags.WordOrder, new TextSpan(10, 4), "b", "x"),
            new(ErrorTags.WordOrder, new TextSpan(10, 7), "a", "x"),
            new(ErrorTags.ConjTense, new TextSpan(0, 3), "q", "x"),
        ];

        var expectedOrder = new[]
        {
            (0, ErrorTags.ConjTense, "q"),
            (10, ErrorTags.VocabWrong, "z"),
            (10, ErrorTags.NounNumber, "b"),
            (10, ErrorTags.WordOrder, "a"),
            (10, ErrorTags.WordOrder, "b"),
            (10, ErrorTags.PronR, "a"),
            (10, "custom.tag", "a"),
        };

        var inputs = Fixtures.WorkedExample();
        foreach (var permutation in new[] { errors, Enumerable.Reverse(errors).ToArray(), Rotate(errors, 3), Rotate(errors, 5) })
        {
            var verdict = VerdictAssembler.Assemble(inputs with { Analysis = inputs.Analysis with { Errors = permutation } });
            Assert.Equal(expectedOrder, verdict.Errors.Select(e => (e.Span.Start, e.Tag, e.Expected)).ToArray());
        }
    }

    [Fact]
    public void ErrorsAreDeduplicatedByTagAndSpanAcrossStages()
    {
        var analysisError = new ErrorTag(ErrorTags.PronR, new TextSpan(0, 4), "r", "z");
        var pronunciationError = new ErrorTag(ErrorTags.PronR, new TextSpan(0, 4), "r", "a");
        var otherSpan = new ErrorTag(ErrorTags.PronR, new TextSpan(0, 5), "r", "q");
        var inputs = Fixtures.WorkedExample() with { Pronunciation = Fixtures.Pronunciation(errors: [pronunciationError, otherSpan]) };
        inputs = inputs with { Analysis = inputs.Analysis with { Errors = [analysisError, analysisError] } };

        var verdict = VerdictAssembler.Assemble(inputs);

        Assert.Equal([pronunciationError, otherSpan], verdict.Errors);
    }

    [Fact]
    public void PronunciationErrorsAreMergedInSpanOrderAndDoNotLowerCorrectness()
    {
        var pronError = new ErrorTag(ErrorTags.PronR, new TextSpan(8, 2), "ɾ", "ɹ");
        var phones = new[] { Fixtures.Phone(0.9), Fixtures.Phone(0.2) };
        var inputs = Fixtures.WorkedExample() with { Pronunciation = Fixtures.Pronunciation(phones, [pronError]) };

        var verdict = VerdictAssembler.Assemble(inputs);

        Assert.Equal(
            [ErrorTags.ConjPerson, ErrorTags.PronR, ErrorTags.NounNumber],
            verdict.Errors.Select(e => e.Tag).ToArray());
        Assert.Equal(0.6, verdict.Correctness);
        Assert.Equal(phones, verdict.Pronunciation);
    }

    [Fact]
    public void CorrectnessIsOneWhenThereAreNoErrors()
    {
        var inputs = Fixtures.WorkedExample();

        Assert.Equal(1.0, VerdictAssembler.Assemble(inputs with { Analysis = inputs.Analysis with { Errors = [] } }).Correctness);
    }

    [Fact]
    public void CorrectnessIsClampedAtZero()
    {
        var inputs = Fixtures.WorkedExample();
        ErrorTag[] errors =
        [
            new(ErrorTags.VocabWrong, new TextSpan(0, 2), "a", "b"),
            new(ErrorTags.VocabUnknown, new TextSpan(3, 2), "a", "b"),
            new(ErrorTags.ConjPerson, new TextSpan(9, 2), "a", "b"),
        ];

        var verdict = VerdictAssembler.Assemble(inputs with { Analysis = inputs.Analysis with { Errors = errors } });

        Assert.Equal(0.0, verdict.Correctness);
    }

    [Fact]
    public void UnknownTagsUseTheUnknownTagWeightAndUnknownPronunciationTagsAreFree()
    {
        var inputs = Fixtures.WorkedExample();
        ErrorTag[] errors = [new("custom.thing", new TextSpan(0, 2), "a", "b"), new("pron.custom", new TextSpan(3, 2), "a", "b")];

        var verdict = VerdictAssembler.Assemble(inputs with { Analysis = inputs.Analysis with { Errors = errors } });

        Assert.Equal(0.7, verdict.Correctness);
    }

    [Fact]
    public void RepeatedTagsAtDifferentSpansEachCount()
    {
        var inputs = Fixtures.WorkedExample();
        ErrorTag[] errors = [new(ErrorTags.NounNumber, new TextSpan(0, 2), "a", "b"), new(ErrorTags.NounNumber, new TextSpan(3, 2), "a", "b")];

        var verdict = VerdictAssembler.Assemble(inputs with { Analysis = inputs.Analysis with { Errors = errors } });

        Assert.Equal(0.7, verdict.Correctness);
    }

    [Fact]
    public void CorrectnessReflectsAConfiguredWeight()
    {
        var inputs = Fixtures.WorkedExample();
        var beginner = EvaluationConfig.Default.Beginner with { ErrorWeights = new Dictionary<string, double> { [ErrorTags.NounNumber] = 0.5 } };
        var config = EvaluationConfig.Default with { Beginner = beginner };

        var verdict = VerdictAssembler.Assemble(inputs with { Config = config });

        // conj.person has no entry any more, so it takes the unknown-tag weight (0.3).
        Assert.Equal(0.2, verdict.Correctness);
    }

    [Fact]
    public void VocabWrongIsDerivedAtAQuizWhenTheFirstContentWordIsKnown()
    {
        var inputs = QuizVocabInputs([Fixtures.Token("perro", 0, "noun")]);

        var verdict = VerdictAssembler.Assemble(inputs);

        var error = Assert.Single(verdict.Errors);
        Assert.Equal(new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 5), string.Empty, "perro"), error);
        Assert.Equal(0.4, verdict.Correctness);
    }

    [Fact]
    public void VocabWrongSkipsLeadingFunctionWords()
    {
        var inputs = QuizVocabInputs([Fixtures.Token("el", 0, "art"), Fixtures.Token("perro", 3, "noun")]);

        var error = Assert.Single(VerdictAssembler.Assemble(inputs).Errors);

        Assert.Equal(new ErrorTag(ErrorTags.VocabWrong, new TextSpan(3, 5), string.Empty, "perro"), error);
    }

    [Fact]
    public void VocabWrongPicksTheFirstContentTokenByPositionNotByListOrder()
    {
        var inputs = QuizVocabInputs([Fixtures.Token("gato", 6, "noun"), Fixtures.Token("perro", 0, "noun")]);

        var error = Assert.Single(VerdictAssembler.Assemble(inputs).Errors);

        Assert.Equal("perro", error.Actual);
    }

    [Fact]
    public void VocabWrongIsNotDerivedInOpenMode()
    {
        var inputs = QuizVocabInputs([Fixtures.Token("perro", 0, "noun")]) with { Prompt = Fixtures.Prompt(RecognitionMode.Open, answerSetId: "animals") with { AllowedIntentIds = [] } };

        Assert.Empty(VerdictAssembler.Assemble(inputs).Errors);
        Assert.Empty(VerdictAssembler.Assemble(NearMissInputs() with { Prompt = Fixtures.Prompt(RecognitionMode.Open, answerSetId: "animals") }).Errors);
    }

    [Theory]
    [InlineData(MatchType.Exact)]
    [InlineData(MatchType.Alias)]
    [InlineData(MatchType.Fuzzy)]
    public void VocabWrongIsNotDerivedWhenTheAnswerMatched(MatchType matchType)
    {
        var inputs = QuizVocabInputs([Fixtures.Token("perro", 0, "noun")]) with { Match = Fixtures.Matched(matchType) };

        Assert.Empty(VerdictAssembler.Assemble(inputs).Errors);
        Assert.Empty(VerdictAssembler.Assemble(NearMissInputs() with { Match = Fixtures.Matched(matchType) }).Errors);
    }

    [Fact]
    public void VocabWrongIsNotDerivedWhenTheWordIsNotInTheLexicon()
    {
        var inputs = QuizVocabInputs([Fixtures.Token("blorp", 0)]);

        Assert.Empty(VerdictAssembler.Assemble(inputs).Errors);
    }

    [Fact]
    public void VocabWrongOnlyLooksAtTheFirstContentToken()
    {
        var inputs = QuizVocabInputs([Fixtures.Token("blorp", 0), Fixtures.Token("perro", 6, "noun")]);

        Assert.Empty(VerdictAssembler.Assemble(inputs).Errors);
    }

    [Fact]
    public void VocabWrongIsNotDerivedWithoutContentTokens()
    {
        Assert.Empty(VerdictAssembler.Assemble(QuizVocabInputs([])).Errors);
        Assert.Empty(VerdictAssembler.Assemble(QuizVocabInputs([Fixtures.Token("el", 0, "art")])).Errors);
    }

    [Fact]
    public void VocabWrongIsNeverDerivedOnALineASlotGrammarUnderstood()
    {
        // M67: the derived tag explains a miss, and a line a slot grammar understood has none, however near an answer it is (buscaré madera para luz
        // at the smith's open prompt and usted comprará pan at a lesson step were scored 0.4 with vocab.wrong on their first content word).
        var inputs = NearMissInputs() with { Intent = Fixtures.Intent("ask_what", IntentRung.Grammar) };

        var verdict = VerdictAssembler.Assemble(inputs);

        Assert.Empty(verdict.Errors);
        Assert.Equal(1.0, verdict.Correctness);
    }

    [Theory]
    [InlineData(IntentRung.Rules)]
    [InlineData(IntentRung.Embedding)]
    public void VocabWrongIsNotDerivedOnALineARuleUnderstoodThatIsNoNearMiss(IntentRung rung)
    {
        // hola luz quiero dos panes at the baker's open prompt (greet, by the keyword hola): no answer phrase is one word from it, so the finder gives no near
        // miss, whatever the matcher's distance.
        var inputs = VocabInputs([Fixtures.Token("hola", 0, "interj"), Fixtures.Token("luz", 5, "noun"), Fixtures.Token("quiero", 9, "verb"), Fixtures.Token("2", 16, "num"), Fixtures.Token("panes", 18, "noun")]) with
        {
            Intent = Fixtures.Intent("greet", rung),
            Match = new MatchResult(null, MatchType.None, 2, false, 0.0),
            KnownLexiconWords = ["hola", "luz", "quiero", "panes"],
        };

        Assert.Empty(VerdictAssembler.Assemble(inputs).Errors);
    }

    [Theory]
    [InlineData(IntentRung.Rules)]
    [InlineData(IntentRung.Embedding)]
    [InlineData(IntentRung.Unresolved)]
    public void ANearMissNamesItsWordWithTheAnswersWordAsTheExpectedText(IntentRung rung)
    {
        // M75: cuál es esto for qué es esto (ask_what, by the keyword esto): the tag is on cuál, the one word that differs, and names qué. M67's rule put it on the
        // first content word (es), with an empty expected word, since the set has several answers.
        var inputs = NearMissInputs() with
        {
            Intent = rung == IntentRung.Unresolved ? Fixtures.UnknownIntent() : Fixtures.Intent("ask_what", rung),
        };

        var verdict = VerdictAssembler.Assemble(inputs);

        Assert.Equal(new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 4), "qué", "cuál"), Assert.Single(verdict.Errors));
        Assert.Equal(0.4, verdict.Correctness);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(40)]
    public void WithoutANearMissAnUnderstoodLineHasNoDerivedTagWhateverTheDistance(int distance)
    {
        // M75 replaces M67's rule (the distance to the nearest answer within the fuzzy budget for the line's own length) with the near miss: quiero hacer un hacha
        // at smith-open (an added word) and rápido por favor at lesson-had (content words far apart) were within that budget and lost 0.6 as correct Spanish.
        var inputs = NearMissInputs() with
        {
            Intent = Fixtures.Intent("ask_what", IntentRung.Rules),
            Match = new MatchResult(null, MatchType.None, distance, false, 0.0),
            NearMiss = null,
        };

        Assert.Empty(VerdictAssembler.Assemble(inputs).Errors);
    }

    [Fact]
    public void ASetWithNoAnswersGivesNoNearMissAndNoTag()
    {
        // The PR #28 review's item 8: a constrained prompt whose set has no answers gives the matcher distance 0, which M67's rule read as a near miss of every
        // line that resolved no intent. With no answer there is no word to name: no near miss, and the line names no word, so it has no error.
        var inputs = VocabInputs([Fixtures.Token("perro", 0, "noun")]) with
        {
            Intent = Fixtures.UnknownIntent(),
            Match = new MatchResult(null, MatchType.None, 0, false, 0.0),
            CanonicalAnswers = [],
        };

        var verdict = VerdictAssembler.Assemble(inputs);

        Assert.Empty(verdict.Errors);
        Assert.Equal(1.0, verdict.Correctness);
        Assert.False(verdict.Communicated);
    }

    [Fact]
    public void ANearMissOnAWordTheLexiconDoesNotKnowIsLeftToItsVocabUnknown()
    {
        var inputs = NearMissInputs() with { KnownLexiconWords = ["es", "esto"] };

        Assert.Empty(VerdictAssembler.Assemble(inputs).Errors);
    }

    [Fact]
    public void ANearMissIsPreferredToTheFirstContentWordAtAPromptThatNamesItsWord()
    {
        // A one-answer set names its canonical answer on the first content word of a line that resolved no intent; a near miss names the very word.
        var inputs = NearMissInputs() with { Intent = Fixtures.UnknownIntent(), CanonicalAnswers = ["qué es esto"] };

        Assert.Equal(new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 4), "qué", "cuál"), Assert.Single(VerdictAssembler.Assemble(inputs).Errors));
        Assert.Equal(
            new ErrorTag(ErrorTags.VocabWrong, new TextSpan(5, 2), "qué es esto", "es"),
            Assert.Single(VerdictAssembler.Assemble(inputs with { NearMiss = null }).Errors));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ALineThatResolvedNoIntentFarFromEveryOneOfSeveralAnswersGetsNoDerivedTag(bool answersKnown)
    {
        // M72 (D-game-22, 2026-11-02): la tienda está en el pueblo at farmer-open, whose 24 answers name no single word, resolved no intent the prompt takes and was
        // scored 0.4 with vocab.wrong and an empty expected word. A correct line that is simply not among the answers has no word to name and is no near miss.
        var inputs = VocabInputs([Fixtures.Token("perro", 0, "noun")]) with
        {
            Intent = Fixtures.UnknownIntent(),
            Match = new MatchResult(null, MatchType.None, 40, false, 0.0),
            CanonicalAnswers = answersKnown ? ["gato negro", "no sé"] : null,
        };

        var verdict = VerdictAssembler.Assemble(inputs);

        Assert.Empty(verdict.Errors);
        Assert.Equal(1.0, verdict.Correctness);
        Assert.False(verdict.Communicated);
    }

    [Fact]
    public void ALineThatResolvedNoIntentAtAOneAnswerSetIsTaggedHoweverFarItIs()
    {
        // The set asks for one word, so the tag names it, wherever the line is.
        var inputs = VocabInputs([Fixtures.Token("perro", 0, "noun")]) with
        {
            Intent = Fixtures.UnknownIntent(),
            Match = new MatchResult(null, MatchType.None, 40, false, 0.0),
            CanonicalAnswers = ["gato"],
        };

        Assert.Equal(new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 5), "gato", "perro"), Assert.Single(VerdictAssembler.Assemble(inputs).Errors));
    }

    [Fact]
    public void ALineAtAQuizIsTaggedHoweverFarItIsAndWhateverItsAnswers()
    {
        // A quiz allows no intent: it asks only for a word, so any other known word is the wrong one, even when its set has several answers.
        var inputs = QuizVocabInputs([Fixtures.Token("perro", 0, "noun")]) with
        {
            Match = new MatchResult(null, MatchType.None, 40, false, 0.0),
            CanonicalAnswers = ["gato", "gato negro"],
        };

        Assert.Equal(new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 5), string.Empty, "perro"), Assert.Single(VerdictAssembler.Assemble(inputs).Errors));
    }

    [Fact]
    public void ALineThatResolvedNoIntentKeepsTheTagWhenItIsANearMissOfOneOfSeveralAnswers()
    {
        // perro for perra, one of two answers: one word, close in spelling. M67's rule gave it an empty expected word; the near miss names perra.
        var inputs = VocabInputs([Fixtures.Token("perro", 0, "noun")]) with
        {
            Intent = Fixtures.UnknownIntent(),
            Match = new MatchResult(null, MatchType.None, 1, false, 0.0),
            CanonicalAnswers = ["perra", "no sé"],
            NearMiss = new AnswerNearMiss("dog", "perra", new TextSpan(0, 5), "perro", "perra", SlipPair.Spelling),
        };

        Assert.Equal(new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 5), "perra", "perro"), Assert.Single(VerdictAssembler.Assemble(inputs).Errors));
    }

    [Fact]
    public void ADerivedVocabWrongNeverReplacesAStageReportedOneAtTheSameSpan()
    {
        var reported = new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 5), "gato", "perro");
        var inputs = QuizVocabInputs([Fixtures.Token("perro", 0, "noun")]);
        inputs = inputs with { Analysis = inputs.Analysis with { Errors = [reported] } };

        var error = Assert.Single(VerdictAssembler.Assemble(inputs).Errors);

        Assert.Equal(reported, error);

        var nearMiss = NearMissInputs();
        var stage = new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 4), "cuáles", "cuál");
        Assert.Equal(stage, Assert.Single(VerdictAssembler.Assemble(nearMiss with { Analysis = nearMiss.Analysis with { Errors = [stage] } }).Errors));
    }

    [Fact]
    public void ALineThePromptDidNotTakeCarriesItsOffPromptIntentAndStaysNotCommunicated()
    {
        // M72: me gusta el río at smith-open resolves no intent the prompt allows; the catalog's state_like rides along, the intent and the flag stay as they were.
        var inputs = VocabInputs([Fixtures.Token("gusta", 3, "verb")]) with
        {
            Intent = Fixtures.UnknownIntent() with { OffPromptIntent = new IntentId("state_like") },
            Match = new MatchResult(null, MatchType.None, 30, false, 0.0),
            CanonicalAnswers = ["cómo te llamas", "qué haces"],
        };

        var verdict = VerdictAssembler.Assemble(inputs);

        Assert.Equal(new IntentId("state_like"), verdict.OffPromptIntent);
        Assert.Equal(IntentId.Unknown, verdict.Intent);
        Assert.False(verdict.Communicated);
        Assert.Empty(verdict.Errors);
        Assert.Equal(ResolvedBy.Unresolved, verdict.ResolvedBy);
    }

    [Theory]
    [InlineData(MatchType.Exact)]
    [InlineData(MatchType.Alias)]
    [InlineData(MatchType.Fuzzy)]
    public void ALineThatMatchedAnAnswerWasTakenAndHasNoOffPromptIntent(MatchType matchType)
    {
        var inputs = VocabInputs([Fixtures.Token("perro", 0, "noun")]) with
        {
            Intent = Fixtures.UnknownIntent() with { OffPromptIntent = new IntentId("state_like") },
            Match = Fixtures.Matched(matchType),
        };

        Assert.Null(VerdictAssembler.Assemble(inputs).OffPromptIntent);
    }

    [Fact]
    public void AResolvedIntentHasNoOffPromptIntent()
    {
        // The resolver never sets both; a hand-built input that does keeps the verdict's rule.
        var inputs = VocabInputs([Fixtures.Token("perro", 0, "noun")]) with { Intent = Fixtures.Intent("answer") with { OffPromptIntent = new IntentId("state_like") } };

        Assert.Null(VerdictAssembler.Assemble(inputs).OffPromptIntent);
    }

    [Fact]
    public void TheOffPromptIntentIsLeftOutOfTheJsonWhileNullAndTakesPartInEquality()
    {
        var without = VerdictAssembler.Assemble(Fixtures.WorkedExample());
        var with = without with { OffPromptIntent = new IntentId("state_like") };

        Assert.DoesNotContain("offPromptIntent", CanonicalJson.Serialize(without), StringComparison.Ordinal);
        Assert.Contains("\"offPromptIntent\":\"state_like\"", CanonicalJson.Serialize(with), StringComparison.Ordinal);
        Assert.Equal(with, CanonicalJson.Deserialize<Verdict>(CanonicalJson.Serialize(with)));
        Assert.Equal(without, CanonicalJson.Deserialize<Verdict>(CanonicalJson.Serialize(without)));
        Assert.NotEqual(without, with);
        Assert.NotEqual(with, with with { OffPromptIntent = new IntentId("greet") });
        Assert.Equal(with.GetHashCode(), (without with { OffPromptIntent = new IntentId("state_like") }).GetHashCode());
    }

    [Fact]
    public void AssemblingTwiceGivesByteIdenticalCanonicalJson()
    {
        var first = CanonicalJson.Serialize(VerdictAssembler.Assemble(Fixtures.WorkedExample()));
        var second = CanonicalJson.Serialize(VerdictAssembler.Assemble(Fixtures.WorkedExample()));

        Assert.Equal(first, second);
    }

    [Fact]
    public void ReorderingEveryInputListDoesNotChangeTheVerdictBytes()
    {
        var inputs = Fixtures.WorkedExample() with
        {
            Pronunciation = Fixtures.Pronunciation(
                [Fixtures.Phone(0.9)],
                [new ErrorTag(ErrorTags.PronR, new TextSpan(8, 2), "ɾ", "ɹ"), new ErrorTag(ErrorTags.PronVowel, new TextSpan(8, 2), "o", "oʊ")]),
        };
        var reordered = inputs with
        {
            Analysis = inputs.Analysis with
            {
                Tokens = inputs.Analysis.Tokens.Reverse().ToArray(),
                Slots = inputs.Analysis.Slots.Reverse().ToArray(),
                Errors = inputs.Analysis.Errors.Reverse().ToArray(),
            },
            Pronunciation = inputs.Pronunciation! with { Errors = inputs.Pronunciation!.Errors.Reverse().ToArray() },
            RequiredSlots = inputs.RequiredSlots.Reverse().ToArray(),
            KnownLexiconWords = inputs.KnownLexiconWords.Reverse().ToArray(),
        };

        Assert.Equal(
            CanonicalJson.Serialize(VerdictAssembler.Assemble(inputs)),
            CanonicalJson.Serialize(VerdictAssembler.Assemble(reordered)));
    }

    [Fact]
    public void AssembleRejectsNullInputs()
    {
        Assert.Throws<ArgumentNullException>(() => VerdictAssembler.Assemble(null!));
    }

    // A quiz prompt: constrained, no allowed intents, so the intent stage finds nothing and only the answer match can resolve the utterance.
    private static VerdictInputs QuizInputs(MatchResult match)
    {
        return new VerdictInputs(
            Fixtures.Prompt(RecognitionMode.Constrained, answerSetId: "items"),
            Fixtures.Asr(mode: RecognitionMode.Constrained),
            match,
            Fixtures.Analysis([Fixtures.Token("espada", 0, "noun")]),
            Fixtures.UnknownIntent(),
            null,
            [],
            ["espada"],
            EvaluationConfig.Default,
            Fixtures.Versions());
    }

    private static VerdictInputs VocabInputs(IReadOnlyList<AnalyzedToken> tokens)
    {
        return new VerdictInputs(
            Fixtures.Prompt(RecognitionMode.Constrained, answerSetId: "animals"),
            Fixtures.Asr(mode: RecognitionMode.Constrained),
            Fixtures.NoMatch(),
            Fixtures.Analysis(tokens),
            Fixtures.UnknownIntent(),
            null,
            [],
            ["gato", "perro", "el"],
            EvaluationConfig.Default,
            Fixtures.Versions());
    }

    // A quiz on the animals set: it allows no intent, so it asks only for a word, and a line that resolved none is tagged on its first content word (M72).
    private static VerdictInputs QuizVocabInputs(IReadOnlyList<AnalyzedToken> tokens)
    {
        return VocabInputs(tokens) with { Prompt = Fixtures.Prompt(RecognitionMode.Constrained, answerSetId: "animals") with { AllowedIntentIds = [] } };
    }

    // M75: cuál es esto at lesson-what, a near miss of the answer qué es esto (the finder's cuál for qué, two question words), understood by a keyword rule.
    private static VerdictInputs NearMissInputs()
    {
        return VocabInputs([Fixtures.Token("cuál", 0, "pron"), Fixtures.Token("es", 5, "verb"), Fixtures.Token("esto", 8, "pron")]) with
        {
            Intent = Fixtures.Intent("ask_what", IntentRung.Rules),
            Match = new MatchResult(null, MatchType.None, 3, false, 0.0),
            KnownLexiconWords = ["cuál", "es", "esto"],
            CanonicalAnswers = ["qué es esto", "ayuda", "no sé"],
            NearMiss = new AnswerNearMiss("what", "qué es esto", new TextSpan(0, 4), "cuál", "qué", SlipPair.QuestionWord),
        };
    }

    private static VerdictInputs WithConfidences(
        double asr = 1.0,
        double analysis = 1.0,
        double intent = 1.0,
        double? pronunciation = null,
        double match = 1.0,
        MatchType matchType = MatchType.None)
    {
        var inputs = Fixtures.WorkedExample();
        return inputs with
        {
            Asr = inputs.Asr with { Confidence = asr },
            Match = matchType == MatchType.None ? Fixtures.NoMatch(match) : Fixtures.Matched(matchType, match),
            Analysis = inputs.Analysis with { Confidence = analysis },
            Intent = inputs.Intent with { Confidence = intent },
            Pronunciation = pronunciation is null ? null : Fixtures.Pronunciation(confidence: pronunciation.Value),
        };
    }

    private static T[] Rotate<T>(T[] items, int by)
    {
        return items.Skip(by).Concat(items.Take(by)).ToArray();
    }
}
