namespace Vocaterra.Game.Core.Items;

/// <summary>
/// The game's item kinds (I01): <c>game/items.json</c>, read once and handed to everything that needs an item's name, stack limit, tool or picture, so no code holds a table of its own.
/// A later source of items (a mod, a pack) is another implementation. The kinds are in catalogue order, the order of the file.
/// </summary>
public interface IItemCatalogue
{
    /// <summary>Every item kind, in catalogue order.</summary>
    IReadOnlyList<ItemDefinition> All { get; }

    /// <summary>The definition of <paramref name="id"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The id has no definition.</exception>
    ItemDefinition Get(ItemId id);

    /// <summary>The definition of <paramref name="id"/>, or <see langword="null"/> when there is none.</summary>
    /// <param name="id">The item's numeric id.</param>
    ItemDefinition? Find(ItemId id);

    /// <summary>The definition whose key is <paramref name="name"/> (compared exactly), or <see langword="null"/>.</summary>
    /// <param name="name">The item's stable key (<c>wood</c>, <c>camp-kit</c>).</param>
    ItemDefinition? Find(string name);

    /// <summary>The largest <see cref="ItemDefinition.Capacity"/> of any item (0 when no item is a bag): the bag adds at most this many slots, so the inventory's array is the base plus this (I03b).</summary>
    /// <remarks>A source that keeps no cache can leave the default, which reads <see cref="All"/>.</remarks>
    int LargestCapacity => All.Select(static d => d.Capacity ?? 0).DefaultIfEmpty(0).Max();
}
