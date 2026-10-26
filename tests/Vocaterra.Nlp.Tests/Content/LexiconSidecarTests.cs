using System.Text.Json;
using Vocaterra.Nlp.ContentBuilder;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>Tests for the sidecar of a generated lexicon: what the builder records, how it is written, and what the loader checks.</summary>
public class LexiconSidecarTests : IDisposable
{
    private const string Frequency = "espada 40\nespadas 20\ngato 30\nhola 10\ntengo 5\ncomprar 2\n";

    private readonly TempDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    private string KaikkiPath => _directory.PathOf("kaikki-sample.jsonl");

    private string FrequencyPath => _directory.PathOf("freq.txt");

    private string SourcesPath => _directory.PathOf("sources.json");

    private (KaikkiImportResult Result, KaikkiImportOptions Options, FrequencyList Frequency) Run(int? top = 3)
    {
        File.Copy(TestSupport.KaikkiFixture, KaikkiPath, overwrite: true);
        File.WriteAllText(FrequencyPath, Frequency);
        TestSupport.WriteSources(_directory.Directory, ("kaikki-es", KaikkiPath), ("frequency-es-50k", FrequencyPath));
        var list = FrequencyList.ReadFile(FrequencyPath);
        var options = new KaikkiImportOptions { Frequency = list, Top = top };
        return (KaikkiImporter.ImportFile(KaikkiPath, options), options, list);
    }

    private LexiconSourceInfo Build(int? top = 3, string generatedFile = "wiktionary-es.json", IReadOnlyList<string>? handAuthored = null)
    {
        var (result, options, list) = Run(top);
        return LexiconSidecarBuilder.Build(result, options, KaikkiPath, (list, FrequencyPath), SourcesFile.Read(SourcesPath), generatedFile, handAuthored);
    }

    // ---- The builder ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheSourceRecordsAreCopiedAndTheFileFactsComeFromTheFilesThatWereRead()
    {
        var info = Build();

        Assert.Equal(LexiconSourceInfo.CurrentSchemaVersion, info.SchemaVersion);
        Assert.Equal(KaikkiImporter.ImporterVersion, info.ImporterVersion);
        Assert.Equal("wiktionary-es.json", info.GeneratedFile);
        Assert.Equal(["frequency-es-50k", "kaikki-es"], info.Sources.Select(source => source.Id));
        var kaikki = info.Sources.Single(source => source.Id == "kaikki-es");
        Assert.Equal("https://example.org/data/kaikki-sample.jsonl", kaikki.Url);
        Assert.Equal("2026-10-20", kaikki.RetrievedOn);
        Assert.Equal("CC-BY-SA-4.0 (test data)", kaikki.License);
        Assert.Equal(new FileInfo(KaikkiPath).Length, kaikki.Bytes);
        Assert.Equal(20, kaikki.Lines);
        Assert.Equal(64, kaikki.Sha256.Length);
        var frequency = info.Sources.Single(source => source.Id == "frequency-es-50k");
        Assert.Equal(6, frequency.Lines);
    }

