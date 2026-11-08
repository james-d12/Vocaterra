using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>V30 (review): the session skip reads the number that goes with the item, and the item as a whole word.</summary>
public sealed class SessionSkipNumberTests
{
    [Theory]
    [InlineData("Compré tres manzanas y dos panes.", "manzana", 3)]
    [InlineData("Compré tres manzanas y dos panes.", "pan", 2)]
    [InlineData("Compré treinta y dos panes.", "pan", 32)]
    [InlineData("Voy a la panadería a comprar dos manzanas.", "pan", null)] // panadería is no pan
    [InlineData("Compré pan.", "pan", null)] // no number goes with it
    public void TheNumberIsTheOneBeforeTheItemAsAWholeWord(string spanish, string lemma, int? expected) =>
        Assert.Equal(expected, SessionSkip.NumberIn(spanish, TestGame.Nouns.ForLemma(lemma)!, TestGame.Bundle.Language.Numbers));
}
