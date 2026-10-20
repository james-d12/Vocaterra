namespace Vocaterra.Nlp.Nlp.Tests.Lexicon;

using Vocaterra.Nlp.Nlp.Lexicon;

/// <summary>The merge policy of several lexicon files: <see cref="Lexicon.FromEntries"/> is called with the entries of all of them.</summary>
public class LexiconMergeTests
{
    private static LexiconEntry Noun(string form, string lemma, string number, string gender)
    {
        return new LexiconEntry(form, lemma, "noun", new EntryFeatures(null, number, null, null, gender));
    }

    private static LexiconEntry Verb(string form, string lemma, string person, string number, string tense, string mood)
    {
        return new LexiconEntry(form, lemma, "verb", new EntryFeatures(person, number, tense, mood, null));
    }

    private static readonly LexiconEntry[] HandAuthored =
    [
        Noun("espada", "espada", "sg", "f"),
        Noun("espadas", "espada", "pl", "f"),
        Verb("compro", "comprar", "1", "sg", "pres", "ind"),
    ];

    private static readonly LexiconEntry[] Imported =
    [
        Verb("compro", "comprar", "1", "sg", "pres", "ind"),
        Verb("compraba", "comprar", "1", "sg", "impf", "ind"),
        Noun("espada", "espada", "sg", "m"),
        Verb("compro", "comprar", "3", "sg", "pres", "subj"),
    ];

    [Fact]
    public void IdenticalReadingsAreStoredOnce()
    {
        var merged = Lexicon.FromEntries([.. HandAuthored, .. Imported]);

        Assert.Single(merged.Lookup("compro"), reading => reading.Person == "1" && reading.Tense == "pres" && reading.Mood == "ind");
        Assert.Equal(1, merged.Entries.Count(entry => entry.Form == "compro" && entry.Features.Person == "1" && entry.Features.Mood == "ind"));
    }

    [Fact]
    public void DifferentReadingsOfOneFormCoexist()
    {
        var merged = Lexicon.FromEntries([.. HandAuthored, .. Imported]);

        var espada = merged.Lookup("espada");
        var compro = merged.Lookup("compro");

        Assert.Equal(["f", "m"], espada.Select(reading => reading.Gender).Order(StringComparer.Ordinal));
        Assert.Equal(2, compro.Count);
        Assert.Contains(compro, reading => reading.Mood == "subj");
    }

    [Fact]
    public void TheUnionHasEveryEntryOfEveryFile()
    {
        var merged = Lexicon.FromEntries([.. HandAuthored, .. Imported]);

        foreach (var entry in HandAuthored.Concat(Imported))
        {
            Assert.Contains(entry, merged.Entries);
        }

        Assert.Equal(HandAuthored.Concat(Imported).Distinct().Count(), merged.Entries.Count);
    }

    [Fact]
    public void TheOrderOfTheFilesDoesNotChangeTheResult()
    {
        var forward = Lexicon.FromEntries([.. HandAuthored, .. Imported]);
        var backward = Lexicon.FromEntries([.. Imported, .. HandAuthored]);

        Assert.Equal(forward.Entries, backward.Entries);
        Assert.Equal(forward.ToJson(), backward.ToJson());
        Assert.Equal(forward.Lookup("espada").ToArray(), backward.Lookup("espada").ToArray());
    }

    [Fact]
    public void ReadingsAreReturnedInEntryOrderSoTheResultIsStable()
    {
        var merged = Lexicon.FromEntries([.. Imported, .. HandAuthored]);

        // Entries sort by form, lemma, part of speech and then features with absent values first, so the order of readings is a property of the data.
        Assert.Equal(
            merged.Entries.Where(entry => entry.Form == "compro").Select(entry => entry.ToFeatures()).ToArray(),
            merged.Lookup("compro").ToArray());
    }

    [Fact]
    public void FormsThatDifferOnlyByAccentShareTheAccentInsensitiveIndexInEntryOrder()
    {
        var merged = Lexicon.FromEntries(
        [
            Verb("está", "estar", "3", "sg", "pres", "ind"),
            new LexiconEntry("esta", "este", "adj", new EntryFeatures(null, "sg", null, null, "f")),
            Verb("estás", "estar", "2", "sg", "pres", "ind"),
        ]);

        var exact = merged.Lookup("esta");
        var accented = merged.Lookup("está");
        var folded = merged.Lookup("ésta");

        Assert.False(exact.IsAccentInsensitive);
        Assert.Equal(["este"], exact.Select(reading => reading.Lemma).ToArray());
        Assert.False(accented.IsAccentInsensitive);
        Assert.Equal(["estar"], accented.Select(reading => reading.Lemma).ToArray());

        // "ésta" is in the lexicon in neither spelling; it folds to the key "esta", which both entries share, in entry order (esta before está).
        Assert.True(folded.IsAccentInsensitive);
        Assert.Equal(["este", "estar"], folded.Select(reading => reading.Lemma).ToArray());
        Assert.Equal(["estar"], merged.Lookup("estás").Select(reading => reading.Lemma).ToArray());
    }
}
