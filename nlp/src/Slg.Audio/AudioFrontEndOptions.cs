namespace Slg.Audio;

/// <summary>Tunable constants of the audio front-end; the defaults are the pipeline's fixed production values.</summary>
public sealed record AudioFrontEndOptions
{
    /// <summary>The default options.</summary>
    public static AudioFrontEndOptions Default { get; } = new();

    /// <summary>Peak level the gain stage aims for, in dBFS.</summary>
    public double TargetPeakDbfs { get; init; } = -3.0;

    /// <summary>Largest gain the gain stage may apply, in dB.</summary>
    public double MaxGainDb { get; init; } = 20.0;

    /// <summary>Voice activity frame length in milliseconds.</summary>
    public int FrameMs { get; init; } = 20;

    /// <summary>Voice activity speech threshold in dBFS RMS.</summary>
    public double VadThresholdDbfs { get; init; } = -40.0;

    /// <summary>Frames kept after the last speech frame.</summary>
    public int HangoverFrames { get; init; } = 5;
}
