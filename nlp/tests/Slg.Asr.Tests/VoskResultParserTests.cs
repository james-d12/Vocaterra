using Slg.Core.Prompts;
using Slg.Core.Stages;

namespace Slg.Asr.Tests;

/// <summary>Tests for <see cref="VoskResultParser"/> over the JSON shapes Vosk emits.</summary>
public class VoskResultParserTests
{
    private const string Alternatives =
        "{\"alternatives\":["
        + "{\"confidence\":280.0,\"result\":[{\"conf\":1.0,\"end\":0.9,\"start\":0.4,\"word\":\"hola\"}],\"text\":\"hola\"},"
        + "{\"confidence\":282.0,\"result\":[{\"conf\":0.75,\"end\":1.0,\"start\":0.5,\"word\":\"adiós\"}],\"text\":\"adiós\"},"
        + "{\"confidence\":282.0,\"text\":\"[unk]\"}"
        + "]}";

    [Fact]
    public void NBestIsOrderedByConfidenceDescendingThenTextOrdinal()
    {
        var result = VoskResultParser.Parse(Alternatives, RecognitionMode.Constrained, "hash");

        // 282 ties between "[unk]" and "adiós"; '[' (U+005B) sorts before 'a' (U+0061) ordinally.
        Assert.Equal(["[unk]", "adiós", "hola"], result.NBest.Select(h => h.Text).ToArray());
        Assert.Equal("[unk]", result.Transcript);
    }

    [Fact]
    public void NBestScoresAreSoftmaxWeightsThatSumToOne()
    {
        var result = VoskResultParser.Parse(Alternatives, RecognitionMode.Constrained, "hash");

        var second = Math.Exp(-2.0);
        var total = 2.0 + second;
        Assert.Equal(1.0 / total, result.NBest[0].Score, 12);
        Assert.Equal(1.0 / total, result.NBest[1].Score, 12);
        Assert.Equal(second / total, result.NBest[2].Score, 12);
        Assert.Equal(1.0, result.NBest.Sum(h => h.Score), 12);
        Assert.Equal(result.NBest[0].Score, result.Confidence);
    }

    [Fact]
    public void WordsComeFromTheBestAlternative()
    {
        const string json =
            "{\"alternatives\":["
            + "{\"confidence\":10.0,\"result\":[{\"conf\":0.5,\"end\":0.6,\"start\":0.1,\"word\":\"hola\"}],\"text\":\"hola\"},"
            + "{\"confidence\":11.0,\"result\":[{\"conf\":0.75,\"end\":1.0,\"start\":0.5,\"word\":\"adiós\"},"
            + "{\"conf\":1.0,\"end\":1.5,\"start\":1.0,\"word\":\"amigo\"}],\"text\":\"adiós amigo\"}"
            + "]}";

        var result = VoskResultParser.Parse(json, RecognitionMode.Open, "hash");

        Assert.Equal("adiós amigo", result.Transcript);
        Assert.Equal([new AsrWord("adiós", 0.5, 1.0, 0.75), new AsrWord("amigo", 1.0, 1.5, 1.0)], result.Words.ToArray());
        Assert.Equal(RecognitionMode.Open, result.Mode);
        Assert.Equal("hash", result.ModelHash);
    }

    [Fact]
    public void WordsWithoutConfidenceDefaultToOne()
    {
        // The shape real Vosk output has when alternatives are enabled: words carry only start, end and word.
        const string json =
            "{\"alternatives\" : [{\"confidence\" : 139.733215, \"result\" : [{\"end\" : 0.240000, \"start\" : 0.000000, \"word\" : \"buenos\"},"
            + " {\"end\" : 1.350000, \"start\" : 1.200000, \"word\" : \"días\"}], \"text\" : \"buenos días\"},"
            + " {\"confidence\" : 139.137573, \"result\" : [{\"end\" : 1.530000, \"start\" : 1.200000, \"word\" : \"gracias\"}], \"text\" : \"gracias\"}]}";

        var result = VoskResultParser.Parse(json, RecognitionMode.Constrained, "hash");

        Assert.Equal("buenos días", result.Transcript);
        Assert.Equal([new AsrWord("buenos", 0.0, 0.24, 1.0), new AsrWord("días", 1.2, 1.35, 1.0)], result.Words.ToArray());
        Assert.InRange(result.Confidence, 0.6, 0.7);
    }

    [Fact]
    public void SingleResultHasConfidenceOneAndASingleHypothesis()
    {
        const string json = "{\"result\":[{\"conf\":0.9,\"end\":1.2,\"start\":0.5,\"word\":\"hola\"}],\"text\":\"hola\"}";

        var result = VoskResultParser.Parse(json, RecognitionMode.Constrained, "hash");

        Assert.Equal("hola", result.Transcript);
        Assert.Equal([new AsrHypothesis("hola", 1.0)], result.NBest.ToArray());
        Assert.Equal(1.0, result.Confidence);
        Assert.Equal([new AsrWord("hola", 0.5, 1.2, 0.9)], result.Words.ToArray());
    }

    [Theory]
    [InlineData("{\"text\":\"\"}")]
    [InlineData("{\"alternatives\":[{\"confidence\":0.0,\"text\":\"\"}]}")]
    [InlineData("{\"alternatives\":[]}")]
    public void EmptyResultsGiveAnEmptyTranscript(string json)
    {
        var result = VoskResultParser.Parse(json, RecognitionMode.Constrained, "hash");

        Assert.Equal(string.Empty, result.Transcript);
        Assert.Empty(result.Words);
        Assert.Equal(1.0, result.Confidence);
        Assert.Single(result.NBest);
    }

    [Fact]
    public void DuplicateTextsKeepTheirBestEntry()
    {
        const string json =
            "{\"alternatives\":["
            + "{\"confidence\":5.0,\"text\":\"hola\"},"
            + "{\"confidence\":7.0,\"text\":\"hola\"},"
            + "{\"confidence\":6.0,\"text\":\"adiós\"}"
            + "]}";

        var result = VoskResultParser.Parse(json, RecognitionMode.Constrained, "hash");

        Assert.Equal(["hola", "adiós"], result.NBest.Select(h => h.Text).ToArray());
    }

    [Fact]
    public void ParsingTheSameJsonTwiceGivesEqualResults()
    {
        var first = VoskResultParser.Parse(Alternatives, RecognitionMode.Constrained, "hash");
        var second = VoskResultParser.Parse(Alternatives, RecognitionMode.Constrained, "hash");

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"alternatives\":[1]}")]
    [InlineData("{\"result\":[{\"start\":0.0}],\"text\":\"x\"}")]
    public void MalformedResultsThrowInvalidData(string json)
    {
        Assert.Throws<InvalidDataException>(() => VoskResultParser.Parse(json, RecognitionMode.Open, "hash"));
    }
}
