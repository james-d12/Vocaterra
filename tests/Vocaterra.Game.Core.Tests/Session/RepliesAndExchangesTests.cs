using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Language.Lessons;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>
/// What the NPCs say back that was wrong or contradictory (V21): the stall and the villager give only what they have (and say so otherwise), the exchange lines say the real
/// reason, and a turn never gives a recast's praise with "No te entendí".
/// </summary>
public sealed class RepliesAndExchangesTests
{
    // A session without lessons (a lesson would take the NPC's talk), in the market's session, at an NPC's talk.
    private static VoxelSession At(string npc, params (ItemId Item, int Count)[] held)
    {
        var data = TestGame.Data;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: noLessons) { Day = 10 };
        _ = session.TakeOrders(); // the orders are taken: this test is not about their offer (issue #54)
        foreach (var (item, count) in held)
        {
            Assert.Equal(0, session.Simulation.GrantItems(item, count));
        }

        Stage.TeleportToNpc(session, npc);
        _ = session.OpenTalkForClass(npc);
        return session;
    }

    [Theory]
    [InlineData("quiero madera")]
    [InlineData("quiero manzanas")]
    public void TheStallHandsOverNoBottleForAnotherItemAndSaysSoOnlyForWhatItHasNeitherOf(string line)
    {
        var session = At("shopkeeper");

        var turn = session.Say(line);

        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Bottle));
        Assert.Empty(turn.Replies);
        Assert.Equal(["No tengo eso. Tengo agua, pan y harina."], turn.FollowUp!);

        _ = session.Say("quiero agua, por favor");
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle));
    }

    [Fact]
    public void ARequestForBreadAtTheStallGetsTheBreadAndNoRefusal()
    {
        var session = At("shopkeeper");

        var turn = session.Say("quiero pan");

        Assert.Equal(1, session.Simulation.Inventory.Count(TestItems.Bread));
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Bottle));
        Assert.Empty(turn.Replies);
        Assert.Contains(turn.FollowUp!, static l => l is "Aquí tienes el pan." or "Toma el pan.");
        Assert.DoesNotContain(turn.FollowUp!, static l => l.StartsWith("No tengo", StringComparison.Ordinal) || l.StartsWith("Solo", StringComparison.Ordinal));
    }

    [Fact]
    public void TheFarmerAnswersHowMuchIsEverythingWithTheUnitPriceOutsideTheLesson()
    {
        var session = At("farmer", (TestItems.Apple, 3));

        var turn = session.Say("¿Cuánto es todo?");

        Assert.Equal(["Una manzana, una moneda."], turn.Replies);
    }

    [Fact]
    public void TheVillagerHandsOverBreadForBreadOnly()
    {
        var session = At("villager");

        var turn = session.Say("quiero madera");
        Assert.Equal(0, session.Simulation.Inventory.Count(TestItems.Bread));
        Assert.Equal(["Solo tengo pan."], turn.FollowUp);

        _ = session.Say("quiero pan");
        Assert.Equal(1, session.Simulation.Inventory.Count(TestItems.Bread));
    }

    [Fact]
    public void TheFarmerWhoBuysBerriesDoesNotRefuseApplesAsIfTheyWereNotFruit()
    {
        var session = At("farmer", (TestItems.Apple, 2));

        var turn = session.Say("vendo manzanas");

        Assert.Equal(["No compro eso. Compro fruta del bosque."], turn.FollowUp);
    }

    [Fact]
    public void AVerbWithoutTheAccentOfItsTenseIsAcceptedAndTheTurnCarriesTheCoresNote()
    {
        var session = At("farmer");

        var turn = session.Say("ayer compre pan");

        Assert.True(turn.Communicated);
        Assert.Empty(turn.Errors);
        Assert.Equal(["«compré», con tilde: es pasado"], turn.AccentNotes);
        Assert.Empty(session.Say("ayer compré pan").AccentNotes);
    }

    [Fact]
    public void TheBookCountsACorrectUseWhereverItWasUnderstoodFreeTalkIncluded()
    {
        // V21b (9.3 G): «Ayer compré pan» said to León in free talk, outside the prompt of any lesson, is a correct use of comprar and of its form.
        var session = At("farmer");
        _ = session.Say("ayer compré pan");

        var row = session.Learner.Verbs.Single(static v => v.Lemma == "comprar");
        Assert.Equal((1, 1), (row.Times, row.TimesCorrect));
        Assert.Equal(1, row.Forms.Single(static f => f.Form == "compré").TimesCorrect);
    }

    [Fact]
    public void AVerbTypedWithoutTheAccentIsCountedAsTheFormTheCoreRead()
    {
        // V26: the summary and the book's Verbs page counted `compre`, as typed, not `compré`.
        var session = At("farmer");

        _ = session.Say("ayer compre pan");

        var row = session.Learner.Verbs.Single(static v => v.Lemma == "comprar");
        Assert.Equal(["compré"], row.Forms.Select(static f => f.Form));
    }

    [Fact]
    public void ALineThatIsNotUnderstoodGetsNoPraiseWithTheRetry()
    {
        var session = TomorrowLessonTests.AtStep("m2-wood"); // QA-language-9: «Claro, «buscaré madera». Muy bien.» and then «No te entendí» for the same line

        var turn = session.Say("buscaré madeda"); // M70 reads «buscé» as the preterite slip, so an unknown word stands for a line the step does not understand

        Assert.Null(turn.Feedback);
        Assert.Contains(turn.Replies.Concat(turn.FollowUp ?? []), static l => l.StartsWith("No te entendí", StringComparison.Ordinal) || l.StartsWith("Perdón, ¿qué dices?", StringComparison.Ordinal)); // either line of not_understood
    }

    [Fact]
    public void ASpellingSlipOfThePreteriteAtTheLessonIsUnderstoodAndPraisedNotAskedAgain()
    {
        var session = TomorrowLessonTests.AtStep("m2-wood");

        var turn = session.Say("buscé madera");

        Assert.NotNull(turn.Feedback);
        Assert.DoesNotContain(turn.Replies.Concat(turn.FollowUp ?? []), static l => l.StartsWith("No te entendí", StringComparison.Ordinal));
    }

    [Fact]
    public void ARecastsPraiseAndTheNpcsBareAcknowledgementAreNotBothGiven()
    {
        var session = At("farmer");

        var turn = session.Say("compré three manzanas");

        Assert.NotNull(turn.Feedback);
        Assert.DoesNotContain("¡Qué bien!", turn.Replies);
    }
}
