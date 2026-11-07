using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.Sound;

/// <summary>
/// The cues made in code (S24, D-game-18): the push-to-talk beeps, the progress chimes, the sparkle, the bottle and the fire's whoosh. They are rendered at load
/// from fixed recipes and a fixed seed, so the same bytes come out on every machine and in every run; no file is read, no random class and no clock is used.
/// </summary>
public static class SoundSynth
{
    /// <summary>The sample rate of every synthesized cue (22.05 kHz, mono, 16 bit).</summary>
    public const int SampleRate = 22050;

    /// <summary>The seed of the noise of the synthesized cues (the sparkle's pitches, the whoosh's air).</summary>
    public const ulong Seed = 0x5EED_0524UL;

    private const double Peak = 0.8;

    /// <summary>Whether the cue is synthesized (otherwise it plays a file of the catalogue).</summary>
    /// <param name="cue">The cue.</param>
    public static bool IsSynthesized(SoundCue cue) => cue is SoundCue.PushToTalkStart or SoundCue.PushToTalkStop or SoundCue.StepDone or SoundCue.UnitDone
        or SoundCue.LessonDone or SoundCue.AnswerUnderstood or SoundCue.AnswerNeutral or SoundCue.WordLearnt or SoundCue.ObjectiveDone or SoundCue.SessionDone
        or SoundCue.BottleFill or SoundCue.FireLight or SoundCue.FootDirt or SoundCue.FootGravel or SoundCue.FootSand or SoundCue.FootWater
        or SoundCue.BirdChirp or SoundCue.FireCrackle or SoundCue.WaterSplash or SoundCue.UnderwaterBubbles;

    /// <summary>The length in seconds of the push-to-talk start beep (the recording discards this much of its start, so the beep never reaches the recognizer).</summary>
    public const double StartBeepSeconds = 0.09;

    /// <summary>Renders a synthesized cue as 16-bit mono samples at <see cref="SampleRate"/>.</summary>
    /// <param name="cue">The cue.</param>
    /// <param name="take">The take (S25: the footsteps, birds and crackle have several); 0 for the only take of the others.</param>
    /// <exception cref="ArgumentException">The cue is a file of the catalogue.</exception>
    public static short[] Render(SoundCue cue, int take = 0)
    {
        var rng = new PresentationRng(Seed ^ (ulong)cue ^ ((ulong)take << 32));
        var buffer = cue switch
        {
            SoundCue.FootDirt => SoundTextures.Dirt(rng),
            SoundCue.FootGravel => SoundTextures.Gravel(rng),
            SoundCue.FootSand => SoundTextures.Sand(rng),
            SoundCue.FootWater => SoundTextures.Splash(rng),
            SoundCue.BirdChirp => SoundTextures.Chirp(take),
            SoundCue.FireCrackle => SoundTextures.Crackle(rng),
            SoundCue.WaterSplash => SoundTextures.Plunge(rng),
            SoundCue.UnderwaterBubbles => SoundTextures.Bubble(rng, take),
            SoundCue.PushToTalkStart => Beep(880, StartBeepSeconds, 0.6),
            SoundCue.PushToTalkStop => Beep(587.33, 0.09, 0.6),
            SoundCue.StepDone => Bell(659.25, 0.5),
            SoundCue.UnitDone => Notes([(784.0, 0.0), (1046.5, 0.11)], 0.8, 0.55),
            SoundCue.LessonDone => Notes([(523.25, 0.0), (659.25, 0.1), (783.99, 0.2), (1046.5, 0.3)], 1.3, 0.5),
            SoundCue.AnswerUnderstood => Notes([(523.25, 0.0), (659.25, 0.08)], 0.45, 0.5),
            SoundCue.AnswerNeutral => Soft(392.0, 0.28),
            SoundCue.WordLearnt => Sparkle(rng),
            SoundCue.ObjectiveDone => Notes([(523.25, 0.0), (659.25, 0.0), (783.99, 0.0)], 1.0, 0.45),
            SoundCue.SessionDone => Notes([(261.63, 0.0), (392.0, 0.24), (523.25, 0.48), (659.25, 0.72), (783.99, 0.96)], 2.4, 0.5),
            SoundCue.BottleFill => Bubbles(rng),
            SoundCue.FireLight => Whoosh(rng),
            _ => throw new ArgumentException($"The cue {cue} is a file of the catalogue, not a synthesized sound.", nameof(cue)),
        };
        return Normalize(buffer);
    }

    internal static float[] Silence(double seconds) => new float[(int)(seconds * SampleRate)];

    // A sine (with partials) with a quick attack and an exponential decay, added into the buffer at the offset.
    private static void AddTone(float[] buffer, double frequency, double start, double length, double level, double decay, double[] partials)
    {
        var first = (int)(start * SampleRate);
        var count = Math.Min((int)(length * SampleRate), buffer.Length - first);
        for (var i = 0; i < count; i++)
        {
            var t = i / (double)SampleRate;
            var attack = Math.Min(1.0, t / 0.006);
            var envelope = attack * Math.Exp(-t * decay);
            var sample = 0.0;
            for (var p = 0; p < partials.Length; p++)
            {
                sample += partials[p] * Math.Sin(2 * Math.PI * frequency * (p + 1) * t);
            }

            buffer[first + i] += (float)(sample * envelope * level);
        }
    }

