using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Feedback;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// The prototype's NPCs in <c>content/game-v1</c> (S5): the shopkeeper at the stall, the villager, the guide's transfer offer, and their
/// prompts. Each exchange runs through the core, with a host turn (<c>Advance</c>) where the agent opens the conversation.
/// </summary>
public sealed class NpcAgentTests
{
    private static readonly StoredTimestamp Start = new(GuideSession.DefaultStartedAt);

    [Fact]
    public void TheBundleHoldsTheGuideTheShopkeeperTheVillagerAndTheFisher()
    {
        // VILLAGE_PLAN V10 added the fisher, his prompt (fisher-open) and El pueblo's (lesson-far to lesson-went-there, and the villager's lesson-sign); V11 the baker, her
        // prompt (baker-open) and Números y pan's (lesson-wood-plan, lesson-coins, lesson-bought).
        Assert.Equal(["baker", "fisher", "guide", "shopkeeper", "villager"], TestGame.Bundle.Agents.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            ["baker-open", "fisher-open", "guide-offer", "guide-open", "guide-review", "lesson-ate", "lesson-bottle", "lesson-bought", "lesson-coins", "lesson-far", "lesson-going", "lesson-had", "lesson-have", "lesson-hay", "lesson-hello", "lesson-name", "lesson-need", "lesson-offer", "lesson-place", "lesson-plan", "lesson-plan-there", "lesson-plan-we", "lesson-she", "lesson-sign", "lesson-task", "lesson-thanks", "lesson-tomorrow", "lesson-unstuck", "lesson-usted", "lesson-ustedes", "lesson-want", "lesson-we", "lesson-went", "lesson-went-there", "lesson-what", "lesson-wood-plan", "lesson-yesno", "stall-need", "stall-offer", "villager-open"],
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
        Assert.Equal("No te entendí. ¿Me lo dices otra vez?", result.Response.Lines[0].Text);
        // The core plans the not-communicated feedback (SlowRepeat) only when the verdict has an expected form; "está el río" has none, so no
        // feedback line is said and in particular no hint.
        Assert.DoesNotContain(result.Response.Lines, static l => l.LineId.StartsWith("feedback.hint", StringComparison.Ordinal));
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
    public void TheNotUnderstoodLineAsksForAnotherTryAndTheFeedbackIsTheHelpLineNotAHint()
    {
        // D-game-9 (S9): a turn the NPC did not understand gets the help line, never a hint ("Casi. Piensa otra vez."). The core picks the pool
        // of the policy's not-communicated type by the first error's tag, falling back to the kind's generic pool, so that type is SlowRepeat (no
        // rule produces it) and its generic pool holds the help lines only.
        var bundle = TestGame.Bundle;

        Assert.Equal("No te entendí. ¿Me lo dices otra vez?", bundle.Dialogue.Pools["not_understood"].Variants[0].Text);
        Assert.DoesNotContain("feedback.hint.not_communicated", bundle.Dialogue.Pools.Keys);
        Assert.All(bundle.Dialogue.Pools["feedback.slow_repeat"].Variants, static v => Assert.Contains("ayuda", v.Text, StringComparison.Ordinal));
        Assert.Equal(["Di «ayuda» y te ayudo.", "Si necesitas ayuda, di «ayuda»."], bundle.Dialogue.Pools["feedback.slow_repeat"].Variants.Select(static v => v.Text));
        Assert.Equal(FeedbackType.SlowRepeat, bundle.FeedbackPolicy.NotCommunicatedType);
        Assert.Equal(FeedbackType.Hint, bundle.FeedbackPolicy.Rules.Single(static r => r.TagPattern == "*" && r.MinRepeat == 2).Type);
    }

    [Fact]
    public void AGarbledLineTheShopkeeperDoesNotUnderstandGetsNoHintWhenItHasErrors()
    {
        // The S7 screenshot: a garbled spoken line at the stall answered "No te entendí..." and then "Casi. Piensa otra vez." Here every line that
        // is not communicated at the stall gets the not-understood line and the help line, whatever errors the analysis found in it.
        var lines = new[] { "yo quieres agua por favor", "quiero dos aguas", "agua agua agua", "el el el", "nada de nada", "xyz", "tienes pan" };
        var notCommunicated = 0;
        var withErrors = 0;
        foreach (var line in lines)
        {
            var context = Context("shopkeeper", "idle");
            var result = Say(line, "stall-need", ref context);
            if (result.Verdict.Communicated)
            {
                continue;
            }

            notCommunicated++;
            if (result.Verdict.Errors.Count > 0)
            {
                withErrors++;
            }

            var feedback = result.Response.Lines.Where(static l => l.LineId.StartsWith("feedback.", StringComparison.Ordinal)).ToList();
            Assert.All(feedback, static f => Assert.StartsWith("feedback.slow_repeat.", f.LineId, StringComparison.Ordinal));
            Assert.All(feedback, static f => Assert.Contains("ayuda", f.Text, StringComparison.Ordinal));
            Assert.DoesNotContain(result.Response.Lines, static l => l.Text.Contains("Casi", StringComparison.Ordinal) || l.Text.Contains("Piensa", StringComparison.Ordinal));
        }

        Assert.True(notCommunicated > 0, "no line was left uncommunicated");
        Assert.True(withErrors > 0, "no uncommunicated line carried an error, so the error path was not exercised");
    }

    [Fact]
    public void ARecastOfAQuestionKeepsItsWordsInQuotesAndShowsTheCorrectForm()
    {
        var context = Context("villager", "idle");
        var gender = Say("dónde está la río", "villager-open", ref context);
        Assert.Equal("¡Eso es! «Dónde está el río».", gender.Response.Lines.Single(static l => l.IsRecast).Text);

        context = Context("villager", "idle");
        var person = Say("yo quieres agua por favor", "villager-open", ref context);
        var recast = person.Response.Lines.Single(static l => l.IsRecast).Text;
        Assert.Contains("quiero", recast, StringComparison.Ordinal);
        Assert.DoesNotContain("quieres", recast, StringComparison.Ordinal);
    }

    [Fact]
    public void TheConjugationRecastPoolShowsTheFirstPersonWhenTheVerbSlotsAreGiven()
    {
        var bundle = TestGame.Bundle;
        var slots = Vocaterra.Nlp.Core.Verdicts.SlotMap.From(new Dictionary<string, string> { ["verb"] = "querer", ["qty"] = "2", ["item"] = "agua" });
        var pool = bundle.Dialogue.Pools["feedback.recast.conj.person"];

        Assert.Equal("Sí: «Quiero dos aguas». ¡Muy bien!", bundle.TemplateEngine.Render(pool.Variants[0], slots));
        Assert.Equal("Mira, ¿quieres dos aguas? Se dice «quiero dos aguas».", bundle.TemplateEngine.Render(pool.Variants[1], slots));
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
