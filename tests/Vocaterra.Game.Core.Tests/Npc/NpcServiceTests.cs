using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Component;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// The NPC conversations of S6 through the real core: the opening as a host turn in the replay record, the shopkeeper's bottle and bread, the
/// villager's beacon on the river, the fail-forward that withholds nothing, and the transfer moment. Each test teleports the player next to the
/// NPC (the walking is tested by <see cref="RouteRunTests"/>) and types the lines.
/// </summary>
public sealed class NpcServiceTests
{
    private static readonly string[] GiveLines = ["Toma la botella. El agua está en el río.", "Aquí tienes la botella. El agua está en el río."];

    private static VoxelSession AtStall(out NpcOpening opening)
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "shopkeeper");
        opening = session.OpenTalk("shopkeeper");
        return session;
    }

    private static IEnumerable<PhraseUse> Phrases(VoxelSession session) => session.Events.OfType<PhraseUse>();

    private static IEnumerable<WorldEvent> WorldEventsOf(VoxelSession session, string name) => session.Events.OfType<WorldEvent>().Where(e => e.Name == name);

    [Fact]
    public void EveryNpcOpensWithItsPromptOrAHostTurnThroughAdvance()
    {
        var session = TestGame.NewSession();

        Stage.TeleportToNpc(session, "shopkeeper");
        var shop = session.OpenTalk("shopkeeper");
        Assert.True(shop.HostTurn);
        Assert.Equal(["¿Qué necesitas?"], shop.Lines);
        Assert.Equal("stall-need", shop.Prompt);
        Assert.Equal("asking", session.Conversation.ContextOf("shopkeeper").AgentState.StateId);

        Stage.TeleportToNpc(session, "villager");
        var villager = session.OpenTalk("villager");
        Assert.True(villager.HostTurn);
        Assert.Equal(["Hola."], villager.Lines);

        Stage.TeleportToNpc(session, "guide");
        var guide = session.OpenTalk("guide");
        Assert.False(guide.HostTurn); // no host turn before the objective is complete: her prompt's text opens
        Assert.Equal(["¡Hola! Soy la guía. ¿En qué te ayudo?"], guide.Lines);
        Assert.Equal("idle", session.Conversation.GuideState.StateId);
    }

    [Fact]
    public void TheOpeningsAreHostTurnsInTheReplayRecordAndReplayWithoutADiff()
    {
        var session = AtStall(out _);
        var record = Assert.Single(session.Conversation.Records);
        var envelope = Assert.IsType<HostTurnEnvelope>(record.HostTurn);
        Assert.Equal("shopkeeper.ask", envelope.HostEvent);
        Assert.Equal("asking", envelope.Result.Agent.Next.StateId);
        Assert.Equal(session.Simulation.ComputeStateDigest(), envelope.Result.HostStateHash); // the world's digest when the player walked up

        Assert.Null(Replayer.Replay(envelope, Pipeline()));
    }

    [Fact]
    public void ARequestAtTheStallHandsOverTheEmptyBottleSendsToTheRiverAndOffersBread()
    {
        var session = AtStall(out _);

        var turn = session.Say("quiero agua, por favor");

        Assert.Equal("request_item", turn.Intent);
        Assert.Equal("shopkeeper", turn.Npc);
        Assert.Equal("stall-need", turn.Prompt);
        Assert.Contains("stall.give_water", turn.HostEvents);
        Assert.False(turn.FailForward);
        Assert.Contains(turn.Replies[0], GiveLines);
        Assert.Equal("¿Quieres pan?", turn.Replies[1]);
        Assert.Equal("offering", turn.AgentState);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle));
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.WaterBottle));
        Assert.Equal("botella", Assert.Single(session.TakeWordCards()).Lemma);
        var use = Assert.Single(Phrases(session));
        Assert.Equal((PhraseIds.RequestWater, PhraseUseKind.Supported, "shopkeeper", "agua"), (use.PhraseId, use.Kind, use.Npc, use.Detail));
    }

    [Fact]
    public void TheBreadOfferGivesBreadOnYesAndNothingOnNo()
    {
        var yes = AtStall(out _);
        _ = yes.Say("quiero agua, por favor");
        var accepted = yes.Say("sí, por favor");
        Assert.Equal("confirm", accepted.Intent);
        Assert.Contains("stall.give_bread", accepted.HostEvents);
        Assert.Equal(1, yes.Simulation.Inventory.Count(ItemId.Bread));
        Assert.Equal("done", accepted.AgentState);
        Assert.Equal("pan", yes.TakeWordCards().Last().Lemma);
        Assert.Equal((PhraseIds.YesPlease, PhraseUseKind.Supported), (Phrases(yes).Last().PhraseId, Phrases(yes).Last().Kind));

        var no = AtStall(out _);
        _ = no.Say("quiero agua, por favor");
        var declined = no.Say("no, gracias");
        Assert.Equal("deny", declined.Intent);
        Assert.Empty(declined.HostEvents);
        Assert.Equal(0, no.Simulation.Inventory.Count(ItemId.Bread));
        Assert.Equal(1, no.Simulation.Inventory.Count(ItemId.Bottle)); // the bottle was given before the offer
        Assert.Equal((PhraseIds.NoThanks, PhraseUseKind.Supported), (Phrases(no).Last().PhraseId, Phrases(no).Last().Kind));
    }

    [Fact]
    public void TheVillagerPointsTheBeaconAtTheNearestRiverBlock()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");

        var turn = session.Say("¿dónde está el río?");

        Assert.Equal("ask_directions", turn.Intent);
        Assert.Equal(["villager.point"], turn.HostEvents);
        var pointer = session.Pointer;
        Assert.NotNull(pointer);
        Assert.Equal("río", pointer.Place);
        Assert.False(pointer.Quiz);
        Assert.Equal(BlockId.Water, session.Scene.Grid.GetBlock(pointer.Target.X, pointer.Target.Y, pointer.Target.Z));
        Assert.Equal(Stage.NearestRiver(session), pointer.Target.ToBlock());
        Assert.True(session.RiverPointedOut);
        var beacon = Assert.Single(WorldEventsOf(session, WorldEvents.BeaconSet));
        Assert.Equal("villager", beacon.Npc);
        var use = Assert.Single(Phrases(session));
        Assert.Equal((PhraseIds.WhereIsTheRiver, PhraseUseKind.Supported, "villager", "río"), (use.PhraseId, use.Kind, use.Npc, use.Detail)); // S8: the card was up
    }

    [Fact]
    public void AskingForHelpAtTheStallModelsTheAnswerAndASecondLineThatIsNoRequestGetsTheBottleAnyway()
    {
        var session = AtStall(out _);

        var first = session.Say("no sé");
        Assert.Equal("dont_know", first.Intent);
        Assert.Equal(["Puedes decir: «Quiero agua, por favor»."], first.Replies);
        Assert.False(first.FailForward);
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Bottle));

        var second = session.Say("ayuda");

        Assert.True(second.FailForward);
        Assert.Equal(["Puedes decir: «Quiero agua, por favor»."], second.Replies);
        Assert.Contains(second.FollowUp![0], GiveLines);
        Assert.Equal("¿Quieres pan?", second.FollowUp![1]);
        Assert.Contains("stall.give_water", second.HostEvents);
        Assert.Equal("offering", second.AgentState);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle));
        Assert.Equal(second.FollowUp, second.GuideLines.TakeLast(2));
        var uses = Phrases(session).Select(static u => (u.PhraseId, u.Kind)).ToArray();
        Assert.Equal(
            [(PhraseIds.DontKnow, PhraseUseKind.DontKnow), (PhraseIds.Help, PhraseUseKind.DontKnow), (PhraseIds.RequestWater, PhraseUseKind.FailForward)],
            uses);
        Assert.DoesNotContain(Phrases(session), static u => u.PhraseId == PhraseIds.RequestWater && u.Kind != PhraseUseKind.FailForward); // not a success
        var shopkeeper = Assert.Single(session.Conversation.Records, static r => r.HostTurn?.HostEvent == "shopkeeper.give");
        Assert.Null(Replayer.Replay(shopkeeper.HostTurn!, Pipeline()));
    }

    [Fact]
    public void WhenTheRepairLadderRunsOutTheBottleIsGivenAndTheLogSaysFailForward()
    {
        var session = AtStall(out _);
        var kinds = new List<RepairKind?>();
        GuideTurn last = null!;
        for (var i = 0; i < 4; i++)
        {
            last = session.Say("zzz qqq");
            kinds.Add(last.RepairKind);
        }

        Assert.Equal([RepairKind.AskAgain, RepairKind.SlowRepeat, RepairKind.OfferChoices, RepairKind.GiveUp], kinds);
        Assert.True(last.FailForward);
        Assert.Equal("Bueno, no importa.", last.Repair);
        Assert.Contains(last.FollowUp![0], GiveLines);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle));
        Assert.Equal(PhraseUseKind.FailForward, Phrases(session).Last().Kind);
        Assert.DoesNotContain(Phrases(session), static u => u.Kind == PhraseUseKind.Supported);
    }

    [Fact]
    public void ALineAfterTheChoicesAreOfferedIsLoggedAsAChoice()
    {
        var session = AtStall(out _);
        _ = session.Say("zzz qqq");
        _ = session.Say("zzz qqq");
        var offered = session.Say("zzz qqq");
        Assert.Equal(RepairKind.OfferChoices, offered.RepairKind);
        Assert.StartsWith("¿Quieres decir", offered.Repair, StringComparison.Ordinal);

        var chosen = session.Say("quiero agua por favor");

        Assert.Equal("request_item", chosen.Intent);
        Assert.Contains("stall.give_water", chosen.HostEvents);
        Assert.False(chosen.FailForward);
        Assert.Equal(PhraseUseKind.Choice, Phrases(session).Last().Kind);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle));
    }

    [Fact]
    public void WalkingAwayAndComingBackHandsOverTheBottleOnTheSecondVisit()
    {
        var session = AtStall(out _);
        session.EndTalk(); // the player left without asking for anything

        var second = session.OpenTalk("shopkeeper");

        Assert.True(second.HostTurn);
        Assert.True(second.FailForward);
        Assert.Contains("stall.give_water", second.HostEvents);
        Assert.Contains(second.Lines[0], GiveLines);
        Assert.Equal("¿Quieres pan?", second.Lines[1]);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle));
        Assert.Equal("stall-offer", second.Prompt);
        Assert.Equal(PhraseUseKind.FailForward, Phrases(session).Single().Kind);
        Assert.Equal(["shopkeeper.ask", "shopkeeper.give"], session.Conversation.Records.Select(static r => r.HostTurn!.HostEvent));
    }

    [Fact]
    public void TheBottleIsGivenOnceAndAnotherRequestDoesNotDoubleIt()
    {
        var session = AtStall(out _);
        _ = session.Say("quiero agua, por favor");
        _ = session.Say("sí, por favor");
        session.EndTalk();
        var back = session.OpenTalk("shopkeeper"); // done -> asking again with the question
        Assert.Equal(["¿Qué necesitas?"], back.Lines);
        Assert.False(back.FailForward);

        var again = session.Say("quiero agua, por favor");

        Assert.Contains("stall.give_water", again.HostEvents);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle)); // not a second bottle
        Assert.DoesNotContain(again.Replies, static r => r.Contains("botella", StringComparison.Ordinal)); // and the line that gives it is not said
        Assert.Equal("¿Quieres pan?", Assert.Single(again.Replies));
    }

    [Fact]
    public void AnUnclearLineAtTheStallBlocksNothingAndKeepsTheQuestionOpen()
    {
        var session = AtStall(out _);

        var turn = session.Say("blah");

        Assert.False(turn.Communicated);
        Assert.Equal("asking", turn.AgentState);
        Assert.Equal(RepairKind.AskAgain, turn.RepairKind);
        Assert.False(turn.FailForward);
        Assert.Empty(session.Events.OfType<PhraseUse>());
        var request = session.Say("quiero agua, por favor"); // the next line is judged like the first
        Assert.Contains("stall.give_water", request.HostEvents);
    }

    [Fact]
    public void TheVillagerGreetsAsAHostTurnOnEveryVisitEvenAfterPointing()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");
        _ = session.Say("¿dónde está el río?");
        Assert.Equal("pointing", session.Conversation.ContextOf("villager").AgentState.StateId);
        session.EndTalk();

        var again = session.OpenTalk("villager");

        Assert.True(again.HostTurn);
        Assert.Equal(["Hola."], again.Lines);
        Assert.Equal("pointing", session.Conversation.ContextOf("villager").AgentState.StateId);
        Assert.Equal(["villager.greet", "villager.greet"], session.Conversation.Records.Select(static r => r.HostTurn?.HostEvent).OfType<string>());
    }

    [Fact]
    public void ThePanelStateFollowsTheAgentStateForThePrompt()
    {
        var session = AtStall(out _);
        Assert.Equal("stall-need", session.Conversation.PromptOf("shopkeeper"));
        _ = session.Say("quiero agua, por favor");
        Assert.Equal("stall-offer", session.Conversation.PromptOf("shopkeeper"));
        Assert.Equal("¿Quieres pan?", session.Conversation.PromptTextOf("shopkeeper"));
        Assert.Equal("villager-open", session.Conversation.PromptOf("villager"));
        Assert.Equal("guide-open", session.Conversation.PromptOf("guide"));
        Assert.Equal("guide-review", session.Conversation.PromptOf("guide", review: true));
    }

    [Fact]
    public void TheGuideKeepsHerThreeDBehaviourWhenTheOtherNpcsExist()
    {
        var session = TestGame.NewSession();

        var turn = session.Say("¿dónde está el árbol?");

        Assert.Equal("guide", turn.Npc);
        Assert.Equal("ask_directions", turn.Intent);
        Assert.Equal(["guide.point"], turn.HostEvents);
        Assert.NotNull(turn.Pointer);
        Assert.Equal("árbol", session.Pointer!.Place);
        Assert.Equal("pointing", session.Conversation.GuideState.StateId);
        Assert.Equal("idle", session.Conversation.ContextOf("shopkeeper").AgentState.StateId); // the others did not move
        Assert.Equal("idle", session.Conversation.ContextOf("villager").AgentState.StateId);
    }

    [Fact]
    public void EachAgentKeepsItsOwnLearnerModelAndRepeatCounts()
    {
        var session = AtStall(out _);
        _ = session.Say("zzz qqq");
        _ = session.Say("zzz qqq");

        Assert.Equal(2, session.Conversation.ContextOf("shopkeeper").RepeatCountsByTag["not_communicated"]);
        Assert.Empty(session.Conversation.ContextOf("villager").RepeatCountsByTag);
        Assert.Empty(session.Conversation.Context.RepeatCountsByTag);
    }

    [Fact]
    public void AfterATurnTheContextTakesTheWorldsDigestAgainAndTheRecordHoldsTheFactsDigest()
    {
        var session = TestGame.NewSession();
        var digest = session.Simulation.ComputeStateDigest();

        _ = session.Say("¿dónde está el árbol?");

        Assert.Equal(digest, session.Conversation.Context.HostStateHash);
        var envelope = Assert.IsType<Vocaterra.Nlp.Pipeline.ReplayEnvelope>(session.Conversation.Records.Last().Utterance);
        var slots = envelope.Context.HostSlots!.ToDictionary(static p => p.Key, static p => p.Value, StringComparer.Ordinal);
        Assert.Equal(HostDigest.Of(digest, slots, envelope.Context.ExtraSkills!), envelope.Context.HostStateHash);
        Assert.Equal(envelope.Context.HostStateHash, envelope.Result.Replay.HostStateHash);
    }

    [Fact]
    public void TheHostEventTableCoversTheEventsTheAgentsRaiseThatChangeTheWorld()
    {
        var raised = new HashSet<(string Npc, string HostEvent)>();
        var table = TestGame.Data.HostEvents;
        foreach (var npc in TestGame.Data.Npcs.Ids)
        {
            foreach (var state in TestGame.Bundle.Agents[npc].Definition.States)
            {
                foreach (var transition in state.Transitions)
                {
                    foreach (var action in transition.Actions ?? [])
                    {
                        if (action.HostEvent is { } hostEvent)
                        {
                            raised.Add((npc, hostEvent));
                        }
                    }
                }
            }
        }

        foreach (var (npc, hostEvent) in table.Mapped())
        {
            Assert.Contains((npc, hostEvent), raised);
        }

        // The ones the table does not map are notes: the guide's own world lines, greetings, the offer being made.
        foreach (var (npc, hostEvent) in raised.Where(r => !table.Mapped().Contains(r)))
        {
            Assert.Equal(NpcEffectKind.None, table.Of(npc, hostEvent).Kind);
        }

        // C08: game/host-events.json gives the five actions the code's table gave.
        Assert.Equal(5, table.Mapped().Count);
        Assert.Equal(NpcEffectKind.GiveItem, table.Of("shopkeeper", "stall.give_water").Kind);
        Assert.Equal(ItemId.Bottle, table.Of("shopkeeper", "stall.give_water").Item);
        Assert.Equal(ItemId.Bread, table.Of("shopkeeper", "stall.give_bread").Item);
        Assert.Equal(ItemId.Bread, table.Of("villager", "villager.give").Item);
        Assert.Equal(NpcEffect.PointAtSlotNamed("place"), table.Of("villager", "villager.point")); // V02: the place the learner names (it was point_at_noun río)
        Assert.Equal(ItemId.WaterBottle, table.Of("guide", "guide.give_water").Item);
    }

    [Fact]
    public void EveryPhraseIdTheGameLogsIsARowOfThePhraseSheet()
    {
        var sheet = File.ReadAllText(Path.Combine(GameBundle.ResolveDirectory(), "..", "PHRASES.md"));
        foreach (var id in PhraseIds.All)
        {
            Assert.Contains($"| {id} |", sheet, StringComparison.Ordinal);
        }

        Assert.Equal(PhraseIds.RequestWater, PhraseIds.For("request_item", SlotMap("item", "pan"), "río"));
        Assert.Equal(PhraseIds.WhereIsTheRiver, PhraseIds.For("ask_directions", SlotMap("place", "río"), "río"));
        Assert.Null(PhraseIds.For("ask_directions", SlotMap("place", "árbol"), "río"));
        Assert.Null(PhraseIds.For("ask_craft", SlotMap("item", "antorcha"), "río"));
        Assert.Equal("shopkeeper", TestGame.Data.Npcs.HomeOf(PhraseIds.YesPlease));
        Assert.Null(TestGame.Data.Npcs.HomeOf(PhraseIds.Hello));
    }

    private static Vocaterra.Nlp.Core.Verdicts.SlotMap SlotMap(string key, string value) => Vocaterra.Nlp.Core.Verdicts.SlotMap.Empty.With(key, value);

    private static UtterancePipeline Pipeline()
    {
        var asr = new AsrResult("hola", [new AsrHypothesis("hola", 1.0)], [], 1.0, TypedUtteranceEvaluator.ScriptedModelHash, RecognitionMode.Open);
        return new UtterancePipeline(new PipelineServices(new ScriptedWordRecognizer(asr), null, new FakeEmbeddingModel(), new SilentSynthesizer(), TestGame.Bundle));
    }
}
