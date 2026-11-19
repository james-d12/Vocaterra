using Vocaterra.Game.Core.Items;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// The shipped item catalogue (<c>content/game-v1/game/items.json</c>, read once) for the tests that build a bag or a simulation without game data, and the ids of the
/// items no code names by behaviour (I01: the enum keeps only the members code refers to, so a test names the others by id).
/// </summary>
internal static class TestItems
{
    private static readonly Lazy<IItemCatalogue> Shipped = new(() => ItemCatalogue.Parse(File.ReadAllText(Repo.Path("content", "game-v1", "game", "items.json"))));

    /// <summary>The bread (<c>pan</c>), a data row only.</summary>
    public const ItemId Bread = (ItemId)28;

    /// <summary>The apple (<c>manzana</c>), a data row only.</summary>
    public const ItemId Apple = (ItemId)31;

    /// <summary>The axe (<c>hacha</c>), a data row only.</summary>
    public const ItemId Axe = (ItemId)32;

    /// <summary>The roast apple, a data row only.</summary>
    public const ItemId RoastApple = (ItemId)33;

    /// <summary>The flour (<c>harina</c>), a data row only.</summary>
    public const ItemId Flour = (ItemId)34;

    /// <summary>The shipped catalogue.</summary>
    public static IItemCatalogue Catalogue => Shipped.Value;

    /// <summary>The shipped rows and some of the test's own, or the shipped rows with some replaced (a row of the same name takes the place of the shipped one).</summary>
    /// <param name="rows">The rows to add or replace.</param>
    public static IItemCatalogue With(params ItemDefinition[] rows)
    {
        var replaced = Catalogue.All.Select(d => rows.FirstOrDefault(r => r.Name == d.Name) ?? d);
        return ItemCatalogue.Create(replaced.Concat(rows.Where(r => Catalogue.Find(r.Name) is null)));
    }
}
