using Vocaterra.Nlp.Language.Lexicon;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>
/// How <see cref="ContentLoader"/> turns the files of <c>lexicon/</c> into one lexicon: the merge policy, the sidecar of a generated file and
/// the checks that tie the generated file, its sidecar and <c>provenance.json</c> together.
/// </summary>
public class LexiconLoadingTests
{
    private const string SidecarPath = "lexicon/wiktionary-es.source.json";
    private const string GeneratedPath = "lexicon/wiktionary-es.json";

    private static string Extra(params string[] entries)
    {
        return "{\"schemaVersion\":1,\"generated_by\":\"hand-authored\",\"entries\":[" + string.Join(',', entries) + "]}";
    }

    private static void AddExtraLexicon(TempBundle bundle, string name, string json)
    {
        bundle.Write("lexicon/" + name, json);
        bundle.Edit(
            "provenance.json",
            "{ \"path\": \"lexicon/game.json\", ",
            "{ \"path\": \"lexicon/" + name + "\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null },\n    { \"path\": \"lexicon/game.json\", ");
    }

    private static ContentValidationException LoadFails(TempBundle bundle)
    {
        return Assert.Throws<ContentValidationException>(() => SpanishContent.Load(bundle.Directory));
    }

    // ---- The merge policy -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void AReadingThatTwoFilesShareIsStoredOnce()
    {
        using var bundle = new TempBundle(full: true);
        var seedHas = TestSupport.Bundle.Lexicon.Lookup("espada").Count;
        AddExtraLexicon(bundle, "dup.json", Extra("{\"form\":\"espada\",\"lemma\":\"espada\",\"pos\":\"noun\",\"features\":{\"number\":\"sg\",\"gender\":\"f\"}}"));
        bundle.Rehash();

        var loaded = SpanishContent.Load(bundle.Directory);

        Assert.Equal(seedHas, loaded.Lexicon.Lookup("espada").Count);
        Assert.Equal(TestSupport.Bundle.Lexicon.Entries.Count, loaded.Lexicon.Entries.Count);
    }

    [Fact]
    public void DifferentReadingsOfOneFormFromDifferentFilesAllStay()
    {
        using var bundle = new TempBundle(full: true);
        AddExtraLexicon(
            bundle,
            "other.json",
            Extra("{\"form\":\"espada\",\"lemma\":\"espada\",\"pos\":\"noun\",\"features\":{\"number\":\"sg\",\"gender\":\"m\"}}", "{\"form\":\"espada\",\"lemma\":\"espadar\",\"pos\":\"verb\",\"features\":{\"person\":\"3\",\"number\":\"sg\",\"tense\":\"pres\",\"mood\":\"ind\"}}"));
        bundle.Rehash();

        var readings = SpanishContent.Load(bundle.Directory).Lexicon.Lookup("espada");

        Assert.Equal(TestSupport.Bundle.Lexicon.Lookup("espada").Count + 2, readings.Count);
        Assert.Contains(readings, reading => reading.Pos == "noun" && reading.Gender == "f");
        Assert.Contains(readings, reading => reading.Pos == "noun" && reading.Gender == "m");
        Assert.Contains(readings, reading => reading.Pos == "verb" && reading.Lemma == "espadar");
    }

