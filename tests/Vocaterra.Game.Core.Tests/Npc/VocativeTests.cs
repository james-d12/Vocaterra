using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Language.Lessons;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// The listener's name said as a vocative (village-plan V14, found by the orchestrator at the core's M59): <c>Luz quiero pan</c> at the baker resolved no intent, <c>hola Luz
/// quiero dos panes</c> only the greeting, <c>Domingo necesito un hacha</c> at the smith nothing, and <c>Mar dónde está la panadería</c> at the fisher asked for the sea.
/// Session 8 teaches the villagers' names, so learners open with them: the game reads a line without its listener's name at its start or its end.
/// </summary>
public sealed class VocativeTests
{
    [Theory]
    [InlineData("Luz, quiero pan", "Luz", "quiero pan")]
    [InlineData("Luz quiero pan", "Luz", "quiero pan")]
    [InlineData("quiero pan, Luz", "Luz", "quiero pan")]
    [InlineData("Quiero pan, Luz.", "Luz", "Quiero pan.")]
    [InlineData("¿Mar, dónde está la panadería?", "Mar", "¿dónde está la panadería?")]
    [InlineData("¿dónde está la herrería, Mar?", "Mar", "¿dónde está la herrería?")]
    [InlineData("hola Luz, quiero dos panes", "Luz", "hola quiero dos panes")] // after a leading greeting
    [InlineData("buenos días Luz quiero pan", "Luz", "buenos días quiero pan")] // after any of Greeting's openings (V19, the review's R4: only hola before)
    [InlineData("Buenas tardes, Mar, ¿dónde está la panadería?", "Mar", "Buenas tardes ¿dónde está la panadería?")]
    [InlineData("buen día León quiero tres manzanas", "León", "buen día quiero tres manzanas")]
    [InlineData("buenas noches Domingo vendo madera", "Domingo", "buenas noches vendo madera")]
    [InlineData("buenos días Luz", "Luz", "buenos días")] // a greeting and the name: the name at the end, as before
    [InlineData("Hola, Luz.", "Luz", "Hola.")]
    [InlineData("gracias Domingo", "Domingo", "gracias")]
    [InlineData("leon quiero tres manzanas", "León", "quiero tres manzanas")] // typed without its accent
    [InlineData("Luz, quiero pan, Luz", "Luz", "quiero pan")]
    public void TheListenersNameAtTheStartOrTheEndIsTakenOut(string line, string name, string without)
    {
        Assert.Equal(without, Vocative.Without(line, name));
    }

    [Theory]
    [InlineData("Luz", "Luz")] // only the name: nothing else to read
    [InlineData("Luz y Mar comieron pan", "Luz")] // joined to another name: the subject
    [InlineData("buenos días, Luz y Mar comieron pan", "Luz")] // after a greeting too (V19)
    [InlineData("buenos días, luz compró pescado", "Mar")] // another NPC's name after a greeting
    [InlineData("me gustó el pan de Luz", "Luz")] // governed by a preposition
    [InlineData("¿qué es la luna?", "Luna")] // after an article
    [InlineData("quiero ver el mar", "Mar")]
    [InlineData("Luz hizo pan", "Domingo")] // another NPC's name: a word of the sentence
    [InlineData("Domingo hizo un hacha", null)] // the listener has no name (the villager)
    [InlineData("quiero pan", "Luz")]
    public void ANameThatIsAWordOfTheSentenceStays(string line, string? name)
    {
        Assert.Equal(line, Vocative.Without(line, name));
    }

    [Fact]
    public void ARecognitionLosesTheNamesWordAndKeepsItsConfidenceAndModel()
    {
        var heard = new AsrResult(
            "luz quiero pan",
            [new AsrHypothesis("luz quiero pan", 0.9)],
            [new AsrWord("luz", 0.1, 0.4, 0.8), new AsrWord("quiero", 0.5, 0.9, 0.95), new AsrWord("pan", 1.0, 1.3, 0.99)],
            1.0,
            "vosk-model",
            RecognitionMode.Constrained);

        var bare = Vocative.Without(heard, "Luz")!;

        Assert.Equal("quiero pan", bare.Transcript);
        Assert.Equal(["quiero", "pan"], bare.Words.Select(static w => w.Text));
        Assert.Equal(("quiero pan", 0.9), (bare.NBest.Single().Text, bare.NBest.Single().Score));
        Assert.Equal((1.0, "vosk-model", RecognitionMode.Constrained), (bare.Confidence, bare.ModelHash, bare.Mode));
        Assert.Null(Vocative.Without(heard, "Mar")); // no vocative of this listener: the recognition is used as it is
    }

    // ---- In the game: free talk and a lesson's step

    private static VoxelSession At(string npc, int coins = 0)
    {
        var session = TestGame.NewSessionWithShippedLessons(); // session 1: no village lesson is unlocked, so each NPC talks as himself
        if (coins > 0)
        {
            Assert.Equal(0, session.Simulation.GrantItems(ItemId.Coin, coins));
        }

        Stage.TeleportToNpc(session, npc);
        _ = session.OpenTalkForClass(npc);
        return session;
    }

