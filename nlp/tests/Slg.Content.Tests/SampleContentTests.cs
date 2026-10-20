using System.Text.Json;
using Slg.Core.Determinism;
using Slg.Core.Prompts;
using Slg.Core.Stages;
using Slg.Core.Taxonomy;
using Slg.Core.Verdicts;
using Slg.Decision.Feedback;
using Slg.Decision.Npc;
using Slg.Evaluation;
using Slg.Nlp.Analysis;
using Slg.Nlp.Intent;
using Slg.Nlp.Matching;
using Slg.Nlp.Normalization;
using Slg.Response.Effects;
using Slg.Response.Templates;
using MatchType = Slg.Core.Stages.MatchType;

namespace Slg.Content.Tests;

/// <summary>Checks that the committed sample bundle really works with the module code that consumes it.</summary>
public class SampleContentTests
{
    private static readonly ContentBundle Bundle = TestSupport.Bundle;
    private static readonly NormalizationOptions KeepArticles = new(ConvertIndefiniteArticles: false);

    public static TheoryData<string, string> IntentExamples()
    {
        var data = new TheoryData<string, string>();
        foreach (var intent in Bundle.Intents.Intents)
        {
            foreach (var example in intent.Examples)
            {
                data.Add(intent.Id.Value, example);
            }
        }

        return data;
    }

    public static TheoryData<string> PromptIds()
    {
        var data = new TheoryData<string>();
        foreach (var id in Bundle.Prompts.Keys)
        {
            data.Add(id);
        }

        return data;
    }

    private static IntentResolution Resolve(string text, params string[] allowedIntents)
    {
        var normalized = TextNormalizer.Normalize(text, KeepArticles);
        var analysis = new LinguisticAnalyzer(Bundle.Lexicon).Analyze(normalized, Bundle.Intents.Grammars);
        var prompt = new PromptSpec("probe", RecognitionMode.Open, Difficulty.Beginner, "items-basic", allowedIntents, null, []);
        return new IntentResolver(Bundle.Intents).Resolve(normalized, analysis, prompt);
    }

    [Fact]
    public void TheBundleHasTheSixSampleIntentsWithEnoughExamples()
    {
        Assert.Equal(
            ["ask_price", "buy_item", "cast_spell", "greet", "offer_item", "sell_item"],
            Bundle.Intents.Intents.Select(intent => intent.Id.Value));
        foreach (var intent in Bundle.Intents.Intents)
        {
            Assert.True(intent.Examples.Count >= 4, $"{intent.Id} has {intent.Examples.Count} examples");
            Assert.NotEmpty(intent.Grammars);
        }

        Assert.Equal(["item", "qty"], Bundle.Intents.RequiredSlots("offer_item"));
        Assert.Equal(["spell"], Bundle.Intents.RequiredSlots("cast_spell"));
        Assert.Empty(Bundle.Intents.RequiredSlots("greet"));
        Assert.Equal(["item"], Bundle.Intents.RequiredSlots("buy_item"));
    }

    [Theory]
    [MemberData(nameof(IntentExamples))]
    public void EveryIntentExampleResolvesToItsIntentWithItsRequiredSlots(string intentId, string example)
    {
        var resolution = Resolve(example);

        Assert.Equal(intentId, resolution.Intent.Value);
        Assert.True(resolution.Rung is IntentRung.Grammar or IntentRung.Rules, $"{example} resolved on {resolution.Rung}");
        foreach (var slot in Bundle.Intents.RequiredSlots(intentId))
        {
            Assert.True(resolution.Slots.ContainsKey(slot), $"'{example}' lacks slot '{slot}': {resolution.Slots}");
        }
    }

    [Theory]
    [InlineData("quiero comprar dos pociones", "buy_item", "item=poción;qty=2")]
    [InlineData("lanzo un hechizo de fuego", "cast_spell", "spell=fuego")]
    [InlineData("lanzo el escudo mágico", "cast_spell", "spell=escudo")]
    [InlineData("tengo dos espadas", "offer_item", "item=espada;qty=2;verb=tener")]
    [InlineData("yo tiene dos espadas", "offer_item", "item=espada;qty=2;verb=tener")]
    [InlineData("cuánto cuestan las flechas", "ask_price", "item=flecha")]
    [InlineData("puedo vender un arco", "sell_item", "item=arco")]
    public void ExtractedSlotsAreTheLemmas(string text, string intent, string expectedSlots)
    {
        var resolution = Resolve(text, intent);

        Assert.Equal(intent, resolution.Intent.Value);
        Assert.Equal(expectedSlots, string.Join(';', resolution.Slots.Select(pair => pair.Key + "=" + pair.Value)));
    }

