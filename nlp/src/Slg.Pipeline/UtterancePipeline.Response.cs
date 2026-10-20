using Slg.Core.Determinism;
using Slg.Core.Prompts;
using Slg.Core.Stages;
using Slg.Core.Verdicts;
using Slg.Decision.Feedback;
using Slg.Decision.Npc;
using Slg.Decision.Scheduling;
using Slg.Nlp.Intent;
using Slg.Nlp.Matching;
using Slg.Response.Templates;

namespace Slg.Pipeline;

public sealed partial class UtterancePipeline
{
    private const string SilentIntent = "__silent__";
    private const string SilentLineId = "__silent__.1";
    private const string PronunciationTagPrefix = "pron.";

    /// <summary>Dialogue pool voiced when a prompt without an NPC is answered acceptably (an answer-only prompt such as a quiz).</summary>
    public const string AnswerAcceptedPool = "answer_accepted";

    // Decision layer: NPC step, learner update, FSRS rating and feedback plan. Pure; the only randomness is the seeded NPC stream.
    private DecisionOutcome Decide(Verdict verdict, PromptSpec prompt, GameContext context)
    {
        var content = _services.Content;
        NpcStepResult? step = null;
        if (content.Npcs.TryGetValue(context.NpcId, out var machine))
        {
            step = machine.Step(context.NpcState, verdict, NewRng(context));
        }

        IReadOnlyList<string> skills = verdict.Intent.IsUnknown ? [] : [$"intent.{verdict.Intent.Value}"];
        var learner = context.Learner.Update(verdict, skills);
        var rating = RatingMapper.FromVerdict(verdict, _options.RatingThresholds);
        var feedback = content.FeedbackPolicy.Plan(verdict, context.RepeatCountsByTag, prompt.Difficulty);
        return new DecisionOutcome(step, learner, rating, feedback);
    }

    // Response realization: the reply comes from the NPC's actions (honouring their variant draws); the first feedback item is voiced as well.
    // Without an NPC step (a prompt that is not a conversation, such as casting a spell) nobody replies, so only feedback is voiced.
    // The exception is an acceptable answer to a prompt that has no NPC (a quiz): it is acknowledged from the answer_accepted pool when the bundle has one.
    private ResponsePlan Respond(
        Verdict verdict,
        PromptSpec prompt,
        DecisionOutcome decision,
        NormalizedText normalized,
        AnalysisResult analysis,
        MatchResult match,
        GameContext context)
    {
        var content = _services.Content;
        var (request, recast) = BuildFeedback(decision.Feedback, normalized, analysis, match, content.AnswerSets, prompt.AnswerSetId);
        if (prompt.NpcId is null && IsAcceptedAnswer(verdict) && content.Dialogue.TryGetPool(AnswerAcceptedPool, out _))
        {
            return PlanWithFallback(verdict, request, recast, AnswerAcceptedPool, content.Dialogue, context);
        }

        if (decision.Npc is null)
        {
            var feedbackOnly = PlanWithFallback(verdict, request, recast, SilentIntent, _catalogWithSilence, context);
            return new ResponsePlan(feedbackOnly.Lines.Where(static line => !string.Equals(line.LineId, SilentLineId, StringComparison.Ordinal)).ToArray());
        }

        var actions = decision.Npc.Actions;
        var dialogueIntent = actions.Count > 0 ? actions[0].DialogueIntent : null;
        var plan = PlanWithFallback(verdict, request, recast, dialogueIntent, content.Dialogue, context);
        return actions.Count == 0 ? plan : ApplyActions(plan, actions, verdict, context);
    }

    // Communicated and free of grammar or vocabulary errors; pronunciation errors do not count against the answer itself.
    private static bool IsAcceptedAnswer(Verdict verdict)
    {
        return verdict.Communicated
            && verdict.Errors.All(static error => error.Tag.StartsWith(PronunciationTagPrefix, StringComparison.Ordinal));
    }

    // The planner always renders a reply, so a placeholder pool stands in for it when nobody replies and its line is dropped afterwards.
    private static DialogueCatalog WithSilentPool(DialogueCatalog catalog)
    {
        var silent = new VariantPool(SilentIntent, [new DialogueTemplate(SilentLineId, "-")]);
        return new DialogueCatalog(catalog.Pools.Append(new KeyValuePair<string, VariantPool>(SilentIntent, silent)));
    }

