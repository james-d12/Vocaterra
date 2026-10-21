using System.Buffers.Binary;
using System.Globalization;

namespace Vocaterra.Nlp.Audio;

/// <summary>
/// Reads and writes RIFF/WAVE files. Reading accepts PCM 16-bit and IEEE float 32-bit data (plain or
/// WAVE_FORMAT_EXTENSIBLE), mono or stereo, at any sample rate, and skips every chunk other than
/// <c>fmt </c> and <c>data</c>. Writing always emits the canonical 44-byte header followed by PCM 16-bit data,
/// so output bytes depend only on the <see cref="WavData"/>.
/// </summary>
public static class WavFile
{
    private const int HeaderSize = 44;
    private const ushort FormatPcm = 1;
    private const ushort FormatFloat = 3;
    private const ushort FormatExtensible = 0xFFFE;

    /// <summary>Reads a WAV file from a stream (read to its end).</summary>
    /// <exception cref="InvalidDataException">The data is not a well-formed WAV file.</exception>
    /// <exception cref="NotSupportedException">The file is valid but uses an unsupported encoding or channel layout.</exception>
    public static WavData Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return Parse(copy.GetBuffer().AsSpan(0, checked((int)copy.Length)));
    }

    /// <summary>Reads a WAV file from disk.</summary>
    public static WavData Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var file = File.OpenRead(path);
        return Read(file);
    }

    /// <summary>Writes PCM 16-bit WAV data with a canonical 44-byte header.</summary>
    public static void Write(Stream stream, WavData data)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(data);
        data.EnsureWholeFrames();

        var dataSize = (long)data.InterleavedSamples.Length * sizeof(short);
        if (dataSize > int.MaxValue - HeaderSize)
        {
            throw new ArgumentException("Audio is too large for a single WAV file.", nameof(data));
        }

        var buffer = new byte[HeaderSize + (int)dataSize];
        var span = buffer.AsSpan();
        var blockAlign = (ushort)(data.Channels * sizeof(short));
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], (uint)(36 + dataSize));
        "WAVE"u8.CopyTo(span[8..]);
        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(span[20..], FormatPcm);
        BinaryPrimitives.WriteUInt16LittleEndian(span[22..], (ushort)data.Channels);
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..], (uint)data.SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], (uint)((long)data.SampleRate * blockAlign));
        BinaryPrimitives.WriteUInt16LittleEndian(span[32..], blockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(span[34..], 16);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[40..], (uint)dataSize);
        for (var i = 0; i < data.InterleavedSamples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(span[(HeaderSize + (i * sizeof(short)))..], data.InterleavedSamples[i]);
        }

        stream.Write(buffer, 0, buffer.Length);
    }

    /// <summary>Writes PCM 16-bit WAV data to a file, replacing any existing file.</summary>
    public static void Write(string path, WavData data)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var file = File.Create(path);
        Write(file, data);
    }

    /// <summary>
    /// Converts a float sample (nominal range -1..1) to 16 bits as <c>round-half-to-even(v * 32768)</c>
    /// clamped to [-32768, 32767]; NaN becomes 0. This is the exact inverse of <c>s / 32768</c>.
    /// </summary>
    internal static short FloatToPcm16(float value)
    {
        if (float.IsNaN(value))
        {
            return 0;
        }

        var scaled = Math.Round((double)value * 32768.0, MidpointRounding.ToEven);
        return scaled >= 32767.0 ? (short)32767 : scaled <= -32768.0 ? (short)-32768 : (short)scaled;
    }

    private static WavData Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12)
        {
            throw new InvalidDataException("Not a WAV file: shorter than a RIFF header.");
        }

        if (bytes[..4].SequenceEqual("RF64"u8) || bytes[..4].SequenceEqual("BW64"u8))
        {
            throw new NotSupportedException("RF64/BW64 WAV files (larger than 4 GiB) are not supported.");
        }

        if (!bytes[..4].SequenceEqual("RIFF"u8) || !bytes.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("Not a WAV file: missing RIFF/WAVE signature.");
        }

        var haveFormat = false;
        ushort tag = 0;
        var channels = 0;
        var sampleRate = 0;
        var blockAlign = 0;
        var bits = 0;
        var dataStart = -1;
        var dataLength = 0;

        long position = 12;
        while (position + 8 <= bytes.Length)
        {
            var id = bytes.Slice((int)position, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(bytes[((int)position + 4)..]);
            var body = position + 8;
            var available = bytes.Length - body;

            if (id.SequenceEqual("fmt "u8))
            {
                if (size < 16 || size > available)
                {
                    throw new InvalidDataException("Malformed 'fmt ' chunk.");
                }

                var fmt = bytes.Slice((int)body, (int)size);
                tag = BinaryPrimitives.ReadUInt16LittleEndian(fmt);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..]);
                var rate = BinaryPrimitives.ReadUInt32LittleEndian(fmt[4..]);
                blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(fmt[12..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(fmt[14..]);
                if (rate == 0 || rate > int.MaxValue)
                {
                    throw new InvalidDataException($"Invalid sample rate {rate}.");
                }

                sampleRate = (int)rate;
                if (tag == FormatExtensible)
                {
                    if (size < 40)
                    {
                        throw new InvalidDataException("Malformed WAVE_FORMAT_EXTENSIBLE 'fmt ' chunk.");
                    }

                    tag = BinaryPrimitives.ReadUInt16LittleEndian(fmt[24..]);
                }

                haveFormat = true;
            }
            else if (id.SequenceEqual("data"u8) && dataStart < 0)
            {
                dataStart = (int)body;
                dataLength = (int)Math.Min(size, available);
            }

            position = body + size + (size & 1);
        }

        if (!haveFormat)
        {
            throw new InvalidDataException("WAV file has no 'fmt ' chunk.");
        }

        if (dataStart < 0)
        {
            throw new InvalidDataException("WAV file has no 'data' chunk.");
        }

        ValidateFormat(tag, channels, bits, blockAlign);
        var data = bytes.Slice(dataStart, dataLength);
        var frames = data.Length / blockAlign;
        var samples = new short[frames * channels];
        if (tag == FormatPcm)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = BinaryPrimitives.ReadInt16LittleEndian(data[(i * 2)..]);
            }
        }
        else
        {
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = FloatToPcm16(BinaryPrimitives.ReadSingleLittleEndian(data[(i * 4)..]));
            }
        }

        return new WavData(sampleRate, channels, samples);
    }

    private static void ValidateFormat(ushort tag, int channels, int bits, int blockAlign)
    {
        if (tag == FormatPcm)
        {
            if (bits != 16)
            {
                throw new NotSupportedException($"Unsupported WAV encoding: integer PCM with {bits} bits per sample (only 16-bit PCM and 32-bit float are supported).");
            }
        }
        else if (tag == FormatFloat)
        {
            if (bits != 32)
            {
                throw new NotSupportedException($"Unsupported WAV encoding: IEEE float with {bits} bits per sample (only 32-bit float and 16-bit PCM are supported).");
            }
        }
        else
        {
            throw new NotSupportedException(
                string.Create(CultureInfo.InvariantCulture, $"Unsupported WAV format tag 0x{tag:X4} (only PCM and IEEE float are supported)."));
        }

        if (channels is not (1 or 2))
        {
            throw new NotSupportedException($"Unsupported WAV channel count {channels} (only mono and stereo are supported).");
        }

        if (blockAlign != channels * (bits / 8))
        {
            throw new InvalidDataException($"Inconsistent WAV block alignment {blockAlign} for {channels} channel(s) of {bits}-bit samples.");
        }
    }
}
