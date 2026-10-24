using Vocaterra.MonoGame.Utilities;

namespace Vocaterra.MonoGame.Sound;

/// <summary>
/// The synthesized takes of S25: the footsteps that no CC0 pack in the catalogue covers (dirt, gravel, sand and the splash in water), the birds' chirps and
/// the fire's crackle. Each is a float buffer at <see cref="SoundSynth.SampleRate"/> made from a fixed recipe and a generator seeded by the caller; no clock, no
/// <c>System.Random</c>. The buffers start and end at silence (no click).
/// </summary>
public static class SoundTextures
{
    private const int Rate = SoundSynth.SampleRate;

    /// <summary>A dirt step: a soft thud, a low-passed body and a short low tone.</summary>
    public static float[] Dirt(PresentationRng rng)
    {
        var buffer = SoundSynth.Silence(0.32);
        var low = 0.0;
        var alpha = 1 - Math.Exp(-2 * Math.PI * 420 / Rate);
        for (var i = 0; i < buffer.Length; i++)
        {
            var t = i / (double)Rate;
            low += alpha * (Noise(rng) - low);
            var envelope = Math.Min(1.0, t / 0.004) * Math.Exp(-t * 16);
            var thump = Math.Sin(2 * Math.PI * 95 * t) * Math.Exp(-t * 22) * 0.8;
            buffer[i] = (float)(((low * 1.2) + thump) * envelope * 0.9);
        }

        return buffer;
    }

    /// <summary>A gravel step: a handful of small grains that crunch within a third of a second, over a low rumble.</summary>
    public static float[] Gravel(PresentationRng rng)
    {
        var buffer = SoundSynth.Silence(0.45);
        var grains = 6 + rng.NextInt(3);
        var low = 0.0;
        var alpha = 1 - Math.Exp(-2 * Math.PI * 2600 / Rate);
        var lowAlpha = 1 - Math.Exp(-2 * Math.PI * 700 / Rate);
        var grainStarts = new double[grains];
        for (var g = 0; g < grains; g++)
        {
            grainStarts[g] = rng.NextBetween(0.0, 0.3);
        }

        var grainLevel = new double[grains];
        for (var g = 0; g < grains; g++)
        {
            grainLevel[g] = rng.NextBetween(0.4, 0.9);
        }

        var lowBody = 0.0;
        for (var i = 0; i < buffer.Length; i++)
        {
            var t = i / (double)Rate;
            var noise = Noise(rng);
            low += alpha * (noise - low);
            var highPassed = noise - low; // the crunch is the high part of the noise
            var sum = 0.0;
            for (var g = 0; g < grains; g++)
            {
                var since = t - grainStarts[g];
                if (since >= 0)
                {
                    sum += grainLevel[g] * Math.Exp(-since * 260);
                }
            }

            lowBody += lowAlpha * (noise - lowBody);
            var rumble = lowBody * Math.Exp(-t * 12) * 0.5;
            var envelope = Math.Min(1.0, t / 0.003) * Math.Min(1.0, (buffer.Length - i) / (double)(Rate / 100));
            buffer[i] = (float)(((highPassed * sum * 1.4) + rumble) * envelope);
        }

        return buffer;
    }

    /// <summary>A sand step: a soft swish of high noise that swells and falls away.</summary>
    public static float[] Sand(PresentationRng rng)
    {
        var buffer = SoundSynth.Silence(0.4);
        var low = 0.0;
        var alpha = 1 - Math.Exp(-2 * Math.PI * 1800 / Rate);
        for (var i = 0; i < buffer.Length; i++)
        {
            var t = i / (double)Rate;
            var noise = Noise(rng);
            low += alpha * (noise - low);
            var swell = Math.Pow(Math.Sin(Math.PI * Math.Min(1.0, t / 0.4)), 2);
            buffer[i] = (float)((noise * 0.35) + (low * 0.6)) * (float)(swell * 0.8);
        }

        return buffer;
    }

