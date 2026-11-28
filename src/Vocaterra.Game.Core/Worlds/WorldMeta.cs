using System.Text.Json;
using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Game.Core.Worlds;

/// <summary>Which world a game plays in (Q01; <c>--world</c>): the tall streamed world, or the bounded valley alone.</summary>
public enum WorldKind
{
    /// <summary>The tall procedural world streamed in round the valley, with the far view: the game's world.</summary>
    Streamed,

    /// <summary>The bounded valley alone.</summary>
    Valley,
}

/// <summary>
/// What a world's folder says about it (<c>world.json</c>, Q01): the name the player gave, the seed and type it was made with, when it was made and last played (Unix seconds), and,
/// from Q03, the terrain options it was made with. The save itself is the folder's other file (<see cref="WorldFiles.SaveFileName"/>).
/// </summary>
/// <param name="Schema">The file's schema version (<see cref="CurrentSchema"/>).</param>
/// <param name="Name">The world's name as the player typed it (cleaned: one space between words, at most <see cref="WorldNames.MaxLength"/> characters).</param>
/// <param name="Seed">The world seed.</param>
/// <param name="World">The world's type.</param>
/// <param name="Created">When the world was made, in Unix seconds.</param>
/// <param name="LastPlayed">When the world was last started or left, in Unix seconds.</param>
/// <param name="Terrain">The terrain options the world was made with, by option id (Q02 and Q03 fill this in; <see langword="null"/>, and left out of the file, for the defaults).</param>
public sealed record WorldMeta(
    int Schema,
    string Name,
    ulong Seed,
    WorldKind World,
    long Created,
    long LastPlayed,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, int>? Terrain = null)
{
    /// <summary>The schema version this code writes and reads.</summary>
    public const int CurrentSchema = 1;

    /// <summary>The text of the metadata file: canonical JSON.</summary>
    public string Serialize() => CanonicalJson.Serialize(this);

    /// <summary>Reads a metadata file's text.</summary>
    /// <param name="json">The text.</param>
    /// <param name="problem">Why the text is no metadata of this schema, or <see langword="null"/>.</param>
    /// <returns>The metadata, or <see langword="null"/> when <paramref name="json"/> is not readable.</returns>
    public static WorldMeta? TryParse(string json, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(json);
        WorldMeta meta;
        try
        {
            meta = CanonicalJson.Deserialize<WorldMeta>(json);
        }
        catch (JsonException ex)
        {
            problem = $"{WorldFiles.MetaFileName} is not readable: {ex.Message}";
            return null;
        }

        if (meta.Schema != CurrentSchema)
        {
            problem = $"{WorldFiles.MetaFileName} is schema {meta.Schema}; this game reads schema {CurrentSchema}.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(meta.Name))
        {
            problem = $"{WorldFiles.MetaFileName} has no name.";
            return null;
        }

        problem = null;
        return meta;
    }
}

/// <summary>The file names inside a world's folder.</summary>
public static class WorldFiles
{
    /// <summary>The metadata file.</summary>
    public const string MetaFileName = "world.json";

    /// <summary>The world's save: the autosave and the Exit save both write it (a crash save may sit beside it).</summary>
    public const string SaveFileName = "world.save";
}
