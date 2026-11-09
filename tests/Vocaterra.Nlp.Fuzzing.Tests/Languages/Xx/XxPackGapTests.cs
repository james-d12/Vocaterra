using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;
using Vocaterra.Nlp.Response.Templates;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Fuzzing.Tests.Languages.Xx;

/// <summary>
/// M88: every new pack member (case on articles and number agreement, polite positions, V2, capitals in recasts, <c>decl()</c> and <c>particle()</c>) is exercised
/// through the toy pack, with no Spanish registered.
/// </summary>
public sealed class XxPackGapTests
{
    private static string Render(ContentBundle bundle, ILanguagePack language, string text, params (string Name, string Value)[] slots)
    {
        var engine = new TemplateEngine(bundle.Lexicon, bundle.LanguageMorphology, language);
        return engine.Render(new DialogueTemplate("t", text), SlotMap.From([.. slots.Select(static s => new KeyValuePair<string, string>(s.Name, s.Value))]));
    }

    private static IReadOnlyList<AnalyzedToken> Tokens(ContentBundle bundle, ILanguagePack language, string text) =>
        new LinguisticAnalyzer(bundle.Lexicon, bundle.LanguageMorphology, language, bundle.AnalysisRules).Analyze(language.Normalizer.Normalize(text)).Tokens;

    // ---- gaps 1 and 2 -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ArticlesTakeACaseThroughTheMorphologyAndTheTemplateFunctions()
    {
        var morphology = new XxMorphology();
        ITemplateFunctions functions = new XxTemplateFunctions();

        Assert.Equal("the", morphology.Article(Definiteness.Definite, "", "sg", "ka", null));
        Assert.Equal("the.acc", morphology.Article(Definiteness.Definite, "", "sg", "ka", "acc"));
        Assert.Equal("a.nom", functions.Article(Definiteness.Indefinite, "", "sg", "ka", "nom"));
        Assert.Equal("a", functions.Article(Definiteness.Indefinite, "", "sg", "ka", null));
    }

    [Fact]
    public void NumberAgreementTakesACase()
    {
        var numbers = XxLanguagePack.Instance.Numbers;

        Assert.Equal("ko", numbers.Agree("ko", "m", null));
        Assert.Equal("ko.acc", numbers.Agree("ko", "m", "acc"));
    }

    [Fact]
    public void TheTemplateSyntaxParsesACaseAndChecksItAgainstThePacksCases()
    {
        using var _ = new TempBundle(out var bundle);
        var language = XxLanguagePack.Instance;

        Assert.Equal("the.acc ka", Render(bundle, language, "{item:article(def,acc)}", ("item", "ka")));
        Assert.Equal("the ka", Render(bundle, language, "{item:article(def)}", ("item", "ka")));
        Assert.Equal("ko.nom", Render(bundle, language, "{qty:words(m,nom)}", ("qty", "1")));
        Assert.Equal("ko", Render(bundle, language, "{qty:words(m)}", ("qty", "1")));
        var unknown = Assert.Throws<TemplateException>(() => Render(bundle, language, "{item:article(def,dat)}", ("item", "ka")));
        Assert.Contains("nom, acc", unknown.Message, StringComparison.Ordinal);
        Assert.Throws<TemplateException>(() => Render(bundle, language, "{item:article(def,acc,nom)}", ("item", "ka")));
    }

    // ---- gap 3 ------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(PolitePositions.Trailing, "mo ka ti", true)]
    [InlineData(PolitePositions.Trailing, "ti mo ka", false)]
    [InlineData(PolitePositions.Trailing, "mo ti ka", false)]
    [InlineData(PolitePositions.Leading, "ti mo ka", true)]
    [InlineData(PolitePositions.Leading, "mo ka ti", false)]
    [InlineData(PolitePositions.AfterFiniteVerb, "mo ti ka", true)]
    [InlineData(PolitePositions.AfterFiniteVerb, "ti mo ka", false)]
    [InlineData(PolitePositions.AfterFiniteVerb, "mo ka ti", false)]
    [InlineData(PolitePositions.Trailing | PolitePositions.Leading | PolitePositions.AfterFiniteVerb, "mo ti ka", true)]
    [InlineData(PolitePositions.None, "mo ka ti", false)]
    public void ThePolitePhraseIsAllowedWhereThePackSaysAndNowhereElse(PolitePositions positions, string text, bool matches)
    {
        using var _ = new TempBundle(out var bundle);
        var language = new VariantPack(polite: new GrammarDefaults(false, [], ["ti"], positions));
        var grammar = SlotGrammar.Parse("[VERB:mo] [NOUN]");

        var match = SlotGrammarMatcher.Match(Tokens(bundle, language, text), grammar, language);

        Assert.Equal(matches, match is not null);
        Assert.True(match is null || match.Grammar == grammar);
    }

