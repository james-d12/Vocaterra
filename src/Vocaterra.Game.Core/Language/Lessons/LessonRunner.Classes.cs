using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;

namespace Vocaterra.Game.Core.Language.Lessons;

// The lessons as optional classes (L01, D-game-25): the teacher's one-line offer with Sí or No, the answer, and the old saves' split of the first lesson.
public sealed partial class LessonRunner
{
    /// <summary>The lesson that was split in two: the introduction keeps the id, its second half became <see cref="FirstClassId"/>.</summary>
    internal const string IntroductionId = "first-words";

    /// <summary>Luna's first class (units u4 and u4v of the first lesson before L01).</summary>
    internal const string FirstClassId = "first-class";

    private readonly Queue<string> _offerLines = new();
    private readonly HashSet<string> _declined = new(StringComparer.Ordinal);
    private int _declinedDay;
    private string? _offerId;
    private string? _offerNpc;

    /// <summary>The lines the teachers said this frame (the offers), for the panel; the audio goes through the support cues.</summary>
    public IReadOnlyList<string> TakeLines()
    {
        var lines = _offerLines.ToList();
        _offerLines.Clear();
        return lines;
    }

    /// <summary>The id of the lesson a teacher has just offered at this NPC (Sí takes it), or <see langword="null"/> when no offer stands.</summary>
    /// <param name="npc">The NPC's agent id.</param>
    public string? OfferStandingAt(string npc) => _offerId is not null && string.Equals(_offerNpc, npc, StringComparison.Ordinal) ? _offerId : null;

    // The panel opened on a teacher: when a class is open there, one line offers it (once a day, and not after a No). A paused class is offered again.
    private void MakeOffer(string npc)
    {
        _offerId = null;
        _offerNpc = null;
        if (Engaged || !_session.MayOfferClass(npc))
        {
            return;
        }

        if (_declinedDay != _session.Day)
        {
            _declined.Clear();
            _declinedDay = _session.Day;
        }

        if (_declined.Contains(npc) || ClassAt(npc) is not { } lesson)
        {
            return;
        }

        var lineId = $"guide.lesson.{npc}.offer.1";
        var line = _session.Data.Speech.FirstOrDefault(l => string.Equals(l.LineId, lineId, StringComparison.Ordinal));
        if (line is null)
        {
            return;
        }

        _offerId = lesson.Id;
        _offerNpc = npc;
        _offerLines.Enqueue(line.Text);
        _session.Support.Enqueue(new AudioCue(line.Speaker, line.Text, line.LineId, "lesson"));
        _session.NoteNpcLine(npc, line.Text);
        _version++;
    }

    // The class a teacher can give now: the paused one at this NPC, else the first open class of the series that is not done.
    private LessonDefinition? ClassAt(string npc)
    {
        if (_activeId is not null)
        {
            return _book.Find(_activeId) is { AutoStart: false } paused && string.Equals(CurrentNpcOf(paused), npc, StringComparison.Ordinal) ? paused : null;
        }

        return _book.Lessons
            .Where(l => !l.AutoStart && string.Equals(l.Npc, npc, StringComparison.Ordinal) && StatusOf(l.Id) != LessonStatus.Completed && IsUnlocked(l))
            .OrderBy(static l => l.Session)
            .FirstOrDefault();
    }

    // A turn at a teacher: «Sí» takes the class on offer, «No» (or «No, gracias») ends the offer at no cost, «¿Me enseñas?» asks for a class. Any other line withdraws the offer, so a
    // later Sí is the NPC's own question's. Returns whether the line was the class's (the NPC's ordinary reply is not shown, as a lesson's step does).
    internal bool OnClassTurn(GuideTurn turn)
    {
        var standing = OfferStandingAt(turn.Npc);
        _offerId = null;
        _offerNpc = null;
        if (Engaged)
        {
            return false;
        }

        var answer = _session.Data.Words.Classes.Read(turn.Line);
        if (standing is not null && answer == ClassAnswer.Yes)
        {
            return Start(standing);
        }

        if (standing is not null && answer == ClassAnswer.No)
        {
            _ = _declined.Add(turn.Npc);
            _version++;
            return true;
        }

        if (answer == ClassAnswer.Ask && ClassAt(turn.Npc) is { } lesson)
        {
            return Start(lesson.Id);
        }

        return false;
    }

    // A save from before L01 holds the introduction with its second half (units u4 and u4v, steps 16 to 27 of the old lesson) inside. The introduction takes the steps
    // that remain; the class is done when the old lesson was past them, and is not begun otherwise.
    private LessonsSave SplitIntroduction(LessonsSave save)
    {
        if (_book.Find(IntroductionId) is not { } intro || _book.Find(FirstClassId) is not { } firstClass
            || save.Lessons.Any(l => string.Equals(l.Id, FirstClassId, StringComparison.Ordinal))
            || save.Lessons.FirstOrDefault(l => string.Equals(l.Id, IntroductionId, StringComparison.Ordinal)) is not { } old)
        {
            return save;
        }

        const int classFirst = 16;
        const int classCount = 12;
        var classIds = firstClass.Steps.Select(static s => s.Id).ToHashSet(StringComparer.Ordinal);
        var introIds = intro.Steps.Select(static s => s.Id).ToHashSet(StringComparer.Ordinal);
        var passed = old.Status == LessonStatus.Completed || old.EverCompleted == true || old.Step >= classFirst + classCount;
        var step = old.Step < classFirst ? old.Step : old.Step < classFirst + classCount ? classFirst : old.Step - classCount;
        step = Math.Clamp(step, 0, intro.Steps.Count - 1);
        var introduction = old with { Step = step, Outcomes = old.Outcomes.Where(o => introIds.Contains(o.StepId)).ToArray() };
        var firstClassProgress = passed
            ? new LessonProgressSave(FirstClassId, LessonStatus.Completed, firstClass.Steps.Count - 1, old.Outcomes.Where(o => classIds.Contains(o.StepId)).ToArray(), 0, false, CompletedSession: old.CompletedSession, EverCompleted: true)
            : new LessonProgressSave(FirstClassId, LessonStatus.NotStarted, 0, [], 0, false);
        return save with { Lessons = [.. save.Lessons.Select(l => ReferenceEquals(l, old) ? introduction : l), firstClassProgress] };
    }
}
