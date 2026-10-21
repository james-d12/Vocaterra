using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Items;

/// <summary>
/// The item kinds the player can hold. The numeric values are saved, so a value is never changed or reused: new kinds are appended. Spanish
/// names are not here; each kind's noun is the bundle's <c>game/nouns.json</c>, keyed by <see cref="ItemDefinition.Name"/>.
/// </summary>
public enum ItemId : ushort
{
    /// <summary>Wood cut from a trunk. Placed, it becomes planks.</summary>
    Wood = 0,

    /// <summary>Stone mined with a pickaxe.</summary>
    Stone = 1,

    /// <summary>Dirt (and grass, which drops dirt).</summary>
    Dirt = 2,

    /// <summary>Sand from a sand patch.</summary>
    Sand = 3,

    /// <summary>A flower picked by hand.</summary>
    Flower = 4,

    /// <summary>A mushroom picked by hand.</summary>
    Mushroom = 5,

    /// <summary>The axe: the tool for trunks.</summary>
    Axe = 6,

    /// <summary>The pickaxe: the tool for stone, dirt, sand and the placed blocks.</summary>
    Pickaxe = 7,

    /// <summary>A door, placeable (crafted in P6b).</summary>
    Door = 8,

    /// <summary>A table, placeable (crafted in P6b).</summary>
    Table = 9,

    /// <summary>A chair, placeable (crafted in P6b).</summary>
    Chair = 10,

    /// <summary>A bed, placeable (crafted in P6b).</summary>
    Bed = 11,

    /// <summary>A chest, placeable (crafted in P6b).</summary>
    Chest = 12,

    /// <summary>A sword (crafted; a weapon for later milestones, one per slot).</summary>
    Sword = 13,

    /// <summary>A furnace, placeable; a crafting station.</summary>
    Furnace = 14,

    /// <summary>Glass, placeable.</summary>
    Glass = 15,

    /// <summary>A brick, placeable.</summary>
    Brick = 16,

    /// <summary>Coal, mined from coal ore (P7).</summary>
    Coal = 17,

    /// <summary>Copper, mined from copper ore.</summary>
    Copper = 18,

    /// <summary>Iron, mined from iron ore.</summary>
    Iron = 19,

    /// <summary>Gold, mined from gold ore.</summary>
    Gold = 20,

    /// <summary>A torch, placeable: it lights the cells around it.</summary>
    Torch = 21,

    /// <summary>A lamp (crafted from iron and glass at a table; not placeable yet).</summary>
    Lamp = 22,

    /// <summary>A bucket (crafted from iron at a furnace; not placeable yet).</summary>
    Bucket = 23,

    /// <summary>A ring (crafted from gold at a furnace; not placeable yet).</summary>
    Ring = 24,
}

/// <summary>The tools a tile can require. <see cref="None"/> is the hand.</summary>
public enum ToolKind : byte
{
    /// <summary>No tool: the hand.</summary>
    None = 0,

    /// <summary>The axe (trunks).</summary>
    Axe = 1,

    /// <summary>The pickaxe (stone and the rest).</summary>
    Pickaxe = 2,
}

/// <summary>
/// What the simulation knows about one item kind: its stable key, its stack limit, the tool it is (if any) and the tile it places (if any).
/// The stack limit lives here and nowhere else.
/// </summary>
/// <param name="Id">The item kind.</param>
/// <param name="Name">A stable lowercase English key; the bundle's noun catalogue names items by it.</param>
/// <param name="MaxStack">How many of this item one inventory slot holds.</param>
/// <param name="Tool">The tool this item is, or <see cref="ToolKind.None"/>.</param>
/// <param name="PlacesTile">The tile kind placing this item makes, or <see langword="null"/> when it cannot be placed.</param>
public sealed record ItemDefinition(ItemId Id, string Name, int MaxStack, ToolKind Tool, TileId? PlacesTile)
{
    /// <summary>The stack limit of a material or a placeable block.</summary>
    public const int DefaultStackLimit = 99;

    /// <summary>The stack limit of a tool: one per slot.</summary>
    public const int ToolStackLimit = 1;

    private static readonly ItemDefinition[] Table =
    [
        new(ItemId.Wood, "wood", DefaultStackLimit, ToolKind.None, TileId.Planks),
        new(ItemId.Stone, "stone", DefaultStackLimit, ToolKind.None, TileId.Stone),
        new(ItemId.Dirt, "dirt", DefaultStackLimit, ToolKind.None, TileId.Dirt),
        new(ItemId.Sand, "sand", DefaultStackLimit, ToolKind.None, TileId.Sand),
        new(ItemId.Flower, "flower", DefaultStackLimit, ToolKind.None, TileId.Flower),
        new(ItemId.Mushroom, "mushroom", DefaultStackLimit, ToolKind.None, TileId.Mushroom),
        new(ItemId.Axe, "axe", ToolStackLimit, ToolKind.Axe, null),
        new(ItemId.Pickaxe, "pickaxe", ToolStackLimit, ToolKind.Pickaxe, null),
        new(ItemId.Door, "door", DefaultStackLimit, ToolKind.None, TileId.Door),
        new(ItemId.Table, "table", DefaultStackLimit, ToolKind.None, TileId.Table),
        new(ItemId.Chair, "chair", DefaultStackLimit, ToolKind.None, TileId.Chair),
        new(ItemId.Bed, "bed", DefaultStackLimit, ToolKind.None, TileId.Bed),
        new(ItemId.Chest, "chest", DefaultStackLimit, ToolKind.None, TileId.Chest),
        new(ItemId.Sword, "sword", ToolStackLimit, ToolKind.None, null),
        new(ItemId.Furnace, "furnace", DefaultStackLimit, ToolKind.None, TileId.Furnace),
        new(ItemId.Glass, "glass", DefaultStackLimit, ToolKind.None, TileId.Glass),
        new(ItemId.Brick, "brick", DefaultStackLimit, ToolKind.None, TileId.Brick),
        new(ItemId.Coal, "coal", DefaultStackLimit, ToolKind.None, null),
        new(ItemId.Copper, "copper", DefaultStackLimit, ToolKind.None, null),
        new(ItemId.Iron, "iron", DefaultStackLimit, ToolKind.None, null),
        new(ItemId.Gold, "gold", DefaultStackLimit, ToolKind.None, null),
        new(ItemId.Torch, "torch", DefaultStackLimit, ToolKind.None, TileId.Torch),
        new(ItemId.Lamp, "lamp", ToolStackLimit, ToolKind.None, null),
        new(ItemId.Bucket, "bucket", ToolStackLimit, ToolKind.None, null),
        new(ItemId.Ring, "ring", ToolStackLimit, ToolKind.None, null),
    ];

    /// <summary>Every item kind, ordered by id.</summary>
    public static IReadOnlyList<ItemDefinition> All => Table;

    /// <summary>The definition of <paramref name="id"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The id has no definition.</exception>
    public static ItemDefinition Get(ItemId id)
    {
        var index = (int)id;
        if ((uint)index >= (uint)Table.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "No item definition for this id.");
        }

        return Table[index];
    }
}

/// <summary>A number of one item kind in a slot. A stack with a count of zero is an empty slot.</summary>
/// <param name="Item">The kind (meaningless when <paramref name="Count"/> is zero).</param>
/// <param name="Count">How many.</param>
public readonly record struct ItemStack(ItemId Item, int Count)
{
    /// <summary>An empty slot.</summary>
    public static ItemStack Empty { get; } = new(ItemId.Wood, 0);

    /// <summary>Whether the slot holds nothing.</summary>
    public bool IsEmpty => Count == 0;
}
