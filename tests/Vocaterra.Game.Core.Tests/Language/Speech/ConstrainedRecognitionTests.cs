using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Language.Intent;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Speech;

/// <summary>
/// The NPC prompts of the route are recognized in the core's Constrained mode (S7, D-game-9): the Vosk grammar is built by the pipeline from the
/// prompt's answer set (<c>UtterancePipeline.Analyze</c> reads <c>PromptSpec.Mode</c>), so the game only has to evaluate on the prompt. These
/// tests check the bundle's prompts are Constrained where the plan says so and, with the real models, print what each mode makes of the
/// scripted demo lines (the report of the milestone).
/// </summary>
[Collection("speech models")]
public sealed class ConstrainedRecognitionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(NpcService.StallNeedPrompt)]
    [InlineData(NpcService.StallOfferPrompt)]
    [InlineData(NpcService.GuideOfferPrompt)]
    public void TheShopkeepersAndTheGuidesOfferPromptsAreConstrained(string promptId)
    {
        Assert.Equal(RecognitionMode.Constrained, TestGame.Bundle.Prompts[promptId].Mode);
    }

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void TheDemoLinesInBothModes()
    {
        using var engine = SpeechEngine.TryCreate(out _)!;
        var evaluator = new SpokenUtteranceEvaluator(TestGame.Bundle, engine.Recognizer);
        output.WriteLine("prompt | line | mode | heard | mean word conf | asr conf | intent | communicated");
        foreach (var (promptId, line) in new[]
        {
            (NpcService.StallNeedPrompt, "quiero agua, por favor"),
            (NpcService.StallNeedPrompt, "quiero agua"),
            (NpcService.StallNeedPrompt, "agua agua mesa"),
            (NpcService.StallNeedPrompt, "el gato come queso"),
            (NpcService.StallNeedPrompt, "necesito ayuda"),
            (NpcService.StallOfferPrompt, "sí, por favor"),
            (NpcService.StallOfferPrompt, "no, gracias"),
            (NpcService.StallOfferPrompt, "claro"),
            (NpcService.GuideOfferPrompt, "sí, por favor"),
            (NpcService.GuideOfferPrompt, "no, gracias"),
            (NpcService.VillagerPrompt, "¿dónde está el río?"),
            (NpcService.VillagerPrompt, "hola"),
        })
        {
            var clip = engine.Synthesize(line, out _);
            var prompt = TestGame.Bundle.Prompts[promptId];
            foreach (var mode in new[] { prompt.Mode, prompt.Mode == RecognitionMode.Open ? RecognitionMode.Constrained : RecognitionMode.Open })
            {
                var analysis = evaluator.Analyze(clip, prompt with { Mode = mode });
                var words = analysis.Asr.Words.Count == 0 ? "-" : string.Join(' ', analysis.Asr.Words.Select(static w => $"{w.Text}:{w.Confidence:0.00}"));
                var mean = analysis.Asr.Words.Count == 0 ? double.NaN : analysis.Asr.Words.Average(static w => w.Confidence);
                output.WriteLine($"{promptId} | {line} | {mode}{(mode == prompt.Mode ? "*" : string.Empty)} | '{analysis.Asr.Transcript}' [{words}] | {mean:0.00} | {analysis.Asr.Confidence:0.00} | {analysis.Verdict.Intent.Value} | {analysis.Verdict.Communicated}");
            }
        }
    }

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void ConstrainedRecognitionOfTheStallsAnswersGivesTheIntentsAndSureWords()
    {
        using var engine = SpeechEngine.TryCreate(out _)!;
        var evaluator = new SpokenUtteranceEvaluator(TestGame.Bundle, engine.Recognizer);
        foreach (var (promptId, line, intent) in new[]
        {
            (NpcService.StallNeedPrompt, "quiero agua, por favor", "request_item"),
            (NpcService.StallNeedPrompt, "quiero agua", "request_item"),
            (NpcService.StallOfferPrompt, "no, gracias", "deny"),
            (NpcService.GuideOfferPrompt, "no, gracias", "deny"),
            (NpcService.StallOfferPrompt, "sí, por favor", "confirm"),
        })
        {
            var analysis = evaluator.Analyze(engine.Synthesize(line, out _), TestGame.Bundle.Prompts[promptId]);

            Assert.Equal(RecognitionMode.Constrained, analysis.Asr.Mode);
            Assert.Equal(intent, analysis.Verdict.Intent.Value);
            Assert.True(analysis.Verdict.Communicated, $"{promptId}: '{line}' heard as '{analysis.Asr.Transcript}'");
            Assert.False(UncertaintyRule.IsUncertain(true, UncertaintyRule.MeanWordConfidence(analysis.Asr)), $"{promptId}: '{line}' confidence {UncertaintyRule.MeanWordConfidence(analysis.Asr):0.00}");
        }
    }

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void AGarbledSpokenLineAtTheStallIsAnUncertainRecognitionNeverALanguageFailureAndThreeOfThemOfferChoices()
    {
        using var session = TestGame.NewSession();
        Assert.True(session.StartWorker().WaitUntilReady((int)HangGuard.Scale(60000)), HangGuard.Hung("the worker was not ready", 60000));
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");

        var turns = new List<GuideTurn>();
        for (var i = 0; i < 3; i++)
        {
            _ = session.SubmitSpeak("el gato duerme"); // nothing the stall teaches (V20: its grammar has «Ayer comí fruta.», and «come queso» was heard as «comí»)
            var turn = Drain(session).Turn!;
            turns.Add(turn);
            output.WriteLine($"heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Summary}, repair {turn.RepairKind}, choices {(turn.Choices is null ? "-" : string.Join(" | ", turn.Choices))}");
            Assert.True(turn.Uncertain);
            Assert.False(turn.Communicated);
            Assert.Empty(turn.Errors);
        }

        var events = session.Events;
        Assert.Equal(3, events.OfType<VoiceEvent>().Count(static e => e.Kind == VoiceEventKind.Uncertain));
        Assert.DoesNotContain(events.OfType<PhraseUse>(), static u => u.Kind != PhraseUseKind.FailForward); // never a phrase use of any kind
        Assert.Equal([RepairKind.AskAgain, RepairKind.SlowRepeat, RepairKind.OfferChoices], turns.Select(static t => t.RepairKind));
        Assert.Equal(3, turns[2].Choices!.Count);
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Bottle));
    }

    private static AppliedTurn Drain(VoxelSession session)
    {
        var deadline = HangGuard.DeadlineAfter(120000);
        while (!HangGuard.Passed(deadline))
        {
            var applied = session.Collect();
            if (applied.Count > 0)
            {
                return applied[0];
            }

            Thread.Sleep(5);
        }

        throw new TimeoutException(HangGuard.Hung("The turn did not come back", 120_000));
    }
}
