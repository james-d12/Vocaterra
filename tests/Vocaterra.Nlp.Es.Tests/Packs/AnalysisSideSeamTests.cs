using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Es.Tests.Analysis;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.SlotGrammar;
using Vocaterra.Nlp.Response.Templates;
using Lex = Vocaterra.Nlp.Language.Lexicon.Lexicon;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Es.Tests.Packs;

/// <summary>
/// The seams of M34: the analysis side of the language goes through the pack. Each test gives the engine a pack that changes one member and shows that the
/// result follows the pack, and not the Spanish code behind it; the Spanish members are shown to be what they were.
/// </summary>
public class AnalysisSideSeamTests
{
    private static readonly ILanguagePack Spanish = SpanishLanguagePack.Instance;

    /// <summary>The Spanish pack with the analysis-side members replaceable; everything else is Spanish.</summary>
    private sealed class PackWith : ILanguagePack
    {
        public ILexiconFolding? Fold { get; init; }

        public LexiconProfile? Profile { get; init; }

        public ErrorTagSet? Tags { get; init; }

        public RepairLines? Repair { get; init; }

        public Func<Lex, IMorphology, IAnalysisRules>? Rules { get; init; }

        public Func<Lex, IMorphology, ITemplateFunctions>? Functions { get; init; }

        public Func<Lex, IMorphology>? Morphology { get; init; }

        public string LanguageId => "es";

        public ITextNormalizer Normalizer => Spanish.Normalizer;

        public ILexiconFolding Folding => Fold ?? Spanish.Folding;

        public INumberWords Numbers => Spanish.Numbers;

        public IPhoneticKey PhoneticKey => Spanish.PhoneticKey;

        public IPunctuation Punctuation => Spanish.Punctuation;

        public IG2p? G2p => Spanish.G2p;

        public GrammarDefaults GrammarDefaults => Spanish.GrammarDefaults;

        public LintVocabulary Lint => Spanish.Lint;

        public RepairLines RepairFallback => Repair ?? Spanish.RepairFallback;

        public IRecastPolicy Recast => Spanish.Recast;

        public SampleWords Samples => Spanish.Samples;

        public LexiconProfile LexiconProfile => Profile ?? Spanish.LexiconProfile;

        public ErrorTagSet ErrorTags => Tags ?? Spanish.ErrorTags;

        public IMorphology CreateMorphology(Lex lexicon) => Morphology is null ? Spanish.CreateMorphology(lexicon) : Morphology(lexicon);

        public IAnalysisRules CreateAnalysisRules(Lex lexicon, IMorphology morphology) =>
            Rules is null ? Spanish.CreateAnalysisRules(lexicon, morphology) : Rules(lexicon, morphology);

        public ITemplateFunctions CreateTemplateFunctions(Lex lexicon, IMorphology morphology) =>
            Functions is null ? Spanish.CreateTemplateFunctions(lexicon, morphology) : Functions(lexicon, morphology);
    }

    /// <summary>Spanish rules with the subject pronouns taken away.</summary>
    private sealed class NoSubjectRules(IAnalysisRules inner) : IAnalysisRules
    {
        public bool IsDeterminerAdjective(string lemma) => inner.IsDeterminerAdjective(lemma);

        public bool IsSubjectPronoun(string lemma) => false;

        public bool IsClitic(string lemma) => inner.IsClitic(lemma);

        public bool IsPreverbalPronoun(string lemma) => inner.IsPreverbalPronoun(lemma);

        public bool IsVerbSeekingQuestionWord(string word) => inner.IsVerbSeekingQuestionWord(word);

        public bool IsNounModifyingAdverb(string lemma) => inner.IsNounModifyingAdverb(lemma);

        public bool IsInterrogativeQuantifier(string lemma) => inner.IsInterrogativeQuantifier(lemma);

        public bool IsInterjectionFirst(string word) => inner.IsInterjectionFirst(word);

        public bool IsNearFutureAuxiliary(string lemma) => inner.IsNearFutureAuxiliary(lemma);

        public bool IsNearFutureLinker(string word) => inner.IsNearFutureLinker(word);

        public bool IsCopula(string lemma) => inner.IsCopula(lemma);

        public bool IsCopulaOrAuxiliary(string lemma) => inner.IsCopulaOrAuxiliary(lemma);

        public bool IsElapsedTimePreposition(string lemma) => inner.IsElapsedTimePreposition(lemma);

