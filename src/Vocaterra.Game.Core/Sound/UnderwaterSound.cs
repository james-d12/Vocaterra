namespace Vocaterra.Game.Core.Sound;

/// <summary>
/// How the ambient beds sound under water (H10): quieter and duller, by how far under the view is (<c>UnderwaterView.Amount</c>, 0 above the surface to 1
/// fully under). Pure. The mixer asks for each bed's gain and its muffled share; the output plays a dry copy and a muffled copy of each loop in those shares,
/// so the muffling is a cross-fade between two pre-filtered copies and needs no filter in the audio path.
/// </summary>
public static class UnderwaterSound
{
    /// <summary>The beds' level fully under water, as a share of their level above (0.5 is about six decibels down).</summary>
    public const double FullUnderwaterGain = 0.5;

    /// <summary>The cut-off of the muffled copy of a bed, in hertz.</summary>
    public const double MuffleCutoffHz = 600;

    /// <summary>How many one-pole sections the muffled copy runs through (two, so the high end falls about 12 decibels an octave).</summary>
    public const int MuffleSections = 2;

    /// <summary>The gain of the beds at an amount: 1 above the surface, <see cref="FullUnderwaterGain"/> fully under, linear between.</summary>
    /// <param name="amount">The view's amount, 0 to 1 (out of range is clamped, NaN is 0).</param>
    public static double Gain(double amount) => 1 - ((1 - FullUnderwaterGain) * Share(amount));

    /// <summary>The share of the muffled copy at an amount: 0 (the dry bed only) above the surface, 1 (the muffled copy only) fully under.</summary>
    /// <param name="amount">The view's amount, 0 to 1 (out of range is clamped, NaN is 0).</param>
    public static double Muffle(double amount) => Share(amount);

    /// <summary>
    /// A low-passed copy of a loop: one-pole sections at <paramref name="cutoffHz"/>. The filter runs twice round the loop, the first pass only to settle its
    /// state, so the loop's start and end agree (no click at the seam, as the dry loop has none).
    /// </summary>
    /// <param name="samples">The loop's samples.</param>
    /// <param name="cutoffHz">The cut-off in hertz.</param>
    /// <param name="sections">How many one-pole sections run in series.</param>
    public static short[] LowPass(short[] samples, double cutoffHz = MuffleCutoffHz, int sections = MuffleSections)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var alpha = 1 - Math.Exp(-2 * Math.PI * cutoffHz / SoundSynth.SampleRate);
        var state = new double[sections];
        var result = new short[samples.Length];
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                var x = (double)samples[i];
                for (var s = 0; s < sections; s++)
                {
                    state[s] += alpha * (x - state[s]);
                    x = state[s];
                }

                if (pass == 1)
                {
                    result[i] = (short)Math.Round(Math.Clamp(x, short.MinValue, short.MaxValue));
                }
            }
        }

        return result;
    }

    private static double Share(double amount) => double.IsNaN(amount) ? 0 : Math.Clamp(amount, 0, 1);
}
