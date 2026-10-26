using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Input;

namespace Vocaterra.Game.Sound;

/// <summary>
/// The default microphone through MonoGame's <see cref="Microphone"/> (OpenAL capture on DesktopGL). MonoGame fixes the capture at the
/// device's rate (44.1 kHz mono, 16-bit on the development machine) and gives no way to ask for 16 kHz, so the samples are converted by
/// <see cref="AudioClips.Prepare"/> when the key goes up. The only class that names a capture API.
/// </summary>
public sealed class MicrophoneCapture : ISpeechCapture
{
    private const int BufferMilliseconds = 100;

    private readonly Microphone _microphone;
    private readonly byte[] _buffer;

    private MicrophoneCapture(Microphone microphone)
    {
        _microphone = microphone;
        _microphone.BufferDuration = TimeSpan.FromMilliseconds(BufferMilliseconds);

        // MonoGame (DesktopGL, 3.8.6) only moves captured audio into the buffer that GetData reads while something listens to BufferReady:
        // without a subscriber GetData returns nothing for ever. The handler does nothing; the samples are read with GetData each frame.
        _microphone.BufferReady += static (_, _) => { };
        _buffer = new byte[_microphone.GetSampleSizeInBytes(_microphone.BufferDuration)];
    }

    /// <summary>The device's name, as the audio system gives it.</summary>
    public string Name => _microphone.Name;

    /// <inheritdoc />
    public int SampleRate => _microphone.SampleRate;

    /// <summary>The default microphone, or <see langword="null"/> when the machine has none (or the audio system cannot list one).</summary>
    public static MicrophoneCapture? TryOpen()
    {
        try
        {
            var microphone = Microphone.Default;
            return microphone is null ? null : new MicrophoneCapture(microphone);
        }
        catch (Exception ex) when (ex is NoMicrophoneConnectedException or InvalidOperationException or DllNotFoundException or NotSupportedException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Start()
    {
        try
        {
            _microphone.Start();
        }
        catch (Exception ex) when (ex is NoMicrophoneConnectedException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public short[] ReadAvailable()
    {
        FrameworkDispatcher.Update(); // MonoGame fills the capture buffers from its dispatcher
        var all = new List<short>();
        for (var i = 0; i < 64; i++)
        {
            var read = _microphone.GetData(_buffer);
            if (read <= 0)
            {
                break;
            }

            all.AddRange(AudioClips.FromPcm16(_buffer.AsSpan(0, read)));
        }

        return [.. all];
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (_microphone.State == MicrophoneState.Started)
        {
            _microphone.Stop();
        }
    }
}
