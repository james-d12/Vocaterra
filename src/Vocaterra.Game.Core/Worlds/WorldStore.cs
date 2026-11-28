using Vocaterra.Game.Core.Save;

namespace Vocaterra.Game.Core.Worlds;

/// <summary>A world on disk: its folder, what its metadata says, whether it has a save yet, and why it cannot be loaded when it cannot.</summary>
/// <param name="Folder">The folder's name under the saves folder.</param>
/// <param name="Directory">The folder's full path.</param>
/// <param name="Meta">The metadata, or <see langword="null"/> when <c>world.json</c> could not be read.</param>
/// <param name="HasSave">Whether the folder holds a save (a world that was made and not yet saved has none: loading it starts it afresh).</param>
/// <param name="Problem">Why the world cannot be loaded (its metadata or its save is unreadable or of another version), or <see langword="null"/>.</param>
public sealed record WorldEntry(string Folder, string Directory, WorldMeta? Meta, bool HasSave, string? Problem)
{
    /// <summary>The name to show: the world's own, else its folder's.</summary>
    public string Name => Meta?.Name ?? Folder;

    /// <summary>The metadata file's path.</summary>
    public string MetaPath => Path.Combine(Directory, WorldFiles.MetaFileName);

    /// <summary>The save's path (whether or not it is there yet).</summary>
    public string SavePath => Path.Combine(Directory, WorldFiles.SaveFileName);

    /// <summary>Whether the world can be loaded.</summary>
    public bool Loadable => Meta is not null && Problem is null;
}

/// <summary>
/// The worlds on disk (Q01): one folder per world under a saves folder (<c>~/.local/share/vocaterra/saves</c> in the game; the front-end gives the path, so a test never touches the real one), each
/// holding <c>world.json</c> (<see cref="WorldMeta"/>) and the world's save (<c>world.save</c>). The store lists them newest played first, makes a new one with a unique folder, touches a world's
/// last-played time, and deletes one. The time comes from the function it is given: no clock is read here.
/// </summary>
public sealed class WorldStore
{
    private readonly string _base;
    private readonly Func<long> _now;

