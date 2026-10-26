using Vocaterra.Game.Core.Utilities;

namespace Vocaterra.Game.Core.Sound;

/// <summary>
/// The looping ambient beds of S25 (wind, the river, the waterfall, the village, the evening insects), made in code at load: no file, no licence, the same
/// samples every time. Each loop is <see cref="LoopSeconds"/> long and seamless: the sound is generated for <see cref="CrossfadeSeconds"/> more than the loop, and
/// that tail is faded into the head with an equal-power curve, so the last sample of the loop is followed by the very next sample the process would make.
/// </summary>
public static class AmbientLoops
{
    /// <summary>The length of a loop in seconds.</summary>
    public const double LoopSeconds = 10;

    /// <summary>How much extra sound is made past the loop and faded into its head.</summary>
    public const double CrossfadeSeconds = 1.0;

    /// <summary>The peak of a rendered loop, as a fraction of full scale (the mixer's bed gains set the level).</summary>
    public const double Peak = 0.7;

    private const int Rate = SoundSynth.SampleRate;

    /// <summary>The beds that loop (the others are scheduled one-shots over a weight).</summary>
    public static IReadOnlyList<string> Looping { get; } = [AmbientBeds.Wind, AmbientBeds.River, AmbientBeds.Waterfall, AmbientBeds.Village, AmbientBeds.Insects];

    /// <summary>Renders the loop of a bed as 16-bit mono samples of <see cref="LoopSeconds"/> at <see cref="SoundSynth.SampleRate"/>.</summary>
    /// <param name="bed">The bed's id.</param>
    /// <exception cref="ArgumentException">The bed does not loop.</exception>
    public static short[] Render(string bed)
    {
        var length = (int)Math.Round(LoopSeconds * Rate);
        var tail = (int)Math.Round(CrossfadeSeconds * Rate);
        var raw = bed switch
        {
            AmbientBeds.Wind => Wind(length + tail),
            AmbientBeds.River => River(length + tail),
            AmbientBeds.Waterfall => Waterfall(length + tail),
            AmbientBeds.Village => Village(length + tail),
            AmbientBeds.Insects => Insects(length + tail),
            _ => throw new ArgumentException($"The bed '{bed}' does not loop.", nameof(bed)),
        };
        return Seamless(raw, length, tail);
    }

    /// <summary>
    /// Cuts a loop of <paramref name="length"/> samples from <paramref name="raw"/> (which holds <paramref name="tail"/> extra samples): the head fades from the
    /// tail with an equal-power curve, so the loop's end meets its start on the sound's own continuation.
    /// </summary>
    public static short[] Seamless(float[] raw, int length, int tail)
    {
        var loop = new float[length];
        Array.Copy(raw, tail, loop, tail, length - tail); // the rest of the loop, unchanged: loop[i] is raw[i] from the tail on
        for (var i = 0; i < tail; i++)
        {
            var fade = i / (double)tail;
            var toHead = Math.Sin(Math.PI / 2 * fade);
            var toTail = Math.Cos(Math.PI / 2 * fade);
            loop[i] = (float)((raw[i] * toHead) + (raw[length + i] * toTail));
        }

        var peak = 0f;
        foreach (var sample in loop)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }

        var scale = peak <= 0 ? 0 : (float)(Peak / peak);
        var result = new short[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = (short)Math.Round(loop[i] * scale * short.MaxValue);
        }

