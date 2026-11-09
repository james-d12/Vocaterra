using System.Security.Cryptography;
using System.Text;
using Vocaterra.Nlp.ContentBuilder;
using Vocaterra.Nlp.Language.Lexicon;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>
/// M36: the German language profile of the wiktextract import on a small fixture of four real lines (Frau, gut, gehen, aufstehen, trimmed to the fields the
/// importer reads), and the proof on the Spanish fixture that the profile mechanism left the Spanish output as it was.
/// </summary>
public class GermanLexiconImportTests
{
    private static readonly string GermanFixture = Path.Combine(AppContext.BaseDirectory, "Data", "kaikki-de-sample.jsonl");

    private static readonly KaikkiImportOptions German = new() { Language = KaikkiLanguageProfile.German };

    private static readonly KaikkiImportResult Result = KaikkiImporter.ImportFile(GermanFixture, German);

    private static readonly NlpLexicon Imported = NlpLexicon.FromEntries(Result.Entries, InvariantLexiconFolding.Instance, GermanKaikkiProfile.LexProfile);

    private static IReadOnlyList<LexiconEntry> Of(string lemma)
    {
        return [.. Result.Entries.Where(entry => entry.Lemma == lemma)];
    }

    [Fact]
    public void ReadsOnlyGermanEntriesAndCountsThem()
    {
        Assert.Equal(4, Result.LinesRead);
        Assert.Equal(4, Result.SpanishEntries);
        Assert.Equal(4, Result.ImportedEntries);
        Assert.Equal("German", Result.LanguageName);
        Assert.Contains("4 German entries", Result.Summary(), StringComparison.Ordinal);
    }

    [Fact]
    public void ANounKeepsItsGenderItsCasesAndItsPlural()
    {
        var frau = Of("frau");
        Assert.All(frau, entry => Assert.Equal("f", entry.Features.Gender));
        Assert.Contains(frau, entry => entry.Form == "frauen" && entry.Features.Number == "pl" && entry.Features.Case == "nom");
        Assert.Contains(frau, entry => entry.Form == "frauen" && entry.Features.Number == "pl" && entry.Features.Case == "dat");
        Assert.Contains(frau, entry => entry.Form == "frau" && entry.Features.Number == "sg" && entry.Features.Case == "gen");
    }

    [Fact]
    public void AnAdjectiveKeepsItsDeclensionClasses()
    {
        var gut = Of("gut");
        Assert.Contains(gut, entry => entry.Form == "guten" && entry.Features.Declension == "mixed" && entry.Features.Case == "dat" && entry.Features.Number == "pl");
        Assert.Contains(gut, entry => entry.Form == "gutem" && entry.Features.Declension == "strong" && entry.Features.Gender == "m" && entry.Features.Case == "dat");
        Assert.Contains(gut, entry => entry.Form == "gute" && entry.Features.Declension == "strong" && entry.Features.Gender == "f" && entry.Features.Case == "nom");
    }

    [Fact]
    public void AStrongVerbWithSeinHasItsParticipleAndItsAuxiliary()
    {
        var gehen = Of("gehen");
        Assert.Contains(gehen, entry => entry.Form == "gegangen" && entry.Features.VerbForm == "part2-sein");
        Assert.DoesNotContain(gehen, entry => entry.Features.VerbForm == "part2-haben");
        Assert.Contains(gehen, entry => entry.Form == "ging" && entry.Features.Tense == "pret" && entry.Features.Person == "3" && entry.Features.Number == "sg");
        Assert.Contains(gehen, entry => entry.Form == "gehen" && entry.Features.VerbForm == "inf" && entry.Features.Declension == "strong");
    }

    [Fact]
    public void ASeparableVerbKeepsItsParticle()
    {
        var stehe = Of("aufstehen");
        Assert.Contains(stehe, entry => entry.Form == "aufstehen" && entry.Features.VerbForm == "inf" && entry.Features.Particle == "auf");
        Assert.Contains(stehe, entry => entry.Form == "stand" && entry.Features.Particle == "auf" && entry.Features.Tense == "pret");
        Assert.Contains(stehe, entry => entry.Form == "aufgestanden" && entry.Features.VerbForm == "part2-sein");
        Assert.Contains(stehe, entry => entry.Form == "aufgestanden" && entry.Features.VerbForm == "part2-haben");
    }

    [Fact]
    public void TheFileRoundTripsThroughTheGermanReaderAndFindsNounsWhateverTheCase()
    {
        var json = KaikkiImporter.ToJson(Result.Entries);
        var again = NlpLexicon.Load(json, InvariantLexiconFolding.Instance, GermanKaikkiProfile.LexProfile);
        Assert.Equal(Imported.Entries.Count, again.Entries.Count);
        Assert.NotEmpty(Imported.Lookup("Frauen"));
        Assert.NotEmpty(Imported.Lookup("frauen"));
    }

    [Fact]
    public void ImportingTwiceGivesTheSameBytes()
    {
        var first = KaikkiImporter.ToJson(KaikkiImporter.ImportFile(GermanFixture, German).Entries);
        var second = KaikkiImporter.ToJson(KaikkiImporter.ImportFile(GermanFixture, German).Entries);
        Assert.Equal(first, second);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(first))), Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(second))));
    }

    [Fact]
    public void TheSpanishProfileGivesTheBytesTheImporterGaveBeforeThereWereProfiles()
    {
        var implicitProfile = KaikkiImporter.ToJson(KaikkiImporter.ImportFile(TestSupport.KaikkiFixture).Entries);
        var explicitProfile = KaikkiImporter.ToJson(KaikkiImporter.ImportFile(TestSupport.KaikkiFixture, new KaikkiImportOptions { Language = KaikkiLanguageProfile.Spanish }).Entries);
        Assert.Equal(implicitProfile, explicitProfile);

        // Recorded with the importer before the language profile (M36, 2026-11-09); the full-file proof is in the milestone report.
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(implicitProfile)));
        Assert.Equal(SpanishFixtureHash, hash);
        Assert.DoesNotContain("\"case\"", implicitProfile, StringComparison.Ordinal);
        Assert.DoesNotContain("\"verbForm\"", implicitProfile, StringComparison.Ordinal);
        Assert.Equal("1.1.0", KaikkiLanguageProfile.Spanish.ImporterVersion);
        Assert.Equal("de-1.0.0", KaikkiLanguageProfile.German.ImporterVersion);
    }

    private const string SpanishFixtureHash = "e15c1715daf7e22b378f8eed766d07339d596445309a8be7117f9bf7b7c43f80";
}
