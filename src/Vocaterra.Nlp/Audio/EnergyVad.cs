using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Nlp.Audio;

/// <summary>
/// Energy-based voice activity detector that trims leading and trailing silence.
/// <para>
/// The signal is cut into frames of <see cref="FrameMs"/> milliseconds (the last frame may be shorter). A frame is
/// speech when its RMS level, in dBFS relative to 32 768, is at or above <see cref="ThresholdDbfs"/>. The decision
/// is evaluated without logarithms as <c>sum(sample^2) &gt;= 10^(threshold/10) * 32768^2 * frameLength</c>, using an
/// exact integer sum, so it is bit-identical on every platform. Speech starts at the first speech frame and ends
/// <see cref="HangoverFrames"/> frames after the last speech frame (capped at the end of the signal); there is no
/// leading padding.
/// </para>
/// </summary>
public sealed class EnergyVad
{
    private const double FullScaleSquared = 32768.0 * 32768.0;
    private readonly double _meanSquareThreshold;

    /// <summary>Creates a detector for audio at <paramref name="sampleRate"/> hertz (16 kHz by default).</summary>
    public EnergyVad(int frameMs = 20, double thresholdDbfs = -40.0, int hangoverFrames = 5, int sampleRate = PcmClip.DefaultSampleRate)
    {
        if (frameMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameMs), frameMs, "Frame length must be positive.");
        }

        if (!double.IsFinite(thresholdDbfs))
        {
            throw new ArgumentOutOfRangeException(nameof(thresholdDbfs), thresholdDbfs, "Threshold must be finite.");
        }

        if (hangoverFrames < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hangoverFrames), hangoverFrames, "Hangover must not be negative.");
        }

        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Sample rate must be positive.");
        }

        FrameSamples = checked((int)((long)sampleRate * frameMs / 1000));
        if (FrameSamples <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameMs), frameMs, "Frame is shorter than one sample.");
        }

        FrameMs = frameMs;
        ThresholdDbfs = thresholdDbfs;
        HangoverFrames = hangoverFrames;
        SampleRate = sampleRate;
        _meanSquareThreshold = DeterministicMath.DbToPower(thresholdDbfs) * FullScaleSquared;
    }

    /// <summary>Frame length in milliseconds.</summary>
    public int FrameMs { get; }

    /// <summary>RMS level in dBFS at or above which a frame counts as speech.</summary>
    public double ThresholdDbfs { get; }

    /// <summary>Number of frames kept after the last speech frame.</summary>
    public int HangoverFrames { get; }

    /// <summary>Sample rate the frame length was derived from, in hertz.</summary>
    public int SampleRate { get; }

    /// <summary>Samples per frame (<c>SampleRate * FrameMs / 1000</c>, truncated).</summary>
    public int FrameSamples { get; }

    /// <summary>
    /// The speech decision of every frame (the last frame may be shorter), made exactly as <see cref="Trim(short[])"/> makes it, without the hangover: the frame
    /// <c>i</c> covers the samples from <c>i * FrameSamples</c>. M84 reads the share of the voiced frames that the recognized words cover from it.
    /// </summary>
    internal bool[] SpeechFrames(short[] samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var frameCount = (samples.Length + FrameSamples - 1) / FrameSamples;
        var speech = new bool[frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var start = frame * FrameSamples;
            var length = Math.Min(FrameSamples, samples.Length - start);
            long energy = 0;
            for (var i = start; i < start + length; i++)
            {
                energy += (long)samples[i] * samples[i];
            }

            speech[frame] = (double)energy >= _meanSquareThreshold * length;
        }

        return speech;
    }

    /// <summary>Trims silence around speech and reports the speech frame range; an input without speech yields an empty result with <see cref="VadResult.HasSpeech"/> false.</summary>
    public VadResult Trim(short[] samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var speech = SpeechFrames(samples);
        var frameCount = speech.Length;
        var first = Array.IndexOf(speech, true);
        var last = Array.LastIndexOf(speech, true);
        if (first < 0)
        {
            return new VadResult([], false, 0, 0, frameCount);
        }

        var end = (int)Math.Min((long)last + 1 + HangoverFrames, frameCount);
        var from = first * FrameSamples;
        var to = Math.Min(end * FrameSamples, samples.Length);
        return new VadResult(samples.AsSpan(from, to - from).ToArray(), true, first, end, frameCount);
    }
}
