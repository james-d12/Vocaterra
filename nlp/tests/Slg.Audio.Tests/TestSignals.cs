namespace Slg.Audio.Tests;

/// <summary>Deterministic synthetic signals shared by the audio tests.</summary>
internal static class TestSignals
{
    public static short[] Silence(int count) => new short[count];

    public static short[] Sine(double frequencyHz, int sampleRate, int count, double amplitude)
    {
        var samples = new short[count];
        for (var i = 0; i < count; i++)
        {
            var value = amplitude * DeterministicMath.SinPi(2.0 * frequencyHz * i / sampleRate);
            samples[i] = (short)Math.Round(value, MidpointRounding.ToEven);
        }

        return samples;
    }

    public static short[] Concat(params short[][] parts)
    {
        return parts.SelectMany(p => p).ToArray();
    }

    public static short[] Interleave(short[] left, short[] right)
    {
        var result = new short[left.Length * 2];
        for (var i = 0; i < left.Length; i++)
        {
            result[2 * i] = left[i];
            result[(2 * i) + 1] = right[i];
        }

        return result;
    }

    public static short Peak(short[] samples) => (short)samples.Max(s => Math.Abs((int)s));
}
