using System.Text;
using Vocaterra.Nlp.ContentBuilder;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>Tests for <c>import-lexicon --frequency --top --hand-authored --sidecar --sources</c> and for <c>lexicon-report</c>.</summary>
public class ProgramLexiconTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Program.Run(args, output, error);
        return (code, output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), error.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private string Kaikki => _directory.PathOf("kaikki-sample.jsonl");

    private string Frequency => _directory.PathOf("freq.txt");

    private string Prepare()
    {
        File.Copy(TestSupport.KaikkiFixture, Kaikki, overwrite: true);
        File.WriteAllText(Frequency, "espada 40\nespadas 20\ngato 30\nhola 10\ntengo 5\ncomprar 2\n");
        return TestSupport.WriteSources(_directory.Directory, ("kaikki-es", Kaikki), ("frequency-es-50k", Frequency));
    }

    // ---- import-lexicon -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TopKeepsTheMostFrequentLemmasAndThePrintedSummaryNamesTheFilter()
    {
        Prepare();
        var output = _directory.PathOf("out.json");

        var (code, text, error) = Run("import-lexicon", Kaikki, output, "--frequency", Frequency, "--top", "2");

        Assert.Equal(Program.ExitOk, code);
        Assert.Equal(string.Empty, error);
        Assert.Contains("lemmas: 13 considered, 5 with a frequency, 2 kept (top 2: 2 below the cutoff of 30,", text, StringComparison.Ordinal);
        Assert.Equal(["espada", "gato"], SpanishLexicon.Load(File.ReadAllText(output)).Lemmas);
    }

    [Fact]
    public void WithoutTopTheFrequencyListOnlyAddsTheCounts()
    {
        Prepare();
        var output = _directory.PathOf("out.json");

        var (code, text, _) = Run("import-lexicon", Kaikki, output, "--frequency", Frequency);

        Assert.Equal(Program.ExitOk, code);
        Assert.Contains("13 kept\n", text, StringComparison.Ordinal);
        Assert.Equal(13, SpanishLexicon.Load(File.ReadAllText(output)).Lemmas.Count);
    }

    [Fact]
    public void TheSidecarIsWrittenNextToTheLexiconAndTwoRunsGiveIdenticalFiles()
    {
        var sources = Prepare();
        string[] args = ["import-lexicon", Kaikki, _directory.PathOf("a.json"), "--frequency", Frequency, "--top", "3", "--sidecar", _directory.PathOf("a.source.json"), "--sources", sources];

        var first = Run(args);
        var second = Run([.. args[..2], _directory.PathOf("b.json"), .. args[3..8], _directory.PathOf("b.source.json"), .. args[9..]]);

        Assert.Equal(Program.ExitOk, first.Code);
        Assert.Equal(Program.ExitOk, second.Code);
        Assert.Contains($"wrote {_directory.PathOf("a.source.json")}\n", first.Output, StringComparison.Ordinal);
        Assert.Equal(File.ReadAllText(_directory.PathOf("a.json")), File.ReadAllText(_directory.PathOf("b.json")));
        var a = Vocaterra.Nlp.Content.LexiconSourceInfo.FromJson(File.ReadAllText(_directory.PathOf("a.source.json")));
        var b = Vocaterra.Nlp.Content.LexiconSourceInfo.FromJson(File.ReadAllText(_directory.PathOf("b.source.json")));
        Assert.Equal("a.json", a.GeneratedFile);
        Assert.Equal("b.json", b.GeneratedFile);
        Assert.Equal(a.Report.LemmasKept, b.Report.LemmasKept);
        Assert.Equal(a.Sources, b.Sources);
        Assert.Equal(3, a.Parameters.Top);
        Assert.Equal(a.OutputEntries, SpanishLexicon.Load(File.ReadAllText(_directory.PathOf("a.json"))).Entries.Count);
    }

    [Fact]
    public void TheSourcesFileIsFoundNextToTheInputByDefault()
    {
        Prepare();

        var (code, _, error) = Run("import-lexicon", Kaikki, _directory.PathOf("out.json"), "--frequency", Frequency, "--top", "3", "--sidecar", _directory.PathOf("out.source.json"));

        Assert.Equal(string.Empty, error);
        Assert.Equal(Program.ExitOk, code);
        Assert.True(File.Exists(_directory.PathOf("out.source.json")));
    }

    [Fact]
    public void ASourceFileThatDiffersFromItsRecordWritesNothing()
    {
        var sources = Prepare();
        File.AppendAllText(Kaikki, "\n");
        var output = _directory.PathOf("out.json");

        var (code, _, error) = Run("import-lexicon", Kaikki, output, "--sidecar", _directory.PathOf("out.source.json"), "--sources", sources);

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("fetch the pinned file again", error, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
        Assert.False(File.Exists(_directory.PathOf("out.source.json")));
    }

    [Fact]
    public void AMissingSourcesFileExitsWithOne()
    {
        Prepare();
        File.Delete(_directory.PathOf("sources.json"));

        var (code, _, error) = Run("import-lexicon", Kaikki, _directory.PathOf("out.json"), "--sidecar", _directory.PathOf("out.source.json"));

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("fetch-lexicon-sources.sh", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingFrequencyListExitsWithOne()
    {
        Prepare();

        var (code, _, error) = Run("import-lexicon", Kaikki, _directory.PathOf("out.json"), "--frequency", _directory.PathOf("nope.txt"));

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("Frequency list not found", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AMalformedFrequencyListExitsWithOne()
    {
        Prepare();
        File.WriteAllText(Frequency, "espada\n");

        var (code, _, error) = Run("import-lexicon", Kaikki, _directory.PathOf("out.json"), "--frequency", Frequency);

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("line 1", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--top", "5")]
    [InlineData("--sources", "x.json")]
    [InlineData("--frequency", "f.txt", "--top", "0")]
    [InlineData("--frequency", "f.txt", "--top", "many")]
    [InlineData("--frequency", "f.txt", "--top", "-3")]
    [InlineData("--hand-authored")]
    [InlineData("--unknown", "x")]
    public void WrongOptionsExitWithTwo(params string[] options)
    {
        var (code, output, error) = Run(["import-lexicon", "in.jsonl", "out.json", .. options]);

        Assert.Equal(Program.ExitUsage, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains("Usage:", error, StringComparison.Ordinal);
    }

    [Fact]
    public void HandAuthoredLexiconsKeepTheirLemmasOutOfTheImport()
    {
        Prepare();
        var hand = _directory.PathOf("hand.json");
        File.WriteAllText(hand, """{"schemaVersion":1,"generated_by":"hand-authored","entries":[{"form":"espada","lemma":"espada","pos":"noun","features":{"number":"sg","gender":"m"}}]}""", new UTF8Encoding(false));

        var (code, text, _) = Run("import-lexicon", Kaikki, _directory.PathOf("out.json"), "--hand-authored", hand);

        Assert.Equal(Program.ExitOk, code);
        Assert.Contains(KaikkiImporter.HandAuthoredReason, text, StringComparison.Ordinal);
        Assert.DoesNotContain("espada", SpanishLexicon.Load(File.ReadAllText(_directory.PathOf("out.json"))).Lemmas);
    }

    [Fact]
    public void AMissingHandAuthoredFileExitsWithOne()
    {
        Prepare();

        var (code, _, error) = Run("import-lexicon", Kaikki, _directory.PathOf("out.json"), "--hand-authored", _directory.PathOf("none.json"));

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("Hand-authored lexicon not found", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHelpMentionsTheNewOptionsAndCommand()
    {
        var (code, output, _) = Run("help");

        Assert.Equal(Program.ExitOk, code);
        foreach (var word in new[] { "--frequency", "--top", "--sidecar", "--sources", "--hand-authored", "lexicon-report" })
        {
            Assert.Contains(word, output, StringComparison.Ordinal);
        }
    }

    // ---- lexicon-report -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheReportOfTheSampleBundleNamesEveryLexiconFileAndTheirOverlap()
    {
        var (code, output, error) = Run("lexicon-report", TestSupport.BundleDir);

        Assert.Equal(Program.ExitOk, code);
        Assert.Equal(string.Empty, error);
        var files = Directory.EnumerateFiles(Path.Combine(TestSupport.BundleDir, "lexicon"), "*.json").Where(path => !path.EndsWith(".source.json", StringComparison.Ordinal)).ToArray();
        Assert.StartsWith($"{files.Length} lexicon files\n", output, StringComparison.Ordinal);
        foreach (var file in files.Select(Path.GetFileName))
        {
            var kind = file == "wiktionary-es.json" ? "wiktionary-import" : "hand-authored";
            Assert.Contains($"lexicon/{file}: generated_by {kind},", output, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("source.json:", output, StringComparison.Ordinal);
        Assert.Contains("overlap lexicon/seed.json / lexicon/wiktionary-es.json:", output, StringComparison.Ordinal);
        Assert.Contains("identical readings (stored once)", output, StringComparison.Ordinal);
        Assert.Contains("merged: ", output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReportCountsOverlapsOnSmallLexicons()
    {
        var lexicon = _directory.PathOf("lexicon");
        Directory.CreateDirectory(lexicon);
        File.WriteAllText(
            Path.Combine(lexicon, "a.json"),
            """{"schemaVersion":1,"entries":[{"form":"casa","lemma":"casa","pos":"noun","features":{"number":"sg","gender":"f"}},{"form":"compro","lemma":"comprar","pos":"verb","features":{"person":"1","number":"sg","tense":"pres","mood":"ind"}},{"form":"solo","lemma":"solo","pos":"adv","features":{}}]}""");
        File.WriteAllText(
            Path.Combine(lexicon, "b.json"),
            """{"schemaVersion":1,"entries":[{"form":"casa","lemma":"casa","pos":"noun","features":{"number":"sg","gender":"f"}},{"form":"casa","lemma":"casar","pos":"verb","features":{"person":"3","number":"sg","tense":"pres","mood":"ind"}},{"form":"compro","lemma":"comprar","pos":"verb","features":{"person":"1","number":"sg","tense":"pres","mood":"ind"}},{"form":"compro","lemma":"comprar","pos":"verb","features":{"person":"3","number":"sg","tense":"pres","mood":"subj"}}]}""");
        File.WriteAllText(Path.Combine(lexicon, "b.source.json"), "not a lexicon and not read");

        var report = LexiconReport.Build(_directory.Directory);

        Assert.Equal(["lexicon/a.json", "lexicon/b.json"], report.Files.Select(file => file.Path));
        var overlap = Assert.Single(report.Overlaps);
        Assert.Equal(2, overlap.SharedLemmas);
        Assert.Equal(2, overlap.SharedForms);
        Assert.Equal(2, overlap.IdenticalReadings);
        Assert.Equal(2, overlap.FormsWithDifferentReadings);
        Assert.Equal(5, report.MergedEntries);
        Assert.Equal(3, report.MergedForms);
        Assert.Equal(4, report.MergedLemmas);
        Assert.Equal(2, report.AmbiguousForms);
        Assert.Equal(1, report.HomographForms);
    }

    [Fact]
    public void AMissingBundleDirectoryExitsWithOne()
    {
        var (code, _, error) = Run("lexicon-report", _directory.PathOf("nope"));

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("Bundle directory not found", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ABundleWithoutALexiconFolderExitsWithOne()
    {
        var (code, _, error) = Run("lexicon-report", _directory.Directory);

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("No lexicon folder", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInvalidLexiconFileIsNamedInTheError()
    {
        var lexicon = _directory.PathOf("lexicon");
        Directory.CreateDirectory(lexicon);
        File.WriteAllText(Path.Combine(lexicon, "bad.json"), """{"schemaVersion":3,"entries":[]}""");

        var (code, _, error) = Run("lexicon-report", _directory.Directory);

        Assert.Equal(Program.ExitInvalid, code);
        Assert.StartsWith("lexicon/bad.json:", error, StringComparison.Ordinal);
    }

    [Fact]
    public void LexiconReportNeedsExactlyOneArgument()
    {
        var (code, _, error) = Run("lexicon-report");

        Assert.Equal(Program.ExitUsage, code);
        Assert.Contains("Usage:", error, StringComparison.Ordinal);
    }
}
