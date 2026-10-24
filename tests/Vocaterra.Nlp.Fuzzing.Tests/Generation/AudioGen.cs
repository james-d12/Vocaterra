using System.Buffers.Binary;
using System.Globalization;
using CsCheck;
using Vocaterra.Nlp.Audio;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>What a generated clip contains (the signal families of plan section 4).</summary>
public enum SignalKind
{
    /// <summary>All zeros.</summary>
    Silence,

    /// <summary>One sine wave.</summary>
    Sine,

    /// <summary>Two sine waves, the second at one and a half times the frequency.</summary>
    TwoTone,

    /// <summary>White noise: every sample drawn from the CsCheck generator.</summary>
    WhiteNoise,

    /// <summary>A square wave driven into clipping.</summary>
    ClippedSquare,

    /// <summary>A constant level.</summary>
    DcOffset,

    /// <summary>One full-scale sample in the middle of silence.</summary>
    Impulse,
}

/// <summary>How the samples are stored in the generated WAV file.</summary>
public enum WavEncoding
{
    /// <summary>Integer PCM, 16 bits.</summary>
    Pcm16,

    /// <summary>IEEE float, 32 bits.</summary>
    Float32,
}

/// <summary>The header shape of the generated WAV file.</summary>
public enum WavLayout
{
    /// <summary>RIFF, <c>fmt </c> (16 bytes), <c>data</c>.</summary>
    Canonical,

    /// <summary>A <c>LIST</c> chunk of odd size (so a pad byte follows) before the data and a trailing chunk after it: the reader must skip unknown chunks.</summary>
    ExtraChunks,

    /// <summary><c>WAVE_FORMAT_EXTENSIBLE</c> with a 40-byte <c>fmt </c> chunk.</summary>
    Extensible,
}

/// <summary>
/// The parameters of a generated clip. These are what a failure message prints (never the samples).
/// </summary>
/// <param name="SampleRate">Samples per second per channel.</param>
/// <param name="Channels">1 or 2.</param>
/// <param name="Encoding">How the file stores the samples.</param>
/// <param name="Layout">The header shape of the file.</param>
/// <param name="Milliseconds">Duration, 0 to 5000.</param>
/// <param name="Signal">The signal family.</param>
/// <param name="FrequencyHz">Tone frequency (sine, two-tone, square).</param>
/// <param name="Amplitude">Peak level as a fraction of full scale.</param>
/// <param name="StereoMode">For stereo, 0 right equals left, 1 right is the negation of left, 2 right is half of left.</param>
/// <param name="Invert">Negates the whole signal (the sign of a DC offset or an impulse).</param>
public sealed record AudioSpec(
    int SampleRate,
    int Channels,
    WavEncoding Encoding,
    WavLayout Layout,
    int Milliseconds,
    SignalKind Signal,
    double FrequencyHz,
    double Amplitude,
    int StereoMode,
    bool Invert)
{
    /// <summary>Sample frames (samples per channel).</summary>
    public int Frames => checked((int)((long)SampleRate * Milliseconds / 1000));
}

/// <summary>A generated clip: its parameters and the exact interleaved samples the WAV bytes encode.</summary>
public sealed record AudioCase(AudioSpec Spec, short[] Samples)
{
    /// <summary>The decoded form the WAV reader must return for <see cref="Bytes"/>.</summary>
    public WavData Wav => new(Spec.SampleRate, Spec.Channels, Samples);

    /// <summary>The WAV file for this clip.</summary>
    public WavBytes Bytes => WavBuilder.Build(Spec, Samples);

    /// <summary>The parameters plus a hash of the samples; never the samples themselves.</summary>
    public string Describe()
    {
        return $"{Spec} samples={Samples.Length} sha256={DeterministicHash.OfSamples(Samples)}";
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Describe();
    }
}

/// <summary>A WAV file's bytes plus where its header chunks sit, so mutations can aim at them.</summary>
/// <param name="Data">The file.</param>
/// <param name="FmtHeader">Offset of the <c>fmt </c> chunk header.</param>
/// <param name="DataHeader">Offset of the <c>data</c> chunk header.</param>
public sealed record WavBytes(byte[] Data, int FmtHeader, int DataHeader)
{
    /// <summary>Number of bytes before the first sample.</summary>
    public int HeaderLength => DataHeader + 8;
}