    [Fact]
    public void AnUnrelatedSentenceIsNotResolved()
    {
        Assert.True(Resolve("el gato come pescado").Intent.IsUnknown);
    }

    [Fact]
    public void TheAnswerSetsListTheSampleItemsSpellsAndGreetings()
    {
        Assert.Equal(["greetings", "items-basic", "spells-basic"], Bundle.AnswerSets.Keys);
        Assert.Equal(["arrow", "bow", "potion", "shield", "sword"], Bundle.AnswerSets["items-basic"].Answers.Select(a => a.AnswerId).Order(StringComparer.Ordinal));
        Assert.Equal(["fire", "ice", "lightning", "magic-shield"], Bundle.AnswerSets["spells-basic"].Answers.Select(a => a.AnswerId).Order(StringComparer.Ordinal));
        Assert.Contains("brebaje", Bundle.AnswerSets["items-basic"].GrammarPhrases());
        Assert.Contains("escudo mágico", Bundle.AnswerSets["spells-basic"].GrammarPhrases());
    }

    [Theory]
    [InlineData("items-basic", "espada", "sword", MatchType.Exact)]
    [InlineData("items-basic", "una espada", "sword", MatchType.Alias)]
    [InlineData("items-basic", "pócima", "potion", MatchType.Alias)]
    [InlineData("items-basic", "brebaje", "potion", MatchType.Alias)]
    [InlineData("items-basic", "poción", "potion", MatchType.Exact)]
    [InlineData("items-basic", "escudos", "shield", MatchType.Fuzzy)]
    [InlineData("spells-basic", "el escudo mágico", "magic-shield", MatchType.Alias)]
    [InlineData("greetings", "qué onda", "how-are-you", MatchType.Alias)]
    [InlineData("greetings", "buenas tardes", "good-afternoon", MatchType.Exact)]
    public void AnswerSetsMatchCanonicalAliasAndDialectPhrases(string setId, string spoken, string answerId, MatchType type)
    {
        var result = AnswerMatcher.Match(TextNormalizer.Normalize(spoken, KeepArticles), Bundle.AnswerSets[setId], Difficulty.Beginner, KeepArticles);

        Assert.Equal(answerId, result.AnswerId);
        Assert.Equal(type, result.Type);
    }

    [Fact]
    public void ThereAreAtLeastTenPromptsSpanningModesAndDifficulties()
    {
        Assert.True(Bundle.Prompts.Count >= 10);
        foreach (var mode in new[] { RecognitionMode.Constrained, RecognitionMode.Open })
        {
            foreach (var difficulty in new[] { Difficulty.Beginner, Difficulty.Intermediate })
            {
                Assert.Contains(Bundle.Prompts.Values, prompt => prompt.Mode == mode && prompt.Difficulty == difficulty);
            }
        }

        Assert.Contains(Bundle.Prompts.Values, prompt => prompt.NpcId is null);
        Assert.Contains(Bundle.Prompts.Values, prompt => prompt.NpcId == "blacksmith");
        Assert.Contains(Bundle.Prompts.Values, prompt => prompt.AllowedIntentIds.Contains("cast_spell"));
        Assert.Contains(Bundle.Prompts.Values, prompt => prompt.AllowedIntentIds.Count == 0);
    }

    [Theory]
    [MemberData(nameof(PromptIds))]
    public void EveryPromptGivesTheRecognizerPhrasesAndOnlyAllowsItsOwnIntents(string promptId)
    {
        var prompt = Bundle.Prompts[promptId];
        var set = Bundle.AnswerSets[prompt.AnswerSetId];

        Assert.NotEmpty(set.GrammarPhrases());
        Assert.Equal(prompt.AllowedIntentIds.Order(StringComparer.Ordinal), prompt.AllowedIntentIds);
        Assert.Equal(promptId, prompt.PromptId);
    }

