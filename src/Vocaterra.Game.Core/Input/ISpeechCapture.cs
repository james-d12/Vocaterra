using Vocaterra.Game.Core.Language.Speech;

namespace Vocaterra.Game.Core.Input;

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
