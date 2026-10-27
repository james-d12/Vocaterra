using Vocaterra.Game.Core.Event;

namespace Vocaterra.Game.Core.Event;

/// <summary>The game events since the last frame, one console line each (the phrase uses, the voice events, the world events) and the support lines an event adds.</summary>
public sealed class EventPrinter
{
    private readonly IReadOnlyList<GameEvent> _events;
    private readonly Func<GameEvent, IReadOnlyList<string>> _supportLines;
    private readonly Action<string> _log;
    private int _printed;

    /// <summary>Creates the printer of a session's events.</summary>
    /// <param name="events">The session's events, which only grow.</param>
    /// <param name="supportLines">The lines the phrase support adds for an event.</param>
    /// <param name="log">Where the lines go (the console).</param>
    public EventPrinter(IReadOnlyList<GameEvent> events, Func<GameEvent, IReadOnlyList<string>> supportLines, Action<string> log)
    {
        _events = events;
        _supportLines = supportLines;
        _log = log;
    }

    /// <summary>Prints the events that are new since the last call.</summary>
    public void Print()
    {
        for (; _printed < _events.Count; _printed++)
        {
            _log($"  event: {_events[_printed].Text}");
            foreach (var line in _supportLines(_events[_printed]))
            {
                _log(line);
            }
        }
    }
}
