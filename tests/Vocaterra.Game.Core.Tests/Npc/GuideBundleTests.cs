using System.Text.Json;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>The guide's part of <c>content/game-v1</c>: its agent, intents, prompt and pools load, are consistent and say what the game relies on.</summary>
public sealed class GuideBundleTests
{
    // M74: ask_wellbeing (¿cómo estás?, taken out of greet) is allowed wherever greet is.
    private static readonly string[] GuideIntents = ["ask_directions", "ask_craft", "ask_find", "ask_what", "state_want", "state_have", "ask_help", "dont_know", "name_object", "greet", "ask_wellbeing", "thank", "farewell", "ask_repeat", "ask_slower"];

    [Fact]
    public void TheGuidePromptAndAgentLoad()
    {
        var bundle = TestGame.Bundle;

        var prompt = bundle.Prompts[NpcService.GuidePrompt];
        Assert.Equal(NpcService.GuideAgent, prompt.AgentId);
        Assert.Equal(GuideIntents.Order(StringComparer.Ordinal), prompt.AllowedIntentIds.Order(StringComparer.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(prompt.Text));

        var machine = bundle.Agents[NpcService.GuideAgent];
        Assert.Equal("idle", machine.InitialState().StateId);
        Assert.Equal(NpcService.GuideAgent, machine.InitialState().AgentId);
    }

    [Fact]
    public void EveryGuideIntentHasGrammarsAndFiveExamples()
    {
        var intents = TestGame.Bundle.Intents;
        foreach (var id in new[] { "ask_directions", "ask_craft", "ask_find", "ask_what", "ask_help", "name_object" })
        {
            var intent = intents.Intents.Single(i => i.Id.Value == id);
            Assert.True(intent.Grammars.Count >= 2, $"{id} needs at least two slot grammars.");
            Assert.True(intent.Examples.Count >= 5, $"{id} needs at least five examples.");
        }

        Assert.Equal(["place"], intents.Intents.Single(i => i.Id.Value == "ask_directions").RequiredSlots);
        Assert.Equal(["object"], intents.Intents.Single(i => i.Id.Value == "name_object").RequiredSlots);
        Assert.Equal(["item"], intents.Intents.Single(i => i.Id.Value == "ask_craft").RequiredSlots);
        Assert.Equal(["material"], intents.Intents.Single(i => i.Id.Value == "ask_find").RequiredSlots);
    }

    [Fact]
    public void EveryExampleOfTheGuidesIntentsResolvesToItsIntentAndIsUnderstood()
    {
        var bundle = TestGame.Bundle;
        var evaluator = new TypedUtteranceEvaluator(bundle);
        var prompt = bundle.Prompts[NpcService.GuidePrompt];
        var context = ConversationContext.Create(
            TestGame.Seed, NpcService.GuideAgent, bundle.Agents[NpcService.GuideAgent].InitialState(), new StoredTimestamp(GuideSession.DefaultStartedAt), LearnerModel.New());
        foreach (var id in new[] { "ask_directions", "ask_craft", "ask_find", "ask_what", "ask_help", "name_object" })
        {
            foreach (var example in bundle.Intents.Intents.Single(i => i.Id.Value == id).Examples)
            {
                var result = evaluator.Evaluate(example, prompt, context);
                Assert.True(result.Verdict.Intent.Value == id, $"'{example}' resolved to {result.Verdict.Intent.Value}, not {id}.");
                Assert.True(result.Verdict.Communicated, $"'{example}' was not understood.");
            }
        }
    }

    [Fact]
    public void EveryPoolTheGameDrawsFromExistsAndRendersWithTheSlotsTheGameGives()
    {
        var bundle = TestGame.Bundle;
        var slots = new Dictionary<string, SlotMap>
        {
            ["guide.point.found"] = SlotMap.From(
                new Dictionary<string, string> { ["place"] = "árbol", ["direction"] = "a la derecha", ["count"] = "12", ["unit"] = "bloque" }),
            ["guide.point.here"] = SlotMap.From(new Dictionary<string, string> { ["place"] = "piedra" }),
            ["guide.point.none"] = SlotMap.From(new Dictionary<string, string> { ["place"] = "cueva" }),
            ["guide.identify.found"] = SlotMap.From(new Dictionary<string, string> { ["target"] = "árbol" }),
            ["guide.identify.none"] = SlotMap.Empty,
            ["guide.help.ask"] = SlotMap.Empty,
            ["guide.help.walk"] = SlotMap.Empty,
        };
        foreach (var (poolId, map) in slots)
        {
            Assert.True(bundle.Dialogue.TryGetPool(poolId, out var pool), $"No pool {poolId}.");
            foreach (var variant in pool.Variants)
            {
                var text = bundle.TemplateEngine.Render(variant, map);
                Assert.False(string.IsNullOrWhiteSpace(text), $"{variant.LineId} rendered empty.");
            }
        }

        Assert.Equal("El árbol está a la derecha, a doce bloques.", bundle.TemplateEngine.Render(bundle.Dialogue.Pools["guide.point.found"].Variants[0], slots["guide.point.found"]));
        var one = SlotMap.From(new Dictionary<string, string> { ["place"] = "árbol", ["direction"] = "arriba", ["count"] = "1", ["unit"] = "bloque" });
        Assert.Equal("El árbol está arriba, a un bloque.", bundle.TemplateEngine.Render(bundle.Dialogue.Pools["guide.point.found"].Variants[0], one));
    }

    [Fact]
    public void TheGuideStateMachineHasTheThreeStatesAndEveryIntentIsHandledInEach()
    {
        var bundle = TestGame.Bundle;
        var evaluator = new TypedUtteranceEvaluator(bundle);
        var prompt = bundle.Prompts[NpcService.GuidePrompt];
        var lines = new Dictionary<string, string>
        {
            ["greet"] = "hola",
            ["ask_directions"] = "dónde está el árbol",
            ["ask_what"] = "qué es esto",
            ["ask_help"] = "ayuda",
            ["name_object"] = "es un árbol",
            ["thank"] = "gracias",
            ["farewell"] = "adiós",
        };
        var expectedEvents = new Dictionary<string, string?>
        {
            ["greet"] = "agent.greet",
            ["ask_directions"] = "guide.point",
            ["ask_what"] = "guide.identify",
            ["ask_help"] = "guide.help",
            ["name_object"] = "guide.name",
            ["thank"] = null,
            ["farewell"] = "agent.farewell",
        };
        foreach (var state in new[] { "idle", "talking", "pointing" })
        {
            foreach (var (intent, line) in lines)
            {
                var context = ConversationContext.Create(
                    TestGame.Seed, NpcService.GuideAgent, new AgentState(NpcService.GuideAgent, state, 0), new StoredTimestamp(GuideSession.DefaultStartedAt), LearnerModel.New());
                var result = evaluator.Evaluate(line, prompt, context);
                Assert.Equal(intent, result.Verdict.Intent.Value);
                var step = result.Decision.Agent;
                Assert.NotNull(step);
                Assert.Equal(expectedEvents[intent], step.Actions.Single().HostEvent);
                Assert.NotEmpty(result.Response.Lines);
            }
        }
    }

    [Fact]
    public void TheManifestHashIsTheHashTheLoaderComputes()
    {
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(GameBundle.Locate()!, ContentPaths.Manifest)));

        Assert.Equal(manifest.RootElement.GetProperty("bundleHash").GetString(), TestGame.Bundle.ContentHash);
    }

    [Fact]
    public void TheWorldNounBlockResolvesInTheLexiconWithItsGender()
    {
        var lexicon = TestGame.Bundle.Lexicon;

        foreach (var form in new[] { "bloque", "bloques", "árbol", "piedra", "hierba", "tierra", "cueva" })
        {
            Assert.Contains(lexicon.Lookup(form), reading => reading.Pos == "noun");
        }

        Assert.Equal("m", lexicon.Lookup("bloque").First(r => r.Pos == "noun").Gender);
    }
}