        public bool IsElapsedTimeNoun(string lemma) => inner.IsElapsedTimeNoun(lemma);

        public IAgreementChecker CreateAgreementChecker(IReadOnlyList<AnalyzedToken> resolved, List<ErrorTag> errors) => inner.CreateAgreementChecker(resolved, errors);
    }

    /// <summary>A morphology that is not the Spanish generator: it forwards to one.</summary>
    private sealed class WrappedMorphology(IMorphology inner) : IMorphology
    {
        public string Pluralize(string noun) => inner.Pluralize(noun);

        public string Singularize(string noun) => inner.Singularize(noun);

        public string InflectNoun(string lemma, string number) => inner.InflectNoun(lemma, number);

        public string Article(Definiteness definiteness, string gender, string number) => inner.Article(definiteness, gender, number);

        public string Article(Definiteness definiteness, string gender, string number, string nounLemma) => inner.Article(definiteness, gender, number, nounLemma);

        public string AgreeAdjective(string lemma, string gender, string number) => inner.AgreeAdjective(lemma, gender, number);

        public string? Conjugate(string lemma, string person, string number, string tense) => inner.Conjugate(lemma, person, number, tense);

        public bool IsRegularizedIrregular(string form, out string lemma, out string expected) => inner.IsRegularizedIrregular(form, out lemma, out expected);

        public bool TryGetRegularization(string form, out RegularizedForm result) => inner.TryGetRegularization(form, out result);

        public bool TryGetRegularizationOfWord(string form, out RegularizedForm result) => inner.TryGetRegularizationOfWord(form, out result);

        public bool TryGetPluralSlip(string form, out PluralSlip result) => inner.TryGetPluralSlip(form, out result);
    }

    /// <summary>Template functions that mark every article.</summary>
    private sealed class MarkedFunctions(ITemplateFunctions inner) : ITemplateFunctions
    {
        public string ResolveVerbLemma(string value) => inner.ResolveVerbLemma(value);

        public string? Conjugate(string lemma, string person, string number, string tense) => inner.Conjugate(lemma, person, number, tense);

        public string InflectNoun(string word, string number) => inner.InflectNoun(word, number);

        public string InferNumber(string word) => inner.InferNumber(word);

        public string GenderOf(string singular) => inner.GenderOf(singular);

        public string Article(Definiteness definiteness, string gender, string number, string singular) => "[" + inner.Article(definiteness, gender, number, singular) + "]";
    }

    /// <summary>The Spanish folding that also folds the letter n-tilde in the lenient key, to show that the lexicon reads the folding it is given.</summary>
    private sealed class TildeFolding : ILexiconFolding
    {
        public string Key(string text) => Spanish.Folding.Key(text);

        public string LenientKey(string text) => Spanish.Folding.LenientKey(text).Replace('ñ', 'n');

        public string MatchingKey(string text) => Spanish.Folding.MatchingKey(text);
    }

    private static IReadOnlyList<AnalyzedToken> Tokens(string text) => Fixtures.Analyzer.Analyze(TestTokens.From(text)).Tokens;

    // ---- LexiconProfile ------------------------------------------------------------------------------------------------------------

    private const string NeuterJson = """{"schemaVersion":1,"entries":[{"form":"das","lemma":"das","pos":"art","features":{"gender":"n"}}]}""";

    private const string CaseJson = """{"schemaVersion":1,"entries":[{"form":"der","lemma":"der","pos":"art","features":{"gender":"m","case":"nom","register":"formal"}}]}""";

    [Fact]
    public void TheSpanishProfileIsTheSchemaV1_AndStillRefusesTheNeuterGender()
    {
        var ex = Assert.Throws<FormatException>(() => Lex.ReadFile(NeuterJson, Spanish.LexiconProfile));

        Assert.Contains("unknown gender 'n'", ex.Message);
        Assert.Same(LexiconProfile.V1, Spanish.LexiconProfile);
        Assert.Equal(LexiconSchema.Genders, LexiconProfile.V1.Genders);
        Assert.Equal(LexiconSchema.Tenses, LexiconProfile.V1.Tenses);
        Assert.Empty(LexiconProfile.V1.Cases);
        Assert.Throws<FormatException>(() => Lex.ReadFile(NeuterJson));
    }

