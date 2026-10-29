namespace Vocaterra.Game.Core.Input;

/// <summary>
/// Push-to-talk over the microphone (S7): one recording at a time, for the dialogue, the mic check or the check battery. A recording starts on a
/// key, collects samples once a frame, and ends when the key goes up or the recording hits its length cap; the clip then goes to its
/// <see cref="ListenTarget"/>. A cancelled recording is dropped and goes nowhere.
/// </summary>
public sealed class ListenSession
{
    private readonly PushToTalkSpeechCapture _capture;
    private ListenTarget? _target;
    private GameKey _key = GameKey.None;

    /// <summary>Creates push-to-talk over a capture.</summary>
    public ListenSession(PushToTalkSpeechCapture capture) => _capture = capture;

    /// <summary>Whether a recording is running.</summary>
    public bool Listening => _capture.Listening;

    /// <summary>Why the last recording could not start or gave no clip, in words for the screen, or <see langword="null"/>.</summary>
    public string? Problem => _capture.Problem;

    /// <summary>Starts a recording for <paramref name="target"/> while <paramref name="key"/> is held. Returns whether it started (else see <see cref="Problem"/>).</summary>
    public bool Begin(ListenTarget target, GameKey key)
    {
        if (!_capture.Begin())
        {
            return false;
        }

        _target = target;
        _key = key;
        return true;
    }

    /// <summary>Once a frame, from the keys: the recording ends when its key is no longer held.</summary>
    public void ReleaseIfUp(IInputFrame input)
    {
        if (_capture.Listening && !input.Down(_key))
        {
            Finish();
        }
    }

    /// <summary>Once a frame: collects the samples, hands the level to the target, and ends the recording when it hits its length cap.</summary>
    public void Pump(double seconds)
    {
        if (_target is not { } target)
        {
            return;
        }

        _capture.Pump();
        target.Level?.Invoke(_capture.LastLevel, seconds);
        if (_capture.ReachedLimit && !_capture.Listening)
        {
            Finish();
        }
    }

    /// <summary>Drops the recording, if one is running: nothing is sent and the target hears nothing.</summary>
    public void Cancel()
    {
        if (_capture.Listening)
        {
            _ = _capture.End();
        }

        _target = null;
    }

    private void Finish()
    {
        var target = _target;
        _target = null;
        var clip = _capture.End();
        target?.Heard(clip);
    }
}
