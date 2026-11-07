using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Determinism;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// The NPCs understand what they teach (village-plan V20, handover 9.3 D (a)). Every row a lesson's learner says (the phrase of each <c>repeat</c> and <c>answer</c> step),
/// said in free talk to the NPC the step is said to, at the prompt that NPC waits on in its first state (its open prompt), is understood: the row's intent, communicated,
/// and an answer of the NPC's that is not "No te entendí". V09 measured 9 of phase B's 27 rows that their own NPC did not understand: the open prompts of the villager,
/// the fisher and the stall took no statements, and Domingo's no <c>tener que</c>. A yes or a no (<c>confirm</c>, <c>deny</c>) answers a question, which an open prompt
/// has not asked: those rows are said where the NPC asks one (the guide's offer, <c>guide-offer</c>). A want said to an NPC who gives things (<c>Necesito pan.</c> at the
/// stall, <c>Quiero madera.</c> to the villager) is a request, as it was before (S6): <c>request_item</c> stands for a <c>state_want</c> row.
/// </summary>
public sealed class TaughtInFreeTalkTests(ITestOutputHelper output)
{
    private static readonly StoredTimestamp Now = new(GuideSession.DefaultStartedAt + 5);

    private static readonly string[] Answers = ["confirm", "deny"];

    private static bool Takes(string intent, string? row) =>
        string.Equals(intent, row, StringComparison.Ordinal) || (row == "state_want" && intent == "request_item");

    [Fact]
    public void EveryRowALessonTeachesIsUnderstoodByTheNpcWhoTaughtIt()
    {
        var data = TestGame.Data;
        var npcs = new NpcService(data, new NoFacts(), TestGame.Seed, new StoredTimestamp(GuideSession.DefaultStartedAt));
        var fresh = data.Npcs.Ids.ToDictionary(static id => id, npcs.Checkpoint, StringComparer.Ordinal);
        var notUnderstood = data.Bundle.Dialogue.Pools["not_understood"].Variants.Select(static v => v.Text).ToHashSet(StringComparer.Ordinal);
        var failures = new List<string>();
        var phaseB = 0;
        foreach (var (lesson, step) in data.Lessons.Lessons.SelectMany(static l => l.Steps.Where(static s => s.Type is LessonStepType.Repeat or LessonStepType.Answer).Select(s => (l, s))))
        {
            var npc = lesson.NpcOf(step);
            var row = data.Phrases.Find(step.Phrase!)!;
            var yesOrNo = Answers.Contains(row.Intent, StringComparer.Ordinal);
            npcs.Rollback(fresh[npc]); // each row to the NPC as the learner finds it: in its first state, at its open prompt
            var scene = new GuideScene(new GuideCell(0, 0, 0), PromptOverride: yesOrNo ? data.Npcs.Find(npc)!.Prompts["offering"] : null);
            var turn = npcs.Say(npc, row.Spanish, scene, Now, "hash");
            var answer = turn.Replies.Concat(turn.FollowUp ?? []).ToList(); // a miss of the world (no place here, no coin) is answered after the turn (AnswerMisses)
            phaseB += lesson.Session >= 7 ? 1 : 0;
            output.WriteLine($"{lesson.Session,2} | {row.Spanish} | {npc} | {turn.Prompt} | {turn.Intent} | {turn.Communicated} | {turn.Feedback} | {string.Join(" ", answer)}");
            if (!turn.Communicated || !Takes(turn.Intent, row.Intent) || turn.Repair is not null || answer.Count == 0 || answer.Any(notUnderstood.Contains))
            {
                failures.Add($"{lesson.Id}/{step.Id}: '{row.Spanish}' to {npc} at {turn.Prompt}: {turn.Intent} (the row's {row.Intent}), communicated {turn.Communicated}, \"{string.Join(" ", answer)}\"");
            }
        }

        Assert.Equal(31, phaseB); // phase B's said rows, sessions 7 to 10 (V09's measure)
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void EveryRowAnNpcPractisesKeepsItsIntentAtItsPrompt()
    {
        // The requests that worked before still resolve to their intents: a statement the open prompts now take must not win a request (V17 found `buscaré madera`
        // read as ask_directions at the fisher's). Each row of an NPC's focus, and each of its variants, at the prompt that practises it.
        var data = TestGame.Data;
        var npcs = new NpcService(data, new NoFacts(), TestGame.Seed, new StoredTimestamp(GuideSession.DefaultStartedAt));
        var fresh = data.Npcs.Ids.ToDictionary(static id => id, npcs.Checkpoint, StringComparer.Ordinal);
        var failures = new List<string>();
        foreach (var npc in data.Npcs.All)
        {
            foreach (var (prompt, phrases) in npc.Focus)
            {
                foreach (var row in phrases.Select(data.Phrases.Find).OfType<PhraseRow>())
                {
                    foreach (var text in row.Texts)
                    {
                        npcs.Rollback(fresh[npc.Id]);
                        var turn = npcs.Say(npc.Id, text, new GuideScene(new GuideCell(0, 0, 0), PromptOverride: prompt), Now, "hash");
                        if (!turn.Communicated || !string.Equals(turn.Intent, row.Intent, StringComparison.Ordinal))
                        {
                            failures.Add($"{row.Id}: '{text}' to {npc.Id} at {prompt}: {turn.Intent} (the row's {row.Intent}), communicated {turn.Communicated}");
                        }
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData("farmer", "¿cuánto es todo?", "ask_total")]
    [InlineData("farmer", "¿cuánto son todo?", "ask_total")] // was unknown (handover 9.3 D (e)); the core grades son as es (M66)
    [InlineData("farmer", "cuanto son todo?", "ask_total")] // was describe and praised (QA-language-8): the noun cuanto took the subject
    [InlineData("baker", "¿cuánto son todo?", "ask_total")]
    [InlineData("baker", "cuanto es todo", "ask_total")]
    [InlineData("farmer", "¿cuánto cuesta?", "ask_price")]
    [InlineData("farmer", "cuanto cuestan las manzanas", "ask_price")]
    [InlineData("farmer", "¿puedo pagar con fruta?", "ask_pay")]
    public void TheMarketsQuestionsAreQuestionsAccentedOrNot(string npc, string line, string intent)
    {
        // ask_total takes son by a keyword rule (cuánto and todo), not a grammar, which the fuzz suite would realise as a correct sentence; describe takes a noun
        // subject only after an article or a name, so cuanto (a noun too, the quantum) is no subject and no question reads as a description.
        var npcs = new NpcService(TestGame.Data, new NoFacts(), TestGame.Seed, new StoredTimestamp(GuideSession.DefaultStartedAt));

        var turn = npcs.Say(npc, line, new GuideScene(new GuideCell(0, 0, 0)), Now, "hash");

        Assert.Equal((intent, true), (turn.Intent, turn.Communicated));
    }

    // The turns ask nothing of the world (the guide's answers name no tile here).
    private sealed class NoFacts : IGuideFacts
    {
        public GuideNearest? NearestOfNoun(GuideViewpoint from, string lemma) => null;

        public GuideNearest? NearestOfNounJoinedTo(GuideViewpoint from, string lemma, GuideCell body) => null;

        public IReadOnlyList<string> NounsAt(GuideCell cell) => [];
    }
}
