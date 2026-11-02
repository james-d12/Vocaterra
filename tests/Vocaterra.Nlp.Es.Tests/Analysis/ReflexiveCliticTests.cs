namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M59 (b), found by the game's V12: a reflexive clitic was not matched to its verb's person, so <c>cómo te llama</c> and <c>cómo se llamas</c> gave nothing. In the
/// question <c>cómo</c> + clitic + <c>llamar</c> or <c>sentir</c> the clitic is reflexive and the verb takes its person (<c>conj.person</c>). Each mistake gave no error
/// before M59; the clean neighbours are tests as well. Runs on the lexicon of the bundle.
/// </summary>
public class ReflexiveCliticTests
{
    // The game's grammars of ask_name, so that the verb is bound as it is at the smith's prompt.
    private const string AskNameTu = "cómo te [VERB:llamar] (tú)?";
    private const string AskNameUsted = "cómo se [VERB:llamar] (usted|él|ella)";

    private static readonly Lazy<LinguisticAnalyzer> Analyzer = new(BuildAnalyzer);

    private static LinguisticAnalyzer BuildAnalyzer()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "content", "lexicon");
        var entries = new List<LexiconEntry>();
        foreach (var path in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            entries.AddRange(Lexicon.ReadFile(File.ReadAllBytes(path)).Entries);
        }

        return new LinguisticAnalyzer(SpanishLexicon.FromEntries(entries), SpanishLanguagePack.Instance);
    }

    private static AnalysisResult Analyze(string sentence, params string[] grammars)
    {
        return Analyzer.Value.Analyze(TestTokens.From(sentence), grammars.Select(static g => SlotGrammar.Parse(g)).ToList());
    }

    private static string Described(string sentence, params string[] grammars)
    {
        return string.Join(';', Analyze(sentence, grammars).Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}"));
    }

    [Theory]
    [InlineData("cómo te llama", "conj.person|llama|llamas")]
    [InlineData("como te llama", "conj.person|llama|llamas")]
    [InlineData("cómo te llama tú", "conj.person|llama|llamas")]
    [InlineData("cómo se llamas", "conj.person|llamas|llama")]
    [InlineData("cómo se llamo", "conj.person|llamo|llama")]
    [InlineData("cómo se llamamos", "conj.person|llamamos|llaman")]
    [InlineData("cómo me llama", "conj.person|llama|llamo")]
    [InlineData("cómo nos llaman", "conj.person|llaman|llamamos")]
    [InlineData("cómo te siente", "conj.person|siente|sientes")]
    [InlineData("cómo se sientes", "conj.person|sientes|siente")]
    public void AReflexiveCliticAfterComoAgreesWithItsVerb(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Fact]
    public void TheNameQuestionIsCheckedWithTheGamesGrammars()
    {
        Assert.Equal("conj.person|llama|llamas", Described("cómo te llama", AskNameTu, AskNameUsted));
        Assert.Equal("conj.person|llamas|llama", Described("cómo se llamas", AskNameTu, AskNameUsted));
        Assert.Equal(string.Empty, Described("cómo te llamas", AskNameTu, AskNameUsted));
        Assert.Equal(string.Empty, Described("cómo se llama usted", AskNameTu, AskNameUsted));
    }

    // llamas is also the plural of the noun llama: after cómo and its clitic the reading resolver takes the verb (M59), which the check needs.
    [Fact]
    public void TheVerbAfterComoAndACliticIsReadAsTheVerb()
    {
        var words = Analyze("cómo se llamas").Words!;
        Assert.Equal(LexiconSchema.Verb, words[2].Reading!.Pos);
        Assert.Equal("llamar", words[2].Reading!.Lemma);
    }

    [Theory]
    [InlineData("me llamo domingo")]
    [InlineData("te llamas luz")]
    [InlineData("se llama domingo")]
    [InlineData("nos llamamos luz y mar")]
    [InlineData("se llaman luz y mar")]
    [InlineData("cómo te llamas")]
    [InlineData("cómo te llamas tú")]
    [InlineData("cómo se llama")]
    [InlineData("cómo se llama usted")]
    [InlineData("cómo se llaman ustedes")]
    [InlineData("cómo os llamáis")]
    [InlineData("cómo nos llamamos")]
    [InlineData("cómo me llamo")]
    [InlineData("cómo se llama esto")]
    [InlineData("cómo te sientes")]
    public void AReflexiveCliticThatAgreesIsClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // A clitic that is the object of a verb of another person is no reflexive: te veo, me ves; so is me or te before another first or second person (¿cómo te llamo?,
    // what shall I call you) and before a third person with a subject after it (¿cómo te llama tu madre?). Another verb after cómo is not checked (¿cómo te va?).
    [Theory]
    [InlineData("te veo")]
    [InlineData("me ves")]
    [InlineData("te llamo mañana")]
    [InlineData("me llama mi madre")]
    [InlineData("cómo te llamo")]
    [InlineData("cómo te llama tu madre")]
    [InlineData("cómo te va")]
    public void ACliticOfAnotherPersonIsTheObject(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }
}
