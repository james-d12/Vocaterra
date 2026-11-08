using System.Buffers.Binary;
using Microsoft.Xna.Framework.Audio;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Game.Sound;

/// <summary>
/// The NPC voices' output (S9): one <see cref="DynamicSoundEffectInstance"/> per clip, mono 16-bit at the clip's own rate. With no audio device
/// the first start fails and the output stays unavailable for the run; nothing else in the game depends on it.
/// </summary>
public sealed class DynamicSoundOutput : IAudioOutput
{
    private DynamicSoundEffectInstance? _instance;
    private bool _unavailable;
    private float _volume = 1f;

    /// <inheritdoc />
    public bool Available => !_unavailable;

    /// <inheritdoc />
    public bool IsPlaying => _instance is { } instance && ClipPlaying(instance.State, instance.PendingBufferCount);

    /// <summary>
    /// Whether a clip is still sounding (#24, #36): a <see cref="DynamicSoundEffectInstance"/> stays <see cref="SoundState.Playing"/> after its one buffer has played out
    /// (it waits, silent, for more), so the end of the clip is the end of its pending buffers, which the frame's <c>FrameworkDispatcher.Update</c> counts down. Reading the
    /// state alone kept the voice "busy" for ever: a queued line never started, and the next utterance dropped it.
    /// </summary>
    /// <param name="state">The instance's state.</param>
    /// <param name="pendingBuffers">The buffers it has not finished playing.</param>
    public static bool ClipPlaying(SoundState state, int pendingBuffers) => state == SoundState.Playing && pendingBuffers > 0;

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
            instance.Volume = _volume;
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
    public void SetVolume(double volume)
    {
        _volume = (float)Math.Clamp(volume, 0, 1);
        if (_instance is { } instance)
        {
            instance.Volume = _volume;
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
