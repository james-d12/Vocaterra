using System.Buffers.Binary;
using System.Text;
using Slg.Core.Audio;

namespace Slg.Response.Speech;

/// <summary>Minimal RIFF/WAVE reader and writer for mono 16-bit PCM, kept local so this project does not depend on the audio project.</summary>
internal static class WavCodec
{
    private const int HeaderSize = 44;
    private const ushort PcmFormat = 1;
    private const ushort ExtensibleFormat = 0xFFFE;

    public static AudioBuffer Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12 || !data[..4].SequenceEqual("RIFF"u8) || !data.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("Not a RIFF/WAVE file.");
        }

        var position = 12;
        var haveFormat = false;
        var sampleRate = 0;
        while (position + 8 <= data.Length)
        {
            var id = data.Slice(position, 4);
            var declared = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(position + 4, 4));
            var bodyStart = position + 8;
            var size = (int)Math.Min(declared, (uint)(data.Length - bodyStart));
            if (id.SequenceEqual("fmt "u8))
            {
                sampleRate = ReadFormat(data.Slice(bodyStart, size));
                haveFormat = true;
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (!haveFormat)
                {
                    throw new InvalidDataException("WAV data chunk precedes the fmt chunk.");
                }

                var count = size / sizeof(short);
                var samples = new short[count];
                for (var i = 0; i < count; i++)
                {
                    samples[i] = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(bodyStart + (i * sizeof(short)), sizeof(short)));
                }

                return new AudioBuffer(samples, sampleRate);
            }

            position = bodyStart + size + (size & 1);
        }

        throw new InvalidDataException("WAV file has no data chunk.");
    }

    public static byte[] Write(AudioBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var dataBytes = buffer.Samples.Length * sizeof(short);
        var bytes = new byte[HeaderSize + dataBytes];
        var span = bytes.AsSpan();
        Encoding.ASCII.GetBytes("RIFF", span[..4]);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), (uint)(HeaderSize - 8 + dataBytes));
        Encoding.ASCII.GetBytes("WAVE", span.Slice(8, 4));
        Encoding.ASCII.GetBytes("fmt ", span.Slice(12, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(20, 2), PcmFormat);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(22, 2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(24, 4), (uint)buffer.SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(28, 4), (uint)(buffer.SampleRate * sizeof(short)));
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(32, 2), sizeof(short));
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(34, 2), 16);
        Encoding.ASCII.GetBytes("data", span.Slice(36, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(40, 4), (uint)dataBytes);
        for (var i = 0; i < buffer.Samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(span.Slice(HeaderSize + (i * sizeof(short)), sizeof(short)), buffer.Samples[i]);
        }

        return bytes;
    }

    private static int ReadFormat(ReadOnlySpan<byte> body)
    {
        if (body.Length < 16)
        {
            throw new InvalidDataException("WAV fmt chunk is too short.");
        }

        var tag = BinaryPrimitives.ReadUInt16LittleEndian(body[..2]);
        if (tag == ExtensibleFormat && body.Length >= 26)
        {
            tag = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(24, 2));
        }

        var channels = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(2, 2));
        var rate = BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(4, 4));
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(14, 2));
        if (tag != PcmFormat || channels != 1 || bits != 16)
        {
            throw new InvalidDataException($"Only mono 16-bit PCM WAV is supported (format {tag}, {channels} channel(s), {bits} bits).");
        }

        if (rate is 0 or > int.MaxValue)
        {
            throw new InvalidDataException($"Unsupported WAV sample rate {rate}.");
        }

        return (int)rate;
    }
}
