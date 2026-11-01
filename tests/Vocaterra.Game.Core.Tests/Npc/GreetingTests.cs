using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// A greeting that opens a line with something else to say (VILLAGE_PLAN V15, found at V14): the core reads one intent per line and read <c>hola, quiero dos panes</c> as
/// the greeting alone, so nothing was bought. The game answers the rest (<see cref="Greeting"/>): the rest is read as a line of its own, and when it is an intent the prompt
/// takes, the turn is that intent's. The greeting credits nothing. Since V15b a greeting's own words are never flagged when the rest is understood: a line read as the
/// rest's intent with vocab.wrong on the greeting is answered by the rest, and a real error after the greeting stays.
/// </summary>
public sealed class GreetingTests
{
    [Theory]
    [InlineData("hola, quiero dos panes", "quiero dos panes")]
    [InlineData("¡Hola! Quiero dos panes.", "Quiero dos panes.")]
    [InlineData("Hola quiero pan", "quiero pan")]
    [InlineData("buenos días, ¿cuánto cuesta el pan?", "¿cuánto cuesta el pan?")]
    [InlineData("Buenas tardes, vendo madera", "vendo madera")]
    [InlineData("buen día quiero pan", "quiero pan")]
    [InlineData("hola amigo", "amigo")] // a rest the core does not take as an intent leaves the line a greeting (below)
    [InlineData("[unk] hola quiero dos panes", "quiero dos panes")] // the recognizer's unknown token before the greeting goes with it (the spoken round trip)
    public void TheRestIsTheLineAfterAnOpeningGreeting(string line, string rest) => Assert.Equal(rest, Greeting.Rest(line));

    [Fact]
    public void TheListenersNameAfterTheRecognizersUnknownTokenAndAGreetingIsTakenOut() =>
        Assert.Equal("[unk] hola quiero dos panes", Vocative.Without("[unk] hola luz quiero dos panes", "Luz")); // heard so in the spoken round trip (V15)

    [Theory]
    [InlineData("hola")]
    [InlineData("¡Hola!")]
    [InlineData("buenos días")]
    [InlineData("quiero dos panes")]
    [InlineData("quiero pan, hola")]
    [InlineData("holanda")]
    public void ALineThatIsOnlyAGreetingOrOpensWithNoneHasNoRest(string line) => Assert.Null(Greeting.Rest(line));

