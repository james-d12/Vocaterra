using Slg.Core.Audio;

namespace Slg.Audio.Tests;

/// <summary>End-to-end tests of the WAV file capture source.</summary>
public sealed class EndToEndTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("slg-audio-");

    public void Dispose()
    {
        _directory.Delete(true);
    }

    [Fact]
    public void WavFileCaptureSourceProducesAStableSixteenKilohertzClip()
    {
        var path = WriteUtteranceWav();
        ICaptureSource source = new WavFileCaptureSource(path);

        var first = source.Capture();
        var second = new WavFileCaptureSource(path).Capture();

        Assert.Equal(PcmClip.DefaultSampleRate, first.SampleRate);
        Assert.Equal(first.ClipHash, second.ClipHash);
        Assert.Equal(first, second);
        Assert.Equal(ClipHasher.Hash(first.Samples), first.ClipHash);
        Assert.InRange(first.Samples.Length, 8000, 10240);
        Assert.InRange(TestSignals.Peak(first.Samples), 23100, 23300);
    }

    [Fact]
    public void CapturedClipMatchesGoldenHash()
    {
        var clip = new WavFileCaptureSource(WriteUtteranceWav()).Capture();

        Assert.Equal(GoldenClipHash, clip.ClipHash);
    }

    [Fact]
    public void MissingFileSurfacesAsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => new WavFileCaptureSource(Path.Combine(_directory.FullName, "missing.wav")).Capture());
    }

    private const string GoldenClipHash = "3a3de434e4ef1065835b2d7eb8c8b456006dba17b01af0c428fc333ba301d9c8";

    private string WriteUtteranceWav()
    {
        var channel = TestSignals.Concat(
            TestSignals.Silence(24000),
            TestSignals.Sine(1000, 48000, 24000, 8000),
            TestSignals.Silence(24000));
        var path = Path.Combine(_directory.FullName, "utterance.wav");
        WavFile.Write(path, new WavData(48000, 2, TestSignals.Interleave(channel, channel)));
        return path;
    }
}
