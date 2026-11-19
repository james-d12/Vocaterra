using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>The <c>--give</c> console line (I10b, QA-2026-11-19-ui-5).</summary>
public sealed class GiveLineTests
{
    [Fact]
    public void Given_AnItemGiven_When_TheLineIsMade_Then_ItNamesTheItemAndTheBagCount()
    {
        Assert.Equal("give: bread, bag 2", GiveLine.Text("bread", 0, 2));
    }

    [Fact]
    public void Given_AFullBag_When_TheLineIsMade_Then_ItSaysNothingWentIn()
    {
        Assert.Equal("give: coin not given, the bag is full, bag 0", GiveLine.Text("coin", 1, 0));
    }

    [Fact]
    public void Given_ADataOnlyItemOfTheShippedBundle_When_ItsLineIsMade_Then_TheLineHasItsNameNotItsNumber()
    {
        var items = Vocaterra.Game.Core.Tests.TestSupport.TestGame.Data.Items;
        foreach (var id in new[] { 28, 31, 34, 100, 102 })
        {
            var line = GiveLine.Text(items.Get((Vocaterra.Game.Core.Items.ItemId)id).Name, 0, 1);
            Assert.DoesNotContain(id.ToString(System.Globalization.CultureInfo.InvariantCulture), line, StringComparison.Ordinal);
        }
    }
}
