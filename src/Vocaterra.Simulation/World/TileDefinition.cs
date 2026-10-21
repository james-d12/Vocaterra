using Vocaterra.Simulation.Items;

namespace Vocaterra.Simulation.Tiles;

/// <summary>
/// What the simulation knows about one tile kind: whether bodies collide with it, how long it takes to mine by hand or with its tool, which
/// tool it needs and what mining it gives. Spanish nouns are not here: game words live in the content bundle.
/// </summary>
/// <param name="Id">The tile kind.</param>
/// <param name="Name">A stable lowercase English key, for debugging and for the front-end's atlas lookup.</param>
/// <param name="Solid">Whether bodies collide with it.</param>
/// <param name="MiningTicks">The ticks a player must hold the mine command on it with the right tool (durability in time).</param>
/// <param name="RequiredTool">The tool needed; <see cref="ToolKind.None"/> when the hand does.</param>
/// <param name="Drop">What mining gives, or <see langword="null"/> for nothing.</param>
public sealed record TileDefinition(TileId Id, string Name, bool Solid, int MiningTicks, ToolKind RequiredTool, ItemId? Drop)
{
    private static readonly TileDefinition[] Table =
    [
        new(TileId.Air, "air", false, 0, ToolKind.None, null),
        new(TileId.Dirt, "dirt", true, 30, ToolKind.Pickaxe, ItemId.Dirt),
        new(TileId.Grass, "grass", true, 30, ToolKind.Pickaxe, ItemId.Dirt),
        new(TileId.Stone, "stone", true, 75, ToolKind.Pickaxe, ItemId.Stone),
        new(TileId.Trunk, "trunk", false, 90, ToolKind.Axe, ItemId.Wood),
        new(TileId.Leaves, "leaves", false, 12, ToolKind.None, null),
        new(TileId.Sand, "sand", true, 20, ToolKind.Pickaxe, ItemId.Sand),
        new(TileId.Flower, "flower", false, 6, ToolKind.None, ItemId.Flower),
        new(TileId.Mushroom, "mushroom", false, 6, ToolKind.None, ItemId.Mushroom),
        new(TileId.Planks, "planks", true, 60, ToolKind.Pickaxe, ItemId.Wood),
        new(TileId.Door, "door", true, 60, ToolKind.Pickaxe, ItemId.Door),
        new(TileId.Table, "table", true, 60, ToolKind.Pickaxe, ItemId.Table),
        new(TileId.Chair, "chair", true, 45, ToolKind.Pickaxe, ItemId.Chair),
        new(TileId.Bed, "bed", true, 60, ToolKind.Pickaxe, ItemId.Bed),
        new(TileId.Chest, "chest", true, 60, ToolKind.Pickaxe, ItemId.Chest),
        new(TileId.Furnace, "furnace", true, 75, ToolKind.Pickaxe, ItemId.Furnace),
        new(TileId.Glass, "glass", true, 30, ToolKind.Pickaxe, ItemId.Glass),
        new(TileId.Brick, "brick", true, 60, ToolKind.Pickaxe, ItemId.Brick),
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
