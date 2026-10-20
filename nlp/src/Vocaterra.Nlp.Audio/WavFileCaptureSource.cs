using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Nlp.Audio;

/// <summary>Capture source that reads a WAV file and runs it through <see cref="AudioFrontEnd"/>; used by tools and tests.</summary>
public sealed class WavFileCaptureSource : ICaptureSource
{
    private readonly string _path;
    private readonly AudioFrontEndOptions? _options;

    /// <summary>Creates a source for the WAV file at <paramref name="path"/>.</summary>
    public WavFileCaptureSource(string path, AudioFrontEndOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _path = path;
        _options = options;
    }

    /// <summary>Reads the file, downmixes, resamples to 16 kHz and returns the processed clip.</summary>
    public PcmClip Capture()
    {
        return AudioFrontEnd.Process(WavFile.Read(_path), _options);
    }
}
