using System.Buffers.Binary;

namespace Vocaterra.Game.Sound;

/// <summary>A sound file read into memory: 16-bit PCM samples (mono, or interleaved if the file has more channels) and the format they came in.</summary>
/// <param name="SampleRate">The sample rate in hertz.</param>
/// <param name="Channels">The number of channels.</param>
/// <param name="BitsPerSample">The bits per sample (16 for every file the game ships).</param>
/// <param name="Samples">The samples.</param>
public sealed record WavFile(int SampleRate, int Channels, int BitsPerSample, short[] Samples)
{
    /// <summary>The length in seconds.</summary>
    public double Seconds => SampleRate <= 0 || Channels <= 0 ? 0 : Samples.Length / (double)(SampleRate * Channels);

    /// <summary>Reads a RIFF/WAVE file of 16-bit PCM (what the game's audio folder holds).</summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <exception cref="InvalidDataException">The bytes are not a 16-bit PCM WAVE file.</exception>
    public static WavFile Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 44 || !bytes[..4].SequenceEqual("RIFF"u8) || !bytes.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("Not a RIFF/WAVE file.");
        }

        int rate = 0, channels = 0, bits = 0, format = 0;
        var position = 12;
        while (position + 8 <= bytes.Length)
        {
            var id = bytes.Slice(position, 4);
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(position + 4, 4));
            var body = position + 8;
            if (id.SequenceEqual("fmt "u8) && size >= 16 && body + 16 <= bytes.Length)
            {
                format = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(body, 2));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(body + 2, 2));
                rate = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(body + 4, 4));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(body + 14, 2));
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (format != 1 || bits != 16 || channels < 1 || rate <= 0)
                {
                    throw new InvalidDataException($"Only 16-bit PCM is read (format {format}, {bits} bits, {channels} channels, {rate} Hz).");
                }

                var length = Math.Min(size, bytes.Length - body) / 2;
                var samples = new short[length];
                for (var i = 0; i < length; i++)
                {
                    samples[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(body + (2 * i), 2));
                }

                return new WavFile(rate, channels, bits, samples);
            }

            position = body + size + (size & 1);
        }

        throw new InvalidDataException("The WAVE file has no data chunk.");
    }
}