    [Fact]
    public void AProfileWithTheNeuterGenderAcceptsAFileThatTheSpanishProfileRefuses()
    {
        var german = new LexiconProfile(["m", "f", "n"], LexiconSchema.Numbers, LexiconSchema.Persons, LexiconSchema.Tenses, LexiconSchema.Moods);

        var file = Lex.ReadFile(NeuterJson, german);

        Assert.Equal("n", Assert.Single(file.Entries).Features.Gender);
    }

    [Fact]
    public void TheOptionalFeaturesAreAcceptedOnlyWhereTheProfileListsThem()
    {
        var withCases = new LexiconProfile(["m", "f"], ["sg", "pl"], ["1", "2", "3"], ["pres"], ["ind"], cases: ["nom", "acc"], registers: ["formal"]);

        var ex = Assert.Throws<FormatException>(() => Lex.ReadFile(CaseJson, Spanish.LexiconProfile));
        var entry = Assert.Single(Lex.ReadFile(CaseJson, withCases).Entries);

        Assert.Contains("feature 'case' is not used by this language", ex.Message);
        Assert.Equal("nom", entry.Features.Case);
        Assert.Equal("formal", entry.Features.Register);
        Assert.Equal("nom", entry.ToFeatures().Case);
        Assert.Throws<FormatException>(() => Lex.ReadFile(CaseJson.Replace("\"nom\"", "\"gen\""), withCases));
    }

    [Fact]
    public void ALexiconWithOptionalFeaturesWritesThemBackAndOneWithoutDoesNotChange()
    {
        var withCases = new LexiconProfile(["m", "f"], ["sg", "pl"], ["1", "2", "3"], ["pres"], ["ind"], cases: ["nom"], registers: ["formal"]);
        var german = Lex.Load(CaseJson, Spanish.Folding, withCases);

        var plain = SpanishLexicon.Seed.ToJson();

        Assert.Contains("\"case\":\"nom\"", german.ToJson());
        Assert.DoesNotContain("\"case\"", plain);
        Assert.DoesNotContain("\"verbForm\"", plain);
        Assert.DoesNotContain("\"register\"", plain);
        Assert.Equal(plain, SpanishLexicon.Load(plain).ToJson());
    }

    [Fact]
    public void TheLoaderValidatesTheLexiconAgainstThePacksProfile()
    {
        // The game bundle uses the moods ind, subj and imp; a pack whose profile lists only ind refuses the bundle with the pack's profile, not the Spanish one.
        var narrow = new PackWith { Profile = new LexiconProfile(["m", "f"], ["sg", "pl"], ["1", "2", "3"], LexiconSchema.Tenses, ["ind"]) };

        var ex = Assert.Throws<ContentValidationException>(
            () => ContentLoader.Load(TestSupport.BundleDir, new ContentLoadOptions { LanguagePacks = LanguagePacks.Of(narrow) }));

        Assert.Contains(ex.Problems, static problem => problem.Contains("unknown mood", StringComparison.Ordinal));
    }

    // ---- The lexicon's fold --------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheLexiconBuildsItsIndexesWithTheFoldingItIsGiven()
    {
        LexiconEntry[] entries = [new("año", "año", "noun", new EntryFeatures(null, "sg", null, null, "m"))];

        var spanish = SpanishLexicon.FromEntries(entries);
        var tilde = Lex.FromEntries(entries, new TildeFolding(), LexiconProfile.V1);

        Assert.Empty(spanish.Lookup("ano"));
        Assert.Single(tilde.Lookup("ano"));
        Assert.True(tilde.Lookup("ano").IsAccentInsensitive);
        Assert.Equal(["año"], tilde.SpellingsIgnoringAccents("ano"));
        Assert.IsType<TildeFolding>(tilde.Folding);
        Assert.Same(Spanish.Folding, spanish.Folding);
    }

    [Fact]
    public void TheLoadedBundleLexiconUsesThePacksFolding()
    {
        var pack = new PackWith { Fold = new TildeFolding() };

        var bundle = ContentLoader.Load(TestSupport.BundleDir, new ContentLoadOptions { LanguagePacks = LanguagePacks.Of(pack) });

        Assert.IsType<TildeFolding>(bundle.Lexicon.Folding);
        Assert.Same(Spanish.Folding, TestSupport.Bundle.Lexicon.Folding);
    }

