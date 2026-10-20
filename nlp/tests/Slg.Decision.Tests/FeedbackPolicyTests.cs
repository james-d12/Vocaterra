using Slg.Core.Determinism;
using Slg.Core.Prompts;
using Slg.Core.Taxonomy;
using Slg.Decision.Feedback;
using Slg.Decision.Scheduling;

namespace Slg.Decision.Tests;

/// <summary>Tests for the feedback rule table.</summary>
public class FeedbackPolicyTests
{
    private static readonly IReadOnlyDictionary<string, int> NoCounts = new Dictionary<string, int>();

    private static IReadOnlyDictionary<string, int> Counts(params (string Tag, int Count)[] counts)
    {
        return counts.ToDictionary(c => c.Tag, c => c.Count, StringComparer.Ordinal);
    }

    private static FeedbackType TypeFor(FeedbackPolicy policy, string tag, int count, Difficulty difficulty = Difficulty.Beginner)
    {
        var plan = policy.Plan(TestVerdicts.WithErrors(TestVerdicts.Error(tag)), Counts((tag, count)), difficulty);
        return Assert.Single(plan.Items).Type;
    }

    [Theory]
    [InlineData(1, FeedbackType.Recast)]
    [InlineData(2, FeedbackType.Hint)]
    [InlineData(3, FeedbackType.Hint)]
    [InlineData(4, FeedbackType.ExplicitCorrection)]
    [InlineData(5, FeedbackType.ExplicitCorrection)]
    [InlineData(500, FeedbackType.ExplicitCorrection)]
    public void DefaultRepeatBuckets(int count, FeedbackType expected)
    {
        Assert.Equal(expected, TypeFor(FeedbackPolicy.Default, ErrorTags.ConjPerson, count));
    }

    [Theory]
    [InlineData(1, FeedbackType.Recast)]
    [InlineData(2, FeedbackType.ModelPronunciation)]
    [InlineData(3, FeedbackType.ModelPronunciation)]
    [InlineData(9, FeedbackType.ModelPronunciation)]
    public void DefaultPronunciationRule(int count, FeedbackType expected)
    {
        foreach (var tag in new[] { ErrorTags.PronR, ErrorTags.PronVowel, ErrorTags.PronSilentH, ErrorTags.PronStress })
        {
            Assert.Equal(expected, TypeFor(FeedbackPolicy.Default, tag, count));
        }
    }

    [Fact]
    public void MissingOrNonPositiveCountsCountAsFirstOffence()
    {
        var verdict = TestVerdicts.WithErrors(TestVerdicts.Error(ErrorTags.NounNumber));

        Assert.Equal(FeedbackType.Recast, Assert.Single(FeedbackPolicy.Default.Plan(verdict, NoCounts, Difficulty.Beginner).Items).Type);
        Assert.Equal(FeedbackType.Recast, Assert.Single(FeedbackPolicy.Default.Plan(verdict, Counts((ErrorTags.NounNumber, 0)), Difficulty.Beginner).Items).Type);
        Assert.Equal(FeedbackType.Recast, Assert.Single(FeedbackPolicy.Default.Plan(verdict, Counts((ErrorTags.NounNumber, -3)), Difficulty.Beginner).Items).Type);
    }

    [Fact]
    public void CountsAreLookedUpPerTag()
    {
        var verdict = TestVerdicts.WithErrors(
            TestVerdicts.Error(ErrorTags.ConjTense, 0, "fui", "voy"),
            TestVerdicts.Error(ErrorTags.GenderAgree, 5, "la casa", "el casa"));

        var plan = FeedbackPolicy.Default.Plan(verdict, Counts((ErrorTags.ConjTense, 4), (ErrorTags.GenderAgree, 1)), Difficulty.Intermediate);

        Assert.Equal(
            [
                new FeedbackItem(ErrorTags.ConjTense, FeedbackType.ExplicitCorrection, "fui", "voy"),
                new FeedbackItem(ErrorTags.GenderAgree, FeedbackType.Recast, "la casa", "el casa"),
            ],
            plan.Items);
    }

    [Fact]
    public void NotCommunicatedIsASingleSlowRepeatQuotingTheFirstError()
    {
        var verdict = TestVerdicts.Make("unknown", false, 0.2, TestVerdicts.Error(ErrorTags.VocabUnknown, 0, "quiero", "kero"), TestVerdicts.Error(ErrorTags.PronR));

        var plan = FeedbackPolicy.Default.Plan(verdict, Counts((ErrorTags.VocabUnknown, 5)), Difficulty.Advanced);

        Assert.Equal(new FeedbackItem(ErrorTags.VocabUnknown, FeedbackType.SlowRepeat, "quiero", "kero"), Assert.Single(plan.Items));
    }