/// <summary>Writes the WAV shapes the reader has to accept: PCM16 or float32, three header layouts. Independent of <c>WavFile.Write</c> on purpose.</summary>
public static class WavBuilder
{
    private static readonly byte[] SubFormatTail = [0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71];

    /// <summary>Builds the WAV file for interleaved 16-bit samples; a float32 file stores <c>s / 32768</c>, which reads back as <c>s</c> exactly.</summary>
    public static WavBytes Build(AudioSpec spec, short[] samples)
    {
        var bytesPerSample = spec.Encoding == WavEncoding.Pcm16 ? 2 : 4;
        var dataSize = samples.Length * bytesPerSample;
        var tag = (ushort)(spec.Encoding == WavEncoding.Pcm16 ? 1 : 3);
        var fmtSize = spec.Layout == WavLayout.Extensible ? 40 : 16;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(0u);
        writer.Write("WAVE"u8.ToArray());

        var fmtHeader = (int)stream.Position;
        writer.Write("fmt "u8.ToArray());
        writer.Write((uint)fmtSize);
        writer.Write(spec.Layout == WavLayout.Extensible ? (ushort)0xFFFE : tag);
        writer.Write((ushort)spec.Channels);
        writer.Write((uint)spec.SampleRate);
        writer.Write((uint)((long)spec.SampleRate * spec.Channels * bytesPerSample));
        writer.Write((ushort)(spec.Channels * bytesPerSample));
        writer.Write((ushort)(bytesPerSample * 8));
        if (spec.Layout == WavLayout.Extensible)
        {
            writer.Write((ushort)22);
            writer.Write((ushort)(bytesPerSample * 8));
            writer.Write(spec.Channels == 1 ? 4u : 3u);
            writer.Write(tag);
            writer.Write(SubFormatTail);
        }

        if (spec.Layout == WavLayout.ExtraChunks)
        {
            writer.Write("LIST"u8.ToArray());
            writer.Write(5u);
            writer.Write("abcde"u8.ToArray());
            writer.Write((byte)0);
        }

        var dataHeader = (int)stream.Position;
        writer.Write("data"u8.ToArray());
        writer.Write((uint)dataSize);
        foreach (var sample in samples)
        {
            if (spec.Encoding == WavEncoding.Pcm16)
            {
                writer.Write(sample);
            }
            else
            {
                writer.Write(sample / 32768f);
            }
        }

        if (spec.Layout == WavLayout.ExtraChunks)
        {
            writer.Write("id3 "u8.ToArray());
            writer.Write(4u);
            writer.Write("tags"u8.ToArray());
        }

        writer.Flush();
        var data = stream.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), (uint)(data.Length - 8));
        return new WavBytes(data, fmtHeader, dataHeader);
    }
}

/// <summary>The kinds of damage applied to a valid WAV file.</summary>
public enum WavMutationKind
{
    /// <summary>Cut the file short.</summary>
    Truncate,

    /// <summary>Overwrite one byte of the header region.</summary>
    CorruptHeaderByte,

    /// <summary>Set the size field of the RIFF, <c>fmt </c> or <c>data</c> chunk to another value.</summary>
    WrongChunkSize,
}

