using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Navigation;

/// <summary>
/// The places a word names on the bundle's route (VILLAGE_PLAN V02): a label of the scene (<c>tienda</c> is the stall), the nearest of the points that share one (the
/// three houses), the river as its nearest water (the villager's answer before V02), a catalogue noun no label shows as its nearest block, and a word that is neither.
/// </summary>
public sealed class ScenePlacesTests
{
    [Fact]
    public void ALabelNamesItsPointAndAWordSharedByPointsNamesTheNearest()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "villager");
        var from = TestGame.ViewpointOf(session);

        var shop = session.Places.Resolve("tienda", from, TestGame.FactsOf(session));
        Assert.Equal(new FoundPlace("stall", "tienda", session.Scene.Point("stall")), shop);

        // Three houses show "casa": the one whose point is nearest the player on the ground.
        var house = session.Places.Resolve("casa", from, TestGame.FactsOf(session));
        Assert.NotNull(house);
        var nearest = new[] { "house1", "house2", "house3" }.MinBy(name => Distance2(session.Scene.Point(name), from.Cell))!;
        Assert.Equal(nearest, house.Name);
        Assert.Equal((GuideCell)session.Scene.Point(nearest), house.Cell);
    }

    [Fact]
    public void TheRiverIsItsNearestWaterAsTheVillagersNounGaveIt()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "villager");

        var river = session.Places.Resolve("río", TestGame.ViewpointOf(session), TestGame.FactsOf(session));

        Assert.NotNull(river);
        Assert.Equal(("river", "río"), (river.Name, river.Label)); // the label names the place,
        Assert.Equal(Stage.NearestRiver(session), river.Cell.ToBlock()); // and the place is made of water: the nearest of it, the block point_at_noun found
        Assert.NotEqual(session.Scene.Point("river"), river.Cell.ToBlock());
    }

    [Fact]
    public void ACatalogueNounNoLabelShowsIsItsNearestBlockAndAnyOtherWordIsNoPlace()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "villager");
        var from = TestGame.ViewpointOf(session);

        var tree = session.Places.Resolve("árbol", from, TestGame.FactsOf(session));
        Assert.Equal(new FoundPlace("árbol", "árbol", TestGame.NearestOf(session, "árbol")), tree);

        Assert.Null(session.Places.Resolve("montaña", from, TestGame.FactsOf(session))); // the signpost's third arm: no point, no block (VILLAGE_PLAN 3.2)
        Assert.Null(session.Places.Resolve("botella", from, TestGame.FactsOf(session))); // a catalogue noun of no block in the world
        var noLabels = ScenePlaces.None.Resolve("tienda", from, TestGame.FactsOf(session)); // without the labels a word is still a noun of blocks (the stall's and the roofs')
        Assert.Equal(new FoundPlace("tienda", "tienda", TestGame.NearestOf(session, "tienda")), noLabels);
    }

    [Fact]
    public void AnArrowAPlaceOfTheSceneStandsOnIsNamedByItAndOneOnANounsBlockIsNot()
    {
        var session = TestGame.NewSession();

        Assert.Equal("stall", session.Places.NameAt(session.Scene.Point("stall"), "tienda"));
        Assert.Equal("camp", session.Places.NameAt(session.Scene.Point("camp.guide"), "campamento")); // two names on one block with one label: the first in ordinal order
        Assert.Null(session.Places.NameAt(session.Scene.Point("stall"), "casa"));
        Assert.Null(session.Places.NameAt(Stage.NearestRiver(session), "río"));
        Assert.Equal("tienda", session.Places.LabelOf("stall"));
        Assert.Equal(session.Scene.Point("signpost"), session.Places.Point("signpost"));
        Assert.Null(session.Places.Point("bakery")); // V04 builds it
    }

    private static long Distance2(BlockPos point, GuideCell from) =>
        ((long)(point.X - from.X) * (point.X - from.X)) + ((long)(point.Z - from.Z) * (point.Z - from.Z));
}
