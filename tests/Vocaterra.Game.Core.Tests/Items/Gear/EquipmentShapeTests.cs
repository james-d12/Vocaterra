using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Gear;

/// <summary>The paper doll's grid read from the data (I04): columns by layer, rows by body slot, the counted groups below, and a new group with no code edit.</summary>
public sealed class EquipmentShapeTests
{
    private static string Group(string id, int? layer, string slots, string accepts = "[\"clothing\"]", string label = "") =>
        "{\"id\":\"" + id + "\"," + (layer is { } l ? "\"layer\":" + l + "," : string.Empty) + slots + ",\"accepts\":" + accepts + ",\"noDuplicates\":false,\"statsApply\":true" + label + "}";

    private static EquipmentData Data(params string[] groups) =>
        EquipmentData.Parse("{\"schemaVersion\":1,\"groups\":[" + string.Join(',', groups) + "]}");

    private const string Body = "\"slots\":[\"head\",\"body\"],\"labels\":{\"head\":\"cabeza\",\"body\":\"cuerpo\"}";

    [Fact]
    public void Given_TheShippedData_When_TheShapeIsMade_Then_ItIsThreeColumnsByFourBodyRowsAndTwoRowsBelow()
    {
        var shape = TestGame.Data.Equipment.Shape;

        Assert.Equal(3, shape.Columns);
        Assert.Equal(4, shape.BodyRows);
        Assert.Equal(6, shape.Rows);
        Assert.Equal(["cabeza", "cuerpo", "pierna", "pie"], shape.RowLabels);
        Assert.Equal(TestGame.Data.Equipment.SlotCount, shape.Cells.Count);
        Assert.Equal(Enumerable.Range(0, shape.Cells.Count), shape.Cells.Select(static c => c.Flat));
    }

    [Fact]
    public void Given_TheShippedData_When_TheCellsAreRead_Then_TheGroupsAreColumnsInLayerOrderAndTheCountedGroupsFlowBelow()
    {
        var data = TestGame.Data.Equipment;
        var shape = data.Shape;
        EquipmentCell Cell(string group, string slot) => shape.Cells.Single(c => c.Flat == data.FlatIndex(GearItems.Slot(group, slot)));

        Assert.Equal((0, 0), (Cell("clothing", "head").Column, Cell("clothing", "head").Row));
        Assert.Equal((1, 3), (Cell("armour", "feet").Column, Cell("armour", "feet").Row));
        Assert.Equal((2, 1), (Cell("cosmetic", "body").Column, Cell("cosmetic", "body").Row));
        Assert.Equal([(4, 0), (4, 1), (4, 2)], shape.Cells.Skip(12).Take(3).Select(static c => (c.Row, c.Column)));
        Assert.Equal((5, 0), (shape.Cells[15].Row, shape.Cells[15].Column));
        Assert.Equal((5, 1), (shape.Cells[16].Row, shape.Cells[16].Column));
        Assert.Equal("mano", shape.Cells[15].Label);
        Assert.Equal("espalda", shape.Cells[16].Label);
        Assert.Null(shape.Cells[12].Label); // an accessory has no noun: the lexicon has no accesorio
    }

    [Fact]
    public void Given_GroupsListedInAnotherOrderThanTheirLayers_When_TheShapeIsMade_Then_TheLayersOrderTheColumns()
    {
        var data = Data(Group("outer", 2, Body), Group("inner", 1, Body), Group("looks", 3, Body));

        var shape = EquipmentShape.Of(data);

        Assert.Equal([1, 0, 2], shape.Cells.Select(static c => c.Column).Where((_, i) => i % 2 == 0)); // flat 0, 2, 4: outer, inner, looks
        Assert.Equal(3, shape.Columns);
    }

