using System.Text.Json.Nodes;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>
/// The objectives and the session as data (content-plan 3.5, C07): <c>game/session.json</c> and <c>game/objectives/&lt;id&gt;.json</c>. They were code
/// (<c>Objective.Camp()</c>, the <c>SessionStage</c> rules, the book grant, the closing pool, the beacon's start); the data gives the same session, the
/// validator checks the hooks and the references, and save version 5 keys the steps by objective.
/// </summary>
public sealed class ObjectiveBookTests
{
    private static ObjectiveBook Book => TestGame.Data.Objectives;

    private static string ObjectiveFile(string id) => File.ReadAllText(Repo.Path("content", "game-v1", "game", "objectives", id + ".json"));

    [Fact]
    public void TheShippedSessionIsTheOneTheCodeHeld()
    {
        // The three the code held, and since village-plan V10 the fisher's order of session 4: three fruits brought to him at the bridge; since V11 the baker's of
        // session 5: five wood for her oven; since V12 the smith's of session 6: two wood and two coins for the axe.
        Assert.Equal(["meet-guide", "camp", "light-fire", "fruit-for-mar", "oven-wood", "axe-handle", "water-for-plants"], Book.Objectives.Select(static o => o.Id));
        var order = Book.Find("fruit-for-mar")!;
        Assert.Equal(("fisher", (int?)4), (order.DeliverTo, order.Session));
        Assert.Equal((ItemId.Berry, 3), (Assert.Single(order.Goods).Item, Assert.Single(order.Goods).Count));
        var oven = Book.Find("oven-wood")!;
        Assert.Equal(("baker", (int?)5), (oven.DeliverTo, oven.Session));
        Assert.Equal((ItemId.Wood, 5), (Assert.Single(oven.Goods).Item, Assert.Single(oven.Goods).Count));
        var axe = Book.Find("axe-handle")!;
        Assert.Equal(("smith", (int?)6), (axe.DeliverTo, axe.Session));
        Assert.Equal([(ItemId.Wood, 2), (ItemId.Coin, 2)], axe.Goods.Select(static g => (g.Item, g.Count)));
        Assert.Equal(("guide.lesson.end", "camp.guide"), (Book.ClosingPool, Book.BeaconStart));
        Assert.Equal([ItemId.Book], Book.Grants);

        var camp = Book.Find("camp")!;
        Assert.Equal("Campamento", camp.Title);
        Assert.Equal(["madera", "frutas", "antorcha", "agua"], camp.Steps.Select(static s => s.Label)); // the label rule gives the old checklist's words
        Assert.Equal([5, 3, 1, 1], camp.Steps.Select(static s => s.Target));
        Assert.Equal(["spare-torch", "point-own-camp"], camp.OnComplete);
        Assert.All(camp.Steps, static s => Assert.Null(s.Restore)); // the save keeps the checklist's counts

        var guide = Book.Find("meet-guide")!;
        Assert.False(guide.IsChecklist);
        Assert.Equal(["camp"], guide.AlsoDoneBy);
        Assert.Equal(new Trigger(WorldEvents.GuideMet), Assert.Single(guide.Steps).Trigger);
        Assert.Equal("scan-fire-ring", Assert.Single(Book.Find("light-fire")!.Steps).Restore);
    }

    [Fact]
    public void ALabelOverridesTheRuleAndTheRuleTakesThePluralOfACountNounAboveOne()
    {
        var json = ObjectiveFile("camp");

        var two = ObjectiveBook.Parse("camp.json", json.Replace("\"noun\": \"antorcha\", \"target\": 1", "\"noun\": \"antorcha\", \"target\": 2", StringComparison.Ordinal), "camp", TestGame.Nouns);
        var named = ObjectiveBook.Parse("camp.json", json.Replace("\"noun\": \"agua\",", "\"noun\": \"agua\", \"label\": \"el agua\",", StringComparison.Ordinal), "camp", TestGame.Nouns);

        Assert.Equal("antorchas", two.Steps[2].Label);
        Assert.Equal("madera", two.Steps[0].Label); // a mass noun keeps its lemma at five
        Assert.Equal("el agua", named.Steps[3].Label);
    }