    [Fact]
    public void TheParametersAndTheCountsDescribeTheImport()
    {
        var info = Build(top: 3, handAuthored: ["game.json", "seed.json"]);

        Assert.Equal(3, info.Parameters.Top);
        Assert.Null(info.Parameters.MaxEntries);
        Assert.Equal("frequency-es-50k", info.Parameters.FrequencySourceId);
        Assert.Equal(["game.json", "seed.json"], info.Parameters.HandAuthoredFiles);
        Assert.Equal(20, info.Report.KaikkiLinesRead);
        Assert.Equal(19, info.Report.SpanishEntries);
        Assert.Equal(13, info.Report.ImportedEntries);
        Assert.Equal(6, info.Report.SkippedEntries);
        Assert.Equal(info.Report.SkippedByReason.Values.Sum(), (int)info.Report.SkippedEntries);
        Assert.Equal(6, info.Report.FrequencyWords);

        // Of the 13 lemmas in the fixture five have a frequency (espada 60, gato 30, hola 10, comprar 2 and tener 5 by its form tengo); tener is not
        // eligible because its own head word is not in the list, and comprar falls below the cut.
        Assert.Equal(13, info.Report.LemmasConsidered);
        Assert.Equal(5, info.Report.LemmasWithFrequency);
        Assert.Equal(3, info.Report.LemmasKept);
        Assert.Equal(8, info.Report.LemmasDroppedNoFrequency);
        Assert.Equal(1, info.Report.LemmasDroppedUnattestedHeadWord);
        Assert.Equal(1, info.Report.LemmasDroppedBelowCutoff);
        Assert.Equal(10, info.Report.CutoffFrequency);
        Assert.Equal(7, info.OutputEntries);
        Assert.Equal(3, info.OutputLemmas);
        Assert.Equal(new Dictionary<string, int> { ["interj"] = 1, ["noun"] = 6 }, info.Report.EntriesByPartOfSpeech.ToDictionary(pair => pair.Key, pair => pair.Value));
        Assert.Equal(new Dictionary<string, int> { ["interj"] = 1, ["noun"] = 2 }, info.Report.LemmasByPartOfSpeech.ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    [Fact]
    public void AnImportWithoutAFrequencyListRecordsOnlyTheWiktionaryExtract()
    {
        File.Copy(TestSupport.KaikkiFixture, KaikkiPath, overwrite: true);
        TestSupport.WriteSources(_directory.Directory, ("kaikki-es", KaikkiPath));
        var options = new KaikkiImportOptions();
        var result = KaikkiImporter.ImportFile(KaikkiPath, options);

        var info = LexiconSidecarBuilder.Build(result, options, KaikkiPath, null, SourcesFile.Read(SourcesPath), "out.json");

        Assert.Equal(["kaikki-es"], info.Sources.Select(source => source.Id));
        Assert.Null(info.Parameters.Top);
        Assert.Null(info.Parameters.FrequencySourceId);
        Assert.Equal(0, info.Report.LemmasKept);
        Assert.Equal(result.Entries.Count, info.OutputEntries);
    }

    [Fact]
    public void AFileThatIsNotTheRecordedOneIsRefused()
    {
        var (result, options, list) = Run();
        File.AppendAllText(KaikkiPath, "\n{\"word\":\"nuevo\",\"lang_code\":\"es\",\"pos\":\"adv\"}\n");

        var changed = Assert.Throws<FormatException>(() => LexiconSidecarBuilder.Build(result, options, KaikkiPath, (list, FrequencyPath), SourcesFile.Read(SourcesPath), "out.json"));

        Assert.Contains("bytes", changed.Message, StringComparison.Ordinal);
        Assert.Contains("kaikki-sample.jsonl", changed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASameSizedFileWithOtherContentIsRefusedByItsHash()
    {
        var (result, options, list) = Run();
        var text = File.ReadAllText(KaikkiPath);
        File.WriteAllText(KaikkiPath, text.Replace("espada", "espadX", StringComparison.Ordinal));

        var changed = Assert.Throws<FormatException>(() => LexiconSidecarBuilder.Build(result, options, KaikkiPath, (list, FrequencyPath), SourcesFile.Read(SourcesPath), "out.json"));

        Assert.Contains("sha256", changed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileWithoutARecordIsRefused()
    {
        var (result, options, list) = Run();
        var elsewhere = _directory.PathOf("other.jsonl");
        File.Copy(KaikkiPath, elsewhere);

        var missing = Assert.Throws<FormatException>(() => LexiconSidecarBuilder.Build(result, options, elsewhere, (list, FrequencyPath), SourcesFile.Read(SourcesPath), "out.json"));

        Assert.Contains("no record for 'other.jsonl'", missing.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"sources":{}}""")]
    [InlineData("""{"sources":[{"id":"a"}]}""")]
    [InlineData("""{"sources":[{"id":"a","file":"f","url":"u","sha256":"s","bytes":"many","retrievedOn":"d","license":"l"}]}""")]
    [InlineData("not json")]
    public void ADamagedSourcesFileIsAFormatException(string json)
    {
        File.WriteAllText(SourcesPath, json);

        Assert.Throws<FormatException>(() => SourcesFile.Read(SourcesPath));
    }

    [Fact]
    public void AMissingSourcesFileMentionsTheFetchScript()
    {
        var exception = Assert.Throws<FileNotFoundException>(() => SourcesFile.Read(_directory.PathOf("none.json")));

        Assert.Contains("fetch-lexicon-sources.sh", exception.Message, StringComparison.Ordinal);
    }

    // ---- The text -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheJsonIsIndentedAndCanonicalAndEndsWithAnEmptyLine()
    {
        var json = Build().ToJson();

        Assert.StartsWith("{\n  \"schemaVersion\": 1,\n  \"importerVersion\": \"" + KaikkiImporter.ImporterVersion + "\",\n  \"generatedFile\": \"wiktionary-es.json\",\n", json, StringComparison.Ordinal);
        Assert.EndsWith("\n}\n", json, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', json);
        Assert.DoesNotContain('\t', json);
    }

    [Fact]
    public void TheMapsAreWrittenInOrdinalKeyOrder()
    {
        using var document = JsonDocument.Parse(Build().ToJson());

        var reasons = document.RootElement.GetProperty("report").GetProperty("skippedByReason").EnumerateObject().Select(property => property.Name).ToArray();
        var byPos = document.RootElement.GetProperty("report").GetProperty("entriesByPartOfSpeech").EnumerateObject().Select(property => property.Name).ToArray();

        Assert.NotEmpty(reasons);
        Assert.Equal(reasons.Order(StringComparer.Ordinal), reasons);
        Assert.Equal(byPos.Order(StringComparer.Ordinal), byPos);
    }

    [Fact]
    public void TheSameImportWritesTheSameBytes()
    {
        Assert.Equal(Build().ToJson(), Build().ToJson());
    }

    [Fact]
    public void TheJsonReadsBackToTheSameSidecar()
    {
        var info = Build(handAuthored: ["seed.json"]);

        var reloaded = LexiconSourceInfo.FromJson(info.ToJson());

        Assert.Equal(info.ToJson(), reloaded.ToJson());
        Assert.Equal(info.Sources, reloaded.Sources);
        Assert.Equal(info.Parameters.HandAuthoredFiles, reloaded.Parameters.HandAuthoredFiles);
        Assert.Equal(info.OutputEntries, reloaded.OutputEntries);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"schemaVersion":1,"sources":[],"parameters":{},"report":{}}""")]
    public void AnIncompleteSidecarIsRejected(string json)
    {
        Assert.ThrowsAny<Exception>(() => LexiconSourceInfo.FromJson(json));
    }

    // ---- What the loader checks -----------------------------------------------------------------------------------------------------

    [Fact]
    public void ASoundSidecarHasNoProblemsAndTheCountsAreChecked()
    {
        var info = Build();

        Assert.Empty(info.Problems(info.OutputEntries, info.OutputLemmas));
        Assert.Empty(info.Problems(null, null));
        Assert.Contains("outputEntries is", Assert.Single(info.Problems(info.OutputEntries + 1, info.OutputLemmas)), StringComparison.Ordinal);
        Assert.Contains("outputLemmas is", Assert.Single(info.Problems(info.OutputEntries, info.OutputLemmas + 1)), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryDamagedFieldIsNamed()
    {
        var info = Build();
        var source = info.Sources[0];

        string Only(LexiconSourceInfo changed)
        {
            return Assert.Single(changed.Problems(null, null));
        }

        Assert.Contains("schemaVersion", Only(info with { SchemaVersion = 2 }), StringComparison.Ordinal);
        Assert.Contains("importerVersion", Only(info with { ImporterVersion = " " }), StringComparison.Ordinal);
        Assert.Contains("generatedFile", Only(info with { GeneratedFile = string.Empty }), StringComparison.Ordinal);
        Assert.Contains("outputEntries", Only(info with { OutputEntries = null }), StringComparison.Ordinal);
        Assert.Contains("outputLemmas", Only(info with { OutputLemmas = -1 }), StringComparison.Ordinal);
        Assert.Contains("sources must list", Only(info with { Sources = [] }), StringComparison.Ordinal);
        Assert.Contains("sources[0]: url", Only(info with { Sources = [source with { Url = "http://example.org/x" }, .. info.Sources.Skip(1)] }), StringComparison.Ordinal);
        Assert.Contains("sources[0]: sha256", Only(info with { Sources = [source with { Sha256 = new string('A', 64) }, .. info.Sources.Skip(1)] }), StringComparison.Ordinal);
        Assert.Contains("sources[0]: sha256", Only(info with { Sources = [source with { Sha256 = "abc" }, .. info.Sources.Skip(1)] }), StringComparison.Ordinal);
        Assert.Contains("sources[0]: bytes", Only(info with { Sources = [source with { Bytes = null }, .. info.Sources.Skip(1)] }), StringComparison.Ordinal);
        Assert.Contains("sources[0]: lines", Only(info with { Sources = [source with { Lines = -5 }, .. info.Sources.Skip(1)] }), StringComparison.Ordinal);
        Assert.Contains("sources[0]: retrievedOn", Only(info with { Sources = [source with { RetrievedOn = "20/10/2026" }, .. info.Sources.Skip(1)] }), StringComparison.Ordinal);
        Assert.Contains("sources[0]: license", Only(info with { Sources = [source with { License = string.Empty }, .. info.Sources.Skip(1)] }), StringComparison.Ordinal);
        Assert.Contains("sources[0]: id", Only(info with { Sources = [source with { Id = string.Empty }, .. info.Sources.Skip(1)] }), StringComparison.Ordinal);
        Assert.Contains("listed twice", Only(info with { Sources = [source, source with { File = "other" }] }), StringComparison.Ordinal);
    }

    [Fact]
    public void SidecarNamesFollowTheLexiconFileName()
    {
        Assert.Equal("wiktionary-es.source.json", LexiconSourceInfo.SidecarNameFor("wiktionary-es.json"));
        Assert.True(LexiconSourceInfo.IsSidecarPath("lexicon/wiktionary-es.source.json"));
        Assert.True(LexiconSourceInfo.IsSidecarPath("lexicon/Other.SOURCE.JSON"));
        Assert.False(LexiconSourceInfo.IsSidecarPath("lexicon/seed.json"));
        Assert.False(LexiconSourceInfo.IsSidecarPath("answer-sets/x.source.json"));
    }

    [Fact]
    public void SortedCountsAreOrdinal()
    {
        var counts = LexiconSourceInfo.SortedCounts([new("b", 2), new("A", 1), new("a", 3)]);

        Assert.Equal(["A", "a", "b"], counts.Keys);
    }
}
