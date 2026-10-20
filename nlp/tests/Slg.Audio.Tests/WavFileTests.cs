using System.Buffers.Binary;

namespace Slg.Audio.Tests;

/// <summary>Tests for WAV reading and writing.</summary>
public class WavFileTests
{
    private static readonly short[] Extremes = [0, 1, -1, 32767, -32768, 12345, -12345, 256, -256, 7];
    private static readonly short[] Reversed = Enumerable.Reverse(Extremes).ToArray();

    [Fact]
    public void MonoPcm16RoundTripsExactly()
    {
        var original = new WavData(16000, 1, Extremes);
        var bytes = Serialize(original);

        var read = WavFile.Read(new MemoryStream(bytes));

        Assert.Equal(original, read);
        Assert.Equal(bytes, Serialize(read));
    }

    [Fact]
    public void StereoPcm16RoundTripsExactly()
    {
        var original = new WavData(44100, 2, TestSignals.Interleave(Extremes, Reversed));
        var bytes = Serialize(original);

        var read = WavFile.Read(new MemoryStream(bytes));

        Assert.Equal(2, read.Channels);
        Assert.Equal(44100, read.SampleRate);
        Assert.Equal(original, read);
        Assert.Equal(bytes, Serialize(read));
    }

    [Fact]
    public void WriterEmitsCanonicalHeader()
    {
        var bytes = Serialize(new WavData(8000, 1, [1, 2, 3]));

        Assert.Equal(44 + 6, bytes.Length);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(36u + 6u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal("WAVEfmt ", System.Text.Encoding.ASCII.GetString(bytes, 8, 8));
        Assert.Equal(8000u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(24)));
        Assert.Equal(16000u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(28)));
        Assert.Equal("data", System.Text.Encoding.ASCII.GetString(bytes, 36, 4));
        Assert.Equal(6u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(40)));
    }

    [Fact]
    public void FileRoundTripWorks()
    {
        var directory = Directory.CreateTempSubdirectory("slg-wav-");
        try
        {
            var path = Path.Combine(directory.FullName, "a.wav");
            var original = new WavData(22050, 1, Extremes);

            WavFile.Write(path, original);

            Assert.Equal(original, WavFile.Read(path));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Float32IsConvertedToPcm16WithClampAndEvenRounding()
    {
        float[] values = [0f, 0.5f / 32768f, 1.5f / 32768f, 2.5f / 32768f, 1.0f, -1.0f, 2.0f, -2.0f, float.NaN, 100f / 32768f, -100f / 32768f];
        var bytes = BuildWav(3, 1, 48000, 32, values.SelectMany(v => BitConverter.GetBytes(v)).ToArray());

        var read = WavFile.Read(new MemoryStream(bytes));

        Assert.Equal(48000, read.SampleRate);
        Assert.Equal(new short[] { 0, 0, 2, 2, 32767, -32768, 32767, -32768, 0, 100, -100 }, read.InterleavedSamples);
    }

    [Fact]
    public void Float32RoundTripIsByteExactOnSamples()
    {
        var pcm = new WavData(16000, 2, TestSignals.Interleave(Extremes, Reversed));
        var floats = pcm.InterleavedSamples.SelectMany(s => BitConverter.GetBytes(s / 32768f)).ToArray();
        var floatWav = BuildWav(3, 2, 16000, 32, floats);

        var read = WavFile.Read(new MemoryStream(floatWav));

        Assert.Equal(pcm, read);
        Assert.Equal(Serialize(pcm), Serialize(read));
    }

    [Fact]
    public void ReaderSkipsExtraChunksIncludingOddSizedOnes()
    {
        var pcm = BuildPcmBytes(Extremes);
        var list = Chunk("LIST", "INFOISFT"u8.ToArray());
        var odd = Chunk("junk", [1, 2, 3]);
        var tail = Chunk("id3 ", [9, 9]);
        var bytes = BuildRiff(
            Chunk("fmt ", FormatBody(1, 1, 16000, 16)),
            list,
            odd,
            Chunk("data", pcm),
            tail);

        var read = WavFile.Read(new MemoryStream(bytes));

        Assert.Equal(new WavData(16000, 1, Extremes), read);
    }

    [Fact]
    public void ExtensibleFormatIsAccepted()
    {
        var body = new byte[40];
        FormatBody(0xFFFE, 1, 16000, 16).CopyTo(body, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(16), 22);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(24), 1);
        var bytes = BuildRiff(Chunk("fmt ", body), Chunk("data", BuildPcmBytes(Extremes)));

        var read = WavFile.Read(new MemoryStream(bytes));

        Assert.Equal(new WavData(16000, 1, Extremes), read);
    }

    [Fact]
    public void OversizedDataChunkIsClampedToAvailableWholeFrames()
    {
        var bytes = BuildWav(1, 2, 16000, 16, BuildPcmBytes([1, 2, 3, 4, 5, 6]));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40), 0xFFFFFFFF);
        Array.Resize(ref bytes, bytes.Length - 1);

        var read = WavFile.Read(new MemoryStream(bytes));

        Assert.Equal(new short[] { 1, 2, 3, 4 }, read.InterleavedSamples);
    }

    [Theory]
    [InlineData(1, 1, 8)]
    [InlineData(1, 1, 24)]
    [InlineData(1, 1, 32)]
    [InlineData(3, 1, 64)]
    [InlineData(1, 3, 16)]
    [InlineData(1, 6, 16)]
    [InlineData(6, 1, 8)]
    [InlineData(2, 1, 4)]
    public void UnsupportedFormatsThrowNotSupported(int tag, int channels, int bits)
    {
        var bytes = BuildWav((ushort)tag, (ushort)channels, 16000, (ushort)bits, new byte[64]);

        var exception = Assert.Throws<NotSupportedException>(() => WavFile.Read(new MemoryStream(bytes)));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Fact]
    public void MalformedInputThrowsInvalidData()
    {
        Assert.Throws<InvalidDataException>(() => WavFile.Read(new MemoryStream([1, 2, 3])));
        Assert.Throws<InvalidDataException>(() => WavFile.Read(new MemoryStream("RIFF\0\0\0\0AVI LIST"u8.ToArray())));
        Assert.Throws<InvalidDataException>(() => WavFile.Read(new MemoryStream(BuildRiff(Chunk("data", [0, 0])))));
        Assert.Throws<InvalidDataException>(() => WavFile.Read(new MemoryStream(BuildRiff(Chunk("fmt ", FormatBody(1, 1, 16000, 16))))));
    }

    [Fact]
    public void WavDataValidatesItsArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WavData(0, 1, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WavData(16000, 3, []));
        Assert.Throws<ArgumentNullException>(() => new WavData(16000, 1, null!));
        Assert.Throws<ArgumentException>(() => WavFile.Write(new MemoryStream(), new WavData(16000, 2, [1, 2, 3])));
    }

    private static byte[] Serialize(WavData data)
    {
        using var stream = new MemoryStream();
        WavFile.Write(stream, data);
        return stream.ToArray();
    }

    private static byte[] BuildPcmBytes(short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), samples[i]);
        }

        return bytes;
    }

    private static byte[] FormatBody(ushort tag, ushort channels, uint rate, ushort bits)
    {
        var body = new byte[16];
        var blockAlign = (ushort)(channels * (bits / 8));
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(0), tag);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(2), channels);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(4), rate);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(8), rate * blockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(12), blockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(14), bits);
        return body;
    }

    private static byte[] Chunk(string id, byte[] body)
    {
        var bytes = new List<byte>();
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(id));
        bytes.AddRange(BitConverter.GetBytes((uint)body.Length));
        bytes.AddRange(body);
        if (body.Length % 2 == 1)
        {
            bytes.Add(0);
        }

        return [.. bytes];
    }

    private static byte[] BuildRiff(params byte[][] chunks)
    {
        var body = chunks.SelectMany(c => c).ToArray();
        return [.. "RIFF"u8, .. BitConverter.GetBytes((uint)(body.Length + 4)), .. "WAVE"u8, .. body];
    }

    private static byte[] BuildWav(ushort tag, ushort channels, uint rate, ushort bits, byte[] data)
    {
        return BuildRiff(Chunk("fmt ", FormatBody(tag, channels, rate, bits)), Chunk("data", data));
    }
}
