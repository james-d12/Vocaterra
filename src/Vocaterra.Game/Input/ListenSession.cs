using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Input;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Game.Input;

/// <summary>Who a recording is for: what to do with the clip when it ends, and with the level while it runs (the mic check's meter).</summary>
/// <param name="Heard">Takes the clip when the recording ends (the key went up or the recording hit its cap), or <see langword="null"/> when nothing usable was heard.</param>
/// <param name="Level">Takes the level of what was collected and the frame's seconds, once a frame while the recording runs; <see langword="null"/> when nobody shows it.</param>
public sealed record ListenTarget(Action<PcmClip?> Heard, Action<double, double>? Level = null);

/// <summary>
/// Push-to-talk over the microphone (S7): one recording at a time, for the dialogue, the mic check or the check battery. A recording starts on a
/// key, collects samples once a frame, and ends when the key goes up or the recording hits its length cap; the clip then goes to its
/// <see cref="ListenTarget"/>. A cancelled recording is dropped and goes nowhere.
/// </summary>
public sealed class ListenSession
{
    private readonly PushToTalkSpeechCapture _capture;
    private ListenTarget? _target;
    private Keys _key = Keys.None;

    /// <summary>Creates push-to-talk over a capture.</summary>
    public ListenSession(PushToTalkSpeechCapture capture) => _capture = capture;

    /// <summary>Whether a recording is running.</summary>
    public bool Listening => _capture.Listening;

    /// <summary>Why the last recording could not start or gave no clip, in words for the screen, or <see langword="null"/>.</summary>
    public string? Problem => _capture.Problem;

    /// <summary>Starts a recording for <paramref name="target"/> while <paramref name="key"/> is held. Returns whether it started (else see <see cref="Problem"/>).</summary>
    public bool Begin(ListenTarget target, Keys key)
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
    public void ReleaseIfUp(InputSnapshot input)
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