    [Theory]
    [InlineData("\"id\": \"camp\"", "\"id\": \"campamento\"", "its file's name")]
    [InlineData("\"noun\": \"fruta\"", "\"noun\": \"queso\"", "not a catalogue noun")]
    [InlineData("\"id\": \"berry\"", "\"id\": \"camp.berry\"", "has a dot")]
    [InlineData("\"noun\": \"agua\", \"target\": 1", "\"noun\": \"agua\", \"target\": 0", "at least 1")]
    [InlineData("\"event\": \"bottle_filled\"", "\"event\": \"rain\"", "not an event the game raises")]
    public void AnObjectiveFileThatDoesNotHoldTogetherIsRefused(string from, string to, string problem)
    {
        var json = ObjectiveFile("camp");
        Assert.Contains(from, json, StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() => ObjectiveBook.Parse("game/objectives/camp.json", json.Replace(from, to, StringComparison.Ordinal), "camp", TestGame.Nouns));

        Assert.Contains(problem, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownHookAnAlsoDoneByThatIsNoOtherObjectiveAndAMissingPoolOrStartAreProblems()
    {
        var guide = Book.Find("meet-guide")!;
        var fire = Book.Find("light-fire")!;
        var broken = new[]
        {
            guide with { AlsoDoneBy = ["meet-guide", "harvest"], OnComplete = ["dance"] },
            Book.Find("camp")!,
            fire with { Steps = [fire.Steps[0] with { Restore = "look-around", Trigger = new Trigger(WorldEvents.TalkOpened, Npc: "miller") }] },
        };
        var data = TestGame.Data.WithObjectives(ObjectiveBook.Of(broken, "guide.no.such.pool", [ItemId.Book], "nowhere"));

        var problems = GameDataValidator.Validate(data);

        Assert.Contains("game/objectives/meet-guide.json: onComplete names the hook 'dance', which the game does not have (guide-met, spare-torch, point-own-camp).", problems);
        Assert.Contains("game/objectives/meet-guide.json: alsoDoneBy names 'meet-guide', which is not another objective of the session.", problems);
        Assert.Contains("game/objectives/meet-guide.json: alsoDoneBy names 'harvest', which is not another objective of the session.", problems);
        Assert.Contains("game/objectives/light-fire.json: the step 'fire' names the restore hook 'look-around', which the game does not have (lesson-begun, scan-fire-ring).", problems);
        Assert.Contains("game/objectives/light-fire.json: the step 'fire' waits at the NPC 'miller', which is not one of the NPCs.", problems);
        Assert.Contains("game/session.json: the closing pool 'guide.no.such.pool' is not a dialogue pool with a line.", problems);
        Assert.Contains("game/session.json: the beacon starts on 'nowhere', which is not a site or a point of the scene.", problems);
    }

    [Fact]
    public void AnOrderNamesItsNpcAndGoodsAndItsOneStepIsTheDelivery()
    {
        // village-plan V03: an objective with deliverTo and deliver, from a play session; the loader makes its step, which order_delivered at that NPC completes.
        var order = ObjectiveBook.Parse(
            "game/objectives/axe-handle.json",
            """{ "schemaVersion": 1, "id": "axe-handle", "session": 6, "deliverTo": "smith", "deliver": [ { "item": "wood", "count": 2 }, { "item": "coin", "count": 3 } ] }""",
            "axe-handle",
            TestGame.Nouns);

        Assert.Equal((6, "smith", true, false, false), (order.Session, order.DeliverTo, order.IsOrder, order.IsChecklist, order.EndsSession));
        Assert.Equal([new OrderGoods(ItemId.Wood, 2), new OrderGoods(ItemId.Coin, 3)], order.Goods);
        var step = Assert.Single(order.Steps);
        Assert.Equal(("delivered", 1, new Trigger(WorldEvents.OrderDelivered, "smith", "axe-handle"), (string?)null), (step.Id, step.Target, step.Trigger, step.Restore));
        Assert.False(order.ShownIn(5));
        Assert.True(order.ShownIn(6));
        Assert.True(Book.Find("camp")!.ShownIn(1)); // no session: every session
        Assert.True(Book.Find("camp")!.EndsSession);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 1, "id": "o", "deliverTo": "smith" }""", "both or neither")]
    [InlineData("""{ "schemaVersion": 1, "id": "o", "deliver": [ { "item": "wood", "count": 2 } ], "steps": [] }""", "both or neither")]
    [InlineData("""{ "schemaVersion": 1, "id": "o", "deliverTo": "smith", "deliver": [ { "item": "wood", "count": 2 } ], "steps": [] }""", "no title and no steps")]
    [InlineData("""{ "schemaVersion": 1, "id": "o", "title": "Hacha", "deliverTo": "smith", "deliver": [ { "item": "wood", "count": 2 } ] }""", "no title and no steps")]
    [InlineData("""{ "schemaVersion": 1, "id": "o", "deliverTo": "smith", "deliver": [ { "item": "wood", "count": 2 }, { "item": "wood", "count": 1 } ] }""", "twice")]
    [InlineData("""{ "schemaVersion": 1, "id": "o", "deliverTo": "smith", "deliver": [ { "item": "wood", "count": 0 } ] }""", "fewer than one")]
    [InlineData("""{ "schemaVersion": 1, "id": "o", "deliverTo": "smith", "deliver": [ { "item": "hacha", "count": 1 } ] }""", "unknown item 'hacha'")]
    [InlineData("""{ "schemaVersion": 1, "id": "o", "session": 0, "deliverTo": "smith", "deliver": [ { "item": "wood", "count": 1 } ] }""", "from 1")]
    public void AnOrderThatDoesNotHoldTogetherIsRefused(string json, string problem)
    {
        var error = Assert.Throws<InvalidDataException>(() => ObjectiveBook.Parse("game/objectives/o.json", json, "o", TestGame.Nouns));
        Assert.Contains(problem, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOrderToNoNpcOrToAnNpcThatCannotThankForItIsAProblem()
    {
        var toMiller = ObjectiveBook.Parse("o.json", """{ "schemaVersion": 1, "id": "flour", "deliverTo": "miller", "deliver": [ { "item": "wood", "count": 1 } ] }""", "flour", TestGame.Nouns);
        var toVillager = ObjectiveBook.Parse("o.json", """{ "schemaVersion": 1, "id": "bread-run", "deliverTo": "villager", "deliver": [ { "item": "bread", "count": 1 } ] }""", "bread-run", TestGame.Nouns);
        var data = TestGame.Data.WithObjectives(ObjectiveBook.Of([.. Book.Objectives, toMiller, toVillager], Book.ClosingPool, Book.Grants, Book.BeaconStart));

        var problems = GameDataValidator.Validate(data);

        Assert.Contains("game/objectives/flour.json: the order is brought to 'miller', which is not one of the NPCs.", problems);
        Assert.Contains("game/objectives/bread-run.json: the order is brought to 'villager', and the state 'idle' of agents/villager.json does not take 'villager.delivered' to thank the player.", problems);
        Assert.Contains("game/objectives/bread-run.json: the order is brought to 'villager', and the state 'pointing' of agents/villager.json does not take 'villager.delivered' to thank the player.", problems);
        Assert.DoesNotContain(problems, static p => p.Contains("waits at the NPC 'miller'", StringComparison.Ordinal)); // the order's own step is not named twice
    }

    [Fact]
    public void TheShippedObjectivesHaveNoProblems() =>
        Assert.DoesNotContain(GameDataValidator.Validate(TestGame.Data), static p => p.StartsWith("game/objectives", StringComparison.Ordinal) || p.StartsWith("game/session", StringComparison.Ordinal));

    [Fact]
    public void TheSessionGrantsTheBookFromTheData()
    {
        var session = TestGame.NewSession();

        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Book));
    }

    [Fact]
    public void AVersionFiveSaveKeysTheChecklistsStepsByObjectiveAndLeavesTheHookedOnesToTheWorld()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        _ = session.Objective.Record(WorldEvents.ItemPickedUp, "madera", 2);

        var document = SaveGame.Parse(SaveGame.Save(session, TestGame.Route()));

        Assert.Equal(5, document.Version);
        Assert.Equal(["camp.wood", "camp.berry", "camp.torch", "camp.water", "fruit-for-mar.delivered", "oven-wood.delivered", "axe-handle.delivered", "water-for-plants.delivered"], document.Objective.Select(static o => o.Key)); // and the orders' one step each (V10 to V13)
        Assert.Equal(2, document.Objective[0].Count);
    }

    [Fact]
    public void AVersionFourSaveLoadsWithItsKeysMappedAndTheOutOfOrderEndingStillEnds()
    {
        // tests/Vocaterra.Game.Core.Tests/Fixtures/save-v4.json was written by the C08 code (format 4): the player lit their own fire first, then gathered five wood
        // and two fruit. Its keys are the checklist's alone (wood, berry, ...); loading maps them to camp.wood and the rest before anything else reads them.
        var text = File.ReadAllText(Repo.Path("tests", "Vocaterra.Game.Core.Tests", "Fixtures", "save-v4.json"));
        var document = SaveGame.Parse(text);
        Assert.Equal(4, document.Version);
        Assert.Equal(["wood", "berry", "torch", "water"], document.Objective.Select(static o => o.Key));

        var loaded = SaveGame.Load(text, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.Null(loaded.Migration);
        Assert.True(loaded.FireLit); // worked out again from the torch on the ring
        Assert.True(loaded.GuideMet); // the checklist has moved
        Assert.Equal("camp", loaded.CurrentObjective?.Id);
        Assert.Equal("Campamento: madera 5/5 · frutas 2/3 · antorcha 0/1 · agua 0/1", loaded.Objective.Summary);
        Assert.False(loaded.SessionComplete);

        _ = loaded.Objective.Record(WorldEvents.ItemPickedUp, "fruta", 1);
        _ = loaded.Objective.Record(WorldEvents.TorchAtRing, "firering");
        _ = loaded.HostEvent(WorldEvents.BottleFilled);

        Assert.True(loaded.SessionComplete);
        Assert.Null(loaded.CurrentObjective);
        Assert.DoesNotContain(loaded.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.SpareTorch); // the fire is lit already
        var resaved = SaveGame.Parse(SaveGame.Save(loaded, TestGame.Route()));
        Assert.Equal(5, resaved.Version);
        Assert.Equal(["camp.wood", "camp.berry", "camp.torch", "camp.water", "fruit-for-mar.delivered", "oven-wood.delivered", "axe-handle.delivered", "water-for-plants.delivered"], resaved.Objective.Select(static o => o.Key));
    }

    [Fact]
    public void AVersionFourSaveOfAnotherBundleKeepsItsStepsThroughTheMigration()
    {
        var text = File.ReadAllText(Repo.Path("tests", "Vocaterra.Game.Core.Tests", "Fixtures", "save-v4.json"));
        var node = JsonNode.Parse(text)!;
        node["bundleHash"] = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        node["objective"]!.AsArray().Add(new JsonObject { ["key"] = "harvest", ["count"] = 1 }); // a step this game does not have

        var loaded = SaveGame.Load(SaveGame.Serialize(SaveGame.Parse(node.ToJsonString())), TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, migrate: true);

        Assert.NotNull(loaded.Migration);
        Assert.Equal(5, loaded.Objective.Step("wood")!.Count); // mapped before the migration, so it is kept
        Assert.Equal(2, loaded.Objective.Step("berry")!.Count);
    }
}
