using System.Text.Json;
using System.Text.Json.Serialization;
using Slg.Core.Determinism;

namespace Slg.Asr.Models;

/// <summary>The allowed values of <see cref="ModelEntry.Role"/>.</summary>
public static class ModelRoles
{
    /// <summary>Closed-vocabulary speech recognition (grammar constrained).</summary>
    public const string AsrConstrained = "asr-constrained";

    /// <summary>Open-vocabulary speech recognition.</summary>
    public const string AsrOpen = "asr-open";

    /// <summary>Phoneme recognition for pronunciation scoring.</summary>
    public const string Phoneme = "phoneme";

    /// <summary>Sentence embedding model for intent resolution.</summary>
    public const string Embedding = "embedding";

    /// <summary>Speech synthesis.</summary>
    public const string Tts = "tts";

    /// <summary>Every allowed role in ordinal order.</summary>
    public static IReadOnlyList<string> All { get; } = [AsrConstrained, AsrOpen, Embedding, Phoneme, Tts];

    /// <summary>Returns true when <paramref name="role"/> is one of <see cref="All"/>.</summary>
    public static bool IsValid(string role)
    {
        return All.Contains(role, StringComparer.Ordinal);
    }
}

/// <summary>One file of a model, relative to the model directory, with its pinned SHA-256 and size (null while not yet pinned).</summary>
/// <param name="Path">Forward-slash path relative to <c>&lt;root&gt;/&lt;id&gt;/</c>.</param>
/// <param name="Sha256">Lowercase hex SHA-256 of the file, or null when the file is not pinned yet.</param>
/// <param name="Bytes">File length in bytes, or null when not pinned yet.</param>
public sealed record ModelFile(string Path, string? Sha256, long? Bytes);

/// <summary>The downloadable archive a model is fetched from; its top-level directory must equal the model id.</summary>
/// <param name="Format">Archive format, <c>zip</c> or <c>tar.bz2</c>.</param>
/// <param name="Sha256">Lowercase hex SHA-256 of the archive, or null when not pinned yet.</param>
/// <param name="Bytes">Archive length in bytes, or null when not pinned yet.</param>
public sealed record ModelArchive(string Format, string? Sha256, long? Bytes);

/// <summary>A model entry of the manifest: identity, license, origin, optional archive and the pinned files.</summary>
/// <param name="Id">Stable identifier; also the directory name below the models root.</param>
/// <param name="Role">One of <see cref="ModelRoles"/>.</param>
/// <param name="Name">Human readable model name.</param>
/// <param name="Version">Upstream version or release label.</param>
/// <param name="License">SPDX-style license identifier.</param>
/// <param name="Source">URL the model is downloaded from (archive URL, or the upstream repository for manual models).</param>
/// <param name="Archive">The archive fetched from <paramref name="Source"/>, or null for models that need a manual export step.</param>
/// <param name="Files">The files that make up the model; the model hash is derived from their hashes.</param>
/// <param name="Notes">Optional free text, for example TODOs for unpinned entries.</param>
public sealed record ModelEntry(
    string Id,
    string Role,
    string Name,
    string Version,
    string License,
    string Source,
    ModelArchive? Archive,
    IReadOnlyList<ModelFile> Files,
    string? Notes)
{
    /// <summary>True when every file has a pinned SHA-256 and the entry lists at least one file.</summary>
    [JsonIgnore]
    public bool IsPinned => Files.Count > 0 && Files.All(static f => f.Sha256 is not null);

    /// <summary>Structural equality including the file list.</summary>
    public bool Equals(ModelEntry? other)
    {
        return other is not null
            && string.Equals(Id, other.Id, StringComparison.Ordinal)
            && string.Equals(Role, other.Role, StringComparison.Ordinal)
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(Version, other.Version, StringComparison.Ordinal)
            && string.Equals(License, other.License, StringComparison.Ordinal)
            && string.Equals(Source, other.Source, StringComparison.Ordinal)
            && Equals(Archive, other.Archive)
            && Files.SequenceEqual(other.Files)
            && string.Equals(Notes, other.Notes, StringComparison.Ordinal);
    }

    /// <summary>Hash consistent with <see cref="Equals(ModelEntry)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Id, StringComparer.Ordinal);
        hash.Add(Role, StringComparer.Ordinal);
        hash.Add(Version, StringComparer.Ordinal);
        hash.Add(Files.Count);
        return hash.ToHashCode();
    }
}

