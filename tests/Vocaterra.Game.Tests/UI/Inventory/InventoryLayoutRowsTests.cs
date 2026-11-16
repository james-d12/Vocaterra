using Vocaterra.Game.UI.Inventory;

namespace Vocaterra.Game.Tests.UI.Inventory;

/// <summary>The inventory screen draws the active count's rows (I03b): three rows with no bag, four and five with a worn bag, and the last cell is the last slot.</summary>
public sealed class InventoryLayoutRowsTests
{
    [Theory]
    [InlineData(30, 3)]
    [InlineData(40, 4)]
    [InlineData(50, 5)]
    public void Given_AnActiveCount_When_TheLayoutIsMade_Then_ItHasThatManyRowsOfTen(int slots, int rows)
    {
        var layout = new InventoryLayout(1280, 800, slots);

        Assert.Equal(rows, layout.Rows);
        Assert.Equal(layout.GridTop + ((rows - 1) * (layout.CellHeight + layout.Gap)), layout.Cell(slots - 1).Y);
    }

    [Fact]
    public void Given_ACountThatIsNotAMultipleOfTen_When_TheLayoutIsMade_Then_TheLastRowIsPartlyFilledAndNoSlotIsLost()
    {
        var layout = new InventoryLayout(1280, 800, 35);

        Assert.Equal(4, layout.Rows);
        Assert.Equal(layout.GridTop + (3 * (layout.CellHeight + layout.Gap)), layout.Cell(34).Y);
    }

    [Fact]
    public void Given_MoreRows_When_TheLayoutIsMade_Then_ThePanelIsNeverShorterThanWithFewer()
    {
        var three = new InventoryLayout(1280, 800, 30);
        var five = new InventoryLayout(1280, 800, 50);

        Assert.True(five.Panel.Height > three.Panel.Height);
    }
}