    [Fact]
    public void TheBlacksmithHasTheThreeStatesAndWalksThroughATrade()
    {
        var smith = Bundle.Npcs["blacksmith"];
        Assert.Equal(["idle", "trading", "done"], smith.Definition.States.Select(state => state.Id));
        Assert.Equal("idle", smith.Definition.InitialState);

        var state = smith.InitialState();
        state = Step(smith, state, "greet", out var greeting);
        Assert.Equal("trading", state.StateId);
        Assert.Equal("greet_reply", Assert.Single(greeting.Actions).DialogueIntent);

        state = Step(smith, state, "buy_item", out var buying);
        Assert.Equal("trading", state.StateId);
        Assert.Equal(new[] { "trade.buy" }, buying.Actions.Select(a => a.GameEvent!));

        state = Step(smith, state, "offer_item", out _);
        Assert.Equal("done", state.StateId);

        state = Step(smith, state, "ask_price", out var closing);
        Assert.Equal("done", state.StateId);
        Assert.Equal("trade_done", Assert.Single(closing.Actions).DialogueIntent);

        state = Step(smith, state, "greet", out _);
        Assert.Equal("trading", state.StateId);
        Assert.Equal(5L, state.InteractionCounter);
    }

    [Theory]
    [InlineData("greet", "greet_reply")]
    [InlineData("buy_item", "buy_item_reply")]
    [InlineData("sell_item", "sell_item_reply")]
    [InlineData("offer_item", "offer_item_reply")]
    [InlineData("ask_price", "ask_price_reply")]
    public void TheBlacksmithHandlesEveryTradeIntentInTheIdleStateAndMovesToTrading(string intent, string dialogueIntent)
    {
        var smith = Bundle.Npcs["blacksmith"];
        var idle = smith.InitialState();
        Assert.Equal("idle", idle.StateId);

        var next = Step(smith, idle, intent, out var result);

        Assert.Equal("trading", next.StateId);
        Assert.Equal(dialogueIntent, result.Actions[0].DialogueIntent);
        Assert.DoesNotContain(result.Actions, static action => action.DialogueIntent == "not_understood");
    }

    [Theory]
    [InlineData("sell_item")]
    [InlineData("offer_item")]
    public void TheIdleStateRepliesToATradeLikeTheTradingState(string intent)
    {
        var smith = Bundle.Npcs["blacksmith"];
        var fromIdle = smith.Step(smith.InitialState(), TestSupport.MakeVerdict(intent), 11UL);
        var fromTrading = smith.Step(new NpcState("blacksmith", "trading", 0), TestSupport.MakeVerdict(intent), 11UL);

        Assert.Equal(fromTrading.Actions, fromIdle.Actions);
    }

    [Fact]
    public void TheDialogueHasAnAnswerAcceptedPoolForAnswerOnlyPrompts()
    {
        Assert.True(Bundle.Dialogue.TryGetPool("answer_accepted", out var pool));
        Assert.InRange(pool.Variants.Count, 2, 3);
        Assert.Contains("¡Correcto!", pool.Variants.Select(static variant => variant.Text));
    }

    [Fact]
    public void ANpcThatDoesNotUnderstandStaysWhereItIs()
    {
        var smith = Bundle.Npcs["blacksmith"];
        var verdict = TestSupport.MakeVerdict("unknown", communicated: false, correctness: 0);

        var result = smith.Step(smith.InitialState(), verdict, 7UL);

        Assert.Equal("idle", result.Next.StateId);
        Assert.Equal("not_understood", Assert.Single(result.Actions).DialogueIntent);
    }

    [Fact]
    public void ChosenVariantsAreStableForTheSameWorldSeed()
    {
        var smith = Bundle.Npcs["blacksmith"];
        var verdict = TestSupport.MakeVerdict("greet");

        var first = smith.Step(smith.InitialState(), verdict, 42UL);
        var second = smith.Step(smith.InitialState(), verdict, 42UL);

        Assert.Equal(first, second);
        Assert.InRange(first.Actions[0].VariantIndex!.Value, 0, 2);
    }

