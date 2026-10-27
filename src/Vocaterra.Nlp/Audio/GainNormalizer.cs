namespace Vocaterra.Nlp.Audio;

/// <summary>Peak normalization with a hard cap on the applied gain.</summary>
public static class GainNormalizer
{
    /// <summary>Linear amplitude treated as 0 dBFS for peak targets (the largest positive 16-bit sample).</summary>
    public const double FullScale = 32767.0;

    /// <summary>
    /// Scales <paramref name="samples"/> so the loudest sample reaches <paramref name="targetPeakDbfs"/>
    /// (relative to <see cref="FullScale"/>), never applying more than <paramref name="maxGainDb"/> of gain.
    /// Attenuation is not capped. Returns a new array; a silent input (peak 0) or an empty input is returned
    /// as an unchanged copy. Gain factors come from <c>DeterministicMath</c> and each scaled sample is rounded
    /// with <see cref="MidpointRounding.ToEven"/> and clamped to the 16-bit range.
    /// </summary>
    public static short[] Normalize(short[] samples, double targetPeakDbfs = -3.0, double maxGainDb = 20.0)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (!double.IsFinite(targetPeakDbfs) || targetPeakDbfs > 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetPeakDbfs), targetPeakDbfs, "Target peak must be a finite level at or below 0 dBFS.");
        }

        if (!double.IsFinite(maxGainDb) || maxGainDb < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxGainDb), maxGainDb, "Maximum gain must be finite and non-negative.");
        }

        var result = (short[])samples.Clone();
        var peak = 0;
        foreach (var sample in samples)
        {
            var magnitude = Math.Abs((int)sample);
            if (magnitude > peak)
            {
                peak = magnitude;
            }
        }

        if (peak == 0)
        {
            return result;
        }

        var wanted = FullScale * DeterministicMath.DbToAmplitude(targetPeakDbfs) / peak;
        var gain = Math.Min(wanted, DeterministicMath.DbToAmplitude(maxGainDb));
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = Resampler.ToInt16(samples[i] * gain);
        }

        return result;
    }
}
