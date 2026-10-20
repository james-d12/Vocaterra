using System.Collections.Immutable;
using System.Text.Json;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Replay;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Core.Tests;

/// <summary>Tests for <see cref="CanonicalJson"/> and the verdict contract types.</summary>
public class CanonicalJsonTests
{
    private static Verdict SampleVerdict()
    {
        var models = new Dictionary<string, string>
        {
            ["phoneme"] = "bbb",
            ["asr"] = "aaa",
            ["Zeta"] = "zzz",
        };
        return new Verdict(
            new IntentId("offer_item"),
            SlotMap.Empty.With("qty", "2").With("item", "espada"),
            true,
            0.7,
            [
                new ErrorTag(ErrorTags.ConjPerson, new TextSpan(3, 5), "tengo", "tiene"),
                new ErrorTag(ErrorTags.NounNumber, new TextSpan(12, 6), "espadas", "espada"),
            ],
            [new PhoneScore(new TextSpan(0, 2), "k", "k", 0.93125)],
            0.8123456789012345,
            ResolvedBy.Grammar,
            new VersionStamp("content-hash", models));
    }

    [Fact]
    public void VerdictRoundTripsByteIdentically()
    {
        var verdict = SampleVerdict();
        var first = CanonicalJson.Serialize(verdict);
        var parsed = CanonicalJson.Deserialize<Verdict>(first);
        var second = CanonicalJson.Serialize(parsed);

        Assert.Equal(first, second);
        Assert.Equal(verdict, parsed);
        Assert.Equal(CanonicalJson.Hash(verdict), CanonicalJson.Hash(parsed));
    }

    [Fact]
    public void VerdictJsonHasExpectedShape()
    {
        const string expected =
            "{\"intent\":\"offer_item\",\"slots\":{\"item\":\"espada\",\"qty\":\"2\"},\"communicated\":true,\"correctness\":0.7,"
            + "\"errors\":[{\"tag\":\"conj.person\",\"span\":{\"start\":3,\"length\":5},\"expected\":\"tengo\",\"actual\":\"tiene\"},"
            + "{\"tag\":\"noun.number\",\"span\":{\"start\":12,\"length\":6},\"expected\":\"espadas\",\"actual\":\"espada\"}],"
            + "\"pronunciation\":[{\"graphemeSpan\":{\"start\":0,\"length\":2},\"expected\":\"k\",\"actual\":\"k\",\"score\":0.93125}],"
            + "\"confidence\":0.8123456789012345,\"resolvedBy\":\"Grammar\","
            + "\"versions\":{\"contentHash\":\"content-hash\",\"modelHashes\":{\"Zeta\":\"zzz\",\"asr\":\"aaa\",\"phoneme\":\"bbb\"}}}";
        Assert.Equal(expected, CanonicalJson.Serialize(SampleVerdict()));
    }

    [Fact]
    public void SlotsAndModelHashesAreSerializedSorted()
    {
        var json = CanonicalJson.Serialize(SampleVerdict());
        Assert.True(json.IndexOf("\"item\"", StringComparison.Ordinal) < json.IndexOf("\"qty\"", StringComparison.Ordinal));
        var zeta = json.IndexOf("\"Zeta\"", StringComparison.Ordinal);
        var asr = json.IndexOf("\"asr\"", StringComparison.Ordinal);
        var phoneme = json.IndexOf("\"phoneme\"", StringComparison.Ordinal);
        Assert.True(zeta < asr && asr < phoneme);
    }

    [Fact]
    public void UnsortedInputDictionariesAreSortedOnSerialize()
    {
        IReadOnlyDictionary<string, string> unsorted = new Dictionary<string, string> { ["b"] = "2", ["c"] = "3", ["a"] = "1" };
        Assert.Equal("{\"a\":\"1\",\"b\":\"2\",\"c\":\"3\"}", CanonicalJson.Serialize(unsorted));
    }

    [Fact]
    public void DeserializedModelHashesAreSortedAndImmutable()
    {
        var stamp = CanonicalJson.Deserialize<VersionStamp>("{\"contentHash\":\"h\",\"modelHashes\":{\"b\":\"2\",\"a\":\"1\"}}");
        Assert.Equal(["a", "b"], stamp.ModelHashes.Keys);
        Assert.IsType<ImmutableSortedDictionary<string, string>>(stamp.ModelHashes);
    }

