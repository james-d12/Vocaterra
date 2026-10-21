using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Nlp.Audio;

/// <summary>
/// Collects 16 kHz mono samples between <see cref="Begin"/> and <see cref="End"/> and turns them into a
/// <see cref="PcmClip"/> by applying gain normalization, then energy VAD trimming, then hashing.
/// A recording without speech yields a clip with no samples; inspect <see cref="LastVadResult"/> to tell.
/// </summary>
public sealed class PushToTalkSession
{
    private readonly AudioFrontEndOptions _options;
    private readonly EnergyVad _vad;
    private readonly List<short> _buffer = [];
    private bool _active;

    /// <summary>Creates a session; <paramref name="options"/> defaults to <see cref="AudioFrontEndOptions.Default"/>.</summary>
    public PushToTalkSession(AudioFrontEndOptions? options = null)
    {
        _options = options ?? AudioFrontEndOptions.Default;
        _vad = new EnergyVad(_options.FrameMs, _options.VadThresholdDbfs, _options.HangoverFrames);
    }

    /// <summary>True between <see cref="Begin"/> and <see cref="End"/>.</summary>
    public bool IsRecording => _active;

    /// <summary>The VAD result of the most recent <see cref="End"/>, or null before the first one.</summary>
    public VadResult? LastVadResult { get; private set; }

    /// <summary>Starts a recording, discarding anything buffered by an unfinished one.</summary>
    public void Begin()
    {
        _buffer.Clear();
        _active = true;
    }

    /// <summary>Appends 16 kHz mono samples to the recording in progress.</summary>
    public void Append(ReadOnlySpan<short> samples)
    {
        if (!_active)
        {
            throw new InvalidOperationException("Call Begin() before Append().");
        }

        _buffer.AddRange(samples);
    }

    /// <summary>Ends the recording and returns the normalized, trimmed clip.</summary>
    public PcmClip End()
    {
        if (!_active)
        {
            throw new InvalidOperationException("Call Begin() before End().");
        }

        _active = false;
        var recorded = _buffer.ToArray();
        _buffer.Clear();
        var normalized = GainNormalizer.Normalize(recorded, _options.TargetPeakDbfs, _options.MaxGainDb);
        var trimmed = _vad.Trim(normalized);
        LastVadResult = trimmed;
        return PcmClip.FromSamples(trimmed.Samples);
    }
}
