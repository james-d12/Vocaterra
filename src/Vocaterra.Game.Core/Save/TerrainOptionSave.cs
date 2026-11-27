using Vocaterra.Game.Core.World.Options;

namespace Vocaterra.Game.Core.Save;

/// <summary>
/// One terrain option of the world in a save (version 12, Q02): the option's id and its value, for an option the player moved off its default (<see cref="WorldOptions.Entries"/>). A world at the defaults has no
/// <c>terrain</c> entry. A load builds the world from the seed and these, so the land is the one the save was made on.
/// </summary>
/// <param name="Id">The option's id in <c>game/world-options.json</c>.</param>
/// <param name="Value">The option's value.</param>
public sealed record TerrainOptionSave(string Id, int Value)
{
    /// <summary>The entries of a world's options, or <see langword="null"/> at the defaults (the save then leaves the entry out).</summary>
    /// <param name="options">The options.</param>
    public static IReadOnlyList<TerrainOptionSave>? Of(WorldOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.IsDefault ? null : [.. options.Entries.Select(static e => new TerrainOptionSave(e.Key, e.Value))];
    }

    /// <summary>The options a save's entries name, in this content's table.</summary>
    /// <param name="entries">The save's entries, or <see langword="null"/> for none.</param>
    /// <param name="table">The content's option rows.</param>
    /// <exception cref="SaveException">An entry names an option the content lacks, or a value off its grid.</exception>
    public static WorldOptions ToOptions(IReadOnlyList<TerrainOptionSave>? entries, TerrainOptionData table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (entries is null || entries.Count == 0)
        {
            return WorldOptions.Default;
        }

        return table.TryCreate(entries.Select(static e => new KeyValuePair<string, int>(e.Id, e.Value)), out var options, out var error)
            ? options
            : throw new SaveException($"The save's terrain options do not fit this game: {error}");
    }
}
