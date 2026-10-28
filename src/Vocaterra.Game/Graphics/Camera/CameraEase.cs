namespace Vocaterra.Game.Graphics.Camera;

/// <summary>
/// Eases the camera over a step-up. When the body climbs a block in one tick the eye jumps a block; the camera holds the old height and lets
/// it go over a few ticks. Plain numbers, one call per simulation tick.
/// </summary>
public sealed class CameraEase
{
    /// <summary>The ticks a one-block step takes to ease out.</summary>
    public const int TicksPerBlock = 8;

    /// <summary>The most the camera lags behind the eye, in blocks.</summary>
    public const double MaximumLag = 1.5;

    /// <summary>How far the camera sits below the eye, in blocks (zero or less).</summary>
    public double Offset { get; private set; }

    /// <summary>Call once per tick: <paramref name="steppedUp"/> is the body's flag for the tick just run, <paramref name="blocksClimbed"/> how far it climbed.</summary>
    public void Tick(bool steppedUp, double blocksClimbed = 1.0)
    {
        if (steppedUp)
        {
            Offset = Math.Max(-MaximumLag, Offset - blocksClimbed);
        }

        Offset = Math.Min(0.0, Offset + (1.0 / TicksPerBlock));
    }

    /// <summary>Forgets any lag (after a teleport).</summary>
    public void Reset() => Offset = 0.0;
}
