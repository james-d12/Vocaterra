namespace Vocaterra.Nlp.Language.Tests.Analysis;

using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Lexicon;

/// <summary>What the analyzer and the reading resolver do with a <c>[WORD]</c> token: no unknown-word error, no agreement check, and the lexicon readings kept.</summary>
public class WordTokenAnalysisTests
{
    [Fact]
    public void AGrammarBoundNameIsNotAnUnknownWordAndKeepsConfidence()
    {
        var analysis = Fixtures.Analyze("me llamo ana", "me [VERB:llamar] [WORD:name]");

        Assert.Empty(analysis.Errors);
        Assert.Equal(1.0, analysis.Confidence);
        Assert.Equal("name=ana", string.Join(",", analysis.Slots.Select(s => $"{s.Name}={s.Value}")));
        Assert.Equal("me [VERB:llamar] [WORD:name]", analysis.MatchedGrammarId);
    }

    [Fact]
    public void WithoutAGrammarTheSameNameIsReportedAsUnknown()
    {
        var analysis = Fixtures.Analyze("me llamo ana");

        Assert.Equal("vocab.unknown@9+3", string.Join(";", analysis.Errors.Select(e => $"{e.Tag}@{e.Span.Start}+{e.Span.Length}")));
        Assert.Equal(0.5, analysis.Confidence);
    }

    [Fact]
    public void WhenNoGrammarMatchesEveryUnknownWordIsStillReported()
    {
        // The grammar cannot consume the extra word, so nothing binds [WORD] and the name is an unknown word like any other.
        var analysis = Fixtures.Analyze("me llamo ana tbqz", "me [VERB:llamar] [WORD:name]");

        Assert.Null(analysis.MatchedGrammarId);
        Assert.Equal(["ana", "tbqz"], analysis.Errors.Where(static e => e.Tag == ErrorTags.VocabUnknown).Select(static e => e.Actual).ToArray());
    }

    [Fact]
    public void AGrammarWithoutAWordTokenDoesNotExcuseAnUnknownWord()
    {
        var analysis = Fixtures.Analyze("tengo tbqz espadas", "tengo [NUM:qty] [NOUN:item]");

        Assert.Contains(analysis.Errors, static e => e.Tag == ErrorTags.VocabUnknown && e.Actual == "tbqz");
    }

    [Fact]
    public void ANameThatIsALexiconNounTakesPartInNoAgreementCheck()
    {
        // "espada" is singular and "dos" plural: as a noun after a numeral it is a noun.number mistake, unless the grammar makes it a name.
        Assert.Contains(Fixtures.Errors("tengo dos espada"), static e => e.Tag == ErrorTags.NounNumber);

        var analysis = Fixtures.Analyze("tengo dos espada", "tengo [NUM:qty] [WORD:thing]");

        Assert.Empty(analysis.Errors);
        Assert.Equal("thing=espada", string.Join(",", analysis.Slots.Where(static s => s.Name == "thing").Select(s => $"{s.Name}={s.Value}")));
    }

    [Fact]
    public void ANameSpelledLikeAnIrregularVerbFormIsNotAnIrregularVerb()
    {
        // "sabo" is the regularized spelling of "sé": as a verb it is a conj.irregular mistake, as a name it is no verb at all. (O3 found the same with the
        // bundle's lexicon, for the name "sona" of "suena", seed 1_BGsh0nh9k4.)
        Assert.Contains(Fixtures.Errors("soy sabo"), static e => e.Tag == ErrorTags.ConjIrregular);

        var analysis = Fixtures.Analyze("soy sabo", "soy [WORD:name]");

        Assert.Empty(analysis.Errors);
        Assert.Equal("name=sabo", string.Join(",", analysis.Slots.Select(s => $"{s.Name}={s.Value}")));
    }

    [Fact]
    public void ANameCarriesNoReadingSoNoRuleOrRecastTakesItForAWord()
    {
        // "espada" is a noun of the lexicon, but as a name it has no reading: the token stays, its readings do not.
        var analysis = Fixtures.Analyze("soy espada", "soy [WORD:name]");

        var word = analysis.Tokens[1];
        Assert.Equal("espada", word.Token.Text);
        Assert.Empty(word.Readings);
        Assert.Empty(analysis.Errors);
    }

    [Fact]
    public void AWordNamedInASentenceWithALearnerErrorStillReportsTheError()
    {
        // "yo es ana": the subject and the verb disagree (the person error), and the name after the verb is still a name.
        var analysis = Fixtures.Analyze("yo es ana", "[VERB:ser] [WORD:name]");

        Assert.Equal(new[] { ErrorTags.ConjPerson }, analysis.Errors.Select(static e => e.Tag).ToArray());
        Assert.Equal(("es", "soy"), (analysis.Errors[0].Actual, analysis.Errors[0].Expected));
        Assert.Equal(1.0, analysis.Confidence);
        Assert.Equal("name=ana", string.Join(",", analysis.Slots.Select(s => $"{s.Name}={s.Value}")));
    }
}
