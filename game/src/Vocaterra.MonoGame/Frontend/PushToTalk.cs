using Vocaterra.Game;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// A microphone as the push-to-talk sees it: start recording, read what has been recorded since the last read, stop. The engine's
/// implementation (<c>MicrophoneCapture</c>) is the only code that names a device API; the rest, and the tests, use this interface.
/// </summary>
public interface ISpeechCapture
{
    /// <summary>The sample rate of what <see cref="ReadAvailable"/> returns, in hertz (the device's own; <see cref="AudioClips.Prepare"/> converts).</summary>
    int SampleRate { get; }

    /// <summary>Starts recording. Throws <see cref="InvalidOperationException"/> when the device cannot be opened.</summary>
    void Start();

    /// <summary>The mono 16-bit samples recorded since the last call (possibly none).</summary>
    short[] ReadAvailable();

    /// <summary>Stops recording.</summary>
    void Stop();
}

/// <summary>
/// The push-to-talk state: <see cref="Begin"/> when the key goes down starts the recording, <see cref="Pump"/> once per frame collects the
/// samples (and stops the recording at <see cref="AudioClips.MaxSeconds"/>), <see cref="End"/> when the key goes up returns the clip the
/// recognizer gets (converted to 16 kHz, silence trimmed at both ends, at most 8 seconds), or <see langword="null"/> for silence. With no
/// microphone, <see cref="Problem"/> says so in Spanish and typing is unaffected.
/// </summary>
public sealed class PushToTalk
{
    /// <summary>The message when the machine has no microphone.</summary>
    public const string NoMicrophone = "No microphone: type your answer.";

    /// <summary>The message when the recording held nothing but silence.</summary>
    public const string Silence = "I didn't hear anything.";

    private readonly ISpeechCapture? _capture;
    private readonly List<short> _samples = [];
    private int _limit;
    private bool _stopped;
    private int _discard;

    /// <summary>Creates the state over a capture device, or <see langword="null"/> when the machine has none.</summary>
    public PushToTalk(ISpeechCapture? capture)
    {
        _capture = capture;
    }

    /// <summary>
    /// The sound side (S24): told before the recording starts (the start beep plays, and the recording discards the beep's length from its start), when it has started and
    /// when it has ended. <see langword="null"/> in the tests that do not care.
    /// </summary>
    public ITalkSounds? Sounds { get; set; }

    /// <summary>Whether a microphone is there.</summary>
    public bool Available => _capture is not null;

    /// <summary>Whether the key is held and the recording is on.</summary>
    public bool Listening { get; private set; }

    /// <summary>How many seconds have been recorded since <see cref="Begin"/> (at the device's own rate).</summary>
    public double SecondsRecorded => _capture is null || _capture.SampleRate <= 0 ? 0 : (double)_samples.Count / _capture.SampleRate;

    /// <summary>The level of the samples the last <see cref="Pump"/> collected, 0 to 1 (the loudest 50 ms RMS window): what the mic check's meter shows.</summary>
    public double LastLevel { get; private set; }

    /// <summary>Whether the recording was stopped by the length cap while the key was still held: the game then calls <see cref="End"/> at once.</summary>
    public bool ReachedLimit { get; private set; }

    /// <summary>The Spanish message about the last attempt (no microphone, the device failed, silence), or <see langword="null"/>.</summary>
    public string? Problem { get; private set; }

    /// <summary>The key went down: starts recording. Does nothing when already listening.</summary>
    /// <returns>Whether the recording started.</returns>
    public bool Begin()
    {
        if (Listening)
        {
            return true;
        }

        Problem = null;
        ReachedLimit = false;
        LastLevel = 0;
        if (_capture is null)
        {
            Problem = NoMicrophone;
            return false;
        }

        var leadIn = Sounds?.Starting() ?? 0;
        try
        {
            _samples.Clear();
            _capture.Start();
        }
        catch (InvalidOperationException ex)
        {
            Problem = $"Could not open the microphone: {ex.Message}. Type your answer.";
            return false;
        }

        _limit = _capture.SampleRate * AudioClips.MaxSeconds;
        _discard = (int)Math.Round(leadIn * _capture.SampleRate);
        Listening = true;
        Sounds?.Started();
        return true;
    }

    /// <summary>Once per frame while listening: collects the recorded samples and stops recording at the length cap.</summary>
    public void Pump()
    {
        if (!Listening || _capture is null)
        {
            return;
        }

        var read = Collect(_capture.ReadAvailable());
        LastLevel = read.Length == 0 ? 0 : MicCheck.Level(read, _capture.SampleRate);
        if (_samples.Count >= _limit)
        {
            _capture.Stop();
            Listening = false;
            ReachedLimit = true;
            _stopped = true;
            Sounds?.Stopped();
        }
    }

    /// <summary>The key went up (or the cap was reached): stops recording and returns the clip, or <see langword="null"/> for silence.</summary>
    public PcmClip? End()
    {
        if (!Listening && !_stopped)
        {
            return null;
        }

        var wasListening = Listening;
        if (Listening && _capture is not null)
        {
            _ = Collect(_capture.ReadAvailable());
            _capture.Stop();
        }

        Listening = false;
        _stopped = false;
        if (wasListening)
        {
            Sounds?.Stopped();
        }

        var clip = _capture is null ? null : AudioClips.Prepare(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_samples), _capture.SampleRate);
        _samples.Clear();
        if (clip is null)
        {
            Problem = Silence;
        }

        return clip;
    }

    // Keeps the samples of the recording, minus the start beep's length at the very start (the beep plays through the speakers just before the recording opens).
    private short[] Collect(short[] read)
    {
        if (_discard > 0)
        {
            var drop = Math.Min(_discard, read.Length);
            _discard -= drop;
            read = read[drop..];
        }

        _samples.AddRange(read);
        return read;
    }
}