    private static float[] Beep(double frequency, double seconds, double level)
    {
        var buffer = Silence(seconds);
        for (var i = 0; i < buffer.Length; i++)
        {
            var t = i / (double)SampleRate;
            var envelope = Math.Min(1.0, t / 0.008) * Math.Min(1.0, (seconds - t) / 0.02); // a click-free edge at both ends
            buffer[i] = (float)(Math.Sin(2 * Math.PI * frequency * t) * envelope * level);
        }

        return buffer;
    }

    private static float[] Bell(double frequency, double seconds)
    {
        var buffer = Silence(seconds);
        AddTone(buffer, frequency, 0, seconds, 0.6, 6.5, [1.0, 0.3, 0.1]);
        return buffer;
    }

    private static float[] Soft(double frequency, double seconds)
    {
        // The neutral cue: one low, round tone with a slow attack and a short tail; no rise, no fall, nothing that reads as "wrong".
        var buffer = Silence(seconds);
        for (var i = 0; i < buffer.Length; i++)
        {
            var t = i / (double)SampleRate;
            var envelope = Math.Min(1.0, t / 0.03) * Math.Min(1.0, (seconds - t) / 0.12);
            buffer[i] = (float)(((Math.Sin(2 * Math.PI * frequency * t) * 0.8) + (Math.Sin(2 * Math.PI * frequency * 2 * t) * 0.1)) * envelope * 0.35);
        }

        return buffer;
    }

    private static float[] Notes((double Frequency, double Start)[] notes, double seconds, double level)
    {
        var buffer = Silence(seconds);
        foreach (var (frequency, start) in notes)
        {
            AddTone(buffer, frequency, start, seconds - start, level, 4.5, [1.0, 0.35, 0.12]);
        }

        return buffer;
    }

    private static float[] Sparkle(PresentationRng rng)
    {
        double[] scale = [1046.5, 1174.66, 1318.51, 1567.98, 1760.0, 2093.0];
        var buffer = Silence(0.7);
        for (var i = 0; i < 7; i++)
        {
            var frequency = scale[rng.NextInt(scale.Length)];
            AddTone(buffer, frequency, i * 0.05, 0.3, 0.3, 13.0, [1.0, 0.2]);
        }

        return buffer;
    }

    private static float[] Bubbles(PresentationRng rng)
    {
        // A glug: short sines that chirp upward, rising in pitch as the bottle fills.
        var buffer = Silence(0.75);
        for (var b = 0; b < 6; b++)
        {
            var start = (b * 0.1) + (rng.NextDouble() * 0.02);
            var baseFrequency = 320 + (b * 55) + (rng.NextDouble() * 20);
            var first = (int)(start * SampleRate);
            var count = (int)(0.07 * SampleRate);
            for (var i = 0; i < count && first + i < buffer.Length; i++)
            {
                var t = i / (double)SampleRate;
                var frequency = baseFrequency * (1 + (t * 9));
                var envelope = Math.Sin(Math.PI * i / count);
                buffer[first + i] += (float)(Math.Sin(2 * Math.PI * frequency * t) * envelope * 0.5);
            }
        }

        return buffer;
    }

    private static float[] Whoosh(PresentationRng rng)
    {
        // Air through a low-pass filter whose cut-off opens and closes: a soft rush with a low body.
        const double seconds = 0.85;
        var buffer = Silence(seconds);
        var low = 0.0;
        for (var i = 0; i < buffer.Length; i++)
        {
            var t = i / (double)SampleRate;
            var swell = Math.Pow(Math.Sin(Math.PI * Math.Min(1.0, t / seconds)), 1.5);
            var cutoff = 250 + (3200 * swell);
            var alpha = 1 - Math.Exp(-2 * Math.PI * cutoff / SampleRate);
            var noise = (rng.NextDouble() * 2) - 1;
            low += alpha * (noise - low);
            var body = Math.Sin(2 * Math.PI * 70 * t) * 0.15;
            buffer[i] = (float)((low + body) * swell);
        }

        return buffer;
    }

    internal static short[] Normalize(float[] buffer)
    {
        var peak = 0f;
        foreach (var sample in buffer)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }

        var scale = peak <= 0 ? 0 : (float)(Peak / peak);
        var result = new short[buffer.Length];
        var fade = Math.Min(buffer.Length, SampleRate / 100); // the last 10 ms ramp to zero: a cue never ends on a click
        for (var i = 0; i < buffer.Length; i++)
        {
            var tail = Math.Min(1.0, (buffer.Length - 1 - i) / (double)fade);
            result[i] = (short)Math.Round(buffer[i] * scale * short.MaxValue * tail);
        }

        return result;
    }
}
