using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Language.Lessons;
using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Speech;

/// <summary>
/// The phrases El mercado asks the learner to say (village-plan V15), spoken: Kokoro says each at its step and Vosk hears it through the worker at the step's prompt (the round
/// trip every content milestone runs once, village-plan section 4). The numbers from 11 to 20, venderé, puedo and barato must be heard; a word the recognizer cannot hear
/// would fail here, and would go to the learner's hearing side or to a typed-only variant. And a greeting before a request, with the baker's name, is heard and answered as
/// the request (V15). Skipped without the models.
/// </summary>
[Collection("speech models")]
public sealed class MarketSpokenTests(ITestOutputHelper output)
{
    private static readonly (string Step, string Spoken, string Intent, string[] MustHear)[] Said =
    [
        ("k1-loaves", "Tienes veinte panes.", "state_have", ["tienes", "veinte", "panes"]),
        ("k2-sell", "Quiero vender madera.", "offer_item", ["quiero", "vender", "madera"]),
        ("k3-sell", "Vendo madera.", "offer_item", ["vendo", "madera"]),
        ("k4-all", "¿Cuánto es todo?", "ask_total", ["cuánto", "todo"]),
        ("k4-dear", "Es muy caro.", "describe", ["muy", "caro"]),
        ("k5-cheaper", "La manzana es más barata que el pan.", "describe", ["manzana", "más", "barata", "que", "pan"]),
        ("k5-fruit", "¿Puedo pagar con fruta?", "ask_pay", ["puedo", "pagar", "fruta"]),
        ("k5-tomorrow", "Mañana venderé fruta.", "state_action", ["mañana", "venderé", "fruta"]),
        ("k7-bought", "Compré tres manzanas.", "state_action", ["compré", "tres", "manzanas"]),
    ];

    private static readonly string[] Numbers = ["once", "doce", "trece", "catorce", "quince", "dieciséis", "diecisiete", "dieciocho", "diecinueve", "veinte"];

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void ElMercadoPhrasesAreHeardWordForWordAtTheirSteps()
    {
        foreach (var (stepId, spoken, intent, mustHear) in Said)
        {
            using var session = MarketLessonTests.AtStep(stepId);
            Assert.True(session.StartWorker().WaitUntilReady(60000));

            _ = session.SubmitSpeak(spoken);
            var turn = Drain(session).Turn!;

            output.WriteLine($"{stepId}: said '{spoken}', heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Intent}, errors [{string.Join(", ", turn.Errors.Select(static e => e.Tag))}]");
            Assert.Equal(intent, turn.Intent);
            Assert.True(turn.Communicated, stepId);
            Assert.Empty(turn.Errors);
            foreach (var word in mustHear)
            {
                Assert.Contains(word, turn.Line, StringComparison.OrdinalIgnoreCase);
            }

            Assert.NotEqual(stepId, session.Lessons.Current?.Id); // the step is done
        }
    }

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void TheNumbersFromElevenToTwentyAreHeardInARequest()
    {
        // At the villager's open prompt (open recognition, no answer set to steer it), each number said in a quantity: the recognizer hears the number word.
        using var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");
        Assert.True(session.StartWorker().WaitUntilReady(60000));

        var missed = new List<string>();
        foreach (var number in Numbers)
        {
            _ = session.SubmitSpeak($"Quiero {number} panes.");
            var turn = Drain(session).Turn!;
            output.WriteLine($"'{number}': heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Intent}, slots {string.Join(", ", turn.Slots.Select(static s => $"{s.Key}={s.Value}"))}");
            if (!PhraseSheet.Plain(turn.Line).Contains(PhraseSheet.Plain(number), StringComparison.Ordinal)) // the recognizer writes dieciseis without its accent
            {
                missed.Add(number);
            }
        }

        Assert.Empty(missed);
    }

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void AGreetingAndHerNameBeforeARequestAreHeardAndTheBreadIsBought()
    {
        var data = TestGame.Data;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        using var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: noLessons) { Day = 10 };
        _ = session.Simulation.GrantItems(ItemId.Coin, 4);
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalk("baker");
        Assert.True(session.StartWorker().WaitUntilReady(60000));

        _ = session.SubmitSpeak("Hola Luz, quiero dos panes.");
        var turn = Drain(session).Turn!;

        output.WriteLine($"baker-open: heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Intent}, errors [{string.Join(", ", turn.Errors.Select(static e => e.Tag))}]");
        Assert.Equal(("request_item", true), (turn.Intent, turn.Communicated));
        Assert.Equal(2, session.Simulation.Inventory.Count(ItemId.Bread));
    }

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void AMorningGreetingAndHerNameBeforeARequestAreHeardAndTheBreadIsBought()
    {
        // V19, the review's R4: the name after a two-word greeting was kept, so the typed line was not understood; the spoken path reads a line as the typed one does.
        var data = TestGame.Data;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        using var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: noLessons) { Day = 10 };
        _ = session.Simulation.GrantItems(ItemId.Coin, 4);
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalk("baker");
        Assert.True(session.StartWorker().WaitUntilReady(60000));

        _ = session.SubmitSpeak("Buenos días, Luz, quiero dos panes.");
        var turn = Drain(session).Turn!;

        output.WriteLine($"baker-open: heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Intent}, errors [{string.Join(", ", turn.Errors.Select(static e => e.Tag))}]");
        Assert.Equal(("request_item", true), (turn.Intent, turn.Communicated));
        Assert.Equal(2, session.Simulation.Inventory.Count(ItemId.Bread));
    }

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void ATwoWordGreetingBeforeAQuestionIsHeardAndNotFlaggedAndBlocksOfWoodAreSold()
    {
        // V15b: the greeting's own words were vocab.wrong when the whole line read as the price; and wood counted in blocks was not understood.
        var data = TestGame.Data;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        using var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: noLessons) { Day = 10 };
        _ = session.Simulation.GrantItems(ItemId.Wood, 4); // four: five would deliver the oven order as the talk opens
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalk("baker");
        Assert.True(session.StartWorker().WaitUntilReady(60000));

        _ = session.SubmitSpeak("Buenas tardes, ¿cuánto cuesta el pan?");
        var price = Drain(session).Turn!;
        output.WriteLine($"baker-open: heard '{price.Line}' ({price.Confidence:0.00}) -> {price.Intent}, errors [{string.Join(", ", price.Errors.Select(static e => e.Tag))}]");
        Assert.Equal(("ask_price", true), (price.Intent, price.Communicated));
        Assert.Empty(price.Errors);

        _ = session.SubmitSpeak("Vendo tres bloques de madera.");
        var sale = Drain(session).Turn!;
        output.WriteLine($"baker-open: heard '{sale.Line}' ({sale.Confidence:0.00}) -> {sale.Intent}, errors [{string.Join(", ", sale.Errors.Select(static e => e.Tag))}]");
        Assert.Equal(("offer_item", true), (sale.Intent, sale.Communicated));
        Assert.Empty(sale.Errors);
        Assert.Equal((1, 3), (session.Simulation.Inventory.Count(ItemId.Wood), session.Simulation.Inventory.Count(ItemId.Coin)));
    }

    private static AppliedTurn Drain(VoxelSession session)
    {
        var deadline = Environment.TickCount64 + 120000;
        while (true)
        {
            if (session.Collect().FirstOrDefault() is { } applied)
            {
                return applied;
            }

            Assert.True(Environment.TickCount64 < deadline, "The turn did not come back.");
            Thread.Sleep(5);
        }
    }
}
