using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Npc;

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

    /// <summary>
    /// The class a teacher would offer at a talk opening now (L01), or <see langword="null"/>: none while a lesson is engaged, none at a teacher the learner said no to today.
    /// An order whose lesson this is is not offered on its own (issue #54): the class gives it at its step.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    internal string? ClassOfferedAt(string npc)
    {
        if (Engaged || (_declinedDay == _session.Day && _declined.Contains(npc)))
        {
            return null;
        }

        return ClassAt(npc)?.Id;
    }

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
}
