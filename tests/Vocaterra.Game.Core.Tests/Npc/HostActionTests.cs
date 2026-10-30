using System.Text.Json.Nodes;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// The host actions of VILLAGE_PLAN V02 through the real core and the bundle's route: the villager points from the place the learner names (a label of the scene,
/// the river as before, a catalogue noun, or a place he does not know, which he says as a host turn), every answer is logged as <c>place_asked</c>, and
/// <c>give_item</c> gives the number asked for, capped at five.
/// </summary>
public sealed class HostActionTests
{
    private static VoxelSession AtVillager(GameData? data = null)
    {
        var session = data is null
            ? TestGame.NewSession()
            : new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: TestGame.NoLessons);
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");
        return session;
    }

    private static IEnumerable<WorldEvent> WorldEventsOf(VoxelSession session, string name) => session.Events.OfType<WorldEvent>().Where(e => e.Name == name);

    [Fact]
    public void AskedForTheShopTheVillagerPointsAtTheStallByItsLabel()
    {
        var session = AtVillager();

        var turn = session.Say("¿dónde está la tienda?");

        Assert.Equal(("ask_directions", "tienda"), (turn.Intent, turn.Slots["place"]));
        Assert.Contains(turn.Replies, static line => line is "Mira, está allí." or "Está allí, mira.");
        Assert.Equal(new PointerMarker("tienda", session.Scene.Point("stall"), Point: "stall"), session.Pointer);
        Assert.Equal("stall", Assert.Single(WorldEventsOf(session, WorldEvents.PlaceAsked)).Detail);
        var set = Assert.Single(WorldEventsOf(session, WorldEvents.BeaconSet));
        Assert.Equal(("villager", $"tienda at {session.Scene.Point("stall").X},{session.Scene.Point("stall").Y},{session.Scene.Point("stall").Z}"), (set.Npc, set.Detail));
        Assert.False(session.RiverPointedOut);
    }

    [Fact]
    public void AskedForTheRiverTheVillagerStillPointsAtItsNearestWater()
    {
        // The S3 behaviour, unchanged by V02: the beacon on the nearest river block (NpcServiceTests holds the rest of it), now found through the label "río".
        var session = AtVillager();

        _ = session.Say("¿dónde está el río?");

        Assert.Equal(new PointerMarker("río", Stage.NearestRiver(session)), session.Pointer);
        Assert.True(session.RiverPointedOut);
        Assert.Equal("river", Assert.Single(WorldEventsOf(session, WorldEvents.PlaceAsked)).Detail);
    }

    [Fact]
    public void AskedForANounNoLabelShowsTheVillagerPointsAtItsNearestBlock()
    {
        var session = AtVillager();

        _ = session.Say("¿dónde está el árbol?");

        Assert.Equal(new PointerMarker("árbol", TestGame.NearestOf(session, "árbol")), session.Pointer);
        Assert.Equal("árbol", Assert.Single(WorldEventsOf(session, WorldEvents.PlaceAsked)).Detail);
    }

    [Fact]
    public void AskedForAPlaceTheVillageDoesNotHaveTheVillagerSaysHeDoesNotKnowAndPointsNowhere()
    {
        var session = AtVillager();
        var records = session.Conversation.RecordCount;

        var turn = session.Say("¿dónde está la montaña?");

        Assert.Equal(("ask_directions", "montaña"), (turn.Intent, turn.Slots["place"]));
        Assert.Empty(turn.Replies); // not "Mira, está allí.": there is nothing to point at
        Assert.Equal(["No sé dónde está."], turn.FollowUp);
        Assert.Equal(["No sé dónde está."], turn.GuideLines);
        Assert.Equal("idle", turn.AgentState); // the answer's host turn took him out of "pointing"
        Assert.Null(session.Pointer);
        Assert.Empty(WorldEventsOf(session, WorldEvents.BeaconSet));
        Assert.Equal(WorldEvents.UnknownPlace, Assert.Single(WorldEventsOf(session, WorldEvents.PlaceAsked)).Detail);

        // The answer is a core host turn in the replay record, after the utterance's.
        Assert.Equal(records + 2, session.Conversation.RecordCount);
        var answer = Assert.IsType<HostTurnEnvelope>(session.Conversation.Records[^1].HostTurn);
        Assert.Equal("villager.unknown_place", answer.HostEvent);
    }

    [Theory]
    [InlineData("quiero pan, por favor", 1)]
    [InlineData("quiero dos panes, por favor", 2)]
    [InlineData("quiero cinco panes, por favor", 5)]
    [InlineData("quiero nueve panes, por favor", 5)] // capped at five
    public void GiveItemWithTheLearnersNumberGivesThatManyUpToFive(string line, int expected)
    {
        var session = AtVillager(WithVillagerGive(new JsonObject { ["kind"] = "give_item", ["item"] = "bread", ["count"] = "qty" }));

        var turn = session.Say(line);

        Assert.Equal("request_item", turn.Intent);
        Assert.Equal(expected, session.Simulation.Inventory.Count(ItemId.Bread));
        var given = Assert.Single(WorldEventsOf(session, WorldEvents.ItemGiven));
        Assert.Equal(("villager", "pan, request"), (given.Npc, given.Detail));
    }

    [Fact]
    public void GiveItemWithAFixedCountGivesThatManyWhateverTheLearnerAskedFor()
    {
        var session = AtVillager(WithVillagerGive(new JsonObject { ["kind"] = "give_item", ["item"] = "bread", ["count"] = 3 }));

        _ = session.Say("quiero dos panes, por favor");

        Assert.Equal(3, session.Simulation.Inventory.Count(ItemId.Bread));
    }

    [Fact]
    public void GiveItemGivesNothingWhenTheInventoryCannotTakeThemAll()
    {
        var session = AtVillager(WithVillagerGive(new JsonObject { ["kind"] = "give_item", ["item"] = "book", ["count"] = 1 }));
        for (var slot = 0; slot < Inventory.SlotCount; slot++)
        {
            session.Simulation.Inventory.SetSlot(slot, new ItemStack(ItemId.Book, 1)); // every slot full: the book stacks one
        }

        _ = session.Say("quiero pan, por favor");

        Assert.Equal(Inventory.SlotCount, session.Simulation.Inventory.Count(ItemId.Book));
        Assert.Empty(WorldEventsOf(session, WorldEvents.ItemGiven)); // nothing is lost: the NPC keeps it
    }

    [Fact]
    public void TakeItemTakesThemAllOrNothing()
    {
        // VILLAGE_PLAN V03: an NPC takes items (here the villager's villager.give, mapped to take two wood); a player with fewer keeps what they have.
        var session = AtVillager(WithVillagerGive(new JsonObject { ["kind"] = "take_item", ["item"] = "wood", ["count"] = 2 }));
        _ = session.Simulation.GrantItems(ItemId.Wood, 3);

        _ = session.Say("quiero pan, por favor");
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Wood));
        var taken = Assert.Single(WorldEventsOf(session, WorldEvents.ItemTaken));
        Assert.Equal(("villager", "madera, request"), (taken.Npc, taken.Detail));

        _ = session.Say("quiero pan, por favor"); // one left: nothing is taken
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Single(WorldEventsOf(session, WorldEvents.ItemTaken));
    }

    // The bundle's content with the villager's villager.give mapped to another action.
    private static GameData WithVillagerGive(JsonObject action)
    {
        var root = JsonNode.Parse(File.ReadAllText(Repo.Path("content", "game-v1", "game", "host-events.json")))!;
        root["events"]!.AsArray().First(e => e!["event"]!.GetValue<string>() == "villager.give")!["action"] = action;
        return TestGame.Data.WithHostEvents(HostEventTable.Parse(root.ToJsonString()));
    }
}
