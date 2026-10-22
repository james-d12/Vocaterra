using System.Buffers.Binary;
using Microsoft.Xna.Framework.Audio;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.MonoGame;

/// <summary>
/// The NPC voices' output (S9): one <see cref="DynamicSoundEffectInstance"/> per clip, mono 16-bit at the clip's own rate. With no audio device
/// the first start fails and the output stays unavailable for the run; nothing else in the game depends on it.
/// </summary>
public sealed class DynamicSoundOutput : IAudioOutput
{
    private DynamicSoundEffectInstance? _instance;
    private bool _unavailable;

    /// <inheritdoc />
    public bool Available => !_unavailable;

    /// <inheritdoc />
    public bool IsPlaying => _instance?.State == SoundState.Playing;

    /// <inheritdoc />
    public bool Start(AudioBuffer clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (_unavailable)
        {
            return false;
        }

        Stop();
        try
        {
            var instance = new DynamicSoundEffectInstance(clip.SampleRate, AudioChannels.Mono);
            instance.SubmitBuffer(Pcm16Bytes(clip.Samples));
            instance.Play();
            _instance = instance;
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The platform's audio stack throws several types when there is no device or its library is missing (NoAudioHardwareException,
            // DllNotFoundException, ...). None of them is a reason to stop the game: the voice is off for the run and the text stays.
            _unavailable = true;
            return false;
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (_instance is { } instance)
        {
            _instance = null;
            instance.Stop();
            instance.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose() => Stop();

    private static byte[] Pcm16Bytes(short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2 * i), samples[i]);
        }

        return bytes;
    }
}
