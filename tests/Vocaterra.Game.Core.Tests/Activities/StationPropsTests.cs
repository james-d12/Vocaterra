using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Activities;

/// <summary>The lookup from a block to the station of its prop (W07, design 10.8): the scene records the cells of each named stamp, as the fire ring's stones are found.</summary>
public sealed class StationPropsTests
{
    [Fact]
    public void TheOvenStampRecordsItsCellsAndEachOneFindsTheStation()
    {
        var scene = TestGame.Route();
        Assert.True(scene.PropCells.TryGetValue("oven", out var cells), "the village's oven is a named stamp");
        Assert.NotEmpty(cells);
        Assert.All(cells, cell => Assert.NotEqual(BlockId.Air, scene.Grid.GetBlock(cell.X, cell.Y, cell.Z)));
        var stations = StationBook.Parse("""{ "schemaVersion": 1, "stations": [ { "id": "horno", "prop": "oven", "npc": "baker", "fixtures": [ { "noun": "bol", "icon": "bowl" } ] } ] }""");
        Assert.All(cells, cell => Assert.Equal("horno", StationProps.At(scene, stations, cell)?.Id));
        Assert.Equal("oven", scene.PropAt(cells[0]));
    }

    [Fact]
    public void AnyOtherCellAndAStationLessBundleFindNothing()
    {
        var scene = TestGame.Route();
        var stations = StationBook.Parse("""{ "schemaVersion": 1, "stations": [ { "id": "horno", "prop": "oven", "npc": "baker", "fixtures": [ { "noun": "bol", "icon": "bowl" } ] } ] }""");
        Assert.Null(StationProps.At(scene, stations, scene.Spawn.Feet));
        var cell = scene.PropCells["oven"][0];
        Assert.Null(StationProps.At(scene, StationBook.Empty, cell));
    }
}
