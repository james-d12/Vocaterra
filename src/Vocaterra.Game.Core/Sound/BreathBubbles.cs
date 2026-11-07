using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Sound;

/// <summary>
/// The bubbles under water (H10): one sound for each bubble the breath meter loses while the eye is under, so the sound follows the breath. Ten bubbles
/// go in a full breath (about one every 1.5 seconds), and none goes above the surface, at full air, or while the air refills. Pure: it reads the breath
/// each tick and keeps the count it last saw.
/// </summary>
public sealed class BreathBubbles
{
    private int? _lastBubbles;

    /// <summary>Reads the breath this tick; true when a bubble is lost under water (the first reading only sets the count).</summary>
    /// <param name="breath">The breath.</param>
    /// <param name="eyeUnder">Whether the eye is under the surface this tick.</param>
    public bool Observe(Breath breath, bool eyeUnder)
    {
        ArgumentNullException.ThrowIfNull(breath);
        var now = breath.Bubbles;
        var popped = _lastBubbles is { } last && now < last && eyeUnder && !breath.IsFull;
        _lastBubbles = now;
        return popped;
    }
}
