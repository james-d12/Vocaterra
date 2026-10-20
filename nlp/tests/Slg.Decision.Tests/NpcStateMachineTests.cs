using Slg.Core.Determinism;
using Slg.Core.Taxonomy;
using Slg.Decision.Npc;

namespace Slg.Decision.Tests;

/// <summary>Tests for the NPC state machine.</summary>
public class NpcStateMachineTests
{
    private const string InnkeeperJson = """
        {
          "npcId": "innkeeper",
          "initialState": "greeting",
          "variantPools": [ { "id": "greet", "count": 4 }, { "id": "nod", "count": 3 } ],
          "states": [
            {
              "id": "greeting",
              "transitions": [
                {
                  "on": { "intent": "greet" },
                  "to": "asking",
                  "actions": [ { "dialogueIntent": "welcome", "gameEvent": "npc.wave", "variantPoolId": "greet" } ]
                },
                {
                  "on": { "intent": "buy_item", "anyErrorTag": [ "gender.agree", "noun.number" ] },
                  "to": "correcting",
                  "actions": [ { "dialogueIntent": "recast_agreement", "variantPoolId": "nod" } ]
                },
                {
                  "on": { "intent": "buy_item", "communicated": true, "noErrorTag": [ "vocab.*" ] },
                  "to": "selling",
                  "actions": [
                    { "dialogueIntent": "show_wares", "gameEvent": "shop.open" },
                    { "dialogueIntent": "smile", "variantPoolId": "nod" }
                  ]
                },
                {
                  "on": { "anyErrorTag": [ "pron.*" ] },
                  "to": "greeting",
                  "actions": [ { "dialogueIntent": "model_word" } ]
                },
                {
                  "on": { "communicated": false },
                  "to": "greeting",
                  "actions": [ { "dialogueIntent": "not_understood" } ]
                }
              ],
              "fallback": { "to": "asking", "actions": [ { "dialogueIntent": "puzzled", "variantPoolId": "greet" } ] }
            },
            {
              "id": "asking",
              "transitions": [],
              "fallback": { "actions": [ { "dialogueIntent": "wait", "variantPoolId": "greet" } ] }
            },
            {
              "id": "correcting",
              "transitions": [ { "on": {}, "to": "greeting", "actions": [ { "dialogueIntent": "move_on" } ] } ]
            },
            {
              "id": "selling",
              "transitions": []
            }
          ]
        }
        """;

    private static readonly NpcStateMachine Innkeeper = NpcStateMachine.FromJson(InnkeeperJson);

    [Fact]
    public void InitialStateStartsAtCounterZero()
    {
        Assert.Equal(new NpcState("innkeeper", "greeting", 0), Innkeeper.InitialState());
    }

    [Fact]
    public void TransitionOnIntent()
    {
        var result = Innkeeper.Step(Innkeeper.InitialState(), TestVerdicts.Clean("greet"), new SeededRng(1));

        Assert.Equal(new NpcState("innkeeper", "asking", 1), result.Next);
        var action = Assert.Single(result.Actions);
        Assert.Equal("welcome", action.DialogueIntent);
        Assert.Equal("npc.wave", action.GameEvent);
        Assert.InRange(action.VariantIndex!.Value, 0, 3);
    }

    [Fact]
    public void TransitionOnErrorTag()
    {
        var verdict = TestVerdicts.WithErrors(TestVerdicts.Error(ErrorTags.GenderAgree));

        var result = Innkeeper.Step(Innkeeper.InitialState(), verdict, new SeededRng(1));

        Assert.Equal("correcting", result.Next.StateId);
        var action = Assert.Single(result.Actions);
        Assert.Equal("recast_agreement", action.DialogueIntent);
        Assert.Null(action.GameEvent);
        Assert.InRange(action.VariantIndex!.Value, 0, 2);
    }

    [Fact]
    public void ErrorFreeVerdictTakesLaterTransitionAndActionsWithoutPoolHaveNoVariant()
    {
        var result = Innkeeper.Step(Innkeeper.InitialState(), TestVerdicts.Clean("buy_item"), new SeededRng(1));

        Assert.Equal("selling", result.Next.StateId);
        Assert.Equal(2, result.Actions.Count);
        Assert.Equal(new NpcAction("show_wares", "shop.open", null), result.Actions[0]);
        Assert.Equal("smile", result.Actions[1].DialogueIntent);
        Assert.NotNull(result.Actions[1].VariantIndex);
    }

