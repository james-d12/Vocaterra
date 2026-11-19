using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Items;

/// <summary>
/// The item kinds that code refers to by behaviour: the log that becomes planks, the torch that is placed, the berry a bush drops, the bottle that fills, the book that
/// opens, the coin that pays, and the stake and the camp kit of the plot. Every other item is a row of <c>game/items.json</c> alone (I01), known by its
/// <see cref="ItemDefinition.Name"/> and its numeric id, and a value of this type that has no member is such an item (<c>(ItemId)100</c>): it works everywhere an item can.
/// The numeric values are saved in the digest and stay as they were; a value is never changed or reused. The loader (<see cref="ItemCatalogue"/>) checks each member against
/// the data row with the same id and name. Spanish names are not here; each kind's noun is the bundle's <c>game/nouns.json</c>, keyed by <see cref="ItemDefinition.Name"/>.
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

    /// <summary>The learner's book (S20, D-game-16): given at the start; using it (selected, right-click) opens the book.</summary>
    Book = 29,

    /// <summary>A coin (<c>moneda</c>, village-plan V03): what an order pays and what a purchase costs (<see cref="Npc.NpcEffectKind.Sell"/>).</summary>
    Coin = 30,

    /// <summary>The stake (<c>estaca</c>, Y10): used on the ground it marks the pending plot (<see cref="Session.VoxelSession.SetPendingPlot"/>); the mayor gives it (Y11). It places no block.</summary>
    Stake = 35,

    /// <summary>The camp kit (<c>equipo</c>, labelled <c>equipo de campamento</c>, Y11): a tent, a bedroll and a fire ring in one piece; the mayor gives it with the plot (<see cref="Npc.NpcEffectKind.SellPlot"/>), Y12 places it on the owned land.</summary>
    CampKit = 36,
}

/// <summary>
/// What a tool does when it is held (W02): it gathers the blocks of its <see cref="Class"/> <paramref name="Speed"/> times as fast, and yields
/// <paramref name="Yield"/> of each gather (recorded for later tools; the gather still gives the block's own drop count).
/// </summary>
/// <param name="Class">The material of the blocks the tool works on.</param>
/// <param name="Speed">How many times faster: a block takes its gather ticks divided by this (at least one tick). Hands are speed 1.</param>
/// <param name="Yield">How many of a gather's drop the tool gives; 1 for every tool so far.</param>
public sealed record ToolSpec(Material Class, int Speed, int Yield = 1);

/// <summary>
/// How an item wears out (I07, inventory-plan 3.12): the wear at which it is worn out, what wears it and how fast. The <paramref name="Source"/> names an <see cref="Wearing.IWearSource"/> (<c>use</c>, <c>time</c>, <c>damage</c>), so a new
/// cause of wear is a source class and a data value, never an edit of the others. A worn-out item is never lost: a tool works at bare hands' speed and gear gives no stats.
/// </summary>
/// <param name="Max">The wear at which the item is worn out (at least 1).</param>
/// <param name="Source">The id of the wear source that wears this item (<see cref="Wearing.WearSources"/>).</param>
/// <param name="Rate">The wear a source's unit adds: per gather for <c>use</c>, per in-game hour worn for <c>time</c>, per point of damage taken for <c>damage</c> (at least 1).</param>
public sealed record DurabilitySpec(int Max, string Source, int Rate = 1);

