using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Response.Templates;

/// <summary>
/// Turns a verdict and optional feedback into the lines an NPC says. The reply line comes from the pool of the dialogue intent; feedback comes from
/// the optional pools <c>feedback.&lt;kind&gt;.&lt;tag&gt;</c> then <c>feedback.&lt;kind&gt;</c>. Feedback templates may read the verdict's slots plus
/// <c>{tag}</c>, <c>{expected}</c>, <c>{actual}</c> and, for recasts, <c>{recast}</c>; these reserved names override verdict slots of the same name.
/// </summary>
public sealed class ResponsePlanner
{
    /// <summary>Dialogue intent used for the reply when the player was not understood and no explicit intent is given.</summary>
    public const string NotUnderstoodIntent = "not_understood";

    private readonly TemplateEngine _engine;

    /// <summary>Creates a planner that renders with the given engine.</summary>
    public ResponsePlanner(TemplateEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <summary>Creates a planner over the embedded seed lexicon.</summary>
    public ResponsePlanner()
        : this(TemplateEngine.CreateDefault())
    {
    }

    /// <summary>
    /// Plans the response. The reply pool is <paramref name="dialogueIntent"/>, else the verdict's intent when it was communicated and known,
    /// else <see cref="NotUnderstoodIntent"/>; a missing reply pool throws <see cref="KeyNotFoundException"/>. The generator is drawn once for the reply,
    /// then once for feedback when a feedback pool exists. A recast line is spoken before the reply, any other feedback after it.
    /// <paramref name="recastText"/> (from <see cref="RecastBuilder"/>) fills <c>{recast}</c>; it defaults to the request's expected text.
    /// </summary>
    public ResponsePlan Plan(
        Verdict verdict,
        FeedbackRequest? feedback,
        DialogueCatalog catalog,
        SeededRng rng,
        string? dialogueIntent = null,
        string? recastText = null)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rng);

        var intent = dialogueIntent ?? (verdict.Communicated && !verdict.Intent.IsUnknown ? verdict.Intent.Value : NotUnderstoodIntent);
        var reply = DialogueSelector.Select(catalog, intent, rng);
        var replyLine = new ResponseLine(reply.LineId, _engine.Render(reply, verdict.Slots), false);
        var feedbackLine = feedback is null ? null : PlanFeedback(verdict, feedback, catalog, rng, recastText);

        var lines = new List<ResponseLine>(2);
        if (feedbackLine is { IsRecast: true })
        {
            lines.Add(feedbackLine);
            lines.Add(replyLine);
        }
        else
        {
            lines.Add(replyLine);
            if (feedbackLine is not null)
            {
                lines.Add(feedbackLine);
            }
        }

        return new ResponsePlan(lines);
    }

    private ResponseLine? PlanFeedback(Verdict verdict, FeedbackRequest feedback, DialogueCatalog catalog, SeededRng rng, string? recastText)
    {
        var isRecast = string.Equals(feedback.Kind, FeedbackKinds.Recast, StringComparison.Ordinal);
        var recast = recastText ?? feedback.Expected;
        var slots = verdict.Slots
            .With("tag", feedback.Tag)
            .With("expected", feedback.Expected)
            .With("actual", feedback.Actual);
        if (isRecast)
        {
            slots = slots.With("recast", recast);
        }

        if (catalog.TryGetPool($"feedback.{feedback.Kind}.{feedback.Tag}", out var pool)
            || catalog.TryGetPool($"feedback.{feedback.Kind}", out pool))
        {
            var template = DialogueSelector.Select(pool, rng);
            return new ResponseLine(template.LineId, _engine.Render(template, slots), isRecast);
        }

        return isRecast && !string.IsNullOrWhiteSpace(recast)
            ? new ResponseLine("recast", recast, true)
            : null;
    }
}
