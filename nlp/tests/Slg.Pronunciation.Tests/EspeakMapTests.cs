using Slg.Phonology;

namespace Slg.Pronunciation.Tests;

/// <summary>Tests for the espeak label mapping and the pure parts of the wav2vec2 adapter.</summary>
public class EspeakMapTests
{
    [Fact]
    public void ThePaddingTokenBecomesTheCtcBlank()
    {
        Assert.Equal(CtcDecoder.BlankSymbol, EspeakPhoneMap.Default.Map(Wav2Vec2PhonemeRecognizer.PaddingToken));
    }

    [Fact]
    public void ScriptGMapsToTheAsciiGOfTheInventory()
    {
        Assert.Equal("g", EspeakPhoneMap.Default.Map("ɡ"));
        Assert.Equal("g", EspeakPhoneMap.Default.Map("g"));
    }

    [Fact]
    public void InventoryPhonesMapToThemselves()
    {
        foreach (var phone in PhoneInventory.All)
        {
            Assert.Equal(phone, EspeakPhoneMap.Default.Map(phone));
            Assert.True(EspeakPhoneMap.Default.IsMapped(phone));
        }
    }

    [Fact]
    public void EveryTableValueIsAnInventoryPhoneOrASpecialToken()
    {
        foreach (var (label, value) in EspeakPhoneMap.Default.Entries)
        {
            Assert.True(PhoneInventory.IsKnown(value), $"'{label}' maps to '{value}', which is not in the inventory.");
        }

        foreach (var (label, value) in EspeakPhoneMap.Default.Specials)
        {
            Assert.True(value == CtcDecoder.BlankSymbol || value == label, $"Special '{label}' must map to the blank or itself.");
        }
    }

    [Fact]
    public void TableKeysDoNotShadowInventoryPhones()
    {
        foreach (var label in EspeakPhoneMap.Default.Entries.Keys)
        {
            Assert.False(PhoneInventory.IsKnown(label), $"'{label}' is already an inventory phone and needs no entry.");
        }
    }