/// <summary>The committed <c>models/manifest.json</c>: names, versions, licenses and SHA-256 hashes of every model file.</summary>
/// <param name="SchemaVersion">Manifest schema version; always 1 today.</param>
/// <param name="Models">The model entries.</param>
public sealed record ModelManifest(int SchemaVersion, IReadOnlyList<ModelEntry> Models)
{
    /// <summary>The only supported schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>An empty manifest.</summary>
    public static ModelManifest Empty { get; } = new(CurrentSchemaVersion, []);

    /// <summary>Returns the entry with the given id (ordinal comparison), or null.</summary>
    public ModelEntry? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        foreach (var model in Models)
        {
            if (string.Equals(model.Id, id, StringComparison.Ordinal))
            {
                return model;
            }
        }

        return null;
    }

    /// <summary>Parses and validates manifest JSON (indentation and unknown properties are tolerated).</summary>
    /// <exception cref="InvalidDataException">The JSON is malformed or violates the manifest rules.</exception>
    public static ModelManifest Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        ModelManifest manifest;
        try
        {
            manifest = CanonicalJson.Deserialize<ModelManifest>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The model manifest is not valid JSON: " + ex.Message, ex);
        }

        manifest.Validate();
        return manifest;
    }

    /// <summary>Reads, parses and validates a manifest file.</summary>
    public static ModelManifest Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Parse(File.ReadAllText(path));
    }

    /// <summary>Serializes the manifest to canonical JSON.</summary>
    public string ToJson()
    {
        return CanonicalJson.Serialize(this);
    }

    /// <summary>Structural equality including the entries.</summary>
    public bool Equals(ModelManifest? other)
    {
        return other is not null && SchemaVersion == other.SchemaVersion && Models.SequenceEqual(other.Models);
    }

    /// <summary>Hash consistent with <see cref="Equals(ModelManifest)"/>.</summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(SchemaVersion, Models.Count);
    }

    private void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported manifest schemaVersion {SchemaVersion}; expected {CurrentSchemaVersion}.");
        }

        if ((object?)Models is null)
        {
            throw new InvalidDataException("The manifest has no 'models' array.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var model in Models)
        {
            ValidateEntry(model, ids);
        }
    }

    private static void ValidateEntry(ModelEntry? model, HashSet<string> ids)
    {
        if (model is null || string.IsNullOrWhiteSpace(model.Id))
        {
            throw new InvalidDataException("Every model needs a non-empty id.");
        }

        if (!ModelStore.IsSafeName(model.Id))
        {
            throw new InvalidDataException($"Model id '{model.Id}' must be a single plain directory name.");
        }

        if (!ids.Add(model.Id))
        {
            throw new InvalidDataException($"Duplicate model id '{model.Id}'.");
        }

        if ((object?)model.Role is null || !ModelRoles.IsValid(model.Role))
        {
            throw new InvalidDataException($"Model '{model.Id}' has an invalid role.");
        }

        if ((object?)model.Name is null || (object?)model.Version is null || (object?)model.License is null || (object?)model.Source is null)
        {
            throw new InvalidDataException($"Model '{model.Id}' needs name, version, license and source.");
        }

        if ((object?)model.Files is null)
        {
            throw new InvalidDataException($"Model '{model.Id}' has no 'files' array.");
        }

        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in model.Files)
        {
            if (file is null || !ModelStore.IsSafeRelativePath(file.Path))
            {
                throw new InvalidDataException($"Model '{model.Id}' has a file with an invalid path.");
            }

            if (!paths.Add(file.Path))
            {
                throw new InvalidDataException($"Model '{model.Id}' lists '{file.Path}' twice.");
            }

            if (file.Sha256 is not null && !ModelStore.IsSha256Hex(file.Sha256))
            {
                throw new InvalidDataException($"Model '{model.Id}' file '{file.Path}' has a malformed sha256.");
            }

            if (file.Bytes is < 0)
            {
                throw new InvalidDataException($"Model '{model.Id}' file '{file.Path}' has a negative size.");
            }
        }

        if (model.Archive is { } archive)
        {
            if (!string.Equals(archive.Format, "zip", StringComparison.Ordinal)
                && !string.Equals(archive.Format, "tar.bz2", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Model '{model.Id}' has an unsupported archive format.");
            }

            if (archive.Sha256 is not null && !ModelStore.IsSha256Hex(archive.Sha256))
            {
                throw new InvalidDataException($"Model '{model.Id}' archive has a malformed sha256.");
            }
        }
    }
}
