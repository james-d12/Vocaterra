using System.Collections;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Npc;

namespace Vocaterra.Game.Core.Session;

// A new play session in the same run (V12c, the sleep at the fire). A load builds a new session and puts back what the save keeps (SaveGame.RestoreParts); a sleep
// puts the same save back into the running session (SaveGame.Sleep), so first everything a save does not keep goes back to how a new session starts it: the review
// question, the second tries, the warm-up, the panel's support, the buttons' English counts, the NPCs' last lines. The run's events stay (the log, the sounds and the
// autosave count them by position); the session's own part of them starts again.
public abstract partial class GuideSession
{
    private int _sessionEventsFrom;
    private EventsSince? _sessionEvents;

    /// <summary>
    /// The events of this play session (V12c): <see cref="Events"/> from the start of the run, or from the last sleep at the fire, which begins a new session in the same
    /// run. A loaded game's are its events, since a load starts with none. The session's end line and its summary read these.
    /// </summary>
    public IReadOnlyList<GameEvent> SessionEvents => _sessionEvents ??= new EventsSince(this);

    /// <summary>
    /// Puts what a save does not keep back to how a new session has it (V12c): the next play session is about to be restored into this one from its own save. The talk
    /// must be closed and the session at rest (<see cref="CanSave"/>); the derived class clears its own part too.
    /// </summary>
    internal virtual void ClearUnsaved()
    {
        _sessionEventsFrom = _events.Count;
        _lastLine.Clear();
        _voiceCounts.Clear();
        _spokenTurns = 0;
        _typedTurns = 0;
        EndReview();
        RetryNote = null;
        LastWordTurn = null;
        Retries.Clear();
        Deck.Reset();
        Support.Clear();
        _deckClaim = false;
        _talkOpen = false;
        _keyWordCards = [];
        CurrentNpc = NpcService.GuideAgent;
    }

    // The session's events: a view of the run's list from the session's first event, so nothing is copied.
    private sealed class EventsSince(GuideSession session) : IReadOnlyList<GameEvent>
    {
        public int Count => session._events.Count - session._sessionEventsFrom;

        public GameEvent this[int index] => index >= 0 && index < Count ? session._events[session._sessionEventsFrom + index] : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<GameEvent> GetEnumerator()
        {
            for (var i = session._sessionEventsFrom; i < session._events.Count; i++)
            {
                yield return session._events[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