        return result;
    }

    // The wind: low-passed noise that gusts (three slow swells) with a thinner, brighter whistle above it.
    private static float[] Wind(int n)
    {
        var rng = new PresentationRng(SoundSynth.Seed ^ 0x57_1D_0001UL);
        var body = new float[n];
        var low = 0.0;
        var high = 0.0;
        var lowAlpha = 1 - Math.Exp(-2 * Math.PI * 160 / Rate);
        var highAlpha = 1 - Math.Exp(-2 * Math.PI * 900 / Rate);
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)Rate;
            var noise = (rng.NextDouble() * 2) - 1;
            low += lowAlpha * (noise - low);
            high += highAlpha * (noise - high);
            var gust = 0.55 + (0.3 * Math.Sin((2 * Math.PI * 0.061 * t) + 0.7)) + (0.15 * Math.Sin((2 * Math.PI * 0.173 * t) + 2.1));
            var whistle = 0.5 + (0.5 * Math.Sin((2 * Math.PI * 0.11 * t) + 0.3));
            body[i] = (float)((low * 2.2 * gust) + ((high - low) * 0.25 * whistle));
        }

        return body;
    }

    // The river: a broad, low-passed rush, with a slow change in its flow and a little hiss on top.
    private static float[] River(int n)
    {
        var rng = new PresentationRng(SoundSynth.Seed ^ 0x51_7E_0002UL);
        var body = new float[n];
        var low = 0.0;
        var hiss = 0.0;
        var lowAlpha = 1 - Math.Exp(-2 * Math.PI * 1100 / Rate);
        var hissAlpha = 1 - Math.Exp(-2 * Math.PI * 3200 / Rate);
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)Rate;
            var noise = (rng.NextDouble() * 2) - 1;
            low += lowAlpha * (noise - low);
            hiss += hissAlpha * (noise - hiss);
            var flow = 0.75 + (0.25 * Math.Sin((2 * Math.PI * 0.37 * t) + 0.4)) + (0.1 * Math.Sin((2 * Math.PI * 1.3 * t) + 1.0));
            body[i] = (float)((low * 1.4 * flow) + ((noise - hiss) * 0.12));
        }

        return body;
    }

    // The waterfall: a heavier rumble under a band of falling water, louder than the river, with a slow pulse in its fall.
    private static float[] Waterfall(int n)
    {
        var rng = new PresentationRng(SoundSynth.Seed ^ 0x3A_11_0003UL);
        var body = new float[n];
        var rumble = 0.0;
        var band = 0.0;
        var rumbleAlpha = 1 - Math.Exp(-2 * Math.PI * 260 / Rate);
        var bandAlpha = 1 - Math.Exp(-2 * Math.PI * 2600 / Rate);
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)Rate;
            var noise = (rng.NextDouble() * 2) - 1;
            rumble += rumbleAlpha * (noise - rumble);
            band += bandAlpha * (noise - band);
            var fall = 0.85 + (0.15 * Math.Sin((2 * Math.PI * 0.9 * t) + 0.2));
            body[i] = (float)(((rumble * 2.4) + (band * 0.5)) * fall);
        }

        return body;
    }

    // The village: a murmur of voices (a band of low-passed noise between about 250 and 800 hertz), swelling and easing in slow turns.
    private static float[] Village(int n)
    {
        var rng = new PresentationRng(SoundSynth.Seed ^ 0x71_11_0004UL);
        var body = new float[n];
        var low = 0.0;
        var mid = 0.0;
        var lowAlpha = 1 - Math.Exp(-2 * Math.PI * 800 / Rate);
        var midAlpha = 1 - Math.Exp(-2 * Math.PI * 250 / Rate);
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)Rate;
            var noise = (rng.NextDouble() * 2) - 1;
            low += lowAlpha * (noise - low);
            mid += midAlpha * (noise - mid);
            var swell = 0.5 + (0.3 * Math.Sin((2 * Math.PI * 0.19 * t) + 1.1)) + (0.2 * Math.Sin((2 * Math.PI * 0.047 * t) + 0.5));
            body[i] = (float)((low - mid) * 2.5 * swell);
        }

        return body;
    }

    // The evening insects: two steady whirrs (a high and a lower tone), each pulsed at its own rate, over a little noise of the grass.
    private static float[] Insects(int n)
    {
        var rng = new PresentationRng(SoundSynth.Seed ^ 0x1E_C7_0005UL);
        var body = new float[n];
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)Rate;
            var high = Math.Sin(2 * Math.PI * 4700 * t) * Math.Pow(0.5 + (0.5 * Math.Cos(2 * Math.PI * 17 * t)), 4);
            var lower = Math.Sin((2 * Math.PI * 3900 * t) + 0.5) * Math.Pow(0.5 + (0.5 * Math.Cos((2 * Math.PI * 23 * t) + 1.0)), 4);
            var grass = ((rng.NextDouble() * 2) - 1) * 0.03;
            body[i] = (float)((high * 0.4) + (lower * 0.25) + grass);
        }

        return body;
    }
}
