using System.Text.Json.Nodes;
using Vocaterra.Nlp.ContentBuilder;
using Vocaterra.Nlp.Language.Lexicon;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>Tests for the ContentBuilder command line through <see cref="Program.Run(string[], TextWriter, TextWriter)"/>.</summary>
public class ProgramTests
{
    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Program.Run(args, output, error);
        return (code, output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), error.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateAcceptsTheSampleBundle()
    {
        var (code, output, error) = Run("validate", TestSupport.BundleDir);

        Assert.Equal(Program.ExitOk, code);
        Assert.Equal(string.Empty, error);
        var manifest = TestSupport.Bundle.Manifest;
        Assert.StartsWith($"OK: bundle '{manifest.BundleId}' {manifest.ContentVersion}, {manifest.Files.Count} files, hash {manifest.BundleHash}\n", output, StringComparison.Ordinal);
        Assert.Contains("\nlint: 0 errors, ", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateExitsWithOneAndListsTheProblems()
    {
        using var bundle = new TempBundle();
        bundle.Touch("prompts/prompts.json");
        bundle.Delete("effects/effects.json");

        var (code, output, error) = Run("validate", bundle.Directory);

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains("prompts/prompts.json: hash mismatch", error, StringComparison.Ordinal);
        Assert.Contains("effects/effects.json: file is missing", error, StringComparison.Ordinal);
        Assert.StartsWith("Content bundle is invalid (", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateExitsWithOneForAMissingDirectory()
    {
        var (code, _, error) = Run("validate", Path.Combine(Path.GetTempPath(), "vocaterra-cli-missing-" + Guid.NewGuid().ToString("N")));

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("does not exist", error, StringComparison.Ordinal);
    }

    public static TheoryData<string> WrongCommandLines()
    {
        // Each entry is one command line, words separated by a single space (no test argument contains a space).
        var lines = new[]
        {
            string.Empty,
            "frobnicate",
            "validate",
            "validate a b",
            "validate a --verbose",
            "validate a --strict --strict",
            "hash",
            "hash a --bundle-id",
            "hash a --bundle-id x --bundle-id y",
            "cefr-check a",
            "cefr-check a --level b2",
            "cefr-check --level a1",
            "import-lexicon only-one",
            "import-lexicon in out --max many",
            "import-lexicon in out --max 0",
            "import-lexicon in out --max=-3",
        };
        var data = new TheoryData<string>();
        foreach (var line in lines)
        {
            data.Add(line);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(WrongCommandLines))]
    public void WrongCommandLinesExitWithTwoAndPrintTheUsage(string commandLine)
    {
        var args = commandLine.Length == 0 ? [] : commandLine.Split(' ');

        var (code, output, error) = Run(args);

        Assert.Equal(Program.ExitUsage, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains("Usage:", error, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpPrintsTheUsageAndSucceeds()
    {
        var (code, output, error) = Run("help");

        Assert.Equal(Program.ExitOk, code);
        Assert.Equal(string.Empty, error);
        foreach (var command in new[] { "validate", "hash", "cefr-check", "import-lexicon" })
        {
            Assert.Contains(command, output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void HashRewritesTheManifestFromTheFilesOnDisk()
    {
        using var bundle = new TempBundle();
        var before = ContentManifest.FromJson(bundle.Read("manifest.json")).BundleHash;
        bundle.Touch("effects/effects.json");
        bundle.Write("extra/notes.txt", "new file");
        Assert.Equal(Program.ExitInvalid, Run("validate", bundle.Directory).Code);

        var (code, output, error) = Run("hash", bundle.Directory);

        Assert.Equal(Program.ExitOk, code);
        Assert.Equal(string.Empty, error);
        var rewritten = ContentManifest.FromJson(File.ReadAllText(bundle.PathOf("manifest.json")));
        Assert.Equal($"wrote manifest.json: {rewritten.Files.Count} files, hash {rewritten.BundleHash}\n", output);
        Assert.Contains(rewritten.Files, file => file.Path == "extra/notes.txt");
        Assert.NotEqual(before, rewritten.BundleHash);
        Assert.Equal(rewritten, ContentManifest.Compute(bundle.Directory));
        Assert.Equal(rewritten.ToJson(), bundle.Read("manifest.json"));
    }

    [Fact]
    public void HashIsIdempotentAndKeepsTheIdentity()
    {
        using var bundle = new TempBundle();
        var before = bundle.Read("manifest.json");

        Assert.Equal(Program.ExitOk, Run("hash", bundle.Directory).Code);
        Assert.Equal(before, bundle.Read("manifest.json"));
        Assert.Equal(Program.ExitOk, Run("validate", bundle.Directory).Code);
    }

    [Fact]
    public void HashAcceptsAnIdentityOnTheCommandLine()
    {
        using var bundle = new TempBundle(full: true);
        var expectedHash = ContentManifest.Compute(bundle.Directory).BundleHash;

        var (code, _, _) = Run("hash", bundle.Directory, "--bundle-id", "spanish", "--content-version=3.1.4");

        Assert.Equal(Program.ExitOk, code);
        var manifest = ContentManifest.FromJson(bundle.Read("manifest.json"));
        Assert.Equal("spanish", manifest.BundleId);
        Assert.Equal("3.1.4", manifest.ContentVersion);
        Assert.Equal(expectedHash, manifest.BundleHash);
    }

    [Fact]
    public void HashExitsWithOneForAMissingDirectory()
    {
        var (code, _, error) = Run("hash", Path.Combine(Path.GetTempPath(), "vocaterra-cli-missing-" + Guid.NewGuid().ToString("N")));

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("Bundle directory not found", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a1")]
    [InlineData("a2")]
    [InlineData("A2")]
    public void CefrCheckPrintsCountsAndExitsWithZero(string level)
    {
        var (code, output, error) = Run("cefr-check", TestSupport.BundleDir, "--level", level);

        Assert.Equal(Program.ExitOk, code);
        Assert.Equal(string.Empty, error);
        Assert.StartsWith($"cefr {level.ToLowerInvariant()}: ", output, StringComparison.Ordinal);
        Assert.Contains("lemmas in dialogue templates", output, StringComparison.Ordinal);
        Assert.Equal(CefrChecker.Format(CefrChecker.Check(TestSupport.Bundle, level)), output);
    }

    [Fact]
    public void CefrCheckListsTheMissingLemmasAndStillSucceeds()
    {
        using var bundle = new TempBundle();
        bundle.Write("cefr/a1.txt", "hola\n");

        var (code, output, _) = Run("cefr-check", bundle.Directory, "--level=a1");

        Assert.Equal(Program.ExitOk, code);
        Assert.Contains("farewell_reply.1", output, StringComparison.Ordinal);
        Assert.Contains("not in the list", output, StringComparison.Ordinal);
        var loaded = ContentLoader.Load(bundle.Directory, new ContentLoadOptions { VerifyHashes = false });
        Assert.Equal(CefrChecker.Format(CefrChecker.Check(loaded, "a1")), output);
    }

    [Fact]
    public void CefrCheckReportsPromptTextWithItsPromptId()
    {
        using var bundle = new TempBundle();
        bundle.EditJson("prompts/prompts.json", root => root.AsArray().Single(prompt => prompt!["promptId"]!.GetValue<string>() == "guide-offer")!["text"] = "¿Quieres agua? Tengo una zzxq.");

        var (code, output, error) = Run("cefr-check", bundle.Directory, "--level", "a1");

        Assert.Equal(Program.ExitOk, code);
        Assert.Equal(string.Empty, error);
        Assert.Contains("lemmas in prompt text, ", output, StringComparison.Ordinal);
        Assert.Contains("  zzxq (not in lexicon): guide-offer\n", output, StringComparison.Ordinal);
        var loaded = ContentLoader.Load(bundle.Directory, new ContentLoadOptions { VerifyHashes = false });
        Assert.Equal(CefrChecker.Format(CefrChecker.Check(loaded, "a1")), output);
    }

    [Fact]
    public void CefrCheckExitsWithOneWhenTheBundleCannotBeLoaded()
    {
        using var bundle = new TempBundle();
        bundle.Delete("dialogue/dialogue.json");

        var (code, output, error) = Run("cefr-check", bundle.Directory, "--level", "a1");

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains("dialogue/dialogue.json: required file is missing.", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportLexiconWritesALexiconFileAndPrintsTheCounts()
    {
        using var bundle = new TempBundle();
        var outPath = bundle.PathOf("imported.json");

        var (code, output, error) = Run("import-lexicon", TestSupport.KaikkiFixture, outPath);

        Assert.Equal(Program.ExitOk, code);
        Assert.Equal(string.Empty, error);
        Assert.StartsWith("read 20 lines, 19 Spanish entries: 13 imported into 41 lexicon entries, 6 skipped\n", output, StringComparison.Ordinal);
        Assert.EndsWith($"wrote {outPath}\n", output, StringComparison.Ordinal);
        var lexicon = NlpLexicon.Load(File.ReadAllText(outPath));
        Assert.Equal("wiktionary-import", lexicon.GeneratedBy);
        Assert.Equal(41, lexicon.Entries.Count);
        Assert.True(lexicon.ContainsLemma("espada"));
        Assert.Equal(KaikkiImporter.ToJson(lexicon.Entries), File.ReadAllText(outPath).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void ImportLexiconHonoursMax()
    {
        using var bundle = new TempBundle();
        var outPath = bundle.PathOf("small.json");

        var (code, output, _) = Run("import-lexicon", TestSupport.KaikkiFixture, outPath, "--max", "2");

        Assert.Equal(Program.ExitOk, code);
        Assert.StartsWith("read 3 lines, 2 Spanish entries: 2 imported into 11 lexicon entries, 0 skipped\n", output, StringComparison.Ordinal);
        Assert.Equal(11, NlpLexicon.Load(File.ReadAllText(outPath)).Entries.Count);
    }

    [Fact]
    public void ImportLexiconExitsWithOneForAMissingInput()
    {
        using var bundle = new TempBundle();

        var (code, _, error) = Run("import-lexicon", bundle.PathOf("nope.jsonl"), bundle.PathOf("out.json"));

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("Input file not found", error, StringComparison.Ordinal);
        Assert.False(File.Exists(bundle.PathOf("out.json")));
    }

    [Fact]
    public void ImportLexiconExitsWithOneForAMalformedLine()
    {
        using var bundle = new TempBundle();
        bundle.Write("bad.jsonl", "{\"word\":\"hola\",\"lang_code\":\"es\",\"pos\":\"intj\"}\nnot json\n");

        var (code, _, error) = Run("import-lexicon", bundle.PathOf("bad.jsonl"), bundle.PathOf("out.json"));

        Assert.Equal(Program.ExitInvalid, code);
        Assert.StartsWith("Line 2: invalid JSON", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AnImportedLexiconCanJoinTheBundleAfterHashing()
    {
        using var bundle = new TempBundle(full: true);
        var sourcesDirectory = Path.Combine(Path.GetTempPath(), "vocaterra-sources-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sources = TestSupport.WriteSources(sourcesDirectory, ("kaikki-es", TestSupport.KaikkiFixture));
            var imported = Run("import-lexicon", TestSupport.KaikkiFixture, bundle.PathOf("lexicon/wiktionary.json"), "--sidecar", bundle.PathOf("lexicon/wiktionary.source.json"), "--sources", sources);
            Assert.Equal(Program.ExitOk, imported.Code);
        }
        finally
        {
            Directory.Delete(sourcesDirectory, recursive: true);
        }

        bundle.Edit(
            "provenance.json",
            "{ \"path\": \"lexicon/seed.json\", ",
            "{ \"path\": \"lexicon/wiktionary.json\", \"generatedBy\": \"wiktionary-import\", \"approvedBy\": null, \"approvedOn\": null },\n    { \"path\": \"lexicon/wiktionary.source.json\", \"generatedBy\": \"wiktionary-import\", \"approvedBy\": null, \"approvedOn\": null },\n    { \"path\": \"lexicon/seed.json\", ");

        Assert.Equal(Program.ExitOk, Run("hash", bundle.Directory).Code);
        var (code, _, error) = Run("validate", bundle.Directory);

        Assert.Equal(string.Empty, error);
        Assert.Equal(Program.ExitOk, code);
        var loaded = ContentLoader.Load(bundle.Directory);
        Assert.True(loaded.Lexicon.Entries.Count >= TestSupport.Bundle.Lexicon.Entries.Count);
        Assert.Contains(loaded.Provenance.Items, item => item.GeneratedBy == GeneratedByKinds.WiktionaryImport && item.Path == "lexicon/wiktionary.json");
        Assert.Contains("lexicon/wiktionary.source.json", loaded.LexiconSources.Keys);
    }

    [Fact]
    public void TheLexiconEntryTypeRoundTripsThroughTheImporterOutput()
    {
        var entries = KaikkiImporter.ImportFile(TestSupport.KaikkiFixture).Entries;

        var reloaded = NlpLexicon.Load(KaikkiImporter.ToJson(entries)).Entries;

        Assert.All(reloaded, entry => Assert.IsType<LexiconEntry>(entry));
        Assert.Equal(entries, reloaded);
    }
}