    [Theory]
    [InlineData("Luz, quiero dos panes", "quiero dos panes")]
    [InlineData("quiero dos panes, Luz", "quiero dos panes")]
    public void AtTheBakerARequestThatNamesHerIsASale(string line, string bare)
    {
        var named = At("baker", coins: 4);
        var turn = named.Say(line);

        Assert.Equal(line, turn.Line); // the turn keeps the line as it was said
        Assert.Equal(("request_item", true, "2"), (turn.Intent, turn.Communicated, turn.Slots["qty"]));
        Assert.Empty(turn.Errors);
        Assert.Equal((0, 2), (named.Simulation.Inventory.Count(ItemId.Coin), named.Simulation.Inventory.Count(ItemId.Bread))); // two loaves for four coins

        var plain = At("baker", coins: 4).Say(bare);
        Assert.Equal((plain.Intent, plain.Slots["qty"]), (turn.Intent, turn.Slots["qty"])); // as the line without the name
        Assert.Equal(plain.GuideLines, turn.GuideLines);
    }

    [Fact]
    public void AtTheFisherADirectionsQuestionThatNamesHimAsksForThePlaceNotTheSea()
    {
        var session = At("fisher");
        var turn = session.Say("Mar, ¿dónde está la panadería?");

        Assert.Equal(("ask_directions", "panadería"), (turn.Intent, turn.Slots["place"]));
        Assert.Equal("bakery", session.Pointer?.Point); // he points at the bakery
    }

    [Fact]
    public void AtTheSmithANeedThatNamesHimIsAnsweredWithHisOffer()
    {
        var session = At("smith");
        var turn = session.Say("Domingo, necesito un hacha");

        Assert.Equal(("state_want", true), (turn.Intent, turn.Communicated));
        Assert.Empty(turn.Errors);
        Assert.Equal(["¿Quieres un hacha? Necesito dos bloques de madera y dos monedas."], turn.GuideLines);
        Assert.Equal(("state_action", true), (session.Say("Domingo hizo un hacha").Intent, session.Say("hizo un hacha").Communicated)); // a statement at his open prompt (V14)
    }

    [Fact]
    public void AfterAnyGreetingTheListenersNameIsTakenOutAndTheTurnIsWhatWasAsked()
    {
        // V19, the review's R4: the vocative knew only hola of the greetings. "buenos días luz quiero pan" at the baker was not understood, and "buenos días domingo vendo
        // madera" at the smith was a statement about Sunday: no bread, no sale. Sessions open in the morning, so buenos días is the likely greeting.
        var baker = At("baker", coins: 4);
        var bread = baker.Say("buenos días luz quiero pan");
        Assert.Equal(("request_item", true), (bread.Intent, bread.Communicated));
        Assert.Empty(bread.Errors);
        Assert.Equal((2, 1), (baker.Simulation.Inventory.Count(ItemId.Coin), baker.Simulation.Inventory.Count(ItemId.Bread)));

        var smith = At("smith");
        _ = smith.Simulation.GrantItems(ItemId.Wood, 3);
        var wood = smith.Say("buenos días domingo vendo madera");
        Assert.Equal(("offer_item", true), (wood.Intent, wood.Communicated));
        Assert.Empty(wood.Errors);
        Assert.Equal((0, 3), (smith.Simulation.Inventory.Count(ItemId.Wood), smith.Simulation.Inventory.Count(ItemId.Coin)));

        var fisher = At("fisher");
        var way = fisher.Say("buenas tardes mar ¿dónde está la panadería?");
        Assert.Equal(("ask_directions", "panadería"), (way.Intent, way.Slots["place"]));
        Assert.Equal("bakery", fisher.Pointer?.Point);

        var farmer = At("farmer", coins: 5);
        var apples = farmer.Say("buen día león quiero tres manzanas");
        Assert.Equal(("request_item", true), (apples.Intent, apples.Communicated));
        Assert.Empty(apples.Errors);
        Assert.Equal((2, 3), (farmer.Simulation.Inventory.Count(ItemId.Coin), farmer.Simulation.Inventory.Count(ItemId.Apple)));
    }

    [Fact]
    public void AGreetingThatNamesTheListenerIsTheGreetingAsWithoutTheName()
    {
        // "hola Luz quiero dos panes" was the greeting alone; so was "hola quiero dos panes": the core gives one intent a line, and the greeting's keyword wins where no
        // grammar takes the whole line. The name is no longer the reason. Since V15 the game answers the rest of a line a greeting opens (Greeting), so both are the request.
        var named = At("baker").Say("hola Luz, quiero dos panes");
        var plain = At("baker").Say("hola, quiero dos panes");

        Assert.Equal(("request_item", true), (named.Intent, named.Communicated));
        Assert.Equal(plain.Intent, named.Intent);
        Assert.Equal(plain.GuideLines, named.GuideLines);
    }

    [Fact]
    public void AtALessonsStepTheListenersNameIsTakenOutTooAndAnotherNpcsNameStays()
    {
        var session = YesterdayLessonTests.AtStep("a3-ate");
        var turn = session.Say("Domingo, ellos comieron pan"); // said to Domingo

        Assert.Equal(("state_action", true), (turn.Intent, turn.Communicated));
        Assert.Empty(turn.Errors);
        Assert.Equal("a3-must", session.Lessons.Current!.Id);

        var mar = YesterdayLessonTests.AtStep("a1-mar");
        var told = mar.Say("Mar pescó en el río"); // said to the villager: Mar is the subject
        Assert.Contains(told.Words!, static w => w.Text == "mar");
        Assert.Equal("a2-ate", mar.Lessons.Current!.Id);
    }
}