    [Fact]
    public void AGrammarAndACatalogFoldTheirLiteralsWithTheFoldingGiven()
    {
        var upper = new UpperKeyFolding();

        var spanish = SlotGrammarType.Parse("quiero [NOUN:item]");
        var custom = SlotGrammarType.Parse("quiero [NOUN:item]", null, upper);

        Assert.Equal("quiero [NOUN:item]", spanish.Pattern);
        Assert.Equal("QUIERO [NOUN:item]", custom.Pattern);
    }

    private sealed class UpperKeyFolding : ILexiconFolding
    {
        public string Key(string text) => text.ToUpperInvariant();

        public string LenientKey(string text) => text.ToUpperInvariant();

        public string MatchingKey(string text) => text;
    }

    // ---- LexicalFeatures, NormalizedText ---------------------------------------------------------------------------------------------

    [Fact]
    public void TheNewFeatureMembersAreNullForSpanishAndLeaveTheJsonAndEqualityAlone()
    {
        var reading = new LexicalFeatures("espada", "noun", null, "sg", null, null, "f");

        var json = CanonicalJson.Serialize(reading);

        Assert.Equal("""{"lemma":"espada","pos":"noun","person":null,"number":"sg","tense":null,"mood":null,"gender":"f"}""", json);
        Assert.Equal(reading, CanonicalJson.Deserialize<LexicalFeatures>(json));
        Assert.Equal(reading, new LexicalFeatures("espada", "noun", null, "sg", null, null, "f", null, null, null, null, null));
        Assert.Equal(reading.GetHashCode(), new LexicalFeatures("espada", "noun", null, "sg", null, null, "f").GetHashCode());
        Assert.All(SpanishLexicon.Seed.Entries.Take(2000), static entry => Assert.Null(entry.ToFeatures().Case));
    }

    [Fact]
    public void AReadingWithACaseDiffersAndSerializesTheMember()
    {
        var plain = new LexicalFeatures("Tisch", "noun", null, "sg", null, null, "m");
        var nominative = plain with { Case = "nom" };

        Assert.NotEqual(plain, nominative);
        Assert.Contains("\"case\":\"nom\"", CanonicalJson.Serialize(nominative));
        Assert.Equal(nominative, CanonicalJson.Deserialize<LexicalFeatures>(CanonicalJson.Serialize(nominative)));
    }

    [Theory]
    [InlineData("¿Dónde está el río?", "?")]
    [InlineData("¡Hola!", "!")]
    [InlineData("Hola", null)]
    [InlineData("¿Qué?  ", "?")]
    [InlineData("«¿Qué?»", "?")]
    [InlineData("Hola. ¿Qué tal? Bien", null)]
    [InlineData("", null)]
    public void TheFinalMarkIsCapturedOnlyOnRequest(string input, string? mark)
    {
        var plain = SpanishTextNormalizer.Normalize(input);
        var captured = SpanishTextNormalizer.Normalize(input, new NormalizationOptions(CaptureFinalMark: true));

        Assert.Null(plain.FinalMark);
        Assert.Equal(mark, captured.FinalMark);
        Assert.Equal(plain.Text, captured.Text);
        Assert.Equal(plain.Tokens, captured.Tokens);
    }

    [Fact]
    public void TheFinalMarkDoesNotChangeTheJsonOrTheHashOfTextWithoutOne()
    {
        var plain = SpanishTextNormalizer.Normalize("¿Dónde está el río?");
        var captured = SpanishTextNormalizer.Normalize("¿Dónde está el río?", new NormalizationOptions(CaptureFinalMark: true));

        Assert.DoesNotContain("finalMark", CanonicalJson.Serialize(plain));
        Assert.Contains("\"finalMark\":\"?\"", CanonicalJson.Serialize(captured));
        Assert.NotEqual(plain, captured);
        Assert.Equal(plain, captured with { FinalMark = null });
        Assert.Equal(plain.GetHashCode(), (captured with { FinalMark = null }).GetHashCode());
        Assert.Equal(CanonicalJson.Hash(plain), CanonicalJson.Hash(new NormalizedText(plain.Text, plain.Tokens)));
    }