    [Fact]
    public void NoErrorTagPatternBlocksTransition()
    {
        var verdict = TestVerdicts.WithErrors(TestVerdicts.Error(ErrorTags.VocabWrong));

        var result = Innkeeper.Step(Innkeeper.InitialState(), verdict, new SeededRng(1));

        // buy_item with a vocab error skips "selling" (noErrorTag vocab.*) and no other transition matches: fallback.
        Assert.Equal("asking", result.Next.StateId);
        Assert.Equal("puzzled", Assert.Single(result.Actions).DialogueIntent);
    }

    [Fact]
    public void FamilyWildcardMatchesErrorTags()
    {
        var verdict = TestVerdicts.Make("unknown", true, 0.6, TestVerdicts.Error(ErrorTags.PronR));

        var result = Innkeeper.Step(Innkeeper.InitialState(), verdict, new SeededRng(1));

        Assert.Equal("greeting", result.Next.StateId);
        Assert.Equal(new NpcAction("model_word", null, null), Assert.Single(result.Actions));
    }

    [Fact]
    public void CommunicatedPredicateMatchesFalse()
    {
        var result = Innkeeper.Step(Innkeeper.InitialState(), TestVerdicts.Make("unknown", false, 0.0), new SeededRng(1));

        Assert.Equal(new NpcAction("not_understood", null, null), Assert.Single(result.Actions));
        Assert.Equal("greeting", result.Next.StateId);
    }

    [Fact]
    public void FirstMatchingTransitionWins()
    {
        // greet + a gender error matches transition 1 (intent greet) and nothing earlier; buy_item + gender error matches 2 and not 3.
        var greetWithError = TestVerdicts.Make("greet", true, 0.8, TestVerdicts.Error(ErrorTags.GenderAgree));
        var result = Innkeeper.Step(Innkeeper.InitialState(), greetWithError, new SeededRng(1));
        Assert.Equal("asking", result.Next.StateId);

        // The "correcting" state has an empty condition that matches anything.
        var inCorrecting = new NpcState("innkeeper", "correcting", 5);
        var second = Innkeeper.Step(inCorrecting, TestVerdicts.Make("unknown", false, 0.0), new SeededRng(1));
        Assert.Equal(new NpcState("innkeeper", "greeting", 6), second.Next);
    }

    [Fact]
    public void FallbackWithoutTargetStaysInState()
    {
        var current = new NpcState("innkeeper", "asking", 9);

        var result = Innkeeper.Step(current, TestVerdicts.Clean("anything"), new SeededRng(1));

        Assert.Equal(new NpcState("innkeeper", "asking", 10), result.Next);
        Assert.Equal("wait", Assert.Single(result.Actions).DialogueIntent);
    }

    [Fact]
    public void StateWithoutFallbackStaysSilentAndKeepsState()
    {
        var current = new NpcState("innkeeper", "selling", 2);

        var result = Innkeeper.Step(current, TestVerdicts.Clean("anything"), new SeededRng(1));

        Assert.Equal(new NpcState("innkeeper", "selling", 3), result.Next);
        Assert.Empty(result.Actions);
    }

    [Fact]
    public void SameSeedGivesSameResult()
    {
        var verdict = TestVerdicts.Clean("greet");

        var a = Innkeeper.Step(Innkeeper.InitialState(), verdict, SeededRng.ForInteraction(1234, "innkeeper", 0));
        var b = Innkeeper.Step(Innkeeper.InitialState(), verdict, SeededRng.ForInteraction(1234, "innkeeper", 0));

        Assert.Equal(a, b);
    }

    [Fact]
    public void WorldSeedOverloadUsesTheInteractionGenerator()
    {
        var current = new NpcState("innkeeper", "asking", 7);
        var verdict = TestVerdicts.Clean("anything");

        var viaSeed = Innkeeper.Step(current, verdict, 99UL);
        var viaRng = Innkeeper.Step(current, verdict, SeededRng.ForInteraction(99UL, "innkeeper", 7));

        Assert.Equal(viaRng, viaSeed);
    }

    [Fact]
    public void VariantIndexIsTheFirstDrawOfTheInteractionGenerator()
    {
        var current = new NpcState("innkeeper", "asking", 3);

        var result = Innkeeper.Step(current, TestVerdicts.Clean("x"), 42UL);

        Assert.Equal(SeededRng.ForInteraction(42UL, "innkeeper", 3).NextInt(4), result.Actions[0].VariantIndex);
    }

    [Fact]
    public void MultipleActionsDrawInOrderFromTheSameGenerator()
    {
        var state = new NpcState("innkeeper", "greeting", 0);

        var result = Innkeeper.Step(state, TestVerdicts.Clean("buy_item"), new SeededRng(5));

        var expected = new SeededRng(5);
        Assert.Null(result.Actions[0].VariantIndex);
        Assert.Equal(expected.NextInt(3), result.Actions[1].VariantIndex);
    }

