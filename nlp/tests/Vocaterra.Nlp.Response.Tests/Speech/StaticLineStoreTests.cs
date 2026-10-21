namespace Vocaterra.Nlp.Response.Tests.Speech;

using System.Buffers.Binary;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Response.Speech;

public sealed class StaticLineStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vocaterra-lines-" + Guid.NewGuid().ToString("N"));

    public StaticLineStoreTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private static AudioBuffer Ramp(int count, int rate = 22_050)
    {
        return new AudioBuffer([.. Enumerable.Range(0, count).Select(static i => (short)((i * 257) - 12_000))], rate);
    }

    private void WriteWav(string name, byte[] bytes)
    {
        File.WriteAllBytes(Path.Combine(_directory, name), bytes);
    }

    [Fact]
    public void ReadsAPcm16MonoWavByLineId()
    {
        var expected = Ramp(500);
        WriteWav("greet.1.wav", WavCodec.Write(expected));
        var store = new StaticLineStore(_directory);
        Assert.True(store.Contains("greet.1"));
        Assert.True(store.TryGet("greet.1", out var buffer));
        Assert.Equal(expected, buffer);
        Assert.Equal(22_050, buffer.SampleRate);
    }

    [Fact]
    public void MissingLinesAndUnsafeIdsAreNotFound()
    {
        WriteWav("ok.wav", WavCodec.Write(Ramp(10)));
        var store = new StaticLineStore(_directory);
        Assert.False(store.TryGet("missing", out var buffer));
        Assert.Null(buffer);
        Assert.False(store.TryGet(string.Empty, out _));
        Assert.False(store.TryGet("../ok", out _));
        Assert.False(store.TryGet("sub/ok", out _));
        Assert.False(store.TryGet("ok\\x", out _));
        Assert.False(store.Contains("ok wav"));
        Assert.False(new StaticLineStore(Path.Combine(_directory, "nope")).TryGet("ok", out _));
    }

    [Fact]
    public void ExtraChunksAndOddPaddingAreSkipped()
    {
        var samples = new short[] { 1, -2, 3, -4 };
        var plain = WavCodec.Write(new AudioBuffer(samples, 16_000));
        var oddChunk = new byte[] { (byte)'L', (byte)'I', (byte)'S', (byte)'T', 3, 0, 0, 0, 9, 9, 9, 0 };
        var withList = plain[..36].Concat(oddChunk).Concat(plain[36..]).ToArray();
        WriteWav("list.wav", withList);
        Assert.True(new StaticLineStore(_directory).TryGet("list", out var buffer));
        Assert.Equal(samples, buffer.Samples);
    }

    [Fact]
    public void ADataSizeLargerThanTheFileIsClamped()
    {
        var bytes = WavCodec.Write(new AudioBuffer([5, 6, 7], 8_000));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40, 4), uint.MaxValue);
        WriteWav("stream.wav", bytes);
        Assert.True(new StaticLineStore(_directory).TryGet("stream", out var buffer));
        Assert.Equal(new short[] { 5, 6, 7 }, buffer.Samples);
    }

    [Fact]
    public void UnsupportedWavFilesThrowInvalidData()
    {
        var stereo = WavCodec.Write(new AudioBuffer([1, 2], 8_000));
        BinaryPrimitives.WriteUInt16LittleEndian(stereo.AsSpan(22, 2), 2);
        WriteWav("stereo.wav", stereo);
        var eightBit = WavCodec.Write(new AudioBuffer([1, 2], 8_000));
        BinaryPrimitives.WriteUInt16LittleEndian(eightBit.AsSpan(34, 2), 8);
        WriteWav("eight.wav", eightBit);
        var floatFormat = WavCodec.Write(new AudioBuffer([1, 2], 8_000));
        BinaryPrimitives.WriteUInt16LittleEndian(floatFormat.AsSpan(20, 2), 3);
        WriteWav("float.wav", floatFormat);
        WriteWav("junk.wav", "not a wav file at all"u8.ToArray());
        WriteWav("empty.wav", []);
        WriteWav("nodata.wav", WavCodec.Write(new AudioBuffer([], 8_000))[..36]);
        var store = new StaticLineStore(_directory);
        foreach (var id in new[] { "stereo", "eight", "float", "junk", "empty", "nodata" })
        {
            Assert.Throws<InvalidDataException>(() => store.TryGet(id, out _));
        }
    }

    [Fact]
    public void WavCodecRoundTripsAnyPcmBuffer()
    {
        var buffer = new AudioBuffer([short.MinValue, -1, 0, 1, short.MaxValue], 24_000);
        var bytes = WavCodec.Write(buffer);
        Assert.Equal(44 + 10, bytes.Length);
        Assert.Equal(buffer, WavCodec.Read(bytes));
        Assert.Equal(bytes, WavCodec.Write(WavCodec.Read(bytes)));
        Assert.Equal(new AudioBuffer([], 16_000), WavCodec.Read(WavCodec.Write(new AudioBuffer([], 16_000))));
    }
}
