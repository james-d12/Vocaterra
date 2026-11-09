using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>The typed-answer credit as a test setting that can change during the run (the menu's Scoring row, <c>--typed-credit</c> at run time).</summary>
public sealed class TypedCreditSettingTests
{
    [Fact]
    public void Given_HalfCredit_When_TheRuleIsChangedToFull_Then_TheNextTypedUseEarnsAWholeUse()
    {
        var half = TestGame.NewSession(credit: CreditRules.Default);
        half.Say("es un árbol", TestGame.NearestOf(half, "árbol"));
        Assert.Equal(WordProgress.UnitsPerUse / 2, half.Words.RecordOf("árbol").CreditUnits);

        var changed = TestGame.NewSession(credit: CreditRules.Default);
        Assert.Equal(CreditRules.Default, changed.Credit);
        changed.SetCredit(CreditRules.FullTyped);
        Assert.Equal(CreditRules.FullTyped, changed.Credit);
        changed.Say("es un árbol", TestGame.NearestOf(changed, "árbol"));
        Assert.Equal(WordProgress.UnitsPerUse, changed.Words.RecordOf("árbol").CreditUnits);
    }

    [Fact]
    public void Given_ANullRule_When_Set_Then_ItIsRefusedAndTheRuleStays()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);
        session.SetCredit(CreditRules.FullTyped);
        Assert.Throws<ArgumentNullException>(() => session.SetCredit(null!));
        Assert.Equal(CreditRules.FullTyped, session.Credit);
    }
}
