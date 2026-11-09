using System.Diagnostics;
using System.Text.Json;
using Vocaterra.Nlp.ContentBuilder;
using Vocaterra.Nlp.Language.Lexicon;
using Xunit.Abstractions;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>
/// M36: invariants of the committed German lexicon (<c>content/game-de-v1/lexicon/wiktionary-de.json</c>, the future German bundle) and a spot check of 53
/// common nouns (gender, plural) and 30 verbs (strong, weak or irregular, separable prefix, the Perfekt auxiliary and the participle), written from the
/// grammar and checked against the file. Where Wiktionary itself lists a variant (a second plural, a second auxiliary) the test accepts the standard
/// form and leaves the variant to the milestone report.
/// </summary>
public class GermanGeneratedLexiconTests(ITestOutputHelper output)
{
    private const long MaxLexiconBytes = 15 * 1024 * 1024;

    private static readonly string Folder = FindFolder();

    private static readonly string Text = File.ReadAllText(Path.Combine(Folder, "wiktionary-de.json"));

    private static readonly NlpLexicon Imported = NlpLexicon.Load(Text, InvariantLexiconFolding.Instance, GermanKaikkiProfile.LexProfile);

    private static string FindFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "content", "game-de-v1", "lexicon");
            if (File.Exists(Path.Combine(candidate, "wiktionary-de.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("content/game-de-v1/lexicon/wiktionary-de.json was not found above " + AppContext.BaseDirectory + ".");
    }

    private static IEnumerable<LexiconEntry> Of(string lemma, string pos)
    {
        return Imported.Entries.Where(entry => entry.Lemma == lemma && entry.Pos == pos);
    }

    [Fact]
    public void TheFileIsExactlyWhatTheImporterWritesForItsOwnEntries()
    {
        Assert.Equal(KaikkiImporter.ToJson(Imported.Entries), Text.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal(KaikkiImporter.GeneratedBy, Imported.GeneratedBy);
    }

    [Fact]
    public void TheFileIsWithinTheSizeGate()
    {
        var size = new FileInfo(Path.Combine(Folder, "wiktionary-de.json")).Length;
        output.WriteLine($"wiktionary-de.json: {size} bytes, {Imported.Entries.Count} entries");
        Assert.True(size <= MaxLexiconBytes, $"{size} bytes");
    }

    [Fact]
    public void TheSidecarPinsTheSourcesAndAgreesWithTheFile()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder, "wiktionary-de.source.json")));
        var root = document.RootElement;
        Assert.Equal(GermanKaikkiProfile.German.ImporterVersion, root.GetProperty("importerVersion").GetString());
        Assert.Equal("wiktionary-de.json", root.GetProperty("generatedFile").GetString());
        Assert.Equal(Imported.Entries.Count, root.GetProperty("outputEntries").GetInt32());
        Assert.Equal(Imported.Entries.Select(static entry => entry.Lemma).Distinct(StringComparer.Ordinal).Count(), root.GetProperty("outputLemmas").GetInt32());
        var sources = root.GetProperty("sources").EnumerateArray().ToDictionary(static source => source.GetProperty("id").GetString()!, static source => source);
        Assert.Equal(["frequency-de-50k", "kaikki-de"], sources.Keys.Order(StringComparer.Ordinal));
        Assert.Matches("/[0-9a-f]{40}/content/2018/de/de_50k.txt$", sources["frequency-de-50k"].GetProperty("url").GetString());
        Assert.Equal("https://kaikki.org/dictionary/German/kaikki.org-dictionary-German.jsonl", sources["kaikki-de"].GetProperty("url").GetString());
        Assert.All(sources.Values, source =>
        {
            Assert.Matches("^[0-9a-f]{64}$", source.GetProperty("sha256").GetString());
            Assert.Matches("^\\d{4}-\\d{2}-\\d{2}$", source.GetProperty("retrievedOn").GetString());
        });
    }

    [Fact]
    public void EveryEntryIsLowerCaseAndAllLetters()
    {
        Assert.All(Imported.Entries, entry =>
        {
            Assert.All(entry.Form, character => Assert.True(char.IsLetter(character), $"'{entry.Form}'"));
            Assert.Equal(entry.Form.ToLowerInvariant(), entry.Form);
        });
    }

    [Fact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void LoadingTheFileIsFastEnough()
    {
        // One warm-up load (JIT, first-touch), then five timed ones; the Spanish gate measures the bundle the same way.
        _ = NlpLexicon.Load(Text, InvariantLexiconFolding.Instance, GermanKaikkiProfile.LexProfile);
        var timings = new List<long>();
        for (var run = 0; run < 5; run++)
        {
            var watch = Stopwatch.StartNew();
            _ = NlpLexicon.Load(Text, InvariantLexiconFolding.Instance, GermanKaikkiProfile.LexProfile);
            timings.Add(watch.ElapsedMilliseconds);
        }

        var ordered = timings.Order().ToArray();
        output.WriteLine($"Lexicon.Load of wiktionary-de.json (text already read): {string.Join(", ", timings)} ms (median {ordered[ordered.Length / 2]} ms)");

        // The recorded gate is 300 ms for a release build; a test run is a debug build on whatever machine runs it, so only a generous bound is asserted.
        Assert.True(ordered[ordered.Length / 2] < 3000, "Loading the German lexicon takes more than three seconds.");
    }

    public static TheoryData<string, string, string> Nouns => new()
    {
        { "haus", "n", "häuser" }, { "frau", "f", "frauen" }, { "kind", "n", "kinder" }, { "buch", "n", "bücher" }, { "tisch", "m", "tische" },
        { "auto", "n", "autos" }, { "hund", "m", "hunde" }, { "katze", "f", "katzen" }, { "freund", "m", "freunde" }, { "schule", "f", "schulen" },
        { "stadt", "f", "städte" }, { "land", "n", "länder" }, { "woche", "f", "wochen" }, { "mutter", "f", "mütter" }, { "vater", "m", "väter" },
        { "bruder", "m", "brüder" }, { "schwester", "f", "schwestern" }, { "name", "m", "namen" }, { "straße", "f", "straßen" }, { "baum", "m", "bäume" },
        { "fenster", "n", "fenster" }, { "tür", "f", "türen" }, { "zimmer", "n", "zimmer" }, { "kaffee", "m", "kaffees" }, { "wein", "m", "weine" },
        { "arzt", "m", "ärzte" }, { "hand", "f", "hände" }, { "kopf", "m", "köpfe" }, { "auge", "n", "augen" }, { "fuß", "m", "füße" },
        { "abend", "m", "abende" }, { "nacht", "f", "nächte" }, { "sache", "f", "sachen" }, { "problem", "n", "probleme" }, { "frage", "f", "fragen" },
        { "antwort", "f", "antworten" }, { "ei", "n", "eier" }, { "fisch", "m", "fische" }, { "bett", "n", "betten" }, { "küche", "f", "küchen" },
        { "bad", "n", "bäder" }, { "garten", "m", "gärten" }, { "zug", "m", "züge" }, { "flugzeug", "n", "flugzeuge" }, { "familie", "f", "familien" },
        { "sohn", "m", "söhne" }, { "tochter", "f", "töchter" }, { "berg", "m", "berge" }, { "stuhl", "m", "stühle" }, { "brot", "n", "brote" },
        { "apfel", "m", "äpfel" }, { "blume", "f", "blumen" }, { "lehrer", "m", "lehrer" },
    };

    [Theory]
    [MemberData(nameof(Nouns))]
    public void ANounHasItsGenderAndItsPlural(string lemma, string gender, string plural)
    {
        var entries = Of(lemma, "noun").ToArray();
        Assert.NotEmpty(entries);
        Assert.Contains(entries, entry => entry.Features.Gender == gender);
        Assert.Contains(entries, entry => entry.Form == plural && entry.Features.Number == "pl" && entry.Features.Case == "nom");
    }

    // lemma, declension class, separable prefix (or ""), Perfekt auxiliary (haben or sein), past participle
    public static TheoryData<string, string, string, string, string> Verbs => new()
    {
        { "gehen", "strong", "", "sein", "gegangen" }, { "kommen", "strong", "", "sein", "gekommen" }, { "haben", "irregular", "", "haben", "gehabt" },
        { "sein", "irregular", "", "sein", "gewesen" }, { "machen", "weak", "", "haben", "gemacht" }, { "sprechen", "strong", "", "haben", "gesprochen" },
        { "essen", "strong", "", "haben", "gegessen" }, { "trinken", "strong", "", "haben", "getrunken" }, { "lesen", "strong", "", "haben", "gelesen" },
        { "schreiben", "strong", "", "haben", "geschrieben" }, { "anrufen", "strong", "an", "haben", "angerufen" },
        { "einkaufen", "weak", "ein", "haben", "eingekauft" }, { "laufen", "strong", "", "sein", "gelaufen" }, { "sehen", "strong", "", "haben", "gesehen" },
        { "nehmen", "strong", "", "haben", "genommen" }, { "geben", "strong", "", "haben", "gegeben" }, { "wissen", "irregular", "", "haben", "gewusst" },
        { "wollen", "irregular", "", "haben", "gewollt" }, { "müssen", "irregular", "", "haben", "gemusst" }, { "arbeiten", "weak", "", "haben", "gearbeitet" },
        { "wohnen", "weak", "", "haben", "gewohnt" }, { "lernen", "weak", "", "haben", "gelernt" }, { "spielen", "weak", "", "haben", "gespielt" },
        { "schlafen", "strong", "", "haben", "geschlafen" }, { "mitkommen", "strong", "mit", "sein", "mitgekommen" },
        { "ankommen", "strong", "an", "sein", "angekommen" }, { "bleiben", "strong", "", "sein", "geblieben" }, { "werden", "irregular", "", "sein", "geworden" },
        { "aufstehen", "strong", "auf", "sein", "aufgestanden" }, { "abfahren", "strong", "ab", "sein", "abgefahren" },
    };

    [Theory]
    [MemberData(nameof(Verbs))]
    public void AVerbHasItsClassItsParticleItsAuxiliaryAndItsParticiple(string lemma, string declension, string particle, string auxiliary, string participle)
    {
        var entries = Of(lemma, "verb").ToArray();
        var infinitive = Assert.Single(entries, entry => entry.Features.VerbForm == "inf");
        Assert.Equal(lemma, infinitive.Form);
        Assert.Equal(declension, infinitive.Features.Declension);
        Assert.Equal(particle.Length == 0 ? null : particle, infinitive.Features.Particle);
        Assert.Contains(entries, entry => entry.Form == participle && entry.Features.VerbForm == "part2-" + auxiliary);
    }

    [Fact]
    public void ThePersonalPronounsAreThereWithTheirCases()
    {
        foreach (var form in new[] { "ich", "du", "er", "sie", "es", "wir", "ihr" })
        {
            Assert.Contains(Imported.Lookup(form), _ => true);
        }

        Assert.Contains(Of("mich", "pron"), entry => entry.Features.Case == "acc");
        Assert.Contains(Of("dich", "pron"), entry => entry.Features.Case == "acc");
        Assert.Contains(Of("dir", "pron"), entry => entry.Features.Case == "dat");
        Assert.Contains(Of("mir", "pron"), entry => entry.Features.Case == "dat");
    }
}
