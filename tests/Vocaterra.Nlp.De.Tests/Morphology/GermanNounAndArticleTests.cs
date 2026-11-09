using Vocaterra.Nlp.De.Morphology;
using Vocaterra.Nlp.Language.Morphology;

namespace Vocaterra.Nlp.De.Tests.Morphology;

/// <summary>Tests for the articles, the possessives, the noun forms and the adjective endings of <see cref="GermanMorphology"/> (M38).</summary>
public class GermanNounAndArticleTests
{
    private static readonly GermanMorphology Morphology = new(TestSupport.FullLexicon);

    // Fifty frequent nouns: lemma, gender, plural, dative plural (as the lexicon and a German grammar give them).
    private static readonly (string Lemma, string Gender, string Plural, string DativePlural)[] Nouns =
    [
        ("haus", "n", "Häuser", "Häusern"), ("kind", "n", "Kinder", "Kindern"), ("mann", "m", "Männer", "Männern"), ("frau", "f", "Frauen", "Frauen"),
        ("tag", "m", "Tage", "Tagen"), ("jahr", "n", "Jahre", "Jahren"), ("mutter", "f", "Mütter", "Müttern"), ("vater", "m", "Väter", "Vätern"),
        ("bruder", "m", "Brüder", "Brüdern"), ("schwester", "f", "Schwestern", "Schwestern"), ("freund", "m", "Freunde", "Freunden"), ("stadt", "f", "Städte", "Städten"),
        ("land", "n", "Länder", "Ländern"), ("buch", "n", "Bücher", "Büchern"), ("auto", "n", "Autos", "Autos"), ("hund", "m", "Hunde", "Hunden"),
        ("katze", "f", "Katzen", "Katzen"), ("tisch", "m", "Tische", "Tischen"), ("stuhl", "m", "Stühle", "Stühlen"), ("apfel", "m", "Äpfel", "Äpfeln"),
        ("brot", "n", "Brote", "Broten"), ("schule", "f", "Schulen", "Schulen"), ("lehrer", "m", "Lehrer", "Lehrern"), ("zimmer", "n", "Zimmer", "Zimmern"),
        ("straße", "f", "Straßen", "Straßen"), ("woche", "f", "Wochen", "Wochen"), ("monat", "m", "Monate", "Monaten"), ("hand", "f", "Hände", "Händen"),
        ("kopf", "m", "Köpfe", "Köpfen"), ("fuß", "m", "Füße", "Füßen"), ("auge", "n", "Augen", "Augen"), ("name", "m", "Namen", "Namen"),
        ("junge", "m", "Jungen", "Jungen"), ("mädchen", "n", "Mädchen", "Mädchen"), ("mensch", "m", "Menschen", "Menschen"), ("herr", "m", "Herren", "Herren"),
        ("arzt", "m", "Ärzte", "Ärzten"), ("baum", "m", "Bäume", "Bäumen"), ("berg", "m", "Berge", "Bergen"), ("fisch", "m", "Fische", "Fischen"),
        ("vogel", "m", "Vögel", "Vögeln"), ("blume", "f", "Blumen", "Blumen"), ("tür", "f", "Türen", "Türen"), ("fenster", "n", "Fenster", "Fenstern"),
        ("bett", "n", "Betten", "Betten"), ("welt", "f", "Welten", "Welten"), ("zeit", "f", "Zeiten", "Zeiten"), ("weg", "m", "Wege", "Wegen"),
        ("platz", "m", "Plätze", "Plätzen"), ("sohn", "m", "Söhne", "Söhnen"), ("tochter", "f", "Töchter", "Töchtern"), ("schwert", "n", "Schwerter", "Schwertern"),
    ];

