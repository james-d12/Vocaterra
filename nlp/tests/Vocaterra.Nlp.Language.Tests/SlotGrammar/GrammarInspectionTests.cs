namespace Vocaterra.Nlp.Language.Tests.SlotGrammar;

using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>The read-only views of a grammar that the content lint uses.</summary>
public class GrammarInspectionTests
{
    [Fact]
    public void LiteralWordsListEveryLiteralInsideAlternativesAndOptionalsDistinctAndSorted()
    {
        var grammar = SlotGrammar.Parse("(buenos|buenas) (días|tardes)? hola hola [NOUN:x] [VERB:ver]");

        Assert.Equal(["buenas", "buenos", "días", "hola", "tardes"], grammar.LiteralWords());
    }

    [Fact]
    public void VerbLemmasListTheLemmasOfVerbElementsOnly()
    {
        var grammar = SlotGrammar.Parse("[VERB:querer] ([VERB:comprar]|[VERB:vender:slot])? [VERB] [NOUN:item] hacer");

        Assert.Equal(["comprar", "querer", "vender"], grammar.VerbLemmas());
    }

    [Theory]
    [InlineData("[WORD:item]? [ART]? [NOUN:x]", 1)]
    [InlineData("[ART]? [WORD]? [NOUN:x]", 1)]
    [InlineData("[ART]? [WORD:name]? [ADJ]?", 2)]
    [InlineData("hola ([ART]? [WORD:n]?|adiós)", 1)]
    [InlineData("me [VERB:llamar] [WORD:name]", 0)]
    [InlineData("[WORD:name]? hola [ART]?", 0)]
    [InlineData("[WORD:name] [ART]?", 0)]
    [InlineData("[ART]? [NOUN:x]? [ADJ]?", 0)]
    public void OptionalWordNeighboursCountsEachOptionalWordNextToAnOptionalElement(string pattern, int expected)
    {
        Assert.Equal(expected, SlotGrammar.Parse(pattern).OptionalWordNeighbours().Count);
    }

    [Fact]
    public void OptionalWordNeighboursDescribesThePair()
    {
        Assert.Equal(["[WORD:item]? next to [ART]?"], SlotGrammar.Parse("[WORD:item]? [ART]? casa").OptionalWordNeighbours());
    }
}