    /// <summary>A splash of the feet in water: a few bubbles that rise, over a short hiss.</summary>
    public static float[] Splash(PresentationRng rng)
    {
        var buffer = SoundSynth.Silence(0.6);
        var low = 0.0;
        var alpha = 1 - Math.Exp(-2 * Math.PI * 1300 / Rate);
        for (var i = 0; i < buffer.Length; i++)
        {
            var t = i / (double)Rate;
            low += alpha * (Noise(rng) - low);
            buffer[i] = (float)(low * Math.Exp(-t * 7) * Math.Min(1.0, t / 0.002) * 0.7);
        }

        for (var b = 0; b < 5; b++)
        {
            var start = rng.NextBetween(0.02, 0.4);
            var frequency = rng.NextBetween(380, 900);
            var first = (int)(start * Rate);
            var count = (int)(0.035 * Rate);
            for (var i = 0; i < count && first + i < buffer.Length; i++)
            {
                var t = i / (double)Rate;
                var envelope = Math.Sin(Math.PI * i / count);
                buffer[first + i] += (float)(Math.Sin(2 * Math.PI * (frequency + (t * 1600)) * t) * envelope * 0.35);
            }
        }

        return buffer;
    }

    /// <summary>A bird's chirp: a short whistle that sweeps up and down, one to three notes; each take has its own pitch and its own notes.</summary>
    public static float[] Chirp(int take)
    {
        var buffer = SoundSynth.Silence(0.5);
        var baseFrequency = 2300 + (take * 340);
        var notes = 1 + (take % 3);
        var phase = 0.0;
        for (var n = 0; n < notes; n++)
        {
            const double length = 0.11;
            var start = n * 0.15;
            var first = (int)(start * Rate);
            var count = (int)(length * Rate);
            phase = 0;
            for (var i = 0; i < count && first + i < buffer.Length; i++)
            {
                var tau = i / (double)count;
                var frequency = baseFrequency * (1 + (0.35 * Math.Sin(Math.PI * tau))) * (1 + (0.05 * n));
                phase += 2 * Math.PI * frequency / Rate;
                var envelope = Math.Pow(Math.Sin(Math.PI * tau), 1.5);
                buffer[first + i] += (float)(Math.Sin(phase) * envelope * 0.8);
            }
        }

        return buffer;
    }

    /// <summary>The fire's crackle: a few sharp snaps of high noise, each a few milliseconds long, over a faint rumble.</summary>
    public static float[] Crackle(PresentationRng rng)
    {
        var buffer = SoundSynth.Silence(0.5);
        var snaps = 5 + rng.NextInt(4);
        for (var s = 0; s < snaps; s++)
        {
            var start = rng.NextBetween(0.02, 0.42);
            var level = rng.NextBetween(0.3, 1.0);
            var first = (int)(start * Rate);
            var count = (int)(0.02 * Rate);
            var high = 0.0;
            var alpha = 1 - Math.Exp(-2 * Math.PI * 1100 / Rate);
            for (var i = 0; i < count && first + i < buffer.Length; i++)
            {
                var t = i / (double)Rate;
                var noise = Noise(rng);
                high += alpha * (noise - high);
                buffer[first + i] += (float)((noise - high) * Math.Exp(-t * 330) * level);
            }
        }

        var low = 0.0;
        var lowAlpha = 1 - Math.Exp(-2 * Math.PI * 280 / Rate);
        for (var i = 0; i < buffer.Length; i++)
        {
            low += lowAlpha * (Noise(rng) - low);
            buffer[i] += (float)(low * 0.25 * Math.Min(1.0, i / (double)(Rate / 50)) * Math.Min(1.0, (buffer.Length - i) / (double)(Rate / 50)));
        }

        return buffer;
    }

    private static double Noise(PresentationRng rng) => (rng.NextDouble() * 2) - 1;
}
