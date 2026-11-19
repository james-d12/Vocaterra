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
        Assert.Equal(["meet-guide", "camp", "inn-room", "fruit-for-mar", "oven-wood", "axe-handle", "water-for-plants", "my-land"], Book.Objectives.Select(static o => o.Id)); // Y11: «Mi terreno», last, shown from session 2
        var order = Book.Find("fruit-for-mar")!;
        Assert.Equal(("fisher", (int?)4), (order.DeliverTo, order.Session));
        Assert.Equal((ItemId.Berry, 3), (Assert.Single(order.Goods).Item, Assert.Single(order.Goods).Count));
        var oven = Book.Find("oven-wood")!;
        Assert.Equal(("baker", (int?)5), (oven.DeliverTo, oven.Session));
        Assert.Equal((ItemId.Wood, 5), (Assert.Single(oven.Goods).Item, Assert.Single(oven.Goods).Count));
        var axe = Book.Find("axe-handle")!;
        Assert.Equal(("smith", (int?)6), (axe.DeliverTo, axe.Session));
        Assert.Equal([(ItemId.Wood, 2), (ItemId.Coin, 2)], axe.Goods.Select(static g => (g.Item, g.Count)));
        var land = Book.Find("my-land")!;
        Assert.Equal(("Mi terreno", (int?)2, false), (land.Title, land.Session, land.EndsSession)); // Y11: it never ends a session, so it never holds sleep back
        Assert.Equal(["estaca", "terreno", "campamento", "fuego"], land.Steps.Select(static s => s.Label));
        Assert.Equal([null, "plot-owned", "camp-placed", "camp-fire-lit"], land.Steps.Select(static s => s.Restore)); // the stake counts the bag; the purchase, the camp and its fire are worked out again from the saved land, the saved camp and the world
        Assert.Equal(["item_picked_up", "plot_bought", "camp_placed", "fire_lit"], land.Steps.Select(static s => s.Trigger.Event));
        Assert.Equal("camp.fire", land.Steps[3].Trigger.Detail); // Y12: the placed ring, not Luna's firering
        Assert.Equal(("innkeeper_night", "camp.guide", "inn"), (Book.ClosingPool, Book.BeaconStart, Book.InnBeacon));
        Assert.Equal([ItemId.Book], Book.Grants);

        var camp = Book.Find("camp")!;
        Assert.Equal("Campamento", camp.Title);
        Assert.Equal(["madera", "frutas", "antorcha", "agua"], camp.Steps.Select(static s => s.Label)); // the label rule gives the old checklist's words
        Assert.Equal([5, 3, 1, 1], camp.Steps.Select(static s => s.Target));
        Assert.Equal(["point-inn"], camp.OnComplete);
        Assert.All(camp.Steps, static s => Assert.Null(s.Restore)); // the save keeps the checklist's counts

        var guide = Book.Find("meet-guide")!;
        Assert.False(guide.IsChecklist);
        Assert.Equal(["camp"], guide.AlsoDoneBy);
        Assert.Equal(new Trigger(WorldEvents.GuideMet), Assert.Single(guide.Steps).Trigger);
        var room = Book.Find("inn-room")!;
        Assert.Equal(("room", new Trigger(WorldEvents.LodgingGranted), "lodging-given"), (Assert.Single(room.Steps).Id, Assert.Single(room.Steps).Trigger, Assert.Single(room.Steps).Restore));
        Assert.Null(Book.Find("light-fire")); // Y07: lighting the player's own fire moved to the campsite
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
        var fire = Book.Find("inn-room")!;
        var broken = new[]
        {
            guide with { AlsoDoneBy = ["meet-guide", "harvest"], OnComplete = ["dance"] },
            Book.Find("camp")!,
            fire with { Steps = [fire.Steps[0] with { Restore = "look-around", Trigger = new Trigger(WorldEvents.TalkOpened, Npc: "miller") }] },
        };
        var data = TestGame.Data.WithObjectives(ObjectiveBook.Of(broken, "guide.no.such.pool", [ItemId.Book], "nowhere"));

        var problems = GameDataValidator.Validate(data);

        Assert.Contains("game/objectives/meet-guide.json: onComplete names the hook 'dance', which the game does not have (guide-met, point-inn).", problems);
        Assert.Contains("game/objectives/meet-guide.json: alsoDoneBy names 'meet-guide', which is not another objective of the session.", problems);
        Assert.Contains("game/objectives/meet-guide.json: alsoDoneBy names 'harvest', which is not another objective of the session.", problems);
        Assert.Contains("game/objectives/inn-room.json: the step 'room' names the restore hook 'look-around', which the game does not have (lesson-begun, lodging-given, plot-owned, camp-placed, camp-fire-lit).", problems);
        Assert.Contains("game/objectives/inn-room.json: the step 'room' waits at the NPC 'miller', which is not one of the NPCs.", problems);
        Assert.Contains("game/session.json: the closing pool 'guide.no.such.pool' is not a dialogue pool with a line.", problems);
        Assert.Contains("game/session.json: the beacon starts on 'nowhere', which is not a site or a point of the scene.", problems);
    }

    [Theory]
    [InlineData("spare-torch")]
    [InlineData("point-own-camp")]
    public void Given_ABundleNamingARetiredCompletionHook_When_ItIsValidated_Then_TheProblemNamesTheHookAndTheOnesTheGameHas(string hook)
    {
        var broken = Book.Objectives.Select(o => o.Id == "camp" ? o with { OnComplete = [hook] } : o).ToList();
        var data = TestGame.Data.WithObjectives(ObjectiveBook.Of(broken, Book.ClosingPool, Book.Grants, Book.BeaconStart, Book.InnBeacon));

        Assert.Contains($"game/objectives/camp.json: onComplete names the hook '{hook}', which the game does not have (guide-met, point-inn).", GameDataValidator.Validate(data));
    }

    [Fact]
    public void Given_ABundleNamingTheRetiredScanFireRingRestoreHook_When_ItIsValidated_Then_TheProblemNamesIt()
    {
        var room = Book.Find("inn-room")!;
        var broken = Book.Objectives.Select(o => o.Id == "inn-room" ? room with { Steps = [room.Steps[0] with { Restore = "scan-fire-ring" }] } : o).ToList();
        var data = TestGame.Data.WithObjectives(ObjectiveBook.Of(broken, Book.ClosingPool, Book.Grants, Book.BeaconStart, Book.InnBeacon));

        Assert.Contains("game/objectives/inn-room.json: the step 'room' names the restore hook 'scan-fire-ring', which the game does not have (lesson-begun, lodging-given, plot-owned, camp-placed, camp-fire-lit).", GameDataValidator.Validate(data));
    }

    [Theory]
    [InlineData(null, "game/session.json: an objective names the hook 'point-inn', so the session needs an \"innBeacon\" (a place of the scene).")]
    [InlineData("nowhere", "game/session.json: innBeacon is 'nowhere', which is not a place of the scene.")]
    public void Given_TheInnBeaconIsMissingOrNotAPlace_When_ItIsValidated_Then_ItIsAProblem(string? inn, string problem)
    {
        var data = TestGame.Data.WithObjectives(ObjectiveBook.Of([.. Book.Objectives], Book.ClosingPool, Book.Grants, Book.BeaconStart, inn));

        Assert.Contains(problem, GameDataValidator.Validate(data));
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
    public void AVersionFiveSaveKeysTheChecklistsStepsByObjectiveTheHookedOnesToo()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        _ = session.Objective.Record(WorldEvents.ItemPickedUp, "madera", 2);

        var document = SaveGame.Parse(SaveGame.Save(session));

        Assert.Equal(SaveGame.CurrentVersion, document.Version);
        Assert.Equal(StepKeys(session), document.Objective.Select(static o => o.Key)); // every step, in the session's order: a step with a restore hook is saved too, so a load never undoes it
        Assert.Contains("meet-guide.met", document.Objective.Select(static o => o.Key));
        Assert.Contains("my-land.camp", document.Objective.Select(static o => o.Key));
        Assert.Equal(2, document.Objective.Single(static o => o.Key == "camp.wood").Count);
    }

    [Fact]
    public void TheFixtureWithTheFireLitFirstLoadsAndTheOutOfOrderEndingStillEnds()
    {
        // tests/Vocaterra.Game.Core.Tests/Fixtures/save-v11.json is a recorded run of the camp (see saves.md): the player lit their own fire (the torch step is done) while the wood and
        // the fruit were not all in: the checklist completes out of the order it lists them in.
        var text = SaveFixture.Read("save-v11.json");
        var document = SaveGame.Parse(text);
        Assert.Equal(SaveGame.CurrentVersion, document.Version);
        Assert.Equal(["camp.wood", "camp.berry", "camp.torch", "camp.water"], document.Objective.Take(4).Select(static o => o.Key));

        var loaded = SaveGame.Load(text, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.Null(loaded.Migration);
        Assert.True(loaded.FireLit); // worked out again from the torch on the ring (the latch the sound beds read; no objective waits for it since Y07)
        Assert.Equal("camp", loaded.CurrentObjective?.Id);
        Assert.False(loaded.SessionComplete);

        _ = loaded.Simulation.GrantItems(ItemId.Wood, 5); // the camp counts the bag (#32)
        _ = loaded.Simulation.GrantItems(ItemId.Berry, 3);
        _ = loaded.HostEvent(WorldEvents.BottleFilled);

        Assert.False(loaded.SessionComplete); // the checklist is done; since Y07 the room at the inn ends the session
        Assert.Equal("inn-room", loaded.CurrentObjective?.Id);

        Vocaterra.Game.Core.Tests.Component.OpeningAndEndingTests.TakeTheRoom(loaded);

        Assert.True(loaded.SessionComplete);
        Assert.Null(loaded.CurrentObjective);
        var resaved = SaveGame.Parse(SaveGame.Save(loaded));
        Assert.Equal(SaveGame.CurrentVersion, resaved.Version);
        Assert.Equal(StepKeys(loaded), resaved.Objective.Select(static o => o.Key));
    }

    // The save key of every step of the session's objectives, in its order (<objective>.<step>).
    private static List<string> StepKeys(VoxelSession session) =>
        session.Objectives.SelectMany(static o => o.Spec.Steps.Select(s => o.Id + "." + s.Id)).ToList();

    [Fact]
    public void TheFixtureOfAnotherBundleKeepsItsStepsThroughTheMigration()
    {
        var text = SaveFixture.Read("save-v11.json");
        var node = JsonNode.Parse(text)!;
        node["bundleHash"] = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        node["objective"]!.AsArray().Add(new JsonObject { ["key"] = "harvest", ["count"] = 1 }); // a step this game does not have

        var loaded = SaveGame.Load(SaveGame.Serialize(SaveGame.Parse(node.ToJsonString())), TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, migrate: true);

        Assert.NotNull(loaded.Migration);
        Assert.Equal(3, loaded.Objective.Step("wood")!.Count);
        Assert.Equal(3, loaded.Objective.Step("berry")!.Count);
        Assert.Equal(1, loaded.Objective.Step("torch")!.Count);
    }
}