    /// <summary>Creates the store of a saves folder.</summary>
    /// <param name="baseDirectory">The saves folder (made when the first world is).</param>
    /// <param name="now">The time now in Unix seconds.</param>
    public WorldStore(string baseDirectory, Func<long> now)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseDirectory);
        ArgumentNullException.ThrowIfNull(now);
        _base = Path.GetFullPath(baseDirectory);
        _now = now;
    }

    /// <summary>The saves folder.</summary>
    public string BaseDirectory => _base;

    /// <summary>
    /// The worlds, newest played first (then newest made, then by name). A folder without <c>world.json</c> is not a world and is left out; one whose file cannot be read is listed with its problem, at the end.
    /// </summary>
    /// <param name="probeSaves">Whether to read each world's save far enough to tell that it is one this game can load (its format and version), and give the world the problem when it is not.</param>
    public IReadOnlyList<WorldEntry> List(bool probeSaves = false)
    {
        if (!Directory.Exists(_base))
        {
            return [];
        }

        var entries = new List<WorldEntry>();
        foreach (var directory in Directory.EnumerateDirectories(_base).Order(StringComparer.Ordinal))
        {
            var metaPath = Path.Combine(directory, WorldFiles.MetaFileName);
            if (!File.Exists(metaPath))
            {
                continue;
            }

            entries.Add(Read(directory, probeSaves));
        }

        return [.. entries
            .OrderBy(static e => e.Meta is null ? 1 : 0)
            .ThenByDescending(static e => e.Meta?.LastPlayed ?? 0)
            .ThenByDescending(static e => e.Meta?.Created ?? 0)
            .ThenBy(static e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static e => e.Folder, StringComparer.Ordinal)];
    }

    /// <summary>The names of the worlds there are (for the name rules).</summary>
    public IReadOnlyList<string> Names() => [.. List().Select(static e => e.Name)];

    /// <summary>Makes a new world: its folder (unique, from the name) and its metadata file.</summary>
    /// <param name="name">The world's name (cleaned here); <see cref="WorldNames.Check"/> must accept it.</param>
    /// <param name="seed">The world seed.</param>
    /// <param name="kind">The world's type.</param>
    /// <param name="terrain">The terrain options (Q03), or <see langword="null"/> for the defaults.</param>
    /// <exception cref="ArgumentException">The name is refused.</exception>
    /// <exception cref="IOException">The folder or the file could not be made.</exception>
    public WorldEntry Create(string name, ulong seed, WorldKind kind, IReadOnlyDictionary<string, int>? terrain = null)
    {
        var clean = WorldNames.Clean(name);
        var problem = WorldNames.Check(clean, Names());
        if (problem != NameProblem.None)
        {
            throw new ArgumentException($"The name '{name}' is refused: {problem}.", nameof(name));
        }

        _ = Directory.CreateDirectory(_base);
        var existing = Directory.EnumerateDirectories(_base).Select(static d => Path.GetFileName(d)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var folder = WorldNames.UniqueFolder(clean, existing.Contains);
        var directory = Path.Combine(_base, folder);
        _ = Directory.CreateDirectory(directory);
        var now = _now();
        var meta = new WorldMeta(WorldMeta.CurrentSchema, clean, seed, kind, now, now, terrain is { Count: > 0 } ? terrain : null);
        SaveFile.Write(Path.Combine(directory, WorldFiles.MetaFileName), meta.Serialize());
        return new WorldEntry(folder, directory, meta, HasSave: false, Problem: null);
    }

    /// <summary>Sets a world's last-played time to now (it was started or left) and writes its metadata again.</summary>
    /// <param name="entry">The world.</param>
    /// <returns>The world with its new time.</returns>
    /// <exception cref="IOException">The file could not be written.</exception>
    public WorldEntry Touch(WorldEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Meta is null)
        {
            return entry;
        }

        var meta = entry.Meta with { LastPlayed = Math.Max(entry.Meta.LastPlayed, _now()) };
        SaveFile.Write(entry.MetaPath, meta.Serialize());
        return entry with { Meta = meta, HasSave = File.Exists(entry.SavePath) };
    }

    /// <summary>The world in a folder (read now), or <see langword="null"/> when there is none.</summary>
    /// <param name="folder">The folder's name.</param>
    public WorldEntry? Find(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        var directory = Path.Combine(_base, folder);
        return File.Exists(Path.Combine(directory, WorldFiles.MetaFileName)) ? Read(directory, probeSave: false) : null;
    }

    /// <summary>
    /// Deletes a world: its whole folder (the metadata, the save, a crash save beside it). Only a folder directly under the saves folder that holds a <c>world.json</c> is deleted, so no other path can be named.
    /// </summary>
    /// <param name="entry">The world.</param>
    /// <exception cref="InvalidOperationException">The entry is not a world folder of this store.</exception>
    /// <exception cref="IOException">The folder could not be deleted.</exception>
    public void Delete(WorldEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var full = Path.GetFullPath(entry.Directory);
        var parent = Path.GetDirectoryName(full.TrimEnd(Path.DirectorySeparatorChar));
        if (!string.Equals(parent, _base.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal)
            || !File.Exists(Path.Combine(full, WorldFiles.MetaFileName)))
        {
            throw new InvalidOperationException($"'{entry.Directory}' is not a world folder of {_base}.");
        }

        Directory.Delete(full, recursive: true);
    }

    private static WorldEntry Read(string directory, bool probeSave)
    {
        var folder = Path.GetFileName(directory);
        var savePath = Path.Combine(directory, WorldFiles.SaveFileName);
        var hasSave = File.Exists(savePath);
        string? problem;
        WorldMeta? meta = null;
        try
        {
            meta = WorldMeta.TryParse(SaveFile.Read(Path.Combine(directory, WorldFiles.MetaFileName)), out problem);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problem = $"{WorldFiles.MetaFileName} cannot be read: {ex.Message}";
        }

        if (meta is not null && hasSave && probeSave)
        {
            try
            {
                _ = SaveGame.Parse(SaveFile.Read(savePath));
            }
            catch (Exception ex) when (ex is SaveException or IOException or UnauthorizedAccessException)
            {
                problem = ex.Message;
            }
        }

        return new WorldEntry(folder, directory, meta, hasSave, problem);
    }
}
