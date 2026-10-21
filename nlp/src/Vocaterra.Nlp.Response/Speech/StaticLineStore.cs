using System.Diagnostics.CodeAnalysis;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Nlp.Response.Speech;

/// <summary>A directory of pre-rendered lines, one mono 16-bit PCM <c>&lt;lineId&gt;.wav</c> per dialogue line.</summary>
public sealed class StaticLineStore
{
    private readonly string _directory;

    /// <summary>Creates a store over a directory; the directory does not have to exist yet.</summary>
    public StaticLineStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <summary>The directory the lines are read from.</summary>
    public string Directory => _directory;

    /// <summary>
    /// Reads the pre-rendered audio of a line. Returns false when the line id is not a plain file name (letters, digits, <c>.</c>, <c>_</c>, <c>-</c>)
    /// or the file does not exist; a file that exists but is not mono 16-bit PCM WAV throws <see cref="InvalidDataException"/>.
    /// </summary>
    public bool TryGet(string lineId, [NotNullWhen(true)] out AudioBuffer? buffer)
    {
        ArgumentNullException.ThrowIfNull(lineId);
        buffer = null;
        var path = PathFor(lineId);
        if (path is null || !File.Exists(path))
        {
            return false;
        }

        buffer = WavCodec.Read(File.ReadAllBytes(path));
        return true;
    }

    /// <summary>Returns true when a pre-rendered file exists for the line id.</summary>
    public bool Contains(string lineId)
    {
        ArgumentNullException.ThrowIfNull(lineId);
        var path = PathFor(lineId);
        return path is not null && File.Exists(path);
    }

    private string? PathFor(string lineId)
    {
        if (lineId.Length == 0)
        {
            return null;
        }

        foreach (var c in lineId)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
            {
                return null;
            }
        }

        return Path.Combine(_directory, lineId + ".wav");
    }
}
