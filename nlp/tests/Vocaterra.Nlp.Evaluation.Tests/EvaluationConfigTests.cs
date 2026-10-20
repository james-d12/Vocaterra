using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Taxonomy;

namespace Vocaterra.Nlp.Evaluation.Tests;

/// <summary>Tests for <see cref="EvaluationConfig"/> and <see cref="DifficultyEvaluationConfig"/>.</summary>
public class EvaluationConfigTests
{
    [Fact]
    public void DefaultRoundTripsThroughCanonicalJson()
    {
        var json = CanonicalJson.Serialize(EvaluationConfig.Default);
        var restored = CanonicalJson.Deserialize<EvaluationConfig>(json);

        Assert.Equal(EvaluationConfig.Default, restored);
        Assert.Equal(EvaluationConfig.Default.GetHashCode(), restored.GetHashCode());
        Assert.Equal(json, CanonicalJson.Serialize(restored));
    }

    [Fact]
    public void DefaultSerializesWithSortedWeightKeysAndCamelCaseNames()
    {
        var json = CanonicalJson.Serialize(EvaluationConfig.Default);

        const string BeginnerPrefix =
            "{\"beginner\":{\"errorWeights\":{\"conj.irregular\":0.2,\"conj.person\":0.25,\"conj.tense\":0.25,\"gender.agree\":0.15,"
            + "\"noun.number\":0.15,\"pron.r\":0,\"pron.silent_h\":0,\"pron.stress\":0,\"pron.vowel\":0,\"ser_estar\":0.2,"
            + "\"vocab.unknown\":0.4,\"vocab.wrong\":0.6,\"word_order\":0.1},"
            + "\"communicatedConfidenceThreshold\":0.6,";
        Assert.StartsWith(BeginnerPrefix, json, StringComparison.Ordinal);
        Assert.Contains("\"intermediate\":{", json, StringComparison.Ordinal);
        Assert.Contains("\"advanced\":{", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultBeginnerWeightsMatchTheSpecification()
    {
        var weights = EvaluationConfig.Default.Beginner.ErrorWeights;

        Assert.Equal(0.6, weights[ErrorTags.VocabWrong]);
        Assert.Equal(0.4, weights[ErrorTags.VocabUnknown]);
        Assert.Equal(0.25, weights[ErrorTags.ConjPerson]);
        Assert.Equal(0.25, weights[ErrorTags.ConjTense]);
        Assert.Equal(0.2, weights[ErrorTags.ConjIrregular]);
        Assert.Equal(0.15, weights[ErrorTags.GenderAgree]);
        Assert.Equal(0.15, weights[ErrorTags.NounNumber]);
        Assert.Equal(0.2, weights[ErrorTags.SerEstar]);
        Assert.Equal(0.1, weights[ErrorTags.WordOrder]);
        foreach (var tag in ErrorTags.All.Where(ErrorTags.IsPronunciation))
        {
            Assert.Equal(0.0, weights[tag]);
        }

        Assert.Equal(0.6, EvaluationConfig.Default.Beginner.CommunicatedConfidenceThreshold);
        Assert.Equal(0.02, EvaluationConfig.Default.Beginner.Epsilon);
    }

    [Fact]
    public void ThresholdsTightenWithDifficulty()
    {
        var config = EvaluationConfig.Default;

        Assert.True(config.Beginner.CorrectnessThreshold < config.Intermediate.CorrectnessThreshold);
        Assert.True(config.Intermediate.CorrectnessThreshold < config.Advanced.CorrectnessThreshold);
        Assert.True(config.Beginner.PronunciationThreshold < config.Advanced.PronunciationThreshold);
        Assert.True(config.Beginner.WeightFor(ErrorTags.ConjPerson) < config.Advanced.WeightFor(ErrorTags.ConjPerson));
    }

    [Fact]
    public void ForSelectsTheTierAndRejectsUnknownDifficulties()
    {
        var config = EvaluationConfig.Default;

        Assert.Same(config.Beginner, config.For(Difficulty.Beginner));
        Assert.Same(config.Intermediate, config.For(Difficulty.Intermediate));
        Assert.Same(config.Advanced, config.For(Difficulty.Advanced));
        Assert.Throws<ArgumentOutOfRangeException>(() => config.For((Difficulty)42));
    }

    [Fact]
    public void WeightForFallsBackByTagFamily()
    {
        var settings = new DifficultyEvaluationConfig(new Dictionary<string, double> { ["a"] = 0.5 }, 0.6, 0.7, 0.5, 0.02, 0.33);

        Assert.Equal(0.5, settings.WeightFor("a"));
        Assert.Equal(0.33, settings.WeightFor("b"));
        Assert.Equal(0.0, settings.WeightFor(ErrorTags.PronR));
        Assert.Equal(0.0, settings.WeightFor("pron.unheard_of"));
    }

    [Fact]
    public void WeightsAreStoredOrdinalSortedWhateverTheSourceOrder()
    {
        var source = new Dictionary<string, double> { ["z"] = 1, ["B"] = 2, ["a"] = 3, ["é"] = 4 };

        var settings = new DifficultyEvaluationConfig(source, 0.6, 0.7, 0.5, 0.02, 0.3);

        Assert.Equal(["B", "a", "z", "é"], settings.ErrorWeights.Keys);
        Assert.StartsWith(
            "{\"errorWeights\":{\"B\":2,\"a\":3,\"z\":1,\"é\":4},",
            CanonicalJson.Serialize(settings),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WeightsAreCopiedSoLaterChangesToTheSourceDoNotLeak()
    {
        var source = new Dictionary<string, double> { ["a"] = 1 };
        var settings = new DifficultyEvaluationConfig(source, 0.6, 0.7, 0.5, 0.02, 0.3);

        source["a"] = 9;
        source["b"] = 9;

        Assert.Equal(1.0, settings.ErrorWeights["a"]);
        Assert.Single(settings.ErrorWeights);
    }

    [Fact]
    public void ValueEqualityComparesWeightContents()
    {
        var left = new DifficultyEvaluationConfig(new Dictionary<string, double> { ["a"] = 1, ["b"] = 2 }, 0.6, 0.7, 0.5, 0.02, 0.3);
        var same = new DifficultyEvaluationConfig(new Dictionary<string, double> { ["b"] = 2, ["a"] = 1 }, 0.6, 0.7, 0.5, 0.02, 0.3);
        var differentWeight = left with { ErrorWeights = new Dictionary<string, double> { ["a"] = 1, ["b"] = 3 } };
        var differentKey = left with { ErrorWeights = new Dictionary<string, double> { ["a"] = 1, ["c"] = 2 } };
        var differentEpsilon = left with { Epsilon = 0.03 };

        Assert.Equal(left, same);
        Assert.Equal(left.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(left, differentWeight);
        Assert.NotEqual(left, differentKey);
        Assert.NotEqual(left, differentEpsilon);
    }

    [Fact]
    public void MarginsUseTheConfiguredThresholdsAndEpsilon()
    {
        var settings = EvaluationConfig.Default.Beginner;

        Assert.Equal(new Margin(0.6, 0.02), settings.CommunicatedMargin());
        Assert.Equal(new Margin(settings.CorrectnessThreshold, 0.02), settings.CorrectnessMargin());
        Assert.Equal(new Margin(settings.PronunciationThreshold, 0.02), settings.PronunciationMargin());
    }

    [Fact]
    public void InvalidNumbersAreRejected()
    {
        var weights = new Dictionary<string, double>();

        Assert.Throws<ArgumentOutOfRangeException>(() => new DifficultyEvaluationConfig(new Dictionary<string, double> { ["a"] = -0.1 }, 0.6, 0.7, 0.5, 0.02, 0.3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DifficultyEvaluationConfig(new Dictionary<string, double> { ["a"] = double.NaN }, 0.6, 0.7, 0.5, 0.02, 0.3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DifficultyEvaluationConfig(weights, double.NaN, 0.7, 0.5, 0.02, 0.3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DifficultyEvaluationConfig(weights, 0.6, double.PositiveInfinity, 0.5, 0.02, 0.3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DifficultyEvaluationConfig(weights, 0.6, 0.7, double.NaN, 0.02, 0.3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DifficultyEvaluationConfig(weights, 0.6, 0.7, 0.5, -0.01, 0.3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DifficultyEvaluationConfig(weights, 0.6, 0.7, 0.5, 0.02, -1.0));
        Assert.Throws<ArgumentNullException>(() => new DifficultyEvaluationConfig(null!, 0.6, 0.7, 0.5, 0.02, 0.3));
    }

    [Fact]
    public void InvalidJsonIsRejected()
    {
        var json = CanonicalJson.Serialize(EvaluationConfig.Default).Replace("\"conj.person\":0.25", "\"conj.person\":-0.25", StringComparison.Ordinal);

        Assert.ThrowsAny<Exception>(() => CanonicalJson.Deserialize<EvaluationConfig>(json));
    }

    [Fact]
    public void ZeroEpsilonProducesNoUncertainBand()
    {
        var settings = EvaluationConfig.Default.Beginner with { Epsilon = 0.0 };

        Assert.Equal(MarginDecision.Accept, settings.CommunicatedMargin().Evaluate(0.6));
        Assert.Equal(MarginDecision.Reject, settings.CommunicatedMargin().Evaluate(0.5999));
    }
}
