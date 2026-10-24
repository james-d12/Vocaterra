using System.Buffers.Binary;
using Vocaterra.Game.Language.Speech;

namespace Vocaterra.Game.Tests;

/// <summary>The audio handling that turns captured or loaded samples into the clip the recognizer gets: no device, no model.</summary>
public sealed class AudioClipsTests
{
    private static short[] Tone(double hertz, double seconds, int rate, double amplitude = 8000)
    {
        var samples = new short[(int)(seconds * rate)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)Math.Round(amplitude * Math.Sin(2 * Math.PI * hertz * i / rate));
        }

        return samples;
    }

    private static int ZeroCrossings(short[] samples)
    {
        var count = 0;
        for (var i = 1; i < samples.Length; i++)
        {
            if ((samples[i - 1] < 0) != (samples[i] < 0))
            {
                count++;
            }
        }

        return count;
    }

    private static byte[] Wav(short[] interleaved, int rate, int channels, int bits = 16, ushort format = 1)
    {
        var data = new byte[interleaved.Length * 2];
        for (var i = 0; i < interleaved.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i * 2), interleaved[i]);
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + data.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write(format);
        writer.Write((ushort)channels);
        writer.Write(rate);
        writer.Write(rate * channels * 2);
        writer.Write((ushort)(channels * 2));
        writer.Write((ushort)bits);
        writer.Write("data"u8);
        writer.Write(data.Length);
        writer.Write(data);
        return stream.ToArray();
    }

    [Fact]
    public void ResamplingFrom24KilohertzTo16KeepsThePitchAndTheLevel()
    {
        var input = Tone(440, 1.0, 24000);

        var output = AudioClips.Resample(input, 24000, 16000);

        Assert.Equal(16000, output.Length);
        Assert.InRange(ZeroCrossings(output), 876, 884); // 440 Hz for one second: 880 crossings
        Assert.InRange(output.Skip(400).Take(15000).Max(s => (int)s), 7800, 8200); // the amplitude survives the low-pass
    }

    [Fact]
    public void ResamplingFrom44100AndTheSameRateAndUpwardsWorks()
    {
        Assert.Equal(16000, AudioClips.Resample(Tone(300, 1.0, 44100), 44100, 16000).Length);
        var same = Tone(300, 0.1, 16000);
        Assert.Equal(same, AudioClips.Resample(same, 16000, 16000));
        Assert.Equal(8000, AudioClips.Resample(Tone(300, 0.5, 8000), 8000, 16000).Length);
    }

    [Fact]
    public void ResamplingAboveTheTargetsNyquistFrequencyDoesNotAlias()
    {
        // 10 kHz is above the 8 kHz Nyquist of the 16 kHz clip: the low-pass must remove it instead of folding it down to 6 kHz.
        var output = AudioClips.Resample(Tone(10000, 1.0, 44100), 44100, 16000);

        var loudest = output.Skip(400).Take(15000).Max(s => Math.Abs((int)s));
        Assert.True(loudest < 1500, $"The 10 kHz tone leaked through at {loudest}.");
    }

    [Fact]
    public void TrimmingKeepsTheSoundAndAShortPaddingAtBothEnds()
    {
        var silence = new short[16000];
        var tone = Tone(300, 0.5, 16000);
        var clip = silence.Concat(tone).Concat(silence).ToArray();

        var trimmed = AudioClips.TrimSilence(clip);

        var padding = AudioClips.SampleRate * AudioClips.PaddingMilliseconds / 1000;
        Assert.InRange(trimmed.Length, tone.Length + (2 * padding) - 400, tone.Length + (2 * padding) + 400);
        Assert.Equal(0, trimmed[0]);
        Assert.Contains(trimmed, static s => Math.Abs(s) > 7000);
    }

    [Fact]
    public void ASilentClipTrimsToNothingAndPreparesToNull()
    {
        Assert.Empty(AudioClips.TrimSilence(new short[32000]));
        Assert.Null(AudioClips.Prepare(new short[44100], 44100));
        Assert.Null(AudioClips.Prepare([], 16000));
    }

    [Fact]
    public void AClipIsCappedAtEightSecondsAndConvertedTo16Kilohertz()
    {
        var long12 = Tone(300, 12.0, 44100);

        var clip = AudioClips.Prepare(long12, 44100);

        Assert.NotNull(clip);
        Assert.Equal(AudioClips.SampleRate, clip.SampleRate);
        Assert.Equal(AudioClips.MaxSeconds * AudioClips.SampleRate, clip.Samples.Length);
        Assert.Equal(AudioClips.MaxSeconds, (int)clip.DurationSeconds);
    }

    [Fact]
    public void PreparingTheSameSamplesTwiceGivesTheSameClipHash()
    {
        var samples = Tone(300, 1.0, 44100);

        Assert.Equal(AudioClips.Prepare(samples, 44100)!.ClipHash, AudioClips.Prepare(samples, 44100)!.ClipHash);
    }

    [Fact]
    public void PcmBytesAreLittleEndianSamplesAndAnOddByteIsIgnored()
    {
        var samples = AudioClips.FromPcm16([0x01, 0x00, 0xFF, 0xFF, 0x00, 0x80, 0x7F]);

        Assert.Equal([1, -1, short.MinValue], samples);
    }

    [Fact]
    public void AWavFileIsReadAndSeveralChannelsAreAveraged()
    {
        var (monoSamples, monoRate) = AudioClips.ReadWav(Wav([100, -200, 300], 16000, 1));
        Assert.Equal([100, -200, 300], monoSamples);
        Assert.Equal(16000, monoRate);

        var (stereoSamples, stereoRate) = AudioClips.ReadWav(Wav([100, 300, -200, -400], 24000, 2));
        Assert.Equal([200, -300], stereoSamples);
        Assert.Equal(24000, stereoRate);
    }

    [Fact]
    public void ABadWavIsRefusedWithAClearMessage()
    {
        Assert.Throws<InvalidDataException>(() => AudioClips.ReadWav([1, 2, 3]));
        Assert.Throws<InvalidDataException>(() => AudioClips.ReadWav(Wav([1, 2], 16000, 1, bits: 8)));
        Assert.Throws<InvalidDataException>(() => AudioClips.ReadWav(Wav([1, 2], 16000, 1, format: 3)));
    }
}