    [Fact]
    public void FiftyNounsHaveTheRightGenderPluralAndDativePlural()
    {
        var failures = new List<string>();
        foreach (var (lemma, gender, plural, dativePlural) in Nouns)
        {
            Check(failures, lemma + " gender", gender, Morphology.GenderOf(lemma));
            Check(failures, lemma + " Pluralize", plural, Morphology.Pluralize(lemma));
            Check(failures, lemma + " InflectNoun pl", plural, Morphology.InflectNoun(lemma, "pl"));
            Check(failures, lemma + " dat pl", dativePlural, Morphology.DeclineNoun(lemma, "pl", "dat"));
            Check(failures, lemma + " Singularize(plural)", Capitalize(lemma), Morphology.Singularize(plural));
            Check(failures, lemma + " nom pl", plural, Morphology.DeclineNoun(lemma, "pl", "nom"));
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheWeakNounsTakeNInEveryCaseButTheNominative()
    {
        Assert.Equal("Jungen", Morphology.DeclineNoun("Junge", "sg", "acc"));
        Assert.Equal("Jungen", Morphology.DeclineNoun("Junge", "sg", "dat"));
        Assert.Equal("Jungen", Morphology.DeclineNoun("Junge", "sg", "gen"));
        Assert.Equal("Junge", Morphology.DeclineNoun("Junge", "sg", "nom"));
        Assert.Equal("Menschen", Morphology.DeclineNoun("Mensch", "sg", "acc"));
        Assert.Equal("Herrn", Morphology.DeclineNoun("Herr", "sg", "dat"));
        Assert.Equal("Jungen", Morphology.Pluralize("Junge"));
    }

    [Fact]
    public void TheStrongNounsTakeSInTheGenitiveAndNInTheDativePlural()
    {
        Assert.Equal("Tages", Morphology.DeclineNoun("tag", "sg", "gen"));
        Assert.Equal("Tag", Morphology.DeclineNoun("tag", "sg", "dat"));
        Assert.Equal("Hauses", Morphology.DeclineNoun("haus", "sg", "gen"));
        Assert.Equal("Häusern", Morphology.DeclineNoun("haus", "pl", "dat"));
        Assert.Equal("Frauen", Morphology.DeclineNoun("frau", "pl", "dat"));
        Assert.Equal("Autos", Morphology.DeclineNoun("auto", "pl", "dat"));
    }

    [Fact]
    public void ANounTheLexiconLacksGetsTheRuleGuess()
    {
        Assert.Equal("Blorken", Morphology.Pluralize("Blorke"));
        Assert.Equal("Blorkinnen", Morphology.Pluralize("Blorkin"));
        Assert.Equal("Blorkel", Morphology.Pluralize("Blorkel"));
        Assert.Equal("Blorks", Morphology.DeclineNoun("Blork", "sg", "gen"));
        Assert.Equal("Blorke", Morphology.Pluralize("Blork"));
        Assert.Equal("Blorken", Morphology.DeclineNoun("Blork", "pl", "dat"));
        Assert.Null(Morphology.GenderOf("Blork"));
    }

    [Fact]
    public void EveryGeneratedNounFormIsAFormOfTheLexicon()
    {
        var lexicon = TestSupport.FullLexicon;
        var failures = new List<string>();
        foreach (var (lemma, _, _, _) in Nouns)
        {
            foreach (var number in new[] { "sg", "pl" })
            {
                foreach (var grammaticalCase in new[] { "nom", "acc", "dat", "gen" })
                {
                    var form = Morphology.DeclineNoun(lemma, number, grammaticalCase);
                    if (!lexicon.Lookup(form).Any(r => r.Lemma == lemma && r.Pos == "noun"))
                    {
                        failures.Add($"{lemma} {number} {grammaticalCase}: {form}");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData("m", "sg", "nom", "der", "ein")]
    [InlineData("m", "sg", "acc", "den", "einen")]
    [InlineData("m", "sg", "dat", "dem", "einem")]
    [InlineData("m", "sg", "gen", "des", "eines")]
    [InlineData("f", "sg", "nom", "die", "eine")]
    [InlineData("f", "sg", "acc", "die", "eine")]
    [InlineData("f", "sg", "dat", "der", "einer")]
    [InlineData("f", "sg", "gen", "der", "einer")]
    [InlineData("n", "sg", "nom", "das", "ein")]
    [InlineData("n", "sg", "acc", "das", "ein")]
    [InlineData("n", "sg", "dat", "dem", "einem")]
    [InlineData("n", "sg", "gen", "des", "eines")]
    [InlineData("m", "pl", "nom", "die", "")]
    [InlineData("f", "pl", "acc", "die", "")]
    [InlineData("n", "pl", "dat", "den", "")]
    [InlineData("m", "pl", "gen", "der", "")]
    public void TheArticlesFollowGenderNumberAndCase(string gender, string number, string grammaticalCase, string definite, string indefinite)
    {
        Assert.Equal(definite, Morphology.Article(Definiteness.Definite, gender, number, "x", grammaticalCase));
        Assert.Equal(indefinite, Morphology.Article(Definiteness.Indefinite, gender, number, "x", grammaticalCase));
    }

    [Fact]
    public void ANullCaseIsTheNominativeAndAnUnknownOneIsRefused()
    {
        Assert.Equal("der", Morphology.Article(Definiteness.Definite, "m", "sg"));
        Assert.Equal("die", Morphology.Article(Definiteness.Definite, "f", "sg", "frau"));
        Assert.Equal("das", ((IMorphology)Morphology).Article(Definiteness.Definite, "n", "sg", "haus", null));
        Assert.Throws<ArgumentException>(() => Morphology.Article(Definiteness.Definite, "m", "sg", "x", "voc"));
        Assert.Throws<ArgumentException>(() => Morphology.Article(Definiteness.Definite, "x", "sg", "x", "nom"));
    }

    [Theory]
    [InlineData("m", "sg", "nom", "kein")]
    [InlineData("f", "sg", "nom", "keine")]
    [InlineData("m", "sg", "acc", "keinen")]
    [InlineData("n", "sg", "dat", "keinem")]
    [InlineData("f", "sg", "gen", "keiner")]
    [InlineData("n", "pl", "nom", "keine")]
    [InlineData("m", "pl", "dat", "keinen")]
    public void KeinTakesTheEinEndings(string gender, string number, string grammaticalCase, string expected) =>
        Assert.Equal(expected, Morphology.Kein(gender, number, grammaticalCase));

    [Theory]
    [InlineData("ich", "m", "sg", "nom", "mein")]
    [InlineData("ich", "f", "sg", "nom", "meine")]
    [InlineData("du", "m", "sg", "acc", "deinen")]
    [InlineData("er", "n", "sg", "dat", "seinem")]
    [InlineData("es", "f", "sg", "dat", "seiner")]
    [InlineData("sie", "n", "sg", "nom", "ihr")]
    [InlineData("sie", "m", "pl", "dat", "ihren")]
    [InlineData("wir", "f", "sg", "nom", "unsere")]
    [InlineData("wir", "m", "sg", "dat", "unserem")]
    [InlineData("ihr", "m", "sg", "nom", "euer")]
    [InlineData("ihr", "f", "sg", "nom", "eure")]
    [InlineData("ihr", "m", "sg", "acc", "euren")]
    [InlineData("ihr", "n", "sg", "gen", "eures")]
    [InlineData("Sie", "m", "sg", "nom", "Ihr")]
    [InlineData("Sie", "f", "pl", "acc", "Ihre")]
    public void ThePossessivesTakeTheEinEndings(string possessor, string gender, string number, string grammaticalCase, string expected) =>
        Assert.Equal(expected, Morphology.Possessive(possessor, gender, number, grammaticalCase));

    [Fact]
    public void AnUnknownPossessorIsRefused() =>
        Assert.Throws<ArgumentException>(() => Morphology.Possessive("man", "m", "sg", "nom"));

    [Theory]
    [InlineData("gut", "m", "sg", "nom", null, "guter")]
    [InlineData("gut", "f", "sg", "nom", null, "gute")]
    [InlineData("gut", "n", "sg", "nom", null, "gutes")]
    [InlineData("gut", "m", "sg", "dat", null, "gutem")]
    [InlineData("gut", "f", "sg", "dat", null, "guter")]
    [InlineData("gut", "m", "sg", "acc", null, "guten")]
    [InlineData("gut", "m", "pl", "nom", null, "gute")]
    [InlineData("gut", "m", "pl", "gen", null, "guter")]
    [InlineData("gut", "m", "sg", "nom", 0, "gute")]
    [InlineData("gut", "m", "sg", "acc", 0, "guten")]
    [InlineData("gut", "f", "sg", "acc", 0, "gute")]
    [InlineData("gut", "n", "sg", "nom", 0, "gute")]
    [InlineData("gut", "m", "sg", "dat", 0, "guten")]
    [InlineData("gut", "m", "pl", "nom", 0, "guten")]
    [InlineData("gut", "m", "sg", "nom", 1, "guter")]
    [InlineData("gut", "f", "sg", "nom", 1, "gute")]
    [InlineData("gut", "n", "sg", "acc", 1, "gutes")]
    [InlineData("gut", "m", "sg", "acc", 1, "guten")]
    [InlineData("gut", "m", "pl", "nom", 1, "guten")]
    [InlineData("hoch", "m", "sg", "nom", 0, "hohe")]
    [InlineData("hoch", "m", "sg", "nom", null, "hoher")]
    [InlineData("dunkel", "f", "sg", "nom", 1, "dunkle")]
    [InlineData("teuer", "n", "sg", "nom", 1, "teures")]
    [InlineData("blorg", "m", "sg", "acc", null, "blorgen")]
    [InlineData("dunkelblau", "f", "sg", "dat", 0, "dunkelblauen")]
    public void TheAdjectiveEndingsFollowTheArticle(string adjective, string gender, string number, string grammaticalCase, int? article, string expected)
    {
        Definiteness? kind = article is null ? null : (Definiteness)article.Value;
        Assert.Equal(expected, Morphology.DeclineAdjective(adjective, gender, number, grammaticalCase, kind));
    }

    [Fact]
    public void AgreeAdjectiveIsTheStrongNominative()
    {
        Assert.Equal("gute", Morphology.AgreeAdjective("gut", "f", "sg"));
        Assert.Equal("gutes", Morphology.AgreeAdjective("gut", "n", "sg"));
        Assert.Equal("gute", Morphology.AgreeAdjective("gut", "m", "pl"));
    }

    [Fact]
    public void TheMorphologyIsDeterministic()
    {
        var other = new GermanMorphology(TestSupport.FullLexicon);
        foreach (var (lemma, _, _, _) in Nouns.Take(20))
        {
            Assert.Equal(Morphology.Pluralize(lemma), other.Pluralize(lemma));
            Assert.Equal(Morphology.DeclineNoun(lemma, "pl", "dat"), other.DeclineNoun(lemma, "pl", "dat"));
        }
    }

    private static string Capitalize(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    private static void Check(List<string> failures, string what, string? expected, string? actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            failures.Add($"{what}: expected '{expected}', got '{actual}'");
        }
    }
}