    [Fact]
    public void TableKeysAreSortedAndFreeOfDuplicates()
    {
        var keys = EspeakPhoneMap.Default.Entries.Keys.ToArray();

        Assert.Equal(keys.OrderBy(static k => k, StringComparer.Ordinal), keys);
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void LengthMarksAreDroppedFromVowels()
    {
        Assert.Equal("a", EspeakPhoneMap.Default.Map("aː"));
        Assert.Equal("i", EspeakPhoneMap.Default.Map("iː"));
    }

    [Fact]
    public void UnknownLabelsAreKeptVerbatimAndFlaggedUnmapped()
    {
        Assert.Equal("ʔ", EspeakPhoneMap.Default.Map("ʔ"));
        Assert.False(EspeakPhoneMap.Default.IsMapped("ʔ"));
    }

    [Fact]
    public void ParseReadsACustomTable()
    {
        var map = EspeakPhoneMap.Parse("""{"specials":{"<pad>":"<blank>"},"map":{"ɛ":"e"}}""");

        Assert.Equal("e", map.Map("ɛ"));
        Assert.Equal("<blank>", map.Map("<pad>"));
        Assert.Throws<System.Text.Json.JsonException>(() => EspeakPhoneMap.Parse("""{"map":{}}"""));
    }

    [Fact]
    public void VocabularyMappingMergesLabelsThatShareAPhoneAndListsUnmappedOnes()
    {
        var labels = new Dictionary<string, int>
        {
            ["<pad>"] = 0,
            ["<s>"] = 1,
            ["a"] = 2,
            ["ɑ"] = 3,
            ["ɡ"] = 4,
            ["g"] = 5,
            ["ʔ"] = 6,
            ["ʕ"] = 7,
        };

        var mapping = VocabularyMapping.Build(labels, EspeakPhoneMap.Default);

        Assert.Equal(["<blank>", "<s>", "a", "g", "ʔ", "ʕ"], mapping.Symbols);
        Assert.Equal([[0], [1], [2, 3], [4, 5], [6], [7]], mapping.Groups);
        Assert.Equal(["ʔ", "ʕ"], mapping.Unmapped);
    }

    [Fact]
    public void VocabularyMappingRejectsGapsDuplicatesAndAMissingPad()
    {
        var map = EspeakPhoneMap.Default;

        Assert.Throws<ArgumentException>(() => VocabularyMapping.Build(new Dictionary<string, int> { ["<pad>"] = 0, ["a"] = 2 }, map));
        Assert.Throws<ArgumentException>(() => VocabularyMapping.Build(new Dictionary<string, int> { ["<pad>"] = 0, ["a"] = 0 }, map));
        Assert.Throws<ArgumentException>(() => VocabularyMapping.Build(new Dictionary<string, int> { ["a"] = 0, ["b"] = 1 }, map));
    }

    [Fact]
    public void LogSoftmaxRowsSumToOneAndMergedColumnsAddProbabilities()
    {
        // One frame, three equal logits, the last two columns merged into one symbol.
        var merged = Wav2Vec2PhonemeRecognizer.LogSoftmaxAndMerge([0f, 0f, 0f], 1, 3, [[0], [1, 2]]);

        Assert.Equal(Math.Log(1.0 / 3.0), merged[0], 5);
        Assert.Equal(Math.Log(2.0 / 3.0), merged[1], 5);
        Assert.Equal(1.0, Math.Exp(merged[0]) + Math.Exp(merged[1]), 5);
    }

    [Fact]
    public void LogSoftmaxIsStableForLargeLogits()
    {
        var merged = Wav2Vec2PhonemeRecognizer.LogSoftmaxAndMerge([1000f, 998f, -1000f, 0f, 0f, 0f], 2, 3, [[0], [1], [2]]);

        Assert.All(merged, value => Assert.True(float.IsFinite(value) || value < -1000f));
        Assert.Equal(Math.Log(1.0 / (1.0 + Math.Exp(-2.0))), merged[0], 5);
        Assert.Equal(Math.Log(1.0 / 3.0), merged[3], 5);
    }

    [Fact]
    public void WaveformNormalizationGivesZeroMeanAndUnitVariance()
    {
        short[] samples = [16384, -16384, 16384, -16384];

        var normalized = Wav2Vec2PhonemeRecognizer.NormalizeWaveform(samples);

        Assert.Equal(4, normalized.Length);
        Assert.Equal(0.0, normalized.Sum(), 5);
        Assert.Equal(1.0, normalized.Select(static v => (double)v * v).Average(), 5);
        Assert.True(normalized[0] > 0 && normalized[1] < 0);
    }

    [Fact]
    public void WaveformNormalizationRemovesTheDcOffsetAndSurvivesSilence()
    {
        var offset = Wav2Vec2PhonemeRecognizer.NormalizeWaveform([1000, 3000, 1000, 3000]);
        Assert.Equal(0.0, offset.Sum(), 5);

        var silence = Wav2Vec2PhonemeRecognizer.NormalizeWaveform(new short[8]);
        Assert.All(silence, value => Assert.Equal(0f, value));

        Assert.Throws<ArgumentException>(() => Wav2Vec2PhonemeRecognizer.NormalizeWaveform([]));
    }

    [Fact]
    public void ConstructorRejectsAMissingModelDirectory()
    {
        var missing = Path.Combine(Path.GetTempPath(), "slg-no-such-model-dir");

        Assert.Throws<FileNotFoundException>(() => new Wav2Vec2PhonemeRecognizer(missing));
        Assert.Throws<ArgumentException>(() => new Wav2Vec2PhonemeRecognizer(string.Empty));
    }

    [Fact]
    public void FrameLengthIsTwentyMilliseconds()
    {
        Assert.Equal(0.02, Wav2Vec2PhonemeRecognizer.FrameSeconds);
    }
}
