using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// Small talk at every point of every NPC's conversation (issues #35 and #37): a learner at A1 says <c>hola</c>, <c>gracias</c>, <c>¿qué tal?</c>,
/// <c>muy bien</c> or <c>adiós</c> to anyone at any time. Before, only some prompts allowed these intents (the stall's <c>¿Qué necesitas?</c> took no
/// <c>gracias</c>, no prompt took <c>muy bien</c>), only some agent states answered them, and a constrained prompt's answer set (its recognizer grammar)
/// held none of them, so the voice could not hear them either.
/// </summary>
public sealed class SmallTalkTests
{
    // The social intents every conversation prompt takes and every agent state answers.
    private static readonly string[] SocialIntents = ["greet", "ask_wellbeing", "state_wellbeing", "thank", "farewell"];

    // What a learner says, and the intent it is.
    public static TheoryData<string, string> Lines => new()
    {
        { "hola", "greet" },
        { "buenas tardes", "greet" },
        { "gracias", "thank" },
        { "muchas gracias", "thank" },
        { "adiós", "farewell" },
        { "hasta luego", "farewell" },
        { "¿qué tal?", "ask_wellbeing" },
        { "¿cómo estás?", "ask_wellbeing" },
        { "como estas", "ask_wellbeing" },
        { "hola, ¿qué tal?", "ask_wellbeing" },
        { "bien, ¿y tú?", "ask_wellbeing" },
        { "muy bien", "state_wellbeing" },
        { "bien, gracias", "state_wellbeing" },
    };

    // The lines every constrained prompt's recognizer grammar holds. Left out: a line typed without its accents (como estas: the recognizer writes them), and the lines
    // whose words pulled other answers onto them in the constrained decode (the gate's Kokoro measurements: «el pan es muy bueno» heard as «muy buenos», «usted hace
    // hachas muy buenas» read as «usted es muy buenas»): buenas tardes and the answers to ¿cómo estás? (muy bien, bien, gracias, bien, ¿y tú?). Those are understood
    // when typed, and by voice through the free decode the pipeline runs when the constrained reading is not taken.
    private static readonly HashSet<string> NotInTheGrammar = new(StringComparer.Ordinal) { "como estas", "buenas tardes", "bien, ¿y tú?", "muy bien", "bien, gracias" };

    public static TheoryData<string, string> SpokenLines()
    {
        var spoken = new TheoryData<string, string>();
        foreach (var row in Lines)
        {
            var (line, intent) = ((string)row[0], (string)row[1]);
            if (!NotInTheGrammar.Contains(line))
            {
                spoken.Add(line, intent);
            }
        }

        return spoken;
    }

    // Every point of a conversation: each NPC, each state of its agent, and the prompt it waits on there (the guide's review prompt too).
    private static IEnumerable<(string Npc, string State, PromptSpec Prompt)> Points()
    {
        var bundle = TestGame.Bundle;
        foreach (var npc in TestGame.Data.Npcs.All)
        {
            var machine = bundle.Agents[npc.Id];
            var initial = machine.Definition.InitialState;
            foreach (var state in machine.Definition.States)
            {
                yield return (npc.Id, state.Id, bundle.Prompts[npc.PromptIn(state.Id, initial)!]);
                if (npc.ReviewPrompt is { } review)
                {
                    yield return (npc.Id, state.Id, bundle.Prompts[review]);
                }
            }
        }
    }

    [Fact]
    public void EveryConversationPromptTakesTheSocialIntents()
    {
        foreach (var prompt in Points().Select(static p => p.Prompt).DistinctBy(static p => p.PromptId))
        {
            foreach (var intent in SocialIntents)
            {
                Assert.True(prompt.AllowedIntentIds.Contains(intent, StringComparer.Ordinal), $"The prompt '{prompt.PromptId}' does not take '{intent}'.");
            }
        }
    }

    [Fact]
    public void EveryAgentStateAnswersTheSocialIntents()
    {
        foreach (var npc in TestGame.Data.Npcs.All)
        {
            foreach (var state in TestGame.Bundle.Agents[npc.Id].Definition.States)
            {
                foreach (var intent in SocialIntents)
                {
                    Assert.True(
                        state.Transitions.Any(t => t.OnHostEvent is null && string.Equals(t.On.Intent, intent, StringComparison.Ordinal)),
                        $"The {npc.Id}'s state '{state.Id}' has no answer to '{intent}'.");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Lines))]
    public void EveryNpcUnderstandsAndAnswersTheLineAtEveryPoint(string line, string intent)
    {
        var bundle = TestGame.Bundle;
        var evaluator = new TypedUtteranceEvaluator(bundle);
        foreach (var (npc, state, prompt) in Points())
        {
            var context = ConversationContext.Create(
                TestGame.Seed, npc, new AgentState(npc, state, 0), new StoredTimestamp(GuideSession.DefaultStartedAt), LearnerModel.New());
            var result = evaluator.Evaluate(line, prompt, context);
            var where = $"'{line}' to the {npc} in '{state}' at '{prompt.PromptId}'";
            Assert.True(result.Verdict.Communicated, $"{where} was not understood.");
            Assert.True(result.Verdict.Intent.Value == intent, $"{where} was read as {result.Verdict.Intent.Value}, not {intent}.");
            var actions = result.Decision.Agent?.Actions ?? [];
            Assert.True(actions.Count > 0, $"{where} got no answer.");
            Assert.DoesNotContain(actions, static a => a.DialogueIntent == "not_understood");
        }
    }

    [Theory]
    [InlineData("¿qué tal?", "que-tal")]
    [InlineData("¿cómo estás?", "que-tal")]
    [InlineData("muy bien", "muy-bien")]
    [InlineData("bien, gracias", "muy-bien")]
    public void TheSmallTalkPhrasesAreCreditedAtEveryNpc(string line, string phrase)
    {
        // PR #48: ¿qué tal? and muy bien are rows of the phrase sheet; said to any NPC they are a use of the row (the villager's prompt practises them, the others
        // credit them by their intent, PhraseIds.For), whatever the prompt's grammar holds.
        foreach (var npc in TestGame.Data.Npcs.All.Select(static n => n.Id))
        {
            using var session = TestGame.NewSession();
            Stage.TeleportToNpc(session, npc);
            _ = session.OpenTalk(npc);

            var turn = session.Say(line);

            Assert.True(turn.Communicated, $"'{line}' to the {npc} was not understood.");
            Assert.True(session.Events.OfType<PhraseUse>().Any(u => u.PhraseId == phrase), $"'{line}' to the {npc} credited no '{phrase}'.");
        }
    }

    [Theory]
    [MemberData(nameof(SpokenLines))]
    public void EveryConstrainedConversationPromptsRecognizerGrammarHoldsTheLine(string line, string intent)
    {
        _ = intent;
        var bundle = TestGame.Bundle;
        var phrase = new AnswerSet("line", [new Answer("line", line, [], [])]).GrammarPhrases(bundle.Language).Single(); // normalized as the recognizer's grammar is
        foreach (var prompt in Points().Select(static p => p.Prompt).DistinctBy(static p => p.PromptId).Where(static p => p.Mode == RecognitionMode.Constrained))
        {
            var grammar = bundle.AnswerSets[prompt.AnswerSetId].GrammarPhrases(bundle.Language);
            Assert.True(grammar.Contains(phrase, StringComparer.Ordinal), $"The recognizer grammar of '{prompt.PromptId}' ({prompt.AnswerSetId}) has no '{phrase}'.");
        }
    }
}
