using Vocaterra.Game.Language.Phrases;
using Vocaterra.Game.Session;

namespace Vocaterra.Game.Npc;

/// <summary>
/// The second try after "no sé" (X04, R02): a phrase the learner said "no sé" to in this session is asked once more, later in the same session, at the next
/// talk with the NPC it was said to. This type is the schedule only: plain state, no clock and no random numbers. It is not saved (a loaded game is a new play
/// session, and a second try belongs to the session it came from).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a talk and not a step.</b> A second try is asked as the guide's phrase review (<see cref="PhraseReview"/>), and the core evaluates the answer on the
/// review's prompt. A review opened between two lesson steps would take the next step's prompt. So a second try waits for the next talk with its NPC, and is
/// only asked when no lesson holds that conversation and no other review is open (<see cref="GuideSession"/> decides that).
/// </para>
/// <para>
/// <b>The rules.</b> At most <see cref="MaxPending"/> phrases wait at a time (repetition fatigue, R02). A phrase is asked at most once in the session: once it has
/// been taken, it is never queued again, so a second "no sé" on the retry does not bring it back. A phrase already waiting is not queued twice.
/// </para>
/// </remarks>
public sealed class RetryQueue
{
    /// <summary>How many phrases may wait for a second try at once.</summary>
    public const int MaxPending = 2;

    private readonly List<RetryItem> _pending = [];
    private readonly HashSet<string> _asked = new(StringComparer.Ordinal);

    /// <summary>The phrases waiting for a second try, in the order they were said "no sé".</summary>
    public IReadOnlyList<RetryItem> Pending => _pending;

    /// <summary>The phrases already asked again in this session (each at most once).</summary>
    public IReadOnlyCollection<string> Asked => _asked;

    /// <summary>
    /// Queues a phrase the learner said "no sé" to, at an NPC. Refused (<see langword="false"/>) when the phrase is already waiting or was asked in this session,
    /// or when <see cref="MaxPending"/> phrases already wait.
    /// </summary>
    /// <param name="phraseId">The phrase sheet's id.</param>
    /// <param name="npc">The NPC the phrase was said to.</param>
    public bool Schedule(string phraseId, string npc)
    {
        ArgumentNullException.ThrowIfNull(phraseId);
        ArgumentNullException.ThrowIfNull(npc);
        if (_asked.Contains(phraseId) || _pending.Any(item => string.Equals(item.PhraseId, phraseId, StringComparison.Ordinal)) || _pending.Count >= MaxPending)
        {
            return false;
        }

        _pending.Add(new RetryItem(phraseId, npc));
        return true;
    }

    /// <summary>
    /// Takes the first phrase waiting for this NPC, for the talk just opened with it. The phrase is marked as asked whether or not the learner answers it, so it is
    /// never asked a second time. Returns <see langword="null"/> when none waits for this NPC.
    /// </summary>
    /// <param name="npc">The NPC the talk is with.</param>
    public RetryItem? TakeFor(string npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        var index = _pending.FindIndex(item => string.Equals(item.Npc, npc, StringComparison.Ordinal));
        if (index < 0)
        {
            return null;
        }

        var item = _pending[index];
        _pending.RemoveAt(index);
        _ = _asked.Add(item.PhraseId);
        return item;
    }
}

/// <summary>A phrase waiting for its second try.</summary>
/// <param name="PhraseId">The phrase sheet's id.</param>
/// <param name="Npc">The NPC the phrase was said to; the second try is asked at the next talk with this NPC.</param>
public sealed record RetryItem(string PhraseId, string Npc);