    [Fact]
    public void EveryNpcDialogueIntentHasAtLeastTwoVariantsAndAMatchingPool()
    {
        foreach (var smith in Bundle.Npcs.Values)
        {
            var pools = smith.Definition.VariantPools!.ToDictionary(pool => pool.Id, pool => pool.Count, StringComparer.Ordinal);
            foreach (var action in smith.Definition.States.SelectMany(Actions))
            {
                Assert.True(Bundle.Dialogue.TryGetPool(action.DialogueIntent, out var pool), action.DialogueIntent);
                Assert.True(pool.Variants.Count >= 2, $"{action.DialogueIntent} has {pool.Variants.Count} variants");
                if (action.VariantPoolId is { } poolId)
                {
                    Assert.True(pools[poolId] <= pool.Variants.Count);
                }
            }
        }
    }

    [Fact]
    public void ThePlayerSentenceRecastTemplateRendersWithTheEngine()
    {
        var pool = Bundle.Dialogue.Pools["feedback.recast.conj.person"];
        var slots = TestSupport.Slots(("item", "espada"), ("qty", "2"), ("verb", "tener"));

        var lines = pool.Variants.Select(variant => Bundle.TemplateEngine.Render(variant, slots)).ToArray();

        Assert.Equal(["¿Tienes dos espadas? ¡Perfecto!", "Ah, ¿tienes dos espadas? ¡Muy bien!"], lines);
    }