    // A session at the bakery, without lessons (a lesson would take the baker's talk), with the coins of two loaves.
    private static VoxelSession AtTheBakery(int coins = 4)
    {
        var data = TestGame.Data;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: noLessons) { PlaySession = 10 };
        _ = session.Simulation.GrantItems(ItemId.Coin, coins);
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalk("baker");
        return session;
    }

    [Theory]
    [InlineData("hola, quiero dos panes")] // the orchestrator's case at V14: greet, and no bread
    [InlineData("hola Luz quiero dos panes")] // the name taken out first (V14), then the greeting
    [InlineData("¡Hola, Luz! Quiero dos panes, por favor.")]
    [InlineData("buenos días, quiero dos panes")]
    public void AGreetingBeforeARequestIsAnsweredAsTheRequestAndTheBreadIsBought(string line)
    {
        var session = AtTheBakery();

        var turn = session.Say(line);

        Assert.Equal(("request_item", true), (turn.Intent, turn.Communicated));
        Assert.Equal(line, turn.Line); // the turn keeps the line as it was said
        Assert.Empty(turn.Errors);
        Assert.EndsWith("¡Gracias!", Assert.Single(turn.Replies), StringComparison.Ordinal); // her sale's line, not her answer to a greeting (¿Qué quieres?)
        Assert.Equal((2, 0), (session.Simulation.Inventory.Count(ItemId.Bread), session.Simulation.Inventory.Count(ItemId.Coin)));
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e is { Name: WorldEvents.ItemGiven, Npc: "baker", Detail: "pan, request" });
    }

    [Theory]
    [InlineData("hola, ¿cuánto cuesta el pan?", "ask_price", "El pan cuesta dos monedas.")]
    [InlineData("hola, vendo madera", "offer_item", "No tienes madera.")] // a sale: she answers it (the bag holds no wood)
    public void AGreetingBeforeAnotherQuestionOrOfferIsAnsweredAsThatToo(string line, string intent, string answer)
    {
        var turn = AtTheBakery().Say(line);

        Assert.Equal((intent, true), (turn.Intent, turn.Communicated));
        Assert.Contains(answer, turn.Replies.Concat(turn.FollowUp ?? []));
    }

    [Theory]
    [InlineData("hola")]
    [InlineData("hola Luz")]
    [InlineData("buenos días")]
    [InlineData("hola, buenas tardes")] // the rest is a greeting too
    [InlineData("hola amigo")] // the rest is no intent: the line stays a greeting
    public void AGreetingAloneOrWithNothingElseTheBakerTakesStaysAGreeting(string line)
    {
        var session = AtTheBakery();

        var turn = session.Say(line);

        Assert.Equal(("greet", true), (turn.Intent, turn.Communicated));
        Assert.Equal(["¿Qué quieres?"], turn.Replies);
        Assert.Equal(4, session.Simulation.Inventory.Count(ItemId.Coin));
    }

    [Theory]
    [InlineData("baker", "buenas tardes, ¿cuánto cuesta el pan?", "ask_price")] // the orchestrator's case: the price, with vocab.wrong on buenas before V15b
    [InlineData("baker", "buenos días, ¿cuánto cuesta el pan?", "ask_price")]
    [InlineData("baker", "buen día, ¿cuánto cuesta el pan?", "ask_price")]
    [InlineData("baker", "buenas noches, ¿cuánto cuesta el pan?", "ask_price")]
    [InlineData("baker", "buenas tardes, ¿hay pan?", "ask_availability")]
    [InlineData("baker", "buenos días, ¿cuánto es todo?", "ask_total")]
    [InlineData("baker", "buen día, vendo tres bloques de madera", "offer_item")]
    [InlineData("farmer", "buen día, quiero tres manzanas", "request_item")]
    [InlineData("farmer", "buenas tardes, ¿cuánto es todo?", "ask_total")]
    [InlineData("farmer", "buenos días, ¿puedo pagar con fruta?", "ask_pay")]
    [InlineData("farmer", "buenas noches, vendo fruta", "offer_item")]
    [InlineData("smith", "buen día, ¿cómo te llamas?", "ask_name")]
    public void AGreetingsOwnWordsAreNeverFlaggedWhenTheRestIsUnderstood(string npc, string line, string intent)
    {
        var turn = At(npc).Say(line);

        Assert.Equal((intent, true), (turn.Intent, turn.Communicated));
        Assert.Empty(turn.Errors);
        Assert.Null(turn.Feedback); // no recast of the greeting («Buenos días cuánto cuesta el pan»)
    }

    [Theory]
    [InlineData("baker", "buenos días, ¿cuánto cuestan el pan?", "ask_price", "conj.person", "cuesta")] // a real error after the greeting stays, alone
    [InlineData("farmer", "buenos días, ¿podo pagar con fruta?", "ask_pay", "conj.irregular", "puedo")]
    [InlineData("baker", "buenas tardes, vendo tres bloque de madera", "offer_item", "noun.number", "bloques")]
    public void AnErrorAfterTheGreetingStaysAndIsTheOnlyOne(string npc, string line, string intent, string tag, string expected)
    {
        var turn = At(npc).Say(line);

        Assert.Equal((intent, true), (turn.Intent, turn.Communicated));
        Assert.Equal((tag, expected), Assert.Single(turn.Errors) is var error ? (error.Tag, error.Expected) : default);
    }

    // A session at an NPC, without lessons.
    private static VoxelSession At(string npc)
    {
        var data = TestGame.Data;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: noLessons) { PlaySession = 10 };
        Stage.TeleportToNpc(session, npc);
        _ = session.OpenTalk(npc);
        return session;
    }

    [Fact]
    public void ARestThePromptDoesNotTakeLeavesTheGreeting()
    {
        // The stall's question takes requests, prices and availability, not a sale: "hola, vendo madera" there stays the greeting it was.
        var data = TestGame.Data;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: noLessons) { PlaySession = 10 };
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");

        var turn = session.Say("hola, vendo madera");

        Assert.NotEqual("offer_item", turn.Intent);
    }
}
