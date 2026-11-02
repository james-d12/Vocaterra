using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Language.Normalization;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Language.Tests.Matching;

/// <summary>M70: <see cref="AnswerAlignment"/> lines the words of a line up with the words of the answer phrase nearest it.</summary>
public class AnswerAlignmentTests
{
    private static readonly NormalizationOptions Options = NormalizationOptions.Default;

    private static int[] Align(string line, string phrase)
    {
        var pack = SpanishLanguagePack.Instance;
        return AnswerAlignment.Align(pack.Normalizer.Normalize(line, Options).Tokens, pack.Normalizer.Normalize(phrase, Options).Tokens, pack.Folding);
    }

    [Theory]
    [InlineData("compré three manzanas", "compré tres manzanas", "0,1,2")]
    [InlineData("manana iré al río", "iré al río", "-1,0,1,2")]
    [InlineData("iré al río", "mañana iré al río", "1,2,3")]
    [InlineData("glorp compré manzanas", "compré tres manzanas", "-1,0,2")]
    [InlineData("compre tres manzanas", "compré tres manzanas", "0,1,2")]
    [InlineData("", "compré tres manzanas", "")]
    public void TheWordsPairWhereThatIsCheaperThanLeavingThemOut(string line, string phrase, string expected)
    {
        Assert.Equal(expected, string.Join(',', Align(line, phrase)));
    }

    [Fact]
    public void TheNearestPhraseIsTheCanonicalOrAnAlias()
    {
        var pack = SpanishLanguagePack.Instance;
        var set = new AnswerSet("go", [new Answer("river", "iré al río", ["mañana iré al río", "yo iré al río"], [])]);

        var nearest = AnswerAlignment.NearestPhrase(pack.Normalizer.Normalize("manana ire al rio", Options), new MatchResult("river", MatchType.Fuzzy, 1, false, 0.9), set, pack, Options);
        var none = AnswerAlignment.NearestPhrase(pack.Normalizer.Normalize("hola", Options), new MatchResult(null, MatchType.None, 9, false, 0.0), set, pack, Options);

        Assert.Equal("mañana iré al río", nearest!.Text);
        Assert.Null(none);
    }
}