    [Fact]
    public void ThePoliteBoundCountsEachPositionOnce()
    {
        var defaults = new GrammarDefaults(false, [], ["ti"], PolitePositions.Leading | PolitePositions.AfterFiniteVerb);

        Assert.Equal(2, defaults.MaxPoliteTokens);
        Assert.Equal(0, XxLanguagePack.Instance.GrammarDefaults.MaxPoliteTokens);
    }

    [Fact]
    public void AnIntentWithOrderV2LoadsWhenThePackDeclaresV2Support()
    {
        const string Json = """{"schemaVersion":1,"intents":[{"intentId":"take","order":"v2","slotGrammars":["mo [NOUN]"]}]}""";
        var pack = XxLanguagePack.Instance;

        var catalog = IntentCatalog.Load(Json, pack.Folding, pack.SupportsV2Order);

        Assert.True(pack.SupportsV2Order);
        Assert.Equal("v2", catalog.Find("take")!.Order);
    }

    // ---- gap 4 ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARecastPolicyRestoresCapitalsFromTheResolvedReadingAndSpanishTheToyPackKeepsTodaysOutput()
    {
        using var _ = new TempBundle(out var bundle);
        var plain = XxLanguagePack.Instance;
        var capitals = new VariantPack(recast: new CapitalNouns());
        var text = plain.Normalizer.Normalize("mo ka");

        var analysis = new LinguisticAnalyzer(bundle.Lexicon, bundle.LanguageMorphology, plain, bundle.AnalysisRules).Analyze(text);

        Assert.Equal("mo ka", RecastBuilder.Build(text, analysis, bundle.Lexicon, plain));
        Assert.Empty(plain.Recast.RestoredCapitals(text, analysis, bundle.Lexicon));
        Assert.Equal("mo Ka", RecastBuilder.Build(text, analysis, bundle.Lexicon, capitals));
    }

    [Fact]
    public void ThePackCanOptIntoTheCapitalTagAndTheToyPackDoesNot()
    {
        var optedIn = new ErrorTagSet([.. XxLanguagePack.Instance.ErrorTags.All, ErrorTags.OrthCapital]);

        Assert.True(optedIn.IsKnown(ErrorTags.OrthCapital));
        Assert.Equal(3, optedIn.Rank(ErrorTags.OrthCapital));
        Assert.False(XxLanguagePack.Instance.ErrorTags.IsKnown(ErrorTags.OrthCapital));
    }

    // ---- gap 5 ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void DeclAndParticleRunThroughTheToyFunctions()
    {
        using var _ = new TempBundle(out var bundle);
        var language = XxLanguagePack.Instance;

        Assert.Equal("ka-sg.acc", Render(bundle, language, "{adj:decl(m,sg,acc)}", ("adj", "ka")));
        Assert.Equal("ka-pl.nom.def", Render(bundle, language, "{adj:decl(f,pl,nom,def)}", ("adj", "ka")));
        Assert.Equal("an", Render(bundle, language, "{verb:particle}", ("verb", "an+mo")));
        Assert.Equal(string.Empty, Render(bundle, language, "x{verb:particle}", ("verb", "mo")).Substring(1));
        Assert.Throws<TemplateException>(() => Render(bundle, language, "{adj:decl(m,sg,dat)}", ("adj", "ka")));
        Assert.Throws<TemplateException>(() => Render(bundle, language, "{adj:decl(m,sg)}", ("adj", "ka")));
    }

    [Fact]
    public void ATemplateFunctionsWithoutDeclOrParticleReportsNotSupportedAtTemplateTime()
    {
        using var _ = new TempBundle(out var bundle);
        var language = new VariantPack(functions: new BareFunctions());

        var decl = Assert.Throws<TemplateException>(() => Render(bundle, language, "{adj:decl(m,sg,nom)}", ("adj", "ka")));
        var particle = Assert.Throws<TemplateException>(() => Render(bundle, language, "{verb:particle}", ("verb", "mo")));

        Assert.Contains("not supported by this language", decl.Message, StringComparison.Ordinal);
        Assert.Contains("not supported by this language", particle.Message, StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => ((ITemplateFunctions)new BareFunctions()).DeclineAdjective("ka", "m", "sg", "nom", null));
    }