    [Fact]
    public void TheBundleLexiconIsTheUnionOfEveryFile()
    {
        var all = new List<LexiconEntry>();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(TestSupport.BundleDir, "lexicon"), "*.json"))
        {
            if (!path.EndsWith(".source.json", StringComparison.Ordinal))
            {
                all.AddRange(SpanishLexicon.Load(File.ReadAllText(path)).Entries);
            }
        }

        var union = SpanishLexicon.FromEntries(all);

        Assert.Equal(union.Entries, TestSupport.Bundle.Lexicon.Entries);
        Assert.Equal(all.Distinct().Count(), TestSupport.Bundle.Lexicon.Entries.Count);
    }

    [Fact]
    public void TheMergeDoesNotDependOnTheFileNames()
    {
        using var first = new TempBundle();
        using var second = new TempBundle();
        const string Entry = "{\"form\":\"yelmo\",\"lemma\":\"yelmo\",\"pos\":\"noun\",\"features\":{\"number\":\"sg\",\"gender\":\"m\"}}";
        AddExtraLexicon(first, "a-first.json", Extra(Entry));
        AddExtraLexicon(second, "z-last.json", Extra(Entry));
        first.Rehash();
        second.Rehash();

        Assert.Equal(SpanishContent.Load(first.Directory).Lexicon.Entries, SpanishContent.Load(second.Directory).Lexicon.Entries);
    }

    // ---- Sidecars -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheSidecarIsNotALexiconAndIsExposedOnTheBundle()
    {
        var sidecar = Assert.Single(TestSupport.Bundle.LexiconSources);

        Assert.Equal(SidecarPath, sidecar.Key);
        Assert.Equal("wiktionary-es.json", sidecar.Value.GeneratedFile);
        Assert.DoesNotContain(TestSupport.Bundle.Lexicon.Entries, entry => entry.Form.Contains("importerVersion", StringComparison.Ordinal));
    }

    [Fact]
    public void AnEntryCountThatDoesNotMatchTheGeneratedFileIsReported()
    {
        using var bundle = new TempBundle(full: true);
        var count = TestSupport.Bundle.LexiconSources[SidecarPath].OutputEntries;
        bundle.Edit(SidecarPath, $"\"outputEntries\": {count},", $"\"outputEntries\": {count + 1},");
        bundle.Rehash();

        var problem = Assert.Single(LoadFails(bundle).Problems);

        Assert.StartsWith(SidecarPath + ": outputEntries is", problem, StringComparison.Ordinal);
        Assert.Contains("regenerate the file and its sidecar together", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ALemmaCountThatDoesNotMatchIsReported()
    {
        using var bundle = new TempBundle(full: true);
        var count = TestSupport.Bundle.LexiconSources[SidecarPath].OutputLemmas;
        bundle.Edit(SidecarPath, $"\"outputLemmas\": {count},", $"\"outputLemmas\": {count - 1},");
        bundle.Rehash();

        Assert.StartsWith(SidecarPath + ": outputLemmas is", Assert.Single(LoadFails(bundle).Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void ASidecarThatNamesAnotherFileIsReported()
    {
        using var bundle = new TempBundle(full: true);
        bundle.Edit(SidecarPath, "\"generatedFile\": \"wiktionary-es.json\"", "\"generatedFile\": \"nowhere.json\"");
        bundle.Rehash();

        var problems = LoadFails(bundle).Problems;

        Assert.Contains(problems, problem => problem.StartsWith(SidecarPath + ": generatedFile 'nowhere.json' is not a lexicon file", StringComparison.Ordinal));
    }

    [Fact]
    public void ASidecarMustBeNamedAfterItsLexiconFile()
    {
        using var bundle = new TempBundle(full: true);
        bundle.Edit(SidecarPath, "\"generatedFile\": \"wiktionary-es.json\"", "\"generatedFile\": \"seed.json\"");
        bundle.Rehash();

        var problems = LoadFails(bundle).Problems;

        Assert.Contains(problems, problem => problem.StartsWith(SidecarPath + ": a sidecar must be named 'seed.source.json'", StringComparison.Ordinal));
    }

    [Fact]
    public void AnImportedLexiconWithoutItsSidecarIsReported()
    {
        using var bundle = new TempBundle(full: true);
        bundle.Delete(SidecarPath);
        bundle.Edit("provenance.json", "    { \"path\": \"" + SidecarPath + "\", \"generatedBy\": \"wiktionary-import\", \"approvedBy\": null, \"approvedOn\": null },\n", string.Empty);
        bundle.Rehash();

        var problem = Assert.Single(LoadFails(bundle).Problems);

        Assert.StartsWith(GeneratedPath + ": an imported lexicon needs its sidecar 'wiktionary-es.source.json'", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void TheImportMarkerInTheFileAndInTheProvenanceMustAgree()
    {
        using var asHandAuthored = new TempBundle(full: true);
        asHandAuthored.Edit("provenance.json", "{ \"path\": \"" + GeneratedPath + "\", \"generatedBy\": \"wiktionary-import\"", "{ \"path\": \"" + GeneratedPath + "\", \"generatedBy\": \"hand-authored\"");
        asHandAuthored.Rehash();
        using var withoutMarker = new TempBundle(full: true);
        withoutMarker.Edit(GeneratedPath, "\"generated_by\": \"wiktionary-import\"", "\"generated_by\": \"hand-authored\"");
        withoutMarker.Rehash();

        var first = Assert.Single(LoadFails(asHandAuthored).Problems);
        var second = Assert.Single(LoadFails(withoutMarker).Problems);

        Assert.StartsWith(GeneratedPath + ": generated_by is 'wiktionary-import' but provenance.json says 'hand-authored'", first, StringComparison.Ordinal);
        Assert.StartsWith(GeneratedPath + ": generated_by is 'hand-authored' but provenance.json says 'wiktionary-import'", second, StringComparison.Ordinal);
    }

    [Fact]
    public void ADamagedSidecarIsReportedWithItsPath()
    {
        using var bundle = new TempBundle(full: true);
        bundle.Write(SidecarPath, "{ not json");
        bundle.Rehash();

        var problems = LoadFails(bundle).Problems;

        Assert.All(problems, problem => Assert.StartsWith(SidecarPath + ":", problem, StringComparison.Ordinal));
    }

    [Fact]
    public void ASidecarWithABadSourceRecordIsReported()
    {
        using var bundle = new TempBundle(full: true);
        var sha = TestSupport.Bundle.LexiconSources[SidecarPath].Sources[0].Sha256;
        bundle.Edit(SidecarPath, $"\"sha256\": \"{sha}\"", "\"sha256\": \"not-a-hash\"");
        bundle.Rehash();

        var problem = Assert.Single(LoadFails(bundle).Problems);

        Assert.StartsWith(SidecarPath + ": sources[0]: sha256 must be 64 lower-case hex digits", problem, StringComparison.Ordinal);
    }

    // ---- The lexicon cache ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void ACacheServesTheSameLexiconWhileTheLexiconFilesAreTheSame()
    {
        using var bundle = new TempBundle(full: true);
        var options = new ContentLoadOptions { LexiconCache = new LexiconCache() };

        var first = SpanishContent.Load(bundle.Directory, options);
        bundle.Touch("effects/effects.json");
        bundle.Rehash();
        var second = SpanishContent.Load(bundle.Directory, options);

        Assert.NotEqual(first.ContentHash, second.ContentHash);
        Assert.Same(first.Lexicon, second.Lexicon);
        Assert.Equal(first.LexiconSources.Keys, second.LexiconSources.Keys);
    }

    [Fact]
    public void ACacheNeverHidesAChangedOrDamagedLexiconFile()
    {
        using var bundle = new TempBundle();
        var options = new ContentLoadOptions { LexiconCache = new LexiconCache() };
        var first = SpanishContent.Load(bundle.Directory, options);

        AddExtraLexicon(bundle, "extra.json", Extra("{\"form\":\"yelmo\",\"lemma\":\"yelmo\",\"pos\":\"noun\",\"features\":{\"number\":\"sg\",\"gender\":\"m\"}}"));
        bundle.Rehash();
        var changed = SpanishContent.Load(bundle.Directory, options);
        bundle.Write("lexicon/extra.json", "{\"schemaVersion\":1,\"entries\":[{\"form\":\"yelmo\"}]}");
        bundle.Rehash();
        var damaged = LoadFails(bundle, options);

        Assert.NotSame(first.Lexicon, changed.Lexicon);
        Assert.True(changed.Lexicon.ContainsLemma("yelmo"));
        Assert.False(first.Lexicon.ContainsLemma("yelmo"));
        Assert.Contains(damaged.Problems, problem => problem.StartsWith("lexicon/extra.json:", StringComparison.Ordinal));
    }

    [Fact]
    public void ACacheHoldsAtMostItsCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LexiconCache(0));
        var cache = new LexiconCache(1);
        using var bundle = new TempBundle();
        var options = new ContentLoadOptions { LexiconCache = cache };
        var first = SpanishContent.Load(bundle.Directory, options);
        AddExtraLexicon(bundle, "extra.json", Extra("{\"form\":\"yelmo\",\"lemma\":\"yelmo\",\"pos\":\"noun\",\"features\":{\"number\":\"sg\",\"gender\":\"m\"}}"));
        bundle.Rehash();
        _ = SpanishContent.Load(bundle.Directory, options);
        bundle.Delete("lexicon/extra.json");
        bundle.Edit("provenance.json", "    { \"path\": \"lexicon/extra.json\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null },\n", string.Empty);
        bundle.Rehash();

        var again = SpanishContent.Load(bundle.Directory, options);

        // The cache forgot the first lexicon when it took the second, so the first set of files is parsed again: equal, but not the same object.
        Assert.NotSame(first.Lexicon, again.Lexicon);
        Assert.Equal(first.Lexicon.Entries, again.Lexicon.Entries);
    }

    private static ContentValidationException LoadFails(TempBundle bundle, ContentLoadOptions options)
    {
        return Assert.Throws<ContentValidationException>(() => SpanishContent.Load(bundle.Directory, options));
    }
}
