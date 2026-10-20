namespace Slg.Audio;

/// <summary>Reduces decoded WAV audio to a single channel with a fixed integer rule.</summary>
public static class Downmixer
{
    /// <summary>
    /// Returns mono samples. Stereo frames become <c>(L + R) &gt;&gt; 1</c> evaluated on <see cref="int"/>
    /// (an arithmetic shift, so the average rounds toward negative infinity and never overflows 16 bits);
    /// mono input is copied unchanged.
    /// </summary>
    public static short[] ToMono(WavData wav)
    {
        ArgumentNullException.ThrowIfNull(wav);
        wav.EnsureWholeFrames();
        var source = wav.InterleavedSamples;
        if (wav.Channels == 1)
        {
            return (short[])source.Clone();
        }

        var mono = new short[source.Length / 2];
        for (var i = 0; i < mono.Length; i++)
        {
            int left = source[2 * i];
            int right = source[(2 * i) + 1];
            mono[i] = (short)((left + right) >> 1);
        }

        return mono;
    }
}
