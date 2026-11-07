namespace Vocaterra.Game.Core.Items;

/// <summary>
/// The item kinds the player can hold in the 3D world: wood (from a trunk), a torch (crafted at the workbench), a berry (from a berry bush), and, since S6, a bottle (empty, then full of water) and bread (both given by NPCs);
/// since village-plan V03 the village's economy: a coin, an apple and the axe.
/// The numeric values are saved in later milestones (S10), so a value is never changed or reused: a kind the prototype retired (the 2D tools,
/// ores and furniture) keeps its number out of this enum. Spanish names are not here; each kind's noun is the bundle's <c>game/nouns.json</c>,
/// keyed by <see cref="ItemDefinition.Name"/>.
/// </summary>
public enum ItemId : ushort
{
    /// <summary>Wood cut from a trunk. Placed, it becomes planks.</summary>
    Wood = 0,

    /// <summary>A torch, crafted at the workbench and placeable; set at the camp's fire ring it is the prototype's light.</summary>
    Torch = 21,

    /// <summary>A berry gathered from a berry bush.</summary>
    Berry = 25,

    /// <summary>An empty bottle, handed over at the stall (S6); at the river it is filled and becomes <see cref="WaterBottle"/>.</summary>
    Bottle = 26,

    /// <summary>A bottle of water (<c>agua</c>): an empty <see cref="Bottle"/> filled at the river, or water the guide gives (S6).</summary>
    WaterBottle = 27,

    /// <summary>A bread (<c>pan</c>), handed over at the stall or by the villager (S6).</summary>
    Bread = 28,

    /// <summary>The learner's book (S20, D-game-16): given at the start; using it (selected, right-click) opens the book.</summary>
    Book = 29,

    /// <summary>A coin (<c>moneda</c>, village-plan V03): what an order pays and what a purchase costs (<see cref="Npc.NpcEffectKind.Sell"/>).</summary>
    Coin = 30,

    /// <summary>An apple (<c>manzana</c>, village-plan V03), which the farmer sells and gives.</summary>
    Apple = 31,

    /// <summary>The axe (<c>hacha</c>, village-plan V03), the smith's order: held, it gathers a trunk in a quarter of the ticks (<see cref="Simulation.VoxelSimulation.GatherTicksOf"/>).</summary>
    Axe = 32,

    /// <summary>A roast apple (<c>manzana</c>, labelled <c>manzana asada</c>, N07): an apple cooked at a lit fire, which feeds more and heals.</summary>
    RoastApple = 33,

    /// <summary>Flour (<c>harina</c>, W10): the bread's first ingredient, sold at the stall for a coin.</summary>
    Flour = 34,
}

/// <summary>
/// What the simulation knows about one item kind: its stable key and its stack limit. The stack limit lives here and nowhere else.
/// </summary>
/// <param name="Id">The item kind.</param>
/// <param name="Name">A stable lowercase English key; the bundle's noun catalogue names items by it.</param>
/// <param name="MaxStack">How many of this item one inventory slot holds.</param>
public sealed record ItemDefinition(ItemId Id, string Name, int MaxStack)
{
    /// <summary>The stack limit of a material or a placeable block.</summary>
    public const int DefaultStackLimit = 99;

    private static readonly ItemDefinition[] Table =
    [
        new(ItemId.Wood, "wood", DefaultStackLimit),
        new(ItemId.Torch, "torch", DefaultStackLimit),
        new(ItemId.Berry, "berry", DefaultStackLimit),
        new(ItemId.Bottle, "bottle", DefaultStackLimit),
        new(ItemId.WaterBottle, "water-bottle", DefaultStackLimit),
        new(ItemId.Bread, "bread", DefaultStackLimit),
        new(ItemId.Book, "book", 1),
        new(ItemId.Coin, "coin", DefaultStackLimit),
        new(ItemId.Apple, "apple", DefaultStackLimit),
        new(ItemId.Axe, "axe", 1),
        new(ItemId.RoastApple, "roast-apple", DefaultStackLimit),
        new(ItemId.Flour, "flour", DefaultStackLimit),
    ];

    /// <summary>Every item kind, ordered by id.</summary>
    public static IReadOnlyList<ItemDefinition> All => Table;

    /// <summary>The definition of <paramref name="id"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The id has no definition.</exception>
    public static ItemDefinition Get(ItemId id)
    {
        foreach (var definition in Table)
        {
            if (definition.Id == id)
            {
                return definition;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(id), id, "No item definition for this id.");
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