    [Fact]
    public void FiveStepSequenceMatchesGolden()
    {
        // Self-recorded golden: world seed 20261018, repeated fallback in "asking" draws from the 4-variant "greet" pool.
        var state = new NpcState("innkeeper", "asking", 0);
        var verdict = TestVerdicts.Clean("anything");
        var drawn = new List<int>();
        var counters = new List<long>();

        for (var i = 0; i < 5; i++)
        {
            var result = Innkeeper.Step(state, verdict, 20261018UL);
            drawn.Add(result.Actions[0].VariantIndex!.Value);
            counters.Add(result.Next.InteractionCounter);
            state = result.Next;
        }

        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, counters);
        Assert.Equal(new[] { 0, 1, 0, 2, 1 }, drawn);
    }

    [Fact]
    public void DifferentCountersCanProduceDifferentVariants()
    {
        var distinct = new HashSet<int>();
        for (long counter = 0; counter < 32; counter++)
        {
            var result = Innkeeper.Step(new NpcState("innkeeper", "asking", counter), TestVerdicts.Clean("x"), 7UL);
            distinct.Add(result.Actions[0].VariantIndex!.Value);
        }

        Assert.True(distinct.Count > 1);
        Assert.All(distinct, index => Assert.InRange(index, 0, 3));
    }

    [Fact]
    public void StepRejectsAnotherNpcsStateAndUnknownStates()
    {
        Assert.Throws<ArgumentException>(() => Innkeeper.Step(new NpcState("blacksmith", "greeting", 0), TestVerdicts.Clean(), new SeededRng(1)));
        Assert.Throws<ArgumentException>(() => Innkeeper.Step(new NpcState("innkeeper", "nowhere", 0), TestVerdicts.Clean(), new SeededRng(1)));
    }

    [Fact]
    public void DefinitionRoundTripsThroughCanonicalJson()
    {
        var json = CanonicalJson.Serialize(Innkeeper.Definition);

        var reloaded = NpcStateMachine.FromJson(json);

        Assert.Equal(json, CanonicalJson.Serialize(reloaded.Definition));
        Assert.Equal(
            Innkeeper.Step(Innkeeper.InitialState(), TestVerdicts.Clean("greet"), new SeededRng(3)),
            reloaded.Step(reloaded.InitialState(), TestVerdicts.Clean("greet"), new SeededRng(3)));
    }

    [Theory]
    [InlineData("""{ "npcId": "n", "initialState": "missing", "states": [ { "id": "a", "transitions": [] } ] }""")]
    [InlineData("""{ "npcId": "n", "initialState": "a", "states": [ { "id": "a", "transitions": [] }, { "id": "a", "transitions": [] } ] }""")]
    [InlineData("""{ "npcId": "n", "initialState": "a", "states": [ { "id": "a", "transitions": [ { "on": {}, "to": "b", "actions": [] } ] } ] }""")]
    [InlineData("""{ "npcId": "n", "initialState": "a", "states": [ { "id": "a", "transitions": [ { "on": {}, "to": "a", "actions": [ { "dialogueIntent": "x", "variantPoolId": "p" } ] } ] } ] }""")]
    [InlineData("""{ "npcId": "n", "initialState": "a", "variantPools": [ { "id": "p", "count": 0 } ], "states": [ { "id": "a", "transitions": [] } ] }""")]
    [InlineData("""{ "npcId": "n", "initialState": "a", "states": [ { "id": "a", "transitions": [ { "on": { "anyErrorTag": [] }, "to": "a", "actions": [] } ] } ] }""")]
    [InlineData("""{ "npcId": "n", "initialState": "a", "states": [ { "id": "a", "transitions": [ { "on": { "noErrorTag": [ "a*b" ] }, "to": "a", "actions": [] } ] } ] }""")]
    [InlineData("""{ "npcId": "n", "initialState": "a", "states": [] }""")]
    [InlineData("""{ "npcId": "n", "initialState": "a", "states": [ { "id": "a" } ] }""")]
    [InlineData("""{ "npcId": "n", "initialState": "a", "states": [ { "id": "a", "transitions": [], "fallback": { "to": "zzz", "actions": [] } } ] }""")]
    public void InvalidDefinitionsAreRejected(string json)
    {
        Assert.Throws<InvalidDataException>(() => NpcStateMachine.FromJson(json));
    }

    [Fact]
    public void MinimalDefinitionIsAccepted()
    {
        var machine = NpcStateMachine.FromJson("""{ "npcId": "n", "initialState": "a", "states": [ { "id": "a", "transitions": [] } ] }""");

        var result = machine.Step(machine.InitialState(), TestVerdicts.Clean(), new SeededRng(1));

        Assert.Equal(new NpcState("n", "a", 1), result.Next);
        Assert.Empty(result.Actions);
    }
}
