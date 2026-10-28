using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests;

/// <summary>The order of a check battery (S11): seeded by the world seed and the check kind, the same for the same pair, and the same content in every order.</summary>
public sealed class CheckPlanTests
{
    private static IReadOnlyList<CheckItem> Bank => CheckBank.Load(TestGame.Bundle);

    [Fact]
    public void TheSameSeedAndKindGiveTheSamePlan()
    {
        var first = CheckPlan.For(Bank, CheckKind.Pre, TestGame.Seed);
        var second = CheckPlan.For(Bank, CheckKind.Pre, TestGame.Seed);

        Assert.Equal(Describe(first), Describe(second));
    }

    [Fact]
    public void APreCheckAndAPostCheckHaveTheSameItemsInOtherOrdersAndOptionsInOtherOrders()
    {
        var pre = CheckPlan.For(Bank, CheckKind.Pre, TestGame.Seed);
        var post = CheckPlan.For(Bank, CheckKind.Post, TestGame.Seed);

        Assert.Equal(pre.Select(static e => e.Item.Id).OrderBy(static id => id, StringComparer.Ordinal), post.Select(static e => e.Item.Id).OrderBy(static id => id, StringComparer.Ordinal));
        Assert.NotEqual(pre.Select(static e => e.Item.Id), post.Select(static e => e.Item.Id));
        Assert.NotEqual(Describe(pre), Describe(post));
    }

    [Fact]
    public void ADifferentWorldSeedGivesADifferentOrder()
    {
        var seeds = new ulong[] { 1, 2, 3, 4, 5 };
        var orders = seeds.Select(seed => string.Join(",", CheckPlan.For(Bank, CheckKind.Pre, seed).Select(static e => e.Item.Id))).Distinct().Count();

        Assert.True(orders > 1, "Five seeds gave one order.");
    }

    [Fact]
    public void EveryOptionIsShownOnceAndTheRightOneStaysRight()
    {
        foreach (var entry in CheckPlan.For(Bank, CheckKind.Recall, TestGame.Seed))
        {
            if (entry.Item.Kind == CheckItemKind.Production)
            {
                Assert.Empty(entry.OptionOrder);
                Assert.Equal(-1, entry.ShownCorrect);
                continue;
            }

            Assert.Equal([0, 1, 2], entry.OptionOrder.OrderBy(static i => i).ToList());
            Assert.Equal(entry.Item.Options[entry.Item.Correct].Label, entry.ShownOptions[entry.ShownCorrect].Label);
            for (var shown = 0; shown < 3; shown++)
            {
                Assert.Equal(shown == entry.ShownCorrect, entry.IsCorrect(shown));
            }
        }
    }

    [Fact]
    public void ThePositionsAreTheOrderOfTheBattery()
    {
        var plan = CheckPlan.For(Bank, CheckKind.Pre, TestGame.Seed);

        Assert.Equal(Enumerable.Range(0, plan.Count), plan.Select(static e => e.Position));
        Assert.Equal(Bank.Count, plan.Count);
    }

    // The items in order with their option orders, as one comparable string.
    private static string Describe(IEnumerable<CheckPlanEntry> plan) =>
        string.Join("|", plan.Select(static e => e.Item.Id + ":" + string.Join(".", e.OptionOrder)));
}
