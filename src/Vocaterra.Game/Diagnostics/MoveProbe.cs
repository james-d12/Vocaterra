using System.Globalization;

namespace Vocaterra.Game.Diagnostics;

/// <summary>
/// Times a start or a move of the body across the streamed world, for <c>--stream-stats</c> (B07b): from the frame the eye is somewhere new until the near zone is resident and until the near view is
/// meshed, and the longest frame in between. A move is an eye farther than <see cref="JumpBlocks"/> from where the last frame had it.
/// </summary>
public sealed class MoveProbe
{
    /// <summary>The distance in blocks that makes a move of the eye one the probe times.</summary>
    public const float JumpBlocks = 64;

    private bool _seen;
    private float _lastX;
    private float _lastZ;
    private double _lastFrameAt;
    private bool _open;
    private string _label = string.Empty;
    private double _startedAt;
    private double _readyAt = double.NaN;
    private double _longest;
    private bool _viewOpen;

    /// <summary>Whether the last <see cref="Observe"/> returned the line that says the whole view is made (B43: the stage counters' line follows it).</summary>
    public bool ViewCompleted { get; private set; }

    /// <summary>Notes one frame; returns the line to print when the move being timed is complete, otherwise <see langword="null"/>.</summary>
    /// <param name="now">The clock in milliseconds at the end of the frame's world drawing.</param>
    /// <param name="frameStart">The clock at the start of the frame's world drawing (the time the move waited for is counted from the frame before).</param>
    /// <param name="eyeX">The body's eye x.</param>
    /// <param name="eyeZ">The body's eye z.</param>
    /// <param name="nearReady">Whether the near zone is resident.</param>
    /// <param name="nearMeshed">Whether the near view is meshed.</param>
    /// <param name="viewComplete">Whether the whole view is made (B21): everything the residency wants is resident and every chunk in view has its mesh; a second line says when.</param>
    public string? Observe(double now, double frameStart, float eyeX, float eyeZ, bool nearReady, bool nearMeshed, bool viewComplete = false)
    {
        ViewCompleted = false;
        var jumped = !_seen || MathF.Sqrt(((eyeX - _lastX) * (eyeX - _lastX)) + ((eyeZ - _lastZ) * (eyeZ - _lastZ))) > JumpBlocks;
        var gap = _seen ? now - _lastFrameAt : 0;
        if (jumped)
        {
            _label = _seen ? string.Create(CultureInfo.InvariantCulture, $"jump to {eyeX:0},{eyeZ:0}") : "start";
            _open = true;
            _startedAt = _seen ? _lastFrameAt : frameStart;
            _readyAt = double.NaN;
            _longest = 0;
            _viewOpen = false;
        }

        _seen = true;
        (_lastX, _lastZ) = (eyeX, eyeZ);
        _lastFrameAt = now;
        if (!_open)
        {
            if (_viewOpen && viewComplete)
            {
                _viewOpen = false;
                ViewCompleted = true;
                return string.Create(CultureInfo.InvariantCulture, $"move: {_label}: whole view resident and meshed after {now - _startedAt:0} ms");
            }

            return null;
        }

        _longest = Math.Max(_longest, gap);
        if (double.IsNaN(_readyAt) && nearReady)
        {
            _readyAt = now - _startedAt;
        }

        if (!nearReady || !nearMeshed)
        {
            return null;
        }

        _open = false;
        _viewOpen = true;
        return string.Create(CultureInfo.InvariantCulture, $"move: {_label}: near zone resident after {_readyAt:0} ms, near view complete after {now - _startedAt:0} ms, longest frame {_longest:0} ms");
    }
}