    [Fact]
    public void NotCommunicatedWithoutErrorsUsesTheSentinelTag()
    {
        var plan = FeedbackPolicy.Default.Plan(TestVerdicts.Make("unknown", false, 0.0), NoCounts, Difficulty.Beginner);

        Assert.Equal(new FeedbackItem(FeedbackPolicy.NotCommunicatedTag, FeedbackType.SlowRepeat, string.Empty, string.Empty), Assert.Single(plan.Items));
    }

    [Fact]
    public void CleanVerdictHasAnEmptyPlan()
    {
        Assert.Empty(FeedbackPolicy.Default.Plan(TestVerdicts.Clean(), NoCounts, Difficulty.Beginner).Items);
    }

    [Fact]
    public void PlanIsCappedAtTwoItemsKeepingTheFirstTwoInErrorOrder()
    {
        var verdict = TestVerdicts.WithErrors(
            TestVerdicts.Error(ErrorTags.WordOrder, 0),
            TestVerdicts.Error(ErrorTags.SerEstar, 4),
            TestVerdicts.Error(ErrorTags.ConjPerson, 9),
            TestVerdicts.Error(ErrorTags.PronR, 12));

        var plan = FeedbackPolicy.Default.Plan(verdict, NoCounts, Difficulty.Beginner);

        Assert.Equal(FeedbackPolicy.MaxItems, plan.Items.Count);
        Assert.Equal([ErrorTags.WordOrder, ErrorTags.SerEstar], plan.Items.Select(i => i.Tag));
    }

    [Fact]
    public void ErrorsWithoutAMatchingRuleDoNotUseUpTheCap()
    {
        var policy = new FeedbackPolicy([new FeedbackRule(ErrorTags.ConjPerson, 1, 5, null, FeedbackType.Hint)]);
        var verdict = TestVerdicts.WithErrors(
            TestVerdicts.Error(ErrorTags.WordOrder, 0),
            TestVerdicts.Error(ErrorTags.ConjPerson, 4),
            TestVerdicts.Error(ErrorTags.ConjPerson, 8),
            TestVerdicts.Error(ErrorTags.ConjPerson, 12));

        var plan = policy.Plan(verdict, NoCounts, Difficulty.Beginner);

        Assert.Equal(2, plan.Items.Count);
        Assert.All(plan.Items, item => Assert.Equal(ErrorTags.ConjPerson, item.Tag));
    }

    [Fact]
    public void ExactTagBeatsFamilyBeatsWildcard()
    {
        var policy = new FeedbackPolicy(
        [
            new FeedbackRule("*", 1, 9, null, FeedbackType.Recast),
            new FeedbackRule("conj.*", 1, 9, null, FeedbackType.Hint),
            new FeedbackRule(ErrorTags.ConjPerson, 1, 9, null, FeedbackType.ExplicitCorrection),
        ]);

        Assert.Equal(FeedbackType.ExplicitCorrection, TypeFor(policy, ErrorTags.ConjPerson, 1));
        Assert.Equal(FeedbackType.Hint, TypeFor(policy, ErrorTags.ConjTense, 1));
        Assert.Equal(FeedbackType.Recast, TypeFor(policy, ErrorTags.NounNumber, 1));
    }

    [Fact]
    public void SpecificityDoesNotDependOnDeclarationOrder()
    {
        var policy = new FeedbackPolicy(
        [
            new FeedbackRule(ErrorTags.ConjPerson, 1, 9, null, FeedbackType.ExplicitCorrection),
            new FeedbackRule("conj.*", 1, 9, null, FeedbackType.Hint),
            new FeedbackRule("*", 1, 9, null, FeedbackType.Recast),
        ]);

        Assert.Equal(FeedbackType.ExplicitCorrection, TypeFor(policy, ErrorTags.ConjPerson, 3));
        Assert.Equal(FeedbackType.Hint, TypeFor(policy, ErrorTags.ConjIrregular, 3));
    }

    [Fact]
    public void FamilyWildcardMatchesTagsWithoutADot()
    {
        var policy = new FeedbackPolicy(
        [
            new FeedbackRule("*", 1, 9, null, FeedbackType.Recast),
            new FeedbackRule("ser_estar.*", 1, 9, null, FeedbackType.Hint),
        ]);

        Assert.Equal(FeedbackType.Hint, TypeFor(policy, ErrorTags.SerEstar, 1));
    }