    [Fact]
    public void Given_ASeventhGroupWithALayer_When_TheShapeIsMade_Then_ItIsAFourthColumnAndNoCodeNamesIt()
    {
        var shipped = TestGame.Data.Equipment;
        var groups = shipped.Groups.Select(static g => g).ToList();
        var seventh = Data(Group("charms", 4, "\"slots\":[\"head\",\"feet\"]", "[\"charm\"]")).Groups[0];
        var data = new EquipmentData([.. groups, seventh], shipped.BagSlots);

        var shape = EquipmentShape.Of(data);

        Assert.Equal(4, shape.Columns);
        Assert.Equal(data.SlotCount, shape.Cells.Count);
        var head = shape.Cells.Single(c => c.Flat == data.FlatIndex(new EquipSlot(6, 0)));
        var feet = shape.Cells.Single(c => c.Flat == data.FlatIndex(new EquipSlot(6, 1)));
        Assert.Equal((0, 3), (head.Row, head.Column));
        Assert.Equal((3, 3), (feet.Row, feet.Column));
    }

    [Fact]
    public void Given_TheShippedData_When_TheColumnHeadingsAreRead_Then_TheyAreTheLayeredGroupsOwnNounsInLayerOrder()
    {
        Assert.Equal(["ropa", "armadura", "apariencia"], TestGame.Data.Equipment.Shape.ColumnHeadings);
        Assert.True(TestGame.Data.Equipment.Shape.HasHeadings);
        Assert.Empty(EquipmentData.Empty.Shape.ColumnHeadings);
    }

    [Fact]
    public void Given_AFourthLayerWithItsOwnHeading_When_TheShapeIsMade_Then_TheHeadingIsThatColumnsAndNoCodeNamesIt()
    {
        var shipped = TestGame.Data.Equipment;
        var charms = Data(Group("charms", 4, "\"slots\":[\"head\"]", "[\"charm\"]", ",\"heading\":\"mano\"")).Groups[0];
        var data = new EquipmentData([.. shipped.Groups, charms], shipped.BagSlots);

        var shape = EquipmentShape.Of(data);

        Assert.Equal(["ropa", "armadura", "apariencia", "mano"], shape.ColumnHeadings);
        Assert.Equal("mano", charms.Heading);
    }

    [Fact]
    public void Given_GroupsWithNoHeading_When_TheShapeIsMade_Then_NoColumnHasOneAndTheShapeSaysSo()
    {
        var shape = EquipmentShape.Of(Data(Group("outer", 1, Body), Group("inner", 2, Body)));

        Assert.Equal([null, null], shape.ColumnHeadings);
        Assert.False(shape.HasHeadings);
    }

    [Fact]
    public void Given_ASeventhCountedGroup_When_TheShapeIsMade_Then_ItGetsCellsInTheRowsBelow()
    {
        var shipped = TestGame.Data.Equipment;
        var seventh = EquipmentData.Parse("{\"schemaVersion\":1,\"groups\":[{\"id\":\"pockets\",\"count\":2,\"wear\":\"pocket\",\"accepts\":[\"pocket\"],\"noDuplicates\":false,\"statsApply\":true}]}").Groups[0];
        var data = new EquipmentData([.. shipped.Groups, seventh], shipped.BagSlots);

        var shape = EquipmentShape.Of(data);

        Assert.Equal(7, shape.Rows);
        Assert.Equal([(6, 0), (6, 1)], shape.Cells.TakeLast(2).Select(static c => (c.Row, c.Column))); // two do not fit in the one cell left of row 5
    }

    [Fact]
    public void Given_NoGroups_When_TheShapeIsMade_Then_ItIsEmpty()
    {
        Assert.True(EquipmentData.Empty.Shape.IsEmpty);
        Assert.Equal(0, EquipmentData.Empty.Shape.Rows);
    }

    [Fact]
    public void Given_ACountedGroupBeforeALayeredOne_When_TheShapeIsMade_Then_TheBodyRowsStillComeFirst()
    {
        var data = Data("{\"id\":\"belt\",\"count\":1,\"wear\":\"belt\",\"accepts\":[\"belt\"],\"noDuplicates\":false,\"statsApply\":true}", Group("outer", 1, Body));

        var shape = EquipmentShape.Of(data);

        Assert.Equal(2, shape.BodyRows);
        Assert.Equal((2, 0), (shape.Cells[0].Row, shape.Cells[0].Column));
        Assert.Equal((0, 0), (shape.Cells[1].Row, shape.Cells[1].Column));
    }
}
