using CsCheck;
using Slg.Audio;
using Slg.Core.Audio;
using Slg.Core.Determinism;
using Slg.Fuzzing.Tests.Generation;
using Slg.Fuzzing.Tests.Oracles;
using Xunit.Abstractions;

namespace Slg.Fuzzing.Tests;

/// <summary>
/// O13: the audio front-end. <c>WavFile.Read</c> throws only <see cref="InvalidDataException"/> or <see cref="NotSupportedException"/> on damaged
/// bytes (the entries of <see cref="KnownTolerances"/>), and anything it accepts is a well-formed clip the front-end can process. A valid clip reads
/// back exactly, survives write and read, and comes out of <c>AudioFrontEnd.Process</c> as a 16 kHz mono clip that agrees with the stages
/// composed by hand: the downmix rule, a resampled length of <c>floor(n * up / down)</c>, a gain that never exceeds the target peak, and a voice
/// activity range that is a contiguous slice of the normalized signal. The hash is stable across runs.
/// </summary>
public sealed class AudioFrontEndFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O13";

    // A corrupted sample-rate field can request a resampling kernel of millions of taps (the kernel grows with the source rate, and every output
    // sample may need its own phase of it); the front-end oracle only processes accepted damaged files up to the highest rate in common use.
    private const int MaxProcessedRate = 192_000;

    [Fact]
    public void DamagedWavFiles_AreRejectedWithTheDocumentedExceptions_OrDecodeToAUsableClip()
    {
        var diagnostics = new FuzzDiagnostics();
        var iterations = FuzzOptions.Iterations(FuzzOptions.SignalVar, FuzzOptions.SignalDefault);

        AudioGen.Mutation().Sample(
            mutation =>
            {
                var bytes = mutation.Apply();
                WavData wav;
                try
                {
                    wav = WavFile.Read(new MemoryStream(bytes));
                }
                catch (Exception ex) when (KnownTolerances.IsWavRejection(ex))
                {
                    diagnostics.Count($"{mutation.Kind}:rejected:{ex.GetType().Name}");
                    return;
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(Oracle, $"WavFile.Read threw {ex.GetType().Name} on damaged bytes: {ex.Message}", mutation.Describe());
                }

                diagnostics.Count($"{mutation.Kind}:accepted");
                if (wav.Channels is not (1 or 2) || wav.SampleRate <= 0 || wav.InterleavedSamples.Length % wav.Channels != 0)
                {
                    throw Determinism.Fail(Oracle, $"WavFile.Read accepted the file but returned a malformed clip: rate {wav.SampleRate}, channels {wav.Channels}, {wav.InterleavedSamples.Length} samples.", mutation.Describe());
                }

                // Whatever was accepted must write and read back unchanged, and a sane clip must run through the front-end.
                RoundTrip(wav, mutation.Describe());
                if (wav.SampleRate is >= 1000 and <= MaxProcessedRate)
                {
                    CheckFrontEnd(wav, null, mutation.Describe(), diagnostics);
                }
                else
                {
                    diagnostics.Count("skip:absurd-sample-rate");
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Damaged WAV diagnostics:\n" + diagnostics.Report());
        foreach (var kind in Enum.GetValues<WavMutationKind>())
        {
            Assert.True(diagnostics.Sum($"{kind}:") > 0, $"The generator never produced {kind}.\n{diagnostics.Report()}");
        }

        Assert.True(diagnostics.Sum("Truncate:rejected") > 0 && diagnostics.Sum("Truncate:accepted") > 0, $"Truncation must reach both outcomes.\n{diagnostics.Report()}");
        var kinds = Enum.GetValues<WavMutationKind>();
        Assert.True(
            kinds.Sum(kind => diagnostics.Sum($"{kind}:rejected")) > 0 && kinds.Sum(kind => diagnostics.Get($"{kind}:accepted")) > 0,
            $"Both outcomes of the reader must be reached.\n{diagnostics.Report()}");
    }

    [Fact]
    public void ValidClips_ReadBackExactly_AndProcessIntoTheComposedFrontEnd()
    {
        var diagnostics = new FuzzDiagnostics();
        var iterations = FuzzOptions.Iterations(FuzzOptions.SignalVar, FuzzOptions.SignalDefault);

        AudioGen.MixedClip().Sample(
            clip =>
            {
                var description = clip.Describe();
                var bytes = clip.Bytes.Data;

                WavData read;
                try
                {
                    read = WavFile.Read(new MemoryStream(bytes));
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(Oracle, $"WavFile.Read threw {ex.GetType().Name} on a valid {clip.Spec.Encoding} file with a {clip.Spec.Layout} header: {ex.Message}", description);
                }

                if (!read.Equals(clip.Wav))
                {
                    throw Determinism.Fail(Oracle, $"A valid file did not read back to the samples it encodes (rate {read.SampleRate}, channels {read.Channels}, {read.InterleavedSamples.Length} samples).", description);
                }

                RoundTrip(read, description);
                CheckFrontEnd(read, clip.Spec, description, diagnostics);
                diagnostics.Count($"signal:{clip.Spec.Signal}");
                diagnostics.Count($"rate:{clip.Spec.SampleRate}");
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Audio generator diagnostics:\n" + diagnostics.Report());
        foreach (var signal in Enum.GetValues<SignalKind>())
        {
            Assert.True(diagnostics.Get($"signal:{signal}") >= 20, $"The generator no longer reaches {signal}.\n{diagnostics.Report()}");
        }

        foreach (var rate in AudioGen.SampleRates)
        {
            Assert.True(diagnostics.Get($"rate:{rate}") >= 20, $"The generator no longer reaches {rate} Hz.\n{diagnostics.Report()}");
        }

        Assert.True(diagnostics.Get("clip:speech") > 0 && diagnostics.Get("clip:no-speech") > 0, $"Both voice-activity outcomes must be reached.\n{diagnostics.Report()}");
    }

    private static void RoundTrip(WavData wav, string description)
    {
        byte[] first;
        using (var stream = new MemoryStream())
        {
            WavFile.Write(stream, wav);
            first = stream.ToArray();
        }

        byte[] second;
        using (var stream = new MemoryStream())
        {
            WavFile.Write(stream, wav);
            second = stream.ToArray();
        }

        if (!first.AsSpan().SequenceEqual(second) || first.Length != 44 + (wav.InterleavedSamples.Length * 2))
        {
            throw Determinism.Fail(Oracle, "WavFile.Write is not deterministic or wrote a file of the wrong size.", description);
        }

        var back = WavFile.Read(new MemoryStream(first));
        if (!back.Equals(wav))
        {
            throw Determinism.Fail(Oracle, "Write followed by Read does not return the same clip.", description);
        }
    }

    // The front-end against the stages composed by hand. spec is null for a damaged-but-accepted file (no expectation about the content).
    private static void CheckFrontEnd(WavData wav, AudioSpec? spec, string description, FuzzDiagnostics diagnostics)
    {
        PcmClip clip;
        try
        {
            clip = AudioFrontEnd.Process(wav);
        }
        catch (Exception ex)
        {
            throw Determinism.Fail(Oracle, $"AudioFrontEnd.Process threw {ex.GetType().Name}: {ex.Message}", description);
        }

        // Stage by stage, composed by hand.
        var mono = Downmixer.ToMono(wav);
        if (mono.Length != wav.FrameCount)
        {
            throw Determinism.Fail(Oracle, $"The downmix has {mono.Length} samples for {wav.FrameCount} frames.", description);
        }

        if (wav.Channels == 2)
        {
            for (var i = 0; i < mono.Length; i += Math.Max(1, mono.Length / 64))
            {
                if (mono[i] != (short)((wav.InterleavedSamples[2 * i] + wav.InterleavedSamples[(2 * i) + 1]) >> 1))
                {
                    throw Determinism.Fail(Oracle, $"Downmix frame {i} is not (L + R) >> 1.", description);
                }
            }
        }

        var resampled = Resampler.To16k(mono, wav.SampleRate);
        var divisor = Gcd(wav.SampleRate, Resampler.TargetRate);
        var expectedLength = (long)mono.Length * (Resampler.TargetRate / divisor) / (wav.SampleRate / divisor);
        if (resampled.Length != expectedLength)
        {
            throw Determinism.Fail(Oracle, $"The resampled clip has {resampled.Length} samples, expected floor(n * up / down) = {expectedLength} for {mono.Length} frames at {wav.SampleRate} Hz.", description);
        }

        var options = AudioFrontEndOptions.Default;
        var normalized = GainNormalizer.Normalize(resampled, options.TargetPeakDbfs, options.MaxGainDb);
        CheckGain(resampled, normalized, options, description);

        var vad = new EnergyVad(options.FrameMs, options.VadThresholdDbfs, options.HangoverFrames).Trim(normalized);
        if (clip.SampleRate != PcmClip.DefaultSampleRate)
        {
            throw Determinism.Fail(Oracle, $"The clip is {clip.SampleRate} Hz, not 16 kHz.", description);
        }

        if (!clip.Samples.AsSpan().SequenceEqual(vad.Samples))
        {
            throw Determinism.Fail(Oracle, $"AudioFrontEnd.Process ({clip.Samples.Length} samples) disagrees with the stages composed by hand ({vad.Samples.Length} samples).", description);
        }

        if (vad.HasSpeech)
        {
            var from = vad.SpeechStartFrame * options.FrameMs * clip.SampleRate / 1000;
            if (from < 0 || from + vad.Samples.Length > normalized.Length || !normalized.AsSpan(from, vad.Samples.Length).SequenceEqual(vad.Samples))
            {
                throw Determinism.Fail(Oracle, "The voice-activity output is not a contiguous slice of the normalized signal.", description);
            }

            diagnostics.Count("clip:speech");
        }
        else
        {
            if (clip.Samples.Length != 0)
            {
                throw Determinism.Fail(Oracle, "A recording without speech must give an empty clip.", description);
            }

            diagnostics.Count("clip:no-speech");
        }

        if (!string.Equals(clip.ClipHash, DeterministicHash.OfSamples(clip.Samples), StringComparison.Ordinal))
        {
            throw Determinism.Fail(Oracle, "The clip hash is not the hash of its samples.", description);
        }

        // The composed stages are a second, independent run over the same input: the clip and its hash must be identical.
        Determinism.AssertSame(
            Oracle,
            "AudioFrontEnd.Process (against the stages composed by hand)",
            description,
            () => Summary(clip),
            () => Summary(PcmClip.FromSamples(vad.Samples)));

        if (spec is not null)
        {
            CheckSemantics(spec, resampled, clip, description);
        }
    }

    // What the content of a clip says about the outcome, for the cases where it is unambiguous.
    private static void CheckSemantics(AudioSpec spec, short[] resampled, PcmClip clip, string description)
    {
        if (resampled.All(static s => s == 0) && clip.Samples.Length != 0)
        {
            throw Determinism.Fail(Oracle, "A silent signal produced a non-empty clip.", description);
        }

        var audible = spec.Signal == SignalKind.Sine
            && spec.Amplitude >= 0.02
            && spec.Milliseconds >= 100
            && spec.FrequencyHz is >= 80.0 and <= 6000.0
            && spec.FrequencyHz < 0.4 * spec.SampleRate
            && !(spec.Channels == 2 && spec.StereoMode == 1);
        if (audible && clip.Samples.Length == 0)
        {
            throw Determinism.Fail(Oracle, "A loud sine wave of at least 100 ms was not detected as speech.", description);
        }

        if (clip.Samples.Length > resampled.Length)
        {
            throw Determinism.Fail(Oracle, "The clip is longer than the resampled signal.", description);
        }
    }

    private static void CheckGain(short[] resampled, short[] normalized, AudioFrontEndOptions options, string description)
    {
        var peakBefore = resampled.Length == 0 ? 0 : resampled.Max(static s => Math.Abs((int)s));
        var peakAfter = normalized.Length == 0 ? 0 : normalized.Max(static s => Math.Abs((int)s));
        var target = GainNormalizer.FullScale * Math.Pow(10.0, options.TargetPeakDbfs / 20.0);
        var cap = Math.Pow(10.0, options.MaxGainDb / 20.0);

        // The output peak is the target (or less when the gain cap applies); rounding to the nearest sample can add one step.
        if (peakAfter > Math.Ceiling(target) + 1)
        {
            throw Determinism.Fail(Oracle, $"The gain stage pushed the peak to {peakAfter}, above the target {target:F1} (input peak {peakBefore}).", description);
        }

        if (peakAfter > Math.Ceiling(peakBefore * cap) + 1)
        {
            throw Determinism.Fail(Oracle, $"The gain stage amplified the peak from {peakBefore} to {peakAfter}, more than the {options.MaxGainDb} dB cap.", description);
        }

        if (peakBefore == 0 && peakAfter != 0)
        {
            throw Determinism.Fail(Oracle, "The gain stage made sound out of silence.", description);
        }
    }

    private static string Summary(PcmClip clip)
    {
        return $"{clip.SampleRate}/{clip.Samples.Length}/{clip.ClipHash}";
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }
}
