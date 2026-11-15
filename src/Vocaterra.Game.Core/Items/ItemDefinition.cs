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
/// What the game knows about one item kind, one row of <c>game/items.json</c> (I01): its stable key, its numeric id, its stack limit, the picture it is drawn with, how the hand holds it,
/// its tags and, for a tool, its <see cref="ToolSpec"/>. The stack limit lives here and nowhere else. The rows are read once into an <see cref="IItemCatalogue"/>.
/// </summary>
/// <param name="Id">The item kind's numeric id (<see cref="ItemId"/>; a data-only item's is above the enum's members, from 100).</param>
/// <param name="Name">A stable lowercase English key; saves, the bundle's noun catalogue and every other file name items by it.</param>
/// <param name="MaxStack">How many of this item one inventory slot holds.</param>
/// <param name="Icon">The atlas key of the item's picture (the hotbar, the held model); a key the atlas has no art for draws the fallback tile.</param>
/// <param name="Held">How the hand shows the item (<see cref="ItemHeld"/>).</param>
/// <param name="Tags">What the item is, as an open list of words (<c>tool</c>, <c>food</c>, <c>material</c>, <c>container</c>): later rules ask for a tag, never for an id.</param>
/// <param name="Tool">The tool's spec, or <see langword="null"/> when the item is not a tool.</param>
public sealed record ItemDefinition(ItemId Id, string Name, int MaxStack, string Icon, string Held, IReadOnlyList<string> Tags, ToolSpec? Tool = null)
{
    /// <summary>The stack limit of a material or a placeable block.</summary>
    public const int DefaultStackLimit = 99;

    /// <summary>Whether the item carries <paramref name="tag"/>.</summary>
    /// <param name="tag">The tag, compared exactly.</param>
    public bool HasTag(string tag) => Tags.Contains(tag, StringComparer.Ordinal);
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
