namespace Vocaterra.Game.Core.Input;

/// <summary>
/// Key auto-repeat for an editing key, counted in frames so it needs no clock: the first press acts at once, then nothing for
/// <see cref="DelayFrames"/> frames, then once every <see cref="IntervalFrames"/> frames while the key stays down.
/// </summary>
public sealed class KeyRepeater
{
    /// <summary>Frames a held key waits before it repeats.</summary>
    public const int DelayFrames = 24;

    /// <summary>Frames between repeats.</summary>
    public const int IntervalFrames = 3;

    private int _held = -1;

    /// <summary>Call once per frame with whether the key is down; returns whether the key acts this frame.</summary>
    public bool Step(bool down)
    {
        if (!down)
        {
            _held = -1;
            return false;
        }

        _held++;
        return _held == 0 || (_held >= DelayFrames && (_held - DelayFrames) % IntervalFrames == 0);
    }
}
