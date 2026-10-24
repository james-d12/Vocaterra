namespace Vocaterra.MonoGame.Utilities;

/// <summary>
/// The front-end's watchdog on a turn (S7, D-game-9: "a timeout"): when a turn has been "Pensando…" for <see cref="LimitSeconds"/> it fires once, and
/// the panel then offers typing ("¿Escribes?", the Escribir button focused) while the turn keeps running. It is fed the seconds of the frame clock
/// by the front-end, the only place with a clock; nothing in the game layer reads one, and a scripted run feeds it nothing.
/// </summary>
public sealed class TurnWatchdog
{
    /// <summary>How long a turn may think before typing is offered, in seconds.</summary>
    public const double LimitSeconds = 5.0;

    /// <summary>How many seconds the current wait has lasted.</summary>
    public double Waited { get; private set; }

    /// <summary>Whether the watchdog has fired for the current wait (it fires once per wait).</summary>
    public bool Fired { get; private set; }

    /// <summary>
    /// Advances the watchdog by one frame. While a turn is thinking the wait grows; when nothing is thinking the wait and the fired mark reset.
    /// </summary>
    /// <param name="seconds">The seconds of this frame (the frame clock's).</param>
    /// <param name="thinking">Whether a turn is on the worker (and not cancelled).</param>
    /// <returns><see langword="true"/> on the one frame the limit is crossed.</returns>
    public bool Update(double seconds, bool thinking)
    {
        if (!thinking)
        {
            Reset();
            return false;
        }

        Waited += Math.Max(0, seconds);
        if (!Fired && Waited >= LimitSeconds)
        {
            Fired = true;
            return true;
        }

        return false;
    }

    /// <summary>Starts a new wait (the turn finished, was cancelled, or the player acted).</summary>
    public void Reset()
    {
        Waited = 0;
        Fired = false;
    }
}
