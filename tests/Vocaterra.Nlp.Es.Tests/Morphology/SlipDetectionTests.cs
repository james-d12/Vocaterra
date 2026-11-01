namespace Vocaterra.Nlp.Es.Tests.Morphology;

using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;

/// <summary>
/// M58: the two slips the Spanish morphology detects for the analyzer, on the lexicon of the bundle. A regularized irregular verb form that is also a lexicon word
/// (<c>costa</c>, the noun, for <c>cuesta</c>; <see cref="MorphologyGenerator.TryGetRegularization"/> never reports a known word), and a plural made with the wrong
/// ending on a lexicon noun (<c>pans</c> for <c>panes</c>).
/// </summary>
public class SlipDetectionTests
{
    private static readonly Lazy<MorphologyGenerator> Morphology = new(static () => new MorphologyGenerator(BundleLexicon()));

    private static Lexicon BundleLexicon()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "content", "lexicon");
        var entries = new List<LexiconEntry>();
        foreach (var path in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            entries.AddRange(Lexicon.ReadFile(File.ReadAllBytes(path)).Entries);
        }

        return SpanishLexicon.FromEntries(entries);
    }

    [Theory]
    [InlineData("costa", "costar", "cuesta", "3", "sg")]
    [InlineData("costas", "costar", "cuestas", "2", "sg")]
    [InlineData("jugo", "jugar", "juego", "1", "sg")]
    [InlineData("mente", "mentir", "miente", "3", "sg")]
    [InlineData("cabo", "caber", "quepo", "1", "sg")]
    [InlineData("esto", "estar", "estoy", "1", "sg")]
    [InlineData("podo", "poder", "puedo", "1", "sg")]
    [InlineData("pode", "poder", "puede", "3", "sg")]
    public void FindsTheRegularizedVerbOfALexiconWord(string form, string lemma, string expected, string person, string number)
    {
        Assert.True(Morphology.Value.TryGetRegularizationOfWord(form, out var found));
        Assert.Equal(new RegularizedForm(lemma, expected, person, number, LexiconSchema.Present), found);
        Assert.False(Morphology.Value.TryGetRegularization(form, out _));
    }

    // An accent variant is no slip (esta, estas for está, estás); a word the lexicon lacks is TryGetRegularization's (costan, quero, tenes, dormo); the real forms
    // and other words are no regularization at all.
    [Theory]
    [InlineData("esta")]
    [InlineData("estas")]
    [InlineData("costan")]
    [InlineData("quero")]
    [InlineData("tenes")]
    [InlineData("dormo")]
    [InlineData("cuesta")]
    [InlineData("puedo")]
    [InlineData("pan")]
    [InlineData("")]
    public void FindsNoneForAccentVariantsUnknownFormsOrOtherWords(string form)
    {
        Assert.False(Morphology.Value.TryGetRegularizationOfWord(form, out _));
    }

    [Theory]
    [InlineData("costan", "cuestan")]
    [InlineData("quero", "quiero")]
    [InlineData("tenes", "tienes")]
    [InlineData("dormo", "duermo")]
    public void TheStemChangesMissedOnFormsTheLexiconLacksStayRegularizations(string form, string expected)
    {
        Assert.True(Morphology.Value.TryGetRegularization(form, out var found));
        Assert.Equal(expected, found.Expected);
    }

    [Theory]
    [InlineData("pans", "pan", "panes")]
    [InlineData("flors", "flor", "flores")]
    [InlineData("relojs", "reloj", "relojes")]
    [InlineData("doctors", "doctor", "doctores")]
    [InlineData("mars", "mar", "mares")]
    [InlineData("luzs", "luz", "luces")]
    [InlineData("luzes", "luz", "luces")]
    [InlineData("cancions", "canción", "canciones")]
    [InlineData("Pans", "pan", "panes")]
    public void FindsAPluralMadeWithTheWrongEnding(string form, string lemma, string expected)
    {
        Assert.True(Morphology.Value.TryGetPluralSlip(form, out var found));
        Assert.Equal(new PluralSlip(lemma, expected), found);
    }

    // A form the lexicon knows (panes, robots, clubs), a noun that takes -s (a loan that ends in t: robots would be robots), a stem that is no noun, and a word too short.
    [Theory]
    [InlineData("panes")]
    [InlineData("pan")]
    [InlineData("flores")]
    [InlineData("robots")]
    [InlineData("clubs")]
    [InlineData("glurps")]
    [InlineData("sombrs")]
    [InlineData("es")]
    [InlineData("")]
    public void FindsNoPluralSlipForKnownFormsOrOtherWords(string form)
    {
        Assert.False(Morphology.Value.TryGetPluralSlip(form, out _));
    }

    // The interface's default finds none, so a morphology of another language need not implement either.
    [Fact]
    public void TheInterfaceDefaultsFindNone()
    {
        IMorphology other = new NoSlips();
        Assert.False(other.TryGetRegularizationOfWord("costa", out _));
        Assert.False(other.TryGetPluralSlip("pans", out _));
    }

    private sealed class NoSlips : IMorphology
    {
        public string Pluralize(string noun) => noun;

        public string Singularize(string noun) => noun;

        public string InflectNoun(string lemma, string number) => lemma;

        public string Article(Definiteness definiteness, string gender, string number) => string.Empty;

        public string Article(Definiteness definiteness, string gender, string number, string nounLemma) => string.Empty;

        public string AgreeAdjective(string lemma, string gender, string number) => lemma;

        public string? Conjugate(string lemma, string person, string number, string tense) => null;

        public bool IsRegularizedIrregular(string form, out string lemma, out string expected)
        {
            lemma = string.Empty;
            expected = string.Empty;
            return false;
        }

        public bool TryGetRegularization(string form, out RegularizedForm result)
        {
            result = null!;
            return false;
        }
    }
}
