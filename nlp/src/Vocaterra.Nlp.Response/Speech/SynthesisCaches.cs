using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Nlp.Response.Speech;

/// <summary>A store of synthesized audio keyed by the hash of what produced it.</summary>
public interface ISynthesisCache
{
    /// <summary>Returns the cached audio for the key, or false on a miss.</summary>
    bool TryGet(string key, [NotNullWhen(true)] out AudioBuffer? buffer);

    /// <summary>Stores the audio under the key, replacing any earlier entry.</summary>
    void Put(string key, AudioBuffer buffer);
}

/// <summary>A process-lifetime synthesis cache; a hit returns the very instance that was stored.</summary>
public sealed class InMemorySynthesisCache : ISynthesisCache
{
    private readonly ConcurrentDictionary<string, AudioBuffer> _entries = new(StringComparer.Ordinal);

    /// <summary>Number of cached entries.</summary>
    public int Count => _entries.Count;

    /// <summary>Returns the cached audio for the key, or false on a miss.</summary>
    public bool TryGet(string key, [NotNullWhen(true)] out AudioBuffer? buffer)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _entries.TryGetValue(key, out buffer);
    }

    /// <summary>Stores the audio under the key, replacing any earlier entry.</summary>
    public void Put(string key, AudioBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(buffer);
        _entries[key] = buffer;
    }
}

/// <summary>A synthesis cache persisted as <c>&lt;key&gt;.wav</c> files (mono 16-bit PCM) in a directory; an unreadable file counts as a miss.</summary>
public sealed class DirectorySynthesisCache : ISynthesisCache
{
    private readonly string _directory;

    /// <summary>Creates a cache over a directory, which is created on the first <see cref="Put"/>.</summary>
    public DirectorySynthesisCache(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <summary>Reads the cached audio for the key, or returns false when there is no readable file.</summary>
    public bool TryGet(string key, [NotNullWhen(true)] out AudioBuffer? buffer)
    {
        var path = PathFor(key);
        buffer = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            buffer = WavCodec.Read(File.ReadAllBytes(path));
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>Writes the audio as <c>&lt;key&gt;.wav</c> through a temporary file so a reader never sees a partial file.</summary>
    public void Put(string key, AudioBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var path = PathFor(key);
        Directory.CreateDirectory(_directory);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, WavCodec.Write(buffer));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private string PathFor(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        foreach (var c in key)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
            {
                throw new ArgumentException($"Cache key '{key}' must contain only letters, digits, '_' and '-'.", nameof(key));
            }
        }

        return Path.Combine(_directory, key + ".wav");
    }
}