    // A feedback template may need a slot the verdict does not have (a recast that conjugates {verb}). Try the specific pool first, then the
    // generic pool of the same kind, then no feedback at all, so a content gap never takes the NPC's reply away.
    private ResponsePlan PlanWithFallback(
        Verdict verdict,
        FeedbackRequest? request,
        string? recast,
        string? dialogueIntent,
        DialogueCatalog dialogue,
        GameContext context)
    {
        try
        {
            return _planner.Plan(verdict, request, dialogue, NewRng(context), dialogueIntent, recast);
        }
        catch (TemplateException) when (request is not null)
        {
            try
            {
                return _planner.Plan(verdict, request with { Tag = string.Empty }, dialogue, NewRng(context), dialogueIntent, recast);
            }
            catch (TemplateException)
            {
                return _planner.Plan(verdict, null, dialogue, NewRng(context), dialogueIntent);
            }
        }
    }

    // Replaces the planner's own variant draw with the NPC's draw (NpcAction.VariantIndex) and voices any further actions after the reply.
    private ResponsePlan ApplyActions(ResponsePlan plan, IReadOnlyList<NpcAction> actions, Verdict verdict, GameContext context)
    {
        var lines = new List<ResponseLine>(plan.Lines);
        var replyIndex = lines.Count > 1 && lines[0].IsRecast ? 1 : 0;
        var rng = NewRng(context);
        var dialogue = _services.Content.Dialogue;
        var engine = _services.Content.TemplateEngine;
        for (var i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            if (!dialogue.TryGetPool(action.DialogueIntent, out var pool))
            {
                continue;
            }

            if (i == 0 && action.VariantIndex is null)
            {
                continue;
            }

            var template = action.VariantIndex is { } index
                ? pool.Variants[index % pool.Variants.Count]
                : DialogueSelector.Select(pool, rng);
            var line = new ResponseLine(template.LineId, engine.Render(template, verdict.Slots), false);
            if (i == 0)
            {
                lines[replyIndex] = line;
            }
            else
            {
                lines.Insert(replyIndex + i, line);
            }
        }

        return new ResponsePlan(lines);
    }

    private static (FeedbackRequest? Request, string? Recast) BuildFeedback(
        FeedbackPlan plan,
        NormalizedText normalized,
        AnalysisResult analysis,
        MatchResult match,
        IReadOnlyDictionary<string, AnswerSet> answerSets,
        string answerSetId)
    {
        if (plan.Items.Count == 0)
        {
            return (null, null);
        }

        var item = plan.Items[0];

        // The verdict assembler cannot name the right answer for vocab.wrong, so it puts the answer set id there; that is not text to say.
        var expected = string.Equals(item.Expected, answerSetId, StringComparison.Ordinal) ? string.Empty : item.Expected;
        var needsExpected = item.Type is FeedbackType.ExplicitCorrection or FeedbackType.ModelPronunciation or FeedbackType.SlowRepeat;
        if (expected.Length == 0 && needsExpected && match.AnswerId is { } answerId)
        {
            expected = CanonicalOf(answerSets, answerId);
        }

        if (expected.Length == 0 && needsExpected)
        {
            // Nothing to model or correct: the repair step carries the NPC's reaction instead.
            return (null, null);
        }

        var recast = item.Type == FeedbackType.Recast ? RecastBuilder.Build(normalized, analysis) : null;
        return (new FeedbackRequest(item.Tag, expected, item.Actual, KindOf(item.Type)), recast);
    }

    private static string CanonicalOf(IReadOnlyDictionary<string, AnswerSet> answerSets, string answerId)
    {
        foreach (var set in answerSets.Values)
        {
            foreach (var answer in set.Answers)
            {
                if (string.Equals(answer.AnswerId, answerId, StringComparison.Ordinal))
                {
                    return answer.Canonical;
                }
            }
        }

        return string.Empty;
    }

    private static string KindOf(FeedbackType type)
    {
        return type switch
        {
            FeedbackType.Recast => FeedbackKinds.Recast,
            FeedbackType.Hint => FeedbackKinds.Hint,
            FeedbackType.ExplicitCorrection => FeedbackKinds.ExplicitCorrection,
            FeedbackType.ModelPronunciation => FeedbackKinds.ModelPronunciation,
            FeedbackType.SlowRepeat => FeedbackKinds.SlowRepeat,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown feedback type."),
        };
    }

    private static RepairStep? RepairFor(Verdict verdict, PromptSpec prompt, AnswerSet answerSet, GameContext context)
    {
        if (verdict.Communicated)
        {
            return null;
        }

        context.RepeatCountsByTag.TryGetValue(FeedbackPolicy.NotCommunicatedTag, out var seen);
        return RepairStrategy.Next(Math.Max(1, seen), prompt, answerSet.GrammarPhrases());
    }

    private static SeededRng NewRng(GameContext context)
    {
        return SeededRng.ForInteraction(context.WorldSeed, context.NpcId, context.NpcState.InteractionCounter);
    }
}