    // ---- ErrorTagSet -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheSpanishErrorTagsAreTheFrozenV1SetInItsOrder()
    {
        Assert.Same(ErrorTagSet.V1, Spanish.ErrorTags);
        Assert.Equal(ErrorTags.All, Spanish.ErrorTags.All);
        for (var i = 0; i < ErrorTags.All.Count; i++)
        {
            Assert.Equal(i, Spanish.ErrorTags.Rank(ErrorTags.All[i]));
        }

        Assert.Equal(ErrorTags.All.Count, Spanish.ErrorTags.Rank("tag.of.another.language"));
        Assert.True(Spanish.ErrorTags.IsKnown(ErrorTags.NounNumber));
        Assert.False(Spanish.ErrorTags.IsKnown("case.agree"));
        Assert.True(Spanish.ErrorTags.IsPronunciation(ErrorTags.PronR));
    }

    [Fact]
    public void AnErrorTagSetRefusesARepeatedOrEmptyTag()
    {
        Assert.Throws<ArgumentException>(() => new ErrorTagSet(["a", "a"]));
        Assert.Throws<ArgumentException>(() => new ErrorTagSet(["a", " "]));
        Assert.Equal(1, new ErrorTagSet(["case.agree", "gender.agree"]).Rank("gender.agree"));
    }

    [Fact]
    public void TheAnalyzerOrdersErrorsByThePacksTagRanks()
    {
        // Two errors at the same position sort by rank. Reversing the v1 set reverses their order.
        var reversed = new PackWith { Tags = new ErrorTagSet(ErrorTags.All.Reverse()) };
        var lexicon = SpanishLexicon.Seed;
        var text = TestTokens.From("yo quiere dos espada");
        var spanishErrors = new LinguisticAnalyzer(lexicon, Spanish).Analyze(text).Errors;
        var reversedErrors = new LinguisticAnalyzer(lexicon, reversed).Analyze(text).Errors;

        Assert.Equal(spanishErrors.Select(static e => e.Tag).Order(StringComparer.Ordinal), reversedErrors.Select(static e => e.Tag).Order(StringComparer.Ordinal));
        Assert.NotEmpty(spanishErrors);
    }

    // ---- IAnalysisRules ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheTypedMatchingReadsTheWordClassesOfTheRulesItIsGiven()
    {
        var grammar = SlotGrammarType.Parse("[VERB:querer] [NOUN:item]");
        var tokens = Tokens("yo quiero agua");
        var morphology = Spanish.CreateMorphology(SpanishLexicon.Seed);

        // The implicit leading subject is a pronoun of the language's subject pronouns; without them, "yo" is an unmatched word.
        Assert.NotNull(SlotGrammarMatcher.Match(tokens, grammar, Spanish, Spanish.CreateAnalysisRules(SpanishLexicon.Seed, morphology)));
        Assert.Null(SlotGrammarMatcher.Match(tokens, grammar, Spanish, new NoSubjectRules(Spanish.CreateAnalysisRules(SpanishLexicon.Seed, morphology))));
    }

    [Fact]
    public void TheAnalyzerChecksAgreementWithTheCheckerOfTheRules()
    {
        var silent = new PackWith { Rules = (_, morphology) => new SilentRules(Spanish.CreateAnalysisRules(SpanishLexicon.Seed, morphology)) };
        var text = TestTokens.From("yo quiere dos espada");

        var spanish = new LinguisticAnalyzer(SpanishLexicon.Seed, Spanish).Analyze(text).Errors;
        var none = new LinguisticAnalyzer(SpanishLexicon.Seed, silent).Analyze(text).Errors;

        Assert.Contains(spanish, static e => e.Tag == ErrorTags.ConjPerson);
        Assert.DoesNotContain(none, static e => e.Tag == ErrorTags.ConjPerson);
    }

    /// <summary>Spanish rules whose agreement checker finds nothing.</summary>
    private sealed class SilentRules(IAnalysisRules inner) : IAnalysisRules
    {
        private sealed class Nothing : IAgreementChecker
        {
            public void Run()
            {
            }

            public bool HasPastTimeMarker() => false;
        }

        public bool IsDeterminerAdjective(string lemma) => inner.IsDeterminerAdjective(lemma);

        public bool IsSubjectPronoun(string lemma) => inner.IsSubjectPronoun(lemma);

        public bool IsClitic(string lemma) => inner.IsClitic(lemma);

        public bool IsPreverbalPronoun(string lemma) => inner.IsPreverbalPronoun(lemma);

        public bool IsVerbSeekingQuestionWord(string word) => inner.IsVerbSeekingQuestionWord(word);

