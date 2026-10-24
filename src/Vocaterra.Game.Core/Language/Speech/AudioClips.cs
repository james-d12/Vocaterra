using System.Buffers.Binary;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Game.Core.Language.Speech;

/// <summary>
/// Plain audio handling for the spoken input, with no device and no engine type: sample-rate conversion to the 16 kHz the recognizer needs,
/// trimming the silence at the edges of a recorded clip, the length cap, and reading a WAV file. The front-end captures samples; everything
/// that turns them into a <see cref="PcmClip"/> lives here so that it is tested without a microphone. Integer and double arithmetic only, no
/// clock and no random numbers: the same samples give the same clip.
/// </summary>
public static class AudioClips
{
    /// <summary>The sample rate of a clip handed to the recognizer, in hertz.</summary>
    public const int SampleRate = PcmClip.DefaultSampleRate;

    /// <summary>The longest clip, in seconds. A longer recording is cut here (the front-end also stops listening at this length).</summary>
    public const int MaxSeconds = 8;

    /// <summary>The shortest clip worth recognizing, in milliseconds after trimming; a shorter one counts as silence (a key tap).</summary>
    public const int MinMilliseconds = 150;

    /// <summary>The audio kept before the first and after the last loud frame when trimming, in milliseconds.</summary>
    public const int PaddingMilliseconds = 120;

    private const int FrameMilliseconds = 10;
    private const int MinimumThreshold = 120;
    private const double RelativeThreshold = 0.1;
    private const int KernelHalfWidth = 12;

    /// <summary>
    /// Converts mono samples to another sample rate with a windowed-sinc low-pass (the cut-off sits under the lower of the two Nyquist
    /// frequencies), so 24 kHz speech and 44.1 kHz microphone audio both become 16 kHz without aliasing.
    /// </summary>
    /// <param name="input">The mono 16-bit samples.</param>
    /// <param name="fromRate">Their sample rate in hertz.</param>
    /// <param name="toRate">The wanted sample rate in hertz.</param>
    public static short[] Resample(ReadOnlySpan<short> input, int fromRate, int toRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fromRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(toRate);
        if (fromRate == toRate)
        {
            return input.ToArray();
        }

        // `step` input samples per output sample; the filter's cut-off, as a fraction of the input Nyquist, is below 1 only when downsampling.
        var step = (double)fromRate / toRate;
        var cutoff = Math.Min(1.0, 1.0 / step) * 0.94;
        var halfWidth = (int)Math.Ceiling(KernelHalfWidth / cutoff);
        var length = (int)(input.Length / step);
        var output = new short[length];
        for (var n = 0; n < length; n++)
        {
            var center = n * step;
            var first = (int)Math.Floor(center) - halfWidth + 1;
            double sum = 0;
            double weights = 0;
            for (var k = first; k <= first + (2 * halfWidth) - 1; k++)
            {
                var x = k - center;
                var weight = cutoff * Sinc(cutoff * x) * (0.5 + (0.5 * Math.Cos(Math.PI * x / halfWidth)));
                weights += weight;
                if ((uint)k < (uint)input.Length)
                {
                    sum += weight * input[k];
                }
            }

            output[n] = (short)Math.Clamp(Math.Round(sum / weights), short.MinValue, short.MaxValue);
        }

        return output;
    }

    /// <summary>
    /// Cuts the silence off both ends of a 16 kHz clip. A frame of 10 ms is loud when its RMS is above a tenth of the loudest frame's (and above
    /// a small floor); the clip keeps <see cref="PaddingMilliseconds"/> around the first and last loud frame. A clip with no loud frame gives an
    /// empty array.
    /// </summary>
    public static short[] TrimSilence(ReadOnlySpan<short> samples)
    {
        var frame = SampleRate * FrameMilliseconds / 1000;
        var frames = samples.Length / frame;
        if (frames == 0)
        {
            return [];
        }

        var rms = new double[frames];
        double loudest = 0;
        for (var f = 0; f < frames; f++)
        {
            double energy = 0;
            foreach (var sample in samples.Slice(f * frame, frame))
            {
                energy += (double)sample * sample;
            }

            rms[f] = Math.Sqrt(energy / frame);
            loudest = Math.Max(loudest, rms[f]);
        }

        var threshold = Math.Max(MinimumThreshold, loudest * RelativeThreshold);
        var firstLoud = Array.FindIndex(rms, r => r > threshold);
        var lastLoud = Array.FindLastIndex(rms, r => r > threshold);
        if (firstLoud < 0)
        {
            return [];
        }

        var pad = SampleRate * PaddingMilliseconds / 1000;
        var start = Math.Max(0, (firstLoud * frame) - pad);
        var end = Math.Min(samples.Length, ((lastLoud + 1) * frame) + pad);
        return samples[start..end].ToArray();
    }