    [Fact]
    public void VersionStampNormalizesModelHashesAndEmptyIsEmpty()
    {
        var stamp = new VersionStamp("h", new Dictionary<string, string> { ["z"] = "1", ["B"] = "2", ["a"] = "3" });
        Assert.Equal(["B", "a", "z"], stamp.ModelHashes.Keys);
        Assert.Equal(string.Empty, VersionStamp.Empty.ContentHash);
        Assert.Empty(VersionStamp.Empty.ModelHashes);
        Assert.Equal(VersionStamp.Empty, new VersionStamp(string.Empty, new Dictionary<string, string>()));
    }

    [Fact]
    public void UnknownIntentSerializesAsPlainString()
    {
        Assert.Equal("\"unknown\"", CanonicalJson.Serialize(IntentId.Unknown));
        Assert.Equal(IntentId.Unknown, CanonicalJson.Deserialize<IntentId>("\"unknown\""));
        Assert.True(IntentId.Unknown.IsUnknown);
        Assert.False(new IntentId("greet").IsUnknown);
        Assert.Equal("unknown", IntentId.Unknown.ToString());
        Assert.Equal("greet", new IntentId("greet").ToString());
    }

    [Fact]
    public void TextSpanSerializesAsStartAndLength()
    {
        Assert.Equal("{\"start\":4,\"length\":2}", CanonicalJson.Serialize(new TextSpan(4, 2)));
        Assert.Equal(new TextSpan(4, 2), CanonicalJson.Deserialize<TextSpan>("{\"length\":2,\"start\":4}"));
        Assert.Equal(6, new TextSpan(4, 2).End);
    }

    [Fact]
    public void EnumsSerializeAsNames()
    {
        Assert.Equal("\"Embedding\"", CanonicalJson.Serialize(ResolvedBy.Embedding));
        Assert.Equal(ResolvedBy.Alias, CanonicalJson.Deserialize<ResolvedBy>("\"Alias\""));
        Assert.Throws<JsonException>(() => CanonicalJson.Deserialize<ResolvedBy>("1"));
    }

    [Fact]
    public void ResolvedByOrderIsFixed()
    {
        Assert.Equal(
            ["Exact", "Alias", "Fuzzy", "Grammar", "Rules", "Embedding", "Unresolved"],
            Enum.GetNames<ResolvedBy>());
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteNumbersThrow(double value)
    {
        var verdict = SampleVerdict() with { Confidence = value };
        Assert.ThrowsAny<ArgumentException>(() => CanonicalJson.Serialize(verdict));
    }

    [Fact]
    public void AccentsAreWrittenLiterallyAndHashIsStable()
    {
        var json = CanonicalJson.Serialize(new ErrorTag("vocab.wrong", new TextSpan(0, 4), "niño", "niña"));
        Assert.Contains("niño", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u", json, StringComparison.Ordinal);
        Assert.Equal(DeterministicHash.OfText(json), CanonicalJson.Hash(new ErrorTag("vocab.wrong", new TextSpan(0, 4), "niño", "niña")));
    }

    [Fact]
    public void MissingNonNullableMembersAreRejected()
    {
        Assert.Throws<JsonException>(() => CanonicalJson.Deserialize<ErrorTag>("{\"tag\":null,\"span\":{\"start\":0,\"length\":1},\"expected\":\"a\",\"actual\":\"b\"}"));
    }

    [Fact]
    public void ReplayRecordRoundTripsAndSerializeUsesCanonicalJson()
    {
        var record = new ReplayRecord("clip", "prompt-1", "state", new VersionStamp("c", new Dictionary<string, string> { ["m"] = "1" }), SampleVerdict());
        var json = record.Serialize();
        Assert.Equal(CanonicalJson.Serialize(record), json);
        Assert.StartsWith("{\"clipHash\":\"clip\",\"promptId\":\"prompt-1\",\"gameStateHash\":\"state\",\"versions\":", json, StringComparison.Ordinal);
        var parsed = ReplayRecord.Deserialize(json);
        Assert.Equal(record, parsed);
        Assert.Equal(json, parsed.Serialize());
    }

    [Fact]
    public void VerdictEqualityComparesListContents()
    {
        Assert.Equal(SampleVerdict(), SampleVerdict());
        Assert.Equal(SampleVerdict().GetHashCode(), SampleVerdict().GetHashCode());
        Assert.NotEqual(SampleVerdict(), SampleVerdict() with { Communicated = false });
        Assert.NotEqual(SampleVerdict(), SampleVerdict() with { Errors = [] });
    }
}