        public bool IsNounModifyingAdverb(string lemma) => inner.IsNounModifyingAdverb(lemma);

        public bool IsInterrogativeQuantifier(string lemma) => inner.IsInterrogativeQuantifier(lemma);

        public bool IsInterjectionFirst(string word) => inner.IsInterjectionFirst(word);

        public bool IsNearFutureAuxiliary(string lemma) => inner.IsNearFutureAuxiliary(lemma);

        public bool IsNearFutureLinker(string word) => inner.IsNearFutureLinker(word);

        public bool IsCopula(string lemma) => inner.IsCopula(lemma);

        public bool IsCopulaOrAuxiliary(string lemma) => inner.IsCopulaOrAuxiliary(lemma);

        public bool IsElapsedTimePreposition(string lemma) => inner.IsElapsedTimePreposition(lemma);

        public bool IsElapsedTimeNoun(string lemma) => inner.IsElapsedTimeNoun(lemma);

        public IAgreementChecker CreateAgreementChecker(IReadOnlyList<AnalyzedToken> resolved, List<ErrorTag> errors) => new Nothing();
    }

    [Fact]
    public void TheSpanishRulesAnswerTheWordClassesAsTheStaticsDid()
    {
        var rules = Spanish.CreateAnalysisRules(SpanishLexicon.Seed, Spanish.CreateMorphology(SpanishLexicon.Seed));

        Assert.True(rules.IsSubjectPronoun("yo"));
        Assert.False(rules.IsSubjectPronoun("me"));
        Assert.True(rules.IsClitic("me"));
        Assert.True(rules.IsDeterminerAdjective("mucho"));
        Assert.True(rules.IsPreverbalPronoun("se"));
        Assert.True(rules.IsVerbSeekingQuestionWord("cómo"));
        Assert.True(rules.IsNounModifyingAdverb("solo"));
        Assert.True(rules.IsInterrogativeQuantifier("cuánto"));
        Assert.True(rules.IsInterjectionFirst("gracias"));
        Assert.True(rules.IsElapsedTimePreposition("hace"));
        Assert.True(rules.IsElapsedTimeNoun("día"));
        Assert.False(rules.IsElapsedTimeNoun("domingo"));
        Assert.True(rules.IsNearFutureAuxiliary("ir"));
        Assert.True(rules.IsNearFutureLinker("a"));
        Assert.True(rules.IsCopula("estar"));
        Assert.False(rules.IsCopula("haber"));
        Assert.True(rules.IsCopulaOrAuxiliary("haber"));
        Assert.Same(rules.GetType(), AnalysisRules.For(Spanish).GetType());
        Assert.Same(AnalysisRules.For(Spanish), AnalysisRules.For(Spanish));
    }

    // ---- IMorphology and ContentBundle.SpanishMorphology() ------------------------------------------------------------------------------------

    [Fact]
    public void TheSpanishPackCreatesTheMorphologyGeneratorAndTheBundleExposesItBothWays()
    {
        var bundle = TestSupport.Bundle;

        Assert.IsType<MorphologyGenerator>(Spanish.CreateMorphology(SpanishLexicon.Seed));
        Assert.Same(bundle.LanguageMorphology, bundle.SpanishMorphology());
        Assert.IsAssignableFrom<IMorphology>(bundle.SpanishMorphology());
        Assert.Equal("espadas", bundle.LanguageMorphology.Pluralize("espada"));
    }

    [Fact]
    public void ABundleOfAPackThatIsNotSpanishHasNoMorphologyGenerator_ButHasTheNeutralMorphology()
    {
        var pack = new PackWith { Morphology = lexicon => new WrappedMorphology(Spanish.CreateMorphology(lexicon)) };

        var bundle = ContentLoader.Load(TestSupport.BundleDir, new ContentLoadOptions { LanguagePacks = LanguagePacks.Of(pack) });

        Assert.Throws<NotSupportedException>(() => bundle.SpanishMorphology());
        Assert.IsType<WrappedMorphology>(bundle.LanguageMorphology);
        Assert.Equal("espadas", bundle.LanguageMorphology.Pluralize("espada"));
        Assert.Equal(bundle.ContentHash, TestSupport.Bundle.ContentHash);
    }