    /// <summary>
    /// Makes the clip the recognizer gets from captured or loaded audio: converts to 16 kHz, trims the silence at the edges and caps the length
    /// at <see cref="MaxSeconds"/>. Returns <see langword="null"/> when nothing but silence (or less than <see cref="MinMilliseconds"/> of
    /// sound) is left.
    /// </summary>
    /// <param name="samples">The mono 16-bit samples.</param>
    /// <param name="sampleRate">Their sample rate in hertz.</param>
    public static PcmClip? Prepare(ReadOnlySpan<short> samples, int sampleRate)
    {
        var at16 = Resample(samples, sampleRate, SampleRate);
        var trimmed = TrimSilence(at16);
        if (trimmed.Length < SampleRate * MinMilliseconds / 1000)
        {
            return null;
        }

        return PcmClip.FromSamples(trimmed.Length > SampleRate * MaxSeconds ? trimmed[..(SampleRate * MaxSeconds)] : trimmed);
    }

    /// <summary>Little-endian 16-bit PCM bytes (what a microphone returns) to samples; a trailing odd byte is ignored.</summary>
    public static short[] FromPcm16(ReadOnlySpan<byte> bytes)
    {
        var samples = new short[bytes.Length / 2];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(i * 2, 2));
        }

        return samples;
    }

    /// <summary>
    /// Reads a RIFF/WAVE file with 16-bit PCM samples; several channels are averaged to mono. The sample rate is returned as it is in the
    /// file (give it to <see cref="Prepare"/>).
    /// </summary>
    /// <exception cref="InvalidDataException">The data is not a 16-bit PCM WAVE file.</exception>
    public static (short[] Samples, int SampleRate) ReadWav(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12 || !data[..4].SequenceEqual("RIFF"u8) || !data.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("Not a RIFF/WAVE file.");
        }

        var position = 12;
        var channels = 0;
        var rate = 0;
        var bits = 0;
        while (position + 8 <= data.Length)
        {
            var id = data.Slice(position, 4);
            var declared = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(position + 4, 4));
            var body = position + 8;
            var size = (int)Math.Min(declared, (uint)(data.Length - body));
            if (id.SequenceEqual("fmt "u8) && size >= 16)
            {
                var format = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(body, 2));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(body + 2, 2));
                rate = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(body + 4, 4));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(body + 14, 2));
                if (format is not (1 or 0xFFFE))
                {
                    throw new InvalidDataException($"The WAV format {format} is not PCM.");
                }
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (channels <= 0 || rate <= 0)
                {
                    throw new InvalidDataException("The WAV data chunk comes before the fmt chunk.");
                }

                if (bits != 16)
                {
                    throw new InvalidDataException($"The WAV has {bits}-bit samples; only 16-bit PCM is read.");
                }

                var frames = size / (2 * channels);
                var mono = new short[frames];
                for (var i = 0; i < frames; i++)
                {
                    var sum = 0;
                    for (var c = 0; c < channels; c++)
                    {
                        sum += BinaryPrimitives.ReadInt16LittleEndian(data.Slice(body + (((i * channels) + c) * 2), 2));
                    }

                    mono[i] = (short)(sum / channels);
                }

                return (mono, rate);
            }

            position = body + size + (size & 1);
        }

        throw new InvalidDataException("The WAV file has no data chunk.");
    }

    private static double Sinc(double x) => Math.Abs(x) < 1e-9 ? 1.0 : Math.Sin(Math.PI * x) / (Math.PI * x);
}