/// <summary>A WAV file with one kind of damage; the parameters decide exactly where and how.</summary>
/// <param name="Clip">The clip the valid file encodes.</param>
/// <param name="Kind">What is damaged.</param>
/// <param name="Position">Selects the byte (corrupt), the new length (truncate) or the chunk (size).</param>
/// <param name="Value">The new byte or size.</param>
public sealed record WavMutation(AudioCase Clip, WavMutationKind Kind, int Position, uint Value)
{
    /// <summary>The damaged file.</summary>
    public byte[] Apply()
    {
        var bytes = Clip.Bytes;
        var data = (byte[])bytes.Data.Clone();
        switch (Kind)
        {
            case WavMutationKind.Truncate:
                return data[..(int)((uint)Position % (uint)data.Length)];
            case WavMutationKind.CorruptHeaderByte:
                data[(int)((uint)Position % (uint)Math.Min(data.Length, bytes.HeaderLength))] = (byte)Value;
                return data;
            default:
                var offset = (Position % 3) switch
                {
                    0 => 4,
                    1 => bytes.FmtHeader + 4,
                    _ => bytes.DataHeader + 4,
                };
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), Value);
                return data;
        }
    }

    /// <summary>The clip parameters and the damage; never the bytes.</summary>
    public string Describe()
    {
        return $"{Kind} position={Position} value={Value.ToString(CultureInfo.InvariantCulture)} clip=({Clip.Describe()})";
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Describe();
    }
}

/// <summary>
/// Audio generators: <see cref="WavData"/> clips at the sample rates the pipeline may meet, mono and stereo, PCM16 and float32, zero to five
/// seconds, from silence to clipped noise, and raw byte damage to valid WAV files. White noise is drawn from the CsCheck generator (never
/// <c>System.Random</c>), so a CsCheck seed replays a clip.
/// </summary>
public static class AudioGen
{
    /// <summary>The sample rates of plan section 4.</summary>
    public static IReadOnlyList<int> SampleRates { get; } = [8000, 11025, 16000, 22050, 44100, 48000, 96000];

    /// <summary>A generator over the clip parameters, durations up to <paramref name="maxMilliseconds"/>.</summary>
    public static Gen<AudioSpec> Spec(int maxMilliseconds = 5000)
    {
        var rate = Gen.OneOfConst([.. SampleRates]);
        var channels = Gen.Int[1, 2];
        var encoding = Gen.Enum<WavEncoding>();
        var layout = Gen.Frequency(
            (6, (IGen<WavLayout>)Gen.Const(WavLayout.Canonical)),
            (1, (IGen<WavLayout>)Gen.Const(WavLayout.ExtraChunks)),
            (1, (IGen<WavLayout>)Gen.Const(WavLayout.Extensible)));
        // Most clips are short (the resampler costs about 200 ns per input sample); the long tail still reaches the full range up to 5 s.
        var milliseconds = Gen.Frequency(
            (40, (IGen<int>)Gen.Int[0, Math.Min(60, maxMilliseconds)]),
            (12, (IGen<int>)Gen.Int[0, Math.Min(400, maxMilliseconds)]),
            (3, (IGen<int>)Gen.Int[0, Math.Min(1500, maxMilliseconds)]),
            (1, (IGen<int>)Gen.Int[0, maxMilliseconds]),
            (4, (IGen<int>)Gen.OneOfConst(0, 1, 19, 20, 21, 39, 40)));
        var signal = Gen.Enum<SignalKind>();
        var frequency = Gen.Frequency(
            (6, (IGen<double>)Gen.Double[60.0, 4000.0]),
            (1, (IGen<double>)Gen.Double[4000.0, 47000.0]),
            (1, (IGen<double>)Gen.OneOfConst(60.0, 440.0, 1000.0, 7999.0)));
        var amplitude = Gen.Frequency(
            (3, (IGen<double>)Gen.OneOfConst(0.0005, 0.001, 0.01, 0.05, 0.3, 0.9, 1.0)),
            (3, (IGen<double>)Gen.Double[0.0, 1.0]));
        var stereo = Gen.Int[0, 2];
        var invert = Gen.Bool;

        var head = Gen.Select(rate, channels, encoding, layout, milliseconds, static (r, c, e, l, ms) => (r, c, e, l, ms));
        var tail = Gen.Select(signal, frequency, amplitude, stereo, invert, static (s, f, a, st, inv) => (s, f, a, st, inv));
        return Gen.Select(head, tail, static (h, t) => new AudioSpec(h.r, h.c, h.e, h.l, h.ms, t.s, t.f, t.a, t.st, t.inv));
    }

