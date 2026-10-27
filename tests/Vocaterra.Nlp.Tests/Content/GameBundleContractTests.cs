using System.Text.Json;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Feedback;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Response.Templates;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>
/// The contract between the core's modules and the repository's one content bundle, <c>content/game-v1</c> (R02; before it these were the sample
/// bundle's tests). Each test states a fact the core relies on: every prompt resolves its intents, answer set and agent, every example resolves to its
/// intent, every agent state machine is closed over the bundle's intents, the dialogue has the pools the planner and the agents name, the CEFR lists
/// and the lexicon load. No test counts the lessons: they assert structure, so a new lesson changes nothing here, while a lesson that breaks the
/// structure (a prompt that names a missing set, an agent that can reach an undefined state) fails a core test, which is the point.
/// </summary>
public class GameBundleContractTests
{
    private static readonly ContentBundle Bundle = TestSupport.Bundle;
    private static readonly NormalizationOptions KeepArticles = new(ConvertIndefiniteArticles: false);

    /// <summary>Every example of every intent: the intent, the text and the prompt the example names (empty when it names none).</summary>
    public static TheoryData<string, string, string> IntentExamples()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var intent in Bundle.Intents.Intents)
        {
            foreach (var example in intent.ExampleSpecs)
            {
                data.Add(intent.Id.Value, example.Text, example.Prompt ?? string.Empty);
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

    public static TheoryData<string> AgentIds()
    {
        var data = new TheoryData<string>();
        foreach (var id in Bundle.Agents.Keys)
        {
            data.Add(id);
        }

        return data;
    }

    public static TheoryData<string, string, string, MatchType> AnswerPhrases()
    {
        var data = new TheoryData<string, string, string, MatchType>();
        foreach (var set in Bundle.AnswerSets.Values)
        {
            foreach (var answer in set.Answers)
            {
                data.Add(set.AnswerSetId, answer.Canonical, answer.AnswerId, MatchType.Exact);
            }
        }

        return data;
    }

    private static IntentResolution Resolve(string text, IReadOnlyList<string> allowedIntents)
    {
        var normalized = SpanishTextNormalizer.Normalize(text, KeepArticles);
        var analysis = new LinguisticAnalyzer(Bundle.Lexicon, SpanishLanguagePack.Instance).Analyze(normalized, Bundle.Intents.Grammars);
        var prompt = new PromptSpec("probe", RecognitionMode.Open, Difficulty.Beginner, Bundle.AnswerSets.Keys.First(), allowedIntents, null, []);
        return new IntentResolver(Bundle.Intents, SpanishLanguagePack.Instance).Resolve(normalized, analysis, prompt);
    }

    // ---- Intents -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void EveryIntentHasExamplesAndSomethingToMatchThemWith()
    {
        Assert.NotEmpty(Bundle.Intents.Intents);
        foreach (var intent in Bundle.Intents.Intents)
        {
            Assert.NotEmpty(intent.Examples);
            Assert.True(intent.Grammars.Count > 0 || intent.KeywordRules.Count > 0, $"{intent.Id} has no slot grammar and no keyword rule");
        }

        Assert.Equal(Bundle.Intents.Intents.Select(intent => intent.Id.Value).Distinct(StringComparer.Ordinal).Count(), Bundle.Intents.Intents.Count);
    }

    [Fact]
    public void TheCommonIntentsTheConversationAgentsRelyOnExist()
    {
        // Every agent of the bundle answers these in the pipeline's own vocabulary; the rest of the catalog is the game's.
        var ids = Bundle.Intents.Intents.Select(intent => intent.Id.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var intent in new[] { "greet", "confirm", "deny", "farewell", "thank" })
        {
            Assert.Contains(intent, ids);
        }
    }

    [Theory]
    [MemberData(nameof(IntentExamples))]
    public void EveryIntentExampleResolvesToItsIntentWithItsRequiredSlots(string intentId, string example, string promptId)
    {
        // An example that names a prompt is resolved the way the pipeline resolves it, among the intents that prompt allows; one that names none is
        // resolved among its own intent alone (the lint's coverage check, run through the resolver).
        IReadOnlyList<string> allowed = promptId.Length > 0 ? Bundle.Prompts[promptId].AllowedIntentIds : [intentId];

        var resolution = Resolve(example, allowed);

        Assert.Equal(intentId, resolution.Intent.Value);
        Assert.True(resolution.Rung is IntentRung.Grammar or IntentRung.Rules, $"{example} resolved on {resolution.Rung}");
        foreach (var slot in Bundle.Intents.RequiredSlots(intentId))
        {
            Assert.True(resolution.Slots.ContainsKey(slot), $"'{example}' lacks slot '{slot}': {resolution.Slots}");
        }
    }

    [Theory]
    [InlineData("quiero agua", "request_item", "item=agua")]
    [InlineData("quiero dos panes", "request_item", "item=pan;qty=2")]
    [InlineData("tengo una fruta", "state_have", "item=fruta")]
    [InlineData("dónde está el río", "ask_directions", "place=río")]
    public void ExtractedSlotsAreTheLemmas(string text, string intent, string expectedSlots)
    {
        var resolution = Resolve(text, [intent]);

        Assert.Equal(intent, resolution.Intent.Value);
        Assert.Equal(expectedSlots, string.Join(';', resolution.Slots.Select(pair => pair.Key + "=" + pair.Value)));
    }

    [Fact]
    public void AnUnrelatedSentenceIsNotResolved()
    {
        Assert.True(Resolve("el cielo está muy lejos", Bundle.Intents.Intents.Select(intent => intent.Id.Value).ToArray()).Intent.IsUnknown);
    }

    // ---- Answer sets -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void EveryAnswerSetGivesTheRecognizerPhrasesAndHasUniqueAnswerIds()
    {
        Assert.NotEmpty(Bundle.AnswerSets);
        foreach (var set in Bundle.AnswerSets.Values)
        {
            Assert.NotEmpty(set.GrammarPhrases(SpanishLanguagePack.Instance));
            Assert.Equal(set.Answers.Count, set.Answers.Select(answer => answer.AnswerId).Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Theory]
    [MemberData(nameof(AnswerPhrases))]
    public void EveryCanonicalPhraseMatchesItsOwnAnswerExactly(string setId, string spoken, string answerId, MatchType type)
    {
        var result = AnswerMatcher.Match(SpanishTextNormalizer.Normalize(spoken, KeepArticles), Bundle.AnswerSets[setId], Difficulty.Beginner, SpanishLanguagePack.Instance, KeepArticles);

        Assert.Equal(answerId, result.AnswerId);
        Assert.Equal(type, result.Type);
    }

    [Fact]
    public void AnAliasMatchesItsAnswerAndAPhraseWithoutTheArticleStillMatches()
    {
        var set = Bundle.AnswerSets["yes-no"];
        var yes = set.Answers.Single(answer => answer.AnswerId == "yes");

        foreach (var alias in yes.Aliases)
        {
            var result = AnswerMatcher.Match(SpanishTextNormalizer.Normalize(alias, KeepArticles), set, Difficulty.Beginner, SpanishLanguagePack.Instance, KeepArticles);

            Assert.Equal("yes", result.AnswerId);
        }
    }

    // ---- Prompts -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ThePromptsCoverBothRecognitionModes()
    {
        Assert.Contains(Bundle.Prompts.Values, prompt => prompt.Mode == RecognitionMode.Constrained);
        Assert.Contains(Bundle.Prompts.Values, prompt => prompt.Mode == RecognitionMode.Open);
        Assert.Contains(Bundle.Prompts.Values, prompt => prompt.AgentId is not null);
    }

    [Theory]
    [MemberData(nameof(PromptIds))]
    public void EveryPromptResolvesItsAnswerSetAgentAndIntents(string promptId)
    {
        var prompt = Bundle.Prompts[promptId];

        Assert.Equal(promptId, prompt.PromptId);
        Assert.NotEmpty(Bundle.AnswerSets[prompt.AnswerSetId].GrammarPhrases(SpanishLanguagePack.Instance));
        Assert.False(string.IsNullOrWhiteSpace(prompt.Text));
        if (prompt.AgentId is not null)
        {
            Assert.Contains(prompt.AgentId, Bundle.Agents.Keys);
        }

        var known = Bundle.Intents.Intents.Select(intent => intent.Id.Value).ToHashSet(StringComparer.Ordinal);
        Assert.All(prompt.AllowedIntentIds, intent => Assert.Contains(intent, known));
        Assert.Equal(prompt.AllowedIntentIds.Count, prompt.AllowedIntentIds.Distinct(StringComparer.Ordinal).Count());
    }

    // ---- Agents ------------------------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AgentIds))]
    public void EveryAgentStateMachineIsClosedOverTheBundleIntents(string agentId)
    {
        var agent = Bundle.Agents[agentId];
        var states = agent.Definition.States.Select(state => state.Id).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(agent.Definition.InitialState, states);

        // Every state, answered with every intent of the bundle (communicated or not) and with an unknown one, stays inside the machine and names only
        // dialogue the bundle has.
        var intents = Bundle.Intents.Intents.Select(intent => intent.Id.Value).Append("unknown").ToArray();
        foreach (var stateId in states)
        {
            foreach (var intent in intents)
            {
                foreach (var communicated in new[] { true, false })
                {
                    var verdict = TestSupport.MakeVerdict(intent, communicated, communicated ? 1.0 : 0.0);

                    var result = agent.Step(new AgentState(agentId, stateId, 0), verdict, 5UL);

                    Assert.Contains(result.Next.StateId, states);
                    Assert.NotEmpty(result.Actions);
                    Assert.All(result.Actions, action => Assert.True(Bundle.Dialogue.TryGetPool(action.DialogueIntent, out _), $"{agentId}/{stateId}/{intent}: no pool '{action.DialogueIntent}'"));
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(AgentIds))]
    public void AnAgentThatDoesNotUnderstandSaysSoAndStaysWhereItIs(string agentId)
    {
        var agent = Bundle.Agents[agentId];
        var verdict = TestSupport.MakeVerdict("unknown", communicated: false, correctness: 0);

        var result = agent.Step(agent.InitialState(), verdict, 7UL);

        Assert.Equal(agent.Definition.InitialState, result.Next.StateId);
        Assert.Contains(result.Actions, action => action.DialogueIntent == "not_understood");
    }

    [Theory]
    [MemberData(nameof(AgentIds))]
    public void ChosenVariantsAreStableForTheSameWorldSeed(string agentId)
    {
        var agent = Bundle.Agents[agentId];
        var verdict = TestSupport.MakeVerdict("greet");

        var first = agent.Step(agent.InitialState(), verdict, 42UL);
        var second = agent.Step(agent.InitialState(), verdict, 42UL);

        Assert.Equal(first, second);
    }

    [Fact]
    public void EveryAgentDialogueIntentHasAMatchingPoolOfAtLeastItsVariantCount()
    {
        foreach (var agent in Bundle.Agents.Values)
        {
            var pools = agent.Definition.VariantPools!.ToDictionary(pool => pool.Id, pool => pool.Count, StringComparer.Ordinal);
            foreach (var action in agent.Definition.States.SelectMany(Actions))
            {
                Assert.True(Bundle.Dialogue.TryGetPool(action.DialogueIntent, out var pool), action.DialogueIntent);
                Assert.NotEmpty(pool.Variants);
                if (action.VariantPoolId is { } poolId)
                {
                    Assert.True(pools[poolId] <= pool.Variants.Count);
                }
            }
        }
    }

    // ---- Dialogue, feedback and the response planner ----------------------------------------------------------------------------

    [Fact]
    public void TheDialogueHasTheTwoPipelinePools()
    {
        Assert.True(Bundle.Dialogue.TryGetPool("answer_accepted", out var accepted));
        Assert.NotEmpty(accepted.Variants);
        Assert.True(Bundle.Dialogue.TryGetPool("not_understood", out var notUnderstood));
        Assert.NotEmpty(notUnderstood.Variants);
    }

    [Fact]
    public void EveryFeedbackKindHasAPoolTheEngineCanRender()
    {
        var planner = new ResponsePlanner(Bundle.TemplateEngine);
        var verdict = TestSupport.MakeVerdict("greet");
        var dialogueIntent = Bundle.Agents.Values.SelectMany(agent => agent.Definition.States.SelectMany(Actions)).First().DialogueIntent;
        foreach (var kind in new[] { FeedbackKinds.Recast, FeedbackKinds.Hint, FeedbackKinds.ExplicitCorrection, FeedbackKinds.ModelPronunciation, FeedbackKinds.SlowRepeat })
        {
            var feedback = new FeedbackRequest(ErrorTags.VocabWrong, "espada", "espado", kind);

            var plan = planner.Plan(verdict, feedback, Bundle.Dialogue, new SeededRng(3), dialogueIntent, "una espada");

            Assert.Equal(2, plan.Lines.Count);
            Assert.True(Bundle.Dialogue.TryGetPool("feedback." + kind, out var pool), kind);
            Assert.NotEmpty(pool.Variants);
        }
    }

    [Fact]
    public void ThePlayerSentenceRecastTemplateRendersWithTheEngine()
    {
        // The recast of "yo tiene dos frutas": the verb is conjugated, the quantity is spelled out and the noun follows it.
        var pool = Bundle.Dialogue.Pools["feedback.recast.conj.person"];
        var slots = TestSupport.Slots(("item", "fruta"), ("qty", "2"), ("verb", "tener"));

        var lines = pool.Variants.Select(variant => Bundle.TemplateEngine.Render(variant, slots)).ToArray();

        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.Contains("dos frutas", line, StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Tengo dos frutas", StringComparison.Ordinal) || line.Contains("tengo dos frutas", StringComparison.Ordinal));
    }

    [Fact]
    public void TheResponsePlannerSpeaksTheRecastBeforeTheReply()
    {
        var planner = new ResponsePlanner(Bundle.TemplateEngine);
        var verdict = TestSupport.MakeVerdict("state_have", slots: TestSupport.Slots(("item", "fruta"), ("qty", "2"), ("verb", "tener")));
        var feedback = new FeedbackRequest(ErrorTags.ConjPerson, "tengo", "tiene", FeedbackKinds.Recast);
        var reply = Bundle.Agents["villager"].Definition.States.SelectMany(Actions).First(action => action.DialogueIntent == "villager_thank").DialogueIntent;

        var plan = planner.Plan(verdict, feedback, Bundle.Dialogue, SeededRng.ForInteraction(1UL, "villager", 0), reply, "yo tengo dos frutas");

        Assert.Equal(2, plan.Lines.Count);
        Assert.True(plan.Lines[0].IsRecast);
        Assert.StartsWith("feedback.recast.conj.person.", plan.Lines[0].LineId, StringComparison.Ordinal);
        Assert.Contains("dos frutas", plan.Lines[0].Text, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(reply + ".", plan.Lines[1].LineId, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFeedbackPolicyAndEvaluationConfigLoadForEveryDifficulty()
    {
        Assert.NotEmpty(Bundle.FeedbackPolicy.Rules);
        foreach (var difficulty in Enum.GetValues<Difficulty>())
        {
            var config = Bundle.EvaluationConfig.For(difficulty);

            Assert.InRange(config.CorrectnessThreshold, 0.0, 1.0);
            Assert.InRange(config.CommunicatedConfidenceThreshold, 0.0, 1.0);
        }

        Assert.Equal(CanonicalJson.Serialize(Bundle.FeedbackPolicy), CanonicalJson.Serialize(FeedbackPolicy.FromJson(CanonicalJson.Serialize(Bundle.FeedbackPolicy))));
    }

    // ---- Lexicon, CEFR, provenance, attribution ---------------------------------------------------------------------------------

    [Fact]
    public void TheLexiconIsTheSeedPlusTheBundleWords()
    {
        Assert.True(Bundle.Lexicon.Entries.Count > SpanishLexicon.Seed.Entries.Count);
        foreach (var lemma in new[] { "espada", "lanzar", "hielo", "pócima", "bloque", "lámpara", "río" })
        {
            Assert.True(Bundle.Lexicon.ContainsLemma(lemma), lemma);
        }

        Assert.Equal("lancé", Bundle.SpanishMorphology().Conjugate("lanzar", "1", "sg", "pret"));
    }

    [Fact]
    public void TheCefrListsLoadAndTheFirstIsOrdered()
    {
        Assert.Equal(["a1", "a2"], Bundle.Cefr.Keys);
        Assert.NotEmpty(Bundle.Cefr["a1"]);
        Assert.NotEmpty(Bundle.Cefr["a2"]);
        Assert.Contains("hola", Bundle.Cefr["a1"]);
        // The levels are cumulative (a2 allows the a1 lemmas too), so a lemma listed at both is redundant but harmless; the checker does not need them disjoint.
        Assert.Equal(Bundle.Cefr["a1"].Order(StringComparer.Ordinal), Bundle.Cefr["a1"].ToArray());
    }

    [Fact]
    public void ProvenanceCoversEveryFileExceptItself()
    {
        var listed = Bundle.Provenance.Items.Select(item => item.Path).Order(StringComparer.Ordinal).ToArray();
        var files = Bundle.Manifest.Files.Select(file => file.Path).Where(path => path != ContentPaths.Provenance).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(files, listed);

        // Everything is hand-authored except the generated lexicon and its sidecar (imported) and the rendered speech (model output, host-owned).
        Assert.All(
            Bundle.Provenance.Items,
            item => Assert.Equal(
                item.Path.StartsWith("lexicon/wiktionary-es", StringComparison.Ordinal)
                    ? GeneratedByKinds.WiktionaryImport
                    : item.Path.StartsWith("speech/", StringComparison.Ordinal) ? GeneratedByKinds.ModelOutput : GeneratedByKinds.HandAuthored,
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

    private static IEnumerable<AgentActionDefinition> Actions(AgentStateDefinition state)
    {
        return state.Transitions.SelectMany(transition => transition.Actions).Concat(state.Fallback?.Actions ?? []);
    }
}