    [Fact]
    public void TheMorphologyInterfaceAndTheGeneratorAgree()
    {
        IMorphology viaInterface = new MorphologyGenerator(SpanishLexicon.Seed);
        var generator = new MorphologyGenerator(SpanishLexicon.Seed);

        Assert.Equal(generator.Conjugate("tener", "1", "sg", "pres"), viaInterface.Conjugate("tener", "1", "sg", "pres"));
        Assert.True(viaInterface.TryGetRegularization("tení", out var regularized));
        Assert.Equal("tener", regularized.Lemma);
        Assert.True(viaInterface.IsRegularizedIrregular("tení", out var lemma, out var expected));
        Assert.Equal((regularized.Lemma, regularized.Expected), (lemma, expected));
        Assert.Equal("el", viaInterface.Article(Definiteness.Definite, "m", "sg"));
        Assert.Equal("el", viaInterface.Article(Definiteness.Definite, "f", "sg", "agua"));
    }

    // ---- ITemplateFunctions ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheTemplateEngineInflectsThroughTheFunctionsOfThePack()
    {
        var marked = new PackWith { Functions = (_, _) => new MarkedFunctions(Spanish.CreateTemplateFunctions(SpanishLexicon.Seed, Spanish.CreateMorphology(SpanishLexicon.Seed))) };
        var template = new DialogueTemplate("t1", "{item:article(def)}");
        var slots = SlotMap.From([new KeyValuePair<string, string>("item", "espada")]);

        var spanish = new TemplateEngine(SpanishLexicon.Seed, Spanish.CreateMorphology(SpanishLexicon.Seed), Spanish).Render(template, slots);
        var custom = new TemplateEngine(SpanishLexicon.Seed, Spanish.CreateMorphology(SpanishLexicon.Seed), marked).Render(template, slots);

        Assert.Equal("la espada", spanish);
        Assert.Equal("[la] espada", custom);
    }

    [Fact]
    public void TheTemplateTenseIsCheckedAgainstTheProfileOfThePack()
    {
        var presentOnly = new PackWith { Profile = new LexiconProfile(["m", "f"], ["sg", "pl"], ["1", "2", "3"], ["pres"], ["ind"]) };
        var template = new DialogueTemplate("t2", "{verb:conj(1,sg,pret)}");
        var slots = SlotMap.From([new KeyValuePair<string, string>("verb", "tener")]);

        Assert.Equal("tuve", new TemplateEngine(SpanishLexicon.Seed, Spanish.CreateMorphology(SpanishLexicon.Seed), Spanish).Render(template, slots));
        Assert.Throws<TemplateException>(() => new TemplateEngine(SpanishLexicon.Seed, Spanish.CreateMorphology(SpanishLexicon.Seed), presentOnly).Render(template, slots));
    }

    // ---- The offer line -----------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, "¿Quieres decir «hola»?")]
    [InlineData(2, "¿Quieres decir «hola» o «adiós»?")]
    [InlineData(3, "¿Quieres decir «hola», «adiós» o «gracias»?")]
    public void TheSpanishOfferLineIsTheTextItAlwaysWas(int count, string expected)
    {
        var choices = new[] { "hola", "adiós", "gracias" }.Take(count).ToArray();

        var step = RepairStrategy.Next(3, Prompt(), choices, choices, Spanish.RepairFallback);

        Assert.Equal(expected, step.AgentLine);
        Assert.Equal(expected, Spanish.RepairFallback.Offer!.Format(choices));
    }

    [Fact]
    public void ThePackOffersChoicesWithItsOwnWording()
    {
        var english = new OfferLineFormat("Do you mean ", "\"", "\"", ", ", " or ", "?");
        var lines = new RepairLines("a", "b", "c", english);

        var step = RepairStrategy.Next(3, Prompt(), ["hello", "goodbye"], ["hello", "goodbye"], lines);

        Assert.Equal("Do you mean \"hello\" or \"goodbye\"?", step.AgentLine);
        Assert.Equal(RepairKind.OfferChoices, step.Kind);
    }

    [Fact]
    public void RepairLinesWithoutAnOfferFormatUseTheEnginesPlainWording()
    {
        var step = RepairStrategy.Next(3, Prompt(), ["hola", "adiós"], ["hola", "adiós"], new RepairLines("a", "b", "c"));

        Assert.Equal("\"hola\", \"adiós\"", step.AgentLine);
    }

    private static PromptSpec Prompt() => new("p1", RecognitionMode.Open, Difficulty.Beginner, "answers", [], null, []);
}