    [Fact]
    public void ExactTagWithMismatchingCountFallsBackToBroaderRule()
    {
        var policy = new FeedbackPolicy(
        [
            new FeedbackRule("*", 1, 9, null, FeedbackType.Recast),
            new FeedbackRule(ErrorTags.GenderAgree, 5, 9, null, FeedbackType.ExplicitCorrection),
        ]);

        Assert.Equal(FeedbackType.Recast, TypeFor(policy, ErrorTags.GenderAgree, 4));
        Assert.Equal(FeedbackType.ExplicitCorrection, TypeFor(policy, ErrorTags.GenderAgree, 5));
    }

    [Fact]
    public void NarrowerRepeatRangeWinsAmongEqualPatterns()
    {
        var policy = new FeedbackPolicy(
        [
            new FeedbackRule("*", 1, 10, null, FeedbackType.Recast),
            new FeedbackRule("*", 3, 4, null, FeedbackType.Hint),
            new FeedbackRule("*", 4, 4, null, FeedbackType.ExplicitCorrection),
        ]);

        Assert.Equal(FeedbackType.Recast, TypeFor(policy, ErrorTags.VocabWrong, 2));
        Assert.Equal(FeedbackType.Hint, TypeFor(policy, ErrorTags.VocabWrong, 3));
        Assert.Equal(FeedbackType.ExplicitCorrection, TypeFor(policy, ErrorTags.VocabWrong, 4));
    }

    [Fact]
    public void PatternSpecificityOutranksRangeWidth()
    {
        var policy = new FeedbackPolicy(
        [
            new FeedbackRule("*", 2, 2, null, FeedbackType.Recast),
            new FeedbackRule("pron.*", 1, 100, null, FeedbackType.ModelPronunciation),
        ]);

        Assert.Equal(FeedbackType.ModelPronunciation, TypeFor(policy, ErrorTags.PronR, 2));
    }

    [Fact]
    public void DifficultyRuleAppliesOnlyToItsDifficultyAndBeatsAnEqualGenericRule()
    {
        var policy = new FeedbackPolicy(
        [
            new FeedbackRule("*", 1, 3, Difficulty.Beginner, FeedbackType.Hint),
            new FeedbackRule("*", 1, 3, null, FeedbackType.Recast),
        ]);

        Assert.Equal(FeedbackType.Hint, TypeFor(policy, ErrorTags.VocabWrong, 1, Difficulty.Beginner));
        Assert.Equal(FeedbackType.Recast, TypeFor(policy, ErrorTags.VocabWrong, 1, Difficulty.Advanced));

        var reversed = new FeedbackPolicy([policy.Rules[1], policy.Rules[0]]);
        Assert.Equal(FeedbackType.Hint, TypeFor(reversed, ErrorTags.VocabWrong, 1, Difficulty.Beginner));
    }

    [Fact]
    public void FullyEqualRulesResolveToTheEarlierOne()
    {
        var policy = new FeedbackPolicy(
        [
            new FeedbackRule("*", 1, 3, null, FeedbackType.Hint),
            new FeedbackRule("*", 1, 3, null, FeedbackType.Recast),
        ]);

        Assert.Equal(FeedbackType.Hint, TypeFor(policy, ErrorTags.VocabWrong, 2));
    }

    [Fact]
    public void NoMatchingRuleSelectsNull()
    {
        var policy = new FeedbackPolicy([new FeedbackRule(ErrorTags.PronR, 1, 1, null, FeedbackType.Recast)]);

        Assert.Null(policy.Select(ErrorTags.PronR, 2, Difficulty.Beginner));
        Assert.Null(policy.Select(ErrorTags.PronVowel, 1, Difficulty.Beginner));
        Assert.NotNull(policy.Select(ErrorTags.PronR, 1, Difficulty.Beginner));
    }

    [Fact]
    public void PolicyLoadsFromJsonAndOverridesNotCommunicatedType()
    {
        var json = """
            {
              "rules": [
                { "tagPattern": "*", "minRepeat": 1, "maxRepeat": 2147483647, "difficulty": null, "type": "Hint" },
                { "tagPattern": "ser_estar", "minRepeat": 1, "maxRepeat": 1, "difficulty": "Advanced", "type": "ExplicitCorrection" }
              ],
              "notCommunicatedType": "Hint"
            }
            """;

        var policy = FeedbackPolicy.FromJson(json);

        Assert.Equal(2, policy.Rules.Count);
        Assert.Equal(Difficulty.Advanced, policy.Rules[1].Difficulty);
        Assert.Equal(FeedbackType.Hint, policy.NotCommunicatedType);
        Assert.Equal(FeedbackType.ExplicitCorrection, TypeFor(policy, ErrorTags.SerEstar, 1, Difficulty.Advanced));
        Assert.Equal(FeedbackType.Hint, TypeFor(policy, ErrorTags.SerEstar, 1, Difficulty.Beginner));
        Assert.Equal(FeedbackType.Hint, Assert.Single(policy.Plan(TestVerdicts.Make("x", false, 0.0), NoCounts, Difficulty.Beginner).Items).Type);
    }

