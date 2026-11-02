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
        foreach (var (item, count) in held)
        {
            Assert.Equal(0, session.Simulation.GrantItems(item, count));
        }

        Stage.TeleportToNpc(session, npc);
        _ = session.OpenTalk(npc);
        return session;
    }

    [Theory]
    [InlineData("quiero madera")]
    [InlineData("quiero pan")]
    public void TheStallHandsOverNoBottleForAnotherItemAndSaysSo(string line)
    {
        var session = At("shopkeeper");

        var turn = session.Say(line);

        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Bottle));
        Assert.Empty(turn.Replies);
        Assert.Equal(["Solo tengo agua."], turn.FollowUp!);

        _ = session.Say("quiero agua, por favor");
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle));
    }

    [Fact]
    public void TheVillagerHandsOverBreadForBreadOnly()
    {
        var session = At("villager");

        var turn = session.Say("quiero madera");
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Bread));
        Assert.Equal(["Solo tengo pan."], turn.FollowUp);

        _ = session.Say("quiero pan");
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bread));
    }

    [Fact]
    public void TheFarmerWhoBuysBerriesDoesNotRefuseApplesAsIfTheyWereNotFruit()
    {
        var session = At("farmer", (ItemId.Apple, 2));

        var turn = session.Say("vendo manzanas");

        Assert.Equal(["No compro eso. Compro fruta del bosque."], turn.FollowUp);
    }

    [Fact]
    public void ALineThatIsNotUnderstoodGetsNoPraiseWithTheRetry()
    {
        var session = TomorrowLessonTests.AtStep("m2-wood"); // QA-language-9: «Claro, «buscaré madera». Muy bien.» and then «No te entendí» for the same line

        var turn = session.Say("buscé madera");

        Assert.Null(turn.Feedback);
        Assert.Contains(turn.Replies.Concat(turn.FollowUp ?? []), static l => l.StartsWith("No te entendí", StringComparison.Ordinal));
    }

    [Fact]
    public void ARecastsPraiseAndTheNpcsBareAcknowledgementAreNotBothGiven()
    {
        var session = At("farmer");

        var turn = session.Say("compré three manzanas");

        Assert.NotNull(turn.Feedback);
        Assert.DoesNotContain("¡Qué bien!", turn.Replies);
    }

    [Fact]
    public void ALessonThatKeepsBackTheOrderIsTheReasonTheBakerGivesForShortCoins()
    {
        var session = BakeryLessonTests.AtStep("n4-wood");
        Assert.Equal(0, session.Simulation.GrantItems(ItemId.Wood, 5));
        _ = session.Simulation.GrantItems(ItemId.Coin, 1);

        var turn = session.Say("Quiero dos panes, por favor");

        Assert.Contains("Primero, la lección. Luego, las monedas.", turn.GuideLines);
    }
}
