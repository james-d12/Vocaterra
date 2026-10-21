using Vocaterra.Game;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.Game.Tests;

/// <summary>
/// The prototype's NPCs in <c>content/game-v1</c> (S5): the shopkeeper at the stall, the villager, the guide's transfer offer, and their
/// prompts. Each exchange runs through the core, with a host turn (<c>Advance</c>) where the agent opens the conversation.
/// </summary>
public sealed class NpcAgentTests
{
    private static readonly StoredTimestamp Start = new(GameSession.DefaultStartedAt);

    [Fact]
    public void TheBundleHoldsTheGuideTheShopkeeperAndTheVillagerOnly()
    {
        Assert.Equal(["guide", "shopkeeper", "villager"], TestGame.Bundle.Agents.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            ["guide-offer", "guide-open", "guide-review", "stall-need", "stall-offer", "villager-open"],
            TestGame.Bundle.Prompts.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ThePromptsHaveTheModesIntentsAndLinesOfThePhraseSheet()
    {
        var prompts = TestGame.Bundle.Prompts;

        AssertPrompt(prompts["stall-need"], RecognitionMode.Constrained, "shopkeeper", "¿Qué necesitas?", "request_item");
        AssertPrompt(prompts["stall-offer"], RecognitionMode.Constrained, "shopkeeper", "¿Quieres pan?", "confirm", "deny");
        AssertPrompt(prompts["villager-open"], RecognitionMode.Open, "villager", "Hola.", "ask_directions", "request_item", "greet", "thank", "farewell");
        AssertPrompt(prompts["guide-offer"], RecognitionMode.Constrained, "guide", "¿Quieres agua?", "confirm", "deny");
    }

    [Fact]
    public void HelpAndIDontKnowAreAllowedOnEveryPrompt()
    {
        foreach (var prompt in TestGame.Bundle.Prompts.Values)
        {
            Assert.Contains("ask_help", prompt.AllowedIntentIds);
            Assert.Contains("dont_know", prompt.AllowedIntentIds);
        }
    }

    [Fact]
    public void TheShopkeeperOpensWithAHostTurnThenGivesWaterOffersBreadAndGivesBread()
    {
        var pipeline = Pipeline();
        var context = Context("shopkeeper", "idle");

        var opening = pipeline.Advance("shopkeeper.ask", context);
        Assert.NotNull(opening);
        Assert.Equal(["¿Qué necesitas?"], opening.Response.Lines.Select(static l => l.Text));
        Assert.Equal("asking", opening.Agent.Next.StateId);
        context = context.Next(opening, Start);

        var asked = Say("quiero agua, por favor", "stall-need", ref context);
        Assert.Equal("request_item", asked.Verdict.Intent.Value);
        Assert.Equal("offering", context.AgentState.StateId);
        Assert.Equal(["stall.give_water"], HostEvents(asked));
        Assert.Equal("¿Quieres pan?", asked.Response.Lines[^1].Text);

        var accepted = Say("sí, por favor", "stall-offer", ref context);
        Assert.Equal("confirm", accepted.Verdict.Intent.Value);
        Assert.Equal("done", context.AgentState.StateId);
        Assert.Equal(["stall.give_bread"], HostEvents(accepted));
    }

    [Fact]
    public void ThePlayerCanDeclineTheBreadAndNothingIsGiven()
    {
        var pipeline = Pipeline();
        var context = Context("shopkeeper", "offering");

        var declined = Say("no, gracias", "stall-offer", ref context);

        Assert.Equal("deny", declined.Verdict.Intent.Value);
        Assert.Equal("done", context.AgentState.StateId);
        Assert.Empty(HostEvents(declined));
    }

    [Theory]
    [InlineData("ayuda", "asking", "Puedes decir: «Quiero agua, por favor».")]
    [InlineData("no sé", "asking", "Puedes decir: «Quiero agua, por favor».")]
    public void HelpAtTheStallModelsTheAnswerAndKeepsTheState(string line, string state, string model)
    {
        var pipeline = Pipeline();
        var context = Context("shopkeeper", state);

        var result = Say(line, "stall-need", ref context);

        Assert.True(result.Verdict.Communicated);
        Assert.Equal(state, context.AgentState.StateId);
        Assert.Equal(model, result.Response.Lines[^1].Text);
        Assert.Empty(HostEvents(result));
    }

    [Fact]
    public void ALineTheVillagerDoesNotUnderstandKeepsTheStateAndOffersHelp()
    {
        var context = Context("villager", "idle");

        var result = Say("está el río", "villager-open", ref context);

        Assert.False(result.Verdict.Communicated);
        Assert.Equal("idle", context.AgentState.StateId);
        Assert.Equal("not_understood.1", result.Response.Lines[0].LineId);
        Assert.Equal("No te entendí. ¿Lo dices otra vez?", result.Response.Lines[0].Text);
        Assert.Contains(result.Response.Lines, static l => l.LineId.StartsWith("feedback.hint.not_communicated", StringComparison.Ordinal));
    }

    [Fact]
    public void TheVillagerGreetsFirstPointsAtTheRiverAndGivesWhatIsAsked()
    {
        var pipeline = Pipeline();
        var context = Context("villager", "idle");

        var hello = pipeline.Advance("villager.greet", context);
        Assert.NotNull(hello);
        Assert.Equal(["Hola."], hello.Response.Lines.Select(static l => l.Text));
        context = context.Next(hello, Start);

        var asked = Say("¿dónde está el río?", "villager-open", ref context);
        Assert.Equal("ask_directions", asked.Verdict.Intent.Value);
        Assert.Equal("pointing", context.AgentState.StateId);
        Assert.Equal(["villager.point"], HostEvents(asked));
        Assert.Equal("place", Assert.Single(asked.Verdict.Slots.Keys));
        Assert.Equal("río", asked.Verdict.Slots["place"]);

        var bread = Say("quiero pan, por favor", "villager-open", ref context);
        Assert.Equal("request_item", bread.Verdict.Intent.Value);
        Assert.Equal("pan", bread.Verdict.Slots["item"]);
        Assert.Equal(["villager.give"], HostEvents(bread));

        var bye = Say("adiós", "villager-open", ref context);
        Assert.Equal("idle", context.AgentState.StateId);
        Assert.Equal("farewell", bye.Verdict.Intent.Value);
    }

    [Fact]
    public void TheGuideOffersWaterWithAHostTurnAndAnswersYesAndNo()
    {
        var pipeline = Pipeline();
        var context = Context("guide", "talking");

        var offer = pipeline.Advance("guide.offer", context);
        Assert.NotNull(offer);
        Assert.Equal(["¿Quieres agua?"], offer.Response.Lines.Select(static l => l.Text));
        Assert.Equal("offering", offer.Agent.Next.StateId);

        var yesContext = context.Next(offer, Start);
        var yes = Say("sí, por favor", "guide-offer", ref yesContext);
        Assert.Equal("confirm", yes.Verdict.Intent.Value);
        Assert.Equal("talking", yesContext.AgentState.StateId);
        Assert.Equal(["guide.give_water"], HostEvents(yes));

        var noContext = context.Next(offer, Start);
        var no = Say("no, gracias", "guide-offer", ref noContext);
        Assert.Equal("deny", no.Verdict.Intent.Value);
        Assert.Equal("talking", noContext.AgentState.StateId);
        Assert.Empty(HostEvents(no));
    }

    [Fact]
    public void TheOfferIsAvailableFromEveryStateOfTheGuide()
    {
        var pipeline = Pipeline();
        foreach (var state in new[] { "idle", "talking", "pointing" })
        {
            var turn = pipeline.Advance("guide.offer", Context("guide", state));
            Assert.NotNull(turn);
            Assert.Equal("offering", turn.Agent.Next.StateId);
        }
    }

    [Fact]
    public void EveryNpcLineOfThePrototypeIsFreeOfSlotsSoItCanBePreRendered()
    {
        var bundle = TestGame.Bundle;
        foreach (var agentId in new[] { "shopkeeper", "villager" })
        {
            foreach (var poolId in bundle.Agents[agentId].Definition.VariantPools!.Select(static p => p.Id))
            {
                Assert.True(bundle.Dialogue.TryGetPool(poolId, out var pool), $"{agentId}: no pool {poolId}.");
                foreach (var variant in pool.Variants)
                {
                    Assert.DoesNotContain('{', variant.Text);
                }
            }
        }

        foreach (var poolId in new[] { "guide_offer", "guide_offer_yes", "guide_offer_no", "guide_offer_help" })
        {
            Assert.True(bundle.Dialogue.TryGetPool(poolId, out var pool));
            Assert.All(pool.Variants, static v => Assert.DoesNotContain('{', v.Text));
        }
    }

    [Fact]
    public void TheNotUnderstoodLineAsksForAnotherTryAndTheHelpLineIsAHint()
    {
        var bundle = TestGame.Bundle;

        Assert.Equal("No te entendí. ¿Lo dices otra vez?", bundle.Dialogue.Pools["not_understood"].Variants[0].Text);
        Assert.Contains("feedback.hint.not_communicated", bundle.Dialogue.Pools.Keys);
        Assert.All(bundle.Dialogue.Pools["feedback.hint.not_communicated"].Variants, static v => Assert.Contains("ayuda", v.Text, StringComparison.Ordinal));
        var path = Path.Combine(GameBundle.ResolveDirectory(), "feedback", "feedback-policy.json");
        Assert.Contains("\"notCommunicatedType\": \"Hint\"", File.ReadAllText(path), StringComparison.Ordinal);
    }

    private static void AssertPrompt(PromptSpec prompt, RecognitionMode mode, string agent, string text, params string[] intents)
    {
        Assert.Equal(mode, prompt.Mode);
        Assert.Equal(agent, prompt.AgentId);
        Assert.Equal(text, prompt.Text);
        foreach (var intent in intents)
        {
            Assert.Contains(intent, prompt.AllowedIntentIds);
        }
    }

    private static UtterancePipeline Pipeline()
    {
        var asr = new AsrResult("hola", [new AsrHypothesis("hola", 1.0)], [], 1.0, TypedUtteranceEvaluator.ScriptedModelHash, RecognitionMode.Open);
        return new UtterancePipeline(new PipelineServices(new ScriptedWordRecognizer(asr), null, new FakeEmbeddingModel(), new SilentSynthesizer(), TestGame.Bundle));
    }

    private static ConversationContext Context(string agentId, string state)
    {
        return ConversationContext.Create(TestGame.Seed, agentId, new AgentState(agentId, state, 0), Start, LearnerModel.New());
    }

    private static PipelineResult Say(string line, string promptId, ref ConversationContext context)
    {
        var result = new TypedUtteranceEvaluator(TestGame.Bundle).Evaluate(line, TestGame.Bundle.Prompts[promptId], context);
        context = context.Next(result, Start);
        return result;
    }

    private static string[] HostEvents(PipelineResult result)
    {
        return result.Decision.Agent!.Actions.Where(static a => a.HostEvent is not null).Select(static a => a.HostEvent!).ToArray();
    }
}