    [Fact]
    public void NotCommunicatedTypeDefaultsToSlowRepeatInJson()
    {
        var policy = FeedbackPolicy.FromJson("""{ "rules": [] }""");

        Assert.Equal(FeedbackType.SlowRepeat, policy.NotCommunicatedType);
        Assert.Empty(policy.Rules);
    }

    [Fact]
    public void DefaultPolicyRoundTripsThroughCanonicalJson()
    {
        var json = CanonicalJson.Serialize(FeedbackPolicy.Default);

        Assert.Equal(FeedbackPolicy.Default, FeedbackPolicy.FromJson(json));
        Assert.Equal(json, CanonicalJson.Serialize(FeedbackPolicy.FromJson(json)));
    }

    [Theory]
    [InlineData("""{ "rules": [ { "tagPattern": "", "minRepeat": 1, "maxRepeat": 1, "difficulty": null, "type": "Hint" } ] }""")]
    [InlineData("""{ "rules": [ { "tagPattern": "a*b", "minRepeat": 1, "maxRepeat": 1, "difficulty": null, "type": "Hint" } ] }""")]
    [InlineData("""{ "rules": [ { "tagPattern": ".*", "minRepeat": 1, "maxRepeat": 1, "difficulty": null, "type": "Hint" } ] }""")]
    [InlineData("""{ "rules": [ { "tagPattern": "*", "minRepeat": 0, "maxRepeat": 1, "difficulty": null, "type": "Hint" } ] }""")]
    [InlineData("""{ "rules": [ { "tagPattern": "*", "minRepeat": 3, "maxRepeat": 2, "difficulty": null, "type": "Hint" } ] }""")]
    [InlineData("""{ "rules": [ { "tagPattern": "*", "minRepeat": 1, "maxRepeat": 2, "difficulty": null, "type": "Shout" } ] }""")]
    public void MalformedRulesAreRejected(string json)
    {
        Assert.ThrowsAny<Exception>(() => FeedbackPolicy.FromJson(json));
    }

    [Fact]
    public void PlanningIsDeterministic()
    {
        var verdict = TestVerdicts.WithErrors(
            TestVerdicts.Error(ErrorTags.PronR, 0),
            TestVerdicts.Error(ErrorTags.ConjTense, 3),
            TestVerdicts.Error(ErrorTags.GenderAgree, 9));
        var counts = Counts((ErrorTags.PronR, 2), (ErrorTags.ConjTense, 7));

        var first = FeedbackPolicy.Default.Plan(verdict, counts, Difficulty.Intermediate);
        var second = FeedbackPolicy.Default.Plan(verdict, counts, Difficulty.Intermediate);

        Assert.Equal(first, second);
        Assert.Equal(CanonicalJson.Serialize(first), CanonicalJson.Serialize(second));
        Assert.Equal(
            [
                new FeedbackItem(ErrorTags.PronR, FeedbackType.ModelPronunciation, "esperado", "obtenido"),
                new FeedbackItem(ErrorTags.ConjTense, FeedbackType.ExplicitCorrection, "esperado", "obtenido"),
            ],
            first.Items);
    }
}

/// <summary>Round-trip tests for the small configuration records.</summary>
public class DecisionConfigJsonTests
{
    [Fact]
    public void FsrsParametersRoundTrip()
    {
        var json = CanonicalJson.Serialize(FsrsParameters.Default);

        Assert.Equal(FsrsParameters.Default, CanonicalJson.Deserialize<FsrsParameters>(json));
    }

    [Fact]
    public void RatingThresholdsRoundTripWithoutDerivedMargins()
    {
        var json = CanonicalJson.Serialize(RatingThresholds.Default);

        Assert.DoesNotContain("Margin", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RatingThresholds.Default, CanonicalJson.Deserialize<RatingThresholds>(json));
    }

    [Fact]
    public void ReviewCardJsonOmitsDerivedFlags()
    {
        var json = CanonicalJson.Serialize(ReviewCard.New("x", new StoredTimestamp(5)));

        Assert.DoesNotContain("isNew", json, StringComparison.Ordinal);
    }
}
