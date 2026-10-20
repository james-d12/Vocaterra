using Slg.Core.Audio;
using Slg.Core.Determinism;
using Slg.Core.Stages;

namespace Slg.Response.Speech;

/// <summary>
/// Test double for text-to-speech: always returns 100 ms of 16 kHz audio that is silent except for its first 16 samples,
/// which hold the 32 bytes of <see cref="DeterministicHash.OfText"/> of the text (little-endian), so equal text gives equal audio.
/// </summary>
public sealed class SilentSynthesizer : ISpeechSynthesizer
{
    /// <summary>Sample rate of every buffer produced.</summary>
    public const int SampleRate = 16_000;

    /// <summary>Number of samples in every buffer (100 ms).</summary>
    public const int SampleCount = 1_600;

    private const int HashSamples = 16;

    private int _calls;

    /// <summary>A fixed identifier for this fake model.</summary>
    public string ModelHash => "silent-synthesizer-v1";

    /// <summary>Number of times <see cref="Synthesize"/> was called.</summary>
    public int CallCount => Volatile.Read(ref _calls);

    /// <summary>Recovers the lowercase hex hash encoded in the first samples of a buffer produced by this class.</summary>
    public static string ReadTextHash(AudioBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (buffer.Samples.Length < HashSamples)
        {
            throw new ArgumentException("Buffer is too short to hold a text hash.", nameof(buffer));
        }

        var bytes = new byte[HashSamples * sizeof(short)];
        for (var i = 0; i < HashSamples; i++)
        {
            var sample = (ushort)buffer.Samples[i];
            bytes[2 * i] = (byte)(sample & 0xFF);
            bytes[(2 * i) + 1] = (byte)(sample >> 8);
        }

        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>Returns the deterministic 100 ms buffer for the request text.</summary>
    public AudioBuffer Synthesize(SynthesisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Interlocked.Increment(ref _calls);
        var digest = Convert.FromHexString(DeterministicHash.OfText(request.Text));
        var samples = new short[SampleCount];
        for (var i = 0; i < HashSamples; i++)
        {
            samples[i] = (short)(ushort)(digest[2 * i] | (digest[(2 * i) + 1] << 8));
        }

        return new AudioBuffer(samples, SampleRate);
    }
}
