using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Words;

namespace Vocaterra.Game.UI;

/// <summary>
/// The check battery's state as the screen shows it (S11): the items in their planned order, the answer of each, the typed line, the notice and
/// the phase. It is a plain model (no engine): the game draws it (<see cref="CheckLayer"/>), feeds it the picks and the worker's grades, and reads
/// the results. Nothing here touches the conversations, the learner model, the phrase progress or the world.
/// </summary>
public sealed class CheckScreen
{
    private readonly Func<double> _clock;
    private readonly Action<CheckResult>? _onResult;
    private readonly List<CheckResult> _results = [];
    private double _shownAt;
    private double _submittedAt;

    /// <summary>Starts a battery on its plan. The first item is shown at once.</summary>
    /// <param name="kind">Pre, post or recall.</param>
    /// <param name="plan">The items in the order shown (<see cref="CheckPlan.For"/>).</param>
    /// <param name="clockMilliseconds">The front-end's clock, for the latency of each answer.</param>
    /// <param name="onResult">Called with each answer as it is recorded (the session log writes it).</param>
    public CheckScreen(CheckKind kind, IReadOnlyList<CheckPlanEntry> plan, Func<double> clockMilliseconds, Action<CheckResult>? onResult = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(clockMilliseconds);
        Kind = kind;
        Plan = plan;
        _clock = clockMilliseconds;
        _onResult = onResult;
        _shownAt = clockMilliseconds();
        if (plan.Count == 0)
        {
            Phase = CheckPhase.Done;
        }
    }

    /// <summary>The check kind.</summary>
    public CheckKind Kind { get; }

    /// <summary>The items in the order shown.</summary>
    public IReadOnlyList<CheckPlanEntry> Plan { get; }

    /// <summary>The place of the current item in the plan (the plan's length when done).</summary>
    public int Index { get; private set; }

    /// <summary>The current item, or <see langword="null"/> when the battery is done.</summary>
    public CheckPlanEntry? Current => Index < Plan.Count ? Plan[Index] : null;

    /// <summary>Where the battery stands.</summary>
    public CheckPhase Phase { get; private set; } = CheckPhase.Showing;

    /// <summary>Whether every item is answered or skipped.</summary>
    public bool IsDone => Phase == CheckPhase.Done;

    /// <summary>The answers so far, in the order given.</summary>
    public IReadOnlyList<CheckResult> Results => _results;

    /// <summary>The typed line of a production item (the text box).</summary>
    public TextEntry Entry { get; } = new(200);

    /// <summary>
    /// What the done screen says under the summary, in place of the footer about the session log (the playtest's thank-you, S12), or
    /// <see langword="null"/> for the plain screen.
    /// </summary>
    public string? Closing { get; set; }

    /// <summary>Whether the text box has the keyboard (W or Tab).</summary>
    public bool IsTyping { get; set; }

    /// <summary>Whether the microphone is recording the answer (V held).</summary>
    public bool Listening { get; set; }

    /// <summary>A line for the learner under the item: a microphone that is not there, a problem with the answer.</summary>
    public string? Notice { get; set; }

    /// <summary>The milliseconds since the current item was shown (the latency of a pick).</summary>
    public double ElapsedMilliseconds => _clock() - _shownAt;

    /// <summary>Records a pick on the current comprehension item.</summary>
    /// <param name="shown">The position picked, from 0, among the options as shown.</param>
    /// <returns>Whether the pick was taken (the battery is showing a comprehension item).</returns>
    public bool Pick(int shown)
    {
        if (Phase != CheckPhase.Showing || Current is not { } entry || entry.Item.Kind != CheckItemKind.Comprehension)
        {
            return false;
        }

        if (shown < 0 || shown >= entry.OptionOrder.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(shown), shown, "The option is not on the screen.");
        }

        Record(new CheckResult(entry.Item.Id, CheckItemKind.Comprehension, entry.Item.Phrase, CheckInput.Pick, shown, entry.IsCorrect(shown), null, null, false, false, [], null, ElapsedMilliseconds, null), advance: true);
        return true;
    }

    /// <summary>Marks the current production item as sent to the worker (typed, spoken or "no sé").</summary>
    /// <returns>Whether the item was showing (a second send while one is with the worker is refused).</returns>
    public bool Sent()
    {
        if (Phase != CheckPhase.Showing || Current is not { Item.Kind: CheckItemKind.Production })
        {
            return false;
        }

        Phase = CheckPhase.Thinking;
        Notice = null;
        IsTyping = false;
        Listening = false;
        _submittedAt = _clock();
        return true;
    }

    /// <summary>The worker's grade of the current production item: recorded, and the battery moves on.</summary>
    /// <param name="grade">What the core made of the answer.</param>
    public void Graded(CheckGrade grade)
    {
        ArgumentNullException.ThrowIfNull(grade);
        if (Phase != CheckPhase.Thinking || Current is not { } entry)
        {
            return; // a grade that arrives after the battery moved on (it cannot, the worker is in order) is not recorded twice
        }

        var input = grade.Input switch
        {
            InputKind.Button => CheckInput.Button,
            InputKind.Spoken => CheckInput.Spoken,
            _ => CheckInput.Typed,
        };
        Record(new CheckResult(
            entry.Item.Id,
            CheckItemKind.Production,
            entry.Item.Phrase,
            input,
            null,
            grade.Correct,
            grade.Transcript,
            grade.Intent,
            grade.Communicated,
            grade.DontKnow,
            grade.ErrorTags,
            grade.Confidence,
            _clock() - _submittedAt,
            grade.Entry), advance: true);
    }

    /// <summary>The worker could not grade the answer (no microphone model, an unreadable file): the item stays on the screen with the reason.</summary>
    /// <param name="problem">The reason, for the learner.</param>
    public void Failed(string problem)
    {
        if (Phase != CheckPhase.Thinking)
        {
            return;
        }

        Phase = CheckPhase.Showing;
        Notice = problem;
    }

    /// <summary>Esc: the rest of the battery is skipped (each unanswered item is recorded as skipped) and the battery is done.</summary>
    public void SkipRest()
    {
        if (IsDone)
        {
            return;
        }

        for (var i = Index; i < Plan.Count; i++)
        {
            var entry = Plan[i];
            Record(new CheckResult(entry.Item.Id, entry.Item.Kind, entry.Item.Phrase, CheckInput.Skipped, null, false, null, null, false, false, [], null, 0, null), advance: false);
        }

        Index = Plan.Count;
        Phase = CheckPhase.Done;
        IsTyping = false;
        Listening = false;
    }

    // Keeps an answer and tells the session log of it; an answered item moves the battery on, a skipped one is recorded as the battery ends.
    private void Record(CheckResult result, bool advance)
    {
        _results.Add(result);
        _onResult?.Invoke(result);
        if (advance)
        {
            Advance();
        }
    }

    private void Advance()
    {
        Index++;
        Notice = null;
        Entry.Clear();
        IsTyping = false;
        Listening = false;
        _shownAt = _clock();
        if (Index >= Plan.Count)
        {
            Phase = CheckPhase.Done;
            Index = Plan.Count;
        }
        else
        {
            Phase = CheckPhase.Showing;
        }
    }
}
