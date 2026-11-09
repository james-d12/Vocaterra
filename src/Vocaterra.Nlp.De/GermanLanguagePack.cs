using Vocaterra.Nlp.De.Morphology;
using Vocaterra.Nlp.De.Normalization;
using Vocaterra.Nlp.De.Text;

namespace Vocaterra.Nlp.De;

/// <summary>
/// The German pack (de-DE, Austrian and Swiss spellings accepted; D-game-27 item 6), the second <see cref="ILanguagePack"/>. M37 is the text layer: the
/// normalizer, the umlaut and ß folding, the number words, the phonetic key, the punctuation, the repair lines and the recast policy are written. The members
/// that need the German grammar are placeholders until their milestones: <see cref="CreateAnalysisRules"/> (M39) and
/// <see cref="CreateTemplateFunctions"/> (M40) throw <see cref="NotSupportedException"/>, and <see cref="G2p"/> is null (no pronunciation scoring, no phone-fed speech
/// until M43 and its G2P decision). Register it with <see cref="GermanContent.WithGerman"/> or <c>LanguagePacks.Default.With(GermanLanguagePack.Instance)</c>.
/// </summary>
public sealed class GermanLanguagePack : ILanguagePack
{
    // The German polite word, once: the grammar defaults and the linter both read it from here. Where it stands is the German three: before the sentence,
    // after it, and after the finite verb ("Kannst du bitte helfen?").
    private static readonly string[] PoliteWord = ["bitte"];

    // The subject pronouns by their lemma; "sie" is both the third person and the formal Sie, and "es" the neuter.
    private static readonly string[] SubjectPronounLemmas = ["ich", "du", "er", "sie", "es", "wir", "ihr"];

    // The article and numeral homographs of the linter: ein and eine are the indefinite article and the numeral one.
    private static readonly string[] ArticleNumeralHomographs = ["ein", "eine"];

    private GermanLanguagePack()
    {
    }

    /// <summary>The one instance.</summary>
    public static GermanLanguagePack Instance { get; } = new();

    /// <summary>
    /// The lexicon profile of the German lexicon files (<c>wiktionary-de.json</c>): three genders, two numbers, three persons, the present and the preterite,
    /// the indicative, the subjunctive and the imperative, four cases, the verb forms of the participles, four declension classes, the separable particles and the formal register of the pronoun <c>Sie</c>.
    /// </summary>
    public static LexiconProfile Profile { get; } = new(
        ["m", "f", "n"],
        ["sg", "pl"],
        ["1", "2", "3"],
        ["pres", "pret"],
        ["ind", "subj", "imp"],
        cases: ["nom", "acc", "dat", "gen"],
        verbForms: ["inf", "part1", "part2", "part2-haben", "part2-sein"],
        declensions: ["strong", "weak", "mixed", "irregular"],
        registers: ["formal"],
        particles:
        [
            "ab", "an", "auf", "aus", "auseinander", "bei", "bereit", "da", "dabei", "dar", "davon", "dazu", "drauf", "durch", "ein", "entgegen", "fest", "fort", "frei",
            "heim", "her", "heran", "heraus", "herbei", "herein", "herum", "herunter", "hervor", "hin", "hinaus", "hinein", "hinweg", "hinzu", "hoch", "inne", "kaputt",
            "kennen", "los", "mit", "nach", "nieder", "ran", "rauf", "raus", "rein", "rum", "runter", "statt", "teil", "um", "unter", "vor", "voran", "voraus", "vorbei",
            "vorher", "weg", "weiter", "wieder", "zu", "zurück", "zusammen", "über",
        ]);

    /// <summary>The error tags of German: the v1 tags, then <c>orth.capital</c> (a capital missing, typed input only).</summary>
    public static ErrorTagSet Tags { get; } = new([.. Core.Taxonomy.ErrorTags.All, Core.Taxonomy.ErrorTags.OrthCapital]);

    /// <inheritdoc />
    public string LanguageId => "de";

    /// <inheritdoc />
    public ITextNormalizer Normalizer => GermanNormalizer.Instance;

    /// <inheritdoc />
    public ILexiconFolding Folding => GermanFolding.Instance;

    /// <inheritdoc />
    public INumberWords Numbers { get; } = new GermanNumberWords();

    /// <inheritdoc />
    public IPhoneticKey PhoneticKey => GermanPhoneticKey.Instance;

    /// <inheritdoc />
    public IPunctuation Punctuation => GermanPunctuation.Instance;

    /// <inheritdoc />
    public IG2p? G2p => null;

    /// <inheritdoc />
    public GrammarDefaults GrammarDefaults { get; } = new(
        false,
        SubjectPronounLemmas,
        PoliteWord,
        PolitePositions.Trailing | PolitePositions.Leading | PolitePositions.AfterFiniteVerb);

    /// <inheritdoc />
    public IReadOnlyList<string> GrammaticalCases { get; } = ["nom", "acc", "dat", "gen"];

    /// <inheritdoc />
    public bool SupportsV2Order => false;

    /// <inheritdoc />
    public LintVocabulary Lint { get; } = new(SubjectPronounLemmas, PoliteWord, ArticleNumeralHomographs);

    /// <inheritdoc />
    public RepairLines RepairFallback => GermanRepairLines.Lines;

    /// <inheritdoc />
    public IRecastPolicy Recast => GermanRecastPolicy.Instance;

    /// <inheritdoc />
    public IOrthographyNotes Orthography { get; } = new GermanOrthographyNotes();

    /// <inheritdoc />
    public SampleWords Samples { get; } = new("schwert", "haben", "feuer", "habe", "hat", "ich habe zwei Schwerter", "conj.person");

    /// <inheritdoc />
    public LexiconProfile LexiconProfile => Profile;

    /// <inheritdoc />
    public ErrorTagSet ErrorTags => Tags;

    /// <inheritdoc />
    public IMorphology CreateMorphology(Lexicon lexicon) => new GermanMorphology(lexicon);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always, until M39 writes the German analysis rules.</exception>
    public IAnalysisRules CreateAnalysisRules(Lexicon lexicon, IMorphology morphology) => throw GermanPlaceholders.NotYet("analysis rules", "M39");

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always, until M40 writes the German template functions.</exception>
    public ITemplateFunctions CreateTemplateFunctions(Lexicon lexicon, IMorphology morphology) => throw GermanPlaceholders.NotYet("template functions", "M40");

    private sealed class GermanNumberWords : INumberWords
    {
        public int MaxValue => GermanNumbers.MaxValue;

        public string ToWords(int value) => GermanNumbers.ToWords(value);

        public bool TryParse(IReadOnlyList<string> tokens, out int value) => GermanNumbers.TryParse(tokens, out value);

        public bool TryParse(string text, out int value) => GermanNumbers.TryParse(text, out value);

        public int TryParsePrefix(IReadOnlyList<string> tokens, int start, out int value) => GermanNumbers.TryParsePrefix(tokens, start, out value);

        public string DigitsToWords(string text) => GermanNumbers.DigitsToWords(text);

        public string Agree(string words, string gender) => GermanNumbers.Agree(words, gender);

        public string Agree(string words, string gender, string? grammaticalCase) => GermanNumbers.Agree(words, gender, grammaticalCase);
    }
}
