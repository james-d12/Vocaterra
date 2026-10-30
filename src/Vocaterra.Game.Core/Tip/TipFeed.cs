using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Tip;

/// <summary>
/// The first-time tips of the game (S16, D-game-13): support-language text (<c>game/tips.json</c> and the locale's <c>tip.&lt;id&gt;</c>), shown once each when something happens for the first time
/// (the start, the first pickup, the first time the crosshair is on someone, the first talk...). A tip is offered by its trigger, shown one at a time for
/// <see cref="ShowTicks"/> ticks of the game (or until it is dismissed), and never again; which tips were shown is saved with the game. Plain state: it counts
/// the game's ticks, so a scripted run shows the same tips at the same moments on every machine.
/// </summary>
public sealed class TipFeed
{
    /// <summary>How long a tip stays on screen: seven seconds of game time.</summary>
    public const long ShowTicks = 7 * 60;

    private readonly GuideSession _session;
    private readonly IReadOnlyList<TipSpec> _tips;
    private readonly HashSet<string> _shown = new(StringComparer.Ordinal);
    private const long VoxelTicksPerSecond = 60;

    private readonly List<TipSpec> _queue = [];
    private readonly Dictionary<string, long> _readyAt = new(StringComparer.Ordinal);
    private TipSpec? _current;
    private long _since;

    internal TipFeed(GuideSession session, IReadOnlyList<TipSpec> tips)
    {
        _session = session;
        _tips = tips;
    }

    /// <summary>The tip on screen now, or <see langword="null"/>.</summary>
    public TipSpec? Current => _current;

    /// <summary>The ids of the tips shown so far, in ordinal order.</summary>
    public IReadOnlyList<string> Shown => _shown.Order(StringComparer.Ordinal).ToList();

    /// <summary>Whether a tip was shown.</summary>
    /// <param name="id">The tip's id.</param>
    public bool WasShown(string id) => _shown.Contains(id);

    /// <summary>Whether the game has a tip with this id (a save across bundles keeps only the tips that still exist, X01).</summary>
    /// <param name="id">The tip's id.</param>
    internal bool Knows(string id) => _tips.Any(t => string.Equals(t.Id, id, StringComparison.Ordinal));

    /// <summary>Queues the tips of a moment that is no world event (<c>start</c>, <c>aimed_npc</c>) that were not shown yet, in the file's order. A tip is queued once.</summary>
    /// <param name="moment">The moment: <c>start</c> or <c>aimed_npc</c>.</param>
    public void Offer(string moment)
    {
        ArgumentNullException.ThrowIfNull(moment);
        Offer(tip => tip.Trigger.Matches(moment));
    }

    /// <summary>Queues the tips whose trigger the world event matches (C06: the event, and its NPC and detail when the trigger names them).</summary>
    /// <param name="world">The world event.</param>
    public void Offer(WorldEvent world)
    {
        ArgumentNullException.ThrowIfNull(world);
        Offer(tip => tip.Trigger.Matches(world));
    }

    private void Offer(Func<TipSpec, bool> matches)
    {
        foreach (var tip in _tips)
        {
            if (matches(tip) && !_shown.Contains(tip.Id) && !_queue.Contains(tip) && !Equals(_current, tip))
            {
                _queue.Add(tip);
                _readyAt[tip.Id] = _session.CurrentTick + (tip.DelaySeconds * VoxelTicksPerSecond); // a delayed tip waits (S17: the opening tips come one at a time)
            }
        }
    }

    /// <summary>Moves the feed on to a tick: an old tip goes, and the next one waiting (whose delay is over) comes up and is logged as shown.</summary>
    /// <param name="tick">The simulation tick.</param>
    public void Step(long tick)
    {
        if (_current is not null && tick - _since >= ShowTicks)
        {
            _current = null;
        }

        var next = _current is null ? _queue.FindIndex(tip => !_readyAt.TryGetValue(tip.Id, out var ready) || ready <= tick) : -1;
        if (next >= 0)
        {
            _current = _queue[next];
            _queue.RemoveAt(next);
            _since = tick;
            _ = _shown.Add(_current.Id);
            _session.LogEvent(new WorldEvent(_session.CurrentTick, _session.StoredTime.UnixSeconds, WorldEvents.TipShown, null, _current.Id));
        }
    }

    /// <summary>Takes the tip on screen down at once (the next one waiting comes up on the next step).</summary>
    public void Dismiss() => _current = null;

    /// <summary>What a save keeps: the tips shown, the ones waiting in order, and the one on screen.</summary>
    internal TipsSave Capture() =>
        new(Shown, _queue.Select(static t => t.Id).ToArray(), _current?.Id, _current is null ? 0 : _since, _queue.Select(t => _readyAt.GetValueOrDefault(t.Id)).ToArray());

    /// <summary>Puts back the tips a save kept. An id the game's English text does not have is refused.</summary>
    /// <param name="save">The saved tips.</param>
    /// <exception cref="ArgumentException">An id is not a tip of the file.</exception>
    internal void Restore(TipsSave save)
    {
        TipSpec Spec(string id) => _tips.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal))
            ?? throw new ArgumentException($"The tip '{id}' is not one of the game's.", nameof(save));
        _shown.Clear();
        foreach (var id in save.Shown)
        {
            _ = Spec(id);
            _ = _shown.Add(id);
        }

        _queue.Clear();
        _readyAt.Clear(); // a save from before the delays (no Ready) has its waiting tips ready at once
        _queue.AddRange(save.Queued.Select(Spec));
        if (save.Ready is { } ready && ready.Count == save.Queued.Count)
        {
            for (var i = 0; i < ready.Count; i++)
            {
                _readyAt[save.Queued[i]] = ready[i];
            }
        }
        _current = save.Current is { } current ? Spec(current) : null;
        _since = save.Since;
    }
}
