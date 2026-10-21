using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Asr.Tests;

/// <summary>Tests for <see cref="ScriptedWordRecognizer"/>, <see cref="ReplayWordRecognizer"/> and <see cref="AsrResultJson"/>.</summary>
public class RecognizerDoubleTests
{
    private static readonly RecognitionRequest Request = RecognitionRequest.Constrained(["hola", "adiós"]);

    [Fact]
    public void ScriptedReturnsTheConfiguredResultForAnyClip()
    {
        var expected = TestData.Result("hola");
        var recognizer = new ScriptedWordRecognizer(expected);

        var first = recognizer.Recognize(PcmClip.FromSamples([1, 2, 3]), Request);
        var second = recognizer.Recognize(PcmClip.FromSamples([4, 5, 6, 7]), RecognitionRequest.Open());

        Assert.Equal(expected, first);
        Assert.Equal(expected, second);
        Assert.Equal(2, recognizer.CallCount);
        Assert.Equal(RecognitionRequest.Open(), recognizer.LastRequest);
        Assert.Equal("model-a", recognizer.ModelHash);
    }

    [Fact]
    public void ScriptedPrefersTheMappingByClipHashOverTheFallback()
    {
        var clipA = PcmClip.FromSamples([1, 2, 3]);
        var clipB = PcmClip.FromSamples([9, 9, 9]);
        var mapped = TestData.Result("adiós");
        var fallback = TestData.Result("hola");
        var recognizer = new ScriptedWordRecognizer(fallback, new Dictionary<string, AsrResult> { [clipA.ClipHash] = mapped });

        Assert.Equal(mapped, recognizer.Recognize(clipA, Request));
        Assert.Equal(fallback, recognizer.Recognize(clipB, Request));
    }

    [Fact]
    public void ScriptedWithoutFallbackThrowsForUnmappedClips()
    {
        var recognizer = new ScriptedWordRecognizer(null, new Dictionary<string, AsrResult>());

        Assert.Equal("scripted", recognizer.ModelHash);
        Assert.Throws<InvalidOperationException>(() => recognizer.Recognize(PcmClip.FromSamples([1]), Request));
    }

    [Fact]
    public void ReplayReturnsTheRecordedResultByClipHash()
    {
        var clip = PcmClip.FromSamples([10, 20, 30]);
        var recorded = TestData.Result("hola");
        var recognizer = new ReplayWordRecognizer(new Dictionary<string, AsrResult> { [clip.ClipHash] = recorded });

        Assert.Equal(recorded, recognizer.Recognize(clip, Request));
        Assert.Equal(recorded, recognizer.Recognize(clip, RecognitionRequest.Open()));
        Assert.Equal(1, recognizer.Count);
        Assert.Equal("model-a", recognizer.ModelHash);
    }

    [Fact]
    public void ReplayThrowsReplayMissForAnUnknownClip()
    {
        var known = PcmClip.FromSamples([10, 20, 30]);
        var unknown = PcmClip.FromSamples([30, 20, 10]);
        var recognizer = new ReplayWordRecognizer(new Dictionary<string, AsrResult> { [known.ClipHash] = TestData.Result("hola") });

        var miss = Assert.Throws<ReplayMissException>(() => recognizer.Recognize(unknown, Request));

        Assert.Equal(unknown.ClipHash, miss.ClipHash);
        Assert.Contains(unknown.ClipHash, miss.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayModelHashCombinesDistinctRecordedHashesInSortedOrder()
    {
        var results = new Dictionary<string, AsrResult>
        {
            ["c2"] = TestData.Result("a", "zzz"),
            ["c1"] = TestData.Result("b", "aaa"),
            ["c3"] = TestData.Result("c", "aaa"),
        };

        var recognizer = new ReplayWordRecognizer(results);

        Assert.Equal(DeterministicHash.Combine("aaa", "zzz"), recognizer.ModelHash);
    }

    [Fact]
    public void AsrResultSurvivesACanonicalJsonRoundTrip()
    {
        var result = new AsrResult(
            "adiós amigo",
            [new AsrHypothesis("adiós amigo", 0.75), new AsrHypothesis("adiós", 0.25)],
            [new AsrWord("adiós", 0.5, 1.0, 0.75), new AsrWord("amigo", 1.0, 1.5, 1.0)],
            0.75,
            "abc123",
            RecognitionMode.Open);

        var json = AsrResultJson.Serialize(result);

        Assert.Equal(
            "{\"transcript\":\"adiós amigo\",\"nBest\":[{\"text\":\"adiós amigo\",\"score\":0.75},{\"text\":\"adiós\",\"score\":0.25}],"
            + "\"words\":[{\"text\":\"adiós\",\"startSeconds\":0.5,\"endSeconds\":1,\"confidence\":0.75},"
            + "{\"text\":\"amigo\",\"startSeconds\":1,\"endSeconds\":1.5,\"confidence\":1}],"
            + "\"confidence\":0.75,\"modelHash\":\"abc123\",\"mode\":\"Open\"}",
            json);
        Assert.Equal(result, AsrResultJson.Deserialize(json));
        Assert.Equal(json, AsrResultJson.Serialize(AsrResultJson.Deserialize(json)));
        Assert.Equal(AsrResultJson.Hash(result), AsrResultJson.Hash(AsrResultJson.Deserialize(json)));
    }

    [Fact]
    public void ResultMapsSerializeWithSortedKeysAndRoundTrip()
    {
        var map = new Dictionary<string, AsrResult>
        {
            ["bbb"] = TestData.Result("adiós"),
            ["aaa"] = TestData.Result("hola"),
        };

        var json = AsrResultJson.SerializeMap(map);
        var parsed = AsrResultJson.DeserializeMap(json);

        Assert.StartsWith("{\"aaa\":{", json, StringComparison.Ordinal);
        Assert.True(json.IndexOf("\"aaa\"", StringComparison.Ordinal) < json.IndexOf("\"bbb\"", StringComparison.Ordinal));
        Assert.Equal(["aaa", "bbb"], parsed.Keys.ToArray());
        Assert.Equal(map["aaa"], parsed["aaa"]);
        Assert.Equal(map["bbb"], parsed["bbb"]);
        Assert.Equal(json, AsrResultJson.SerializeMap(parsed));
    }

    [Fact]
    public void ReplayOverADeserializedMapReturnsTheRecordedResults()
    {
        var clip = PcmClip.FromSamples([5, 6, 7]);
        var recorded = TestData.Result("hola");
        var json = AsrResultJson.SerializeMap(new Dictionary<string, AsrResult> { [clip.ClipHash] = recorded });

        var recognizer = new ReplayWordRecognizer(AsrResultJson.DeserializeMap(json));

        Assert.Equal(recorded, recognizer.Recognize(clip, Request));
    }
}