    [Fact]
    public void TheResponsePlannerSpeaksTheRecastBeforeTheReply()
    {
        var planner = new ResponsePlanner(Bundle.TemplateEngine);
        var verdict = TestSupport.MakeVerdict("offer_item", slots: TestSupport.Slots(("item", "espada"), ("qty", "2"), ("verb", "tener")));
        var feedback = new FeedbackRequest(ErrorTags.ConjPerson, "tengo", "tiene", FeedbackKinds.Recast);

        var plan = planner.Plan(verdict, feedback, Bundle.Dialogue, SeededRng.ForInteraction(1UL, "blacksmith", 0), "offer_item_reply", "yo tengo dos espadas");

        Assert.Equal(2, plan.Lines.Count);
        Assert.True(plan.Lines[0].IsRecast);
        Assert.StartsWith("feedback.recast.conj.person.", plan.Lines[0].LineId, StringComparison.Ordinal);
        Assert.Contains("tienes dos espadas", plan.Lines[0].Text, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("offer_item_reply.", plan.Lines[1].LineId, StringComparison.Ordinal);
        Assert.Contains("dos espadas", plan.Lines[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryFeedbackKindHasAPoolTheEngineCanRender()
    {
        var planner = new ResponsePlanner(Bundle.TemplateEngine);
        var verdict = TestSupport.MakeVerdict("greet");
        foreach (var kind in new[] { FeedbackKinds.Recast, FeedbackKinds.Hint, FeedbackKinds.ExplicitCorrection, FeedbackKinds.ModelPronunciation, FeedbackKinds.SlowRepeat })
        {
            var feedback = new FeedbackRequest(ErrorTags.VocabWrong, "espada", "espado", kind);

            var plan = planner.Plan(verdict, feedback, Bundle.Dialogue, new SeededRng(3), "greet_reply", "una espada");

            Assert.Equal(2, plan.Lines.Count);
            Assert.True(Bundle.Dialogue.TryGetPool("feedback." + kind, out var pool), kind);
            Assert.True(pool.Variants.Count >= 2, kind);
        }
    }

    [Fact]
    public void TheFeedbackPolicyAndEvaluationConfigAreTheModuleDefaults()
    {
        Assert.Equal(FeedbackPolicy.Default, Bundle.FeedbackPolicy);
        Assert.Equal(EvaluationConfig.Default, Bundle.EvaluationConfig);
        Assert.Equal(CanonicalJson.Serialize(EvaluationConfig.Default), CanonicalJson.Serialize(Bundle.EvaluationConfig));
        Assert.Equal(CanonicalJson.Serialize(FeedbackPolicy.Default), CanonicalJson.Serialize(Bundle.FeedbackPolicy));
    }

    [Fact]
    public void TheEffectTableMapsSpellsAndPurchasesToCurves()
    {
        var spell = Bundle.Effects.Effects.Single(effect => effect.Intent == "cast_spell");
        Assert.Equal(EffectKinds.SpellPower, spell.Kind);
        Assert.Equal([new CurvePoint(0, 0), new CurvePoint(0.5, 0.5), new CurvePoint(1, 1)], spell.Curve);
        var purchase = Bundle.Effects.Effects.Single(effect => effect.Intent == "buy_item");
        Assert.Equal(EffectKinds.TradePrice, purchase.Kind);
        Assert.Equal([new CurvePoint(0, 1.3), new CurvePoint(1, 1.0)], purchase.Curve);

        var cast = EffectResolver.Resolve(TestSupport.MakeVerdict("cast_spell", correctness: 0.25), Bundle.Effects);
        Assert.Equal(0.25, Assert.Single(cast).Value, 12);
        var buy = EffectResolver.Resolve(TestSupport.MakeVerdict("buy_item", correctness: 0.5), Bundle.Effects);
        Assert.Equal(1.15, Assert.Single(buy).Value, 12);
        Assert.Empty(EffectResolver.Resolve(TestSupport.MakeVerdict("buy_item", communicated: false, correctness: 1), Bundle.Effects));
    }

    [Fact]
    public void TheLexiconIsTheSeedPlusTheGameWords()
    {
        Assert.True(Bundle.Lexicon.Entries.Count > Slg.Nlp.Lexicon.Lexicon.Seed.Entries.Count);
        foreach (var lemma in new[] { "espada", "lanzar", "ofrecer", "hielo", "rayo", "pócima" })
        {
            Assert.True(Bundle.Lexicon.ContainsLemma(lemma), lemma);
        }

        Assert.Equal("lancé", Bundle.Morphology.Conjugate("lanzar", "1", "sg", "pret"));
        Assert.Equal("ofrezco", Bundle.Morphology.Conjugate("ofrecer", "1", "sg", "pres"));
    }

    [Fact]
    public void TheCefrListsAreTheExpectedSize()
    {
        Assert.Equal(["a1", "a2"], Bundle.Cefr.Keys);
        Assert.InRange(Bundle.Cefr["a1"].Count, 250, 400);
        Assert.InRange(Bundle.Cefr["a2"].Count, 450, 700);
        Assert.Contains("comprar", Bundle.Cefr["a1"]);
        Assert.Empty(Bundle.Cefr["a1"].Intersect(Bundle.Cefr["a2"]));
        Assert.Equal(Bundle.Cefr["a1"].Order(StringComparer.Ordinal), Bundle.Cefr["a1"].ToArray());
    }

    [Fact]
    public void ProvenanceCoversEveryFileExceptItself()
    {
        var listed = Bundle.Provenance.Items.Select(item => item.Path).Order(StringComparer.Ordinal).ToArray();
        var files = Bundle.Manifest.Files.Select(file => file.Path).Where(path => path != ContentPaths.Provenance).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(files, listed);

        // Everything is hand-authored except the generated lexicon and its sidecar, which are marked as imported.
        Assert.All(
            Bundle.Provenance.Items,
            item => Assert.Equal(
                item.Path.StartsWith("lexicon/wiktionary-es", StringComparison.Ordinal) ? GeneratedByKinds.WiktionaryImport : GeneratedByKinds.HandAuthored,
                item.GeneratedBy));
    }

    [Fact]
    public void TheAttributionNoticeNamesEveryModelAndItsLicense()
    {
        var notice = File.ReadAllText(Path.Combine(TestSupport.BundleDir, ContentPaths.Attribution));
        using var manifest = JsonDocument.Parse(File.ReadAllText(TestSupport.ModelManifest));

        var models = manifest.RootElement.GetProperty("models").EnumerateArray().ToArray();
        Assert.NotEmpty(models);
        foreach (var model in models)
        {
            Assert.Contains(model.GetProperty("id").GetString()!, notice, StringComparison.Ordinal);
            Assert.Contains(model.GetProperty("license").GetString()!, notice, StringComparison.Ordinal);
        }

        Assert.Contains("CC BY-SA", notice, StringComparison.Ordinal);
        Assert.Contains("3.0", notice, StringComparison.Ordinal);
        Assert.Contains("4.0", notice, StringComparison.Ordinal);
    }

    private static IEnumerable<NpcActionDefinition> Actions(NpcStateDefinition state)
    {
        return state.Transitions.SelectMany(transition => transition.Actions).Concat(state.Fallback?.Actions ?? []);
    }

    private static NpcState Step(NpcStateMachine machine, NpcState state, string intent, out NpcStepResult result)
    {
        result = machine.Step(state, TestSupport.MakeVerdict(intent), 11UL);
        return result.Next;
    }
}