    /// <summary>A clip: parameters plus the exact samples (noise drawn from the CsCheck generator).</summary>
    public static Gen<AudioCase> Clip(int maxMilliseconds = 5000)
    {
        return Spec(maxMilliseconds).SelectMany(
            static spec => spec.Signal == SignalKind.WhiteNoise
                ? Gen.Short.Array[spec.Frames].Select(noise => Synthesize(spec, noise))
                : Gen.Const(Synthesize(spec, null)));
    }

    /// <summary>Includes the fixed edge shapes of <see cref="AdversarialCorpus.AudioShapes"/> one time in four.</summary>
    public static Gen<AudioCase> MixedClip(int maxMilliseconds = 5000)
    {
        var edges = Gen.OneOfConst([.. AdversarialCorpus.AudioShapes.Where(shape => shape.Milliseconds <= maxMilliseconds)]);
        var edgeClip = edges.SelectMany(static spec => spec.Signal == SignalKind.WhiteNoise
            ? Gen.Short.Array[spec.Frames].Select(noise => Synthesize(spec, noise))
            : Gen.Const(Synthesize(spec, null)));
        return Gen.Frequency(
            (FuzzOptions.BenignWeight, (IGen<AudioCase>)Clip(maxMilliseconds)),
            (FuzzOptions.AdversarialWeight, (IGen<AudioCase>)edgeClip));
    }

    /// <summary>One kind of damage to a short valid file.</summary>
    public static Gen<WavMutation> Mutation()
    {
        var value = Gen.Frequency(
            (4, (IGen<uint>)Gen.UInt),
            (3, (IGen<uint>)Gen.OneOfConst(0u, 1u, 15u, 16u, 17u, 18u, 39u, 40u, 41u, 255u, 65535u, 0x7FFFFFFFu, 0x80000000u, 0xFFFFFFFFu, 0xFFFFFFFEu, 2u, 44u, 4u)));
        return Gen.Select(Clip(120), Gen.Enum<WavMutationKind>(), Gen.Int[0, 100_000], value, static (clip, kind, position, v) => new WavMutation(clip, kind, position, v));
    }

    /// <summary>Renders the samples of a spec (left channel from the signal, the right channel derived), interleaved.</summary>
    public static AudioCase Synthesize(AudioSpec spec, short[]? noise)
    {
        var frames = spec.Frames;
        var left = new short[frames];
        var sign = spec.Invert ? -1.0 : 1.0;
        var peak = spec.Amplitude * short.MaxValue;
        for (var i = 0; i < frames; i++)
        {
            var t = (double)i / spec.SampleRate;
            double value = spec.Signal switch
            {
                SignalKind.Silence => 0.0,
                SignalKind.Sine => peak * Math.Sin(2.0 * Math.PI * spec.FrequencyHz * t),
                SignalKind.TwoTone => 0.5 * peak * (Math.Sin(2.0 * Math.PI * spec.FrequencyHz * t) + Math.Sin(2.0 * Math.PI * 1.5 * spec.FrequencyHz * t)),
                SignalKind.WhiteNoise => noise![i] * spec.Amplitude,
                SignalKind.ClippedSquare => Math.Clamp(1.8 * peak * Math.Sign(Math.Sin(2.0 * Math.PI * spec.FrequencyHz * t)), short.MinValue, short.MaxValue),
                SignalKind.DcOffset => sign * peak,
                _ => i == frames / 2 ? sign * short.MaxValue : 0.0,
            };
            left[i] = ToSample(spec.Signal is SignalKind.DcOffset or SignalKind.Impulse ? value : sign * value);
        }

        if (spec.Channels == 1)
        {
            return new AudioCase(spec, left);
        }

        var interleaved = new short[frames * 2];
        for (var i = 0; i < frames; i++)
        {
            interleaved[2 * i] = left[i];
            interleaved[(2 * i) + 1] = spec.StereoMode switch
            {
                0 => left[i],
                1 => (short)Math.Clamp(-(int)left[i], short.MinValue, short.MaxValue),
                _ => (short)(left[i] / 2),
            };
        }

        return new AudioCase(spec, interleaved);
    }

    private static short ToSample(double value)
    {
        return (short)Math.Clamp(Math.Round(value, MidpointRounding.ToEven), short.MinValue, short.MaxValue);
    }
}
