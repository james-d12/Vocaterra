using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// The innkeeper Rosa (Y06): she offers the room when the first talk opens, «sí» or asking for it takes it (the host event <c>innkeeper.room</c>, the kind
/// <c>grant_lodging</c>), «no sé» or two misses give it anyway (her <c>patience</c>, D-game-27), «No, gracias» leaves the offer open, and a later visit says
/// goodnight and grants nothing again. Every test drives a talk turn of the shipped bundle on the shipped scene.
/// </summary>
public sealed class InnkeeperTests
{
    private const string Offer = "inn-offer";
    private const string Given = "inn-given";

    private static VoxelSession AtTheCounter()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "innkeeper");
        return session;
    }

    private static int Granted(VoxelSession session) => session.Events.OfType<WorldEvent>().Count(static e => e.Name == WorldEvents.LodgingGranted);

    [Fact]
    public void Given_TheFirstTalk_When_ItOpens_Then_RosaOffersTheRoomOnTheOfferPromptAndGivesNothingYet()
    {
        var session = AtTheCounter();

        var opening = session.OpenTalk("innkeeper");

        Assert.True(opening.HostTurn);
        Assert.Equal(["¡Hola! ¿Quieres una habitación?"], opening.Lines);
        Assert.Equal(Offer, opening.Prompt);
        Assert.False(session.LodgingGiven);
        Assert.Equal(0, Granted(session));
    }

    [Theory]
    [InlineData("sí")]
    [InlineData("sí, por favor")]
    [InlineData("Quiero una habitación, por favor.")]
    [InlineData("Una habitación, por favor.")]
    [InlineData("¿Hay una habitación?")]
    [InlineData("quiero una cama")]
    public void Given_TheOffer_When_ThePlayerAcceptsOrAsksForTheRoom_Then_LodgingIsGivenOnceAndTheAgentIsInTheGivenState(string line)
    {
        var session = AtTheCounter();
        _ = session.OpenTalk("innkeeper");

        var turn = session.Say(line);

        Assert.True(turn.Communicated);
        Assert.Equal("given", turn.AgentState);
        Assert.Contains("innkeeper.room", turn.HostEvents);
        Assert.True(session.LodgingGiven);
        Assert.Equal(1, Granted(session));
        Assert.Contains(turn.Replies, static r => r.Contains("gratis", StringComparison.Ordinal));
        Assert.Equal(Given, session.Conversation.PromptOf("innkeeper"));
    }

    [Fact]
    public void Given_TheOffer_When_ThePlayerSaysNoSe_Then_TheRoomIsGivenAtOnce()
    {
        var session = AtTheCounter();
        _ = session.OpenTalk("innkeeper");

        var turn = session.Say("no sé");

        Assert.True(turn.FailForward);
        Assert.Equal("given", turn.AgentState);
        Assert.True(session.LodgingGiven);
        Assert.Equal(1, Granted(session));
    }

    [Fact]
    public void Given_TheOffer_When_TheLearnerMissesTwice_Then_PatienceGivesTheRoomAnyway()
    {
        var session = AtTheCounter();
        _ = session.OpenTalk("innkeeper");

        var first = session.Say("zzz qqq");
        Assert.False(first.FailForward);
        Assert.False(session.LodgingGiven);

        var second = session.Say("azul");

        Assert.True(second.FailForward);
        Assert.True(session.LodgingGiven);
        Assert.Equal(1, Granted(session));
        Assert.Equal("given", second.AgentState);
        Assert.Equal(new NpcPatienceRule(2, 1, "innkeeper.give", Offer, true), TestGame.Data.Npcs.Find("innkeeper")!.Patience);
    }

    [Fact]
    public void Given_ANoThanks_When_TheTalkEndsAndOpensAgain_Then_TheOfferIsStillOpenAndYesTakesIt()
    {
        var session = AtTheCounter();
        _ = session.OpenTalk("innkeeper");

        var turn = session.Say("No, gracias.");

        Assert.Equal("idle", turn.AgentState);
        Assert.False(session.LodgingGiven);
        session.EndTalk();

        var again = session.OpenTalk("innkeeper");
        Assert.False(again.HostTurn); // the second talk opens with the prompt's own text
        Assert.Equal(["¿Quieres una habitación?"], again.Lines);
        Assert.Equal(Offer, again.Prompt);

        _ = session.Say("sí");
        Assert.True(session.LodgingGiven);
        Assert.Equal(1, Granted(session));
    }

    [Fact]
    public void Given_TheRoomGiven_When_ThePlayerComesBackAndSaysYesAgain_Then_RosaSaysGoodnightAndNothingIsGrantedTwice()
    {
        var session = AtTheCounter();
        _ = session.OpenTalk("innkeeper");
        _ = session.Say("sí");
        session.EndTalk();

        var again = session.OpenTalk("innkeeper");
        Assert.Equal(["Buenas noches."], again.Lines);
        Assert.Equal(Given, again.Prompt);
        var turn = session.Say("sí");
        var greeting = session.Say("buenas noches");

        Assert.DoesNotContain("innkeeper.room", turn.HostEvents);
        Assert.DoesNotContain("innkeeper.room", greeting.HostEvents);
        Assert.Equal("given", greeting.AgentState);
        Assert.Equal(1, Granted(session));
        Assert.True(session.LodgingGiven);
    }

    [Fact]
    public void Given_TheRoomGiven_When_TheGameIsSavedAndLoaded_Then_LodgingAndRosasStateSurvive()
    {
        var session = AtTheCounter();
        _ = session.OpenTalk("innkeeper");
        _ = session.Say("sí");
        session.EndTalk();

        var loaded = SaveGame.Load(SaveGame.Save(session, TestGame.Route()), TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.True(loaded.LodgingGiven);
        Assert.Equal(Given, loaded.Conversation.PromptOf("innkeeper"));
    }

    [Fact]
    public void Given_TheShippedBundle_When_ItIsValidated_Then_NoProblemNamesTheInnkeeper()
    {
        var problems = GameDataValidator.Validate(TestGame.Data);

        Assert.DoesNotContain(problems, static p => p.Contains("innkeeper", StringComparison.Ordinal) || p.Contains("inn-offer", StringComparison.Ordinal) || p.Contains("inn-given", StringComparison.Ordinal));
        var npc = TestGame.Data.Npcs.Find("innkeeper")!;
        Assert.Equal("Posadera", npc.Label);
        Assert.Equal(NpcEffectKind.GrantLodging, TestGame.Data.HostEvents.Of("innkeeper", "innkeeper.room").Kind);
        Assert.Equal("innkeeper", TestGame.Data.Npcs.Ids[^1]);
    }
}