/// <summary>
/// What the game knows about one item kind, one row of <c>game/items.json</c> (I01): its stable key, its numeric id, its stack limit, the picture it is drawn with, how the hand holds it,
/// its tags and, for a tool, its <see cref="ToolSpec"/>; a piece of gear also has the slot it fits, the stats it gives and how it looks on the body (I03a). The stack limit lives here and nowhere else.
/// The rows are read once into an <see cref="IItemCatalogue"/>.
/// </summary>
/// <param name="Id">The item kind's numeric id (<see cref="ItemId"/>; a data-only item's is above the enum's members, from 100).</param>
/// <param name="Name">A stable lowercase English key; saves, the bundle's noun catalogue and every other file name items by it.</param>
/// <param name="MaxStack">How many of this item one inventory slot holds.</param>
/// <param name="Icon">The atlas key of the item's picture (the hotbar, the held model); a key the atlas has no art for draws the fallback tile.</param>
/// <param name="Held">How the hand shows the item (<see cref="ItemHeld"/>).</param>
/// <param name="Tags">What the item is, as an open list of words (<c>tool</c>, <c>food</c>, <c>material</c>, <c>container</c>): later rules ask for a tag, never for an id.</param>
/// <param name="Tool">The tool's spec, or <see langword="null"/> when the item is not a tool.</param>
/// <param name="Wear">The kind of slot the item is worn in (<c>head</c>, <c>body</c>, <c>legs</c>, <c>feet</c>, <c>accessory</c>, <c>offhand</c>, <c>back</c>: the <c>wear</c> of a slot of <c>game/equipment.json</c>), or <see langword="null"/> when it cannot be worn.</param>
/// <param name="Stats">What the item adds to the player's stat sheet while it is worn, by open stat key (<c>warmth</c> in tenths of a degree), or <see langword="null"/> for none. Sorted by key.</param>
/// <param name="Look">The colours the item gives the body parts it covers (drawn on the player by I09), or <see langword="null"/> when it changes nothing.</param>
/// <param name="Capacity">The bag slots a worn bag adds to the base of the inventory (I03b; only an item tagged <c>bag</c> and worn on the <c>back</c> has one), or <see langword="null"/> for an item that adds none.</param>
/// <param name="Durability">How the item wears out (I07), or <see langword="null"/> for an item that never does. An item with a durability stacks to 1.</param>
public sealed record ItemDefinition(
    ItemId Id,
    string Name,
    int MaxStack,
    string Icon,
    string Held,
    IReadOnlyList<string> Tags,
    ToolSpec? Tool = null,
    string? Wear = null,
    IReadOnlyDictionary<string, double>? Stats = null,
    ItemLook? Look = null,
    int? Capacity = null,
    DurabilitySpec? Durability = null)
{
    /// <summary>The stack limit of a material or a placeable block.</summary>
    public const int DefaultStackLimit = 99;

    /// <summary>The tag a bag carries (the item with a <see cref="Capacity"/>).</summary>
    public const string BagTag = "bag";

    /// <summary>The kind of slot a bag is worn in.</summary>
    public const string BagWear = "back";

    /// <summary>The largest wear at which an item may wear out (a sanity limit, so the wear and its digest stay small whole numbers).</summary>
    public const int MaxDurability = 100000;

    /// <summary>The most slots one bag may add (a sanity limit, so the inventory's array stays a few rows).</summary>
    public const int MaxCapacity = 100;

    /// <summary>Whether an item of this kind at <paramref name="wear"/> is worn out (I07): it has a durability and the wear has reached it. A worn-out item is never lost; it gives no stats and a tool works at bare hands' speed.</summary>
    /// <param name="wear">The stack's wear.</param>
    public bool IsWornOut(int wear) => Durability is { } durability && wear >= durability.Max;

    /// <summary>Whether the item carries <paramref name="tag"/>.</summary>
    /// <param name="tag">The tag, compared exactly.</param>
    public bool HasTag(string tag) => Tags.Contains(tag, StringComparer.Ordinal);
}

/// <summary>A number of one item kind in a slot. A stack with a count of zero is an empty slot.</summary>
/// <param name="Item">The kind (meaningless when <paramref name="Count"/> is zero).</param>
/// <param name="Count">How many.</param>
/// <param name="Wear">How worn the item is (I07): 0 for new, up to the item's <see cref="ItemDefinition.Durability"/> maximum, where it is worn out. Meaningful only for an item with a durability, which stacks to 1; it travels with the item through every move, and two stacks with different wear never merge (<see cref="CanMergeWith"/>).</param>
public readonly record struct ItemStack(ItemId Item, int Count, int Wear = 0)
{
    /// <summary>An empty slot.</summary>
    public static ItemStack Empty { get; } = new(ItemId.Wood, 0);

    /// <summary>Whether the slot holds nothing.</summary>
    public bool IsEmpty => Count == 0;

    /// <summary>Whether this stack and <paramref name="other"/> are of one item at one wear, so a move may put them into one slot (an empty stack merges with nothing).</summary>
    /// <param name="other">The other stack.</param>
    public bool CanMergeWith(ItemStack other) => !IsEmpty && !other.IsEmpty && Item == other.Item && Wear == other.Wear;

    /// <summary>The same item at the same wear with another count (what a split or a partial move leaves); an empty stack when <paramref name="count"/> is zero or less.</summary>
    /// <param name="count">The new count.</param>
    public ItemStack WithCount(int count) => count <= 0 ? Empty : this with { Count = count };

    /// <summary>The stack of a durable item at a wear: <paramref name="wear"/> clamped to 0 or more.</summary>
    /// <param name="wear">The wear.</param>
    public ItemStack WithWear(int wear) => this with { Wear = Math.Max(0, wear) };
}
