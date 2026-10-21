namespace Vocaterra.MonoGame.Frontend;

/// <summary>One line shown across the top of the screen for a while, for example <c>¡Nueva palabra: el árbol!</c>.</summary>
/// <param name="Text">The line.</param>
/// <param name="StartTick">The simulation tick it appeared at.</param>
public sealed record Toast(string Text, long StartTick);

/// <summary>
/// The toasts waiting to be shown, in order. A toast lasts <see cref="Duration"/> ticks and the next one starts when the current one ends.
/// Time is the simulation's tick, never the clock, so a scripted run shows the same toasts on every machine.
/// </summary>
public sealed class ToastQueue
{
    /// <summary>How long one toast stays on screen, in ticks (four seconds at 60 ticks a second).</summary>
    public const int Duration = 240;

    private readonly Queue<string> _waiting = new();
    private Toast? _current;

    /// <summary>Queues a line to show.</summary>
    /// <param name="text">The line.</param>
    public void Show(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _waiting.Enqueue(text);
    }

    /// <summary>
    /// The toast on screen at <paramref name="tick"/>, or <see langword="null"/>. Starts the next waiting line when the current one has run its
    /// course. Call it once per frame with the simulation tick.
    /// </summary>
    public Toast? Current(long tick)
    {
        if (_current is not null && tick - _current.StartTick >= Duration)
        {
            _current = null;
        }

        if (_current is null && _waiting.Count > 0)
        {
            _current = new Toast(_waiting.Dequeue(), tick);
        }

        return _current;
    }

    /// <summary>How many lines are still waiting behind the one on screen.</summary>
    public int Waiting => _waiting.Count;
}