    [Fact]
    public void ThePackWithNoCasesRejectsACaseArgument()
    {
        using var _ = new TempBundle(out var bundle);
        var language = new VariantPack(cases: []);

        var ex = Assert.Throws<TemplateException>(() => Render(bundle, language, "{item:article(def,acc)}", ("item", "ka")));

        Assert.Contains("no grammatical case", ex.Message, StringComparison.Ordinal);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------------------------------

    private sealed class TempBundle : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "vocaterra-xx-gap-" + Guid.NewGuid().ToString("N"));

        public TempBundle(out ContentBundle bundle)
        {
            XxBundle.WriteTo(_directory);
            bundle = XxBundle.Load(_directory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }

    /// <summary>Template functions that implement only the required members: decl() and particle() keep their defaults.</summary>
    private sealed class BareFunctions : ITemplateFunctions
    {
        public string ResolveVerbLemma(string value) => value;

        public string? Conjugate(string lemma, string person, string number, string tense) => null;

        public string InflectNoun(string word, string number) => word;

        public string InferNumber(string word) => "sg";

        public string GenderOf(string singular) => string.Empty;

        public string Article(Definiteness definiteness, string gender, string number, string singular) => "the";
    }

    /// <summary>Capitalizes each token that the resolved reading says is a noun (the first letter of its original spelling).</summary>
    private sealed class CapitalNouns : IRecastPolicy
    {
        public IReadOnlyDictionary<int, string> RestoredSpellings(NormalizedText text, AnalysisResult analysis, NlpLexicon lexicon) => new Dictionary<int, string>();

        public IReadOnlyDictionary<int, string> RestoredCapitals(NormalizedText text, AnalysisResult analysis, NlpLexicon lexicon)
        {
            var result = new Dictionary<int, string>();
            for (var i = 0; i < analysis.Tokens.Count; i++)
            {
                var token = analysis.Tokens[i];
                if (token.Readings.Count > 0 && token.Readings[0].Pos == LexiconSchema.Noun)
                {
                    result[token.Token.Span.Start] = char.ToUpperInvariant(token.Token.Original[0]) + token.Token.Original[1..];
                }
            }

            return result;
        }
    }

    /// <summary>The toy pack with the polite defaults, the recast policy, the template functions or the cases replaced.</summary>
    private sealed class VariantPack(GrammarDefaults? polite = null, IRecastPolicy? recast = null, ITemplateFunctions? functions = null, IReadOnlyList<string>? cases = null) : ILanguagePack
    {
        private static readonly XxLanguagePack Toy = XxLanguagePack.Instance;

        public string LanguageId => Toy.LanguageId;

        public ITextNormalizer Normalizer => Toy.Normalizer;

        public ILexiconFolding Folding => Toy.Folding;

        public INumberWords Numbers => Toy.Numbers;

        public IPhoneticKey PhoneticKey => Toy.PhoneticKey;

        public IPunctuation Punctuation => Toy.Punctuation;

        public IG2p? G2p => Toy.G2p;

        public GrammarDefaults GrammarDefaults => polite ?? Toy.GrammarDefaults;

        public IReadOnlyList<string> GrammaticalCases => cases ?? Toy.GrammaticalCases;

        public LintVocabulary Lint => Toy.Lint;

        public RepairLines RepairFallback => Toy.RepairFallback;

        public IRecastPolicy Recast => recast ?? Toy.Recast;

        public IOrthographyNotes Orthography => Toy.Orthography;

        public SampleWords Samples => Toy.Samples;

        public LexiconProfile LexiconProfile => Toy.LexiconProfile;

        public ErrorTagSet ErrorTags => Toy.ErrorTags;

        public IMorphology CreateMorphology(NlpLexicon lexicon) => Toy.CreateMorphology(lexicon);

        public IAnalysisRules CreateAnalysisRules(NlpLexicon lexicon, IMorphology morphology) => Toy.CreateAnalysisRules(lexicon, morphology);

        public ITemplateFunctions CreateTemplateFunctions(NlpLexicon lexicon, IMorphology morphology) => functions ?? Toy.CreateTemplateFunctions(lexicon, morphology);
    }
}
