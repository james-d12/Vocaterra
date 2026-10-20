using Slg.Core.Audio;

namespace Slg.Audio;

/// <summary>One-call entry point of the audio front-end used by tools and capture sources.</summary>
public static class AudioFrontEnd
{
    /// <summary>
    /// Runs the full chain: downmix to mono, resample to 16 kHz, then a <see cref="PushToTalkSession"/>
    /// (gain normalization, VAD trim, hashing).
    /// </summary>
    public static PcmClip Process(WavData wav, AudioFrontEndOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(wav);
        var mono = Downmixer.ToMono(wav);
        var resampled = Resampler.To16k(mono, wav.SampleRate);
        var session = new PushToTalkSession(options);
        session.Begin();
        session.Append(resampled);
        return session.End();
    }
}
