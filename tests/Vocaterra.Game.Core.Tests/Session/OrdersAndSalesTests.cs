using System.Globalization;
using System.Text.Json.Nodes;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>
/// A purchase and an order by data alone (VILLAGE_PLAN V03), on a copy of the bundle with a test baker added the way <c>content/README.md</c> says: her agent sells
/// bread at two coins (<c>sell</c>), says when the coins are too few (<c>baker.cant_pay</c>) and thanks the player for the oven's wood (<c>baker.delivered</c>, whose
/// reward is four coins); the order <c>oven-wood</c> (five wood, from play session 2) is an objective with <c>deliverTo</c>. No order ships in the bundle before the
/// lessons that teach them (V10 to V12); the shipped bundle is not touched.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class OrdersAndSalesTests
{
    private const string BakerFile = """
        {
          "schemaVersion": 2,
          "id": "baker",
          "label": "Panadera",
          "name": "Luz",
          "figure": "baker",
          "prompts": { "idle": "baker-open" },
          "opening": { "event": "baker.greet", "when": "always" },
          "voice": { "speaker": 28, "name": "ef_dora", "speed": 1.0 }
        }
        """;

    private const string BakerAgent = """
        {
          "agentId": "baker",
          "initialState": "idle",
          "variantPools": [ { "id": "baker_hello", "count": 1 }, { "id": "baker_sell", "count": 1 }, { "id": "baker_cant_pay", "count": 1 }, { "id": "baker_thanks", "count": 1 }, { "id": "not_understood", "count": 2 } ],
          "states": [
            {
              "id": "idle",
              "transitions": [
                { "onHostEvent": "baker.greet", "to": "idle", "actions": [{ "dialogueIntent": "baker_hello", "variantPoolId": "baker_hello" }] },
                { "onHostEvent": "baker.cant_pay", "to": "idle", "actions": [{ "dialogueIntent": "baker_cant_pay", "variantPoolId": "baker_cant_pay" }] },
                { "onHostEvent": "baker.delivered", "to": "idle", "actions": [{ "dialogueIntent": "baker_thanks", "hostEvent": "baker.pay", "variantPoolId": "baker_thanks" }] },
                { "on": {"intent": "request_item", "communicated": true}, "to": "idle", "actions": [{ "dialogueIntent": "baker_sell", "hostEvent": "baker.sell_bread", "variantPoolId": "baker_sell" }] }
              ],
              "fallback": { "actions": [{ "dialogueIntent": "not_understood", "variantPoolId": "not_understood" }] }
            }
          ]
        }
        """;

    private const string OvenWood = """
        { "schemaVersion": 1, "id": "oven-wood", "session": 2, "deliverTo": "baker", "deliver": [ { "item": "wood", "count": 5 } ] }
        """;

    private const string Thanks = "¡Gracias! Toma cuatro monedas.";

    private static readonly Lazy<GameData> Baker = new(BuildBakerBundle);

    // A session on the bundle's route with the baker's data, in a play session; without lessons unless asked (the shipped first lesson would take the HUD's line).
    private static VoxelSession NewSession(int playSession = 1, GameData? data = null, bool shippedLessons = false)
    {
        data ??= Baker.Value;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        return new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: shippedLessons ? null : noLessons) { PlaySession = playSession };
    }

    private static IEnumerable<WorldEvent> WorldEventsOf(VoxelSession session, string name) => session.Events.OfType<WorldEvent>().Where(e => e.Name == name);

    [Fact]
    public void TheBakersDataHoldsTogetherWithTheChecksOfOrdersAndSales()
    {
        var order = Baker.Value.Objectives.Find("oven-wood")!;
        Assert.Equal((2, "baker", true, false), (order.Session, order.DeliverTo, order.IsOrder, order.EndsSession));
        Assert.Equal([new OrderGoods(ItemId.Wood, 5)], order.Goods);
        var delivery = Assert.Single(order.Steps);
        Assert.Equal((ObjectiveBook.DeliveredStep, 1, new Trigger(WorldEvents.OrderDelivered, "baker", "oven-wood")), (delivery.Id, delivery.Target, delivery.Trigger));
        Assert.Equal(NpcEffect.SellAt(ItemId.Bread, 2), Baker.Value.HostEvents.Of("baker", "baker.sell_bread"));

        // The copy has no scene placement or glosses for her (the scene's and the locale's); what the order and the sale need of her agent is there.
        Assert.DoesNotContain(GameDataValidator.Validate(Baker.Value), static p => p.Contains("baker", StringComparison.Ordinal) && !p.StartsWith("game/scene.json", StringComparison.Ordinal) && !p.StartsWith("locale", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("quiero pan, por favor", 1)]
    [InlineData("quiero dos panes, por favor", 2)]
    [InlineData("quiero nueve panes, por favor", 5)] // the number asked for is capped at five
    public void ASaleTakesThePriceOfTheNumberAskedForAndGivesThatMany(string line, int loaves)
    {
        var session = NewSession();
        _ = session.Simulation.GrantItems(ItemId.Coin, 20);
        Assert.Equal(["Hola, soy Luz."], session.OpenTalk("baker").Lines);

        var turn = session.Say(line);

        Assert.Equal("request_item", turn.Intent);
        Assert.Equal(["Aquí tienes el pan."], turn.Replies);
        Assert.Empty(turn.FollowUp ?? []);
        Assert.Equal(loaves, session.Simulation.Inventory.Count(ItemId.Bread));
        Assert.Equal(20 - (2 * loaves), session.Simulation.Inventory.Count(ItemId.Coin));
        var paid = Assert.Single(WorldEventsOf(session, WorldEvents.ItemTaken));
        Assert.Equal(("baker", "moneda, request"), (paid.Npc, paid.Detail));
        Assert.Equal("pan, request", Assert.Single(WorldEventsOf(session, WorldEvents.ItemGiven)).Detail);
    }

    [Fact]
    public void WithTooFewCoinsNothingChangesHandsAndTheBakerSaysSo()
    {
        var session = NewSession();
        _ = session.Simulation.GrantItems(ItemId.Coin, 3);
        _ = session.OpenTalk("baker");
        var records = session.Conversation.RecordCount;

        var turn = session.Say("quiero dos panes, por favor"); // four coins

        Assert.Empty(turn.Replies); // not "Aquí tienes el pan.": nothing is handed over
        Assert.Equal(["Cuesta dos monedas. No tienes monedas."], turn.FollowUp);
        Assert.Equal(3, session.Simulation.Inventory.Count(ItemId.Coin));
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Bread));
        Assert.Empty(WorldEventsOf(session, WorldEvents.ItemTaken));
        Assert.Empty(WorldEventsOf(session, WorldEvents.ItemGiven));
        Assert.Equal(records + 2, session.Conversation.RecordCount);
        Assert.Equal("baker.cant_pay", Assert.IsType<HostTurnEnvelope>(session.Conversation.Records[^1].HostTurn).HostEvent);

        var one = session.Say("quiero pan, por favor"); // two coins: enough for one
        Assert.Equal(["Aquí tienes el pan."], one.Replies);
        Assert.Equal((1, 1), (session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(ItemId.Bread)));
    }

    [Fact]
    public void AnOrderIsHiddenAndCountsNothingBeforeItsSession()
    {
        var data = WithOrderFirst(Baker.Value);
        var early = NewSession(playSession: 1, data);
        _ = early.Simulation.GrantItems(ItemId.Wood, 5);

        Assert.Equal("meet-guide", early.CurrentObjective!.Id); // the order and the gated objective come first in this HUD's order, and are hidden before session 2
        Assert.Equal(early.Locale.ObjectiveHudLine("meet-guide"), early.ObjectiveHud().Single().Text);
        var opening = early.OpenTalk("baker");
        Assert.False(opening.Delivered); // the goods are there, the order is not yet
        Assert.Equal(["Hola, soy Luz."], opening.Lines);
        Assert.Equal(5, early.Simulation.Inventory.Count(ItemId.Wood));
        early.HostEvent(WorldEvents.ItemPickedUp, "madera");
        Assert.Equal(0, early.ObjectiveStep("gated.wood")!.Count); // a gated objective counts nothing before its session
        Assert.Equal(1, early.ObjectiveStep("camp.wood")!.Count);

        var due = NewSession(playSession: 2, data);
        Assert.Equal("oven-wood", due.CurrentObjective!.Id);
        Assert.Equal("Bring Luz five blocks of wood for her oven.", due.ObjectiveHud().Single().Text);
        due.HostEvent(WorldEvents.ItemPickedUp, "madera");
        Assert.Equal(1, due.ObjectiveStep("gated.wood")!.Count);
    }

    [Fact]
    public void BringingTheGoodsDeliversTheOrderTheBakerThanksAndPaysAndTheObjectiveIsDone()
    {
        var session = NewSession(playSession: 2);
        _ = session.Simulation.GrantItems(ItemId.Wood, 6);
        var records = session.Conversation.RecordCount;

        var opening = session.OpenTalk("baker"); // walking up with the goods is enough: no line is asked for

        Assert.True(opening.Delivered);
        Assert.Equal([Thanks], opening.Lines);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Equal(4, session.Simulation.Inventory.Count(ItemId.Coin));
        Assert.Equal("baker.delivered", Assert.IsType<HostTurnEnvelope>(Assert.Single(session.Conversation.Records.Skip(records)).HostTurn).HostEvent);
        var world = session.Events.OfType<WorldEvent>().Where(static e => e.Name is WorldEvents.ItemTaken or WorldEvents.ItemGiven or WorldEvents.OrderDelivered).ToList();
        Assert.Equal(
            [(WorldEvents.ItemTaken, "baker", "madera, order"), (WorldEvents.ItemGiven, "baker", "moneda, order"), (WorldEvents.OrderDelivered, "baker", "oven-wood")],
            world.Select(static e => (e.Name, e.Npc, e.Detail)));
        Assert.True(session.Objectives.Single(o => o.Id == "oven-wood").Complete);
        Assert.False(session.SessionComplete); // an order is no part of the first session's ending

        // Delivered once: the next talk opens as usual and takes nothing.
        session.EndTalk();
        Assert.Equal(["Hola, soy Luz."], session.OpenTalk("baker").Lines);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void WithoutAllTheGoodsTheTalkOpensAsUsualAndNothingIsTaken()
    {
        var session = NewSession(playSession: 2);
        _ = session.Simulation.GrantItems(ItemId.Wood, 4);

        var opening = session.OpenTalk("baker");

        Assert.False(opening.Delivered);
        Assert.Equal(["Hola, soy Luz."], opening.Lines);
        Assert.Equal(4, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Empty(WorldEventsOf(session, WorldEvents.OrderDelivered));
    }

    [Fact]
    public void ADeliveredOrderAndTheVillagesItemsSurviveASaveAndALoad()
    {
        // No save field is new (V03): the inventory keeps items by name, and the order's one step is keyed oven-wood.delivered like any other step.
        var session = NewSession(playSession: 2, shippedLessons: true);
        _ = session.Simulation.GrantItems(ItemId.Wood, 5);
        _ = session.OpenTalk("baker");
        session.EndTalk();
        _ = session.Simulation.GrantItems(ItemId.Apple, 3);
        _ = session.Simulation.GrantItems(ItemId.Axe, 1);

        var text = SaveGame.Save(session, TestGame.Route());
        Assert.Contains("\"oven-wood.delivered\"", text, StringComparison.Ordinal);
        Assert.Contains("{\"item\":\"Coin\",\"count\":4}", text, StringComparison.Ordinal); // the inventory keeps an item by its name
        var loaded = SaveGame.Load(text, Baker.Value, static seed => TestGame.Route(seed), bumpSession: false);

        Assert.Equal(session.Simulation.Inventory.Slots, loaded.Simulation.Inventory.Slots);
        Assert.Equal((4, 3, 1), (loaded.Simulation.Inventory.Count(ItemId.Coin), loaded.Simulation.Inventory.Count(ItemId.Apple), loaded.Simulation.Inventory.Count(ItemId.Axe)));
        Assert.True(loaded.Objectives.Single(o => o.Id == "oven-wood").Complete);
        Assert.Equal(text, SaveGame.Save(loaded, TestGame.Route()));
    }

    // The baker's bundle data with the order first in the HUD's order, then an objective of session 2 that counts wood picked up, and the order's line in the locale.
    private static GameData WithOrderFirst(GameData data)
    {
        var book = data.Objectives;
        var gated = ObjectiveBook.Parse(
            "gated.json",
            """{ "schemaVersion": 1, "id": "gated", "session": 2, "steps": [ { "id": "wood", "target": 2, "trigger": { "event": "item_picked_up", "detail": "madera" } } ] }""",
            "gated",
            data.Nouns);
        var objectives = book.Objectives.Where(static o => o.IsOrder).Append(gated).Concat(book.Objectives.Where(static o => !o.IsOrder)).ToList();
        var locale = data.Locale.With(new Dictionary<string, string>(StringComparer.Ordinal) { ["objective.oven-wood.hud"] = "Bring Luz five blocks of wood for her oven." });
        return data.WithObjectives(ObjectiveBook.Of(objectives, book.ClosingPool, book.Grants, book.BeaconStart)).WithLocale(locale);
    }

    // The bundle with the baker: copied without the speech folder, the files edited or added as content/README.md says, the manifest computed again, loaded by the
    // core and read by the game without the checks between the files (the copy lacks her scene placement and glosses, which these tests are not about).
    private static GameData BuildBakerBundle()
    {
        var source = GameBundle.ResolveDirectory();
        var copy = Path.Combine(Path.GetTempPath(), "vocaterra-v03-baker-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        try
        {
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file).Replace('\\', '/');
                if (!relative.StartsWith(SpeechLines.FolderName + "/", StringComparison.Ordinal))
                {
                    var target = Path.Combine(copy, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target, overwrite: true);
                }
            }

            File.WriteAllText(Path.Combine(copy, "agents", "baker.json"), BakerAgent);
            File.WriteAllText(Path.Combine(copy, "game", "npcs", "baker.json"), BakerFile);
            File.WriteAllText(Path.Combine(copy, "game", "objectives", "oven-wood.json"), OvenWood);
            Edit(copy, "game/npcs.json", root => root["npcs"]!.AsArray().Add("baker"));
            Edit(copy, "game/session.json", root => root["objectives"]!.AsArray().Add("oven-wood"));
            Edit(copy, "game/host-events.json", root =>
            {
                var events = root["events"]!.AsArray();
                events.Add(Event("baker.sell_bread", new JsonObject { ["kind"] = "sell", ["item"] = "bread", ["price"] = 2 }));
                events.Add(Event("baker.pay", new JsonObject { ["kind"] = "give_item", ["item"] = "coin", ["count"] = 4 }));
            });
            Edit(copy, "prompts/prompts.json", root => root.AsArray().Add(new JsonObject
            {
                ["promptId"] = "baker-open",
                ["mode"] = "Open",
                ["difficulty"] = "Beginner",
                ["answerSetId"] = "supplies",
                ["allowedIntentIds"] = new JsonArray("request_item", "greet", "thank", "farewell", "ask_help", "dont_know"),
                ["agentId"] = "baker",
                ["slotGrammarIds"] = new JsonArray(),
                ["text"] = "¿Qué quieres?",
            }));
            Edit(copy, "dialogue/dialogue.json", root =>
            {
                var pools = root["pools"]!.AsObject();
                pools.Add("baker_hello", Pool("baker_hello", "Hola, soy Luz."));
                pools.Add("baker_sell", Pool("baker_sell", "Aquí tienes el pan."));
                pools.Add("baker_cant_pay", Pool("baker_cant_pay", "Cuesta dos monedas. No tienes monedas."));
                pools.Add("baker_thanks", Pool("baker_thanks", Thanks));
            });
            Edit(copy, "provenance.json", root =>
            {
                var items = root["items"]!.AsArray();
                foreach (var speech in items.Where(static item => item!["path"]!.GetValue<string>().StartsWith(SpeechLines.FolderName + "/", StringComparison.Ordinal)).ToList())
                {
                    items.Remove(speech);
                }

                foreach (var path in new[] { "agents/baker.json", "game/npcs/baker.json", "game/objectives/oven-wood.json" })
                {
                    items.Add(new JsonObject { ["path"] = path, ["generatedBy"] = "hand-authored", ["approvedBy"] = null, ["approvedOn"] = null });
                }
            });
            File.WriteAllText(Path.Combine(copy, ContentPaths.Manifest), ContentManifest.Compute(copy).ToJson());

            var bundle = GameBundle.Load(copy);
            return GameData.Read(bundle, TestGame.Locale);
        }
        finally
        {
            if (Directory.Exists(copy))
            {
                Directory.Delete(copy, recursive: true);
            }
        }

        static JsonObject Event(string name, JsonObject action) => new() { ["npc"] = "baker", ["event"] = name, ["action"] = action };
    }

    private static void Edit(string bundle, string path, Action<JsonNode> change)
    {
        var file = Path.Combine(bundle, path);
        var root = JsonNode.Parse(File.ReadAllText(file))!;
        change(root);
        File.WriteAllText(file, root.ToJsonString());
    }

    private static JsonObject Pool(string id, string text) => new()
    {
        ["poolId"] = id,
        ["variants"] = new JsonArray(new JsonObject { ["lineId"] = id + ".1", ["text"] = text }),
    };
}
