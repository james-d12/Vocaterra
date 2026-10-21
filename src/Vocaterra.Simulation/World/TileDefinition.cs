namespace Vocaterra.Simulation.Tiles;

/// <summary>What the simulation knows about one tile kind. Spanish nouns are not here: game words live in the content bundle.</summary>
/// <param name="Id">The tile kind.</param>
/// <param name="Name">A stable lowercase English key, for debugging and for the front-end's atlas lookup.</param>
/// <param name="Solid">Whether bodies collide with it.</param>
public sealed record TileDefinition(TileId Id, string Name, bool Solid)
{
    private static readonly TileDefinition[] Table =
    [
        new(TileId.Air, "air", false),
        new(TileId.Dirt, "dirt", true),
        new(TileId.Grass, "grass", true),
        new(TileId.Stone, "stone", true),
        new(TileId.Trunk, "trunk", false),
        new(TileId.Leaves, "leaves", false),
    ];

    /// <summary>Every defined tile kind, ordered by id.</summary>
    public static IReadOnlyList<TileDefinition> All => Table;

    /// <summary>The definition of <paramref name="id"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The id has no definition.</exception>
    public static TileDefinition Get(TileId id)
    {
        var index = (int)id;
        if ((uint)index >= (uint)Table.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "No tile definition for this id.");
        }

        return Table[index];
    }

    /// <summary>Whether bodies collide with tiles of kind <paramref name="id"/>.</summary>
    public static bool IsSolid(TileId id)
    {
        var index = (int)id;
        return (uint)index < (uint)Table.Length && Table[index].Solid;
    }
}
